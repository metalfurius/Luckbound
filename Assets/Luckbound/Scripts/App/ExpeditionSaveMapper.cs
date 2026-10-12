using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Maps the runtime expedition to its durable document and restores the exact room-entry state.</summary>
    public static class ExpeditionSaveMapper
    {
        public static ExpeditionSaveData Create(ExpeditionRunConfiguration run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            string startNodeId = run.Route.MandatoryNodeIds[0];
            var save = new ExpeditionSaveData
            {
                runId = run.RunId.ToString(),
                seed = run.Seed,
                generationVersion = run.Route.GenerationVersion,
                contentVersion = SaveFormat.ContentVersion,
                classId = run.ClassId,
                raceId = run.RaceId,
                selectedSkillIds = run.SkillIds.ToArray(),
                startingEquipmentInstanceIds = run.StartingEquipmentInstanceIds.Select(id => id.ToString()).ToArray(),
                routeSnapshotJson = run.Route.CanonicalForm,
                currentNodeId = startNodeId,
                visitedNodeIds = new[] { startNodeId },
                completedRoomIds = Array.Empty<string>(),
                completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>(),
                inventorySnapshotJson = JsonUtility.ToJson(run.Inventory.CreateSnapshot()),
                maximumHealth = ExpeditionRunConfiguration.InitialHealth,
                currentHealth = ExpeditionRunConfiguration.InitialHealth,
                maximumEnergy = ExpeditionRunConfiguration.InitialEnergy,
                currentEnergy = ExpeditionRunConfiguration.InitialEnergy,
                healthPotionCharges = run.Inventory.QuickConsumable == null ? 0 : run.Inventory.QuickConsumable.Quantity,
                accumulatedRenown = 0,
                pactIds = Array.Empty<string>(),
                fortunaCharges = run.StartingFortunaCharges,
                pendingOfferIds = Array.Empty<string>(),
                offerRerollCounts = Array.Empty<int>(),
                collectedItemIds = Array.Empty<string>(),
                claimedRewardIds = Array.Empty<string>()
            };
            save.roomEntryCheckpointJson = SerializeCheckpoint(CreateCheckpoint(save));
            return save;
        }

        public static ExpeditionRunConfiguration Restore(ExpeditionSaveData persisted, out ExpeditionSaveData resumedState)
        {
            if (persisted == null) throw new ArgumentNullException(nameof(persisted));
            resumedState = RestoreRoomEntry(persisted);
            ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(resumedState.routeSnapshotJson);
            if (route.Seed != resumedState.seed || route.GenerationVersion != resumedState.generationVersion)
                throw new FormatException("Saved route seed or generation version does not match the expedition header.");
            var traversal = new ExpeditionRouteTraversal(route, resumedState.visitedNodeIds);
            if (!string.Equals(traversal.CurrentNodeId, resumedState.currentNodeId, StringComparison.Ordinal))
                throw new FormatException("Saved route cursor does not match its visited-node history.");

            ExpeditionInventorySnapshot inventorySnapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(resumedState.inventorySnapshotJson);
            if (inventorySnapshot == null) throw new FormatException("Saved expedition inventory could not be decoded.");
            ItemCatalog catalog = ItemCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Items/Items").text);
            ExpeditionInventory inventory = ExpeditionInventory.Restore(catalog, inventorySnapshot);
            ItemInstanceId[] startingEquipment = (resumedState.startingEquipmentInstanceIds ?? Array.Empty<string>())
                .Select(ItemInstanceId.Parse).ToArray();
            return new ExpeditionRunConfiguration(
                RunId.Parse(resumedState.runId), resumedState.classId, resumedState.raceId,
                resumedState.selectedSkillIds, resumedState.seed, route, inventory, startingEquipment);
        }

        public static ExpeditionSaveData RestoreRoomEntry(ExpeditionSaveData persisted)
        {
            if (persisted == null) throw new ArgumentNullException(nameof(persisted));
            ExpeditionSaveData restored = persisted.Clone();
            if (string.IsNullOrWhiteSpace(restored.roomEntryCheckpointJson)) return restored;

            ExpeditionRoomCheckpointSaveData checkpoint;
            try { checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(restored.roomEntryCheckpointJson); }
            catch (Exception exception) { throw new FormatException("Room-entry checkpoint is invalid.", exception); }
            if (checkpoint == null || !string.Equals(checkpoint.roomNodeId, restored.currentNodeId, StringComparison.Ordinal))
                throw new FormatException("Room-entry checkpoint does not belong to the current route node.");
            if (string.IsNullOrWhiteSpace(checkpoint.inventorySnapshotJson))
                throw new FormatException("Room-entry checkpoint inventory is missing.");

            restored.inventorySnapshotJson = checkpoint.inventorySnapshotJson;
            restored.maximumHealth = checkpoint.maximumHealth;
            restored.currentHealth = checkpoint.currentHealth;
            restored.maximumEnergy = checkpoint.maximumEnergy;
            restored.currentEnergy = checkpoint.currentEnergy;
            restored.healthPotionCharges = checkpoint.healthPotionCharges;
            restored.accumulatedRenown = checkpoint.accumulatedRenown;
            restored.fortunaCharges = checkpoint.fortunaCharges;
            restored.pactIds = Copy(checkpoint.pactIds);
            restored.pendingOfferIds = Copy(checkpoint.pendingOfferIds);
            restored.offerRerollCounts = checkpoint.offerRerollCounts == null ? null : (int[])checkpoint.offerRerollCounts.Clone();
            restored.collectedItemIds = Copy(checkpoint.collectedItemIds);
            restored.claimedRewardIds = Copy(checkpoint.claimedRewardIds);
            return restored;
        }

        public static ExpeditionRoomCheckpointSaveData CreateCheckpoint(ExpeditionSaveData state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new ExpeditionRoomCheckpointSaveData
            {
                roomNodeId = state.currentNodeId,
                inventorySnapshotJson = state.inventorySnapshotJson,
                maximumHealth = state.maximumHealth,
                currentHealth = state.currentHealth,
                maximumEnergy = state.maximumEnergy,
                currentEnergy = state.currentEnergy,
                healthPotionCharges = state.healthPotionCharges,
                accumulatedRenown = state.accumulatedRenown,
                fortunaCharges = state.fortunaCharges,
                pactIds = Copy(state.pactIds),
                pendingOfferIds = Copy(state.pendingOfferIds),
                offerRerollCounts = state.offerRerollCounts == null ? null : (int[])state.offerRerollCounts.Clone(),
                collectedItemIds = Copy(state.collectedItemIds),
                claimedRewardIds = Copy(state.claimedRewardIds)
            };
        }

        public static ExpeditionSaveData CompleteRoom(ExpeditionSaveData state, string resultId, string resultJson)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(resultId) || string.IsNullOrWhiteSpace(resultJson))
                throw new ArgumentException("A stable result ID and serialized room result are required.");
            ExpeditionRoomCompletionSaveData existing = state.completedRoomResults
                .FirstOrDefault(result => result != null && string.Equals(result.roomNodeId, state.currentNodeId, StringComparison.Ordinal));
            if (existing != null)
            {
                if (!string.Equals(existing.resultId, resultId, StringComparison.Ordinal) ||
                    !string.Equals(existing.resultJson, resultJson, StringComparison.Ordinal))
                    throw new InvalidOperationException("A different result is already committed for this room.");
                return state.Clone();
            }
            if (state.completedRoomIds.Contains(state.currentNodeId))
                throw new FormatException("The current room is marked complete without its durable result.");

            ExpeditionSaveData completed = state.Clone();
            completed.completedRoomIds = completed.completedRoomIds.Concat(new[] { completed.currentNodeId }).ToArray();
            completed.completedRoomResults = completed.completedRoomResults.Concat(new[]
            {
                new ExpeditionRoomCompletionSaveData
                {
                    roomNodeId = completed.currentNodeId,
                    resultId = resultId,
                    resultJson = resultJson
                }
            }).ToArray();
            completed.roomEntryCheckpointJson = string.Empty;
            return completed;
        }

        public static ExpeditionSaveData AdvanceToRoom(ExpeditionSaveData state, string nextNodeId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.completedRoomIds.Contains(state.currentNodeId))
                throw new InvalidOperationException("The current room result must be committed before advancing.");
            ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(state.routeSnapshotJson);
            var traversal = new ExpeditionRouteTraversal(route, state.visitedNodeIds);
            if (!traversal.TryTransitionTo(nextNodeId))
                throw new InvalidOperationException("The requested room is not connected to the saved route cursor.");

            ExpeditionSaveData advanced = state.Clone();
            advanced.currentNodeId = nextNodeId;
            advanced.visitedNodeIds = traversal.VisitedNodeIds.ToArray();
            advanced.roomEntryCheckpointJson = SerializeCheckpoint(CreateCheckpoint(advanced));
            return advanced;
        }

        public static string SerializeCheckpoint(ExpeditionRoomCheckpointSaveData checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            return JsonUtility.ToJson(checkpoint);
        }

        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();

        private static TextAsset LoadRequired(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Required expedition data is missing: Resources/" + path + ".");
            return asset;
        }
    }
}
