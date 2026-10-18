using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class BellatorWeaponRuntimeAcceptanceTests
    {
        private const string RustedSwordId = "weapon.bellator.rusted_sword";
        private const string TrailSaberId = "weapon.bellator.trail_saber";
        private const string UmbraGreatswordId = "weapon.bellator.umbra_greatsword";

        private readonly List<GameObject> _objects = new List<GameObject>();
        private Vector2 _previousGravity;
        private float _previousFixedDeltaTime;
        private float _previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _previousTimeScale = Time.timeScale;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in _objects)
            {
                if (gameObject != null) Object.Destroy(gameObject);
            }
            _objects.Clear();
            yield return null;
            Physics2D.gravity = _previousGravity;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator RustedSwordCatalogProfileReachesRealDamageAndTwentySevenTickCadence()
        {
            return VerifyBellatorWeapon(
                RustedSwordId,
                expectedDamage: 12,
                expectedIntervalSeconds: 0.45f,
                expectedIntervalTicks: 27,
                expectedTrait: WeaponTraitId.Balanced);
        }

        [UnityTest]
        public IEnumerator TrailSaberCatalogProfileReachesRealDamageAndTwentyTickCadence()
        {
            return VerifyBellatorWeapon(
                TrailSaberId,
                expectedDamage: 9,
                expectedIntervalSeconds: 0.33f,
                expectedIntervalTicks: 20,
                expectedTrait: WeaponTraitId.RapidCadence);
        }

        [UnityTest]
        public IEnumerator EpicSwordAtPlusThreeScalesRealDamageWithoutIntermediateRounding()
        {
            return VerifyBellatorWeapon(RustedSwordId, 12, 0.45f, 27, WeaponTraitId.Balanced,
                rarity: ItemRarity.Epic, upgradeLevel: 3, expectedRawDamage: 17.94m, expectedHitDamage: 18);
        }

        [UnityTest]
        public IEnumerator UmbraGreatswordCatalogProfileReachesRealDamageAndFortyTwoTickCadence()
        {
            return VerifyBellatorWeapon(
                UmbraGreatswordId,
                expectedDamage: 18,
                expectedIntervalSeconds: 0.70f,
                expectedIntervalTicks: 42,
                expectedTrait: WeaponTraitId.GreaterRange,
                additionalTrait: WeaponTraitId.GreaterKnockback);
        }

        private IEnumerator VerifyBellatorWeapon(
            string weaponId,
            int expectedDamage,
            float expectedIntervalSeconds,
            int expectedIntervalTicks,
            WeaponTraitId expectedTrait,
            WeaponTraitId? additionalTrait = null, ItemRarity rarity = ItemRarity.Common, int upgradeLevel = 0,
            decimal? expectedRawDamage = null, int? expectedHitDamage = null)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });

            if (weaponId != RustedSwordId || rarity != ItemRarity.Common || upgradeLevel != 0)
            {
                EquipWeapon(run.Inventory, weaponId, rarity, upgradeLevel);
            }

            Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo(weaponId),
                "The run inventory must hold the requested weapon before composition.");

            ItemCatalog itemCatalog = run.Inventory.Catalog;
            WeaponCatalog weaponCatalog = LoadWeaponCatalog(itemCatalog);
            WeaponProfile expectedProfile = weaponCatalog.Get(weaponId);
            Assert.That(expectedProfile.ClassId, Is.EqualTo(BellatorSkillLoadout.ClassId));
            Assert.That(expectedProfile.Damage, Is.EqualTo(expectedDamage));
            Assert.That(expectedProfile.AttackIntervalSeconds, Is.EqualTo(expectedIntervalSeconds).Within(0.0001f));
            Assert.That(WeaponBasicAttackProfile.Create(expectedProfile).AttackIntervalTicks, Is.EqualTo(expectedIntervalTicks));
            Assert.That(expectedProfile.HasTrait(expectedTrait), Is.True,
                "The declared qualitative weapon profile trait must remain present.");
            Assert.That(expectedProfile.HasTrait(WeaponTraitId.GreaterRange), Is.EqualTo(weaponId == UmbraGreatswordId),
                "Only Umbra declares greater reach than the worn sword; no numeric reach is authored here.");
            Assert.That(expectedProfile.HasTrait(WeaponTraitId.GreaterKnockback), Is.EqualTo(weaponId == UmbraGreatswordId),
                "Only Umbra declares greater push than the worn sword; no numeric push is authored here.");
            if (additionalTrait.HasValue)
            {
                Assert.That(expectedProfile.HasTrait(additionalTrait.Value), Is.True,
                    "Umbra must retain both declared qualitative traits without adding numeric tuning.");
            }

            int groundLayer = RequiredLayer("Ground");
            int enemyLayer = RequiredLayer("Enemies");
            CreateFloor(groundLayer);
            CreatePlayer(
                out PlayerMotor2D motor,
                out ItemCollector2D collector,
                out ActorId actorId,
                out PlayerInputBuffer input,
                new Vector2(0f, 0.75f));

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: 1 << groundLayer,
                enemyLayers: 1 << enemyLayer,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                weaponCatalog: weaponCatalog,
                skillCatalog: LoadSkillCatalog());

            Assert.That(motor.EquippedWeaponProfile, Is.SameAs(expectedProfile),
                "The equipped inventory item must resolve to its authoritative catalog profile on PlayerMotor2D.");
            Assert.That(motor.EquippedWeaponProfile.ItemId, Is.EqualTo(run.Inventory.Weapon.ItemId));
            Assert.That(motor.EquippedWeaponProfile.ClassId, Is.EqualTo(run.ClassId));
            Assert.That(motor.EquippedWeaponBasicAttackProfile.Rarity, Is.EqualTo(rarity));
            Assert.That(motor.EquippedWeaponBasicAttackProfile.UpgradeLevel, Is.EqualTo(upgradeLevel));

            Slime2D damageReceiver = CreateSlimeDamageReceiver(
                motor,
                groundLayer,
                enemyLayer,
                new Vector2(1f, motor.transform.position.y + 0.9f));
            Physics2D.SyncTransforms();

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True,
                "A simulated Attack input must start the composed player's melee attack.");
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1));
            input.Release(PlayerAction.Attack);

            while (motor.SimulationTick < attackStartTick + 6)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(motor.CurrentMeleeHitWindow, Is.Not.Null,
                "The weapon profile must open the real six-tick melee impact window.");
            Assert.That(motor.CurrentMeleeHitWindow.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));

            for (int tick = 0; tick < 12 && damageReceiver.MeleeImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(damageReceiver.MeleeImpactCount, Is.EqualTo(1),
                "Physics2D overlap must queue and resolve one impact on the real Slime damage receiver.");
            Assert.That(damageReceiver.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth - (expectedHitDamage ?? expectedDamage)),
                "The first resolved hit must apply exactly the equipped weapon's catalog damage.");
            Assert.That(motor.MeleeImpactsQueuedCount, Is.EqualTo(1));

            while (motor.SimulationTick < attackStartTick + expectedIntervalTicks - 2)
            {
                yield return new WaitForFixedUpdate();
            }

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + expectedIntervalTicks - 1));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.False,
                "The equipped weapon must reject an attack one tick before its catalog cadence.");
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1),
                "A rejected input must not count as an attack activation.");

            input.Release(PlayerAction.Attack);
            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + expectedIntervalTicks));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True,
                "The equipped weapon must accept its next attack exactly at its catalog cadence.");
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));
            Assert.That(motor.CurrentMeleeAttack.DamageMultiplier, Is.EqualTo(1m),
                "The rejected pre-cadence input must not advance Bellator's accepted-attack sequence.");
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(2));

            for (int tick = 0; tick < 12 && damageReceiver.MeleeImpactCount < 2; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(damageReceiver.MeleeImpactCount, Is.EqualTo(2),
                "A later attack with a fresh hit ID must resolve through the same real damage receiver.");
            Assert.That(damageReceiver.CurrentHealth, Is.EqualTo(Mathf.Max(0, Slime2D.MaximumHealth - (expectedHitDamage ?? expectedDamage) * 2)),
                "The second resolved hit must preserve the equipped weapon's damage at its accepted cadence.");
        }

        private void EquipWeapon(ExpeditionInventory inventory, string weaponId,
            ItemRarity rarity = ItemRarity.Common, int upgradeLevel = 0)
        {
            var stack = new InventoryItemStack(
                ItemInstanceId.New(),
                weaponId,
                rarity,
                upgradeLevel: upgradeLevel,
                quantity: 1);
            InventoryOperationOutcome collected = inventory.TryCollectItem(stack, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "The alternative Bellator weapon must enter the run inventory.");

            int backpackSlot = -1;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                if (inventory.GetBackpackSlot(index)?.InstanceId == stack.InstanceId)
                {
                    backpackSlot = index;
                    break;
                }
            }

            Assert.That(backpackSlot, Is.GreaterThanOrEqualTo(0), "The collected weapon must occupy a real backpack slot.");
            InventoryOperationOutcome equipped = inventory.EquipBackpackItem(
                backpackSlot,
                EquipmentSlot.Weapon,
                TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "The selected alternative weapon must equip through the inventory API.");
        }

        private void CreateFloor(int groundLayer)
        {
            var floor = Track(new GameObject("G01 Bellator weapon acceptance ground"));
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
        }

        private void CreatePlayer(
            out PlayerMotor2D motor,
            out ItemCollector2D collector,
            out ActorId actorId,
            out PlayerInputBuffer input,
            Vector2 position)
        {
            actorId = ActorId.New();
            input = new PlayerInputBuffer(actorId);
            var player = Track(new GameObject("G01 Bellator weapon acceptance player"));
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            motor = player.AddComponent<PlayerMotor2D>();
            collector = player.AddComponent<ItemCollector2D>();
        }

        private Slime2D CreateSlimeDamageReceiver(
            PlayerMotor2D player,
            int groundLayer,
            int enemyLayer,
            Vector2 position)
        {
            var enemy = Track(new GameObject("G01 Bellator weapon acceptance Slime receiver"));
            enemy.layer = enemyLayer;
            enemy.transform.position = position;
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = enemy.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            Slime2D receiver = enemy.AddComponent<Slime2D>();
            receiver.Initialize(ActorId.New(), player, 1 << groundLayer);

            // Hold the receiver in the melee overlap so the test isolates weapon damage and cadence.
            // Its Slime damage state and direct-impact resolution remain live.
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            return receiver;
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static int RequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The " + layerName + " layer is required by the runtime acceptance fixture.");
            return layer;
        }

        private static WeaponCatalog LoadWeaponCatalog(ItemCatalog itemCatalog)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(source, Is.Not.Null, "The authoritative weapon catalog must be loaded before binding.");
            return WeaponCatalogJsonLoader.Load(source.text, itemCatalog);
        }

        private static SkillCatalog LoadSkillCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The authoritative Bellator skill catalog must be loaded before binding.");
            return SkillCatalogJsonLoader.Load(source.text);
        }
    }
}
