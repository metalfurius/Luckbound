using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Economy
{
    /// <summary>Policy facade for fixed rest-stop stock, purchases, and sales.</summary>
    public static class MerchantService
    {
        public static MerchantStockSnapshot EnsureStock(
            ExpeditionInventory inventory,
            RunId runId,
            RoomInstanceId roomInstanceId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.EnsureMerchantStock(runId, roomInstanceId);
        }

        public static EconomyTransactionOutcome TryPurchase(
            ExpeditionInventory inventory,
            RunId runId,
            RoomInstanceId roomInstanceId,
            string itemId,
            int quantity,
            int discountPercent,
            TransactionId transactionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.TryPurchase(runId, roomInstanceId, itemId, quantity, discountPercent, transactionId);
        }

        public static EconomyTransactionOutcome TrySellItem(
            ExpeditionInventory inventory,
            RunId runId,
            RoomInstanceId roomInstanceId,
            ItemInstanceId itemInstanceId,
            int quantity,
            TransactionId transactionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.TrySellItem(runId, roomInstanceId, itemInstanceId, quantity, transactionId);
        }

        public static EconomyTransactionOutcome TrySellMaterial(
            ExpeditionInventory inventory,
            RunId runId,
            RoomInstanceId roomInstanceId,
            MaterialKind materialKind,
            int quantity,
            TransactionId transactionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.TrySellMaterial(runId, roomInstanceId, materialKind, quantity, transactionId);
        }

        internal static MerchantStockSnapshot CreateInitialStock(ItemCatalog catalog, string classId, RoomInstanceId roomInstanceId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!roomInstanceId.IsValid) throw new ArgumentException("Merchant stock requires a valid rest-room instance ID.", nameof(roomInstanceId));

            StartingLoadout loadout = catalog.GetStartingLoadout(classId);
            var offers = new List<MerchantOfferSnapshot>(11)
            {
                Offer("consumable.health_potion", ItemRarity.Common, 2),
                Offer("consumable.energy_tonic", ItemRarity.Common, 1)
            };
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
                offers.Add(Offer(catalog.GetMaterial(kind).Id, ItemRarity.Common, 2));
            offers.Add(Offer(loadout.WeaponItemId, ItemRarity.Common, 1));
            offers.Add(Offer(loadout.ArmorItemId, ItemRarity.Common, 1));
            offers.Add(Offer("relic.marked_coin", ItemRarity.Common, 1));

            foreach (MerchantOfferSnapshot offer in offers)
            {
                if (!catalog.TryGet(offer.itemId, out ItemDefinition definition))
                    throw new ArgumentException("Merchant stock references an unknown item: " + offer.itemId + ".", nameof(catalog));
                MerchantPricing.GetBasePrice(definition, (ItemRarity)offer.rarity);
            }

            return new MerchantStockSnapshot
            {
                roomInstanceId = roomInstanceId.ToString(),
                offers = offers.ToArray()
            };
        }

        internal static MerchantStockSnapshot ValidateAndClone(
            ItemCatalog catalog,
            string classId,
            MerchantStockSnapshot snapshot)
        {
            if (snapshot == null) throw new FormatException("Merchant stock snapshot cannot be null.");
            if (!RoomInstanceId.TryParse(snapshot.roomInstanceId, out RoomInstanceId roomInstanceId))
                throw new FormatException("Merchant stock room instance ID is invalid.");
            if (snapshot.offers == null) throw new FormatException("Merchant stock offers are missing.");

            MerchantStockSnapshot expected = CreateInitialStock(catalog, classId, roomInstanceId);
            if (snapshot.offers.Length != expected.offers.Length)
                throw new FormatException("Merchant stock must contain the eleven canonical rest-stop offers.");

            for (int index = 0; index < expected.offers.Length; index++)
            {
                MerchantOfferSnapshot actual = snapshot.offers[index];
                MerchantOfferSnapshot canonical = expected.offers[index];
                if (actual == null || actual.itemId != canonical.itemId || actual.rarity != canonical.rarity ||
                    actual.initialQuantity != canonical.initialQuantity || actual.remainingQuantity < 0 ||
                    actual.remainingQuantity > canonical.initialQuantity)
                    throw new FormatException("Merchant stock contains an invalid or reordered offer at index " + index + ".");
            }

            return snapshot.Clone();
        }

        private static MerchantOfferSnapshot Offer(string itemId, ItemRarity rarity, int quantity)
        {
            return new MerchantOfferSnapshot
            {
                itemId = itemId,
                rarity = (int)rarity,
                initialQuantity = quantity,
                remainingQuantity = quantity
            };
        }
    }
}
