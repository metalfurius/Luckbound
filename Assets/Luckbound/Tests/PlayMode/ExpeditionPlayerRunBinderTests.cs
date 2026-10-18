using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionPlayerRunBinderTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<RoomLifetimeScope> _rooms = new List<RoomLifetimeScope>();
        private Vector2 _previousGravity;
        private float _previousFixedDeltaTime;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _previousTimeScale = Time.timeScale;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _rooms.Count - 1; index >= 0; index--)
            {
                if (_rooms[index] != null) _rooms[index].Dispose();
            }
            _rooms.Clear();

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) Object.Destroy(_objects[index]);
            }
            _objects.Clear();
            yield return null;
            Physics2D.gravity = _previousGravity;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator BindsConfirmedBellatorRunEquipmentSkillsAndRace()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.DvergarRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                5,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills);

            AssertBoundRun(fixture, run, BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId,
                BellatorSkillLoadout.GuardId, baseDefense: 5);
            Assert.That(fixture.Motor.IsBellator, Is.True);
            Assert.That(fixture.Motor.IsVenator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(8));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RunBinderPassesDurableFortuneRuntimeToThePlayerActor()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            InventoryItemStack brew = EquipQuickConsumable(run, "consumable.fortune_brew", quantity: 2);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            var fortuneRuntime = new ImmediateFortuneBrewRuntime(run.Inventory);

            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fortuneBrewRuntime: fortuneRuntime);

            fixture.Input.Press(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Consumable);
            yield return new WaitForFixedUpdate();

            Assert.That(fortuneRuntime.CallCount, Is.EqualTo(1));
            Assert.That(fortuneRuntime.LastItemInstanceId, Is.EqualTo(brew.InstanceId));
            Assert.That(fortuneRuntime.LastTransactionId.IsValid, Is.True);
            Assert.That(fixture.Motor.LastConsumableUseOutcome.Status,
                Is.EqualTo(PlayerConsumableUseStatus.Consumed));
            Assert.That(fixture.Collector.Inventory.QuickConsumable.Quantity, Is.EqualTo(brew.Quantity - 1));
            Assert.That(fixture.Collector.Inventory.HasSuccessfulQuickConsumableConsumption(
                brew.InstanceId,
                "consumable.fortune_brew",
                fortuneRuntime.LastTransactionId), Is.True);
        }

        [UnityTest]
        public IEnumerator BindsConfirmedVenatorRunEquipmentSkillsRaceAndRoomScopes()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.AlvariRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                3,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room);

            AssertBoundRun(fixture, run, VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId,
                VenatorSkillLoadout.RootTrapId, baseDefense: 3);
            Assert.That(fixture.Motor.IsVenator, Is.True);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(5));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindsConfirmedMagusRunEquipmentSkillsRaceAndRoomScopes()
        {
            ExpeditionRunConfiguration run = CreateRun(
                MagusSkillLoadout.ClassId,
                ExpeditionRunConfiguration.OrciniRaceId,
                MagusSkillLoadout.FireballId,
                MagusSkillLoadout.FrostNovaId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                2,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room);

            AssertBoundRun(fixture, run, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId,
                MagusSkillLoadout.ArcaneSealId, baseDefense: 2);
            Assert.That(fixture.Motor.IsMagus, Is.True);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsVenator, Is.False);
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(3));
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsArmorDefenseOverflowBeforeMutatingEitherActorComponent()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);

            Assert.Throws<System.OverflowException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                int.MaxValue,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindsAllSixStandardArmorsWithInstanceUpgradeAndRuntimeModifiers()
        {
            ItemCatalog catalog = CreateRun(BellatorSkillLoadout.ClassId, ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId).Inventory.Catalog;
            foreach (ArmorProfile profile in LoadArmors(catalog).Profiles)
            {
                ExpeditionRunConfiguration run = CreateRun(
                    BellatorSkillLoadout.ClassId,
                    ExpeditionRunConfiguration.HumanRaceId,
                    BellatorSkillLoadout.ChargeId,
                    BellatorSkillLoadout.CircularCutId);
                const int upgradeLevel = 2;
                ReplaceArmor(run, profile.ItemId, upgradeLevel);
                BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);

                ExpeditionPlayerRunBinder.Bind(
                    run,
                    fixture.ActorId,
                    fixture.Input,
                    GroundLayers(),
                    EnemyLayers(),
                    7,
                    fixture.Motor,
                    fixture.Collector,
                    run.Inventory.Catalog,
                    fixture.Weapons,
                    fixture.Skills);

                ArmorResolution expected = ArmorStatsResolver.Resolve(
                    profile,
                    upgradeLevel,
                    run.StartingHealth,
                    run.StartingEnergy,
                    run.StartingHealth,
                    run.StartingEnergy);
                Assert.That(run.Inventory.Armor.ItemId, Is.EqualTo(profile.ItemId));
                Assert.That(run.Inventory.Armor.UpgradeLevel, Is.EqualTo(upgradeLevel));
                Assert.That(fixture.Motor.EquippedArmorResolution.Armor.ItemId, Is.EqualTo(profile.ItemId));
                Assert.That(fixture.Motor.EquippedArmorResolution.UpgradeLevel, Is.EqualTo(upgradeLevel));
                Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(7 + expected.Defense), profile.ItemId);
                Assert.That(fixture.Motor.CombatStats.MaximumHealth, Is.EqualTo(expected.MaximumHealth), profile.ItemId);
                Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(expected.MaximumEnergy), profile.ItemId);
                Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(expected.HealthAfterMaximumClamp), profile.ItemId);
                Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(expected.EnergyAfterMaximumClamp), profile.ItemId);
                Assert.That(fixture.Motor.EffectiveMaximumHorizontalSpeed,
                    Is.EqualTo(PlayerMotor2D.MaximumHorizontalSpeed * (100 + profile.MovementSpeedPercentModifier) / 100f)
                        .Within(0.0001f), profile.ItemId);
                Assert.That(fixture.Motor.ArmorEnergyRegenerationPerSecondBonus,
                    Is.EqualTo(profile.EnergyRegenerationPerSecondBonus), profile.ItemId);
                Assert.That(fixture.Motor.HarmfulStatusDurationReductionPercent,
                    Is.EqualTo(profile.HarmfulStatusDurationReductionPercent), profile.ItemId);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LeatherVestRaisesActualHorizontalMovementSpeed()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            ReplaceArmor(run, ArmorCatalog.LeatherVestItemId, upgradeLevel: 0);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills);

            fixture.Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            fixture.Input.SetMoveX(1f);
            while (fixture.Motor.SimulationTick < 12) yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.Velocity.x,
                Is.EqualTo(PlayerMotor2D.MaximumHorizontalSpeed * 1.03f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator RunicMantleAddsOneEnergyPerSecondAndBarkArmorReducesReceivedStun()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            ReplaceArmor(run, ArmorCatalog.RunicMantleItemId, upgradeLevel: 0);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills);
            fixture.Motor.SkillUses.SetCurrentEnergy(0);
            while (fixture.Motor.SimulationTick < 120) yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(8),
                "The existing five-per-second recovery plus the mantle's one-per-second bonus should be applied.");

            ExpeditionRunConfiguration barkRun = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            ReplaceArmor(barkRun, ArmorCatalog.BarkArmorItemId, upgradeLevel: 0);
            BindingFixture barkFixture = CreatePlayer(barkRun, includeRoomProjectiles: false);
            ExpeditionPlayerRunBinder.Bind(
                barkRun,
                barkFixture.ActorId,
                barkFixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                barkFixture.Motor,
                barkFixture.Collector,
                barkRun.Inventory.Catalog,
                barkFixture.Weapons,
                barkFixture.Skills);

            var hit = new DamageCalculationRequest(
                baseDamage: 1m,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: barkFixture.Motor.CombatStats,
                targetIsInvulnerable: false);
            barkFixture.Motor.ReceiveDirectImpact(
                HitId.New(), hit, impactTick: 0, knockbackVelocity: Vector2.zero, stunDurationTicks: 100);
            Assert.That(barkFixture.Motor.StunTicksRemaining, Is.EqualTo(85));
        }

        [UnityTest]
        public IEnumerator ArmorResolverKeepsIncreasedResourcesAndCapsResourcesWhenMaximumFalls()
        {
            ExpeditionRunConfiguration run = CreateRun(
                BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkillLoadout.ChargeId,
                BellatorSkillLoadout.CircularCutId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            ArmorResolution increased = ArmorStatsResolver.Resolve(
                LoadArmors(run.Inventory.Catalog).Get(ArmorCatalog.TravelerRobeItemId),
                upgradeLevel: 0,
                baseMaximumHealth: 100,
                baseMaximumEnergy: 50,
                currentHealth: 70,
                currentEnergy: 34);
            var increasedStats = new CombatStats(
                increased.MaximumHealth,
                increased.MaximumEnergy,
                4 + increased.Defense);
            fixture.Motor.Initialize(
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                increasedStats,
                EnemyLayers(),
                ExpeditionRunConfiguration.HumanRaceId,
                increased);

            Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(70));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(34));
            Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(55));

            BindingFixture decreasedFixture = CreatePlayer(run, includeRoomProjectiles: false);
            ArmorResolution decreased = ArmorStatsResolver.Resolve(
                LoadArmors(run.Inventory.Catalog).Get(ArmorCatalog.GuildCuirassItemId),
                upgradeLevel: 0,
                baseMaximumHealth: 100,
                baseMaximumEnergy: 50,
                currentHealth: 104,
                currentEnergy: 54);
            var decreasedStats = new CombatStats(
                decreased.MaximumHealth,
                decreased.MaximumEnergy,
                4 + decreased.Defense);
            decreasedFixture.Motor.Initialize(
                decreasedFixture.ActorId,
                decreasedFixture.Input,
                GroundLayers(),
                decreasedStats,
                EnemyLayers(),
                ExpeditionRunConfiguration.HumanRaceId,
                decreased);

            Assert.That(decreasedFixture.Motor.CurrentHealth, Is.EqualTo(100));
            Assert.That(decreasedFixture.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(decreasedFixture.Motor.CombatStats.MaximumHealth, Is.EqualTo(100));
            Assert.That(decreasedFixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(50));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsCrossClassWeaponProfileBeforeMutatingEitherActorComponent()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);
            var mismatchedProfile = new WeaponProfile(
                run.Inventory.Weapon.ItemId,
                BellatorSkillLoadout.ClassId,
                damage: 10,
                attackIntervalSeconds: 0.4f,
                traits: new[] { new WeaponTrait(WeaponTraitId.Balanced) });
            var mismatchedCatalog = new WeaponCatalog(new[] { mismatchedProfile });

            Assert.Throws<System.ArgumentException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                mismatchedCatalog,
                fixture.Skills));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsVenatorRunWithoutRoomProjectileDependenciesBeforeMutation()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: false);

            Assert.Throws<System.ArgumentNullException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectsUninitializedProjectilePoolBeforeMutatingVenatorRun()
        {
            ExpeditionRunConfiguration run = CreateRun(
                VenatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId);
            BindingFixture fixture = CreatePlayer(run, includeRoomProjectiles: true, initializeProjectilePool: false);
            Assert.That(fixture.ProjectileScope.Pool, Is.SameAs(fixture.ProjectilePool));
            Assert.That(fixture.ProjectilePool.IsInitialized, Is.False);

            Assert.Throws<System.InvalidOperationException>(() => ExpeditionPlayerRunBinder.Bind(
                run,
                fixture.ActorId,
                fixture.Input,
                GroundLayers(),
                EnemyLayers(),
                0,
                fixture.Motor,
                fixture.Collector,
                run.Inventory.Catalog,
                fixture.Weapons,
                fixture.Skills,
                fixture.ProjectilePool,
                fixture.ProjectileScope,
                fixture.Room));

            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeDefenseIncludesRareAndEpicInstanceBonusesForEveryClass()
        {
            string[] classes = { BellatorSkillLoadout.ClassId, VenatorSkillLoadout.ClassId, MagusSkillLoadout.ClassId };
            string[][] skills = {
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId },
                new[] { VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId },
                new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId }
            };
            for (int index = 0; index < classes.Length; index++)
            {
                foreach (ItemRarity rarity in new[] { ItemRarity.Rare, ItemRarity.Epic })
                {
                    ExpeditionRunConfiguration run = CreateRun(classes[index], ExpeditionRunConfiguration.HumanRaceId,
                        skills[index][0], skills[index][1]);
                    ReplaceArmor(run, ArmorCatalog.IronCuirassItemId, 3, rarity);
                    ReplaceWeapon(run, rarity, 3);
                    BindingFixture fixture = CreatePlayer(run, index != 0);
                    ExpeditionPlayerRunBinder.Bind(run, fixture.ActorId, fixture.Input, GroundLayers(), EnemyLayers(), 4,
                        fixture.Motor, fixture.Collector, run.Inventory.Catalog, fixture.Weapons, fixture.Skills,
                        fixture.ProjectilePool, fixture.ProjectileScope, fixture.Room);

                    int rarityBonus = rarity == ItemRarity.Rare ? 1 : 2;
                    Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(4 + 5 + 3 + rarityBonus), classes[index]);
                    Assert.That(fixture.Motor.EquippedArmorResolution.Rarity, Is.EqualTo(rarity));
                    Assert.That(fixture.Motor.EquippedWeaponBasicAttackProfile.Rarity, Is.EqualTo(rarity));
                    Assert.That(fixture.Motor.EquippedWeaponBasicAttackProfile.UpgradeLevel, Is.EqualTo(3));
                    decimal[] expectedDamage = rarity == ItemRarity.Rare
                        ? new[] { 15.87m, 13.225m, 15.87m } : new[] { 17.94m, 14.95m, 17.94m };
                    Assert.That(fixture.Motor.EquippedWeaponBasicAttackProfile.BaseDamage, Is.EqualTo(expectedDamage[index]));
                    Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(run.StartingHealth));
                    Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(run.StartingEnergy));
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator RestoredCheckpointBindsCommittedResourcesAndEmptyQuickSlotForEveryClass()
        {
            string[] classes = { BellatorSkillLoadout.ClassId, VenatorSkillLoadout.ClassId, MagusSkillLoadout.ClassId };
            string[][] skills = {
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId },
                new[] { VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId },
                new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId }
            };
            for (int index = 0; index < classes.Length; index++)
            {
                ExpeditionRunConfiguration original = ExpeditionRunConfigurationFactory.Create(classes[index],
                    ExpeditionRunConfiguration.HumanRaceId, skills[index], 2, 2, 1);
                ReplaceArmor(original, ArmorCatalog.TravelerRobeItemId, 2, ItemRarity.Epic);
                while (original.Inventory.QuickConsumable != null)
                {
                    InventoryItemStack potion = original.Inventory.QuickConsumable;
                    Assert.That(original.Inventory.TryConsumeQuickConsumable(potion.InstanceId, potion.ItemId,
                        TransactionId.New()).Succeeded, Is.True);
                }
                ExpeditionSaveData saved = ExpeditionSaveMapper.Create(original);
                Assert.That(saved.maximumEnergy, Is.EqualTo(original.StartingEnergy + 5), "Robe maximum is durable.");
                Assert.That(saved.currentEnergy, Is.EqualTo(original.StartingEnergy), "Equipping the robe must not fill energy.");
                saved.currentHealth = 37;
                saved.currentEnergy = 11;
                saved.roomEntryCheckpointJson = JsonUtility.ToJson(ExpeditionSaveMapper.CreateCheckpoint(saved));
                ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(saved, out ExpeditionSaveData resumed);
                BindingFixture fixture = CreatePlayer(restored, index != 0);

                ExpeditionPlayerRunBinder.BindFromCheckpoint(restored, resumed, fixture.ActorId, fixture.Input,
                    GroundLayers(), EnemyLayers(), 0, fixture.Motor, fixture.Collector, restored.Inventory.Catalog,
                    fixture.Weapons, fixture.Skills, fixture.ProjectilePool, fixture.ProjectileScope, fixture.Room);

                Assert.That(restored.HealthUpgradeLevel, Is.EqualTo(2));
                Assert.That(restored.EnergyUpgradeLevel, Is.EqualTo(2));
                Assert.That(restored.AdditionalPotionUpgradeLevel, Is.EqualTo(1));
                Assert.That(fixture.Motor.CombatStats.MaximumHealth, Is.EqualTo(saved.maximumHealth));
                Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(saved.maximumEnergy));
                Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(37), "Restore must not heal the actor.");
                Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(11), "Restore must not refill energy.");
                Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(1 + 2 + 2));
                Assert.That(fixture.Collector.Inventory.QuickConsumable, Is.Null);
                Assert.That(fixture.Motor.HealthPotionCharges, Is.Zero, "HUD charges must match before the first simulation tick.");
                Assert.That(fixture.Collector.Inventory.Armor.InstanceId, Is.EqualTo(original.Inventory.Armor.InstanceId));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LegacyCheckpointRestoresSavedMaximumsWithoutInventingStartingUpgradeLevels()
        {
            ExpeditionRunConfiguration original = CreateRun(MagusSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId, MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId);
            ExpeditionSaveData saved = ExpeditionSaveMapper.Create(original);
            saved.frozenPermanentUpgrades = null;
            saved.hasFrozenPermanentUpgrades = false;
            saved.maximumEnergy = 50; // Historical saves did not add the robe's five maximum energy.
            saved.currentEnergy = 7;
            saved.currentHealth = 43;
            saved.roomEntryCheckpointJson = JsonUtility.ToJson(ExpeditionSaveMapper.CreateCheckpoint(saved));
            ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(saved, out ExpeditionSaveData resumed);
            Assert.That(restored.HasKnownStartingLoadout, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => { _ = restored.EnergyUpgradeLevel; });
            BindingFixture fixture = CreatePlayer(restored, true);
            ExpeditionPlayerRunBinder.BindFromCheckpoint(restored, resumed, fixture.ActorId, fixture.Input,
                GroundLayers(), EnemyLayers(), 0, fixture.Motor, fixture.Collector, restored.Inventory.Catalog,
                fixture.Weapons, fixture.Skills, fixture.ProjectilePool, fixture.ProjectileScope, fixture.Room);
            Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(50));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(7));
            Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(43));
            Assert.That(fixture.Motor.HealthPotionCharges, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckpointMismatchRejectsCompositionBeforeMutatingActorOrCollector()
        {
            ExpeditionRunConfiguration run = CreateRun(BellatorSkillLoadout.ClassId,
                ExpeditionRunConfiguration.HumanRaceId, BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId);
            ExpeditionSaveData checkpoint = ExpeditionSaveMapper.Create(run);
            BindingFixture fixture = CreatePlayer(run, false);
            checkpoint.runId = RunId.New().ToString();
            Assert.Throws<System.ArgumentException>(() => ExpeditionPlayerRunBinder.BindFromCheckpoint(run, checkpoint,
                fixture.ActorId, fixture.Input, GroundLayers(), EnemyLayers(), 0, fixture.Motor, fixture.Collector,
                run.Inventory.Catalog, fixture.Weapons, fixture.Skills));
            AssertActorAndCollectorUnconfigured(fixture);

            checkpoint = ExpeditionSaveMapper.Create(run);
            checkpoint.frozenPermanentUpgrades.energyUpgradeLevel = 2;
            Assert.Throws<System.ArgumentException>(() => ExpeditionPlayerRunBinder.BindFromCheckpoint(run, checkpoint,
                fixture.ActorId, fixture.Input, GroundLayers(), EnemyLayers(), 0, fixture.Motor, fixture.Collector,
                run.Inventory.Catalog, fixture.Weapons, fixture.Skills));
            AssertActorAndCollectorUnconfigured(fixture);

            checkpoint = ExpeditionSaveMapper.Create(run);
            checkpoint.inventorySnapshotJson = "{}";
            Assert.Throws<System.ArgumentException>(() => ExpeditionPlayerRunBinder.BindFromCheckpoint(run, checkpoint,
                fixture.ActorId, fixture.Input, GroundLayers(), EnemyLayers(), 0, fixture.Motor, fixture.Collector,
                run.Inventory.Catalog, fixture.Weapons, fixture.Skills));
            AssertActorAndCollectorUnconfigured(fixture);
            yield return null;
        }

        private ExpeditionRunConfiguration CreateRun(string classId, string raceId, string skill1, string skill2)
        {
            return ExpeditionRunConfigurationFactory.Create(classId, raceId, new[] { skill1, skill2 });
        }

        private BindingFixture CreatePlayer(
            ExpeditionRunConfiguration run,
            bool includeRoomProjectiles,
            bool initializeProjectilePool = true)
        {
            var fixture = new BindingFixture();
            fixture.ActorId = ActorId.New();
            fixture.Input = new PlayerInputBuffer(fixture.ActorId);

            TextAsset weaponSource = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(weaponSource, Is.Not.Null, "The authoritative Weapons.json resource must be available.");
            fixture.Weapons = WeaponCatalogJsonLoader.Load(weaponSource.text, run.Inventory.Catalog);
            TextAsset skillSource = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skillSource, Is.Not.Null, "The authoritative Skills.json resource must be available.");
            fixture.Skills = SkillCatalogJsonLoader.Load(skillSource.text);

            var player = Track(new GameObject("F09 Run Binder Player"));
            player.transform.position = new Vector2(0f, 2f);
            fixture.Body = player.AddComponent<Rigidbody2D>();
            fixture.Body.gravityScale = 0f;
            fixture.Body.constraints = RigidbodyConstraints2D.FreezeAll;
            fixture.Body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            fixture.Motor = player.AddComponent<PlayerMotor2D>();
            fixture.Collector = player.AddComponent<ItemCollector2D>();

            if (includeRoomProjectiles) CreateRoomProjectiles(fixture, initializeProjectilePool);
            return fixture;
        }

        private void CreateRoomProjectiles(BindingFixture fixture, bool initializeProjectilePool)
        {
            var roomObject = Track(new GameObject("F09 Run Binder Room Lifetime"));
            fixture.Room = roomObject.AddComponent<RoomLifetimeScope>();
            fixture.Room.Initialize(RoomInstanceId.New(), "room.test.f09.run_binder");
            _rooms.Add(fixture.Room);

            var poolObject = Track(new GameObject("F09 Run Binder Projectile Pool"));
            fixture.ProjectilePool = poolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = Track(new GameObject("F09 Run Binder Projectile Prefab"));
            projectilePrefab.AddComponent<Rigidbody2D>();
            projectilePrefab.AddComponent<CircleCollider2D>();
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            if (initializeProjectilePool) fixture.ProjectilePool.Initialize(projectilePrefab);

            var scopeObject = new GameObject("F09 Run Binder Room Projectiles");
            fixture.ProjectileScope = scopeObject.AddComponent<RoomProjectileScope>();
            fixture.ProjectileScope.Initialize(EntityId.New(), fixture.ProjectilePool);
            fixture.Room.OwnActor(scopeObject);
            fixture.Room.OwnResource(fixture.ProjectileScope);
        }

        private void AssertBoundRun(
            BindingFixture fixture,
            ExpeditionRunConfiguration run,
            string skill1,
            string skill2,
            string unselectedSkill,
            int baseDefense)
        {
            Assert.That(fixture.Motor.ActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(fixture.Motor.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(fixture.Collector.RaceId, Is.EqualTo(run.RaceId));
            Assert.That(fixture.Collector.Inventory, Is.SameAs(run.Inventory));
            Assert.That(fixture.Collector.Catalog, Is.SameAs(run.Inventory.Catalog));
            Assert.That(fixture.Motor.EquippedWeaponProfile, Is.SameAs(fixture.Weapons.Get(run.Inventory.Weapon.ItemId)));
            Assert.That(fixture.Motor.EquippedWeaponProfile.ClassId, Is.EqualTo(run.ClassId));
            Assert.That(fixture.Motor.EquippedWeaponProfile.ItemId, Is.EqualTo(run.Inventory.Weapon.ItemId));
            Assert.That(run.Inventory.Armor, Is.Not.Null, "The run owns its equipped armor.");
            Assert.That(run.Inventory.QuickConsumable, Is.Not.Null, "The run owns its quick consumable instance.");
            Assert.That(fixture.Collector.Inventory.Armor, Is.SameAs(run.Inventory.Armor));
            Assert.That(fixture.Collector.Inventory.QuickConsumable, Is.SameAs(run.Inventory.QuickConsumable));
            ArmorResolution armor = ArmorStatsResolver.Resolve(
                LoadArmors(run.Inventory.Catalog).Get(run.Inventory.Armor.ItemId),
                run.Inventory.Armor.UpgradeLevel,
                run.StartingHealth,
                run.StartingEnergy,
                run.StartingHealth,
                run.StartingEnergy);
            Assert.That(fixture.Motor.CombatStats.MaximumHealth, Is.EqualTo(armor.MaximumHealth));
            Assert.That(fixture.Motor.CombatStats.MaximumEnergy, Is.EqualTo(armor.MaximumEnergy));
            Assert.That(fixture.Motor.CombatStats.Defense, Is.EqualTo(baseDefense + armor.Defense));
            Assert.That(fixture.Motor.CurrentHealth, Is.EqualTo(armor.HealthAfterMaximumClamp));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(armor.EnergyAfterMaximumClamp));
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, skill1), Is.True);
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, skill2), Is.True);
            Assert.That(IsSkillReady(fixture.Motor, run.ClassId, unselectedSkill), Is.False,
                "The third class skill must not enter the two-skill run loadout.");
        }

        private static ArmorCatalog LoadArmors(ItemCatalog items)
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Armors");
            Assert.That(source, Is.Not.Null, "The authoritative Armors.json must be available.");
            return ArmorCatalogJsonLoader.Load(source.text, items);
        }

        private static void ReplaceWeapon(ExpeditionRunConfiguration run, ItemRarity rarity, int upgradeLevel)
        {
            var replacement = new InventoryItemStack(ItemInstanceId.New(), run.Inventory.Weapon.ItemId,
                rarity, upgradeLevel, 1);
            Assert.That(run.Inventory.TryCollectItem(replacement, TransactionId.New()).Succeeded, Is.True);
            int slot = -1;
            for (int index = 0; index < run.Inventory.Backpack.Count; index++)
                if (run.Inventory.GetBackpackSlot(index)?.InstanceId == replacement.InstanceId) slot = index;
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.Inventory.EquipBackpackItem(slot, EquipmentSlot.Weapon, TransactionId.New()).Succeeded, Is.True);
        }

        private static void ReplaceArmor(ExpeditionRunConfiguration run, string armorItemId, int upgradeLevel,
            ItemRarity rarity = ItemRarity.Common)
        {
            var replacement = new InventoryItemStack(
                ItemInstanceId.New(),
                armorItemId,
                rarity,
                upgradeLevel,
                quantity: 1);
            InventoryOperationOutcome collected = run.Inventory.TryCollectItem(replacement, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "Could not collect test armor " + armorItemId + ".");

            int slot = -1;
            for (int index = 0; index < run.Inventory.Backpack.Count; index++)
            {
                if (run.Inventory.GetBackpackSlot(index)?.InstanceId == replacement.InstanceId)
                {
                    slot = index;
                    break;
                }
            }
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "Collected test armor should occupy a backpack slot.");

            InventoryOperationOutcome equipped = run.Inventory.EquipBackpackItem(
                slot,
                EquipmentSlot.Armor,
                TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "Could not equip test armor " + armorItemId + ".");
            Assert.That(run.Inventory.Armor, Is.SameAs(replacement));
        }

        private static InventoryItemStack EquipQuickConsumable(
            ExpeditionRunConfiguration run, string itemId, int quantity = 1)
        {
            var stack = new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, quantity);
            InventoryOperationOutcome collected = run.Inventory.TryCollectItem(stack, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, "Could not collect test quick consumable " + itemId + ".");
            int slot = -1;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                if (run.Inventory.GetBackpackSlot(index)?.InstanceId == stack.InstanceId)
                {
                    slot = index;
                    break;
                }
            }
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "The test quick consumable should occupy a backpack slot.");
            InventoryOperationOutcome equipped = run.Inventory.SetQuickConsumable(slot, TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "Could not set the test quick consumable.");
            Assert.That(run.Inventory.QuickConsumable, Is.Not.Null);
            Assert.That(run.Inventory.QuickConsumable.InstanceId, Is.EqualTo(stack.InstanceId));
            Assert.That(run.Inventory.QuickConsumable.ItemId, Is.EqualTo(itemId));
            Assert.That(run.Inventory.QuickConsumable.Quantity, Is.EqualTo(quantity));
            return stack;
        }

        private void AssertActorAndCollectorUnconfigured(BindingFixture fixture)
        {
            Assert.That(fixture.Motor.ActorId.IsValid, Is.False);
            Assert.That(fixture.Motor.CombatStats, Is.Null);
            Assert.That(fixture.Motor.RaceId, Is.Null);
            Assert.That(fixture.Motor.EquippedWeaponProfile, Is.Null);
            Assert.That(fixture.Motor.IsBellator || fixture.Motor.IsVenator || fixture.Motor.IsMagus, Is.False);
            Assert.That(fixture.Collector.Inventory, Is.Null);
            Assert.That(fixture.Collector.Catalog, Is.Null);
            Assert.That(fixture.Collector.RaceId, Is.Null);
            Assert.That(fixture.Collector.GrantsDvergarBonus, Is.False);
        }

        private static bool IsSkillReady(PlayerMotor2D motor, string classId, string skillId)
        {
            if (classId == BellatorSkillLoadout.ClassId) return motor.IsBellatorSkillReady(skillId);
            if (classId == VenatorSkillLoadout.ClassId) return motor.IsVenatorSkillReady(skillId);
            return motor.IsMagusSkillReady(skillId);
        }

        private static LayerMask GroundLayers()
        {
            int layer = LayerMask.NameToLayer("Ground");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Ground physics layer is required.");
            return 1 << layer;
        }

        private static LayerMask EnemyLayers()
        {
            int layer = LayerMask.NameToLayer("Enemies");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "The Enemies physics layer is required.");
            return 1 << layer;
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private sealed class BindingFixture
        {
            public ActorId ActorId;
            public PlayerInputBuffer Input;
            public PlayerMotor2D Motor;
            public Rigidbody2D Body;
            public ItemCollector2D Collector;
            public WeaponCatalog Weapons;
            public SkillCatalog Skills;
            public ProjectilePool2D ProjectilePool;
            public RoomProjectileScope ProjectileScope;
            public RoomLifetimeScope Room;
        }

        private sealed class ImmediateFortuneBrewRuntime : IPlayerFortuneBrewRuntime
        {
            private readonly ExpeditionInventory _sourceInventory;

            public ImmediateFortuneBrewRuntime(ExpeditionInventory sourceInventory)
            {
                _sourceInventory = sourceInventory;
            }

            public int CallCount { get; private set; }
            public ItemInstanceId LastItemInstanceId { get; private set; }
            public TransactionId LastTransactionId { get; private set; }

            public Task<PlayerFortuneBrewCommitOutcome> ConsumeFortuneBrewAsync(
                ItemInstanceId itemInstanceId,
                TransactionId transactionId)
            {
                CallCount++;
                LastItemInstanceId = itemInstanceId;
                LastTransactionId = transactionId;
                ExpeditionInventory committed = ExpeditionInventory.Restore(
                    _sourceInventory.Catalog, _sourceInventory.CreateSnapshot());
                InventoryOperationOutcome consumed = committed.TryConsumeQuickConsumable(
                    itemInstanceId, "consumable.fortune_brew", transactionId);
                Assert.That(consumed.Succeeded, Is.True, consumed.Status.ToString());
                return Task.FromResult(new PlayerFortuneBrewCommitOutcome(
                    persisted: true,
                    status: Luckbound.Core.Fortune.FortuneOperationStatus.Succeeded,
                    currentCharges: 2,
                    committedInventory: committed));
            }
        }
    }
}
