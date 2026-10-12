using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Randomness;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Composes catalog-backed, non-inherited state for one new expedition.</summary>
    public static class ExpeditionRunConfigurationFactory
    {
        public static ExpeditionRunConfiguration Create(string classId, string raceId, IEnumerable<string> selectedSkillIds)
        {
            if (selectedSkillIds == null) throw new ArgumentNullException(nameof(selectedSkillIds));
            string[] skillIds = selectedSkillIds.ToArray();
            if (skillIds.Length != 2 || skillIds.Distinct(StringComparer.Ordinal).Count() != 2)
                throw new ArgumentException("Choose exactly two different skills before starting.", nameof(selectedSkillIds));

            ItemCatalog items = ItemCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Items/Items").text);
            SkillCatalog skills = SkillCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Skills").text);
            ExpeditionRouteCatalog routes = ExpeditionRouteCatalogJsonLoader.Load(
                LoadRequired("Luckbound/Data/Rooms/RouteTemplates").text);

            foreach (string skillId in skillIds)
            {
                if (!skills.TryGet(skillId, out SkillDefinition skill) || !string.Equals(skill.ClassId, classId, StringComparison.Ordinal))
                    throw new ArgumentException("Every selected skill must belong to the selected class.", nameof(selectedSkillIds));
            }

            RunId runId = RunId.New();
            uint seed = Xorshift32.NormalizeSeed(SeedDeriver.HashUtf8("luckbound-run-seed-v1|" + runId));
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(seed, routes);
            ExpeditionInventory inventory = CreateStartingInventory(items, classId);
            return new ExpeditionRunConfiguration(
                runId,
                classId,
                raceId,
                skillIds,
                seed,
                route,
                inventory);
        }

        private static ExpeditionInventory CreateStartingInventory(ItemCatalog catalog, string classId)
        {
            StartingLoadout loadout = catalog.GetStartingLoadout(classId);
            var inventory = new ExpeditionInventory(catalog, classId);

            InventoryItemStack weapon = CreateStack(loadout.WeaponItemId, 1);
            RequireSuccess(inventory.TryCollectItem(weapon, TransactionId.New()), "collect starting weapon");
            RequireSuccess(inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New()), "equip starting weapon");

            InventoryItemStack armor = CreateStack(loadout.ArmorItemId, 1);
            RequireSuccess(inventory.TryCollectItem(armor, TransactionId.New()), "collect starting armor");
            RequireSuccess(inventory.EquipBackpackItem(0, EquipmentSlot.Armor, TransactionId.New()), "equip starting armor");

            InventoryItemStack potions = CreateStack("consumable.health_potion", ExpeditionRunConfiguration.InitialHealthPotions);
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
