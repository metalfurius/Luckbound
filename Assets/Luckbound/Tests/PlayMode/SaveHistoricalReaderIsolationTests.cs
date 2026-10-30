using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.App.Compatibility;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveHistoricalReaderIsolationTests
    {
        private const string FixtureDirectory = "Luckbound/Tests/Fixtures/Saves/V6";
        private const string V5FixtureDirectory = "Luckbound/Tests/Fixtures/Saves/V5";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [TestCase("V6-P0-profile-no-active", "Valid")]
        [TestCase("V6-P1-active-known-zero", "Valid")]
        [TestCase("V6-P1-active-known-nontrivial", "Valid")]
        [TestCase("V6-P2-active-unknown-null", "Valid")]
        [TestCase("V6-P2-active-unknown-inline-empty", "Valid")]
        [TestCase("V6-P3-receipts-full", "Valid")]
        [TestCase("V6-P4-null-empty-boundary", "Corrupted")]
        [TestCase("V6-P5-flag-payload-conflict", "Corrupted")]
        public void FrozenProfileFixturesUseTheReaderAndRealDecodeDispatch(string fixtureId, string expectedStatus)
        {
            string suffix = fixtureId + ".Frozen.";
            byte[] envelopeBytes = ReadFixtureBytes(suffix + "envelope.json");
            byte[] expectedPreimage = ReadFixtureBytes(suffix + "checksum-preimage.utf8");
            byte[] expectedChecksumBytes = ReadFixtureBytes(suffix + "checksum.sha256.txt");
            string json = StrictUtf8.GetString(envelopeBytes);
            string expectedChecksum = Encoding.ASCII.GetString(expectedChecksumBytes);

            Assert.That(expectedChecksumBytes.Length, Is.EqualTo(64), "Fixture checksum text is exact ASCII hex.");
            Assert.That(ComputeSha256(expectedPreimage), Is.EqualTo(expectedChecksum));
            Assert.That(SaveV6ChecksumInspector.InspectProfile(json).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));

            LegacyProfileSaveEnvelopeV6 verified;
            DecodeStatus readStatus = SaveV6HistoricalReader.ReadProfile(json, out verified);
            Assert.That(readStatus.ToString(), Is.EqualTo(expectedStatus));
            if (readStatus == DecodeStatus.Valid)
            {
                Assert.That(verified, Is.Not.Null);
                CollectionAssert.AreEqual(expectedPreimage,
                    SaveV6HistoricalReader.GetProfileChecksumPreimage(verified));
                byte[] first = SaveV6HistoricalReader.GetProfileChecksumPreimage(verified);
                byte[] second = SaveV6HistoricalReader.GetProfileChecksumPreimage(verified);
                Assert.That(first, Is.Not.SameAs(second));
                byte original = second[0];
                first[0] = original == byte.MaxValue ? (byte)0 : (byte)(original + 1);
                Assert.That(second[0], Is.EqualTo(original), "Reader preimages are independent byte arrays.");
            }
            else
            {
                Assert.That(verified, Is.Null, "The reader exposes only a verified envelope.");
                LegacyProfileSaveEnvelopeV6 checksumSource = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV6>(json);
                CollectionAssert.AreEqual(expectedPreimage,
                    SaveV6HistoricalReader.GetProfileChecksumPreimage(checksumSource));
            }

            ProfileSaveEnvelope dispatched;
            int sourceContentVersion;
            DecodeStatus dispatchStatus = SaveJsonCodec.Decode(json, out dispatched, out sourceContentVersion);
            Assert.That(dispatchStatus.ToString(), Is.EqualTo(expectedStatus), "The public content-version dispatch must agree with the isolated reader.");
            if (readStatus == DecodeStatus.Valid)
            {
                Assert.That(sourceContentVersion, Is.EqualTo(6));
                ProfileSaveEnvelope mapped = HistoricalSaveMapper.ToLive(verified);
                Assert.That(dispatched, Is.Not.Null);
                Assert.That(dispatched.contentVersion, Is.EqualTo(mapped.contentVersion));
                Assert.That(dispatched.checksum, Is.EqualTo(mapped.checksum));
                Assert.That(dispatched.profile.availableRenown, Is.EqualTo(mapped.profile.availableRenown));
                CollectionAssert.AreEqual(mapped.profile.unlockedRaceIds, dispatched.profile.unlockedRaceIds);
                Assert.That(dispatched.hasActiveExpedition, Is.EqualTo(mapped.hasActiveExpedition));
                if (mapped.activeExpedition != null)
                {
                    Assert.That(dispatched.activeExpedition.routeSnapshotJson,
                        Is.EqualTo(mapped.activeExpedition.routeSnapshotJson));
                    Assert.That(dispatched.activeExpedition.roomEntryCheckpointJson,
                        Is.EqualTo(mapped.activeExpedition.roomEntryCheckpointJson));
                }
            }
            else
            {
                Assert.That(sourceContentVersion, Is.EqualTo(0), "Rejected input must not publish an authenticated source version.");
                Assert.That(dispatched, Is.Null);
            }

            CollectionAssert.AreEqual(envelopeBytes, ReadFixtureBytes(suffix + "envelope.json"),
                "Reader and dispatch calls preserve the frozen source bytes.");
        }

        [Test]
        public void FrozenSettingsFixtureUsesTheReaderAndRealDecodeDispatch()
        {
            const string suffix = "V6-S0-settings.Frozen.";
            byte[] envelopeBytes = ReadFixtureBytes(suffix + "envelope.json");
            byte[] expectedPreimage = ReadFixtureBytes(suffix + "checksum-preimage.utf8");
            byte[] checksumBytes = ReadFixtureBytes(suffix + "checksum.sha256.txt");
            string json = StrictUtf8.GetString(envelopeBytes);
            Assert.That(checksumBytes.Length, Is.EqualTo(64));
            Assert.That(ComputeSha256(expectedPreimage), Is.EqualTo(Encoding.ASCII.GetString(checksumBytes)));
            Assert.That(SaveV6ChecksumInspector.InspectSettings(json).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));

            LegacySettingsSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadSettings(json, out verified), Is.EqualTo(DecodeStatus.Valid));
            Assert.That(verified, Is.Not.Null);
            CollectionAssert.AreEqual(expectedPreimage,
                SaveV6HistoricalReader.GetSettingsChecksumPreimage(verified));

            SettingsSaveEnvelope dispatched;
            Assert.That(SaveJsonCodec.Decode(json, out dispatched), Is.EqualTo(DecodeStatus.Valid));
            SettingsSaveEnvelope mapped = HistoricalSaveMapper.ToLive(verified);
            Assert.That(dispatched.contentVersion, Is.EqualTo(mapped.contentVersion));
            Assert.That(dispatched.checksum, Is.EqualTo(mapped.checksum));
            Assert.That(dispatched.settings.languageId, Is.EqualTo(mapped.settings.languageId));
            Assert.That(dispatched.settings.masterVolume, Is.EqualTo(mapped.settings.masterVolume));
            Assert.That(dispatched.settings.musicVolume, Is.EqualTo(mapped.settings.musicVolume));
            Assert.That(dispatched.settings.effectsVolume, Is.EqualTo(mapped.settings.effectsVolume));
            Assert.That(dispatched.settings.ambienceVolume, Is.EqualTo(mapped.settings.ambienceVolume));
            CollectionAssert.AreEqual(envelopeBytes, ReadFixtureBytes(suffix + "envelope.json"));
        }

        [Test]
        public void FrozenV5ReaderReproducesAll24NativePreimagesAndRealDispatchStatuses()
        {
            V5ReaderFixtureManifest manifest = JsonUtility.FromJson<V5ReaderFixtureManifest>(
                StrictUtf8.GetString(ReadV5FixtureBytes("reader-fixtures.json")));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.schemaVersion, Is.EqualTo(1));
            Assert.That(manifest.captureRoot, Is.EqualTo("capture"));
            Assert.That(manifest.rows, Is.Not.Null);
            Assert.That(manifest.rows.Length, Is.EqualTo(24));
            Assert.That(manifest.nativeIndexSha256, Is.EqualTo("8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9"));
            Assert.That(manifest.authoritySha256, Is.EqualTo("55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7"));
            Assert.That(ComputeSha256(ReadV5FixtureBytes(Path.Combine(manifest.captureRoot, "fixture-index.json"))),
                Is.EqualTo(manifest.nativeIndexSha256));
            Assert.That(ComputeSha256(ReadV5FixtureBytes(manifest.authorityPath)), Is.EqualTo(manifest.authoritySha256));
            string[] expectedCases =
            {
                "P0-no-active", "P1-known-zero", "P1-known-nontrivial", "P2-unknown-null",
                "P2-unknown-inline", "P3-receipts-completed", "B0-arrays-null", "B1-arrays-empty",
                "S0-settings", "M0-false-marker-payload", "M1-true-marker-null", "N0-tampered"
            };
            string[] expectedCultures = { "en-US", "es-ES" };
            CollectionAssert.AreEquivalent(expectedCases, manifest.rows.Select(row => row.caseId).Distinct(StringComparer.Ordinal));
            foreach (string caseId in expectedCases)
            foreach (string culture in expectedCultures)
            {
                V5ReaderFixtureRow row = manifest.rows.Single(candidate =>
                    candidate.caseId == caseId && candidate.culture == culture);
                bool negative = caseId == "M0-false-marker-payload" || caseId == "M1-true-marker-null" ||
                    caseId == "N0-tampered";
                Assert.That(row.expectedDecode, Is.EqualTo(negative ? "Corrupted" : "Valid"),
                    caseId + "/" + culture + " frozen expectation");
                Assert.That(row.document, Is.EqualTo(caseId == "S0-settings" ? "Settings" : "Profile"),
                    caseId + "/" + culture + " document expectation");
                Assert.That(row.sourceContentVersion,
                    Is.EqualTo(row.document == "Profile" && !negative ? 5 : 0),
                    caseId + "/" + culture + " authenticated source version expectation");
            }

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                foreach (V5ReaderFixtureRow row in manifest.rows)
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(row.culture);
                    string label = row.caseId + "/" + row.culture + "/" + row.document;
                    string envelopePath = ResolveV5EnvelopePath(manifest.captureRoot, row.envelopePath);
                    string directory = Path.GetDirectoryName(envelopePath);
                    byte[] envelopeBytes = File.ReadAllBytes(envelopePath);
                    byte[] expectedPreimage = File.ReadAllBytes(Path.Combine(directory, "read-preimage.utf8"));
                    byte[] storedChecksumBytes = File.ReadAllBytes(Path.Combine(directory, "checksum.txt"));
                    string json = StrictUtf8.GetString(envelopeBytes);
                    string storedChecksum = Encoding.ASCII.GetString(storedChecksumBytes);

                    Assert.That(ComputeSha256(envelopeBytes), Is.EqualTo(row.envelopeSha256), label + " raw envelope hash");
                    Assert.That(storedChecksumBytes.Length, Is.EqualTo(64), label + " checksum length");
                    Assert.That(ComputeSha256(expectedPreimage) == storedChecksum, Is.EqualTo(row.caseId != "N0-tampered"),
                        label + " native read-preimage checksum relationship");

                    if (row.document == "Profile")
                    {
                        LegacyProfileSaveEnvelopeV5 raw = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV5>(json);
                        Assert.That(raw, Is.Not.Null, label + " frozen raw DTO");
                        Assert.That(raw.contentVersion, Is.EqualTo(5), label + " raw content version");
                        Assert.That(raw.checksum, Is.EqualTo(storedChecksum), label + " stored checksum");
                        byte[] projected = SaveV5HistoricalReader.GetProfileChecksumPreimage(raw);
                        CollectionAssert.AreEqual(expectedPreimage, projected, label + " raw-to-native preimage");

                        LegacyProfileSaveEnvelopeV5 verified;
                        DecodeStatus readStatus = SaveV5HistoricalReader.ReadProfile(json, out verified);
                        Assert.That(readStatus.ToString(), Is.EqualTo(row.expectedDecode), label + " frozen read");
                        if (row.expectedDecode == "Valid")
                        {
                            Assert.That(verified, Is.Not.Null, label + " verified envelope");
                            Assert.That(verified.contentVersion, Is.EqualTo(row.sourceContentVersion), label + " source version");
                            Assert.That(row.sourceContentVersion, Is.EqualTo(5), label + " fixture source version");
                            CollectionAssert.AreEqual(expectedPreimage,
                                SaveV5HistoricalReader.GetProfileChecksumPreimage(verified),
                                label + " verified-reader preimage");
                        }
                        else
                        {
                            Assert.That(verified, Is.Null, label + " rejected envelope is withheld");
                            Assert.That(row.sourceContentVersion, Is.EqualTo(0), label + " rejected source version");
                        }

                        ProfileSaveEnvelope dispatched;
                        int sourceContentVersion;
                        DecodeStatus dispatchStatus = SaveJsonCodec.Decode(json, out dispatched, out sourceContentVersion);
                        Assert.That(dispatchStatus.ToString(), Is.EqualTo(row.expectedDecode), label + " actual profile dispatch");
                        Assert.That(sourceContentVersion, Is.EqualTo(row.sourceContentVersion), label + " dispatch source version");
                        if (row.expectedDecode == "Valid")
                        {
                            Assert.That(dispatched, Is.Not.Null, label + " mapped envelope");
                            Assert.That(dispatched.contentVersion, Is.EqualTo(6), label + " migrated in-memory content version");
                            Assert.That(dispatched.profile.processedContractProgressEvents, Is.Not.Null,
                                label + " v5 migration supplies the current empty progress-event receipt array");
                            Assert.That(dispatched.profile.processedContractProgressEvents.Length, Is.EqualTo(0),
                                label + " v5 migration supplies no invented progress events");
                        }
                        else
                        {
                            Assert.That(dispatched, Is.Null, label + " rejected dispatch envelope");
                        }

                        if (row.caseId == "M0-false-marker-payload" || row.caseId == "M1-true-marker-null")
                        {
                            Assert.That(ComputeSha256(projected), Is.EqualTo(storedChecksum),
                                label + " semantic negative keeps a valid checksum");
                            Assert.That(readStatus, Is.EqualTo(DecodeStatus.Corrupted),
                                label + " remains rejected for its original semantic reason");
                        }
                        if (row.caseId == "N0-tampered")
                        {
                            Assert.That(ComputeSha256(projected), Is.Not.EqualTo(storedChecksum),
                                label + " retains its original checksum mismatch");
                            Assert.That(readStatus, Is.EqualTo(DecodeStatus.Corrupted), label + " remains checksum-rejected");
                        }
                    }
                    else
                    {
                        Assert.That(row.document, Is.EqualTo("Settings"), label + " document kind");
                        LegacySettingsSaveEnvelopeV5 raw = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV5>(json);
                        Assert.That(raw, Is.Not.Null, label + " frozen raw DTO");
                        Assert.That(raw.contentVersion, Is.EqualTo(5), label + " raw content version");
                        Assert.That(raw.checksum, Is.EqualTo(storedChecksum), label + " stored checksum");
                        CollectionAssert.AreEqual(expectedPreimage,
                            SaveV5HistoricalReader.GetSettingsChecksumPreimage(raw), label + " raw-to-native preimage");

                        LegacySettingsSaveEnvelopeV5 verified;
                        Assert.That(SaveV5HistoricalReader.ReadSettings(json, out verified).ToString(),
                            Is.EqualTo(row.expectedDecode), label + " frozen read");
                        Assert.That(verified, Is.Not.Null, label + " verified settings");
                        CollectionAssert.AreEqual(expectedPreimage,
                            SaveV5HistoricalReader.GetSettingsChecksumPreimage(verified),
                            label + " verified-reader preimage");
                        SettingsSaveEnvelope dispatched;
                        Assert.That(SaveJsonCodec.Decode(json, out dispatched), Is.EqualTo(DecodeStatus.Valid),
                            label + " actual settings dispatch");
                        Assert.That(dispatched, Is.Not.Null);
                        Assert.That(dispatched.contentVersion, Is.EqualTo(6), label + " migrated in-memory content version");
                    }

                    CollectionAssert.AreEqual(envelopeBytes, File.ReadAllBytes(envelopePath), label + " raw bytes remain unchanged");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void V5LiveMappingCannotAliasTheFrozenFortuneGraphOrRawSource()
        {
            const string relative = "cultures/en-US/P3-receipts-completed/envelope.json";
            string path = ResolveV5EnvelopePath("capture", relative);
            byte[] rawEnvelope = File.ReadAllBytes(path);
            string json = StrictUtf8.GetString(rawEnvelope);
            LegacyProfileSaveEnvelopeV5 verified;
            Assert.That(SaveV5HistoricalReader.ReadProfile(json, out verified), Is.EqualTo(DecodeStatus.Valid));
            string frozenBefore = JsonUtility.ToJson(verified, false);
            byte[] preimageBefore = SaveV5HistoricalReader.GetProfileChecksumPreimage(verified);
            ProfileSaveEnvelope live = HistoricalSaveMapper.ToLive(verified);

            live.profile.unlockedRaceIds[0] = "mutation.race";
            live.profile.contractCounters[0].progress++;
            live.profile.progressionPurchaseTransactions[0].cost++;
            live.activeExpedition.selectedSkillIds[0] = "mutation.skill";
            live.activeExpedition.completedRoomResults[0].resultJson = "mutation.result";
            live.activeExpedition.fortuneOfferBookSnapshot.offers[0].history[0].optionIds[0] = "mutation.option";
            live.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts[0].optionIds[0] = "mutation.receipt-option";
            live.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges++;

            Assert.That(JsonUtility.ToJson(verified, false), Is.EqualTo(frozenBefore));
            CollectionAssert.AreEqual(preimageBefore, SaveV5HistoricalReader.GetProfileChecksumPreimage(verified));
            CollectionAssert.AreEqual(rawEnvelope, File.ReadAllBytes(path));
            Assert.That(verified.contentVersion, Is.EqualTo(5));
            Assert.That(live.contentVersion, Is.EqualTo(6));
            Assert.That(live.profile.processedContractProgressEvents, Is.Not.Null);
            Assert.That(live.profile.processedContractProgressEvents.Length, Is.EqualTo(0));

            ProfileSaveEnvelope secondLive = HistoricalSaveMapper.ToLive(verified);
            Assert.That(secondLive.profile.unlockedRaceIds[0], Is.Not.EqualTo("mutation.race"));
            Assert.That(secondLive.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges,
                Is.Not.EqualTo(live.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges));
        }

        [TestCase(null, "Missing")]
        [TestCase("", "Missing")]
        [TestCase(" \r\n\t", "Missing")]
        [TestCase("{", "Corrupted")]
        [TestCase("not-json", "Corrupted")]
        public void ProfileReaderClassifiesBlankAndMalformedDocuments(string json, string expected)
        {
            LegacyProfileSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadProfile(json, out verified).ToString(), Is.EqualTo(expected));
            Assert.That(verified, Is.Null);
        }

        [TestCase(null, "Missing")]
        [TestCase("", "Missing")]
        [TestCase(" \r\n\t", "Missing")]
        [TestCase("{", "Corrupted")]
        [TestCase("not-json", "Corrupted")]
        public void SettingsReaderClassifiesBlankAndMalformedDocuments(string json, string expected)
        {
            LegacySettingsSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadSettings(json, out verified).ToString(), Is.EqualTo(expected));
            Assert.That(verified, Is.Null);
        }

        [Test]
        public void DirectReadersRejectForeignVersionsAsIncompatible()
        {
            LegacyProfileSaveEnvelopeV6 profile = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV6>(
                ReadFixture("V6-P0-profile-no-active.Frozen.envelope.json"));
            profile.contentVersion = 5;
            LegacyProfileSaveEnvelopeV6 verifiedProfile;
            Assert.That(SaveV6HistoricalReader.ReadProfile(JsonUtility.ToJson(profile, false), out verifiedProfile),
                Is.EqualTo(DecodeStatus.Incompatible));
            Assert.That(verifiedProfile, Is.Null);

            LegacySettingsSaveEnvelopeV6 settings = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV6>(
                ReadFixture("V6-S0-settings.Frozen.envelope.json"));
            settings.schemaVersion = 2;
            LegacySettingsSaveEnvelopeV6 verifiedSettings;
            Assert.That(SaveV6HistoricalReader.ReadSettings(JsonUtility.ToJson(settings, false), out verifiedSettings),
                Is.EqualTo(DecodeStatus.Incompatible));
            Assert.That(verifiedSettings, Is.Null);
        }

        [Test]
        public void ChecksumMismatchKeepsItsRoleDistinctFromMalformedJson()
        {
            string original = ReadFixture("V6-P0-profile-no-active.Frozen.envelope.json");
            const string originalToken = "\"availableRenown\": 0";
            const string changedToken = "\"availableRenown\": 1";
            int index = original.IndexOf(originalToken, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(original.IndexOf(originalToken, index + originalToken.Length, StringComparison.Ordinal), Is.EqualTo(-1));
            string changed = original.Substring(0, index) + changedToken +
                original.Substring(index + originalToken.Length);

            LegacyProfileSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadProfile(changed, out verified), Is.EqualTo(DecodeStatus.Corrupted));
            Assert.That(verified, Is.Null);
            Assert.That(SaveV6ChecksumInspector.InspectProfile(changed).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumMismatch));
            Assert.That(SaveV6ChecksumInspector.InspectProfile("{").Status,
                Is.EqualTo(SaveV6ChecksumStatus.Malformed));
        }

        [Test]
        public void UnknownFalseMarkerZeroUpgradesNormalizeOnlyAfterChecksumVerification()
        {
            LegacyProfileSaveEnvelopeV6 source = ParseProfile("V6-P2-active-unknown-inline-empty.Frozen.envelope.json");
            Assert.That(source.activeExpedition, Is.Not.Null);
            Assert.That(source.activeExpedition.hasFrozenPermanentUpgrades, Is.False);
            Assert.That(source.activeExpedition.frozenPermanentUpgrades, Is.Not.Null);

            string json = Rechecksum(source);
            Assert.That(SaveV6ChecksumInspector.InspectProfile(json).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid));
            LegacyProfileSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadProfile(json, out verified), Is.EqualTo(DecodeStatus.Valid));
            Assert.That(verified.activeExpedition.frozenPermanentUpgrades, Is.Null);
        }

        [Test]
        public void ChecksumValidUnknownNonzeroFrozenUpgradesAreRejectedByTheReader()
        {
            LegacyProfileSaveEnvelopeV6 source = ParseProfile("V6-P2-active-unknown-null.Frozen.envelope.json");
            source.activeExpedition.frozenPermanentUpgrades = new LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6
            {
                healthUpgradeLevel = 1
            };
            AssertChecksumValidProfileIsRejected(source);
        }

        [Test]
        public void ChecksumValidCheckpointFortuneIdentityMismatchIsRejectedByTheReader()
        {
            LegacyProfileSaveEnvelopeV6 source = ParseProfile("V6-P1-active-known-nontrivial.Frozen.envelope.json");
            LegacyExpeditionRoomCheckpointSaveDataV6 checkpoint = JsonUtility.FromJson<LegacyExpeditionRoomCheckpointSaveDataV6>(
                source.activeExpedition.roomEntryCheckpointJson);
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(checkpoint.fortuneOfferBookSnapshot, Is.Not.Null);
            checkpoint.fortuneOfferBookSnapshot.seed++;
            source.activeExpedition.roomEntryCheckpointJson = JsonUtility.ToJson(checkpoint, false);

            AssertChecksumValidProfileIsRejected(source);
        }

        [Test]
        public void ChecksumValidDuplicateFortuneTransactionIsRejectedByTheReader()
        {
            LegacyProfileSaveEnvelopeV6 source = ParseProfile("V6-P3-receipts-full.Frozen.envelope.json");
            LegacyFortuneOfferBookSnapshotV6 book = source.activeExpedition.fortuneOfferBookSnapshot;
            Assert.That(book.rerollReceipts, Is.Not.Null.And.Not.Empty);
            Assert.That(book.fortuneBrewReceipts, Is.Not.Null.And.Not.Empty);
            book.fortuneBrewReceipts[0].transactionId = book.rerollReceipts[0].transactionId;

            AssertChecksumValidProfileIsRejected(source);
        }

        [Test]
        public void LiveMappingCannotAliasFrozenArraysOrNestedFortuneRecords()
        {
            byte[] rawEnvelope = ReadFixtureBytes("V6-P3-receipts-full.Frozen.envelope.json");
            string json = StrictUtf8.GetString(rawEnvelope);
            LegacyProfileSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadProfile(json, out verified), Is.EqualTo(DecodeStatus.Valid));
            string frozenBefore = JsonUtility.ToJson(verified, false);
            byte[] preimageBefore = SaveV6HistoricalReader.GetProfileChecksumPreimage(verified);
            string checksumBefore = verified.checksum;
            ProfileSaveEnvelope live = HistoricalSaveMapper.ToLive(verified);

            live.profile.unlockedRaceIds[0] = "mutation.race";
            live.profile.contractCounters[0].progress++;
            live.profile.progressionPurchaseTransactions[0].cost++;
            live.activeExpedition.selectedSkillIds[0] = "mutation.skill";
            live.activeExpedition.completedRoomResults[0].resultJson = "mutation.result";
            live.activeExpedition.fortuneOfferBookSnapshot.offers[0].history[0].optionIds[0] = "mutation.option";
            live.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts[0].optionIds[0] = "mutation.receipt-option";
            live.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges++;

            Assert.That(JsonUtility.ToJson(verified, false), Is.EqualTo(frozenBefore));
            Assert.That(verified.checksum, Is.EqualTo(checksumBefore));
            CollectionAssert.AreEqual(preimageBefore, SaveV6HistoricalReader.GetProfileChecksumPreimage(verified));
            CollectionAssert.AreEqual(rawEnvelope, ReadFixtureBytes("V6-P3-receipts-full.Frozen.envelope.json"));
            ProfileSaveEnvelope secondLive = HistoricalSaveMapper.ToLive(verified);
            Assert.That(secondLive.profile.unlockedRaceIds[0], Is.Not.EqualTo("mutation.race"));
            Assert.That(secondLive.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges,
                Is.Not.EqualTo(live.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].currentCharges));
        }

        [Test]
        public void FrozenV6TypeClosureMatchesIndependentOrderedShapeAndDefaults()
        {
            Dictionary<string, string> expectedShapes = CreateExpectedFrozenShapes();
            AssertFrozenTypeClosure(typeof(LegacyProfileSaveEnvelopeV6), expectedShapes);
            Assert.That(expectedShapes.Count, Is.EqualTo(16));

            AssertDefaults(typeof(LegacyProfileSaveEnvelopeV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacySettingsSaveEnvelopeV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyProfileSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyContractCounterSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyContractProgressEventReceiptSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyPermanentUpgradePurchaseSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionSaveDataV6), new Dictionary<string, object>
            {
                { "maximumHealth", 100 }, { "currentHealth", 100 },
                { "maximumEnergy", 50 }, { "currentEnergy", 50 }, { "healthPotionCharges", 2 }
            });
            AssertDefaults(typeof(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionRoomCheckpointSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionRoomCompletionSaveDataV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferBookSnapshotV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferSnapshotV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferHistorySnapshotV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneRerollReceiptSnapshotV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneBrewReceiptSnapshotV6), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyGameSettingsSaveDataV6), new Dictionary<string, object>
            {
                { "languageId", "es" }, { "masterVolume", 1f }, { "musicVolume", 0.65f },
                { "effectsVolume", 0.8f }, { "ambienceVolume", 0.45f }
            });
        }

        [Test]
        public void FrozenV5TypeClosureMatchesIndependentOrderedShapeAndDefaults()
        {
            Dictionary<string, string> expectedShapes = CreateExpectedFrozenV5Shapes();
            AssertFrozenTypeClosure(typeof(LegacyProfileSaveEnvelopeV5), expectedShapes);
            Assert.That(expectedShapes.Count, Is.EqualTo(15));

            AssertDefaults(typeof(LegacyProfileSaveEnvelopeV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacySettingsSaveEnvelopeV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyProfileSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyContractCounterSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyPermanentUpgradePurchaseSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionSaveDataV5), new Dictionary<string, object>
            {
                { "maximumHealth", 100 }, { "currentHealth", 100 },
                { "maximumEnergy", 50 }, { "currentEnergy", 50 }, { "healthPotionCharges", 2 }
            });
            AssertDefaults(typeof(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionRoomCheckpointSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyExpeditionRoomCompletionSaveDataV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferBookSnapshotV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferSnapshotV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneOfferHistorySnapshotV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneRerollReceiptSnapshotV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyFortuneBrewReceiptSnapshotV5), new Dictionary<string, object>());
            AssertDefaults(typeof(LegacyGameSettingsSaveDataV5), new Dictionary<string, object>
            {
                { "languageId", "es" }, { "masterVolume", 1f }, { "musicVolume", 0.65f },
                { "effectsVolume", 0.8f }, { "ambienceVolume", 0.45f }
            });
        }

        [Test]
        public void ReaderAndDispatchPublicSignaturesKeepTheFrozenBoundaryExplicit()
        {
            MethodInfo readProfile = typeof(SaveV6HistoricalReader).GetMethod("ReadProfile",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo readSettings = typeof(SaveV6HistoricalReader).GetMethod("ReadSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo profilePreimage = typeof(SaveV6HistoricalReader).GetMethod("GetProfileChecksumPreimage",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo settingsPreimage = typeof(SaveV6HistoricalReader).GetMethod("GetSettingsChecksumPreimage",
                BindingFlags.Static | BindingFlags.NonPublic);
            AssertReaderSignature(readProfile, typeof(LegacyProfileSaveEnvelopeV6));
            AssertReaderSignature(readSettings, typeof(LegacySettingsSaveEnvelopeV6));
            AssertPreimageSignature(profilePreimage, typeof(LegacyProfileSaveEnvelopeV6));
            AssertPreimageSignature(settingsPreimage, typeof(LegacySettingsSaveEnvelopeV6));

            MethodInfo[] dispatches = typeof(SaveJsonCodec).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "Decode").ToArray();
            Assert.That(dispatches.Any(method =>
                    method.ReturnType == typeof(DecodeStatus) &&
                    method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(
                        new[] { typeof(string), typeof(ProfileSaveEnvelope).MakeByRefType(), typeof(int).MakeByRefType() })),
                Is.True, "The actual profile dispatch must expose the authenticated source content version.");
            Assert.That(dispatches.Any(method =>
                    method.ReturnType == typeof(DecodeStatus) &&
                    method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(
                        new[] { typeof(string), typeof(SettingsSaveEnvelope).MakeByRefType() })),
                Is.True, "The actual settings dispatch remains part of the reader boundary.");
        }

        private static void AssertReaderSignature(MethodInfo method, Type frozenType)
        {
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(DecodeStatus)));
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(frozenType.MakeByRefType()));
            Assert.That(parameters[1].IsOut, Is.True);
        }

        private static void AssertPreimageSignature(MethodInfo method, Type frozenType)
        {
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(byte[])));
            Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { frozenType }));
        }

        private static void AssertFrozenTypeClosure(Type anchor, Dictionary<string, string> expectedShapes)
        {
            HashSet<string> frozenNames = new HashSet<string>(expectedShapes.Keys, StringComparer.Ordinal);
            Assembly assembly = anchor.Assembly;
            foreach (KeyValuePair<string, string> expected in expectedShapes)
            {
                Type type = assembly.GetType("Luckbound.App.Compatibility." + expected.Key, throwOnError: false);
                Assert.That(type, Is.Not.Null, "Missing frozen DTO: " + expected.Key);
                Assert.That(type.IsSealed, Is.True, expected.Key + " remains sealed.");
                Assert.That(Attribute.IsDefined(type, typeof(SerializableAttribute)), Is.True,
                    expected.Key + " remains serializable.");

                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                string actualShape = string.Join("|", fields.Select(field => field.Name + ":" + FieldTypeTag(field.FieldType)));
                Assert.That(actualShape, Is.EqualTo(expected.Value), "Ordered wire shape changed: " + expected.Key);

                object instance = Activator.CreateInstance(type, nonPublic: true);
                foreach (FieldInfo field in fields)
                {
                    Type valueType = field.FieldType;
                    Type elementType = valueType.IsArray ? valueType.GetElementType() : valueType;
                    if (valueType.IsArray)
                    {
                        Array values = (Array)field.GetValue(instance);
                        Assert.That(values, Is.Not.Null, expected.Key + "." + field.Name + " default array");
                        Assert.That(values.Length, Is.EqualTo(0), expected.Key + "." + field.Name + " default array");
                    }
                    if (!elementType.IsPrimitive && elementType != typeof(string))
                        Assert.That(frozenNames.Contains(elementType.Name), Is.True,
                            "The frozen graph escaped through " + expected.Key + "." + field.Name + " to " + elementType.FullName);
                }
            }
        }

        private static void AssertChecksumValidProfileIsRejected(LegacyProfileSaveEnvelopeV6 source)
        {
            string json = Rechecksum(source);
            Assert.That(SaveV6ChecksumInspector.InspectProfile(json).Status,
                Is.EqualTo(SaveV6ChecksumStatus.ChecksumValid), "Synthetic input must isolate semantic rejection.");
            LegacyProfileSaveEnvelopeV6 verified;
            Assert.That(SaveV6HistoricalReader.ReadProfile(json, out verified), Is.EqualTo(DecodeStatus.Corrupted));
            Assert.That(verified, Is.Null);
            ProfileSaveEnvelope dispatched;
            Assert.That(SaveJsonCodec.Decode(json, out dispatched), Is.EqualTo(DecodeStatus.Corrupted));
            Assert.That(dispatched, Is.Null);

            const string corruptBackup = "{";
            var store = new ImmediateMemorySaveStore(json, corruptBackup);
            var repository = new SaveRepository(store);
            Task<SaveLoadResult<ProfileSaveEnvelope>> loadTask = repository.LoadProfileAsync();
            Assert.That(loadTask.IsCompleted && !loadTask.IsFaulted && !loadTask.IsCanceled, Is.True,
                "The in-memory repository read must complete before consuming its result.");
            SaveLoadResult<ProfileSaveEnvelope> loaded = loadTask.GetAwaiter().GetResult();

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(loaded.Data, Is.Null);
            Assert.That(loaded.HasUsableData, Is.False);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(repository.CurrentProfile, Is.Null);
            Assert.That(repository.ExportLoadedProfileJson(), Is.Null);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(repository.ExportPendingJson(SaveDocument.Settings), Is.Null);
            Assert.That(repository.PendingSettingsForRetry, Is.Null);
            CollectionAssert.AreEqual(new[] { SaveDocument.Profile }, store.ReadCalls);
            Assert.That(store.WriteCount, Is.EqualTo(0));
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(json));
            Assert.That(store.ProfileBackupJson, Is.EqualTo(corruptBackup));
        }

        private static string Rechecksum(LegacyProfileSaveEnvelopeV6 source)
        {
            source.checksum = ComputeSha256(SaveV6HistoricalReader.GetProfileChecksumPreimage(source));
            return JsonUtility.ToJson(source, false);
        }

        private static LegacyProfileSaveEnvelopeV6 ParseProfile(string fixtureName) =>
            JsonUtility.FromJson<LegacyProfileSaveEnvelopeV6>(ReadFixture(fixtureName));

        private static string ReadFixture(string name) => StrictUtf8.GetString(ReadFixtureBytes(name));

        private static byte[] ReadV5FixtureBytes(string relativePath) =>
            File.ReadAllBytes(Path.Combine(Application.dataPath, V5FixtureDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static string ResolveV5EnvelopePath(string captureRoot, string relativeEnvelopePath)
        {
            if (Path.IsPathRooted(relativeEnvelopePath))
                throw new InvalidDataException("V5 fixture paths must be relative.");
            string fixtureBase = Path.GetFullPath(Path.Combine(Application.dataPath,
                "Luckbound/Tests/Fixtures/Saves/V5", captureRoot));
            string path = Path.GetFullPath(Path.Combine(fixtureBase,
                relativeEnvelopePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = fixtureBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("V5 fixture path escapes its frozen capture root.");
            return path;
        }

        private static byte[] ReadFixtureBytes(string name)
        {
            string path = Path.Combine(Application.dataPath, FixtureDirectory, name);
            return File.ReadAllBytes(path);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class V5ReaderFixtureManifest
        {
            public int schemaVersion;
            public string captureRoot;
            public string nativeIndexSha256;
            public string authoritySha256;
            public string authorityPath;
            public V5ReaderFixtureRow[] rows;
        }

        [Serializable]
        private sealed class V5ReaderFixtureRow
        {
            public string caseId;
            public string culture;
            public string document;
            public string expectedDecode;
            public string envelopePath;
            public string envelopeSha256;
            public int sourceContentVersion;
        }

        private static string FieldTypeTag(Type type)
        {
            if (type.IsArray) return FieldTypeTag(type.GetElementType()) + "[]";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(float)) return "float";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            return type.Name;
        }

        private static void AssertDefaults(Type type, Dictionary<string, object> overrides)
        {
            object instance = Activator.CreateInstance(type, nonPublic: true);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                object expected;
                if (overrides.TryGetValue(field.Name, out expected))
                {
                    Assert.That(field.GetValue(instance), Is.EqualTo(expected), type.Name + "." + field.Name + " default");
                }
                else if (field.FieldType.IsArray)
                {
                    Array values = (Array)field.GetValue(instance);
                    Assert.That(values, Is.Not.Null, type.Name + "." + field.Name + " default array");
                    Assert.That(values.Length, Is.EqualTo(0), type.Name + "." + field.Name + " default array");
                }
                else if (!field.FieldType.IsValueType)
                {
                    Assert.That(field.GetValue(instance), Is.Null, type.Name + "." + field.Name + " default");
                }
                else
                {
                    Assert.That(field.GetValue(instance), Is.EqualTo(Activator.CreateInstance(field.FieldType)),
                        type.Name + "." + field.Name + " default");
                }
            }
        }

        private static Dictionary<string, string> CreateExpectedFrozenShapes() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "LegacyProfileSaveEnvelopeV6", "schemaVersion:int|contentVersion:int|generation:int|sequence:long|savedAtUtc:string|checksum:string|profile:LegacyProfileSaveDataV6|hasActiveExpedition:bool|activeExpedition:LegacyExpeditionSaveDataV6" },
            { "LegacySettingsSaveEnvelopeV6", "schemaVersion:int|contentVersion:int|generation:int|sequence:long|savedAtUtc:string|checksum:string|settings:LegacyGameSettingsSaveDataV6" },
            { "LegacyProfileSaveDataV6", "availableRenown:int|healthUpgradeLevel:int|energyUpgradeLevel:int|additionalPotionUpgradeLevel:int|unlockedRaceIds:string[]|unlockedStartingKitIds:string[]|acceptedContractIds:string[]|claimedContractIds:string[]|readStoryFragmentIds:string[]|contractCounters:LegacyContractCounterSaveDataV6[]|processedSettlementRunIds:string[]|processedContractProgressEvents:LegacyContractProgressEventReceiptSaveDataV6[]|progressionPurchaseTransactions:LegacyPermanentUpgradePurchaseSaveDataV6[]" },
            { "LegacyContractCounterSaveDataV6", "contractId:string|progress:int" },
            { "LegacyContractProgressEventReceiptSaveDataV6", "eventId:string|progressEvent:int" },
            { "LegacyPermanentUpgradePurchaseSaveDataV6", "transactionId:string|kind:int|cost:int" },
            { "LegacyExpeditionSaveDataV6", "runId:string|seed:uint|generationVersion:int|contentVersion:int|classId:string|raceId:string|selectedSkillIds:string[]|startingEquipmentInstanceIds:string[]|routeSnapshotJson:string|currentNodeId:string|visitedNodeIds:string[]|completedRoomIds:string[]|completedRoomResults:LegacyExpeditionRoomCompletionSaveDataV6[]|roomEntryCheckpointJson:string|inventorySnapshotJson:string|maximumHealth:int|currentHealth:int|maximumEnergy:int|currentEnergy:int|healthPotionCharges:int|frozenPermanentUpgrades:LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6|hasFrozenPermanentUpgrades:bool|accumulatedRenown:int|pactIds:string[]|fortuneOfferBookSnapshot:LegacyFortuneOfferBookSnapshotV6|collectedItemIds:string[]|claimedRewardIds:string[]" },
            { "LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6", "healthUpgradeLevel:int|energyUpgradeLevel:int|additionalPotionUpgradeLevel:int" },
            { "LegacyExpeditionRoomCheckpointSaveDataV6", "roomNodeId:string|inventorySnapshotJson:string|maximumHealth:int|currentHealth:int|maximumEnergy:int|currentEnergy:int|healthPotionCharges:int|accumulatedRenown:int|fortuneOfferBookSnapshot:LegacyFortuneOfferBookSnapshotV6|pactIds:string[]|collectedItemIds:string[]|claimedRewardIds:string[]" },
            { "LegacyExpeditionRoomCompletionSaveDataV6", "roomNodeId:string|resultId:string|resultJson:string" },
            { "LegacyFortuneOfferBookSnapshotV6", "snapshotVersion:int|generationVersion:int|seed:uint|raceId:string|charges:int|offers:LegacyFortuneOfferSnapshotV6[]|processedGuardianIds:string[]|rerollReceipts:LegacyFortuneRerollReceiptSnapshotV6[]|fortuneBrewReceipts:LegacyFortuneBrewReceiptSnapshotV6[]" },
            { "LegacyFortuneOfferSnapshotV6", "offerId:string|contextId:string|eligibleOptionIds:string[]|history:LegacyFortuneOfferHistorySnapshotV6[]|selectedOptionId:string|claimed:bool" },
            { "LegacyFortuneOfferHistorySnapshotV6", "optionIds:string[]" },
            { "LegacyFortuneRerollReceiptSnapshotV6", "transactionId:string|offerId:string|status:int|contextId:string|optionIds:string[]|selectedOptionId:string|rerollCount:int|claimed:bool|remainingCharges:int|hasOffer:bool" },
            { "LegacyFortuneBrewReceiptSnapshotV6", "transactionId:string|itemInstanceId:string|status:int|currentCharges:int" },
            { "LegacyGameSettingsSaveDataV6", "languageId:string|masterVolume:float|musicVolume:float|effectsVolume:float|ambienceVolume:float" }
        };

        private static Dictionary<string, string> CreateExpectedFrozenV5Shapes() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "LegacyProfileSaveEnvelopeV5", "schemaVersion:int|contentVersion:int|generation:int|sequence:long|savedAtUtc:string|checksum:string|profile:LegacyProfileSaveDataV5|hasActiveExpedition:bool|activeExpedition:LegacyExpeditionSaveDataV5" },
            { "LegacySettingsSaveEnvelopeV5", "schemaVersion:int|contentVersion:int|generation:int|sequence:long|savedAtUtc:string|checksum:string|settings:LegacyGameSettingsSaveDataV5" },
            { "LegacyProfileSaveDataV5", "availableRenown:int|healthUpgradeLevel:int|energyUpgradeLevel:int|additionalPotionUpgradeLevel:int|unlockedRaceIds:string[]|unlockedStartingKitIds:string[]|acceptedContractIds:string[]|claimedContractIds:string[]|readStoryFragmentIds:string[]|contractCounters:LegacyContractCounterSaveDataV5[]|processedSettlementRunIds:string[]|progressionPurchaseTransactions:LegacyPermanentUpgradePurchaseSaveDataV5[]" },
            { "LegacyContractCounterSaveDataV5", "contractId:string|progress:int" },
            { "LegacyPermanentUpgradePurchaseSaveDataV5", "transactionId:string|kind:int|cost:int" },
            { "LegacyExpeditionSaveDataV5", "runId:string|seed:uint|generationVersion:int|contentVersion:int|classId:string|raceId:string|selectedSkillIds:string[]|startingEquipmentInstanceIds:string[]|routeSnapshotJson:string|currentNodeId:string|visitedNodeIds:string[]|completedRoomIds:string[]|completedRoomResults:LegacyExpeditionRoomCompletionSaveDataV5[]|roomEntryCheckpointJson:string|inventorySnapshotJson:string|maximumHealth:int|currentHealth:int|maximumEnergy:int|currentEnergy:int|healthPotionCharges:int|frozenPermanentUpgrades:LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5|hasFrozenPermanentUpgrades:bool|accumulatedRenown:int|pactIds:string[]|fortuneOfferBookSnapshot:LegacyFortuneOfferBookSnapshotV5|collectedItemIds:string[]|claimedRewardIds:string[]" },
            { "LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5", "healthUpgradeLevel:int|energyUpgradeLevel:int|additionalPotionUpgradeLevel:int" },
            { "LegacyExpeditionRoomCheckpointSaveDataV5", "roomNodeId:string|inventorySnapshotJson:string|maximumHealth:int|currentHealth:int|maximumEnergy:int|currentEnergy:int|healthPotionCharges:int|accumulatedRenown:int|fortuneOfferBookSnapshot:LegacyFortuneOfferBookSnapshotV5|pactIds:string[]|collectedItemIds:string[]|claimedRewardIds:string[]" },
            { "LegacyExpeditionRoomCompletionSaveDataV5", "roomNodeId:string|resultId:string|resultJson:string" },
            { "LegacyFortuneOfferBookSnapshotV5", "snapshotVersion:int|generationVersion:int|seed:uint|raceId:string|charges:int|offers:LegacyFortuneOfferSnapshotV5[]|processedGuardianIds:string[]|rerollReceipts:LegacyFortuneRerollReceiptSnapshotV5[]|fortuneBrewReceipts:LegacyFortuneBrewReceiptSnapshotV5[]" },
            { "LegacyFortuneOfferSnapshotV5", "offerId:string|contextId:string|eligibleOptionIds:string[]|history:LegacyFortuneOfferHistorySnapshotV5[]|selectedOptionId:string|claimed:bool" },
            { "LegacyFortuneOfferHistorySnapshotV5", "optionIds:string[]" },
            { "LegacyFortuneRerollReceiptSnapshotV5", "transactionId:string|offerId:string|status:int|contextId:string|optionIds:string[]|selectedOptionId:string|rerollCount:int|claimed:bool|remainingCharges:int|hasOffer:bool" },
            { "LegacyFortuneBrewReceiptSnapshotV5", "transactionId:string|itemInstanceId:string|status:int|currentCharges:int" },
            { "LegacyGameSettingsSaveDataV5", "languageId:string|masterVolume:float|musicVolume:float|effectsVolume:float|ambienceVolume:float" }
        };

        private sealed class ImmediateMemorySaveStore : ISaveStore
        {
            public ImmediateMemorySaveStore(string profilePrimaryJson, string profileBackupJson)
            {
                ProfilePrimaryJson = profilePrimaryJson;
                ProfileBackupJson = profileBackupJson;
            }

            public string ProfilePrimaryJson { get; }
            public string ProfileBackupJson { get; }
            public List<SaveDocument> ReadCalls { get; } = new List<SaveDocument>();
            public int WriteCount { get; private set; }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                ReadCalls.Add(document);
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(ProfilePrimaryJson, ProfileBackupJson)
                    : new SaveStorageSnapshot(null, null));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                WriteCount++;
                return Task.CompletedTask;
            }
        }
    }
}
