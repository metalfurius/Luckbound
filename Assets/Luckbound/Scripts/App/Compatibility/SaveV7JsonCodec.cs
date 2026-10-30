using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves.V7;

namespace Luckbound.App
{
    /// <summary>
    /// Closed, preproduction JSON wire codec for the V7 save DTOs. This codec is deliberately
    /// independent of JsonUtility and never reflects over a DTO.
    /// </summary>
    internal static class SaveV7JsonCodec
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const string EmptyChecksum = "0000000000000000000000000000000000000000000000000000000000000000";

        internal static byte[] EncodeProfile(ProfileSaveEnvelopeV7 value)
        {
            try
            {
                EnsureProfileValidForEncode(value);
                string preimage = WriteProfile(value, string.Empty);
                string checksum = ComputeChecksum(StrictUtf8.GetBytes(preimage));
                return StrictUtf8.GetBytes(WriteProfile(value, checksum));
            }
            catch (SaveV7ValidationException) { throw; }
            catch (Exception exception)
            {
                throw ValidationFailure("/", "Profile cannot be encoded as strict V7 JSON: " + exception.Message);
            }
        }

        internal static bool TryDecodeProfile(
            byte[] bytes,
            out ProfileSaveEnvelopeV7 value,
            out SaveV7Error error)
        {
            value = null;
            error = None();
            try
            {
                JsonValue root = ParseBytes(bytes, "/");
                ProfileSaveEnvelopeV7 parsed = ParseProfile(root, "/");
                if (!IsV7Header(parsed.schemaVersion, parsed.contentVersion))
                {
                    error = Error(SaveV7ErrorCode.Incompatible, "/contentVersion",
                        "The profile header is not schema 1, content version 7.");
                    return false;
                }

                if (!TryAuthenticateProfile(parsed, out error)) return false;
                if (!SaveV7Validation.TryValidateProfile(parsed, out error)) return false;
                if (!TryValidateEmbeddedCheckpoint(parsed, out error)) return false;

                value = parsed;
                error = None();
                return true;
            }
            catch (JsonCodecException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, exception.Path, exception.Message);
                return false;
            }
            catch (DecoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input is not strict UTF-8: " + exception.Message);
                return false;
            }
            catch (EncoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input contains invalid Unicode: " + exception.Message);
                return false;
            }
            catch (Exception exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Could not decode profile JSON: " + exception.Message);
                return false;
            }
        }

        internal static byte[] EncodeSettings(SettingsSaveEnvelopeV7 value)
        {
            try
            {
                if (value == null)
                    throw ValidationFailure("/", "Settings envelope is null.");
                if (!SaveV7Validation.TryValidateSettings(value, out SaveV7Error sourceError))
                    throw new SaveV7ValidationException(sourceError);

                SettingsSaveEnvelopeV7 validationCopy = SaveV7Clone.Clone(value);
                validationCopy.checksum = EmptyChecksum;
                if (!SaveV7Validation.TryValidateSettings(validationCopy, out SaveV7Error error))
                    throw new SaveV7ValidationException(error);

                string preimage = WriteSettings(value, string.Empty);
                string checksum = ComputeChecksum(StrictUtf8.GetBytes(preimage));
                return StrictUtf8.GetBytes(WriteSettings(value, checksum));
            }
            catch (SaveV7ValidationException) { throw; }
            catch (Exception exception)
            {
                throw ValidationFailure("/", "Settings cannot be encoded as strict V7 JSON: " + exception.Message);
            }
        }

        internal static bool TryDecodeSettings(
            byte[] bytes,
            out SettingsSaveEnvelopeV7 value,
            out SaveV7Error error)
        {
            value = null;
            error = None();
            try
            {
                JsonValue root = ParseBytes(bytes, "/");
                SettingsSaveEnvelopeV7 parsed = ParseSettings(root, "/");
                if (!IsV7Header(parsed.schemaVersion, parsed.contentVersion))
                {
                    error = Error(SaveV7ErrorCode.Incompatible, "/contentVersion",
                        "The settings header is not schema 1, content version 7.");
                    return false;
                }

                if (!HasMatchingChecksum(WriteSettings(parsed, string.Empty), parsed.checksum))
                {
                    error = Error(SaveV7ErrorCode.Corrupted, "/checksum", "Settings checksum does not match.");
                    return false;
                }
                if (!SaveV7Validation.TryValidateSettings(parsed, out error)) return false;

                value = parsed;
                error = None();
                return true;
            }
            catch (JsonCodecException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, exception.Path, exception.Message);
                return false;
            }
            catch (DecoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input is not strict UTF-8: " + exception.Message);
                return false;
            }
            catch (EncoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input contains invalid Unicode: " + exception.Message);
                return false;
            }
            catch (Exception exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Could not decode settings JSON: " + exception.Message);
                return false;
            }
        }

        internal static byte[] EncodeCheckpoint(ExpeditionRoomCheckpointSaveDataV7 value)
        {
            try
            {
                if (value == null)
                    throw ValidationFailure("/", "Checkpoint is null.");
                if (!SaveV7Validation.TryValidateCheckpoint(value, out SaveV7Error error))
                    throw new SaveV7ValidationException(error);
                return StrictUtf8.GetBytes(WriteCheckpoint(value));
            }
            catch (SaveV7ValidationException) { throw; }
            catch (Exception exception)
            {
                throw ValidationFailure("/", "Checkpoint cannot be encoded as strict V7 JSON: " + exception.Message);
            }
        }

        internal static bool TryDecodeCheckpoint(
            byte[] bytes,
            out ExpeditionRoomCheckpointSaveDataV7 value,
            out SaveV7Error error)
        {
            value = null;
            error = None();
            try
            {
                JsonValue root = ParseBytes(bytes, "/");
                ExpeditionRoomCheckpointSaveDataV7 parsed = ParseCheckpoint(root, "/");
                if (!SaveV7Validation.TryValidateCheckpoint(parsed, out error)) return false;
                value = parsed;
                error = None();
                return true;
            }
            catch (JsonCodecException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, exception.Path, exception.Message);
                return false;
            }
            catch (DecoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input is not strict UTF-8: " + exception.Message);
                return false;
            }
            catch (EncoderFallbackException exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Input contains invalid Unicode: " + exception.Message);
                return false;
            }
            catch (Exception exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Could not decode checkpoint JSON: " + exception.Message);
                return false;
            }
        }

        internal static bool TryAuthenticateProfile(ProfileSaveEnvelopeV7 value, out SaveV7Error error)
        {
            if (value == null)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Profile envelope is null.");
                return false;
            }

            try
            {
                if (!HasMatchingChecksum(WriteProfile(value, string.Empty), value.checksum))
                {
                    error = Error(SaveV7ErrorCode.Corrupted, "/checksum", "Profile checksum does not match.");
                    return false;
                }
                error = None();
                return true;
            }
            catch (Exception exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted, "/", "Could not authenticate profile: " + exception.Message);
                return false;
            }
        }

        private static void EnsureProfileValidForEncode(ProfileSaveEnvelopeV7 value)
        {
            if (value == null)
                throw ValidationFailure("/", "Profile envelope is null.");
            if (!SaveV7Validation.TryValidateProfile(value, out SaveV7Error sourceError))
                throw new SaveV7ValidationException(sourceError);

            ProfileSaveEnvelopeV7 validationCopy = SaveV7Clone.Clone(value);
            validationCopy.checksum = EmptyChecksum;
            if (!SaveV7Validation.TryValidateProfile(validationCopy, out SaveV7Error error))
                throw new SaveV7ValidationException(error);
            if (!ValidateEmbeddedCheckpoint(validationCopy, out error))
                throw new SaveV7ValidationException(error);
        }

        private static bool TryValidateEmbeddedCheckpoint(ProfileSaveEnvelopeV7 value, out SaveV7Error error)
        {
            return ValidateEmbeddedCheckpoint(value, out error);
        }

        private static bool ValidateEmbeddedCheckpoint(ProfileSaveEnvelopeV7 value, out SaveV7Error error)
        {
            error = None();
            if (value == null)
                return true;
            if (!value.hasActiveExpedition || value.activeExpedition == null)
                return SaveV7Validation.TryValidateCheckpointBinding(value, null, out error);

            bool currentRoomCompleted = false;
            string currentNodeId = value.activeExpedition.currentNodeId;
            string[] completedRoomIds = value.activeExpedition.completedRoomIds;
            if (currentNodeId != null && completedRoomIds != null)
            {
                for (int i = 0; i < completedRoomIds.Length; i++)
                {
                    if (string.Equals(completedRoomIds[i], currentNodeId, StringComparison.Ordinal))
                    {
                        currentRoomCompleted = true;
                        break;
                    }
                }
            }

            string checkpointJson = value.activeExpedition.roomEntryCheckpointJson;
            if (currentRoomCompleted)
                return SaveV7Validation.TryValidateCheckpointBinding(value, null, out error);

            if (string.IsNullOrWhiteSpace(checkpointJson))
                return SaveV7Validation.TryValidateCheckpointBinding(value, null, out error);

            ExpeditionRoomCheckpointSaveDataV7 checkpoint;
            try
            {
                checkpoint = ParseCheckpoint(ParseText(checkpointJson, "/activeExpedition/roomEntryCheckpointJson"),
                    "/activeExpedition/roomEntryCheckpointJson");
            }
            catch (Exception exception)
            {
                error = Error(SaveV7ErrorCode.Corrupted,
                    "/activeExpedition/roomEntryCheckpointJson", "Embedded checkpoint JSON is invalid: " + exception.Message);
                return false;
            }

            if (!SaveV7Validation.TryValidateCheckpoint(checkpoint, out error)) return false;
            if (!SaveV7Validation.TryValidateCheckpointBinding(value, checkpoint, out error)) return false;
            return true;
        }

        private static JsonValue ParseBytes(byte[] bytes, string path)
        {
            if (bytes == null) throw new JsonCodecException(path, "Input byte array is null.");
            return ParseText(StrictUtf8.GetString(bytes), path);
        }

        private static JsonValue ParseText(string text, string path)
        {
            if (text == null) throw new JsonCodecException(path, "JSON text is null.");
            return new JsonParser(text, path).Parse();
        }

        private static bool IsV7Header(int schemaVersion, int contentVersion) =>
            schemaVersion == 1 && contentVersion == 7;

        private static bool HasMatchingChecksum(string preimage, string storedChecksum)
        {
            if (!IsLowerSha256(storedChecksum)) return false;
            return string.Equals(ComputeChecksum(StrictUtf8.GetBytes(preimage)), storedChecksum, StringComparison.Ordinal);
        }

        private static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                    return false;
            }
            return true;
        }

        private static string ComputeChecksum(byte[] preimage)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(preimage);
                var result = new StringBuilder(64);
                for (int i = 0; i < digest.Length; i++) result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static SaveV7ValidationException ValidationFailure(string path, string message) =>
            new SaveV7ValidationException(Error(SaveV7ErrorCode.InvalidData, path, message));

        private static SaveV7Error None() => new SaveV7Error(SaveV7ErrorCode.None, string.Empty, string.Empty);

        private static SaveV7Error Error(SaveV7ErrorCode code, string path, string message) =>
            new SaveV7Error(code, path ?? string.Empty, message ?? string.Empty);

        private static ProfileSaveEnvelopeV7 ParseProfile(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) throw new JsonCodecException(path, "Profile envelope must be an object.");
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "schemaVersion", "contentVersion", "generation", "sequence", "savedAtUtc", "checksum",
                "profile", "hasActiveExpedition", "activeExpedition");
            return new ProfileSaveEnvelopeV7
            {
                schemaVersion = Int32(fields["schemaVersion"], Child(path, "schemaVersion")),
                contentVersion = Int32(fields["contentVersion"], Child(path, "contentVersion")),
                generation = Int32(fields["generation"], Child(path, "generation")),
                sequence = Int64(fields["sequence"], Child(path, "sequence")),
                savedAtUtc = StringValue(fields["savedAtUtc"], Child(path, "savedAtUtc")),
                checksum = StringValue(fields["checksum"], Child(path, "checksum")),
                profile = ParseProfileData(fields["profile"], Child(path, "profile")),
                hasActiveExpedition = Boolean(fields["hasActiveExpedition"], Child(path, "hasActiveExpedition")),
                activeExpedition = ParseExpedition(fields["activeExpedition"], Child(path, "activeExpedition"))
            };
        }

        private static SettingsSaveEnvelopeV7 ParseSettings(JsonValue value, string path)
        {
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "schemaVersion", "contentVersion", "generation", "sequence", "savedAtUtc", "checksum", "settings");
            return new SettingsSaveEnvelopeV7
            {
                schemaVersion = Int32(fields["schemaVersion"], Child(path, "schemaVersion")),
                contentVersion = Int32(fields["contentVersion"], Child(path, "contentVersion")),
                generation = Int32(fields["generation"], Child(path, "generation")),
                sequence = Int64(fields["sequence"], Child(path, "sequence")),
                savedAtUtc = StringValue(fields["savedAtUtc"], Child(path, "savedAtUtc")),
                checksum = StringValue(fields["checksum"], Child(path, "checksum")),
                settings = ParseGameSettings(fields["settings"], Child(path, "settings"))
            };
        }

        private static ProfileSaveDataV7 ParseProfileData(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "availableRenown", "healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel",
                "unlockedRaceIds", "unlockedStartingKitIds", "acceptedContractIds", "claimedContractIds",
                "readStoryFragmentIds", "contractCounters", "processedSettlementRunIds",
                "processedContractProgressEvents", "progressionPurchaseTransactions");
            return new ProfileSaveDataV7
            {
                availableRenown = Int32(fields["availableRenown"], Child(path, "availableRenown")),
                healthUpgradeLevel = Int32(fields["healthUpgradeLevel"], Child(path, "healthUpgradeLevel")),
                energyUpgradeLevel = Int32(fields["energyUpgradeLevel"], Child(path, "energyUpgradeLevel")),
                additionalPotionUpgradeLevel = Int32(fields["additionalPotionUpgradeLevel"], Child(path, "additionalPotionUpgradeLevel")),
                unlockedRaceIds = StringArray(fields["unlockedRaceIds"], Child(path, "unlockedRaceIds")),
                unlockedStartingKitIds = StringArray(fields["unlockedStartingKitIds"], Child(path, "unlockedStartingKitIds")),
                acceptedContractIds = StringArray(fields["acceptedContractIds"], Child(path, "acceptedContractIds")),
                claimedContractIds = StringArray(fields["claimedContractIds"], Child(path, "claimedContractIds")),
                readStoryFragmentIds = StringArray(fields["readStoryFragmentIds"], Child(path, "readStoryFragmentIds")),
                contractCounters = ArrayValue(fields["contractCounters"], Child(path, "contractCounters"), ParseContractCounter),
                processedSettlementRunIds = StringArray(fields["processedSettlementRunIds"], Child(path, "processedSettlementRunIds")),
                processedContractProgressEvents = ArrayValue(fields["processedContractProgressEvents"], Child(path, "processedContractProgressEvents"), ParseContractProgressEvent),
                progressionPurchaseTransactions = ArrayValue(fields["progressionPurchaseTransactions"], Child(path, "progressionPurchaseTransactions"), ParsePermanentUpgradePurchase)
            };
        }

        private static ContractCounterSaveDataV7 ParseContractCounter(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "contractId", "progress");
            return new ContractCounterSaveDataV7
            {
                contractId = StringValue(fields["contractId"], Child(path, "contractId")),
                progress = Int32(fields["progress"], Child(path, "progress"))
            };
        }

        private static ContractProgressEventReceiptSaveDataV7 ParseContractProgressEvent(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "eventId", "progressEvent");
            return new ContractProgressEventReceiptSaveDataV7
            {
                eventId = StringValue(fields["eventId"], Child(path, "eventId")),
                progressEvent = Int32(fields["progressEvent"], Child(path, "progressEvent"))
            };
        }

        private static PermanentUpgradePurchaseSaveDataV7 ParsePermanentUpgradePurchase(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "transactionId", "kind", "cost");
            return new PermanentUpgradePurchaseSaveDataV7
            {
                transactionId = StringValue(fields["transactionId"], Child(path, "transactionId")),
                kind = Int32(fields["kind"], Child(path, "kind")),
                cost = Int32(fields["cost"], Child(path, "cost"))
            };
        }

        private static ExpeditionSaveDataV7 ParseExpedition(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "runId", "seed", "generationVersion", "contentVersion", "classId", "raceId", "selectedSkillIds",
                "startingEquipmentInstanceIds", "routeSnapshotJson", "currentNodeId", "visitedNodeIds", "completedRoomIds",
                "completedRoomResults", "roomEntryCheckpointJson", "inventorySnapshotJson", "maximumHealth", "currentHealth",
                "maximumEnergy", "currentEnergy", "healthPotionCharges", "frozenPermanentUpgrades", "hasFrozenPermanentUpgrades",
                "accumulatedRenown", "pactIds", "fortuneOfferBookSnapshot", "collectedItemIds", "claimedRewardIds", "encounterPlanPin");
            return new ExpeditionSaveDataV7
            {
                runId = StringValue(fields["runId"], Child(path, "runId")),
                seed = UInt32(fields["seed"], Child(path, "seed")),
                generationVersion = Int32(fields["generationVersion"], Child(path, "generationVersion")),
                contentVersion = Int32(fields["contentVersion"], Child(path, "contentVersion")),
                classId = StringValue(fields["classId"], Child(path, "classId")),
                raceId = StringValue(fields["raceId"], Child(path, "raceId")),
                selectedSkillIds = StringArray(fields["selectedSkillIds"], Child(path, "selectedSkillIds")),
                startingEquipmentInstanceIds = StringArray(fields["startingEquipmentInstanceIds"], Child(path, "startingEquipmentInstanceIds")),
                routeSnapshotJson = StringValue(fields["routeSnapshotJson"], Child(path, "routeSnapshotJson")),
                currentNodeId = StringValue(fields["currentNodeId"], Child(path, "currentNodeId")),
                visitedNodeIds = StringArray(fields["visitedNodeIds"], Child(path, "visitedNodeIds")),
                completedRoomIds = StringArray(fields["completedRoomIds"], Child(path, "completedRoomIds")),
                completedRoomResults = ArrayValue(fields["completedRoomResults"], Child(path, "completedRoomResults"), ParseExpeditionRoomCompletion),
                roomEntryCheckpointJson = StringValue(fields["roomEntryCheckpointJson"], Child(path, "roomEntryCheckpointJson")),
                inventorySnapshotJson = StringValue(fields["inventorySnapshotJson"], Child(path, "inventorySnapshotJson")),
                maximumHealth = Int32(fields["maximumHealth"], Child(path, "maximumHealth")),
                currentHealth = Int32(fields["currentHealth"], Child(path, "currentHealth")),
                maximumEnergy = Int32(fields["maximumEnergy"], Child(path, "maximumEnergy")),
                currentEnergy = Int32(fields["currentEnergy"], Child(path, "currentEnergy")),
                healthPotionCharges = Int32(fields["healthPotionCharges"], Child(path, "healthPotionCharges")),
                frozenPermanentUpgrades = ParseExpeditionPermanentUpgradeSnapshot(fields["frozenPermanentUpgrades"], Child(path, "frozenPermanentUpgrades")),
                hasFrozenPermanentUpgrades = Boolean(fields["hasFrozenPermanentUpgrades"], Child(path, "hasFrozenPermanentUpgrades")),
                accumulatedRenown = Int32(fields["accumulatedRenown"], Child(path, "accumulatedRenown")),
                pactIds = StringArray(fields["pactIds"], Child(path, "pactIds")),
                fortuneOfferBookSnapshot = ParseFortuneOfferBook(fields["fortuneOfferBookSnapshot"], Child(path, "fortuneOfferBookSnapshot")),
                collectedItemIds = StringArray(fields["collectedItemIds"], Child(path, "collectedItemIds")),
                claimedRewardIds = StringArray(fields["claimedRewardIds"], Child(path, "claimedRewardIds")),
                encounterPlanPin = ParseEncounterPlanPin(fields["encounterPlanPin"], Child(path, "encounterPlanPin"))
            };
        }

        private static ExpeditionPermanentUpgradeSnapshotSaveDataV7 ParseExpeditionPermanentUpgradeSnapshot(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel");
            return new ExpeditionPermanentUpgradeSnapshotSaveDataV7
            {
                healthUpgradeLevel = Int32(fields["healthUpgradeLevel"], Child(path, "healthUpgradeLevel")),
                energyUpgradeLevel = Int32(fields["energyUpgradeLevel"], Child(path, "energyUpgradeLevel")),
                additionalPotionUpgradeLevel = Int32(fields["additionalPotionUpgradeLevel"], Child(path, "additionalPotionUpgradeLevel"))
            };
        }

        private static ExpeditionRoomCheckpointSaveDataV7 ParseCheckpoint(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) throw new JsonCodecException(path, "Checkpoint must be an object.");
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "roomNodeId", "inventorySnapshotJson", "maximumHealth", "currentHealth", "maximumEnergy", "currentEnergy",
                "healthPotionCharges", "accumulatedRenown", "fortuneOfferBookSnapshot", "pactIds", "collectedItemIds",
                "claimedRewardIds", "encounterEntry");
            return new ExpeditionRoomCheckpointSaveDataV7
            {
                roomNodeId = StringValue(fields["roomNodeId"], Child(path, "roomNodeId")),
                inventorySnapshotJson = StringValue(fields["inventorySnapshotJson"], Child(path, "inventorySnapshotJson")),
                maximumHealth = Int32(fields["maximumHealth"], Child(path, "maximumHealth")),
                currentHealth = Int32(fields["currentHealth"], Child(path, "currentHealth")),
                maximumEnergy = Int32(fields["maximumEnergy"], Child(path, "maximumEnergy")),
                currentEnergy = Int32(fields["currentEnergy"], Child(path, "currentEnergy")),
                healthPotionCharges = Int32(fields["healthPotionCharges"], Child(path, "healthPotionCharges")),
                accumulatedRenown = Int32(fields["accumulatedRenown"], Child(path, "accumulatedRenown")),
                fortuneOfferBookSnapshot = ParseFortuneOfferBook(fields["fortuneOfferBookSnapshot"], Child(path, "fortuneOfferBookSnapshot")),
                pactIds = StringArray(fields["pactIds"], Child(path, "pactIds")),
                collectedItemIds = StringArray(fields["collectedItemIds"], Child(path, "collectedItemIds")),
                claimedRewardIds = StringArray(fields["claimedRewardIds"], Child(path, "claimedRewardIds")),
                encounterEntry = ParseEncounterEntry(fields["encounterEntry"], Child(path, "encounterEntry"))
            };
        }

        private static ExpeditionRoomCompletionSaveDataV7 ParseExpeditionRoomCompletion(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "roomNodeId", "resultId", "resultJson");
            return new ExpeditionRoomCompletionSaveDataV7
            {
                roomNodeId = StringValue(fields["roomNodeId"], Child(path, "roomNodeId")),
                resultId = StringValue(fields["resultId"], Child(path, "resultId")),
                resultJson = StringValue(fields["resultJson"], Child(path, "resultJson"))
            };
        }

        private static FortuneOfferBookSnapshotV7 ParseFortuneOfferBook(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "snapshotVersion", "generationVersion", "seed", "raceId", "charges", "offers", "processedGuardianIds",
                "rerollReceipts", "fortuneBrewReceipts");
            return new FortuneOfferBookSnapshotV7
            {
                snapshotVersion = Int32(fields["snapshotVersion"], Child(path, "snapshotVersion")),
                generationVersion = Int32(fields["generationVersion"], Child(path, "generationVersion")),
                seed = UInt32(fields["seed"], Child(path, "seed")),
                raceId = StringValue(fields["raceId"], Child(path, "raceId")),
                charges = Int32(fields["charges"], Child(path, "charges")),
                offers = ArrayValue(fields["offers"], Child(path, "offers"), ParseFortuneOffer),
                processedGuardianIds = StringArray(fields["processedGuardianIds"], Child(path, "processedGuardianIds")),
                rerollReceipts = ArrayValue(fields["rerollReceipts"], Child(path, "rerollReceipts"), ParseFortuneRerollReceipt),
                fortuneBrewReceipts = ArrayValue(fields["fortuneBrewReceipts"], Child(path, "fortuneBrewReceipts"), ParseFortuneBrewReceipt)
            };
        }

        private static FortuneOfferSnapshotV7 ParseFortuneOffer(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "offerId", "contextId", "eligibleOptionIds", "history", "selectedOptionId", "claimed");
            return new FortuneOfferSnapshotV7
            {
                offerId = StringValue(fields["offerId"], Child(path, "offerId")),
                contextId = StringValue(fields["contextId"], Child(path, "contextId")),
                eligibleOptionIds = StringArray(fields["eligibleOptionIds"], Child(path, "eligibleOptionIds")),
                history = ArrayValue(fields["history"], Child(path, "history"), ParseFortuneOfferHistory),
                selectedOptionId = StringValue(fields["selectedOptionId"], Child(path, "selectedOptionId")),
                claimed = Boolean(fields["claimed"], Child(path, "claimed"))
            };
        }

        private static FortuneOfferHistorySnapshotV7 ParseFortuneOfferHistory(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "optionIds");
            return new FortuneOfferHistorySnapshotV7
            {
                optionIds = StringArray(fields["optionIds"], Child(path, "optionIds"))
            };
        }

        private static FortuneRerollReceiptSnapshotV7 ParseFortuneRerollReceipt(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "transactionId", "offerId", "status", "contextId", "optionIds", "selectedOptionId", "rerollCount",
                "claimed", "remainingCharges", "hasOffer");
            return new FortuneRerollReceiptSnapshotV7
            {
                transactionId = StringValue(fields["transactionId"], Child(path, "transactionId")),
                offerId = StringValue(fields["offerId"], Child(path, "offerId")),
                status = Int32(fields["status"], Child(path, "status")),
                contextId = StringValue(fields["contextId"], Child(path, "contextId")),
                optionIds = StringArray(fields["optionIds"], Child(path, "optionIds")),
                selectedOptionId = StringValue(fields["selectedOptionId"], Child(path, "selectedOptionId")),
                rerollCount = Int32(fields["rerollCount"], Child(path, "rerollCount")),
                claimed = Boolean(fields["claimed"], Child(path, "claimed")),
                remainingCharges = Int32(fields["remainingCharges"], Child(path, "remainingCharges")),
                hasOffer = Boolean(fields["hasOffer"], Child(path, "hasOffer"))
            };
        }

        private static FortuneBrewReceiptSnapshotV7 ParseFortuneBrewReceipt(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "transactionId", "itemInstanceId", "status", "currentCharges");
            return new FortuneBrewReceiptSnapshotV7
            {
                transactionId = StringValue(fields["transactionId"], Child(path, "transactionId")),
                itemInstanceId = StringValue(fields["itemInstanceId"], Child(path, "itemInstanceId")),
                status = Int32(fields["status"], Child(path, "status")),
                currentCharges = Int32(fields["currentCharges"], Child(path, "currentCharges"))
            };
        }

        private static GameSettingsSaveDataV7 ParseGameSettings(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "languageId", "masterVolume", "musicVolume", "effectsVolume", "ambienceVolume");
            return new GameSettingsSaveDataV7
            {
                languageId = StringValue(fields["languageId"], Child(path, "languageId")),
                masterVolume = Single(fields["masterVolume"], Child(path, "masterVolume")),
                musicVolume = Single(fields["musicVolume"], Child(path, "musicVolume")),
                effectsVolume = Single(fields["effectsVolume"], Child(path, "effectsVolume")),
                ambienceVolume = Single(fields["ambienceVolume"], Child(path, "ambienceVolume"))
            };
        }

        private static EncounterPlanPinSaveDataV7 ParseEncounterPlanPin(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path,
                "schemaVersion", "runId", "catalogId", "catalogRevision", "catalogSha256", "routeSha256",
                "identityCodec", "bindings", "entry");
            return new EncounterPlanPinSaveDataV7
            {
                schemaVersion = Int32(fields["schemaVersion"], Child(path, "schemaVersion")),
                runId = StringValue(fields["runId"], Child(path, "runId")),
                catalogId = StringValue(fields["catalogId"], Child(path, "catalogId")),
                catalogRevision = Int32(fields["catalogRevision"], Child(path, "catalogRevision")),
                catalogSha256 = StringValue(fields["catalogSha256"], Child(path, "catalogSha256")),
                routeSha256 = StringValue(fields["routeSha256"], Child(path, "routeSha256")),
                identityCodec = StringValue(fields["identityCodec"], Child(path, "identityCodec")),
                bindings = ArrayValue(fields["bindings"], Child(path, "bindings"), ParseEncounterPlanNodeBinding),
                entry = ParseEncounterEntry(fields["entry"], Child(path, "entry"))
            };
        }

        private static EncounterPlanNodeBindingSaveDataV7 ParseEncounterPlanNodeBinding(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            if (value.Kind != JsonKind.Object || !value.Properties.TryGetValue("mode", out JsonValue modeValue))
                throw new JsonCodecException(Child(path, "mode"), "Required mode field is missing.");
            string modeToken = StringValue(modeValue, Child(path, "mode"));
            EncounterCatalogBindingMode mode = ParseMode(modeToken, Child(path, "mode"));
            Dictionary<string, JsonValue> fields;
            if (mode == EncounterCatalogBindingMode.Encounter)
            {
                fields = ObjectFields(value, path,
                    "nodeId", "generationVersion", "templateId", "roomInstanceId", "mode", "encounterId",
                    "definitionRevision", "definitionSha256", "variantId", "contextPactIds", "calendarOrdinal",
                    "resolvedPlanSha256");
            }
            else
            {
                fields = ObjectFields(value, path,
                    "nodeId", "generationVersion", "templateId", "roomInstanceId", "mode", "contextPactIds",
                    "calendarOrdinal", "resolvedPlanSha256");
            }

            return new EncounterPlanNodeBindingSaveDataV7
            {
                nodeId = StringValue(fields["nodeId"], Child(path, "nodeId")),
                generationVersion = Int32(fields["generationVersion"], Child(path, "generationVersion")),
                templateId = StringValue(fields["templateId"], Child(path, "templateId")),
                roomInstanceId = StringValue(fields["roomInstanceId"], Child(path, "roomInstanceId")),
                mode = mode,
                encounterId = mode == EncounterCatalogBindingMode.Encounter
                    ? StringValue(fields["encounterId"], Child(path, "encounterId")) : null,
                definitionRevision = mode == EncounterCatalogBindingMode.Encounter
                    ? Int32(fields["definitionRevision"], Child(path, "definitionRevision")) : 0,
                definitionSha256 = mode == EncounterCatalogBindingMode.Encounter
                    ? StringValue(fields["definitionSha256"], Child(path, "definitionSha256")) : null,
                variantId = mode == EncounterCatalogBindingMode.Encounter
                    ? StringValue(fields["variantId"], Child(path, "variantId")) : null,
                contextPactIds = StringArray(fields["contextPactIds"], Child(path, "contextPactIds")),
                calendarOrdinal = Int32(fields["calendarOrdinal"], Child(path, "calendarOrdinal")),
                resolvedPlanSha256 = StringValue(fields["resolvedPlanSha256"], Child(path, "resolvedPlanSha256"))
            };
        }

        private static EncounterPlanEntryReferenceSaveDataV7 ParseEncounterEntry(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonValue> fields = ObjectFields(value, path, "nodeId", "resolvedPlanSha256");
            return new EncounterPlanEntryReferenceSaveDataV7
            {
                nodeId = StringValue(fields["nodeId"], Child(path, "nodeId")),
                resolvedPlanSha256 = StringValue(fields["resolvedPlanSha256"], Child(path, "resolvedPlanSha256"))
            };
        }

        private static string WriteProfile(ProfileSaveEnvelopeV7 value, string checksumOverride)
        {
            var writer = new JsonWriter();
            if (value == null) { writer.Null(); return writer.ToString(); }
            writer.BeginObject();
            writer.Name("schemaVersion"); writer.Number(value.schemaVersion);
            writer.Name("contentVersion"); writer.Number(value.contentVersion);
            writer.Name("generation"); writer.Number(value.generation);
            writer.Name("sequence"); writer.Number(value.sequence);
            writer.Name("savedAtUtc"); writer.String(value.savedAtUtc);
            writer.Name("checksum"); writer.String(checksumOverride ?? value.checksum);
            writer.Name("profile"); WriteProfileData(writer, value.profile);
            writer.Name("hasActiveExpedition"); writer.Boolean(value.hasActiveExpedition);
            writer.Name("activeExpedition"); WriteExpedition(writer, value.activeExpedition);
            writer.EndObject();
            return writer.ToString();
        }

        private static string WriteSettings(SettingsSaveEnvelopeV7 value, string checksumOverride)
        {
            var writer = new JsonWriter();
            if (value == null) { writer.Null(); return writer.ToString(); }
            writer.BeginObject();
            writer.Name("schemaVersion"); writer.Number(value.schemaVersion);
            writer.Name("contentVersion"); writer.Number(value.contentVersion);
            writer.Name("generation"); writer.Number(value.generation);
            writer.Name("sequence"); writer.Number(value.sequence);
            writer.Name("savedAtUtc"); writer.String(value.savedAtUtc);
            writer.Name("checksum"); writer.String(checksumOverride ?? value.checksum);
            writer.Name("settings"); WriteGameSettings(writer, value.settings);
            writer.EndObject();
            return writer.ToString();
        }

        private static string WriteCheckpoint(ExpeditionRoomCheckpointSaveDataV7 value)
        {
            var writer = new JsonWriter();
            WriteCheckpoint(writer, value);
            return writer.ToString();
        }

        private static void WriteProfileData(JsonWriter writer, ProfileSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("availableRenown"); writer.Number(value.availableRenown);
            writer.Name("healthUpgradeLevel"); writer.Number(value.healthUpgradeLevel);
            writer.Name("energyUpgradeLevel"); writer.Number(value.energyUpgradeLevel);
            writer.Name("additionalPotionUpgradeLevel"); writer.Number(value.additionalPotionUpgradeLevel);
            writer.Name("unlockedRaceIds"); WriteStrings(writer, value.unlockedRaceIds);
            writer.Name("unlockedStartingKitIds"); WriteStrings(writer, value.unlockedStartingKitIds);
            writer.Name("acceptedContractIds"); WriteStrings(writer, value.acceptedContractIds);
            writer.Name("claimedContractIds"); WriteStrings(writer, value.claimedContractIds);
            writer.Name("readStoryFragmentIds"); WriteStrings(writer, value.readStoryFragmentIds);
            writer.Name("contractCounters"); WriteArray(writer, value.contractCounters, WriteContractCounter);
            writer.Name("processedSettlementRunIds"); WriteStrings(writer, value.processedSettlementRunIds);
            writer.Name("processedContractProgressEvents"); WriteArray(writer, value.processedContractProgressEvents, WriteContractProgressEvent);
            writer.Name("progressionPurchaseTransactions"); WriteArray(writer, value.progressionPurchaseTransactions, WritePermanentUpgradePurchase);
            writer.EndObject();
        }

        private static void WriteContractCounter(JsonWriter writer, ContractCounterSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("contractId"); writer.String(value.contractId);
            writer.Name("progress"); writer.Number(value.progress);
            writer.EndObject();
        }

        private static void WriteContractProgressEvent(JsonWriter writer, ContractProgressEventReceiptSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("eventId"); writer.String(value.eventId);
            writer.Name("progressEvent"); writer.Number(value.progressEvent);
            writer.EndObject();
        }

        private static void WritePermanentUpgradePurchase(JsonWriter writer, PermanentUpgradePurchaseSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("transactionId"); writer.String(value.transactionId);
            writer.Name("kind"); writer.Number(value.kind);
            writer.Name("cost"); writer.Number(value.cost);
            writer.EndObject();
        }

        private static void WriteExpedition(JsonWriter writer, ExpeditionSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("runId"); writer.String(value.runId);
            writer.Name("seed"); writer.Number(value.seed);
            writer.Name("generationVersion"); writer.Number(value.generationVersion);
            writer.Name("contentVersion"); writer.Number(value.contentVersion);
            writer.Name("classId"); writer.String(value.classId);
            writer.Name("raceId"); writer.String(value.raceId);
            writer.Name("selectedSkillIds"); WriteStrings(writer, value.selectedSkillIds);
            writer.Name("startingEquipmentInstanceIds"); WriteStrings(writer, value.startingEquipmentInstanceIds);
            writer.Name("routeSnapshotJson"); writer.String(value.routeSnapshotJson);
            writer.Name("currentNodeId"); writer.String(value.currentNodeId);
            writer.Name("visitedNodeIds"); WriteStrings(writer, value.visitedNodeIds);
            writer.Name("completedRoomIds"); WriteStrings(writer, value.completedRoomIds);
            writer.Name("completedRoomResults"); WriteArray(writer, value.completedRoomResults, WriteExpeditionRoomCompletion);
            writer.Name("roomEntryCheckpointJson"); writer.String(value.roomEntryCheckpointJson);
            writer.Name("inventorySnapshotJson"); writer.String(value.inventorySnapshotJson);
            writer.Name("maximumHealth"); writer.Number(value.maximumHealth);
            writer.Name("currentHealth"); writer.Number(value.currentHealth);
            writer.Name("maximumEnergy"); writer.Number(value.maximumEnergy);
            writer.Name("currentEnergy"); writer.Number(value.currentEnergy);
            writer.Name("healthPotionCharges"); writer.Number(value.healthPotionCharges);
            writer.Name("frozenPermanentUpgrades"); WriteExpeditionPermanentUpgradeSnapshot(writer, value.frozenPermanentUpgrades);
            writer.Name("hasFrozenPermanentUpgrades"); writer.Boolean(value.hasFrozenPermanentUpgrades);
            writer.Name("accumulatedRenown"); writer.Number(value.accumulatedRenown);
            writer.Name("pactIds"); WriteStrings(writer, value.pactIds);
            writer.Name("fortuneOfferBookSnapshot"); WriteFortuneOfferBook(writer, value.fortuneOfferBookSnapshot);
            writer.Name("collectedItemIds"); WriteStrings(writer, value.collectedItemIds);
            writer.Name("claimedRewardIds"); WriteStrings(writer, value.claimedRewardIds);
            writer.Name("encounterPlanPin"); WriteEncounterPlanPin(writer, value.encounterPlanPin);
            writer.EndObject();
        }

        private static void WriteExpeditionPermanentUpgradeSnapshot(JsonWriter writer, ExpeditionPermanentUpgradeSnapshotSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("healthUpgradeLevel"); writer.Number(value.healthUpgradeLevel);
            writer.Name("energyUpgradeLevel"); writer.Number(value.energyUpgradeLevel);
            writer.Name("additionalPotionUpgradeLevel"); writer.Number(value.additionalPotionUpgradeLevel);
            writer.EndObject();
        }

        private static void WriteCheckpoint(JsonWriter writer, ExpeditionRoomCheckpointSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("roomNodeId"); writer.String(value.roomNodeId);
            writer.Name("inventorySnapshotJson"); writer.String(value.inventorySnapshotJson);
            writer.Name("maximumHealth"); writer.Number(value.maximumHealth);
            writer.Name("currentHealth"); writer.Number(value.currentHealth);
            writer.Name("maximumEnergy"); writer.Number(value.maximumEnergy);
            writer.Name("currentEnergy"); writer.Number(value.currentEnergy);
            writer.Name("healthPotionCharges"); writer.Number(value.healthPotionCharges);
            writer.Name("accumulatedRenown"); writer.Number(value.accumulatedRenown);
            writer.Name("fortuneOfferBookSnapshot"); WriteFortuneOfferBook(writer, value.fortuneOfferBookSnapshot);
            writer.Name("pactIds"); WriteStrings(writer, value.pactIds);
            writer.Name("collectedItemIds"); WriteStrings(writer, value.collectedItemIds);
            writer.Name("claimedRewardIds"); WriteStrings(writer, value.claimedRewardIds);
            writer.Name("encounterEntry"); WriteEncounterEntry(writer, value.encounterEntry);
            writer.EndObject();
        }

        private static void WriteExpeditionRoomCompletion(JsonWriter writer, ExpeditionRoomCompletionSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("roomNodeId"); writer.String(value.roomNodeId);
            writer.Name("resultId"); writer.String(value.resultId);
            writer.Name("resultJson"); writer.String(value.resultJson);
            writer.EndObject();
        }

        private static void WriteFortuneOfferBook(JsonWriter writer, FortuneOfferBookSnapshotV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("snapshotVersion"); writer.Number(value.snapshotVersion);
            writer.Name("generationVersion"); writer.Number(value.generationVersion);
            writer.Name("seed"); writer.Number(value.seed);
            writer.Name("raceId"); writer.String(value.raceId);
            writer.Name("charges"); writer.Number(value.charges);
            writer.Name("offers"); WriteArray(writer, value.offers, WriteFortuneOffer);
            writer.Name("processedGuardianIds"); WriteStrings(writer, value.processedGuardianIds);
            writer.Name("rerollReceipts"); WriteArray(writer, value.rerollReceipts, WriteFortuneRerollReceipt);
            writer.Name("fortuneBrewReceipts"); WriteArray(writer, value.fortuneBrewReceipts, WriteFortuneBrewReceipt);
            writer.EndObject();
        }

        private static void WriteFortuneOffer(JsonWriter writer, FortuneOfferSnapshotV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("offerId"); writer.String(value.offerId);
            writer.Name("contextId"); writer.String(value.contextId);
            writer.Name("eligibleOptionIds"); WriteStrings(writer, value.eligibleOptionIds);
            writer.Name("history"); WriteArray(writer, value.history, WriteFortuneOfferHistory);
            writer.Name("selectedOptionId"); writer.String(value.selectedOptionId);
            writer.Name("claimed"); writer.Boolean(value.claimed);
            writer.EndObject();
        }

        private static void WriteFortuneOfferHistory(JsonWriter writer, FortuneOfferHistorySnapshotV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("optionIds"); WriteStrings(writer, value.optionIds);
            writer.EndObject();
        }

        private static void WriteFortuneRerollReceipt(JsonWriter writer, FortuneRerollReceiptSnapshotV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("transactionId"); writer.String(value.transactionId);
            writer.Name("offerId"); writer.String(value.offerId);
            writer.Name("status"); writer.Number(value.status);
            writer.Name("contextId"); writer.String(value.contextId);
            writer.Name("optionIds"); WriteStrings(writer, value.optionIds);
            writer.Name("selectedOptionId"); writer.String(value.selectedOptionId);
            writer.Name("rerollCount"); writer.Number(value.rerollCount);
            writer.Name("claimed"); writer.Boolean(value.claimed);
            writer.Name("remainingCharges"); writer.Number(value.remainingCharges);
            writer.Name("hasOffer"); writer.Boolean(value.hasOffer);
            writer.EndObject();
        }

        private static void WriteFortuneBrewReceipt(JsonWriter writer, FortuneBrewReceiptSnapshotV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("transactionId"); writer.String(value.transactionId);
            writer.Name("itemInstanceId"); writer.String(value.itemInstanceId);
            writer.Name("status"); writer.Number(value.status);
            writer.Name("currentCharges"); writer.Number(value.currentCharges);
            writer.EndObject();
        }

        private static void WriteGameSettings(JsonWriter writer, GameSettingsSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("languageId"); writer.String(value.languageId);
            writer.Name("masterVolume"); writer.Number(value.masterVolume);
            writer.Name("musicVolume"); writer.Number(value.musicVolume);
            writer.Name("effectsVolume"); writer.Number(value.effectsVolume);
            writer.Name("ambienceVolume"); writer.Number(value.ambienceVolume);
            writer.EndObject();
        }

        private static void WriteEncounterPlanPin(JsonWriter writer, EncounterPlanPinSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("schemaVersion"); writer.Number(value.schemaVersion);
            writer.Name("runId"); writer.String(value.runId);
            writer.Name("catalogId"); writer.String(value.catalogId);
            writer.Name("catalogRevision"); writer.Number(value.catalogRevision);
            writer.Name("catalogSha256"); writer.String(value.catalogSha256);
            writer.Name("routeSha256"); writer.String(value.routeSha256);
            writer.Name("identityCodec"); writer.String(value.identityCodec);
            writer.Name("bindings"); WriteArray(writer, value.bindings, WriteEncounterPlanNodeBinding);
            writer.Name("entry"); WriteEncounterEntry(writer, value.entry);
            writer.EndObject();
        }

        private static void WriteEncounterPlanNodeBinding(JsonWriter writer, EncounterPlanNodeBindingSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            string modeToken = ModeToken(value.mode);
            writer.BeginObject();
            writer.Name("nodeId"); writer.String(value.nodeId);
            writer.Name("generationVersion"); writer.Number(value.generationVersion);
            writer.Name("templateId"); writer.String(value.templateId);
            writer.Name("roomInstanceId"); writer.String(value.roomInstanceId);
            writer.Name("mode"); writer.String(modeToken);
            if (value.mode == EncounterCatalogBindingMode.Encounter)
            {
                writer.Name("encounterId"); writer.String(value.encounterId);
                writer.Name("definitionRevision"); writer.Number(value.definitionRevision);
                writer.Name("definitionSha256"); writer.String(value.definitionSha256);
                writer.Name("variantId"); writer.String(value.variantId);
            }
            writer.Name("contextPactIds"); WriteStrings(writer, value.contextPactIds);
            writer.Name("calendarOrdinal"); writer.Number(value.calendarOrdinal);
            writer.Name("resolvedPlanSha256"); writer.String(value.resolvedPlanSha256);
            writer.EndObject();
        }

        private static void WriteEncounterEntry(JsonWriter writer, EncounterPlanEntryReferenceSaveDataV7 value)
        {
            if (value == null) { writer.Null(); return; }
            writer.BeginObject();
            writer.Name("nodeId"); writer.String(value.nodeId);
            writer.Name("resolvedPlanSha256"); writer.String(value.resolvedPlanSha256);
            writer.EndObject();
        }

        private static string ModeToken(EncounterCatalogBindingMode mode)
        {
            if (mode == EncounterCatalogBindingMode.None) return "none";
            if (mode == EncounterCatalogBindingMode.Encounter) return "encounter";
            throw new InvalidOperationException("Encounter binding mode is not defined by the V7 wire contract.");
        }

        private static EncounterCatalogBindingMode ParseMode(string token, string path)
        {
            if (string.Equals(token, "none", StringComparison.Ordinal)) return EncounterCatalogBindingMode.None;
            if (string.Equals(token, "encounter", StringComparison.Ordinal)) return EncounterCatalogBindingMode.Encounter;
            throw new JsonCodecException(path, "Mode must be the exact string token 'none' or 'encounter'.");
        }

        private static void WriteStrings(JsonWriter writer, string[] values)
        {
            if (values == null) { writer.Null(); return; }
            writer.BeginArray();
            for (int i = 0; i < values.Length; i++)
            {
                if (i != 0) writer.Comma();
                writer.String(values[i]);
            }
            writer.EndArray();
        }

        private static void WriteArray<T>(JsonWriter writer, T[] values, Action<JsonWriter, T> writeItem)
        {
            if (values == null) { writer.Null(); return; }
            writer.BeginArray();
            for (int i = 0; i < values.Length; i++)
            {
                if (i != 0) writer.Comma();
                writeItem(writer, values[i]);
            }
            writer.EndArray();
        }

        private static string Child(string path, string name) => path == "/" ? "/" + EscapePath(name) : path + "/" + EscapePath(name);

        private static string EscapePath(string name) => name.Replace("~", "~0").Replace("/", "~1");

        private static Dictionary<string, JsonValue> ObjectFields(JsonValue value, string path, params string[] expectedNames)
        {
            if (value == null || value.Kind != JsonKind.Object)
                throw new JsonCodecException(path, "Expected an object with the closed V7 field set.");
            if (value.Properties.Count != expectedNames.Length)
            {
                foreach (KeyValuePair<string, JsonValue> pair in value.Properties)
                {
                    bool expected = false;
                    for (int i = 0; i < expectedNames.Length; i++)
                    {
                        if (string.Equals(expectedNames[i], pair.Key, StringComparison.Ordinal))
                        {
                            expected = true;
                            break;
                        }
                    }
                    if (!expected) throw new JsonCodecException(Child(path, pair.Key), "Unknown object field.");
                }
            }

            var result = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            for (int i = 0; i < expectedNames.Length; i++)
            {
                string name = expectedNames[i];
                if (!value.Properties.TryGetValue(name, out JsonValue field))
                    throw new JsonCodecException(Child(path, name), "Required object field is missing.");
                result.Add(name, field);
            }
            if (value.Properties.Count != expectedNames.Length)
                throw new JsonCodecException(path, "Object field count does not match the closed V7 field set.");
            return result;
        }

        private static string StringValue(JsonValue value, string path)
        {
            if (value.Kind == JsonKind.Null) return null;
            if (value.Kind != JsonKind.String) throw new JsonCodecException(path, "Expected a string or explicit null.");
            return value.String;
        }

        private static bool Boolean(JsonValue value, string path)
        {
            if (value.Kind != JsonKind.Boolean) throw new JsonCodecException(path, "Expected a JSON boolean.");
            return value.Boolean;
        }

        private static int Int32(JsonValue value, string path)
        {
            int number;
            if (value.Kind != JsonKind.Number || !int.TryParse(value.Number, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out number))
                throw new JsonCodecException(path, "Expected an in-range JSON integer.");
            return number;
        }

        private static long Int64(JsonValue value, string path)
        {
            long number;
            if (value.Kind != JsonKind.Number || !long.TryParse(value.Number, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out number))
                throw new JsonCodecException(path, "Expected an in-range JSON integer.");
            return number;
        }

        private static uint UInt32(JsonValue value, string path)
        {
            uint number;
            if (value.Kind != JsonKind.Number || !uint.TryParse(value.Number, NumberStyles.None,
                    CultureInfo.InvariantCulture, out number))
                throw new JsonCodecException(path, "Expected an in-range unsigned JSON integer.");
            return number;
        }

        private static float Single(JsonValue value, string path)
        {
            float number;
            if (value.Kind != JsonKind.Number || !float.TryParse(value.Number, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out number) || float.IsNaN(number) || float.IsInfinity(number))
                throw new JsonCodecException(path, "Expected a finite, in-range JSON number.");
            return number;
        }

        private static string[] StringArray(JsonValue value, string path) =>
            ArrayValue(value, path, ParseStringElement);

        private static string ParseStringElement(JsonValue value, string path) => StringValue(value, path);

        private static T[] ArrayValue<T>(JsonValue value, string path, Func<JsonValue, string, T> parseElement)
        {
            if (value.Kind == JsonKind.Null) return null;
            if (value.Kind != JsonKind.Array) throw new JsonCodecException(path, "Expected an array or explicit null.");
            var result = new T[value.Items.Count];
            for (int i = 0; i < result.Length; i++) result[i] = parseElement(value.Items[i], Child(path, i.ToString(CultureInfo.InvariantCulture)));
            return result;
        }

        private enum JsonKind
        {
            Null,
            Boolean,
            Number,
            String,
            Array,
            Object
        }

        private sealed class JsonValue
        {
            internal JsonKind Kind;
            internal bool Boolean;
            internal string Number;
            internal string String;
            internal List<JsonValue> Items;
            internal Dictionary<string, JsonValue> Properties;
        }

        private sealed class JsonCodecException : Exception
        {
            internal JsonCodecException(string path, string message) : base(message)
            {
                Path = path ?? "/";
            }

            internal string Path { get; }
        }

        private sealed class JsonParser
        {
            private readonly string _text;
            private readonly string _rootPath;
            private int _index;
            private int _depth;

            internal JsonParser(string text, string rootPath)
            {
                _text = text;
                _rootPath = rootPath;
            }

            internal JsonValue Parse()
            {
                SkipWhitespace();
                JsonValue value = ParseValue(_rootPath);
                SkipWhitespace();
                if (_index != _text.Length) Fail(_rootPath, "Trailing content after the JSON value.");
                return value;
            }

            private JsonValue ParseValue(string path)
            {
                if (_index >= _text.Length) Fail(path, "Unexpected end of JSON input.");
                char character = _text[_index];
                if (character == 'n') { ConsumeLiteral("null", path); return new JsonValue { Kind = JsonKind.Null }; }
                if (character == 't') { ConsumeLiteral("true", path); return new JsonValue { Kind = JsonKind.Boolean, Boolean = true }; }
                if (character == 'f') { ConsumeLiteral("false", path); return new JsonValue { Kind = JsonKind.Boolean, Boolean = false }; }
                if (character == '"') return new JsonValue { Kind = JsonKind.String, String = ParseString(path) };
                if (character == '[') return ParseArray(path);
                if (character == '{') return ParseObject(path);
                if (character == '-' || (character >= '0' && character <= '9'))
                    return new JsonValue { Kind = JsonKind.Number, Number = ParseNumber(path) };
                Fail(path, "Unexpected token in JSON value.");
                return null;
            }

            private JsonValue ParseArray(string path)
            {
                Enter(path);
                _index++;
                SkipWhitespace();
                var items = new List<JsonValue>();
                if (Take(']')) { Leave(); return new JsonValue { Kind = JsonKind.Array, Items = items }; }
                while (true)
                {
                    items.Add(ParseValue(Child(path, items.Count.ToString(CultureInfo.InvariantCulture))));
                    SkipWhitespace();
                    if (Take(']')) break;
                    if (!Take(',')) Fail(path, "Expected ',' or ']' in array.");
                    SkipWhitespace();
                }
                Leave();
                return new JsonValue { Kind = JsonKind.Array, Items = items };
            }

            private JsonValue ParseObject(string path)
            {
                Enter(path);
                _index++;
                SkipWhitespace();
                var properties = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                if (Take('}')) { Leave(); return new JsonValue { Kind = JsonKind.Object, Properties = properties }; }
                while (true)
                {
                    if (_index >= _text.Length || _text[_index] != '"') Fail(path, "Object field name must be a JSON string.");
                    string name = ParseString(path);
                    string fieldPath = Child(path, name);
                    if (properties.ContainsKey(name)) Fail(fieldPath, "Duplicate object field.");
                    SkipWhitespace();
                    if (!Take(':')) Fail(fieldPath, "Expected ':' after object field name.");
                    SkipWhitespace();
                    properties.Add(name, ParseValue(fieldPath));
                    SkipWhitespace();
                    if (Take('}')) break;
                    if (!Take(',')) Fail(path, "Expected ',' or '}' in object.");
                    SkipWhitespace();
                }
                Leave();
                return new JsonValue { Kind = JsonKind.Object, Properties = properties };
            }

            private string ParseString(string path)
            {
                _index++;
                var result = new StringBuilder();
                while (_index < _text.Length)
                {
                    char character = _text[_index++];
                    if (character == '"') return result.ToString();
                    if (character < 0x20) Fail(path, "Unescaped control character in JSON string.");
                    if (character == '\\')
                    {
                        if (_index >= _text.Length) Fail(path, "Incomplete string escape.");
                        char escaped = _text[_index++];
                        switch (escaped)
                        {
                            case '"': result.Append('"'); break;
                            case '\\': result.Append('\\'); break;
                            case '/': result.Append('/'); break;
                            case 'b': result.Append('\b'); break;
                            case 'f': result.Append('\f'); break;
                            case 'n': result.Append('\n'); break;
                            case 'r': result.Append('\r'); break;
                            case 't': result.Append('\t'); break;
                            case 'u': AppendUnicodeEscape(result, path); break;
                            default: Fail(path, "Unsupported or malformed string escape."); break;
                        }
                    }
                    else if (char.IsHighSurrogate(character))
                    {
                        if (_index >= _text.Length || !char.IsLowSurrogate(_text[_index]))
                            Fail(path, "Unpaired high surrogate in JSON string.");
                        result.Append(character);
                        result.Append(_text[_index++]);
                    }
                    else if (char.IsLowSurrogate(character))
                    {
                        Fail(path, "Unpaired low surrogate in JSON string.");
                    }
                    else
                    {
                        result.Append(character);
                    }
                }
                Fail(path, "Unterminated JSON string.");
                return null;
            }

            private void AppendUnicodeEscape(StringBuilder result, string path)
            {
                char first = ParseHexCodeUnit(path);
                if (char.IsHighSurrogate(first))
                {
                    if (_index + 1 >= _text.Length || _text[_index] != '\\' || _text[_index + 1] != 'u')
                        Fail(path, "Escaped high surrogate must be followed by an escaped low surrogate.");
                    _index += 2;
                    char second = ParseHexCodeUnit(path);
                    if (!char.IsLowSurrogate(second)) Fail(path, "Escaped surrogate pair has an invalid low surrogate.");
                    result.Append(first);
                    result.Append(second);
                    return;
                }
                if (char.IsLowSurrogate(first)) Fail(path, "Escaped low surrogate has no matching high surrogate.");
                result.Append(first);
            }

            private char ParseHexCodeUnit(string path)
            {
                if (_index + 4 > _text.Length) Fail(path, "Incomplete Unicode escape.");
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char character = _text[_index++];
                    int digit;
                    if (character >= '0' && character <= '9') digit = character - '0';
                    else if (character >= 'a' && character <= 'f') digit = character - 'a' + 10;
                    else if (character >= 'A' && character <= 'F') digit = character - 'A' + 10;
                    else { Fail(path, "Malformed hexadecimal Unicode escape."); return '\0'; }
                    value = (value << 4) | digit;
                }
                return (char)value;
            }

            private string ParseNumber(string path)
            {
                int start = _index;
                if (Take('-') && _index >= _text.Length) Fail(path, "Incomplete JSON number.");
                if (Take('0'))
                {
                    if (_index < _text.Length && IsDigit(_text[_index])) Fail(path, "Leading zero in JSON number.");
                }
                else
                {
                    if (_index >= _text.Length || _text[_index] < '1' || _text[_index] > '9')
                        Fail(path, "Malformed JSON number.");
                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }
                if (Take('.'))
                {
                    if (_index >= _text.Length || !IsDigit(_text[_index])) Fail(path, "Fraction has no digits.");
                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }
                if (Take('e') || Take('E'))
                {
                    if (!Take('+')) Take('-');
                    if (_index >= _text.Length || !IsDigit(_text[_index])) Fail(path, "Exponent has no digits.");
                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }
                return _text.Substring(start, _index - start);
            }

            private void ConsumeLiteral(string literal, string path)
            {
                if (_index + literal.Length > _text.Length ||
                    string.Compare(_text, _index, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
                    Fail(path, "Malformed JSON literal.");
                _index += literal.Length;
            }

            private void Enter(string path)
            {
                _depth++;
                if (_depth > 128) Fail(path, "JSON nesting exceeds the codec limit.");
            }

            private void Leave() { _depth--; }

            private void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char character = _text[_index];
                    if (character == ' ' || character == '\t' || character == '\r' || character == '\n') _index++;
                    else break;
                }
            }

            private bool Take(char expected)
            {
                if (_index >= _text.Length || _text[_index] != expected) return false;
                _index++;
                return true;
            }

            private static bool IsDigit(char value) => value >= '0' && value <= '9';

            private static void Fail(string path, string message) => throw new JsonCodecException(path, message);
        }

        private sealed class JsonWriter
        {
            private readonly StringBuilder _builder = new StringBuilder(1024);
            private readonly List<bool> _firstObjectFields = new List<bool>();

            internal void BeginObject()
            {
                _builder.Append('{');
                _firstObjectFields.Add(true);
            }
            internal void EndObject()
            {
                _builder.Append('}');
                _firstObjectFields.RemoveAt(_firstObjectFields.Count - 1);
            }
            internal void BeginArray() { _builder.Append('['); }
            internal void EndArray() { _builder.Append(']'); }
            internal void Comma() { _builder.Append(','); }
            internal void Name(string value)
            {
                int index = _firstObjectFields.Count - 1;
                if (!_firstObjectFields[index]) _builder.Append(',');
                _firstObjectFields[index] = false;
                String(value);
                _builder.Append(':');
            }
            internal void Null() { _builder.Append("null"); }
            internal void Boolean(bool value) { _builder.Append(value ? "true" : "false"); }
            internal void Number(int value) { _builder.Append(value.ToString(CultureInfo.InvariantCulture)); }
            internal void Number(long value) { _builder.Append(value.ToString(CultureInfo.InvariantCulture)); }
            internal void Number(uint value) { _builder.Append(value.ToString(CultureInfo.InvariantCulture)); }
            internal void Number(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidOperationException("JSON number must be finite.");
                _builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
            internal void String(string value)
            {
                if (value == null) { Null(); return; }
                _builder.Append('"');
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    switch (character)
                    {
                        case '"': _builder.Append("\\\""); break;
                        case '\\': _builder.Append("\\\\"); break;
                        case '\b': _builder.Append("\\b"); break;
                        case '\f': _builder.Append("\\f"); break;
                        case '\n': _builder.Append("\\n"); break;
                        case '\r': _builder.Append("\\r"); break;
                        case '\t': _builder.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                _builder.Append("\\u00");
                                _builder.Append(((int)character).ToString("x2", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                _builder.Append(character);
                            }
                            break;
                    }
                }
                _builder.Append('"');
            }
            public override string ToString() => _builder.ToString();
        }
    }
}
