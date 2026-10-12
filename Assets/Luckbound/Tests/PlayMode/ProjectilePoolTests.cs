using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ProjectilePoolTests
    {
        [UnityTest]
        public IEnumerator PiercingProjectileHitsEachActorOnceAndReturnsAtItsTargetLimit()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C03", "C03_PiercingProjectile", 16180339))
            {
                Vector2 previousGravity = Physics2D.gravity;
                Physics2D.gravity = Vector2.zero;
                ProjectileFixture fixture = CreateFixture("C03_Piercing");
                GameObject firstTarget = CreateTarget("C03_TargetA", new Vector2(0f, 0f), 2);
                GameObject secondTarget = CreateTarget("C03_TargetB", new Vector2(1.5f, 0f), 1);
                var firstActorId = new ActorId(System.Guid.Parse("40000000-0000-4000-8000-000000000004"));
                var secondActorId = new ActorId(System.Guid.Parse("50000000-0000-4000-8000-000000000005"));
                firstTarget.GetComponent<ProjectileImpactTarget2D>().Initialize(firstActorId);
                secondTarget.GetComponent<ProjectileImpactTarget2D>().Initialize(secondActorId);
                Physics2D.SyncTransforms();

                var impacts = new List<ProjectileImpact>();
                ProjectileDefinition definition = new ProjectileDefinition(
                    baseDamage: 24m,
                    speedUnitsPerSecond: 12m,
                    lifetimeTicks: 120,
                    maximumTargets: 2);
                var sourceActorId = new ActorId(System.Guid.Parse("60000000-0000-4000-8000-000000000006"));
                Projectile2D projectile = fixture.Pool.Spawn(
                    definition,
                    EntityId.New(),
                    sourceActorId,
                    new Vector2(-2f, 0f),
                    Vector2.right,
                    1 << 0,
                    fixture.Scope);
                projectile.Impacted += impacts.Add;
                HitId projectileHitId = projectile.HitId;
                evidence.RecordInput(0, "ProjectileSpawn", "right; damage=24; maxTargets=2");

                for (int tick = 0; tick < 40 && fixture.Scope.ActiveProjectileCount > 0; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                int firstActorHits = 0;
                int secondActorHits = 0;
                Rigidbody2D projectileBody = projectile.GetComponent<Rigidbody2D>();
                evidence.RecordEvent("projectileAfterWait position=" + projectile.transform.position + "; velocity=" + projectileBody.linearVelocity +
                    "; ageTicks=" + projectile.AgeTicks + "; bodyType=" + projectileBody.bodyType + "; simulated=" + projectileBody.simulated);
                foreach (ProjectileImpact impact in impacts)
                {
                    if (impact.TargetActorId == firstActorId) firstActorHits++;
                    if (impact.TargetActorId == secondActorId) secondActorHits++;
                }

                bool sameHitId = impacts.Count == 2 && impacts[0].HitId == projectileHitId && impacts[1].HitId == projectileHitId;
                evidence.RecordEvent("impactCount=" + impacts.Count);
                evidence.RecordEvent("targetA hits=" + firstActorHits + "; targetB hits=" + secondActorHits);
                evidence.RecordAssertion("Projectile reaches two different actors", impacts.Count == 2,
                    "two impact records", impacts.Count.ToString(CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Sibling colliders do not duplicate an actor hit", firstActorHits == 1 && secondActorHits == 1,
                    "one impact per actor ID", "targetA=" + firstActorHits + "; targetB=" + secondActorHits);
                evidence.RecordAssertion("One projectile keeps one HitId across its pierces", sameHitId,
                    projectileHitId.ToString(), impacts.Count < 2 ? "fewer than two impacts" : impacts[0].HitId + "; " + impacts[1].HitId);
                evidence.RecordAssertion("Projectile returns after the target limit", fixture.Scope.ActiveProjectileCount == 0 && fixture.Pool.PooledCount == 1,
                    "scope=0, pool=1", "scope=" + fixture.Scope.ActiveProjectileCount + "; pool=" + fixture.Pool.PooledCount);
                int exitCode = evidence.Complete();

                fixture.Scope.Dispose();
                Object.Destroy(firstTarget);
                Object.Destroy(secondTarget);
                fixture.Destroy();
                Physics2D.gravity = previousGravity;
                yield return null;

                Assert.That(exitCode, Is.Zero, "C03 projectile impact scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator LeavingRoomReturnsProjectilesAndReuseCreatesANewHitId()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C03", "C03_RoomProjectileCleanup", 14142135))
            {
                ProjectileFixture fixture = CreateFixture("C03_RoomCleanup");
                var ownerId = new ActorId(System.Guid.Parse("70000000-0000-4000-8000-000000000007"));
                ProjectileDefinition definition = new ProjectileDefinition(8m, 6m, 120, 1);
                Projectile2D first = fixture.Pool.Spawn(definition, EntityId.New(), ownerId,
                    new Vector2(100f, 100f), Vector2.right, 1, fixture.Scope);
                HitId firstHitId = first.HitId;
                bool staleEventCalled = false;
                first.Impacted += _ => staleEventCalled = true;

                evidence.RecordEvent("scopeBeforeExit=" + fixture.Scope.ActiveProjectileCount + "; poolBeforeExit=" + fixture.Pool.PooledCount);
                evidence.RecordInput(0, "LeaveRoom", "dispose projectile scope");
                fixture.Scope.Dispose();
                bool inactiveAfterLeave = !first.gameObject.activeSelf;
                int pooledAfterLeave = fixture.Pool.PooledCount;
                evidence.RecordEvent("scopeAfterExit=" + fixture.Scope.ActiveProjectileCount + "; poolAfterExit=" + pooledAfterLeave);
                RoomProjectileScope secondScope = CreateScope(fixture.Pool, "C03_NextRoomScope");
                Projectile2D second = fixture.Pool.Spawn(definition, EntityId.New(), ownerId,
                    new Vector2(100f, 100f), Vector2.right, 1, secondScope);

                evidence.RecordAssertion("Room exit immediately returns its active projectile", inactiveAfterLeave && pooledAfterLeave == 1,
                    "projectile inactive; pool=1", inactiveAfterLeave + "; pool=" + pooledAfterLeave);
                evidence.RecordAssertion("Next room reuses the pooled object", first == second,
                    "same Projectile2D instance", first == second ? "same instance" : "different instance");
                evidence.RecordAssertion("Each rental receives a fresh HitId and event list", second.HitId != firstHitId && !staleEventCalled,
                    "new HitId and cleared prior subscribers", second.HitId + "; stale event=" + staleEventCalled);
                int exitCode = evidence.Complete();

                secondScope.Dispose();
                fixture.Destroy();
                Object.Destroy(secondScope.gameObject);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C03 room cleanup scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator ProjectileLifetimeReturnsItToThePool()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C03", "C03_ProjectileExpiry", 17320508))
            {
                ProjectileFixture fixture = CreateFixture("C03_Expiry");
                var ownerId = new ActorId(System.Guid.Parse("80000000-0000-4000-8000-000000000008"));
                Projectile2D projectile = fixture.Pool.Spawn(
                    new ProjectileDefinition(1m, 6m, lifetimeTicks: 2, maximumTargets: 1),
                    EntityId.New(), ownerId, new Vector2(100f, 100f), Vector2.right, 1, fixture.Scope);
                evidence.RecordEvent("projectileLifetimeTicks=2");

                for (int tick = 0; tick < 5 && fixture.Scope.ActiveProjectileCount > 0; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                evidence.RecordAssertion("Expired projectile returns to pool", fixture.Scope.ActiveProjectileCount == 0 && fixture.Pool.PooledCount == 1,
                    "scope=0, pool=1", "scope=" + fixture.Scope.ActiveProjectileCount + "; pool=" + fixture.Pool.PooledCount);
                int exitCode = evidence.Complete();

                fixture.Scope.Dispose();
                fixture.Destroy();
                yield return null;

                Assert.That(exitCode, Is.Zero, "C03 projectile expiry scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        private static ProjectileFixture CreateFixture(string name)
        {
            var poolObject = new GameObject(name + "_Pool");
            var pool = poolObject.AddComponent<ProjectilePool2D>();
            var prefab = new GameObject(name + "_Prefab");
            Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = prefab.AddComponent<CircleCollider2D>();
            collider.radius = 0.12f;
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            pool.Initialize(prefab);
            RoomProjectileScope scope = CreateScope(pool, name + "_Scope");
            return new ProjectileFixture(poolObject, prefab, pool, scope);
        }

        private static RoomProjectileScope CreateScope(ProjectilePool2D pool, string name)
        {
            var scopeObject = new GameObject(name);
            RoomProjectileScope scope = scopeObject.AddComponent<RoomProjectileScope>();
            scope.Initialize(EntityId.New(), pool);
            return scope;
        }

        private static GameObject CreateTarget(string name, Vector2 position, int colliderCount)
        {
            var target = new GameObject(name);
            target.transform.position = position;
            target.AddComponent<ProjectileImpactTarget2D>();
            for (int index = 0; index < colliderCount; index++)
            {
                var collider = target.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.45f, 0.7f);
                collider.offset = new Vector2(index * 0.08f, 0f);
            }

            return target;
        }

        private sealed class ProjectileFixture
        {
            public ProjectileFixture(GameObject poolObject, GameObject prefab, ProjectilePool2D pool, RoomProjectileScope scope)
            {
                PoolObject = poolObject;
                Prefab = prefab;
                Pool = pool;
                Scope = scope;
            }

            public GameObject PoolObject { get; }

            public GameObject Prefab { get; }

            public ProjectilePool2D Pool { get; }

            public RoomProjectileScope Scope { get; }

            public void Destroy()
            {
                Object.Destroy(Scope.gameObject);
                Object.Destroy(PoolObject);
                Object.Destroy(Prefab);
            }
        }
    }
}
