using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Combat;
using Luckbound.Core.Consumables;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerConsumableRuntimeTests
    {
        [UnityTest]
        public IEnumerator HealthPotionUsesTheQuickStackAfterPreparationAndClampsToCurrentMaximum()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.HealthPotionItemId, 2);
            try
            {
                DirectImpactOutcome hit = player.Motor.ReceiveDirectImpact(
                    HitId.New(), Request(player.Motor.CombatStats, 10m), player.Motor.SimulationTick, Vector2.zero);
                Assert.That(hit.Status, Is.EqualTo(DirectImpactStatus.Applied));
                Assert.That(player.Motor.CurrentHealth, Is.EqualTo(90));

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                Assert.That(player.Motor.IsPreparingHealthPotion, Is.True);
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                for (int tick = 0; tick < HealthPotionUseState.PreparationDurationTicks; tick++)
                    yield return new WaitForFixedUpdate();

                Assert.That(player.Motor.CurrentHealth, Is.EqualTo(player.Motor.CombatStats.MaximumHealth));
                Assert.That(player.Motor.LastConsumableUseOutcome.HealthRestored, Is.EqualTo(10));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.RejectedAtFullResource));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator EnergyTonicUsesTheEquippedStackAndRejectsFullEnergyWithoutSpending()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.EnergyTonicItemId, 2);
            try
            {
                player.Motor.SkillUses.SetCurrentEnergy(25);
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Preparing));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                for (int tick = 0; tick < EnergyTonicUseState.PreparationDurationTicks; tick++)
                    yield return new WaitForFixedUpdate();

                Assert.That(player.Motor.CurrentEnergy, Is.EqualTo(player.Motor.MaximumEnergy));
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Motor.LastConsumableUseOutcome.EnergyRestored, Is.EqualTo(25));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.RejectedAtFullResource));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator AntidoteClearsAnActiveStunAndBlocksReapplicationForSixHundredTicks()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.AntidoteItemId, 2);
            try
            {
                DirectImpactOutcome initialHit = player.Motor.ReceiveDirectImpact(
                    HitId.New(), Request(player.Motor.CombatStats, 1m), player.Motor.SimulationTick,
                    Vector2.zero, stunDurationTicks: 45);
                Assert.That(initialHit.Status, Is.EqualTo(DirectImpactStatus.Applied));
                Assert.That(player.Motor.IsStunned, Is.True);

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.IsStunned, Is.False);
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                long firstProtectionEnd = player.Motor.AntidoteProtectionEndsAtTickExclusive;
                Assert.That(firstProtectionEnd, Is.EqualTo(player.Motor.SimulationTick + 600));

                for (int tick = 0; tick < PlayerMotor2D.DirectHitProtectionTicks + 2; tick++)
                    yield return new WaitForFixedUpdate();

                DirectImpactOutcome protectedHit = player.Motor.ReceiveDirectImpact(
                    HitId.New(), Request(player.Motor.CombatStats, 1m), player.Motor.SimulationTick,
                    Vector2.zero, stunDurationTicks: 45);
                Assert.That(protectedHit.Status, Is.EqualTo(DirectImpactStatus.Applied));
                Assert.That(player.Motor.IsStunned, Is.False, "The accepted hit must not reapply stun during antidote protection.");

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Inventory.QuickConsumable, Is.Null);
                Assert.That(player.Motor.AntidoteProtectionEndsAtTickExclusive,
                    Is.EqualTo(player.Motor.SimulationTick + 600));
                Assert.That(player.Motor.AntidoteProtectionEndsAtTickExclusive, Is.GreaterThan(firstProtectionEnd));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator ValidDodgeTakesPriorityOverSimultaneousAntidoteWithoutConsumingIt()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.AntidoteItemId, 1);
            try
            {
                player.Input.Press(PlayerAction.Dodge);
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();

                Assert.That(player.Motor.DodgeActivationCount, Is.EqualTo(1));
                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.Cancelled));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1),
                    "A lower-priority action must not consume its item on the dodge activation tick.");
                Assert.That(player.Motor.AntidoteProtectionEndsAtTickExclusive, Is.Zero);
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator BombInputQueuesFortyDamageForActorsInsideItsInclusiveRadiusOnly()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.BombItemId, 1);
            GameObject insideObject = CreateBombTarget("BombInside", new Vector2(0f, 2f), out ProjectileImpactTarget2D inside);
            GameObject outsideObject = CreateBombTarget("BombOutside", new Vector2(1.5f, 1.5f), out ProjectileImpactTarget2D outside);
            try
            {
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Inventory.QuickConsumable, Is.Null);
                Assert.That(inside.PendingImpactCount, Is.EqualTo(1));
                Assert.That(outside.PendingImpactCount, Is.Zero);
                Assert.That(inside.TryDequeueImpact(out var impact), Is.True);
                Assert.That(impact.TargetActorId, Is.EqualTo(inside.ActorId));
                Assert.That(impact.SourceActorId, Is.EqualTo(player.Motor.ActorId));
                Assert.That(impact.BaseDamage, Is.EqualTo(40m));
            }
            finally
            {
                player.Destroy();
                UnityEngine.Object.DestroyImmediate(insideObject);
                UnityEngine.Object.DestroyImmediate(outsideObject);
            }
        }

        [UnityTest]
        public IEnumerator TorchInputCreatesARealSixtySecondAdditionalLight()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.TorchItemId, 1);
            try
            {
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Inventory.QuickConsumable, Is.Null);
                Assert.That(player.Motor.IsTorchLightActive, Is.True);
                Assert.That(player.Motor.TorchLightEndsAtTickExclusive, Is.EqualTo(player.Motor.SimulationTick + 3600));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator TorchUseQueuesOneCueAfterConsumptionAndLightActivationBehindTheClosedGate()
        {
            bool previousListenerPause = AudioListener.pause;
            PlayerFixture player = null;
            ApplicationAudioRouter router = null;
            Action observeTorchActivation = null;
            var audioObjects = new List<GameObject>();
            int activationCount = 0;
            try
            {
                AudioListener.pause = true;
                player = CreatePlayer(PlayerMotor2D.TorchItemId, 2);
                router = CreatePlayerAudioBridge(player.Motor, audioObjects);
                observeTorchActivation = () =>
                {
                    activationCount++;
                    InventoryItemStack remaining = player.Inventory.QuickConsumable;
                    Assert.That(remaining == null ? 0 : remaining.Quantity, Is.EqualTo(2 - activationCount),
                        "The cue event runs only after the quick stack was consumed.");
                    Assert.That(player.Motor.IsTorchLightActive, Is.True);
                    Light2D light = player.GameObject.GetComponentInChildren<Light2D>();
                    Assert.That(light, Is.Not.Null);
                    Assert.That(light.enabled, Is.True, "The additional light is active before the cue event.");
                    Assert.That(player.Motor.TorchLightEndsAtTickExclusive,
                        Is.EqualTo(player.Motor.SimulationTick + TorchEffect.DurationTicks));
                };
                player.Motor.TorchActivated += observeTorchActivation;

                Assert.That(router.UserGestureObserved, Is.False);
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                long firstEndTick = player.Motor.TorchLightEndsAtTickExclusive;
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(activationCount, Is.EqualTo(1));
                Assert.That(router.UserGestureObserved, Is.False, "The test input buffer must not unlock the audio gate.");
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1));
                Assert.That(PendingEffectIdAt(router, 0), Is.EqualTo(ApplicationAudioRouter.TorchEffectId));
                Assert.That(router.GetComponent<RuntimeAudioDirector>().OneShotSourceCount, Is.Zero,
                    "The closed gate queues the cue without starting a voice.");

                for (int tick = 0; tick < 4; tick++) yield return new WaitForFixedUpdate();
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(activationCount, Is.EqualTo(2));
                Assert.That(player.Motor.TorchLightEndsAtTickExclusive,
                    Is.EqualTo(player.Motor.SimulationTick + TorchEffect.DurationTicks));
                Assert.That(player.Motor.TorchLightEndsAtTickExclusive, Is.GreaterThan(firstEndTick),
                    "A second successful use renews the light interval and requests one additional cue.");
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(2));
                Assert.That(PendingEffectIdAt(router, 1), Is.EqualTo(ApplicationAudioRouter.TorchEffectId));
                Assert.That(router.UserGestureObserved, Is.False);

                router.enabled = false;
                Assert.That(router.PendingEffectRequestCount, Is.Zero, "Disabling the router clears its scene queue.");
                player.Motor.TorchActivated -= observeTorchActivation;
            }
            finally
            {
                try
                {
                    if (router != null) router.enabled = false;
                    foreach (GameObject audioObject in audioObjects)
                    {
                        if (audioObject != null) UnityEngine.Object.DestroyImmediate(audioObject);
                    }
                    if (player != null)
                    {
                        if (observeTorchActivation != null) player.Motor.TorchActivated -= observeTorchActivation;
                        player.Destroy();
                    }
                }
                finally
                {
                    AudioListener.pause = previousListenerPause;
                }
            }
        }

        [UnityTest]
        public IEnumerator UnsupportedTorchUseIsRejectedWithoutRequestingAudio()
        {
            bool previousListenerPause = AudioListener.pause;
            PlayerFixture player = null;
            ApplicationAudioRouter router = null;
            var audioObjects = new List<GameObject>();
            try
            {
                AudioListener.pause = true;
                player = CreatePlayer(PlayerMotor2D.TorchItemId, 1);
                router = CreatePlayerAudioBridge(player.Motor, audioObjects);
                FieldInfo lightField = typeof(PlayerMotor2D).GetField("_torchLight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lightField, Is.Not.Null);
                lightField.SetValue(player.Motor, null);

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.RejectedUnsupportedSubsystem));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(player.Motor.IsTorchLightActive, Is.False);
                Assert.That(router.PendingEffectRequestCount, Is.Zero);
                Assert.That(router.UserGestureObserved, Is.False);
                router.enabled = false;
                Assert.That(router.PendingEffectRequestCount, Is.Zero);
            }
            finally
            {
                try
                {
                    if (router != null) router.enabled = false;
                    foreach (GameObject audioObject in audioObjects)
                    {
                        if (audioObject != null) UnityEngine.Object.DestroyImmediate(audioObject);
                    }
                    if (player != null) player.Destroy();
                }
                finally
                {
                    AudioListener.pause = previousListenerPause;
                }
            }
        }

        [UnityTest]
        public IEnumerator FortuneBrewDoesNotConsumeWhenTheDurableFortuneMutationAdapterIsAbsent()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.FortuneBrewItemId, 1);
            try
            {
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.RejectedUnsupportedSubsystem));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator FortuneBrewWaitsForThePersistedAtomicMutationBeforePublishingInventory()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.FortuneBrewItemId, 2);
            var runtime = new DeferredFortuneBrewRuntime();
            player.Motor.ConfigureFortuneBrewRuntime(runtime);
            try
            {
                ExpeditionInventory originalInventory = player.Inventory;
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(runtime.CallCount, Is.EqualTo(1));
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Pending));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2),
                    "Gameplay must not decrement the live stack before App confirms persistence.");

                ExpeditionInventory committedInventory = runtime.CompleteSuccess(originalInventory, currentCharges: 2);
                yield return new WaitForFixedUpdate();

                ItemCollector2D collector = player.GameObject.GetComponent<ItemCollector2D>();
                Assert.That(collector.Inventory, Is.SameAs(committedInventory));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(committedInventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(committedInventory.HasSuccessfulQuickConsumableConsumption(
                    runtime.ItemInstanceId, PlayerMotor2D.FortuneBrewItemId, runtime.TransactionId), Is.True);
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));
                Assert.That(player.Motor.LastConsumableUseOutcome.ItemId, Is.EqualTo(PlayerMotor2D.FortuneBrewItemId));
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator FortuneBrewPendingCommitRetriesOnlyOnNewInputWithTheSameIdentityAndPublishesOnce()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.FortuneBrewItemId, 2);
            var runtime = new RetryableFortuneBrewRuntime();
            player.Motor.ConfigureFortuneBrewRuntime(runtime);
            try
            {
                ExpeditionInventory originalInventory = player.Inventory;
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(runtime.CallCount, Is.EqualTo(1));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2));

                runtime.CompleteFirstAsPending(currentCharges: 1);
                yield return new WaitForFixedUpdate();

                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Pending));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2),
                    "An unpersisted Fortune result must leave the live quick stack untouched.");
                yield return new WaitForFixedUpdate();
                Assert.That(runtime.CallCount, Is.EqualTo(1),
                    "The runtime must wait for a new player input instead of retrying automatically.");

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);

                Assert.That(runtime.CallCount, Is.EqualTo(2));
                Assert.That(runtime.ItemInstanceIds[1], Is.EqualTo(runtime.ItemInstanceIds[0]));
                Assert.That(runtime.TransactionIds[1], Is.EqualTo(runtime.TransactionIds[0]));
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Pending));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2));

                ExpeditionInventory committedInventory = runtime.CompleteSecondSuccess(originalInventory, currentCharges: 2);
                yield return new WaitForFixedUpdate();

                ItemCollector2D collector = player.GameObject.GetComponent<ItemCollector2D>();
                Assert.That(collector.Inventory, Is.SameAs(committedInventory));
                Assert.That(committedInventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(originalInventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(committedInventory.HasSuccessfulQuickConsumableConsumption(
                    runtime.ItemInstanceIds[0], PlayerMotor2D.FortuneBrewItemId, runtime.TransactionIds[0]), Is.True);
                Assert.That(player.Motor.LastConsumableUseOutcome.Status, Is.EqualTo(PlayerConsumableUseStatus.Consumed));

                yield return new WaitForFixedUpdate();
                Assert.That(runtime.CallCount, Is.EqualTo(2));
                Assert.That(collector.Inventory, Is.SameAs(committedInventory));
                Assert.That(committedInventory.QuickConsumable.Quantity, Is.EqualTo(1),
                    "Publishing a durable result is one-shot and must not decrement the candidate again.");
            }
            finally
            {
                player.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator FortuneBrewAtChargeCapacityPersistsNoConsumption()
        {
            PlayerFixture player = CreatePlayer(PlayerMotor2D.FortuneBrewItemId, 1);
            var runtime = new DeferredFortuneBrewRuntime();
            player.Motor.ConfigureFortuneBrewRuntime(runtime);
            try
            {
                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                runtime.CompleteCapacityRejection(player.Inventory, currentCharges: FortuneBrewEffect.MaximumCharges);
                yield return new WaitForFixedUpdate();

                Assert.That(player.Motor.LastConsumableUseOutcome.Status,
                    Is.EqualTo(PlayerConsumableUseStatus.RejectedAtFullResource));
                Assert.That(player.GameObject.GetComponent<ItemCollector2D>().Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(player.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
            }
            finally
            {
                player.Destroy();
            }
        }

        private static PlayerFixture CreatePlayer(string quickItemId, int quantity)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            if (groundLayer < 0 || enemyLayer < 0)
                throw new InvalidOperationException("The player consumable scenarios require the project's Ground and Enemies layers.");

            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var playerObject = new GameObject("ConsumablePlayer");
            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            playerObject.AddComponent<CapsuleCollider2D>();
            ItemCollector2D collector = playerObject.AddComponent<ItemCollector2D>();
            PlayerMotor2D motor = playerObject.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, 1 << groundLayer, enemyLayers: 1 << enemyLayer);
            ItemCatalog catalog = CreateItemCatalog();
            ExpeditionInventory inventory = new ExpeditionInventory(catalog, "class.bellator");
            var stack = new InventoryItemStack(ItemInstanceId.New(), quickItemId, ItemRarity.Common, 0, quantity);
            Assert.That(inventory.TryCollectItem(stack, TransactionId.New()).Succeeded, Is.True);
            int slot = 0;
            for (; slot < inventory.Backpack.Count; slot++)
            {
                if (inventory.GetBackpackSlot(slot)?.InstanceId == stack.InstanceId) break;
            }
            Assert.That(slot, Is.LessThan(inventory.Backpack.Count));
            Assert.That(inventory.SetQuickConsumable(slot, TransactionId.New()).Succeeded, Is.True);
            collector.Configure(inventory, catalog);
            return new PlayerFixture(playerObject, motor, input, inventory);
        }

        private static ApplicationAudioRouter CreatePlayerAudioBridge(PlayerMotor2D motor,
            List<GameObject> temporaryObjects)
        {
            var compositionOwner = new GameObject("I06_TorchAudioComposition");
            temporaryObjects.Add(compositionOwner);
            ExpeditionGameplayComposition composition = compositionOwner.AddComponent<ExpeditionGameplayComposition>();
            SetPrivateField(composition, "_playerMotor", motor);

            var audioOwner = new GameObject("I06_TorchAudioRouter");
            temporaryObjects.Add(audioOwner);
            audioOwner.AddComponent<RuntimeAudioDirector>();
            ApplicationAudioRouter router = audioOwner.AddComponent<ApplicationAudioRouter>();
            router.SetSceneContext("Expedition");
            SetPrivateField(composition, "_audioRouter", router);

            MethodInfo bindAudio = typeof(ExpeditionGameplayComposition).GetMethod(
                "BindPlayerMagusAudio", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bindAudio, Is.Not.Null, "Use the production Expedition player-audio binding.");
            bindAudio.Invoke(composition, null);
            return router;
        }

        private static void SetPrivateField<T>(object instance, string fieldName, T value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private composition field: " + fieldName);
            field.SetValue(instance, value);
        }

        private static string PendingEffectIdAt(ApplicationAudioRouter router, int index)
        {
            FieldInfo field = typeof(ApplicationAudioRouter).GetField("_pendingEffectRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            IList requests = (IList)field.GetValue(router);
            Assert.That(index, Is.InRange(0, requests.Count - 1));
            PropertyInfo effectId = requests[index].GetType().GetProperty("EffectId",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(effectId, Is.Not.Null);
            return (string)effectId.GetValue(requests[index]);
        }

        private static ItemCatalog CreateItemCatalog()
        {
            var definitions = new List<ItemDefinition>();
            foreach (string id in new[]
            {
                PlayerMotor2D.HealthPotionItemId,
                PlayerMotor2D.EnergyTonicItemId,
                PlayerMotor2D.AntidoteItemId,
                PlayerMotor2D.BombItemId,
                PlayerMotor2D.FortuneBrewItemId,
                PlayerMotor2D.TorchItemId
            })
            {
                definitions.Add(new ItemDefinition(id, "test." + id, "test." + id + ".description", ItemKind.Consumable, 3));
            }
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
            {
                string suffix = kind.ToString().ToLowerInvariant();
                definitions.Add(new ItemDefinition("material.test_" + suffix, "test.material." + suffix,
                    "test.material." + suffix + ".description", ItemKind.Material, 99, materialKind: kind));
            }
            return new ItemCatalog(definitions);
        }

        private static GameObject CreateBombTarget(string name, Vector2 position, out ProjectileImpactTarget2D target)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            var targetObject = new GameObject(name);
            targetObject.layer = enemyLayer;
            targetObject.transform.position = position;
            targetObject.AddComponent<BoxCollider2D>();
            target = targetObject.AddComponent<ProjectileImpactTarget2D>();
            target.Initialize(ActorId.New());
            return targetObject;
        }

        private static DamageCalculationRequest Request(CombatStats target, decimal damage)
        {
            return new DamageCalculationRequest(
                damage, 1m, 1m, 0m, 0, 0m, target, targetIsInvulnerable: false);
        }

        private sealed class DeferredFortuneBrewRuntime : IPlayerFortuneBrewRuntime
        {
            private readonly TaskCompletionSource<PlayerFortuneBrewCommitOutcome> _completion =
                new TaskCompletionSource<PlayerFortuneBrewCommitOutcome>();

            public int CallCount { get; private set; }
            public ItemInstanceId ItemInstanceId { get; private set; }
            public TransactionId TransactionId { get; private set; }

            public Task<PlayerFortuneBrewCommitOutcome> ConsumeFortuneBrewAsync(
                ItemInstanceId itemInstanceId,
                TransactionId transactionId)
            {
                CallCount++;
                ItemInstanceId = itemInstanceId;
                TransactionId = transactionId;
                return _completion.Task;
            }

            public ExpeditionInventory CompleteSuccess(ExpeditionInventory liveInventory, int currentCharges)
            {
                ExpeditionInventory committed = Clone(liveInventory);
                InventoryOperationOutcome consumed = committed.TryConsumeQuickConsumable(
                    ItemInstanceId, PlayerMotor2D.FortuneBrewItemId, TransactionId);
                Assert.That(consumed.Succeeded, Is.True);
                Assert.That(consumed.WasReplay, Is.False);
                _completion.SetResult(new PlayerFortuneBrewCommitOutcome(
                    persisted: true,
                    status: FortuneOperationStatus.Succeeded,
                    currentCharges: currentCharges,
                    committedInventory: committed));
                return committed;
            }

            public ExpeditionInventory CompleteCapacityRejection(ExpeditionInventory liveInventory, int currentCharges)
            {
                ExpeditionInventory committed = Clone(liveInventory);
                _completion.SetResult(new PlayerFortuneBrewCommitOutcome(
                    persisted: true,
                    status: FortuneOperationStatus.RejectedAtCapacity,
                    currentCharges: currentCharges,
                    committedInventory: committed));
                return committed;
            }

            private static ExpeditionInventory Clone(ExpeditionInventory inventory)
            {
                return ExpeditionInventory.Restore(inventory.Catalog, inventory.CreateSnapshot());
            }
        }

        private sealed class RetryableFortuneBrewRuntime : IPlayerFortuneBrewRuntime
        {
            private readonly TaskCompletionSource<PlayerFortuneBrewCommitOutcome> _firstCompletion =
                new TaskCompletionSource<PlayerFortuneBrewCommitOutcome>();
            private readonly TaskCompletionSource<PlayerFortuneBrewCommitOutcome> _secondCompletion =
                new TaskCompletionSource<PlayerFortuneBrewCommitOutcome>();

            public int CallCount { get; private set; }
            public List<ItemInstanceId> ItemInstanceIds { get; } = new List<ItemInstanceId>();
            public List<TransactionId> TransactionIds { get; } = new List<TransactionId>();

            public Task<PlayerFortuneBrewCommitOutcome> ConsumeFortuneBrewAsync(
                ItemInstanceId itemInstanceId,
                TransactionId transactionId)
            {
                ItemInstanceIds.Add(itemInstanceId);
                TransactionIds.Add(transactionId);
                CallCount++;
                if (CallCount == 1) return _firstCompletion.Task;
                if (CallCount == 2) return _secondCompletion.Task;
                throw new InvalidOperationException("The retry scenario expects exactly two explicit use attempts.");
            }

            public void CompleteFirstAsPending(int currentCharges)
            {
                _firstCompletion.SetResult(new PlayerFortuneBrewCommitOutcome(
                    persisted: false,
                    status: FortuneOperationStatus.RejectedConflict,
                    currentCharges: currentCharges,
                    committedInventory: null,
                    pending: true));
            }

            public ExpeditionInventory CompleteSecondSuccess(ExpeditionInventory liveInventory, int currentCharges)
            {
                ItemInstanceId itemInstanceId = ItemInstanceIds[1];
                TransactionId transactionId = TransactionIds[1];
                ExpeditionInventory committed = ExpeditionInventory.Restore(
                    liveInventory.Catalog, liveInventory.CreateSnapshot());
                InventoryOperationOutcome consumed = committed.TryConsumeQuickConsumable(
                    itemInstanceId, PlayerMotor2D.FortuneBrewItemId, transactionId);
                Assert.That(consumed.Succeeded, Is.True);
                Assert.That(consumed.WasReplay, Is.False);
                _secondCompletion.SetResult(new PlayerFortuneBrewCommitOutcome(
                    persisted: true,
                    status: FortuneOperationStatus.Succeeded,
                    currentCharges: currentCharges,
                    committedInventory: committed));
                return committed;
            }
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(GameObject gameObject, PlayerMotor2D motor, PlayerInputBuffer input,
                ExpeditionInventory inventory)
            {
                GameObject = gameObject;
                Motor = motor;
                Input = input;
                Inventory = inventory;
            }

            public GameObject GameObject { get; }
            public PlayerMotor2D Motor { get; }
            public PlayerInputBuffer Input { get; }
            public ExpeditionInventory Inventory { get; }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }
    }
}
