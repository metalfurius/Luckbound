using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Luckbound.Core.Combat;
using Luckbound.Core.Consumables;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
