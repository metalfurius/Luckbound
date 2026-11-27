#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Luckbound.App
{
    /// <summary>
    /// Fixed diagnostic identity and source paths for one measured physical descriptor.
    /// This row is lookup data only; it does not prove that the files or a bundle were verified.
    /// </summary>
    internal sealed class F10PhysicalDescriptorV1
    {
        internal F10PhysicalDescriptorV1(string descriptorId, string kind,
            string rootAssetPath, string sourceDefinitionAssetPath)
        {
            DescriptorId = descriptorId;
            Kind = kind;
            RootAssetPath = rootAssetPath;
            SourceDefinitionAssetPath = sourceDefinitionAssetPath;
        }

        internal string DescriptorId { get; }
        internal string Kind { get; }
        internal string RootAssetPath { get; }
        internal string SourceDefinitionAssetPath { get; }
    }

    /// <summary>
    /// A deterministic fingerprint result. It carries no verification or ingestion authority.
    /// </summary>
    internal sealed class F10PhysicalDescriptorFingerprintResultV1
    {
        internal F10PhysicalDescriptorFingerprintResultV1(F10PhysicalDescriptorV1 descriptor,
            int revision, string physicalDescriptorPayloadSha256, string bundleManifestSha256,
            string fingerprint)
        {
            Descriptor = descriptor;
            Revision = revision;
            PhysicalDescriptorPayloadSha256 = physicalDescriptorPayloadSha256;
            BundleManifestSha256 = bundleManifestSha256;
            Fingerprint = fingerprint;
        }

        internal F10PhysicalDescriptorV1 Descriptor { get; }
        internal int Revision { get; }
        internal string PhysicalDescriptorPayloadSha256 { get; }
        internal string BundleManifestSha256 { get; }
        internal string Fingerprint { get; }
    }

    /// <summary>
    /// Fixed F10 V7 diagnostic descriptor table. Paths and kinds are selected only by an exact
    /// descriptor ID; callers cannot substitute paths or kinds. The table and fingerprint are
    /// not evidence that an asset, payload, bundle, or provenance was verified.
    /// </summary>
    internal static class F10PhysicalDescriptorTableV1
    {
        private static readonly F10PhysicalDescriptorV1[] Rows =
        {
            Room("room.sample.tutorial", "F10TutorialDiagnostic.prefab", "F10TutorialRoom.json"),
            Room("room.patio.c1", "F10PatioCombatC1Diagnostic.prefab", "F10PatioCombatC1Room.json"),
            Room("room.patio.e1", "F10PatioExplorationE1Diagnostic.prefab", "F10PatioExplorationE1Room.json"),
            Room("room.patio.guardian", "F10PatioGuardianDiagnostic.prefab", "F10PatioGuardianRoom.json"),
            Room("room.sample.extraction", "F10SafeExtractionDiagnostic.prefab", "F10SafeExtractionRoom.json"),
            // C08Arena.json stays encounter-global in the bundle manifest; it is not an
            // individual enemy definition source.
            new F10PhysicalDescriptorV1("enemy.slime", "enemy",
                "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab", string.Empty),
            new F10PhysicalDescriptorV1("enemy.huesoguardia", "enemy",
                "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab", string.Empty),
            new F10PhysicalDescriptorV1("boss.custodian_of_the_oaks", "enemy",
                "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab",
                "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json"),
            new F10PhysicalDescriptorV1("cache.exploration", "cache",
                "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab",
                string.Empty)
        };

        private static readonly IReadOnlyList<F10PhysicalDescriptorV1> ReadOnlyRows = Array.AsReadOnly(Rows);

        internal static int DescriptorCount => Rows.Length;
        internal static IReadOnlyList<F10PhysicalDescriptorV1> Descriptors => ReadOnlyRows;

        /// <summary>
        /// Resolves the canonical kind and asset paths from the fixed table. Exact ordinal IDs
        /// are required. A successful lookup does not establish asset or bundle validity.
        /// </summary>
        internal static bool TryGetDescriptor(string descriptorId,
            out F10PhysicalDescriptorV1 descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(descriptorId)) return false;

            foreach (F10PhysicalDescriptorV1 row in Rows)
            {
                if (!string.Equals(row.DescriptorId, descriptorId, StringComparison.Ordinal)) continue;
                descriptor = row;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Derives kind and paths from the fixed descriptor ID, then uses the pure V1 fingerprint
        /// helper. The caller must first verify both supplied SHA-256 values against the exact
        /// physical payload and bundle manifest. This method only validates their format and
        /// combines them; it does not read files, validate a bundle, check provenance, or authorize
        /// physical ingestion.
        /// </summary>
        internal static bool TryComputeFingerprint(string descriptorId, int revision,
            string physicalDescriptorPayloadSha256, string bundleManifestSha256,
            out F10PhysicalDescriptorFingerprintResultV1 result)
        {
            result = null;
            if (!TryGetDescriptor(descriptorId, out F10PhysicalDescriptorV1 descriptor) ||
                !F10PhysicalDescriptorFingerprintV1.TryCompute(descriptor.DescriptorId,
                    descriptor.Kind, revision, descriptor.RootAssetPath,
                    descriptor.SourceDefinitionAssetPath, physicalDescriptorPayloadSha256,
                    bundleManifestSha256, out string fingerprint))
                return false;

            result = new F10PhysicalDescriptorFingerprintResultV1(descriptor, revision,
                physicalDescriptorPayloadSha256, bundleManifestSha256, fingerprint);
            return true;
        }

        private static F10PhysicalDescriptorV1 Room(string descriptorId, string prefabFileName,
            string sourceJsonFileName) => new F10PhysicalDescriptorV1(descriptorId, "room",
                "Assets/Luckbound/Editor/Diagnostic/F10/" + prefabFileName,
                "Assets/Resources/Luckbound/Data/Rooms/" + sourceJsonFileName);
    }
}
#endif
