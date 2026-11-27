#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Private receipt transport and lazy C1 physical asset issuance for F10 V7.</summary>
    public sealed partial class ApplicationBootstrapper
    {
        internal static bool TryRevalidateExactF10V7PhysicalInputs(
            F10V7VerifiedPhysicalInputs retained,
            out F10V7VerifiedPhysicalInputs fresh, out string error)
        {
            fresh = null;
            error = string.Empty;
            if (retained == null)
                return FailF10V7C1Binding(
                    "An App-retained verified physical-input receipt is required.", out error);

            F10V7VerifiedPhysicalInputs candidate;
            try { candidate = retained.Revalidate(); }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The retained physical-input receipt could not be revalidated: " +
                    exception.GetType().Name + ": " + exception.Message, out error);
            }

            try
            {
                if (candidate == null || !AreEquivalentF10V7PhysicalInputs(retained, candidate))
                    return FailF10V7C1Binding(
                        "The refreshed physical-input closure or persistent Unity identities differ from the retained receipt.",
                        out error);
            }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The refreshed physical-input receipt could not be compared safely: " +
                    exception.GetType().Name + ".", out error);
            }

            fresh = candidate;
            return true;
        }

        private bool TryValidateF10V7VerifiedInputsForAdmission(
            F10V7AdmissionContext context, ProfileSaveEnvelopeV7 envelope,
            F10V7VerifiedPhysicalInputs retained, out string error)
        {
            error = string.Empty;
            if (context == null || envelope?.activeExpedition == null ||
                !ReferenceEquals(context.Bootstrapper, this) ||
                !ReferenceEquals(context.Registry, context.SessionHandle?.Registry) ||
                string.IsNullOrWhiteSpace(context.ConfirmedJson) ||
                !string.Equals(envelope.activeExpedition.runId, context.RunId,
                    StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "The physical-input receipt is not attached to the exact admitted V7 JSON and registry.",
                    out error);

            return TryValidateF10V7ReceiptAgainstExactOpening(context.ConfirmedJson,
                context.Registry, retained, out _, out error);
        }

        private static bool TryValidateF10V7ReceiptAgainstExactOpening(
            string exactOpeningJson, EncounterCatalogRegistryV1 registry,
            F10V7VerifiedPhysicalInputs retained,
            out F10V7VerifiedPhysicalInputs fresh, out string error)
        {
            fresh = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(exactOpeningJson) || registry == null || retained == null)
                return FailF10V7C1Binding(
                    "The exact admitted JSON, catalog registry, and verified physical-input receipt are required.",
                    out error);

            if (!TryRevalidateExactF10V7PhysicalInputs(retained,
                    out F10V7VerifiedPhysicalInputs revalidated, out error)) return false;

            ProfileSaveEnvelopeV7 envelope;
            EncounterPlanPinV1 pin;
            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(exactOpeningJson),
                        out envelope, out SaveV7Error decodeError))
                {
                    return FailF10V7C1Binding(
                        "The exact admitted V7 JSON has no valid catalog pin: " +
                        decodeError.Code + ": " + decodeError.Message, out error);
                }
                if (envelope == null || envelope.activeExpedition == null)
                    return FailF10V7C1Binding(
                        "The exact admitted V7 JSON has no active expedition catalog pin.", out error);
                if (!SaveV7CandidateMapper.TryToDomainPin(
                        envelope.activeExpedition.encounterPlanPin,
                        out pin, out SaveV7Error pinError))
                    return FailF10V7C1Binding(
                        "The exact admitted V7 JSON has no valid catalog pin: " +
                        pinError.Code + ": " + pinError.Message, out error);
                if (!string.Equals(envelope.activeExpedition.runId, pin.RunId.ToString(),
                        StringComparison.Ordinal))
                    return FailF10V7C1Binding(
                        "The exact admitted V7 pin belongs to a different run.", out error);

                if (!registry.TryGetExact(pin.CatalogId, pin.CatalogRevision,
                        pin.CatalogSha256, out EncounterCatalogManifest manifest,
                        out EncounterCatalogError registryError) || manifest == null ||
                    !string.Equals(manifest.CatalogId, pin.CatalogId, StringComparison.Ordinal) ||
                    manifest.CatalogRevision != pin.CatalogRevision ||
                    !string.Equals(EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest),
                        pin.CatalogSha256, StringComparison.Ordinal))
                    return FailF10V7C1Binding(
                        "The exact admitted pin is unavailable from its retained registry: " +
                        registryError.Code + ": " + registryError.Message, out error);

                if (!TryValidateReceiptDescriptorFingerprints(manifest, revalidated, out error))
                    return false;
            }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The exact admitted catalog pin could not be checked against the physical receipt: " +
                    exception.GetType().Name + ".", out error);
            }

            fresh = revalidated;
            return true;
        }

        private static bool TryValidateReceiptDescriptorFingerprints(
            EncounterCatalogManifest manifest, F10V7VerifiedPhysicalInputs inputs,
            out string error)
        {
            error = string.Empty;
            if (manifest == null || inputs == null || manifest.DescriptorRefs == null ||
                manifest.DescriptorRefs.Count != F10PhysicalDescriptorTableV1.DescriptorCount ||
                !IsLowerF10V7C1Sha256(inputs.PhysicalDescriptorPayloadSha256) ||
                !IsLowerF10V7C1Sha256(inputs.BundleManifestSha256))
                return FailF10V7C1Binding(
                    "The exact registry catalog and measured physical payload must contain the fixed descriptor set.",
                    out error);

            var descriptorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EncounterCatalogDescriptor descriptor in manifest.DescriptorRefs)
            {
                if (descriptor == null || descriptor.Revision != 1 ||
                    string.IsNullOrWhiteSpace(descriptor.DescriptorId) ||
                    !descriptorIds.Add(descriptor.DescriptorId) ||
                    !F10PhysicalDescriptorTableV1.TryGetDescriptor(descriptor.DescriptorId,
                        out F10PhysicalDescriptorV1 fixedDescriptor))
                    return FailF10V7C1Binding(
                        "The exact registry descriptor set contains a duplicate or noncanonical descriptor identity.",
                        out error);

                EncounterCatalogDescriptorKind expectedKind =
                    string.Equals(fixedDescriptor.Kind, "room", StringComparison.Ordinal)
                        ? EncounterCatalogDescriptorKind.Room
                        : string.Equals(fixedDescriptor.Kind, "enemy", StringComparison.Ordinal)
                            ? EncounterCatalogDescriptorKind.Enemy
                            : string.Equals(fixedDescriptor.Kind, "cache", StringComparison.Ordinal)
                                ? EncounterCatalogDescriptorKind.Cache
                                : (EncounterCatalogDescriptorKind)0;
                if (expectedKind == 0 || descriptor.Kind != expectedKind ||
                    !F10PhysicalDescriptorTableV1.TryComputeFingerprint(
                        descriptor.DescriptorId, descriptor.Revision,
                        inputs.PhysicalDescriptorPayloadSha256, inputs.BundleManifestSha256,
                        out F10PhysicalDescriptorFingerprintResultV1 fingerprint) ||
                    fingerprint == null || fingerprint.Revision != descriptor.Revision ||
                    !string.Equals(fingerprint.Descriptor.Kind, fixedDescriptor.Kind,
                        StringComparison.Ordinal) ||
                    !string.Equals(descriptor.Sha256, fingerprint.Fingerprint,
                        StringComparison.Ordinal))
                    return FailF10V7C1Binding(
                        "The retained registry descriptor does not match its fixed-table kind and receipt fingerprints.",
                        out error);
            }

            return descriptorIds.Count == F10PhysicalDescriptorTableV1.DescriptorCount ||
                FailF10V7C1Binding(
                    "The retained registry catalog omitted a fixed-table physical descriptor.", out error);
        }

        private static bool AreEquivalentF10V7PhysicalInputs(
            F10V7VerifiedPhysicalInputs left, F10V7VerifiedPhysicalInputs right)
        {
            return left != null && right != null &&
                AreSamePath(left.ProjectRoot, right.ProjectRoot) &&
                AreSamePath(left.CopySourceRoot, right.CopySourceRoot) &&
                AreSamePath(left.CopyMarkerPath, right.CopyMarkerPath) &&
                AreSamePath(left.BundleDirectory, right.BundleDirectory) &&
                string.Equals(left.CopyIsolationChecks, right.CopyIsolationChecks, StringComparison.Ordinal) &&
                string.Equals(left.RevisionId, right.RevisionId, StringComparison.Ordinal) &&
                string.Equals(left.BundleManifestSha256, right.BundleManifestSha256, StringComparison.Ordinal) &&
                string.Equals(left.PhysicalDescriptorPayloadSha256,
                    right.PhysicalDescriptorPayloadSha256, StringComparison.Ordinal) &&
                string.Equals(left.BundleManifestJson, right.BundleManifestJson, StringComparison.Ordinal) &&
                string.Equals(left.PhysicalDescriptorJson, right.PhysicalDescriptorJson, StringComparison.Ordinal) &&
                left.PhysicalDescriptorInstallable == right.PhysicalDescriptorInstallable &&
                left.BundleInstallable == right.BundleInstallable &&
                string.Equals(left.ExporterSourceSha256, right.ExporterSourceSha256, StringComparison.Ordinal) &&
                string.Equals(left.ExporterMetaSha256, right.ExporterMetaSha256, StringComparison.Ordinal) &&
                AreSameStringSet(left.MissingPhysicalEvidenceIds, right.MissingPhysicalEvidenceIds) &&
                AreSameDependencies(left, left.DependencyClosure, right, right.DependencyClosure) &&
                AreSameAssets(left, left.PersistentAssets, right, right.PersistentAssets) &&
                AreSameAssets(left, left.SourceAssets, right, right.SourceAssets) &&
                AreSameAssets(left, left.PrefabAssets, right, right.PrefabAssets);
        }

        private static bool AreSameStringSet(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in left)
                if (string.IsNullOrWhiteSpace(value) || !values.Add(value)) return false;
            foreach (string value in right)
                if (!values.Remove(value)) return false;
            return values.Count == 0;
        }

        private static bool AreSameDependencies(F10V7VerifiedPhysicalInputs leftOwner,
            IReadOnlyList<F10V7VerifiedPhysicalInputs.DependencyReference> left,
            F10V7VerifiedPhysicalInputs rightOwner,
            IReadOnlyList<F10V7VerifiedPhysicalInputs.DependencyReference> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            StringComparer comparer = ProjectPathComparer;
            var rightByPath = new Dictionary<string, F10V7VerifiedPhysicalInputs.DependencyReference>(comparer);
            foreach (F10V7VerifiedPhysicalInputs.DependencyReference item in right)
            {
                if (item == null || !TryNormalizeAssetPath(rightOwner.ProjectRoot,
                        item.AssetPath, out string path) || rightByPath.ContainsKey(path)) return false;
                rightByPath.Add(path, item);
            }

            var leftPaths = new HashSet<string>(comparer);
            foreach (F10V7VerifiedPhysicalInputs.DependencyReference item in left)
            {
                if (item == null || !TryNormalizeAssetPath(leftOwner.ProjectRoot,
                        item.AssetPath, out string path) || !leftPaths.Add(path) ||
                    !rightByPath.TryGetValue(path, out F10V7VerifiedPhysicalInputs.DependencyReference match) ||
                    !string.Equals(item.Guid, match.Guid, StringComparison.Ordinal) ||
                    !string.Equals(item.AssetSha256, match.AssetSha256, StringComparison.Ordinal) ||
                    !string.Equals(item.MetaSha256, match.MetaSha256, StringComparison.Ordinal)) return false;
            }
            return leftPaths.Count == rightByPath.Count;
        }

        private static bool AreSameAssets(F10V7VerifiedPhysicalInputs leftOwner,
            IReadOnlyList<F10V7VerifiedPhysicalInputs.AssetReference> left,
            F10V7VerifiedPhysicalInputs rightOwner,
            IReadOnlyList<F10V7VerifiedPhysicalInputs.AssetReference> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            StringComparer comparer = ProjectPathComparer;
            var rightByPath = new Dictionary<string, F10V7VerifiedPhysicalInputs.AssetReference>(comparer);
            foreach (F10V7VerifiedPhysicalInputs.AssetReference item in right)
            {
                if (item == null || !TryNormalizeAssetPath(rightOwner.ProjectRoot,
                        item.AssetPath, out string path) || rightByPath.ContainsKey(path)) return false;
                rightByPath.Add(path, item);
            }

            var leftPaths = new HashSet<string>(comparer);
            foreach (F10V7VerifiedPhysicalInputs.AssetReference item in left)
            {
                if (item == null || !TryNormalizeAssetPath(leftOwner.ProjectRoot,
                        item.AssetPath, out string path) || !leftPaths.Add(path) ||
                    !rightByPath.TryGetValue(path,
                        out F10V7VerifiedPhysicalInputs.AssetReference match) ||
                    !string.Equals(item.Guid, match.Guid, StringComparison.Ordinal) ||
                    item.LocalFileId != match.LocalFileId ||
                    !string.Equals(item.AssetSha256, match.AssetSha256, StringComparison.Ordinal) ||
                    !string.Equals(item.MetaSha256, match.MetaSha256, StringComparison.Ordinal) ||
                    !ReferenceEquals(item.Asset, match.Asset) ||
                    item.Asset == null || match.Asset == null) return false;
            }
            return leftPaths.Count == rightByPath.Count;
        }

        private static bool TryNormalizeAssetPath(string projectRoot, string assetPath,
            out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(assetPath) ||
                Path.IsPathRooted(assetPath)) return false;
            string root = NormalizeDirectoryPath(projectRoot);
            string relative = assetPath.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            string path = NormalizeDirectoryPath(Path.Combine(root, relative));
            string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root : root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, ProjectPathComparison)) return false;
            normalized = path;
            return true;
        }

        private static bool AreSamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(NormalizeDirectoryPath(left), NormalizeDirectoryPath(right),
                ProjectPathComparison);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full) ?? string.Empty;
            while (full.Length > root.Length &&
                (full[full.Length - 1] == Path.DirectorySeparatorChar ||
                 full[full.Length - 1] == Path.AltDirectorySeparatorChar))
                full = full.Substring(0, full.Length - 1);
            return full;
        }

        private static StringComparer ProjectPathComparer => Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private static StringComparison ProjectPathComparison => Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private bool TryCreateFreshF10V7C1MeasuredSnapshot(
            V7C1AttachmentView expectedAttachment, ResolvedEncounterPlanV1 expectedPlan,
            string expectedPlanSha256, object gateOwnerToken,
            out V7C1AttachmentView refreshedAttachment,
            out ResolvedEncounterPlanV1 refreshedPlan, out EncounterPlanPinV1 refreshedPin,
            out ExpeditionRoomCheckpointSaveDataV7 refreshedCheckpoint,
            out string refreshedPlanSha256, out F10V7C1MeasuredAssetSnapshot snapshot,
            out string error)
        {
            refreshedAttachment = null;
            refreshedPlan = null;
            refreshedPin = null;
            refreshedCheckpoint = null;
            refreshedPlanSha256 = string.Empty;
            snapshot = null;
            error = string.Empty;
            if (expectedAttachment?.SessionBinding == null || expectedPlan == null ||
                string.IsNullOrWhiteSpace(expectedPlanSha256))
                return FailF10V7C1Binding(
                    "The exact V7 C1 attachment and restored plan are required before receipt issuance.",
                    out error);

            F10V7SessionInitializationBinding sessionBinding = expectedAttachment.SessionBinding;
            if (!TryValidateF10V7ReceiptAgainstExactOpening(sessionBinding.AdmittedJson,
                    sessionBinding.AdmissionRegistry, sessionBinding.VerifiedPhysicalInputs,
                    out F10V7VerifiedPhysicalInputs freshInputs, out error)) return false;

            if (!TryCaptureCurrentV7C1Attachment(expectedAttachment.Composition, gateOwnerToken,
                    out V7C1AttachmentView currentAttachment, out error) ||
                !TryRestoreCurrentV7C1Plan(currentAttachment.SessionBinding,
                    currentAttachment.Published, currentAttachment.ConfirmedJson,
                    currentAttachment.Revision, out ResolvedEncounterPlanV1 currentPlan,
                    out EncounterPlanPinV1 currentPin,
                    out ExpeditionRoomCheckpointSaveDataV7 currentCheckpoint,
                    out string currentPlanSha256, out error)) return false;

            if (!AreSameF10V7C1Attachment(expectedAttachment, currentAttachment) ||
                !string.Equals(expectedPlanSha256, currentPlanSha256, StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "The current publisher, owner, room, revision, confirmed JSON, or pinned plan changed during receipt verification.",
                    out error);

            if (!TryIssueF10V7C1MeasuredSnapshot(freshInputs, currentAttachment,
                    currentPlan, out F10V7C1MeasuredAssetSnapshot issued, out error)) return false;

            refreshedAttachment = currentAttachment;
            refreshedPlan = currentPlan;
            refreshedPin = currentPin;
            refreshedCheckpoint = currentCheckpoint;
            refreshedPlanSha256 = currentPlanSha256;
            snapshot = issued;
            return true;
        }

        private bool TryIssueF10V7C1MeasuredSnapshot(
            F10V7VerifiedPhysicalInputs inputs, V7C1AttachmentView attachment,
            ResolvedEncounterPlanV1 plan,
            out F10V7C1MeasuredAssetSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (inputs == null || attachment?.SessionBinding == null ||
                !AreEquivalentF10V7PhysicalInputs(
                    attachment.SessionBinding.VerifiedPhysicalInputs, inputs) ||
                plan?.UsedDescriptors == null)
                return FailF10V7C1Binding(
                    "The App session binding, verified receipt, and exact plan descriptors are required.",
                    out error);

            try
            {
                var references = new List<F10V7C1MeasuredAssetReference>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (EncounterCatalogDescriptor descriptor in plan.UsedDescriptors)
                {
                    if (descriptor == null || descriptor.Revision != 1 ||
                        !seen.Add(F10V7C1DescriptorKey(descriptor.DescriptorId, descriptor.Revision)) ||
                        !F10PhysicalDescriptorTableV1.TryGetDescriptor(descriptor.DescriptorId,
                            out F10PhysicalDescriptorV1 fixedDescriptor) ||
                        !F10PhysicalDescriptorTableV1.TryComputeFingerprint(
                            descriptor.DescriptorId, descriptor.Revision,
                            inputs.PhysicalDescriptorPayloadSha256, inputs.BundleManifestSha256,
                            out F10PhysicalDescriptorFingerprintResultV1 fingerprint) ||
                        !string.Equals(descriptor.Sha256, fingerprint.Fingerprint,
                            StringComparison.Ordinal) ||
                        !IsKindMatch(fixedDescriptor.Kind, descriptor.Kind))
                        return FailF10V7C1Binding(
                            "A C1 plan descriptor does not match the exact fixed-table receipt fingerprint.",
                            out error);

                    if (descriptor.Kind == EncounterCatalogDescriptorKind.Room)
                    {
                        if (string.IsNullOrWhiteSpace(fixedDescriptor.SourceDefinitionAssetPath) ||
                            !TryCreateF10V7C1MeasuredReference(inputs, descriptor, fixedDescriptor,
                                fingerprint.Fingerprint, F10V7C1MeasuredAssetRole.RoomPrefab,
                                fixedDescriptor.RootAssetPath, typeof(GameObject),
                                out F10V7C1MeasuredAssetReference prefabReference, out error) ||
                            !TryCreateF10V7C1MeasuredReference(inputs, descriptor, fixedDescriptor,
                                fingerprint.Fingerprint, F10V7C1MeasuredAssetRole.RoomSource,
                                fixedDescriptor.SourceDefinitionAssetPath, typeof(TextAsset),
                                out F10V7C1MeasuredAssetReference sourceReference, out error)) return false;
                        references.Add(prefabReference);
                        references.Add(sourceReference);
                    }
                    else if (descriptor.Kind == EncounterCatalogDescriptorKind.Enemy)
                    {
                        if (!TryCreateF10V7C1MeasuredReference(inputs, descriptor, fixedDescriptor,
                                fingerprint.Fingerprint, F10V7C1MeasuredAssetRole.EnemyPrefab,
                                fixedDescriptor.RootAssetPath, typeof(GameObject),
                                out F10V7C1MeasuredAssetReference enemyReference, out error)) return false;
                        references.Add(enemyReference);
                    }
                    else
                        return FailF10V7C1Binding(
                            "The exact C1 runtime adapter accepts only its pinned room and enemy descriptor roles.",
                            out error);
                }

                var candidate = new F10V7C1MeasuredAssetSnapshot(
                    F10V7C1MeasuredAssetFactoryKey, this, attachment.SessionBinding,
                    attachment.Revision, attachment.Scope, attachment.Room, plan,
                    inputs.PhysicalDescriptorPayloadSha256, inputs.BundleManifestSha256,
                    references);
                if (!candidate.IsBoundTo(this, attachment.SessionBinding, attachment.Revision,
                        attachment.Scope, attachment.Room, plan) ||
                    !candidate.HasExactDescriptors(plan))
                    return FailF10V7C1Binding(
                        "The App-issued measured references do not cover the exact pinned C1 descriptor roles.",
                        out error);

                snapshot = candidate;
                return true;
            }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The private C1 measured-asset issuer failed closed: " +
                    exception.GetType().Name + ".", out error);
            }
        }

        private static bool TryCreateF10V7C1MeasuredReference(
            F10V7VerifiedPhysicalInputs inputs, EncounterCatalogDescriptor descriptor,
            F10PhysicalDescriptorV1 fixedDescriptor, string descriptorFingerprint,
            F10V7C1MeasuredAssetRole role, string canonicalAssetPath, Type expectedAssetType,
            out F10V7C1MeasuredAssetReference reference, out string error)
        {
            reference = null;
            error = string.Empty;
            if (inputs == null || descriptor == null || fixedDescriptor == null ||
                string.IsNullOrWhiteSpace(canonicalAssetPath) || expectedAssetType == null)
                return FailF10V7C1Binding(
                    "A canonical fixed-table C1 asset path and receipt reference are required.",
                    out error);

            F10V7VerifiedPhysicalInputs.AssetReference measured;
            try { measured = inputs.RequireAsset(canonicalAssetPath); }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The exact receipt has no persistent asset for the fixed-table path " +
                    canonicalAssetPath + ": " + exception.GetType().Name + ".", out error);
            }

            bool source = role == F10V7C1MeasuredAssetRole.RoomSource;
            IReadOnlyList<F10V7VerifiedPhysicalInputs.AssetReference> measuredSet = source
                ? inputs.SourceAssets : inputs.PrefabAssets;
            if (measured == null || !string.Equals(measured.AssetPath, canonicalAssetPath,
                    StringComparison.Ordinal) || measured.Asset == null ||
                !expectedAssetType.IsInstanceOfType(measured.Asset) ||
                string.IsNullOrWhiteSpace(measured.Guid) ||
                !IsLowerF10V7C1Sha256(measured.AssetSha256) ||
                !IsLowerF10V7C1Sha256(measured.MetaSha256) || measuredSet == null ||
                !ContainsExactAssetReference(measuredSet, measured) ||
                !ContainsExactAssetReference(inputs.PersistentAssets, measured))
                return FailF10V7C1Binding(
                    "The exact receipt did not retain the fixed-path persistent Unity object, asset ID, and measured hashes.",
                    out error);

            reference = new F10V7C1MeasuredAssetReference(
                F10V7C1MeasuredAssetFactoryKey, descriptor.DescriptorId, descriptor.Revision,
                descriptor.Kind, role, descriptorFingerprint,
                inputs.PhysicalDescriptorPayloadSha256, canonicalAssetPath, measured.Guid,
                measured.LocalFileId.ToString(CultureInfo.InvariantCulture),
                measured.AssetSha256, measured.MetaSha256, measured.Asset);
            return true;
        }

        private static bool ContainsExactAssetReference(
            IReadOnlyList<F10V7VerifiedPhysicalInputs.AssetReference> assets,
            F10V7VerifiedPhysicalInputs.AssetReference expected)
        {
            if (assets == null || expected == null) return false;
            foreach (F10V7VerifiedPhysicalInputs.AssetReference asset in assets)
                if (ReferenceEquals(asset, expected)) return true;
            return false;
        }

        private static bool IsKindMatch(string fixedKind, EncounterCatalogDescriptorKind kind) =>
            string.Equals(fixedKind, "room", StringComparison.Ordinal)
                ? kind == EncounterCatalogDescriptorKind.Room
                : string.Equals(fixedKind, "enemy", StringComparison.Ordinal)
                    ? kind == EncounterCatalogDescriptorKind.Enemy
                    : string.Equals(fixedKind, "cache", StringComparison.Ordinal) &&
                        kind == EncounterCatalogDescriptorKind.Cache;

        private static bool AreSameF10V7C1Attachment(
            V7C1AttachmentView expected, V7C1AttachmentView current)
        {
            return expected != null && current != null &&
                ReferenceEquals(expected.SessionBinding, current.SessionBinding) &&
                ReferenceEquals(expected.Published, current.Published) &&
                ReferenceEquals(expected.Projection, current.Projection) &&
                ReferenceEquals(expected.Registration, current.Registration) &&
                ReferenceEquals(expected.Activation, current.Activation) &&
                ReferenceEquals(expected.Run, current.Run) &&
                ReferenceEquals(expected.Transitions, current.Transitions) &&
                ReferenceEquals(expected.Scope, current.Scope) &&
                ReferenceEquals(expected.Room, current.Room) &&
                ReferenceEquals(expected.Composition, current.Composition) &&
                expected.Revision == current.Revision &&
                string.Equals(expected.ConfirmedJson, current.ConfirmedJson, StringComparison.Ordinal);
        }

        private static bool AreEquivalentF10V7C1MeasuredSnapshots(
            F10V7C1MeasuredAssetSnapshot left, F10V7C1MeasuredAssetSnapshot right,
            ApplicationBootstrapper bootstrapper,
            F10V7SessionInitializationBinding sessionBinding, long revision,
            RoomLifetimeScope scope, RoomTemplatePrefab2D room,
            ResolvedEncounterPlanV1 plan)
        {
            if (left == null || right == null || plan?.UsedDescriptors == null ||
                !left.IsBoundTo(bootstrapper, sessionBinding, revision, scope, room, plan) ||
                !right.IsBoundTo(bootstrapper, sessionBinding, revision, scope, room, plan) ||
                !left.HasExactDescriptors(plan) || !right.HasExactDescriptors(plan) ||
                !string.Equals(left.PhysicalDescriptorPayloadSha256,
                    right.PhysicalDescriptorPayloadSha256, StringComparison.Ordinal) ||
                !string.Equals(left.BundleManifestSha256, right.BundleManifestSha256,
                    StringComparison.Ordinal)) return false;

            foreach (EncounterCatalogDescriptor descriptor in plan.UsedDescriptors)
            {
                if (descriptor == null) return false;
                if (descriptor.Kind == EncounterCatalogDescriptorKind.Room)
                {
                    if (!AreSameMeasuredReference(left, right, descriptor,
                            F10V7C1MeasuredAssetRole.RoomPrefab) ||
                        !AreSameMeasuredReference(left, right, descriptor,
                            F10V7C1MeasuredAssetRole.RoomSource)) return false;
                }
                else if (descriptor.Kind == EncounterCatalogDescriptorKind.Enemy)
                {
                    if (!AreSameMeasuredReference(left, right, descriptor,
                            F10V7C1MeasuredAssetRole.EnemyPrefab)) return false;
                }
                else return false;
            }
            return true;
        }

        private static bool AreSameMeasuredReference(
            F10V7C1MeasuredAssetSnapshot left, F10V7C1MeasuredAssetSnapshot right,
            EncounterCatalogDescriptor descriptor, F10V7C1MeasuredAssetRole role)
        {
            if (!left.TryGet(descriptor.DescriptorId, descriptor.Revision, role,
                    out F10V7C1MeasuredAssetReference a) ||
                !right.TryGet(descriptor.DescriptorId, descriptor.Revision, role,
                    out F10V7C1MeasuredAssetReference b)) return false;
            return a.Kind == b.Kind && a.Role == b.Role && a.Revision == b.Revision &&
                string.Equals(a.DescriptorId, b.DescriptorId, StringComparison.Ordinal) &&
                string.Equals(a.DescriptorSha256, b.DescriptorSha256, StringComparison.Ordinal) &&
                string.Equals(a.FullDescriptorSha256, b.FullDescriptorSha256, StringComparison.Ordinal) &&
                string.Equals(a.AssetPath, b.AssetPath, StringComparison.Ordinal) &&
                string.Equals(a.Guid, b.Guid, StringComparison.Ordinal) &&
                string.Equals(a.LocalId, b.LocalId, StringComparison.Ordinal) &&
                string.Equals(a.AssetSha256, b.AssetSha256, StringComparison.Ordinal) &&
                string.Equals(a.MetaSha256, b.MetaSha256, StringComparison.Ordinal) &&
                ReferenceEquals(a.Asset, b.Asset);
        }
    }
}
#endif
