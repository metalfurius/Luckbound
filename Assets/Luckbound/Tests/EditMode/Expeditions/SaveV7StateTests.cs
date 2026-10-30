using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Expeditions
{
    public sealed class SaveV7StateTests
    {
        private const uint Seed = 0x31415926u;
        private const string RunIdText = "0123456789abcdef0123456789abcdef";
        private const string CurrentNodeId = "sample.tutorial";
        private const string PlanSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Test]
        public void TypedCloneDeepCopiesNestedDtosAndPreservesValuesOrderAndNulls()
        {
            ProfileSaveEnvelopeV7 source = CreateValidProfile();
            source.profile.unlockedRaceIds = new[] { "race.z", null, "race.a" };
            source.profile.contractCounters = new[]
            {
                new ContractCounterSaveDataV7 { contractId = "contract.z", progress = -2 },
                null
            };
            source.profile.processedContractProgressEvents = new[]
            {
                new ContractProgressEventReceiptSaveDataV7 { eventId = "event.z", progressEvent = -1 }
            };
            source.profile.progressionPurchaseTransactions = new[]
            {
                new PermanentUpgradePurchaseSaveDataV7 { transactionId = "transaction.raw", kind = -1, cost = -7 }
            };
            source.activeExpedition.completedRoomResults = new[]
            {
                new ExpeditionRoomCompletionSaveDataV7 { roomNodeId = "node.z", resultId = "result.z", resultJson = "opaque-z" },
                null
            };
            source.activeExpedition.frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveDataV7
            {
                healthUpgradeLevel = 9,
                energyUpgradeLevel = -3,
                additionalPotionUpgradeLevel = 4
            };
            source.activeExpedition.hasFrozenPermanentUpgrades = false;
            source.activeExpedition.fortuneOfferBookSnapshot.offers = new[]
            {
                new FortuneOfferSnapshotV7
                {
                    offerId = "offer.z",
                    contextId = "context.z",
                    eligibleOptionIds = new[] { "option.z", "option.a" },
                    history = new[] { new FortuneOfferHistorySnapshotV7 { optionIds = new[] { "history.z" } } },
                    selectedOptionId = "option.z",
                    claimed = true
                },
                null
            };
            source.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts = new[]
            {
                new FortuneRerollReceiptSnapshotV7
                {
                    transactionId = "reroll.raw",
                    offerId = "offer.z",
                    status = -1,
                    contextId = "context.z",
                    optionIds = new[] { "second", "first" },
                    selectedOptionId = "second",
                    rerollCount = -1,
                    claimed = true,
                    remainingCharges = -1,
                    hasOffer = true
                }
            };
            source.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts = new[]
            {
                new FortuneBrewReceiptSnapshotV7
                {
                    transactionId = "brew.raw",
                    itemInstanceId = "item.raw",
                    status = -1,
                    currentCharges = -2
                }
            };
            source.activeExpedition.encounterPlanPin.bindings[0].mode = EncounterCatalogBindingMode.None;
            source.activeExpedition.encounterPlanPin.bindings[0].encounterId = "opaque-none-payload";
            source.activeExpedition.encounterPlanPin.bindings[0].contextPactIds = new[] { "pact.z", "pact.a" };
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(source);

            ProfileSaveEnvelopeV7 clone = SaveV7Clone.Clone(source);
            ExpeditionRoomCheckpointSaveDataV7 checkpointClone = SaveV7Clone.Clone(checkpoint);

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.profile, Is.Not.SameAs(source.profile));
            Assert.That(clone.profile.unlockedRaceIds, Is.Not.SameAs(source.profile.unlockedRaceIds));
            CollectionAssert.AreEqual(new[] { "race.z", null, "race.a" }, clone.profile.unlockedRaceIds);
            Assert.That(clone.profile.contractCounters, Is.Not.SameAs(source.profile.contractCounters));
            Assert.That(clone.profile.contractCounters[0], Is.Not.SameAs(source.profile.contractCounters[0]));
            Assert.That(clone.profile.contractCounters[1], Is.Null);
            Assert.That(clone.profile.contractCounters[0].progress, Is.EqualTo(-2));
            Assert.That(clone.activeExpedition.completedRoomResults, Is.Not.SameAs(source.activeExpedition.completedRoomResults));
            Assert.That(clone.activeExpedition.completedRoomResults[0], Is.Not.SameAs(source.activeExpedition.completedRoomResults[0]));
            Assert.That(clone.activeExpedition.completedRoomResults[1], Is.Null);
            Assert.That(clone.activeExpedition.frozenPermanentUpgrades.healthUpgradeLevel, Is.EqualTo(9));
            Assert.That(clone.activeExpedition.hasFrozenPermanentUpgrades, Is.False);
            Assert.That(clone.activeExpedition.fortuneOfferBookSnapshot.offers, Is.Not.SameAs(source.activeExpedition.fortuneOfferBookSnapshot.offers));
            Assert.That(clone.activeExpedition.fortuneOfferBookSnapshot.offers[0], Is.Not.SameAs(source.activeExpedition.fortuneOfferBookSnapshot.offers[0]));
            Assert.That(clone.activeExpedition.fortuneOfferBookSnapshot.offers[0].history[0],
                Is.Not.SameAs(source.activeExpedition.fortuneOfferBookSnapshot.offers[0].history[0]));
            Assert.That(clone.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts[0],
                Is.Not.SameAs(source.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts[0]));
            Assert.That(clone.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0],
                Is.Not.SameAs(source.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts[0]));
            Assert.That(clone.activeExpedition.encounterPlanPin.bindings[0],
                Is.Not.SameAs(source.activeExpedition.encounterPlanPin.bindings[0]));
            Assert.That(clone.activeExpedition.encounterPlanPin.entry,
                Is.Not.SameAs(source.activeExpedition.encounterPlanPin.entry));
            Assert.That(clone.activeExpedition.encounterPlanPin.bindings[0].encounterId,
                Is.EqualTo("opaque-none-payload"));
            Assert.That(clone.activeExpedition.encounterPlanPin.bindings[0].contextPactIds,
                Is.EqualTo(new[] { "pact.z", "pact.a" }));
            Assert.That(checkpointClone, Is.Not.SameAs(checkpoint));
            Assert.That(checkpointClone.encounterEntry, Is.Not.SameAs(checkpoint.encounterEntry));

            Assert.That(SaveV7Clone.Clone((ProfileSaveEnvelopeV7)null), Is.Null);
            Assert.That(SaveV7Clone.Clone((SettingsSaveEnvelopeV7)null), Is.Null);
            Assert.That(SaveV7Clone.Clone((ExpeditionSaveDataV7)null), Is.Null);
            Assert.That(SaveV7Clone.Clone((ExpeditionRoomCheckpointSaveDataV7)null), Is.Null);
        }

        [Test]
        public void TypedClonePreservesNullArraysWithoutFillingDefaults()
        {
            var profile = new ProfileSaveDataV7
            {
                unlockedRaceIds = null,
                contractCounters = null,
                processedContractProgressEvents = null,
                progressionPurchaseTransactions = null
            };
            var expedition = new ExpeditionSaveDataV7
            {
                selectedSkillIds = null,
                visitedNodeIds = null,
                completedRoomResults = null,
                pactIds = null,
                collectedItemIds = null,
                claimedRewardIds = null
            };
            var pin = new EncounterPlanPinSaveDataV7 { bindings = null };
            var binding = new EncounterPlanNodeBindingSaveDataV7 { contextPactIds = null };
            var checkpoint = new ExpeditionRoomCheckpointSaveDataV7
            {
                pactIds = null,
                collectedItemIds = null,
                claimedRewardIds = null
            };
            var book = new FortuneOfferBookSnapshotV7
            {
                offers = null,
                processedGuardianIds = null,
                rerollReceipts = null,
                fortuneBrewReceipts = null
            };

            Assert.That(SaveV7Clone.Clone(profile).unlockedRaceIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(profile).contractCounters, Is.Null);
            Assert.That(SaveV7Clone.Clone(expedition).selectedSkillIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(expedition).visitedNodeIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(expedition).completedRoomResults, Is.Null);
            Assert.That(SaveV7Clone.Clone(pin).bindings, Is.Null);
            Assert.That(SaveV7Clone.Clone(binding).contextPactIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(checkpoint).claimedRewardIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(book).offers, Is.Null);
            Assert.That(SaveV7Clone.Clone(book).processedGuardianIds, Is.Null);
            Assert.That(SaveV7Clone.Clone(book).rerollReceipts, Is.Null);
            Assert.That(SaveV7Clone.Clone(book).fortuneBrewReceipts, Is.Null);
        }

        [Test]
        public void ActiveV7RequiresTypedPinAndUnfinishedCheckpointMarker()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error validError), Is.True,
                validError.Message);
            Assert.That(validError.Code, Is.EqualTo(SaveV7ErrorCode.None));
            Assert.That(profile.activeExpedition.encounterPlanPin.bindings[0].mode,
                Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(profile.activeExpedition.encounterPlanPin.bindings[0].encounterId, Is.Null);
            Assert.That(profile.activeExpedition.encounterPlanPin.bindings[0].definitionRevision, Is.Zero);
            Assert.That(profile.activeExpedition.encounterPlanPin.bindings[0].definitionSha256, Is.Null);
            Assert.That(profile.activeExpedition.encounterPlanPin.bindings[0].variantId, Is.Null);
            Assert.That(profile.activeExpedition.completedRoomIds, Is.Empty);

            profile.activeExpedition.encounterPlanPin = null;
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error missingPin), Is.False);
            Assert.That(missingPin.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));

            profile = CreateValidProfile();
            profile.activeExpedition.roomEntryCheckpointJson = null;
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error missingCheckpoint), Is.False);
            Assert.That(missingCheckpoint.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData));
        }

        [Test]
        public void RouteMutationCannotPassThePinnedCanonicalRouteDigest()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.routeSnapshotJson = profile.activeExpedition.routeSnapshotJson + "\n";

            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
            Assert.That(error.Path, Is.EqualTo("activeExpedition.routeSnapshotJson"));
            Assert.That(error.Path, Is.Not.Null);
            Assert.That(error.Message, Is.Not.Null);
        }

        [Test]
        public void CheckpointRequiresExactCurrentNodeAndPinnedPlanReference()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(profile);
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error error), Is.True,
                error.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(profile, checkpoint, out error), Is.True,
                error.Message);

            checkpoint.encounterEntry.resolvedPlanSha256 = new string('c', 64);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(profile, checkpoint, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));

            checkpoint = CreateCheckpoint(profile);
            checkpoint.roomNodeId = "sample.patio.combat";
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(profile, checkpoint, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
        }

        [TestCase("seed")]
        [TestCase("generation")]
        [TestCase("race")]
        [TestCase("pacts")]
        public void CheckpointBindingRejectsFortuneIdentityOrPactContextMismatchWithoutChangingRoomReference(
            string mismatch)
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.encounterPlanPin.bindings[0].contextPactIds =
                new[] { "pact.ember", "pact.fervor" };
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(profile);
            checkpoint.pactIds = new[] { "pact.ember", "pact.fervor" };
            switch (mismatch)
            {
                case "seed": checkpoint.fortuneOfferBookSnapshot.seed++; break;
                case "generation": checkpoint.fortuneOfferBookSnapshot.generationVersion++; break;
                case "race": checkpoint.fortuneOfferBookSnapshot.raceId = "race.dwarf"; break;
                case "pacts": checkpoint.pactIds = new[] { "pact.ember", "pact.haste" }; break;
                default: Assert.Fail("Unknown mismatch fixture."); break;
            }

            string roomNodeId = checkpoint.roomNodeId;
            string entryNodeId = checkpoint.encounterEntry.nodeId;
            string entryPlanSha256 = checkpoint.encounterEntry.resolvedPlanSha256;
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error profileError), Is.True,
                profileError.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error checkpointError), Is.True,
                checkpointError.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(profile, checkpoint, out SaveV7Error error),
                Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
            Assert.That(checkpoint.roomNodeId, Is.EqualTo(roomNodeId));
            Assert.That(checkpoint.encounterEntry.nodeId, Is.EqualTo(entryNodeId));
            Assert.That(checkpoint.encounterEntry.resolvedPlanSha256, Is.EqualTo(entryPlanSha256));
        }

        [Test]
        public void CheckpointBindingComparesPactsAsAnOrdinalSetAndAllowsZeroEnergy()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.encounterPlanPin.bindings[0].contextPactIds =
                new[] { "pact.ember", "pact.fervor" };
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(profile);
            checkpoint.pactIds = new[] { "pact.fervor", "pact.ember" };
            checkpoint.maximumEnergy = 0;
            checkpoint.currentEnergy = 0;

            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error profileError), Is.True,
                profileError.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error checkpointError), Is.True,
                checkpointError.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(profile, checkpoint, out SaveV7Error error),
                Is.True, error.Message);
        }

        [Test]
        public void CheckpointRejectsNegativeOrOverMaximumEnergyAndActiveRunStillRequiresPositiveMaximum()
        {
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(CreateValidProfile());
            checkpoint.maximumEnergy = -1;
            checkpoint.currentEnergy = 0;
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error error), Is.False);

            checkpoint = CreateCheckpoint(CreateValidProfile());
            checkpoint.maximumEnergy = 0;
            checkpoint.currentEnergy = -1;
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out error), Is.False);

            checkpoint = CreateCheckpoint(CreateValidProfile());
            checkpoint.maximumEnergy = 0;
            checkpoint.currentEnergy = 1;
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out error), Is.False);

            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.maximumEnergy = 0;
            profile.activeExpedition.currentEnergy = 0;
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out error), Is.False);
        }

        [Test]
        public void CoreFortuneRestoreAcceptsNontrivialProfileAndCheckpointBooksWithoutMutatingDtos()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(profile);
            FortuneOfferBookSnapshotV7 profileBefore = SaveV7Clone.Clone(
                profile.activeExpedition.fortuneOfferBookSnapshot);
            FortuneOfferBookSnapshotV7 checkpointBefore = SaveV7Clone.Clone(
                checkpoint.fortuneOfferBookSnapshot);

            Assert.That(profile.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts, Has.Length.EqualTo(3));
            Assert.That(profile.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts,
                Has.Length.EqualTo(1));
            Assert.That(profile.activeExpedition.fortuneOfferBookSnapshot.offers[0].history,
                Has.Length.EqualTo(2));
            Assert.That(profile.activeExpedition.fortuneOfferBookSnapshot.offers[0].claimed, Is.True);
            AssertUnknownOfferReceipts(profile.activeExpedition.fortuneOfferBookSnapshot);
            AssertUnknownOfferReceipts(checkpoint.fortuneOfferBookSnapshot);
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error profileError), Is.True,
                profileError.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error checkpointError), Is.True,
                checkpointError.Message);
            AssertFortuneBookEqual(profile.activeExpedition.fortuneOfferBookSnapshot, profileBefore);
            AssertFortuneBookEqual(checkpoint.fortuneOfferBookSnapshot, checkpointBefore);
        }

        [TestCase("future-version")]
        [TestCase("charges")]
        [TestCase("status")]
        [TestCase("duplicate-offer")]
        [TestCase("duplicate-transaction")]
        [TestCase("invalid-selection")]
        [TestCase("invalid-history")]
        [TestCase("invalid-brew-id")]
        [TestCase("unknown-status")]
        [TestCase("unknown-options")]
        public void CoreFortuneRestoreRejectsInvalidProfileAndCheckpointBooksWithoutMutatingDtos(string corruption)
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            ApplyFortuneCorruption(profile.activeExpedition.fortuneOfferBookSnapshot, corruption);
            FortuneOfferBookSnapshotV7 profileBefore = SaveV7Clone.Clone(
                profile.activeExpedition.fortuneOfferBookSnapshot);
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error profileError), Is.False,
                corruption);
            Assert.That(profileError.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData), corruption);
            Assert.That(profileError.Path, Is.Not.Null, corruption);
            Assert.That(profileError.Message, Is.Not.Null, corruption);
            AssertFortuneBookEqual(profile.activeExpedition.fortuneOfferBookSnapshot, profileBefore);

            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(CreateValidProfile());
            ApplyFortuneCorruption(checkpoint.fortuneOfferBookSnapshot, corruption);
            FortuneOfferBookSnapshotV7 checkpointBefore = SaveV7Clone.Clone(checkpoint.fortuneOfferBookSnapshot);
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error checkpointError), Is.False,
                corruption);
            Assert.That(checkpointError.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData), corruption);
            Assert.That(checkpointError.Path, Is.Not.Null, corruption);
            Assert.That(checkpointError.Message, Is.Not.Null, corruption);
            AssertFortuneBookEqual(checkpoint.fortuneOfferBookSnapshot, checkpointBefore);
        }

        [Test]
        public void CheckpointBindingAllowsEmptyCheckpointForInactiveOrCompletedCurrentState()
        {
            var inactive = new ProfileSaveEnvelopeV7
            {
                schemaVersion = 1,
                contentVersion = 7,
                generation = 1,
                sequence = 2,
                savedAtUtc = "2026-10-30T12:00:00.0000000Z",
                checksum = PlanSha256,
                profile = new ProfileSaveDataV7(),
                hasActiveExpedition = false,
                activeExpedition = null
            };
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(inactive, null, out SaveV7Error error),
                Is.True, error.Message);

            ProfileSaveEnvelopeV7 completed = CreateValidProfile();
            completed.activeExpedition.completedRoomIds = new[] { CurrentNodeId };
            completed.activeExpedition.completedRoomResults = new[]
            {
                new ExpeditionRoomCompletionSaveDataV7
                {
                    roomNodeId = CurrentNodeId,
                    resultId = "result.tutorial.completed",
                    resultJson = "opaque completed result"
                }
            };
            completed.activeExpedition.roomEntryCheckpointJson = null;
            Assert.That(SaveV7Validation.TryValidateProfile(completed, out error), Is.True, error.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(completed, null, out error),
                Is.True, error.Message);
            Assert.That(SaveV7Validation.TryValidateCheckpointBinding(completed,
                CreateCheckpoint(completed), out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
        }

        [Test]
        public void PinEntryMustNameTheCurrentRouteCursor()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.encounterPlanPin.entry.nodeId = "sample.patio.combat";

            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
            Assert.That(error.Path, Is.EqualTo("activeExpedition.encounterPlanPin.entry.nodeId"));
        }

        [Test]
        public void NullPinAndCheckpointArraysAreRejectedAndErrorsHaveNonNullText()
        {
            ProfileSaveEnvelopeV7 profile = CreateValidProfile();
            profile.activeExpedition.encounterPlanPin.bindings = null;
            Assert.That(SaveV7Validation.TryValidateProfile(profile, out SaveV7Error pinError), Is.False);
            Assert.That(pinError.Code, Is.EqualTo(SaveV7ErrorCode.PlanMismatch));
            Assert.That(pinError.Path, Is.Not.Null);
            Assert.That(pinError.Message, Is.Not.Null);

            ExpeditionRoomCheckpointSaveDataV7 checkpoint = CreateCheckpoint(CreateValidProfile());
            checkpoint.pactIds = null;
            Assert.That(SaveV7Validation.TryValidateCheckpoint(checkpoint, out SaveV7Error checkpointError), Is.False);
            Assert.That(checkpointError.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData));
            Assert.That(checkpointError.Path, Is.Not.Null);
            Assert.That(checkpointError.Message, Is.Not.Null);
        }

        [Test]
        public void SettingsV7UsesFrozenFieldsAndDefaultsWithoutChangingLiveSaveFormat()
        {
            var settings = new SettingsSaveEnvelopeV7
            {
                schemaVersion = 1,
                contentVersion = 7,
                generation = 1,
                sequence = 3,
                savedAtUtc = "2026-10-30T12:00:00.0000000Z",
                checksum = PlanSha256,
                settings = new GameSettingsSaveDataV7()
            };

            Assert.That(settings.settings.languageId, Is.EqualTo("es"));
            Assert.That(settings.settings.masterVolume, Is.EqualTo(1f));
            Assert.That(settings.settings.musicVolume, Is.EqualTo(0.65f));
            Assert.That(settings.settings.effectsVolume, Is.EqualTo(0.8f));
            Assert.That(settings.settings.ambienceVolume, Is.EqualTo(0.45f));
            Assert.That(SaveFormat.ContentVersion, Is.EqualTo(6));
            Assert.That(settings.contentVersion, Is.EqualTo(7));
            Assert.That(SaveV7Validation.TryValidateSettings(settings, out SaveV7Error error), Is.True,
                error.Message);
            Assert.That(SaveV7Clone.Clone(settings).settings, Is.Not.SameAs(settings.settings));
        }

        private static ProfileSaveEnvelopeV7 CreateValidProfile()
        {
            ExpeditionRoutePlan route = CreateRoute(Seed);
            string routeSha256 = ComputeSha256(route.CanonicalForm);
            RunId runId = RunId.Parse(RunIdText);
            ExpeditionRouteNode currentNode = route.Nodes.First(node =>
                string.Equals(node.NodeId, CurrentNodeId, StringComparison.Ordinal));
            var binding = new EncounterPlanNodeBindingSaveDataV7
            {
                nodeId = CurrentNodeId,
                generationVersion = route.GenerationVersion,
                templateId = currentNode.TemplateId,
                roomInstanceId = ExpeditionRoomIdentity.ForNode(runId, CurrentNodeId).ToString(),
                mode = EncounterCatalogBindingMode.None,
                encounterId = null,
                definitionRevision = 0,
                definitionSha256 = null,
                variantId = null,
                contextPactIds = Array.Empty<string>(),
                calendarOrdinal = 0,
                resolvedPlanSha256 = PlanSha256
            };
            var pin = new EncounterPlanPinSaveDataV7
            {
                schemaVersion = EncounterPlanPinV1.SupportedSchemaVersion,
                runId = RunIdText,
                catalogId = "catalog.save-v7.synthetic",
                catalogRevision = 1,
                catalogSha256 = new string('b', 64),
                routeSha256 = routeSha256,
                identityCodec = EncounterPlanPinV1.SupportedIdentityCodec,
                bindings = new[] { binding },
                entry = new EncounterPlanEntryReferenceSaveDataV7
                {
                    nodeId = CurrentNodeId,
                    resolvedPlanSha256 = PlanSha256
                }
            };
            var expedition = new ExpeditionSaveDataV7
            {
                runId = RunIdText,
                seed = Seed,
                generationVersion = route.GenerationVersion,
                contentVersion = 7,
                classId = "class.bellator",
                raceId = ExpeditionRunConfiguration.HumanRaceId,
                selectedSkillIds = new[] { "skill.fixture.one", "skill.fixture.two" },
                startingEquipmentInstanceIds = new[]
                {
                    "00000000000000000000000000000001",
                    "00000000000000000000000000000002"
                },
                routeSnapshotJson = route.CanonicalForm,
                currentNodeId = CurrentNodeId,
                visitedNodeIds = new[] { CurrentNodeId },
                completedRoomIds = Array.Empty<string>(),
                completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveDataV7>(),
                roomEntryCheckpointJson = "opaque checkpoint payload",
                inventorySnapshotJson = "opaque inventory payload",
                maximumHealth = 100,
                currentHealth = 100,
                maximumEnergy = 50,
                currentEnergy = 50,
                healthPotionCharges = 2,
                frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveDataV7(),
                hasFrozenPermanentUpgrades = true,
                accumulatedRenown = 0,
                pactIds = Array.Empty<string>(),
                fortuneOfferBookSnapshot = CreateFortuneBook(route.GenerationVersion, Seed,
                    ExpeditionRunConfiguration.HumanRaceId),
                collectedItemIds = Array.Empty<string>(),
                claimedRewardIds = Array.Empty<string>(),
                encounterPlanPin = pin
            };
            return new ProfileSaveEnvelopeV7
            {
                schemaVersion = 1,
                contentVersion = 7,
                generation = 1,
                sequence = 1,
                savedAtUtc = "2026-10-30T12:00:00.0000000Z",
                checksum = PlanSha256,
                profile = new ProfileSaveDataV7(),
                hasActiveExpedition = true,
                activeExpedition = expedition
            };
        }

        private static ExpeditionRoomCheckpointSaveDataV7 CreateCheckpoint(ProfileSaveEnvelopeV7 profile)
        {
            ExpeditionSaveDataV7 expedition = profile.activeExpedition;
            EncounterPlanNodeBindingSaveDataV7 binding = expedition.encounterPlanPin.bindings[0];
            return new ExpeditionRoomCheckpointSaveDataV7
            {
                roomNodeId = expedition.currentNodeId,
                inventorySnapshotJson = "opaque checkpoint inventory",
                maximumHealth = 100,
                currentHealth = 100,
                maximumEnergy = 50,
                currentEnergy = 50,
                healthPotionCharges = 2,
                accumulatedRenown = 0,
                fortuneOfferBookSnapshot = CreateFortuneBook(expedition.generationVersion,
                    expedition.seed, expedition.raceId),
                pactIds = Array.Empty<string>(),
                collectedItemIds = Array.Empty<string>(),
                claimedRewardIds = Array.Empty<string>(),
                encounterEntry = new EncounterPlanEntryReferenceSaveDataV7
                {
                    nodeId = binding.nodeId,
                    resolvedPlanSha256 = binding.resolvedPlanSha256
                }
            };
        }

        private static FortuneOfferBookSnapshotV7 CreateFortuneBook(int generationVersion, uint seed, string raceId)
        {
            var book = new FortuneOfferBook(generationVersion, seed, raceId);
            string[] eligible = { "option.alpha", "option.bravo", "option.charlie", "option.delta", "option.echo" };
            Assert.That(book.CreateOffer("offer.fixture", "context.fixture", eligible).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Reroll("offer.fixture", TransactionId.Parse("00000000000000000000000000000011")).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Choose("offer.fixture", book.GetOffer("offer.fixture").OptionIds[0]).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Claim("offer.fixture").Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.GrantFortuneBrewCharge(ItemInstanceId.Parse("00000000000000000000000000000021"),
                TransactionId.Parse("00000000000000000000000000000031")).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.GrantGuardianCharge("guardian.fixture.1").Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Reroll("offer.unknown", TransactionId.Parse("00000000000000000000000000000012")).Status,
                Is.EqualTo(FortuneOperationStatus.RejectedUnknownOffer));
            Assert.That(book.Reroll(null, TransactionId.Parse("00000000000000000000000000000013")).Status,
                Is.EqualTo(FortuneOperationStatus.RejectedUnknownOffer));
            return ToV7(book.CreateSnapshot());
        }

        private static FortuneOfferBookSnapshotV7 ToV7(FortuneOfferBookSnapshot value)
        {
            return new FortuneOfferBookSnapshotV7
            {
                snapshotVersion = value.snapshotVersion,
                generationVersion = value.generationVersion,
                seed = value.seed,
                raceId = value.raceId,
                charges = value.charges,
                offers = value.offers.Select(offer => new FortuneOfferSnapshotV7
                {
                    offerId = offer.offerId,
                    contextId = offer.contextId,
                    eligibleOptionIds = (string[])offer.eligibleOptionIds.Clone(),
                    history = offer.history.Select(history => new FortuneOfferHistorySnapshotV7
                    {
                        optionIds = (string[])history.optionIds.Clone()
                    }).ToArray(),
                    selectedOptionId = offer.selectedOptionId,
                    claimed = offer.claimed
                }).ToArray(),
                processedGuardianIds = (string[])value.processedGuardianIds.Clone(),
                rerollReceipts = value.rerollReceipts.Select(receipt => new FortuneRerollReceiptSnapshotV7
                {
                    transactionId = receipt.transactionId,
                    offerId = receipt.offerId,
                    status = receipt.status,
                    contextId = receipt.contextId,
                    optionIds = (string[])receipt.optionIds.Clone(),
                    selectedOptionId = receipt.selectedOptionId,
                    rerollCount = receipt.rerollCount,
                    claimed = receipt.claimed,
                    remainingCharges = receipt.remainingCharges,
                    hasOffer = receipt.hasOffer
                }).ToArray(),
                fortuneBrewReceipts = value.fortuneBrewReceipts.Select(receipt => new FortuneBrewReceiptSnapshotV7
                {
                    transactionId = receipt.transactionId,
                    itemInstanceId = receipt.itemInstanceId,
                    status = receipt.status,
                    currentCharges = receipt.currentCharges
                }).ToArray()
            };
        }

        private static void ApplyFortuneCorruption(FortuneOfferBookSnapshotV7 value, string corruption)
        {
            switch (corruption)
            {
                case "future-version": value.snapshotVersion = 999; break;
                case "charges": value.charges = FortuneOfferBook.MaximumCharges + 1; break;
                case "status": value.rerollReceipts[0].status = 999; break;
                case "duplicate-offer":
                    value.offers = value.offers.Concat(new[] { SaveV7Clone.Clone(value.offers[0]) }).ToArray();
                    break;
                case "duplicate-transaction":
                    value.fortuneBrewReceipts[0].transactionId = value.rerollReceipts[0].transactionId;
                    break;
                case "invalid-selection": value.offers[0].selectedOptionId = "option.not-current"; break;
                case "invalid-history":
                    value.offers[0].history[1].optionIds = (string[])value.offers[0].history[0].optionIds.Clone();
                    break;
                case "invalid-brew-id": value.fortuneBrewReceipts[0].itemInstanceId = "not-a-guid"; break;
                case "unknown-status":
                    value.rerollReceipts[1].status = (int)FortuneOperationStatus.Succeeded;
                    break;
                case "unknown-options": value.rerollReceipts[1].optionIds = new[] { "option.invalid" }; break;
                default: Assert.Fail("Unknown corruption fixture."); break;
            }
        }

        private static void AssertUnknownOfferReceipts(FortuneOfferBookSnapshotV7 value)
        {
            FortuneRerollReceiptSnapshotV7 unknownOfferReceipt = value.rerollReceipts[1];
            FortuneRerollReceiptSnapshotV7 nullOfferReceipt = value.rerollReceipts[2];
            Assert.That(unknownOfferReceipt.hasOffer, Is.False);
            Assert.That(unknownOfferReceipt.status, Is.EqualTo((int)FortuneOperationStatus.RejectedUnknownOffer));
            Assert.That(unknownOfferReceipt.offerId, Is.EqualTo("offer.unknown"));
            Assert.That(unknownOfferReceipt.contextId, Is.EqualTo(string.Empty));
            Assert.That(unknownOfferReceipt.optionIds, Is.Empty);
            Assert.That(nullOfferReceipt.hasOffer, Is.False);
            Assert.That(nullOfferReceipt.status, Is.EqualTo((int)FortuneOperationStatus.RejectedUnknownOffer));
            Assert.That(nullOfferReceipt.offerId, Is.EqualTo(string.Empty));
            Assert.That(nullOfferReceipt.contextId, Is.EqualTo(string.Empty));
            Assert.That(nullOfferReceipt.optionIds, Is.Empty);
        }

        private static void AssertFortuneBookEqual(FortuneOfferBookSnapshotV7 actual,
            FortuneOfferBookSnapshotV7 expected)
        {
            Assert.That(actual.snapshotVersion, Is.EqualTo(expected.snapshotVersion));
            Assert.That(actual.generationVersion, Is.EqualTo(expected.generationVersion));
            Assert.That(actual.seed, Is.EqualTo(expected.seed));
            Assert.That(actual.raceId, Is.EqualTo(expected.raceId));
            Assert.That(actual.charges, Is.EqualTo(expected.charges));
            CollectionAssert.AreEqual(expected.processedGuardianIds, actual.processedGuardianIds);
            Assert.That(actual.offers, Has.Length.EqualTo(expected.offers.Length));
            for (int offerIndex = 0; offerIndex < expected.offers.Length; offerIndex++)
            {
                FortuneOfferSnapshotV7 actualOffer = actual.offers[offerIndex];
                FortuneOfferSnapshotV7 expectedOffer = expected.offers[offerIndex];
                Assert.That(actualOffer.offerId, Is.EqualTo(expectedOffer.offerId));
                Assert.That(actualOffer.contextId, Is.EqualTo(expectedOffer.contextId));
                CollectionAssert.AreEqual(expectedOffer.eligibleOptionIds, actualOffer.eligibleOptionIds);
                Assert.That(actualOffer.selectedOptionId, Is.EqualTo(expectedOffer.selectedOptionId));
                Assert.That(actualOffer.claimed, Is.EqualTo(expectedOffer.claimed));
                Assert.That(actualOffer.history, Has.Length.EqualTo(expectedOffer.history.Length));
                for (int historyIndex = 0; historyIndex < expectedOffer.history.Length; historyIndex++)
                    CollectionAssert.AreEqual(expectedOffer.history[historyIndex].optionIds,
                        actualOffer.history[historyIndex].optionIds);
            }
            Assert.That(actual.rerollReceipts, Has.Length.EqualTo(expected.rerollReceipts.Length));
            for (int index = 0; index < expected.rerollReceipts.Length; index++)
            {
                FortuneRerollReceiptSnapshotV7 actualReceipt = actual.rerollReceipts[index];
                FortuneRerollReceiptSnapshotV7 expectedReceipt = expected.rerollReceipts[index];
                Assert.That(actualReceipt.transactionId, Is.EqualTo(expectedReceipt.transactionId));
                Assert.That(actualReceipt.offerId, Is.EqualTo(expectedReceipt.offerId));
                Assert.That(actualReceipt.status, Is.EqualTo(expectedReceipt.status));
                Assert.That(actualReceipt.contextId, Is.EqualTo(expectedReceipt.contextId));
                CollectionAssert.AreEqual(expectedReceipt.optionIds, actualReceipt.optionIds);
                Assert.That(actualReceipt.selectedOptionId, Is.EqualTo(expectedReceipt.selectedOptionId));
                Assert.That(actualReceipt.rerollCount, Is.EqualTo(expectedReceipt.rerollCount));
                Assert.That(actualReceipt.claimed, Is.EqualTo(expectedReceipt.claimed));
                Assert.That(actualReceipt.remainingCharges, Is.EqualTo(expectedReceipt.remainingCharges));
                Assert.That(actualReceipt.hasOffer, Is.EqualTo(expectedReceipt.hasOffer));
            }
            Assert.That(actual.fortuneBrewReceipts, Has.Length.EqualTo(expected.fortuneBrewReceipts.Length));
            for (int index = 0; index < expected.fortuneBrewReceipts.Length; index++)
            {
                FortuneBrewReceiptSnapshotV7 actualReceipt = actual.fortuneBrewReceipts[index];
                FortuneBrewReceiptSnapshotV7 expectedReceipt = expected.fortuneBrewReceipts[index];
                Assert.That(actualReceipt.transactionId, Is.EqualTo(expectedReceipt.transactionId));
                Assert.That(actualReceipt.itemInstanceId, Is.EqualTo(expectedReceipt.itemInstanceId));
                Assert.That(actualReceipt.status, Is.EqualTo(expectedReceipt.status));
                Assert.That(actualReceipt.currentCharges, Is.EqualTo(expectedReceipt.currentCharges));
            }
        }

        private static ExpeditionRoutePlan CreateRoute(uint seed)
        {
            string[] mandatory =
            {
                "sample.tutorial",
                "sample.patio.combat",
                "sample.patio.exploration",
                "sample.patio.guardian",
                "sample.safe_extraction"
            };
            ExpeditionSampleRouteNodeDefinition[] nodes =
            {
                Node(mandatory[0], "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0),
                Node(mandatory[1], "room.patio.c1", ExpeditionRouteNodeKind.Combat, 1),
                Node(mandatory[2], "room.patio.e1", ExpeditionRouteNodeKind.Exploration, 2),
                Node(mandatory[3], "room.patio.guardian", ExpeditionRouteNodeKind.Guardian, 3),
                Node(mandatory[4], "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction, 4)
            };
            var connections = new ExpeditionSampleRouteConnectionDefinition[mandatory.Length - 1];
            for (int index = 0; index < connections.Length; index++)
            {
                connections[index] = new ExpeditionSampleRouteConnectionDefinition(mandatory[index],
                    ExpeditionSampleRouteDefinition.ExitPortId, mandatory[index + 1],
                    ExpeditionSampleRouteDefinition.EntryPortId, false);
            }
            var definition = new ExpeditionSampleRouteDefinition(
                ExpeditionSampleRouteDefinition.RequiredSchemaVersion,
                ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                nodes,
                connections,
                mandatory);
            return ExpeditionSampleRouteGenerator.Generate(seed, definition);
        }

        private static ExpeditionSampleRouteNodeDefinition Node(string nodeId, string templateId,
            ExpeditionRouteNodeKind kind, int pathIndex) =>
            new ExpeditionSampleRouteNodeDefinition(nodeId, "patio", templateId, kind, true,
                pathIndex, ExpeditionSampleRouteDefinition.EntryPortId,
                ExpeditionSampleRouteDefinition.ExitPortId);

        private static string ComputeSha256(string value)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }
    }
}
