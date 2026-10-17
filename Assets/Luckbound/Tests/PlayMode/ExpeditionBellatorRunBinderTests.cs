using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionBellatorRunBinderTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private Vector2 _previousGravity;
        private float _previousFixedDeltaTime;
        private float _previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _previousTimeScale = Time.timeScale;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in _objects)
            {
                if (gameObject != null) Object.Destroy(gameObject);
            }
            _objects.Clear();
            yield return null;
            Physics2D.gravity = _previousGravity;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator DvergarRaceFromRunConfigurationReachesMotorAndPhysicalWorldPickup()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.DvergarRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId },
                healthUpgradeLevel: 2,
                energyUpgradeLevel: 1);
            ItemCatalog itemCatalog = run.Inventory.Catalog;
            SkillCatalog skillCatalog = LoadSkillCatalog();
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreateFloor();
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector, allowMovement: true);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 7,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                skillCatalog: skillCatalog);

            Assert.That(motor.IsBellator, Is.True);
            Assert.That(motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.GrantsDvergarBonus, Is.True);
            Assert.That(collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(collector.Catalog, Is.SameAs(itemCatalog));
            Assert.That(motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CombatStats.MaximumEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CurrentHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CombatStats.Defense, Is.EqualTo(7), "Defense is passed as an independent calculated value.");

            var pickupObject = Track(new GameObject("E09 Dvergar physical ore pickup"));
            pickupObject.transform.position = new Vector2(1f, 0.5f);
            Rigidbody2D pickupBody = pickupObject.AddComponent<Rigidbody2D>();
            pickupBody.bodyType = RigidbodyType2D.Kinematic;
            pickupBody.gravityScale = 0f;
            CircleCollider2D pickupCollider = pickupObject.AddComponent<CircleCollider2D>();
            pickupCollider.radius = 0.25f;
            WorldMaterialPickup2D pickup = pickupObject.AddComponent<WorldMaterialPickup2D>();
            pickup.Initialize(MaterialKind.Ore, 5, itemCatalog);

            Physics2D.SyncTransforms();
            for (int tick = 0; tick < 30 && !motor.IsGrounded; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.True, "The player must stand on real ground before moving into the pickup.");

            input.SetMoveX(1f);
            for (int tick = 0; tick < 60 && !pickup.IsConsumed; tick++)
                yield return new WaitForFixedUpdate();
            input.SetMoveX(0f);

            Assert.That(pickup.IsConsumed, Is.True, "The real world pickup trigger must be consumed by the configured collector.");
            Assert.That(motor.transform.position.x, Is.GreaterThan(0f), "Simulated movement input must carry the real player into pickup range.");
            Assert.That(run.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(6));
            Assert.That(run.Inventory.GetWorldMaterialRemainder(MaterialKind.Ore), Is.Zero);
            Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.Collected));
        }

        [UnityTest]
        public IEnumerator AlvariRunSkillChoiceUsesItsDiscountedBellatorKitCost()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.AlvariRaceId,
                new[] { BellatorSkillLoadout.GuardId, BellatorSkillLoadout.ChargeId },
                healthUpgradeLevel: 1,
                energyUpgradeLevel: 2);
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 3,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: run.Inventory.Catalog,
                skillCatalog: LoadSkillCatalog());

            Assert.That(motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(motor.CombatStats.MaximumHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CombatStats.MaximumEnergy, Is.EqualTo(run.StartingEnergy));
            Assert.That(motor.CurrentHealth, Is.EqualTo(run.StartingHealth));
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy));

            input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 12 && motor.BellatorGuardActivationCount == 0; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(motor.BellatorGuardActivationCount, Is.EqualTo(1),
                "Skill1 must come from the Alvari run's selected Bellator kit.");
            Assert.That(motor.LastBellatorCombatResult.SkillOutcome.EnergySpent, Is.EqualTo(11),
                "The 12 EN Guard cost is discounted by Alvari's configured race rule.");
            Assert.That(motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy - 11));
        }

        [UnityTest]
        public IEnumerator NonBellatorRunIsRejectedWithoutPartiallyConfiguringEitherComponent()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                "class.venator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.venator.piercing_arrow", "skill.venator.triple_shot" });
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector);

            Assert.Throws<System.ArgumentException>(() => ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: GroundLayers(),
                enemyLayers: 0,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: run.Inventory.Catalog,
                skillCatalog: LoadSkillCatalog()));

            Assert.That(motor.ActorId.IsValid, Is.False);
            Assert.That(motor.CombatStats, Is.Null);
            Assert.That(motor.IsBellator || motor.IsVenator || motor.IsMagus, Is.False);
            Assert.That(collector.Inventory, Is.Null);
            Assert.That(collector.Catalog, Is.Null);
            Assert.That(collector.RaceId, Is.Null);
            Assert.That(collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        private void CreatePlayer(out PlayerMotor2D motor, out ItemCollector2D collector, bool allowMovement = false)
        {
            var player = Track(new GameObject("E09 Bellator run player"));
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            player.transform.position = allowMovement ? new Vector2(0f, 0.75f) : Vector2.zero;
            body.gravityScale = allowMovement ? 3f : 0f;
            body.constraints = allowMovement ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.75f, 1.5f);
            motor = player.AddComponent<PlayerMotor2D>();
            collector = player.AddComponent<ItemCollector2D>();
        }

        private void CreateFloor()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by PlayerMotor2D.");
            var floor = Track(new GameObject("E09 Bellator run ground"));
            floor.layer = layer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static int GroundLayers()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by PlayerMotor2D.");
            return 1 << layer;
        }

        private static SkillCatalog LoadSkillCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The accepted Bellator skill catalog must be loaded before binding.");
            return SkillCatalogJsonLoader.Load(source.text);
        }
    }
}
