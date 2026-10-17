using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionBellatorRunBinderTests
    {
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
        public IEnumerator DvergarRaceFromRunConfigurationReachesMotorAndPhysicalWorldPickup()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.DvergarRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId },
                healthUpgradeLevel: 2,
                energyUpgradeLevel: 1);
            ItemCatalog itemCatalog = run.Inventory.Catalog;
            SkillCatalog skillCatalog = LoadSkillCatalog();
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreateFloor();
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector, allowMovement: true);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 7,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                weaponCatalog: LoadWeaponCatalog(itemCatalog),
                skillCatalog: skillCatalog);

            Assert.That(motor.IsBellator, Is.True);
            Assert.That(motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.GrantsDvergarBonus, Is.True);
            Assert.That(collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(collector.Catalog, Is.SameAs(itemCatalog));
            Assert.That(motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CombatStats.MaximumEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CurrentHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CombatStats.Defense, Is.EqualTo(7), "Defense is passed as an independent calculated value.");

            var pickupObject = Track(new GameObject("E09 Dvergar physical ore pickup"));
            pickupObject.transform.position = new Vector2(1f, 0.5f);
            Rigidbody2D pickupBody = pickupObject.AddComponent<Rigidbody2D>();
            pickupBody.bodyType = RigidbodyType2D.Kinematic;
            pickupBody.gravityScale = 0f;
            CircleCollider2D pickupCollider = pickupObject.AddComponent<CircleCollider2D>();
            pickupCollider.radius = 0.25f;
            WorldMaterialPickup2D pickup = pickupObject.AddComponent<WorldMaterialPickup2D>();
            pickup.Initialize(MaterialKind.Ore, 5, itemCatalog);

            Physics2D.SyncTransforms();
            for (int tick = 0; tick < 30 && !motor.IsGrounded; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.True, "The player must stand on real ground before moving into the pickup.");

            input.SetMoveX(1f);
            for (int tick = 0; tick < 60 && !pickup.IsConsumed; tick++)
                yield return new WaitForFixedUpdate();
            input.SetMoveX(0f);

            Assert.That(pickup.IsConsumed, Is.True, "The real world pickup trigger must be consumed by the configured collector.");
            Assert.That(motor.transform.position.x, Is.GreaterThan(0f), "Simulated movement input must carry the real player into pickup range.");
            Assert.That(run.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(6));
            Assert.That(run.Inventory.GetWorldMaterialRemainder(MaterialKind.Ore), Is.Zero);
            Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.Collected));
        }

        [UnityTest]
        public IEnumerator EquippedBellatorTrailSaberProfileControlsRealAttackDamageAndCadence()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });
            const string trailSaberId = "weapon.bellator.trail_saber";
            var trailSaber = new InventoryItemStack(
                ItemInstanceId.New(), trailSaberId, ItemRarity.Common, upgradeLevel: 0, quantity: 1);
            InventoryOperationOutcome collected = run.Inventory.TryCollectItem(trailSaber, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "The alternative Bellator weapon must enter the run inventory.");

            int trailSaberSlot = -1;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                if (run.Inventory.GetBackpackSlot(index)?.InstanceId == trailSaber.InstanceId)
                {
                    trailSaberSlot = index;
                    break;
                }
            }
            Assert.That(trailSaberSlot, Is.GreaterThanOrEqualTo(0));
            InventoryOperationOutcome equipped = run.Inventory.EquipBackpackItem(
                trailSaberSlot, EquipmentSlot.Weapon, TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "The alternative Bellator weapon must be equipped in the run inventory.");
            Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo(trailSaberId));

            ItemCatalog itemCatalog = run.Inventory.Catalog;
            WeaponCatalog weaponCatalog = LoadWeaponCatalog(itemCatalog);
            WeaponProfile expectedProfile = weaponCatalog.Get(trailSaberId);
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                weaponCatalog: weaponCatalog,
                skillCatalog: LoadSkillCatalog());

            Assert.That(motor.EquippedWeaponProfile, Is.SameAs(expectedProfile));
            Assert.That(motor.EquippedWeaponProfile.ItemId, Is.EqualTo(trailSaberId));
            Assert.That(motor.EquippedWeaponProfile.Damage, Is.EqualTo(9));
            Assert.That(motor.EquippedWeaponProfile.AttackIntervalSeconds, Is.EqualTo(0.33f).Within(0.0001f));

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True);
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(9m));
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1));
            input.Release(PlayerAction.Attack);

            while (motor.SimulationTick < attackStartTick + 6)
                yield return new WaitForFixedUpdate();
            Assert.That(motor.CurrentMeleeHitWindow, Is.Not.Null,
                "The equipped weapon's real melee hit window must open on the motor.");
            Assert.That(motor.CurrentMeleeHitWindow.BaseDamage, Is.EqualTo(9m));

            while (motor.SimulationTick < attackStartTick + 18)
                yield return new WaitForFixedUpdate();
            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + 19));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.False,
                "The trail saber must reject an attack one tick before its 20-tick cadence is ready.");
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1));

            input.Release(PlayerAction.Attack);
            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + 20));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True,
                "The trail saber must accept the next attack at its 20-tick cadence.");
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(9m));
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator WeaponProfileForAnotherClassIsRejectedBeforeEitherComponentIsConfigured()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });
            var mismatchedProfile = new WeaponProfile(
                run.Inventory.Weapon.ItemId,
                "class.venator",
                damage: 12,
                attackIntervalSeconds: 0.45f,
                traits: new[] { new WeaponTrait(WeaponTraitId.RapidCadence) });
            var mismatchedCatalog = new WeaponCatalog(new[] { mismatchedProfile });
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            Assert.Throws<System.ArgumentException>(() => ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: run.Inventory.Catalog,
                weaponCatalog: mismatchedCatalog,
                skillCatalog: LoadSkillCatalog()));

            Assert.That(motor.ActorId.IsValid, Is.False);
            Assert.That(motor.CombatStats, Is.Null);
            Assert.That(motor.EquippedWeaponProfile, Is.Null);
            Assert.That(collector.Inventory, Is.Null);
            Assert.That(collector.Catalog, Is.Null);

            TextAsset itemSource = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(itemSource, Is.Not.Null);
            ItemCatalog separateItemCatalog = ItemCatalogJsonLoader.Load(itemSource.text);
            Assert.Throws<System.ArgumentException>(() => ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: separateItemCatalog,
                weaponCatalog: LoadWeaponCatalog(separateItemCatalog),
                skillCatalog: LoadSkillCatalog()),
                "Composition must use the same item catalog owned by the run inventory.");
            Assert.That(motor.ActorId.IsValid, Is.False);
            Assert.That(collector.Inventory, Is.Null);

            InventoryItemStack equippedBefore = run.Inventory.Weapon;
            InventoryItemStack backpackBefore = run.Inventory.GetBackpackSlot(0);
            motor.Initialize(actorId, input, GroundLayers(), new CombatStats(run.StartingHealth, run.StartingEnergy, 0));
            WeaponProfile existingProfile = LoadWeaponCatalog(run.Inventory.Catalog).Get(equippedBefore.ItemId);
            motor.ConfigureEquippedWeapon(existingProfile);
            Assert.Throws<System.ArgumentException>(() => motor.ConfigureEquippedWeapon(run, mismatchedCatalog),
                "The run-based motor seam must reject a profile whose class differs from the equipped inventory.");
            Assert.That(motor.EquippedWeaponProfile, Is.SameAs(existingProfile),
                "A rejected run must not partially replace the motor's already valid weapon.");
            Assert.That(run.Inventory.Weapon, Is.SameAs(equippedBefore));
            Assert.That(run.Inventory.GetBackpackSlot(0), Is.SameAs(backpackBefore));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlvariRunSkillChoiceUsesItsDiscountedBellatorKitCost()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.AlvariRaceId,
                new[] { BellatorSkillLoadout.GuardId, BellatorSkillLoadout.ChargeId },
                healthUpgradeLevel: 1,
                energyUpgradeLevel: 2);
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 3,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: run.Inventory.Catalog,
                weaponCatalog: LoadWeaponCatalog(run.Inventory.Catalog),
                skillCatalog: LoadSkillCatalog());

            Assert.That(motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CombatStats.MaximumEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CurrentHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy));

            input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 12 && motor.BellatorGuardActivationCount == 0; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(motor.BellatorGuardActivationCount, Is.EqualTo(1),
                "Skill1 must come from the Alvari run's selected Bellator kit.");
            Assert.That(motor.LastBellatorCombatResult.SkillOutcome.EnergySpent, Is.EqualTo(11),
                "The 12 EN Guard cost is discounted by Alvari's configured race rule.");
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy - 11));
        }

        [UnityTest]
        public IEnumerator NonBellatorRunIsRejectedWithoutPartiallyConfiguringEitherComponent()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                "class.venator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.venator.piercing_arrow", "skill.venator.triple_shot" });
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            Assert.Throws<System.ArgumentException>(() => ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: run.Inventory.Catalog,
                weaponCatalog: LoadWeaponCatalog(run.Inventory.Catalog),
                skillCatalog: LoadSkillCatalog()));

            Assert.That(motor.ActorId.IsValid, Is.False);
            Assert.That(motor.CombatStats, Is.Null);
            Assert.That(motor.IsBellator || motor.IsVenator || motor.IsMagus, Is.False);
            Assert.That(collector.Inventory, Is.Null);
            Assert.That(collector.Catalog, Is.Null);
            Assert.That(collector.RaceId, Is.Null);
            Assert.That(collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        private void CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector, bool allowMovement = false)
        {
            var player = Track(new GameObject("E09 Bellator run player"));
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            player.transform.position = allowMovement ? new Vector2(0f, 0.75f) : Vector2.zero;
            body.gravityScale = allowMovement ? 3f : 0f;
            body.constraints = allowMovement ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.75f, 1.5f);
            motor = player.AddComponent<PlayerMotor2D>();
            collector = player.AddComponent<ItemCollector2D>();
        }

        private void CreateFloor()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by PlayerMotor2D.");
            var floor = Track(new GameObject("E09 Bellator run ground"));
            floor.layer = layer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static int GroundLayers()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by PlayerMotor2D.");
            return 1 << layer;
        }

        private static SkillCatalog LoadSkillCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The accepted Bellator skill catalog must be loaded before binding.");
            return SkillCatalogJsonLoader.Load(source.text);
        }

        private static WeaponCatalog LoadWeaponCatalog(ItemCatalog itemCatalog)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(source, Is.Not.Null, "The authoritative weapon catalog must be loaded before binding.");
            return WeaponCatalogJsonLoader.Load(source.text, itemCatalog);
        }
    }
}
