#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using UnityEngine;

namespace Luckbound.App
{
    internal sealed class PinnedProfileCandidateV7
    {
        private readonly ProfileSaveDataV7 _profile;
        private readonly ExpeditionSaveDataV7 _expedition;

        private PinnedProfileCandidateV7(ProfileSaveDataV7 profile, ExpeditionSaveDataV7 expedition)
        {
            _profile = SaveV7Clone.Clone(profile);
            _expedition = SaveV7Clone.Clone(expedition);
        }

        internal static PinnedProfileCandidateV7 CaptureValidated(ProfileSaveDataV7 profile,
            ExpeditionSaveDataV7 expedition)
        {
            var result = new PinnedProfileCandidateV7(profile, expedition);
            ProfileSaveEnvelopeV7 envelope = result.CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            if (!SaveV7CandidateMapper.TryValidatePrepared(envelope, out SaveV7Error error))
                throw new SaveV7ValidationException(error);
            return result;
        }

        internal ProfileSaveEnvelopeV7 CreateEnvelope(int generation, long sequence, string savedAtUtc)
        {
            return new ProfileSaveEnvelopeV7
            {
                schemaVersion = 1, contentVersion = 7, generation = generation, sequence = sequence,
                savedAtUtc = savedAtUtc, checksum = new string('0', 64),
                profile = SaveV7Clone.Clone(_profile), hasActiveExpedition = _expedition != null,
                activeExpedition = SaveV7Clone.Clone(_expedition)
            };
        }
    }

    /// <summary>Typed preproduction mapping. Does not install content or publish runtime state.</summary>
    internal static class SaveV7CandidateMapper
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static PinnedProfileCandidateV7 Prepare(ProfileSaveDataV7 profile,
            ExpeditionSaveDataV7 expedition, EncounterPlanPinV1 pin)
        {
            if (profile == null) throw Invalid("profile", "Profile data is required.");
            if ((expedition == null) != (pin == null))
                throw Invalid("activeExpedition", "An expedition and its exact pin must be supplied together.");
            ExpeditionSaveDataV7 state = SaveV7Clone.Clone(expedition);
            if (state != null)
            {
                EncounterPlanPinSaveDataV7 pinDto = ToPinDto(pin);
                if (state.encounterPlanPin != null)
                {
                    if (!TryToDomainPin(state.encounterPlanPin, out EncounterPlanPinV1 prior, out SaveV7Error error))
                        throw new SaveV7ValidationException(error);
                    if (!EncounterPlanPinCanonicalV1.Encode(prior).SequenceEqual(EncounterPlanPinCanonicalV1.Encode(pin)))
                        throw Invalid("activeExpedition.encounterPlanPin", "A supplied pin cannot replace a different saved pin.");
                }
                state.encounterPlanPin = pinDto;
                bool completed = state.completedRoomIds != null && state.completedRoomIds.Contains(state.currentNodeId);
                if (!completed && string.IsNullOrWhiteSpace(state.roomEntryCheckpointJson))
                {
                    var checkpoint = new ExpeditionRoomCheckpointSaveDataV7
                    {
                        roomNodeId = state.currentNodeId, inventorySnapshotJson = state.inventorySnapshotJson,
                        maximumHealth = state.maximumHealth, currentHealth = state.currentHealth,
                        maximumEnergy = state.maximumEnergy, currentEnergy = state.currentEnergy,
                        healthPotionCharges = state.healthPotionCharges, accumulatedRenown = state.accumulatedRenown,
                        fortuneOfferBookSnapshot = SaveV7Clone.Clone(state.fortuneOfferBookSnapshot),
                        pactIds = Copy(state.pactIds), collectedItemIds = Copy(state.collectedItemIds),
                        claimedRewardIds = Copy(state.claimedRewardIds), encounterEntry = SaveV7Clone.Clone(pinDto.entry)
                    };
                    state.roomEntryCheckpointJson = StrictUtf8.GetString(SaveV7JsonCodec.EncodeCheckpoint(checkpoint));
                }
            }
            return PinnedProfileCandidateV7.CaptureValidated(profile, state);
        }

        internal static bool TryRestore(ProfileSaveEnvelopeV7 envelope, ItemCatalog itemCatalog,
            EncounterCatalogRegistryV1 registry, out ExpeditionRunConfiguration run,
            out ExpeditionSaveDataV7 resumedState, out ResolvedEncounterPlanV1 resolvedPlan,
            out SaveV7Error error)
        {
            run = null; resumedState = null; resolvedPlan = null;
            if (!SaveV7JsonCodec.TryAuthenticateProfile(envelope, out error)) return false;
            if (envelope.activeExpedition == null)
                return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition", "There is no expedition to restore.", out error);
            if (!TryBuildRun(envelope, itemCatalog, true, out ExpeditionRunConfiguration restoredRun,
                out ExpeditionSaveDataV7 restoredState, out error)) return false;
            if (!TryToDomainPin(restoredState.encounterPlanPin, out EncounterPlanPinV1 pin, out error)) return false;
            if (registry == null)
                return Fail(SaveV7ErrorCode.CatalogRevisionUnavailable, "registry", "An exact retained registry is required.", out error);
            if (!TryRequireRouteCoverage(restoredState.encounterPlanPin, restoredRun, registry, out error)) return false;
            if (!EncounterPlanPinRestorerV1.TryRestore(pin, restoredRun, restoredState.currentNodeId, registry,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanPinErrorV1 pinError))
            { error = MapPinError(pinError); return false; }
            run = restoredRun; resumedState = restoredState; resolvedPlan = plan;
            error = default(SaveV7Error);
            return true;
        }

        internal static bool TryValidatePrepared(ProfileSaveEnvelopeV7 envelope, out SaveV7Error error)
        {
            if (!SaveV7Validation.TryValidateProfile(envelope, out error)) return false;
            if (envelope.activeExpedition == null) return true;
            ExpeditionSaveDataV7 state = envelope.activeExpedition;
            if (state.visitedNodeIds.Any(id => !state.encounterPlanPin.bindings.Any(b => b.nodeId == id)))
                return Fail(SaveV7ErrorCode.PlanMismatch, "encounterPlanPin.bindings", "Every entered route node must retain its exact binding.", out error);
            ExpeditionRoomCheckpointSaveDataV7 checkpoint = null;
            string checkpointJson = envelope.activeExpedition.roomEntryCheckpointJson;
            if (!string.IsNullOrWhiteSpace(checkpointJson) &&
                !SaveV7JsonCodec.TryDecodeCheckpoint(StrictUtf8.GetBytes(checkpointJson), out checkpoint, out error)) return false;
            if (!SaveV7Validation.TryValidateCheckpointBinding(envelope, checkpoint, out error)) return false;
            if (checkpoint != null)
            {
                EncounterPlanNodeBindingSaveDataV7 binding = state.encounterPlanPin.bindings.Single(b => b.nodeId == state.currentNodeId);
                if (!binding.contextPactIds.SequenceEqual(checkpoint.pactIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                    return Fail(SaveV7ErrorCode.PlanMismatch, "checkpoint.pactIds", "Entry resources must retain the resolution context of the pinned entry.", out error);
            }
            return true;
        }

        internal static bool TryValidateCommit(ProfileSaveEnvelopeV7 envelope, ExpeditionRunConfiguration run,
            EncounterCatalogRegistryV1 registry, out SaveV7Error error)
        {
            if (!TryValidatePrepared(envelope, out error)) return false;
            if (envelope.activeExpedition == null)
            {
                if (run != null) return Fail(SaveV7ErrorCode.PlanMismatch, "run", "An inactive profile cannot carry a run.", out error);
                return true;
            }
            if (run == null || registry == null)
                return Fail(SaveV7ErrorCode.CatalogRevisionUnavailable, "run/registry", "Commit requires its run and exact registry.", out error);
            if (!TryBuildRun(envelope, run.Inventory.Catalog, false, out ExpeditionRunConfiguration savedRun,
                out ExpeditionSaveDataV7 state, out error)) return false;
            if (savedRun.RunId != run.RunId || savedRun.Seed != run.Seed || savedRun.ClassId != run.ClassId ||
                savedRun.RaceId != run.RaceId || savedRun.Route.CanonicalForm != run.Route.CanonicalForm ||
                !savedRun.SkillIds.SequenceEqual(run.SkillIds, StringComparer.Ordinal) ||
                !savedRun.StartingEquipmentInstanceIds.SequenceEqual(run.StartingEquipmentInstanceIds) ||
                savedRun.HasKnownStartingLoadout != run.HasKnownStartingLoadout ||
                (savedRun.HasKnownStartingLoadout && (savedRun.HealthUpgradeLevel != run.HealthUpgradeLevel ||
                savedRun.EnergyUpgradeLevel != run.EnergyUpgradeLevel || savedRun.AdditionalPotionUpgradeLevel != run.AdditionalPotionUpgradeLevel)))
                return Fail(SaveV7ErrorCode.PlanMismatch, "run", "Saved run identity, route or frozen choices differ from the supplied run.", out error);

            EncounterPlanPinSaveDataV7 dto = state.encounterPlanPin;
            if (!TryRequireRouteCoverage(dto, savedRun, registry, out error)) return false;
            foreach (EncounterPlanNodeBindingSaveDataV7 binding in dto.bindings)
            {
                EncounterPlanPinSaveDataV7 selected = SaveV7Clone.Clone(dto);
                selected.entry = new EncounterPlanEntryReferenceSaveDataV7
                    { nodeId = binding.nodeId, resolvedPlanSha256 = binding.resolvedPlanSha256 };
                if (!TryToDomainPin(selected, out EncounterPlanPinV1 pin, out error)) return false;
                if (!EncounterPlanPinRestorerV1.TryRestore(pin, savedRun, binding.nodeId, registry,
                    out ResolvedEncounterPlanV1 ignored, out EncounterPlanPinErrorV1 pinError))
                { error = MapPinError(pinError); return false; }
            }
            error = default(SaveV7Error);
            return true;
        }

        private static bool TryRequireRouteCoverage(EncounterPlanPinSaveDataV7 pin, ExpeditionRunConfiguration run,
            EncounterCatalogRegistryV1 registry, out SaveV7Error error)
        {
            if (!registry.TryGetExact(pin.catalogId, pin.catalogRevision, pin.catalogSha256,
                out EncounterCatalogManifest manifest, out EncounterCatalogError catalogError))
                return Fail(SaveV7ErrorCode.CatalogRevisionUnavailable, catalogError.Path, catalogError.Message, out error);
            foreach (ExpeditionRouteNode node in run.Route.Nodes)
            {
                int rows = manifest.Bindings.Count(binding => binding.GenerationVersion == run.Route.GenerationVersion &&
                    binding.TemplateId == node.TemplateId && binding.NodeKind == node.Kind);
                if (rows != 1)
                    return Fail(SaveV7ErrorCode.MissingResolutionAuthority, "route.nodes." + node.NodeId,
                        "The pinned catalog must explicitly bind every node of this route, including future nodes.", out error);
            }
            error = default(SaveV7Error);
            return true;
        }

        private static bool TryBuildRun(ProfileSaveEnvelopeV7 envelope, ItemCatalog catalog, bool rollback,
            out ExpeditionRunConfiguration run, out ExpeditionSaveDataV7 resumedState, out SaveV7Error error)
        {
            run = null; resumedState = null;
            if (!TryValidatePrepared(envelope, out error)) return false;
            if (catalog == null || envelope.activeExpedition == null)
                return Fail(SaveV7ErrorCode.InvalidData, "inventory/catalog", "Saved state and an explicit item catalog are required.", out error);
            try
            {
                ExpeditionSaveDataV7 state = SaveV7Clone.Clone(envelope.activeExpedition);
                ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(state.routeSnapshotJson);
                RestoreInventory(catalog, state.inventorySnapshotJson);
                FortuneOfferBook.Restore(ToDomain(state.fortuneOfferBookSnapshot));
                if (!string.IsNullOrWhiteSpace(state.roomEntryCheckpointJson))
                {
                    if (!SaveV7JsonCodec.TryDecodeCheckpoint(StrictUtf8.GetBytes(state.roomEntryCheckpointJson),
                        out ExpeditionRoomCheckpointSaveDataV7 checkpoint, out error)) return false;
                    RestoreInventory(catalog, checkpoint.inventorySnapshotJson);
                    FortuneOfferBook.Restore(ToDomain(checkpoint.fortuneOfferBookSnapshot));
                    ExpeditionRouteNode node = route.Nodes.Single(n => n.NodeId == state.currentNodeId);
                    if (rollback && IsCombat(node.Kind))
                    {
                        state.inventorySnapshotJson = checkpoint.inventorySnapshotJson;
                        state.maximumHealth = checkpoint.maximumHealth; state.currentHealth = checkpoint.currentHealth;
                        state.maximumEnergy = checkpoint.maximumEnergy; state.currentEnergy = checkpoint.currentEnergy;
                        state.healthPotionCharges = checkpoint.healthPotionCharges; state.accumulatedRenown = checkpoint.accumulatedRenown;
                        state.fortuneOfferBookSnapshot = SaveV7Clone.Clone(checkpoint.fortuneOfferBookSnapshot);
                        state.pactIds = Copy(checkpoint.pactIds); state.collectedItemIds = Copy(checkpoint.collectedItemIds);
                        state.claimedRewardIds = Copy(checkpoint.claimedRewardIds);
                    }
                }
                ExpeditionInventory inventory = RestoreInventory(catalog, state.inventorySnapshotJson);
                ExpeditionPermanentUpgradeSnapshotSaveDataV7 upgrades = state.hasFrozenPermanentUpgrades ? state.frozenPermanentUpgrades : null;
                var restoredRun = new ExpeditionRunConfiguration(RunId.Parse(state.runId), state.classId, state.raceId,
                    state.selectedSkillIds, state.seed, route, inventory,
                    state.startingEquipmentInstanceIds == null ? null : state.startingEquipmentInstanceIds.Select(ItemInstanceId.Parse),
                    upgrades == null ? 0 : upgrades.healthUpgradeLevel, upgrades == null ? 0 : upgrades.energyUpgradeLevel,
                    upgrades == null ? 0 : upgrades.additionalPotionUpgradeLevel, hasKnownStartingLoadout: upgrades != null);
                run = restoredRun; resumedState = state; error = default(SaveV7Error);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                exception is InvalidOperationException || exception is OverflowException)
            { return Fail(SaveV7ErrorCode.InvalidData, "activeExpedition", exception.Message, out error); }
        }

        private static ExpeditionInventory RestoreInventory(ItemCatalog catalog, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Inventory snapshot is missing.");
            ExpeditionInventorySnapshot snapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(json);
            if (snapshot == null || snapshot.snapshotVersion < 0 || snapshot.snapshotVersion > ExpeditionInventory.SnapshotSchemaVersion)
                throw new FormatException("Inventory schema is unsupported.");
            return ExpeditionInventory.Restore(catalog, snapshot);
        }

        private static bool IsCombat(ExpeditionRouteNodeKind kind) => kind == ExpeditionRouteNodeKind.Combat ||
            kind == ExpeditionRouteNodeKind.OptionalCombat || kind == ExpeditionRouteNodeKind.Guardian || kind == ExpeditionRouteNodeKind.FinalBoss;

        internal static EncounterPlanPinSaveDataV7 ToPinDto(EncounterPlanPinV1 pin)
        {
            if (pin == null) throw Invalid("pin", "A pin is required.");
            EncounterPlanPinCanonicalV1.Encode(pin);
            return new EncounterPlanPinSaveDataV7
            {
                schemaVersion = pin.SchemaVersion, runId = pin.RunId.ToString(), catalogId = pin.CatalogId,
                catalogRevision = pin.CatalogRevision, catalogSha256 = pin.CatalogSha256, routeSha256 = pin.RouteSha256,
                identityCodec = pin.IdentityCodec,
                bindings = pin.Bindings.Select(b => new EncounterPlanNodeBindingSaveDataV7
                {
                    nodeId = b.NodeId, generationVersion = b.GenerationVersion, templateId = b.TemplateId,
                    roomInstanceId = b.RoomInstanceId.ToString(), mode = b.Mode, encounterId = b.EncounterId,
                    definitionRevision = b.DefinitionRevision, definitionSha256 = b.DefinitionSha256, variantId = b.VariantId,
                    contextPactIds = b.ContextPactIds.ToArray(), calendarOrdinal = b.CalendarOrdinal,
                    resolvedPlanSha256 = b.ResolvedPlanSha256
                }).ToArray(),
                entry = new EncounterPlanEntryReferenceSaveDataV7 { nodeId = pin.Entry.NodeId, resolvedPlanSha256 = pin.Entry.ResolvedPlanSha256 }
            };
        }

        internal static bool TryToDomainPin(EncounterPlanPinSaveDataV7 dto, out EncounterPlanPinV1 pin, out SaveV7Error error)
        {
            pin = null;
            if (dto == null || dto.bindings == null || dto.entry == null || dto.schemaVersion < 1 || dto.catalogRevision < 1)
                return Fail(SaveV7ErrorCode.InvalidData, "pin", "Pin identity, collections and entry are required.", out error);
            if (dto.schemaVersion != 1)
                return Fail(SaveV7ErrorCode.Incompatible, "pin.schemaVersion", "Only pin schema 1 is supported.", out error);
            try
            {
                using (var stream = new MemoryStream())
                {
                    byte[] prefix = Encoding.ASCII.GetBytes("Luckbound.EncounterPins.v1\0");
                    stream.Write(prefix, 0, prefix.Length);
                    U32(stream, (uint)dto.schemaVersion); Text(stream, dto.runId); Text(stream, dto.catalogId);
                    U32(stream, (uint)dto.catalogRevision); Text(stream, dto.catalogSha256); Text(stream, dto.routeSha256);
                    Text(stream, dto.identityCodec); U32(stream, (uint)dto.bindings.Length);
                    for (int index = 0; index < dto.bindings.Length; index++)
                    {
                        EncounterPlanNodeBindingSaveDataV7 b = dto.bindings[index];
                        string path = "pin.bindings[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                        if (b == null || b.contextPactIds == null || b.generationVersion <= 0)
                            return Fail(SaveV7ErrorCode.InvalidData, path, "Binding, generation and context are required.", out error);
                        if (b.calendarOrdinal != 0)
                            return Fail(SaveV7ErrorCode.MissingResolutionAuthority, path + ".calendarOrdinal", "No authoritative calendar is available.", out error);
                        bool encounter = b.mode == EncounterCatalogBindingMode.Encounter;
                        if (!encounter && b.mode != EncounterCatalogBindingMode.None)
                            return Fail(SaveV7ErrorCode.InvalidData, path + ".mode", "Unknown binding mode.", out error);
                        if (!encounter && (b.encounterId != null || b.definitionRevision != 0 || b.definitionSha256 != null || b.variantId != null))
                            return Fail(SaveV7ErrorCode.InvalidData, path, "None must omit encounter payload.", out error);
                        if (encounter && b.definitionRevision <= 0)
                            return Fail(SaveV7ErrorCode.InvalidData, path + ".definitionRevision", "Encounter revision must be positive.", out error);
                        Text(stream, b.nodeId); U32(stream, (uint)b.generationVersion); Text(stream, b.templateId);
                        Text(stream, b.roomInstanceId); Text(stream, encounter ? "encounter" : "none"); stream.WriteByte(encounter ? (byte)1 : (byte)0);
                        if (encounter) { Text(stream, b.encounterId); U32(stream, (uint)b.definitionRevision); Text(stream, b.definitionSha256); Text(stream, b.variantId); }
                        U32(stream, (uint)b.contextPactIds.Length);
                        foreach (string pact in b.contextPactIds) Text(stream, pact);
                        stream.WriteByte(0); Text(stream, b.resolvedPlanSha256);
                    }
                    Text(stream, dto.entry.nodeId); Text(stream, dto.entry.resolvedPlanSha256);
                    byte[] bytes = stream.ToArray();
                    if (!EncounterPlanPinCanonicalV1.TryDecode(bytes, out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 pinError))
                    { error = MapPinError(pinError); return false; }
                    if (!bytes.SequenceEqual(EncounterPlanPinCanonicalV1.Encode(decoded)))
                        return Fail(SaveV7ErrorCode.InvalidData, "pin", "Pin encoding is not canonical.", out error);
                    pin = decoded; error = default(SaveV7Error); return true;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            { return Fail(SaveV7ErrorCode.InvalidData, "pin", exception.Message, out error); }
        }

        private static void U32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
        }

        private static void Text(Stream stream, string value)
        {
            if (value == null) throw new ArgumentException("A canonical pin string is null.");
            byte[] bytes = StrictUtf8.GetBytes(value); U32(stream, (uint)bytes.Length); stream.Write(bytes, 0, bytes.Length);
        }

        private static SaveV7Error MapPinError(EncounterPlanPinErrorV1 error)
        {
            SaveV7ErrorCode code;
            switch (error.Code)
            {
                case EncounterPlanPinErrorCodeV1.UnsupportedSchema: code = SaveV7ErrorCode.Incompatible; break;
                case EncounterPlanPinErrorCodeV1.CatalogRevisionUnavailable: code = SaveV7ErrorCode.CatalogRevisionUnavailable; break;
                case EncounterPlanPinErrorCodeV1.MissingResolutionAuthority: code = SaveV7ErrorCode.MissingResolutionAuthority; break;
                case EncounterPlanPinErrorCodeV1.PlanMismatch: code = SaveV7ErrorCode.PlanMismatch; break;
                default: code = SaveV7ErrorCode.InvalidData; break;
            }
            return new SaveV7Error(code, error.Path, error.Message);
        }

        private static bool Fail(SaveV7ErrorCode code, string path, string message, out SaveV7Error error)
        { error = new SaveV7Error(code, path, message); return false; }
        private static SaveV7ValidationException Invalid(string path, string message) =>
            new SaveV7ValidationException(new SaveV7Error(SaveV7ErrorCode.InvalidData, path, message));
        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();

        internal static bool TryValidateTransition(ProfileSaveEnvelopeV7 prior, ProfileSaveEnvelope legacy,
            ProfileSaveEnvelopeV7 next, out SaveV7Error error)
        {
            error = default(SaveV7Error);
            if (legacy != null)
            {
                if (legacy.hasActiveExpedition)
                    return Fail(SaveV7ErrorCode.LegacyEncounterPinMissing, "legacy.activeExpedition", "An active legacy run cannot be migrated without its pin.", out error);
                // Migration is explicit and lossless. A fresh run may be added in the same
                // transaction, but old profile receipts/arrays must survive without normalization.
                if (!ProfileBytes(ToV7(legacy.profile)).SequenceEqual(ProfileBytes(next.profile)))
                    return Fail(SaveV7ErrorCode.InvalidData, "profile", "Explicit legacy migration must preserve the complete profile payload.", out error);
            }
            if (prior == null) return true;
            if (!Retains(prior.profile.processedSettlementRunIds, next.profile.processedSettlementRunIds) ||
                !Retains(prior.profile.processedContractProgressEvents, next.profile.processedContractProgressEvents,
                    (a, b) => a != null && b != null && a.eventId == b.eventId && a.progressEvent == b.progressEvent) ||
                !Retains(prior.profile.progressionPurchaseTransactions, next.profile.progressionPurchaseTransactions,
                    (a, b) => a != null && b != null && a.transactionId == b.transactionId && a.kind == b.kind && a.cost == b.cost))
                return Fail(SaveV7ErrorCode.PlanMismatch, "profile.receipts", "A commit cannot discard or alter durable profile receipts.", out error);
            ExpeditionSaveDataV7 old = prior.activeExpedition;
            ExpeditionSaveDataV7 state = next.activeExpedition;
            if (old == null) return true;
            if (state == null || old.runId != state.runId || old.seed != state.seed || old.generationVersion != state.generationVersion ||
                old.classId != state.classId || old.raceId != state.raceId || old.routeSnapshotJson != state.routeSnapshotJson ||
                !Sequence(old.selectedSkillIds, state.selectedSkillIds) || !Sequence(old.startingEquipmentInstanceIds, state.startingEquipmentInstanceIds) ||
                old.hasFrozenPermanentUpgrades != state.hasFrozenPermanentUpgrades || !UpgradesEqual(old.frozenPermanentUpgrades, state.frozenPermanentUpgrades))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition", "An existing durable run and frozen choices cannot be replaced or discarded.", out error);
            EncounterPlanPinSaveDataV7 previous = old.encounterPlanPin;
            EncounterPlanPinSaveDataV7 current = state.encounterPlanPin;
            if (previous.schemaVersion != current.schemaVersion || previous.runId != current.runId || previous.catalogId != current.catalogId ||
                previous.catalogRevision != current.catalogRevision || previous.catalogSha256 != current.catalogSha256 ||
                previous.routeSha256 != current.routeSha256 || previous.identityCodec != current.identityCodec ||
                !Retains(previous.bindings, current.bindings, BindingsEqual))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.encounterPlanPin", "Pinned catalog and existing node bindings are immutable.", out error);
            if (!Prefix(old.visitedNodeIds, state.visitedNodeIds) || !Retains(old.completedRoomIds, state.completedRoomIds) ||
                !Retains(old.completedRoomResults, state.completedRoomResults,
                    (a, b) => a != null && b != null && a.roomNodeId == b.roomNodeId && a.resultId == b.resultId && a.resultJson == b.resultJson))
                return Fail(SaveV7ErrorCode.PlanMismatch, "activeExpedition.history", "Durable route history and room results must be retained exactly.", out error);
            if (old.currentNodeId == state.currentNodeId)
            {
                bool justCompleted = state.completedRoomIds.Contains(state.currentNodeId) &&
                    !old.completedRoomIds.Contains(old.currentNodeId) && string.IsNullOrWhiteSpace(state.roomEntryCheckpointJson);
                if (!justCompleted && old.roomEntryCheckpointJson != state.roomEntryCheckpointJson)
                    return Fail(SaveV7ErrorCode.PlanMismatch, "roomEntryCheckpointJson", "A commit cannot regenerate the rollback checkpoint of the same entry.", out error);
            }
            else if (!state.completedRoomIds.Contains(old.currentNodeId) || state.visitedNodeIds.Length != old.visitedNodeIds.Length + 1)
                return Fail(SaveV7ErrorCode.PlanMismatch, "currentNodeId", "A transition must complete the previous entry and advance once.", out error);
            return true;
        }

        private static byte[] ProfileBytes(ProfileSaveDataV7 profile)
        {
            return SaveV7JsonCodec.EncodeProfile(new ProfileSaveEnvelopeV7
            {
                schemaVersion = 1, contentVersion = 7, generation = 1, sequence = 1,
                savedAtUtc = "2000-01-01T00:00:00.0000000Z", checksum = string.Empty,
                profile = SaveV7Clone.Clone(profile), hasActiveExpedition = false, activeExpedition = null
            });
        }

        private static bool UpgradesEqual(ExpeditionPermanentUpgradeSnapshotSaveDataV7 a, ExpeditionPermanentUpgradeSnapshotSaveDataV7 b) =>
            ReferenceEquals(a, b) || (a != null && b != null && a.healthUpgradeLevel == b.healthUpgradeLevel &&
                a.energyUpgradeLevel == b.energyUpgradeLevel && a.additionalPotionUpgradeLevel == b.additionalPotionUpgradeLevel);

        private static bool BindingsEqual(EncounterPlanNodeBindingSaveDataV7 a, EncounterPlanNodeBindingSaveDataV7 b) =>
            a != null && b != null && a.nodeId == b.nodeId && a.generationVersion == b.generationVersion && a.templateId == b.templateId &&
            a.roomInstanceId == b.roomInstanceId && a.mode == b.mode && a.encounterId == b.encounterId &&
            a.definitionRevision == b.definitionRevision && a.definitionSha256 == b.definitionSha256 && a.variantId == b.variantId &&
            a.calendarOrdinal == b.calendarOrdinal && a.resolvedPlanSha256 == b.resolvedPlanSha256 && Sequence(a.contextPactIds, b.contextPactIds);

        private static bool Sequence(string[] a, string[] b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b, StringComparer.Ordinal));
        private static bool Prefix(string[] prior, string[] next) => prior != null && next != null &&
            next.Length >= prior.Length && prior.SequenceEqual(next.Take(prior.Length), StringComparer.Ordinal);
        private static bool Retains(string[] prior, string[] next) => Retains(prior, next, (a, b) => a == b);
        private static bool Retains<T>(T[] prior, T[] next, Func<T, T, bool> same)
        {
            if (prior == null) return next == null;
            return next != null && prior.All(a => next.Any(b => same(a, b)));
        }

        internal static ProfileSaveDataV7 ToV7(ProfileSaveData value)
        {
            return value == null ? null : new ProfileSaveDataV7
            {
                availableRenown = value.availableRenown,
                healthUpgradeLevel = value.healthUpgradeLevel,
                energyUpgradeLevel = value.energyUpgradeLevel,
                additionalPotionUpgradeLevel = value.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(value.unlockedRaceIds),
                unlockedStartingKitIds = Copy(value.unlockedStartingKitIds),
                acceptedContractIds = Copy(value.acceptedContractIds),
                claimedContractIds = Copy(value.claimedContractIds),
                readStoryFragmentIds = Copy(value.readStoryFragmentIds),
                contractCounters = value.contractCounters == null ? null : value.contractCounters.Select(ToV7).ToArray(),
                processedSettlementRunIds = Copy(value.processedSettlementRunIds),
                processedContractProgressEvents = value.processedContractProgressEvents == null ? null : value.processedContractProgressEvents.Select(ToV7).ToArray(),
                progressionPurchaseTransactions = value.progressionPurchaseTransactions == null ? null : value.progressionPurchaseTransactions.Select(ToV7).ToArray()
            };
        }

        internal static ContractCounterSaveDataV7 ToV7(ContractCounterSaveData value)
        {
            return value == null ? null : new ContractCounterSaveDataV7
            {
                contractId = value.contractId,
                progress = value.progress
            };
        }

        internal static ContractProgressEventReceiptSaveDataV7 ToV7(ContractProgressEventReceiptSaveData value)
        {
            return value == null ? null : new ContractProgressEventReceiptSaveDataV7
            {
                eventId = value.eventId,
                progressEvent = value.progressEvent
            };
        }

        internal static PermanentUpgradePurchaseSaveDataV7 ToV7(PermanentUpgradePurchaseSaveData value)
        {
            return value == null ? null : new PermanentUpgradePurchaseSaveDataV7
            {
                transactionId = value.transactionId,
                kind = value.kind,
                cost = value.cost
            };
        }

        internal static ExpeditionSaveDataV7 ToV7(ExpeditionSaveData value)
        {
            return value == null ? null : new ExpeditionSaveDataV7
            {
                runId = value.runId,
                seed = value.seed,
                generationVersion = value.generationVersion,
                contentVersion = value.contentVersion,
                classId = value.classId,
                raceId = value.raceId,
                selectedSkillIds = Copy(value.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(value.startingEquipmentInstanceIds),
                routeSnapshotJson = value.routeSnapshotJson,
                currentNodeId = value.currentNodeId,
                visitedNodeIds = Copy(value.visitedNodeIds),
                completedRoomIds = Copy(value.completedRoomIds),
                completedRoomResults = value.completedRoomResults == null ? null : value.completedRoomResults.Select(ToV7).ToArray(),
                roomEntryCheckpointJson = value.roomEntryCheckpointJson,
                inventorySnapshotJson = value.inventorySnapshotJson,
                maximumHealth = value.maximumHealth,
                currentHealth = value.currentHealth,
                maximumEnergy = value.maximumEnergy,
                currentEnergy = value.currentEnergy,
                healthPotionCharges = value.healthPotionCharges,
                frozenPermanentUpgrades = ToV7(value.frozenPermanentUpgrades),
                hasFrozenPermanentUpgrades = value.hasFrozenPermanentUpgrades,
                accumulatedRenown = value.accumulatedRenown,
                pactIds = Copy(value.pactIds),
                fortuneOfferBookSnapshot = ToV7(value.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(value.collectedItemIds),
                claimedRewardIds = Copy(value.claimedRewardIds)
            };
        }

        internal static ExpeditionPermanentUpgradeSnapshotSaveDataV7 ToV7(ExpeditionPermanentUpgradeSnapshotSaveData value)
        {
            return value == null ? null : new ExpeditionPermanentUpgradeSnapshotSaveDataV7
            {
                healthUpgradeLevel = value.healthUpgradeLevel,
                energyUpgradeLevel = value.energyUpgradeLevel,
                additionalPotionUpgradeLevel = value.additionalPotionUpgradeLevel
            };
        }

        internal static ExpeditionRoomCompletionSaveDataV7 ToV7(ExpeditionRoomCompletionSaveData value)
        {
            return value == null ? null : new ExpeditionRoomCompletionSaveDataV7
            {
                roomNodeId = value.roomNodeId,
                resultId = value.resultId,
                resultJson = value.resultJson
            };
        }

        internal static FortuneOfferBookSnapshotV7 ToV7(FortuneOfferBookSnapshot value)
        {
            return value == null ? null : new FortuneOfferBookSnapshotV7
            {
                snapshotVersion = value.snapshotVersion,
                generationVersion = value.generationVersion,
                seed = value.seed,
                raceId = value.raceId,
                charges = value.charges,
                offers = value.offers == null ? null : value.offers.Select(ToV7).ToArray(),
                processedGuardianIds = Copy(value.processedGuardianIds),
                rerollReceipts = value.rerollReceipts == null ? null : value.rerollReceipts.Select(ToV7).ToArray(),
                fortuneBrewReceipts = value.fortuneBrewReceipts == null ? null : value.fortuneBrewReceipts.Select(ToV7).ToArray()
            };
        }

        private static FortuneOfferBookSnapshot ToDomain(FortuneOfferBookSnapshotV7 value)
        {
            return value == null ? null : new FortuneOfferBookSnapshot
            {
                snapshotVersion = value.snapshotVersion,
                generationVersion = value.generationVersion,
                seed = value.seed,
                raceId = value.raceId,
                charges = value.charges,
                offers = value.offers == null ? null : value.offers.Select(ToDomain).ToArray(),
                processedGuardianIds = Copy(value.processedGuardianIds),
                rerollReceipts = value.rerollReceipts == null ? null : value.rerollReceipts.Select(ToDomain).ToArray(),
                fortuneBrewReceipts = value.fortuneBrewReceipts == null ? null : value.fortuneBrewReceipts.Select(ToDomain).ToArray()
            };
        }

        internal static FortuneOfferSnapshotV7 ToV7(FortuneOfferSnapshot value)
        {
            return value == null ? null : new FortuneOfferSnapshotV7
            {
                offerId = value.offerId,
                contextId = value.contextId,
                eligibleOptionIds = Copy(value.eligibleOptionIds),
                history = value.history == null ? null : value.history.Select(ToV7).ToArray(),
                selectedOptionId = value.selectedOptionId,
                claimed = value.claimed
            };
        }

        private static FortuneOfferSnapshot ToDomain(FortuneOfferSnapshotV7 value)
        {
            return value == null ? null : new FortuneOfferSnapshot
            {
                offerId = value.offerId,
                contextId = value.contextId,
                eligibleOptionIds = Copy(value.eligibleOptionIds),
                history = value.history == null ? null : value.history.Select(ToDomain).ToArray(),
                selectedOptionId = value.selectedOptionId,
                claimed = value.claimed
            };
        }

        internal static FortuneOfferHistorySnapshotV7 ToV7(FortuneOfferHistorySnapshot value)
        {
            return value == null ? null : new FortuneOfferHistorySnapshotV7
            {
                optionIds = Copy(value.optionIds)
            };
        }

        private static FortuneOfferHistorySnapshot ToDomain(FortuneOfferHistorySnapshotV7 value)
        {
            return value == null ? null : new FortuneOfferHistorySnapshot
            {
                optionIds = Copy(value.optionIds)
            };
        }

        internal static FortuneRerollReceiptSnapshotV7 ToV7(FortuneRerollReceiptSnapshot value)
        {
            return value == null ? null : new FortuneRerollReceiptSnapshotV7
            {
                transactionId = value.transactionId,
                offerId = value.offerId,
                status = value.status,
                contextId = value.contextId,
                optionIds = Copy(value.optionIds),
                selectedOptionId = value.selectedOptionId,
                rerollCount = value.rerollCount,
                claimed = value.claimed,
                remainingCharges = value.remainingCharges,
                hasOffer = value.hasOffer
            };
        }

        private static FortuneRerollReceiptSnapshot ToDomain(FortuneRerollReceiptSnapshotV7 value)
        {
            return value == null ? null : new FortuneRerollReceiptSnapshot
            {
                transactionId = value.transactionId,
                offerId = value.offerId,
                status = value.status,
                contextId = value.contextId,
                optionIds = Copy(value.optionIds),
                selectedOptionId = value.selectedOptionId,
                rerollCount = value.rerollCount,
                claimed = value.claimed,
                remainingCharges = value.remainingCharges,
                hasOffer = value.hasOffer
            };
        }

        internal static FortuneBrewReceiptSnapshotV7 ToV7(FortuneBrewReceiptSnapshot value)
        {
            return value == null ? null : new FortuneBrewReceiptSnapshotV7
            {
                transactionId = value.transactionId,
                itemInstanceId = value.itemInstanceId,
                status = value.status,
                currentCharges = value.currentCharges
            };
        }

        private static FortuneBrewReceiptSnapshot ToDomain(FortuneBrewReceiptSnapshotV7 value)
        {
            return value == null ? null : new FortuneBrewReceiptSnapshot
            {
                transactionId = value.transactionId,
                itemInstanceId = value.itemInstanceId,
                status = value.status,
                currentCharges = value.currentCharges
            };
        }

        internal static ExpeditionSaveData CloneLegacyExpedition(ExpeditionSaveData value)
        {
            return value == null ? null : new ExpeditionSaveData
            {
                runId = value.runId,
                seed = value.seed,
                generationVersion = value.generationVersion,
                contentVersion = value.contentVersion,
                classId = value.classId,
                raceId = value.raceId,
                selectedSkillIds = Copy(value.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(value.startingEquipmentInstanceIds),
                routeSnapshotJson = value.routeSnapshotJson,
                currentNodeId = value.currentNodeId,
                visitedNodeIds = Copy(value.visitedNodeIds),
                completedRoomIds = Copy(value.completedRoomIds),
                completedRoomResults = value.completedRoomResults == null ? null : value.completedRoomResults.Select(x => x == null ? null : x.Clone()).ToArray(),
                roomEntryCheckpointJson = value.roomEntryCheckpointJson,
                inventorySnapshotJson = value.inventorySnapshotJson,
                maximumHealth = value.maximumHealth,
                currentHealth = value.currentHealth,
                maximumEnergy = value.maximumEnergy,
                currentEnergy = value.currentEnergy,
                healthPotionCharges = value.healthPotionCharges,
                frozenPermanentUpgrades = value.frozenPermanentUpgrades == null ? null : value.frozenPermanentUpgrades.Clone(),
                hasFrozenPermanentUpgrades = value.hasFrozenPermanentUpgrades,
                accumulatedRenown = value.accumulatedRenown,
                pactIds = Copy(value.pactIds),
                fortuneOfferBookSnapshot = ToDomain(ToV7(value.fortuneOfferBookSnapshot)),
                collectedItemIds = Copy(value.collectedItemIds),
                claimedRewardIds = Copy(value.claimedRewardIds)
            };
        }
    }
}
#endif
