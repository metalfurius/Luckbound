using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using Luckbound.Tests.PlayMode.Venator;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode
{
    public sealed class VenatorWeaponRuntimeAcceptanceTests
    {
        private readonly List<WeaponFixture> _fixtures = new List<WeaponFixture>();
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _fixtures.Clear();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _fixtures.Count - 1; index >= 0; index--)
            {
                _fixtures[index].Destroy();
            }

            Time.timeScale = _previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShortBowDealsTenDamageAndFiresAgainAtTwentyFourTicks()
        {
            WeaponFixture fixture = CreateFixture("weapon.venator.short_bow");
            yield return SettlePlayer(fixture);
            VenatorTestImpactActor2D target = fixture.CreateTarget("Short Bow Target", new Vector2(4.5f, 1f), multipleColliders: true);
            Physics2D.SyncTransforms();

            yield return TapAttack(fixture);
            long firstAttackTick = fixture.Motor.SimulationTick;
            Assert.That(fixture.Motor.EquippedWeaponProfile.ItemId, Is.EqualTo("weapon.venator.short_bow"));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(target, 1, maximumTicks: 30);

            Assert.That(target.AppliedImpactCount, Is.EqualTo(1), "The simulated attack must travel through 2D physics into a real damage receiver.");
            Assert.That(target.CurrentHealth, Is.EqualTo(90), "The catalog short bow deals 10 damage.");
            Assert.That(target.DuplicateImpactCount, Is.Zero, "Sibling target colliders resolve the actor only once per shot.");
            Assert.That(fixture.Impacts.Select(impact => impact.BaseDamage), Is.EqualTo(new[] { 10m }));
            Assert.That(fixture.Impacts[0].TargetActorId, Is.EqualTo(target.ImpactTarget.ActorId));

            yield return WaitForTick(fixture, firstAttackTick + 22);
            yield return TapAttack(fixture);
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(firstAttackTick + 23));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1), "A shot at tick 23 is still on the 24-tick reload.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));

            yield return TapAttack(fixture);
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(firstAttackTick + 24));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(2), "The next shot is accepted on the exact ready tick.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(2));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(target, 2, maximumTicks: 30);

            Assert.That(target.AppliedImpactCount, Is.EqualTo(2));
            Assert.That(target.CurrentHealth, Is.EqualTo(80));
            Assert.That(target.DuplicateImpactCount, Is.Zero);
            Assert.That(fixture.Impacts.Select(impact => impact.BaseDamage), Is.EqualTo(new[] { 10m, 10m }));
        }

        [UnityTest]
        public IEnumerator LongBowDealsFourteenAndChargedEighteenPointTwoWithThirtyNineTickCadence()
        {
            WeaponFixture fixture = CreateFixture("weapon.venator.long_bow");
            yield return SettlePlayer(fixture);
            VenatorTestImpactActor2D target = fixture.CreateTarget("Long Bow Target", new Vector2(4.5f, 1f), multipleColliders: true);
            Physics2D.SyncTransforms();

            yield return TapAttack(fixture);
            long firstAttackTick = fixture.Motor.SimulationTick;
            Assert.That(fixture.Motor.EquippedWeaponProfile.ItemId, Is.EqualTo("weapon.venator.long_bow"));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(target, 1, maximumTicks: 30);
            Assert.That(target.CurrentHealth, Is.EqualTo(86), "The uncharged long bow shot deals 14 damage.");
            Assert.That(fixture.Impacts[0].BaseDamage, Is.EqualTo(14m));

            yield return WaitForTick(fixture, firstAttackTick + 37);
            yield return TapAttack(fixture);
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(firstAttackTick + 38));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1), "The long bow is unavailable one tick before its 39-tick cadence.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));

            yield return TapAttack(fixture);
            long secondAttackTick = fixture.Motor.SimulationTick;
            Assert.That(secondAttackTick, Is.EqualTo(firstAttackTick + 39));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(2), "The long bow becomes available on tick 39.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(2));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(target, 2, maximumTicks: 30);
            Assert.That(target.CurrentHealth, Is.EqualTo(72));
            Assert.That(fixture.Impacts[1].BaseDamage, Is.EqualTo(14m));

            yield return WaitForTick(fixture, secondAttackTick + 38);
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(secondAttackTick + 39));
            Assert.That(fixture.Motor.CurrentVenatorBasicAttack.IsCharging, Is.True,
                "Holding Attack starts the optional charge on the exact ready tick without spawning until release.");
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(2));
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(2));

            fixture.Input.Release(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(secondAttackTick + 40));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(3));
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(3));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(target, 3, maximumTicks: 30);

            Assert.That(target.AppliedImpactCount, Is.EqualTo(3));
            Assert.That(target.CurrentHealth, Is.EqualTo(54), "The receiver rounds 18.2 damage to 18 health; the projectile retains its decimal payload.");
            Assert.That(target.DuplicateImpactCount, Is.Zero);
            Assert.That(fixture.Impacts.Select(impact => impact.BaseDamage), Is.EqualTo(new[] { 14m, 14m, 18.2m }));
            Assert.That(fixture.Impacts.All(impact => impact.TargetActorId == target.ImpactTarget.ActorId), Is.True);
        }

        [UnityTest]
        public IEnumerator RuinsCrossbowDealsSixteenPiercesOneTargetAndFiresAgainAtFortyFiveTicks()
        {
            WeaponFixture fixture = CreateFixture("weapon.venator.ruins_crossbow");
            yield return SettlePlayer(fixture);
            VenatorTestImpactActor2D first = fixture.CreateTarget("Crossbow Target 1", new Vector2(2.6f, 1f), multipleColliders: true);
            VenatorTestImpactActor2D second = fixture.CreateTarget("Crossbow Target 2", new Vector2(4.2f, 1f), multipleColliders: true);
            VenatorTestImpactActor2D third = fixture.CreateTarget("Crossbow Target 3", new Vector2(5.8f, 1f), multipleColliders: true);
            Physics2D.SyncTransforms();

            yield return TapAttack(fixture);
            long firstAttackTick = fixture.Motor.SimulationTick;
            Assert.That(fixture.Motor.EquippedWeaponProfile.ItemId, Is.EqualTo("weapon.venator.ruins_crossbow"));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(first, 1, maximumTicks: 30);
            yield return WaitForAppliedImpacts(second, 1, maximumTicks: 30);
            yield return WaitForProjectileReturn(fixture, maximumTicks: 10);

            Assert.That(first.CurrentHealth, Is.EqualTo(84), "The ruins crossbow deals 16 damage to the first actor.");
            Assert.That(second.CurrentHealth, Is.EqualTo(84), "The one pierce allows the same bolt to hit one additional actor.");
            Assert.That(third.AppliedImpactCount, Is.Zero, "A crossbow bolt is limited to two targets total.");
            Assert.That(third.CurrentHealth, Is.EqualTo(100));
            Assert.That(first.DuplicateImpactCount + second.DuplicateImpactCount + third.DuplicateImpactCount, Is.Zero,
                "Sibling colliders do not consume extra target slots or duplicate damage.");
            Assert.That(fixture.Impacts.Count, Is.EqualTo(2));
            Assert.That(fixture.Impacts.All(impact => impact.BaseDamage == 16m), Is.True);
            Assert.That(fixture.Impacts.Count(impact => impact.TargetActorId == first.ImpactTarget.ActorId), Is.EqualTo(1));
            Assert.That(fixture.Impacts.Count(impact => impact.TargetActorId == second.ImpactTarget.ActorId), Is.EqualTo(1));
            Assert.That(fixture.Impacts.Any(impact => impact.TargetActorId == third.ImpactTarget.ActorId), Is.False);

            yield return WaitForTick(fixture, firstAttackTick + 43);
            yield return TapAttack(fixture);
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(firstAttackTick + 44));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1), "The crossbow remains on reload through tick 44.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));

            yield return TapAttack(fixture);
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(firstAttackTick + 45));
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(2), "The crossbow is ready at tick 45.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(2));
            fixture.ObserveActiveProjectile();
            yield return WaitForAppliedImpacts(first, 2, maximumTicks: 30);
            yield return WaitForAppliedImpacts(second, 2, maximumTicks: 30);
            yield return WaitForProjectileReturn(fixture, maximumTicks: 10);

            Assert.That(first.CurrentHealth, Is.EqualTo(68));
            Assert.That(second.CurrentHealth, Is.EqualTo(68));
            Assert.That(third.AppliedImpactCount, Is.Zero);
            Assert.That(third.CurrentHealth, Is.EqualTo(100));
            Assert.That(fixture.Impacts.Count, Is.EqualTo(4));
            Assert.That(fixture.Impacts.All(impact => impact.BaseDamage == 16m), Is.True);
            Assert.That(fixture.Impacts.Count(impact => impact.TargetActorId == first.ImpactTarget.ActorId), Is.EqualTo(2));
            Assert.That(fixture.Impacts.Count(impact => impact.TargetActorId == second.ImpactTarget.ActorId), Is.EqualTo(2));
            Assert.That(fixture.Impacts.Any(impact => impact.TargetActorId == third.ImpactTarget.ActorId), Is.False);
        }

        private WeaponFixture CreateFixture(string equippedWeaponId)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0));

            TextAsset itemJson = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            TextAsset weaponJson = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(itemJson, Is.Not.Null, "The authoritative Items.json resource must load.");
            Assert.That(weaponJson, Is.Not.Null, "The authoritative Weapons.json resource must load.");
            WeaponCatalog weaponCatalog = WeaponCatalogJsonLoader.Load(
                weaponJson.text,
                ItemCatalogJsonLoader.Load(itemJson.text));
            Assert.That(weaponCatalog.Count, Is.EqualTo(WeaponCatalogJsonLoader.ExpectedWeaponCount));
            WeaponProfile equippedWeapon = weaponCatalog.Get(equippedWeaponId);

            var floor = new GameObject("Venator Weapon Acceptance Ground");
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(40f, 1f);

            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject("Venator Weapon Acceptance Player");
            player.transform.position = new Vector2(0f, 1.5f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D playerCollider = player.AddComponent<CapsuleCollider2D>();
            playerCollider.direction = CapsuleDirection2D.Vertical;
            playerCollider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            LayerMask enemyLayers = 1 << enemyLayer;
            motor.Initialize(actorId, input, 1 << groundLayer, new CombatStats(100, 50, 0), enemyLayers);

            var projectilePoolObject = new GameObject("Venator Weapon Acceptance Projectile Pool");
            ProjectilePool2D projectilePool = projectilePoolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = new GameObject("Venator Weapon Acceptance Arrow Prefab");
            projectilePrefab.AddComponent<Rigidbody2D>();
            CircleCollider2D projectileCollider = projectilePrefab.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.12f;
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            projectilePool.Initialize(projectilePrefab);

            var projectileScopeObject = new GameObject("Venator Weapon Acceptance Room Projectiles");
            RoomProjectileScope projectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
            projectileScope.Initialize(EntityId.New(), projectilePool);

            var roomObject = new GameObject("Venator Weapon Acceptance Room");
            RoomLifetimeScope room = roomObject.AddComponent<RoomLifetimeScope>();
            room.Initialize(RoomInstanceId.New(), "room.test.venator.weapons");
            room.OwnActor(projectileScopeObject);
            room.OwnResource(projectileScope);

            motor.ConfigureVenator(
                new[] { VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.RootTrapId },
                projectilePool,
                projectileScope,
                room,
                enemyLayers);
            motor.ConfigureEquippedWeapon(equippedWeapon);
            Physics2D.SyncTransforms();

            var fixture = new WeaponFixture(
                floor,
                player,
                projectilePoolObject,
                projectilePrefab,
                roomObject,
                motor,
                input,
                projectileScope,
                room,
                enemyLayer);
            _fixtures.Add(fixture);
            return fixture;
        }

        private static IEnumerator SettlePlayer(WeaponFixture fixture)
        {
            for (int tick = 0; tick < 10 && !fixture.Motor.IsGrounded; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsGrounded, Is.True, "The projectile test must start after the motor settles on its physical floor.");
        }

        private static IEnumerator TapAttack(WeaponFixture fixture)
        {
            fixture.Input.Press(PlayerAction.Attack);
            fixture.Input.Release(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
        }

        private static IEnumerator WaitForTick(WeaponFixture fixture, long targetTick)
        {
            while (fixture.Motor.SimulationTick < targetTick)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(targetTick), "Fixed-step input timing must remain on the expected simulation tick.");
        }

        private static IEnumerator WaitForAppliedImpacts(VenatorTestImpactActor2D target, int expectedCount, int maximumTicks)
        {
            for (int tick = 0; tick < maximumTicks && target.AppliedImpactCount < expectedCount; tick++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitForProjectileReturn(WeaponFixture fixture, int maximumTicks)
        {
            for (int tick = 0; tick < maximumTicks && fixture.ProjectileScope.ActiveProjectileCount > 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero, "The bounded-target bolt must return to its room pool after impacts.");
        }

        private sealed class WeaponFixture
        {
            private readonly GameObject _floor;
            private readonly GameObject _player;
            private readonly GameObject _projectilePoolObject;
            private readonly GameObject _projectilePrefab;
            private readonly GameObject _roomObject;
            private readonly int _enemyLayer;
            private bool _destroyed;

            public WeaponFixture(
                GameObject floor,
                GameObject player,
                GameObject projectilePoolObject,
                GameObject projectilePrefab,
                GameObject roomObject,
                PlayerMotor2D motor,
                PlayerInputBuffer input,
                RoomProjectileScope projectileScope,
                RoomLifetimeScope room,
                int enemyLayer)
            {
                _floor = floor;
                _player = player;
                _projectilePoolObject = projectilePoolObject;
                _projectilePrefab = projectilePrefab;
                _roomObject = roomObject;
                Motor = motor;
                Input = input;
                ProjectileScope = projectileScope;
                Room = room;
                _enemyLayer = enemyLayer;
            }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }

            public RoomProjectileScope ProjectileScope { get; }

            public RoomLifetimeScope Room { get; }

            public List<ProjectileImpact> Impacts { get; } = new List<ProjectileImpact>();

            public VenatorTestImpactActor2D CreateTarget(string objectName, Vector2 position, bool multipleColliders)
            {
                var target = new GameObject(objectName);
                target.transform.position = position;
                VenatorTestImpactActor2D impactActor = target.AddComponent<VenatorTestImpactActor2D>();
                impactActor.Initialize(ActorId.New(), 100, _enemyLayer, multipleColliders);
                Room.OwnActor(target);
                return impactActor;
            }

            public void ObserveActiveProjectile()
            {
                Projectile2D[] active = ProjectileScope.GetComponentsInChildren<Projectile2D>(includeInactive: false);
                Assert.That(active.Length, Is.EqualTo(1), "Each basic attack must create one active catalog-configured projectile.");
                active[0].Impacted += Impacts.Add;
            }

            public void Destroy()
            {
                if (_destroyed) return;
                _destroyed = true;
                if (Room != null) Room.Dispose();
                Object.Destroy(_player);
                Object.Destroy(_floor);
                Object.Destroy(_projectilePoolObject);
                Object.Destroy(_projectilePrefab);
                Object.Destroy(_roomObject);
            }
        }
    }
}
