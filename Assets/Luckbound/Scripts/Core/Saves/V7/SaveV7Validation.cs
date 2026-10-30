using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Progression.Contracts;

namespace Luckbound.Core.Saves.V7
{
    public static class SaveV7Validation
    {
        private const int SchemaVersion = 1;
        private const int ContentVersion = 7;
        private const string IdentityCodec = "encounter-spawn.v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> CanonicalPactIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "pact.avarice", "pact.fervor", "pact.haste", "pact.clairvoyance",
            "pact.ember", "pact.silence", "pact.gravity", "pact.bond"
        };

        public static bool TryValidateProfile(ProfileSaveEnvelopeV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (value == null)
                return Fail(SaveV7ErrorCode.InvalidData, "profile", "Profile envelope is required.", out error);
            if (!ValidateHeader(value.schemaVersion, value.contentVersion, value.generation, value.sequence,
                value.savedAtUtc, value.checksum, out error)) return false;
            if (!ValidateProfileData(value.profile, out error)) return false;
            if (value.hasActiveExpedition != (value.activeExpedition != null))
                return Fail(SaveV7ErrorCode.InvalidData, "hasActiveExpedition",
                    "The active expedition marker must match its payload.", out error);
            if (value.activeExpedition == null) return true;
            return ValidateExpedition(value.activeExpedition, out error);
        }

        public static bool TryValidateSettings(SettingsSaveEnvelopeV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (value == null)
                return Fail(SaveV7ErrorCode.InvalidData, "settings", "Settings envelope is required.", out error);
            if (!ValidateHeader(value.schemaVersion, value.contentVersion, value.generation, value.sequence,
                value.savedAtUtc, value.checksum, out error)) return false;
            if (value.settings == null)
                return Fail(SaveV7ErrorCode.InvalidData, "settings", "Settings payload is required.", out error);
            if (value.settings.languageId != "es" && value.settings.languageId != "en")
                return Fail(SaveV7ErrorCode.InvalidData, "settings.languageId", "Language must be es or en.", out error);
            if (!IsVolume(value.settings.masterVolume))
                return Fail(SaveV7ErrorCode.InvalidData, "settings.masterVolume", "Volume must be between 0 and 1.", out error);
            if (!IsVolume(value.settings.musicVolume))
                return Fail(SaveV7ErrorCode.InvalidData, "settings.musicVolume", "Volume must be between 0 and 1.", out error);
            if (!IsVolume(value.settings.effectsVolume))
                return Fail(SaveV7ErrorCode.InvalidData, "settings.effectsVolume", "Volume must be between 0 and 1.", out error);
            if (!IsVolume(value.settings.ambienceVolume))
                return Fail(SaveV7ErrorCode.InvalidData, "settings.ambienceVolume", "Volume must be between 0 and 1.", out error);
            return true;
        }

        public static bool TryValidateCheckpoint(ExpeditionRoomCheckpointSaveDataV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (value == null)
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint", "Checkpoint is required.", out error);
            if (!IsStableText(value.roomNodeId))
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint.roomNodeId", "A stable room node ID is required.", out error);
            if (string.IsNullOrWhiteSpace(value.inventorySnapshotJson))
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint.inventorySnapshotJson", "Inventory snapshot is required.", out error);
            if (value.maximumHealth < 1 || value.currentHealth < 0 || value.currentHealth > value.maximumHealth)
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint.health", "Checkpoint health is outside its positive maximum.", out error);
            if (value.maximumEnergy < 0 || value.currentEnergy < 0 || value.currentEnergy > value.maximumEnergy)
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint.energy", "Checkpoint energy is outside its nonnegative maximum.", out error);
            if (value.healthPotionCharges < 0 || value.accumulatedRenown < 0)
                return Fail(SaveV7ErrorCode.InvalidData, "checkpoint.resources", "Checkpoint charges and Renown cannot be negative.", out error);
            if (!ValidateFortuneBook(value.fortuneOfferBookSnapshot, null, null, null,
                "checkpoint.fortuneOfferBookSnapshot", out error)) return false;
            if (!ValidateIds(value.pactIds, "checkpoint.pactIds", true, out error)) return false;
            if (!ValidateIds(value.collectedItemIds, "checkpoint.collectedItemIds", true, out error)) return false;
            if (!ValidateIds(value.claimedRewardIds, "checkpoint.claimedRewardIds", true, out error)) return false;
            return ValidateEntryReference(value.encounterEntry, "checkpoint.encounterEntry", out error);
        }

        public static bool TryValidateCheckpointBinding(ProfileSaveEnvelopeV7 value,
            ExpeditionRoomCheckpointSaveDataV7 parsedCheckpoint, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (!TryValidateProfile(value, out error)) return false;
            if (value.activeExpedition == null)
            {
                if (parsedCheckpoint == null) return true;
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.roomEntryCheckpointJson",
                    "A profile without an active expedition cannot bind a checkpoint.", out error);
            }
            ExpeditionSaveDataV7 expedition = value.activeExpedition;
            bool currentCompleted = expedition.completedRoomIds.Contains(expedition.currentNodeId, StringComparer.Ordinal);
            if (parsedCheckpoint == null)
            {
                if (currentCompleted) return true;
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.roomEntryCheckpointJson",
                    "An unfinished current room requires its parsed checkpoint.", out error);
            }
            if (currentCompleted)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.roomEntryCheckpointJson",
                    "A completed current room cannot retain an entry checkpoint.", out error);
            if (!TryValidateCheckpoint(parsedCheckpoint, out error)) return false;
            if (expedition == null || expedition.encounterPlanPin == null)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin",
                    "Checkpoint binding requires an active pinned expedition.", out error);
            if (!string.Equals(parsedCheckpoint.roomNodeId, expedition.currentNodeId, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.roomNodeId",
                    "Checkpoint room must match the active expedition cursor.", out error);
            if (!string.Equals(parsedCheckpoint.encounterEntry.nodeId, parsedCheckpoint.roomNodeId,
                StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.encounterEntry.nodeId",
                    "Checkpoint entry must name its exact room node.", out error);
            EncounterPlanNodeBindingSaveDataV7 binding = FindBinding(expedition.encounterPlanPin,
                parsedCheckpoint.roomNodeId);
            if (binding == null || !string.Equals(parsedCheckpoint.encounterEntry.resolvedPlanSha256,
                binding.resolvedPlanSha256, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.encounterEntry.resolvedPlanSha256",
                    "Checkpoint entry must match the exact pinned plan for its room.", out error);
            FortuneOfferBookSnapshotV7 checkpointFortune = parsedCheckpoint.fortuneOfferBookSnapshot;
            FortuneOfferBookSnapshotV7 expeditionFortune = expedition.fortuneOfferBookSnapshot;
            if (checkpointFortune.seed != expedition.seed
                || checkpointFortune.generationVersion != expedition.generationVersion
                || !string.Equals(checkpointFortune.raceId, expedition.raceId, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.fortuneOfferBookSnapshot",
                    "Checkpoint Fortuna identity must match the active run seed, generation, and race.", out error);
            if (!SameOrdinalSet(parsedCheckpoint.pactIds, binding.contextPactIds))
                return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.pactIds",
                    "Checkpoint pacts must match the exact pinned binding context.", out error);
            return true;
        }

        private static bool ValidateHeader(int schemaVersion, int contentVersion, int generation,
            long sequence, string savedAtUtc, string checksum, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (schemaVersion != SchemaVersion || contentVersion != ContentVersion)
                return Fail(SaveV7ErrorCode.Incompatible, "header.version", "Only schema 1 and content version 7 are supported.", out error);
            if (generation < 1 || sequence < 1)
                return Fail(SaveV7ErrorCode.InvalidData, "header.generation", "Generation and sequence must be positive.", out error);
            if (!DateTimeOffset.TryParse(savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTimeOffset parsedDate) || parsedDate.Offset != TimeSpan.Zero)
                return Fail(SaveV7ErrorCode.InvalidData, "header.savedAtUtc", "Save timestamp must be a round-trip UTC date.", out error);
            if (!string.IsNullOrEmpty(checksum) && !IsLowerSha256(checksum))
                return Fail(SaveV7ErrorCode.Corrupted, "header.checksum", "Checksum must be empty for a preimage or 64 lowercase hexadecimal characters.", out error);
            return true;
        }

        private static bool ValidateProfileData(ProfileSaveDataV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (value == null)
                return Fail(SaveV7ErrorCode.InvalidData, "profile", "Profile payload is required.", out error);
            if (value.availableRenown < 0 || value.healthUpgradeLevel < 0 || value.healthUpgradeLevel > 2
                || value.energyUpgradeLevel < 0 || value.energyUpgradeLevel > 2
                || value.additionalPotionUpgradeLevel < 0 || value.additionalPotionUpgradeLevel > 1)
                return Fail(SaveV7ErrorCode.InvalidData, "profile.upgrades", "Profile Renown or permanent upgrades are outside their supported range.", out error);
            if (!ValidateIds(value.unlockedRaceIds, "profile.unlockedRaceIds", true, out error)) return false;
            if (!ValidateIds(value.unlockedStartingKitIds, "profile.unlockedStartingKitIds", true, out error)) return false;
            if (!ValidateIds(value.acceptedContractIds, "profile.acceptedContractIds", true, out error)) return false;
            if (!ValidateIds(value.claimedContractIds, "profile.claimedContractIds", true, out error)) return false;
            if (!ValidateIds(value.readStoryFragmentIds, "profile.readStoryFragmentIds", true, out error)) return false;
            if (!ValidateIds(value.processedSettlementRunIds, "profile.processedSettlementRunIds", true, out error)) return false;
            if (!ValidateObjectArray(value.contractCounters, "profile.contractCounters", out error)) return false;
            if (!ValidateObjectArray(value.processedContractProgressEvents, "profile.processedContractProgressEvents", out error)) return false;
            if (!ValidateObjectArray(value.progressionPurchaseTransactions, "profile.progressionPurchaseTransactions", out error)) return false;

            var accepted = new HashSet<string>(value.acceptedContractIds, StringComparer.Ordinal);
            var claimed = new HashSet<string>(value.claimedContractIds, StringComparer.Ordinal);
            if (claimed.Any(id => !accepted.Contains(id)))
                return Fail(SaveV7ErrorCode.InvalidData, "profile.claimedContractIds", "Claimed contracts must also be accepted.", out error);
            if (accepted.Except(claimed, StringComparer.Ordinal).Count() > ContractProgressionRules.MaximumActiveContracts)
                return Fail(SaveV7ErrorCode.InvalidData, "profile.acceptedContractIds", "A profile cannot have more than three active contracts.", out error);

            foreach (string runId in value.processedSettlementRunIds)
            {
                if (!RunId.TryParse(runId, out _))
                    return Fail(SaveV7ErrorCode.InvalidData, "profile.processedSettlementRunIds", "Processed settlement IDs must be valid RunIds.", out error);
            }
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContractCounterSaveDataV7 counter in value.contractCounters)
            {
                if (counter == null || !IsStableText(counter.contractId) || counter.progress < 0
                    || !contractIds.Add(counter.contractId))
                    return Fail(SaveV7ErrorCode.InvalidData, "profile.contractCounters", "Contract counters must have unique IDs and nonnegative progress.", out error);
            }
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContractProgressEventReceiptSaveDataV7 receipt in value.processedContractProgressEvents)
            {
                if (receipt == null || !IsStableEventId(receipt.eventId)
                    || !Enum.IsDefined(typeof(ContractProgressEvent), receipt.progressEvent)
                    || !eventIds.Add(receipt.eventId))
                    return Fail(SaveV7ErrorCode.InvalidData, "profile.processedContractProgressEvents", "Processed contract events must have unique stable IDs and known event kinds.", out error);
            }
            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PermanentUpgradePurchaseSaveDataV7 transaction in value.progressionPurchaseTransactions)
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out _)
                    || !Enum.IsDefined(typeof(PermanentUpgradeKind), transaction.kind) || transaction.cost <= 0
                    || !transactionIds.Add(transaction.transactionId))
                    return Fail(SaveV7ErrorCode.InvalidData, "profile.progressionPurchaseTransactions", "Purchase receipts must have unique valid transactions and positive costs.", out error);
            }
            return true;
        }

        private static bool ValidateExpedition(ExpeditionSaveDataV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (!RunId.TryParse(value.runId, out RunId runId)
                || !string.Equals(runId.ToString(), value.runId, StringComparison.Ordinal) || value.seed == 0
                || value.generationVersion < 1 || value.contentVersion != ContentVersion
                || !IsStableText(value.classId) || !IsStableText(value.raceId))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.identity", "Active expedition identity, seed, and versions are invalid.", out error);
            if (!ValidateIds(value.selectedSkillIds, "activeExpedition.selectedSkillIds", false, out error)) return false;
            if (value.selectedSkillIds.Length != 2)
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.selectedSkillIds", "An active expedition requires exactly two skills.", out error);
            if (!ValidateIds(value.startingEquipmentInstanceIds, "activeExpedition.startingEquipmentInstanceIds", false, out error)) return false;
            if (value.startingEquipmentInstanceIds.Length != 2 || value.startingEquipmentInstanceIds.Any(id => !ItemInstanceId.TryParse(id, out _)))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.startingEquipmentInstanceIds", "An active expedition requires two valid starting equipment instance IDs.", out error);
            if (!IsStableText(value.currentNodeId))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.currentNodeId", "Current route node is required.", out error);
            if (!ValidateIds(value.visitedNodeIds, "activeExpedition.visitedNodeIds", false, out error)) return false;
            if (!string.Equals(value.visitedNodeIds[value.visitedNodeIds.Length - 1], value.currentNodeId, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.currentNodeId", "Current node must match the final visited node.", out error);
            if (!ValidateIds(value.completedRoomIds, "activeExpedition.completedRoomIds", true, out error)) return false;
            if (!ValidateObjectArray(value.completedRoomResults, "activeExpedition.completedRoomResults", out error)) return false;
            if (string.IsNullOrWhiteSpace(value.inventorySnapshotJson))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.inventorySnapshotJson", "Inventory snapshot is required.", out error);
            if (value.accumulatedRenown < 0 || value.maximumHealth < 1 || value.currentHealth < 0
                || value.currentHealth > value.maximumHealth || value.maximumEnergy < 1
                || value.currentEnergy < 0 || value.currentEnergy > value.maximumEnergy || value.healthPotionCharges < 0)
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.resources", "Expedition resources are outside their supported range.", out error);
            if (value.hasFrozenPermanentUpgrades && value.frozenPermanentUpgrades == null)
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.frozenPermanentUpgrades", "Frozen upgrades are marked present but missing.", out error);
            if (value.frozenPermanentUpgrades != null && value.hasFrozenPermanentUpgrades
                && (value.frozenPermanentUpgrades.healthUpgradeLevel < 0 || value.frozenPermanentUpgrades.healthUpgradeLevel > 2
                    || value.frozenPermanentUpgrades.energyUpgradeLevel < 0 || value.frozenPermanentUpgrades.energyUpgradeLevel > 2
                    || value.frozenPermanentUpgrades.additionalPotionUpgradeLevel < 0 || value.frozenPermanentUpgrades.additionalPotionUpgradeLevel > 1))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.frozenPermanentUpgrades", "Frozen upgrades are outside their supported range.", out error);
            if (!value.hasFrozenPermanentUpgrades && value.frozenPermanentUpgrades != null
                && (value.frozenPermanentUpgrades.healthUpgradeLevel != 0 || value.frozenPermanentUpgrades.energyUpgradeLevel != 0
                    || value.frozenPermanentUpgrades.additionalPotionUpgradeLevel != 0))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.frozenPermanentUpgrades", "Unmarked frozen upgrades cannot contain values.", out error);
            if (!ValidateIds(value.pactIds, "activeExpedition.pactIds", true, out error)) return false;
            if (!ValidateIds(value.collectedItemIds, "activeExpedition.collectedItemIds", true, out error)) return false;
            if (!ValidateIds(value.claimedRewardIds, "activeExpedition.claimedRewardIds", true, out error)) return false;
            if (!ValidateFortuneBook(value.fortuneOfferBookSnapshot, value.seed, value.generationVersion,
                value.raceId, "activeExpedition.fortuneOfferBookSnapshot", out error)) return false;

            ExpeditionRoutePlan route;
            try
            {
                route = ExpeditionRoutePlan.ParseCanonical(value.routeSnapshotJson);
            }
            catch (Exception)
            {
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.routeSnapshotJson", "Route snapshot must be a valid canonical route.", out error);
            }
            if (route.Seed != value.seed || route.GenerationVersion != value.generationVersion)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.routeSnapshotJson", "Route seed and generation must match the expedition.", out error);
            string routeSha256;
            try
            {
                routeSha256 = ComputeSha256(StrictUtf8.GetBytes(route.CanonicalForm));
            }
            catch (EncoderFallbackException)
            {
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.routeSnapshotJson", "Route snapshot is not strict UTF-8 text.", out error);
            }
            try
            {
                var traversal = new ExpeditionRouteTraversal(route, value.visitedNodeIds);
                if (!string.Equals(traversal.CurrentNodeId, value.currentNodeId, StringComparison.Ordinal))
                    return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.visitedNodeIds", "Route history does not match its cursor.", out error);
            }
            catch (Exception)
            {
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.visitedNodeIds", "Visited nodes must be a valid canonical route traversal.", out error);
            }
            var routeNodeIds = new HashSet<string>(route.Nodes.Select(node => node.NodeId), StringComparer.Ordinal);
            var visitedNodeIds = new HashSet<string>(value.visitedNodeIds, StringComparer.Ordinal);
            foreach (string completedNodeId in value.completedRoomIds)
            {
                if (!routeNodeIds.Contains(completedNodeId) || !visitedNodeIds.Contains(completedNodeId))
                    return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.completedRoomIds", "Completed rooms must belong to the visited route.", out error);
            }
            if (!ValidateRoomResults(value, routeNodeIds, out error)) return false;
            bool currentCompleted = value.completedRoomIds.Contains(value.currentNodeId, StringComparer.Ordinal);
            if (currentCompleted && !string.IsNullOrWhiteSpace(value.roomEntryCheckpointJson))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.roomEntryCheckpointJson", "A completed current room cannot retain an entry checkpoint.", out error);
            if (!currentCompleted && string.IsNullOrWhiteSpace(value.roomEntryCheckpointJson))
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.roomEntryCheckpointJson", "An unfinished active room requires its entry checkpoint.", out error);
            if (!ValidatePin(value.encounterPlanPin, value, route, runId, routeSha256, out error)) return false;
            return true;
        }

        private static bool ValidateRoomResults(ExpeditionSaveDataV7 expedition,
            HashSet<string> routeNodeIds, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            var resultRooms = new HashSet<string>(StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpeditionRoomCompletionSaveDataV7 result in expedition.completedRoomResults)
            {
                if (result == null || !IsStableText(result.roomNodeId) || !IsStableText(result.resultId)
                    || string.IsNullOrWhiteSpace(result.resultJson) || !routeNodeIds.Contains(result.roomNodeId)
                    || !expedition.completedRoomIds.Contains(result.roomNodeId, StringComparer.Ordinal)
                    || !resultRooms.Add(result.roomNodeId) || !resultIds.Add(result.resultId))
                    return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.completedRoomResults",
                        "Room completion results must be complete, unique, and refer to completed route nodes.", out error);
            }
            if (resultRooms.Count != expedition.completedRoomIds.Length)
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition.completedRoomResults",
                    "Every completed room must have exactly one result.", out error);
            return true;
        }

        private static bool ValidatePin(EncounterPlanPinSaveDataV7 pin, ExpeditionSaveDataV7 expedition,
            ExpeditionRoutePlan route, RunId runId, string routeSha256, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (pin == null)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin",
                    "Every active V7 expedition requires an encounter plan pin.", out error);
            if (pin.schemaVersion != EncounterPlanPinV1.SupportedSchemaVersion
                || !RunId.TryParse(pin.runId, out RunId pinRunId)
                || !string.Equals(pinRunId.ToString(), pin.runId, StringComparison.Ordinal) || pinRunId != runId
                || !IsStableText(pin.catalogId) || pin.catalogRevision <= 0
                || !IsLowerSha256(pin.catalogSha256) || !IsLowerSha256(pin.routeSha256)
                || !string.Equals(pin.routeSha256, routeSha256, StringComparison.Ordinal)
                || !string.Equals(pin.identityCodec, IdentityCodec, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin",
                    "Pin root identity, route, catalog digest, or identity codec is invalid.", out error);
            if (pin.bindings == null || pin.bindings.Length == 0)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.bindings",
                    "At least one typed node binding is required.", out error);
            string previousNodeId = null;
            EncounterPlanNodeBindingSaveDataV7 currentBinding = null;
            EncounterPlanNodeBindingSaveDataV7 entryBinding = null;
            foreach (EncounterPlanNodeBindingSaveDataV7 binding in pin.bindings)
            {
                if (binding == null || !IsStableText(binding.nodeId) || binding.generationVersion != route.GenerationVersion
                    || !IsStableText(binding.templateId) || !RoomInstanceId.TryParse(binding.roomInstanceId, out RoomInstanceId roomId)
                    || !string.Equals(roomId.ToString(), binding.roomInstanceId, StringComparison.Ordinal)
                    || !IsLowerSha256(binding.resolvedPlanSha256))
                    return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.bindings",
                        "Binding identity, generation, room ID, or plan digest is invalid.", out error);
                if (previousNodeId != null && string.CompareOrdinal(previousNodeId, binding.nodeId) >= 0)
                    return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.bindings",
                        "Bindings must be unique and in ordinal node order.", out error);
                previousNodeId = binding.nodeId;
                ExpeditionRouteNode routeNode = route.Nodes.FirstOrDefault(node =>
                    string.Equals(node.NodeId, binding.nodeId, StringComparison.Ordinal));
                if (routeNode == null || !string.Equals(routeNode.TemplateId, binding.templateId, StringComparison.Ordinal)
                    || roomId != ExpeditionRoomIdentity.ForNode(runId, binding.nodeId))
                    return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.bindings",
                        "Binding must match the canonical route node, template, and ForNode room identity.", out error);
                if (!ValidateBinding(binding, out error)) return false;
                if (string.Equals(binding.nodeId, expedition.currentNodeId, StringComparison.Ordinal)) currentBinding = binding;
                if (pin.entry != null && string.Equals(binding.nodeId, pin.entry.nodeId, StringComparison.Ordinal)) entryBinding = binding;
            }
            if (currentBinding == null)
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.bindings",
                    "The active cursor must have an exact run/node binding.", out error);
            if (!ValidateEntryReference(pin.entry, "activeExpedition.encounterPlanPin.entry", out error)) return false;
            if (!string.Equals(pin.entry.nodeId, expedition.currentNodeId, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.entry.nodeId",
                    "Pin entry must name the active cursor node.", out error);
            if (entryBinding == null || !string.Equals(entryBinding.resolvedPlanSha256,
                pin.entry.resolvedPlanSha256, StringComparison.Ordinal))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin.entry",
                    "Pin entry must match an exact binding and its plan digest.", out error);
            return true;
        }

        private static bool ValidateBinding(EncounterPlanNodeBindingSaveDataV7 value, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (!Enum.IsDefined(typeof(EncounterCatalogBindingMode), value.mode))
                return Fail(SaveV7ErrorCode.InvalidData, "encounterPlanPin.bindings.mode", "Binding mode is not supported.", out error);
            if (value.contextPactIds == null || value.contextPactIds.Length > 2)
                return Fail(SaveV7ErrorCode.InvalidData, "encounterPlanPin.bindings.contextPactIds", "Pact context must be a non-null array of at most two IDs.", out error);
            string previous = null;
            foreach (string pactId in value.contextPactIds)
            {
                if (!IsStableText(pactId) || !CanonicalPactIds.Contains(pactId)
                    || (previous != null && string.CompareOrdinal(previous, pactId) >= 0))
                    return Fail(SaveV7ErrorCode.InvalidData, "encounterPlanPin.bindings.contextPactIds",
                        "Pact IDs must be known, unique, and ordinally sorted.", out error);
                previous = pactId;
            }
            if (value.calendarOrdinal != 0)
                return Fail(SaveV7ErrorCode.MissingResolutionAuthority, "encounterPlanPin.bindings.calendarOrdinal",
                    "No authoritative Vínculo calendar is available in this preproduction cut.", out error);
            if (value.mode == EncounterCatalogBindingMode.None)
            {
                if (value.encounterId != null || value.definitionRevision != 0
                    || value.definitionSha256 != null || value.variantId != null)
                    return Fail(SaveV7ErrorCode.InvalidData, "encounterPlanPin.bindings",
                        "A None binding must have null or zero encounter payload values.", out error);
                return true;
            }
            if (!IsStableText(value.encounterId) || value.definitionRevision <= 0
                || !IsLowerSha256(value.definitionSha256) || !IsStableText(value.variantId))
                return Fail(SaveV7ErrorCode.InvalidData, "encounterPlanPin.bindings",
                    "Encounter binding payload is incomplete or malformed.", out error);
            return true;
        }

        private static bool ValidateEntryReference(EncounterPlanEntryReferenceSaveDataV7 value,
            string path, out SaveV7Error error)
        {
            if (value == null || !IsStableText(value.nodeId) || !IsLowerSha256(value.resolvedPlanSha256))
                return Fail(SaveV7ErrorCode.InvalidData, path,
                    "A typed entry reference requires a stable node ID and lowercase SHA-256.", out error);
            error = default(SaveV7Error);
            return true;
        }

        private static bool ValidateFortuneBook(FortuneOfferBookSnapshotV7 value, uint? seed,
            int? generationVersion, string raceId, string path, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (value == null)
                return Fail(SaveV7ErrorCode.InvalidData, path, "Fortuna offer book snapshot is required.", out error);
            if (value.snapshotVersion < 1 || value.generationVersion < 1 || value.charges < 0
                || !IsStableText(value.raceId) || (seed.HasValue && value.seed != seed.Value)
                || (generationVersion.HasValue && value.generationVersion != generationVersion.Value)
                || (!string.IsNullOrEmpty(raceId) && !string.Equals(value.raceId, raceId, StringComparison.Ordinal)))
                return Fail(SaveV7ErrorCode.InvalidData, path, "Fortuna offer book identity or charges are invalid.", out error);
            if (!ValidateIds(value.processedGuardianIds, path + ".processedGuardianIds", true, out error)) return false;
            if (!ValidateObjectArray(value.offers, path + ".offers", out error)) return false;
            if (!ValidateObjectArray(value.rerollReceipts, path + ".rerollReceipts", out error)) return false;
            if (!ValidateObjectArray(value.fortuneBrewReceipts, path + ".fortuneBrewReceipts", out error)) return false;
            foreach (FortuneOfferSnapshotV7 offer in value.offers)
            {
                if (offer == null || !IsStableText(offer.offerId) || !IsStableText(offer.contextId)
                    || offer.eligibleOptionIds == null || offer.history == null
                    || !ValidateIds(offer.eligibleOptionIds, path + ".offers.eligibleOptionIds", true, out error)
                    || !ValidateObjectArray(offer.history, path + ".offers.history", out error))
                {
                    if (error.Code != SaveV7ErrorCode.None) return false;
                    return Fail(SaveV7ErrorCode.InvalidData, path + ".offers", "Fortuna offers must have stable IDs and non-null arrays.", out error);
                }
                foreach (FortuneOfferHistorySnapshotV7 history in offer.history)
                {
                    if (history == null || !ValidateIds(history.optionIds, path + ".offers.history.optionIds", true, out error))
                    {
                        if (error.Code != SaveV7ErrorCode.None) return false;
                        return Fail(SaveV7ErrorCode.InvalidData, path + ".offers.history", "Fortuna history entries and option arrays are required.", out error);
                    }
                }
            }
            foreach (FortuneRerollReceiptSnapshotV7 receipt in value.rerollReceipts)
            {
                if (receipt == null || !TransactionId.TryParse(receipt.transactionId, out _)
                    || (receipt.hasOffer && (!IsStableText(receipt.offerId) || !IsStableText(receipt.contextId)))
                    || receipt.optionIds == null || receipt.rerollCount < 0 || receipt.remainingCharges < 0
                    || !ValidateIds(receipt.optionIds, path + ".rerollReceipts.optionIds", true, out error))
                {
                    if (error.Code != SaveV7ErrorCode.None) return false;
                    return Fail(SaveV7ErrorCode.InvalidData, path + ".rerollReceipts", "Fortuna reroll receipt is malformed.", out error);
                }
            }
            foreach (FortuneBrewReceiptSnapshotV7 receipt in value.fortuneBrewReceipts)
            {
                if (receipt == null || !TransactionId.TryParse(receipt.transactionId, out _)
                    || !IsStableText(receipt.itemInstanceId) || receipt.currentCharges < 0)
                    return Fail(SaveV7ErrorCode.InvalidData, path + ".fortuneBrewReceipts", "Fortuna brew receipt is malformed.", out error);
            }
            try
            {
                FortuneOfferBook.Restore(ToCoreFortuneBookSnapshot(value));
            }
            catch (FormatException)
            {
                return Fail(SaveV7ErrorCode.InvalidData, path, "Fortuna offer book snapshot is rejected by Core.", out error);
            }
            catch (ArgumentException)
            {
                return Fail(SaveV7ErrorCode.InvalidData, path, "Fortuna offer book snapshot is rejected by Core.", out error);
            }
            return true;
        }

        private static FortuneOfferBookSnapshot ToCoreFortuneBookSnapshot(FortuneOfferBookSnapshotV7 value)
        {
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = value.snapshotVersion,
                generationVersion = value.generationVersion,
                seed = value.seed,
                raceId = value.raceId,
                charges = value.charges,
                offers = ToCoreFortuneOffers(value.offers),
                processedGuardianIds = CopyIds(value.processedGuardianIds),
                rerollReceipts = ToCoreFortuneRerollReceipts(value.rerollReceipts),
                fortuneBrewReceipts = ToCoreFortuneBrewReceipts(value.fortuneBrewReceipts)
            };
        }

        private static FortuneOfferSnapshot[] ToCoreFortuneOffers(FortuneOfferSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneOfferSnapshot[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                FortuneOfferSnapshotV7 value = values[index];
                copy[index] = value == null ? null : new FortuneOfferSnapshot
                {
                    offerId = value.offerId,
                    contextId = value.contextId,
                    eligibleOptionIds = CopyIds(value.eligibleOptionIds),
                    history = ToCoreFortuneHistory(value.history),
                    selectedOptionId = value.selectedOptionId,
                    claimed = value.claimed
                };
            }
            return copy;
        }

        private static FortuneOfferHistorySnapshot[] ToCoreFortuneHistory(FortuneOfferHistorySnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneOfferHistorySnapshot[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                FortuneOfferHistorySnapshotV7 value = values[index];
                copy[index] = value == null ? null : new FortuneOfferHistorySnapshot
                {
                    optionIds = CopyIds(value.optionIds)
                };
            }
            return copy;
        }

        private static FortuneRerollReceiptSnapshot[] ToCoreFortuneRerollReceipts(
            FortuneRerollReceiptSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneRerollReceiptSnapshot[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                FortuneRerollReceiptSnapshotV7 value = values[index];
                copy[index] = value == null ? null : new FortuneRerollReceiptSnapshot
                {
                    transactionId = value.transactionId,
                    offerId = value.offerId,
                    status = value.status,
                    contextId = value.contextId,
                    optionIds = CopyIds(value.optionIds),
                    selectedOptionId = value.selectedOptionId,
                    rerollCount = value.rerollCount,
                    claimed = value.claimed,
                    remainingCharges = value.remainingCharges,
                    hasOffer = value.hasOffer
                };
            }
            return copy;
        }

        private static FortuneBrewReceiptSnapshot[] ToCoreFortuneBrewReceipts(
            FortuneBrewReceiptSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneBrewReceiptSnapshot[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                FortuneBrewReceiptSnapshotV7 value = values[index];
                copy[index] = value == null ? null : new FortuneBrewReceiptSnapshot
                {
                    transactionId = value.transactionId,
                    itemInstanceId = value.itemInstanceId,
                    status = value.status,
                    currentCharges = value.currentCharges
                };
            }
            return copy;
        }

        private static string[] CopyIds(string[] values)
        {
            if (values == null) return null;
            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static bool SameOrdinalSet(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            string[] leftCopy = CopyIds(left);
            string[] rightCopy = CopyIds(right);
            Array.Sort(leftCopy, StringComparer.Ordinal);
            Array.Sort(rightCopy, StringComparer.Ordinal);
            return leftCopy.SequenceEqual(rightCopy, StringComparer.Ordinal);
        }

        private static bool ValidateIds(string[] values, string path, bool allowEmpty, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (values == null)
                return Fail(SaveV7ErrorCode.InvalidData, path, "Array cannot be null.", out error);
            if (!allowEmpty && values.Length == 0)
                return Fail(SaveV7ErrorCode.InvalidData, path, "Array cannot be empty.", out error);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (!IsStableText(value) || !ids.Add(value))
                    return Fail(SaveV7ErrorCode.InvalidData, path, "Values must be stable, non-empty, and unique.", out error);
            }
            return true;
        }

        private static bool ValidateObjectArray<T>(T[] values, string path, out SaveV7Error error)
            where T : class
        {
            error = default(SaveV7Error);
            if (values == null)
                return Fail(SaveV7ErrorCode.InvalidData, path, "Array cannot be null.", out error);
            if (values.Any(value => value == null))
                return Fail(SaveV7ErrorCode.InvalidData, path, "Array cannot contain null entries.", out error);
            return true;
        }

        private static bool IsStableEventId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 160) return false;
            foreach (char character in value)
            {
                if (!(character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9' || character == '.' || character == '_'
                    || character == ':' || character == '/' || character == '-')) return false;
            }
            return true;
        }

        private static bool IsStableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int index = 0; index < value.Length; index++)
                if (char.IsWhiteSpace(value[index])) return false;
            try
            {
                StrictUtf8.GetByteCount(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        private static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char character in value)
                if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f')) return false;
            return true;
        }

        private static bool IsVolume(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var characters = new char[digest.Length * 2];
                const string digits = "0123456789abcdef";
                for (int index = 0; index < digest.Length; index++)
                {
                    characters[index * 2] = digits[digest[index] >> 4];
                    characters[index * 2 + 1] = digits[digest[index] & 0x0f];
                }
                return new string(characters);
            }
        }

        private static EncounterPlanNodeBindingSaveDataV7 FindBinding(EncounterPlanPinSaveDataV7 pin, string nodeId)
        {
            for (int index = 0; index < pin.bindings.Length; index++)
            {
                EncounterPlanNodeBindingSaveDataV7 binding = pin.bindings[index];
                if (binding != null && string.Equals(binding.nodeId, nodeId, StringComparison.Ordinal)) return binding;
            }
            return null;
        }

        private static bool Fail(SaveV7ErrorCode code, string path, string message, out SaveV7Error error)
        {
            error = new SaveV7Error(code, path, message);
            return false;
        }
    }
}
