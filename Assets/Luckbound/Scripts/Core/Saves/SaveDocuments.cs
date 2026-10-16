using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;

namespace Luckbound.Core.Saves
{
    public static class SaveFormat
    {
        public const int SchemaVersion = 1;
        public const int ContentVersion = 3;
        public const int Generation = 1;
    }

    [Serializable]
    public sealed class ProfileSaveData
    {
        public int availableRenown;
        public int healthUpgradeLevel;
        public int energyUpgradeLevel;
        public int additionalPotionUpgradeLevel;
        public string[] unlockedRaceIds = Array.Empty<string>();
        public string[] unlockedStartingKitIds = Array.Empty<string>();
        public string[] acceptedContractIds = Array.Empty<string>();
        public string[] claimedContractIds = Array.Empty<string>();
        public string[] readStoryFragmentIds = Array.Empty<string>();
        public ContractCounterSaveData[] contractCounters = Array.Empty<ContractCounterSaveData>();
        public string[] processedSettlementRunIds = Array.Empty<string>();
        public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();

        public static ProfileSaveData CreateNewProfile()
        {
            return new ProfileSaveData
            {
                unlockedRaceIds = new[] { "race.human" },
                unlockedStartingKitIds = new[]
                {
                    "weapon.bellator.rusted_sword",
                    "weapon.venator.short_bow",
                    "weapon.magus.ember_staff"
                }
            };
        }

        public ProfileSaveData Clone()
        {
            return new ProfileSaveData
            {
                availableRenown = availableRenown,
                healthUpgradeLevel = healthUpgradeLevel,
                energyUpgradeLevel = energyUpgradeLevel,
                additionalPotionUpgradeLevel = additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(unlockedRaceIds),
                unlockedStartingKitIds = Copy(unlockedStartingKitIds),
                acceptedContractIds = Copy(acceptedContractIds),
                claimedContractIds = Copy(claimedContractIds),
                readStoryFragmentIds = Copy(readStoryFragmentIds),
                contractCounters = contractCounters == null ? null : contractCounters
                    .Select(counter => counter == null ? null : counter.Clone()).ToArray(),
                processedSettlementRunIds = Copy(processedSettlementRunIds),
                progressionPurchaseTransactions = progressionPurchaseTransactions == null ? null : progressionPurchaseTransactions
                    .Select(transaction => transaction == null ? null : transaction.Clone()).ToArray()
            };
        }

        public void NormalizeForSave()
        {
            unlockedRaceIds = Sort(unlockedRaceIds);
            unlockedStartingKitIds = Sort(unlockedStartingKitIds);
            acceptedContractIds = Sort(acceptedContractIds);
            claimedContractIds = Sort(claimedContractIds);
            readStoryFragmentIds = Sort(readStoryFragmentIds);
            processedSettlementRunIds = Sort(processedSettlementRunIds);
            progressionPurchaseTransactions = progressionPurchaseTransactions == null
                ? Array.Empty<PermanentUpgradePurchaseSaveData>()
                : progressionPurchaseTransactions
                    .OrderBy(transaction => transaction == null ? string.Empty : transaction.transactionId, StringComparer.Ordinal)
                    .Select(transaction => transaction == null ? null : transaction.Clone()).ToArray();
            if (contractCounters != null)
            {
                contractCounters = contractCounters.OrderBy(counter => counter == null ? string.Empty : counter.contractId,
                    StringComparer.Ordinal).Select(counter => counter == null ? null : counter.Clone()).ToArray();
            }
        }

        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();
        private static string[] Sort(string[] values) => values == null ? null : values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class ContractCounterSaveData
    {
        public string contractId;
        public int progress;

        public ContractCounterSaveData Clone() => new ContractCounterSaveData { contractId = contractId, progress = progress };
    }

    [Serializable]
    public sealed class PermanentUpgradePurchaseSaveData
    {
        public string transactionId;
        public int kind;
        public int cost;

        public PermanentUpgradePurchaseSaveData Clone() => new PermanentUpgradePurchaseSaveData
        {
            transactionId = transactionId,
            kind = kind,
            cost = cost
        };
    }

    [Serializable]
    public sealed class ExpeditionSaveData
    {
        public string runId;
        public uint seed;
        public int generationVersion;
        public int contentVersion;
        public string classId;
        public string raceId;
        public string[] selectedSkillIds = Array.Empty<string>();
        public string[] startingEquipmentInstanceIds = Array.Empty<string>();
        public string routeSnapshotJson;
        public string currentNodeId;
        public string[] visitedNodeIds = Array.Empty<string>();
        public string[] completedRoomIds = Array.Empty<string>();
        public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
        public string roomEntryCheckpointJson;
        public string inventorySnapshotJson;
        public int maximumHealth = 100;
        public int currentHealth = 100;
        public int maximumEnergy = 50;
        public int currentEnergy = 50;
        public int healthPotionCharges = 2;
        public int accumulatedRenown;
        public string[] pactIds = Array.Empty<string>();
        public FortuneOfferBookSnapshot fortuneOfferBookSnapshot;
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();

        // Kept as an in-memory bridge for existing callers; the canonical saved value lives in the book snapshot.
        public int fortunaCharges
        {
            get => fortuneOfferBookSnapshot == null ? 0 : fortuneOfferBookSnapshot.charges;
            set
            {
                if (fortuneOfferBookSnapshot == null)
                    throw new InvalidOperationException("Fortuna charges require a canonical offer-book snapshot.");
                fortuneOfferBookSnapshot.charges = value;
            }
        }

        public ExpeditionSaveData Clone() => new ExpeditionSaveData
        {
            runId = runId,
            seed = seed,
            generationVersion = generationVersion,
            contentVersion = contentVersion,
            classId = classId,
            raceId = raceId,
            selectedSkillIds = Copy(selectedSkillIds),
            startingEquipmentInstanceIds = Copy(startingEquipmentInstanceIds),
            routeSnapshotJson = routeSnapshotJson,
            currentNodeId = currentNodeId,
            visitedNodeIds = Copy(visitedNodeIds),
            completedRoomIds = Copy(completedRoomIds),
            completedRoomResults = completedRoomResults == null ? null : completedRoomResults
                .Select(result => result == null ? null : result.Clone()).ToArray(),
            roomEntryCheckpointJson = roomEntryCheckpointJson,
            inventorySnapshotJson = inventorySnapshotJson,
            maximumHealth = maximumHealth,
            currentHealth = currentHealth,
            maximumEnergy = maximumEnergy,
            currentEnergy = currentEnergy,
            healthPotionCharges = healthPotionCharges,
            accumulatedRenown = accumulatedRenown,
            pactIds = Copy(pactIds),
            fortuneOfferBookSnapshot = Clone(fortuneOfferBookSnapshot),
            collectedItemIds = Copy(collectedItemIds),
            claimedRewardIds = Copy(claimedRewardIds)
        };

        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();

        private static FortuneOfferBookSnapshot Clone(FortuneOfferBookSnapshot source)
        {
            if (source == null) return null;
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = source.rerollReceipts == null ? null : source.rerollReceipts
                    .Select(receipt => receipt == null ? null : new FortuneRerollReceiptSnapshot
                    {
                        transactionId = receipt.transactionId,
                        offerId = receipt.offerId,
                        status = receipt.status,
                        contextId = receipt.contextId,
                        optionIds = Copy(receipt.optionIds),
                        selectedOptionId = receipt.selectedOptionId,
                        rerollCount = receipt.rerollCount,
                        claimed = receipt.claimed,
                        remainingCharges = receipt.remainingCharges,
                        hasOffer = receipt.hasOffer
                    }).ToArray(),
                offers = source.offers == null ? null : source.offers.Select(offer => offer == null ? null : new FortuneOfferSnapshot
                {
                    offerId = offer.offerId,
                    contextId = offer.contextId,
                    eligibleOptionIds = Copy(offer.eligibleOptionIds),
                    selectedOptionId = offer.selectedOptionId,
                    claimed = offer.claimed,
                    history = offer.history == null ? null : offer.history.Select(entry => entry == null ? null : new FortuneOfferHistorySnapshot
                    {
                        optionIds = Copy(entry.optionIds)
                    }).ToArray()
                }).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class ExpeditionRoomCheckpointSaveData
    {
        public string roomNodeId;
        public string inventorySnapshotJson;
        public int maximumHealth;
        public int currentHealth;
        public int maximumEnergy;
        public int currentEnergy;
        public int healthPotionCharges;
        public int accumulatedRenown;
        public FortuneOfferBookSnapshot fortuneOfferBookSnapshot;
        public string[] pactIds = Array.Empty<string>();
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ExpeditionRoomCompletionSaveData
    {
        public string roomNodeId;
        public string resultId;
        public string resultJson;

        public ExpeditionRoomCompletionSaveData Clone() => new ExpeditionRoomCompletionSaveData
        {
            roomNodeId = roomNodeId,
            resultId = resultId,
            resultJson = resultJson
        };
    }

    [Serializable]
    public sealed class ProfileSaveEnvelope
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public ProfileSaveData profile;
        public bool hasActiveExpedition;
        public ExpeditionSaveData activeExpedition;
    }

    [Serializable]
    public sealed class GameSettingsSaveData
    {
        public string languageId = "es";
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float effectsVolume = 1f;

        public GameSettingsSaveData Clone() => new GameSettingsSaveData
        {
            languageId = languageId,
            masterVolume = masterVolume,
            musicVolume = musicVolume,
            effectsVolume = effectsVolume
        };
    }

    [Serializable]
    public sealed class SettingsSaveEnvelope
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public GameSettingsSaveData settings;
    }

    public static class SaveDataValidation
    {
        public static IReadOnlyList<string> Validate(ProfileSaveEnvelope envelope)
        {
            var errors = new List<string>();
            if (envelope == null) return new[] { "Profile save envelope is missing." };
            ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                envelope.sequence, envelope.savedAtUtc, envelope.checksum, errors);
            if (envelope.hasActiveExpedition != (envelope.activeExpedition != null))
                errors.Add("Active expedition marker must match the saved expedition payload.");
            ValidateProfile(envelope.profile, errors);
            ValidateExpedition(envelope.activeExpedition, errors);
            return errors.AsReadOnly();
        }

        public static IReadOnlyList<string> Validate(SettingsSaveEnvelope envelope)
        {
            var errors = new List<string>();
            if (envelope == null) return new[] { "Settings save envelope is missing." };
            ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                envelope.sequence, envelope.savedAtUtc, envelope.checksum, errors);
            ValidateSettings(envelope.settings, errors);
            return errors.AsReadOnly();
        }

        public static IReadOnlyList<string> Validate(ProfileSaveData profile)
        {
            var errors = new List<string>();
            ValidateProfile(profile, errors);
            return errors.AsReadOnly();
        }

        public static IReadOnlyList<string> Validate(ExpeditionSaveData expedition)
        {
            var errors = new List<string>();
            ValidateExpedition(expedition, errors);
            return errors.AsReadOnly();
        }

        public static IReadOnlyList<string> Validate(GameSettingsSaveData settings)
        {
            var errors = new List<string>();
            ValidateSettings(settings, errors);
            return errors.AsReadOnly();
        }

        private static void ValidateCommon(int schema, int content, int generation, long sequence,
            string savedAtUtc, string checksum, List<string> errors)
        {
            if (schema != SaveFormat.SchemaVersion) errors.Add("Unsupported save schema version.");
            if (content != SaveFormat.ContentVersion) errors.Add("Unsupported save content version.");
            if (generation < 1) errors.Add("Save generation must be positive.");
            if (sequence < 1) errors.Add("Save sequence must be positive.");
            if (!DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                errors.Add("Save timestamp must be a round-trip UTC date.");
            if (!IsSha256(checksum)) errors.Add("Save checksum must be 64 lowercase hexadecimal characters.");
        }

        private static void ValidateProfile(ProfileSaveData profile, List<string> errors)
        {
            if (profile == null) { errors.Add("Profile data is missing."); return; }
            if (profile.availableRenown < 0) errors.Add("Available Renown cannot be negative.");
            if (profile.healthUpgradeLevel < 0 || profile.healthUpgradeLevel > 2) errors.Add("Health upgrade level must be 0–2.");
            if (profile.energyUpgradeLevel < 0 || profile.energyUpgradeLevel > 2) errors.Add("Energy upgrade level must be 0–2.");
            if (profile.additionalPotionUpgradeLevel < 0 || profile.additionalPotionUpgradeLevel > 1)
                errors.Add("Additional potion upgrade level must be 0–1.");
            ValidateIds(profile.unlockedRaceIds, "unlocked race IDs", errors);
            ValidateIds(profile.unlockedStartingKitIds, "unlocked starting kit IDs", errors);
            ValidateIds(profile.acceptedContractIds, "accepted contract IDs", errors);
            ValidateIds(profile.claimedContractIds, "claimed contract IDs", errors);
            ValidateIds(profile.readStoryFragmentIds, "read story fragment IDs", errors);
            ValidateIds(profile.processedSettlementRunIds, "processed settlement RunIds", errors);
            if (profile.processedSettlementRunIds != null)
            {
                foreach (string value in profile.processedSettlementRunIds)
                {
                    if (!RunId.TryParse(value, out _)) errors.Add("Invalid processed settlement RunId: " + value + ".");
                }
            }
            if (profile.progressionPurchaseTransactions == null)
            {
                errors.Add("Progression purchase transactions cannot be null.");
            }
            else
            {
                var transactionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (PermanentUpgradePurchaseSaveData transaction in profile.progressionPurchaseTransactions)
                {
                    if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out _))
                    {
                        errors.Add("Progression purchase transactions require a valid transaction ID.");
                        continue;
                    }
                    if (!transactionIds.Add(transaction.transactionId))
                        errors.Add("Progression purchase transaction IDs must be unique.");
                    if (!Enum.IsDefined(typeof(PermanentUpgradeKind), transaction.kind))
                        errors.Add("Progression purchase transaction has an unknown upgrade kind.");
                    if (transaction.cost <= 0)
                        errors.Add("Progression purchase transaction cost must be positive.");
                }
            }
            if (profile.contractCounters == null) errors.Add("Contract counters cannot be null.");
            else
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (ContractCounterSaveData counter in profile.contractCounters)
                {
                    if (counter == null || string.IsNullOrWhiteSpace(counter.contractId))
                    {
                        errors.Add("Contract counters require a contract ID.");
                        continue;
                    }
                    if (!ids.Add(counter.contractId)) errors.Add("Contract counters must be unique by contract ID.");
                    if (counter.progress < 0) errors.Add("Contract progress cannot be negative.");
                }
            }
        }

        private static void ValidateExpedition(ExpeditionSaveData expedition, List<string> errors)
        {
            if (expedition == null) return;
            if (!RunId.TryParse(expedition.runId, out _)) errors.Add("Active expedition requires a valid RunId.");
            if (expedition.seed == 0) errors.Add("Active expedition seed cannot be zero.");
            if (expedition.generationVersion < 1 || expedition.contentVersion < 1)
                errors.Add("Active expedition versions must be positive.");
            if (string.IsNullOrWhiteSpace(expedition.classId) || string.IsNullOrWhiteSpace(expedition.raceId))
                errors.Add("Active expedition requires class and race IDs.");
            ValidateIds(expedition.selectedSkillIds, "active expedition skill IDs", errors, allowEmpty: false);
            if (expedition.selectedSkillIds != null && expedition.selectedSkillIds.Length != 2)
                errors.Add("An active expedition must contain exactly two selected skills.");
            ValidateIds(expedition.startingEquipmentInstanceIds, "starting equipment instance IDs", errors, allowEmpty: false);
            if (expedition.startingEquipmentInstanceIds != null)
            {
                if (expedition.startingEquipmentInstanceIds.Length != 2)
                    errors.Add("Active expedition requires the two starting equipment instance IDs.");
                foreach (string value in expedition.startingEquipmentInstanceIds)
                    if (!ItemInstanceId.TryParse(value, out _)) errors.Add("Invalid starting equipment instance ID: " + value + ".");
            }
            ExpeditionRoutePlan route = null;
            if (string.IsNullOrWhiteSpace(expedition.routeSnapshotJson)) errors.Add("Active expedition route snapshot is missing.");
            else
            {
                try { route = ExpeditionRoutePlan.ParseCanonical(expedition.routeSnapshotJson); }
                catch (Exception exception) { errors.Add("Active expedition route snapshot is invalid: " + exception.Message); }
            }
            if (string.IsNullOrWhiteSpace(expedition.currentNodeId)) errors.Add("Active expedition current node is missing.");
            ValidateIds(expedition.visitedNodeIds, "visited route node IDs", errors, allowEmpty: false);
            if (expedition.visitedNodeIds != null && expedition.visitedNodeIds.Length > 0 &&
                !string.Equals(expedition.visitedNodeIds[expedition.visitedNodeIds.Length - 1], expedition.currentNodeId, StringComparison.Ordinal))
                errors.Add("Current route node must match the final visited node.");
            if (route != null)
            {
                if (route.Seed != expedition.seed || route.GenerationVersion != expedition.generationVersion)
                    errors.Add("Active expedition route header does not match its saved seed and generation version.");
                try
                {
                    var traversal = new ExpeditionRouteTraversal(route, expedition.visitedNodeIds);
                    if (!string.Equals(traversal.CurrentNodeId, expedition.currentNodeId, StringComparison.Ordinal))
                        errors.Add("Active expedition cursor does not match route history.");
                }
                catch (Exception exception) { errors.Add("Active expedition route history is invalid: " + exception.Message); }
                var routeNodeIds = new HashSet<string>(route.Nodes.Select(node => node.NodeId), StringComparer.Ordinal);
                if (expedition.completedRoomIds != null && expedition.completedRoomIds.Any(id => !routeNodeIds.Contains(id)))
                    errors.Add("Completed room history references a node outside the saved route.");
                if (expedition.completedRoomIds != null && expedition.visitedNodeIds != null &&
                    expedition.completedRoomIds.Any(id => !expedition.visitedNodeIds.Contains(id)))
                    errors.Add("Completed room history cannot include a room that was never visited.");
            }
            ValidateIds(expedition.completedRoomIds, "completed room IDs", errors);
            ValidateRoomResults(expedition, errors);
            if (string.IsNullOrWhiteSpace(expedition.inventorySnapshotJson))
                errors.Add("Active expedition inventory snapshot is missing.");
            if (expedition.accumulatedRenown < 0) errors.Add("Active expedition Renown cannot be negative.");
            ValidateFortuneBook(expedition.fortuneOfferBookSnapshot, expedition.seed,
                expedition.generationVersion, expedition.raceId, "Active expedition", errors);
            if (expedition.maximumHealth < 1 || expedition.currentHealth < 0 || expedition.currentHealth > expedition.maximumHealth)
                errors.Add("Active expedition health must be within its positive maximum.");
            if (expedition.maximumEnergy < 0 || expedition.currentEnergy < 0 || expedition.currentEnergy > expedition.maximumEnergy)
                errors.Add("Active expedition energy must be within its maximum.");
            if (expedition.healthPotionCharges < 0) errors.Add("Active expedition potion charges cannot be negative.");
            ValidateIds(expedition.pactIds, "active expedition pact IDs", errors);
            ValidateIds(expedition.collectedItemIds, "collected item IDs", errors);
            ValidateIds(expedition.claimedRewardIds, "claimed reward IDs", errors);
        }

        private static void ValidateFortuneBook(FortuneOfferBookSnapshot snapshot, uint seed, int generationVersion,
            string raceId, string label, List<string> errors)
        {
            if (snapshot == null)
            {
                errors.Add(label + " Fortuna offer book snapshot is missing.");
                return;
            }
            try
            {
                FortuneOfferBook.Restore(snapshot);
                if (snapshot.seed != seed || snapshot.generationVersion != generationVersion ||
                    !string.Equals(snapshot.raceId, raceId, StringComparison.Ordinal))
                    errors.Add(label + " Fortuna offer book identity does not match the expedition seed, generation version, and race.");
            }
            catch (Exception exception)
            {
                errors.Add(label + " Fortuna offer book snapshot is invalid: " + exception.Message);
            }
        }

        private static void ValidateRoomResults(ExpeditionSaveData expedition, List<string> errors)
        {
            if (expedition.completedRoomResults == null)
            {
                errors.Add("Completed room result history cannot be null.");
                return;
            }
            var resultRooms = new HashSet<string>(StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpeditionRoomCompletionSaveData result in expedition.completedRoomResults)
            {
                if (result == null || string.IsNullOrWhiteSpace(result.roomNodeId) ||
                    string.IsNullOrWhiteSpace(result.resultId) || string.IsNullOrWhiteSpace(result.resultJson))
                {
                    errors.Add("Completed room results require a node, stable result ID and serialized result.");
                    continue;
                }
                if (!resultRooms.Add(result.roomNodeId)) errors.Add("A route room cannot have more than one completion result: " + result.roomNodeId + ".");
                if (!resultIds.Add(result.resultId)) errors.Add("A room completion result ID cannot be reused: " + result.resultId + ".");
                if (expedition.completedRoomIds == null || !expedition.completedRoomIds.Contains(result.roomNodeId))
                    errors.Add("Completed room result has no matching completed room ID: " + result.roomNodeId + ".");
            }
            if (expedition.completedRoomIds != null && resultRooms.Count != expedition.completedRoomIds.Length)
                errors.Add("Every completed room must have exactly one saved result.");
            if (expedition.completedRoomIds != null && expedition.completedRoomIds.Contains(expedition.currentNodeId) &&
                !string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson))
                errors.Add("A completed current room cannot retain an entry checkpoint.");
            if (expedition.completedRoomIds != null && !expedition.completedRoomIds.Contains(expedition.currentNodeId) &&
                string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson))
                errors.Add("An unfinished current room requires its entry checkpoint.");
        }

        private static void ValidateSettings(GameSettingsSaveData settings, List<string> errors)
        {
            if (settings == null) { errors.Add("Settings data is missing."); return; }
            if (settings.languageId != "es" && settings.languageId != "en") errors.Add("Language must be es or en.");
            if (!IsVolume(settings.masterVolume)) errors.Add("Master volume must be between 0 and 1.");
            if (!IsVolume(settings.musicVolume)) errors.Add("Music volume must be between 0 and 1.");
            if (!IsVolume(settings.effectsVolume)) errors.Add("Effects volume must be between 0 and 1.");
        }

        private static void ValidateIds(string[] values, string name, List<string> errors, bool allowEmpty = true)
        {
            if (values == null) { errors.Add(name + " cannot be null."); return; }
            if (!allowEmpty && values.Length == 0) errors.Add(name + " cannot be empty.");
            if (values.Any(string.IsNullOrWhiteSpace)) errors.Add(name + " cannot contain empty values.");
            if (values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Count() !=
                values.Count(value => !string.IsNullOrWhiteSpace(value)))
                errors.Add(name + " must be unique.");
        }

        private static bool IsVolume(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            return value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        }
    }
}
