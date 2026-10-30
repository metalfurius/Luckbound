using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Luckbound.App;
using Luckbound.App.Compatibility;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveV6CharacterizationTests
    {
        private const string ProfileInputFixture = "V6-P0-profile-no-active.InputOnly.json";
        private const string SettingsInputFixture = "V6-S0-settings.InputOnly.json";
        private const string FixedSavedAtUtc = "2000-01-01T00:00:00Z";
        private const uint FixedExpeditionSeed = 17320508u;
        private const string V6CaptureArgument = "-luckboundV6CaptureDir";
        private const string V6CaptureExpectedUnityVersionArgument = "-luckboundV6ExpectedUnityVersion";
        private const string V6CaptureUnityVersion = "6000.6.6f1";
        private const string V6CaptureAlternateUnityVersion = "6000.6.7f1";
        private const string V6CaptureRelativePrefix = "Logs/Scenarios/";
        private static readonly UTF8Encoding V6CaptureUtf8NoBom = new UTF8Encoding(false);
        private static readonly object V6CaptureGate = new object();
        private static readonly string[] V6CaptureExpectedCaseIds =
        {
            "V6-P0-profile-no-active",
            "V6-P1-active-known-zero",
            "V6-P1-active-known-nontrivial",
            "V6-P2-active-unknown-null",
            "V6-P2-active-unknown-inline-empty",
            "V6-P3-receipts-full",
            "V6-P4-null-empty-boundary",
            "V6-P5-flag-payload-conflict",
            "V6-S0-settings",
            "V6-checkpoint-empty-slots",
            "V6-checkpoint-null-slots"
        };
        private static readonly string[] V6CaptureSourcePaths =
        {
            "Assets/Luckbound/Scripts/App/Compatibility/SaveV6HistoricalDtos.cs",
            "Assets/Luckbound/Scripts/App/Compatibility/SaveV6ChecksumInspector.cs",
            "Assets/Luckbound/Scripts/Core/Saves/SaveDocuments.cs",
            "Assets/Luckbound/Scripts/Core/Fortune/FortuneOfferBookSnapshot.cs",
            "Assets/Luckbound/Scripts/App/SaveRepository.cs",
            "Assets/Luckbound/Scripts/App/AssemblyInfo.cs",
            "Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs",
            "Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json",
            "Assets/Luckbound/Scripts/App/Luckbound.App.asmdef",
            "Assets/Luckbound/Scripts/Core/Luckbound.Core.asmdef",
            "Assets/Luckbound/Tests/PlayMode/Luckbound.PlayMode.Tests.asmdef",
            "ProjectSettings/ProjectVersion.txt",
            "Packages/manifest.json",
            "Packages/packages-lock.json"
        };
        private static V6CaptureSessionState _v6CaptureSession;

        private static readonly string[] FortuneOptionIds =
        {
            "consumable.health_potion",
            "consumable.energy_tonic",
            "consumable.antidote",
            "consumable.bomb",
            "consumable.fortune_brew",
            "consumable.torch"
        };

        [OneTimeSetUp]
        public void InitializeV6CaptureIfRequested()
        {
            string relativeCapturePath;
            string expectedUnityVersion;
            if (!TryReadV6CaptureArguments(out relativeCapturePath, out expectedUnityVersion)) return;
            if (!IsSupportedV6CaptureUnityVersion(expectedUnityVersion))
                throw new InvalidOperationException("V6 capture expected Unity version is not allowlisted: " + expectedUnityVersion + ".");

            string runtimeVersion = Application.unityVersion;
            string projectRoot = ResolveProjectRoot();
            string declaredEditorVersion = ReadDeclaredEditorVersion(projectRoot);
            if (!string.Equals(runtimeVersion, expectedUnityVersion, StringComparison.Ordinal) ||
                !string.Equals(declaredEditorVersion, expectedUnityVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("V6 capture requires expected Unity " + expectedUnityVersion +
                    " to match Application.unityVersion and ProjectSettings/ProjectVersion.txt m_EditorVersion; observed runtime=" +
                    runtimeVersion + ", projectVersionDeclared=" + declaredEditorVersion + ".");

            string commit = Environment.GetEnvironmentVariable("GITHUB_SHA");
            if (!IsSha40(commit))
                throw new InvalidOperationException("V6 capture requires the public GITHUB_SHA value to be a 40-character commit SHA.");
            commit = commit.ToLowerInvariant();

            string captureRoot = ResolveAndCreateCaptureRoot(projectRoot, relativeCapturePath);
            V6CaptureSourceHash[] sourceHashesBefore = HashCaptureSources(projectRoot, false);
            V6CaptureRuntimeIdentity runtimeIdentity = ReadJsonUtilityIdentity();
            CultureInfo culture = CultureInfo.CurrentCulture;
            string sessionId = Guid.NewGuid().ToString("N");
            string startedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var session = new V6CaptureSessionState
            {
                projectRoot = projectRoot,
                captureRoot = captureRoot,
                sessionId = sessionId,
                commit = commit,
                unityVersion = runtimeVersion,
                projectVersionDeclared = declaredEditorVersion,
                startedAtUtc = startedAtUtc,
                activationArgument = V6CaptureArgument,
                activationValue = relativeCapturePath,
                sourceHashesBefore = sourceHashesBefore,
                runtimeIdentity = runtimeIdentity,
                cultureName = culture.Name,
                cultureNumberDecimalSeparator = culture.NumberFormat.NumberDecimalSeparator
            };

            var document = CreateSessionDocument(session);
            string sessionPath = Path.Combine(captureRoot, "session.json");
            WriteExclusiveAndVerify(sessionPath,
                V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(document, true)));
            byte[] sessionBytes = File.ReadAllBytes(sessionPath);
            session.sessionFileBytes = sessionBytes.LongLength;
            session.sessionFileSha256 = ComputeSha256(sessionBytes);
            lock (V6CaptureGate)
            {
                if (_v6CaptureSession != null)
                    throw new InvalidOperationException("A V6 capture session is already active in this fixture process.");
                _v6CaptureSession = session;
            }
        }

        [OneTimeTearDown]
        public void FinalizeV6CaptureIfRequested()
        {
            V6CaptureSessionState session;
            lock (V6CaptureGate) session = _v6CaptureSession;
            if (session == null) return;

            V6CaptureSourceHash[] sourceHashesAfter = HashCaptureSources(session.projectRoot, true);
            bool sourcesStable = SourceHashesEqual(session.sourceHashesBefore, sourceHashesAfter);
            bool sessionDocumentStable = VerifySessionDocument(session);
            V6CaptureAttemptRecord[] attempts = CollectCaptureAttempts(session);
            bool allExpectedIdsAttempted = V6CaptureExpectedCaseIds.All(expectedId =>
                attempts.Any(attempt => string.Equals(attempt.caseId, expectedId, StringComparison.Ordinal)));
            bool allAttemptsComplete = attempts.Length > 0 && attempts.All(attempt => attempt.complete);
            bool dataComplete = sourcesStable && sessionDocumentStable && allExpectedIdsAttempted && allAttemptsComplete;
            bool roleRequirementsPassed = allExpectedIdsAttempted && attempts.Length > 0 &&
                attempts.All(attempt => attempt.semanticAcceptance);

            var manifest = CreateManifestDocument(session, sourceHashesAfter, attempts,
                sourcesStable, sessionDocumentStable, dataComplete, roleRequirementsPassed);
            WriteExclusiveAndVerify(Path.Combine(session.captureRoot, "manifest.json"),
                V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(manifest, true)));

            lock (V6CaptureGate) _v6CaptureSession = null;
            if (!dataComplete || !roleRequirementsPassed)
            {
                throw new AssertionException("Requested V6 capture is incomplete or failed a role-specific requirement. " +
                    "manifest.json was preserved; dataComplete=" + dataComplete +
                    ", roleRequirementsPassed=" + roleRequirementsPassed +
                    ", sourcesStable=" + sourcesStable +
                    ", sessionDocumentStable=" + sessionDocumentStable + ".");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \r\n\t")]
        public void NullOrWhitespaceDocumentsAreMalformed(string originalJson)
        {
            SaveV6ChecksumInspection profile = SaveV6ChecksumInspector.InspectProfile(originalJson);
            SaveV6ChecksumInspection settings = SaveV6ChecksumInspector.InspectSettings(originalJson);

            Assert.That(profile.Status, Is.EqualTo(SaveV6ChecksumStatus.Malformed));
            Assert.That(settings.Status, Is.EqualTo(SaveV6ChecksumStatus.Malformed));
            Assert.That(profile.OriginalJson, Is.EqualTo(originalJson));
            Assert.That(settings.OriginalJson, Is.EqualTo(originalJson));
        }

        [TestCase("{")]
        [TestCase("not-json")]
        public void JsonUtilityMalformedInputNeverReportsChecksumValid(string originalJson)
        {
            SaveV6ChecksumInspection profile = SaveV6ChecksumInspector.InspectProfile(originalJson);
            SaveV6ChecksumInspection settings = SaveV6ChecksumInspector.InspectSettings(originalJson);

            Assert.That(profile.Status, Is.Not.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(settings.Status, Is.Not.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(profile.Status, Is.Not.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(settings.Status, Is.Not.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(profile.OriginalJson, Is.EqualTo(originalJson));
            Assert.That(settings.OriginalJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void NonV6HeadersAreReportedWithoutRewritingTheInput()
        {
            const string profileJson = "{\"schemaVersion\":1,\"contentVersion\":5,\"checksum\":\"legacy\"}";
            const string settingsJson = "{\"schemaVersion\":1,\"contentVersion\":7,\"checksum\":\"candidate\"}";

            SaveV6ChecksumInspection profile = SaveV6ChecksumInspector.InspectProfile(profileJson);
            SaveV6ChecksumInspection settings = SaveV6ChecksumInspector.InspectSettings(settingsJson);

            Assert.That(profile.Status, Is.EqualTo(SaveV6ChecksumStatus.NotVersion6));
            Assert.That(settings.Status, Is.EqualTo(SaveV6ChecksumStatus.NotVersion6));
            Assert.That(profile.OriginalJson, Is.EqualTo(profileJson));
            Assert.That(settings.OriginalJson, Is.EqualTo(settingsJson));
            Assert.That(profile.StoredChecksum, Is.EqualTo("legacy"));
            Assert.That(settings.StoredChecksum, Is.EqualTo("candidate"));
        }

        [Test]
        public void InputOnlyProfileAndSettingsDocumentsReportChecksumMismatch()
        {
            string profileJson = ReadFixture(ProfileInputFixture);
            string settingsJson = ReadFixture(SettingsInputFixture);

            SaveV6ChecksumInspection profile = SaveV6ChecksumInspector.InspectProfile(profileJson);
            SaveV6ChecksumInspection settings = SaveV6ChecksumInspector.InspectSettings(settingsJson);

            Assert.That(profile.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(settings.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(profile.StoredChecksum, Is.EqualTo("candidate-not-captured"));
            Assert.That(settings.StoredChecksum, Is.EqualTo("candidate-not-captured"));
            Assert.That(profile.OriginalJson, Is.EqualTo(profileJson));
            Assert.That(settings.OriginalJson, Is.EqualTo(settingsJson));
            Assert.That(profile.ComputedChecksum, Is.Not.Null.And.Not.Empty);
            Assert.That(settings.ComputedChecksum, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void InspectionPreimageGetterReturnsDefensiveCopies()
        {
            string originalJson = ReadFixture(ProfileInputFixture);
            SaveV6ChecksumInspection inspection = SaveV6ChecksumInspector.InspectProfile(originalJson);
            Assert.That(inspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));

            byte[] first = inspection.GetPreimageUtf8();
            byte[] second = inspection.GetPreimageUtf8();
            Assert.That(first, Is.Not.Null.And.Not.Empty);
            Assert.That(second, Is.Not.SameAs(first));

            byte untouchedValue = second[0];
            first[0] = untouchedValue == byte.MaxValue ? (byte)0 : (byte)(untouchedValue + 1);

            Assert.That(second[0], Is.EqualTo(untouchedValue));
            byte[] third = inspection.GetPreimageUtf8();
            CollectionAssert.AreEqual(second, third);
            Assert.That(inspection.OriginalJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void ProfileProjectionDoesNotMutateInputAndReturnsIndependentByteArrays()
        {
            var counters = new[] { new LegacyContractCounterSaveDataV6 { contractId = "input-only", progress = 1 } };
            var results = new[]
            {
                new LegacyExpeditionRoomCompletionSaveDataV6
                {
                    roomNodeId = "input-only",
                    resultId = "input-only",
                    resultJson = "{\"value\":\"input-only\"}"
                }
            };
            var selectedSkills = new[] { "input-only" };
            var frozenUpgrades = new LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6();
            var fortuneOffers = new[] { new LegacyFortuneOfferSnapshotV6 { offerId = "input-only" } };
            var fortuneBook = new LegacyFortuneOfferBookSnapshotV6 { offers = fortuneOffers };
            var expedition = new LegacyExpeditionSaveDataV6
            {
                selectedSkillIds = selectedSkills,
                completedRoomResults = results,
                frozenPermanentUpgrades = frozenUpgrades,
                fortuneOfferBookSnapshot = fortuneBook
            };
            var profileData = new LegacyProfileSaveDataV6 { contractCounters = counters };
            var source = new LegacyProfileSaveEnvelopeV6
            {
                schemaVersion = 1,
                contentVersion = 6,
                checksum = "input-only-original-checksum",
                profile = profileData,
                hasActiveExpedition = true,
                activeExpedition = expedition
            };

            byte[] first = SaveV6ChecksumInspector.GetProfileChecksumPreimage(source);
            byte[] second = SaveV6ChecksumInspector.GetProfileChecksumPreimage(source);

            Assert.That(first, Is.Not.Null.And.Not.Empty);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(source.checksum, Is.EqualTo("input-only-original-checksum"));
            Assert.That(source.profile, Is.SameAs(profileData));
            Assert.That(source.activeExpedition, Is.SameAs(expedition));
            Assert.That(profileData.contractCounters, Is.SameAs(counters));
            Assert.That(expedition.selectedSkillIds, Is.SameAs(selectedSkills));
            Assert.That(expedition.completedRoomResults, Is.SameAs(results));
            Assert.That(expedition.completedRoomResults[0].resultJson, Is.EqualTo("{\"value\":\"input-only\"}"));
            Assert.That(expedition.frozenPermanentUpgrades, Is.SameAs(frozenUpgrades));
            Assert.That(expedition.fortuneOfferBookSnapshot, Is.SameAs(fortuneBook));
            Assert.That(fortuneBook.offers, Is.SameAs(fortuneOffers));

            byte untouchedValue = second[0];
            first[0] = untouchedValue == byte.MaxValue ? (byte)0 : (byte)(untouchedValue + 1);
            Assert.That(second[0], Is.EqualTo(untouchedValue));
        }

        [Test]
        public void SettingsProjectionDoesNotMutateInputAndReturnsIndependentByteArrays()
        {
            var settings = new LegacyGameSettingsSaveDataV6
            {
                languageId = "input-only",
                masterVolume = 0.5f,
                musicVolume = 0.25f,
                effectsVolume = 0.75f,
                ambienceVolume = 0.4f
            };
            var source = new LegacySettingsSaveEnvelopeV6
            {
                schemaVersion = 1,
                contentVersion = 6,
                checksum = "input-only-original-checksum",
                settings = settings
            };

            byte[] first = SaveV6ChecksumInspector.GetSettingsChecksumPreimage(source);
            byte[] second = SaveV6ChecksumInspector.GetSettingsChecksumPreimage(source);

            Assert.That(first, Is.Not.Null.And.Not.Empty);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(source.checksum, Is.EqualTo("input-only-original-checksum"));
            Assert.That(source.settings, Is.SameAs(settings));
            Assert.That(settings.languageId, Is.EqualTo("input-only"));
            Assert.That(settings.masterVolume, Is.EqualTo(0.5f));
            Assert.That(settings.musicVolume, Is.EqualTo(0.25f));
            Assert.That(settings.effectsVolume, Is.EqualTo(0.75f));
            Assert.That(settings.ambienceVolume, Is.EqualTo(0.4f));

            byte untouchedValue = second[0];
            first[0] = untouchedValue == byte.MaxValue ? (byte)0 : (byte)(untouchedValue + 1);
            Assert.That(second[0], Is.EqualTo(untouchedValue));
        }

        [Test]
        public void ProjectionRejectsHeadersOutsideTheFrozenV6Shape()
        {
            var profile = new LegacyProfileSaveEnvelopeV6 { schemaVersion = 1, contentVersion = 5 };
            var settings = new LegacySettingsSaveEnvelopeV6 { schemaVersion = 1, contentVersion = 7 };

            Assert.Throws<ArgumentException>(() => SaveV6ChecksumInspector.GetProfileChecksumPreimage(profile));
            Assert.Throws<ArgumentException>(() => SaveV6ChecksumInspector.GetSettingsChecksumPreimage(settings));
        }

        [Test]
        public void InspectionFactoryRejectsNullAndCallerCreatedAccessKeys()
        {
            Assert.Throws<ArgumentException>(() => SaveV6ChecksumInspection.CreateForInspector(
                null, SaveV6ChecksumStatus.ChecksumValid, null, null, null, null));
            Assert.Throws<ArgumentException>(() => SaveV6ChecksumInspection.CreateForInspector(
                new object(), SaveV6ChecksumStatus.ChecksumValid, null, null, null, null));
        }

        [Test]
        public void DeterministicProfileBuildersHaveCompleteLiveAndLegacyProjectionByteEquivalence()
        {
            ProfileCase[] cases =
            {
                ProfileCase.P0NoActive,
                ProfileCase.P1KnownFrozenZero,
                ProfileCase.P1KnownFrozenNonTrivial,
                ProfileCase.P2UnknownNullFrozen,
                ProfileCase.P2UnknownEmptyInlineFrozen,
                ProfileCase.P3ReceiptsFull,
                ProfileCase.P4NullEmptyBoundary,
                ProfileCase.P5FalseMarkerWithPayload
            };

            foreach (ProfileCase profileCase in cases)
            {
                ProfileSaveEnvelope live = BuildProfileEnvelope(profileCase);
                LegacyProfileSaveEnvelopeV6 legacy = AdaptProfileEnvelopeToLegacyV6(live);
                byte[] liveProjection = BuildCurrentProfileProjectionBytes(live);
                byte[] legacyProjection = SaveV6ChecksumInspector.GetProfileChecksumPreimage(legacy);

                CollectionAssert.AreEqual(liveProjection, legacyProjection,
                    "Live-vs-Legacy DTO byte equivalence only; no historical bytes are asserted. Case: " + profileCase);
                CaptureProfileAfterAssertions(profileCase, liveProjection, legacyProjection);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoomEntryCheckpointUsesCompleteLiveAndLegacyDtoByteEquivalence(bool includeNullSlots)
        {
            ExpeditionSaveData expedition = BuildActiveExpedition(ProfileCase.P1KnownFrozenNonTrivial);
            ExpeditionRoomCheckpointSaveData live = BuildRoomEntryCheckpoint(expedition, includeNullSlots);
            LegacyExpeditionRoomCheckpointSaveDataV6 legacy = AdaptCheckpointToLegacyV6(live);
            byte[] liveBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(live, false));
            byte[] legacyBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy, false));

            CollectionAssert.AreEqual(liveBytes, legacyBytes,
                "Checkpoint DTO serializer equivalence only; the checkpoint is a separate JSON string in the parent save.");
            if (!includeNullSlots)
            {
                Assert.That(expedition.roomEntryCheckpointJson,
                    Is.EqualTo(JsonUtility.ToJson(BuildRoomEntryCheckpoint(expedition, false), false)));
            }
            CaptureCheckpointAfterAssertions(includeNullSlots, liveBytes, legacyBytes);
        }

        [Test]
        public void FalseActiveMarkerExcludesPayloadFromProjectionButSemanticValidationRejectsIt()
        {
            ProfileSaveEnvelope noPayload = BuildProfileEnvelope(ProfileCase.P0NoActive);
            ProfileSaveEnvelope payloadWithFalseMarker = BuildProfileEnvelope(ProfileCase.P5FalseMarkerWithPayload);

            CollectionAssert.AreEqual(BuildCurrentProfileProjectionBytes(noPayload),
                BuildCurrentProfileProjectionBytes(payloadWithFalseMarker));
            CollectionAssert.AreEqual(
                SaveV6ChecksumInspector.GetProfileChecksumPreimage(AdaptProfileEnvelopeToLegacyV6(noPayload)),
                SaveV6ChecksumInspector.GetProfileChecksumPreimage(
                    AdaptProfileEnvelopeToLegacyV6(payloadWithFalseMarker)));

            Assert.That(payloadWithFalseMarker.hasActiveExpedition, Is.False);
            Assert.That(payloadWithFalseMarker.activeExpedition, Is.Not.Null);
            Assert.That(SaveDataValidation.Validate(payloadWithFalseMarker).Any(error =>
                    error.Contains("Active expedition marker must match the saved expedition payload.")),
                Is.True,
                "Checksum projection equivalence does not make a false-marker payload semantically acceptable.");
        }

        [Test]
        public void DeterministicSettingsBuilderHasCompleteLiveAndLegacyProjectionByteEquivalence()
        {
            SettingsSaveEnvelope live = BuildSettingsEnvelope();
            LegacySettingsSaveEnvelopeV6 legacy = AdaptSettingsEnvelopeToLegacyV6(live);
            byte[] liveProjection = BuildCurrentSettingsProjectionBytes(live);
            byte[] legacyProjection = SaveV6ChecksumInspector.GetSettingsChecksumPreimage(legacy);

            CollectionAssert.AreEqual(liveProjection, legacyProjection,
                "Live-vs-Legacy DTO byte equivalence only; no historical bytes are asserted.");
            CaptureSettingsAfterAssertions(liveProjection, legacyProjection);
        }

        [Test]
        public void P3NestedJsonStringsRemainLiteralAcrossBothByteProjections()
        {
            ProfileSaveEnvelope live = BuildProfileEnvelope(ProfileCase.P3ReceiptsFull);
            ExpeditionSaveData expedition = live.activeExpedition;
            string routeJson = expedition.routeSnapshotJson;
            string inventoryJson = expedition.inventorySnapshotJson;
            string checkpointJson = expedition.roomEntryCheckpointJson;
            string resultJson = expedition.completedRoomResults[0].resultJson;

            byte[] currentProjection = BuildCurrentProfileProjectionBytes(live);
            byte[] legacyProjection = SaveV6ChecksumInspector.GetProfileChecksumPreimage(
                AdaptProfileEnvelopeToLegacyV6(live));

            CollectionAssert.AreEqual(currentProjection, legacyProjection,
                "The full serialized byte arrays are compared; this remains current-code equivalence only.");
            Assert.That(expedition.routeSnapshotJson, Is.EqualTo(routeJson));
            Assert.That(expedition.inventorySnapshotJson, Is.EqualTo(inventoryJson));
            Assert.That(expedition.roomEntryCheckpointJson, Is.EqualTo(checkpointJson));
            Assert.That(expedition.completedRoomResults[0].resultJson, Is.EqualTo(resultJson));
            Assert.That(resultJson, Is.EqualTo("{\"outcome\":\"cleared\",\"renown\":12,\"lootId\":\"reward.d09.test\"}"));
        }

        [TestCase("V6-P0-profile-no-active", "P0NoActive", true, "Valid")]
        [TestCase("V6-P1-active-known-zero", "P1KnownFrozenZero", true, "Valid")]
        [TestCase("V6-P1-active-known-nontrivial", "P1KnownFrozenNonTrivial", true, "Valid")]
        [TestCase("V6-P2-active-unknown-null", "P2UnknownNullFrozen", true, "Valid")]
        [TestCase("V6-P2-active-unknown-inline-empty", "P2UnknownEmptyInlineFrozen", true, "Valid")]
        [TestCase("V6-P3-receipts-full", "P3ReceiptsFull", true, "Valid")]
        [TestCase("V6-P4-null-empty-boundary", "P4NullEmptyBoundary", false, "Corrupted")]
        [TestCase("V6-P5-flag-payload-conflict", "P5FalseMarkerWithPayload", false, "Corrupted")]
        public void CapturedFrozenProfileFixturesMatchMeasuredBytesAndKeepTheirRoles(
            string fixtureId,
            string profileCaseName,
            bool expectedSemanticAcceptance,
            string expectedDecodeStatus)
        {
            ProfileCase profileCase = (ProfileCase)Enum.Parse(typeof(ProfileCase), profileCaseName, false);
            string fixturePrefix = fixtureId + ".Frozen.";
            byte[] capturedEnvelopeBytes = ReadFixtureBytes(fixturePrefix + "envelope.json");
            byte[] capturedPreimage = ReadFixtureBytes(fixturePrefix + "checksum-preimage.utf8");
            byte[] capturedChecksumBytes = ReadFixtureBytes(fixturePrefix + "checksum.sha256.txt");
            string capturedEnvelope = new UTF8Encoding(false, true).GetString(capturedEnvelopeBytes);
            string capturedChecksum = Encoding.ASCII.GetString(capturedChecksumBytes);

            Assert.That(capturedChecksumBytes.Length, Is.EqualTo(64), "Captured checksum text is exact ASCII hex, without a newline.");
            Assert.That(ComputeSha256(capturedPreimage), Is.EqualTo(capturedChecksum));

            SaveV6ChecksumInspection capturedInspection = SaveV6ChecksumInspector.InspectProfile(capturedEnvelope);
            Assert.That(capturedInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(capturedInspection.OriginalJson, Is.EqualTo(capturedEnvelope));
            Assert.That(capturedInspection.StoredChecksum, Is.EqualTo(capturedChecksum));
            Assert.That(capturedInspection.ComputedChecksum, Is.EqualTo(capturedChecksum));
            CollectionAssert.AreEqual(capturedPreimage, capturedInspection.GetPreimageUtf8(),
                "The inspector must reproduce the frozen Unity JsonUtility bytes exactly.");

            ProfileSaveEnvelope live = BuildProfileEnvelope(profileCase);
            byte[] liveProjection = BuildCurrentProfileProjectionBytes(live);
            byte[] legacyProjection = SaveV6ChecksumInspector.GetProfileChecksumPreimage(
                AdaptProfileEnvelopeToLegacyV6(live));
            CollectionAssert.AreEqual(capturedPreimage, liveProjection,
                "Frozen bytes must match the current profile projection for the fixed builder.");
            CollectionAssert.AreEqual(capturedPreimage, legacyProjection,
                "Frozen bytes must match the independent frozen-v6 DTO projection.");

            string encodedEnvelope = SaveJsonCodec.Encode(live);
            byte[] encodedEnvelopeBytes = V6CaptureUtf8NoBom.GetBytes(encodedEnvelope);
            CollectionAssert.AreEqual(capturedEnvelopeBytes, encodedEnvelopeBytes,
                "The real encoder must reproduce the exact captured envelope bytes.");
            SaveV6ChecksumInspection encodedInspection = SaveV6ChecksumInspector.InspectProfile(encodedEnvelope);
            Assert.That(encodedInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(encodedInspection.StoredChecksum, Is.EqualTo(capturedChecksum));
            Assert.That(encodedInspection.ComputedChecksum, Is.EqualTo(capturedChecksum));
            CollectionAssert.AreEqual(capturedPreimage, encodedInspection.GetPreimageUtf8());

            ProfileSaveEnvelope decoded;
            object decodeStatus = SaveJsonCodec.Decode(capturedEnvelope, out decoded);
            Assert.That(decodeStatus.ToString(), Is.EqualTo(expectedDecodeStatus));
            IReadOnlyList<string> validationErrors = SaveDataValidation.Validate(live);
            Assert.That(validationErrors.Any(), Is.EqualTo(!expectedSemanticAcceptance));

            if (profileCase == ProfileCase.P5FalseMarkerWithPayload)
            {
                Assert.That(live.hasActiveExpedition, Is.False);
                Assert.That(live.activeExpedition, Is.Not.Null);
                Assert.That(validationErrors.Any(error =>
                        error.Contains("Active expedition marker must match the saved expedition payload.")),
                    Is.True,
                    "A checksum-valid negative-semantic fixture must remain semantically rejected.");
            }
        }

        [Test]
        public void CapturedFrozenSettingsFixtureMatchesMeasuredBytesAndEncoder()
        {
            const string fixturePrefix = "V6-S0-settings.Frozen.";
            byte[] capturedEnvelopeBytes = ReadFixtureBytes(fixturePrefix + "envelope.json");
            byte[] capturedPreimage = ReadFixtureBytes(fixturePrefix + "checksum-preimage.utf8");
            byte[] capturedChecksumBytes = ReadFixtureBytes(fixturePrefix + "checksum.sha256.txt");
            string capturedEnvelope = new UTF8Encoding(false, true).GetString(capturedEnvelopeBytes);
            string capturedChecksum = Encoding.ASCII.GetString(capturedChecksumBytes);

            Assert.That(capturedChecksumBytes.Length, Is.EqualTo(64));
            Assert.That(ComputeSha256(capturedPreimage), Is.EqualTo(capturedChecksum));

            SaveV6ChecksumInspection inspection = SaveV6ChecksumInspector.InspectSettings(capturedEnvelope);
            Assert.That(inspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(inspection.OriginalJson, Is.EqualTo(capturedEnvelope));
            Assert.That(inspection.StoredChecksum, Is.EqualTo(capturedChecksum));
            Assert.That(inspection.ComputedChecksum, Is.EqualTo(capturedChecksum));
            CollectionAssert.AreEqual(capturedPreimage, inspection.GetPreimageUtf8());

            SettingsSaveEnvelope live = BuildSettingsEnvelope();
            CollectionAssert.AreEqual(capturedPreimage, BuildCurrentSettingsProjectionBytes(live));
            CollectionAssert.AreEqual(capturedPreimage, SaveV6ChecksumInspector.GetSettingsChecksumPreimage(
                AdaptSettingsEnvelopeToLegacyV6(live)));

            string encodedEnvelope = SaveJsonCodec.Encode(live);
            CollectionAssert.AreEqual(capturedEnvelopeBytes, V6CaptureUtf8NoBom.GetBytes(encodedEnvelope),
                "The real settings encoder must reproduce the exact captured envelope bytes.");
            SaveV6ChecksumInspection encodedInspection = SaveV6ChecksumInspector.InspectSettings(encodedEnvelope);
            Assert.That(encodedInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(encodedInspection.StoredChecksum, Is.EqualTo(capturedChecksum));
            Assert.That(encodedInspection.ComputedChecksum, Is.EqualTo(capturedChecksum));
            CollectionAssert.AreEqual(capturedPreimage, encodedInspection.GetPreimageUtf8());
        }

        [TestCase(false, "V6-checkpoint-empty-slots")]
        [TestCase(true, "V6-checkpoint-null-slots")]
        public void CapturedFrozenCheckpointBytesMatchMeasuredSeparateJson(bool includeNullSlots, string fixtureId)
        {
            string fixturePrefix = fixtureId + ".Frozen.";
            byte[] capturedCheckpoint = ReadFixtureBytes(fixturePrefix + "checkpoint-legacy.json");
            byte[] capturedChecksumBytes = ReadFixtureBytes(fixturePrefix + "checkpoint-legacy.sha256.txt");
            string capturedChecksum = Encoding.ASCII.GetString(capturedChecksumBytes);

            Assert.That(capturedChecksumBytes.Length, Is.EqualTo(64));
            Assert.That(ComputeSha256(capturedCheckpoint), Is.EqualTo(capturedChecksum));

            ExpeditionSaveData expedition = BuildActiveExpedition(ProfileCase.P1KnownFrozenNonTrivial);
            ExpeditionRoomCheckpointSaveData live = BuildRoomEntryCheckpoint(expedition, includeNullSlots);
            LegacyExpeditionRoomCheckpointSaveDataV6 legacy = AdaptCheckpointToLegacyV6(live);
            byte[] liveBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(live, false));
            byte[] legacyBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy, false));

            CollectionAssert.AreEqual(capturedCheckpoint, liveBytes,
                "The checkpoint's captured live JSON remains separate from parent-save checksum data.");
            CollectionAssert.AreEqual(capturedCheckpoint, legacyBytes,
                "The captured checkpoint bytes must match the frozen-v6 checkpoint DTO.");
        }

        [Test]
        public void N0MutationOfCapturedEnvelopeKeepsOriginalChecksumAndDiffersFromMalformed()
        {
            const string fixturePrefix = "V6-P0-profile-no-active.Frozen.";
            byte[] capturedEnvelopeBytes = ReadFixtureBytes(fixturePrefix + "envelope.json");
            string capturedEnvelope = new UTF8Encoding(false, true).GetString(capturedEnvelopeBytes);
            byte[] capturedChecksumBytes = ReadFixtureBytes(fixturePrefix + "checksum.sha256.txt");
            string capturedChecksum = Encoding.ASCII.GetString(capturedChecksumBytes);
            const string originalToken = "\"availableRenown\": 0";
            const string changedToken = "\"availableRenown\": 1";

            int mutationIndex = capturedEnvelope.IndexOf(originalToken, StringComparison.Ordinal);
            Assert.That(mutationIndex, Is.GreaterThanOrEqualTo(0), "The frozen source field must exist before mutation.");
            Assert.That(capturedEnvelope.IndexOf(originalToken, mutationIndex + originalToken.Length,
                    StringComparison.Ordinal),
                Is.EqualTo(-1), "The corruption must change exactly one captured profile value.");
            string corruptedEnvelope = capturedEnvelope.Substring(0, mutationIndex) + changedToken +
                capturedEnvelope.Substring(mutationIndex + originalToken.Length);
            byte[] corruptedEnvelopeBytes = V6CaptureUtf8NoBom.GetBytes(corruptedEnvelope);
            Assert.That(ByteArraysEqual(capturedEnvelopeBytes, corruptedEnvelopeBytes), Is.False,
                "N0 must be an actual byte change to a measured envelope.");

            SaveV6ChecksumInspection originalInspection = SaveV6ChecksumInspector.InspectProfile(capturedEnvelope);
            SaveV6ChecksumInspection corruptedInspection = SaveV6ChecksumInspector.InspectProfile(corruptedEnvelope);
            SaveV6ChecksumInspection malformedInspection = SaveV6ChecksumInspector.InspectProfile("{");

            Assert.That(originalInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(originalInspection.StoredChecksum, Is.EqualTo(capturedChecksum));
            Assert.That(corruptedInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(corruptedInspection.OriginalJson, Is.EqualTo(corruptedEnvelope));
            Assert.That(corruptedInspection.StoredChecksum, Is.EqualTo(capturedChecksum),
                "The mutation retains the frozen envelope's original stored checksum.");
            Assert.That(corruptedInspection.ComputedChecksum, Is.Not.EqualTo(capturedChecksum));
            Assert.That(malformedInspection.Status, Is.EqualTo(SaveV6ChecksumStatus.Malformed));
            Assert.That(corruptedInspection.Status, Is.Not.EqualTo(malformedInspection.Status),
                "A parseable checksum mismatch is distinct from malformed JSON.");
            CollectionAssert.AreEqual(capturedEnvelopeBytes, ReadFixtureBytes(fixturePrefix + "envelope.json"),
                "N0 is mutated in memory only; the frozen positive fixture remains unchanged.");
        }

        [Test]
        public void FrozenInspectionIsRepeatableAcrossCultureAndReturnsUnmodifiedCapturedBytes()
        {
            const string profilePrefix = "V6-P3-receipts-full.Frozen.";
            const string settingsPrefix = "V6-S0-settings.Frozen.";
            string profileJson = new UTF8Encoding(false, true).GetString(
                ReadFixtureBytes(profilePrefix + "envelope.json"));
            string settingsJson = new UTF8Encoding(false, true).GetString(
                ReadFixtureBytes(settingsPrefix + "envelope.json"));
            byte[] expectedProfilePreimage = ReadFixtureBytes(profilePrefix + "checksum-preimage.utf8");
            byte[] expectedSettingsPreimage = ReadFixtureBytes(settingsPrefix + "checksum-preimage.utf8");
            CultureInfo originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                SaveV6ChecksumInspection profileFirst = SaveV6ChecksumInspector.InspectProfile(profileJson);
                SaveV6ChecksumInspection profileSecond = SaveV6ChecksumInspector.InspectProfile(profileJson);
                SaveV6ChecksumInspection settingsFirst = SaveV6ChecksumInspector.InspectSettings(settingsJson);
                SaveV6ChecksumInspection settingsSecond = SaveV6ChecksumInspector.InspectSettings(settingsJson);

                Assert.That(profileFirst.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
                Assert.That(settingsFirst.Status, Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
                Assert.That(profileFirst.OriginalJson, Is.EqualTo(profileJson));
                Assert.That(settingsFirst.OriginalJson, Is.EqualTo(settingsJson));
                Assert.That(profileFirst.ComputedChecksum, Is.EqualTo(profileSecond.ComputedChecksum));
                Assert.That(settingsFirst.ComputedChecksum, Is.EqualTo(settingsSecond.ComputedChecksum));
                CollectionAssert.AreEqual(expectedProfilePreimage, profileFirst.GetPreimageUtf8());
                CollectionAssert.AreEqual(expectedProfilePreimage, profileSecond.GetPreimageUtf8());
                CollectionAssert.AreEqual(expectedSettingsPreimage, settingsFirst.GetPreimageUtf8());
                CollectionAssert.AreEqual(expectedSettingsPreimage, settingsSecond.GetPreimageUtf8());

                CollectionAssert.AreEqual(expectedProfilePreimage,
                    BuildCurrentProfileProjectionBytes(BuildProfileEnvelope(ProfileCase.P3ReceiptsFull)));
                CollectionAssert.AreEqual(expectedSettingsPreimage,
                    BuildCurrentSettingsProjectionBytes(BuildSettingsEnvelope()));

                byte[] mutableCopy = profileFirst.GetPreimageUtf8();
                byte[] untouchedCopy = profileFirst.GetPreimageUtf8();
                mutableCopy[0] = mutableCopy[0] == byte.MaxValue ? (byte)0 : (byte)(mutableCopy[0] + 1);
                CollectionAssert.AreEqual(expectedProfilePreimage, untouchedCopy);
                CollectionAssert.AreEqual(expectedProfilePreimage, profileFirst.GetPreimageUtf8());
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }

            Assert.That(profileJson, Is.EqualTo(new UTF8Encoding(false, true).GetString(
                ReadFixtureBytes(profilePrefix + "envelope.json"))));
            Assert.That(settingsJson, Is.EqualTo(new UTF8Encoding(false, true).GetString(
                ReadFixtureBytes(settingsPrefix + "envelope.json"))));
        }

        [Test]
        public void FrozenP3RetainsNestedJsonStringsAndSerializedArrayOrder()
        {
            const string fixturePrefix = "V6-P3-receipts-full.Frozen.";
            string capturedEnvelope = new UTF8Encoding(false, true).GetString(
                ReadFixtureBytes(fixturePrefix + "envelope.json"));
            ProfileSaveEnvelope captured;
            object decodeStatus = SaveJsonCodec.Decode(capturedEnvelope, out captured);
            Assert.That(decodeStatus.ToString(), Is.EqualTo("Valid"));

            ProfileSaveEnvelope expected = BuildProfileEnvelope(ProfileCase.P3ReceiptsFull);
            Assert.That(captured.activeExpedition, Is.Not.Null);
            CollectionAssert.AreEqual(expected.profile.unlockedRaceIds, captured.profile.unlockedRaceIds);
            CollectionAssert.AreEqual(expected.profile.unlockedStartingKitIds, captured.profile.unlockedStartingKitIds);
            CollectionAssert.AreEqual(expected.profile.processedSettlementRunIds, captured.profile.processedSettlementRunIds);
            CollectionAssert.AreEqual(
                expected.profile.processedContractProgressEvents.Select(receipt =>
                    receipt.eventId + ":" + receipt.progressEvent),
                captured.profile.processedContractProgressEvents.Select(receipt =>
                    receipt.eventId + ":" + receipt.progressEvent));
            CollectionAssert.AreEqual(
                expected.profile.progressionPurchaseTransactions.Select(transaction =>
                    transaction.transactionId + ":" + transaction.kind + ":" + transaction.cost),
                captured.profile.progressionPurchaseTransactions.Select(transaction =>
                    transaction.transactionId + ":" + transaction.kind + ":" + transaction.cost));
            CollectionAssert.AreEqual(expected.activeExpedition.selectedSkillIds,
                captured.activeExpedition.selectedSkillIds);
            CollectionAssert.AreEqual(expected.activeExpedition.startingEquipmentInstanceIds,
                captured.activeExpedition.startingEquipmentInstanceIds);
            CollectionAssert.AreEqual(expected.activeExpedition.visitedNodeIds,
                captured.activeExpedition.visitedNodeIds);
            CollectionAssert.AreEqual(expected.activeExpedition.completedRoomIds,
                captured.activeExpedition.completedRoomIds);
            CollectionAssert.AreEqual(expected.activeExpedition.pactIds, captured.activeExpedition.pactIds);
            CollectionAssert.AreEqual(expected.activeExpedition.collectedItemIds,
                captured.activeExpedition.collectedItemIds);
            CollectionAssert.AreEqual(expected.activeExpedition.claimedRewardIds,
                captured.activeExpedition.claimedRewardIds);

            Assert.That(captured.activeExpedition.routeSnapshotJson,
                Is.EqualTo(expected.activeExpedition.routeSnapshotJson));
            Assert.That(captured.activeExpedition.inventorySnapshotJson,
                Is.EqualTo(expected.activeExpedition.inventorySnapshotJson));
            Assert.That(captured.activeExpedition.roomEntryCheckpointJson,
                Is.EqualTo(expected.activeExpedition.roomEntryCheckpointJson));
            Assert.That(captured.activeExpedition.completedRoomResults[0].resultJson,
                Is.EqualTo(expected.activeExpedition.completedRoomResults[0].resultJson));
        }

        [Test]
        public void CapturedNullEmptyAndFalseMarkerCasesKeepTheirDistinctRolesDespiteSharedBytes()
        {
            byte[] unknownNullEnvelope = ReadFixtureBytes(
                "V6-P2-active-unknown-null.Frozen.envelope.json");
            byte[] unknownInlineEmptyEnvelope = ReadFixtureBytes(
                "V6-P2-active-unknown-inline-empty.Frozen.envelope.json");
            byte[] unknownNullPreimage = ReadFixtureBytes(
                "V6-P2-active-unknown-null.Frozen.checksum-preimage.utf8");
            byte[] unknownInlineEmptyPreimage = ReadFixtureBytes(
                "V6-P2-active-unknown-inline-empty.Frozen.checksum-preimage.utf8");
            CollectionAssert.AreEqual(unknownNullEnvelope, unknownInlineEmptyEnvelope,
                "The captured JsonUtility representation for these two builder variants is byte-identical.");
            CollectionAssert.AreEqual(unknownNullPreimage, unknownInlineEmptyPreimage);

            byte[] noActiveEnvelope = ReadFixtureBytes("V6-P0-profile-no-active.Frozen.envelope.json");
            byte[] falseMarkerEnvelope = ReadFixtureBytes("V6-P5-flag-payload-conflict.Frozen.envelope.json");
            byte[] noActivePreimage = ReadFixtureBytes("V6-P0-profile-no-active.Frozen.checksum-preimage.utf8");
            byte[] falseMarkerPreimage = ReadFixtureBytes("V6-P5-flag-payload-conflict.Frozen.checksum-preimage.utf8");
            CollectionAssert.AreEqual(noActivePreimage, falseMarkerPreimage,
                "The false marker excludes its payload from this checksum projection.");
            Assert.That(ByteArraysEqual(noActiveEnvelope, falseMarkerEnvelope), Is.False,
                "The captured full envelope remains different even when projected checksum bytes coincide.");

            string noActiveJson = new UTF8Encoding(false, true).GetString(noActiveEnvelope);
            string falseMarkerJson = new UTF8Encoding(false, true).GetString(falseMarkerEnvelope);
            Assert.That(SaveV6ChecksumInspector.InspectProfile(noActiveJson).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            Assert.That(SaveV6ChecksumInspector.InspectProfile(falseMarkerJson).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            ProfileSaveEnvelope noActive;
            ProfileSaveEnvelope falseMarker;
            object noActiveDecode = SaveJsonCodec.Decode(noActiveJson, out noActive);
            object falseMarkerDecode = SaveJsonCodec.Decode(falseMarkerJson, out falseMarker);
            Assert.That(noActiveDecode.ToString(), Is.EqualTo("Valid"));
            Assert.That(falseMarkerDecode.ToString(), Is.EqualTo("Corrupted"));
            Assert.That(SaveDataValidation.Validate(noActive).Any(), Is.False);
            Assert.That(SaveDataValidation.Validate(falseMarker).Any(), Is.True);
            Assert.That(falseMarker.hasActiveExpedition, Is.False);
            Assert.That(falseMarker.activeExpedition, Is.Not.Null);
        }

        [Test]
        public void FrozenPositiveProfilesAndSettingsAreReadByAnIsolatedRepositoryWithoutRewritingEitherCopy()
        {
            byte[] profileBytes = ReadFixtureBytes("V6-P1-active-known-nontrivial.Frozen.envelope.json");
            byte[] settingsBytes = ReadFixtureBytes("V6-S0-settings.Frozen.envelope.json");
            string directory = Path.Combine(Path.GetTempPath(), "Luckbound.SaveV6Frozen." + Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(directory, null);

            try
            {
                Directory.CreateDirectory(directory);
                string profilePrimary = store.GetPrimaryPath(SaveDocument.Profile);
                string profileBackup = store.GetBackupPath(SaveDocument.Profile);
                string settingsPrimary = store.GetPrimaryPath(SaveDocument.Settings);
                string settingsBackup = store.GetBackupPath(SaveDocument.Settings);
                File.WriteAllBytes(profilePrimary, profileBytes);
                File.WriteAllBytes(profileBackup, profileBytes);
                File.WriteAllBytes(settingsPrimary, settingsBytes);
                File.WriteAllBytes(settingsBackup, settingsBytes);

                var repository = new SaveRepository(store);
                SaveLoadResult<ProfileSaveEnvelope> profile = repository.LoadProfileAsync().GetAwaiter().GetResult();
                SaveLoadResult<SettingsSaveEnvelope> settings = repository.LoadSettingsAsync().GetAwaiter().GetResult();

                Assert.That(profile.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(profile.Data, Is.Not.Null);
                Assert.That(settings.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(settings.Data, Is.Not.Null);
                CollectionAssert.AreEqual(profileBytes, File.ReadAllBytes(profilePrimary));
                CollectionAssert.AreEqual(profileBytes, File.ReadAllBytes(profileBackup));
                CollectionAssert.AreEqual(settingsBytes, File.ReadAllBytes(settingsPrimary));
                CollectionAssert.AreEqual(settingsBytes, File.ReadAllBytes(settingsBackup));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private enum ProfileCase
        {
            P0NoActive,
            P1KnownFrozenZero,
            P1KnownFrozenNonTrivial,
            P2UnknownNullFrozen,
            P2UnknownEmptyInlineFrozen,
            P3ReceiptsFull,
            P4NullEmptyBoundary,
            P5FalseMarkerWithPayload
        }

        private static ProfileSaveEnvelope BuildProfileEnvelope(ProfileCase profileCase)
        {
            bool includeReceipts = profileCase == ProfileCase.P3ReceiptsFull;
            bool hasPayload = profileCase != ProfileCase.P0NoActive;
            ExpeditionSaveData expedition = hasPayload ? BuildActiveExpedition(profileCase) : null;
            ProfileSaveEnvelope envelope = new ProfileSaveEnvelope
            {
                schemaVersion = 1,
                contentVersion = 6,
                generation = 1,
                sequence = 11,
                savedAtUtc = FixedSavedAtUtc,
                checksum = string.Empty,
                profile = BuildProfileData(includeReceipts),
                hasActiveExpedition = profileCase != ProfileCase.P0NoActive &&
                    profileCase != ProfileCase.P5FalseMarkerWithPayload,
                activeExpedition = expedition
            };

            if (profileCase == ProfileCase.P4NullEmptyBoundary)
                AddNullEmptyBoundaries(envelope);
            return envelope;
        }

        private static ProfileSaveData BuildProfileData(bool includeReceipts)
        {
            var profile = new ProfileSaveData
            {
                unlockedRaceIds = new[] { "race.human" },
                unlockedStartingKitIds = new[]
                {
                    "weapon.bellator.rusted_sword",
                    "weapon.venator.short_bow",
                    "weapon.magus.ember_staff"
                },
                acceptedContractIds = Array.Empty<string>(),
                claimedContractIds = Array.Empty<string>(),
                readStoryFragmentIds = Array.Empty<string>(),
                contractCounters = Array.Empty<ContractCounterSaveData>(),
                processedSettlementRunIds = Array.Empty<string>(),
                processedContractProgressEvents = Array.Empty<ContractProgressEventReceiptSaveData>(),
                progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>()
            };

            if (!includeReceipts)
                return profile;

            profile.availableRenown = 33;
            profile.healthUpgradeLevel = 2;
            profile.energyUpgradeLevel = 2;
            profile.additionalPotionUpgradeLevel = 1;
            profile.unlockedRaceIds = new[] { "race.human", "race.dvergar" };
            profile.unlockedStartingKitIds = new[]
            {
                "weapon.bellator.rusted_sword",
                "weapon.venator.short_bow",
                "weapon.magus.ember_staff",
                "armor.bellator.guild_cuirass"
            };
            profile.acceptedContractIds = new[] { "CT04" };
            profile.claimedContractIds = new[] { "CT04" };
            profile.contractCounters = new[] { new ContractCounterSaveData { contractId = "CT04", progress = 3 } };
            profile.processedSettlementRunIds = new[] { "000000000000000000000000000000d8" };
            profile.processedContractProgressEvents = new[]
            {
                new ContractProgressEventReceiptSaveData { eventId = "test/craft/0", progressEvent = (int)ContractProgressEvent.ItemCrafted },
                new ContractProgressEventReceiptSaveData { eventId = "test/craft/1", progressEvent = (int)ContractProgressEvent.ItemCrafted },
                new ContractProgressEventReceiptSaveData { eventId = "test/craft/2", progressEvent = (int)ContractProgressEvent.ItemCrafted }
            };
            profile.progressionPurchaseTransactions = new[]
            {
                new PermanentUpgradePurchaseSaveData
                {
                    transactionId = FixedGuid(1).ToString("N"),
                    kind = (int)PermanentUpgradeKind.MaximumHealth,
                    cost = 60
                },
                new PermanentUpgradePurchaseSaveData
                {
                    transactionId = FixedGuid(2).ToString("N"),
                    kind = (int)PermanentUpgradeKind.MaximumHealth,
                    cost = 120
                },
                new PermanentUpgradePurchaseSaveData
                {
                    transactionId = FixedGuid(3).ToString("N"),
                    kind = (int)PermanentUpgradeKind.MaximumEnergy,
                    cost = 60
                },
                new PermanentUpgradePurchaseSaveData
                {
                    transactionId = FixedGuid(4).ToString("N"),
                    kind = (int)PermanentUpgradeKind.MaximumEnergy,
                    cost = 120
                },
                new PermanentUpgradePurchaseSaveData
                {
                    transactionId = FixedGuid(5).ToString("N"),
                    kind = (int)PermanentUpgradeKind.AdditionalHealthPotion,
                    cost = 150
                }
            };
            return profile;
        }

        private static ExpeditionSaveData BuildActiveExpedition(ProfileCase profileCase)
        {
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(FixedExpeditionSeed, CreateRouteCatalog());
            string runId = FixedGuid(41).ToString("N");
            string weaponInstanceId = FixedGuid(42).ToString("N");
            string armorInstanceId = FixedGuid(43).ToString("N");
            FortuneOfferBookSnapshot fortuneBook = profileCase == ProfileCase.P2UnknownNullFrozen ||
                profileCase == ProfileCase.P2UnknownEmptyInlineFrozen || profileCase == ProfileCase.P4NullEmptyBoundary
                ? CreateSparseFortuneBook()
                : CreateFortuneBookWithReceipts();
            ExpeditionInventorySnapshot inventory = CreateInventorySnapshot("class.magus", weaponInstanceId, armorInstanceId);
            string inventoryJson = JsonUtility.ToJson(inventory, false);
            bool isKnownZero = profileCase == ProfileCase.P1KnownFrozenZero;
            bool isKnownNonTrivial = profileCase == ProfileCase.P1KnownFrozenNonTrivial ||
                profileCase == ProfileCase.P3ReceiptsFull || profileCase == ProfileCase.P5FalseMarkerWithPayload;
            bool hasFrozen = isKnownZero || isKnownNonTrivial;
            var expedition = new ExpeditionSaveData
            {
                runId = runId,
                seed = FixedExpeditionSeed,
                generationVersion = route.GenerationVersion,
                contentVersion = 6,
                classId = "class.magus",
                raceId = "race.human",
                selectedSkillIds = new[] { "skill.magus.fireball", "skill.magus.frost_nova" },
                startingEquipmentInstanceIds = new[] { weaponInstanceId, armorInstanceId },
                routeSnapshotJson = route.CanonicalForm,
                currentNodeId = route.MandatoryNodeIds[0],
                visitedNodeIds = new[] { route.MandatoryNodeIds[0] },
                completedRoomIds = Array.Empty<string>(),
                completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>(),
                inventorySnapshotJson = inventoryJson,
                maximumHealth = 110,
                currentHealth = 64,
                maximumEnergy = 50,
                currentEnergy = 37,
                healthPotionCharges = 2,
                frozenPermanentUpgrades = hasFrozen
                    ? new ExpeditionPermanentUpgradeSnapshotSaveData
                    {
                        healthUpgradeLevel = isKnownNonTrivial ? 2 : 0,
                        energyUpgradeLevel = isKnownNonTrivial ? 2 : 0,
                        additionalPotionUpgradeLevel = isKnownNonTrivial ? 1 : 0
                    }
                    : null,
                hasFrozenPermanentUpgrades = hasFrozen,
                accumulatedRenown = 0,
                pactIds = Array.Empty<string>(),
                fortuneOfferBookSnapshot = fortuneBook,
                collectedItemIds = Array.Empty<string>(),
                claimedRewardIds = Array.Empty<string>()
            };

            ExpeditionRoomCheckpointSaveData checkpoint = BuildRoomEntryCheckpoint(expedition, includeNullSlots: false);
            expedition.roomEntryCheckpointJson = JsonUtility.ToJson(checkpoint, false);

            if (profileCase == ProfileCase.P3ReceiptsFull)
            {
                const string resultJson = "{\"outcome\":\"cleared\",\"renown\":12,\"lootId\":\"reward.d09.test\"}";
                expedition.completedRoomIds = new[] { expedition.currentNodeId };
                expedition.completedRoomResults = new[]
                {
                    new ExpeditionRoomCompletionSaveData
                    {
                        roomNodeId = expedition.currentNodeId,
                        resultId = "result." + expedition.currentNodeId,
                        resultJson = resultJson
                    }
                };
                expedition.roomEntryCheckpointJson = string.Empty;
            }

            if (profileCase == ProfileCase.P2UnknownEmptyInlineFrozen)
                expedition.frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveData();

            return expedition;
        }

        private static ExpeditionInventorySnapshot CreateInventorySnapshot(string classId, string weaponInstanceId,
            string armorInstanceId)
        {
            MaterialKind[] materialKinds = (MaterialKind[])Enum.GetValues(typeof(MaterialKind));
            return new ExpeditionInventorySnapshot
            {
                snapshotVersion = ExpeditionInventory.SnapshotSchemaVersion,
                classId = classId,
                gold = 0,
                weapon = new InventoryStackSnapshot
                {
                    instanceId = weaponInstanceId,
                    itemId = "weapon.magus.ember_staff",
                    rarity = 0,
                    upgradeLevel = 0,
                    quantity = 1
                },
                armor = new InventoryStackSnapshot
                {
                    instanceId = armorInstanceId,
                    itemId = "armor.magus.traveler_robe",
                    rarity = 0,
                    upgradeLevel = 0,
                    quantity = 1
                },
                backpackSlots = new InventoryStackSnapshot[ExpeditionInventory.BackpackCapacity],
                quickConsumableInstanceId = string.Empty,
                materials = materialKinds.Select(kind => new MaterialCountSnapshot { kind = (int)kind, count = 0 }).ToArray(),
                worldMaterialRemainders = materialKinds.Select(kind => new WorldMaterialRemainderSnapshot { kind = (int)kind, remainder = 0 }).ToArray(),
                merchantRunId = string.Empty,
                merchantStocks = Array.Empty<MerchantStockSnapshot>(),
                restStops = Array.Empty<RestStopUseSnapshot>(),
                transactions = Array.Empty<InventoryTransactionSnapshot>()
            };
        }

        private static FortuneOfferBookSnapshot CreateSparseFortuneBook()
        {
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = 3,
                generationVersion = ExpeditionRouteGenerator.GenerationVersion,
                seed = FixedExpeditionSeed,
                raceId = "race.human",
                charges = 2,
                offers = Array.Empty<FortuneOfferSnapshot>(),
                processedGuardianIds = Array.Empty<string>(),
                rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>(),
                fortuneBrewReceipts = Array.Empty<FortuneBrewReceiptSnapshot>()
            };
        }

        private static FortuneOfferBookSnapshot CreateFortuneBookWithReceipts()
        {
            var book = new FortuneOfferBook(ExpeditionRouteGenerator.GenerationVersion,
                FixedExpeditionSeed, FortuneOfferBook.HumanRaceId);
            Assert.That(book.GrantGuardianCharge("patio.guardian").Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            FortuneOfferResult created = book.CreateOffer("patio.guardian", "patio.guardian", FortuneOptionIds);
            Assert.That(created.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Reroll(created.Offer.OfferId, FixedTransactionId(21)).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            FortuneRerollResult reroll = book.Reroll(created.Offer.OfferId, FixedTransactionId(22));
            Assert.That(reroll.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            string selectedOptionId = reroll.Offer.OptionIds[0];
            Assert.That(book.Choose(created.Offer.OfferId, selectedOptionId).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Claim(created.Offer.OfferId).Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.GrantFortuneBrewCharge(ItemInstanceId.Parse(FixedGuid(23).ToString("N")),
                FixedTransactionId(24)).Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            return book.CreateSnapshot();
        }

        private static ExpeditionRoomCheckpointSaveData BuildRoomEntryCheckpoint(ExpeditionSaveData expedition,
            bool includeNullSlots)
        {
            return new ExpeditionRoomCheckpointSaveData
            {
                roomNodeId = expedition.currentNodeId,
                inventorySnapshotJson = expedition.inventorySnapshotJson,
                maximumHealth = expedition.maximumHealth,
                currentHealth = expedition.currentHealth,
                maximumEnergy = expedition.maximumEnergy,
                currentEnergy = expedition.currentEnergy,
                healthPotionCharges = expedition.healthPotionCharges,
                accumulatedRenown = expedition.accumulatedRenown,
                fortuneOfferBookSnapshot = expedition.fortuneOfferBookSnapshot,
                pactIds = includeNullSlots ? new string[] { null } : Array.Empty<string>(),
                collectedItemIds = Array.Empty<string>(),
                claimedRewardIds = includeNullSlots ? null : Array.Empty<string>()
            };
        }

        private static void AddNullEmptyBoundaries(ProfileSaveEnvelope envelope)
        {
            envelope.profile.unlockedRaceIds = null;
            envelope.profile.unlockedStartingKitIds = Array.Empty<string>();
            envelope.profile.acceptedContractIds = new[] { "CT04" };
            envelope.profile.claimedContractIds = Array.Empty<string>();
            envelope.profile.readStoryFragmentIds = null;
            envelope.profile.contractCounters = new ContractCounterSaveData[]
            {
                null,
                new ContractCounterSaveData { contractId = "CT04", progress = 3 }
            };
            envelope.profile.processedSettlementRunIds = Array.Empty<string>();
            envelope.profile.processedContractProgressEvents = null;
            envelope.profile.progressionPurchaseTransactions = new[] { (PermanentUpgradePurchaseSaveData)null };

            ExpeditionSaveData expedition = envelope.activeExpedition;
            expedition.selectedSkillIds = null;
            expedition.startingEquipmentInstanceIds = Array.Empty<string>();
            expedition.visitedNodeIds = null;
            expedition.completedRoomIds = Array.Empty<string>();
            expedition.completedRoomResults = new ExpeditionRoomCompletionSaveData[]
            {
                null,
                new ExpeditionRoomCompletionSaveData
                {
                    roomNodeId = expedition.currentNodeId,
                    resultId = "result." + expedition.currentNodeId,
                    resultJson = "{\"value\":\"input-only\"}"
                }
            };
            expedition.pactIds = Array.Empty<string>();
            expedition.collectedItemIds = null;
            expedition.claimedRewardIds = Array.Empty<string>();
            expedition.frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveData();
            expedition.hasFrozenPermanentUpgrades = false;
            expedition.fortuneOfferBookSnapshot = new FortuneOfferBookSnapshot
            {
                snapshotVersion = 3,
                generationVersion = expedition.generationVersion,
                seed = expedition.seed,
                raceId = expedition.raceId,
                charges = 0,
                offers = null,
                processedGuardianIds = Array.Empty<string>(),
                rerollReceipts = new FortuneRerollReceiptSnapshot[] { null },
                fortuneBrewReceipts = null
            };
        }

        private static SettingsSaveEnvelope BuildSettingsEnvelope()
        {
            return new SettingsSaveEnvelope
            {
                schemaVersion = 1,
                contentVersion = 6,
                generation = 1,
                sequence = 11,
                savedAtUtc = FixedSavedAtUtc,
                checksum = string.Empty,
                settings = new GameSettingsSaveData
                {
                    languageId = "en",
                    masterVolume = 0.8f,
                    musicVolume = 0.4f,
                    effectsVolume = 0.7f,
                    ambienceVolume = 0.3f
                }
            };
        }

        // Test-only adapter for SaveRepository.ComputeChecksum at its recorded source hash.
        // Its output demonstrates current-code DTO equivalence only; it is not a historical oracle.
        private static byte[] BuildCurrentProfileProjectionBytes(ProfileSaveEnvelope source)
        {
            ExpeditionSaveData canonicalExpedition = source.hasActiveExpedition && source.activeExpedition != null
                ? source.activeExpedition.Clone()
                : null;
            var canonical = new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                profile = source.profile,
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.hasActiveExpedition ? canonicalExpedition : null
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(canonical, false));
        }

        private static byte[] BuildCurrentSettingsProjectionBytes(SettingsSaveEnvelope source)
        {
            var canonical = new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                settings = source.settings
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(canonical, false));
        }

        private static LegacyProfileSaveEnvelopeV6 AdaptProfileEnvelopeToLegacyV6(ProfileSaveEnvelope source)
        {
            return new LegacyProfileSaveEnvelopeV6
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                profile = AdaptProfileToLegacyV6(source.profile),
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = AdaptExpeditionToLegacyV6(source.activeExpedition)
            };
        }

        private static LegacySettingsSaveEnvelopeV6 AdaptSettingsEnvelopeToLegacyV6(SettingsSaveEnvelope source)
        {
            return new LegacySettingsSaveEnvelopeV6
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                settings = source.settings == null ? null : new LegacyGameSettingsSaveDataV6
                {
                    languageId = source.settings.languageId,
                    masterVolume = source.settings.masterVolume,
                    musicVolume = source.settings.musicVolume,
                    effectsVolume = source.settings.effectsVolume,
                    ambienceVolume = source.settings.ambienceVolume
                }
            };
        }

        private static LegacyProfileSaveDataV6 AdaptProfileToLegacyV6(ProfileSaveData source)
        {
            if (source == null) return null;
            return new LegacyProfileSaveDataV6
            {
                availableRenown = source.availableRenown,
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(source.unlockedRaceIds),
                unlockedStartingKitIds = Copy(source.unlockedStartingKitIds),
                acceptedContractIds = Copy(source.acceptedContractIds),
                claimedContractIds = Copy(source.claimedContractIds),
                readStoryFragmentIds = Copy(source.readStoryFragmentIds),
                contractCounters = AdaptArray(source.contractCounters, value => new LegacyContractCounterSaveDataV6
                {
                    contractId = value.contractId,
                    progress = value.progress
                }),
                processedSettlementRunIds = Copy(source.processedSettlementRunIds),
                processedContractProgressEvents = AdaptArray(source.processedContractProgressEvents,
                    value => new LegacyContractProgressEventReceiptSaveDataV6
                    {
                        eventId = value.eventId,
                        progressEvent = value.progressEvent
                    }),
                progressionPurchaseTransactions = AdaptArray(source.progressionPurchaseTransactions,
                    value => new LegacyPermanentUpgradePurchaseSaveDataV6
                    {
                        transactionId = value.transactionId,
                        kind = value.kind,
                        cost = value.cost
                    })
            };
        }

        private static LegacyExpeditionSaveDataV6 AdaptExpeditionToLegacyV6(ExpeditionSaveData source)
        {
            if (source == null) return null;
            return new LegacyExpeditionSaveDataV6
            {
                runId = source.runId,
                seed = source.seed,
                generationVersion = source.generationVersion,
                contentVersion = source.contentVersion,
                classId = source.classId,
                raceId = source.raceId,
                selectedSkillIds = Copy(source.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(source.startingEquipmentInstanceIds),
                routeSnapshotJson = source.routeSnapshotJson,
                currentNodeId = source.currentNodeId,
                visitedNodeIds = Copy(source.visitedNodeIds),
                completedRoomIds = Copy(source.completedRoomIds),
                completedRoomResults = AdaptArray(source.completedRoomResults,
                    value => new LegacyExpeditionRoomCompletionSaveDataV6
                    {
                        roomNodeId = value.roomNodeId,
                        resultId = value.resultId,
                        resultJson = value.resultJson
                    }),
                roomEntryCheckpointJson = source.roomEntryCheckpointJson,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                frozenPermanentUpgrades = source.frozenPermanentUpgrades == null ? null :
                    new LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6
                    {
                        healthUpgradeLevel = source.frozenPermanentUpgrades.healthUpgradeLevel,
                        energyUpgradeLevel = source.frozenPermanentUpgrades.energyUpgradeLevel,
                        additionalPotionUpgradeLevel = source.frozenPermanentUpgrades.additionalPotionUpgradeLevel
                    },
                hasFrozenPermanentUpgrades = source.hasFrozenPermanentUpgrades,
                accumulatedRenown = source.accumulatedRenown,
                pactIds = Copy(source.pactIds),
                fortuneOfferBookSnapshot = AdaptFortuneBookToLegacyV6(source.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(source.collectedItemIds),
                claimedRewardIds = Copy(source.claimedRewardIds)
            };
        }

        private static LegacyFortuneOfferBookSnapshotV6 AdaptFortuneBookToLegacyV6(FortuneOfferBookSnapshot source)
        {
            if (source == null) return null;
            return new LegacyFortuneOfferBookSnapshotV6
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                offers = AdaptArray(source.offers, offer => new LegacyFortuneOfferSnapshotV6
                {
                    offerId = offer.offerId,
                    contextId = offer.contextId,
                    eligibleOptionIds = Copy(offer.eligibleOptionIds),
                    history = AdaptArray(offer.history, entry => new LegacyFortuneOfferHistorySnapshotV6
                    {
                        optionIds = Copy(entry.optionIds)
                    }),
                    selectedOptionId = offer.selectedOptionId,
                    claimed = offer.claimed
                }),
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = AdaptArray(source.rerollReceipts, receipt => new LegacyFortuneRerollReceiptSnapshotV6
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
                }),
                fortuneBrewReceipts = AdaptArray(source.fortuneBrewReceipts,
                    receipt => new LegacyFortuneBrewReceiptSnapshotV6
                    {
                        transactionId = receipt.transactionId,
                        itemInstanceId = receipt.itemInstanceId,
                        status = receipt.status,
                        currentCharges = receipt.currentCharges
                    })
            };
        }

        private static LegacyExpeditionRoomCheckpointSaveDataV6 AdaptCheckpointToLegacyV6(
            ExpeditionRoomCheckpointSaveData source)
        {
            return new LegacyExpeditionRoomCheckpointSaveDataV6
            {
                roomNodeId = source.roomNodeId,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                accumulatedRenown = source.accumulatedRenown,
                fortuneOfferBookSnapshot = AdaptFortuneBookToLegacyV6(source.fortuneOfferBookSnapshot),
                pactIds = Copy(source.pactIds),
                collectedItemIds = Copy(source.collectedItemIds),
                claimedRewardIds = Copy(source.claimedRewardIds)
            };
        }

        private static T[] Copy<T>(T[] source)
        {
            return source == null ? null : (T[])source.Clone();
        }

        private static TResult[] AdaptArray<TSource, TResult>(TSource[] source, Func<TSource, TResult> adapt)
            where TSource : class
            where TResult : class
        {
            if (source == null) return null;
            var result = new TResult[source.Length];
            for (int index = 0; index < source.Length; index++)
                result[index] = source[index] == null ? null : adapt(source[index]);
            return result;
        }

        private static ExpeditionRouteCatalog CreateRouteCatalog()
        {
            return new ExpeditionRouteCatalog(new[]
            {
                CreateRouteSector("patio"),
                CreateRouteSector("archivo"),
                CreateRouteSector("forjas")
            }, "room.hub.rest", "room.final.arena");
        }

        private static ExpeditionRouteSectorCatalog CreateRouteSector(string sectorId)
        {
            return new ExpeditionRouteSectorCatalog(
                sectorId,
                Enumerable.Range(1, 5).Select(index => "room." + sectorId + ".c" + index),
                Enumerable.Range(1, 2).Select(index => "room." + sectorId + ".e" + index),
                "room." + sectorId + ".t",
                "room." + sectorId + ".p",
                "room." + sectorId + ".n",
                "room." + sectorId + ".guardian");
        }

        private static Guid FixedGuid(int seed)
        {
            // Same deterministic byte layout used by PermanentUpgradeRulesTests.Buy; no GUID generator is called.
            var bytes = new byte[16];
            bytes[0] = (byte)seed;
            bytes[15] = 1;
            return new Guid(bytes);
        }

        private static TransactionId FixedTransactionId(int seed) => new TransactionId(FixedGuid(seed));

        private static string ReadFixture(string fileName)
        {
            string fixturePath = Path.Combine(Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6", fileName);
            return File.ReadAllText(fixturePath);
        }

        private static byte[] ReadFixtureBytes(string fileName)
        {
            string fixturePath = Path.Combine(Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6", fileName);
            return File.ReadAllBytes(fixturePath);
        }

        private static bool TryReadV6CaptureArguments(out string captureValue, out string expectedUnityVersion)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            captureValue = null;
            expectedUnityVersion = V6CaptureUnityVersion;
            bool captureArgumentFound = false;
            bool expectedUnityVersionArgumentFound = false;
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                if (string.Equals(argument, V6CaptureArgument, StringComparison.Ordinal))
                {
                    if (captureArgumentFound)
                        throw new InvalidOperationException("V6 capture argument was supplied more than once.");
                    captureArgumentFound = true;
                    if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                        arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new InvalidOperationException("V6 capture argument requires one directory value.");
                    captureValue = arguments[index + 1];
                }
                else if (argument != null && argument.StartsWith(V6CaptureArgument, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("V6 capture argument must use the exact flag followed by one value.");
                }
                else if (string.Equals(argument, V6CaptureExpectedUnityVersionArgument, StringComparison.Ordinal))
                {
                    if (expectedUnityVersionArgumentFound)
                        throw new InvalidOperationException("V6 capture expected Unity version argument was supplied more than once.");
                    expectedUnityVersionArgumentFound = true;
                    if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                        arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new InvalidOperationException("V6 capture expected Unity version argument requires one version value.");
                    expectedUnityVersion = arguments[index + 1];
                }
                else if (argument != null && argument.StartsWith(V6CaptureExpectedUnityVersionArgument, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("V6 capture expected Unity version argument must use the exact flag followed by one value.");
                }
            }

            if (expectedUnityVersionArgumentFound && !captureArgumentFound)
                throw new InvalidOperationException("V6 capture expected Unity version requires the explicit capture directory opt-in.");

            return captureArgumentFound;
        }

        private static bool IsSupportedV6CaptureUnityVersion(string version)
        {
            return string.Equals(version, V6CaptureUnityVersion, StringComparison.Ordinal) ||
                string.Equals(version, V6CaptureAlternateUnityVersion, StringComparison.Ordinal);
        }

        private static bool IsSha40(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 40) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f') ||
                    (character >= 'A' && character <= 'F')))
                    return false;
            }

            return true;
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Path.GetFullPath(Application.dataPath);
            if (!string.Equals(Path.GetFileName(TrimEndingSeparatorsPreservingRoot(dataPath)), "Assets",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Application.dataPath does not identify the project's Assets directory.");

            DirectoryInfo parent = Directory.GetParent(dataPath);
            if (parent == null || !parent.Exists)
                throw new InvalidOperationException("Could not resolve the Unity project root from Application.dataPath.");
            RejectReparsePointAncestors(parent.FullName);
            return Path.GetFullPath(parent.FullName);
        }

        private static string ResolveAndCreateCaptureRoot(string projectRoot, string relativeCapturePath)
        {
            if (string.IsNullOrEmpty(relativeCapturePath) ||
                !relativeCapturePath.StartsWith(V6CaptureRelativePrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("V6 capture path must be Logs/Scenarios/<one-safe-folder-name>.");

            string folderName = relativeCapturePath.Substring(V6CaptureRelativePrefix.Length);
            if (!IsSafeCaptureFolderName(folderName))
                throw new InvalidOperationException("V6 capture folder name contains unsupported path syntax.");

            string logsPath = Path.GetFullPath(Path.Combine(projectRoot, "Logs"));
            string scenariosPath = Path.GetFullPath(Path.Combine(logsPath, "Scenarios"));
            string captureRoot = Path.GetFullPath(Path.Combine(scenariosPath, folderName));
            EnsurePathContained(captureRoot, scenariosPath);

            string assetsPath = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
            if (string.Equals(captureRoot, assetsPath, StringComparison.OrdinalIgnoreCase) ||
                captureRoot.StartsWith(assetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("V6 capture output must remain outside Assets.");

            EnsureDirectoryExistsWithoutReparse(projectRoot, logsPath);
            EnsureDirectoryExistsWithoutReparse(projectRoot, scenariosPath);
            RejectReparsePointAncestors(captureRoot);
            if (Directory.Exists(captureRoot) || File.Exists(captureRoot))
                throw new IOException("V6 capture output directory must be new and nonexistent: " + relativeCapturePath);

            Directory.CreateDirectory(captureRoot);
            RejectReparsePointAncestors(captureRoot);
            return captureRoot;
        }

        private static bool IsSafeCaptureFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || folderName.Length > 80)
                return false;

            char firstCharacter = folderName[0];
            bool firstIsLetterOrDigit = (firstCharacter >= 'A' && firstCharacter <= 'Z') ||
                (firstCharacter >= 'a' && firstCharacter <= 'z') ||
                (firstCharacter >= '0' && firstCharacter <= '9');
            if (!firstIsLetterOrDigit) return false;

            for (int index = 0; index < folderName.Length; index++)
            {
                char character = folderName[index];
                bool letterOrDigit = (character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9');
                if (!letterOrDigit && character != '-' && character != '_') return false;
            }

            return true;
        }

        private static void EnsureDirectoryExistsWithoutReparse(string projectRoot, string directoryPath)
        {
            EnsurePathContained(directoryPath, projectRoot);
            if (File.Exists(directoryPath))
                throw new IOException("V6 capture parent path is a file: " + directoryPath);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            RejectReparsePointAncestors(directoryPath);
        }

        private static void EnsurePathContained(string candidatePath, string parentPath)
        {
            string fullCandidate = Path.GetFullPath(candidatePath);
            string fullParent = TrimEndingSeparatorsPreservingRoot(Path.GetFullPath(parentPath));
            string prefix = fullParent;
            if (prefix.Length == 0 || !IsPathDirectorySeparator(prefix[prefix.Length - 1]))
                prefix += Path.DirectorySeparatorChar;
            if (!fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("V6 capture path escapes its required parent directory.");
        }

        private static string TrimEndingSeparatorsPreservingRoot(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;
            int length = fullPath.Length;
            while (length > root.Length && IsPathDirectorySeparator(fullPath[length - 1]))
                length--;
            return length == fullPath.Length ? fullPath : fullPath.Substring(0, length);
        }

        private static bool IsPathDirectorySeparator(char character)
        {
            return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
        }

        private static void RejectReparsePointAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current))
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("V6 capture path contains a reparse point: " + current);
                }

                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    break;
                current = parent;
            }
        }

        private static string ReadDeclaredEditorVersion(string projectRoot)
        {
            string projectVersionPath = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            string[] lines = File.ReadAllLines(projectVersionPath);
            for (int index = 0; index < lines.Length; index++)
            {
                const string key = "m_EditorVersion:";
                if (lines[index].StartsWith(key, StringComparison.Ordinal))
                    return lines[index].Substring(key.Length).Trim();
            }

            throw new InvalidDataException("ProjectVersion.txt does not declare m_EditorVersion.");
        }

        private static V6CaptureSourceHash[] HashCaptureSources(string projectRoot, bool tolerateReadErrors)
        {
            var hashes = new V6CaptureSourceHash[V6CaptureSourcePaths.Length];
            for (int index = 0; index < V6CaptureSourcePaths.Length; index++)
            {
                string relativePath = V6CaptureSourcePaths[index];
                string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    if (!File.Exists(fullPath)) throw new FileNotFoundException("Capture source file is missing.", fullPath);
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    hashes[index] = new V6CaptureSourceHash
                    {
                        path = relativePath,
                        bytes = bytes.LongLength,
                        sha256 = ComputeSha256(bytes),
                        error = string.Empty
                    };
                }
                catch (Exception exception)
                {
                    if (!tolerateReadErrors)
                        throw new InvalidDataException("Could not hash V6 capture source " + relativePath + ".", exception);
                    hashes[index] = new V6CaptureSourceHash
                    {
                        path = relativePath,
                        bytes = -1,
                        sha256 = string.Empty,
                        error = exception.GetType().FullName + ": " + exception.Message
                    };
                }
            }

            return hashes;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool SourceHashesEqual(V6CaptureSourceHash[] before, V6CaptureSourceHash[] after)
        {
            if (before == null || after == null || before.Length != after.Length) return false;
            for (int index = 0; index < before.Length; index++)
            {
                if (before[index] == null || after[index] == null ||
                    !string.IsNullOrEmpty(before[index].error) || !string.IsNullOrEmpty(after[index].error) ||
                    !string.Equals(before[index].path, after[index].path, StringComparison.Ordinal) ||
                    before[index].bytes != after[index].bytes ||
                    !string.Equals(before[index].sha256, after[index].sha256, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static V6CaptureRuntimeIdentity ReadJsonUtilityIdentity()
        {
            Assembly assembly = typeof(JsonUtility).Assembly;
            Module module = assembly.ManifestModule;
            return new V6CaptureRuntimeIdentity
            {
                assembly = assembly.FullName ?? string.Empty,
                module = module.Name ?? string.Empty,
                mvid = module.ModuleVersionId.ToString("D")
            };
        }

        private static V6CaptureSessionDocument CreateSessionDocument(V6CaptureSessionState session)
        {
            return new V6CaptureSessionDocument
            {
                schemaVersion = 1,
                sessionId = session.sessionId,
                commit = session.commit,
                unityVersion = session.unityVersion,
                projectVersionDeclared = session.projectVersionDeclared,
                startedAtUtc = session.startedAtUtc,
                activationArgumentObserved = true,
                activationArgument = session.activationArgument,
                activationValue = session.activationValue,
                expectedCaseIds = (string[])V6CaptureExpectedCaseIds.Clone(),
                sourceHashesBefore = session.sourceHashesBefore,
                jsonUtilityAssembly = session.runtimeIdentity.assembly,
                jsonUtilityModule = session.runtimeIdentity.module,
                jsonUtilityMvid = session.runtimeIdentity.mvid,
                cultureName = session.cultureName,
                cultureNumberDecimalSeparator = session.cultureNumberDecimalSeparator
            };
        }

        private static V6CaptureManifestDocument CreateManifestDocument(
            V6CaptureSessionState session,
            V6CaptureSourceHash[] sourceHashesAfter,
            V6CaptureAttemptRecord[] attempts,
            bool sourcesStable,
            bool sessionDocumentStable,
            bool dataComplete,
            bool roleRequirementsPassed)
        {
            return new V6CaptureManifestDocument
            {
                schemaVersion = 1,
                sessionId = session.sessionId,
                commit = session.commit,
                unityVersion = session.unityVersion,
                projectVersionDeclared = session.projectVersionDeclared,
                startedAtUtc = session.startedAtUtc,
                finalizedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                activationArgumentObserved = true,
                activationArgument = session.activationArgument,
                activationValue = session.activationValue,
                expectedCaseIds = (string[])V6CaptureExpectedCaseIds.Clone(),
                sourceHashesBefore = session.sourceHashesBefore,
                sourceHashesAfter = sourceHashesAfter,
                sourcesStable = sourcesStable,
                sessionDocumentStable = sessionDocumentStable,
                dataComplete = dataComplete,
                roleRequirementsPassed = roleRequirementsPassed,
                attempts = attempts,
                sessionFileBytes = session.sessionFileBytes,
                sessionFileSha256 = session.sessionFileSha256,
                jsonUtilityAssembly = session.runtimeIdentity.assembly,
                jsonUtilityModule = session.runtimeIdentity.module,
                jsonUtilityMvid = session.runtimeIdentity.mvid,
                cultureName = session.cultureName,
                cultureNumberDecimalSeparator = session.cultureNumberDecimalSeparator
            };
        }

        private static void WriteExclusiveAndVerify(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            byte[] writtenBytes = File.ReadAllBytes(path);
            if (!ByteArraysEqual(bytes, writtenBytes))
                throw new IOException("V6 capture file failed byte-for-byte readback verification: " + path);
        }

        private static bool VerifySessionDocument(V6CaptureSessionState session)
        {
            try
            {
                string sessionPath = Path.Combine(session.captureRoot, "session.json");
                if (!File.Exists(sessionPath) || (File.GetAttributes(sessionPath) & FileAttributes.ReparsePoint) != 0)
                    return false;
                byte[] bytes = File.ReadAllBytes(sessionPath);
                return bytes.LongLength == session.sessionFileBytes &&
                    string.Equals(ComputeSha256(bytes), session.sessionFileSha256, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }

        private static V6CaptureSessionState GetActiveV6CaptureSession()
        {
            lock (V6CaptureGate) return _v6CaptureSession;
        }

        private static void CaptureProfileAfterAssertions(
            ProfileCase profileCase,
            byte[] assertedLiveProjection,
            byte[] assertedLegacyProjection)
        {
            V6CaptureSessionState session = GetActiveV6CaptureSession();
            if (session == null) return;

            string caseId;
            string role;
            string roleExpectation;
            GetProfileCaptureDefinition(profileCase, out caseId, out role, out roleExpectation);
            V6CaptureAttemptRecord attempt = BeginV6CaptureAttempt(session, caseId,
                nameof(DeterministicProfileBuildersHaveCompleteLiveAndLegacyProjectionByteEquivalence),
                profileCase.ToString(), role);

            try
            {
                ProfileSaveEnvelope fresh = BuildProfileEnvelope(profileCase);
                LegacyProfileSaveEnvelopeV6 freshLegacy = AdaptProfileEnvelopeToLegacyV6(fresh);
                byte[] liveProjection = BuildCurrentProfileProjectionBytes(fresh);
                byte[] legacyProjection = SaveV6ChecksumInspector.GetProfileChecksumPreimage(freshLegacy);
                bool matchesAssertedProjection = ByteArraysEqual(assertedLiveProjection, liveProjection) &&
                    ByteArraysEqual(assertedLegacyProjection, legacyProjection);
                bool liveMatchesLegacy = ByteArraysEqual(liveProjection, legacyProjection);
                string inputJson = JsonUtility.ToJson(fresh, false);
                string envelopeJson = SaveJsonCodec.Encode(fresh);
                string encodedChecksum = fresh.checksum;
                string liveChecksum = ComputeSha256(liveProjection);
                V6CaptureValidationObservation validation = ObserveProfileValidation(fresh);
                V6CaptureDecodeObservation decode = ObserveProfileDecode(envelopeJson);
                V6CaptureInspectorObservation inspector = ObserveProfileInspection(
                    envelopeJson, encodedChecksum, liveProjection);

                bool semanticAcceptance = EvaluateProfileCaptureRole(profileCase, validation, decode,
                    inspector, liveChecksum, encodedChecksum, matchesAssertedProjection, liveMatchesLegacy);
                string failureMessage = semanticAcceptance ? string.Empty : BuildProfileRoleFailure(
                    profileCase, validation, decode, inspector, liveChecksum, encodedChecksum,
                    matchesAssertedProjection, liveMatchesLegacy);
                var files = new List<V6CaptureFileRecord>();
                string attemptPath = ResolveAttemptPath(session, attempt.attemptDirectory);
                AddV6CaptureFile(session, attemptPath, files, "input.json", V6CaptureUtf8NoBom.GetBytes(inputJson));
                AddV6CaptureFile(session, attemptPath, files, "envelope.json", V6CaptureUtf8NoBom.GetBytes(envelopeJson));
                AddV6CaptureFile(session, attemptPath, files, "checksum-preimage.utf8", liveProjection);
                AddV6CaptureFile(session, attemptPath, files, "checksum.sha256.txt", V6CaptureUtf8NoBom.GetBytes(liveChecksum));
                AddV6CaptureFile(session, attemptPath, files, "legacy-checksum-preimage.utf8", legacyProjection);
                if (inspector.preimageUtf8 != null)
                    AddV6CaptureFile(session, attemptPath, files, "inspector-preimage.utf8", inspector.preimageUtf8);
                AddV6CaptureFile(session, attemptPath, files, "semantic-validation.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(validation, true)));
                AddV6CaptureFile(session, attemptPath, files, "decode.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(decode, true)));
                AddV6CaptureFile(session, attemptPath, files, "inspector.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(inspector, true)));

                string[] requiredFiles =
                {
                    "input.json", "envelope.json", "checksum-preimage.utf8", "checksum.sha256.txt",
                    "legacy-checksum-preimage.utf8", "inspector-preimage.utf8", "semantic-validation.json",
                    "decode.json", "inspector.json"
                };
                var descriptor = CreateAttemptDescriptor(session, attempt, role, roleExpectation,
                    files, validation, decode, inspector, matchesAssertedProjection, liveMatchesLegacy,
                    liveProjection.LongLength, legacyProjection.LongLength);
                PublishV6CaptureAttempt(session, attempt, attemptPath, descriptor, requiredFiles,
                    semanticAcceptance, failureMessage);
            }
            catch (Exception exception)
            {
                RecordV6CaptureAttemptException(attempt, exception);
                throw;
            }
        }

        private static void GetProfileCaptureDefinition(
            ProfileCase profileCase,
            out string caseId,
            out string role,
            out string roleExpectation)
        {
            switch (profileCase)
            {
                case ProfileCase.P0NoActive:
                    caseId = "V6-P0-profile-no-active";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P1KnownFrozenZero:
                    caseId = "V6-P1-active-known-zero";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P1KnownFrozenNonTrivial:
                    caseId = "V6-P1-active-known-nontrivial";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P2UnknownNullFrozen:
                    caseId = "V6-P2-active-unknown-null";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P2UnknownEmptyInlineFrozen:
                    caseId = "V6-P2-active-unknown-inline-empty";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P3ReceiptsFull:
                    caseId = "V6-P3-receipts-full";
                    role = "positive-candidate-validity-to-measure";
                    roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
                    return;
                case ProfileCase.P4NullEmptyBoundary:
                    caseId = "V6-P4-null-empty-boundary";
                    role = "checksum-serialization-only";
                    roleExpectation = "Capture null/empty serialization and checksum only; no semantic save-validity claim.";
                    return;
                case ProfileCase.P5FalseMarkerWithPayload:
                    caseId = "V6-P5-flag-payload-conflict";
                    role = "negative-semantic-candidate";
                    roleExpectation = "Preserve marker/payload conflict and its semantic rejection; checksum validity is not acceptance.";
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profileCase), profileCase, "Unknown V6 capture profile variant.");
            }
        }

        private static void CaptureSettingsAfterAssertions(byte[] assertedLiveProjection, byte[] assertedLegacyProjection)
        {
            V6CaptureSessionState session = GetActiveV6CaptureSession();
            if (session == null) return;

            const string caseId = "V6-S0-settings";
            const string role = "positive-candidate-validity-to-measure";
            const string roleExpectation = "Require successful validation and Decode before treating this as a positive candidate.";
            V6CaptureAttemptRecord attempt = BeginV6CaptureAttempt(session, caseId,
                nameof(DeterministicSettingsBuilderHasCompleteLiveAndLegacyProjectionByteEquivalence),
                "BuildSettingsEnvelope", role);

            try
            {
                SettingsSaveEnvelope fresh = BuildSettingsEnvelope();
                LegacySettingsSaveEnvelopeV6 freshLegacy = AdaptSettingsEnvelopeToLegacyV6(fresh);
                byte[] liveProjection = BuildCurrentSettingsProjectionBytes(fresh);
                byte[] legacyProjection = SaveV6ChecksumInspector.GetSettingsChecksumPreimage(freshLegacy);
                bool matchesAssertedProjection = ByteArraysEqual(assertedLiveProjection, liveProjection) &&
                    ByteArraysEqual(assertedLegacyProjection, legacyProjection);
                bool liveMatchesLegacy = ByteArraysEqual(liveProjection, legacyProjection);
                string inputJson = JsonUtility.ToJson(fresh, false);
                string envelopeJson = SaveJsonCodec.Encode(fresh);
                string encodedChecksum = fresh.checksum;
                string liveChecksum = ComputeSha256(liveProjection);
                V6CaptureValidationObservation validation = ObserveSettingsValidation(fresh);
                V6CaptureDecodeObservation decode = ObserveSettingsDecode(envelopeJson);
                V6CaptureInspectorObservation inspector = ObserveSettingsInspection(
                    envelopeJson, encodedChecksum, liveProjection);

                bool semanticAcceptance = validation.executed && validation.accepted && decode.executed &&
                    decode.valid && InspectionConfirmsEncodedProjection(inspector, liveChecksum, encodedChecksum) &&
                    matchesAssertedProjection && liveMatchesLegacy;
                string failureMessage = semanticAcceptance ? string.Empty : BuildSettingsRoleFailure(
                    validation, decode, inspector, liveChecksum, encodedChecksum,
                    matchesAssertedProjection, liveMatchesLegacy);
                var files = new List<V6CaptureFileRecord>();
                string attemptPath = ResolveAttemptPath(session, attempt.attemptDirectory);
                AddV6CaptureFile(session, attemptPath, files, "input.json", V6CaptureUtf8NoBom.GetBytes(inputJson));
                AddV6CaptureFile(session, attemptPath, files, "envelope.json", V6CaptureUtf8NoBom.GetBytes(envelopeJson));
                AddV6CaptureFile(session, attemptPath, files, "checksum-preimage.utf8", liveProjection);
                AddV6CaptureFile(session, attemptPath, files, "checksum.sha256.txt", V6CaptureUtf8NoBom.GetBytes(liveChecksum));
                AddV6CaptureFile(session, attemptPath, files, "legacy-checksum-preimage.utf8", legacyProjection);
                if (inspector.preimageUtf8 != null)
                    AddV6CaptureFile(session, attemptPath, files, "inspector-preimage.utf8", inspector.preimageUtf8);
                AddV6CaptureFile(session, attemptPath, files, "semantic-validation.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(validation, true)));
                AddV6CaptureFile(session, attemptPath, files, "decode.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(decode, true)));
                AddV6CaptureFile(session, attemptPath, files, "inspector.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(inspector, true)));

                string[] requiredFiles =
                {
                    "input.json", "envelope.json", "checksum-preimage.utf8", "checksum.sha256.txt",
                    "legacy-checksum-preimage.utf8", "inspector-preimage.utf8", "semantic-validation.json",
                    "decode.json", "inspector.json"
                };
                var descriptor = CreateAttemptDescriptor(session, attempt, role, roleExpectation,
                    files, validation, decode, inspector, matchesAssertedProjection, liveMatchesLegacy,
                    liveProjection.LongLength, legacyProjection.LongLength);
                PublishV6CaptureAttempt(session, attempt, attemptPath, descriptor, requiredFiles,
                    semanticAcceptance, failureMessage);
            }
            catch (Exception exception)
            {
                RecordV6CaptureAttemptException(attempt, exception);
                throw;
            }
        }

        private static void CaptureCheckpointAfterAssertions(bool includeNullSlots, byte[] liveBytes, byte[] legacyBytes)
        {
            V6CaptureSessionState session = GetActiveV6CaptureSession();
            if (session == null) return;

            string caseId = includeNullSlots ? "V6-checkpoint-null-slots" : "V6-checkpoint-empty-slots";
            string variant = "includeNullSlots=" + includeNullSlots.ToString().ToLowerInvariant();
            string role = includeNullSlots
                ? "checkpoint-null-boundary-dto-json-separated-not-envelope"
                : "checkpoint-dto-json-separated-not-envelope";
            string roleExpectation = "Capture separate checkpoint DTO JSON only; no envelope, checksum, or save-validity claim.";
            V6CaptureAttemptRecord attempt = BeginV6CaptureAttempt(session, caseId,
                nameof(RoomEntryCheckpointUsesCompleteLiveAndLegacyDtoByteEquivalence), variant, role);

            try
            {
                bool liveLegacyBytesEqual = ByteArraysEqual(liveBytes, legacyBytes);
                var validation = CreateNotApplicableValidation("Not executed for the separate checkpoint DTO role.");
                var decode = CreateNotApplicableDecode("Not executed for the separate checkpoint DTO role.");
                var inspector = CreateNotApplicableInspector("Not executed for the separate checkpoint DTO role.");
                var comparison = new V6CaptureCheckpointComparison
                {
                    role = role,
                    liveLegacyBytesEqual = liveLegacyBytesEqual,
                    liveByteCount = liveBytes == null ? -1 : liveBytes.LongLength,
                    legacyByteCount = legacyBytes == null ? -1 : legacyBytes.LongLength,
                    claim = "Separate DTO serializer equivalence only; no profile/settings envelope was produced."
                };

                var files = new List<V6CaptureFileRecord>();
                string attemptPath = ResolveAttemptPath(session, attempt.attemptDirectory);
                AddV6CaptureFile(session, attemptPath, files, "checkpoint-current.json", liveBytes);
                AddV6CaptureFile(session, attemptPath, files, "checkpoint-legacy.json", legacyBytes);
                AddV6CaptureFile(session, attemptPath, files, "checkpoint-current.sha256.txt",
                    V6CaptureUtf8NoBom.GetBytes(ComputeSha256(liveBytes)));
                AddV6CaptureFile(session, attemptPath, files, "checkpoint-legacy.sha256.txt",
                    V6CaptureUtf8NoBom.GetBytes(ComputeSha256(legacyBytes)));
                AddV6CaptureFile(session, attemptPath, files, "checkpoint-comparison.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(comparison, true)));
                AddV6CaptureFile(session, attemptPath, files, "semantic-validation.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(validation, true)));
                AddV6CaptureFile(session, attemptPath, files, "decode.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(decode, true)));
                AddV6CaptureFile(session, attemptPath, files, "inspector.json",
                    V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(inspector, true)));

                string[] requiredFiles =
                {
                    "checkpoint-current.json", "checkpoint-legacy.json", "checkpoint-current.sha256.txt",
                    "checkpoint-legacy.sha256.txt", "checkpoint-comparison.json", "semantic-validation.json",
                    "decode.json", "inspector.json"
                };
                var descriptor = CreateAttemptDescriptor(session, attempt, role, roleExpectation,
                    files, validation, decode, inspector, liveLegacyBytesEqual, liveLegacyBytesEqual,
                    liveBytes == null ? -1 : liveBytes.LongLength,
                    legacyBytes == null ? -1 : legacyBytes.LongLength);
                descriptor.checkpointComparison = comparison;
                PublishV6CaptureAttempt(session, attempt, attemptPath, descriptor, requiredFiles,
                    liveLegacyBytesEqual, liveLegacyBytesEqual ? string.Empty :
                        "The already-asserted live and Legacy checkpoint bytes differed during capture.");
            }
            catch (Exception exception)
            {
                RecordV6CaptureAttemptException(attempt, exception);
                throw;
            }
        }

        private static V6CaptureAttemptRecord BeginV6CaptureAttempt(
            V6CaptureSessionState session,
            string caseId,
            string testMethod,
            string variant,
            string role)
        {
            lock (session.gate)
            {
                int attemptNumber;
                session.attemptCounters.TryGetValue(caseId, out attemptNumber);
                attemptNumber++;
                session.attemptCounters[caseId] = attemptNumber;

                string casesPath = Path.Combine(session.captureRoot, "cases");
                EnsureDirectoryExistsWithoutReparse(session.captureRoot, casesPath);
                string casePath = Path.Combine(casesPath, caseId);
                EnsureDirectoryExistsWithoutReparse(casesPath, casePath);
                string attemptName = "attempt-" + attemptNumber.ToString("D4", CultureInfo.InvariantCulture);
                string attemptPath = Path.Combine(casePath, attemptName);
                if (Directory.Exists(attemptPath) || File.Exists(attemptPath))
                    throw new IOException("V6 capture attempt directory already exists: " + attemptName);
                Directory.CreateDirectory(attemptPath);
                RejectReparsePointAncestors(attemptPath);

                string attemptDirectory = "cases/" + caseId + "/" + attemptName;
                var attempt = new V6CaptureAttemptRecord
                {
                    caseId = caseId,
                    attemptDirectory = attemptDirectory,
                    descriptorPath = attemptDirectory + "/descriptor.json",
                    complete = false,
                    semanticAcceptance = false,
                    role = role,
                    testMethod = testMethod,
                    variant = variant,
                    startedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    failureCode = string.Empty,
                    failureMessage = string.Empty
                };
                session.attempts.Add(attempt);
                return attempt;
            }
        }

        private static string ResolveAttemptPath(V6CaptureSessionState session, string attemptDirectory)
        {
            string fullPath = Path.GetFullPath(Path.Combine(session.captureRoot,
                attemptDirectory.Replace('/', Path.DirectorySeparatorChar)));
            EnsurePathContained(fullPath, session.captureRoot);
            RejectReparsePointAncestors(fullPath);
            return fullPath;
        }

        private static void AddV6CaptureFile(
            V6CaptureSessionState session,
            string attemptPath,
            List<V6CaptureFileRecord> files,
            string fileName,
            byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes), "Cannot export a null V6 capture file.");
            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0 || fileName == "." || fileName == "..")
                throw new InvalidOperationException("V6 capture output filename is not a safe leaf name.");

            string path = Path.Combine(attemptPath, fileName);
            EnsurePathContained(path, attemptPath);
            RejectReparsePointAncestors(attemptPath);
            WriteExclusiveAndVerify(path, bytes);
            byte[] verifiedBytes = File.ReadAllBytes(path);
            files.Add(new V6CaptureFileRecord
            {
                path = fileName,
                bytes = verifiedBytes.LongLength,
                sha256 = ComputeSha256(verifiedBytes)
            });
        }

        private static V6CaptureValidationObservation ObserveProfileValidation(ProfileSaveEnvelope envelope)
        {
            var observation = new V6CaptureValidationObservation
            {
                method = "SaveDataValidation.Validate(ProfileSaveEnvelope)",
                executed = true,
                result = "Not completed",
                accepted = false,
                errors = Array.Empty<string>()
            };
            try
            {
                observation.errors = SaveDataValidation.Validate(envelope).ToArray();
                observation.accepted = observation.errors.Length == 0;
                observation.result = "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static V6CaptureValidationObservation ObserveSettingsValidation(SettingsSaveEnvelope envelope)
        {
            var observation = new V6CaptureValidationObservation
            {
                method = "SaveDataValidation.Validate(SettingsSaveEnvelope)",
                executed = true,
                result = "Not completed",
                accepted = false,
                errors = Array.Empty<string>()
            };
            try
            {
                observation.errors = SaveDataValidation.Validate(envelope).ToArray();
                observation.accepted = observation.errors.Length == 0;
                observation.result = "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static V6CaptureDecodeObservation ObserveProfileDecode(string envelopeJson)
        {
            var observation = new V6CaptureDecodeObservation
            {
                method = "SaveJsonCodec.Decode(string, out ProfileSaveEnvelope)",
                executed = true,
                result = "Not completed",
                status = string.Empty,
                valid = false
            };
            try
            {
                ProfileSaveEnvelope decoded;
                DecodeStatus status = SaveJsonCodec.Decode(envelopeJson, out decoded);
                observation.status = status.ToString();
                observation.valid = status == DecodeStatus.Valid && decoded != null;
                observation.result = decoded == null ? "Returned; envelope is null" : "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static V6CaptureDecodeObservation ObserveSettingsDecode(string envelopeJson)
        {
            var observation = new V6CaptureDecodeObservation
            {
                method = "SaveJsonCodec.Decode(string, out SettingsSaveEnvelope)",
                executed = true,
                result = "Not completed",
                status = string.Empty,
                valid = false
            };
            try
            {
                SettingsSaveEnvelope decoded;
                DecodeStatus status = SaveJsonCodec.Decode(envelopeJson, out decoded);
                observation.status = status.ToString();
                observation.valid = status == DecodeStatus.Valid && decoded != null;
                observation.result = decoded == null ? "Returned; envelope is null" : "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static V6CaptureInspectorObservation ObserveProfileInspection(
            string envelopeJson,
            string encodedChecksum,
            byte[] liveProjection)
        {
            var observation = new V6CaptureInspectorObservation
            {
                method = "SaveV6ChecksumInspector.InspectProfile(string)",
                executed = true,
                result = "Not completed",
                status = string.Empty,
                encodeChecksum = encodedChecksum ?? string.Empty,
                preimageAvailable = false,
                preimageMatchesLiveProjection = false,
                storedChecksumMatchesEncode = false,
                computedChecksumMatchesEncode = false,
                preimageUtf8 = null
            };
            try
            {
                SaveV6ChecksumInspection inspection = SaveV6ChecksumInspector.InspectProfile(envelopeJson);
                if (inspection == null) throw new InvalidDataException("Inspector returned no observation.");
                observation.status = inspection.Status.ToString();
                observation.storedChecksum = inspection.StoredChecksum ?? string.Empty;
                observation.computedChecksum = inspection.ComputedChecksum ?? string.Empty;
                observation.storedChecksumMatchesEncode = string.Equals(
                    observation.storedChecksum, encodedChecksum, StringComparison.Ordinal);
                observation.computedChecksumMatchesEncode = string.Equals(
                    observation.computedChecksum, encodedChecksum, StringComparison.Ordinal);
                observation.preimageUtf8 = inspection.GetPreimageUtf8();
                observation.preimageAvailable = observation.preimageUtf8 != null;
                observation.preimageMatchesLiveProjection = ByteArraysEqual(
                    observation.preimageUtf8, liveProjection);
                observation.result = "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static V6CaptureInspectorObservation ObserveSettingsInspection(
            string envelopeJson,
            string encodedChecksum,
            byte[] liveProjection)
        {
            var observation = new V6CaptureInspectorObservation
            {
                method = "SaveV6ChecksumInspector.InspectSettings(string)",
                executed = true,
                result = "Not completed",
                status = string.Empty,
                encodeChecksum = encodedChecksum ?? string.Empty,
                preimageAvailable = false,
                preimageMatchesLiveProjection = false,
                storedChecksumMatchesEncode = false,
                computedChecksumMatchesEncode = false,
                preimageUtf8 = null
            };
            try
            {
                SaveV6ChecksumInspection inspection = SaveV6ChecksumInspector.InspectSettings(envelopeJson);
                if (inspection == null) throw new InvalidDataException("Inspector returned no observation.");
                observation.status = inspection.Status.ToString();
                observation.storedChecksum = inspection.StoredChecksum ?? string.Empty;
                observation.computedChecksum = inspection.ComputedChecksum ?? string.Empty;
                observation.storedChecksumMatchesEncode = string.Equals(
                    observation.storedChecksum, encodedChecksum, StringComparison.Ordinal);
                observation.computedChecksumMatchesEncode = string.Equals(
                    observation.computedChecksum, encodedChecksum, StringComparison.Ordinal);
                observation.preimageUtf8 = inspection.GetPreimageUtf8();
                observation.preimageAvailable = observation.preimageUtf8 != null;
                observation.preimageMatchesLiveProjection = ByteArraysEqual(
                    observation.preimageUtf8, liveProjection);
                observation.result = "Returned";
            }
            catch (Exception exception)
            {
                SetObservationException(observation, exception);
            }

            return observation;
        }

        private static bool EvaluateProfileCaptureRole(
            ProfileCase profileCase,
            V6CaptureValidationObservation validation,
            V6CaptureDecodeObservation decode,
            V6CaptureInspectorObservation inspector,
            string liveChecksum,
            string encodedChecksum,
            bool matchesAssertedProjection,
            bool liveMatchesLegacy)
        {
            bool checksumAndProjection = InspectionConfirmsEncodedProjection(inspector,
                liveChecksum, encodedChecksum) && matchesAssertedProjection && liveMatchesLegacy;
            if (profileCase == ProfileCase.P4NullEmptyBoundary)
                return checksumAndProjection;

            if (profileCase == ProfileCase.P5FalseMarkerWithPayload)
            {
                const string markerError = "Active expedition marker must match the saved expedition payload.";
                bool expectedSemanticRejection = validation.executed && validation.errors != null &&
                    validation.errors.Any(error => error != null && error.Contains(markerError)) &&
                    decode.executed && string.Equals(decode.status, DecodeStatus.Corrupted.ToString(), StringComparison.Ordinal);
                return checksumAndProjection && expectedSemanticRejection;
            }

            return checksumAndProjection && validation.executed && validation.accepted &&
                decode.executed && decode.valid && string.Equals(decode.status,
                    DecodeStatus.Valid.ToString(), StringComparison.Ordinal);
        }

        private static bool InspectionConfirmsEncodedProjection(
            V6CaptureInspectorObservation inspector,
            string liveChecksum,
            string encodedChecksum)
        {
            return inspector != null && inspector.executed &&
                string.Equals(inspector.status, SaveV6ChecksumStatus.ChecksumValid.ToString(), StringComparison.Ordinal) &&
                inspector.preimageAvailable && inspector.preimageMatchesLiveProjection &&
                inspector.storedChecksumMatchesEncode && inspector.computedChecksumMatchesEncode &&
                string.Equals(liveChecksum, encodedChecksum, StringComparison.Ordinal);
        }

        private static string BuildProfileRoleFailure(
            ProfileCase profileCase,
            V6CaptureValidationObservation validation,
            V6CaptureDecodeObservation decode,
            V6CaptureInspectorObservation inspector,
            string liveChecksum,
            string encodedChecksum,
            bool matchesAssertedProjection,
            bool liveMatchesLegacy)
        {
            return "Profile role requirement failed for " + profileCase + ": projectionMatchesAssertions=" +
                matchesAssertedProjection + ", liveMatchesLegacy=" + liveMatchesLegacy +
                ", validation=" + DescribeValidation(validation) + ", decode=" + DescribeDecode(decode) +
                ", inspector=" + DescribeInspector(inspector) + ", liveDigestMatchesEncode=" +
                string.Equals(liveChecksum, encodedChecksum, StringComparison.Ordinal) + ".";
        }

        private static string BuildSettingsRoleFailure(
            V6CaptureValidationObservation validation,
            V6CaptureDecodeObservation decode,
            V6CaptureInspectorObservation inspector,
            string liveChecksum,
            string encodedChecksum,
            bool matchesAssertedProjection,
            bool liveMatchesLegacy)
        {
            return "Settings positive-candidate role requirement failed: projectionMatchesAssertions=" +
                matchesAssertedProjection + ", liveMatchesLegacy=" + liveMatchesLegacy +
                ", validation=" + DescribeValidation(validation) + ", decode=" + DescribeDecode(decode) +
                ", inspector=" + DescribeInspector(inspector) + ", liveDigestMatchesEncode=" +
                string.Equals(liveChecksum, encodedChecksum, StringComparison.Ordinal) + ".";
        }

        private static string DescribeValidation(V6CaptureValidationObservation observation)
        {
            if (observation == null) return "missing";
            return observation.result + "; accepted=" + observation.accepted + "; errors=" +
                (observation.errors == null ? "null" : string.Join(" | ", observation.errors));
        }

        private static string DescribeDecode(V6CaptureDecodeObservation observation)
        {
            if (observation == null) return "missing";
            return observation.result + "; status=" + observation.status + "; valid=" + observation.valid;
        }

        private static string DescribeInspector(V6CaptureInspectorObservation observation)
        {
            if (observation == null) return "missing";
            return observation.result + "; status=" + observation.status + "; stored=" +
                observation.storedChecksum + "; computed=" + observation.computedChecksum +
                "; preimageMatchesLive=" + observation.preimageMatchesLiveProjection;
        }

        private static V6CaptureValidationObservation CreateNotApplicableValidation(string reason)
        {
            return new V6CaptureValidationObservation
            {
                method = "Not executed: no complete save envelope exists for checkpoint DTO capture.",
                executed = false,
                result = "NotApplicable",
                accepted = false,
                errors = Array.Empty<string>(),
                errorMessage = reason
            };
        }

        private static V6CaptureDecodeObservation CreateNotApplicableDecode(string reason)
        {
            return new V6CaptureDecodeObservation
            {
                method = "Not executed: no complete save envelope exists for checkpoint DTO capture.",
                executed = false,
                result = "NotApplicable",
                status = string.Empty,
                valid = false,
                errorMessage = reason
            };
        }

        private static V6CaptureInspectorObservation CreateNotApplicableInspector(string reason)
        {
            return new V6CaptureInspectorObservation
            {
                method = "Not executed: no complete save envelope exists for checkpoint DTO capture.",
                executed = false,
                result = "NotApplicable",
                status = string.Empty,
                encodeChecksum = string.Empty,
                preimageAvailable = false,
                preimageMatchesLiveProjection = false,
                storedChecksumMatchesEncode = false,
                computedChecksumMatchesEncode = false,
                storedChecksum = string.Empty,
                computedChecksum = string.Empty,
                errorMessage = reason,
                preimageUtf8 = null
            };
        }

        private static void SetObservationException(V6CaptureValidationObservation observation, Exception exception)
        {
            observation.result = "Threw";
            observation.accepted = false;
            observation.errorType = exception.GetType().FullName;
            observation.errorMessage = exception.Message;
        }

        private static void SetObservationException(V6CaptureDecodeObservation observation, Exception exception)
        {
            observation.result = "Threw";
            observation.valid = false;
            observation.errorType = exception.GetType().FullName;
            observation.errorMessage = exception.Message;
        }

        private static void SetObservationException(V6CaptureInspectorObservation observation, Exception exception)
        {
            observation.result = "Threw";
            observation.status = string.Empty;
            observation.errorType = exception.GetType().FullName;
            observation.errorMessage = exception.Message;
            observation.preimageUtf8 = null;
        }

        private static V6CaptureAttemptDescriptor CreateAttemptDescriptor(
            V6CaptureSessionState session,
            V6CaptureAttemptRecord attempt,
            string role,
            string roleExpectation,
            List<V6CaptureFileRecord> files,
            V6CaptureValidationObservation validation,
            V6CaptureDecodeObservation decode,
            V6CaptureInspectorObservation inspector,
            bool matchesAssertedProjection,
            bool liveMatchesLegacy,
            long liveByteCount,
            long legacyByteCount)
        {
            return new V6CaptureAttemptDescriptor
            {
                schemaVersion = 1,
                sessionId = session.sessionId,
                commit = session.commit,
                unityVersion = session.unityVersion,
                projectVersionDeclared = session.projectVersionDeclared,
                caseId = attempt.caseId,
                attemptDirectory = attempt.attemptDirectory,
                descriptorPath = attempt.descriptorPath,
                testMethod = attempt.testMethod,
                variant = attempt.variant,
                role = role,
                roleExpectation = roleExpectation,
                startedAtUtc = attempt.startedAtUtc,
                observedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                activationArgumentObserved = true,
                activationArgument = session.activationArgument,
                activationValue = session.activationValue,
                sourceHashesBefore = session.sourceHashesBefore,
                jsonUtilityAssembly = session.runtimeIdentity.assembly,
                jsonUtilityModule = session.runtimeIdentity.module,
                jsonUtilityMvid = session.runtimeIdentity.mvid,
                cultureName = session.cultureName,
                cultureNumberDecimalSeparator = session.cultureNumberDecimalSeparator,
                files = files.ToArray(),
                semanticValidation = validation,
                decode = decode,
                inspector = inspector,
                liveProjectionMatchesTestAssertions = matchesAssertedProjection,
                liveProjectionMatchesLegacyProjection = liveMatchesLegacy,
                liveProjectionByteCount = liveByteCount,
                legacyProjectionByteCount = legacyByteCount,
                complete = false,
                semanticAcceptance = false,
                semanticAcceptanceMeaning = "Role-specific requirement only; see roleExpectation and separate observations.",
                failureCode = string.Empty,
                failureMessage = string.Empty
            };
        }

        private static void PublishV6CaptureAttempt(
            V6CaptureSessionState session,
            V6CaptureAttemptRecord attempt,
            string attemptPath,
            V6CaptureAttemptDescriptor descriptor,
            string[] requiredFiles,
            bool semanticAcceptance,
            string roleFailureMessage)
        {
            bool filesComplete = requiredFiles.All(required => descriptor.files.Any(file =>
                string.Equals(file.path, required, StringComparison.Ordinal)));
            descriptor.complete = filesComplete;
            descriptor.semanticAcceptance = semanticAcceptance;
            descriptor.failureCode = !filesComplete ? "RequiredCaptureFileMissing" :
                semanticAcceptance ? string.Empty : "RoleRequirementFailed";
            descriptor.failureMessage = !filesComplete
                ? "One or more required output files were not emitted."
                : roleFailureMessage ?? string.Empty;
            descriptor.observedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            string descriptorPath = Path.Combine(attemptPath, "descriptor.json");
            EnsurePathContained(descriptorPath, attemptPath);
            RejectReparsePointAncestors(attemptPath);
            WriteExclusiveAndVerify(descriptorPath,
                V6CaptureUtf8NoBom.GetBytes(JsonUtility.ToJson(descriptor, true)));

            attempt.complete = filesComplete;
            attempt.semanticAcceptance = semanticAcceptance;
            attempt.failureCode = descriptor.failureCode;
            attempt.failureMessage = descriptor.failureMessage;
            attempt.fileCount = descriptor.files.Length;
            attempt.finishedAtUtc = descriptor.observedAtUtc;
            attempt.descriptorPublished = true;

            if (!filesComplete)
                throw new AssertionException("V6 capture attempt did not emit all required files for " + attempt.caseId + ".");
            if (!semanticAcceptance)
                throw new AssertionException("V6 capture role failed for " + attempt.caseId + ": " + roleFailureMessage);
        }

        private static void RecordV6CaptureAttemptException(V6CaptureAttemptRecord attempt, Exception exception)
        {
            if (attempt == null || attempt.descriptorPublished) return;
            attempt.complete = false;
            attempt.semanticAcceptance = false;
            attempt.failureCode = "CaptureAttemptThrew";
            attempt.failureMessage = exception.GetType().FullName + ": " + exception.Message;
            attempt.finishedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        private static V6CaptureAttemptRecord[] CollectCaptureAttempts(V6CaptureSessionState session)
        {
            var records = new Dictionary<string, V6CaptureAttemptRecord>(StringComparer.Ordinal);
            var tracked = new Dictionary<string, V6CaptureAttemptRecord>(StringComparer.Ordinal);
            for (int index = 0; index < session.attempts.Count; index++)
            {
                V6CaptureAttemptRecord attempt = session.attempts[index];
                if (attempt != null && !string.IsNullOrEmpty(attempt.attemptDirectory))
                    tracked[attempt.attemptDirectory] = attempt;
            }

            string casesPath = Path.Combine(session.captureRoot, "cases");
            if (Directory.Exists(casesPath))
            {
                try
                {
                    RejectReparsePointAncestors(casesPath);
                    string[] caseDirectories = Directory.GetDirectories(casesPath);
                    for (int caseIndex = 0; caseIndex < caseDirectories.Length; caseIndex++)
                    {
                        string casePath = caseDirectories[caseIndex];
                        string caseId = Path.GetFileName(casePath);
                        bool knownCase = V6CaptureExpectedCaseIds.Contains(caseId, StringComparer.Ordinal);
                        try
                        {
                            RejectReparsePointAncestors(casePath);
                        }
                        catch (Exception exception)
                        {
                            string relative = "cases/" + caseId;
                            records[relative] = CreateIncompleteAttemptRecord(caseId, relative,
                                "cases/" + caseId + "/descriptor.json", "ReparsePoint", exception.Message,
                                knownCase);
                            continue;
                        }

                        string[] attemptDirectories = Directory.GetDirectories(casePath);
                        if (attemptDirectories.Length == 0)
                        {
                            string relative = "cases/" + caseId;
                            V6CaptureAttemptRecord trackedCase;
                            if (!tracked.TryGetValue(relative, out trackedCase))
                                records[relative] = CreateIncompleteAttemptRecord(caseId, relative,
                                    relative + "/descriptor.json", "AttemptDirectoryMissing",
                                    "Case folder exists without an attempt directory.", knownCase);
                            continue;
                        }

                        for (int attemptIndex = 0; attemptIndex < attemptDirectories.Length; attemptIndex++)
                        {
                            string attemptPath = attemptDirectories[attemptIndex];
                            string attemptName = Path.GetFileName(attemptPath);
                            string attemptRelative = "cases/" + caseId + "/" + attemptName;
                            string descriptorRelative = attemptRelative + "/descriptor.json";
                            V6CaptureAttemptRecord trackedAttempt;
                            tracked.TryGetValue(attemptRelative, out trackedAttempt);
                            V6CaptureAttemptRecord collected = ReadCaptureAttemptFromDisk(
                                session, attemptPath, caseId, attemptName, knownCase, trackedAttempt);
                            records[attemptRelative] = collected;
                        }
                    }
                }
                catch (Exception exception)
                {
                    const string relative = "cases/__enumeration-error__";
                    records[relative] = CreateIncompleteAttemptRecord("__enumeration-error__", relative,
                        relative + "/descriptor.json", "AttemptEnumerationFailed",
                        exception.GetType().FullName + ": " + exception.Message, false);
                }
            }

            foreach (KeyValuePair<string, V6CaptureAttemptRecord> entry in tracked)
            {
                if (records.ContainsKey(entry.Key)) continue;
                V6CaptureAttemptRecord original = entry.Value;
                records[entry.Key] = CreateIncompleteAttemptRecord(original.caseId, original.attemptDirectory,
                    original.descriptorPath, original.failureCode.Length == 0 ? "AttemptDirectoryMissing" : original.failureCode,
                    original.failureMessage.Length == 0 ? "Tracked attempt directory is missing at finalization." : original.failureMessage,
                    V6CaptureExpectedCaseIds.Contains(original.caseId, StringComparer.Ordinal));
            }

            return records.Values
                .OrderBy(record => record.caseId, StringComparer.Ordinal)
                .ThenBy(record => record.attemptDirectory, StringComparer.Ordinal)
                .ToArray();
        }

        private static V6CaptureAttemptRecord ReadCaptureAttemptFromDisk(
            V6CaptureSessionState session,
            string attemptPath,
            string caseId,
            string attemptName,
            bool knownCase,
            V6CaptureAttemptRecord trackedAttempt)
        {
            string attemptRelative = "cases/" + caseId + "/" + attemptName;
            string descriptorRelative = attemptRelative + "/descriptor.json";
            try
            {
                RejectReparsePointAncestors(attemptPath);
            }
            catch (Exception exception)
            {
                return CreateIncompleteAttemptRecord(caseId, attemptRelative, descriptorRelative,
                    "ReparsePoint", exception.Message, knownCase);
            }

            string descriptorPath = Path.Combine(attemptPath, "descriptor.json");
            if (!File.Exists(descriptorPath))
            {
                string failureCode = trackedAttempt != null && !string.IsNullOrEmpty(trackedAttempt.failureCode)
                    ? trackedAttempt.failureCode : "DescriptorMissing";
                string failureMessage = trackedAttempt != null && !string.IsNullOrEmpty(trackedAttempt.failureMessage)
                    ? trackedAttempt.failureMessage : "Attempt directory has no final descriptor.";
                return CreateIncompleteAttemptRecord(caseId, attemptRelative, descriptorRelative,
                    failureCode, failureMessage, knownCase);
            }

            try
            {
                FileAttributes descriptorAttributes = File.GetAttributes(descriptorPath);
                if ((descriptorAttributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Attempt descriptor is a reparse point.");
                byte[] descriptorBytes = File.ReadAllBytes(descriptorPath);
                string descriptorJson = V6CaptureUtf8NoBom.GetString(descriptorBytes);
                V6CaptureAttemptDescriptor descriptor = JsonUtility.FromJson<V6CaptureAttemptDescriptor>(descriptorJson);
                if (descriptor == null) throw new InvalidDataException("Attempt descriptor could not be parsed.");

                bool identityMatches = string.Equals(descriptor.caseId, caseId, StringComparison.Ordinal) &&
                    string.Equals(descriptor.attemptDirectory, attemptRelative, StringComparison.Ordinal) &&
                    string.Equals(descriptor.descriptorPath, descriptorRelative, StringComparison.Ordinal) &&
                    string.Equals(descriptor.sessionId, session.sessionId, StringComparison.Ordinal) &&
                    string.Equals(descriptor.commit, session.commit, StringComparison.Ordinal) &&
                    SourceHashesEqual(session.sourceHashesBefore, descriptor.sourceHashesBefore);
                bool filesVerified = VerifyCaptureAttemptFiles(attemptPath, descriptor.files, caseId);
                bool complete = knownCase && identityMatches && descriptor.complete && filesVerified;
                string failureCode = descriptor.failureCode ?? string.Empty;
                string failureMessage = descriptor.failureMessage ?? string.Empty;
                if (!knownCase)
                {
                    failureCode = "UnexpectedCaseId";
                    failureMessage = "Attempt directory uses a case ID outside the fixed eleven-case contract.";
                }
                else if (!identityMatches)
                {
                    failureCode = "DescriptorIdentityMismatch";
                    failureMessage = "Attempt descriptor identity or source snapshot did not match the active session.";
                }
                else if (!filesVerified)
                {
                    failureCode = "AttemptFileVerificationFailed";
                    failureMessage = "Attempt files were missing, extra, reparse-backed, or did not match descriptor hashes.";
                }
                else if (!descriptor.complete && string.IsNullOrEmpty(failureCode))
                {
                    failureCode = "DescriptorMarkedIncomplete";
                    failureMessage = "Attempt descriptor recorded incomplete required outputs.";
                }

                return new V6CaptureAttemptRecord
                {
                    caseId = caseId,
                    attemptDirectory = attemptRelative,
                    descriptorPath = descriptorRelative,
                    complete = complete,
                    semanticAcceptance = knownCase && identityMatches && descriptor.semanticAcceptance,
                    role = descriptor.role ?? string.Empty,
                    testMethod = descriptor.testMethod ?? string.Empty,
                    variant = descriptor.variant ?? string.Empty,
                    startedAtUtc = descriptor.startedAtUtc ?? string.Empty,
                    finishedAtUtc = descriptor.observedAtUtc ?? string.Empty,
                    failureCode = failureCode,
                    failureMessage = failureMessage,
                    fileCount = descriptor.files == null ? 0 : descriptor.files.Length,
                    descriptorBytes = descriptorBytes.LongLength,
                    descriptorSha256 = ComputeSha256(descriptorBytes),
                    descriptorPublished = true
                };
            }
            catch (Exception exception)
            {
                return CreateIncompleteAttemptRecord(caseId, attemptRelative, descriptorRelative,
                    "DescriptorReadFailed", exception.GetType().FullName + ": " + exception.Message, knownCase);
            }
        }

        private static bool VerifyCaptureAttemptFiles(
            string attemptPath,
            V6CaptureFileRecord[] files,
            string caseId)
        {
            if (files == null || files.Length == 0) return false;
            string[] requiredFiles = RequiredCaptureFilesForCase(caseId);
            if (requiredFiles == null) return false;

            var declaredNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
            {
                V6CaptureFileRecord file = files[index];
                if (file == null || !IsSafeCaptureFileLeaf(file.path) || !declaredNames.Add(file.path)) return false;
                string filePath = Path.Combine(attemptPath, file.path);
                EnsurePathContained(filePath, attemptPath);
                if (!File.Exists(filePath)) return false;
                FileAttributes attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.LongLength != file.bytes ||
                    !string.Equals(ComputeSha256(bytes), file.sha256, StringComparison.Ordinal))
                    return false;
            }

            if (!requiredFiles.All(declaredNames.Contains)) return false;
            string[] actualFiles = Directory.GetFiles(attemptPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToArray();
            if (actualFiles.Length != declaredNames.Count + 1 ||
                !actualFiles.Contains("descriptor.json", StringComparer.Ordinal))
                return false;
            for (int index = 0; index < actualFiles.Length; index++)
                if (!string.Equals(actualFiles[index], "descriptor.json", StringComparison.Ordinal) &&
                    !declaredNames.Contains(actualFiles[index]))
                    return false;
            return Directory.GetDirectories(attemptPath, "*", SearchOption.TopDirectoryOnly).Length == 0;
        }

        private static string[] RequiredCaptureFilesForCase(string caseId)
        {
            if (string.Equals(caseId, "V6-checkpoint-empty-slots", StringComparison.Ordinal) ||
                string.Equals(caseId, "V6-checkpoint-null-slots", StringComparison.Ordinal))
            {
                return new[]
                {
                    "checkpoint-current.json", "checkpoint-legacy.json", "checkpoint-current.sha256.txt",
                    "checkpoint-legacy.sha256.txt", "checkpoint-comparison.json", "semantic-validation.json",
                    "decode.json", "inspector.json"
                };
            }

            if (string.Equals(caseId, "V6-S0-settings", StringComparison.Ordinal) ||
                caseId.StartsWith("V6-P", StringComparison.Ordinal))
            {
                return new[]
                {
                    "input.json", "envelope.json", "checksum-preimage.utf8", "checksum.sha256.txt",
                    "legacy-checksum-preimage.utf8", "inspector-preimage.utf8", "semantic-validation.json",
                    "decode.json", "inspector.json"
                };
            }

            return null;
        }

        private static bool IsSafeCaptureFileLeaf(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && fileName != "." && fileName != ".." &&
                fileName.IndexOf('/') < 0 && fileName.IndexOf('\\') < 0 &&
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static V6CaptureAttemptRecord CreateIncompleteAttemptRecord(
            string caseId,
            string attemptDirectory,
            string descriptorPath,
            string failureCode,
            string failureMessage,
            bool knownCase)
        {
            return new V6CaptureAttemptRecord
            {
                caseId = caseId ?? string.Empty,
                attemptDirectory = attemptDirectory ?? string.Empty,
                descriptorPath = descriptorPath ?? string.Empty,
                complete = false,
                semanticAcceptance = false,
                role = string.Empty,
                testMethod = string.Empty,
                variant = string.Empty,
                startedAtUtc = string.Empty,
                finishedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                failureCode = knownCase ? failureCode : "UnexpectedCaseId",
                failureMessage = knownCase ? failureMessage :
                    "Attempt directory uses a case ID outside the fixed eleven-case contract.",
                fileCount = 0,
                descriptorPublished = false
            };
        }

        [Serializable]
        private sealed class V6CaptureSourceHash
        {
            public string path;
            public long bytes;
            public string sha256;
            public string error;
        }

        [Serializable]
        private sealed class V6CaptureFileRecord
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class V6CaptureValidationObservation
        {
            public string method;
            public bool executed;
            public string result;
            public bool accepted;
            public string[] errors;
            public string errorType;
            public string errorMessage;
        }

        [Serializable]
        private sealed class V6CaptureDecodeObservation
        {
            public string method;
            public bool executed;
            public string result;
            public string status;
            public bool valid;
            public string errorType;
            public string errorMessage;
        }

        [Serializable]
        private sealed class V6CaptureInspectorObservation
        {
            public string method;
            public bool executed;
            public string result;
            public string status;
            public string encodeChecksum;
            public string storedChecksum;
            public string computedChecksum;
            public bool storedChecksumMatchesEncode;
            public bool computedChecksumMatchesEncode;
            public bool preimageAvailable;
            public bool preimageMatchesLiveProjection;
            public string errorType;
            public string errorMessage;
            [NonSerialized] public byte[] preimageUtf8;
        }

        [Serializable]
        private sealed class V6CaptureCheckpointComparison
        {
            public string role;
            public bool liveLegacyBytesEqual;
            public long liveByteCount;
            public long legacyByteCount;
            public string claim;
        }

        private sealed class V6CaptureRuntimeIdentity
        {
            public string assembly;
            public string module;
            public string mvid;
        }

        private sealed class V6CaptureSessionState
        {
            public readonly object gate = new object();
            public readonly List<V6CaptureAttemptRecord> attempts = new List<V6CaptureAttemptRecord>();
            public readonly Dictionary<string, int> attemptCounters = new Dictionary<string, int>(StringComparer.Ordinal);
            public string projectRoot;
            public string captureRoot;
            public string sessionId;
            public string commit;
            public string unityVersion;
            public string projectVersionDeclared;
            public string startedAtUtc;
            public string activationArgument;
            public string activationValue;
            public V6CaptureSourceHash[] sourceHashesBefore;
            public V6CaptureRuntimeIdentity runtimeIdentity;
            public string cultureName;
            public string cultureNumberDecimalSeparator;
            public long sessionFileBytes;
            public string sessionFileSha256;
        }

        [Serializable]
        private sealed class V6CaptureSessionDocument
        {
            public int schemaVersion;
            public string sessionId;
            public string commit;
            public string unityVersion;
            public string projectVersionDeclared;
            public string startedAtUtc;
            public bool activationArgumentObserved;
            public string activationArgument;
            public string activationValue;
            public string[] expectedCaseIds;
            public V6CaptureSourceHash[] sourceHashesBefore;
            public string jsonUtilityAssembly;
            public string jsonUtilityModule;
            public string jsonUtilityMvid;
            public string cultureName;
            public string cultureNumberDecimalSeparator;
        }

        [Serializable]
        private sealed class V6CaptureManifestDocument
        {
            public int schemaVersion;
            public string sessionId;
            public string commit;
            public string unityVersion;
            public string projectVersionDeclared;
            public string startedAtUtc;
            public string finalizedAtUtc;
            public bool activationArgumentObserved;
            public string activationArgument;
            public string activationValue;
            public string[] expectedCaseIds;
            public V6CaptureSourceHash[] sourceHashesBefore;
            public V6CaptureSourceHash[] sourceHashesAfter;
            public bool sourcesStable;
            public bool sessionDocumentStable;
            public bool dataComplete;
            public bool roleRequirementsPassed;
            public V6CaptureAttemptRecord[] attempts;
            public long sessionFileBytes;
            public string sessionFileSha256;
            public string jsonUtilityAssembly;
            public string jsonUtilityModule;
            public string jsonUtilityMvid;
            public string cultureName;
            public string cultureNumberDecimalSeparator;
        }

        [Serializable]
        private sealed class V6CaptureAttemptRecord
        {
            public string caseId;
            public string attemptDirectory;
            public string descriptorPath;
            public bool complete;
            public bool semanticAcceptance;
            public string role;
            public string testMethod;
            public string variant;
            public string startedAtUtc;
            public string finishedAtUtc;
            public string failureCode;
            public string failureMessage;
            public int fileCount;
            public long descriptorBytes;
            public string descriptorSha256;
            [NonSerialized] public bool descriptorPublished;
        }

        [Serializable]
        private sealed class V6CaptureAttemptDescriptor
        {
            public int schemaVersion;
            public string sessionId;
            public string commit;
            public string unityVersion;
            public string projectVersionDeclared;
            public string caseId;
            public string attemptDirectory;
            public string descriptorPath;
            public string testMethod;
            public string variant;
            public string role;
            public string roleExpectation;
            public string startedAtUtc;
            public string observedAtUtc;
            public bool activationArgumentObserved;
            public string activationArgument;
            public string activationValue;
            public V6CaptureSourceHash[] sourceHashesBefore;
            public string jsonUtilityAssembly;
            public string jsonUtilityModule;
            public string jsonUtilityMvid;
            public string cultureName;
            public string cultureNumberDecimalSeparator;
            public V6CaptureFileRecord[] files;
            public V6CaptureValidationObservation semanticValidation;
            public V6CaptureDecodeObservation decode;
            public V6CaptureInspectorObservation inspector;
            public bool liveProjectionMatchesTestAssertions;
            public bool liveProjectionMatchesLegacyProjection;
            public long liveProjectionByteCount;
            public long legacyProjectionByteCount;
            public V6CaptureCheckpointComparison checkpointComparison;
            public bool complete;
            public bool semanticAcceptance;
            public string semanticAcceptanceMeaning;
            public string failureCode;
            public string failureMessage;
        }
    }
}
