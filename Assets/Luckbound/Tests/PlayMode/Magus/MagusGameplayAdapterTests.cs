using System.Collections;
using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode.Magus
{
    public sealed class MagusGameplayAdapterTests
    {
        [UnityTest]
        public IEnumerator BasicAttackIsFreeAndRespectsItsThirtyTickInterval()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                var requests = new List<MagusBasicAttackRequest>();
                fixture.Adapter.BasicAttackRequested += requests.Add;

                MagusCombatResult first = fixture.Tick(0, PlayerAction.Attack);
                MagusCombatResult early = fixture.Tick(1, PlayerAction.Attack);
                MagusCombatResult beforeReady = fixture.Tick(29, PlayerAction.Attack);
                MagusCombatResult ready = fixture.Tick(30, PlayerAction.Attack);

                Assert.That(first.BasicAttack.WasStartedThisTick, Is.True);
                Assert.That(first.BasicAttack.BaseDamage, Is.EqualTo(12m));
                Assert.That(first.BasicAttack.ProjectileElement, Is.EqualTo("fire"));
                Assert.That(first.BasicAttack.MovementSpeedMultiplier, Is.EqualTo(0.65m));
                Assert.That(early.BasicAttack.WasStartedThisTick, Is.False);
                Assert.That(beforeReady.BasicAttack.WasStartedThisTick, Is.False);
                Assert.That(ready.BasicAttack.WasStartedThisTick, Is.True);
                Assert.That(requests, Has.Count.EqualTo(2));
                Assert.That(requests[0].Direction, Is.EqualTo(Vector2.right));
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(50));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FireballHitsEachActorOnceAndAppliesThreeDamagePerSecondBurn()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                ProjectileImpactTarget2D directTarget = fixture.CreateTarget("Magus_Fireball_Direct", new Vector2(1f, 0f), colliderCount: 2);
                ProjectileImpactTarget2D splashTarget = fixture.CreateTarget("Magus_Fireball_Splash", new Vector2(1.5f, 0f), colliderCount: 2);
                Physics2D.SyncTransforms();

                MagusCombatResult preparing = fixture.Tick(0, PlayerAction.Skill1);
                for (long tick = 1; tick < 9; tick++) fixture.Tick(tick);
                MagusCombatResult activated = fixture.Tick(9);

                Assert.That(preparing.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
                Assert.That(preparing.SkillOutcome.EnergySpent, Is.Zero);
                Assert.That(activated.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
                Assert.That(activated.SkillOutcome.EnergySpent, Is.EqualTo(8));
                Assert.That(activated.SkillOutcome.ReadyAtTick, Is.EqualTo(249));
                Assert.That(activated.SkillActivation.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(42));
                Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.EqualTo(1));

                for (int step = 0; step < 30 && directTarget.PendingImpactCount == 0; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(directTarget.PendingImpactCount, Is.EqualTo(1), "The fireball should produce one direct hit despite sibling colliders.");
                Assert.That(splashTarget.PendingImpactCount, Is.EqualTo(1), "The explosion should produce one splash hit per actor.");
                List<ProjectileImpact> directImpacts = DrainImpacts(directTarget);
                List<ProjectileImpact> splashImpacts = DrainImpacts(splashTarget);
                Assert.That(directImpacts, Has.Count.EqualTo(1));
                Assert.That(directImpacts[0].BaseDamage, Is.EqualTo(24m));
                Assert.That(splashImpacts, Has.Count.EqualTo(1));
                Assert.That(splashImpacts[0].BaseDamage, Is.EqualTo(24m));

                int activeProjectilesBeforeCooldownAttempt = fixture.ProjectileScope.ActiveProjectileCount;
                MagusCombatResult cooldown = fixture.Tick(10, PlayerAction.Skill1);
                Assert.That(cooldown.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
                Assert.That(cooldown.SkillOutcome.EnergySpent, Is.Zero);
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(42));
                Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.EqualTo(activeProjectilesBeforeCooldownAttempt));
                MagusStatusEffectTarget2D directStatus = directTarget.GetComponent<MagusStatusEffectTarget2D>();
                MagusStatusEffectTarget2D splashStatus = splashTarget.GetComponent<MagusStatusEffectTarget2D>();
                Assert.That(directStatus.IsFrozen, Is.False);
                Assert.That(directStatus.StatusSnapshot.IsBurning, Is.True);
                Assert.That(splashStatus.StatusSnapshot.IsBurning, Is.True);

                for (long tick = 11; tick <= 69; tick++) fixture.Tick(tick);

                Assert.That(directStatus.TryDequeueBurnTick(out MagusBurnTick directBurn), Is.True);
                Assert.That(directBurn.Damage, Is.EqualTo(3m));
                Assert.That(directBurn.Tick, Is.EqualTo(69));
                Assert.That(splashStatus.TryDequeueBurnTick(out MagusBurnTick splashBurn), Is.True);
                Assert.That(splashBurn.Damage, Is.EqualTo(3m));
                Assert.That(splashBurn.TargetActorId, Is.EqualTo(splashTarget.ActorId));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FireballAppliesBurnToAnActorAlreadyTrackedByFrostNova()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.FireballId))
            {
                ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Fireball_TrackedTarget", new Vector2(1f, 0f), colliderCount: 2);
                Physics2D.SyncTransforms();
                MagusCombatResult frost = Cast(fixture, 0, PlayerAction.Skill1);
                MagusStatusEffectTarget2D status = target.GetComponent<MagusStatusEffectTarget2D>();
                Assert.That(frost.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
                Assert.That(status.IsFrozen, Is.True);

                MagusCombatResult fireball = Cast(fixture, 10, PlayerAction.Skill2);
                Assert.That(fireball.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
                for (int step = 0; step < 30 && target.PendingImpactCount == 0; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(target.PendingImpactCount, Is.EqualTo(1), "The fireball direct hit remains unique while the target is already tracked.");
                fixture.Tick(20);
                Assert.That(status.StatusSnapshot.IsBurning, Is.True);
                Assert.That(status.StatusSnapshot.IsFrozen, Is.True);
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(30));

                for (long tick = 21; tick <= 79; tick++) fixture.Tick(tick);
                Assert.That(status.TryDequeueBurnTick(out MagusBurnTick burn), Is.True);
                Assert.That(burn.Damage, Is.EqualTo(3m));
                Assert.That(burn.Tick, Is.EqualTo(79));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FrostNovaDamagesOncePerActorFreezesAndGrantsFreezeProtection()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId))
            {
                ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_FrostNova_Target", new Vector2(1f, 0f), colliderCount: 3);
                Physics2D.SyncTransforms();

                MagusCombatResult preparing = fixture.Tick(0, PlayerAction.Skill1);
                for (long tick = 1; tick < 9; tick++) fixture.Tick(tick);
                MagusCombatResult activated = fixture.Tick(9);

                Assert.That(preparing.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
                Assert.That(activated.SkillOutcome.EnergySpent, Is.EqualTo(12));
                Assert.That(activated.SkillOutcome.ReadyAtTick, Is.EqualTo(369));
                Assert.That(activated.SkillActivation.FrostNova.BaseDamage, Is.EqualTo(18m));
                Assert.That(activated.SkillActivation.FrostNova.Radius, Is.EqualTo(2.5m));
                Assert.That(target.PendingImpactCount, Is.EqualTo(1), "A nova must deduplicate colliders by actor ID.");
                List<ProjectileImpact> impacts = DrainImpacts(target);
                Assert.That(impacts[0].BaseDamage, Is.EqualTo(18m));

                MagusStatusEffectTarget2D status = target.GetComponent<MagusStatusEffectTarget2D>();
                Assert.That(status.IsFrozen, Is.True);
                for (long tick = 10; tick <= 69; tick++) fixture.Tick(tick);

                Assert.That(status.StatusSnapshot.IsFrozen, Is.False);
                Assert.That(status.IsFreezeProtected, Is.True);
                Assert.That(status.ApplyFreeze(69, 60), Is.False, "A second freeze must be rejected during the two-second protection period.");
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(38));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ArcaneSealPulsesFourTimesAndDeduplicatesSiblingColliders()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.ArcaneSealId, MagusSkillLoadout.FrostNovaId))
            {
                ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_ArcaneSeal_Target", new Vector2(1f, 0f), colliderCount: 2);
                Physics2D.SyncTransforms();

                MagusCombatResult preparing = fixture.Tick(0, PlayerAction.Skill1);
                for (long tick = 1; tick < 9; tick++) fixture.Tick(tick);
                MagusCombatResult activated = fixture.Tick(9);

                Assert.That(preparing.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
                Assert.That(activated.SkillOutcome.EnergySpent, Is.EqualTo(16));
                Assert.That(activated.SkillOutcome.ReadyAtTick, Is.EqualTo(489));
                Assert.That(activated.SkillActivation.ArcaneSeal.Radius, Is.EqualTo(2.5m));
                Assert.That(activated.SkillActivation.ArcaneSeal.DurationTicks, Is.EqualTo(240));
                Assert.That(activated.SkillActivation.ArcaneSeal.DamagePerSecond, Is.EqualTo(8m));
                Assert.That(fixture.Adapter.ActiveArcaneSealCount, Is.EqualTo(1));
                Assert.That(target.PendingImpactCount, Is.EqualTo(1), "The first pulse is immediate and hits the actor once.");

                for (long tick = 10; tick <= 249; tick++) fixture.Tick(tick);

                List<ProjectileImpact> impacts = DrainImpacts(target);
                var distinctHitIds = new HashSet<HitId>();
                foreach (ProjectileImpact impact in impacts)
                {
                    Assert.That(impact.BaseDamage, Is.EqualTo(8m));
                    Assert.That(distinctHitIds.Add(impact.HitId), Is.True, "Each seal pulse must own a distinct HitId.");
                }

                Assert.That(impacts, Has.Count.EqualTo(4));
                Assert.That(fixture.Adapter.ActiveArcaneSealCount, Is.Zero);
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(34));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidEnergyAttackDodgeExternalInterruptAndDeathDoNotSpendSkillEnergy()
        {
            using (var poor = CreateFixture(7, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                MagusCombatResult rejected = poor.Tick(0, PlayerAction.Skill1);
                Assert.That(rejected.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
                Assert.That(rejected.SkillOutcome.EnergySpent, Is.Zero);
                Assert.That(poor.Energy.CurrentEnergy, Is.EqualTo(7));
                Assert.That(poor.ProjectileScope.ActiveProjectileCount, Is.Zero);
            }

            using (var energyLost = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                energyLost.Tick(0, PlayerAction.Skill1);
                energyLost.Energy.SetCurrentEnergy(0);
                for (long tick = 1; tick < 9; tick++) energyLost.Tick(tick);
                MagusCombatResult rejected = energyLost.Tick(9);
                Assert.That(rejected.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
                Assert.That(rejected.SkillActivation, Is.Null);
                Assert.That(energyLost.Energy.CurrentEnergy, Is.Zero);
                Assert.That(energyLost.CombatState.SkillUses.IsReady(MagusSkillLoadout.FireballId, 9), Is.True);
                Assert.That(energyLost.ProjectileScope.ActiveProjectileCount, Is.Zero);
            }

            using (var attackCancel = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                attackCancel.Tick(0, PlayerAction.Skill1);
                MagusCombatResult cancelled = attackCancel.Tick(1, PlayerAction.Attack);
                Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
                Assert.That(cancelled.BasicAttack.WasStartedThisTick, Is.True);
                Assert.That(attackCancel.Energy.CurrentEnergy, Is.EqualTo(50));
                Assert.That(attackCancel.ProjectileScope.ActiveProjectileCount, Is.Zero);
            }

            using (var dodgeCancel = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                dodgeCancel.Tick(0, PlayerAction.Skill1);
                MagusCombatResult cancelled = dodgeCancel.Tick(1, PlayerAction.Dodge);
                Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
                Assert.That(cancelled.BasicAttack.WasStartedThisTick, Is.False);
                Assert.That(dodgeCancel.Energy.CurrentEnergy, Is.EqualTo(50));
            }

            using (var interrupted = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                interrupted.Tick(0, PlayerAction.Skill1);
                MagusCombatResult cancelled = interrupted.Tick(1, interrupted: true);
                Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
                Assert.That(interrupted.Energy.CurrentEnergy, Is.EqualTo(50));
                Assert.That(interrupted.ProjectileScope.ActiveProjectileCount, Is.Zero);
            }

            using (var death = CreateFixture(50, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId))
            {
                death.Tick(0, PlayerAction.Skill1);
                for (long tick = 1; tick < 9; tick++) death.Tick(tick);
                MagusCombatResult cancelled = death.Tick(9, isAlive: false);
                Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
                Assert.That(cancelled.SkillActivation, Is.Null);
                Assert.That(death.Energy.CurrentEnergy, Is.EqualTo(50));
                Assert.That(death.ProjectileScope.ActiveProjectileCount, Is.Zero);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingRoomClearsStatusZonesAndProjectilesBeforeNextRoom()
        {
            using (var fixture = CreateFixture(50, MagusSkillLoadout.ArcaneSealId, MagusSkillLoadout.FrostNovaId))
            {
                ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_RoomExit_Target", new Vector2(1f, 0f), colliderCount: 1);
                Physics2D.SyncTransforms();
                MagusCombatResult freeze = Cast(fixture, 0, PlayerAction.Skill2);
                MagusStatusEffectTarget2D oldStatus = target.GetComponent<MagusStatusEffectTarget2D>();
                Assert.That(freeze.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
                Assert.That(oldStatus.IsFrozen, Is.True);

                MagusCombatResult seal = Cast(fixture, 10, PlayerAction.Skill1);
                Assert.That(seal.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
                Assert.That(fixture.Adapter.ActiveArcaneSealCount, Is.EqualTo(1));
                Projectile2D roomProjectile = fixture.SpawnUnhittingProjectile();
                Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.EqualTo(1));

                MagusCombatAdapter2D previousAdapter = fixture.Adapter;
                RoomLifetimeScope previousLifetime = fixture.LifetimeScope;
                RoomProjectileScope previousProjectileScope = fixture.ProjectileScope;
                fixture.TransitionToNextRoom("second");

                Assert.That(previousAdapter.IsDisposed, Is.True);
                Assert.That(previousAdapter.ActiveArcaneSealCount, Is.Zero);
                Assert.That(previousLifetime.IsDisposed, Is.True);
                Assert.That(previousProjectileScope.ActiveProjectileCount, Is.Zero);
                Assert.That(roomProjectile.IsPooled, Is.True);
                Assert.That(fixture.ProjectilePool.PooledCount, Is.EqualTo(1));
                Assert.That(oldStatus.IsFrozen, Is.False);
                Assert.That(oldStatus.StatusSnapshot.IsFreezeProtected, Is.False);

                fixture.Tick(20);
                Assert.That(fixture.Adapter.ActiveArcaneSealCount, Is.Zero);
                Assert.That(fixture.Energy.CurrentEnergy, Is.EqualTo(22));
                Assert.That(() => previousAdapter.Tick(
                    fixture.Command(21), true, false, Vector2.zero, Vector2.zero, Vector2.right),
                    Throws.TypeOf<System.ObjectDisposedException>());
            }

            yield return null;
        }

        private static MagusCombatResult Cast(MagusFixture fixture, long startTick, PlayerAction slot)
        {
            fixture.Tick(startTick, slot);
            for (long tick = startTick + 1; tick < startTick + 9; tick++) fixture.Tick(tick);
            return fixture.Tick(startTick + 9);
        }

        private static List<ProjectileImpact> DrainImpacts(ProjectileImpactTarget2D target)
        {
            var impacts = new List<ProjectileImpact>();
            while (target.TryDequeueImpact(out ProjectileImpact impact)) impacts.Add(impact);
            return impacts;
        }

        private static MagusFixture CreateFixture(int initialEnergy, string skill1, string skill2)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The authoritative skill catalog must be available to PlayMode tests.");
            return new MagusFixture(SkillCatalogJsonLoader.Load(source.text), initialEnergy, skill1, skill2);
        }

        private sealed class MagusFixture : System.IDisposable
        {
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly Vector2 _previousGravity;
            private bool _disposed;

            public MagusFixture(SkillCatalog catalog, int initialEnergy, string skill1, string skill2)
            {
                _previousGravity = Physics2D.gravity;
                Physics2D.gravity = Vector2.zero;
                ActorId = Luckbound.Core.Identifiers.ActorId.New();
                Energy = new SkillUseState(maximumEnergy: 50, initialEnergy: initialEnergy);
                CombatState = new MagusCombatState(ActorId, new MagusSkillLoadout(catalog, new[] { skill1, skill2 }), Energy);

                GameObject poolObject = NewObject("Magus_ProjectilePool");
                ProjectilePool = poolObject.AddComponent<ProjectilePool2D>();
                GameObject prefab = NewObject("Magus_ProjectilePrefab");
                Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
                collider.radius = 0.12f;
                prefab.AddComponent<Projectile2D>();
                prefab.SetActive(false);
                ProjectilePool.Initialize(prefab);

                InitializeRoom("first");
            }

            public ActorId ActorId { get; }

            public SkillUseState Energy { get; }

            public MagusCombatState CombatState { get; }

            public ProjectilePool2D ProjectilePool { get; }

            public RoomProjectileScope ProjectileScope { get; private set; }

            public RoomLifetimeScope LifetimeScope { get; private set; }

            public MagusCombatAdapter2D Adapter { get; private set; }

            public MagusCombatResult Tick(
                long tick,
                PlayerAction pressed = PlayerAction.None,
                bool isAlive = true,
                bool interrupted = false,
                Vector2 actorPosition = default,
                Vector2 projectileOrigin = default)
            {
                Vector2 aim = Vector2.right;
                return Adapter.Tick(Command(tick, pressed), isAlive, interrupted, actorPosition, projectileOrigin, aim);
            }

            public PlayerCommand Command(long tick, PlayerAction pressed = PlayerAction.None)
            {
                var input = new PlayerInputBuffer(ActorId);
                PlayerAction[] actions =
                {
                    PlayerAction.Jump, PlayerAction.Attack, PlayerAction.Dodge, PlayerAction.Skill1,
                    PlayerAction.Skill2, PlayerAction.Interact, PlayerAction.Consumable,
                    PlayerAction.Inventory, PlayerAction.Map, PlayerAction.Pause
                };
                foreach (PlayerAction action in actions)
                {
                    if ((pressed & action) == action) input.Press(action);
                }

                return input.Consume(tick);
            }

            public ProjectileImpactTarget2D CreateTarget(string name, Vector2 position, int colliderCount)
            {
                var targetObject = NewObject(name);
                targetObject.transform.position = position;
                ProjectileImpactTarget2D target = targetObject.AddComponent<ProjectileImpactTarget2D>();
                target.Initialize(Luckbound.Core.Identifiers.ActorId.New());
                for (int index = 0; index < colliderCount; index++)
                {
                    BoxCollider2D collider = targetObject.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(0.4f, 0.7f);
                    collider.offset = new Vector2((index - (colliderCount - 1) * 0.5f) * 0.04f, 0f);
                }

                LifetimeScope.OwnActor(targetObject);
                return target;
            }

            public Projectile2D SpawnUnhittingProjectile()
            {
                return ProjectilePool.Spawn(
                    new ProjectileDefinition(24m, 10m, 180, 1),
                    EntityId.New(),
                    ActorId,
                    new Vector2(100f, 100f),
                    Vector2.right,
                    1 << 0,
                    ProjectileScope);
            }

            public void TransitionToNextRoom(string suffix)
            {
                Adapter.Dispose();
                LifetimeScope.Dispose();
                ProjectileScope.Dispose();
                InitializeRoom(suffix);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Adapter?.Dispose();
                LifetimeScope?.Dispose();
                ProjectileScope?.Dispose();
                foreach (GameObject gameObject in _objects)
                {
                    if (gameObject != null) Object.Destroy(gameObject);
                }

                Physics2D.gravity = _previousGravity;
            }

            private void InitializeRoom(string suffix)
            {
                GameObject lifetimeObject = NewObject("Magus_RoomLifetime_" + suffix);
                LifetimeScope = lifetimeObject.AddComponent<RoomLifetimeScope>();
                LifetimeScope.Initialize(RoomInstanceId.New(), "room.magus." + suffix);

                GameObject projectileScopeObject = NewObject("Magus_RoomProjectiles_" + suffix);
                ProjectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
                ProjectileScope.Initialize(EntityId.New(), ProjectilePool);

                Adapter = new MagusCombatAdapter2D(
                    ActorId,
                    CombatState,
                    ProjectilePool,
                    ProjectileScope,
                    LifetimeScope,
                    1 << 0);
            }

            private GameObject NewObject(string name)
            {
                var gameObject = new GameObject(name);
                _objects.Add(gameObject);
                return gameObject;
            }
        }
    }
}
