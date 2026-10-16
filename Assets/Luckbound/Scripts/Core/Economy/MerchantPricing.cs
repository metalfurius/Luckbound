using System;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Economy
{
    /// <summary>Canonical purchase and sale prices from DEVELOPMENT_PLAN.md section 3.10.</summary>
    public static class MerchantPricing
    {
        public const int MaximumDiscountPercent = 30;
        public const int SalePercent = 25;

        public static int GetBasePrice(ItemDefinition definition, ItemRarity rarity)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!Enum.IsDefined(typeof(ItemRarity), rarity)) throw new ArgumentOutOfRangeException(nameof(rarity));

            switch (definition.Kind)
            {
                case ItemKind.Weapon:
                    return rarity switch
                    {
                        ItemRarity.Common => 30,
                        ItemRarity.Rare => 45,
                        ItemRarity.Epic => 65,
                        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
                    };
                case ItemKind.Armor:
                    return rarity switch
                    {
                        ItemRarity.Common => 25,
                        ItemRarity.Rare => 40,
                        ItemRarity.Epic => 60,
                        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
                    };
                case ItemKind.Relic:
                    return 50;
                case ItemKind.Consumable:
                    switch (definition.Id)
                    {
                        case "consumable.health_potion":
                        case "consumable.energy_tonic": return 18;
                        case "consumable.antidote": return 14;
                        case "consumable.bomb": return 20;
                        case "consumable.fortune_brew": return 35;
                        case "consumable.torch": return 10;
                        default: throw new ArgumentException("Consumable has no canonical merchant price: " + definition.Id + ".", nameof(definition));
                    }
                case ItemKind.Material:
                    switch (definition.MaterialKind)
                    {
                        case MaterialKind.ShadowWood: return 6;
                        case MaterialKind.Ore: return 8;
                        case MaterialKind.UmbraHerb: return 6;
                        case MaterialKind.ArcaneDust: return 10;
                        case MaterialKind.DarkResin: return 8;
                        case MaterialKind.VitalEmber: return 15;
                        default: throw new ArgumentException("Material has no canonical merchant price: " + definition.Id + ".", nameof(definition));
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition));
            }
        }

        public static int GetPurchaseUnitPrice(ItemDefinition definition, ItemRarity rarity, int discountPercent = 0)
        {
            if (discountPercent < 0) throw new ArgumentOutOfRangeException(nameof(discountPercent));
            int discount = Math.Min(discountPercent, MaximumDiscountPercent);
            return GetBasePrice(definition, rarity) * (100 - discount) / 100;
        }

        /// <summary>Sale value is floored per unit, then multiplied by the sold quantity.</summary>
        public static int GetSaleUnitPrice(ItemDefinition definition, ItemRarity rarity)
        {
            return GetBasePrice(definition, rarity) * SalePercent / 100;
        }

        public static int GetTotalPrice(int unitPrice, int quantity)
        {
            if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            long total = (long)unitPrice * quantity;
            if (total > int.MaxValue) throw new OverflowException("Merchant transaction total exceeds the gold counter.");
            return (int)total;
        }
    }
}
