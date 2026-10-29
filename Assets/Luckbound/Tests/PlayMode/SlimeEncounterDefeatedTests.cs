using System;
using System.Collections;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SlimeEncounterDefeatedTests
    {
        [UnityTest]
        public IEnumerator DefeatedIsRaisedOnceForTheActorAfterResolvedLethalProjectileDamage()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            GameObject floor = CreateFloor("F09_SlimeDefeatedGround", groundLayer);
            PlayerFixture player = CreatePlayer("F09_SlimeDefeatedPlayer", new Vector2(20f, 1.5f), groundLayer);
            ProjectileFixture projectileFixture = CreateProjectileFixture("F09_SlimeDefeatedProjectile");
            ActorId slimeActorId = ActorId.New();
            int defeatedCount = 0;
            ActorId reportedActorId = default;
            SlimeFixture slimeFixture = CreateSlime(
                "F09_SlimeDefeatedActor",
                new Vector2(0f, 0.4f),
                slimeActorId,
                player.Motor,
                groundLayer,
                slime => slime.Defeated += actorId =>
                {
                    defeatedCount++;
                    reportedActorId = actorId;
                });

            Assert.That(slimeFixture.Slime.ActorId, Is.EqualTo(slimeActorId));
            Assert.That(defeatedCount, Is.Zero, "Initialization must not publish a defeat.");
            Physics2D.SyncTransforms();

            SpawnProjectile(projectileFixture, player.Motor.ActorId, 1m);
            for (int tick = 0; tick < 30 && slimeFixture.Slime.ProjectileImpactCount < 1; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(slimeFixture.Slime.ProjectileImpactCount, Is.EqualTo(1));
            Assert.That(slimeFixture.Slime.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth - 1));
            Assert.That(defeatedCount, Is.Zero, "A resolved nonlethal impact must not publish a defeat.");

            SpawnProjectile(projectileFixture, player.Motor.ActorId, Slime2D.MaximumHealth - 1m);
            for (int tick = 0; tick < 30 && !slimeFixture.Slime.IsDead; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(slimeFixture.Slime.IsDead, Is.True);
            Assert.That(slimeFixture.Slime.CurrentHealth, Is.Zero);
            Assert.That(slimeFixture.Slime.ProjectileImpactCount, Is.EqualTo(2));
            Assert.That(defeatedCount, Is.EqualTo(1));
            Assert.That(reportedActorId, Is.EqualTo(slimeActorId));

            for (int tick = 0; tick < 3; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(defeatedCount, Is.EqualTo(1), "Later physics ticks must not repeat the defeat signal.");

            SlimeFixture livingSlime = CreateSlime(
                "F09_SlimeDespawnedAlive",
                new Vector2(-5f, 0.4f),
                ActorId.New(),
                player.Motor,
                groundLayer);
            int despawnedActorDefeatedCount = 0;
            livingSlime.Slime.Defeated += _ => despawnedActorDefeatedCount++;
            livingSlime.GameObject.SetActive(false);
            Assert.That(despawnedActorDefeatedCount, Is.Zero, "Disabling a living actor must not publish a defeat.");
            Object.Destroy(livingSlime.GameObject);
            Object.Destroy(slimeFixture.GameObject);
            yield return null;

            Assert.That(defeatedCount, Is.EqualTo(1), "Destroying the defeated actor must not publish another defeat.");
            Assert.That(despawnedActorDefeatedCount, Is.Zero, "Destroying a living actor must not publish a defeat.");

            projectileFixture.Scope.Dispose();
            projectileFixture.Destroy();
            player.Destroy();
            Object.Destroy(floor);
        }

        private static void SpawnProjectile(ProjectileFixture fixture, ActorId sourceActorId, decimal damage)
        {
            fixture.Pool.Spawn(
                new ProjectileDefinition(baseDamage: damage, speedUnitsPerSecond: 12m, lifetimeTicks: 120, maximumTargets: 1),
                EntityId.New(),
                sourceActorId,
                new Vector2(-2f, 0.4f),
                Vector2.right,
                1 << 0,
                fixture.Scope);
        }

        private static GameObject CreateFloor(string name, int groundLayer)
        {
            var floor = new GameObject(name);
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(60f, 1f);
            return floor;
        }

        private static PlayerFixture CreatePlayer(string name, Vector2 position, int groundLayer)
        {
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject(name);
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, 1 << groundLayer);
            return new PlayerFixture(player, motor);
        }

        private static SlimeFixture CreateSlime(
            string name,
            Vector2 position,
            ActorId actorId,
            PlayerMotor2D player,
            int groundLayer,
            Action<Slime2D> beforeInitialize = null)
        {
            var slimeObject = new GameObject(name);
            slimeObject.transform.position = position;
            Rigidbody2D body = slimeObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = slimeObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            Slime2D slime = slimeObject.AddComponent<Slime2D>();
            beforeInitialize?.Invoke(slime);
            slime.Initialize(actorId, player, 1 << groundLayer);
            return new SlimeFixture(slimeObject, slime);
        }

        private static ProjectileFixture CreateProjectileFixture(string name)
        {
            var poolObject = new GameObject(name + "_Pool");
            ProjectilePool2D pool = poolObject.AddComponent<ProjectilePool2D>();
            var prefab = new GameObject(name + "_Prefab");
            Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
            collider.radius = 0.12f;
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            pool.Initialize(prefab);

            var scopeObject = new GameObject(name + "_RoomScope");
            RoomProjectileScope scope = scopeObject.AddComponent<RoomProjectileScope>();
            scope.Initialize(EntityId.New(), pool);
            return new ProjectileFixture(poolObject, prefab, scopeObject, pool, scope);
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(GameObject gameObject, PlayerMotor2D motor)
            {
                GameObject = gameObject;
                Motor = motor;
            }

            public GameObject GameObject { get; }

            public PlayerMotor2D Motor { get; }

            public void Destroy()
            {
                Object.Destroy(GameObject);
            }
        }

        private sealed class SlimeFixture
        {
            public SlimeFixture(GameObject gameObject, Slime2D slime)
            {
                GameObject = gameObject;
                Slime = slime;
            }

            public GameObject GameObject { get; }

            public Slime2D Slime { get; }
        }

        private sealed class ProjectileFixture
        {
            public ProjectileFixture(
                GameObject poolObject,
                GameObject prefab,
                GameObject scopeObject,
                ProjectilePool2D pool,
                RoomProjectileScope scope)
            {
                PoolObject = poolObject;
                Prefab = prefab;
                ScopeObject = scopeObject;
                Pool = pool;
                Scope = scope;
            }

            public GameObject PoolObject { get; }

            public GameObject Prefab { get; }

            public GameObject ScopeObject { get; }

            public ProjectilePool2D Pool { get; }

            public RoomProjectileScope Scope { get; }

            public void Destroy()
            {
                Object.Destroy(ScopeObject);
                Object.Destroy(PoolObject);
                Object.Destroy(Prefab);
            }
        }
    }
}
