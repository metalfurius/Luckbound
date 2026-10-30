using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Pacts;
using Luckbound.Core.Relics;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Venator;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using EntityId = Luckbound.Core.Identifiers.EntityId;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>Composite G04 tests for the committed runtime effects of support relics.</summary>
    public sealed class SupportRelicPassiveEquipmentRuntimeTests
    {
        private const int WaitFrameLimit = 600;
        private const string V6ActiveFixtureSha256 = "6814ec3b42d7e754622dac845339b53175ec79f95f6354e7a2fb57c3e03493f3";

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<RoomLifetimeScope> _rooms = new List<RoomLifetimeScope>();
        private RuntimeSession _session;
        private Func<ISaveStore> _previousStoreFactory;
        private Vector2 _previousGravity;
        private SimulationMode2D _previousPhysicsMode;
        private float _previousTimeScale;
        private bool _previousAudioPaused;

        [SetUp]
        public void SetUp()
        {
            _previousStoreFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            _previousGravity = Physics2D.gravity;
            _previousPhysicsMode = Physics2D.simulationMode;
            _previousTimeScale = Time.timeScale;
            _previousAudioPaused = AudioListener.pause;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            Time.timeScale = 1f;
            AudioListener.pause = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DisposeSession(_session);
            _session = null;
            if (ApplicationBootstrapper.Instance != null)
                Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            for (int index = _rooms.Count - 1; index >= 0; index--)
            {
                if (_rooms[index] != null) _rooms[index].Dispose();
            }
            _rooms.Clear();
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) Object.Destroy(_objects[index]);
            }
            _objects.Clear();
            yield return null;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = _previousStoreFactory;
            AudioListener.pause = _previousAudioPaused;
            Time.timeScale = _previousTimeScale;
            Physics2D.simulationMode = _previousPhysicsMode;
            Physics2D.gravity = _previousGravity;
        }

        [UnityTest]
        public IEnumerator GearAndLeafInBothRelicSlotsChangeEffectiveCostAndReadyTickWithPacts()
        {
            ExpeditionRunConfiguration run = CreateRun(VenatorSkillLoadout.ClassId,
                VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId);
            InventoryItemStack gear = CollectItem(run.Inventory, SupportRelicIds.ApprenticeGear);
            InventoryItemStack leaf = CollectItem(run.Inventory, SupportRelicIds.AlvariLeaf);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> first = null;
            yield return EquipRelic(session, gear.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => first = value);
            AssertCommitted(first);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> second = null;
            yield return EquipRelic(session, leaf.InstanceId, EquipmentSlot.RelicTwo,
                TransactionId.New(), value => second = value);
            AssertCommitted(second);

            SupportRelicModifiers committed = SupportRelicResolver.Resolve(
                session.App.ActiveRun.Inventory.RelicOne.ItemId,
                session.App.ActiveRun.Inventory.RelicTwo.ItemId);
            PactModifierTotals pacts = PactModifierTotals.Sum(new[] { PactIds.Fervor, PactIds.Silence });
            session.Motor.ConfigureCommittedModifiers(committed, pacts);
            session.Motor.SkillUses.SetCurrentEnergy(40);

            SkillDefinition skill = session.Skills.Get(VenatorSkillLoadout.PiercingArrowId);
            Assert.That(skill.EnergyCost, Is.EqualTo(8));
            Assert.That(skill.CooldownTicks, Is.EqualTo(240));
            session.Actor.Input.Press(PlayerAction.Skill1);

            SkillUseOutcome activated = default;
            long activatedAt = -1;
            for (int frame = 0; frame < WaitFrameLimit; frame++)
            {
                yield return new WaitForFixedUpdate();
                VenatorCombatResult result = session.Motor.LastVenatorCombatResult;
                if (result.SkillOutcome.Status != SkillUseStatus.Activated) continue;
                activated = result.SkillOutcome;
                activatedAt = session.Motor.SimulationTick;
                break;
            }
            session.Actor.Input.Release(PlayerAction.Skill1);

            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(10),
                "The effective cost is ceil(8 × (100 + 25 Fervor − 10 Leaf) / 100).");
            Assert.That(activated.ReadyAtTick, Is.EqualTo(activatedAt + 168),
                "The effective ready tick applies the summed 20% Silence and 10% Gear reductions.");
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(30));
            Assert.That(session.Motor.SkillUses.IsReady(skill.Id, activatedAt + 167), Is.False);
            Assert.That(session.Motor.SkillUses.IsReady(skill.Id, activatedAt + 168), Is.True);
        }

        [UnityTest]
        public IEnumerator VialChangesHealthPotionOnlyAndPreservesFullInterruptAndTonicRules()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack vial = CollectItem(run.Inventory, SupportRelicIds.BlessedVial);
            InventoryItemStack tonic = CollectItem(run.Inventory, PlayerMotor2D.EnergyTonicItemId);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> equipped = null;
            yield return EquipRelic(session, vial.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => equipped = value);
            AssertCommitted(equipped);
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(2));

            session.Actor.Input.Press(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();
            session.Actor.Input.Release(PlayerAction.Consumable);
            Assert.That(session.Motor.LastConsumableUseOutcome.Status,
                Is.EqualTo(PlayerConsumableUseStatus.RejectedAtFullResource));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(2),
                "A full-health rejection must not consume a charge.");

            DirectImpactOutcome initialImpact = ApplyDamage(session.Motor, 50m, stunTicks: 0);
            Assert.That(initialImpact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(session.Motor.CurrentHealth, Is.LessThan(session.Motor.CombatStats.MaximumHealth - 30));
            int healthBeforePotion = session.Motor.CurrentHealth;

            session.Actor.Input.Press(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();
            session.Actor.Input.Release(PlayerAction.Consumable);
            Assert.That(session.Motor.IsPreparingHealthPotion, Is.True);
            for (int frame = 0; frame < HealthPotionUseState.PreparationDurationTicks + 5 &&
                session.Motor.LastConsumableUseOutcome.Status != PlayerConsumableUseStatus.Consumed; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(session.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
            Assert.That(session.Motor.LastConsumableUseOutcome.HealthRestored, Is.EqualTo(30));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBeforePotion + 30));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(1));

            session.Actor.Input.Press(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();
            session.Actor.Input.Release(PlayerAction.Consumable);
            Assert.That(session.Motor.IsPreparingHealthPotion, Is.True);
            session.Actor.Input.Press(PlayerAction.Dodge);
            yield return new WaitForFixedUpdate();
            session.Actor.Input.Release(PlayerAction.Dodge);
            Assert.That(session.Motor.IsPreparingHealthPotion, Is.False);
            Assert.That(session.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Cancelled));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(1),
                "A real dodge input interrupts the potion without consuming its stack.");
            for (int frame = 0; frame < WaitFrameLimit && session.Motor.IsDodging; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(session.Motor.IsDodging, Is.False,
                "The real dodge must finish before the skill and tonic inputs begin.");

            int tonicIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, tonic.InstanceId);
            InventoryOperationOutcome quickSlot = session.App.ActiveRun.Inventory.SetQuickConsumable(
                tonicIndex, TransactionId.New());
            Assert.That(quickSlot.Succeeded, Is.True, quickSlot.Status.ToString());
            session.Motor.RefreshConsumableChargesFromInventory();
            int healthBeforeTonic = session.Motor.CurrentHealth;
            session.Motor.SkillUses.SetCurrentEnergy(20);

            SkillDefinition energyRecoveryAnchorSkill = session.Skills.Get(BellatorSkillLoadout.CircularCutId);
            int circularCutsBeforeAnchor = session.Motor.BellatorCircularCutActivationCount;
            session.Actor.Input.Press(PlayerAction.Skill2);
            SkillUseOutcome energyRecoveryAnchor = default;
            long energyRecoveryAnchorTick = -1;
            bool energyRecoveryAnchorActivated = false;
            for (int frame = 0; frame < WaitFrameLimit && !energyRecoveryAnchorActivated; frame++)
            {
                yield return new WaitForFixedUpdate();
                BellatorCombatResult result = session.Motor.LastBellatorCombatResult;
                if (result.SkillOutcome.Status != SkillUseStatus.Activated) continue;
                energyRecoveryAnchor = result.SkillOutcome;
                energyRecoveryAnchorTick = session.Motor.SimulationTick;
                energyRecoveryAnchorActivated = true;
            }
            session.Actor.Input.Release(PlayerAction.Skill2);

            Assert.That(energyRecoveryAnchorActivated, Is.True,
                "A real Circular Cut input must activate before measuring tonic energy.");
            Assert.That(energyRecoveryAnchor.SkillId, Is.EqualTo(energyRecoveryAnchorSkill.Id));
            Assert.That(energyRecoveryAnchor.EnergySpent, Is.GreaterThan(0));
            Assert.That(session.Motor.BellatorCircularCutActivationCount, Is.EqualTo(circularCutsBeforeAnchor + 1));
            Assert.That(session.Motor.IsPreparingBellatorSkill, Is.False);
            Assert.That(session.Motor.IsCharging, Is.False);

            int energyBeforeTonic = session.Motor.CurrentEnergy;
            Assert.That(energyBeforeTonic + EnergyTonicUseState.EnergyAmount,
                Is.LessThanOrEqualTo(session.Motor.MaximumEnergy),
                "The exact tonic gain must not be clamped at maximum energy.");
            session.Actor.Input.Press(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();
            long tonicPreparationStartedTick = session.Motor.SimulationTick;
            Assert.That(session.Motor.LastConsumableUseOutcome.Status,
                Is.EqualTo(PlayerConsumableUseStatus.Preparing));
            session.Actor.Input.Release(PlayerAction.Consumable);
            long tonicConsumedAtTick = -1;
            for (int frame = 0; frame < EnergyTonicUseState.PreparationDurationTicks + 5 &&
                session.Motor.LastConsumableUseOutcome.Status != PlayerConsumableUseStatus.Consumed; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (session.Motor.LastConsumableUseOutcome.Status == PlayerConsumableUseStatus.Consumed)
                    tonicConsumedAtTick = session.Motor.SimulationTick;
            }

            Assert.That(session.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
            Assert.That(session.Motor.LastConsumableUseOutcome.ItemId, Is.EqualTo(PlayerMotor2D.EnergyTonicItemId));
            Assert.That(session.Motor.LastConsumableUseOutcome.EnergyRestored, Is.EqualTo(25));
            Assert.That(tonicConsumedAtTick - tonicPreparationStartedTick,
                Is.EqualTo(EnergyTonicUseState.PreparationDurationTicks));
            Assert.That(tonicConsumedAtTick - energyRecoveryAnchorTick, Is.LessThan(60),
                "The tonic completes before the real skill's 60-tick Bellator recovery delay expires.");
            Assert.That(session.Motor.CurrentEnergy,
                Is.EqualTo(energyBeforeTonic + EnergyTonicUseState.EnergyAmount),
                "The Motor's normal recovery remains enabled; the measured tick is before its skill-reset delay.");
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBeforeTonic),
                "The Vial bonus does not alter the energy tonic or restore health.");
            Assert.That(session.Motor.EnergyTonicCharges, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CordAndBarkUseArmorAndPactsForRigidbodySpeedAndRealReceivedStun()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            EquipArmor(run.Inventory, ArmorCatalog.BarkArmorItemId);
            InventoryItemStack cord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            InventoryItemStack bark = CollectItem(run.Inventory, SupportRelicIds.SigiledBark);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> cordResult = null;
            yield return EquipRelic(session, cord.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => cordResult = value);
            AssertCommitted(cordResult);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> barkResult = null;
            yield return EquipRelic(session, bark.InstanceId, EquipmentSlot.RelicTwo,
                TransactionId.New(), value => barkResult = value);
            AssertCommitted(barkResult);

            SupportRelicModifiers support = SupportRelicResolver.Resolve(
                session.App.ActiveRun.Inventory.RelicOne.ItemId,
                session.App.ActiveRun.Inventory.RelicTwo.ItemId);
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });
            session.Motor.ConfigureCommittedModifiers(support, haste);
            ArmorProfile armor = session.Motor.EquippedArmorResolution.Armor;
            decimal expectedSpeed = SupportRelicActorRules.ResolveHorizontalSpeed(
                (decimal)PlayerMotor2D.MaximumHorizontalSpeed, armor.MovementSpeedPercentModifier, haste, support);
            Assert.That(session.Motor.EffectiveMaximumHorizontalSpeed,
                Is.EqualTo((float)expectedSpeed).Within(0.001f));

            session.Actor.Input.SetMoveX(1f);
            for (int frame = 0; frame < 24; frame++) yield return new WaitForFixedUpdate();
            Assert.That(session.Motor.Velocity.x, Is.EqualTo((float)expectedSpeed).Within(0.1f),
                "The configured maximum is reached through the actor's Rigidbody movement update.");

            DirectImpactOutcome impact = ApplyDamage(session.Motor, 1m, stunTicks: 60);
            Assert.That(impact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(session.Motor.IsStunned, Is.True);
            Assert.That(session.Motor.StunTicksRemaining, Is.EqualTo(36),
                "Bark Armor 15% plus Sigiled Bark 25% resolves a received 60-tick stun to 36.");
        }

        [UnityTest]
        public IEnumerator RepeatedCommittedSwapsPreserveActorAndSkillTimelineAndResources()
        {
            ExpeditionRunConfiguration run = CreateRun(VenatorSkillLoadout.ClassId,
                VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId);
            InventoryItemStack gear = CollectItem(run.Inventory, SupportRelicIds.ApprenticeGear);
            InventoryItemStack leaf = CollectItem(run.Inventory, SupportRelicIds.AlvariLeaf);
            InventoryItemStack cord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            InventoryItemStack bark = CollectItem(run.Inventory, SupportRelicIds.SigiledBark);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> gearResult = null;
            yield return EquipRelic(session, gear.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => gearResult = value);
            AssertCommitted(gearResult);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> leafResult = null;
            yield return EquipRelic(session, leaf.InstanceId, EquipmentSlot.RelicTwo,
                TransactionId.New(), value => leafResult = value);
            AssertCommitted(leafResult);

            Assert.That(ApplyDamage(session.Motor, 10m, stunTicks: 0).Status, Is.EqualTo(DirectImpactStatus.Applied));
            session.Motor.SkillUses.SetCurrentEnergy(30);
            SkillUseState skillUses = session.Motor.SkillUses;
            SkillDefinition firstSkill = session.Skills.Get(VenatorSkillLoadout.PiercingArrowId);
            SkillDefinition secondSkill = session.Skills.Get(VenatorSkillLoadout.TripleShotId);

            session.Actor.Input.Press(PlayerAction.Skill1);
            SkillUseOutcome activated = default;
            for (int frame = 0; frame < WaitFrameLimit; frame++)
            {
                yield return new WaitForFixedUpdate();
                VenatorCombatResult result = session.Motor.LastVenatorCombatResult;
                if (result.SkillOutcome.Status != SkillUseStatus.Activated) continue;
                activated = result.SkillOutcome;
                break;
            }
            session.Actor.Input.Release(PlayerAction.Skill1);
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.ReadyAtTick, Is.GreaterThan(session.Motor.SimulationTick));

            session.Actor.Input.Press(PlayerAction.Skill2);
            SkillUseOutcome preparing = default;
            for (int frame = 0; frame < WaitFrameLimit; frame++)
            {
                yield return new WaitForFixedUpdate();
                VenatorCombatResult result = session.Motor.LastVenatorCombatResult;
                if (result.SkillOutcome.Status != SkillUseStatus.Preparing) continue;
                preparing = result.SkillOutcome;
                break;
            }
            session.Actor.Input.Release(PlayerAction.Skill2);
            Assert.That(preparing.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(skillUses.IsPreparing, Is.True);
            Assert.That(skillUses.PreparingSkillId, Is.EqualTo(secondSkill.Id));
            Assert.That(skillUses.IsReady(firstSkill.Id, session.Motor.SimulationTick), Is.False);

            int healthBeforeSwaps = session.Motor.CurrentHealth;
            int energyBeforeSwaps = session.Motor.CurrentEnergy;
            int chargesBeforeSwaps = session.Motor.HealthPotionCharges;
            ActorId actorIdBefore = session.Motor.ActorId;
            long readyAtBefore = activated.ReadyAtTick;
            session.Pause.SetPaused(true);

            ExpeditionMutationCommitResult<InventoryOperationOutcome> swapCord = null;
            yield return EquipRelic(session, cord.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => swapCord = value);
            AssertCommitted(swapCord);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> swapBark = null;
            yield return EquipRelic(session, bark.InstanceId, EquipmentSlot.RelicTwo,
                TransactionId.New(), value => swapBark = value);
            AssertCommitted(swapBark);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> restoreGear = null;
            yield return EquipRelic(session, gear.InstanceId, EquipmentSlot.RelicOne,
                TransactionId.New(), value => restoreGear = value);
            AssertCommitted(restoreGear);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> restoreLeaf = null;
            yield return EquipRelic(session, leaf.InstanceId, EquipmentSlot.RelicTwo,
                TransactionId.New(), value => restoreLeaf = value);
            AssertCommitted(restoreLeaf);

            Assert.That(session.Motor.ActorId, Is.EqualTo(actorIdBefore));
            Assert.That(session.Motor.SkillUses, Is.SameAs(skillUses));
            Assert.That(session.Motor.SkillUses.IsPreparing, Is.True);
            Assert.That(session.Motor.SkillUses.PreparingSkillId, Is.EqualTo(secondSkill.Id));
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, session.Motor.SimulationTick), Is.False);
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, readyAtBefore - 1), Is.False);
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, readyAtBefore), Is.True);
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBeforeSwaps));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBeforeSwaps));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(chargesBeforeSwaps));
        }

        [UnityTest]
        public IEnumerator FailedWriteKeepsOldPassiveAndExactRetryPublishesOneCandidate()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack cord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            string inventoryBefore = JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot());
            string saveBefore = JsonUtility.ToJson(session.App.ActiveExpedition);
            float speedBefore = session.Motor.EffectiveMaximumHorizontalSpeed;
            int writesBefore = store.ProfileWriteAttempts;
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int backpackIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, cord.InstanceId);
            TransactionId transactionId = TransactionId.New();
            string operationKey = RelicOperationKey(room, backpackIndex, EquipmentSlot.RelicOne, transactionId);
            store.FailNextProfileWrite = true;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> failed = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, backpackIndex, EquipmentSlot.RelicOne, transactionId), value => failed = value);
            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True);
            Assert.That(failed.Outcome.Succeeded, Is.True);
            Assert.That(session.App.PendingEconomyOperationKey, Is.EqualTo(operationKey));
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(inventoryBefore));
            Assert.That(JsonUtility.ToJson(session.App.ActiveExpedition), Is.EqualTo(saveBefore));
            Assert.That(session.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(session.Motor.EffectiveMaximumHorizontalSpeed, Is.EqualTo(speedBefore));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(session.PublicationAttempts, Is.Zero);
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1));

            ExpeditionMutationCommitResult<InventoryOperationOutcome> retry = null;
            yield return Complete(session.App.RetryPendingEconomyMutationAsync<InventoryOperationOutcome>(operationKey),
                value => retry = value);
            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Outcome.Succeeded, Is.True);
            Assert.That(retry.Outcome.TransactionId, Is.EqualTo(transactionId));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.RouteCord));
            Assert.That(session.Collector.Inventory, Is.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.Motor.EffectiveMaximumHorizontalSpeed, Is.GreaterThan(speedBefore));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(session.PublicationAttempts, Is.EqualTo(1));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 2));
            Assert.That(store.LastSuccessfulProfileAttempt.Json,
                Is.EqualTo(store.LastFailedProfileAttempt.Json), "Retry must write the exact original candidate bytes.");
            Assert.That(store.LastSuccessfulProfileAttempt.LastKnownGoodPrimaryJson,
                Is.EqualTo(store.LastFailedProfileAttempt.LastKnownGoodPrimaryJson));
        }

        [UnityTest]
        public IEnumerator FailedActorPublicationRetriesWithoutASecondWrite()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack cord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            float speedBefore = session.Motor.EffectiveMaximumHorizontalSpeed;
            int healthBefore = session.Motor.CurrentHealth;
            int energyBefore = session.Motor.CurrentEnergy;
            int healthPotionChargesBefore = session.Motor.HealthPotionCharges;
            int energyTonicChargesBefore = session.Motor.EnergyTonicCharges;
            string inventorySnapshotBefore = JsonUtility.ToJson(run.Inventory.CreateSnapshot());
            string collectorSnapshotBefore = JsonUtility.ToJson(session.Collector.Inventory.CreateSnapshot());
            int writesBefore = store.ProfileWriteAttempts;
            int backpackIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, cord.InstanceId);
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            TransactionId transactionId = TransactionId.New();
            string operationKey = RelicOperationKey(room, backpackIndex, EquipmentSlot.RelicOne, transactionId);
            session.FailNextPublication = true;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> failed = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, backpackIndex, EquipmentSlot.RelicOne, transactionId), value => failed = value);
            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True);
            Assert.That(failed.Outcome.Succeeded, Is.True);
            Assert.That(session.App.HasPendingEconomyMutation, Is.True);
            Assert.That(session.App.PendingEconomyOperationKey, Is.EqualTo(operationKey));
            Assert.That(session.Pause.IsPaused, Is.False,
                "The pending simulation pause comes from the session lease, not manual pause input.");
            Assert.That(session.Pause.IsSimulationPaused, Is.True,
                "The pending live-actor publication retains its simulation pause lease until retry.");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(session.App.ActiveExpedition.inventorySnapshotJson,
                Is.Not.EqualTo(inventorySnapshotBefore),
                "The save is durable even while its live actor publication remains pending.");
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.RouteCord));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.InstanceId, Is.EqualTo(cord.InstanceId));
            string durableCandidateSnapshot = JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot());
            Assert.That(session.App.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(durableCandidateSnapshot),
                "The durable checkpoint must match the exact candidate now exposed as ActiveRun.");
            ProfileSaveEnvelope durableProfile = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimaryJson);
            Assert.That(durableProfile, Is.Not.Null);
            Assert.That(durableProfile.activeExpedition.inventorySnapshotJson,
                Is.EqualTo(durableCandidateSnapshot), "The successful write must contain that exact durable candidate.");
            Assert.That(store.LastSuccessfulProfileAttempt, Is.Not.Null);
            Assert.That(store.LastSuccessfulProfileAttempt.Json, Is.EqualTo(store.ProfilePrimaryJson));
            Assert.That(session.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(session.Collector.Inventory, Is.Not.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.Collector.Inventory.RelicOne, Is.Null);
            Assert.That(JsonUtility.ToJson(session.Collector.Inventory.CreateSnapshot()),
                Is.EqualTo(collectorSnapshotBefore), "The live collector's old inventory must remain byte-stable.");
            Assert.That(session.Motor.EffectiveMaximumHorizontalSpeed, Is.EqualTo(speedBefore));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBefore));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBefore));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(healthPotionChargesBefore));
            Assert.That(session.Motor.EnergyTonicCharges, Is.EqualTo(energyTonicChargesBefore));
            Assert.That(session.PublicationAttempts, Is.EqualTo(1));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1));

            ExpeditionMutationCommitResult<InventoryOperationOutcome> retry = null;
            yield return Complete(session.App.RetryPendingEconomyMutationAsync<InventoryOperationOutcome>(operationKey),
                value => retry = value);
            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(retry.Pending, Is.False);
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.RouteCord));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.InstanceId, Is.EqualTo(cord.InstanceId));
            Assert.That(session.App.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(durableCandidateSnapshot));
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()),
                Is.EqualTo(durableCandidateSnapshot));
            Assert.That(session.Motor.EffectiveMaximumHorizontalSpeed, Is.GreaterThan(speedBefore));
            Assert.That(session.Collector.Inventory, Is.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBefore));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBefore));
            Assert.That(session.Motor.HealthPotionCharges, Is.EqualTo(healthPotionChargesBefore));
            Assert.That(session.Motor.EnergyTonicCharges, Is.EqualTo(energyTonicChargesBefore));
            Assert.That(session.PublicationAttempts, Is.EqualTo(2));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(session.App.HasPendingEconomyMutation, Is.False);
            Assert.That(session.App.PendingEconomyOperationKey, Is.Empty);
            Assert.That(session.Pause.IsPaused, Is.False);
            Assert.That(session.Pause.IsSimulationPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1),
                "Retrying an already saved actor publication must not write the profile again.");
        }

        [UnityTest]
        public IEnumerator CheckpointRebindSlotsReplayAndReadonlyRepositoryPreserveTheContract()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            EquipArmor(run.Inventory, ArmorCatalog.BarkArmorItemId);
            InventoryItemStack cord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            InventoryItemStack duplicateCord = CollectItem(run.Inventory, SupportRelicIds.RouteCord);
            InventoryItemStack bark = CollectItem(run.Inventory, SupportRelicIds.SigiledBark);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int cordIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, cord.InstanceId);
            TransactionId cordTransaction = TransactionId.New();
            int writesBeforeCord = store.ProfileWriteAttempts;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> firstCord = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, cordIndex, EquipmentSlot.RelicOne, cordTransaction), value => firstCord = value);
            AssertCommitted(firstCord);
            int writesAfterCord = store.ProfileWriteAttempts;
            ExpeditionMutationCommitResult<InventoryOperationOutcome> replay = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, cordIndex, EquipmentSlot.RelicOne, cordTransaction), value => replay = value);
            AssertCommitted(replay);
            AssertSameOutcome(replay.Outcome, firstCord.Outcome);
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesAfterCord),
                "A completed transaction replay does not issue another profile write.");
            Assert.That(writesAfterCord, Is.GreaterThan(writesBeforeCord));

            string committedInventoryBeforeInvalidSlot = JsonUtility.ToJson(
                session.App.ActiveRun.Inventory.CreateSnapshot());
            int writesBeforeInvalidSlot = store.ProfileWriteAttempts;
            int barkIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bark.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> invalidSlot = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, barkIndex, EquipmentSlot.Armor, TransactionId.New()), value => invalidSlot = value);
            Assert.That(invalidSlot.Persisted, Is.False);
            Assert.That(invalidSlot.Pending, Is.False);
            Assert.That(invalidSlot.Outcome, Is.Null);
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()),
                Is.EqualTo(committedInventoryBeforeInvalidSlot));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeInvalidSlot));

            int duplicateIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, duplicateCord.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> duplicate = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, duplicateIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => duplicate = value);
            Assert.That(duplicate.Persisted, Is.True, duplicate.Message);
            Assert.That(duplicate.Outcome.Succeeded, Is.False);
            Assert.That(duplicate.Outcome.Status, Is.EqualTo(InventoryOperationStatus.RejectedDuplicateRelic));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.RouteCord));
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo, Is.Null,
                "A duplicate second copy cannot create a second support modifier.");

            barkIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bark.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> barkResult = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, barkIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => barkResult = value);
            AssertCommitted(barkResult);
            ExpeditionSaveData durableCheckpoint = session.App.ActiveExpedition;
            Assert.That(durableCheckpoint.inventorySnapshotJson,
                Is.EqualTo(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot())));

            ActorFixture rebound = CreateActor(session.App.ActiveRun, "G04 passive checkpoint rebind");
            ExpeditionPlayerRunBinder.BindFromCheckpoint(session.App.ActiveRun, durableCheckpoint,
                rebound.ActorId, rebound.Input, GroundLayers(), EnemyLayers(), 0, rebound.Motor,
                rebound.Collector, session.App.ActiveRun.Inventory.Catalog, rebound.Weapons, rebound.Skills,
                rebound.ProjectilePool, rebound.ProjectileScope, rebound.Room,
                armorCatalog: session.Armors);
            Assert.That(rebound.Motor.EffectiveMaximumHorizontalSpeed, Is.EqualTo(6.48f).Within(0.001f));
            DirectImpactOutcome reboundImpact = ApplyDamage(rebound.Motor, 1m, stunTicks: 60);
            Assert.That(reboundImpact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(rebound.Motor.StunTicksRemaining, Is.EqualTo(36),
                "Rebinding the durable inventory restores Cord and Bark with Bark Armor exactly once.");

            DisposeSession(session);
            _session = null;
            string v6Profile = ReadFrozenFixture(
                "Luckbound/Tests/Fixtures/Saves/V6/V6-P1-active-known-nontrivial.Frozen.envelope.json",
                V6ActiveFixtureSha256);
            var readOnlyStore = new RecordingSaveStore(profilePrimaryJson: v6Profile);
            ApplicationBootstrapper readOnlyApp = null;
            yield return StartApplication(readOnlyStore, value => readOnlyApp = value,
                expectedSceneName: "Menu");
            Assert.That(readOnlyApp.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(readOnlyApp.CanWriteProfile, Is.False);
            Assert.That(readOnlyApp.ActiveRun, Is.Null);
            Assert.That(readOnlyApp.ActiveExpedition, Is.Not.Null);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> readOnly = null;
            yield return Complete(readOnlyApp.EquipActiveExpeditionRelicAsync(
                RoomInstanceId.New(), 0, EquipmentSlot.RelicOne, TransactionId.New()), value => readOnly = value);
            Assert.That(readOnly.Persisted, Is.False);
            Assert.That(readOnly.Pending, Is.False);
            Assert.That(readOnly.Outcome, Is.Null);
            Assert.That(readOnly.Message, Is.Not.Empty);
            Assert.That(readOnlyStore.ProfileWriteAttempts, Is.Zero,
                "The current save repository's read-only policy rejects before inventory mutation or write.");
        }

        private IEnumerator StartBoundRun(RecordingSaveStore store, ExpeditionRunConfiguration run,
            Action<RuntimeSession> ready)
        {
            ApplicationBootstrapper app = null;
            yield return StartApplication(store, value => app = value);
            SaveWriteResult begin = null;
            yield return Complete(app.BeginExpeditionAsync(run), value => begin = value);
            Assert.That(begin, Is.Not.Null);
            Assert.That(begin.Succeeded, Is.True, begin.Message);
            ready(CreateRuntimeSession(app, run, app.ActiveExpedition));
        }

        private IEnumerator StartApplication(RecordingSaveStore store, Action<ApplicationBootstrapper> ready,
            string expectedSceneName = "Menu")
        {
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            if (ApplicationBootstrapper.Instance != null)
                Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady ||
                SceneManager.GetActiveScene().name != expectedSceneName); frame++)
                yield return null;
            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(expectedSceneName));
            ready(ApplicationBootstrapper.Instance);
        }

        private RuntimeSession CreateRuntimeSession(ApplicationBootstrapper app,
            ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint)
        {
            var session = new RuntimeSession
            {
                App = app,
                Run = run,
                Actor = CreateActor(run, "G04 support relic session player"),
                Armors = LoadArmorCatalog(run.Inventory.Catalog)
            };
            ExpeditionPlayerRunBinder.BindFromCheckpoint(run, checkpoint, session.Actor.ActorId,
                session.Actor.Input, GroundLayers(), EnemyLayers(), 0, session.Actor.Motor,
                session.Actor.Collector, run.Inventory.Catalog, session.Actor.Weapons, session.Actor.Skills,
                session.Actor.ProjectilePool, session.Actor.ProjectileScope, session.Actor.Room,
                armorCatalog: session.Armors);
            CreatePauseController(session);
            session.PauseBinding = app.BindExpeditionSimulationPauseLeaseProvider(session.Pause, () =>
            {
                IDisposable lease = session.Pause.AcquireSimulationPauseLease();
                return new CountingPauseLease(lease);
            });
            session.PublicationBinding = app.BindExpeditionSessionMutationPublication(session.Pause,
                (candidateRun, candidateSave) =>
                {
                    Assert.That(candidateRun, Is.Not.Null);
                    candidateSave.maximumHealth = session.Motor.CombatStats.MaximumHealth;
                    candidateSave.currentHealth = session.Motor.CurrentHealth;
                    candidateSave.maximumEnergy = session.Motor.MaximumEnergy;
                    candidateSave.currentEnergy = session.Motor.CurrentEnergy;
                },
                (candidateRun, candidateSave) =>
                {
                    session.PreparationCalls++;
                    ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared =
                        ExpeditionPlayerRunBinder.PrepareCommittedRunPublication(candidateRun, candidateSave,
                            session.Motor, session.Collector, session.Actor.Weapons, session.Armors);
                    prepared.ApplyToSave(candidateSave);
                    return () =>
                    {
                        session.PublicationAttempts++;
                        if (session.FailNextPublication)
                        {
                            session.FailNextPublication = false;
                            throw new InvalidOperationException("simulated G04 actor publication failure");
                        }
                        prepared.Publish();
                    };
                });
            return session;
        }

        private IEnumerator EquipRelic(RuntimeSession session, ItemInstanceId itemId,
            EquipmentSlot slot, TransactionId transactionId,
            Action<ExpeditionMutationCommitResult<InventoryOperationOutcome>> completed)
        {
            int index = FindBackpackIndex(session.App.ActiveRun.Inventory, itemId);
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                session.App.ActiveRoomInstanceId, index, slot, transactionId), completed);
        }

        private void CreatePauseController(RuntimeSession session)
        {
            session.PauseRoot = Track(new GameObject("G04 support relic pause owner"));
            var labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(session.PauseRoot.transform, false);
            session.Pause = session.PauseRoot.AddComponent<ExpeditionPauseController>();
            session.Pause.Initialize(labelObject.GetComponent<Text>(), pauseOnFocusLoss: false);
        }

        private ActorFixture CreateActor(ExpeditionRunConfiguration run, string name)
        {
            var actor = new ActorFixture { ActorId = ActorId.New() };
            actor.Input = new PlayerInputBuffer(actor.ActorId);
            actor.Weapons = LoadWeaponCatalog(run.Inventory.Catalog);
            actor.Skills = LoadSkillCatalog();
            GameObject player = Track(new GameObject(name));
            actor.Body = player.AddComponent<Rigidbody2D>();
            actor.Body.gravityScale = 0f;
            actor.Body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            actor.Body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            actor.Motor = player.AddComponent<PlayerMotor2D>();
            actor.Collector = player.AddComponent<ItemCollector2D>();
            if (run.ClassId != BellatorSkillLoadout.ClassId)
                CreateRoomProjectiles(actor, name);
            return actor;
        }

        private void CreateRoomProjectiles(ActorFixture actor, string name)
        {
            GameObject roomObject = Track(new GameObject(name + " room scope"));
            actor.Room = roomObject.AddComponent<RoomLifetimeScope>();
            actor.Room.Initialize(RoomInstanceId.New(), "room.g04.passive_relic_test");
            _rooms.Add(actor.Room);

            GameObject poolObject = Track(new GameObject(name + " projectile pool"));
            actor.ProjectilePool = poolObject.AddComponent<ProjectilePool2D>();
            GameObject prefab = Track(new GameObject(name + " projectile prefab"));
            prefab.AddComponent<Rigidbody2D>();
            prefab.AddComponent<CircleCollider2D>();
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            actor.ProjectilePool.Initialize(prefab);

            var scopeObject = new GameObject(name + " projectile scope");
            actor.ProjectileScope = scopeObject.AddComponent<RoomProjectileScope>();
            actor.ProjectileScope.Initialize(EntityId.New(), actor.ProjectilePool);
            actor.Room.OwnActor(scopeObject);
            actor.Room.OwnResource(actor.ProjectileScope);
        }

        private static ExpeditionRunConfiguration CreateRun(string classId, params string[] skills)
        {
            return ExpeditionRunConfigurationFactory.Create(classId,
                ExpeditionRunConfiguration.HumanRaceId, skills);
        }

        private static InventoryItemStack CollectItem(ExpeditionInventory inventory, string itemId)
        {
            var item = new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, 1);
            InventoryOperationOutcome outcome = inventory.TryCollectItem(item, TransactionId.New());
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Succeeded, Is.True, outcome.Status.ToString());
            return item;
        }

        private static void EquipArmor(ExpeditionInventory inventory, string itemId)
        {
            InventoryItemStack armor = CollectItem(inventory, itemId);
            int index = FindBackpackIndex(inventory, armor.InstanceId);
            InventoryOperationOutcome outcome = inventory.EquipBackpackItem(index, EquipmentSlot.Armor, TransactionId.New());
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Succeeded, Is.True, outcome.Status.ToString());
        }

        private static DirectImpactOutcome ApplyDamage(PlayerMotor2D motor, decimal damage, int stunTicks)
        {
            var request = new DamageCalculationRequest(damage, 1m, 1m, 0m, 0, 0m,
                motor.CombatStats, targetIsInvulnerable: false);
            return motor.ReceiveDirectImpact(HitId.New(), request, motor.SimulationTick,
                Vector2.zero, stunDurationTicks: stunTicks);
        }

        private static int FindBackpackIndex(ExpeditionInventory inventory, ItemInstanceId itemInstanceId)
        {
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                if (inventory.GetBackpackSlot(index)?.InstanceId == itemInstanceId) return index;
            Assert.Fail("Item instance " + itemInstanceId + " is not in the backpack.");
            return -1;
        }

        private static void AssertCommitted(ExpeditionMutationCommitResult<InventoryOperationOutcome> result)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Persisted, Is.True, result.Message);
            Assert.That(result.Pending, Is.False);
            Assert.That(result.Outcome, Is.Not.Null);
            Assert.That(result.Outcome.Succeeded, Is.True, result.Outcome.Status.ToString());
        }

        private static void AssertSameOutcome(InventoryOperationOutcome repeated,
            InventoryOperationOutcome original)
        {
            Assert.That(original, Is.Not.Null);
            Assert.That(repeated, Is.Not.Null);
            Assert.That(repeated.TransactionId, Is.EqualTo(original.TransactionId));
            Assert.That(repeated.Status, Is.EqualTo(original.Status));
            Assert.That(repeated.ItemId, Is.EqualTo(original.ItemId));
            Assert.That(repeated.WasReplay, Is.EqualTo(original.WasReplay));
            Assert.That(repeated.DisplacedItem?.InstanceId, Is.EqualTo(original.DisplacedItem?.InstanceId));
        }

        private static string RelicOperationKey(RoomInstanceId room, int backpackIndex,
            EquipmentSlot slot, TransactionId transactionId)
        {
            return "equip-relic:" + room + ":" + backpackIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)slot).ToString(CultureInfo.InvariantCulture) + ":" + transactionId;
        }

        private static string ReadFrozenFixture(string relativePath, string expectedSha256)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(ComputeSha256(bytes), Is.EqualTo(expectedSha256),
                "Frozen save fixture bytes changed: " + relativePath);
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static ItemCatalog LoadItemCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(source, Is.Not.Null, "The canonical Items.json resource must be available.");
            return ItemCatalogJsonLoader.Load(source.text);
        }

        private static WeaponCatalog LoadWeaponCatalog(ItemCatalog items)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(source, Is.Not.Null, "The canonical Weapons.json resource must be available.");
            return WeaponCatalogJsonLoader.Load(source.text, items);
        }

        private static SkillCatalog LoadSkillCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The canonical Skills.json resource must be available.");
            return SkillCatalogJsonLoader.Load(source.text);
        }

        private static ArmorCatalog LoadArmorCatalog(ItemCatalog items)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Armors");
            Assert.That(source, Is.Not.Null, "The canonical Armors.json resource must be available.");
            return ArmorCatalogJsonLoader.Load(source.text, items);
        }

        private static LayerMask GroundLayers()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground physics layer is required.");
            return 1 << layer;
        }

        private static LayerMask EnemyLayers()
        {
            int layer = LayerMask.NameToLayer("Enemies");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Enemies physics layer is required.");
            return 1 << layer;
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void DisposeSession(RuntimeSession session)
        {
            if (session == null) return;
            session.PublicationBinding?.Dispose();
            session.PauseBinding?.Dispose();
            if (session.Pause != null && session.Pause.IsPaused) session.Pause.SetPaused(false);
        }

        private static IEnumerator Complete<T>(Task<T> task,
            Action<T> completed)
        {
            Assert.That(task, Is.Not.Null, "A runtime operation must return a Task.");
            for (int frame = 0; frame < WaitFrameLimit && !task.IsCompleted; frame++) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Runtime operation exceeded " + WaitFrameLimit + " frames.");
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private sealed class ActorFixture
        {
            public ActorId ActorId;
            public PlayerInputBuffer Input;
            public PlayerMotor2D Motor;
            public Rigidbody2D Body;
            public ItemCollector2D Collector;
            public WeaponCatalog Weapons;
            public SkillCatalog Skills;
            public ProjectilePool2D ProjectilePool;
            public RoomProjectileScope ProjectileScope;
            public RoomLifetimeScope Room;
        }

        private sealed class RuntimeSession
        {
            public ApplicationBootstrapper App;
            public ExpeditionRunConfiguration Run;
            public ActorFixture Actor;
            public ArmorCatalog Armors;
            public GameObject PauseRoot;
            public ExpeditionPauseController Pause;
            public IDisposable PauseBinding;
            public IDisposable PublicationBinding;
            public int PreparationCalls;
            public int PublicationAttempts;
            public bool FailNextPublication;

            public PlayerMotor2D Motor => Actor.Motor;
            public ItemCollector2D Collector => Actor.Collector;
            public SkillCatalog Skills => Actor.Skills;
        }

        private sealed class CountingPauseLease : IDisposable
        {
            private IDisposable _lease;

            public CountingPauseLease(IDisposable lease) { _lease = lease; }

            public void Dispose()
            {
                IDisposable lease = _lease;
                _lease = null;
                lease?.Dispose();
            }
        }

        private sealed class RecordingSaveStore : ISaveStore
        {
            private string _profilePrimary;
            private string _profileBackup;
            private string _settingsPrimary;
            private string _settingsBackup;
            private readonly List<WriteAttempt> _attempts = new List<WriteAttempt>();

            public RecordingSaveStore(string profilePrimaryJson = null, string settingsPrimaryJson = null)
            {
                _profilePrimary = profilePrimaryJson;
                _settingsPrimary = settingsPrimaryJson;
            }

            public bool FailNextProfileWrite { get; set; }
            public int ProfileWriteAttempts { get; private set; }
            public string ProfilePrimaryJson => _profilePrimary;
            public WriteAttempt LastFailedProfileAttempt => _attempts.FindLast(attempt =>
                attempt.Document == SaveDocument.Profile && attempt.Failed);
            public WriteAttempt LastSuccessfulProfileAttempt => _attempts.FindLast(attempt =>
                attempt.Document == SaveDocument.Profile && !attempt.Failed);

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(_profilePrimary, _profileBackup)
                    : new SaveStorageSnapshot(_settingsPrimary, _settingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                var attempt = new WriteAttempt(document, json, lastKnownGoodPrimaryJson);
                _attempts.Add(attempt);
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    if (FailNextProfileWrite)
                    {
                        FailNextProfileWrite = false;
                        attempt.Failed = true;
                        throw new IOException("simulated profile storage failure");
                    }
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson))
                        _profileBackup = lastKnownGoodPrimaryJson;
                    _profilePrimary = json;
                    return Task.CompletedTask;
                }

                if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _settingsBackup = lastKnownGoodPrimaryJson;
                _settingsPrimary = json;
                return Task.CompletedTask;
            }
        }

        private sealed class WriteAttempt
        {
            public WriteAttempt(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Document = document;
                Json = json;
                LastKnownGoodPrimaryJson = lastKnownGoodPrimaryJson;
            }

            public SaveDocument Document { get; }
            public string Json { get; }
            public string LastKnownGoodPrimaryJson { get; }
            public bool Failed { get; set; }
        }
    }
}
