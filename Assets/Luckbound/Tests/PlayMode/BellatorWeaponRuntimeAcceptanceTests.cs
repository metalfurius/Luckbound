using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class BellatorWeaponRuntimeAcceptanceTests
    {
        private const string RustedSwordId = "weapon.bellator.rusted_sword";
        private const string TrailSaberId = "weapon.bellator.trail_saber";
        private const string UmbraGreatswordId = "weapon.bellator.umbra_greatsword";

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
        public IEnumerator RustedSwordCatalogProfileReachesRealDamageAndTwentySevenTickCadence()
        {
            return VerifyBellatorWeapon(
                RustedSwordId,
                expectedDamage: 12,
                expectedIntervalSeconds: 0.45f,
                expectedIntervalTicks: 27,
                expectedTrait: WeaponTraitId.Balanced);
        }

        [UnityTest]
        public IEnumerator TrailSaberCatalogProfileReachesRealDamageAndTwentyTickCadence()
        {
            return VerifyBellatorWeapon(
                TrailSaberId,
                expectedDamage: 9,
                expectedIntervalSeconds: 0.33f,
                expectedIntervalTicks: 20,
                expectedTrait: WeaponTraitId.RapidCadence);
        }

        [UnityTest]
        public IEnumerator EpicSwordAtPlusThreeScalesRealDamageWithoutIntermediateRounding()
        {
            return VerifyBellatorWeapon(RustedSwordId, 12, 0.45f, 27, WeaponTraitId.Balanced,
                rarity: ItemRarity.Epic, upgradeLevel: 3, expectedRawDamage: 17.94m, expectedHitDamage: 18);
        }

        [UnityTest]
        public IEnumerator UmbraGreatswordCatalogProfileReachesRealDamageAndFortyTwoTickCadence()
        {
            return VerifyBellatorWeapon(
                UmbraGreatswordId,
                expectedDamage: 18,
                expectedIntervalSeconds: 0.70f,
                expectedIntervalTicks: 42,
                expectedTrait: WeaponTraitId.GreaterRange,
                additionalTrait: WeaponTraitId.GreaterKnockback);
        }

        [UnityTest]
        public IEnumerator SwordDoesNotReachTheTargetInsideUmbraOnlyRange()
        {
            ExpeditionRunConfiguration run = CreateBoundBellatorRun(
                ExpeditionRunConfiguration.HumanRaceId,
                RustedSwordId,
                out PlayerMotor2D motor,
                out _,
                out _,
                out PlayerInputBuffer input);
            int enemyLayer = RequiredLayer("Enemies");
            ProjectileImpactTarget2D target = CreateImpactTarget(
                enemyLayer,
                new Vector2(2f, motor.transform.position.y + 0.9f),
                new Vector2(0.2f, 0.2f));
            Physics2D.SyncTransforms();

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            input.Release(PlayerAction.Attack);
            while (motor.SimulationTick < attackStartTick + 6) yield return new WaitForFixedUpdate();

            Assert.That(target.PendingMeleeImpactCount, Is.Zero,
                "The worn sword's 1.35-unit box must not overlap a target at the Umbra-only distance.");
            Assert.That(motor.MeleeImpactsQueuedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UmbraMirrorsItsReachAndAppliesOrciniKnockbackAwayForTwelveTicks()
        {
            ExpeditionRunConfiguration run = CreateBoundBellatorRun(
                PlayableRaceRules.OrciniRaceId,
                UmbraGreatswordId,
                out PlayerMotor2D motor,
                out _,
                out _,
                out PlayerInputBuffer input);
            ProjectileImpactTarget2D target = CreateImpactTarget(
                RequiredLayer("Enemies"),
                new Vector2(-2f, motor.transform.position.y + 0.9f),
                new Vector2(0.2f, 0.2f));
            BoxCollider2D siblingCollider = target.gameObject.AddComponent<BoxCollider2D>();
            siblingCollider.size = new Vector2(0.2f, 0.2f);
            Physics2D.SyncTransforms();

            input.SetMoveX(-1f);
            yield return new WaitForFixedUpdate();
            input.SetMoveX(0f);
            Assert.That(motor.FacingDirection, Is.EqualTo(-1f), "A left-facing actor mirrors the hitbox center.");

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            input.Release(PlayerAction.Attack);
            while (motor.SimulationTick < attackStartTick + 6) yield return new WaitForFixedUpdate();

            Assert.That(target.PendingMeleeImpactCount, Is.EqualTo(1),
                "A target just inside Umbra's mirrored 1.80-unit reach must be collected by real Physics2D overlap.");
            Assert.That(motor.MeleeImpactsQueuedCount, Is.EqualTo(1),
                "Sibling colliders for the same ActorId must enqueue only one direct hit.");
            Assert.That(target.TryDequeueMeleeImpactForTick(long.MaxValue, out MeleeImpact impact), Is.True);
            Assert.That(impact.Damage, Is.EqualTo(18m), "The physical profile must preserve Umbra's damage.");
            AssertVector(impact.KnockbackVelocity, new Vector2(-6.6f, 2.75f));
            Assert.That(impact.AiControlSuspensionTicks, Is.EqualTo(12),
                "Orcini scales both velocity components without extending the 12-tick AI suspension.");
            Assert.That(impact.SourceActorId, Is.EqualTo(motor.ActorId));
            Assert.That(impact.TargetActorId, Is.EqualTo(target.ActorId));
            Assert.That(run.RaceId, Is.EqualTo(PlayableRaceRules.OrciniRaceId));
        }

        [UnityTest]
        public IEnumerator UmbraDamagesCustodianWithoutDisplacingTheBoss()
        {
            CreateBoundBellatorRun(
                ExpeditionRunConfiguration.HumanRaceId,
                UmbraGreatswordId,
                out PlayerMotor2D motor,
                out _,
                out _,
                out PlayerInputBuffer input);
            int enemyLayer = RequiredLayer("Enemies");
            var bossObject = Track(new GameObject("G01 Umbra Custodian receiver"));
            bossObject.layer = enemyLayer;
            bossObject.transform.position = new Vector2(1.4f, motor.transform.position.y + 0.9f);
            Rigidbody2D body = bossObject.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = bossObject.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.9f, 0.9f);
            bossObject.AddComponent<SpriteRenderer>();
            CustodianBoss2D boss = bossObject.AddComponent<CustodianBoss2D>();
            GameObject waveParent = Track(new GameObject("G01 Umbra Custodian wave parent"));
            GameObject wavePrefab = Track(new GameObject("G01 Umbra Custodian wave prefab"));
            wavePrefab.AddComponent<Rigidbody2D>();
            wavePrefab.AddComponent<CircleCollider2D>();
            wavePrefab.SetActive(false);
            TextAsset bossSource = Resources.Load<TextAsset>("Luckbound/Data/Encounters/CustodianBoss");
            Assert.That(bossSource, Is.Not.Null, "The real Custodian definition is required for boss immunity acceptance.");
            CustodianBossDefinition definition = CustodianBossJsonLoader.Load(bossSource.text);
            boss.Initialize(ActorId.New(), motor, definition, wavePrefab, waveParent.transform);
            Physics2D.SyncTransforms();

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            input.Release(PlayerAction.Attack);
            while (motor.SimulationTick < attackStartTick + 6) yield return new WaitForFixedUpdate();
            for (int tick = 0; tick < 12 && boss.MeleeImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(motor.MeleeImpactsQueuedCount, Is.EqualTo(1));
            Assert.That(boss.MeleeImpactCount, Is.EqualTo(1));
            Assert.That(boss.CurrentHealth, Is.LessThan(boss.MaximumHealth),
                "Custodian remains a valid damage receiver while ignoring Umbra's push.");
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.05f),
                "Bosses are immune to direct melee displacement.");
        }

        private IEnumerator VerifyBellatorWeapon(
            string weaponId,
            int expectedDamage,
            float expectedIntervalSeconds,
            int expectedIntervalTicks,
            WeaponTraitId expectedTrait,
            WeaponTraitId? additionalTrait = null, ItemRarity rarity = ItemRarity.Common, int upgradeLevel = 0,
            decimal? expectedRawDamage = null, int? expectedHitDamage = null)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });

            if (weaponId != RustedSwordId || rarity != ItemRarity.Common || upgradeLevel != 0)
            {
                EquipWeapon(run.Inventory, weaponId, rarity, upgradeLevel);
            }

            Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo(weaponId),
                "The run inventory must hold the requested weapon before composition.");

            ItemCatalog itemCatalog = run.Inventory.Catalog;
            WeaponCatalog weaponCatalog = LoadWeaponCatalog(itemCatalog);
            WeaponProfile expectedProfile = weaponCatalog.Get(weaponId);
            Assert.That(expectedProfile.ClassId, Is.EqualTo(BellatorSkillLoadout.ClassId));
            Assert.That(expectedProfile.Damage, Is.EqualTo(expectedDamage));
            Assert.That(expectedProfile.AttackIntervalSeconds, Is.EqualTo(expectedIntervalSeconds).Within(0.0001f));
            Assert.That(WeaponBasicAttackProfile.Create(expectedProfile).AttackIntervalTicks, Is.EqualTo(expectedIntervalTicks));
            Assert.That(expectedProfile.HasTrait(expectedTrait), Is.True,
                "The declared qualitative weapon profile trait must remain present.");
            Assert.That(expectedProfile.HasTrait(WeaponTraitId.GreaterRange), Is.EqualTo(weaponId == UmbraGreatswordId),
                "Only Umbra declares greater reach than the worn sword.");
            Assert.That(expectedProfile.HasTrait(WeaponTraitId.GreaterKnockback), Is.EqualTo(weaponId == UmbraGreatswordId),
                "Only Umbra declares greater push than the worn sword.");
            WeaponMeleeParameters melee = expectedProfile.MeleeParameters;
            Assert.That(melee, Is.Not.Null, "Every Bellator weapon must resolve its authoritative melee parameters.");
            bool isUmbra = weaponId == UmbraGreatswordId;
            Assert.That(melee.CenterOffsetX, Is.EqualTo(isUmbra ? 1.05f : 0.90f).Within(0.0001f));
            Assert.That(melee.CenterOffsetY, Is.EqualTo(0.90f).Within(0.0001f));
            Assert.That(melee.Width, Is.EqualTo(isUmbra ? 1.80f : 1.35f).Within(0.0001f));
            Assert.That(melee.Height, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(melee.KnockbackHorizontalSpeed, Is.EqualTo(isUmbra ? 6f : 0f).Within(0.0001f));
            Assert.That(melee.KnockbackVerticalSpeed, Is.EqualTo(isUmbra ? 2.5f : 0f).Within(0.0001f));
            Assert.That(melee.AiControlSuspensionTicks, Is.EqualTo(isUmbra ? 12 : 0));
            if (additionalTrait.HasValue)
            {
                Assert.That(expectedProfile.HasTrait(additionalTrait.Value), Is.True,
                    "Umbra must retain both declared qualitative traits without adding numeric tuning.");
            }

            int groundLayer = RequiredLayer("Ground");
            int enemyLayer = RequiredLayer("Enemies");
            CreateFloor(groundLayer);
            if (weaponId == UmbraGreatswordId)
            {
                var raisedEnemyPlatform = Track(new GameObject("G01 Umbra receiver platform"));
                raisedEnemyPlatform.layer = groundLayer;
                raisedEnemyPlatform.transform.position = new Vector2(2.5f, 0.5f);
                BoxCollider2D platformCollider = raisedEnemyPlatform.AddComponent<BoxCollider2D>();
                platformCollider.size = new Vector2(4f, 1f);
            }
            CreatePlayer(
                out PlayerMotor2D motor,
                out ItemCollector2D collector,
                out ActorId actorId,
                out PlayerInputBuffer input,
                new Vector2(0f, 0.75f),
                freezePosition: weaponId != UmbraGreatswordId);

            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: 1 << groundLayer,
                enemyLayers: 1 << enemyLayer,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                weaponCatalog: weaponCatalog,
                skillCatalog: LoadSkillCatalog());

            Assert.That(motor.EquippedWeaponProfile, Is.SameAs(expectedProfile),
                "The equipped inventory item must resolve to its authoritative catalog profile on PlayerMotor2D.");
            Assert.That(motor.EquippedWeaponProfile.ItemId, Is.EqualTo(run.Inventory.Weapon.ItemId));
            Assert.That(motor.EquippedWeaponProfile.ClassId, Is.EqualTo(run.ClassId));
            Assert.That(motor.EquippedWeaponBasicAttackProfile.Rarity, Is.EqualTo(rarity));
            Assert.That(motor.EquippedWeaponBasicAttackProfile.UpgradeLevel, Is.EqualTo(upgradeLevel));

            Slime2D damageReceiver = CreateSlimeDamageReceiver(
                motor,
                groundLayer,
                enemyLayer,
                new Vector2(1f, motor.transform.position.y + 0.9f),
                freezeBody: weaponId != UmbraGreatswordId);
            Physics2D.SyncTransforms();

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            long attackStartTick = motor.SimulationTick;
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True,
                "A simulated Attack input must start the composed player's melee attack.");
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1));
            input.Release(PlayerAction.Attack);

            while (motor.SimulationTick < attackStartTick + 6)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(motor.CurrentMeleeHitWindow, Is.Not.Null,
                "The weapon profile must open the real six-tick melee impact window.");
            Assert.That(motor.CurrentMeleeHitWindow.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));

            for (int tick = 0; tick < 12 && damageReceiver.MeleeImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(damageReceiver.MeleeImpactCount, Is.EqualTo(1),
                "Physics2D overlap must queue and resolve one impact on the real Slime damage receiver.");
            Assert.That(damageReceiver.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth - (expectedHitDamage ?? expectedDamage)),
                "The first resolved hit must apply exactly the equipped weapon's catalog damage.");
            Assert.That(motor.MeleeImpactsQueuedCount, Is.EqualTo(1));
            if (weaponId == UmbraGreatswordId)
            {
                AssertVector(damageReceiver.LastMeleeKnockbackVelocity, new Vector2(6f, 2.5f));
                Assert.That(damageReceiver.AiControlSuspensionTicksRemaining, Is.GreaterThan(0));
                Assert.That(damageReceiver.GetComponent<Rigidbody2D>().constraints,
                    Is.Not.EqualTo(RigidbodyConstraints2D.FreezeAll),
                    "The Umbra receiver remains dynamic so its accepted force can move it.");
            }
            else
            {
                AssertVector(damageReceiver.LastMeleeKnockbackVelocity, Vector2.zero);
                Assert.That(damageReceiver.AiControlSuspensionTicksRemaining, Is.Zero,
                    "Sword and saber direct impacts do not suspend enemy AI.");
            }

            while (motor.SimulationTick < attackStartTick + 11) yield return new WaitForFixedUpdate();
            Assert.That(motor.CurrentMeleeHitWindow, Is.Not.Null,
                "The damage window remains open through its inclusive tick 11.");
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + 12));
            Assert.That(motor.CurrentMeleeHitWindow, Is.Null,
                "The damage window closes at tick 12, preserving [6,12).");

            while (motor.SimulationTick < attackStartTick + expectedIntervalTicks - 2)
            {
                if (weaponId == UmbraGreatswordId)
                {
                    SetPlayerMovementTowardTargetWhenOutsideReach(motor, input, damageReceiver);
                }
                yield return new WaitForFixedUpdate();
            }
            input.SetMoveX(0f);

            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + expectedIntervalTicks - 1));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.False,
                "The equipped weapon must reject an attack one tick before its catalog cadence.");
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(1),
                "A rejected input must not count as an attack activation.");

            input.Release(PlayerAction.Attack);
            input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.SimulationTick, Is.EqualTo(attackStartTick + expectedIntervalTicks));
            Assert.That(motor.CurrentMeleeAttack.WasStartedThisTick, Is.True,
                "The equipped weapon must accept its next attack exactly at its catalog cadence.");
            Assert.That(motor.CurrentMeleeAttack.BaseDamage, Is.EqualTo(expectedRawDamage ?? expectedDamage));
            Assert.That(motor.CurrentMeleeAttack.DamageMultiplier, Is.EqualTo(1m),
                "The rejected pre-cadence input must not advance Bellator's accepted-attack sequence.");
            Assert.That(motor.MeleeAttackActivationCount, Is.EqualTo(2));

            for (int tick = 0; tick < 12 && damageReceiver.MeleeImpactCount < 2; tick++)
            {
                if (weaponId == UmbraGreatswordId)
                {
                    SetPlayerMovementTowardTargetWhenOutsideReach(motor, input, damageReceiver);
                }
                yield return new WaitForFixedUpdate();
            }
            input.SetMoveX(0f);

            Assert.That(damageReceiver.MeleeImpactCount, Is.EqualTo(2),
                "A later attack with a fresh hit ID must resolve through the same real damage receiver. " +
                "tick=" + motor.SimulationTick + "; player=" + motor.transform.position +
                "; playerVelocity=" + motor.Velocity + "; facing=" + motor.FacingDirection +
                "; slime=" + damageReceiver.transform.position +
                "; slimeVelocity=" + damageReceiver.GetComponent<Rigidbody2D>().linearVelocity +
                "; queued=" + motor.MeleeImpactsQueuedCount + "; health=" + damageReceiver.CurrentHealth + ".");
            Assert.That(damageReceiver.CurrentHealth, Is.EqualTo(Mathf.Max(0, Slime2D.MaximumHealth - (expectedHitDamage ?? expectedDamage) * 2)),
                "The second resolved hit must preserve the equipped weapon's damage at its accepted cadence.");
        }

        private void EquipWeapon(ExpeditionInventory inventory, string weaponId,
            ItemRarity rarity = ItemRarity.Common, int upgradeLevel = 0)
        {
            var stack = new InventoryItemStack(
                ItemInstanceId.New(),
                weaponId,
                rarity,
                upgradeLevel: upgradeLevel,
                quantity: 1);
            InventoryOperationOutcome collected = inventory.TryCollectItem(stack, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "The alternative Bellator weapon must enter the run inventory.");

            int backpackSlot = -1;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                if (inventory.GetBackpackSlot(index)?.InstanceId == stack.InstanceId)
                {
                    backpackSlot = index;
                    break;
                }
            }

            Assert.That(backpackSlot, Is.GreaterThanOrEqualTo(0), "The collected weapon must occupy a real backpack slot.");
            InventoryOperationOutcome equipped = inventory.EquipBackpackItem(
                backpackSlot,
                EquipmentSlot.Weapon,
                TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "The selected alternative weapon must equip through the inventory API.");
        }

        private void CreateFloor(int groundLayer)
        {
            var floor = Track(new GameObject("G01 Bellator weapon acceptance ground"));
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
        }

        private void CreatePlayer(
            out PlayerMotor2D motor,
            out ItemCollector2D collector,
            out ActorId actorId,
            out PlayerInputBuffer input,
            Vector2 position,
            bool freezePosition = true)
        {
            actorId = ActorId.New();
            input = new PlayerInputBuffer(actorId);
            var player = Track(new GameObject("G01 Bellator weapon acceptance player"));
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = freezePosition ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            motor = player.AddComponent<PlayerMotor2D>();
            collector = player.AddComponent<ItemCollector2D>();
        }

        private Slime2D CreateSlimeDamageReceiver(
            PlayerMotor2D player,
            int groundLayer,
            int enemyLayer,
            Vector2 position,
            bool freezeBody = true)
        {
            var enemy = Track(new GameObject("G01 Bellator weapon acceptance Slime receiver"));
            enemy.layer = enemyLayer;
            enemy.transform.position = position;
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = enemy.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            Slime2D receiver = enemy.AddComponent<Slime2D>();
            receiver.Initialize(ActorId.New(), player, 1 << groundLayer);

            // Keep sword/saber receivers stationary because their authored push is zero.
            // Umbra tests leave the receiver dynamic and move the player back into reach after a push.
            if (freezeBody) body.constraints = RigidbodyConstraints2D.FreezeAll;
            return receiver;
        }

        private ExpeditionRunConfiguration CreateBoundBellatorRun(
            string raceId,
            string weaponId,
            out PlayerMotor2D motor,
            out ItemCollector2D collector,
            out ActorId actorId,
            out PlayerInputBuffer input,
            bool freezePlayer = true)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId,
                raceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });
            if (weaponId != RustedSwordId) EquipWeapon(run.Inventory, weaponId);

            int groundLayer = RequiredLayer("Ground");
            int enemyLayer = RequiredLayer("Enemies");
            CreateFloor(groundLayer);
            CreatePlayer(out motor, out collector, out actorId, out input,
                new Vector2(0f, 0.75f), freezePlayer);
            ItemCatalog itemCatalog = run.Inventory.Catalog;
            WeaponCatalog weaponCatalog = LoadWeaponCatalog(itemCatalog);
            ExpeditionBellatorRunBinder.Bind(
                run,
                actorId: actorId,
                inputSource: input,
                groundLayers: 1 << groundLayer,
                enemyLayers: 1 << enemyLayer,
                defense: 0,
                playerMotor: motor,
                itemCollector: collector,
                itemCatalog: itemCatalog,
                weaponCatalog: weaponCatalog,
                skillCatalog: LoadSkillCatalog());
            return run;
        }

        private ProjectileImpactTarget2D CreateImpactTarget(
            int enemyLayer,
            Vector2 position,
            Vector2 size)
        {
            var targetObject = Track(new GameObject("G01 Bellator physical impact target"));
            targetObject.layer = enemyLayer;
            targetObject.transform.position = position;
            BoxCollider2D collider = targetObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            ProjectileImpactTarget2D target = targetObject.AddComponent<ProjectileImpactTarget2D>();
            target.Initialize(ActorId.New());
            return target;
        }

        private static void SetPlayerMovementTowardTargetWhenOutsideReach(
            PlayerMotor2D motor,
            PlayerInputBuffer input,
            Slime2D target)
        {
            WeaponMeleeParameters melee = motor.EquippedWeaponBasicAttackProfile.MeleeParameters;
            float targetOffsetX = target.transform.position.x - motor.transform.position.x;
            float direction = Mathf.Abs(targetOffsetX) > 0.001f ? Mathf.Sign(targetOffsetX) : motor.FacingDirection;
            float maximumCenterDistance = melee.CenterOffsetX + (melee.Width * 0.5f) +
                target.GetComponent<Collider2D>().bounds.extents.x - 0.15f;
            input.SetMoveX(Mathf.Abs(targetOffsetX) > maximumCenterDistance ||
                Mathf.Sign(direction) != Mathf.Sign(motor.FacingDirection) ? direction : 0f);
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private static int RequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The " + layerName + " layer is required by the runtime acceptance fixture.");
            return layer;
        }

        private static WeaponCatalog LoadWeaponCatalog(ItemCatalog itemCatalog)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(source, Is.Not.Null, "The authoritative weapon catalog must be loaded before binding.");
            return WeaponCatalogJsonLoader.Load(source.text, itemCatalog);
        }

        private static SkillCatalog LoadSkillCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "The authoritative Bellator skill catalog must be loaded before binding.");
            return SkillCatalogJsonLoader.Load(source.text);
        }
    }
}
