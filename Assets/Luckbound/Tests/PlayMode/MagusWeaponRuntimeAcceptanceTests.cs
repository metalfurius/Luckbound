using System;
using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode
{
    public sealed class MagusWeaponRuntimeAcceptanceTests
    {
        private readonly List<WeaponFixture> _fixtures = new List<WeaponFixture>();
        private Vector2 _previousGravity;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousTimeScale = Time.timeScale;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _fixtures.Count - 1; index >= 0; index--)
            {
                _fixtures[index].Dispose();
            }

            _fixtures.Clear();
            Physics2D.gravity = _previousGravity;
            Time.timeScale = _previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmberStaffCatalogProfileDrivesInputAttackAndFireImpact()
        {
            yield return AssertWeaponRuntime(
                new ExpectedWeapon(
                    "weapon.magus.ember_staff",
                    damage: 12,
                    intervalTicks: 30,
                    element: "fire",
                    slowPercent: 0,
                    slowDurationTicks: 0,
                    defensePenetration: 0,
                    guardedDamage: 6));
        }

        [UnityTest]
        public IEnumerator FrostStaffCatalogProfileDrivesInputAttackAndTimedSlowImpact()
        {
            yield return AssertWeaponRuntime(
                new ExpectedWeapon(
                    "weapon.magus.frost_staff",
                    damage: 10,
                    intervalTicks: 27,
                    element: "frost",
                    slowPercent: 20,
                    slowDurationTicks: 45,
                    defensePenetration: 0,
                    guardedDamage: 4));
        }

        [UnityTest]
        public IEnumerator ShadowStaffCatalogProfileDrivesInputAttackAndDefensePenetrationImpact()
        {
            yield return AssertWeaponRuntime(
                new ExpectedWeapon(
                    "weapon.magus.shadow_staff",
                    damage: 14,
                    intervalTicks: 36,
                    element: "shadow",
                    slowPercent: 0,
                    slowDurationTicks: 0,
                    defensePenetration: 1,
                    guardedDamage: 9));
        }

        private IEnumerator AssertWeaponRuntime(ExpectedWeapon expected)
        {
            WeaponFixture fixture = CreateFixture(expected.ItemId);
            AssertWeaponProfile(fixture, expected.ItemId, expected.Damage, expected.IntervalTicks);
            Physics2D.SyncTransforms();

            for (int tick = 0; tick < 30 && (!fixture.Motor.IsGrounded || !fixture.Target.IsGuarding); tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsGrounded, Is.True, "The player must settle on the physical test floor.");
            Assert.That(fixture.Target.IsGuarding, Is.True, "The Huesoguardia must be using its real frontal guard.");
            Assert.That(fixture.Target.CombatStats.Defense, Is.EqualTo(Huesoguardia2D.BaseDefense));

            WeaponProfile equipped = fixture.Motor.EquippedWeaponProfile;
            Assert.That(equipped, Is.SameAs(fixture.ExpectedProfile));

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            MagusBasicAttackSnapshot attack = fixture.Motor.CurrentMagusBasicAttack;
            long attackStartTick = fixture.Motor.SimulationTick;
            fixture.Input.Release(PlayerAction.Attack);

            Assert.That(attack.WasStartedThisTick, Is.True, "The equipped weapon must start from simulated player input.");
            Assert.That(attack.BaseDamage, Is.EqualTo(expected.Damage));
            Assert.That(attack.ProjectileElement, Is.EqualTo(expected.Element));
            Assert.That(attack.Projectile, Is.Not.Null);
            Assert.That(attack.Projectile.BaseDamage, Is.EqualTo(expected.Damage));
            Assert.That(attack.Projectile.SpeedUnitsPerSecond, Is.EqualTo(10m));
            Assert.That(attack.Projectile.LifetimeTicks, Is.EqualTo(180));
            Assert.That(attack.Projectile.MaximumTargets, Is.EqualTo(1));
            Assert.That(attack.Projectile.Element, Is.EqualTo(expected.Element));
            Assert.That(attack.Projectile.DefensePenetration, Is.EqualTo(expected.DefensePenetration));
            Assert.That(attack.Projectile.SlowPercent, Is.EqualTo(expected.SlowPercent));
            Assert.That(attack.Projectile.SlowDurationTicks, Is.EqualTo(expected.SlowDurationTicks));
            Assert.That(fixture.Motor.MagusBasicAttackActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.MagusBasicAttackProjectileSpawnCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.MeleeAttackActivationCount, Is.Zero,
                "A Magus basic attack must not fall through to Bellator melee.");

            Projectile2D projectile = fixture.ProjectileScope.GetComponentInChildren<Projectile2D>(includeInactive: true);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.IsPooled, Is.False);
            Assert.That(projectile.Element, Is.EqualTo(expected.Element),
                "The spawned physical projectile must retain the weapon's element.");

            bool physicalImpactObserved = false;
            ProjectileImpact physicalImpact = default;
            projectile.Impacted += impact =>
            {
                physicalImpactObserved = true;
                physicalImpact = impact;
            };

            for (int tick = 0; tick < 60 && fixture.Target.ProjectileImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(physicalImpactObserved, Is.True, "The shot must contact the enemy through Unity 2D physics.");
            Assert.That(physicalImpact.SourceActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(physicalImpact.TargetActorId, Is.EqualTo(fixture.Target.ActorId));
            Assert.That(physicalImpact.BaseDamage, Is.EqualTo(expected.Damage));
            Assert.That(physicalImpact.DefensePenetration, Is.EqualTo(expected.DefensePenetration));
            Assert.That(physicalImpact.SlowPercent, Is.EqualTo(expected.SlowPercent));
            Assert.That(physicalImpact.SlowDurationTicks, Is.EqualTo(expected.SlowDurationTicks));
            Assert.That(fixture.Target.IsGuarding, Is.True,
                "The impact must resolve against the enemy's real guarded defense state.");
            Assert.That(fixture.Target.ProjectileImpactCount, Is.EqualTo(1),
                "The real enemy damage receiver must resolve one physical projectile impact.");
            Assert.That(fixture.Target.CurrentHealth, Is.EqualTo(Huesoguardia2D.MaximumHealth - expected.GuardedDamage),
                "Health must reflect weapon damage after the guarded defense and weapon penetration are resolved.");

            MagusStatusEffectTarget2D status = fixture.Target.GetComponent<MagusStatusEffectTarget2D>();
            if (expected.SlowPercent > 0)
            {
                Assert.That(status, Is.Not.Null);
                Assert.That(status.IsInitialized, Is.True);
                Assert.That(status.SlowStartsAtTick, Is.GreaterThanOrEqualTo(0));
                Assert.That(status.SlowEndsAtTick - status.SlowStartsAtTick, Is.EqualTo(expected.SlowDurationTicks));
                Assert.That(status.GetMovementSpeedMultiplierAt(status.SlowStartsAtTick), Is.EqualTo(0.8m));
                Assert.That(status.GetMovementSpeedMultiplierAt(status.SlowEndsAtTick - 1), Is.EqualTo(0.8m));
                Assert.That(status.GetMovementSpeedMultiplierAt(status.SlowEndsAtTick), Is.EqualTo(1m),
                    "The 20% slow expires on the expected exclusive end tick.");
            }
            else
            {
                Assert.That(status, Is.Null,
                    "Weapons without a slow trait must not create a slow receiver on impact.");
            }

            long readyTick = attackStartTick + expected.IntervalTicks;
            while (fixture.Motor.SimulationTick < readyTick - 2)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(readyTick - 1));
            Assert.That(fixture.Motor.MagusBasicAttackActivationCount, Is.EqualTo(1),
                "A new attack one tick before the catalog cadence is ready must be rejected.");
            Assert.That(fixture.Motor.CurrentMagusBasicAttack.WasStartedThisTick, Is.False);

            fixture.Input.Release(PlayerAction.Attack);
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(readyTick));
            Assert.That(fixture.Motor.MagusBasicAttackActivationCount, Is.EqualTo(2),
                "The next attack must be accepted on the weapon's exact ready tick.");
            Assert.That(fixture.Motor.MagusBasicAttackProjectileSpawnCount, Is.EqualTo(2));
            Assert.That(fixture.Motor.CurrentMagusBasicAttack.WasStartedThisTick, Is.True);
            Assert.That(fixture.Motor.CurrentMagusBasicAttack.BaseDamage, Is.EqualTo(expected.Damage));
            fixture.Input.Release(PlayerAction.Attack);
        }

        private WeaponFixture CreateFixture(string weaponId)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by player physics.");
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0), "The Enemies layer is required by projectile impacts.");

            var fixture = new WeaponFixture();
            _fixtures.Add(fixture);

            var floor = new GameObject("G01 Magus Weapon Test Ground");
            fixture.Own(floor);
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(40f, 1f);

            ActorId actorId = ActorId.New();
            fixture.ActorId = actorId;
            var input = new PlayerInputBuffer(actorId);
            fixture.Input = input;

            var player = new GameObject("G01 Magus Weapon Test Player");
            fixture.Own(player);
            player.transform.position = new Vector2(0f, 0.75f);
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 3f;
            playerBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D playerCollider = player.AddComponent<CapsuleCollider2D>();
            playerCollider.direction = CapsuleDirection2D.Vertical;
            playerCollider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            LayerMask enemyLayers = 1 << enemyLayer;
            fixture.Motor = motor;

            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                MagusSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId });
            if (run.Inventory.Weapon.ItemId != weaponId)
                EquipWeapon(run.Inventory, weaponId);

            TextAsset weaponsJson = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(weaponsJson, Is.Not.Null, "The authoritative Weapons.json resource is required.");
            ItemCatalog items = run.Inventory.Catalog;
            WeaponCatalog weapons = WeaponCatalogJsonLoader.Load(weaponsJson.text, items);
            WeaponProfile equippedWeapon = weapons.Get(run.Inventory.Weapon.ItemId);
            Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo(weaponId));
            motor.Initialize(actorId, input, 1 << groundLayer,
                new CombatStats(run.StartingHealth, run.StartingEnergy, 0), enemyLayers, run.RaceId);
            motor.ConfigureEquippedWeapon(run, weapons);

            var poolObject = new GameObject("G01 Magus Weapon Test Projectile Pool");
            fixture.Own(poolObject);
            ProjectilePool2D projectilePool = poolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = new GameObject("G01 Magus Weapon Test Projectile Prefab");
            fixture.Own(projectilePrefab);
            projectilePrefab.AddComponent<Rigidbody2D>();
            CircleCollider2D projectileCollider = projectilePrefab.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.12f;
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            projectilePool.Initialize(projectilePrefab);

            var roomObject = new GameObject("G01 Magus Weapon Test Room Lifetime");
            fixture.Own(roomObject);
            RoomLifetimeScope room = roomObject.AddComponent<RoomLifetimeScope>();
            room.Initialize(RoomInstanceId.New(), "room.test.g01.magus.weapons");
            fixture.Room = room;

            var projectileScopeObject = new GameObject("G01 Magus Weapon Test Room Projectiles");
            fixture.Own(projectileScopeObject);
            RoomProjectileScope projectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
            projectileScope.Initialize(EntityId.New(), projectilePool);
            room.OwnActor(projectileScopeObject);
            room.OwnResource(projectileScope);
            fixture.ProjectileScope = projectileScope;

            var targetObject = new GameObject("G01 Magus Weapon Test Huesoguardia");
            fixture.Own(targetObject);
            targetObject.layer = enemyLayer;
            targetObject.transform.position = new Vector2(3.5f, 0.5f);
            Rigidbody2D targetBody = targetObject.AddComponent<Rigidbody2D>();
            targetBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D targetCollider = targetObject.AddComponent<CapsuleCollider2D>();
            targetCollider.direction = CapsuleDirection2D.Vertical;
            targetCollider.size = new Vector2(0.9f, 0.9f);
            Huesoguardia2D target = targetObject.AddComponent<Huesoguardia2D>();
            target.Initialize(ActorId.New(), motor, 1 << groundLayer);
            fixture.Target = target;

            motor.ConfigureMagus(
                run.SkillIds,
                projectilePool,
                projectileScope,
                room,
                enemyLayers);

            fixture.Run = run;
            fixture.ExpectedProfile = equippedWeapon;
            return fixture;
        }

        private static void EquipWeapon(ExpeditionInventory inventory, string weaponId)
        {
            var stack = new InventoryItemStack(ItemInstanceId.New(), weaponId, ItemRarity.Common, 0, 1);
            InventoryOperationOutcome collected = inventory.TryCollectItem(stack, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "The requested weapon must enter the run inventory.");

            int slot = -1;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                if (inventory.GetBackpackSlot(index)?.InstanceId == stack.InstanceId)
                {
                    slot = index;
                    break;
                }
            }

            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(inventory.EquipBackpackItem(slot, EquipmentSlot.Weapon, TransactionId.New()).Succeeded, Is.True,
                "The requested weapon must be equipped through ExpeditionInventory.");
        }

        private static void AssertWeaponProfile(WeaponFixture fixture, string itemId, int damage, int intervalTicks)
        {
            WeaponProfile equipped = fixture.Motor.EquippedWeaponProfile;
            Assert.That(equipped, Is.Not.Null);
            Assert.That(equipped, Is.SameAs(fixture.ExpectedProfile),
                "The motor must hold the exact profile resolved from the authoritative weapon catalog.");
            Assert.That(equipped.ItemId, Is.EqualTo(fixture.Run.Inventory.Weapon.ItemId));
            Assert.That(equipped.ItemId, Is.EqualTo(itemId));
            Assert.That(equipped.ClassId, Is.EqualTo(fixture.Run.ClassId));
            Assert.That(equipped.ClassId, Is.EqualTo(MagusSkillLoadout.ClassId));
            Assert.That(equipped.Damage, Is.EqualTo(damage));
            Assert.That(WeaponBasicAttackProfile.Create(equipped).AttackIntervalTicks, Is.EqualTo(intervalTicks));
        }

        private sealed class ExpectedWeapon
        {
            public ExpectedWeapon(
                string itemId,
                int damage,
                int intervalTicks,
                string element,
                int slowPercent,
                int slowDurationTicks,
                int defensePenetration,
                int guardedDamage)
            {
                ItemId = itemId;
                Damage = damage;
                IntervalTicks = intervalTicks;
                Element = element;
                SlowPercent = slowPercent;
                SlowDurationTicks = slowDurationTicks;
                DefensePenetration = defensePenetration;
                GuardedDamage = guardedDamage;
            }

            public string ItemId { get; }
            public int Damage { get; }
            public int IntervalTicks { get; }
            public string Element { get; }
            public int SlowPercent { get; }
            public int SlowDurationTicks { get; }
            public int DefensePenetration { get; }
            public int GuardedDamage { get; }
        }

        private sealed class WeaponFixture
        {
            private readonly List<GameObject> _ownedObjects = new List<GameObject>();
            private bool _disposed;

            public ActorId ActorId { get; set; }
            public PlayerInputBuffer Input { get; set; }
            public PlayerMotor2D Motor { get; set; }
            public Huesoguardia2D Target { get; set; }
            public RoomLifetimeScope Room { get; set; }
            public RoomProjectileScope ProjectileScope { get; set; }
            public ExpeditionRunConfiguration Run { get; set; }
            public WeaponProfile ExpectedProfile { get; set; }

            public void Own(GameObject gameObject)
            {
                _ownedObjects.Add(gameObject);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Room?.Dispose();
                for (int index = _ownedObjects.Count - 1; index >= 0; index--)
                {
                    if (_ownedObjects[index] != null) UnityEngine.Object.Destroy(_ownedObjects[index]);
                }
                _ownedObjects.Clear();
            }
        }
    }
}
