using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Expeditions
{
    public enum ExpeditionOutcome
    {
        Death = 0,
        Extracted = 1,
        FinalVictory = 2,
        Abandoned = 3
    }

    /// <summary>Immutable result of applying one expedition outcome to the profile's Renown balance.</summary>
    public sealed class ExpeditionSettlementReceipt
    {
        internal ExpeditionSettlementReceipt(
            RunId runId,
            ExpeditionOutcome outcome,
            int accumulatedRenown,
            int lootRenown,
            int renownAwarded,
            int balanceAfter,
            bool alreadyProcessed)
        {
            RunId = runId;
            Outcome = outcome;
            AccumulatedRenown = accumulatedRenown;
            LootRenown = lootRenown;
            RenownAwarded = renownAwarded;
            BalanceAfter = balanceAfter;
            AlreadyProcessed = alreadyProcessed;
        }

        public RunId RunId { get; }
        public ExpeditionOutcome Outcome { get; }
        public int AccumulatedRenown { get; }
        public int LootRenown { get; }
        public int RenownAwarded { get; }
        public int BalanceAfter { get; }
        public bool AlreadyProcessed { get; }
    }

    /// <summary>
    /// Applies the v1 death, extraction, final-victory and abandonment rules exactly once per RunId.
    /// D08 persists AvailableRenown and ProcessedRunIds in the profile save block.
    /// </summary>
    public sealed class ExpeditionSettlementLedger
    {
        private readonly HashSet<RunId> _processedRunIds = new HashSet<RunId>();
        private readonly Dictionary<RunId, ExpeditionSettlementReceipt> _receipts =
            new Dictionary<RunId, ExpeditionSettlementReceipt>();
        private ReadOnlyCollection<RunId> _processedRunIdsView;

        public ExpeditionSettlementLedger(int availableRenown = 0, IEnumerable<RunId> processedRunIds = null)
        {
            if (availableRenown < 0) throw new ArgumentOutOfRangeException(nameof(availableRenown));
            AvailableRenown = availableRenown;
            if (processedRunIds != null)
            {
                foreach (RunId runId in processedRunIds)
                {
                    if (!runId.IsValid) throw new ArgumentException("Processed run IDs must be valid.", nameof(processedRunIds));
                    if (!_processedRunIds.Add(runId))
                        throw new ArgumentException("Processed run IDs must be unique.", nameof(processedRunIds));
                }
            }
        }

        public int AvailableRenown { get; private set; }

        /// <summary>Returns a stable, read-only snapshot suitable for serialization by the save layer.</summary>
        public IReadOnlyCollection<RunId> ProcessedRunIds
        {
            get
            {
                if (_processedRunIdsView == null)
                {
                    _processedRunIdsView = Array.AsReadOnly(_processedRunIds
                        .OrderBy(runId => runId.ToString(), StringComparer.Ordinal).ToArray());
                }
                return _processedRunIdsView;
            }
        }

        public ExpeditionSettlementReceipt Settle(
            RunId runId,
            ExpeditionOutcome outcome,
            int accumulatedRenown,
            ExpeditionInventory inventory,
            IEnumerable<ItemInstanceId> startingEquipmentInstanceIds = null)
        {
            if (!runId.IsValid) throw new ArgumentException("A settlement requires a valid RunId.", nameof(runId));
            if (!Enum.IsDefined(typeof(ExpeditionOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));

            if (_receipts.TryGetValue(runId, out ExpeditionSettlementReceipt previous))
            {
                return new ExpeditionSettlementReceipt(runId, previous.Outcome, previous.AccumulatedRenown,
                    previous.LootRenown, renownAwarded: 0, AvailableRenown, alreadyProcessed: true);
            }
            if (_processedRunIds.Contains(runId))
            {
                return new ExpeditionSettlementReceipt(runId, outcome, accumulatedRenown,
                    lootRenown: 0, renownAwarded: 0, AvailableRenown, alreadyProcessed: true);
            }

            if (accumulatedRenown < 0) throw new ArgumentOutOfRangeException(nameof(accumulatedRenown));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            HashSet<ItemInstanceId> startingEquipment = ValidateStartingEquipment(startingEquipmentInstanceIds);
            int lootRenown = outcome == ExpeditionOutcome.Extracted || outcome == ExpeditionOutcome.FinalVictory
                ? ConvertLoot(inventory, startingEquipment)
                : 0;
            int awarded = CalculateAward(outcome, accumulatedRenown, lootRenown);
            int nextBalance = checked(AvailableRenown + awarded);
            var receipt = new ExpeditionSettlementReceipt(runId, outcome, accumulatedRenown,
                lootRenown, awarded, nextBalance, alreadyProcessed: false);

            // Validate arithmetic and build the receipt before changing any ledger state.
            AvailableRenown = nextBalance;
            _processedRunIds.Add(runId);
            _processedRunIdsView = null;
            _receipts.Add(runId, receipt);
            return receipt;
        }

        private static int CalculateAward(ExpeditionOutcome outcome, int accumulatedRenown, int lootRenown)
        {
            switch (outcome)
            {
                case ExpeditionOutcome.Death:
                    return accumulatedRenown / 4;
                case ExpeditionOutcome.Extracted:
                case ExpeditionOutcome.FinalVictory:
                    return checked(accumulatedRenown + lootRenown);
                case ExpeditionOutcome.Abandoned:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        private static HashSet<ItemInstanceId> ValidateStartingEquipment(IEnumerable<ItemInstanceId> instanceIds)
        {
            var result = new HashSet<ItemInstanceId>();
            if (instanceIds == null) return result;
            foreach (ItemInstanceId instanceId in instanceIds)
            {
                if (!instanceId.IsValid) throw new ArgumentException("Starting equipment IDs must be valid.", nameof(instanceIds));
                if (!result.Add(instanceId)) throw new ArgumentException("Starting equipment IDs must be unique.", nameof(instanceIds));
            }
            return result;
        }

        private static int ConvertLoot(ExpeditionInventory inventory, ISet<ItemInstanceId> startingEquipment)
        {
            long value = inventory.Gold / 20;
            long totalMaterials = inventory.GetMaterialSnapshot().Values.Aggregate(0L, (sum, count) => sum + count);
            value += totalMaterials / 5;

            var countedInstances = new HashSet<ItemInstanceId>();
            foreach (InventoryItemStack item in EnumerateItems(inventory))
            {
                if (item == null || !countedInstances.Add(item.InstanceId) || startingEquipment.Contains(item.InstanceId)) continue;
                ItemDefinition definition = inventory.Catalog.Get(item.ItemId);
                switch (definition.Kind)
                {
                    case ItemKind.Weapon:
                        value += WeaponValue(item.Rarity);
                        break;
                    case ItemKind.Armor:
                        value += ArmorValue(item.Rarity);
                        break;
                    case ItemKind.Relic:
                        value += 8;
                        break;
                    case ItemKind.Consumable:
                    case ItemKind.Material:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (value > int.MaxValue) throw new OverflowException("Converted loot exceeds the supported Renown balance.");
            }

            return checked((int)value);
        }

        private static IEnumerable<InventoryItemStack> EnumerateItems(ExpeditionInventory inventory)
        {
            foreach (InventoryItemStack item in inventory.Backpack) yield return item;
            yield return inventory.Weapon;
            yield return inventory.Armor;
            yield return inventory.RelicOne;
            yield return inventory.RelicTwo;
        }

        private static int WeaponValue(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 5;
                case ItemRarity.Rare: return 8;
                case ItemRarity.Epic: return 12;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        private static int ArmorValue(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 4;
                case ItemRarity.Rare: return 6;
                case ItemRarity.Epic: return 9;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }
    }
}
