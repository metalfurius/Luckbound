using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Saves
{
    public static class SaveFormat
    {
        public const int SchemaVersion = 1;
        public const int ContentVersion = 1;
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
                processedSettlementRunIds = Copy(processedSettlementRunIds)
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
    public sealed class ExpeditionSaveData
    {
        public string runId;
        public uint seed;
        public int generationVersion;
        public int contentVersion;
        public string classId;
        public string raceId;
        public string[] selectedSkillIds = Array.Empty<string>();
        public string routeSnapshotJson;
        public string currentNodeId;
        public string[] completedRoomIds = Array.Empty<string>();
        public string roomEntryCheckpointJson;
        public string inventorySnapshotJson;
        public int accumulatedRenown;
        public string[] pactIds = Array.Empty<string>();
        public int fortunaCharges;
        public string[] pendingOfferIds = Array.Empty<string>();
        public int[] offerRerollCounts = Array.Empty<int>();
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
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
            if (string.IsNullOrWhiteSpace(expedition.routeSnapshotJson)) errors.Add("Active expedition route snapshot is missing.");
            if (string.IsNullOrWhiteSpace(expedition.currentNodeId)) errors.Add("Active expedition current node is missing.");
            ValidateIds(expedition.completedRoomIds, "completed room IDs", errors);
            if (expedition.accumulatedRenown < 0) errors.Add("Active expedition Renown cannot be negative.");
            if (expedition.fortunaCharges < 0) errors.Add("Active expedition Fortuna charges cannot be negative.");
            ValidateIds(expedition.pactIds, "active expedition pact IDs", errors);
            ValidateIds(expedition.pendingOfferIds, "pending offer IDs", errors);
            ValidateIds(expedition.collectedItemIds, "collected item IDs", errors);
            ValidateIds(expedition.claimedRewardIds, "claimed reward IDs", errors);
            if (expedition.offerRerollCounts == null || expedition.offerRerollCounts.Any(count => count < 0))
                errors.Add("Offer reroll counts must be present and non-negative.");
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
