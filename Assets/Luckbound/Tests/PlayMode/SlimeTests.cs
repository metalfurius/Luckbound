using System.Collections;
using System.Globalization;
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

namespace Luckbound.Tests.PlayMode
{
    public sealed class SlimeTests
    {
        [UnityTest]
        public IEnumerator SlimeDetectsTelegraphsForTwentySevenTicksAndHitsOnceWithItsLeap()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C05", "C05_SlimeTelegraphedLeap", 26457513))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateFloor("C05_LeapGround");
                PlayerFixture player = CreatePlayer("C05_LeapPlayer", position: new Vector2(0f, 1.5f), groundLayer);
                SlimeFixture slimeFixture = CreateSlime("C05_LeapSlime", new Vector2(2.5f, 0.4f), player.Motor, groundLayer);
                Physics2D.SyncTransforms();

                evidence.RecordInput(0, "PlayerCommand", "moveX=0; no combat actions");
                int chaseTicks = 0;
                while (chaseTicks < 100 && slimeFixture.Slime.State != SlimeBehaviorState.Telegraph)
                {
                    yield return new WaitForFixedUpdate();
                    chaseTicks++;
                }

                int warningAtStart = slimeFixture.Slime.TelegraphTicksRemaining;
                evidence.RecordEvent("chaseTicks=" + chaseTicks + "; telegraphTicks=" + warningAtStart +
                    "; slimeX=" + slimeFixture.GameObject.transform.position.x.ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Slime detects the player and chases into attack range",
                    slimeFixture.Slime.State == SlimeBehaviorState.Telegraph && warningAtStart == Slime2D.TelegraphDurationTicks,
                    "Telegraph with 27 ticks remaining", slimeFixture.Slime.State + "; remaining=" + warningAtStart);

                for (int tick = 0; tick < Slime2D.TelegraphDurationTicks - 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool warningHeldForTwentySixTicks = slimeFixture.Slime.State == SlimeBehaviorState.Telegraph &&
                    slimeFixture.Slime.TelegraphTicksRemaining == 1;
                yield return new WaitForFixedUpdate();
                bool leapStartedAfterWarning = slimeFixture.Slime.State == SlimeBehaviorState.Attack &&
                    slimeFixture.Slime.LeapAttackCount == 1;

                evidence.RecordAssertion("Telegraph lasts exactly 27 ticks before the leap begins",
                    warningHeldForTwentySixTicks && leapStartedAfterWarning,
                    "Telegraph after 26 ticks; Attack after tick 27",
                    warningHeldForTwentySixTicks + "; state=" + slimeFixture.Slime.State +
                    "; attacks=" + slimeFixture.Slime.LeapAttackCount);

                int attackTicks = 0;
                while (attackTicks < Slime2D.LeapTimeoutTicks && player.Motor.CurrentHealth == 100)
                {
                    yield return new WaitForFixedUpdate();
                    attackTicks++;
                }

                evidence.RecordEvent("attackTicks=" + attackTicks + "; playerHealth=" + player.Motor.CurrentHealth +
                    "; appliedHits=" + slimeFixture.Slime.SuccessfulPlayerHitCount +
                    "; slimeState=" + slimeFixture.Slime.State);
                evidence.RecordAssertion("A physical leap collision applies one eight-point hit",
                    player.Motor.CurrentHealth == 100 - Slime2D.AttackDamage && slimeFixture.Slime.SuccessfulPlayerHitCount == 1,
                    "playerHealth=92; one applied hit", "playerHealth=" + player.Motor.CurrentHealth +
                    "; appliedHits=" + slimeFixture.Slime.SuccessfulPlayerHitCount);
                int exitCode = evidence.Complete();

                slimeFixture.Destroy();
                player.Destroy();
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C05 Slime leap scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator PhysicalProjectileDamageKillsTheSlimeAndDisablesItsCollider()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C05", "C05_SlimeProjectileDeath", 28284271))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateFloor("C05_DeathGround");
                PlayerFixture player = CreatePlayer("C05_DeathPlayer", position: new Vector2(20f, 1.5f), groundLayer);
                SlimeFixture slimeFixture = CreateSlime("C05_DeathSlime", new Vector2(0f, 0.4f), player.Motor, groundLayer);
                ProjectileFixture projectileFixture = CreateProjectileFixture("C05_SlimeProjectile");
                Physics2D.SyncTransforms();

                ProjectileImpactTarget2D impactTarget = slimeFixture.GameObject.GetComponent<ProjectileImpactTarget2D>();
                bool impactQueuedBeforeDamageResolution = false;
                Projectile2D projectile = projectileFixture.Pool.Spawn(
                    new ProjectileDefinition(baseDamage: 24m, speedUnitsPerSecond: 12m, lifetimeTicks: 120, maximumTargets: 1),
                    EntityId.New(),
                    player.Motor.ActorId,
                    new Vector2(-2f, 0.4f),
                    Vector2.right,
                    1 << 0,
                    projectileFixture.Scope);
                projectile.Impacted += impact => impactQueuedBeforeDamageResolution =
                    impact.TargetActorId == slimeFixture.Slime.ActorId &&
                    impactTarget.PendingImpactCount == 1 &&
                    slimeFixture.Slime.CurrentHealth == Slime2D.MaximumHealth;
                evidence.RecordInput(0, "ProjectileSpawn", "right; damage=24; maximumTargets=1");

                for (int tick = 0; tick < 20 && !slimeFixture.Slime.IsDead; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool dead = slimeFixture.Slime.State == SlimeBehaviorState.Dead && slimeFixture.Slime.CurrentHealth == 0;
                bool hitboxDisabled = !slimeFixture.Collider.enabled && !slimeFixture.Body.simulated;
                bool impactWasResolved = slimeFixture.Slime.ProjectileImpactCount == 1;
                evidence.RecordEvent("slimeHealth=" + slimeFixture.Slime.CurrentHealth + "; state=" + slimeFixture.Slime.State +
                    "; appliedProjectileImpacts=" + slimeFixture.Slime.ProjectileImpactCount +
                    "; hitboxEnabled=" + slimeFixture.Collider.enabled + "; simulated=" + slimeFixture.Body.simulated +
                    "; projectileActive=" + projectile.gameObject.activeSelf);
                evidence.RecordAssertion("Projectile trigger queues impact before the Slime resolves damage on its next tick",
                    impactQueuedBeforeDamageResolution,
                    "impact queued; Slime still at 24 health during trigger callback",
                    "queuedBeforeResolution=" + impactQueuedBeforeDamageResolution +
                    "; queuedImpacts=" + impactTarget.PendingImpactCount + "; health=" + slimeFixture.Slime.CurrentHealth);
                evidence.RecordAssertion("The physical projectile is queued and resolved as Slime damage",
                    impactWasResolved && slimeFixture.Slime.CurrentHealth == 0,
                    "one applied impact; health=0", "impacts=" + slimeFixture.Slime.ProjectileImpactCount +
                    "; health=" + slimeFixture.Slime.CurrentHealth);
                evidence.RecordAssertion("Lethal damage enters Dead and removes physical collision",
                    dead && hitboxDisabled,
                    "Dead; collider disabled; Rigidbody2D not simulated",
                    slimeFixture.Slime.State + "; collider=" + slimeFixture.Collider.enabled +
                    "; simulated=" + slimeFixture.Body.simulated);
                int exitCode = evidence.Complete();

                projectileFixture.Scope.Dispose();
                slimeFixture.Destroy();
                projectileFixture.Destroy();
                player.Destroy();
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C05 Slime projectile death scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        private static GameObject CreateFloor(string name)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            var floor = new GameObject(name);
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(60f, 1f);
            return floor;
        }

        private static PlayerFixture CreatePlayer(string name, Vector2 position, int groundLayer)
        {
            var actorId = ActorId.New();
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
            return new PlayerFixture(player, motor, input);
        }

        private static SlimeFixture CreateSlime(string name, Vector2 position, PlayerMotor2D player, int groundLayer)
        {
            var slimeObject = new GameObject(name);
            slimeObject.transform.position = position;
            Rigidbody2D body = slimeObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = slimeObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            Slime2D slime = slimeObject.AddComponent<Slime2D>();
            slime.Initialize(ActorId.New(), player, 1 << groundLayer);
            return new SlimeFixture(slimeObject, body, collider, slime);
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
            public PlayerFixture(GameObject gameObject, PlayerMotor2D motor, PlayerInputBuffer input)
            {
                GameObject = gameObject;
                Motor = motor;
                Input = input;
            }

            public GameObject GameObject { get; }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }

            public void Destroy()
            {
                Object.Destroy(GameObject);
            }
        }

        private sealed class SlimeFixture
        {
            public SlimeFixture(GameObject gameObject, Rigidbody2D body, Collider2D collider, Slime2D slime)
            {
                GameObject = gameObject;
                Body = body;
                Collider = collider;
                Slime = slime;
            }

            public GameObject GameObject { get; }

            public Rigidbody2D Body { get; }

            public Collider2D Collider { get; }

            public Slime2D Slime { get; }

            public void Destroy()
            {
                Object.Destroy(GameObject);
            }
        }

        private sealed class ProjectileFixture
        {
            public ProjectileFixture(GameObject poolObject, GameObject prefab, GameObject scopeObject, ProjectilePool2D pool, RoomProjectileScope scope)
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
