using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Venator;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode.Venator
{
    public sealed class VenatorGameplayTests
    {
        private readonly List<VenatorFixture> _fixtures = new List<VenatorFixture>();
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
        public IEnumerator BasicBowAttackUsesRealProjectileCollisionWithoutSpendingEnergy()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            VenatorTestImpactActor2D target = fixture.CreateTarget("Bow Target", new Vector2(3f, 1.75f));

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Attack);
            for (int tick = 0; tick < 35 && target.AppliedImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));
            Assert.That(target.AppliedImpactCount, Is.EqualTo(1), "The arrow must contact a target collider and resolve on its next simulation tick.");
            Assert.That(target.CurrentHealth, Is.EqualTo(90), "Shortbow damage is 10 from the weapon data in section 3.8.");
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(50), "The basic bow attack is free.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PiercingArrowDamagesThreeActorsOnceDespiteSiblingColliders()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.RootTrapId);
            VenatorTestImpactActor2D first = fixture.CreateTarget("Piercing Target 1", new Vector2(2.4f, 1.75f), multipleColliders: true);
            VenatorTestImpactActor2D second = fixture.CreateTarget("Piercing Target 2", new Vector2(4f, 1.75f), multipleColliders: true);
            VenatorTestImpactActor2D third = fixture.CreateTarget("Piercing Target 3", new Vector2(5.6f, 1.75f), multipleColliders: true);

            fixture.Input.Press(PlayerAction.Skill1);
            SkillUseOutcome activation = default;
            long activationTick = -1;
            for (int tick = 0; tick < 12; tick++)
            {
                yield return new WaitForFixedUpdate();
                activation = fixture.Motor.LastVenatorCombatResult.SkillOutcome;
                if (activation.Status == SkillUseStatus.Activated)
                {
                    activationTick = fixture.Motor.SimulationTick;
                    break;
                }
            }

            fixture.Input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 45 && third.AppliedImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(activation.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activationTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(activation.ReadyAtTick, Is.EqualTo(activationTick + 240), "Piercing Arrow recovers after 240 simulation ticks.");
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(42), "Piercing Arrow costs 8 energy.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));
            Assert.That(new[] { first, second, third }.All(actor => actor.AppliedImpactCount == 1 && actor.CurrentHealth == 76), Is.True,
                "One 24-damage arrow must accept three distinct ActorIds.");
            Assert.That(new[] { first, second, third }.All(actor => actor.DuplicateImpactCount == 0), Is.True,
                "Sibling colliders for one ActorId must not duplicate an impact.");

            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            SkillUseOutcome earlyRetry = fixture.Motor.LastVenatorCombatResult.SkillOutcome;
            fixture.Input.Release(PlayerAction.Skill1);
            Assert.That(earlyRetry.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(earlyRetry.EnergySpent, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(42));

            while (fixture.Motor.SimulationTick < activationTick + 59)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(42), "Energy recovery has a one-second delay.");
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(activationTick + 60));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(43), "The first energy point returns one second after use.");

            while (fixture.Motor.SimulationTick < activationTick + 108)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(47), "Energy recovers at five points per second after the initial delay.");

            while (fixture.Motor.SimulationTick < activation.ReadyAtTick - 1)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(activation.ReadyAtTick));
            Assert.That(fixture.Motor.LastVenatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing),
                "The skill becomes available on its exact ready tick.");
            yield return AdvanceFixedTicks(9);
            Assert.That(fixture.Motor.LastVenatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(2));
            fixture.Input.Release(PlayerAction.Skill1);

            for (int tick = 0; tick < 20 && third.AppliedImpactCount < 2; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(new[] { first, second, third }.All(actor => actor.AppliedImpactCount == 2 && actor.CurrentHealth == 52), Is.True,
                "The readied ability creates a new physical arrow with a new one-hit-per-actor result.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TripleShotSpawnsThreeIndependentTwelveDegreeProjectiles()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.TripleShotId,
                VenatorSkillLoadout.PiercingArrowId);

            fixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(10);
            fixture.Input.Release(PlayerAction.Skill1);

            Projectile2D[] arrows = fixture.ProjectileScope.GetComponentsInChildren<Projectile2D>(includeInactive: true)
                .Where(projectile => projectile != null && !projectile.IsPooled)
                .ToArray();
            float[] angles = arrows.Select(arrow => Vector2.SignedAngle(Vector2.right, arrow.GetComponent<Rigidbody2D>().linearVelocity))
                .OrderBy(angle => angle)
                .ToArray();
            HitId[] hitIds = arrows.Select(arrow => arrow.HitId).ToArray();

            Assert.That(fixture.Motor.VenatorSkillActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(38), "Triple Shot costs 12 energy.");
            Assert.That(arrows, Has.Length.EqualTo(3));
            Assert.That(angles[0], Is.EqualTo(-12f).Within(0.25f));
            Assert.That(angles[1], Is.EqualTo(0f).Within(0.25f));
            Assert.That(angles[2], Is.EqualTo(12f).Within(0.25f));
            Assert.That(arrows.All(arrow => Mathf.Abs(arrow.GetComponent<Rigidbody2D>().linearVelocity.magnitude - 18f) < 0.1f), Is.True);
            Assert.That(hitIds.Distinct().Count(), Is.EqualTo(3), "Every arrow instance needs an independent HitId.");
            Assert.That(fixture.Motor.LastVenatorCombatResult.SkillOutcome.ReadyAtTick,
                Is.EqualTo(fixture.Motor.SimulationTick + 360), "Triple Shot recovers after 360 simulation ticks.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InsufficientEnergyAndDodgeCancellationDoNotChargeOrActivate()
        {
            VenatorFixture insufficient = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.RootTrapId);
            insufficient.Motor.SkillUses.SetCurrentEnergy(7);
            insufficient.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();

            Assert.That(insufficient.Motor.LastVenatorCombatResult.SkillOutcome.Status,
                Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(insufficient.Motor.CurrentEnergy, Is.EqualTo(7));
            Assert.That(insufficient.Motor.VenatorProjectileSpawnCount, Is.Zero);
            insufficient.Destroy();
            yield return null;

            VenatorFixture drainedDuringPreparation = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.RootTrapId);
            drainedDuringPreparation.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            Assert.That(drainedDuringPreparation.Motor.IsPreparingVenatorSkill, Is.True);
            drainedDuringPreparation.Motor.SkillUses.SetCurrentEnergy(7);
            yield return AdvanceFixedTicks(9);

            Assert.That(drainedDuringPreparation.Motor.LastVenatorCombatResult.SkillOutcome.Status,
                Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy), "Energy is revalidated when the effect begins.");
            Assert.That(drainedDuringPreparation.Motor.CurrentEnergy, Is.EqualTo(7));
            Assert.That(drainedDuringPreparation.Motor.VenatorProjectileSpawnCount, Is.Zero);
            drainedDuringPreparation.Destroy();
            yield return null;

            VenatorFixture cancelled = CreateFixture(
                VenatorSkillLoadout.RootTrapId,
                VenatorSkillLoadout.PiercingArrowId);
            cancelled.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(5);
            Assert.That(cancelled.Motor.IsPreparingVenatorSkill, Is.True);

            cancelled.Input.Release(PlayerAction.Skill1);
            cancelled.Input.Press(PlayerAction.Dodge);
            yield return new WaitForFixedUpdate();

            Assert.That(cancelled.Motor.LastVenatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(cancelled.Motor.IsPreparingVenatorSkill, Is.False);
            Assert.That(cancelled.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(cancelled.Motor.VenatorRootTrapSpawnCount, Is.Zero);
            Assert.That(cancelled.Motor.DodgeActivationCount, Is.EqualTo(1));

            cancelled.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActivePotionPreparationHasPriorityOverVenatorSkillsAndBasicAttacks()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            fixture.Motor.ReceiveDirectImpact(
                HitId.New(),
                Request(fixture.Motor.CombatStats, 1m),
                fixture.Motor.SimulationTick,
                Vector2.zero);

            fixture.Input.Press(PlayerAction.Consumable);
            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.IsPreparingHealthPotion, Is.True);
            Assert.That(fixture.Motor.IsPreparingVenatorSkill, Is.False);
            Assert.That(fixture.Motor.VenatorSkillActivationCount, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(50));

            fixture.Input.Release(PlayerAction.Consumable);
            fixture.Input.Release(PlayerAction.Skill1);
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.Zero);
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.Zero);
            Assert.That(fixture.Motor.IsPreparingHealthPotion, Is.True);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcceptedDamageCancelsVenatorPreparationWithoutSpendingEnergy()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.RootTrapId);
            fixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(5);
            Assert.That(fixture.Motor.IsPreparingVenatorSkill, Is.True);

            DirectImpactOutcome impact = fixture.Motor.ReceiveDirectImpact(
                HitId.New(),
                Request(fixture.Motor.CombatStats, 5m),
                fixture.Motor.SimulationTick,
                Vector2.zero);
            fixture.Input.Release(PlayerAction.Skill1);
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();

            Assert.That(impact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(fixture.Motor.LastVenatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(fixture.Motor.IsPreparingVenatorSkill, Is.False);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(fixture.Motor.VenatorSkillActivationCount, Is.Zero);
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.Zero,
                "The interrupting tick is consumed before a new basic attack can start.");

            fixture.Input.Release(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.VenatorBasicAttackActivationCount, Is.EqualTo(1),
                "A later real input can act after the interrupted preparation is cleared.");
            Assert.That(fixture.Motor.VenatorProjectileSpawnCount, Is.EqualTo(1));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RootTrapDamagesAndRootsAContactTargetThroughPhysics()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.RootTrapId,
                VenatorSkillLoadout.PiercingArrowId);
            VenatorTestImpactActor2D target = fixture.CreateTarget("Root Trap Target", new Vector2(1.2f, 0.85f), multipleColliders: true);
            target.Body.linearVelocity = new Vector2(2f, 0f);

            fixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(16);
            fixture.Input.Release(PlayerAction.Skill1);

            VenatorRootedMovement2D rooted = target.GetComponent<VenatorRootedMovement2D>();
            Assert.That(fixture.Motor.VenatorRootTrapSpawnCount, Is.EqualTo(1));
            Assert.That(target.AppliedImpactCount, Is.EqualTo(1));
            Assert.That(target.DuplicateImpactCount, Is.Zero, "Sibling colliders cannot cause repeated trap impacts.");
            Assert.That(target.CurrentHealth, Is.EqualTo(84), "Root Trap deals 16 damage.");
            Assert.That(rooted, Is.Not.Null);
            Assert.That(rooted.IsRooted, Is.True);
            Assert.That(rooted.RemainingTicks, Is.GreaterThan(0).And.LessThanOrEqualTo(75));
            Assert.That((target.Body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0, Is.True);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(34), "Root Trap costs 16 energy.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RootTrapReturnsToItsRoomPoolAfterEightSeconds()
        {
            VenatorFixture fixture = CreateFixture(
                VenatorSkillLoadout.RootTrapId,
                VenatorSkillLoadout.PiercingArrowId);
            fixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(10);
            fixture.Input.Release(PlayerAction.Skill1);

            VenatorRootTrapPool2D trapPool = fixture.Room.GetComponentInChildren<VenatorRootTrapPool2D>(includeInactive: true);
            Assert.That(trapPool, Is.Not.Null);
            Assert.That(trapPool.ActiveTrapCount, Is.EqualTo(1));

            yield return AdvanceFixedTicks(480);

            Assert.That(trapPool.ActiveTrapCount, Is.Zero, "The trap lifetime is capped at 480 simulation ticks.");
            Assert.That(trapPool.PooledTrapCount, Is.EqualTo(1), "An expired trap is reusable within the same room.");
            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDeathAndRoomDisposalCancelPendingSkillsAndReleaseRoomEffects()
        {
            VenatorFixture deathFixture = CreateFixture(
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.RootTrapId);
            deathFixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(5);
            Assert.That(deathFixture.Motor.IsPreparingVenatorSkill, Is.True);

            DirectImpactOutcome lethal = deathFixture.Motor.ReceiveDirectImpact(
                HitId.New(),
                Request(deathFixture.Motor.CombatStats, 200m),
                deathFixture.Motor.SimulationTick,
                Vector2.zero);
            deathFixture.Input.Release(PlayerAction.Skill1);
            deathFixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();

            Assert.That(lethal.DeathOccurred, Is.True);
            Assert.That(deathFixture.Motor.IsDead, Is.True);
            Assert.That(deathFixture.Motor.LastVenatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(deathFixture.Motor.IsPreparingVenatorSkill, Is.False);
            Assert.That(deathFixture.Motor.VenatorProjectileSpawnCount, Is.Zero);
            Assert.That(deathFixture.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(deathFixture.Motor.VenatorBasicAttackActivationCount, Is.Zero);

            deathFixture.Input.Release(PlayerAction.Attack);
            deathFixture.Input.Press(PlayerAction.Skill2);
            yield return new WaitForFixedUpdate();
            Assert.That(deathFixture.Motor.LastVenatorCombatResult.SkillOutcome.Status,
                Is.EqualTo(SkillUseStatus.RejectedInvalid));
            Assert.That(deathFixture.Motor.VenatorSkillActivationCount, Is.Zero);
            Assert.That(deathFixture.Motor.VenatorRootTrapSpawnCount, Is.Zero);
            Assert.That(deathFixture.Motor.VenatorProjectileSpawnCount, Is.Zero);
            deathFixture.Destroy();
            yield return null;

            VenatorFixture roomFixture = CreateFixture(
                VenatorSkillLoadout.RootTrapId,
                VenatorSkillLoadout.PiercingArrowId);
            roomFixture.CreateTarget("Room Trap Target", new Vector2(1.2f, 0.85f));
            roomFixture.Input.Press(PlayerAction.Skill1);
            yield return AdvanceFixedTicks(16);
            roomFixture.Input.Release(PlayerAction.Skill1);
            Assert.That(roomFixture.Motor.VenatorRootTrapSpawnCount, Is.EqualTo(1));

            roomFixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            roomFixture.Input.Release(PlayerAction.Attack);
            Assert.That(roomFixture.ProjectileScope.ActiveProjectileCount, Is.EqualTo(1),
                "The room owns the free bow projectile while it is in flight.");

            roomFixture.Input.Press(PlayerAction.Skill2);
            yield return new WaitForFixedUpdate();
            Assert.That(roomFixture.Motor.IsPreparingVenatorSkill, Is.True);
            VenatorRootTrapPool2D trapPool = roomFixture.Room.GetComponentInChildren<VenatorRootTrapPool2D>(includeInactive: true);
            Assert.That(trapPool, Is.Not.Null);
            Assert.That(trapPool.ActiveTrapCount, Is.EqualTo(1));

            roomFixture.Room.Dispose();
            yield return new WaitForFixedUpdate();

            Assert.That(roomFixture.ProjectileScope.IsDisposed, Is.True);
            Assert.That(roomFixture.ProjectileScope.ActiveProjectileCount, Is.Zero);
            Assert.That(trapPool.ActiveTrapCount, Is.Zero);
            Assert.That(roomFixture.Motor.IsPreparingVenatorSkill, Is.False);
            Assert.That(roomFixture.Motor.CurrentEnergy, Is.EqualTo(34), "Room exit cancels the second preparation without its 8 energy cost.");

            roomFixture.Destroy();
            yield return null;
        }

        private VenatorFixture CreateFixture(params string[] selectedSkillIds)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0));

            var floor = new GameObject("Venator Test Ground");
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(40f, 1f);

            var actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject("Venator Test Player");
            player.transform.position = new Vector2(0f, 1.5f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            LayerMask enemyLayers = 1 << enemyLayer;
            motor.Initialize(actorId, input, 1 << groundLayer, new CombatStats(100, 50, 0), enemyLayers);

            GameObject projectilePoolObject = new GameObject("Venator Projectile Pool");
            ProjectilePool2D projectilePool = projectilePoolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = new GameObject("Venator Arrow Prefab");
            projectilePrefab.AddComponent<Rigidbody2D>();
            CircleCollider2D projectileCollider = projectilePrefab.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.12f;
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            projectilePool.Initialize(projectilePrefab);

            var projectileScopeObject = new GameObject("Venator Room Projectile Scope");
            RoomProjectileScope projectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
            projectileScope.Initialize(EntityId.New(), projectilePool);

            var roomObject = new GameObject("Venator Room Lifetime");
            RoomLifetimeScope room = roomObject.AddComponent<RoomLifetimeScope>();
            room.Initialize(RoomInstanceId.New(), "room.test.venator");
            room.OwnActor(projectileScopeObject);
            room.OwnResource(projectileScope);

            TextAsset skillsJson = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skillsJson, Is.Not.Null, "The integration must use the authoritative Skills.json resource.");
            motor.ConfigureVenator(
                SkillCatalogJsonLoader.Load(skillsJson.text),
                selectedSkillIds,
                projectilePool,
                projectileScope,
                room,
                enemyLayers);
            Physics2D.SyncTransforms();
            var fixture = new VenatorFixture(
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

        private static DamageCalculationRequest Request(CombatStats targetStats, decimal baseDamage)
        {
            return new DamageCalculationRequest(
                baseDamage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats,
                targetIsInvulnerable: false);
        }

        private static IEnumerator AdvanceFixedTicks(int count)
        {
            for (int tick = 0; tick < count; tick++) yield return new WaitForFixedUpdate();
        }

        private sealed class VenatorFixture
        {
            private readonly GameObject _floor;
            private readonly GameObject _player;
            private readonly GameObject _projectilePoolObject;
            private readonly GameObject _projectilePrefab;
            private readonly GameObject _roomObject;
            private readonly int _enemyLayer;
            private bool _destroyed;

            public VenatorFixture(
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

            public VenatorTestImpactActor2D CreateTarget(string objectName, Vector2 position, bool multipleColliders = false)
            {
                var target = new GameObject(objectName);
                target.transform.position = position;
                VenatorTestImpactActor2D impactActor = target.AddComponent<VenatorTestImpactActor2D>();
                impactActor.Initialize(ActorId.New(), 100, _enemyLayer, multipleColliders);
                Room.OwnActor(target);
                return impactActor;
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
