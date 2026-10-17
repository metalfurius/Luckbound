using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Maps expeditions to saves and rolls unfinished combat encounters back to their entry state.</summary>
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
                maximumHealth = run.StartingHealth,
                currentHealth = run.StartingHealth,
                maximumEnergy = run.StartingEnergy,
                currentEnergy = run.StartingEnergy,
                healthPotionCharges = run.Inventory.QuickConsumable == null ? 0 : run.Inventory.QuickConsumable.Quantity,
                accumulatedRenown = 0,
                pactIds = Array.Empty<string>(),
                fortuneOfferBookSnapshot = new FortuneOfferBook(
                    run.Route.GenerationVersion, run.Seed, run.RaceId).CreateSnapshot(),
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
            ValidateFortuneBookSnapshot(persisted.fortuneOfferBookSnapshot, persisted.seed,
                persisted.generationVersion, persisted.raceId, "Active expedition");
            ExpeditionSaveData restored = persisted.Clone();
            if (string.IsNullOrWhiteSpace(restored.roomEntryCheckpointJson)) return restored;

            ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(restored.routeSnapshotJson);
            ExpeditionRouteNode currentNode = route.Nodes.SingleOrDefault(node =>
                string.Equals(node.NodeId, restored.currentNodeId, StringComparison.Ordinal));
            if (currentNode == null)
                throw new FormatException("The current route node is missing from the saved route.");

            ExpeditionRoomCheckpointSaveData checkpoint;
            try { checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(restored.roomEntryCheckpointJson); }
            catch (Exception exception) { throw new FormatException("Room-entry checkpoint is invalid.", exception); }
            if (checkpoint == null || !string.Equals(checkpoint.roomNodeId, restored.currentNodeId, StringComparison.Ordinal))
                throw new FormatException("Room-entry checkpoint does not belong to the current route node.");
            if (string.IsNullOrWhiteSpace(checkpoint.inventorySnapshotJson))
                throw new FormatException("Room-entry checkpoint inventory is missing.");
            ValidateCheckpoint(checkpoint, restored);
            if (!IsCombatEncounter(currentNode.Kind)) return restored;

            restored.inventorySnapshotJson = checkpoint.inventorySnapshotJson;
            restored.maximumHealth = checkpoint.maximumHealth;
            restored.currentHealth = checkpoint.currentHealth;
            restored.maximumEnergy = checkpoint.maximumEnergy;
            restored.currentEnergy = checkpoint.currentEnergy;
            restored.healthPotionCharges = checkpoint.healthPotionCharges;
            restored.accumulatedRenown = checkpoint.accumulatedRenown;
            restored.fortuneOfferBookSnapshot = checkpoint.fortuneOfferBookSnapshot;
            restored.pactIds = Copy(checkpoint.pactIds);
            restored.collectedItemIds = Copy(checkpoint.collectedItemIds);
            restored.claimedRewardIds = Copy(checkpoint.claimedRewardIds);
            return restored;
        }

        private static bool IsCombatEncounter(ExpeditionRouteNodeKind kind)
        {
            return kind == ExpeditionRouteNodeKind.Combat || kind == ExpeditionRouteNodeKind.OptionalCombat ||
                kind == ExpeditionRouteNodeKind.Guardian || kind == ExpeditionRouteNodeKind.FinalBoss;
        }

        /// <summary>Validates the nested room-entry payload before a checksummed save is trusted or written.</summary>
        public static void ValidateRoomEntryCheckpoint(ExpeditionSaveData persisted)
        {
            if (persisted == null) throw new ArgumentNullException(nameof(persisted));
            ValidateFortuneBookSnapshot(persisted.fortuneOfferBookSnapshot, persisted.seed,
                persisted.generationVersion, persisted.raceId, "Active expedition");
            if (!string.IsNullOrWhiteSpace(persisted.roomEntryCheckpointJson))
            {
                ExpeditionRoomCheckpointSaveData checkpoint;
                try { checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(persisted.roomEntryCheckpointJson); }
                catch (Exception exception) { throw new FormatException("Room-entry checkpoint is invalid.", exception); }
                if (checkpoint == null || !string.Equals(checkpoint.roomNodeId, persisted.currentNodeId, StringComparison.Ordinal))
                    throw new FormatException("Room-entry checkpoint does not belong to the current route node.");
                ValidateCheckpoint(checkpoint, persisted);
            }
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
                fortuneOfferBookSnapshot = state.Clone().fortuneOfferBookSnapshot,
                pactIds = Copy(state.pactIds),
                collectedItemIds = Copy(state.collectedItemIds),
                claimedRewardIds = Copy(state.claimedRewardIds)
            };
        }

        private static void ValidateCheckpoint(ExpeditionRoomCheckpointSaveData checkpoint, ExpeditionSaveData expedition)
        {
            if (checkpoint == null) throw new FormatException("Room-entry checkpoint is missing.");
            if (string.IsNullOrWhiteSpace(checkpoint.inventorySnapshotJson))
                throw new FormatException("Room-entry checkpoint inventory is missing.");
            if (checkpoint.maximumHealth < 1 || checkpoint.currentHealth < 0 || checkpoint.currentHealth > checkpoint.maximumHealth)
                throw new FormatException("Room-entry checkpoint health is invalid.");
            if (checkpoint.maximumEnergy < 0 || checkpoint.currentEnergy < 0 || checkpoint.currentEnergy > checkpoint.maximumEnergy)
                throw new FormatException("Room-entry checkpoint energy is invalid.");
            if (checkpoint.healthPotionCharges < 0 || checkpoint.accumulatedRenown < 0)
                throw new FormatException("Room-entry checkpoint contains a negative resource value.");
            ValidateIds(checkpoint.pactIds, "checkpoint pact IDs");
            ValidateIds(checkpoint.collectedItemIds, "checkpoint collected item IDs");
            ValidateIds(checkpoint.claimedRewardIds, "checkpoint claimed reward IDs");
            ValidateFortuneBookSnapshot(checkpoint.fortuneOfferBookSnapshot, expedition.seed,
                expedition.generationVersion, expedition.raceId, "Room-entry checkpoint");
        }

        private static void ValidateFortuneBookSnapshot(FortuneOfferBookSnapshot snapshot, uint seed,
            int generationVersion, string raceId, string label)
        {
            if (snapshot == null) throw new FormatException(label + " Fortuna offer book snapshot is missing.");
            try { FortuneOfferBook.Restore(snapshot); }
            catch (Exception exception) { throw new FormatException(label + " Fortuna offer book snapshot is invalid.", exception); }
            if (snapshot.seed != seed || snapshot.generationVersion != generationVersion ||
                !string.Equals(snapshot.raceId, raceId, StringComparison.Ordinal))
                throw new FormatException(label + " Fortuna offer book identity does not match the expedition.");
        }

        private static void ValidateIds(string[] values, string label)
        {
            if (values == null || values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new FormatException(label + " are missing, invalid, or duplicated.");
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

        /// <summary>Builds one candidate that records the source result and advances its route cursor together.</summary>
        public static ExpeditionSaveData CompleteAndAdvanceRoom(ExpeditionSaveData state, string resultId,
            string resultJson, string nextNodeId)
        {
            ExpeditionSaveData completed = CompleteRoom(state, resultId, resultJson);
            return AdvanceToRoom(completed, nextNodeId);
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
