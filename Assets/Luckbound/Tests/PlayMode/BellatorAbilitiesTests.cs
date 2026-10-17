using System.Collections;
using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class BellatorAbilitiesTests
    {
        private const string EnemyLayerName = "Enemies";
        private const string GroundLayerName = "Ground";
        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _objects.Clear();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) Object.Destroy(_objects[index]);
            }

            Time.timeScale = _previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CircularCutUsesCatalogAreaDamageAndQueuesEachActorOnlyOnce()
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            int enemyLayer = RequiredLayer(EnemyLayerName);
            CreateFloor(new Vector2(0f, -0.5f));
            PlayerFixture player = CreatePlayer(
                new Vector2(0f, 1.5f),
                1 << groundLayer,
                1 << enemyLayer,
                BellatorSkillLoadout.CircularCutId,
                BellatorSkillLoadout.GuardId);
            SlimeFixture right = CreateSlime(new Vector2(1.6f, 0.5f), player.Motor, 1 << groundLayer);
            SlimeFixture left = CreateSlime(new Vector2(-1.6f, 0.5f), player.Motor, 1 << groundLayer);
            SlimeFixture outside = CreateSlime(new Vector2(4.8f, 0.5f), player.Motor, 1 << groundLayer);
            AddDuplicateCollider(right, enemyLayer);
            Physics2D.SyncTransforms();
            yield return Settle(player);

            yield return ActivateBellatorSkill(player, PlayerAction.Skill1);
            SkillUseOutcome activation = player.Motor.LastBellatorCombatResult.SkillOutcome;
            long activationTick = player.Motor.SimulationTick;
            for (int tick = 0; tick < 6 &&
                (right.Enemy.MeleeImpactCount == 0 || left.Enemy.MeleeImpactCount == 0); tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(activation.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activation.EnergySpent, Is.EqualTo(8));
            Assert.That(activation.ReadyAtTick, Is.EqualTo(activationTick + 240));
            Assert.That(player.Motor.BellatorCircularCutActivationCount, Is.EqualTo(1));
            Assert.That(player.Motor.BellatorSkillActivationCount, Is.EqualTo(1));
            Assert.That(player.Motor.LastBellatorCircularCutHitId.IsValid, Is.True);
            Assert.That(player.Motor.MeleeImpactsQueuedCount, Is.EqualTo(2),
                "The duplicate collider on one Slime resolves to its ActorId only once, while the second actor also receives one hit.");
            Assert.That(right.Enemy.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(left.Enemy.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(right.Enemy.CurrentHealth, Is.Zero, "The 24 damage comes from the catalog and resolves at the Slime's normal damage boundary.");
            Assert.That(left.Enemy.CurrentHealth, Is.Zero);
            Assert.That(outside.Enemy.MeleeImpactCount, Is.Zero, "Targets beyond the catalog radius are untouched.");
            Assert.That(outside.Enemy.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth));
            Assert.That(player.Motor.CurrentEnergy, Is.EqualTo(42));
        }

        [UnityTest]
        public IEnumerator GuardReducesDirectDamageOnItsNinetiethActiveTick()
        {
            PlayerFixture player = CreateGuardPlayer();
            yield return Settle(player);
            yield return ActivateBellatorSkill(player, PlayerAction.Skill1);
            long guardStartTick = player.Motor.SimulationTick;

            for (int tick = 0; tick < 89; tick++) yield return new WaitForFixedUpdate();

            Assert.That(player.Motor.SimulationTick, Is.EqualTo(guardStartTick + 89));
            Assert.That(player.Motor.BellatorGuardActivationCount, Is.EqualTo(1));
            Assert.That(player.Motor.IsBellatorGuardActive, Is.True);

            DirectImpactOutcome outcome = player.Motor.ReceiveDirectImpact(
                HitId.New(),
                DirectDamage(player.Motor.CombatStats, baseDamage: 10m),
                player.Motor.SimulationTick,
                Vector2.zero);

            Assert.That(outcome.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(outcome.Resolution.Damage, Is.EqualTo(4), "The active Guard reduces ten direct damage by the catalog's sixty percent.");
            Assert.That(outcome.Resolution.WasPreventedByInvulnerability, Is.False,
                "Guard is damage reduction and does not become an invulnerability window.");
            Assert.That(player.Motor.CurrentHealth, Is.EqualTo(96));
        }

        [UnityTest]
        public IEnumerator GuardNoLongerReducesDirectDamageAtItsExpiryTick()
        {
            PlayerFixture player = CreateGuardPlayer();
            yield return Settle(player);
            yield return ActivateBellatorSkill(player, PlayerAction.Skill1);
            long guardStartTick = player.Motor.SimulationTick;

            for (int tick = 0; tick < 90; tick++) yield return new WaitForFixedUpdate();

            Assert.That(player.Motor.SimulationTick, Is.EqualTo(guardStartTick + 90));
            Assert.That(player.Motor.IsBellatorGuardActive, Is.False);
            DirectImpactOutcome outcome = player.Motor.ReceiveDirectImpact(
                HitId.New(),
                DirectDamage(player.Motor.CombatStats, baseDamage: 10m),
                player.Motor.SimulationTick,
                Vector2.zero);

            Assert.That(outcome.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(outcome.Resolution.Damage, Is.EqualTo(10));
            Assert.That(player.Motor.CurrentHealth, Is.EqualTo(90));
        }

        [UnityTest]
        public IEnumerator GuardCombinesWithExistingDirectReductionAndLeavesTheBellatorSkillCostExact()
        {
            PlayerFixture player = CreateGuardPlayer();
            yield return Settle(player);
            yield return ActivateBellatorSkill(player, PlayerAction.Skill1);
            long activationTick = player.Motor.SimulationTick;

            Assert.That(player.Motor.LastBellatorCombatResult.SkillOutcome.EnergySpent, Is.EqualTo(12));
            Assert.That(player.Motor.LastBellatorCombatResult.SkillOutcome.ReadyAtTick,
                Is.EqualTo(activationTick + 360));
            Assert.That(player.Motor.CurrentEnergy, Is.EqualTo(38));

            DirectImpactOutcome outcome = player.Motor.ReceiveDirectImpact(
                HitId.New(),
                DirectDamage(player.Motor.CombatStats, baseDamage: 10m, existingReduction: 0.2m),
                player.Motor.SimulationTick,
                Vector2.zero);

            Assert.That(outcome.Resolution.Damage, Is.EqualTo(2),
                "The existing twenty percent reduction and Guard's sixty percent are summed, then capped by the shared damage rule.");
        }

        [UnityTest]
        public IEnumerator DirectDamageInterruptsBellatorPreparationBeforeItsEffectStarts()
        {
            PlayerFixture player = CreateGuardPlayer();
            yield return Settle(player);
            player.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            player.Input.Release(PlayerAction.Skill1);

            Assert.That(player.Motor.IsPreparingBellatorSkill, Is.True);
            Assert.That(player.Motor.CurrentEnergy, Is.EqualTo(50));
            DirectImpactOutcome impact = player.Motor.ReceiveDirectImpact(
                HitId.New(),
                DirectDamage(player.Motor.CombatStats, baseDamage: 1m),
                player.Motor.SimulationTick,
                Vector2.zero);
            yield return new WaitForFixedUpdate();

            Assert.That(impact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(player.Motor.LastBellatorCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(player.Motor.IsPreparingBellatorSkill, Is.False);
            Assert.That(player.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(player.Motor.IsBellatorSkillReady(BellatorSkillLoadout.GuardId), Is.True);
        }

        private PlayerFixture CreateGuardPlayer()
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            CreateFloor(new Vector2(0f, -0.5f));
            return CreatePlayer(
                new Vector2(0f, 1.5f),
                1 << groundLayer,
                1 << RequiredLayer(EnemyLayerName),
                BellatorSkillLoadout.GuardId,
                BellatorSkillLoadout.CircularCutId);
        }

        private PlayerFixture CreatePlayer(
            Vector2 position,
            LayerMask groundLayers,
            LayerMask enemyLayers,
            string skill1,
            string skill2)
        {
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var gameObject = Track(new GameObject("Bellator Abilities Test Player"));
            gameObject.transform.position = position;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = gameObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = gameObject.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, groundLayers, new CombatStats(100, 50, 0), enemyLayers);

            TextAsset skills = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skills, Is.Not.Null, "Bellator abilities must use the authoritative Skills.json resource.");
            motor.ConfigureBellator(SkillCatalogJsonLoader.Load(skills.text), new[] { skill1, skill2 });
            return new PlayerFixture(gameObject, body, motor, input);
        }

        private SlimeFixture CreateSlime(Vector2 position, PlayerMotor2D player, LayerMask groundLayers)
        {
            var gameObject = Track(new GameObject("Bellator Circular Cut Slime"));
            gameObject.layer = RequiredLayer(EnemyLayerName);
            gameObject.transform.position = position;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = gameObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.8f, 0.8f);
            Slime2D enemy = gameObject.AddComponent<Slime2D>();
            enemy.Initialize(ActorId.New(), player, groundLayers);
            return new SlimeFixture(gameObject, body, enemy);
        }

        private void AddDuplicateCollider(SlimeFixture slime, int enemyLayer)
        {
            var duplicate = Track(new GameObject("Bellator Circular Cut Duplicate Collider"));
            duplicate.layer = enemyLayer;
            duplicate.transform.SetParent(slime.GameObject.transform, worldPositionStays: false);
            CircleCollider2D collider = duplicate.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
        }

        private GameObject CreateFloor(Vector2 position)
        {
            var floor = Track(new GameObject("Bellator Abilities Test Ground"));
            floor.layer = RequiredLayer(GroundLayerName);
            floor.transform.position = position;
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(30f, 1f);
            return floor;
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static DamageCalculationRequest DirectDamage(
            CombatStats stats,
            decimal baseDamage,
            decimal existingReduction = 0m)
        {
            return new DamageCalculationRequest(
                baseDamage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: existingReduction,
                targetStats: stats,
                targetIsInvulnerable: false);
        }

        private static IEnumerator ActivateBellatorSkill(PlayerFixture player, PlayerAction slot)
        {
            player.Input.Press(slot);
            yield return new WaitForFixedUpdate();
            player.Input.Release(slot);
            for (int tick = 0; tick < 9; tick++) yield return new WaitForFixedUpdate();
        }

        private static IEnumerator Settle(PlayerFixture player)
        {
            Physics2D.SyncTransforms();
            for (int tick = 0; tick < 30 && !player.Motor.IsGrounded; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(player.Motor.IsGrounded, Is.True, "The player must be grounded before ability acceptance begins.");
        }

        private static int RequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The " + layerName + " layer is required by this physical scenario.");
            return layer;
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(GameObject gameObject, Rigidbody2D body, PlayerMotor2D motor, PlayerInputBuffer input)
            {
                GameObject = gameObject;
                Body = body;
                Motor = motor;
                Input = input;
            }

            public GameObject GameObject { get; }
            public Rigidbody2D Body { get; }
            public PlayerMotor2D Motor { get; }
            public PlayerInputBuffer Input { get; }
        }

        private sealed class SlimeFixture
        {
            public SlimeFixture(GameObject gameObject, Rigidbody2D body, Slime2D enemy)
            {
                GameObject = gameObject;
                Body = body;
                Enemy = enemy;
            }

            public GameObject GameObject { get; }
            public Rigidbody2D Body { get; }
            public Slime2D Enemy { get; }
        }
    }
}
