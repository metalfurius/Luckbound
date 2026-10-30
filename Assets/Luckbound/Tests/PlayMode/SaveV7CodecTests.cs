using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveV7CodecTests
    {
        private const string GoldenSettingsPreimage =
            "{\"schemaVersion\":1,\"contentVersion\":7,\"generation\":1,\"sequence\":42,\"savedAtUtc\":\"2026-10-30T12:34:56.0000000Z\",\"checksum\":\"\",\"settings\":{\"languageId\":\"es\",\"masterVolume\":1,\"musicVolume\":0.65,\"effectsVolume\":0.8,\"ambienceVolume\":0.45}}";

        private const string GoldenSettingsChecksum =
            "cd5dc92f9d160e3d01e8c5338c9fed7ba4c94f8e84a7caf14d888b7660720e89";

        [Test]
        public void SettingsVector_UsesIndependentBytesAndSha256()
        {
            SettingsSaveEnvelopeV7 input = CreateSettings();
            byte[] actual = SaveV7JsonCodec.EncodeSettings(input);
            string expectedJson = GoldenSettingsPreimage.Replace(
                "\"checksum\":\"\"", "\"checksum\":\"" + GoldenSettingsChecksum + "\"");

            CollectionAssert.AreEqual(StrictUtf8Encoding().GetBytes(expectedJson), actual);
            Assert.That(Sha256(StrictUtf8Encoding().GetBytes(GoldenSettingsPreimage)), Is.EqualTo(GoldenSettingsChecksum));
            Assert.That(input.checksum, Is.EqualTo(string.Empty), "Encode must not write its generated checksum into the input DTO.");
            Assert.That(Encoding.UTF8.GetString(actual), Does.Not.Contain(" "));
        }

        [Test]
        public void SettingsVector_IsInvariantAcrossCulturesAndRoundTrips()
        {
            SettingsSaveEnvelopeV7 input = CreateSettings();
            CultureInfo oldCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo oldUiCulture = Thread.CurrentThread.CurrentUICulture;
            byte[] englishBytes = null;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                englishBytes = SaveV7JsonCodec.EncodeSettings(input);
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                byte[] frenchBytes = SaveV7JsonCodec.EncodeSettings(input);
                CollectionAssert.AreEqual(englishBytes, frenchBytes);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCulture;
                Thread.CurrentThread.CurrentUICulture = oldUiCulture;
            }

            bool decoded = SaveV7JsonCodec.TryDecodeSettings(englishBytes,
                out SettingsSaveEnvelopeV7 result, out SaveV7Error error);
            Assert.That(decoded, Is.True, error.Message);
            Assert.That(result.checksum, Is.EqualTo(GoldenSettingsChecksum));
            Assert.That(result.settings.musicVolume, Is.EqualTo(0.65f));
            CollectionAssert.AreEqual(englishBytes, SaveV7JsonCodec.EncodeSettings(result));
        }

        [Test]
        public void SettingsEncode_RequiresAnEmptyOrWellFormedInputChecksum()
        {
            SettingsSaveEnvelopeV7 invalid = CreateSettings();
            invalid.checksum = "not-a-sha256";

            SaveV7ValidationException exception = Assert.Throws<SaveV7ValidationException>(
                () => SaveV7JsonCodec.EncodeSettings(invalid));
            Assert.That(exception.Error.Code, Is.EqualTo(SaveV7ErrorCode.Corrupted));
            Assert.That(invalid.checksum, Is.EqualTo("not-a-sha256"));
        }

        [Test]
        public void ProfileRoundTrip_PreservesSixteenClosedDtosAndTypedPin()
        {
            ProfileSaveEnvelopeV7 input = CreateValidProfile();
            string originalChecksum = input.checksum;
            string originalCheckpoint = input.activeExpedition.roomEntryCheckpointJson;
            string[] originalRaces = (string[])input.profile.unlockedRaceIds.Clone();
            byte[] bytes = SaveV7JsonCodec.EncodeProfile(input);
            string json = Encoding.UTF8.GetString(bytes);

            Assert.That(input.checksum, Is.EqualTo(originalChecksum));
            Assert.That(input.activeExpedition.roomEntryCheckpointJson, Is.EqualTo(originalCheckpoint));
            Assert.That(json, Does.Contain("\"mode\":\"none\""));
            Assert.That(json, Does.Contain("\"calendarOrdinal\":0"));
            Assert.That(json, Does.Contain("\"encounterPlanPin\":{\"schemaVersion\":1"));
            Assert.That(json, Does.Not.Contain("\"encounterId\""));
            Assert.That(json, Does.Not.Contain("\"definitionRevision\""));
            Assert.That(json, Does.Not.Contain("\"definitionSha256\""));
            Assert.That(json, Does.Not.Contain("\"variantId\""));

            bool decoded = SaveV7JsonCodec.TryDecodeProfile(bytes,
                out ProfileSaveEnvelopeV7 result, out SaveV7Error error);
            Assert.That(decoded, Is.True, error.Message);
            Assert.That(SaveV7JsonCodec.TryDecodeCheckpoint(Encoding.UTF8.GetBytes(result.activeExpedition.roomEntryCheckpointJson),
                out ExpeditionRoomCheckpointSaveDataV7 checkpoint, out error), Is.True, error.Message);
            Assert.That(checkpoint.encounterEntry.nodeId, Is.EqualTo(result.activeExpedition.currentNodeId));
            Assert.That(checkpoint.encounterEntry.resolvedPlanSha256, Is.EqualTo(result.activeExpedition.encounterPlanPin.entry.resolvedPlanSha256));
            Assert.That(result.activeExpedition.encounterPlanPin, Is.TypeOf<EncounterPlanPinSaveDataV7>());
            Assert.That(result.activeExpedition.encounterPlanPin.bindings[0],
                Is.TypeOf<EncounterPlanNodeBindingSaveDataV7>());
            Assert.That(result.activeExpedition.encounterPlanPin.entry,
                Is.TypeOf<EncounterPlanEntryReferenceSaveDataV7>());
            CollectionAssert.AreEqual(originalRaces, result.profile.unlockedRaceIds);
            CollectionAssert.AreEqual(new[] { "sample.patio.combat", "sample.tutorial" },
                new[]
                {
                    result.activeExpedition.encounterPlanPin.bindings[0].nodeId,
                    result.activeExpedition.encounterPlanPin.bindings[1].nodeId
                });
            Assert.That(result.activeExpedition.encounterPlanPin.bindings[0].calendarOrdinal, Is.EqualTo(0));
            Assert.That(result.activeExpedition.roomEntryCheckpointJson, Is.EqualTo(originalCheckpoint));
            Assert.That(result.activeExpedition.completedRoomResults[0].resultJson, Is.EqualTo("{}"));
            Assert.That(result.activeExpedition.frozenPermanentUpgrades.energyUpgradeLevel, Is.EqualTo(1));
            Assert.That(result.activeExpedition.fortuneOfferBookSnapshot.offers[0].history[0].optionIds[0],
                Is.EqualTo("option.one"));
            Assert.That(result.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts[0].transactionId,
                Is.EqualTo("bbbbbbbbbbbb4bbb8bbbbbbbbbbbbbbb"));
            Assert.That(result.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].itemInstanceId,
                Is.EqualTo("dddddddddddd4ddd8ddddddddddddddd"));
            Assert.That(result.profile.contractCounters[0].progress, Is.EqualTo(4));
            Assert.That(result.profile.processedContractProgressEvents[0].progressEvent,
                Is.EqualTo((int)ContractProgressEvent.CombatRoomCompleted));
            Assert.That(result.profile.progressionPurchaseTransactions[0].cost, Is.EqualTo(15));
            Assert.That(result.activeExpedition.encounterPlanPin.bindings[0].contextPactIds[0],
                Is.EqualTo("pact.avarice"));
            Assert.That(SaveV7JsonCodec.TryAuthenticateProfile(result, out error), Is.True, error.Message);
            CollectionAssert.AreEqual(bytes, SaveV7JsonCodec.EncodeProfile(result));
        }

        [Test]
        public void ProfileEncode_EmitsExplicitEncounterPayloadOnlyForEncounterMode()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            EncounterPlanNodeBindingSaveDataV7 binding = profile.activeExpedition.encounterPlanPin.bindings[0];
            binding.mode = EncounterCatalogBindingMode.Encounter;
            binding.encounterId = "encounter.fixture";
            binding.definitionRevision = 3;
            binding.definitionSha256 = new string('d', 64);
            binding.variantId = "variant.fixture";

            byte[] bytes = SaveV7JsonCodec.EncodeProfile(profile);
            string json = Encoding.UTF8.GetString(bytes);
            Assert.That(json, Does.Contain("\"mode\":\"encounter\""));
            Assert.That(json, Does.Contain("\"encounterId\":\"encounter.fixture\""));
            Assert.That(json, Does.Contain("\"definitionRevision\":3"));
            Assert.That(json, Does.Contain("\"definitionSha256\":\"" + new string('d', 64) + "\""));
            Assert.That(json, Does.Contain("\"variantId\":\"variant.fixture\""));

            bool decoded = SaveV7JsonCodec.TryDecodeProfile(bytes,
                out ProfileSaveEnvelopeV7 result, out SaveV7Error error);
            Assert.That(decoded, Is.True, error.Message);
            Assert.That(result.activeExpedition.encounterPlanPin.bindings[0].mode,
                Is.EqualTo(EncounterCatalogBindingMode.Encounter));
        }

        [Test]
        public void ProfileEncode_EscapesDeterministicallyAndPreservesOpaqueUnicode()
        {
            ProfileSaveEnvelopeV7 input = CreateValidProfile();
            string opaque = "opaque \"quote\" slash\\ backspace\b tab\t line\n control\u0001 café \ud83d\ude00";
            input.activeExpedition.inventorySnapshotJson = opaque;
            byte[] bytes = SaveV7JsonCodec.EncodeProfile(input);
            string json = Encoding.UTF8.GetString(bytes);

            Assert.That(json, Does.Contain("\\\"quote\\\""));
            Assert.That(json, Does.Contain("\\\\"));
            Assert.That(json, Does.Contain("\\b"));
            Assert.That(json, Does.Contain("\\t"));
            Assert.That(json, Does.Contain("\\n"));
            Assert.That(json, Does.Contain("\\u0001"));
            Assert.That(json, Does.Contain("café"));
            Assert.That(json, Does.Contain("😀"));

            bool decoded = SaveV7JsonCodec.TryDecodeProfile(bytes,
                out ProfileSaveEnvelopeV7 result, out SaveV7Error error);
            Assert.That(decoded, Is.True, error.Message);
            Assert.That(result.activeExpedition.inventorySnapshotJson, Is.EqualTo(opaque));
        }

        [Test]
        public void ProfileEncode_RejectsUnpairedSurrogateWithoutMutatingInput()
        {
            ProfileSaveEnvelopeV7 input = CreateValidProfile();
            string invalid = "opaque" + '\ud800';
            input.activeExpedition.inventorySnapshotJson = invalid;

            SaveV7ValidationException exception = Assert.Throws<SaveV7ValidationException>(
                () => SaveV7JsonCodec.EncodeProfile(input));
            Assert.That(exception.Error.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData));
            Assert.That(input.activeExpedition.inventorySnapshotJson, Is.EqualTo(invalid));
        }

        [Test]
        public void EmbeddedCheckpointMustMatchThePinFromTheSameProfile()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(
                profile.activeExpedition.encounterPlanPin.bindings[0].resolvedPlanSha256);
            checkpoint.encounterEntry.nodeId = "sample.tutorial";
            profile.activeExpedition.roomEntryCheckpointJson =
                Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeCheckpoint(checkpoint));

            SaveV7ValidationException exception = Assert.Throws<SaveV7ValidationException>(
                () => SaveV7JsonCodec.EncodeProfile(profile));
            Assert.That(exception.Error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
        }

        [Test]
        public void ProfileRoundTrip_DoesNotCreateCheckpointForCompletedOrInactiveRuns()
        {
            ProfileSaveEnvelopeV7 completed = CreateValidProfile();
            completed.activeExpedition.completedRoomIds = new[] { "sample.tutorial", "sample.patio.combat" };
            completed.activeExpedition.completedRoomResults = new[]
            {
                completed.activeExpedition.completedRoomResults[0],
                new ExpeditionRoomCompletionSaveDataV7
                {
                    roomNodeId = "sample.patio.combat",
                    resultId = "result.combat.fixture",
                    resultJson = "{}"
                }
            };
            completed.activeExpedition.roomEntryCheckpointJson = string.Empty;

            byte[] completedBytes = SaveV7JsonCodec.EncodeProfile(completed);
            bool completedDecoded = SaveV7JsonCodec.TryDecodeProfile(completedBytes,
                out ProfileSaveEnvelopeV7 completedResult, out SaveV7Error completedError);
            Assert.That(completedDecoded, Is.True, completedError.Message);
            Assert.That(completedResult.activeExpedition.roomEntryCheckpointJson, Is.EqualTo(string.Empty));

            ProfileSaveEnvelopeV7 inactive = CreateValidProfile();
            inactive.hasActiveExpedition = false;
            inactive.activeExpedition = null;
            byte[] inactiveBytes = SaveV7JsonCodec.EncodeProfile(inactive);
            bool inactiveDecoded = SaveV7JsonCodec.TryDecodeProfile(inactiveBytes,
                out ProfileSaveEnvelopeV7 inactiveResult, out SaveV7Error inactiveError);
            Assert.That(inactiveDecoded, Is.True, inactiveError.Message);
            Assert.That(inactiveResult.activeExpedition, Is.Null);
        }

        [Test]
        public void CheckpointCodec_IsTypedAndStrictWithoutAnIndependentChecksum()
        {
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(new string('b', 64));
            byte[] bytes = SaveV7JsonCodec.EncodeCheckpoint(checkpoint);
            bool decoded = SaveV7JsonCodec.TryDecodeCheckpoint(bytes,
                out ExpeditionRoomCheckpointSaveDataV7 result, out SaveV7Error error);

            Assert.That(decoded, Is.True, error.Message);
            Assert.That(result.encounterEntry, Is.TypeOf<EncounterPlanEntryReferenceSaveDataV7>());
            Assert.That(result.encounterEntry.resolvedPlanSha256, Is.EqualTo(new string('b', 64)));
            CollectionAssert.AreEqual(checkpoint.pactIds, result.pactIds);
            Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("\"encounterEntry\""));
        }

        [Test]
        public void StrictReaderRejectsInvalidUtf8SurrogatesUnknownDuplicateMissingAndTrailingData()
        {
            byte[] invalidUtf8 = { 0xc3, 0x28 };
            AssertRejectedSettings(invalidUtf8);

            string valid = Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeSettings(CreateSettings()));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("\"languageId\":\"es\"",
                "\"languageId\":\"\\uD800\"")));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("\"settings\":{",
                "\"settings\":{\"unknown\":0,")));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("{\"schemaVersion\":1",
                "{\"schemaVersion\":1,\"schemaVersion\":1")));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace(",\"ambienceVolume\":0.45", string.Empty)));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid + "null"));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("\"generation\":1",
                "\"generation\":2147483648")));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("\"masterVolume\":1",
                "\"masterVolume\":true")));
            AssertRejectedSettings(StrictUtf8Encoding().GetBytes(valid.Replace("\"masterVolume\":1",
                "\"masterVolume\":1e999")));
        }

        [Test]
        public void StrictReaderRejectsModeNumbersCasingAndNonePayloadKeys()
        {
            string json = Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeProfile(CreateValidProfile()));
            AssertRejectedProfile(StrictUtf8Encoding().GetBytes(json.Replace("\"mode\":\"none\"", "\"mode\":1")));
            AssertRejectedProfile(StrictUtf8Encoding().GetBytes(json.Replace("\"mode\":\"none\"", "\"mode\":\"None\"")));
            AssertRejectedProfile(StrictUtf8Encoding().GetBytes(json.Replace("\"mode\":\"none\"",
                "\"mode\":\"none\",\"encounterId\":null,\"definitionRevision\":0")));
            AssertRejectedProfile(StrictUtf8Encoding().GetBytes(json.Replace("\"mode\":\"none\"",
                "\"mode\":\"none\",\"calendarOrdinal\":1")));
        }

        [Test]
        public void ChecksumTamperingAndSemanticMutationAreRejectedWithoutRepair()
        {
            byte[] encoded = SaveV7JsonCodec.EncodeSettings(CreateSettings());
            string json = Encoding.UTF8.GetString(encoded);
            byte[] tampered = StrictUtf8Encoding().GetBytes(json.Replace("\"musicVolume\":0.65", "\"musicVolume\":0.64"));
            AssertRejectedSettings(tampered);

            bool authenticated = SaveV7JsonCodec.TryDecodeSettings(encoded,
                out SettingsSaveEnvelopeV7 decoded, out SaveV7Error error);
            Assert.That(authenticated, Is.True, error.Message);
            string originalChecksum = decoded.checksum;
            decoded.settings.musicVolume = 0.64f;
            Assert.That(decoded.checksum, Is.EqualTo(originalChecksum));
            Assert.That(SaveV7JsonCodec.TryDecodeSettings(tampered,
                out SettingsSaveEnvelopeV7 ignored, out SaveV7Error tamperError), Is.False);
            Assert.That(tamperError.Code, Is.EqualTo(SaveV7ErrorCode.Corrupted));
        }

        [Test]
        public void ProfileAuthenticationRejectsMutationWithoutReplacingChecksum()
        {
            byte[] bytes = SaveV7JsonCodec.EncodeProfile(CreateValidProfile());
            bool decoded = SaveV7JsonCodec.TryDecodeProfile(bytes,
                out ProfileSaveEnvelopeV7 profile, out SaveV7Error error);
            Assert.That(decoded, Is.True, error.Message);
            string checksum = profile.checksum;
            profile.activeExpedition.encounterPlanPin.bindings[0].resolvedPlanSha256 = new string('e', 64);

            Assert.That(SaveV7JsonCodec.TryAuthenticateProfile(profile, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.Corrupted));
            Assert.That(profile.checksum, Is.EqualTo(checksum));
        }

        [Test]
        public void NullArrayIsNotNormalizedToEmptyByEncode()
        {
            ProfileSaveEnvelopeV7 input = CreateValidProfile();
            input.profile.unlockedStartingKitIds = null;

            SaveV7ValidationException exception = Assert.Throws<SaveV7ValidationException>(
                () => SaveV7JsonCodec.EncodeProfile(input));
            Assert.That(exception.Error.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData));
            Assert.That(input.profile.unlockedStartingKitIds, Is.Null);
        }

        private static SettingsSaveEnvelopeV7 CreateSettings()
        {
            return new SettingsSaveEnvelopeV7
            {
                schemaVersion = 1,
                contentVersion = 7,
                generation = 1,
                sequence = 42,
                savedAtUtc = "2026-10-30T12:34:56.0000000Z",
                checksum = string.Empty,
                settings = new GameSettingsSaveDataV7
                {
                    languageId = "es",
                    masterVolume = 1f,
                    musicVolume = 0.65f,
                    effectsVolume = 0.8f,
                    ambienceVolume = 0.45f
                }
            };
        }

        private static ProfileSaveEnvelopeV7 CreateValidProfile()
        {
            const uint seed = 0x31415926u;
            const string runText = "0123456789abcdef0123456789abcdef";
            const string tutorialNode = "sample.tutorial";
            const string currentNode = "sample.patio.combat";
            const string tutorialPlanSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string currentPlanSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            ExpeditionRoutePlan route = CreateRoute(seed);
            RunId runId = RunId.Parse(runText);
            FortuneOfferBookSnapshotV7 book = CreateFortuneBook(seed);

            var bindings = new[]
            {
                CreateBinding(runId, currentNode, "room.patio.c1", currentPlanSha),
                CreateBinding(runId, tutorialNode, "room.sample.tutorial", tutorialPlanSha)
            };
            var pin = new EncounterPlanPinSaveDataV7
            {
                schemaVersion = 1,
                runId = runText,
                catalogId = "catalog.fixture",
                catalogRevision = 1,
                catalogSha256 = new string('c', 64),
                routeSha256 = Sha256(StrictUtf8Encoding().GetBytes(route.CanonicalForm)),
                identityCodec = "encounter-spawn.v1",
                bindings = bindings,
                entry = new EncounterPlanEntryReferenceSaveDataV7
                {
                    nodeId = currentNode,
                    resolvedPlanSha256 = currentPlanSha
                }
            };

            var checkpoint = new ExpeditionRoomCheckpointSaveDataV7
            {
                roomNodeId = currentNode,
                inventorySnapshotJson = "{\"items\":[]}",
                maximumHealth = 100,
                currentHealth = 88,
                maximumEnergy = 50,
                currentEnergy = 45,
                healthPotionCharges = 1,
                accumulatedRenown = 3,
                fortuneOfferBookSnapshot = SaveV7Clone.Clone(book),
                pactIds = new[] { "pact.avarice" },
                collectedItemIds = new[] { "item.fixture" },
                claimedRewardIds = Array.Empty<string>(),
                encounterEntry = new EncounterPlanEntryReferenceSaveDataV7
                {
                    nodeId = currentNode,
                    resolvedPlanSha256 = currentPlanSha
                }
            };

            return new ProfileSaveEnvelopeV7
            {
                schemaVersion = 1,
                contentVersion = 7,
                generation = 1,
                sequence = 42,
                savedAtUtc = "2026-10-30T12:34:56.0000000Z",
                checksum = string.Empty,
                profile = new ProfileSaveDataV7
                {
                    availableRenown = 27,
                    healthUpgradeLevel = 1,
                    energyUpgradeLevel = 0,
                    additionalPotionUpgradeLevel = 0,
                    unlockedRaceIds = new[] { "race.orcini", "race.human" },
                    unlockedStartingKitIds = new[] { "kit.bellator" },
                    acceptedContractIds = new[] { "contract.fixture" },
                    claimedContractIds = Array.Empty<string>(),
                    readStoryFragmentIds = new[] { "story.fixture" },
                    contractCounters = new[]
                    {
                        new ContractCounterSaveDataV7 { contractId = "contract.fixture", progress = 4 }
                    },
                    processedSettlementRunIds = Array.Empty<string>(),
                    processedContractProgressEvents = new[]
                    {
                        new ContractProgressEventReceiptSaveDataV7
                        {
                            eventId = "event.fixture:1",
                            progressEvent = (int)ContractProgressEvent.CombatRoomCompleted
                        }
                    },
                    progressionPurchaseTransactions = new[]
                    {
                        new PermanentUpgradePurchaseSaveDataV7
                        {
                            transactionId = "aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa",
                            kind = (int)PermanentUpgradeKind.MaximumHealth,
                            cost = 15
                        }
                    }
                },
                hasActiveExpedition = true,
                activeExpedition = new ExpeditionSaveDataV7
                {
                    runId = runText,
                    seed = seed,
                    generationVersion = ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                    contentVersion = 7,
                    classId = "class.bellator",
                    raceId = ExpeditionRunConfiguration.HumanRaceId,
                    selectedSkillIds = new[] { "skill.fixture.one", "skill.fixture.two" },
                    startingEquipmentInstanceIds = new[]
                    {
                        "11111111111141118111111111111111", "22222222222242228222222222222222"
                    },
                    routeSnapshotJson = route.CanonicalForm,
                    currentNodeId = currentNode,
                    visitedNodeIds = new[] { tutorialNode, currentNode },
                    completedRoomIds = new[] { tutorialNode },
                    completedRoomResults = new[]
                    {
                        new ExpeditionRoomCompletionSaveDataV7
                        {
                            roomNodeId = tutorialNode,
                            resultId = "result.tutorial.fixture",
                            resultJson = "{}"
                        }
                    },
                    roomEntryCheckpointJson = Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeCheckpoint(checkpoint)),
                    inventorySnapshotJson = "{\"items\":[]}",
                    maximumHealth = 100,
                    currentHealth = 88,
                    maximumEnergy = 50,
                    currentEnergy = 45,
                    healthPotionCharges = 1,
                    frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveDataV7
                    {
                        healthUpgradeLevel = 0,
                        energyUpgradeLevel = 1,
                        additionalPotionUpgradeLevel = 0
                    },
                    hasFrozenPermanentUpgrades = true,
                    accumulatedRenown = 3,
                    pactIds = new[] { "pact.avarice" },
                    fortuneOfferBookSnapshot = book,
                    collectedItemIds = new[] { "item.fixture" },
                    claimedRewardIds = Array.Empty<string>(),
                    encounterPlanPin = pin
                }
            };
        }

        private static ExpeditionRoomCheckpointSaveDataV7 CreateCheckpoint(string entrySha)
        {
            return new ExpeditionRoomCheckpointSaveDataV7
            {
                roomNodeId = "sample.patio.combat",
                inventorySnapshotJson = "{\"items\":[]}",
                maximumHealth = 100,
                currentHealth = 88,
                maximumEnergy = 50,
                currentEnergy = 45,
                healthPotionCharges = 1,
                accumulatedRenown = 3,
                fortuneOfferBookSnapshot = CreateFortuneBook(0x31415926u),
                pactIds = new[] { "pact.avarice" },
                collectedItemIds = new[] { "item.fixture" },
                claimedRewardIds = Array.Empty<string>(),
                encounterEntry = new EncounterPlanEntryReferenceSaveDataV7
                {
                    nodeId = "sample.patio.combat",
                    resolvedPlanSha256 = entrySha
                }
            };
        }

        private static FortuneOfferBookSnapshotV7 CreateFortuneBook(uint seed)
        {
            return new FortuneOfferBookSnapshotV7
            {
                snapshotVersion = 3,
                generationVersion = ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                seed = seed,
                raceId = ExpeditionRunConfiguration.HumanRaceId,
                charges = 1,
                offers = new[]
                {
                    new FortuneOfferSnapshotV7
                    {
                        offerId = "offer.fixture",
                        contextId = "context.fixture",
                        eligibleOptionIds = new[] { "option.four", "option.one", "option.three", "option.two" },
                        history = new[]
                        {
                            new FortuneOfferHistorySnapshotV7 { optionIds = new[] { "option.one", "option.three", "option.two" } },
                            new FortuneOfferHistorySnapshotV7 { optionIds = new[] { "option.four", "option.one", "option.two" } }
                        },
                        selectedOptionId = "option.one",
                        claimed = false
                    }
                },
                processedGuardianIds = Array.Empty<string>(),
                rerollReceipts = new[]
                {
                    new FortuneRerollReceiptSnapshotV7
                    {
                        transactionId = "bbbbbbbbbbbb4bbb8bbbbbbbbbbbbbbb",
                        offerId = "offer.fixture",
                        status = 0,
                        contextId = "context.fixture",
                        optionIds = new[] { "option.four", "option.one", "option.two" },
                        selectedOptionId = string.Empty,
                        rerollCount = 1,
                        claimed = false,
                        remainingCharges = 1,
                        hasOffer = true
                    }
                },
                fortuneBrewReceipts = new[]
                {
                    new FortuneBrewReceiptSnapshotV7
                    {
                        transactionId = "cccccccccccc4ccc8ccccccccccccccc",
                        itemInstanceId = "dddddddddddd4ddd8ddddddddddddddd",
                        status = 0,
                        currentCharges = 1
                    }
                }
            };
        }

        private static EncounterPlanNodeBindingSaveDataV7 CreateBinding(
            RunId runId, string nodeId, string templateId, string resolvedPlanSha)
        {
            return new EncounterPlanNodeBindingSaveDataV7
            {
                nodeId = nodeId,
                generationVersion = ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                templateId = templateId,
                roomInstanceId = ExpeditionRoomIdentity.ForNode(runId, nodeId).ToString(),
                mode = EncounterCatalogBindingMode.None,
                encounterId = null,
                definitionRevision = 0,
                definitionSha256 = null,
                variantId = null,
                contextPactIds = new[] { "pact.avarice" },
                calendarOrdinal = 0,
                resolvedPlanSha256 = resolvedPlanSha
            };
        }

        private static ExpeditionRoutePlan CreateRoute(uint seed)
        {
            string[] nodeIds =
            {
                "sample.tutorial", "sample.patio.combat", "sample.patio.exploration",
                "sample.patio.guardian", "sample.safe_extraction"
            };
            string[] templateIds =
            {
                "room.sample.tutorial", "room.patio.c1", "room.patio.e1",
                "room.patio.guardian", "room.sample.extraction"
            };
            ExpeditionRouteNodeKind[] kinds =
            {
                ExpeditionRouteNodeKind.Tutorial, ExpeditionRouteNodeKind.Combat,
                ExpeditionRouteNodeKind.Exploration, ExpeditionRouteNodeKind.Guardian,
                ExpeditionRouteNodeKind.SafeExtraction
            };
            var nodes = new ExpeditionSampleRouteNodeDefinition[nodeIds.Length];
            var connections = new ExpeditionSampleRouteConnectionDefinition[nodeIds.Length - 1];
            for (int i = 0; i < nodeIds.Length; i++)
            {
                nodes[i] = new ExpeditionSampleRouteNodeDefinition(nodeIds[i], "patio", templateIds[i], kinds[i],
                    true, i, ExpeditionSampleRouteDefinition.EntryPortId, ExpeditionSampleRouteDefinition.ExitPortId);
                if (i < connections.Length)
                {
                    connections[i] = new ExpeditionSampleRouteConnectionDefinition(nodeIds[i],
                        ExpeditionSampleRouteDefinition.ExitPortId, nodeIds[i + 1],
                        ExpeditionSampleRouteDefinition.EntryPortId, false);
                }
            }
            var definition = new ExpeditionSampleRouteDefinition(
                ExpeditionSampleRouteDefinition.RequiredSchemaVersion,
                ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                nodes,
                connections,
                nodeIds);
            return ExpeditionSampleRouteGenerator.Generate(seed, definition);
        }

        private static UTF8Encoding StrictUtf8Encoding() => new UTF8Encoding(false, true);

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var result = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                    result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void AssertRejectedSettings(byte[] bytes)
        {
            bool decoded = SaveV7JsonCodec.TryDecodeSettings(bytes,
                out SettingsSaveEnvelopeV7 value, out SaveV7Error error);
            Assert.That(decoded, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(error.Code, Is.Not.EqualTo(SaveV7ErrorCode.None));
        }

        private static void AssertRejectedProfile(byte[] bytes)
        {
            bool decoded = SaveV7JsonCodec.TryDecodeProfile(bytes,
                out ProfileSaveEnvelopeV7 value, out SaveV7Error error);
            Assert.That(decoded, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(error.Code, Is.Not.EqualTo(SaveV7ErrorCode.None));
        }
    }
}
