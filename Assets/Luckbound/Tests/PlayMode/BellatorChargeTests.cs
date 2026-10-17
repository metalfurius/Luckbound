using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
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
    public sealed class BellatorChargeTests
    {
        private const string EnemyLayerName = "Enemies";
        private const string GroundLayerName = "Ground";
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<RoomLifetimeScope> _rooms = new List<RoomLifetimeScope>();
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _objects.Clear();
            _rooms.Clear();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _rooms.Count - 1; index >= 0; index--)
            {
                if (_rooms[index] != null) _rooms[index].Dispose();
            }

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) UnityEngine.Object.Destroy(_objects[index]);
            }

            Time.timeScale = _previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CatalogChargeDamagesPhysicalHuesoguardiaAndAppliesItsInitialForce()
        {
            ArenaFixture fixture = CreateArena(PlayableRaceRules.HumanRaceId, new Vector2(0f, 1.5f), new Vector2(2.7f, 0.5f));
            yield return Settle(fixture.Player);
            yield return ActivateBellatorCharge(fixture.Player);
            SkillUseStatus activationStatus = fixture.Motor.LastBellatorCombatResult.SkillOutcome.Status;

            for (int tick = 0; tick < 50 && fixture.Enemy.MeleeImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.BellatorChargeActivationCount, Is.EqualTo(1));
            Assert.That(activationStatus, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(fixture.Enemy.MeleeImpactCount, Is.EqualTo(1), "A physical charge collision resolves on its target's queued-impact tick.");
            Assert.That(fixture.Enemy.CurrentHealth, Is.LessThan(Huesoguardia2D.MaximumHealth));
            AssertVector(fixture.Enemy.LastMeleeKnockbackVelocity, new Vector2(6f, 3f));
            Assert.That(fixture.Enemy.AiControlSuspensionTicksRemaining, Is.GreaterThan(0));

            yield return AdvanceFixedTicks(24);
            Assert.That(fixture.Enemy.MeleeImpactCount, Is.EqualTo(1), "One charge activation registers this ActorId once, even while colliders remain in contact.");
        }

        [UnityTest]
        public IEnumerator OrciniChargeMultipliesBothCatalogKnockbackComponentsByTenPercent()
        {
            ArenaFixture human = CreateArena(PlayableRaceRules.HumanRaceId, new Vector2(0f, 1.5f), new Vector2(2.7f, 0.5f));
            ArenaFixture orcini = CreateArena(PlayableRaceRules.OrciniRaceId, new Vector2(20f, 1.5f), new Vector2(22.7f, 0.5f));
            yield return Settle(human.Player);
            yield return Settle(orcini.Player);

            yield return ActivateBellatorCharge(human.Player);
            for (int tick = 0; tick < 50 && human.Enemy.MeleeImpactCount == 0; tick++) yield return new WaitForFixedUpdate();
            yield return ActivateBellatorCharge(orcini.Player);
            for (int tick = 0; tick < 50 && orcini.Enemy.MeleeImpactCount == 0; tick++) yield return new WaitForFixedUpdate();

            Assert.That(human.Enemy.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(orcini.Enemy.MeleeImpactCount, Is.EqualTo(1));
            AssertVector(human.Enemy.LastMeleeKnockbackVelocity, new Vector2(6f, 3f));
            AssertVector(orcini.Enemy.LastMeleeKnockbackVelocity, new Vector2(6.6f, 3.3f));
            Assert.That(orcini.Enemy.LastMeleeKnockbackVelocity.x / human.Enemy.LastMeleeKnockbackVelocity.x,
                Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(orcini.Enemy.LastMeleeKnockbackVelocity.y / human.Enemy.LastMeleeKnockbackVelocity.y,
                Is.EqualTo(1.1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator NormalBellatorMeleeDamageDoesNotDisplaceItsAcceptedTarget()
        {
            ArenaFixture fixture = CreateArena(PlayableRaceRules.HumanRaceId, new Vector2(0f, 1.5f), new Vector2(1.1f, 0.5f));
            yield return Settle(fixture.Player);
            CapsuleCollider2D targetCollider = fixture.EnemyObject.GetComponent<CapsuleCollider2D>();
            targetCollider.size = new Vector2(0.9f, 1.2f);
            Physics2D.SyncTransforms();
            yield return AdvanceFixedTicks(3);

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Attack);

            for (int tick = 0; tick < 60 && fixture.Enemy.MeleeImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.MeleeAttackActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.MeleeImpactsQueuedCount, Is.EqualTo(1));
            Assert.That(fixture.Enemy.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(fixture.Enemy.CurrentHealth, Is.LessThan(Huesoguardia2D.MaximumHealth));
            AssertVector(fixture.Enemy.LastMeleeKnockbackVelocity, Vector2.zero);
            Assert.That(fixture.Enemy.AiControlSuspensionTicksRemaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RejectedDuplicateDirectHitDoesNotApplyItsAttachedKnockback()
        {
            ArenaFixture fixture = CreateArena(PlayableRaceRules.HumanRaceId, new Vector2(0f, 1.5f), new Vector2(2.7f, 0.5f));
            yield return Settle(fixture.Player);
            yield return ActivateBellatorCharge(fixture.Player);
            for (int tick = 0; tick < 50 && fixture.Enemy.MeleeImpactCount == 0; tick++) yield return new WaitForFixedUpdate();

            Vector2 acceptedKnockback = fixture.Enemy.LastMeleeKnockbackVelocity;
            int acceptedHealth = fixture.Enemy.CurrentHealth;
            ProjectileImpactTarget2D impactTarget = fixture.EnemyObject.GetComponent<ProjectileImpactTarget2D>();
            MethodInfo queueMeleeImpact = typeof(ProjectileImpactTarget2D).GetMethod(
                "QueueMeleeImpact", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueMeleeImpact, Is.Not.Null, "The duplicate is queued through the same target boundary as gameplay impacts.");
            var duplicate = new MeleeImpact(
                fixture.Motor.LastBellatorChargeHitId,
                fixture.Motor.ActorId,
                fixture.Enemy.ActorId,
                30m,
                fixture.Motor.transform.position.x,
                0,
                new Vector2(90f, 90f),
                18);
            queueMeleeImpact.Invoke(impactTarget, new object[] { duplicate });
            yield return new WaitForFixedUpdate();

            Assert.That(fixture.Enemy.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(fixture.Enemy.CurrentHealth, Is.EqualTo(acceptedHealth));
            AssertVector(fixture.Enemy.LastMeleeKnockbackVelocity, acceptedKnockback);
            Assert.That(Mathf.Abs(fixture.EnemyBody.linearVelocity.x), Is.LessThan(90f));
        }

        [UnityTest]
        public IEnumerator MagusFireballBurnPulseDamagesWithoutOutgoingDisplacement()
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            int enemyLayer = RequiredLayer(EnemyLayerName);
            CreateFloor(new Vector2(0f, -0.5f));
            PlayerFixture player = CreatePlayer(new Vector2(0f, 1.5f), 1 << groundLayer, 1 << enemyLayer,
                PlayableRaceRules.HumanRaceId, configureBellator: false);
            CreateMagusRoom(player, 1 << enemyLayer);
            HuesoguardiaFixture enemy = CreateHuesoguardia(new Vector2(9.5f, 0.5f), player.Motor, 1 << groundLayer);
            Physics2D.SyncTransforms();
            yield return AdvanceFixedTicks(4);

            player.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            player.Input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 15 && player.Motor.MagusSkillActivationCount == 0; tick++) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.MagusSkillActivationCount, Is.EqualTo(1));

            for (int tick = 0; tick < 150 && enemy.Enemy.ProjectileImpactCount < 2; tick++) yield return new WaitForFixedUpdate();

            Assert.That(enemy.Enemy.ProjectileImpactCount, Is.GreaterThanOrEqualTo(2), "The physical fireball and its periodic burn pulse both resolve through projectile-impact damage.");
            Assert.That(enemy.Enemy.CurrentHealth, Is.LessThan(Huesoguardia2D.MaximumHealth));
            AssertVector(enemy.Enemy.LastMeleeKnockbackVelocity, Vector2.zero);
            Assert.That(enemy.Enemy.AiControlSuspensionTicksRemaining, Is.Zero);
            Assert.That(enemy.Body.linearVelocity.x, Is.EqualTo(0f).Within(0.05f), "This dormant target is outside its detection radius, so projectile ticks have no unrelated chase movement.");
        }

        [UnityTest]
        public IEnumerator ChargeDamagesCustodianWithoutDisplacingTheBoss()
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            int enemyLayer = RequiredLayer(EnemyLayerName);
            CreateFloor(new Vector2(0f, -0.5f));
            PlayerFixture player = CreatePlayer(new Vector2(0f, 1.5f), 1 << groundLayer, 1 << enemyLayer,
                PlayableRaceRules.HumanRaceId, configureBellator: true);
            var bossObject = Track(new GameObject("Bellator Charge Test Custodian"));
            bossObject.layer = enemyLayer;
            bossObject.transform.position = new Vector2(2.7f, 0.5f);
            GameObject waveParent = Track(new GameObject("Bellator Boss Wave Parent"));
            GameObject wavePrefab = Track(new GameObject("Bellator Boss Wave Prefab"));
            wavePrefab.AddComponent<Rigidbody2D>();
            wavePrefab.AddComponent<CircleCollider2D>();
            wavePrefab.SetActive(false);
            CustodianBossDefinition definition = CustodianBossJsonLoader.Load(
                Resources.Load<TextAsset>("Luckbound/Data/Encounters/CustodianBoss").text);
            var bossBody = bossObject.AddComponent<Rigidbody2D>();
            bossBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            bossBody.mass = 1000f;
            CapsuleCollider2D bossCollider = bossObject.AddComponent<CapsuleCollider2D>();
            bossCollider.size = new Vector2(0.9f, 0.9f);
            bossObject.AddComponent<SpriteRenderer>();
            CustodianBoss2D boss = bossObject.AddComponent<CustodianBoss2D>();
            boss.Initialize(ActorId.New(), player.Motor, definition, wavePrefab, waveParent.transform);
            Physics2D.SyncTransforms();
            yield return Settle(player);
            yield return ActivateBellatorCharge(player);

            for (int tick = 0; tick < 50 && boss.MeleeImpactCount == 0; tick++) yield return new WaitForFixedUpdate();

            Assert.That(boss.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(boss.CurrentHealth, Is.LessThan(boss.MaximumHealth), "Bosses still receive Charge damage.");
            Assert.That(Mathf.Abs(bossBody.linearVelocity.x), Is.LessThan(0.05f), "Custodian's existing melee resolver accepts damage and ignores the optional knockback payload.");
        }

        [UnityTest]
        public IEnumerator ChargeCastStopsAtGroundLayerWalls()
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            int enemyLayer = RequiredLayer(EnemyLayerName);
            CreateFloor(new Vector2(0f, -0.5f));
            PlayerFixture player = CreatePlayer(new Vector2(0f, 1.5f), 1 << groundLayer, 1 << enemyLayer,
                PlayableRaceRules.HumanRaceId, configureBellator: true);
            GameObject wall = Track(new GameObject("Bellator Charge Wall"));
            wall.layer = groundLayer;
            wall.transform.position = new Vector2(1.5f, 2f);
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(0.3f, 5f);
            Physics2D.SyncTransforms();
            yield return AdvanceFixedTicks(4);

            player.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            player.Input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 15 && player.Motor.BellatorChargeActivationCount == 0; tick++) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.BellatorChargeActivationCount, Is.EqualTo(1));
            for (int tick = 0; tick < 5 && player.Motor.IsCharging; tick++) yield return new WaitForFixedUpdate();

            Assert.That(player.Motor.IsCharging, Is.False);
            Assert.That(player.Body.position.x, Is.LessThan(wall.transform.position.x - (wallCollider.size.x * 0.5f)));
            Assert.That(Mathf.Abs(player.Body.linearVelocity.x), Is.LessThan(0.05f));
        }

        private IEnumerator ActivateBellatorCharge(PlayerFixture fixture)
        {
            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 15 && fixture.Motor.BellatorChargeActivationCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator Settle(PlayerFixture fixture)
        {
            Physics2D.SyncTransforms();
            for (int tick = 0; tick < 30 && !fixture.Motor.IsGrounded; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsGrounded, Is.True, "The player must be grounded before movement acceptance begins.");
        }

        private static IEnumerator AdvanceFixedTicks(int count)
        {
            for (int tick = 0; tick < count; tick++) yield return new WaitForFixedUpdate();
        }

        private ArenaFixture CreateArena(string raceId, Vector2 playerPosition, Vector2 enemyPosition)
        {
            int groundLayer = RequiredLayer(GroundLayerName);
            int enemyLayer = RequiredLayer(EnemyLayerName);
            CreateFloor(new Vector2(playerPosition.x, -0.5f));
            PlayerFixture player = CreatePlayer(playerPosition, 1 << groundLayer, 1 << enemyLayer, raceId, configureBellator: true);
            HuesoguardiaFixture enemy = CreateHuesoguardia(enemyPosition, player.Motor, 1 << groundLayer);
            Physics2D.SyncTransforms();
            return new ArenaFixture(player, enemy);
        }

        private PlayerFixture CreatePlayer(
            Vector2 position,
            LayerMask groundLayers,
            LayerMask enemyLayers,
            string raceId,
            bool configureBellator)
        {
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var playerObject = Track(new GameObject("Bellator Charge Test Player"));
            playerObject.transform.position = position;
            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = playerObject.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, groundLayers, new CombatStats(100, 50, 0), enemyLayers, raceId);
            if (configureBellator)
            {
                SkillCatalog catalog = LoadSkills();
                motor.ConfigureBellator(catalog, new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });
            }

            return new PlayerFixture(playerObject, body, motor, input);
        }

        private HuesoguardiaFixture CreateHuesoguardia(Vector2 position, PlayerMotor2D player, LayerMask groundLayers)
        {
            var enemyObject = Track(new GameObject("Bellator Charge Test Huesoguardia"));
            enemyObject.layer = RequiredLayer(EnemyLayerName);
            enemyObject.transform.position = position;
            Rigidbody2D body = enemyObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = enemyObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.9f);
            Huesoguardia2D enemy = enemyObject.AddComponent<Huesoguardia2D>();
            enemy.Initialize(ActorId.New(), player, groundLayers);
            return new HuesoguardiaFixture(enemyObject, body, enemy);
        }

        private void CreateMagusRoom(PlayerFixture player, LayerMask enemyLayers)
        {
            var poolObject = Track(new GameObject("Magus Periodic Test Projectile Pool"));
            ProjectilePool2D pool = poolObject.AddComponent<ProjectilePool2D>();
            var prefab = Track(new GameObject("Magus Periodic Test Fireball Prefab"));
            Rigidbody2D projectileBody = prefab.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            CircleCollider2D projectileCollider = prefab.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.12f;
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            pool.Initialize(prefab);

            var roomObject = Track(new GameObject("Magus Periodic Test Room"));
            RoomLifetimeScope room = roomObject.AddComponent<RoomLifetimeScope>();
            room.Initialize(RoomInstanceId.New(), "room.test.magus.periodic");
            _rooms.Add(room);

            var scopeObject = Track(new GameObject("Magus Periodic Test Projectile Scope"));
            RoomProjectileScope scope = scopeObject.AddComponent<RoomProjectileScope>();
            scope.Initialize(EntityId.New(), pool);
            room.OwnActor(scopeObject);
            room.OwnResource(scope);

            player.Motor.ConfigureMagus(
                LoadSkills(),
                new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId },
                pool,
                scope,
                room,
                enemyLayers);
        }

        private GameObject CreateFloor(Vector2 position)
        {
            var floor = Track(new GameObject("Bellator Charge Test Ground"));
            floor.layer = RequiredLayer(GroundLayerName);
            floor.transform.position = position;
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
            return floor;
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static SkillCatalog LoadSkills()
        {
            TextAsset skills = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skills, Is.Not.Null, "Combat acceptance must consume the authoritative Skills.json catalog.");
            return SkillCatalogJsonLoader.Load(skills.text);
        }

        private static int RequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The " + layerName + " layer is required by this physical scenario.");
            return layer;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
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

        private sealed class HuesoguardiaFixture
        {
            public HuesoguardiaFixture(GameObject gameObject, Rigidbody2D body, Huesoguardia2D enemy)
            {
                GameObject = gameObject;
                Body = body;
                Enemy = enemy;
            }

            public GameObject GameObject { get; }
            public Rigidbody2D Body { get; }
            public Huesoguardia2D Enemy { get; }
        }

        private sealed class ArenaFixture
        {
            public ArenaFixture(PlayerFixture player, HuesoguardiaFixture enemy)
            {
                Player = player;
                EnemyObject = enemy.GameObject;
                EnemyBody = enemy.Body;
                Enemy = enemy.Enemy;
            }

            public PlayerFixture Player { get; }
            public GameObject EnemyObject { get; }
            public Rigidbody2D EnemyBody { get; }
            public Huesoguardia2D Enemy { get; }
            public PlayerMotor2D Motor => Player.Motor;
            public PlayerInputBuffer Input => Player.Input;
        }
    }
}
