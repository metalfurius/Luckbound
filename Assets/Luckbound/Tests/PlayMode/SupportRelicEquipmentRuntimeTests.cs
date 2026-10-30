using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.App.Compatibility;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Relics;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
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
    public sealed class SupportRelicEquipmentRuntimeTests
    {
        private const int WaitFrameLimit = 600;
        private const string V5UnknownFixtureSha256 = "249bd5150c0e4fea0610644b18bf684b096ee1ba0a3f7db8e1ac86df41a3a2a3";
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
        public IEnumerator NewBindingsApplyKnownBonusesInBothOrdersAndIgnoreCombatRelicsAcrossAllClasses()
        {
            string[] classes =
            {
                BellatorSkillLoadout.ClassId,
                VenatorSkillLoadout.ClassId,
                MagusSkillLoadout.ClassId
            };
            string[][] skills =
            {
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId },
                new[] { VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId },
                new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId }
            };

            for (int classIndex = 0; classIndex < classes.Length; classIndex++)
            {
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    string firstId = scenario < 2 ? SupportRelicIds.PaleBone :
                        scenario == 2 ? SupportRelicIds.PaleBone : SupportRelicIds.ArcaneFlask;
                    string secondId = scenario < 2 ? SupportRelicIds.ArcaneFlask : CombatRelicIds.UmbraFang;
                    ExpeditionRunConfiguration run = CreateRun(classes[classIndex], skills[classIndex]);
                    InventoryItemStack first = CollectItem(run.Inventory, firstId);
                    InventoryItemStack second = CollectItem(run.Inventory, secondId);
                    bool reverseOrder = scenario == 1;
                    EquipBackpackItem(run.Inventory,
                        reverseOrder ? second.InstanceId : first.InstanceId, EquipmentSlot.RelicOne);
                    EquipBackpackItem(run.Inventory,
                        reverseOrder ? first.InstanceId : second.InstanceId, EquipmentSlot.RelicTwo);

                    ActorFixture actor = CreateActor(run, "Support relic bind " + classes[classIndex] + " " + scenario);
                    ArmorCatalog armors = LoadArmorCatalog(run.Inventory.Catalog);
                    ExpeditionPlayerRunBinder.BindFromCheckpoint(run, ExpeditionSaveMapper.Create(run),
                        actor.ActorId, actor.Input, GroundLayers(), EnemyLayers(), 0,
                        actor.Motor, actor.Collector, run.Inventory.Catalog, actor.Weapons, actor.Skills,
                        actor.ProjectilePool, actor.ProjectileScope, actor.Room, armorCatalog: armors);

                    ArmorResolution expected = ExpectedKnownArmor(run, armors);
                    Assert.That(actor.Motor.CombatStats.MaximumHealth, Is.EqualTo(expected.MaximumHealth),
                        classes[classIndex] + " health maxima, scenario " + scenario);
                    Assert.That(actor.Motor.MaximumEnergy, Is.EqualTo(expected.MaximumEnergy),
                        classes[classIndex] + " energy maxima, scenario " + scenario);
                    Assert.That(actor.Motor.CurrentHealth, Is.EqualTo(expected.HealthAfterMaximumClamp),
                        "Binding must leave starting health unfilled.");
                    Assert.That(actor.Motor.CurrentEnergy, Is.EqualTo(expected.EnergyAfterMaximumClamp),
                        "Binding must leave starting energy unfilled.");
                    Assert.That(actor.Collector.Inventory, Is.SameAs(run.Inventory));
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplicationRelicSwapsReplayAndMerchantSealNeverNeedAnUnequipOperation()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack bone = CollectItem(run.Inventory, SupportRelicIds.PaleBone);
            InventoryItemStack flask = CollectItem(run.Inventory, SupportRelicIds.ArcaneFlask);
            InventoryItemStack seal = CollectItem(run.Inventory, SupportRelicIds.MerchantSeal);
            EquipBackpackItem(run.Inventory, bone.InstanceId, EquipmentSlot.RelicOne);
            EquipBackpackItem(run.Inventory, flask.InstanceId, EquipmentSlot.RelicTwo);
            var store = new RecordingSaveStore();
            ApplicationBootstrapper app = null;
            yield return StartApplication(store, value => app = value);
            SaveWriteResult begin = null;
            yield return Complete(app.BeginExpeditionAsync(run), value => begin = value);
            Assert.That(begin, Is.Not.Null);
            Assert.That(begin.Succeeded, Is.True, begin.Message);

            ArmorCatalog armors = LoadArmorCatalog(run.Inventory.Catalog);
            ArmorResolution initialExpected = ExpectedKnownArmor(run, armors);
            Assert.That(initialExpected.MaximumHealth, Is.EqualTo(run.StartingHealth + 15));
            Assert.That(initialExpected.MaximumEnergy, Is.GreaterThan(run.StartingEnergy + 5));
            ExpeditionSaveData resourceFixture = app.ActiveExpedition.Clone();
            resourceFixture.maximumHealth = initialExpected.MaximumHealth;
            resourceFixture.currentHealth = checked(run.StartingHealth + 10);
            resourceFixture.maximumEnergy = initialExpected.MaximumEnergy;
            resourceFixture.currentEnergy = checked(run.StartingEnergy + 5);
            SaveWriteResult resourceSave = null;
            yield return Complete(app.SaveActiveExpeditionStateAsync(resourceFixture), value => resourceSave = value);
            Assert.That(resourceSave, Is.Not.Null);
            Assert.That(resourceSave.Succeeded, Is.True, resourceSave.Message);

            _session = CreateRuntimeSession(app, run, app.ActiveExpedition);
            RuntimeSession session = _session;
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(run.StartingHealth + 10));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy + 5));
            AssertKnownMaximums(session, initialExpected);
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int sealIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, seal.InstanceId);
            TransactionId sealTransaction = TransactionId.New();
            Assert.That(session.Motor.CurrentHealth, Is.GreaterThan(run.StartingHealth),
                "The saved checkpoint provides health above the base maximum before Pale Bone is removed.");

            ExpeditionMutationCommitResult<InventoryOperationOutcome> sealResult = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, sealIndex, EquipmentSlot.RelicOne, sealTransaction), value => sealResult = value);
            Assert.That(sealResult, Is.Not.Null, "The App equipment API must return a commit result.");
            Assert.That(sealResult.Persisted, Is.True, sealResult.Message);
            Assert.That(sealResult.Pending, Is.False);
            Assert.That(sealResult.Outcome, Is.Not.Null, "A persisted operation still needs its Core outcome.");
            Assert.That(sealResult.Outcome.Succeeded, Is.True, sealResult.Outcome.Status.ToString());
            Assert.That(sealResult.Outcome.DisplacedItem, Is.Not.Null);
            Assert.That(sealResult.Outcome.DisplacedItem.InstanceId, Is.EqualTo(bone.InstanceId));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.MerchantSeal));
            Assert.That(FindBackpackIndex(session.App.ActiveRun.Inventory, bone.InstanceId), Is.GreaterThanOrEqualTo(0));

            ArmorResolution withoutBone = ExpectedKnownArmor(session.App.ActiveRun, session.Armors);
            AssertKnownMaximums(session, withoutBone);
            Assert.That(withoutBone.MaximumHealth, Is.EqualTo(run.StartingHealth),
                "Removing Pale Bone reduces the maximum health back to the run's base maximum.");
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(run.StartingHealth),
                "Removing Pale Bone clamps current health to the reduced maximum.");
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy + 5),
                "Removing Pale Bone does not change current energy.");

            string inventoryAfterSeal = JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot());
            int healthAfterSeal = session.Motor.CurrentHealth;
            int energyAfterSeal = session.Motor.CurrentEnergy;
            int maximumHealthAfterSeal = session.Motor.CombatStats.MaximumHealth;
            int maximumEnergyAfterSeal = session.Motor.MaximumEnergy;
            int writesAfterSeal = store.ProfileWriteAttempts;
            ExpeditionMutationCommitResult<InventoryOperationOutcome> replay = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, sealIndex, EquipmentSlot.RelicOne, sealTransaction), value => replay = value);
            Assert.That(replay, Is.Not.Null);
            Assert.That(replay.Persisted, Is.True, replay.Message);
            Assert.That(replay.Pending, Is.False);
            Assert.That(replay.Outcome, Is.Not.Null);
            Assert.That(replay.Outcome.Succeeded, Is.True);
            AssertSameInventoryOutcome(replay.Outcome, sealResult.Outcome);
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(inventoryAfterSeal));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthAfterSeal));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyAfterSeal));
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(maximumHealthAfterSeal));
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(maximumEnergyAfterSeal));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesAfterSeal),
                "A transaction replay must not write or apply the displaced Pale Bone again.");

            int boneIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bone.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> restoredBone = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, boneIndex, EquipmentSlot.RelicOne, TransactionId.New()), value => restoredBone = value);
            Assert.That(restoredBone, Is.Not.Null);
            Assert.That(restoredBone.Persisted, Is.True, restoredBone.Message);
            Assert.That(restoredBone.Outcome, Is.Not.Null);
            Assert.That(restoredBone.Outcome.Succeeded, Is.True);
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(run.StartingHealth),
                "Re-equipping Pale Bone raises only the maximum and does not refill health.");
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(initialExpected.MaximumHealth));

            int secondSealIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, seal.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> secondSeal = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, secondSealIndex, EquipmentSlot.RelicOne, TransactionId.New()), value => secondSeal = value);
            Assert.That(secondSeal, Is.Not.Null);
            Assert.That(secondSeal.Persisted, Is.True, secondSeal.Message);
            Assert.That(secondSeal.Outcome, Is.Not.Null);
            Assert.That(secondSeal.Outcome.Succeeded, Is.True);

            int boneInSecondSlotIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bone.InstanceId);
            int energyMaximumWithoutFlask = checked(run.StartingEnergy +
                session.Armors.Get(run.Inventory.Armor.ItemId).MaximumEnergyBonus);
            Assert.That(session.Motor.CurrentEnergy, Is.GreaterThan(energyMaximumWithoutFlask),
                "The saved checkpoint provides energy above the armor-only maximum before Arcane Flask is removed.");
            ExpeditionMutationCommitResult<InventoryOperationOutcome> movedBone = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, boneInSecondSlotIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => movedBone = value);
            Assert.That(movedBone, Is.Not.Null);
            Assert.That(movedBone.Persisted, Is.True, movedBone.Message);
            Assert.That(movedBone.Outcome, Is.Not.Null);
            Assert.That(movedBone.Outcome.Succeeded, Is.True);
            Assert.That(movedBone.Outcome.DisplacedItem, Is.Not.Null);
            Assert.That(movedBone.Outcome.DisplacedItem.InstanceId, Is.EqualTo(flask.InstanceId));
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            ArmorResolution withoutFlask = ExpectedKnownArmor(session.App.ActiveRun, session.Armors);
            AssertKnownMaximums(session, withoutFlask);
            Assert.That(withoutFlask.MaximumEnergy, Is.EqualTo(energyMaximumWithoutFlask));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(withoutFlask.MaximumEnergy),
                "Removing Arcane Flask clamps current energy to the reduced maximum.");

            int flaskIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, flask.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> restoredFlask = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, flaskIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => restoredFlask = value);
            Assert.That(restoredFlask, Is.Not.Null);
            Assert.That(restoredFlask.Persisted, Is.True, restoredFlask.Message);
            Assert.That(restoredFlask.Outcome, Is.Not.Null);
            Assert.That(restoredFlask.Outcome.Succeeded, Is.True);
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.ArcaneFlask));
            ArmorResolution withFlask = ExpectedKnownArmor(session.App.ActiveRun, session.Armors);
            AssertKnownMaximums(session, withFlask);
            Assert.That(withFlask.MaximumEnergy, Is.EqualTo(initialExpected.MaximumEnergy));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(withoutFlask.MaximumEnergy),
                "Re-equipping Arcane Flask raises only maximum energy and does not refill it.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ArmorAndRelicCommitsPreserveActorSkillTimelineAndUnfilledResources()
        {
            ExpeditionRunConfiguration run = CreateRun(VenatorSkillLoadout.ClassId,
                VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId);
            EquipArmor(run.Inventory, "armor.bark_armor", ItemRarity.Rare, upgradeLevel: 1);
            InventoryItemStack bone = CollectItem(run.Inventory, SupportRelicIds.PaleBone);
            InventoryItemStack flask = CollectItem(run.Inventory, SupportRelicIds.ArcaneFlask);
            InventoryItemStack alternateArmor = CollectItem(run.Inventory, "armor.iron_cuirass");
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;

            SkillUseState skillState = session.Motor.SkillUses;
            SkillDefinition firstSkill = session.Skills.Get(VenatorSkillLoadout.PiercingArrowId);
            SkillDefinition secondSkill = session.Skills.Get(VenatorSkillLoadout.TripleShotId);
            session.Motor.SkillUses.SetCurrentEnergy(40);
            var damage = new DamageCalculationRequest(6m, 1m, 1m, 0m, 0, 0m,
                session.Motor.CombatStats, targetIsInvulnerable: false);
            session.Motor.ReceiveDirectImpact(HitId.New(), damage, impactTick: session.Motor.SimulationTick,
                knockbackVelocity: Vector2.zero, stunDurationTicks: 0);
            int healthAfterImpact = session.Motor.CurrentHealth;

            session.Actor.Input.Press(PlayerAction.Skill1);
            SkillUseOutcome activated = default;
            long activationTick = -1;
            for (int frame = 0; frame < WaitFrameLimit; frame++)
            {
                yield return new WaitForFixedUpdate();
                VenatorCombatResult result = session.Motor.LastVenatorCombatResult;
                Assert.That(result, Is.Not.Null, "The bound motor must publish its real Venator tick result.");
                activated = result.SkillOutcome;
                if (activated.Status == SkillUseStatus.Activated)
                {
                    activationTick = session.Motor.SimulationTick;
                    break;
                }
            }
            session.Actor.Input.Release(PlayerAction.Skill1);
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activationTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(activated.ReadyAtTick,
                Is.EqualTo(activationTick + firstSkill.CooldownTicks));

            session.Actor.Input.Press(PlayerAction.Skill2);
            SkillUseOutcome preparingSecond = default;
            long preparingTick = -1;
            for (int frame = 0; frame < WaitFrameLimit; frame++)
            {
                yield return new WaitForFixedUpdate();
                VenatorCombatResult result = session.Motor.LastVenatorCombatResult;
                Assert.That(result, Is.Not.Null, "The bound motor must publish its real Venator tick result.");
                preparingSecond = result.SkillOutcome;
                if (preparingSecond.Status == SkillUseStatus.Preparing && skillState.IsPreparing)
                {
                    preparingTick = session.Motor.SimulationTick;
                    break;
                }
            }
            session.Actor.Input.Release(PlayerAction.Skill2);
            Assert.That(preparingSecond.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(preparingTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(skillState.IsPreparing, Is.True);
            Assert.That(skillState.IsReady(firstSkill.Id, preparingTick), Is.False);
            Assert.That(preparingTick, Is.LessThan(activated.ReadyAtTick),
                "The first skill is still cooling down while the second skill prepares.");

            int healthBefore = session.Motor.CurrentHealth;
            int energyBefore = session.Motor.CurrentEnergy;
            Assert.That(healthBefore, Is.EqualTo(healthAfterImpact));
            ActorId actorIdBefore = session.Motor.ActorId;
            int writesBefore = store.ProfileWriteAttempts;

            session.Pause.SetPaused(true);
            Assert.That(session.Pause.IsPaused, Is.True);
            Assert.That(session.Pause.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int boneIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bone.InstanceId);
            TransactionId boneTransaction = TransactionId.New();
            ExpeditionMutationCommitResult<InventoryOperationOutcome> boneResult = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, boneIndex, EquipmentSlot.RelicOne, boneTransaction), value => boneResult = value);
            Assert.That(boneResult, Is.Not.Null);
            Assert.That(boneResult.Persisted, Is.True, boneResult.Message);
            Assert.That(boneResult.Outcome, Is.Not.Null);
            Assert.That(boneResult.Outcome.Succeeded, Is.True);

            int flaskIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, flask.InstanceId);
            TransactionId flaskTransaction = TransactionId.New();
            ExpeditionMutationCommitResult<InventoryOperationOutcome> flaskResult = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, flaskIndex, EquipmentSlot.RelicTwo, flaskTransaction), value => flaskResult = value);
            Assert.That(flaskResult, Is.Not.Null);
            Assert.That(flaskResult.Persisted, Is.True, flaskResult.Message);
            Assert.That(flaskResult.Outcome, Is.Not.Null);
            Assert.That(flaskResult.Outcome.Succeeded, Is.True);

            ArmorResolution expected = ExpectedKnownArmor(session.App.ActiveRun, session.Armors);
            AssertKnownMaximums(session, expected);
            Assert.That(session.Motor.ActorId, Is.EqualTo(actorIdBefore));
            Assert.That(session.Motor.SkillUses, Is.SameAs(skillState));
            Assert.That(session.Motor.SkillUses.IsPreparing, Is.True);
            Assert.That(session.Motor.SkillUses.PreparingSkillId, Is.EqualTo(secondSkill.Id));
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, session.Motor.SimulationTick), Is.False,
                "Equipment publication must retain the independent cooldown timeline.");
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBefore),
                "Maximum health bonuses do not heal the actor.");
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBefore),
                "Maximum energy bonuses do not refill the actor.");
            Assert.That(session.Motor.EquippedArmorInstanceId, Is.EqualTo(session.App.ActiveRun.Inventory.Armor.InstanceId));
            Assert.That(session.Motor.EquippedArmorResolution.Rarity, Is.EqualTo(ItemRarity.Rare));

            string inventoryAfterEquipment = JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot());
            int healthAfterEquipment = session.Motor.CurrentHealth;
            int energyAfterEquipment = session.Motor.CurrentEnergy;
            int writesAfterEquipment = store.ProfileWriteAttempts;
            ExpeditionMutationCommitResult<InventoryOperationOutcome> flaskReplay = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, flaskIndex, EquipmentSlot.RelicTwo, flaskTransaction), value => flaskReplay = value);
            Assert.That(flaskReplay, Is.Not.Null);
            Assert.That(flaskReplay.Persisted, Is.True, flaskReplay.Message);
            Assert.That(flaskReplay.Outcome, Is.Not.Null);
            AssertSameInventoryOutcome(flaskReplay.Outcome, flaskResult.Outcome);
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(inventoryAfterEquipment));
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.ArcaneFlask));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthAfterEquipment));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyAfterEquipment));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesAfterEquipment));
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(expected.MaximumHealth));
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(expected.MaximumEnergy));
            Assert.That(writesAfterEquipment, Is.GreaterThan(writesBefore));

            ExpeditionInventory committedInventory = session.App.ActiveRun.Inventory;
            ExpeditionInventory candidateInventory = ExpeditionInventory.Restore(
                committedInventory.Catalog, committedInventory.CreateSnapshot());
            int alternateArmorIndex = FindBackpackIndex(candidateInventory, alternateArmor.InstanceId);
            InventoryOperationOutcome armorOutcome = candidateInventory.EquipBackpackItem(
                alternateArmorIndex, EquipmentSlot.Armor, TransactionId.New());
            Assert.That(armorOutcome, Is.Not.Null);
            Assert.That(armorOutcome.Succeeded, Is.True, armorOutcome.Status.ToString());
            Assert.That(candidateInventory.Armor.InstanceId, Is.EqualTo(alternateArmor.InstanceId));

            ExpeditionRunConfiguration candidateRun = session.App.ActiveRun.WithInventory(candidateInventory);
            ExpeditionSaveData candidateSave = session.App.ActiveExpedition.Clone();
            candidateSave.maximumHealth = session.Motor.CombatStats.MaximumHealth;
            candidateSave.currentHealth = session.Motor.CurrentHealth;
            candidateSave.maximumEnergy = session.Motor.MaximumEnergy;
            candidateSave.currentEnergy = session.Motor.CurrentEnergy;
            ArmorResolution oldArmorResolution = session.Motor.EquippedArmorResolution;
            int baseDefense = session.Motor.CombatStats.Defense - oldArmorResolution.Defense;
            ArmorResolution expectedAlternateArmor = ExpectedKnownArmor(candidateRun, session.Armors);
            ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared =
                ExpeditionPlayerRunBinder.PrepareCommittedRunPublication(candidateRun, candidateSave,
                    session.Motor, session.Collector, session.Actor.Weapons, session.Armors);
            prepared.ApplyToSave(candidateSave);
            Assert.That(candidateSave.inventorySnapshotJson,
                Is.EqualTo(JsonUtility.ToJson(candidateInventory.CreateSnapshot())));
            Assert.That(candidateSave.maximumHealth, Is.EqualTo(expectedAlternateArmor.MaximumHealth));
            Assert.That(candidateSave.maximumEnergy, Is.EqualTo(expectedAlternateArmor.MaximumEnergy));
            Assert.That(session.Motor.EquippedArmorInstanceId, Is.EqualTo(session.App.ActiveRun.Inventory.Armor.InstanceId),
                "Preparing a changed armor candidate must not publish it before the explicit commit.");
            Assert.That(session.Collector.Inventory, Is.SameAs(committedInventory));
            Assert.That(session.Motor.SkillUses, Is.SameAs(skillState));
            Assert.That(session.Motor.SkillUses.IsPreparing, Is.True);
            Assert.That(session.Motor.SkillUses.PreparingSkillId, Is.EqualTo(secondSkill.Id));
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, session.Motor.SimulationTick), Is.False);
            int healthBeforeArmorPublish = session.Motor.CurrentHealth;
            int energyBeforeArmorPublish = session.Motor.CurrentEnergy;
            int writesBeforeArmorPublish = store.ProfileWriteAttempts;

            prepared.Publish();

            Assert.That(session.Motor.EquippedArmorInstanceId, Is.EqualTo(alternateArmor.InstanceId));
            Assert.That(session.Motor.EquippedArmorResolution.Armor.ItemId, Is.EqualTo("armor.iron_cuirass"));
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(expectedAlternateArmor.MaximumHealth));
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(expectedAlternateArmor.MaximumEnergy));
            Assert.That(session.Motor.CombatStats.Defense,
                Is.EqualTo(checked(baseDefense + expectedAlternateArmor.Defense)));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBeforeArmorPublish),
                "Publishing different armor does not heal the actor.");
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBeforeArmorPublish),
                "Publishing different armor does not refill energy.");
            Assert.That(session.Collector.Inventory, Is.SameAs(candidateInventory));
            Assert.That(session.App.ActiveRun.Inventory, Is.SameAs(committedInventory),
                "The direct binder candidate is isolated from the App's committed run.");
            Assert.That(session.Motor.SkillUses, Is.SameAs(skillState));
            Assert.That(session.Motor.SkillUses.IsPreparing, Is.True);
            Assert.That(session.Motor.SkillUses.PreparingSkillId, Is.EqualTo(secondSkill.Id));
            Assert.That(session.Motor.SkillUses.IsReady(firstSkill.Id, session.Motor.SimulationTick), Is.False);
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeArmorPublish),
                "Preparing and publishing a binder candidate does not claim a profile save.");
        }

        [UnityTest]
        public IEnumerator FailedWriteKeepsOldActorAndRetriesTheExactCandidateAndLease()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack bone = CollectItem(run.Inventory, SupportRelicIds.PaleBone);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            string savedInventoryBefore = session.App.ActiveExpedition.inventorySnapshotJson;
            string activeInventoryBefore = JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot());
            int healthBefore = session.Motor.CurrentHealth;
            int energyBefore = session.Motor.CurrentEnergy;
            int writesBefore = store.ProfileWriteAttempts;
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int backpackIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, bone.InstanceId);
            TransactionId transactionId = TransactionId.New();
            string operationKey = RelicOperationKey(room, backpackIndex, EquipmentSlot.RelicOne, transactionId);
            store.FailNextProfileWrite = true;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> failed = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, backpackIndex, EquipmentSlot.RelicOne, transactionId), value => failed = value);
            Assert.That(failed, Is.Not.Null);
            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True, failed.Message);
            Assert.That(failed.Outcome, Is.Not.Null);
            Assert.That(failed.Outcome.Succeeded, Is.True,
                "Durability and the inventory operation outcome are separate facts.");
            Assert.That(session.App.PendingEconomyOperationKey, Is.EqualTo(operationKey));
            Assert.That(session.App.HasPendingExpeditionWrite, Is.True);
            Assert.That(session.App.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(savedInventoryBefore));
            Assert.That(JsonUtility.ToJson(session.App.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(activeInventoryBefore));
            Assert.That(session.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBefore));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBefore));
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(session.Pause.IsPaused, Is.False);
            Assert.That(session.Pause.IsSimulationPaused, Is.True);
            Assert.That(session.LeasesAcquired, Is.EqualTo(1));
            Assert.That(session.LeasesReleased, Is.Zero);
            Assert.That(session.CaptureCalls, Is.EqualTo(1));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(session.PublicationAttempts, Is.Zero);
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1));
            Assert.That(store.LastFailedProfileAttempt, Is.Not.Null);

            ExpeditionMutationCommitResult<InventoryOperationOutcome> retry = null;
            yield return Complete(session.App.RetryPendingEconomyMutationAsync<InventoryOperationOutcome>(operationKey),
                value => retry = value);
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Outcome, Is.Not.Null);
            Assert.That(retry.Outcome.Succeeded, Is.True);
            Assert.That(session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            Assert.That(session.Collector.Inventory, Is.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth + 15));
            Assert.That(session.CaptureCalls, Is.EqualTo(1), "Retry must reuse the original captured actor state.");
            Assert.That(session.PreparationCalls, Is.EqualTo(1), "Retry must reuse the prepared candidate.");
            Assert.That(session.PublicationAttempts, Is.EqualTo(1));
            Assert.That(session.LeasesAcquired, Is.EqualTo(1));
            Assert.That(session.LeasesReleased, Is.EqualTo(1));
            Assert.That(session.Pause.IsSimulationPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 2));
            WriteAttempt successfulRetry = store.LastSuccessfulProfileAttempt;
            Assert.That(successfulRetry, Is.Not.Null);
            Assert.That(successfulRetry.Json, Is.EqualTo(store.LastFailedProfileAttempt.Json),
                "The retry must persist byte-for-byte the pending candidate.");
            Assert.That(successfulRetry.LastKnownGoodPrimaryJson,
                Is.EqualTo(store.LastFailedProfileAttempt.LastKnownGoodPrimaryJson));
        }

        [UnityTest]
        public IEnumerator DurableSaveWithFailedPublicationRetriesWithoutAnotherWriteAndPreservesManualPause()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack flask = CollectItem(run.Inventory, SupportRelicIds.ArcaneFlask);
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            int writesBefore = store.ProfileWriteAttempts;
            int currentEnergyBefore = session.Motor.CurrentEnergy;
            int maximumEnergyBefore = session.Motor.MaximumEnergy;
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int backpackIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, flask.InstanceId);
            TransactionId transactionId = TransactionId.New();
            string operationKey = RelicOperationKey(room, backpackIndex, EquipmentSlot.RelicTwo, transactionId);
            session.Pause.SetPaused(true);
            session.FailNextPublication = true;

            ExpeditionMutationCommitResult<InventoryOperationOutcome> failedPublication = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, backpackIndex, EquipmentSlot.RelicTwo, transactionId), value => failedPublication = value);
            Assert.That(failedPublication, Is.Not.Null);
            Assert.That(failedPublication.Persisted, Is.False,
                "The operation is not complete until its saved equipment reaches the live actor.");
            Assert.That(failedPublication.Pending, Is.True, failedPublication.Message);
            Assert.That(failedPublication.Outcome, Is.Not.Null);
            Assert.That(failedPublication.Outcome.Succeeded, Is.True);
            Assert.That(session.App.ActiveExpedition.maximumEnergy, Is.EqualTo(maximumEnergyBefore + 10),
                "The candidate save is durable even while actor publication is pending.");
            Assert.That(session.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(maximumEnergyBefore));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(currentEnergyBefore));
            Assert.That(session.Pause.IsPaused, Is.True);
            Assert.That(session.Pause.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(session.LeasesAcquired, Is.EqualTo(1));
            Assert.That(session.LeasesReleased, Is.Zero);
            Assert.That(session.PublicationAttempts, Is.EqualTo(1));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1));
            ProfileSaveEnvelope durable = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimaryJson);
            Assert.That(durable, Is.Not.Null);
            Assert.That(durable.activeExpedition.maximumEnergy, Is.EqualTo(maximumEnergyBefore + 10));

            ExpeditionMutationCommitResult<InventoryOperationOutcome> retry = null;
            yield return Complete(session.App.RetryPendingEconomyMutationAsync<InventoryOperationOutcome>(operationKey),
                value => retry = value);
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Outcome, Is.Not.Null);
            Assert.That(retry.Outcome.Succeeded, Is.True);
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(maximumEnergyBefore + 10));
            Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(currentEnergyBefore));
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.ArcaneFlask));
            Assert.That(session.Collector.Inventory, Is.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.PublicationAttempts, Is.EqualTo(2));
            Assert.That(session.CaptureCalls, Is.EqualTo(1));
            Assert.That(session.PreparationCalls, Is.EqualTo(1));
            Assert.That(session.LeasesReleased, Is.EqualTo(1));
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore + 1),
                "A publication retry must not issue a second profile write.");
            Assert.That(session.Pause.IsPaused, Is.True,
                "The session lease must not change the independent manual pause state.");
            Assert.That(session.Pause.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            session.Pause.SetPaused(false);
            Assert.That(session.Pause.IsSimulationPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator CheckpointRoomRebindAndUnknownLegacyMaximumGuardUseTheExactPersistedState()
        {
            ExpeditionRunConfiguration knownRun = CreateRun(VenatorSkillLoadout.ClassId,
                VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId);
            InventoryItemStack knownBone = CollectItem(knownRun.Inventory, SupportRelicIds.PaleBone);
            InventoryItemStack knownFlask = CollectItem(knownRun.Inventory, SupportRelicIds.ArcaneFlask);
            EquipBackpackItem(knownRun.Inventory, knownBone.InstanceId, EquipmentSlot.RelicOne);
            EquipBackpackItem(knownRun.Inventory, knownFlask.InstanceId, EquipmentSlot.RelicTwo);
            var knownStore = new RecordingSaveStore();
            ApplicationBootstrapper knownApp = null;
            yield return StartApplication(knownStore, value => knownApp = value);
            SaveWriteResult begin = null;
            yield return Complete(knownApp.BeginExpeditionAsync(knownRun), value => begin = value);
            Assert.That(begin, Is.Not.Null);
            Assert.That(begin.Succeeded, Is.True, begin.Message);
            _session = CreateRuntimeSession(knownApp, knownRun, knownApp.ActiveExpedition);
            ArmorResolution knownExpected = ExpectedKnownArmor(knownRun, _session.Armors);
            AssertKnownMaximums(_session, knownExpected);
            Assert.That(_session.App.ActiveExpedition.maximumHealth, Is.EqualTo(knownExpected.MaximumHealth));
            Assert.That(_session.App.ActiveExpedition.maximumEnergy, Is.EqualTo(knownExpected.MaximumEnergy));

            ExpeditionSaveData checkpoint = knownApp.ActiveExpedition;
            ExpeditionRoomCheckpointSaveData entry = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(
                checkpoint.roomEntryCheckpointJson);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.maximumHealth, Is.EqualTo(knownExpected.MaximumHealth));
            Assert.That(entry.maximumEnergy, Is.EqualTo(knownExpected.MaximumEnergy));
            ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(checkpoint, out ExpeditionSaveData resumed);
            ActorFixture rebound = CreateActor(restored, "Support relic checkpoint room rebind");
            BindActorFromCheckpoint(restored, resumed, rebound);
            Assert.That(rebound.Motor.CombatStats.MaximumHealth, Is.EqualTo(checkpoint.maximumHealth),
                "A restored bind must keep both saved relic bonuses without adding them again.");
            Assert.That(rebound.Motor.MaximumEnergy, Is.EqualTo(checkpoint.maximumEnergy));
            Assert.That(rebound.Motor.CurrentHealth, Is.EqualTo(resumed.currentHealth));
            Assert.That(rebound.Motor.CurrentEnergy, Is.EqualTo(resumed.currentEnergy));
            Assert.That(rebound.Collector.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            Assert.That(rebound.Collector.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.ArcaneFlask));

            DisposeSession(_session);
            _session = null;
            Object.Destroy(knownApp.gameObject);
            yield return null;

            string unknownLegacyProfile = CreateUnknownLegacyProfileWithEquippedPaleBone();
            var unknownStore = new RecordingSaveStore(profilePrimaryJson: unknownLegacyProfile);
            ApplicationBootstrapper unknownApp = null;
            yield return StartApplication(unknownStore, value => unknownApp = value, expectedSceneName: "Expedition");
            Assert.That(unknownApp.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(unknownApp.ActiveRun, Is.Not.Null);
            Assert.That(unknownApp.ActiveRun.HasKnownStartingLoadout, Is.False);
            Assert.That(unknownApp.ActiveExpedition.maximumHealth, Is.EqualTo(115));
            _session = CreateRuntimeSession(unknownApp, unknownApp.ActiveRun, unknownApp.ActiveExpedition);
            Assert.That(_session.Motor.CombatStats.MaximumHealth, Is.EqualTo(unknownApp.ActiveExpedition.maximumHealth),
                "An unknown historical loadout binds its saved maximum without consulting current upgrades.");
            Assert.That(_session.Motor.CurrentHealth, Is.EqualTo(unknownApp.ActiveExpedition.currentHealth));
            Assert.That(_session.App.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            int writesBeforeAmbiguousChange = unknownStore.ProfileWriteAttempts;
            int sealIndex = FindBackpackIndexByItemId(unknownApp.ActiveRun.Inventory, SupportRelicIds.MerchantSeal);
            RoomInstanceId unknownRoom = unknownApp.ActiveRoomInstanceId;
            ExpeditionMutationCommitResult<InventoryOperationOutcome> ambiguous = null;
            yield return Complete(unknownApp.EquipActiveExpeditionRelicAsync(
                unknownRoom, sealIndex, EquipmentSlot.RelicOne, TransactionId.New()), value => ambiguous = value);
            Assert.That(ambiguous, Is.Not.Null);
            Assert.That(ambiguous.Persisted, Is.False);
            Assert.That(ambiguous.Pending, Is.False, ambiguous.Message);
            Assert.That(ambiguous.Outcome, Is.Not.Null);
            Assert.That(ambiguous.Outcome.Succeeded, Is.True,
                "The candidate Core operation may succeed while the App rejects an unknowable maximum change.");
            Assert.That(ambiguous.Message, Does.Contain("relic maximum bonus"));
            Assert.That(unknownStore.ProfileWriteAttempts, Is.EqualTo(writesBeforeAmbiguousChange),
                "The unknown-base guard must reject before SaveRepository writes.");
            Assert.That(unknownApp.ActiveExpedition.maximumHealth, Is.EqualTo(115));
            Assert.That(unknownApp.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.PaleBone));
            Assert.That(_session.Motor.CombatStats.MaximumHealth, Is.EqualTo(115));
            Assert.That(_session.Collector.Inventory, Is.SameAs(unknownApp.ActiveRun.Inventory));
            Assert.That(unknownApp.HasPendingExpeditionWrite, Is.False);
            Assert.That(unknownApp.HasPendingEconomyMutation, Is.False);
            Assert.That(_session.LeasesAcquired, Is.EqualTo(1));
            Assert.That(_session.LeasesReleased, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DuplicateWrongItemSlotKeyConflictAndV6ReadonlyRejectWithoutFalseSuccess()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            InventoryItemStack firstBone = CollectItem(run.Inventory, SupportRelicIds.PaleBone);
            InventoryItemStack secondBone = CollectItem(run.Inventory, SupportRelicIds.PaleBone);
            InventoryItemStack flask = CollectItem(run.Inventory, SupportRelicIds.ArcaneFlask);
            InventoryItemStack spareArmor = CollectItem(run.Inventory, "armor.bellator.guild_cuirass");
            var store = new RecordingSaveStore();
            yield return StartBoundRun(store, run, value => _session = value);
            RuntimeSession session = _session;
            RoomInstanceId room = session.App.ActiveRoomInstanceId;
            int firstBoneIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, firstBone.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> first = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, firstBoneIndex, EquipmentSlot.RelicOne, TransactionId.New()), value => first = value);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Persisted, Is.True, first.Message);
            Assert.That(first.Outcome, Is.Not.Null);
            Assert.That(first.Outcome.Succeeded, Is.True);

            int duplicateIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, secondBone.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> duplicate = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, duplicateIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => duplicate = value);
            Assert.That(duplicate, Is.Not.Null);
            Assert.That(duplicate.Persisted, Is.True, duplicate.Message);
            Assert.That(duplicate.Outcome, Is.Not.Null);
            Assert.That(duplicate.Outcome.Succeeded, Is.False,
                "Persisting an idempotent rejection receipt does not mean the equipment operation succeeded.");
            Assert.That(duplicate.Outcome.Status, Is.EqualTo(InventoryOperationStatus.RejectedDuplicateRelic));

            int armorIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, spareArmor.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> wrongKind = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, armorIndex, EquipmentSlot.RelicTwo, TransactionId.New()), value => wrongKind = value);
            Assert.That(wrongKind, Is.Not.Null);
            Assert.That(wrongKind.Persisted, Is.True, wrongKind.Message);
            Assert.That(wrongKind.Outcome, Is.Not.Null);
            Assert.That(wrongKind.Outcome.Succeeded, Is.False);
            Assert.That(wrongKind.Outcome.Status, Is.EqualTo(InventoryOperationStatus.RejectedWrongEquipmentSlot));

            ExpeditionRunConfiguration runBeforeInvalidSlots = session.App.ActiveRun;
            ExpeditionInventory inventoryBeforeInvalidSlots = session.App.ActiveRun.Inventory;
            string inventoryJsonBeforeInvalidSlots = JsonUtility.ToJson(inventoryBeforeInvalidSlots.CreateSnapshot());
            string saveJsonBeforeInvalidSlots = JsonUtility.ToJson(session.App.ActiveExpedition);
            int healthBeforeInvalidSlots = session.Motor.CurrentHealth;
            int energyBeforeInvalidSlots = session.Motor.CurrentEnergy;
            int maximumHealthBeforeInvalidSlots = session.Motor.CombatStats.MaximumHealth;
            int maximumEnergyBeforeInvalidSlots = session.Motor.MaximumEnergy;
            ItemInstanceId armorBeforeInvalidSlots = session.Motor.EquippedArmorInstanceId;
            int writesBeforeInvalidSlots = store.ProfileWriteAttempts;
            EquipmentSlot[] unsupportedSlots = { EquipmentSlot.Armor, EquipmentSlot.Weapon };
            for (int slotIndex = 0; slotIndex < unsupportedSlots.Length; slotIndex++)
            {
                int invalidSlotBackpackIndex = FindBackpackIndex(inventoryBeforeInvalidSlots, flask.InstanceId);
                ExpeditionMutationCommitResult<InventoryOperationOutcome> invalidSlotResult = null;
                yield return Complete(session.App.EquipActiveExpeditionRelicAsync(room, invalidSlotBackpackIndex,
                    unsupportedSlots[slotIndex], TransactionId.New()), value => invalidSlotResult = value);
                Assert.That(invalidSlotResult, Is.Not.Null);
                Assert.That(invalidSlotResult.Persisted, Is.False);
                Assert.That(invalidSlotResult.Pending, Is.False);
                Assert.That(invalidSlotResult.Outcome, Is.Null,
                    "Weapon and armor slot requests must be rejected before a Core inventory outcome exists.");
                Assert.That(invalidSlotResult.Message, Is.Not.Empty);
                Assert.That(session.App.ActiveRun, Is.SameAs(runBeforeInvalidSlots));
                Assert.That(session.App.ActiveRun.Inventory, Is.SameAs(inventoryBeforeInvalidSlots));
                Assert.That(JsonUtility.ToJson(inventoryBeforeInvalidSlots.CreateSnapshot()),
                    Is.EqualTo(inventoryJsonBeforeInvalidSlots));
                Assert.That(JsonUtility.ToJson(session.App.ActiveExpedition), Is.EqualTo(saveJsonBeforeInvalidSlots));
                Assert.That(session.Collector.Inventory, Is.SameAs(inventoryBeforeInvalidSlots));
                Assert.That(session.Motor.CurrentHealth, Is.EqualTo(healthBeforeInvalidSlots));
                Assert.That(session.Motor.CurrentEnergy, Is.EqualTo(energyBeforeInvalidSlots));
                Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(maximumHealthBeforeInvalidSlots));
                Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(maximumEnergyBeforeInvalidSlots));
                Assert.That(session.Motor.EquippedArmorInstanceId, Is.EqualTo(armorBeforeInvalidSlots));
                Assert.That(session.App.HasPendingExpeditionWrite, Is.False);
                Assert.That(session.App.PendingEconomyOperationKey, Is.Empty);
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeInvalidSlots),
                    "Unsupported slot rejection must not write the save profile.");
            }

            int flaskIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, flask.InstanceId);
            TransactionId pendingTransaction = TransactionId.New();
            string pendingKey = RelicOperationKey(room, flaskIndex, EquipmentSlot.RelicTwo, pendingTransaction);
            store.FailNextProfileWrite = true;
            int writesBeforePending = store.ProfileWriteAttempts;
            ExpeditionMutationCommitResult<InventoryOperationOutcome> pending = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, flaskIndex, EquipmentSlot.RelicTwo, pendingTransaction), value => pending = value);
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.Persisted, Is.False);
            Assert.That(pending.Pending, Is.True);
            Assert.That(pending.Outcome, Is.Not.Null);
            Assert.That(pending.Outcome.Succeeded, Is.True);
            Assert.That(session.App.PendingEconomyOperationKey, Is.EqualTo(pendingKey));

            int conflictingIndex = FindBackpackIndex(session.App.ActiveRun.Inventory, secondBone.InstanceId);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> conflict = null;
            yield return Complete(session.App.EquipActiveExpeditionRelicAsync(
                room, conflictingIndex, EquipmentSlot.RelicOne, pendingTransaction), value => conflict = value);
            Assert.That(conflict, Is.Not.Null);
            Assert.That(conflict.Persisted, Is.False);
            Assert.That(conflict.Pending, Is.True,
                "A pending TransactionId with changed index/slot arguments must keep the original candidate locked.");
            Assert.That(conflict.Outcome, Is.Null,
                "The conflicting arguments did not produce a second Core inventory outcome.");
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforePending + 1));
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo, Is.Null,
                "The conflicting call must not publish or activate its different slot request.");

            ExpeditionMutationCommitResult<InventoryOperationOutcome> exactRetry = null;
            yield return Complete(session.App.RetryPendingEconomyMutationAsync<InventoryOperationOutcome>(pendingKey),
                value => exactRetry = value);
            Assert.That(exactRetry, Is.Not.Null);
            Assert.That(exactRetry.Persisted, Is.True, exactRetry.Message);
            Assert.That(exactRetry.Pending, Is.False);
            Assert.That(exactRetry.Outcome, Is.Not.Null);
            Assert.That(exactRetry.Outcome.Succeeded, Is.True);
            Assert.That(session.App.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo(SupportRelicIds.ArcaneFlask));

            DisposeSession(_session);
            _session = null;
            Object.Destroy(session.App.gameObject);
            yield return null;

            string v6Profile = ReadFrozenFixture(
                "Luckbound/Tests/Fixtures/Saves/V6/V6-P1-active-known-nontrivial.Frozen.envelope.json",
                V6ActiveFixtureSha256);
            var readOnlyStore = new RecordingSaveStore(profilePrimaryJson: v6Profile);
            ApplicationBootstrapper readOnlyApp = null;
            yield return StartApplication(readOnlyStore, value => readOnlyApp = value);
            Assert.That(readOnlyApp.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(readOnlyApp.CanWriteProfile, Is.False);
            Assert.That(readOnlyApp.ActiveRun, Is.Null);
            Assert.That(readOnlyApp.ActiveExpedition, Is.Not.Null);
            ExpeditionMutationCommitResult<InventoryOperationOutcome> readOnly = null;
            yield return Complete(readOnlyApp.EquipActiveExpeditionRelicAsync(
                RoomInstanceId.New(), 0, EquipmentSlot.RelicOne, TransactionId.New()), value => readOnly = value);
            Assert.That(readOnly, Is.Not.Null);
            Assert.That(readOnly.Persisted, Is.False);
            Assert.That(readOnly.Pending, Is.False);
            Assert.That(readOnly.Outcome, Is.Null,
                "A V6 protected profile must reject before attempting a Core equipment operation.");
            Assert.That(readOnly.Message, Is.Not.Empty);
            Assert.That(readOnlyStore.ProfileWriteAttempts, Is.Zero);
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
            BindActorFromCheckpoint(run, checkpoint, session.Actor, session.Armors);
            CreatePauseController(session);
            session.PauseBinding = app.BindExpeditionSimulationPauseLeaseProvider(session.Pause, () =>
            {
                session.LeasesAcquired++;
                IDisposable lease = session.Pause.AcquireSimulationPauseLease();
                return new CountingPauseLease(() =>
                {
                    lease.Dispose();
                    session.LeasesReleased++;
                });
            });
            session.PublicationBinding = app.BindExpeditionSessionMutationPublication(session.Pause,
                (candidateRun, candidateSave) =>
                {
                    session.CaptureCalls++;
                    Assert.That(candidateRun, Is.Not.Null);
                    Assert.That(candidateSave, Is.Not.Null);
                    Assert.That(candidateRun.RunId, Is.EqualTo(session.Run.RunId));
                    Assert.That(session.Pause.IsSimulationPaused, Is.True);
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
                            throw new InvalidOperationException("simulated support relic actor publication fault");
                        }
                        prepared.Publish();
                    };
                });
            return session;
        }

        private void CreatePauseController(RuntimeSession session)
        {
            session.PauseRoot = Track(new GameObject("G04 support relic pause owner"));
            var labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(session.PauseRoot.transform, false);
            session.Pause = session.PauseRoot.AddComponent<ExpeditionPauseController>();
            session.Pause.Initialize(labelObject.GetComponent<Text>(), pauseOnFocusLoss: false);
        }

        private void BindActorFromCheckpoint(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint,
            ActorFixture actor, ArmorCatalog armors = null)
        {
            Assert.That(checkpoint, Is.Not.Null);
            ExpeditionPlayerRunBinder.BindFromCheckpoint(run, checkpoint, actor.ActorId, actor.Input,
                GroundLayers(), EnemyLayers(), 0, actor.Motor, actor.Collector, run.Inventory.Catalog,
                actor.Weapons, actor.Skills, actor.ProjectilePool, actor.ProjectileScope, actor.Room,
                armorCatalog: armors ?? LoadArmorCatalog(run.Inventory.Catalog));
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
            actor.Body.constraints = RigidbodyConstraints2D.FreezeAll;
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
            actor.Room.Initialize(RoomInstanceId.New(), "room.g04.support_relic_test");
            _rooms.Add(actor.Room);

            GameObject poolObject = Track(new GameObject(name + " projectile pool"));
            actor.ProjectilePool = poolObject.AddComponent<ProjectilePool2D>();
            GameObject prefab = Track(new GameObject(name + " projectile prefab"));
            prefab.AddComponent<Rigidbody2D>();
            prefab.AddComponent<CircleCollider2D>();
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            actor.ProjectilePool.Initialize(prefab);

            GameObject scopeObject = new GameObject(name + " projectile scope");
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

        private static InventoryItemStack CollectItem(ExpeditionInventory inventory, string itemId,
            ItemRarity rarity = ItemRarity.Common, int upgradeLevel = 0)
        {
            var item = new InventoryItemStack(ItemInstanceId.New(), itemId, rarity, upgradeLevel, 1);
            InventoryOperationOutcome outcome = inventory.TryCollectItem(item, TransactionId.New());
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Succeeded, Is.True, outcome.Status.ToString());
            return item;
        }

        private static void EquipArmor(ExpeditionInventory inventory, string itemId,
            ItemRarity rarity, int upgradeLevel)
        {
            InventoryItemStack item = CollectItem(inventory, itemId, rarity, upgradeLevel);
            EquipBackpackItem(inventory, item.InstanceId, EquipmentSlot.Armor);
            Assert.That(inventory.Armor.InstanceId, Is.EqualTo(item.InstanceId));
        }

        private static void EquipBackpackItem(ExpeditionInventory inventory,
            ItemInstanceId itemInstanceId, EquipmentSlot slot)
        {
            int index = FindBackpackIndex(inventory, itemInstanceId);
            InventoryOperationOutcome outcome = inventory.EquipBackpackItem(index, slot, TransactionId.New());
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Succeeded, Is.True, outcome.Status.ToString());
        }

        private static int FindBackpackIndex(ExpeditionInventory inventory, ItemInstanceId itemInstanceId)
        {
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                if (inventory.GetBackpackSlot(index)?.InstanceId == itemInstanceId) return index;
            Assert.Fail("Item instance " + itemInstanceId + " is not in the backpack.");
            return -1;
        }

        private static int FindBackpackIndexByItemId(ExpeditionInventory inventory, string itemId)
        {
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                if (string.Equals(inventory.GetBackpackSlot(index)?.ItemId, itemId, StringComparison.Ordinal)) return index;
            Assert.Fail("Item " + itemId + " is not in the backpack.");
            return -1;
        }

        private static ArmorResolution ExpectedKnownArmor(ExpeditionRunConfiguration run, ArmorCatalog armors)
        {
            SupportRelicModifiers support = SupportRelicResolver.Resolve(
                SupportedRelicId(run.Inventory.RelicOne), SupportedRelicId(run.Inventory.RelicTwo));
            ArmorProfile profile = armors.Get(run.Inventory.Armor.ItemId);
            return ArmorStatsResolver.Resolve(profile, run.Inventory.Armor.UpgradeLevel,
                checked(run.StartingHealth + support.MaximumHealthBonus),
                checked(run.StartingEnergy + support.MaximumEnergyBonus),
                run.StartingHealth, run.StartingEnergy, run.Inventory.Armor.Rarity);
        }

        private static string SupportedRelicId(InventoryItemStack item)
        {
            return item != null && SupportRelicIds.IsSupported(item.ItemId) ? item.ItemId : null;
        }

        private static void AssertKnownMaximums(RuntimeSession session, ArmorResolution expected)
        {
            Assert.That(session.Motor.CombatStats.MaximumHealth, Is.EqualTo(expected.MaximumHealth));
            Assert.That(session.Motor.MaximumEnergy, Is.EqualTo(expected.MaximumEnergy));
            Assert.That(session.Collector.Inventory, Is.SameAs(session.App.ActiveRun.Inventory));
            Assert.That(session.App.ActiveExpedition.maximumHealth, Is.EqualTo(expected.MaximumHealth));
            Assert.That(session.App.ActiveExpedition.maximumEnergy, Is.EqualTo(expected.MaximumEnergy));
        }

        private static string RelicOperationKey(RoomInstanceId room, int backpackIndex,
            EquipmentSlot slot, TransactionId transactionId)
        {
            return "equip-relic:" + room + ":" + backpackIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)slot).ToString(CultureInfo.InvariantCulture) + ":" + transactionId;
        }

        private static void AssertSameInventoryOutcome(
            InventoryOperationOutcome repeated, InventoryOperationOutcome original)
        {
            Assert.That(original, Is.Not.Null);
            Assert.That(repeated, Is.Not.Null);
            Assert.That(original.WasReplay, Is.False,
                "EquipBackpackItem caches and returns its original outcome without marking a replay copy.");
            Assert.That(repeated.TransactionId, Is.EqualTo(original.TransactionId));
            Assert.That(repeated.Status, Is.EqualTo(original.Status));
            Assert.That(repeated.ItemId, Is.EqualTo(original.ItemId));
            Assert.That(repeated.WasReplay, Is.EqualTo(original.WasReplay));
            Assert.That(repeated.DisplacedItem == null, Is.EqualTo(original.DisplacedItem == null));
            if (original.DisplacedItem != null)
            {
                Assert.That(repeated.DisplacedItem.InstanceId, Is.EqualTo(original.DisplacedItem.InstanceId));
                Assert.That(repeated.DisplacedItem.ItemId, Is.EqualTo(original.DisplacedItem.ItemId));
                Assert.That(repeated.DisplacedItem.Rarity, Is.EqualTo(original.DisplacedItem.Rarity));
                Assert.That(repeated.DisplacedItem.UpgradeLevel, Is.EqualTo(original.DisplacedItem.UpgradeLevel));
                Assert.That(repeated.DisplacedItem.Quantity, Is.EqualTo(original.DisplacedItem.Quantity));
            }
        }

        private static string CreateUnknownLegacyProfileWithEquippedPaleBone()
        {
            string original = ReadFrozenFixture(
                "Luckbound/Tests/Fixtures/Saves/V5/capture/cultures/en-US/P2-unknown-null/envelope.json",
                V5UnknownFixtureSha256);
            LegacyProfileSaveEnvelopeV5 source = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV5>(original);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.contentVersion, Is.EqualTo(5));
            Assert.That(source.hasActiveExpedition, Is.True);
            Assert.That(source.activeExpedition, Is.Not.Null);
            Assert.That(source.activeExpedition.hasFrozenPermanentUpgrades, Is.False);

            ItemCatalog items = LoadItemCatalog();
            ExpeditionInventory inventory = ExpeditionInventory.Restore(items,
                JsonUtility.FromJson<ExpeditionInventorySnapshot>(source.activeExpedition.inventorySnapshotJson));
            CollectItem(inventory, SupportRelicIds.PaleBone);
            CollectItem(inventory, SupportRelicIds.MerchantSeal);
            int boneIndex = FindBackpackIndexByItemId(inventory, SupportRelicIds.PaleBone);
            InventoryOperationOutcome equipped = inventory.EquipBackpackItem(
                boneIndex, EquipmentSlot.RelicOne, TransactionId.New());
            Assert.That(equipped, Is.Not.Null);
            Assert.That(equipped.Succeeded, Is.True, equipped.Status.ToString());

            string inventoryJson = JsonUtility.ToJson(inventory.CreateSnapshot());
            source.activeExpedition.inventorySnapshotJson = inventoryJson;
            source.activeExpedition.maximumHealth = checked(source.activeExpedition.maximumHealth + 15);
            source.activeExpedition.currentHealth = Math.Min(source.activeExpedition.currentHealth,
                source.activeExpedition.maximumHealth);
            source.activeExpedition.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(inventory);

            LegacyExpeditionRoomCheckpointSaveDataV5 checkpoint = JsonUtility.FromJson<LegacyExpeditionRoomCheckpointSaveDataV5>(
                source.activeExpedition.roomEntryCheckpointJson);
            Assert.That(checkpoint, Is.Not.Null);
            checkpoint.inventorySnapshotJson = inventoryJson;
            checkpoint.maximumHealth = source.activeExpedition.maximumHealth;
            checkpoint.currentHealth = source.activeExpedition.currentHealth;
            checkpoint.healthPotionCharges = source.activeExpedition.healthPotionCharges;
            source.activeExpedition.roomEntryCheckpointJson = JsonUtility.ToJson(checkpoint, false);

            source.checksum = ComputeSha256(SaveV5HistoricalReader.GetProfileChecksumPreimage(source));
            string generated = JsonUtility.ToJson(source, false);
            Assert.That(SaveV5HistoricalReader.ReadProfile(generated, out LegacyProfileSaveEnvelopeV5 verified),
                Is.EqualTo(DecodeStatus.Valid), "The unknown-loadout seed must remain a valid V5 input.");
            Assert.That(verified.activeExpedition.hasFrozenPermanentUpgrades, Is.False);
            return generated;
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
            session.PublicationBinding = null;
            session.PauseBinding?.Dispose();
            session.PauseBinding = null;
            if (session.Pause != null && session.Pause.IsPaused) session.Pause.SetPaused(false);
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
            public int LeasesAcquired;
            public int LeasesReleased;
            public int CaptureCalls;
            public int PreparationCalls;
            public int PublicationAttempts;
            public bool FailNextPublication;

            public PlayerMotor2D Motor => Actor.Motor;
            public ItemCollector2D Collector => Actor.Collector;
            public SkillCatalog Skills => Actor.Skills;
        }

        private sealed class CountingPauseLease : IDisposable
        {
            private Action _release;

            public CountingPauseLease(Action release) { _release = release; }

            public void Dispose()
            {
                Action callback = _release;
                _release = null;
                callback?.Invoke();
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

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            Assert.That(task, Is.Not.Null, "A runtime operation must return a Task.");
            for (int frame = 0; frame < WaitFrameLimit && !task.IsCompleted; frame++) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Runtime operation exceeded " + WaitFrameLimit + " frames.");
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }
    }
}
