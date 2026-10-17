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
    public sealed class HuesoguardiaTests
    {
        [UnityTest]
        public IEnumerator ProjectileDamageDependsOnWhetherSourceIsInHuesoguardiaFrontArc()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C06", "C06_FrontGuardAndRearDamage", 16180339))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateFloor("C06_GuardGround");
                PlayerFixture player = CreatePlayer("C06_GuardPlayer", new Vector2(7f, 1.5f), groundLayer);
                HuesoguardiaFixture enemyFixture = CreateEnemy("C06_GuardEnemy", new Vector2(0f, 0.5f), player.Motor, groundLayer);
                ProjectileFixture projectileFixture = CreateProjectileFixture("C06_GuardProjectile");
                Physics2D.SyncTransforms();

                int chaseTicks = 0;
                while (chaseTicks < 20 && enemyFixture.Enemy.State != HuesoguardiaBehaviorState.Chase)
                {
                    yield return new WaitForFixedUpdate();
                    chaseTicks++;
                }

                evidence.RecordAssertion("Huesoguardia detects the nearby player and raises its frontal guard",
                    enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Chase && enemyFixture.Enemy.IsGuarding &&
                    enemyFixture.Enemy.FacingDirection > 0f,
                    "Chase, guarding, facing right toward the player",
                    enemyFixture.Enemy.State + "; guarding=" + enemyFixture.Enemy.IsGuarding +
                    "; facing=" + enemyFixture.Enemy.FacingDirection.ToString("F0", CultureInfo.InvariantCulture));
                evidence.RecordEvent("chaseTicks=" + chaseTicks + "; enemyX=" +
                    enemyFixture.GameObject.transform.position.x.ToString("F2", CultureInfo.InvariantCulture));

                FireProjectile(projectileFixture, player.Motor.ActorId, enemyFixture.Enemy,
                    sourceOffset: 1.5f, direction: Vector2.left);
                for (int tick = 0; tick < 20 && enemyFixture.Enemy.ProjectileImpactCount < 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                evidence.RecordAssertion("A physical projectile from the guarded front includes four bonus defense",
                    enemyFixture.Enemy.ProjectileImpactCount == 1 && enemyFixture.Enemy.CurrentHealth == 42 &&
                    enemyFixture.Enemy.CombatStats.Defense == Huesoguardia2D.BaseDefense,
                    "one impact; 48 to 42 health; base defense remains 2",
                    "impacts=" + enemyFixture.Enemy.ProjectileImpactCount + "; health=" + enemyFixture.Enemy.CurrentHealth +
                    "; baseDefense=" + enemyFixture.Enemy.CombatStats.Defense);

                FireProjectile(projectileFixture, player.Motor.ActorId, enemyFixture.Enemy,
                    sourceOffset: -1.5f, direction: Vector2.right);
                for (int tick = 0; tick < 20 && enemyFixture.Enemy.ProjectileImpactCount < 2; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                evidence.RecordEvent("projectileImpacts=" + enemyFixture.Enemy.ProjectileImpactCount +
                    "; healthAfterFrontAndRearHits=" + enemyFixture.Enemy.CurrentHealth +
                    "; guarding=" + enemyFixture.Enemy.IsGuarding);
                evidence.RecordAssertion("A physical projectile from behind bypasses the frontal bonus",
                    enemyFixture.Enemy.ProjectileImpactCount == 2 && enemyFixture.Enemy.CurrentHealth == 32 &&
                    enemyFixture.Enemy.IsGuarding,
                    "second impact deals 10; health=32 while guard remains active toward the player",
                    "impacts=" + enemyFixture.Enemy.ProjectileImpactCount + "; health=" + enemyFixture.Enemy.CurrentHealth +
                    "; guarding=" + enemyFixture.Enemy.IsGuarding);
                int exitCode = evidence.Complete();

                projectileFixture.Scope.Dispose();
                enemyFixture.Destroy();
                projectileFixture.Destroy();
                player.Destroy();
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C06 frontal guard scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator ShadowProjectileIgnoresExactlyOneDefensePointAgainstTheGuard()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            GameObject floor = CreateFloor("G01_Shadow_Ground");
            PlayerFixture player = CreatePlayer("G01_Shadow_Player", new Vector2(7f, 1.5f), groundLayer);
            HuesoguardiaFixture enemy = CreateEnemy("G01_Shadow_Huesoguardia", new Vector2(0f, 0.5f), player.Motor, groundLayer);
            ProjectileFixture projectile = CreateProjectileFixture("G01_Shadow_Projectile");
            Physics2D.SyncTransforms();

            for (int tick = 0; tick < 20 && !enemy.Enemy.IsGuarding; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(enemy.Enemy.IsGuarding, Is.True);
            FireProjectile(projectile, player.Motor.ActorId, enemy.Enemy,
                sourceOffset: 1.5f, direction: Vector2.left, defensePenetration: 1);
            for (int tick = 0; tick < 20 && enemy.Enemy.ProjectileImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(enemy.Enemy.ProjectileImpactCount, Is.EqualTo(1));
            Assert.That(enemy.Enemy.CurrentHealth, Is.EqualTo(41),
                "Twelve damage minus one ignored point of base-plus-guard defense deals seven.");

            projectile.Scope.Dispose();
            enemy.Destroy();
            projectile.Destroy();
            player.Destroy();
            Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HuesoguardiaCutMakesOneRealHitThenHasThirtyUnguardedRecoveryTicks()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C06", "C06_CutAndRecovery", 14142135))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateFloor("C06_CutGround");
                PlayerFixture player = CreatePlayer("C06_CutPlayer", new Vector2(0f, 1.5f), groundLayer);
                HuesoguardiaFixture enemyFixture = CreateEnemy("C06_CutEnemy", new Vector2(-2.5f, 0.5f), player.Motor, groundLayer);
                ProjectileFixture projectileFixture = CreateProjectileFixture("C06_RecoveryProjectile");
                Physics2D.SyncTransforms();

                int approachTicks = 0;
                while (approachTicks < 160 && enemyFixture.Enemy.State != HuesoguardiaBehaviorState.Telegraph)
                {
                    yield return new WaitForFixedUpdate();
                    approachTicks++;
                }

                int warningAtStart = enemyFixture.Enemy.TelegraphTicksRemaining;
                for (int tick = 0; tick < Huesoguardia2D.TelegraphDurationTicks - 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool warningHeld = enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Telegraph &&
                    enemyFixture.Enemy.TelegraphTicksRemaining == 1;
                yield return new WaitForFixedUpdate();
                bool attackStarted = enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Attack;
                evidence.RecordEvent("approachTicks=" + approachTicks + "; telegraphStart=" + warningAtStart +
                    "; attackStarted=" + attackStarted);
                evidence.RecordAssertion("The guarded cut warns for exactly 30 ticks before attacking",
                    warningAtStart == Huesoguardia2D.TelegraphDurationTicks && warningHeld && attackStarted,
                    "30 ticks at entry, one tick after 29, then Attack",
                    "start=" + warningAtStart + "; held=" + warningHeld + "; state=" + enemyFixture.Enemy.State);

                int attackTicks = 0;
                while (attackTicks < Huesoguardia2D.AttackDurationTicks && player.Motor.CurrentHealth == 100)
                {
                    yield return new WaitForFixedUpdate();
                    attackTicks++;
                }

                evidence.RecordAssertion("The advancing cut physically collides and deals ten damage once",
                    player.Motor.CurrentHealth == 90 && enemyFixture.Enemy.SuccessfulPlayerHitCount == 1,
                    "player health=90; one accepted hit",
                    "attackTicks=" + attackTicks + "; health=" + player.Motor.CurrentHealth +
                    "; acceptedHits=" + enemyFixture.Enemy.SuccessfulPlayerHitCount);

                int stateTicks = 0;
                while (stateTicks < 5 && enemyFixture.Enemy.State != HuesoguardiaBehaviorState.Recovery)
                {
                    yield return new WaitForFixedUpdate();
                    stateTicks++;
                }

                int recoveryAtStart = enemyFixture.Enemy.RecoveryTicksRemaining;
                evidence.RecordAssertion("Contact ends the cut and opens a 30-tick unguarded recovery",
                    enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Recovery && recoveryAtStart == Huesoguardia2D.RecoveryDurationTicks &&
                    !enemyFixture.Enemy.IsGuarding,
                    "Recovery, 30 ticks remaining, guard disabled",
                    enemyFixture.Enemy.State + "; remaining=" + recoveryAtStart + "; guarding=" + enemyFixture.Enemy.IsGuarding);

                FireProjectile(projectileFixture, player.Motor.ActorId, enemyFixture.Enemy,
                    sourceOffset: 3f, direction: Vector2.left);
                for (int tick = 0; tick < 20 && enemyFixture.Enemy.ProjectileImpactCount < 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                evidence.RecordAssertion("A front projectile deals unguarded base-defense damage during recovery",
                    enemyFixture.Enemy.ProjectileImpactCount == 1 && enemyFixture.Enemy.CurrentHealth == 38 &&
                    enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Recovery && !enemyFixture.Enemy.IsGuarding,
                    "front hit deals 10; enemy health=38 while still recovering without guard",
                    "impacts=" + enemyFixture.Enemy.ProjectileImpactCount + "; health=" + enemyFixture.Enemy.CurrentHealth +
                    "; state=" + enemyFixture.Enemy.State + "; guarding=" + enemyFixture.Enemy.IsGuarding);

                int remaining = enemyFixture.Enemy.RecoveryTicksRemaining;
                for (int tick = 0; tick < remaining - 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool recoveryHadOneTickRemaining = enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Recovery &&
                    enemyFixture.Enemy.RecoveryTicksRemaining == 1;
                yield return new WaitForFixedUpdate();
                evidence.RecordAssertion("Recovery returns to pursuit after exactly thirty ticks",
                    recoveryHadOneTickRemaining && enemyFixture.Enemy.State == HuesoguardiaBehaviorState.Chase &&
                    enemyFixture.Enemy.IsGuarding,
                    "one tick remains before Chase resumes",
                    "oneTickRemaining=" + recoveryHadOneTickRemaining + "; state=" + enemyFixture.Enemy.State +
                    "; guarding=" + enemyFixture.Enemy.IsGuarding);
                int exitCode = evidence.Complete();

                projectileFixture.Scope.Dispose();
                enemyFixture.Destroy();
                projectileFixture.Destroy();
                player.Destroy();
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C06 cut and recovery scenario failed. Evidence: " + evidence.ReportPath);
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

        private static HuesoguardiaFixture CreateEnemy(string name, Vector2 position, PlayerMotor2D player, int groundLayer)
        {
            var enemyObject = new GameObject(name);
            enemyObject.layer = LayerMask.NameToLayer("Enemies");
            enemyObject.transform.position = position;
            Rigidbody2D body = enemyObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = enemyObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.9f);
            Huesoguardia2D enemy = enemyObject.AddComponent<Huesoguardia2D>();
            enemy.Initialize(ActorId.New(), player, 1 << groundLayer);
            return new HuesoguardiaFixture(enemyObject, collider, enemy);
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

        private static void FireProjectile(
            ProjectileFixture fixture,
            ActorId sourceActorId,
            Huesoguardia2D enemy,
            float sourceOffset,
            Vector2 direction,
            int defensePenetration = 0)
        {
            float sourceX = enemy.transform.position.x + sourceOffset;
            fixture.Pool.Spawn(
                new ProjectileDefinition(
                    baseDamage: 12m,
                    speedUnitsPerSecond: 12m,
                    lifetimeTicks: 120,
                    maximumTargets: 1,
                    defensePenetration: defensePenetration),
                EntityId.New(),
                sourceActorId,
                new Vector2(sourceX, enemy.transform.position.y),
                direction,
                1 << LayerMask.NameToLayer("Enemies"),
                fixture.Scope);
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

        private sealed class HuesoguardiaFixture
        {
            public HuesoguardiaFixture(GameObject gameObject, Collider2D collider, Huesoguardia2D enemy)
            {
                GameObject = gameObject;
                Collider = collider;
                Enemy = enemy;
            }

            public GameObject GameObject { get; }

            public Collider2D Collider { get; }

            public Huesoguardia2D Enemy { get; }

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
