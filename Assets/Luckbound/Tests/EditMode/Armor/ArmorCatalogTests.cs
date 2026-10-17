using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Armor;
using NUnit.Framework;

namespace Luckbound.Tests.Armor
{
    public sealed class ArmorCatalogTests
    {
        private static readonly ExpectedArmor[] ExpectedArmors =
        {
            new ExpectedArmor(ArmorCatalog.TravelerRobeItemId, 1, 5, 0, 0, 0),
            new ExpectedArmor(ArmorCatalog.LeatherVestItemId, 2, 0, 3, 0, 0),
            new ExpectedArmor(ArmorCatalog.GuildCuirassItemId, 3, 0, -3, 0, 0),
            new ExpectedArmor(ArmorCatalog.RunicMantleItemId, 2, 0, 0, 1, 0),
            new ExpectedArmor(ArmorCatalog.IronCuirassItemId, 5, 0, -5, 0, 0),
            new ExpectedArmor(ArmorCatalog.BarkArmorItemId, 3, 0, 0, 0, 15)
        };

        private readonly ArmorCatalog _catalog = ArmorCatalog.Standard;

        [Test]
        public void StandardCatalogContainsAllSixProfilesAndTheirExactModifiers()
        {
            Assert.That(_catalog.Count, Is.EqualTo(6));
            CollectionAssert.AreEqual(ExpectedArmors.Select(expected => expected.ItemId).ToArray(),
                _catalog.Profiles.Select(profile => profile.ItemId).ToArray());

            foreach (ExpectedArmor expected in ExpectedArmors)
            {
                ArmorProfile profile = _catalog.Get(expected.ItemId);
                Assert.That(profile.BaseDefense, Is.EqualTo(expected.BaseDefense), expected.ItemId);
                Assert.That(profile.MaximumEnergyBonus, Is.EqualTo(expected.MaximumEnergyBonus), expected.ItemId);
                Assert.That(profile.MovementSpeedPercentModifier, Is.EqualTo(expected.MovementSpeedPercentModifier), expected.ItemId);
                Assert.That(profile.EnergyRegenerationPerSecondBonus,
                    Is.EqualTo(expected.EnergyRegenerationPerSecondBonus), expected.ItemId);
                Assert.That(profile.HarmfulStatusDurationReductionPercent,
                    Is.EqualTo(expected.HarmfulStatusDurationReductionPercent), expected.ItemId);
            }
        }

        [Test]
        public void EachUpgradeLevelAddsExactlyOneDefenseThroughPlusThree()
        {
            foreach (ExpectedArmor expected in ExpectedArmors)
            {
                ArmorProfile profile = _catalog.Get(expected.ItemId);
                for (int upgradeLevel = 0; upgradeLevel <= ArmorProfile.MaximumUpgradeLevel; upgradeLevel++)
                {
                    ArmorResolution result = ArmorStatsResolver.Resolve(
                        profile, upgradeLevel, baseMaximumHealth: 100, baseMaximumEnergy: 50,
                        currentHealth: 70, currentEnergy: 30);
                    Assert.That(result.Defense, Is.EqualTo(expected.BaseDefense + upgradeLevel),
                        expected.ItemId + " at +" + upgradeLevel);
                    Assert.That(result.UpgradeLevel, Is.EqualTo(upgradeLevel));
                }
            }
        }

        [Test]
        public void MaximumEnergyBonusDoesNotRefillAndADecreaseReturnsTheCurrentEnergyCap()
        {
            ArmorResolution increased = ArmorStatsResolver.Resolve(
                _catalog.Get(ArmorCatalog.TravelerRobeItemId), upgradeLevel: 0,
                baseMaximumHealth: 100, baseMaximumEnergy: 50, currentHealth: 62, currentEnergy: 34);
            Assert.That(increased.MaximumEnergy, Is.EqualTo(55));
            Assert.That(increased.EnergyAfterMaximumClamp, Is.EqualTo(34), "Increasing the maximum must not refill energy.");
            Assert.That(increased.HealthAfterMaximumClamp, Is.EqualTo(62));

            ArmorResolution decreased = ArmorStatsResolver.Resolve(
                _catalog.Get(ArmorCatalog.GuildCuirassItemId), upgradeLevel: 0,
                baseMaximumHealth: 100, baseMaximumEnergy: 50, currentHealth: 62, currentEnergy: 54);
            Assert.That(decreased.MaximumEnergy, Is.EqualTo(50));
            Assert.That(decreased.EnergyAfterMaximumClamp, Is.EqualTo(50),
                "The result gives the integrator the cap to apply when maximum energy falls.");
            Assert.That(decreased.HealthAfterMaximumClamp, Is.EqualTo(62));
        }

        [Test]
        public void CatalogRejectsMalformedAndUnknownIds()
        {
            string[] invalidIds = { null, string.Empty, "  ", "Armor.magus.traveler_robe", "armor.Magus.robe", "weapon.magus.staff" };
            foreach (string itemId in invalidIds)
            {
                Assert.That(_catalog.TryGet(itemId, out ArmorProfile profile), Is.False, itemId ?? "<null>");
                Assert.That(profile, Is.Null);
            }

            Assert.Throws<KeyNotFoundException>(() => _catalog.Get("armor.unknown"));
            Assert.Throws<KeyNotFoundException>(() => _catalog.Get(null));
        }

        [Test]
        public void ResolverRejectsInvalidInputsAndUpgradeLevels()
        {
            ArmorProfile robe = _catalog.Get(ArmorCatalog.TravelerRobeItemId);
            Assert.Throws<ArgumentNullException>(() => ArmorStatsResolver.Resolve(
                null, 0, 100, 50, 50, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, -1, 100, 50, 50, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, ArmorProfile.MaximumUpgradeLevel + 1, 100, 50, 50, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, 0, 0, 50, 50, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, 0, 100, 0, 50, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, 0, 100, 50, -1, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmorStatsResolver.Resolve(
                robe, 0, 100, 50, 50, -1));
        }

        [Test]
        public void ProfilesRejectInvalidStableIdsAndInvalidModifierInputs()
        {
            Assert.Throws<ArgumentException>(() => new ArmorProfile("weapon.iron_cuirass", 5));
            Assert.Throws<ArgumentException>(() => new ArmorProfile("armor.Iron_cuirass", 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmorProfile("armor.invalid", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmorProfile("armor.invalid", 1, maximumEnergyBonus: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmorProfile("armor.invalid", 1,
                harmfulStatusDurationReductionPercent: 101));
        }

        private sealed class ExpectedArmor
        {
            public ExpectedArmor(
                string itemId,
                int baseDefense,
                int maximumEnergyBonus,
                int movementSpeedPercentModifier,
                int energyRegenerationPerSecondBonus,
                int harmfulStatusDurationReductionPercent)
            {
                ItemId = itemId;
                BaseDefense = baseDefense;
                MaximumEnergyBonus = maximumEnergyBonus;
                MovementSpeedPercentModifier = movementSpeedPercentModifier;
                EnergyRegenerationPerSecondBonus = energyRegenerationPerSecondBonus;
                HarmfulStatusDurationReductionPercent = harmfulStatusDurationReductionPercent;
            }

            public string ItemId { get; }
            public int BaseDefense { get; }
            public int MaximumEnergyBonus { get; }
            public int MovementSpeedPercentModifier { get; }
            public int EnergyRegenerationPerSecondBonus { get; }
            public int HarmfulStatusDurationReductionPercent { get; }
        }
    }
}
