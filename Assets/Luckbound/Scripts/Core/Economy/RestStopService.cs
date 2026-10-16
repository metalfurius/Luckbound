using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Economy
{
    /// <summary>Policy facade for one-time rest recovery and Talia's paid service.</summary>
    public static class RestStopService
    {
        public const int EntryHealingAmount = 20;
        public const int TaliaHealingAmount = 50;
        public const int TaliaGoldCost = 20;

        public static RestStopOutcome Enter(
            ExpeditionInventory inventory,
            RoomInstanceId roomInstanceId,
            int currentHealth,
            int maximumHealth)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.TryEnterRest(roomInstanceId, currentHealth, maximumHealth);
        }

        public static EconomyTransactionOutcome TryUseTalia(
            ExpeditionInventory inventory,
            RoomInstanceId roomInstanceId,
            int currentHealth,
            int maximumHealth,
            TransactionId transactionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return inventory.TryUseTalia(roomInstanceId, currentHealth, maximumHealth, transactionId);
        }
    }
}
