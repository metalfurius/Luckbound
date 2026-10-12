using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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

        public ExpeditionInventorySnapshot CreateSnapshot()
        {
            var materialSnapshots = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select(kind => new MaterialCountSnapshot { kind = (int)kind, count = _materials[kind] }).ToArray();
            return new ExpeditionInventorySnapshot
            {
                classId = ClassId,
                gold = _gold,
                weapon = Snapshot(_weapon),
                armor = Snapshot(_armor),
                relicOne = Snapshot(_relicOne),
                relicTwo = Snapshot(_relicTwo),
                backpackSlots = _backpackSlots.Select(Snapshot).ToArray(),
                quickConsumableInstanceId = _quickConsumableId.IsValid ? _quickConsumableId.ToString() : string.Empty,
                materials = materialSnapshots,
                transactions = _transactions.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => new InventoryTransactionSnapshot
                    {
                        transactionId = pair.Key.ToString(),
                        signature = pair.Value.Signature,
                        status = (int)pair.Value.Outcome.Status,
                        itemId = pair.Value.Outcome.ItemId,
                        displacedItem = Snapshot(pair.Value.Outcome.DisplacedItem)
                    }).ToArray()
            };
        }

        public static ExpeditionInventory Restore(ItemCatalog catalog, ExpeditionInventorySnapshot snapshot)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.backpackSlots == null || snapshot.backpackSlots.Length != BackpackCapacity)
                throw new FormatException("Inventory snapshot must contain exactly twelve backpack slots.");
            if (snapshot.materials == null || snapshot.materials.Length != Enum.GetValues(typeof(MaterialKind)).Length)
                throw new FormatException("Inventory snapshot must contain all six material counters.");
            if (snapshot.transactions == null) throw new FormatException("Inventory transaction history is missing.");

            var restored = new ExpeditionInventory(catalog, snapshot.classId, snapshot.gold);
            var instanceIds = new HashSet<ItemInstanceId>();
            for (int index = 0; index < snapshot.backpackSlots.Length; index++)
                restored._backpackSlots[index] = RestoreStack(restored, snapshot.backpackSlots[index], instanceIds, "backpack slot " + index);

            restored._weapon = RestoreEquipped(restored, snapshot.weapon, ItemKind.Weapon, instanceIds, "weapon");
            restored._armor = RestoreEquipped(restored, snapshot.armor, ItemKind.Armor, instanceIds, "armor");
            restored._relicOne = RestoreEquipped(restored, snapshot.relicOne, ItemKind.Relic, instanceIds, "first relic");
            restored._relicTwo = RestoreEquipped(restored, snapshot.relicTwo, ItemKind.Relic, instanceIds, "second relic");
            if (restored._relicOne != null && restored._relicTwo != null &&
                string.Equals(restored._relicOne.ItemId, restored._relicTwo.ItemId, StringComparison.Ordinal))
                throw new FormatException("The same relic definition cannot occupy both equipment slots.");

            if (!string.IsNullOrWhiteSpace(snapshot.quickConsumableInstanceId))
            {
                if (!ItemInstanceId.TryParse(snapshot.quickConsumableInstanceId, out ItemInstanceId quickId))
                    throw new FormatException("Quick consumable instance ID is invalid.");
                InventoryItemStack quick = restored._backpackSlots.FirstOrDefault(item => item != null && item.InstanceId == quickId);
                if (quick == null || catalog.Get(quick.ItemId).Kind != ItemKind.Consumable)
                    throw new FormatException("Quick consumable must point to a consumable in the backpack.");
                restored._quickConsumableId = quickId;
            }

            var materialKinds = new HashSet<MaterialKind>();
            foreach (MaterialCountSnapshot material in snapshot.materials)
            {
                if (material == null || !Enum.IsDefined(typeof(MaterialKind), material.kind))
                    throw new FormatException("Inventory snapshot contains an unknown material kind.");
                MaterialKind kind = (MaterialKind)material.kind;
                if (!materialKinds.Add(kind)) throw new FormatException("Inventory snapshot repeats material " + kind + ".");
                if (material.count < 0 || material.count > MaximumMaterialCount)
                    throw new FormatException("Inventory material count is outside 0–99 for " + kind + ".");
                restored._materials[kind] = material.count;
            }

            var transactionIds = new HashSet<TransactionId>();
            foreach (InventoryTransactionSnapshot transaction in snapshot.transactions)
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out TransactionId id) ||
                    string.IsNullOrWhiteSpace(transaction.signature) ||
                    !Enum.IsDefined(typeof(InventoryOperationStatus), transaction.status))
                    throw new FormatException("Inventory transaction history contains an invalid entry.");
                if (!transactionIds.Add(id)) throw new FormatException("Inventory transaction history repeats " + id + ".");
                InventoryItemStack displaced = RestoreStack(restored, transaction.displacedItem, null, "transaction result");
                var outcome = new InventoryOperationOutcome(id, (InventoryOperationStatus)transaction.status,
                    displaced, transaction.itemId);
                restored._transactions.Add(id, new CachedTransaction(transaction.signature, outcome));
            }
            return restored;
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

        private static InventoryItemStack RestoreEquipped(ExpeditionInventory inventory, InventoryStackSnapshot snapshot,
            ItemKind expectedKind, HashSet<ItemInstanceId> instanceIds, string slotName)
        {
            InventoryItemStack item = RestoreStack(inventory, snapshot, instanceIds, slotName);
            if (item == null) return null;
            ItemDefinition definition = inventory._catalog.Get(item.ItemId);
            if (definition.Kind != expectedKind || expectedKind == ItemKind.Weapon && !definition.IsCompatibleWithClass(inventory.ClassId))
                throw new FormatException("Saved " + slotName + " is incompatible with the selected class or equipment slot.");
            return item;
        }

        private static InventoryItemStack RestoreStack(ExpeditionInventory inventory, InventoryStackSnapshot snapshot,
            HashSet<ItemInstanceId> instanceIds, string location)
        {
            if (snapshot == null) return null;
            // JsonUtility materializes null reference entries in object arrays as empty DTO instances.
            if (string.IsNullOrEmpty(snapshot.instanceId) && string.IsNullOrEmpty(snapshot.itemId) &&
                snapshot.rarity == 0 && snapshot.upgradeLevel == 0 && snapshot.quantity == 0) return null;
            if (!ItemInstanceId.TryParse(snapshot.instanceId, out ItemInstanceId instanceId) ||
                !Enum.IsDefined(typeof(ItemRarity), snapshot.rarity))
                throw new FormatException("Saved item identity or rarity is invalid at " + location + ".");
            var item = new InventoryItemStack(instanceId, snapshot.itemId, (ItemRarity)snapshot.rarity,
                snapshot.upgradeLevel, snapshot.quantity);
            if (inventory.ValidateStack(item) != InventoryOperationStatus.Succeeded)
                throw new FormatException("Saved item is incompatible with the catalog at " + location + ": " + item.ItemId + ".");
            if (instanceIds != null && !instanceIds.Add(instanceId))
                throw new FormatException("Saved item instance ID is duplicated: " + instanceId + ".");
            return item;
        }

        private static InventoryStackSnapshot Snapshot(InventoryItemStack item)
        {
            return item == null ? null : new InventoryStackSnapshot
            {
                instanceId = item.InstanceId.ToString(),
                itemId = item.ItemId,
                rarity = (int)item.Rarity,
                upgradeLevel = item.UpgradeLevel,
                quantity = item.Quantity
            };
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
