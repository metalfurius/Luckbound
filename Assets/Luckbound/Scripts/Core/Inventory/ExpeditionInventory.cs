using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Inventory
{
    /// <summary>Mutable, per-expedition inventory. Every public mutation is idempotent by transaction ID.</summary>
    public sealed class ExpeditionInventory
    {
        public const int BackpackCapacity = 12;
        public const int MaximumMaterialCount = 99;

        private readonly ItemCatalog _catalog;
        private readonly InventoryItemStack[] _backpackSlots = new InventoryItemStack[BackpackCapacity];
        private readonly ReadOnlyCollection<InventoryItemStack> _backpackView;
        private readonly Dictionary<MaterialKind, int> _materials = new Dictionary<MaterialKind, int>();
        private readonly Dictionary<TransactionId, CachedTransaction> _transactions = new Dictionary<TransactionId, CachedTransaction>();
        private InventoryItemStack _weapon;
        private InventoryItemStack _armor;
        private InventoryItemStack _relicOne;
        private InventoryItemStack _relicTwo;
        private ItemInstanceId _quickConsumableId;
        private int _gold;

        public ExpeditionInventory(ItemCatalog catalog, string classId, int initialGold = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ValidateClassId(classId);
            if (initialGold < 0) throw new ArgumentOutOfRangeException(nameof(initialGold));

            ClassId = classId;
            _gold = initialGold;
            _backpackView = Array.AsReadOnly(_backpackSlots);
            foreach (MaterialKind materialKind in Enum.GetValues(typeof(MaterialKind))) _materials.Add(materialKind, 0);
        }

        public string ClassId { get; }
        public ItemCatalog Catalog => _catalog;
        public int Gold => _gold;
        public IReadOnlyList<InventoryItemStack> Backpack => _backpackView;
        public InventoryItemStack Weapon => _weapon;
        public InventoryItemStack Armor => _armor;
        public InventoryItemStack RelicOne => _relicOne;
        public InventoryItemStack RelicTwo => _relicTwo;
        public ItemInstanceId QuickConsumableInstanceId => _quickConsumableId;
        public InventoryItemStack QuickConsumable => FindByInstanceId(_quickConsumableId);

        public InventoryItemStack GetBackpackSlot(int index)
        {
            ValidateSlotIndex(index);
            return _backpackSlots[index];
        }

        public IReadOnlyDictionary<MaterialKind, int> GetMaterialSnapshot()
        {
            return new ReadOnlyDictionary<MaterialKind, int>(new Dictionary<MaterialKind, int>(_materials));
        }

        public int GetMaterialCount(MaterialKind materialKind)
        {
            if (!Enum.IsDefined(typeof(MaterialKind), materialKind)) throw new ArgumentOutOfRangeException(nameof(materialKind));
            return _materials[materialKind];
        }

        public InventoryOperationOutcome TryCollectItem(InventoryItemStack item, TransactionId transactionId)
        {
            string signature = "collect|" + StackSignature(item);
            return Execute(transactionId, signature, () =>
            {
                InventoryOperationStatus validation = ValidateStack(item);
                if (validation != InventoryOperationStatus.Succeeded) return Rejected(transactionId, validation, item?.ItemId);
                if (ContainsInstanceId(item.InstanceId)) return Rejected(transactionId, InventoryOperationStatus.RejectedDuplicateInstance, item.ItemId);

                ItemDefinition definition = _catalog.Get(item.ItemId);
                if (definition.Kind == ItemKind.Consumable)
                {
                    if (!CanFitConsumable(item, definition)) return Rejected(transactionId, InventoryOperationStatus.RejectedFull, item.ItemId);
                    int remaining = item.Quantity;
                    for (int index = 0; index < _backpackSlots.Length && remaining > 0; index++)
                    {
                        InventoryItemStack existing = _backpackSlots[index];
                        if (!CanMerge(existing, item)) continue;
                        int moved = Math.Min(remaining, definition.MaxStack - existing.Quantity);
                        _backpackSlots[index] = existing.WithQuantity(existing.Quantity + moved);
                        remaining -= moved;
                    }

                    if (remaining > 0)
                    {
                        int emptyIndex = FindEmptySlot();
                        _backpackSlots[emptyIndex] = item.WithQuantity(remaining);
                    }
                }
                else
                {
                    int emptyIndex = FindEmptySlot();
                    if (emptyIndex < 0) return Rejected(transactionId, InventoryOperationStatus.RejectedFull, item.ItemId);
                    _backpackSlots[emptyIndex] = item;
                }

                return Succeeded(transactionId, item.ItemId);
            });
        }

        public InventoryOperationOutcome EquipBackpackItem(int index, EquipmentSlot equipmentSlot, TransactionId transactionId)
        {
            string signature = "equip|" + index.ToString(CultureInfo.InvariantCulture) + "|" + equipmentSlot;
            return Execute(transactionId, signature, () =>
            {
                if (!IsBackpackIndex(index)) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidSlot);
                if (!Enum.IsDefined(typeof(EquipmentSlot), equipmentSlot) || equipmentSlot == EquipmentSlot.QuickConsumable)
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedWrongEquipmentSlot);
                }

                InventoryItemStack incoming = _backpackSlots[index];
                if (incoming == null) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidSlot);
                ItemDefinition definition;
                if (!_catalog.TryGet(incoming.ItemId, out definition)) return Rejected(transactionId, InventoryOperationStatus.RejectedUnknownItem, incoming.ItemId);

                if (!IsCompatibleEquipment(definition.Kind, equipmentSlot))
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedWrongEquipmentSlot, incoming.ItemId);
                }
                if (definition.Kind == ItemKind.Weapon && !definition.IsCompatibleWithClass(ClassId))
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedIncompatibleClass, incoming.ItemId);
                }
                if (definition.Kind == ItemKind.Relic &&
                    ((equipmentSlot == EquipmentSlot.RelicOne && _relicTwo?.ItemId == incoming.ItemId) ||
                     (equipmentSlot == EquipmentSlot.RelicTwo && _relicOne?.ItemId == incoming.ItemId)))
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedDuplicateRelic, incoming.ItemId);
                }

                InventoryItemStack displaced = GetEquippedItem(equipmentSlot);
                SetEquippedItem(equipmentSlot, incoming);
                _backpackSlots[index] = displaced;
                if (displaced == null && _quickConsumableId == incoming.InstanceId) _quickConsumableId = default;
                return Succeeded(transactionId, incoming.ItemId, displaced);
            });
        }

        public InventoryOperationOutcome SetQuickConsumable(int index, TransactionId transactionId)
        {
            string signature = "quick|" + index.ToString(CultureInfo.InvariantCulture);
            return Execute(transactionId, signature, () =>
            {
                if (!IsBackpackIndex(index)) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidSlot);
                InventoryItemStack item = _backpackSlots[index];
                if (item == null || !_catalog.TryGet(item.ItemId, out ItemDefinition definition) || definition.Kind != ItemKind.Consumable)
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidQuickConsumable, item?.ItemId);
                }

                _quickConsumableId = item.InstanceId;
                return Succeeded(transactionId, item.ItemId);
            });
        }

        public InventoryOperationOutcome TryReplaceBackpackSlot(int index, InventoryItemStack incoming, TransactionId transactionId)
        {
            string signature = "replace|" + index.ToString(CultureInfo.InvariantCulture) + "|" + StackSignature(incoming);
            return Execute(transactionId, signature, () =>
            {
                if (!IsBackpackIndex(index)) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidSlot, incoming?.ItemId);
                InventoryOperationStatus validation = ValidateStack(incoming);
                if (validation != InventoryOperationStatus.Succeeded) return Rejected(transactionId, validation, incoming?.ItemId);
                if (ContainsInstanceId(incoming.InstanceId)) return Rejected(transactionId, InventoryOperationStatus.RejectedDuplicateInstance, incoming.ItemId);

                InventoryItemStack displaced = _backpackSlots[index];
                _backpackSlots[index] = incoming;
                if (displaced != null && _quickConsumableId == displaced.InstanceId) _quickConsumableId = default;
                return Succeeded(transactionId, incoming.ItemId, displaced);
            });
        }

        public InventoryOperationOutcome TryAddMaterial(MaterialKind materialKind, int amount, TransactionId transactionId)
        {
            string signature = "material|" + materialKind + "|" + amount.ToString(CultureInfo.InvariantCulture);
            return Execute(transactionId, signature, () =>
            {
                if (!Enum.IsDefined(typeof(MaterialKind), materialKind)) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidMaterial);
                if (amount <= 0) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidMaterial);
                long total = (long)_materials[materialKind] + amount;
                if (total > MaximumMaterialCount) return Rejected(transactionId, InventoryOperationStatus.RejectedMaterialCapacity);
                _materials[materialKind] = (int)total;
                return Succeeded(transactionId, _catalog.GetMaterial(materialKind).Id);
            });
        }

        public InventoryOperationOutcome TryAddGold(int amount, TransactionId transactionId)
        {
            string signature = "gold|" + amount.ToString(CultureInfo.InvariantCulture);
            return Execute(transactionId, signature, () =>
            {
                if (amount <= 0 || (long)_gold + amount > int.MaxValue)
                {
                    return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidGold);
                }
                _gold += amount;
                return Succeeded(transactionId, "currency.gold");
            });
        }

        private InventoryOperationOutcome Execute(TransactionId transactionId, string signature, Func<InventoryOperationOutcome> operation)
        {
            if (!transactionId.IsValid) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidTransaction);
            if (_transactions.TryGetValue(transactionId, out CachedTransaction cached))
            {
                return string.Equals(cached.Signature, signature, StringComparison.Ordinal)
                    ? cached.Outcome
                    : Rejected(transactionId, InventoryOperationStatus.RejectedTransactionConflict);
            }

            InventoryOperationOutcome outcome = operation();
            _transactions.Add(transactionId, new CachedTransaction(signature, outcome));
            return outcome;
        }

        private InventoryOperationStatus ValidateStack(InventoryItemStack item)
        {
            if (item == null || !item.InstanceId.IsValid || !Enum.IsDefined(typeof(ItemRarity), item.Rarity) ||
                item.UpgradeLevel < 0 || item.UpgradeLevel > 3 || item.Quantity < 1)
            {
                return InventoryOperationStatus.RejectedInvalidItem;
            }
            if (!_catalog.TryGet(item.ItemId, out ItemDefinition definition)) return InventoryOperationStatus.RejectedUnknownItem;
            if (definition.Kind == ItemKind.Material || item.Quantity > definition.MaxStack)
            {
                return InventoryOperationStatus.RejectedInvalidItem;
            }
            if (item.UpgradeLevel > 0 && definition.Kind != ItemKind.Weapon && definition.Kind != ItemKind.Armor)
            {
                return InventoryOperationStatus.RejectedInvalidItem;
            }
            if (definition.Kind == ItemKind.Consumable && (item.Rarity != ItemRarity.Common || item.UpgradeLevel != 0))
            {
                return InventoryOperationStatus.RejectedInvalidItem;
            }
            if (definition.Kind == ItemKind.Relic && item.UpgradeLevel != 0)
            {
                return InventoryOperationStatus.RejectedInvalidItem;
            }
            return InventoryOperationStatus.Succeeded;
        }

        private bool CanFitConsumable(InventoryItemStack incoming, ItemDefinition definition)
        {
            int capacity = 0;
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                InventoryItemStack existing = _backpackSlots[index];
                if (existing == null) capacity += definition.MaxStack;
                else if (CanMerge(existing, incoming)) capacity += definition.MaxStack - existing.Quantity;
            }
            return capacity >= incoming.Quantity;
        }

        private static bool CanMerge(InventoryItemStack existing, InventoryItemStack incoming)
        {
            return existing != null && existing.ItemId == incoming.ItemId && existing.Rarity == incoming.Rarity &&
                existing.UpgradeLevel == incoming.UpgradeLevel;
        }

        private int FindEmptySlot()
        {
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                if (_backpackSlots[index] == null) return index;
            }
            return -1;
        }

        private bool ContainsInstanceId(ItemInstanceId instanceId)
        {
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                if (_backpackSlots[index]?.InstanceId == instanceId) return true;
            }
            return _weapon?.InstanceId == instanceId || _armor?.InstanceId == instanceId ||
                _relicOne?.InstanceId == instanceId || _relicTwo?.InstanceId == instanceId;
        }

        private InventoryItemStack FindByInstanceId(ItemInstanceId instanceId)
        {
            if (!instanceId.IsValid) return null;
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                if (_backpackSlots[index]?.InstanceId == instanceId) return _backpackSlots[index];
            }
            if (_weapon?.InstanceId == instanceId) return _weapon;
            if (_armor?.InstanceId == instanceId) return _armor;
            if (_relicOne?.InstanceId == instanceId) return _relicOne;
            if (_relicTwo?.InstanceId == instanceId) return _relicTwo;
            return null;
        }

        private static bool IsBackpackIndex(int index)
        {
            return index >= 0 && index < BackpackCapacity;
        }

        private static void ValidateSlotIndex(int index)
        {
            if (!IsBackpackIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static bool IsCompatibleEquipment(ItemKind kind, EquipmentSlot slot)
        {
            return (kind == ItemKind.Weapon && slot == EquipmentSlot.Weapon) ||
                (kind == ItemKind.Armor && slot == EquipmentSlot.Armor) ||
                (kind == ItemKind.Relic && (slot == EquipmentSlot.RelicOne || slot == EquipmentSlot.RelicTwo));
        }

        private InventoryItemStack GetEquippedItem(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return _weapon;
                case EquipmentSlot.Armor: return _armor;
                case EquipmentSlot.RelicOne: return _relicOne;
                case EquipmentSlot.RelicTwo: return _relicTwo;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private void SetEquippedItem(EquipmentSlot slot, InventoryItemStack item)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: _weapon = item; break;
                case EquipmentSlot.Armor: _armor = item; break;
                case EquipmentSlot.RelicOne: _relicOne = item; break;
                case EquipmentSlot.RelicTwo: _relicTwo = item; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static void ValidateClassId(string classId)
        {
            if (classId != "class.bellator" && classId != "class.venator" && classId != "class.magus")
            {
                throw new ArgumentException("Expedition inventory requires one of the three playable class IDs.", nameof(classId));
            }
        }

        private static string StackSignature(InventoryItemStack item)
        {
            return item == null ? "null" : item.InstanceId + ":" + item.ItemId + ":" + item.Rarity + ":" +
                item.UpgradeLevel.ToString(CultureInfo.InvariantCulture) + ":" + item.Quantity.ToString(CultureInfo.InvariantCulture);
        }

        private static InventoryOperationOutcome Succeeded(TransactionId transactionId, string itemId, InventoryItemStack displaced = null)
        {
            return new InventoryOperationOutcome(transactionId, InventoryOperationStatus.Succeeded, displaced, itemId);
        }

        private static InventoryOperationOutcome Rejected(TransactionId transactionId, InventoryOperationStatus status, string itemId = null)
        {
            return new InventoryOperationOutcome(transactionId, status, null, itemId);
        }

        private readonly struct CachedTransaction
        {
            public CachedTransaction(string signature, InventoryOperationOutcome outcome)
            {
                Signature = signature;
                Outcome = outcome;
            }
            public string Signature { get; }
            public InventoryOperationOutcome Outcome { get; }
        }
    }
}
