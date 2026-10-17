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
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionPlayerRunBinderTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<RoomLifetimeScope> _rooms = new List<RoomLifetimeScope>();
        private Vector2 _previousGravity;
        private float _previousFixedDeltaTime;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _previousTimeScale = Time.timeScale;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            Physics2D.gravity = _previousGravity;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator BindsConfirmedBellatorRunEquipmentSkillsAndRace()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.DvergarRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                5,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills);

            AssertBoundRun(fixture, run, BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId,
                BellatorSkillLoadout.GuardId);
            Assert.That(fixture.Motor.IsBellator, Is.True);
            Assert.That(fixture.Motor.IsVenator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(5));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindsConfirmedVenatorRunEquipmentSkillsRaceAndRoomScopes()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.AlvariRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                3,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room);

            AssertBoundRun(fixture, run, VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId,
                VenatorSkillLoadout.RootTrapId);
            Assert.That(fixture.Motor.IsVenator, Is.True);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(3));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindsConfirmedMagusRunEquipmentSkillsRaceAndRoomScopes()
        {
            ExpeditionRunConfiguration run = CreateRun(
                MagusSkillLoadout.ClassId,
                ExpeditionRunConfiguration.OrciniRaceId,
                MagusSkillLoadout.FireballId,
                MagusSkillLoadout.FrostNovaId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                2,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room);

            AssertBoundRun(fixture, run, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId,
                MagusSkillLoadout.ArcaneSealId);
            Assert.That(fixture.Motor.IsMagus, Is.True);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsVenator, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(2));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsCrossClassWeaponProfileBeforeMutatingEitherActorComponent()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            var mismatchedProfile = new WeaponProfile(
                run.Inventory.Weapon.ItemId,
                BellatorSkillLoadout.ClassId,
                damage: 10,
                attackIntervalSeconds: 0.4f,
                traits: new[] { new WeaponTrait(WeaponTraitId.Balanced) });
            var mismatchedCatalog = new WeaponCatalog(new[] { mismatchedProfile });

            Assert.Throws<System.ArgumentException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                mismatchedCatalog,
                fixture.Skills));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsVenatorRunWithoutRoomProjectileDependenciesBeforeMutation()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);

            Assert.Throws<System.ArgumentNullException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsUninitializedProjectilePoolBeforeMutatingVenatorRun()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true, initializeProjectilePool: false);
            Assert.That(fixture.ProjectileScope.Pool, Is.SameAs(fixture.ProjectilePool));
            Assert.That(fixture.ProjectilePool.IsInitialized, Is.False);

            Assert.Throws<System.InvalidOperationException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        private ExpeditionRunConfiguration CreateRun(string classId, string raceId, string skill1, string skill2)
        {
            return ExpeditionRunConfigurationFactory.Create(classId, raceId, new[] { skill1, skill2 });
        }

        private BindingFixture CreatePlayer(
            ExpeditionRunConfiguration run,
            bool includeRoomProjectiles,
            bool initializeProjectilePool = true)
        {
            var fixture = new BindingFixture();
            fixture.ActorId = ActorId.New();
            fixture.Input = new PlayerInputBuffer(fixture.ActorId);

            TextAsset weaponSource = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(weaponSource, Is.Not.Null, "The authoritative Weapons.json resource must be available.");
            fixture.Weapons = WeaponCatalogJsonLoader.Load(weaponSource.text, run.Inventory.Catalog);
            TextAsset skillSource = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skillSource, Is.Not.Null, "The authoritative Skills.json resource must be available.");
            fixture.Skills = SkillCatalogJsonLoader.Load(skillSource.text);

            var player = Track(new GameObject("F09 Run Binder Player"));
            player.transform.position = new Vector2(0f, 2f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            fixture.Motor = player.AddComponent<PlayerMotor2D>();
            fixture.Collector = player.AddComponent<ItemCollector2D>();

            if (includeRoomProjectiles) CreateRoomProjectiles(fixture, initializeProjectilePool);
            return fixture;
        }

        private void CreateRoomProjectiles(BindingFixture fixture, bool initializeProjectilePool)
        {
            var roomObject = Track(new GameObject("F09 Run Binder Room Lifetime"));
            fixture.Room = roomObject.AddComponent<RoomLifetimeScope>();
            fixture.Room.Initialize(RoomInstanceId.New(), "room.test.f09.run_binder");
            _rooms.Add(fixture.Room);

            var poolObject = Track(new GameObject("F09 Run Binder Projectile Pool"));
            fixture.ProjectilePool = poolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = Track(new GameObject("F09 Run Binder Projectile Prefab"));
            projectilePrefab.AddComponent<Rigidbody2D>();
            projectilePrefab.AddComponent<CircleCollider2D>();
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            if (initializeProjectilePool) fixture.ProjectilePool.Initialize(projectilePrefab);

            var scopeObject = new GameObject("F09 Run Binder Room Projectiles");
            fixture.ProjectileScope = scopeObject.AddComponent<RoomProjectileScope>();
            fixture.ProjectileScope.Initialize(EntityId.New(), fixture.ProjectilePool);
            fixture.Room.OwnActor(scopeObject);
            fixture.Room.OwnResource(fixture.ProjectileScope);
        }

        private void AssertBoundRun(
            BindingFixture fixture,
            ExpeditionRunConfiguration run,
            string skill1,
            string skill2,
            string unselectedSkill)
        {
            Assert.That(fixture.Motor.ActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(fixture.Motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(fixture.Collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(fixture.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(fixture.Collector.Catalog, Is.SameAs(run.Inventory.Catalog));
            Assert.That(fixture.Motor.EquippedWeaponProfile, Is.SameAs(fixture.Weapons.Get(run.Inventory.Weapon.ItemId)));
            Assert.That(fixture.Motor.EquippedWeaponProfile.ClassId, Is.EqualTo(run.ClassId));
            Assert.That(fixture.Motor.EquippedWeaponProfile.ItemId, Is.EqualTo(run.Inventory.Weapon.ItemId));
            Assert.That(run.Inventory.Armor, Is.Not.Null, "The run owns its equipped armor.");
            Assert.That(run.Inventory.QuickConsumable, Is.Not.Null, "The run owns its quick consumable instance.");
            Assert.That(fixture.Collector.Inventory.Armor, Is.SameAs(run.Inventory.Armor));
            Assert.That(fixture.Collector.Inventory.QuickConsumable, Is.SameAs(run.Inventory.QuickConsumable));
            Assert.That(fixture.Motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(fixture.Motor.CombatStats.Defense, Is.GreaterThanOrEqualTo(0));
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, skill1), Is.True);
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, skill2), Is.True);
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, unselectedSkill), Is.False,
                "The third class skill must not enter the two-skill run loadout.");
        }

        private void AssertActorAndCollectorUnconfigured(BindingFixture fixture)
        {
            Assert.That(fixture.Motor.ActorId.IsValid, Is.False);
            Assert.That(fixture.Motor.CombatStats, Is.Null);
            Assert.That(fixture.Motor.RaceId, Is.Null);
            Assert.That(fixture.Motor.EquippedWeaponProfile, Is.Null);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsVenator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Collector.Inventory, Is.Null);
            Assert.That(fixture.Collector.Catalog, Is.Null);
            Assert.That(fixture.Collector.RaceId, Is.Null);
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
        }

        private static bool IsSkillReady(PlayerMotor2D motor, string classId, string skillId)
        {
            if (classId == BellatorSkillLoadout.ClassId) return motor.IsBellatorSkillReady(skillId);
            if (classId == VenatorSkillLoadout.ClassId) return motor.IsVenatorSkillReady(skillId);
            return motor.IsMagusSkillReady(skillId);
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

        private sealed class BindingFixture
        {
            public ActorId ActorId;
            public PlayerInputBuffer Input;
            public PlayerMotor2D Motor;
            public ItemCollector2D Collector;
            public WeaponCatalog Weapons;
            public SkillCatalog Skills;
            public ProjectilePool2D ProjectilePool;
            public RoomProjectileScope ProjectileScope;
            public RoomLifetimeScope Room;
        }
    }
}
