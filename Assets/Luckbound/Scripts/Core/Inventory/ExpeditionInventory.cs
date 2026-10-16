using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Crafting;
using Luckbound.Core.Economy;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Inventory
{
    /// <summary>Mutable, per-expedition inventory. Every public mutation is idempotent by transaction ID.</summary>
    public sealed class ExpeditionInventory
    {
        public const int BackpackCapacity = 12;
        public const int MaximumMaterialCount = 99;
        public const int SnapshotSchemaVersion = 4;

        private readonly ItemCatalog _catalog;
        private readonly InventoryItemStack[] _backpackSlots = new InventoryItemStack[BackpackCapacity];
        private readonly ReadOnlyCollection<InventoryItemStack> _backpackView;
        private readonly Dictionary<MaterialKind, int> _materials = new Dictionary<MaterialKind, int>();
        private readonly Dictionary<MaterialKind, int> _worldMaterialRemainders = new Dictionary<MaterialKind, int>();
        private readonly Dictionary<TransactionId, CachedTransaction> _transactions = new Dictionary<TransactionId, CachedTransaction>();
        private readonly Dictionary<string, MerchantStockSnapshot> _merchantStocks = new Dictionary<string, MerchantStockSnapshot>(StringComparer.Ordinal);
        private readonly Dictionary<string, RestStopUseSnapshot> _restStops = new Dictionary<string, RestStopUseSnapshot>(StringComparer.Ordinal);
        private InventoryItemStack _weapon;
        private InventoryItemStack _armor;
        private InventoryItemStack _relicOne;
        private InventoryItemStack _relicTwo;
        private ItemInstanceId _quickConsumableId;
        private int _gold;
        private string _merchantRunId = string.Empty;

        public ExpeditionInventory(ItemCatalog catalog, string classId, int initialGold = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ValidateClassId(classId);
            if (initialGold < 0) throw new ArgumentOutOfRangeException(nameof(initialGold));

            ClassId = classId;
            _gold = initialGold;
            _backpackView = Array.AsReadOnly(_backpackSlots);
            foreach (MaterialKind materialKind in Enum.GetValues(typeof(MaterialKind)))
            {
                _materials.Add(materialKind, 0);
                _worldMaterialRemainders.Add(materialKind, 0);
            }
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

        /// <summary>Creates a rest stop's stock once, or returns its current stock without refreshing it.</summary>
        public MerchantStockSnapshot EnsureMerchantStock(RunId runId, RoomInstanceId roomInstanceId)
        {
            if (!runId.IsValid) throw new ArgumentException("Merchant stock requires a valid run ID.", nameof(runId));
            if (!roomInstanceId.IsValid) throw new ArgumentException("Merchant stock requires a valid room instance ID.", nameof(roomInstanceId));

            string runIdText = runId.ToString();
            if (_merchantRunId.Length > 0 && !string.Equals(_merchantRunId, runIdText, StringComparison.Ordinal))
                throw new InvalidOperationException("Merchant stock belongs to another expedition run.");

            string roomIdText = roomInstanceId.ToString();
            if (_merchantStocks.TryGetValue(roomIdText, out MerchantStockSnapshot existing)) return existing.Clone();

            MerchantStockSnapshot created = MerchantService.CreateInitialStock(_catalog, ClassId, roomInstanceId);
            _merchantRunId = runIdText;
            _merchantStocks.Add(roomIdText, created);
            return created.Clone();
        }

        public MerchantStockSnapshot GetMerchantStock(RunId runId, RoomInstanceId roomInstanceId)
        {
            if (!runId.IsValid) throw new ArgumentException("Merchant stock requires a valid run ID.", nameof(runId));
            if (!roomInstanceId.IsValid) throw new ArgumentException("Merchant stock requires a valid room instance ID.", nameof(roomInstanceId));
            if (_merchantRunId.Length == 0) return null;
            if (!string.Equals(_merchantRunId, runId.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException("Merchant stock belongs to another expedition run.");
            return _merchantStocks.TryGetValue(roomInstanceId.ToString(), out MerchantStockSnapshot stock)
                ? stock.Clone()
                : null;
        }

        public ExpeditionInventorySnapshot CreateSnapshot()
        {
            var materialSnapshots = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select(kind => new MaterialCountSnapshot { kind = (int)kind, count = _materials[kind] }).ToArray();
            var remainderSnapshots = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select(kind => new WorldMaterialRemainderSnapshot { kind = (int)kind, remainder = _worldMaterialRemainders[kind] }).ToArray();
            return new ExpeditionInventorySnapshot
            {
                snapshotVersion = SnapshotSchemaVersion,
                classId = ClassId,
                gold = _gold,
                weapon = Snapshot(_weapon),
                armor = Snapshot(_armor),
                relicOne = Snapshot(_relicOne),
                relicTwo = Snapshot(_relicTwo),
                backpackSlots = _backpackSlots.Select(Snapshot).ToArray(),
                quickConsumableInstanceId = _quickConsumableId.IsValid ? _quickConsumableId.ToString() : string.Empty,
                materials = materialSnapshots,
                worldMaterialRemainders = remainderSnapshots,
                merchantRunId = _merchantRunId,
                merchantStocks = _merchantStocks.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Value.Clone()).ToArray(),
                restStops = _restStops.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Value.Clone()).ToArray(),
                transactions = _transactions.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => new InventoryTransactionSnapshot
                    {
                        transactionId = pair.Key.ToString(),
                        signature = pair.Value.Signature,
                        status = pair.Value.Outcome == null
                            ? (int)InventoryOperationStatus.Unknown
                            : (int)pair.Value.Outcome.Status,
                        itemId = pair.Value.EconomyOutcome != null
                            ? pair.Value.EconomyOutcome.ItemId
                            : pair.Value.Outcome == null ? string.Empty : pair.Value.Outcome.ItemId,
                        displacedItem = pair.Value.Outcome == null ? null : Snapshot(pair.Value.Outcome.DisplacedItem),
                        craftingStatus = pair.Value.CraftingOutcome == null
                            ? (int)CraftingOperationStatus.Unknown
                            : (int)pair.Value.CraftingOutcome.Status,
                        craftingRecipeId = pair.Value.CraftingOutcome == null
                            ? string.Empty
                            : pair.Value.CraftingOutcome.RecipeId,
                        craftingResultStacks = pair.Value.CraftingOutcome == null
                            ? Array.Empty<InventoryStackSnapshot>()
                            : pair.Value.CraftingOutcome.ResultStacks.Select(Snapshot).ToArray(),
                        smithingRejectedNonUpgradeRecipe = pair.Value.SmithingOutcome != null &&
                            pair.Value.SmithingOutcome.RejectedNonUpgradeRecipe,
                        economyStatus = pair.Value.EconomyOutcome == null
                            ? (int)EconomyOperationStatus.Unknown
                            : (int)pair.Value.EconomyOutcome.Status,
                        economyItemInstanceId = pair.Value.EconomyOutcome != null && pair.Value.EconomyOutcome.ItemInstanceId.IsValid
                            ? pair.Value.EconomyOutcome.ItemInstanceId.ToString()
                            : string.Empty,
                        economyQuantity = pair.Value.EconomyOutcome == null ? 0 : pair.Value.EconomyOutcome.Quantity,
                        economyGoldDelta = pair.Value.EconomyOutcome == null ? 0 : pair.Value.EconomyOutcome.GoldDelta,
                        economyResultingHealth = pair.Value.EconomyOutcome == null ? -1 : pair.Value.EconomyOutcome.ResultingHealth,
                        economyHealthRestored = pair.Value.EconomyOutcome == null ? 0 : pair.Value.EconomyOutcome.HealthRestored,
                        economyRoomInstanceId = pair.Value.EconomyOutcome == null
                            ? string.Empty
                            : pair.Value.EconomyOutcome.RoomInstanceId
                    }).ToArray()
            };
        }

        public static ExpeditionInventory Restore(ItemCatalog catalog, ExpeditionInventorySnapshot snapshot)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.snapshotVersion < 0 || snapshot.snapshotVersion > SnapshotSchemaVersion)
                throw new FormatException("Inventory snapshot version is unsupported.");
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

            // Snapshots written before E01 have no Dvergar progress field; their safe migration is six zero remainders.
            if (snapshot.worldMaterialRemainders != null && snapshot.worldMaterialRemainders.Length > 0)
            {
                if (snapshot.worldMaterialRemainders.Length != Enum.GetValues(typeof(MaterialKind)).Length)
                    throw new FormatException("Inventory snapshot must contain all six world-material remainders.");
                var remainderKinds = new HashSet<MaterialKind>();
                foreach (WorldMaterialRemainderSnapshot progress in snapshot.worldMaterialRemainders)
                {
                    if (progress == null || !Enum.IsDefined(typeof(MaterialKind), progress.kind))
                        throw new FormatException("Inventory snapshot contains an unknown world-material kind.");
                    MaterialKind kind = (MaterialKind)progress.kind;
                    if (!remainderKinds.Add(kind)) throw new FormatException("Inventory snapshot repeats world-material progress for " + kind + ".");
                    if (progress.remainder < 0 || progress.remainder >= DvergarWorldCollectionThreshold)
                        throw new FormatException("Dvergar world-material remainder is outside 0–4 for " + kind + ".");
                    restored._worldMaterialRemainders[kind] = progress.remainder;
                }
            }
            else if (snapshot.snapshotVersion >= 3)
            {
                throw new FormatException("Current inventory snapshot is missing Dvergar world-material progress.");
            }

            if (snapshot.snapshotVersion >= 4)
            {
                if (snapshot.merchantStocks == null || snapshot.restStops == null || snapshot.merchantRunId == null)
                    throw new FormatException("Current inventory snapshot is missing E03 merchant or rest-stop state.");

                if (snapshot.merchantRunId.Length > 0 && !RunId.TryParse(snapshot.merchantRunId, out _))
                    throw new FormatException("Merchant stock run ID is invalid.");
                if (snapshot.merchantStocks.Length == 0 && snapshot.merchantRunId.Length > 0 ||
                    snapshot.merchantStocks.Length > 0 && snapshot.merchantRunId.Length == 0)
                    throw new FormatException("Merchant stock run ID does not match the stored stock entries.");

                restored._merchantRunId = snapshot.merchantRunId;
                foreach (MerchantStockSnapshot market in snapshot.merchantStocks)
                {
                    MerchantStockSnapshot validated = MerchantService.ValidateAndClone(catalog, restored.ClassId, market);
                    if (!restored._merchantStocks.TryAdd(validated.roomInstanceId, validated))
                        throw new FormatException("Inventory snapshot repeats merchant stock for room " + validated.roomInstanceId + ".");
                }

                foreach (RestStopUseSnapshot rest in snapshot.restStops)
                {
                    if (rest == null || !RoomInstanceId.TryParse(rest.roomInstanceId, out RoomInstanceId roomId) ||
                        rest.entryHealthRestored < 0 || rest.entryHealthRestored > RestStopService.EntryHealingAmount ||
                        rest.taliaHealthRestored < 0 || rest.taliaHealthRestored > RestStopService.TaliaHealingAmount ||
                        !rest.entered && rest.entryHealthRestored != 0 ||
                        !rest.taliaUsed && (rest.taliaHealthRestored != 0 || rest.taliaHealthAfter != 0) ||
                        rest.taliaUsed && !rest.entered || rest.taliaUsed && rest.taliaHealthAfter <= 0)
                        throw new FormatException("Rest-stop usage state is invalid.");
                    if (!restored._restStops.TryAdd(roomId.ToString(), rest.Clone()))
                        throw new FormatException("Inventory snapshot repeats rest-stop usage for room " + roomId + ".");
                }
            }

            var transactionIds = new HashSet<TransactionId>();
            foreach (InventoryTransactionSnapshot transaction in snapshot.transactions)
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out TransactionId id) ||
                    string.IsNullOrWhiteSpace(transaction.signature) ||
                    !Enum.IsDefined(typeof(InventoryOperationStatus), transaction.status))
                    throw new FormatException("Inventory transaction history contains an invalid entry.");
                if (!transactionIds.Add(id)) throw new FormatException("Inventory transaction history repeats " + id + ".");
                bool isEconomyTransaction = transaction.economyStatus != (int)EconomyOperationStatus.Unknown;
                bool hasEconomySignature = transaction.signature.StartsWith("economy|", StringComparison.Ordinal);
                if (isEconomyTransaction != hasEconomySignature)
                    throw new FormatException("Inventory economy transaction signature does not match its receipt.");
                if (isEconomyTransaction && ((InventoryOperationStatus)transaction.status != InventoryOperationStatus.Unknown ||
                    !Enum.IsDefined(typeof(EconomyOperationStatus), transaction.economyStatus) ||
                    (EconomyOperationStatus)transaction.economyStatus == EconomyOperationStatus.RejectedInvalidTransaction ||
                    (EconomyOperationStatus)transaction.economyStatus == EconomyOperationStatus.RejectedTransactionConflict ||
                    (EconomyOperationStatus)transaction.economyStatus == EconomyOperationStatus.Succeeded &&
                        transaction.economyQuantity <= 0 ||
                    (EconomyOperationStatus)transaction.economyStatus == EconomyOperationStatus.Succeeded &&
                        transaction.signature.StartsWith("economy|buy|", StringComparison.Ordinal) &&
                        string.IsNullOrWhiteSpace(transaction.itemId) ||
                    transaction.economyQuantity < 0 || transaction.economyHealthRestored < 0))
                    throw new FormatException("Inventory economy transaction receipt is invalid.");
                if (!isEconomyTransaction && (InventoryOperationStatus)transaction.status == InventoryOperationStatus.Unknown)
                    throw new FormatException("Inventory transaction is missing its operation result.");

                InventoryItemStack displaced = RestoreStack(restored, transaction.displacedItem, null, "transaction result");
                var outcome = new InventoryOperationOutcome(id, (InventoryOperationStatus)transaction.status,
                    displaced, transaction.itemId);
                CraftingOutcome craftingOutcome = null;
                bool isCraftingTransaction = transaction.signature.StartsWith("craft|", StringComparison.Ordinal);
                bool isSmithingTransaction = transaction.signature.StartsWith("smith|", StringComparison.Ordinal);
                if ((isCraftingTransaction || isSmithingTransaction) && isEconomyTransaction ||
                    transaction.smithingRejectedNonUpgradeRecipe && !isSmithingTransaction)
                    throw new FormatException("A transaction cannot mix crafting, smithing, and economy results.");
                if (transaction.craftingStatus != (int)CraftingOperationStatus.Unknown)
                {
                    if (!Enum.IsDefined(typeof(CraftingOperationStatus), transaction.craftingStatus) ||
                        !(isCraftingTransaction || isSmithingTransaction) || transaction.craftingRecipeId == null ||
                        transaction.craftingResultStacks == null)
                        throw new FormatException("Inventory crafting transaction history contains an invalid entry.");

                    var resultStacks = new List<InventoryItemStack>(transaction.craftingResultStacks.Length);
                    foreach (InventoryStackSnapshot resultSnapshot in transaction.craftingResultStacks)
                    {
                        InventoryItemStack resultStack = RestoreStack(restored, resultSnapshot, null, "crafting transaction result");
                        if (resultStack == null)
                            throw new FormatException("Inventory crafting transaction contains an empty result stack.");
                        resultStacks.Add(resultStack);
                    }

                    CraftingOperationStatus craftingStatus = (CraftingOperationStatus)transaction.craftingStatus;
                    if (craftingStatus == CraftingOperationStatus.Succeeded && resultStacks.Count == 0 ||
                        craftingStatus != CraftingOperationStatus.Succeeded && resultStacks.Count != 0)
                        throw new FormatException("Inventory crafting transaction result does not match its status.");
                    craftingOutcome = new CraftingOutcome(id, transaction.craftingRecipeId, craftingStatus, resultStacks);
                }
                else if (isCraftingTransaction || isSmithingTransaction)
                {
                    throw new FormatException("Inventory crafting or smithing transaction history is missing its result status.");
                }

                SmithingOutcome smithingOutcome = null;
                if (isSmithingTransaction)
                {
                    if (craftingOutcome == null || transaction.smithingRejectedNonUpgradeRecipe &&
                        craftingOutcome.Status != CraftingOperationStatus.RejectedInvalidRecipe)
                        throw new FormatException("Inventory smithing transaction receipt is invalid.");
                    smithingOutcome = new SmithingOutcome(craftingOutcome, transaction.smithingRejectedNonUpgradeRecipe);
                }

                EconomyTransactionOutcome economyOutcome = null;
                if (isEconomyTransaction)
                {
                    ItemInstanceId itemInstanceId = default;
                    if (!string.IsNullOrEmpty(transaction.economyItemInstanceId) &&
                        !ItemInstanceId.TryParse(transaction.economyItemInstanceId, out itemInstanceId))
                        throw new FormatException("Inventory economy transaction has an invalid item instance ID.");
                    if (!string.IsNullOrEmpty(transaction.economyRoomInstanceId) &&
                        !RoomInstanceId.TryParse(transaction.economyRoomInstanceId, out _))
                        throw new FormatException("Inventory economy transaction has an invalid room instance ID.");
                    if (transaction.craftingStatus != (int)CraftingOperationStatus.Unknown)
                        throw new FormatException("An economy transaction cannot contain a crafting result.");

                    economyOutcome = new EconomyTransactionOutcome(id, (EconomyOperationStatus)transaction.economyStatus,
                        transaction.itemId, itemInstanceId, transaction.economyQuantity, transaction.economyGoldDelta,
                        transaction.economyResultingHealth, transaction.economyHealthRestored,
                        transaction.economyRoomInstanceId);
                }

                restored._transactions.Add(id, new CachedTransaction(
                    transaction.signature, outcome, craftingOutcome, economyOutcome, smithingOutcome));
            }
            return restored;
        }

        public int GetMaterialCount(MaterialKind materialKind)
        {
            if (!Enum.IsDefined(typeof(MaterialKind), materialKind)) throw new ArgumentOutOfRangeException(nameof(materialKind));
            return _materials[materialKind];
        }

        public int GetWorldMaterialRemainder(MaterialKind materialKind)
        {
            if (!Enum.IsDefined(typeof(MaterialKind), materialKind)) throw new ArgumentOutOfRangeException(nameof(materialKind));
            return _worldMaterialRemainders[materialKind];
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

        public InventoryOperationOutcome TryCollectWorldMaterial(
            MaterialKind materialKind,
            int amount,
            bool grantsDvergarBonus,
            TransactionId transactionId)
        {
            string signature = "world-material|" + materialKind + "|" + amount.ToString(CultureInfo.InvariantCulture) + "|" + grantsDvergarBonus;
            return Execute(transactionId, signature, () =>
            {
                if (!Enum.IsDefined(typeof(MaterialKind), materialKind)) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidMaterial);
                if (amount <= 0) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidMaterial);

                int remainder = _worldMaterialRemainders[materialKind];
                long trackedWorldUnits = grantsDvergarBonus ? (long)remainder + amount : 0L;
                long bonus = grantsDvergarBonus ? trackedWorldUnits / DvergarWorldCollectionThreshold : 0L;
                long total = (long)_materials[materialKind] + amount + bonus;
                if (total > MaximumMaterialCount) return Rejected(transactionId, InventoryOperationStatus.RejectedMaterialCapacity);

                _materials[materialKind] = (int)total;
                if (grantsDvergarBonus) _worldMaterialRemainders[materialKind] = (int)(trackedWorldUnits % DvergarWorldCollectionThreshold);
                return Succeeded(transactionId, _catalog.GetMaterial(materialKind).Id);
            });
        }

        private const int DvergarWorldCollectionThreshold = 5;

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

        public EconomyTransactionOutcome TryPurchase(
            RunId runId,
            RoomInstanceId roomInstanceId,
            string itemId,
            int quantity,
            int discountPercent,
            TransactionId transactionId)
        {
            string signature = "economy|buy|" + runId + "|" + roomInstanceId + "|" + (itemId ?? string.Empty) + "|" +
                quantity.ToString(CultureInfo.InvariantCulture) + "|" + discountPercent.ToString(CultureInfo.InvariantCulture);
            return ExecuteEconomy(transactionId, signature,
                () => Purchase(runId, roomInstanceId, itemId, quantity, discountPercent, transactionId));
        }

        public EconomyTransactionOutcome TrySellItem(
            RunId runId,
            RoomInstanceId roomInstanceId,
            ItemInstanceId itemInstanceId,
            int quantity,
            TransactionId transactionId)
        {
            string signature = "economy|sell-item|" + runId + "|" + roomInstanceId + "|" +
                (itemInstanceId.IsValid ? itemInstanceId.ToString() : string.Empty) + "|" + quantity.ToString(CultureInfo.InvariantCulture);
            return ExecuteEconomy(transactionId, signature,
                () => SellItem(runId, roomInstanceId, itemInstanceId, quantity, transactionId));
        }

        public EconomyTransactionOutcome TrySellMaterial(
            RunId runId,
            RoomInstanceId roomInstanceId,
            MaterialKind materialKind,
            int quantity,
            TransactionId transactionId)
        {
            string signature = "economy|sell-material|" + runId + "|" + roomInstanceId + "|" + materialKind + "|" +
                quantity.ToString(CultureInfo.InvariantCulture);
            return ExecuteEconomy(transactionId, signature,
                () => SellMaterial(runId, roomInstanceId, materialKind, quantity, transactionId));
        }

        public RestStopOutcome TryEnterRest(RoomInstanceId roomInstanceId, int currentHealth, int maximumHealth)
        {
            if (!roomInstanceId.IsValid)
                return new RestStopOutcome(roomInstanceId, EconomyOperationStatus.RejectedInvalidRoom, currentHealth, 0);
            if (maximumHealth <= 0 || currentHealth < 0 || currentHealth > maximumHealth)
                return new RestStopOutcome(roomInstanceId, EconomyOperationStatus.RejectedInvalidHealth, currentHealth, 0);
            if (currentHealth == 0)
                return new RestStopOutcome(roomInstanceId, EconomyOperationStatus.RejectedDead, currentHealth, 0);

            string roomIdText = roomInstanceId.ToString();
            if (_restStops.TryGetValue(roomIdText, out RestStopUseSnapshot existing) && existing.entered)
                return new RestStopOutcome(roomInstanceId, EconomyOperationStatus.RejectedAlreadyUsed, currentHealth, 0);

            int restored = Math.Min(RestStopService.EntryHealingAmount, maximumHealth - currentHealth);
            _restStops[roomIdText] = new RestStopUseSnapshot
            {
                roomInstanceId = roomIdText,
                entered = true,
                entryHealthRestored = restored
            };
            return new RestStopOutcome(roomInstanceId, EconomyOperationStatus.Succeeded, currentHealth + restored, restored);
        }

        public EconomyTransactionOutcome TryUseTalia(
            RoomInstanceId roomInstanceId,
            int currentHealth,
            int maximumHealth,
            TransactionId transactionId)
        {
            string signature = "economy|talia|" +
                (roomInstanceId.IsValid ? roomInstanceId.ToString() : string.Empty) + "|" +
                currentHealth.ToString(CultureInfo.InvariantCulture) + "|" +
                maximumHealth.ToString(CultureInfo.InvariantCulture);
            return ExecuteEconomy(transactionId, signature,
                () => UseTalia(roomInstanceId, currentHealth, maximumHealth, transactionId));
        }

        /// <summary>
        /// Crafts a consumable or upgrades one owned equipment instance as a single inventory transaction.
        /// A failed craft leaves gameplay state untouched; its idempotency receipt is still recorded.
        /// </summary>
        public CraftingOutcome TryCraft(
            RecipeCatalog recipeCatalog,
            string recipeId,
            TransactionId transactionId,
            ItemInstanceId upgradeTargetInstanceId = default)
        {
            if (recipeCatalog == null) throw new ArgumentNullException(nameof(recipeCatalog));

            string targetId = upgradeTargetInstanceId.IsValid ? upgradeTargetInstanceId.ToString() : string.Empty;
            string signature = "craft|" + (recipeId ?? string.Empty) + "|" + targetId;
            return ExecuteCrafting(transactionId, signature,
                () => Craft(recipeCatalog, recipeId, transactionId, upgradeTargetInstanceId));
        }

        /// <summary>Applies only an equipment-upgrade recipe through the inventory's atomic crafting rules and ledger.</summary>
        public SmithingOutcome TryUpgrade(
            RecipeCatalog recipeCatalog,
            string recipeId,
            ItemInstanceId targetInstanceId,
            TransactionId transactionId)
        {
            if (recipeCatalog == null) throw new ArgumentNullException(nameof(recipeCatalog));

            string targetId = targetInstanceId.IsValid ? targetInstanceId.ToString() : string.Empty;
            string signature = "smith|" + (recipeId ?? string.Empty) + "|" + targetId;
            return ExecuteSmithing(transactionId, signature, () =>
            {
                if (!recipeCatalog.TryGet(recipeId, out RecipeDefinition recipe))
                    return new SmithingOutcome(CraftingRejected(
                        transactionId, recipeId, CraftingOperationStatus.RejectedUnknownRecipe));

                if (recipe.ResultKind != CraftingResultKind.Upgrade)
                    return new SmithingOutcome(CraftingRejected(
                        transactionId, recipe.Id, CraftingOperationStatus.RejectedInvalidRecipe),
                        rejectedNonUpgradeRecipe: true);

                return new SmithingOutcome(Craft(recipeCatalog, recipeId, transactionId, targetInstanceId));
            });
        }

        private EconomyTransactionOutcome Purchase(
            RunId runId,
            RoomInstanceId roomInstanceId,
            string itemId,
            int quantity,
            int discountPercent,
            TransactionId transactionId)
        {
            EconomyOperationStatus marketStatus = ValidateMerchant(runId, roomInstanceId, out MerchantStockSnapshot stock);
            if (marketStatus != EconomyOperationStatus.Succeeded)
                return EconomyRejected(transactionId, marketStatus, itemId);
            if (string.IsNullOrWhiteSpace(itemId) || !TryGetOffer(stock, itemId, out MerchantOfferSnapshot offer))
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedUnknownOffer, itemId);
            if (quantity <= 0) return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity, itemId);
            if (discountPercent < 0) return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidDiscount, itemId);
            if (offer.remainingQuantity < quantity)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedOutOfStock, itemId);
            if (!_catalog.TryGet(itemId, out ItemDefinition definition))
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedUnknownItem, itemId);

            int totalPrice;
            try
            {
                int unitPrice = MerchantPricing.GetPurchaseUnitPrice(definition, (ItemRarity)offer.rarity, discountPercent);
                totalPrice = MerchantPricing.GetTotalPrice(unitPrice, quantity);
            }
            catch (OverflowException)
            {
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity, itemId);
            }

            if (definition.Kind == ItemKind.Weapon && !definition.IsCompatibleWithClass(ClassId))
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedIncompatibleClass, itemId);

            EconomyOperationStatus capacityStatus = CanStorePurchasedItem(definition, (ItemRarity)offer.rarity, quantity);
            if (capacityStatus != EconomyOperationStatus.Succeeded)
                return EconomyRejected(transactionId, capacityStatus, itemId);
            if (_gold < totalPrice)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInsufficientGold, itemId);

            ItemInstanceId purchasedInstanceId = StorePurchasedItem(definition, (ItemRarity)offer.rarity, quantity);
            _gold -= totalPrice;
            offer.remainingQuantity -= quantity;
            return new EconomyTransactionOutcome(transactionId, EconomyOperationStatus.Succeeded,
                itemId, purchasedInstanceId, quantity, -totalPrice);
        }

        private EconomyTransactionOutcome SellItem(
            RunId runId,
            RoomInstanceId roomInstanceId,
            ItemInstanceId itemInstanceId,
            int quantity,
            TransactionId transactionId)
        {
            EconomyOperationStatus marketStatus = ValidateMerchant(runId, roomInstanceId, out _);
            if (marketStatus != EconomyOperationStatus.Succeeded)
                return EconomyRejected(transactionId, marketStatus);
            if (!itemInstanceId.IsValid || quantity <= 0)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity);

            InventoryItemStack item = FindByInstanceId(itemInstanceId);
            if (item == null) return EconomyRejected(transactionId, EconomyOperationStatus.RejectedNotOwned);
            if (quantity > item.Quantity)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInsufficientQuantity, item.ItemId, itemInstanceId);
            if (!_catalog.TryGet(item.ItemId, out ItemDefinition definition) || definition.Kind == ItemKind.Material)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedUnknownItem, item.ItemId, itemInstanceId);

            bool equipped = IsEquipped(itemInstanceId);
            if (equipped && quantity != item.Quantity)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity, item.ItemId, itemInstanceId);

            int totalValue;
            try
            {
                totalValue = MerchantPricing.GetTotalPrice(
                    MerchantPricing.GetSaleUnitPrice(definition, item.Rarity), quantity);
            }
            catch (OverflowException)
            {
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity, item.ItemId, itemInstanceId);
            }
            if ((long)_gold + totalValue > int.MaxValue)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedGoldCapacity, item.ItemId, itemInstanceId);

            RemoveOwnedItem(itemInstanceId, quantity);
            _gold += totalValue;
            return new EconomyTransactionOutcome(transactionId, EconomyOperationStatus.Succeeded,
                item.ItemId, itemInstanceId, quantity, totalValue);
        }

        private EconomyTransactionOutcome SellMaterial(
            RunId runId,
            RoomInstanceId roomInstanceId,
            MaterialKind materialKind,
            int quantity,
            TransactionId transactionId)
        {
            EconomyOperationStatus marketStatus = ValidateMerchant(runId, roomInstanceId, out _);
            if (marketStatus != EconomyOperationStatus.Succeeded)
                return EconomyRejected(transactionId, marketStatus);
            if (!Enum.IsDefined(typeof(MaterialKind), materialKind) || quantity <= 0)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity);
            if (_materials[materialKind] < quantity)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInsufficientQuantity,
                    _catalog.GetMaterial(materialKind).Id);

            ItemDefinition definition = _catalog.GetMaterial(materialKind);
            int totalValue;
            try
            {
                totalValue = MerchantPricing.GetTotalPrice(
                    MerchantPricing.GetSaleUnitPrice(definition, ItemRarity.Common), quantity);
            }
            catch (OverflowException)
            {
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidQuantity, definition.Id);
            }
            if ((long)_gold + totalValue > int.MaxValue)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedGoldCapacity, definition.Id);

            _materials[materialKind] -= quantity;
            _gold += totalValue;
            return new EconomyTransactionOutcome(transactionId, EconomyOperationStatus.Succeeded,
                definition.Id, default, quantity, totalValue);
        }

        private EconomyTransactionOutcome UseTalia(
            RoomInstanceId roomInstanceId,
            int currentHealth,
            int maximumHealth,
            TransactionId transactionId)
        {
            if (!roomInstanceId.IsValid)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidRoom);
            if (maximumHealth <= 0 || currentHealth < 0 || currentHealth > maximumHealth)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidHealth, roomInstanceId: roomInstanceId.ToString());
            if (currentHealth == 0)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedDead, roomInstanceId: roomInstanceId.ToString());
            if (!_restStops.TryGetValue(roomInstanceId.ToString(), out RestStopUseSnapshot rest) || !rest.entered)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedRestNotEntered, roomInstanceId: roomInstanceId.ToString());
            if (rest.taliaUsed)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedAlreadyUsed, roomInstanceId: roomInstanceId.ToString());
            if (currentHealth >= maximumHealth)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedFullHealth, roomInstanceId: roomInstanceId.ToString());
            if (_gold < RestStopService.TaliaGoldCost)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInsufficientGold, roomInstanceId: roomInstanceId.ToString());

            int restored = Math.Min(RestStopService.TaliaHealingAmount, maximumHealth - currentHealth);
            int healthAfter = currentHealth + restored;
            _gold -= RestStopService.TaliaGoldCost;
            rest.taliaUsed = true;
            rest.taliaHealthRestored = restored;
            rest.taliaHealthAfter = healthAfter;
            return new EconomyTransactionOutcome(transactionId, EconomyOperationStatus.Succeeded,
                quantity: 1,
                goldDelta: -RestStopService.TaliaGoldCost,
                resultingHealth: healthAfter,
                healthRestored: restored,
                roomInstanceId: roomInstanceId.ToString());
        }

        private EconomyOperationStatus ValidateMerchant(RunId runId, RoomInstanceId roomInstanceId, out MerchantStockSnapshot stock)
        {
            stock = null;
            if (!runId.IsValid || !roomInstanceId.IsValid)
                return EconomyOperationStatus.RejectedStockNotInitialized;
            if (_merchantRunId.Length == 0 || !_merchantStocks.TryGetValue(roomInstanceId.ToString(), out stock))
                return EconomyOperationStatus.RejectedStockNotInitialized;
            if (!string.Equals(_merchantRunId, runId.ToString(), StringComparison.Ordinal))
                return EconomyOperationStatus.RejectedWrongRun;
            return EconomyOperationStatus.Succeeded;
        }

        private static bool TryGetOffer(MerchantStockSnapshot stock, string itemId, out MerchantOfferSnapshot offer)
        {
            foreach (MerchantOfferSnapshot candidate in stock.offers)
            {
                if (!string.Equals(candidate.itemId, itemId, StringComparison.Ordinal)) continue;
                offer = candidate;
                return true;
            }
            offer = null;
            return false;
        }

        private EconomyOperationStatus CanStorePurchasedItem(ItemDefinition definition, ItemRarity rarity, int quantity)
        {
            if (definition.Kind == ItemKind.Material)
            {
                long total = (long)_materials[definition.MaterialKind.Value] + quantity;
                return total <= MaximumMaterialCount
                    ? EconomyOperationStatus.Succeeded
                    : EconomyOperationStatus.RejectedMaterialCapacity;
            }

            if (definition.Kind == ItemKind.Consumable)
            {
                int capacity = 0;
                for (int index = 0; index < _backpackSlots.Length; index++)
                {
                    InventoryItemStack existing = _backpackSlots[index];
                    if (existing == null) capacity += definition.MaxStack;
                    else if (existing.ItemId == definition.Id && existing.Rarity == rarity && existing.UpgradeLevel == 0)
                        capacity += definition.MaxStack - existing.Quantity;
                }
                return capacity >= quantity
                    ? EconomyOperationStatus.Succeeded
                    : EconomyOperationStatus.RejectedBackpackFull;
            }

            if (quantity != 1) return EconomyOperationStatus.RejectedInvalidQuantity;
            return FindEmptySlot() >= 0
                ? EconomyOperationStatus.Succeeded
                : EconomyOperationStatus.RejectedBackpackFull;
        }

        private ItemInstanceId StorePurchasedItem(ItemDefinition definition, ItemRarity rarity, int quantity)
        {
            if (definition.Kind == ItemKind.Material)
            {
                _materials[definition.MaterialKind.Value] += quantity;
                return default;
            }

            if (definition.Kind == ItemKind.Consumable)
            {
                int remaining = quantity;
                ItemInstanceId firstChangedInstanceId = default;
                for (int index = 0; index < _backpackSlots.Length && remaining > 0; index++)
                {
                    InventoryItemStack existing = _backpackSlots[index];
                    if (existing == null || existing.ItemId != definition.Id || existing.Rarity != rarity ||
                        existing.UpgradeLevel != 0 || existing.Quantity >= definition.MaxStack)
                        continue;
                    int moved = Math.Min(remaining, definition.MaxStack - existing.Quantity);
                    _backpackSlots[index] = existing.WithQuantity(existing.Quantity + moved);
                    if (!firstChangedInstanceId.IsValid) firstChangedInstanceId = existing.InstanceId;
                    remaining -= moved;
                }

                for (int index = 0; index < _backpackSlots.Length && remaining > 0; index++)
                {
                    if (_backpackSlots[index] != null) continue;
                    int moved = Math.Min(remaining, definition.MaxStack);
                    ItemInstanceId instanceId = CreateUniqueItemInstanceId();
                    _backpackSlots[index] = new InventoryItemStack(instanceId, definition.Id, rarity, 0, moved);
                    if (!firstChangedInstanceId.IsValid) firstChangedInstanceId = instanceId;
                    remaining -= moved;
                }
                return firstChangedInstanceId;
            }

            ItemInstanceId newInstanceId = CreateUniqueItemInstanceId();
            _backpackSlots[FindEmptySlot()] = new InventoryItemStack(newInstanceId, definition.Id, rarity, 0, 1);
            return newInstanceId;
        }

        private ItemInstanceId CreateUniqueItemInstanceId()
        {
            ItemInstanceId instanceId;
            do
            {
                instanceId = ItemInstanceId.New();
            }
            while (ContainsInstanceId(instanceId));
            return instanceId;
        }

        private bool IsEquipped(ItemInstanceId instanceId)
        {
            return _weapon?.InstanceId == instanceId || _armor?.InstanceId == instanceId ||
                _relicOne?.InstanceId == instanceId || _relicTwo?.InstanceId == instanceId;
        }

        private void RemoveOwnedItem(ItemInstanceId instanceId, int quantity)
        {
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                InventoryItemStack existing = _backpackSlots[index];
                if (existing == null || existing.InstanceId != instanceId) continue;
                if (existing.Quantity == quantity)
                {
                    _backpackSlots[index] = null;
                    if (_quickConsumableId == instanceId) _quickConsumableId = default;
                }
                else
                {
                    _backpackSlots[index] = existing.WithQuantity(existing.Quantity - quantity);
                }
                return;
            }

            if (_weapon?.InstanceId == instanceId) _weapon = null;
            else if (_armor?.InstanceId == instanceId) _armor = null;
            else if (_relicOne?.InstanceId == instanceId) _relicOne = null;
            else if (_relicTwo?.InstanceId == instanceId) _relicTwo = null;
        }

        private EconomyTransactionOutcome ExecuteEconomy(
            TransactionId transactionId,
            string signature,
            Func<EconomyTransactionOutcome> operation)
        {
            if (!transactionId.IsValid)
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedInvalidTransaction);
            if (_transactions.TryGetValue(transactionId, out CachedTransaction cached))
            {
                if (string.Equals(cached.Signature, signature, StringComparison.Ordinal) && cached.EconomyOutcome != null)
                    return cached.EconomyOutcome;
                return EconomyRejected(transactionId, EconomyOperationStatus.RejectedTransactionConflict);
            }

            EconomyTransactionOutcome outcome = operation();
            var inventoryOutcome = new InventoryOperationOutcome(
                transactionId, InventoryOperationStatus.Unknown, itemId: outcome.ItemId);
            _transactions.Add(transactionId, new CachedTransaction(signature, inventoryOutcome, economyOutcome: outcome));
            return outcome;
        }

        private static EconomyTransactionOutcome EconomyRejected(
            TransactionId transactionId,
            EconomyOperationStatus status,
            string itemId = null,
            ItemInstanceId itemInstanceId = default,
            string roomInstanceId = null)
        {
            return new EconomyTransactionOutcome(transactionId, status, itemId, itemInstanceId,
                roomInstanceId: roomInstanceId);
        }

        private CraftingOutcome Craft(
            RecipeCatalog recipeCatalog,
            string recipeId,
            TransactionId transactionId,
            ItemInstanceId upgradeTargetInstanceId)
        {
            if (!recipeCatalog.TryGet(recipeId, out RecipeDefinition recipe))
                return CraftingRejected(transactionId, recipeId, CraftingOperationStatus.RejectedUnknownRecipe);

            InventoryItemStack upgradeTarget = null;
            ItemDefinition consumableDefinition = null;
            if (recipe.ResultKind == CraftingResultKind.Consumable)
            {
                if (upgradeTargetInstanceId.IsValid)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedUnexpectedTarget);

                if (!_catalog.TryGet(recipe.ResultItemId, out consumableDefinition) ||
                    consumableDefinition.Kind != ItemKind.Consumable || recipe.ResultQuantity < 1 ||
                    recipe.ResultQuantity > consumableDefinition.MaxStack)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInvalidRecipe);

                if (!CanFitCraftedConsumable(consumableDefinition, recipe.ResultQuantity))
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedBackpackFull);
            }
            else if (recipe.ResultKind == CraftingResultKind.Upgrade)
            {
                if (!upgradeTargetInstanceId.IsValid || (upgradeTarget = FindByInstanceId(upgradeTargetInstanceId)) == null)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedMissingTarget);

                if (!_catalog.TryGet(upgradeTarget.ItemId, out ItemDefinition targetDefinition))
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInvalidRecipe);
                if (targetDefinition.Kind != recipe.UpgradeTargetKind)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedWrongTargetKind);
                if (targetDefinition.Kind == ItemKind.Weapon && !targetDefinition.IsCompatibleWithClass(ClassId))
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedIncompatibleClass);
                if (upgradeTarget.UpgradeLevel >= 3)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedUpgradeLimit);
                if (upgradeTarget.UpgradeLevel != recipe.RequiredCurrentUpgradeLevel ||
                    recipe.ResultUpgradeLevel != upgradeTarget.UpgradeLevel + 1)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInvalidUpgradeLevel);
            }
            else
            {
                return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInvalidRecipe);
            }

            foreach (RecipeMaterialCost cost in recipe.MaterialCosts)
            {
                if (GetMaterialCount(cost.MaterialKind) < cost.Quantity)
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInsufficientMaterials);
            }
            if (_gold < recipe.GoldCost)
                return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedInsufficientGold);

            var changedSlots = new List<ConsumableStackChange>();
            var resultStacks = new List<InventoryItemStack>();
            InventoryItemStack upgradedStack = null;
            if (recipe.ResultKind == CraftingResultKind.Consumable)
            {
                if (!TryPlanCraftedConsumable(consumableDefinition, recipe.ResultQuantity, changedSlots))
                    return CraftingRejected(transactionId, recipe.Id, CraftingOperationStatus.RejectedBackpackFull);

                var createdIds = new HashSet<ItemInstanceId>();
                for (int index = 0; index < changedSlots.Count; index++)
                {
                    ConsumableStackChange change = changedSlots[index];
                    InventoryItemStack changedStack;
                    if (change.ExistingStack == null)
                    {
                        ItemInstanceId instanceId;
                        do
                        {
                            instanceId = ItemInstanceId.New();
                        }
                        while (ContainsInstanceId(instanceId) || !createdIds.Add(instanceId));

                        changedStack = new InventoryItemStack(instanceId, consumableDefinition.Id,
                            ItemRarity.Common, 0, change.Quantity);
                    }
                    else
                    {
                        changedStack = change.ExistingStack.WithQuantity(change.ExistingStack.Quantity + change.Quantity);
                    }
                    changedSlots[index] = change.WithResult(changedStack);
                    resultStacks.Add(changedStack);
                }
            }
            else
            {
                upgradedStack = new InventoryItemStack(upgradeTarget.InstanceId, upgradeTarget.ItemId,
                    upgradeTarget.Rarity, recipe.ResultUpgradeLevel, upgradeTarget.Quantity);
                resultStacks.Add(upgradedStack);
            }

            foreach (RecipeMaterialCost cost in recipe.MaterialCosts)
                _materials[cost.MaterialKind] -= cost.Quantity;
            _gold -= recipe.GoldCost;
            if (recipe.ResultKind == CraftingResultKind.Consumable)
            {
                foreach (ConsumableStackChange change in changedSlots)
                    _backpackSlots[change.Index] = change.ResultStack;
            }
            else
            {
                ReplaceOwnedStack(upgradeTargetInstanceId, upgradedStack);
            }

            return new CraftingOutcome(transactionId, recipe.Id, CraftingOperationStatus.Succeeded, resultStacks);
        }

        private CraftingOutcome ExecuteCrafting(
            TransactionId transactionId,
            string signature,
            Func<CraftingOutcome> operation)
        {
            if (!transactionId.IsValid)
                return CraftingRejected(transactionId, string.Empty, CraftingOperationStatus.RejectedInvalidTransaction);
            if (_transactions.TryGetValue(transactionId, out CachedTransaction cached))
            {
                if (string.Equals(cached.Signature, signature, StringComparison.Ordinal) &&
                    cached.CraftingOutcome != null && cached.SmithingOutcome == null)
                    return cached.CraftingOutcome;
                return CraftingRejected(transactionId, string.Empty, CraftingOperationStatus.RejectedTransactionConflict);
            }

            CraftingOutcome outcome = operation();
            string resultItemId = outcome.ResultStacks.Count > 0 ? outcome.ResultStacks[0].ItemId : string.Empty;
            var inventoryOutcome = new InventoryOperationOutcome(transactionId,
                MapCraftingStatus(outcome.Status), null, resultItemId);
            _transactions.Add(transactionId, new CachedTransaction(signature, inventoryOutcome, outcome));
            return outcome;
        }

        private SmithingOutcome ExecuteSmithing(
            TransactionId transactionId,
            string signature,
            Func<SmithingOutcome> operation)
        {
            if (!transactionId.IsValid)
                return new SmithingOutcome(CraftingRejected(
                    transactionId, string.Empty, CraftingOperationStatus.RejectedInvalidTransaction));
            if (_transactions.TryGetValue(transactionId, out CachedTransaction cached))
            {
                if (string.Equals(cached.Signature, signature, StringComparison.Ordinal) && cached.SmithingOutcome != null)
                    return cached.SmithingOutcome;
                return new SmithingOutcome(CraftingRejected(
                    transactionId, string.Empty, CraftingOperationStatus.RejectedTransactionConflict));
            }

            SmithingOutcome smithingOutcome = operation();
            CraftingOutcome craftingOutcome = smithingOutcome.CraftingOutcome;
            string resultItemId = craftingOutcome.ResultStacks.Count > 0 ? craftingOutcome.ResultStacks[0].ItemId : string.Empty;
            var inventoryOutcome = new InventoryOperationOutcome(transactionId,
                MapCraftingStatus(craftingOutcome.Status), null, resultItemId);
            _transactions.Add(transactionId, new CachedTransaction(
                signature, inventoryOutcome, craftingOutcome, smithingOutcome: smithingOutcome));
            return smithingOutcome;
        }

        private bool CanFitCraftedConsumable(ItemDefinition definition, int quantity)
        {
            int capacity = 0;
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                InventoryItemStack existing = _backpackSlots[index];
                if (existing == null) capacity += definition.MaxStack;
                else if (existing.ItemId == definition.Id && existing.Rarity == ItemRarity.Common && existing.UpgradeLevel == 0)
                    capacity += definition.MaxStack - existing.Quantity;
            }
            return capacity >= quantity;
        }

        private bool TryPlanCraftedConsumable(
            ItemDefinition definition,
            int quantity,
            List<ConsumableStackChange> changes)
        {
            int remaining = quantity;
            for (int index = 0; index < _backpackSlots.Length && remaining > 0; index++)
            {
                InventoryItemStack existing = _backpackSlots[index];
                if (existing == null || existing.ItemId != definition.Id || existing.Rarity != ItemRarity.Common ||
                    existing.UpgradeLevel != 0 || existing.Quantity >= definition.MaxStack)
                    continue;

                int addedQuantity = Math.Min(remaining, definition.MaxStack - existing.Quantity);
                changes.Add(new ConsumableStackChange(index, existing, addedQuantity));
                remaining -= addedQuantity;
            }

            for (int index = 0; index < _backpackSlots.Length && remaining > 0; index++)
            {
                if (_backpackSlots[index] != null) continue;
                int addedQuantity = Math.Min(remaining, definition.MaxStack);
                changes.Add(new ConsumableStackChange(index, null, addedQuantity));
                remaining -= addedQuantity;
            }

            return remaining == 0;
        }

        private void ReplaceOwnedStack(ItemInstanceId instanceId, InventoryItemStack replacement)
        {
            for (int index = 0; index < _backpackSlots.Length; index++)
            {
                if (_backpackSlots[index]?.InstanceId != instanceId) continue;
                _backpackSlots[index] = replacement;
                return;
            }

            if (_weapon?.InstanceId == instanceId) _weapon = replacement;
            else if (_armor?.InstanceId == instanceId) _armor = replacement;
            else if (_relicOne?.InstanceId == instanceId) _relicOne = replacement;
            else if (_relicTwo?.InstanceId == instanceId) _relicTwo = replacement;
        }

        private static CraftingOutcome CraftingRejected(
            TransactionId transactionId,
            string recipeId,
            CraftingOperationStatus status)
        {
            return new CraftingOutcome(transactionId, recipeId, status);
        }

        private static InventoryOperationStatus MapCraftingStatus(CraftingOperationStatus status)
        {
            switch (status)
            {
                case CraftingOperationStatus.Succeeded: return InventoryOperationStatus.Succeeded;
                case CraftingOperationStatus.RejectedInvalidTransaction: return InventoryOperationStatus.RejectedInvalidTransaction;
                case CraftingOperationStatus.RejectedTransactionConflict: return InventoryOperationStatus.RejectedTransactionConflict;
                case CraftingOperationStatus.RejectedUnknownRecipe: return InventoryOperationStatus.RejectedUnknownItem;
                case CraftingOperationStatus.RejectedInvalidRecipe: return InventoryOperationStatus.RejectedInvalidItem;
                case CraftingOperationStatus.RejectedInsufficientMaterials: return InventoryOperationStatus.RejectedInvalidMaterial;
                case CraftingOperationStatus.RejectedInsufficientGold: return InventoryOperationStatus.RejectedInvalidGold;
                case CraftingOperationStatus.RejectedBackpackFull: return InventoryOperationStatus.RejectedFull;
                case CraftingOperationStatus.RejectedMissingTarget:
                case CraftingOperationStatus.RejectedUnexpectedTarget: return InventoryOperationStatus.RejectedInvalidSlot;
                case CraftingOperationStatus.RejectedWrongTargetKind: return InventoryOperationStatus.RejectedWrongEquipmentSlot;
                case CraftingOperationStatus.RejectedIncompatibleClass: return InventoryOperationStatus.RejectedIncompatibleClass;
                case CraftingOperationStatus.RejectedInvalidUpgradeLevel:
                case CraftingOperationStatus.RejectedUpgradeLimit: return InventoryOperationStatus.RejectedInvalidItem;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private InventoryOperationOutcome Execute(TransactionId transactionId, string signature, Func<InventoryOperationOutcome> operation)
        {
            if (!transactionId.IsValid) return Rejected(transactionId, InventoryOperationStatus.RejectedInvalidTransaction);
            if (_transactions.TryGetValue(transactionId, out CachedTransaction cached))
            {
                return string.Equals(cached.Signature, signature, StringComparison.Ordinal) &&
                    cached.Outcome != null && cached.EconomyOutcome == null && cached.SmithingOutcome == null
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
            public CachedTransaction(
                string signature,
                InventoryOperationOutcome outcome,
                CraftingOutcome craftingOutcome = null,
                EconomyTransactionOutcome economyOutcome = null,
                SmithingOutcome smithingOutcome = null)
            {
                Signature = signature;
                Outcome = outcome;
                CraftingOutcome = craftingOutcome;
                EconomyOutcome = economyOutcome;
                SmithingOutcome = smithingOutcome;
            }
            public string Signature { get; }
            public InventoryOperationOutcome Outcome { get; }
            public CraftingOutcome CraftingOutcome { get; }
            public EconomyTransactionOutcome EconomyOutcome { get; }
            public SmithingOutcome SmithingOutcome { get; }
        }

        private readonly struct ConsumableStackChange
        {
            public ConsumableStackChange(int index, InventoryItemStack existingStack, int quantity, InventoryItemStack resultStack = null)
            {
                Index = index;
                ExistingStack = existingStack;
                Quantity = quantity;
                ResultStack = resultStack;
            }

            public int Index { get; }
            public InventoryItemStack ExistingStack { get; }
            public int Quantity { get; }
            public InventoryItemStack ResultStack { get; }

            public ConsumableStackChange WithResult(InventoryItemStack resultStack)
            {
                return new ConsumableStackChange(Index, ExistingStack, Quantity, resultStack);
            }
        }
    }
}
