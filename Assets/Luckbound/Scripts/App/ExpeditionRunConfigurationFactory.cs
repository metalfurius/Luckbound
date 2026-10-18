using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression;
using Luckbound.Core.Randomness;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Composes catalog-backed, non-inherited state for one new expedition.</summary>
    public static class ExpeditionRunConfigurationFactory
    {
        public static ExpeditionRunConfiguration Create(string classId, string raceId, IEnumerable<string> selectedSkillIds,
            int healthUpgradeLevel = 0, int energyUpgradeLevel = 0, int additionalPotionUpgradeLevel = 0)
        {
            return CreateInternal(classId, raceId, selectedSkillIds, healthUpgradeLevel, energyUpgradeLevel,
                additionalPotionUpgradeLevel, sample: false);
        }

        /// <summary>Creates the explicitly versioned F10 route without changing campaign generation.</summary>
        public static ExpeditionRunConfiguration CreateSample(string classId, string raceId,
            IEnumerable<string> selectedSkillIds, int healthUpgradeLevel = 0, int energyUpgradeLevel = 0,
            int additionalPotionUpgradeLevel = 0)
        {
            return CreateInternal(classId, raceId, selectedSkillIds, healthUpgradeLevel, energyUpgradeLevel,
                additionalPotionUpgradeLevel, sample: true);
        }

        private static ExpeditionRunConfiguration CreateInternal(string classId, string raceId,
            IEnumerable<string> selectedSkillIds, int healthUpgradeLevel, int energyUpgradeLevel,
            int additionalPotionUpgradeLevel, bool sample)
        {
            if (selectedSkillIds == null) throw new ArgumentNullException(nameof(selectedSkillIds));
            if (healthUpgradeLevel < 0 || healthUpgradeLevel > PermanentUpgradeSnapshot.MaximumHealthUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(healthUpgradeLevel));
            if (energyUpgradeLevel < 0 || energyUpgradeLevel > PermanentUpgradeSnapshot.MaximumEnergyUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(energyUpgradeLevel));
            if (additionalPotionUpgradeLevel < 0 || additionalPotionUpgradeLevel > PermanentUpgradeSnapshot.MaximumPotionUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(additionalPotionUpgradeLevel));
            string[] skillIds = selectedSkillIds.ToArray();
            if (skillIds.Length != 2 || skillIds.Distinct(StringComparer.Ordinal).Count() != 2)
                throw new ArgumentException("Choose exactly two different skills before starting.", nameof(selectedSkillIds));

            ItemCatalog items = ItemCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Items/Items").text);
            SkillCatalog skills = SkillCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Skills").text);

            foreach (string skillId in skillIds)
            {
                if (!skills.TryGet(skillId, out SkillDefinition skill) || !string.Equals(skill.ClassId, classId, StringComparison.Ordinal))
                    throw new ArgumentException("Every selected skill must belong to the selected class.", nameof(selectedSkillIds));
            }

            RunId runId = RunId.New();
            uint seed = Xorshift32.NormalizeSeed(SeedDeriver.HashUtf8("luckbound-run-seed-v1|" + runId));
            ExpeditionRoutePlan route = sample
                ? ExpeditionSampleRouteGenerator.Generate(seed, ExpeditionSampleRouteJsonLoader.Load(
                    LoadRequired("Luckbound/Data/Rooms/F10SampleRoute").text))
                : ExpeditionRouteGenerator.Generate(seed, ExpeditionRouteCatalogJsonLoader.Load(
                    LoadRequired("Luckbound/Data/Rooms/RouteTemplates").text));
            ExpeditionInventory inventory = CreateStartingInventory(items, classId,
                ExpeditionRunConfiguration.InitialHealthPotions + additionalPotionUpgradeLevel);
            return new ExpeditionRunConfiguration(
                runId,
                classId,
                raceId,
                skillIds,
                seed,
                route,
                inventory,
                new[] { inventory.Weapon.InstanceId, inventory.Armor.InstanceId },
                healthUpgradeLevel,
                energyUpgradeLevel,
                additionalPotionUpgradeLevel);
        }

        private static ExpeditionInventory CreateStartingInventory(ItemCatalog catalog, string classId, int startingHealthPotions)
        {
            StartingLoadout loadout = catalog.GetStartingLoadout(classId);
            var inventory = new ExpeditionInventory(catalog, classId);

            InventoryItemStack weapon = CreateStack(loadout.WeaponItemId, 1);
            RequireSuccess(inventory.TryCollectItem(weapon, TransactionId.New()), "collect starting weapon");
            RequireSuccess(inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New()), "equip starting weapon");

            InventoryItemStack armor = CreateStack(loadout.ArmorItemId, 1);
            RequireSuccess(inventory.TryCollectItem(armor, TransactionId.New()), "collect starting armor");
            RequireSuccess(inventory.EquipBackpackItem(0, EquipmentSlot.Armor, TransactionId.New()), "equip starting armor");

            InventoryItemStack potions = CreateStack("consumable.health_potion", startingHealthPotions);
            RequireSuccess(inventory.TryCollectItem(potions, TransactionId.New()), "collect starting health potions");
            RequireSuccess(inventory.SetQuickConsumable(0, TransactionId.New()), "set starting quick consumable");
            return inventory;
        }

        private static InventoryItemStack CreateStack(string itemId, int quantity)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, quantity);
        }

        private static void RequireSuccess(InventoryOperationOutcome outcome, string action)
        {
            if (outcome == null || !outcome.Succeeded)
                throw new InvalidOperationException("Could not " + action + ": " + (outcome == null ? "no outcome" : outcome.Status.ToString()) + ".");
        }

        private static TextAsset LoadRequired(string resourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) throw new InvalidOperationException("Required expedition data is missing: Resources/" + resourcePath + ".");
            return asset;
        }
    }
}
