using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.V5SourceCapture
{
    public sealed class V5SourceCaptureTests
    {
        private const string CaptureArgument = "-luckboundV5CaptureDir";
        private const string CaptureKind = "reconstructed-synthetic-from-versioned-v5-source";
        private const string ExpectedUnityVersion = "6000.6.6f1";
        private const string ExpectedUnityRevision = "d20c9ac777da";
        private const string HistoricalCommit = "a2590e141c46729e6efa7d01ea33647768b58711";
        private const string HistoricalTree = "658f45412efb0b6f554d6c458400116838369a97";
        private const string HistoricalProofPath = ".github/v5-source-capture/ORIGINAL-TREE-PROOF.json";
        private const string HistoricalProofSha256 = "394cae064afb63346056b7f6ffa02f2a9143c1bee6ad2a3f91dc713c80a0cc1e";

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, InvocationRecord> Invocations = new Dictionary<string, InvocationRecord>(StringComparer.Ordinal);
        private static string _projectRoot;
        private static string _captureRoot;
        private static string _executionCommit;
        private static int _configured;

        private static readonly string[] CaseOrder =
        {
            "P0-no-active", "P1-known-zero", "P1-known-nontrivial", "P2-unknown-null", "P2-unknown-inline",
            "P3-receipts-completed", "B0-arrays-null", "B1-arrays-empty", "M0-false-marker-payload",
            "M1-true-marker-null", "S0-settings", "N0-tampered"
        };

        private static readonly string[] CultureOrder = { "en-US", "es-ES" };

        [OneTimeSetUp]
        public void ConfigureCapture()
        {
            lock (Sync)
            {
                if (_configured != 0) return;
                _configured = 1;
                _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string captureArgumentValue = ReadCaptureArgument(Environment.GetCommandLineArgs());
                if (string.IsNullOrWhiteSpace(captureArgumentValue)) return;

                _captureRoot = ResolveNewCaptureDirectory(_projectRoot, captureArgumentValue);
                _executionCommit = ReadExecutionCommit();
                RequireNoReparseAncestors(_captureRoot);
                Directory.CreateDirectory(_captureRoot);
            }
        }

        [OneTimeTearDown]
        public void FinalizeCapture()
        {
            lock (Sync)
            {
                Assert.That(Invocations.Count, Is.EqualTo(24), "All twelve case IDs must run with both cultures.");
                AssertP2AndN0Relations();
                if (string.IsNullOrWhiteSpace(_captureRoot)) return;
                WriteFixtureIndex();
            }
        }

        [TestCase("en-US", TestName = "P0-no-active-en-US")]
        [TestCase("es-ES", TestName = "P0-no-active-es-ES")]
        public void P0NoActive(string cultureName) => RunCase("P0-no-active", cultureName);

        [TestCase("en-US", TestName = "P1-known-zero-en-US")]
        [TestCase("es-ES", TestName = "P1-known-zero-es-ES")]
        public void P1KnownZero(string cultureName) => RunCase("P1-known-zero", cultureName);

        [TestCase("en-US", TestName = "P1-known-nontrivial-en-US")]
        [TestCase("es-ES", TestName = "P1-known-nontrivial-es-ES")]
        public void P1KnownNontrivial(string cultureName) => RunCase("P1-known-nontrivial", cultureName);

        [TestCase("en-US", TestName = "P2-unknown-null-en-US")]
        [TestCase("es-ES", TestName = "P2-unknown-null-es-ES")]
        public void P2UnknownNull(string cultureName) => RunCase("P2-unknown-null", cultureName);

        [TestCase("en-US", TestName = "P2-unknown-inline-en-US")]
        [TestCase("es-ES", TestName = "P2-unknown-inline-es-ES")]
        public void P2UnknownInline(string cultureName) => RunCase("P2-unknown-inline", cultureName);

        [TestCase("en-US", TestName = "P3-receipts-completed-en-US")]
        [TestCase("es-ES", TestName = "P3-receipts-completed-es-ES")]
        public void P3ReceiptsCompleted(string cultureName) => RunCase("P3-receipts-completed", cultureName);

        [TestCase("en-US", TestName = "B0-arrays-null-en-US")]
        [TestCase("es-ES", TestName = "B0-arrays-null-es-ES")]
        public void B0ArraysNull(string cultureName) => RunCase("B0-arrays-null", cultureName);

        [TestCase("en-US", TestName = "B1-arrays-empty-en-US")]
        [TestCase("es-ES", TestName = "B1-arrays-empty-es-ES")]
        public void B1ArraysEmpty(string cultureName) => RunCase("B1-arrays-empty", cultureName);

        [TestCase("en-US", TestName = "M0-false-marker-payload-en-US")]
        [TestCase("es-ES", TestName = "M0-false-marker-payload-es-ES")]
        public void M0FalseMarkerPayload(string cultureName) => RunCase("M0-false-marker-payload", cultureName);

        [TestCase("en-US", TestName = "M1-true-marker-null-en-US")]
        [TestCase("es-ES", TestName = "M1-true-marker-null-es-ES")]
        public void M1TrueMarkerNull(string cultureName) => RunCase("M1-true-marker-null", cultureName);

        [TestCase("en-US", TestName = "S0-settings-en-US")]
        [TestCase("es-ES", TestName = "S0-settings-es-ES")]
        public void S0Settings(string cultureName) => RunCase("S0-settings", cultureName);

        [TestCase("en-US", TestName = "N0-tampered-en-US")]
        [TestCase("es-ES", TestName = "N0-tampered-es-ES")]
        public void N0Tampered(string cultureName) => RunCase("N0-tampered", cultureName);

        private static void RunCase(string caseId, string cultureName)
        {
            lock (Sync)
            {
                CultureInfo previousCulture = CultureInfo.CurrentCulture;
                CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
                try
                {
                    CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                    AssertHistoricalEditorIdentity();

                    InvocationRecord record;
                    if (caseId == "S0-settings") record = CaptureSettingsCase(caseId, cultureName);
                    else if (IsApiProfileCase(caseId)) record = CaptureApiProfileCase(caseId, cultureName);
                    else if (caseId == "N0-tampered") record = CaptureTamperedCase(caseId, cultureName);
                    else record = CaptureSyntheticProfileCase(caseId, cultureName);

                    string key = InvocationKey(caseId, cultureName);
                    Invocations[key] = record;
                    if (_captureRoot != null) WriteInvocation(record);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                    CultureInfo.CurrentUICulture = previousUiCulture;
                }
            }
        }

        private static bool IsApiProfileCase(string caseId)
        {
            return caseId == "P0-no-active" || caseId == "P1-known-zero" || caseId == "P1-known-nontrivial" ||
                caseId == "P2-unknown-null" || caseId == "P2-unknown-inline" || caseId == "P3-receipts-completed";
        }

        private static InvocationRecord CaptureApiProfileCase(string caseId, string cultureName)
        {
            ProfileCandidate candidate = CreateApiProfileCandidate(caseId);
            ProfileSaveEnvelope apiParsed = JsonUtility.FromJson<ProfileSaveEnvelope>(candidate.ApiJson);
            Assert.That(apiParsed, Is.Not.Null);
            byte[] apiPreimage = V5SourceCaptureInputs.ProjectProfilePreimage(apiParsed);
            Assert.That(apiParsed.checksum, Is.EqualTo(HashBytes(apiPreimage)), "Raw API checksum must match its historical write projection.");
            Assert.That(SaveJsonCodec.Decode(candidate.ApiJson, out ProfileSaveEnvelope apiDecoded), Is.EqualTo(DecodeStatus.Valid),
                "The raw historical API output must decode before it is converted to the fixed header.");
            Assert.That(apiDecoded.profile.availableRenown, Is.EqualTo(apiParsed.profile.availableRenown));

            ProfileSaveEnvelope fixedHeader = CloneProfileEnvelope(candidate.Envelope);
            SetFixedHeader(fixedHeader);
            fixedHeader.checksum = string.Empty;
            string fixedJson = SaveJsonCodec.Encode(fixedHeader);
            Assert.That(apiParsed.schemaVersion, Is.EqualTo(fixedHeader.schemaVersion));
            Assert.That(apiParsed.contentVersion, Is.EqualTo(fixedHeader.contentVersion));
            Assert.That(apiParsed.generation, Is.EqualTo(fixedHeader.generation));
            byte[] writePreimage = V5SourceCaptureInputs.ProjectProfilePreimage(fixedHeader);
            Assert.That(fixedHeader.checksum, Is.EqualTo(HashBytes(writePreimage)));

            ProfileSaveEnvelope parsedBeforeDecode = JsonUtility.FromJson<ProfileSaveEnvelope>(fixedJson);
            byte[] readPreimage = V5SourceCaptureInputs.ProjectProfilePreimage(parsedBeforeDecode);
            Assert.That(readPreimage, Is.EqualTo(writePreimage), "Read preimage is projected before Decode or normalization.");
            DecodeStatus status = SaveJsonCodec.Decode(fixedJson, out ProfileSaveEnvelope decoded);
            Assert.That(status, Is.EqualTo(DecodeStatus.Valid));
            Assert.That(decoded, Is.Not.Null);

            if (caseId == "P2-unknown-inline")
            {
                Assert.That(candidate.InputActive.frozenPermanentUpgrades, Is.Not.Null,
                    "The fixed source recipe must begin with the inline zero unknown-upgrade object.");
                Assert.That(candidate.Envelope.activeExpedition.frozenPermanentUpgrades, Is.Null,
                    "The historical Clone used by SaveProfileAsync must reduce the unknown zero object to null.");
            }
            else if (caseId == "P2-unknown-null")
            {
                Assert.That(candidate.InputActive.frozenPermanentUpgrades, Is.Null);
                Assert.That(candidate.Envelope.activeExpedition.frozenPermanentUpgrades, Is.Null);
            }
            if (caseId == "P3-receipts-completed") AssertP3Contents(candidate.Envelope);
            if (caseId == "P1-known-nontrivial") AssertNontrivialActiveContents(candidate.Envelope);

            ApiObservation apiObservation = CreateApiObservation(caseId, cultureName, "profile", apiParsed.savedAtUtc,
                apiParsed.generation, apiParsed.sequence, apiParsed.checksum,
                fixedHeader.savedAtUtc, fixedHeader.generation, fixedHeader.sequence,
                PayloadsEqual(apiParsed.profile, parsedBeforeDecode.profile),
                apiParsed.activeExpedition == null ? null : JsonUtility.ToJson(apiParsed.activeExpedition, false),
                parsedBeforeDecode.activeExpedition == null ? null : JsonUtility.ToJson(parsedBeforeDecode.activeExpedition, false),
                GetWireFrozenUpgradeRepresentation(candidate.ApiJson), GetWireFrozenUpgradeRepresentation(fixedJson));
            string activeStringsJson = SerializeActiveStrings(caseId, parsedBeforeDecode, fixedJson);
            string recipeJson = SerializeRecipe(caseId, candidate.Envelope.hasActiveExpedition,
                candidate.Envelope.activeExpedition != null, expectedDecode: "valid", derivedFrom: string.Empty,
                tamperField: string.Empty, tamperFrom: 0, tamperTo: 0,
                inputFrozenUpgradeRepresentation: caseId == "P2-unknown-null" ? "null" :
                    caseId == "P2-unknown-inline" ? "zero-inline" : "not-applicable",
                writerCloneMemorySnapshot: caseId == "P2-unknown-null" || caseId == "P2-unknown-inline"
                    ? "null" : "not-applicable",
                fixedEnvelopeWireFrozenUpgradeRepresentation: caseId == "P2-unknown-null" || caseId == "P2-unknown-inline"
                    ? GetWireFrozenUpgradeRepresentation(fixedJson) : "not-applicable");
            string decodeJson = SerializeDecodeResult(caseId, cultureName, status, decoded != null,
                message: string.Empty);
            AssertCultureInvariantRecord(caseId, cultureName, recipeJson, fixedJson, writePreimage, readPreimage,
                fixedHeader.checksum, activeStringsJson);

            return new InvocationRecord(caseId, cultureName, RoleForCase(caseId), candidate.Envelope.hasActiveExpedition,
                candidate.Envelope.activeExpedition != null, status, decodeJson, recipeJson, fixedJson, writePreimage,
                readPreimage, fixedHeader.checksum, candidate.ApiJson, SerializeApiObservation(apiObservation),
                activeStringsJson, apiObservation);
        }

        private static InvocationRecord CaptureSettingsCase(string caseId, string cultureName)
        {
            var store = new ImmediateMemoryStore();
            var repository = new SaveRepository(store);
            SaveWriteResult result = repository.SaveSettingsAsync(V5SourceCaptureInputs.BuildSettingsInput())
                .GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(store.PrimaryJson, Is.Not.Null.And.Not.Empty);

            string apiJson = store.PrimaryJson;
            SettingsSaveEnvelope apiParsed = JsonUtility.FromJson<SettingsSaveEnvelope>(apiJson);
            Assert.That(apiParsed, Is.Not.Null);
            byte[] apiPreimage = V5SourceCaptureInputs.ProjectSettingsPreimage(apiParsed);
            Assert.That(apiParsed.checksum, Is.EqualTo(HashBytes(apiPreimage)));
            Assert.That(SaveJsonCodec.Decode(apiJson, out SettingsSaveEnvelope apiDecoded), Is.EqualTo(DecodeStatus.Valid));
            Assert.That(apiDecoded.settings.languageId, Is.EqualTo("es"));
            Assert.That(apiDecoded.settings.masterVolume, Is.EqualTo(0.83f));
            Assert.That(apiDecoded.settings.musicVolume, Is.EqualTo(0.57f));
            Assert.That(apiDecoded.settings.effectsVolume, Is.EqualTo(0.31f));
            Assert.That(apiDecoded.settings.ambienceVolume, Is.EqualTo(0.19f));

            SettingsSaveEnvelope fixedHeader = CloneSettingsEnvelope(repository.CurrentSettings);
            SetFixedHeader(fixedHeader);
            fixedHeader.checksum = string.Empty;
            string fixedJson = SaveJsonCodec.Encode(fixedHeader);
            Assert.That(apiParsed.schemaVersion, Is.EqualTo(fixedHeader.schemaVersion));
            Assert.That(apiParsed.contentVersion, Is.EqualTo(fixedHeader.contentVersion));
            Assert.That(apiParsed.generation, Is.EqualTo(fixedHeader.generation));
            byte[] writePreimage = V5SourceCaptureInputs.ProjectSettingsPreimage(fixedHeader);
            Assert.That(fixedHeader.checksum, Is.EqualTo(HashBytes(writePreimage)));
            SettingsSaveEnvelope parsedBeforeDecode = JsonUtility.FromJson<SettingsSaveEnvelope>(fixedJson);
            byte[] readPreimage = V5SourceCaptureInputs.ProjectSettingsPreimage(parsedBeforeDecode);
            Assert.That(readPreimage, Is.EqualTo(writePreimage));
            DecodeStatus status = SaveJsonCodec.Decode(fixedJson, out SettingsSaveEnvelope decoded);
            Assert.That(status, Is.EqualTo(DecodeStatus.Valid));
            Assert.That(decoded.settings.ambienceVolume, Is.EqualTo(0.19f));

            ApiObservation apiObservation = CreateApiObservation(caseId, cultureName, "settings", apiParsed.savedAtUtc,
                apiParsed.generation, apiParsed.sequence, apiParsed.checksum,
                fixedHeader.savedAtUtc, fixedHeader.generation, fixedHeader.sequence,
                PayloadsEqual(apiParsed.settings, fixedHeader.settings), null, null,
                "not-applicable", "not-applicable");
            string recipeJson = SerializeRecipe(caseId, hasActiveExpedition: false,
                activePayloadPresent: false, expectedDecode: "valid", derivedFrom: string.Empty,
                tamperField: string.Empty, tamperFrom: 0, tamperTo: 0);
            string decodeJson = SerializeDecodeResult(caseId, cultureName, status, decoded != null, string.Empty);
            AssertCultureInvariantRecord(caseId, cultureName, recipeJson, fixedJson, writePreimage, readPreimage,
                fixedHeader.checksum, activeStringsJson: string.Empty);

            return new InvocationRecord(caseId, cultureName, RoleForCase(caseId), false, false, status,
                decodeJson, recipeJson, fixedJson, writePreimage, readPreimage, fixedHeader.checksum,
                apiJson, SerializeApiObservation(apiObservation), null, apiObservation);
        }

        private static InvocationRecord CaptureSyntheticProfileCase(string caseId, string cultureName)
        {
            ProfileSaveEnvelope envelope;
            if (caseId == "B0-arrays-null" || caseId == "B1-arrays-empty")
            {
                ProfileSaveData profile = V5SourceCaptureInputs.BuildProfileInput("P0-no-active");
                if (caseId == "B0-arrays-null")
                {
                    profile.contractCounters = null;
                    profile.progressionPurchaseTransactions = null;
                }
                else
                {
                    profile.contractCounters = Array.Empty<ContractCounterSaveData>();
                    profile.progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();
                }
                envelope = NewProfileEnvelope(profile, hasActiveExpedition: false, activeExpedition: null);
            }
            else if (caseId == "M0-false-marker-payload")
            {
                envelope = NewProfileEnvelope(V5SourceCaptureInputs.BuildProfileInput("P0-no-active"),
                    hasActiveExpedition: false,
                    activeExpedition: V5SourceCaptureInputs.BuildExpeditionInput("P1-known-zero"));
            }
            else if (caseId == "M1-true-marker-null")
            {
                envelope = NewProfileEnvelope(V5SourceCaptureInputs.BuildProfileInput("P0-no-active"),
                    hasActiveExpedition: true, activeExpedition: null);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Case does not use a synthetic profile envelope.");
            }

            string fixedJson = SaveJsonCodec.Encode(envelope);
            byte[] writePreimage = V5SourceCaptureInputs.ProjectProfilePreimage(envelope);
            Assert.That(envelope.checksum, Is.EqualTo(HashBytes(writePreimage)));
            ProfileSaveEnvelope parsedBeforeDecode = JsonUtility.FromJson<ProfileSaveEnvelope>(fixedJson);
            byte[] readPreimage = V5SourceCaptureInputs.ProjectProfilePreimage(parsedBeforeDecode);
            DecodeStatus status = SaveJsonCodec.Decode(fixedJson, out ProfileSaveEnvelope decoded);

            if (caseId == "M0-false-marker-payload" || caseId == "M1-true-marker-null")
                Assert.That(status, Is.EqualTo(DecodeStatus.Corrupted));
            else
                Assert.That(Enum.IsDefined(typeof(DecodeStatus), status), Is.True,
                    "Boundary cases record the source Decode observation without assuming null and empty are equivalent.");

            string activeStringsJson = SerializeActiveStrings(caseId, parsedBeforeDecode, fixedJson);
            string expectedStatus = caseId.StartsWith("M", StringComparison.Ordinal)
                ? "corrupted" : caseId.StartsWith("B", StringComparison.Ordinal) ? "record-observed-result" : DecodeStatusName(status);
            string recipeJson = SerializeRecipe(caseId, envelope.hasActiveExpedition,
                envelope.activeExpedition != null, expectedStatus, string.Empty, string.Empty, 0, 0,
                boundaryArraysInputRepresentation: caseId == "B0-arrays-null"
                    ? "contractCounters:null;progressionPurchaseTransactions:null"
                    : caseId == "B1-arrays-empty"
                        ? "contractCounters:empty;progressionPurchaseTransactions:empty"
                        : "not-applicable");
            string decodeJson = SerializeDecodeResult(caseId, cultureName, status, decoded != null, string.Empty);
            AssertCultureInvariantRecord(caseId, cultureName, recipeJson, fixedJson, writePreimage, readPreimage,
                envelope.checksum, activeStringsJson);

            return new InvocationRecord(caseId, cultureName, RoleForCase(caseId), envelope.hasActiveExpedition,
                envelope.activeExpedition != null, status, decodeJson, recipeJson, fixedJson, writePreimage,
                readPreimage, envelope.checksum, null, null, activeStringsJson, null);
        }

        private static InvocationRecord CaptureTamperedCase(string caseId, string cultureName)
        {
            ProfileCandidate p3 = CreateApiProfileCandidate("P3-receipts-completed");
            ProfileSaveEnvelope tampered = CloneProfileEnvelope(p3.FixedCandidate);
            string originalChecksum = p3.FixedCandidate.checksum;
            Assert.That(tampered.hasActiveExpedition, Is.True);
            Assert.That(tampered.activeExpedition, Is.Not.Null);
            Assert.That(tampered.profile.availableRenown, Is.EqualTo(33));
            tampered.profile.availableRenown = 34;
            tampered.checksum = originalChecksum;
            ProfileSaveData revertedProfile = tampered.profile.Clone();
            revertedProfile.availableRenown = 33;
            Assert.That(JsonUtility.ToJson(revertedProfile, false),
                Is.EqualTo(JsonUtility.ToJson(p3.FixedCandidate.profile, false)),
                "Reverting the one declared field must restore the entire P3 profile payload.");
            Assert.That(JsonUtility.ToJson(tampered.activeExpedition, false),
                Is.EqualTo(JsonUtility.ToJson(p3.FixedCandidate.activeExpedition, false)),
                "N0 preserves the complete P3 active expedition payload.");

            string fixedJson = JsonUtility.ToJson(tampered, true);
            byte[] writePreimage = V5SourceCaptureInputs.ProjectProfilePreimage(tampered);
            Assert.That(tampered.checksum, Is.EqualTo(originalChecksum), "Tampering preserves the P3 checksum.");
            Assert.That(HashBytes(writePreimage), Is.Not.EqualTo(originalChecksum));
            Assert.That(tampered.profile.availableRenown, Is.EqualTo(34));

            ProfileSaveEnvelope parsedBeforeDecode = JsonUtility.FromJson<ProfileSaveEnvelope>(fixedJson);
            byte[] readPreimage = V5SourceCaptureInputs.ProjectProfilePreimage(parsedBeforeDecode);
            Assert.That(readPreimage, Is.EqualTo(writePreimage));
            DecodeStatus status = SaveJsonCodec.Decode(fixedJson, out ProfileSaveEnvelope decoded);
            Assert.That(status, Is.EqualTo(DecodeStatus.Corrupted));

            string activeStringsJson = SerializeActiveStrings(caseId, parsedBeforeDecode, fixedJson);
            string recipeJson = SerializeRecipe(caseId, true, true, "corrupted",
                "P3-receipts-completed", "availableRenown", 33, 34);
            string decodeJson = SerializeDecodeResult(caseId, cultureName, status, decoded != null, string.Empty);
            AssertCultureInvariantRecord(caseId, cultureName, recipeJson, fixedJson, writePreimage, readPreimage,
                originalChecksum, activeStringsJson);

            return new InvocationRecord(caseId, cultureName, RoleForCase(caseId), true, true, status,
                decodeJson, recipeJson, fixedJson, writePreimage, readPreimage, originalChecksum,
                null, null, activeStringsJson, null);
        }

        private static ProfileCandidate CreateApiProfileCandidate(string caseId)
        {
            ProfileSaveData profile = V5SourceCaptureInputs.BuildProfileInput(caseId);
            ExpeditionSaveData active = caseId == "P0-no-active" ? null : V5SourceCaptureInputs.BuildExpeditionInput(caseId);
            var store = new ImmediateMemoryStore();
            var repository = new SaveRepository(store);
            SaveWriteResult result = repository.SaveProfileAsync(profile, active).GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(store.LastDocument, Is.EqualTo(SaveDocument.Profile));
            Assert.That(store.PrimaryJson, Is.Not.Null.And.Not.Empty,
                "The spy store must preserve SaveRepository's raw JSON without rewriting it.");
            Assert.That(repository.CurrentProfile, Is.Not.Null);

            if (caseId == "P2-unknown-inline")
            {
                Assert.That(active.frozenPermanentUpgrades, Is.Not.Null,
                    "The input object remains inline; SaveProfileAsync must clone it into the stored candidate.");
                Assert.That(repository.CurrentProfile.activeExpedition.frozenPermanentUpgrades, Is.Null);
            }

            ProfileSaveEnvelope apiEnvelope = repository.CurrentProfile;
            Assert.That(apiEnvelope.savedAtUtc, Is.Not.EqualTo(V5SourceCaptureInputs.SavedAtUtc),
                "API envelope keeps the historical real clock value; the fixture candidate is made separately.");
            ProfileSaveEnvelope fixedCandidate = CloneProfileEnvelope(apiEnvelope);
            SetFixedHeader(fixedCandidate);
            fixedCandidate.checksum = string.Empty;
            _ = SaveJsonCodec.Encode(fixedCandidate);
            return new ProfileCandidate(store.PrimaryJson, apiEnvelope, fixedCandidate, active);
        }

        private static ProfileSaveEnvelope NewProfileEnvelope(ProfileSaveData profile, bool hasActiveExpedition,
            ExpeditionSaveData activeExpedition)
        {
            return new ProfileSaveEnvelope
            {
                schemaVersion = V5SourceCaptureInputs.SchemaVersion,
                contentVersion = V5SourceCaptureInputs.ContentVersion,
                generation = V5SourceCaptureInputs.Generation,
                sequence = V5SourceCaptureInputs.Sequence,
                savedAtUtc = V5SourceCaptureInputs.SavedAtUtc,
                checksum = string.Empty,
                profile = profile,
                hasActiveExpedition = hasActiveExpedition,
                activeExpedition = activeExpedition
            };
        }

        private static ProfileSaveEnvelope CloneProfileEnvelope(ProfileSaveEnvelope source)
        {
            return new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                profile = source.profile == null ? null : source.profile.Clone(),
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.activeExpedition == null ? null : source.activeExpedition.Clone()
            };
        }

        private static SettingsSaveEnvelope CloneSettingsEnvelope(SettingsSaveEnvelope source)
        {
            return new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                settings = source.settings == null ? null : source.settings.Clone()
            };
        }

        private static void SetFixedHeader(ProfileSaveEnvelope envelope)
        {
            envelope.schemaVersion = V5SourceCaptureInputs.SchemaVersion;
            envelope.contentVersion = V5SourceCaptureInputs.ContentVersion;
            envelope.generation = V5SourceCaptureInputs.Generation;
            envelope.sequence = V5SourceCaptureInputs.Sequence;
            envelope.savedAtUtc = V5SourceCaptureInputs.SavedAtUtc;
        }

        private static void SetFixedHeader(SettingsSaveEnvelope envelope)
        {
            envelope.schemaVersion = V5SourceCaptureInputs.SchemaVersion;
            envelope.contentVersion = V5SourceCaptureInputs.ContentVersion;
            envelope.generation = V5SourceCaptureInputs.Generation;
            envelope.sequence = V5SourceCaptureInputs.Sequence;
            envelope.savedAtUtc = V5SourceCaptureInputs.SavedAtUtc;
        }

        private static void AssertP3Contents(ProfileSaveEnvelope envelope)
        {
            Assert.That(envelope.profile.availableRenown, Is.EqualTo(33));
            Assert.That(envelope.profile.healthUpgradeLevel, Is.EqualTo(2));
            Assert.That(envelope.profile.energyUpgradeLevel, Is.EqualTo(2));
            Assert.That(envelope.profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
            Assert.That(envelope.profile.progressionPurchaseTransactions, Has.Length.EqualTo(5));
            Assert.That(envelope.profile.contractCounters, Has.Length.EqualTo(1));
            Assert.That(envelope.profile.contractCounters[0].contractId, Is.EqualTo(V5SourceCaptureInputs.ContractId));
            Assert.That(envelope.profile.processedSettlementRunIds, Has.Length.EqualTo(2));
            Assert.That(envelope.activeExpedition, Is.Not.Null);
            Assert.That(envelope.activeExpedition.roomEntryCheckpointJson, Is.Empty,
                "Completed P3 retains an empty checkpoint string.");
            Assert.That(envelope.activeExpedition.completedRoomResults, Has.Length.EqualTo(1));
            Assert.That(envelope.activeExpedition.completedRoomResults[0].resultJson, Is.EqualTo(V5SourceCaptureInputs.ResultJson));
            Assert.That(envelope.activeExpedition.fortuneOfferBookSnapshot.offers, Is.Not.Empty);
            Assert.That(envelope.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts, Is.Not.Empty);
            Assert.That(envelope.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts, Is.Not.Empty);

            ExpeditionInventorySnapshot inventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                envelope.activeExpedition.inventorySnapshotJson);
            Assert.That(inventory.transactions, Is.Not.Empty, "P3 inventory receipt comes from a historical Core operation.");
            Assert.That(inventory.backpackSlots.Any(stack => stack != null && stack.quantity > 1), Is.True,
                "P3 includes a real item stack.");
            Assert.That(inventory.materials.Any(material => material != null && material.count > 0), Is.True,
                "P3 includes a real material count.");
        }

        private static void AssertNontrivialActiveContents(ProfileSaveEnvelope envelope)
        {
            Assert.That(envelope.hasActiveExpedition, Is.True);
            Assert.That(envelope.activeExpedition, Is.Not.Null);
            Assert.That(envelope.activeExpedition.frozenPermanentUpgrades.healthUpgradeLevel, Is.EqualTo(2));
            Assert.That(envelope.activeExpedition.frozenPermanentUpgrades.energyUpgradeLevel, Is.EqualTo(2));
            Assert.That(envelope.activeExpedition.frozenPermanentUpgrades.additionalPotionUpgradeLevel, Is.EqualTo(1));
            Assert.That(envelope.activeExpedition.currentHealth, Is.Not.EqualTo(envelope.activeExpedition.maximumHealth));
            Assert.That(envelope.activeExpedition.currentEnergy, Is.Not.EqualTo(envelope.activeExpedition.maximumEnergy));
            Assert.That(envelope.activeExpedition.routeSnapshotJson, Is.Not.Empty);
            Assert.That(envelope.activeExpedition.inventorySnapshotJson, Is.Not.Empty);
            Assert.That(envelope.activeExpedition.roomEntryCheckpointJson, Is.Not.Empty);
            ExpeditionRoomCheckpointSaveData checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(
                envelope.activeExpedition.roomEntryCheckpointJson);
            Assert.That(checkpoint.fortuneOfferBookSnapshot, Is.Not.Null);
        }

        private static ApiObservation CreateApiObservation(string caseId, string cultureName, string kind,
            string apiSavedAtUtc, int apiGeneration, long apiSequence, string apiChecksum,
            string fixedSavedAtUtc, int fixedGeneration, long fixedSequence, bool payloadsEqual,
            string apiActiveJson, string fixedActiveJson,
            string apiFrozenUpgradeWireRepresentation, string fixedFrozenUpgradeWireRepresentation)
        {
            string[] differingHeaders = new[] { "savedAtUtc", "sequence" };
            bool activeEqual = string.Equals(apiActiveJson, fixedActiveJson, StringComparison.Ordinal);
            bool onlyDeclaredHeaderFieldsDiffer = apiSavedAtUtc != fixedSavedAtUtc && apiSequence != fixedSequence &&
                apiGeneration == fixedGeneration && payloadsEqual && activeEqual;
            Assert.That(onlyDeclaredHeaderFieldsDiffer, Is.True,
                "Only the declared savedAtUtc and sequence header values distinguish API and fixed-header candidates; checksum is derived.");
            Assert.That(apiSequence, Is.EqualTo(1L));
            Assert.That(fixedSequence, Is.EqualTo(V5SourceCaptureInputs.Sequence));
            Assert.That(fixedSavedAtUtc, Is.EqualTo(V5SourceCaptureInputs.SavedAtUtc));
            return new ApiObservation
            {
                caseId = caseId,
                culture = cultureName,
                apiKind = kind,
                apiSavedAtUtc = apiSavedAtUtc,
                apiGeneration = apiGeneration,
                apiSequence = apiSequence,
                apiChecksum = apiChecksum,
                fixedSavedAtUtc = fixedSavedAtUtc,
                fixedGeneration = fixedGeneration,
                fixedSequence = fixedSequence,
                onlyHeaderFieldsDiffer = onlyDeclaredHeaderFieldsDiffer,
                differingHeaderFields = differingHeaders,
                payloadsEqual = payloadsEqual && activeEqual,
                apiFrozenUpgradeWireRepresentation = apiFrozenUpgradeWireRepresentation,
                fixedFrozenUpgradeWireRepresentation = fixedFrozenUpgradeWireRepresentation
            };
        }

        private static string GetWireFrozenUpgradeRepresentation(string json)
        {
            if (string.IsNullOrEmpty(json)) return "not-applicable";
            Match match = Regex.Match(json,
                "(?<!\\\\)\\\"frozenPermanentUpgrades\\\"\\s*:\\s*(null|\\{[^{}]*\\})",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value.Trim() : "omitted";
        }

        private static bool WireActivePayloadIsObject(string json)
        {
            MatchCollection members = Regex.Matches(json, @"(?<!\\)""activeExpedition""\s*:\s*",
                RegexOptions.CultureInvariant);
            if (members.Count != 1)
                throw new InvalidOperationException("The emitted profile JSON must contain exactly one activeExpedition member.");
            int valueOffset = members[0].Index + members[0].Length;
            Match token = Regex.Match(json.Substring(valueOffset), @"\A(null(?=\s*[,}])|\{)",
                RegexOptions.CultureInvariant);
            if (!token.Success)
                throw new InvalidOperationException("The emitted activeExpedition member must be null or an object.");
            return token.Groups[1].Value == "{";
        }

        private static bool HasCandidateActivePayload(string caseId)
        {
            switch (caseId)
            {
                case "P0-no-active":
                case "B0-arrays-null":
                case "B1-arrays-empty":
                case "M1-true-marker-null":
                case "S0-settings":
                    return false;
                case "P1-known-zero":
                case "P1-known-nontrivial":
                case "P2-unknown-null":
                case "P2-unknown-inline":
                case "P3-receipts-completed":
                case "M0-false-marker-payload":
                case "N0-tampered":
                    return true;
                default: throw new ArgumentOutOfRangeException(nameof(caseId));
            }
        }

        private static bool PayloadsEqual<T>(T left, T right)
        {
            return string.Equals(JsonUtility.ToJson(left, false), JsonUtility.ToJson(right, false), StringComparison.Ordinal);
        }

        private static string SerializeApiObservation(ApiObservation observation) => JsonUtility.ToJson(observation, true);

        private static string SerializeActiveStrings(string caseId, ProfileSaveEnvelope envelope, string envelopeJson)
        {
            bool wirePayloadPresent = WireActivePayloadIsObject(envelopeJson);
            bool sourcePayload = HasCandidateActivePayload(caseId);
            ExpeditionSaveData active = wirePayloadPresent ? envelope.activeExpedition : null;
            if (wirePayloadPresent && active == null)
                throw new InvalidOperationException("An observed wire object did not deserialize as an active DTO.");
            var output = new ActiveStringsRecord
            {
                caseId = caseId,
                hasActiveExpedition = envelope.hasActiveExpedition,
                activePayloadPresent = wirePayloadPresent,
                routeSnapshotJson = active == null ? null : active.routeSnapshotJson,
                inventorySnapshotJson = active == null ? null : active.inventorySnapshotJson,
                roomEntryCheckpointJson = active == null ? null : active.roomEntryCheckpointJson,
                completedRoomResults = active == null || active.completedRoomResults == null
                    ? Array.Empty<CompletedResultText>()
                    : active.completedRoomResults.Select(result => new CompletedResultText
                    {
                        roomNodeId = result == null ? null : result.roomNodeId,
                        resultId = result == null ? null : result.resultId,
                        resultJson = result == null ? null : result.resultJson
                    }).ToArray(),
                provenance = new ActiveStringProvenance
                {
                    routeSnapshotJson = !sourcePayload ? "none; source candidate has no active expedition payload" :
                        "Luckbound.Core.Expeditions.ExpeditionRouteGenerator.Generate(seed=17320508, fixed V5SourceCaptureInputs fixture catalog).CanonicalForm",
                    inventorySnapshotJson = !sourcePayload ? "none; source candidate has no active expedition payload" :
                        "historical Core ExpeditionInventory.CreateSnapshot serialized with historical Unity JsonUtility after fixed TransactionId operations",
                    roomEntryCheckpointJson = !sourcePayload ? "none; source candidate has no active expedition payload" :
                        "historical App ExpeditionSaveMapper.CreateCheckpoint and SerializeCheckpoint; empty string retained for completed P3",
                    completedRoomResults = !sourcePayload ? "none; source candidate has no active expedition payload" :
                        "fixed synthetic completion result; resultJson is exported verbatim as an opaque V5 string"
                }
            };
            return JsonUtility.ToJson(output, true);
        }

        private static string SerializeRecipe(string caseId, bool hasActiveExpedition,
            bool activePayloadPresent, string expectedDecode, string derivedFrom, string tamperField,
            int tamperFrom, int tamperTo,
            string inputFrozenUpgradeRepresentation = "not-applicable",
            string writerCloneMemorySnapshot = "not-applicable",
            string fixedEnvelopeWireFrozenUpgradeRepresentation = "not-applicable",
            string boundaryArraysInputRepresentation = "not-applicable")
        {
            var recipe = new RecipeRecord
            {
                schemaVersion = 1,
                caseId = caseId,
                role = RoleForCase(caseId),
                seed = V5SourceCaptureInputs.Seed,
                fixedInputHeader = new FixedHeaderRecord
                {
                    schemaVersion = V5SourceCaptureInputs.SchemaVersion,
                    contentVersion = V5SourceCaptureInputs.ContentVersion,
                    generation = V5SourceCaptureInputs.Generation,
                    sequence = V5SourceCaptureInputs.Sequence,
                    savedAtUtc = V5SourceCaptureInputs.SavedAtUtc,
                    seed = V5SourceCaptureInputs.Seed
                },
                fixedIds = new FixedIdsRecord
                {
                    profileGuidN = V5SourceCaptureInputs.ProfileGuidN,
                    activeRunGuidN = V5SourceCaptureInputs.ActiveRunGuidN,
                    settlementGuidN = V5SourceCaptureInputs.SettlementGuidN,
                    progressionPurchaseGuidN = V5SourceCaptureInputs.ProgressionPurchaseGuidN,
                    inventoryTransactionGuidN = V5SourceCaptureInputs.InventoryTransactionGuidN,
                    fortuneOfferGuidN = V5SourceCaptureInputs.FortuneOfferGuidN,
                    fortuneHistoryGuidN = V5SourceCaptureInputs.FortuneHistoryGuidN,
                    fortuneRerollGuidN = V5SourceCaptureInputs.FortuneRerollGuidN,
                    fortuneBrewGuidN = V5SourceCaptureInputs.FortuneBrewGuidN
                },
                expected = new RecipeExpectation
                {
                    decode = expectedDecode,
                    hasActiveExpedition = hasActiveExpedition,
                    activePayloadPresent = activePayloadPresent,
                    derivedFrom = derivedFrom ?? string.Empty,
                    tamperField = tamperField ?? string.Empty,
                    tamperFrom = tamperFrom,
                    tamperTo = tamperTo,
                    inputFrozenUpgradeRepresentation = inputFrozenUpgradeRepresentation,
                    writerCloneMemorySnapshot = writerCloneMemorySnapshot,
                    fixedEnvelopeWireFrozenUpgradeRepresentation = fixedEnvelopeWireFrozenUpgradeRepresentation,
                    boundaryArraysInputRepresentation = boundaryArraysInputRepresentation
                }
            };
            return JsonUtility.ToJson(recipe, true);
        }

        private static string SerializeDecodeResult(string caseId, string cultureName, DecodeStatus status,
            bool decodedEnvelopePresent, string message)
        {
            var result = new DecodeResultRecord
            {
                caseId = caseId,
                culture = cultureName,
                status = DecodeStatusName(status),
                statusEnum = status.ToString(),
                statusNormalized = DecodeStatusName(status),
                accepted = status == DecodeStatus.Valid,
                decodedEnvelopePresent = decodedEnvelopePresent,
                message = message ?? string.Empty
            };
            return JsonUtility.ToJson(result, true);
        }

        private static string RoleForCase(string caseId)
        {
            switch (caseId)
            {
                case "P0-no-active":
                case "P1-known-zero":
                case "P1-known-nontrivial":
                case "P2-unknown-null":
                case "P2-unknown-inline":
                case "P3-receipts-completed":
                    return "positive-api-and-codec";
                case "B0-arrays-null":
                case "B1-arrays-empty":
                    return "boundary-observation";
                case "M0-false-marker-payload":
                case "M1-true-marker-null":
                    return "synthetic-marker-negative";
                case "S0-settings":
                    return "positive-settings-api-and-codec";
                case "N0-tampered":
                    return "checksum-negative";
                default:
                    throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown V5 capture case ID.");
            }
        }

        private static string DecodeStatusName(DecodeStatus status)
        {
            switch (status)
            {
                case DecodeStatus.Valid: return "valid";
                case DecodeStatus.Corrupted: return "corrupted";
                case DecodeStatus.Incompatible: return "incompatible";
                case DecodeStatus.Missing: return "missing";
                default: return "unknown";
            }
        }

        private static void AssertCultureInvariantRecord(string caseId, string cultureName, string recipeJson,
            string envelopeJson, byte[] writePreimage, byte[] readPreimage,
            string checksum, string activeStringsJson)
        {
            string key = InvocationKey(caseId, cultureName);
            var output = new CultureInvariantOutput(recipeJson, envelopeJson, writePreimage, readPreimage, checksum, activeStringsJson);
            CultureInvariantOutputs[key] = output;
            string otherCulture = cultureName == "en-US" ? "es-ES" : "en-US";
            string otherKey = InvocationKey(caseId, otherCulture);
            if (!CultureInvariantOutputs.TryGetValue(otherKey, out CultureInvariantOutput other)) return;

            Assert.That(recipeJson, Is.EqualTo(other.RecipeJson), "Recipe bytes differ by culture: " + caseId);
            Assert.That(envelopeJson, Is.EqualTo(other.EnvelopeJson), "Fixed-header envelope differs by culture: " + caseId);
            CollectionAssert.AreEqual(writePreimage, other.WritePreimage, "Full write preimage differs by culture: " + caseId);
            CollectionAssert.AreEqual(readPreimage, other.ReadPreimage, "Full read preimage differs by culture: " + caseId);
            Assert.That(checksum, Is.EqualTo(other.Checksum), "Stored checksum differs by culture: " + caseId);
            Assert.That(activeStringsJson, Is.EqualTo(other.ActiveStringsJson), "Opaque active string export differs by culture: " + caseId);
        }

        private static void AssertP2AndN0Relations()
        {
            foreach (string culture in CultureOrder)
            {
                string nullKey = InvocationKey("P2-unknown-null", culture);
                string inlineKey = InvocationKey("P2-unknown-inline", culture);
                Assert.That(CultureInvariantOutputs.ContainsKey(nullKey), Is.True);
                Assert.That(CultureInvariantOutputs.ContainsKey(inlineKey), Is.True);
                CultureInvariantOutput nullOutput = CultureInvariantOutputs[nullKey];
                CultureInvariantOutput inlineOutput = CultureInvariantOutputs[inlineKey];
                Assert.That(inlineOutput.EnvelopeJson, Is.EqualTo(nullOutput.EnvelopeJson),
                    "Historical Clone reduction must make P2 null and inline fixed-header envelopes identical.");
                CollectionAssert.AreEqual(inlineOutput.WritePreimage, nullOutput.WritePreimage,
                    "P2 null and inline write preimages must be compared in full.");
                CollectionAssert.AreEqual(inlineOutput.ReadPreimage, nullOutput.ReadPreimage,
                    "P2 null and inline read preimages must be compared in full.");
                Assert.That(inlineOutput.Checksum, Is.EqualTo(nullOutput.Checksum));

                InvocationRecord p3 = Invocations[InvocationKey("P3-receipts-completed", culture)];
                InvocationRecord n0 = Invocations[InvocationKey("N0-tampered", culture)];
                Assert.That(n0.HasActiveExpedition, Is.True);
                Assert.That(n0.ActivePayloadPresent, Is.True);
                Assert.That(n0.StoredChecksum, Is.EqualTo(p3.StoredChecksum),
                    "N0 retains the original P3 checksum.");
                CollectionAssert.AreNotEqual(n0.WritePreimage, p3.WritePreimage,
                    "The one-field N0 tamper must change the exact projected bytes.");
                AssertActiveStringPayloadsEqual(n0.ActiveStringsJson, p3.ActiveStringsJson);
            }
        }

        private static void AssertActiveStringPayloadsEqual(string n0Json, string p3Json)
        {
            // Compare all payload fields; caseId remains distinct metadata in the captured artifacts.
            ActiveStringsRecord n0 = JsonUtility.FromJson<ActiveStringsRecord>(n0Json);
            ActiveStringsRecord p3 = JsonUtility.FromJson<ActiveStringsRecord>(p3Json);
            Assert.That(n0, Is.Not.Null);
            Assert.That(p3, Is.Not.Null);
            Assert.That(n0.caseId, Is.EqualTo("N0-tampered"));
            Assert.That(p3.caseId, Is.EqualTo("P3-receipts-completed"));
            Assert.That(n0.hasActiveExpedition, Is.EqualTo(p3.hasActiveExpedition));
            Assert.That(n0.activePayloadPresent, Is.EqualTo(p3.activePayloadPresent));
            Assert.That(n0.routeSnapshotJson, Is.EqualTo(p3.routeSnapshotJson));
            Assert.That(n0.inventorySnapshotJson, Is.EqualTo(p3.inventorySnapshotJson));
            Assert.That(n0.roomEntryCheckpointJson, Is.EqualTo(p3.roomEntryCheckpointJson));
            Assert.That(n0.completedRoomResults, Is.Not.Null);
            Assert.That(p3.completedRoomResults, Is.Not.Null);
            Assert.That(n0.completedRoomResults.Length, Is.EqualTo(p3.completedRoomResults.Length));
            for (int index = 0; index < p3.completedRoomResults.Length; index++)
            {
                CompletedResultText left = n0.completedRoomResults[index];
                CompletedResultText right = p3.completedRoomResults[index];
                Assert.That(left, Is.Not.Null);
                Assert.That(right, Is.Not.Null);
                Assert.That(left.roomNodeId, Is.EqualTo(right.roomNodeId));
                Assert.That(left.resultId, Is.EqualTo(right.resultId));
                Assert.That(left.resultJson, Is.EqualTo(right.resultJson));
            }
            Assert.That(n0.provenance, Is.Not.Null);
            Assert.That(p3.provenance, Is.Not.Null);
            Assert.That(n0.provenance.routeSnapshotJson, Is.EqualTo(p3.provenance.routeSnapshotJson));
            Assert.That(n0.provenance.inventorySnapshotJson, Is.EqualTo(p3.provenance.inventorySnapshotJson));
            Assert.That(n0.provenance.roomEntryCheckpointJson, Is.EqualTo(p3.provenance.roomEntryCheckpointJson));
            Assert.That(n0.provenance.completedRoomResults, Is.EqualTo(p3.provenance.completedRoomResults));
        }

        private static readonly Dictionary<string, CultureInvariantOutput> CultureInvariantOutputs =
            new Dictionary<string, CultureInvariantOutput>(StringComparer.Ordinal);

        private static string InvocationKey(string caseId, string cultureName) => caseId + "|" + cultureName;

        private static void WriteInvocation(InvocationRecord invocation)
        {
            string directory = Path.Combine(_captureRoot, "cultures", invocation.Culture, invocation.CaseId);
            RequireNoReparseAncestors(directory);
            Directory.CreateDirectory(directory);
            invocation.Outputs = new List<OutputRecord>();

            AddOutput(invocation, directory, "recipe.json", Utf8NoBom.GetBytes(invocation.RecipeJson), "utf-8-no-bom");
            AddOutput(invocation, directory, "envelope.json", Utf8NoBom.GetBytes(invocation.EnvelopeJson), "utf-8-no-bom");
            AddOutput(invocation, directory, "write-preimage.utf8", invocation.WritePreimage, "utf-8-no-bom");
            AddOutput(invocation, directory, "read-preimage.utf8", invocation.ReadPreimage, "utf-8-no-bom");
            AddOutput(invocation, directory, "checksum.txt", Encoding.ASCII.GetBytes(invocation.StoredChecksum),
                "ascii-lower-hex-no-newline");
            AddOutput(invocation, directory, "decode-result.json", Utf8NoBom.GetBytes(invocation.DecodeResultJson), "utf-8-no-bom");

            if (invocation.ApiEnvelopeJson != null)
            {
                AddOutput(invocation, directory, "api-envelope.json", Utf8NoBom.GetBytes(invocation.ApiEnvelopeJson), "utf-8-no-bom");
                AddOutput(invocation, directory, "api-observation.json", Utf8NoBom.GetBytes(invocation.ApiObservationJson), "utf-8-no-bom");
            }
            if (invocation.ActiveStringsJson != null)
                AddOutput(invocation, directory, "active-strings.json", Utf8NoBom.GetBytes(invocation.ActiveStringsJson), "utf-8-no-bom");
        }

        private static void AddOutput(InvocationRecord invocation, string directory, string fileName,
            byte[] bytes, string encoding)
        {
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) throw new IOException("Capture output already exists: " + fileName);
            WriteNewCaptureFile(path, bytes);
            invocation.Outputs.Add(new OutputRecord
            {
                path = "cultures/" + invocation.Culture + "/" + invocation.CaseId + "/" + fileName,
                bytes = bytes.LongLength,
                sha256 = HashBytes(bytes),
                encoding = encoding
            });
        }

        private static void WriteFixtureIndex()
        {
            byte[] proofBytes = ReadProjectFile(HistoricalProofPath);
            Assert.That(HashBytes(proofBytes), Is.EqualTo(HistoricalProofSha256),
                "The complete historical tree proof must be the approved CI copy.");
            List<SourceRecord> originalSources = RequiredOriginalSourcePaths.Select(DescribeProjectFile).ToList();
            List<SourceRecord> runtimeAdditions = RuntimeAdditionPaths.Select(DescribeProjectFile).ToList();
            SourceRecord packageManifest = DescribeProjectFile("Packages/manifest.json");
            SourceRecord packageLock = DescribeProjectFile("Packages/packages-lock.json");
            SourceRecord projectVersion = DescribeProjectFile("ProjectSettings/ProjectVersion.txt");
            string editorRevision = ReadEditorRevision(projectVersion.path);
            Assert.That(Application.unityVersion, Is.EqualTo(ExpectedUnityVersion));
            Assert.That(editorRevision, Is.EqualTo(ExpectedUnityRevision));

            var cases = new List<IndexCaseRecord>();
            foreach (string caseId in CaseOrder)
            {
                foreach (string culture in CultureOrder)
                {
                    if (!Invocations.TryGetValue(InvocationKey(caseId, culture), out InvocationRecord invocation)) continue;
                    cases.Add(new IndexCaseRecord
                    {
                        caseId = invocation.CaseId,
                        culture = invocation.Culture,
                        role = invocation.Role,
                        hasActiveExpedition = invocation.HasActiveExpedition,
                        activePayloadPresent = invocation.ActivePayloadPresent,
                        decode = new DecodeIndexRecord
                        {
                            status = DecodeStatusName(invocation.DecodeStatus),
                            accepted = invocation.DecodeStatus == DecodeStatus.Valid
                        },
                        apiObservation = invocation.ApiObservation,
                        outputs = invocation.Outputs == null ? Array.Empty<OutputRecord>() : invocation.Outputs.ToArray()
                    });
                }
            }

            var index = new FixtureIndexRecord
            {
                schemaVersion = 1,
                captureKind = CaptureKind,
                historicalSource = new HistoricalSourceRecord
                {
                    commit = HistoricalCommit,
                    tree = HistoricalTree,
                    schemaVersion = V5SourceCaptureInputs.SchemaVersion,
                    contentVersion = V5SourceCaptureInputs.ContentVersion,
                    fullTreeProof = new FullTreeProofRecord
                    {
                        path = HistoricalProofPath,
                        bytes = proofBytes.LongLength,
                        sha256 = HashBytes(proofBytes),
                        trackedBlobCount = 4195,
                        sumOriginalBlobBytes = 159244821L
                    }
                },
                execution = new ExecutionRecord
                {
                    commit = _executionCommit,
                    editor = new EditorRecord
                    {
                        version = Application.unityVersion,
                        revision = editorRevision,
                        projectVersionPath = projectVersion.path,
                        projectVersionBytes = projectVersion.bytes,
                        projectVersionSha256 = projectVersion.sha256
                    },
                    packageManifest = packageManifest,
                    packageLock = packageLock
                },
                fixedInputHeader = new FixedHeaderRecord
                {
                    schemaVersion = V5SourceCaptureInputs.SchemaVersion,
                    contentVersion = V5SourceCaptureInputs.ContentVersion,
                    generation = V5SourceCaptureInputs.Generation,
                    sequence = V5SourceCaptureInputs.Sequence,
                    savedAtUtc = V5SourceCaptureInputs.SavedAtUtc,
                    seed = V5SourceCaptureInputs.Seed
                },
                fixedIds = new FixedIdsRecord
                {
                    profileGuidN = V5SourceCaptureInputs.ProfileGuidN,
                    activeRunGuidN = V5SourceCaptureInputs.ActiveRunGuidN,
                    settlementGuidN = V5SourceCaptureInputs.SettlementGuidN,
                    progressionPurchaseGuidN = V5SourceCaptureInputs.ProgressionPurchaseGuidN,
                    inventoryTransactionGuidN = V5SourceCaptureInputs.InventoryTransactionGuidN,
                    fortuneOfferGuidN = V5SourceCaptureInputs.FortuneOfferGuidN,
                    fortuneHistoryGuidN = V5SourceCaptureInputs.FortuneHistoryGuidN,
                    fortuneRerollGuidN = V5SourceCaptureInputs.FortuneRerollGuidN,
                    fortuneBrewGuidN = V5SourceCaptureInputs.FortuneBrewGuidN
                },
                cultures = CultureOrder.Select(name => new CultureIndexRecord { name = name, caseCount = 12, invocationCount = 12 }).ToArray(),
                expectedTotalCases = 12,
                expectedTotalInvocations = 24,
                requiredOriginalSources = originalSources.ToArray(),
                runtimeAdditions = runtimeAdditions.ToArray(),
                cases = cases.ToArray(),
                capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            string indexPath = Path.Combine(_captureRoot, "fixture-index.json");
            if (File.Exists(indexPath)) throw new IOException("fixture-index.json already exists; capture output is not new.");
            WriteNewCaptureFile(indexPath, Utf8NoBom.GetBytes(JsonUtility.ToJson(index, true)));
        }

        private static readonly string[] RequiredOriginalSourcePaths =
        {
            "Assets/Luckbound/Scripts/App/SaveRepository.cs",
            "Assets/Luckbound/Scripts/Core/Saves/SaveDocuments.cs",
            "Assets/Luckbound/Scripts/App/ExpeditionSaveMapper.cs",
            "Assets/Luckbound/Scripts/Core/Saves/ISaveStore.cs",
            "Assets/Luckbound/Scripts/Core/Fortune/FortuneOfferBookSnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Fortune/FortuneOfferBook.cs",
            "Assets/Luckbound/Scripts/Core/Inventory/ExpeditionInventorySnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Inventory/ExpeditionInventory.cs",
            "Assets/Luckbound/Scripts/Core/Inventory/InventoryItemStack.cs",
            "Assets/Luckbound/Scripts/Core/Identifiers/TransactionId.cs",
            "Assets/Luckbound/Scripts/Core/Economy/MerchantStockSnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Economy/MerchantOfferSnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Economy/RestStopUseSnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Progression/PermanentUpgradeRules.cs",
            "Assets/Luckbound/Scripts/Core/Expeditions/ExpeditionRouteGenerator.cs",
            "Assets/Luckbound/Scripts/Core/Expeditions/ExpeditionSettlementLedger.cs",
            "Assets/Luckbound/Scripts/App/Luckbound.App.asmdef",
            "Assets/Luckbound/Scripts/Core/Luckbound.Core.asmdef",
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "ProjectSettings/ProjectVersion.txt"
        };

        private static readonly string[] RuntimeAdditionPaths =
        {
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureInputs.cs",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureInputs.cs.meta",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureTests.cs",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureTests.cs.meta",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/Luckbound.V5SourceCapture.Tests.asmdef",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture/Luckbound.V5SourceCapture.Tests.asmdef.meta",
            "Assets/Luckbound/Tests/EditMode/V5SourceCapture.meta",
            "Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs",
            "Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs.meta",
            ".github/workflows/v5-source-capture.yml"
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static void AssertHistoricalEditorIdentity()
        {
            Assert.That(Application.unityVersion, Is.EqualTo(ExpectedUnityVersion),
                "This capture is valid only under the pinned historical Unity editor.");
            Assert.That(ReadEditorRevision("ProjectSettings/ProjectVersion.txt"), Is.EqualTo(ExpectedUnityRevision));
        }

        private static string ReadEditorRevision(string projectRelativePath)
        {
            string path = Path.Combine(_projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string[] lines = File.ReadAllLines(path);
            const string key = "m_EditorVersionWithRevision:";
            foreach (string line in lines)
            {
                if (!line.StartsWith(key, StringComparison.Ordinal)) continue;
                int open = line.LastIndexOf('(');
                int close = line.LastIndexOf(')');
                if (open < 0 || close <= open) throw new FormatException("ProjectVersion revision line is malformed.");
                return line.Substring(open + 1, close - open - 1).Trim();
            }
            throw new FormatException("ProjectVersion.txt does not include m_EditorVersionWithRevision.");
        }

        private static string ReadCaptureArgument(string[] arguments)
        {
            string value = null;
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                if (argument.StartsWith(CaptureArgument + "=", StringComparison.Ordinal))
                {
                    if (value != null) throw new ArgumentException("Capture directory was supplied more than once.");
                    value = argument.Substring(CaptureArgument.Length + 1);
                    continue;
                }
                if (!string.Equals(argument, CaptureArgument, StringComparison.Ordinal)) continue;
                if (value != null || index + 1 >= arguments.Length)
                    throw new ArgumentException("Capture argument requires exactly one directory value.");
                value = arguments[++index];
            }
            return value;
        }

        private static string ResolveNewCaptureDirectory(string projectRoot, string argumentValue)
        {
            string captureBase = Path.GetFullPath(Path.Combine(projectRoot, "Logs", "Scenarios", "V5Source"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.IsPathRooted(argumentValue)
                ? argumentValue
                : Path.Combine(projectRoot, argumentValue))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = captureBase + Path.DirectorySeparatorChar;
            // Strict casing also rejects ambiguous case aliases conservatively on Windows.
            if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Capture directory must be a new child under Logs/Scenarios/V5Source/ with exact path casing.");
            RequireNoReparseAncestors(candidate);
            if (Directory.Exists(candidate))
            {
                // Top-level enumeration detects non-emptiness without traversing any child link.
                if (Directory.EnumerateFileSystemEntries(candidate).Any())
                    throw new IOException("Capture directory contains previous results.");
            }
            else if (File.Exists(candidate))
            {
                throw new IOException("Capture path already names a file.");
            }
            return candidate;
        }

        private static void WriteNewCaptureFile(string path, byte[] bytes)
        {
            RequireNoReparseAncestors(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static void RequireNoReparseAncestors(string path)
        {
            string leaf = Path.GetFullPath(path);
            string current = leaf;
            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Capture/source paths cannot contain a reparse point: " + current);
                    if (!string.Equals(current, leaf, StringComparison.Ordinal) &&
                        (attributes & FileAttributes.Directory) == 0)
                        throw new IOException("An existing capture/source path ancestor is not a directory: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.Ordinal)) break;
                current = parent;
            }
        }

        private static string ReadExecutionCommit()
        {
            string value = Environment.GetEnvironmentVariable("LUCKBOUND_V5_EXECUTION_COMMIT");
            if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable("GITHUB_SHA");
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length != 40 || value.Any(character => !(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')))
                throw new InvalidOperationException("Capture requires LUCKBOUND_V5_EXECUTION_COMMIT or GITHUB_SHA as a full 40-hex commit.");
            return value;
        }

        private static SourceRecord DescribeProjectFile(string relativePath)
        {
            byte[] bytes = ReadProjectFile(relativePath);
            return new SourceRecord { path = relativePath.Replace('\\', '/'), bytes = bytes.LongLength, sha256 = HashBytes(bytes) };
        }

        private static byte[] ReadProjectFile(string relativePath)
        {
            string safeRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(_projectRoot, safeRelative));
            string prefix = _projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                throw new IOException("Project source path escaped the historical project root or has ambiguous casing.");
            RequireNoReparseAncestors(path);
            return File.ReadAllBytes(path);
        }

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes ?? Array.Empty<byte>());
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        [Serializable]
        private sealed class RecipeRecord
        {
            public int schemaVersion;
            public string caseId;
            public string role;
            public uint seed;
            public FixedHeaderRecord fixedInputHeader;
            public FixedIdsRecord fixedIds;
            public RecipeExpectation expected;
        }

        [Serializable]
        private sealed class RecipeExpectation
        {
            public string decode;
            public bool hasActiveExpedition;
            public bool activePayloadPresent;
            public string derivedFrom;
            public string tamperField;
            public int tamperFrom;
            public int tamperTo;
            public string inputFrozenUpgradeRepresentation;
            public string writerCloneMemorySnapshot;
            public string fixedEnvelopeWireFrozenUpgradeRepresentation;
            public string boundaryArraysInputRepresentation;
        }

        [Serializable]
        private sealed class FixedHeaderRecord
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public uint seed;
        }

        [Serializable]
        private sealed class FixedIdsRecord
        {
            public string profileGuidN;
            public string activeRunGuidN;
            public string settlementGuidN;
            public string progressionPurchaseGuidN;
            public string inventoryTransactionGuidN;
            public string fortuneOfferGuidN;
            public string fortuneHistoryGuidN;
            public string fortuneRerollGuidN;
            public string fortuneBrewGuidN;
        }

        [Serializable]
        private sealed class DecodeResultRecord
        {
            public string caseId;
            public string culture;
            public string status;
            public string statusEnum;
            public string statusNormalized;
            public bool accepted;
            public bool decodedEnvelopePresent;
            public string message;
        }

        [Serializable]
        private sealed class ApiObservation
        {
            public string caseId;
            public string culture;
            public string apiKind;
            public string apiSavedAtUtc;
            public int apiGeneration;
            public long apiSequence;
            public string apiChecksum;
            public string fixedSavedAtUtc;
            public int fixedGeneration;
            public long fixedSequence;
            public bool onlyHeaderFieldsDiffer;
            public string[] differingHeaderFields;
            public bool payloadsEqual;
            public string apiFrozenUpgradeWireRepresentation;
            public string fixedFrozenUpgradeWireRepresentation;
        }

        [Serializable]
        private sealed class ActiveStringsRecord
        {
            public string caseId;
            public bool hasActiveExpedition;
            public bool activePayloadPresent;
            public string routeSnapshotJson;
            public string inventorySnapshotJson;
            public string roomEntryCheckpointJson;
            public CompletedResultText[] completedRoomResults;
            public ActiveStringProvenance provenance;
        }

        [Serializable]
        private sealed class CompletedResultText
        {
            public string roomNodeId;
            public string resultId;
            public string resultJson;
        }

        [Serializable]
        private sealed class ActiveStringProvenance
        {
            public string routeSnapshotJson;
            public string inventorySnapshotJson;
            public string roomEntryCheckpointJson;
            public string completedRoomResults;
        }

        [Serializable]
        private sealed class OutputRecord
        {
            public string path;
            public long bytes;
            public string sha256;
            public string encoding;
        }

        [Serializable]
        private sealed class SourceRecord
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class FixtureIndexRecord
        {
            public int schemaVersion;
            public string captureKind;
            public HistoricalSourceRecord historicalSource;
            public ExecutionRecord execution;
            public FixedHeaderRecord fixedInputHeader;
            public FixedIdsRecord fixedIds;
            public CultureIndexRecord[] cultures;
            public int expectedTotalCases;
            public int expectedTotalInvocations;
            public SourceRecord[] requiredOriginalSources;
            public SourceRecord[] runtimeAdditions;
            public IndexCaseRecord[] cases;
            public string capturedAtUtc;
        }

        [Serializable]
        private sealed class HistoricalSourceRecord
        {
            public string commit;
            public string tree;
            public int schemaVersion;
            public int contentVersion;
            public FullTreeProofRecord fullTreeProof;
        }

        [Serializable]
        private sealed class FullTreeProofRecord
        {
            public string path;
            public long bytes;
            public string sha256;
            public int trackedBlobCount;
            public long sumOriginalBlobBytes;
        }

        [Serializable]
        private sealed class ExecutionRecord
        {
            public string commit;
            public EditorRecord editor;
            public SourceRecord packageManifest;
            public SourceRecord packageLock;
        }

        [Serializable]
        private sealed class EditorRecord
        {
            public string version;
            public string revision;
            public string projectVersionPath;
            public long projectVersionBytes;
            public string projectVersionSha256;
        }

        [Serializable]
        private sealed class CultureIndexRecord
        {
            public string name;
            public int caseCount;
            public int invocationCount;
        }

        [Serializable]
        private sealed class IndexCaseRecord
        {
            public string caseId;
            public string culture;
            public string role;
            public bool hasActiveExpedition;
            public bool activePayloadPresent;
            public DecodeIndexRecord decode;
            public ApiObservation apiObservation;
            public OutputRecord[] outputs;
        }

        [Serializable]
        private sealed class DecodeIndexRecord
        {
            public string status;
            public bool accepted;
        }

        private sealed class InvocationRecord
        {
            internal InvocationRecord(string caseId, string culture, string role, bool hasActiveExpedition,
                bool activePayloadPresent, DecodeStatus decodeStatus, string decodeResultJson, string recipeJson,
                string envelopeJson, byte[] writePreimage, byte[] readPreimage, string storedChecksum,
                string apiEnvelopeJson, string apiObservationJson, string activeStringsJson, ApiObservation apiObservation)
            {
                CaseId = caseId;
                Culture = culture;
                Role = role;
                HasActiveExpedition = hasActiveExpedition;
                ActivePayloadPresent = activePayloadPresent;
                DecodeStatus = decodeStatus;
                DecodeResultJson = decodeResultJson;
                RecipeJson = recipeJson;
                EnvelopeJson = envelopeJson;
                WritePreimage = (byte[])writePreimage.Clone();
                ReadPreimage = (byte[])readPreimage.Clone();
                StoredChecksum = storedChecksum;
                ApiEnvelopeJson = apiEnvelopeJson;
                ApiObservationJson = apiObservationJson;
                ActiveStringsJson = activeStringsJson;
                ApiObservation = apiObservation;
            }

            internal string CaseId { get; }
            internal string Culture { get; }
            internal string Role { get; }
            internal bool HasActiveExpedition { get; }
            internal bool ActivePayloadPresent { get; }
            internal DecodeStatus DecodeStatus { get; }
            internal string DecodeResultJson { get; }
            internal string RecipeJson { get; }
            internal string EnvelopeJson { get; }
            internal byte[] WritePreimage { get; }
            internal byte[] ReadPreimage { get; }
            internal string StoredChecksum { get; }
            internal string ApiEnvelopeJson { get; }
            internal string ApiObservationJson { get; }
            internal string ActiveStringsJson { get; }
            internal ApiObservation ApiObservation { get; }
            internal List<OutputRecord> Outputs { get; set; }
        }

        private sealed class ProfileCandidate
        {
            internal ProfileCandidate(string apiJson, ProfileSaveEnvelope envelope, ProfileSaveEnvelope fixedCandidate,
                ExpeditionSaveData inputActive)
            {
                ApiJson = apiJson;
                Envelope = envelope;
                FixedCandidate = fixedCandidate;
                InputActive = inputActive;
            }

            internal string ApiJson { get; }
            internal ProfileSaveEnvelope Envelope { get; }
            internal ProfileSaveEnvelope FixedCandidate { get; }
            internal ExpeditionSaveData InputActive { get; }
        }

        private sealed class CultureInvariantOutput
        {
            internal CultureInvariantOutput(string recipeJson, string envelopeJson, byte[] writePreimage,
                byte[] readPreimage, string checksum, string activeStringsJson)
            {
                RecipeJson = recipeJson;
                EnvelopeJson = envelopeJson;
                WritePreimage = (byte[])writePreimage.Clone();
                ReadPreimage = (byte[])readPreimage.Clone();
                Checksum = checksum;
                ActiveStringsJson = activeStringsJson;
            }

            internal string RecipeJson { get; }
            internal string EnvelopeJson { get; }
            internal byte[] WritePreimage { get; }
            internal byte[] ReadPreimage { get; }
            internal string Checksum { get; }
            internal string ActiveStringsJson { get; }
        }

        private sealed class ImmediateMemoryStore : ISaveStore
        {
            private readonly Dictionary<SaveDocument, SaveStorageSnapshot> _documents =
                new Dictionary<SaveDocument, SaveStorageSnapshot>();

            internal string PrimaryJson { get; private set; }
            internal SaveDocument LastDocument { get; private set; }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(_documents.TryGetValue(document, out SaveStorageSnapshot snapshot)
                    ? snapshot : new SaveStorageSnapshot(null, null));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                LastDocument = document;
                PrimaryJson = json;
                _documents[document] = new SaveStorageSnapshot(json,
                    lastKnownGoodPrimaryJson ?? (_documents.TryGetValue(document, out SaveStorageSnapshot previous)
                        ? previous.BackupJson : null));
                return Task.CompletedTask;
            }
        }
    }
}



