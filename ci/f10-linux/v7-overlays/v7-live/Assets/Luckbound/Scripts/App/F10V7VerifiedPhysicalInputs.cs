#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves.V7;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Rooms;
using UnityEditor;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>
    /// App-owned, editor-only result of the single F10 V7 physical-input verification path.
    /// Instances can only be produced by VerifyAndLoad after the current disposable copy,
    /// pinned bundle, dependency snapshot, descriptor, and AssetDatabase objects are checked.
    /// This is diagnostic provenance, not a product-registry or gameplay authorization.
    /// </summary>
    public sealed class F10V7VerifiedPhysicalInputs
    {
        public const string BundleRootRelativePath = "Logs/F10V7PhysicalExport-20261116/candidate/revisions";

        private const string ExpectedLiveCheckoutName = "Luckbound";
        private const string DisposableCopiesRootRelativePath = "Logs/F10DisposableArt-20261116/copies";
        private const string MarkerRelativePath = "Logs/F10DisposableArt-20261116/copy-certificate.json";
        private const string PreflightReceiptRelativePath = "Logs/F10DisposableArt-20261116/copy-preflight-receipt.json";
        private const string ExpectedUnityVersion = "6000.6.7f1";
        private const string ExpectedLinuxUnityVersion = "6000.6.10f1";
        private static readonly string[] UnityWriteRoots =
        {
            "Assets", "Packages", "ProjectSettings", "Library", "Temp", "UserSettings", "Logs"
        };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private const string RoutePath = "Assets/Resources/Luckbound/Data/Rooms/F10SampleRoute.json";
        private const string C08Path = "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json";
        private const string CustodianPath = "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json";
        private const string DiagnosticRoomRoot = "Assets/Luckbound/Editor/Diagnostic/F10";
        private const string PlayerPrefabPath = "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab";
        private const string CachePrefabPath = "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab";
        private const string SlimePrefabPath = "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab";
        private const string HuesoguardiaPrefabPath = "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab";
        private const string CustodianPrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab";
        private const string CustodianWavePrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab";
        private static readonly RoomSource[] Rooms =
        {
            new RoomSource("sample.tutorial", "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial,
                "F10TutorialRoom.json", "F10TutorialDiagnostic.prefab", false),
            new RoomSource("sample.patio.combat", "room.patio.c1", ExpeditionRouteNodeKind.Combat,
                "F10PatioCombatC1Room.json", "F10PatioCombatC1Diagnostic.prefab", false),
            new RoomSource("sample.patio.exploration", "room.patio.e1", ExpeditionRouteNodeKind.Exploration,
                "F10PatioExplorationE1Room.json", "F10PatioExplorationE1Diagnostic.prefab", false),
            new RoomSource("sample.patio.guardian", "room.patio.guardian", ExpeditionRouteNodeKind.Guardian,
                "F10PatioGuardianRoom.json", "F10PatioGuardianDiagnostic.prefab", false),
            new RoomSource("sample.safe_extraction", "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction,
                "F10SafeExtractionRoom.json", "F10SafeExtractionDiagnostic.prefab", true)
        };

        private readonly ReadOnlyCollection<DependencyReference> _dependencyClosure;
        private readonly ReadOnlyCollection<AssetReference> _persistentAssets;
        private readonly ReadOnlyCollection<AssetReference> _sourceAssets;
        private readonly ReadOnlyCollection<AssetReference> _prefabAssets;
        private readonly ReadOnlyCollection<string> _missingPhysicalEvidenceIds;
        private readonly Dictionary<string, AssetReference> _assetsByPath;

        private F10V7VerifiedPhysicalInputs(CopyVerification copy, string projectRoot,
            string bundleDirectory, string revisionId, string bundleManifestSha256,
            string physicalDescriptorPayloadSha256, string bundleManifestJson,
            string physicalDescriptorJson, DependencyReference[] dependencyClosure,
            AssetReference[] persistentAssets, AssetReference[] sourceAssets,
            AssetReference[] prefabAssets, string[] missingPhysicalEvidenceIds,
            bool physicalDescriptorInstallable, bool bundleInstallable,
            string exporterSourceSha256, string exporterMetaSha256)
        {
            ProjectRoot = projectRoot;
            CopySourceRoot = copy.SourceRoot;
            CopyMarkerPath = copy.MarkerPath;
            CopyIsolationChecks = copy.IsolationChecks;
            BundleDirectory = bundleDirectory;
            RevisionId = revisionId;
            BundleManifestSha256 = bundleManifestSha256;
            PhysicalDescriptorPayloadSha256 = physicalDescriptorPayloadSha256;
            BundleManifestJson = bundleManifestJson;
            PhysicalDescriptorJson = physicalDescriptorJson;
            _dependencyClosure = Array.AsReadOnly((DependencyReference[])dependencyClosure.Clone());
            _persistentAssets = Array.AsReadOnly((AssetReference[])persistentAssets.Clone());
            _sourceAssets = Array.AsReadOnly((AssetReference[])sourceAssets.Clone());
            _prefabAssets = Array.AsReadOnly((AssetReference[])prefabAssets.Clone());
            _missingPhysicalEvidenceIds = Array.AsReadOnly((string[])missingPhysicalEvidenceIds.Clone());
            PhysicalDescriptorInstallable = physicalDescriptorInstallable;
            BundleInstallable = bundleInstallable;
            ExporterSourceSha256 = exporterSourceSha256;
            ExporterMetaSha256 = exporterMetaSha256;
            _assetsByPath = _persistentAssets.ToDictionary(row => row.AssetPath, StringComparer.Ordinal);
        }

        public string ProjectRoot { get; }
        public string CopySourceRoot { get; }
        public string CopyMarkerPath { get; }
        public string CopyIsolationChecks { get; }
        public string BundleDirectory { get; }
        public string RevisionId { get; }
        public string BundleManifestSha256 { get; }
        public string PhysicalDescriptorPayloadSha256 { get; }
        public string BundleManifestJson { get; }
        public string PhysicalDescriptorJson { get; }
        public IReadOnlyList<DependencyReference> DependencyClosure => _dependencyClosure;
        public IReadOnlyList<AssetReference> PersistentAssets => _persistentAssets;
        public IReadOnlyList<AssetReference> SourceAssets => _sourceAssets;
        public IReadOnlyList<AssetReference> PrefabAssets => _prefabAssets;
        public IReadOnlyList<string> MissingPhysicalEvidenceIds => _missingPhysicalEvidenceIds;
        public bool PhysicalDescriptorInstallable { get; }
        public bool BundleInstallable { get; }
        public string ExporterSourceSha256 { get; }
        public string ExporterMetaSha256 { get; }

        public AssetReference RequireAsset(string assetPath) =>
            _assetsByPath.TryGetValue(assetPath, out AssetReference value)
                ? value
                : throw new FileNotFoundException("No exact physical/source asset was retained for " + assetPath);

        /// <summary>Repeats the same sole verification path for this exact root, revision and pinned manifest.</summary>
        public F10V7VerifiedPhysicalInputs Revalidate() =>
            VerifyAndLoad(ProjectRoot, RevisionId, BundleManifestSha256);

        /// <summary>
        /// Verifies the current editor project is the exact named disposable copy, then verifies
        /// its pinned bundle, full recorded dependency closure, and required persistent Unity assets.
        /// </summary>
        public static F10V7VerifiedPhysicalInputs VerifyAndLoad(string projectRoot,
            string revisionId, string expectedManifestSha256)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            string root = CanonicalPath(projectRoot);
            CopyVerification copy = VerifyWorkspaceForAcceptance(root);
            string bundleDirectory = ResolveBundleDirectory(root, revisionId);
            if (!IsLowerSha256(expectedManifestSha256))
                throw new InvalidDataException("A pinned lowercase SHA-256 for bundle-manifest.json is required.");

            BundleManifestData manifest;
            byte[] manifestBytes;
            try
            {
                manifest = VerifyBundleIntegrityCore(bundleDirectory, expectedManifestSha256, out manifestBytes);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("The exact physical bundle did not verify: " +
                    exception.GetType().Name + ": " + exception.Message, exception);
            }

            string manifestJson = StrictUtf8.GetString(manifestBytes);
            if (manifest == null || manifest.schema != "luckbound.f10.v7.physical-bundle.v1" ||
                !string.Equals(manifest.revisionId, revisionId, StringComparison.Ordinal) ||
                manifest.rootAssetPaths == null || manifest.payloads == null || manifest.dependencies == null)
                throw new InvalidDataException("The pinned bundle manifest has an incomplete or mismatched shape.");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Application.platform == RuntimePlatform.LinuxEditor)
            {
                if (!string.Equals(Application.unityVersion, ExpectedLinuxUnityVersion, StringComparison.Ordinal) ||
                    !string.Equals(copy.V7RevisionId, revisionId, StringComparison.Ordinal) ||
                    !string.Equals(copy.V7BundleManifestSha256, expectedManifestSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("The V7 Linux opening receipt is not bound to the exact requested revision, manifest SHA, and 6.10 Editor.");
                VerifyLinuxBundlePackageDependencies(manifest);
            }

            F10PhysicalDescriptor descriptor = ReadPhysicalDescriptor(bundleDirectory, manifest,
                out string descriptorPayloadSha256, out string descriptorJson);
            ValidatePhysicalDescriptor(descriptor, manifest);
            VerifyProjectDependencySnapshot(root, manifest);
            string exporterSourceSha256 = RequirePayloadSha256(manifest,
                "Assets/Luckbound/Editor/F10TechnicalPhysicalDescriptorExporter.cs", "assembly-source");
            string exporterMetaSha256 = RequirePayloadSha256(manifest,
                "Assets/Luckbound/Editor/F10TechnicalPhysicalDescriptorExporter.cs.meta", "assembly-source-meta");

            var assetRows = new Dictionary<string, AssetReference>(StringComparer.Ordinal);
            foreach (string path in RequiredSourcePaths().Concat(RequiredPrefabPaths(descriptor)).Distinct(StringComparer.Ordinal))
                assetRows.Add(path, LoadMeasuredAsset(root, manifest, path));
            AssetReference[] sourceRows = RequiredSourcePaths().Select(path => assetRows[path])
                .OrderBy(row => row.AssetPath, StringComparer.Ordinal).ToArray();
            AssetReference[] prefabRows = assetRows.Values.Where(row => row.AssetPath.EndsWith(".prefab", StringComparison.Ordinal))
                .OrderBy(row => row.AssetPath, StringComparer.Ordinal).ToArray();
            DependencyReference[] closure = manifest.dependencies.Select(row => new DependencyReference(
                    row.assetPath, row.guid, row.assetSha256, row.metaSha256))
                .OrderBy(row => row.AssetPath, StringComparer.Ordinal).ToArray();

            return new F10V7VerifiedPhysicalInputs(copy, root, bundleDirectory, revisionId,
                expectedManifestSha256, descriptorPayloadSha256, manifestJson, descriptorJson,
                closure, assetRows.Values.OrderBy(row => row.AssetPath, StringComparer.Ordinal).ToArray(),
                sourceRows, prefabRows,
                descriptor.missingPhysicalDependencies.Where(row => row != null)
                    .Select(row => row.dependencyId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                descriptor.installable, manifest.installable, exporterSourceSha256, exporterMetaSha256);
        }

        /// <summary>Compatibility facade; validation and its false eligibility result live here only.</summary>
        public static bool ValidateBundleIntegrity(string bundleDirectory, string expectedManifestSha256,
            out bool registryEligible, out string error)
        {
            registryEligible = false;
            error = string.Empty;
            try
            {
                VerifyBundleIntegrityCore(bundleDirectory, expectedManifestSha256, out _);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        /// <summary>Captures only the V6-policy BuiltIn Editor package evidence needed by a Linux V7 bundle.</summary>
        public static object[] CaptureV7LinuxPackageDependencies(string[] dependencyAssetPaths) =>
            F10DiagnosticLinuxRuntimeEvidenceReader.CaptureV7PackageDependencies(dependencyAssetPaths);

        /// <summary>Compatibility facade for the exporter and acceptance harness copy gate.</summary>
        public static CopyVerification VerifyWorkspaceForAcceptance(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            string normalized = CanonicalPath(projectRoot);
            string currentRoot = CanonicalPath(Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName));
            if (!PathsEqual(normalized, currentRoot))
                throw new InvalidOperationException("Physical verification must use the exact Unity editor project containing the loaded AssetDatabase.");
            ApplicationBootstrapper.EnsureDiagnosticPathIsDisposable(normalized);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Application.platform == RuntimePlatform.LinuxEditor)
            {
                F10DiagnosticLinuxV7ExportEvidence evidence =
                    F10DiagnosticLinuxRuntimeEvidenceReader.ValidateForV7Opening(normalized);
                return new CopyVerification(evidence.Preflight.sourceCheckoutRoot, normalized,
                    F10DiagnosticLinuxRuntimeEvidenceReader.V7ExportReceiptRelativePath,
                    "V7 Linux preflight inputs, post-export inventory, exact delta, bundle revision, and statx identities revalidated.",
                    evidence.ExportReceipt.revisionId, evidence.ExportReceipt.bundleManifestSha256);
            }
            CopyVerification copy = VerifyDisposableCopy(normalized);
            VerifyUnityVersion(normalized);
            return copy;
        }

        /// <summary>Pre-export guard: Linux uses only its V7 measured input preflight; Windows retains the current certificate and Editor version.</summary>
        internal static CopyVerification VerifyV7ExportPreflight(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            string normalized = CanonicalPath(projectRoot);
            string currentRoot = CanonicalPath(Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName));
            if (!PathsEqual(normalized, currentRoot))
                throw new InvalidOperationException("Physical export must use the exact Unity editor project containing the loaded AssetDatabase.");
            ApplicationBootstrapper.EnsureDiagnosticPathIsDisposable(normalized);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Application.platform == RuntimePlatform.LinuxEditor)
            {
                F10DiagnosticLinuxV7ExportEvidence evidence =
                    F10DiagnosticLinuxRuntimeEvidenceReader.ValidateForV7Export(normalized);
                return new CopyVerification(evidence.Preflight.sourceCheckoutRoot, normalized,
                    F10DiagnosticLinuxRuntimeEvidenceReader.V7PreflightReceiptRelativePath,
                    "V7 Linux create-only preflight inventory, frozen V6 source, exact overlay union, and statx identities validated.");
            }
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Application.platform != RuntimePlatform.WindowsEditor)
                throw new PlatformNotSupportedException("V7 physical export supports the current Windows certificate path or its measured Linux 6.10 preflight.");
            CopyVerification copy = VerifyDisposableCopy(normalized);
            VerifyUnityVersion(normalized);
            return copy;
        }

        public static string ResolveBundleDirectory(string projectRoot, string revisionId)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !RevisionPattern.IsMatch(revisionId ?? string.Empty))
                throw new InvalidDataException("A physical-export revision ID in the exact exporter format is required.");
            string root = Path.GetFullPath(projectRoot);
            string allowedRoot = Path.GetFullPath(Path.Combine(root, BundleRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string result = Path.GetFullPath(Path.Combine(allowedRoot, "revision-" + revisionId));
            if (!IsEqualOrWithin(result, allowedRoot) || (IsEqualOrWithin(result, root) && PathsEqual(result, root)))
                throw new InvalidDataException("The selected physical revision escaped the exact export root.");
            return result;
        }

        private static string RoomJsonPath(RoomSource room) =>
            "Assets/Resources/Luckbound/Data/Rooms/" + room.JsonFileName;

        private static bool IsEqualOrWithin(string candidate, string root)
        {
            string fullCandidate = CanonicalPath(candidate);
            string fullRoot = CanonicalPath(root);
            return PathsEqual(fullCandidate, fullRoot) || fullCandidate.StartsWith(
                fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison);
        }

        private static void VerifyUnityVersion(string projectRoot)
        {
            string projectVersion = ReadProjectUnityVersion(projectRoot);
            if (!string.Equals(projectVersion, ExpectedUnityVersion, StringComparison.Ordinal) ||
                !string.Equals(Application.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Physical evidence requires project and Editor version " + ExpectedUnityVersion + ".");
        }

        private static BundleManifestData VerifyBundleIntegrityCore(string bundleDirectory,
            string expectedManifestSha256, out byte[] verifiedManifestBytes)
        {
            verifiedManifestBytes = null;
            if (string.IsNullOrWhiteSpace(expectedManifestSha256) || expectedManifestSha256.Length != 64 ||
                expectedManifestSha256.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("A pinned 64-character SHA-256 for bundle-manifest.json is required.");
            string root = CanonicalPath(bundleDirectory);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Physical bundle directory does not exist.");
            RejectReparsePointsFromVolumeRoot(root);
            string manifestPath = Path.Combine(root, "bundle-manifest.json");
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            if (!string.Equals(Sha256Hex(manifestBytes), expectedManifestSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Bundle manifest bytes do not match the pinned SHA-256.");
            BundleManifestData manifest = JsonUtility.FromJson<BundleManifestData>(StrictUtf8.GetString(manifestBytes));
            if (manifest == null || !string.Equals(manifest.schema, "luckbound.f10.v7.physical-bundle.v1", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifest.revisionId) || manifest.payloads == null || manifest.dependencies == null)
                throw new InvalidDataException("Physical bundle manifest has an unsupported or incomplete shape.");

            string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var payloadByPath = new Dictionary<string, BundlePayloadRecord>(StringComparer.Ordinal);
            foreach (BundlePayloadRecord record in manifest.payloads)
            {
                if (record == null || !IsSafeBundlePath(record.path) || payloadByPath.ContainsKey(record.path))
                    throw new InvalidDataException("Bundle manifest contains an invalid or duplicate payload path.");
                string full = Path.GetFullPath(Path.Combine(root, record.path.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(rootPrefix, PathComparison))
                    throw new InvalidDataException("Bundle payload escaped its revision directory: " + record.path);
                FileRecord measured = ReadStrictFileRecord(record.path, full);
                if (measured.Length != record.bytes || !string.Equals(measured.Sha256, record.sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("Bundle payload bytes changed: " + record.path);
                payloadByPath.Add(record.path, record);
            }

            string payloadRoot = Path.Combine(root, "payload");
            if (!Directory.Exists(payloadRoot)) throw new DirectoryNotFoundException("Bundle has no payload directory.");
            var actualPayloadPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in EnumerateFilesNoReparse(payloadRoot))
            {
                string relative = MakeProjectRelative(root, file);
                if (!IsSafeBundlePath(relative) || !payloadByPath.ContainsKey(relative))
                    throw new InvalidDataException("Bundle contains an unmanifested payload file: " + relative);
                actualPayloadPaths.Add(relative);
            }
            if (actualPayloadPaths.Count != payloadByPath.Count)
                throw new InvalidDataException("Bundle is missing one or more manifest-listed payload files.");

            var dependencyPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (BundleDependencyRecord dependency in manifest.dependencies)
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.assetPath) || string.IsNullOrWhiteSpace(dependency.guid) ||
                    !payloadByPath.TryGetValue(dependency.assetPayloadPath, out BundlePayloadRecord asset) ||
                    !payloadByPath.TryGetValue(dependency.metaPayloadPath, out BundlePayloadRecord meta) ||
                    !string.Equals(asset.sha256, dependency.assetSha256, StringComparison.Ordinal) ||
                    !string.Equals(meta.sha256, dependency.metaSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A dependency or exact asset/.meta payload is absent from the bundle: " + (dependency == null ? "<null>" : dependency.assetPath));
                dependencyPaths.Add(dependency.assetPath);
            }
            if (manifest.rootAssetPaths == null || manifest.rootAssetPaths.Length == 0 ||
                manifest.rootAssetPaths.Distinct(StringComparer.Ordinal).Count() != manifest.rootAssetPaths.Length ||
                manifest.rootAssetPaths.Any(path => !dependencyPaths.Contains(path)))
                throw new InvalidDataException("Bundle roots are not all represented by measured dependency bytes.");

            // Integrity is not physical authority. This verifier has no signed/runtime witness,
            // so JSON flags alone must never make the bundle registry-eligible.
            verifiedManifestBytes = (byte[])manifestBytes.Clone();
            return manifest;
        }

        private static F10PhysicalDescriptor ReadPhysicalDescriptor(string bundleDirectory,
            BundleManifestData manifest, out string payloadSha256, out string descriptorJson)
        {
            BundlePayloadRecord payload = manifest.payloads.SingleOrDefault(row => row != null &&
                row.path == "payload/evidence/physical-descriptor.json" && row.role == "physical-descriptor");
            if (payload == null || !IsLowerSha256(payload.sha256) || payload.bytes <= 0)
                throw new InvalidDataException("The bundle has no unique measured physical descriptor payload.");
            string path = Path.Combine(bundleDirectory, "payload/evidence/physical-descriptor.json");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != payload.bytes || !string.Equals(Sha256(bytes), payload.sha256, StringComparison.Ordinal))
                throw new InvalidDataException("The exact physical descriptor payload changed after export.");
            descriptorJson = StrictUtf8.GetString(bytes);
            F10PhysicalDescriptor descriptor = JsonUtility.FromJson<F10PhysicalDescriptor>(descriptorJson);
            if (descriptor == null || descriptor.schema != "luckbound.f10.v7.physical-export.descriptor.v1" ||
                descriptor.physicalPrefabs == null || descriptor.rooms == null || descriptor.missingPhysicalDependencies == null ||
                descriptor.assetRoots == null || descriptor.dependencies == null)
                throw new InvalidDataException("The physical descriptor is malformed or incomplete.");
            payloadSha256 = payload.sha256;
            return descriptor;
        }

        private static AssetReference LoadMeasuredAsset(string projectRoot,
            BundleManifestData manifest, string assetPath)
        {
            BundleDependencyRecord row = manifest.dependencies.SingleOrDefault(value =>
                value != null && string.Equals(value.assetPath, assetPath, StringComparison.Ordinal));
            if (row == null) throw new FileNotFoundException("The exact bundle has no GUID/hash row for " + assetPath);
            UnityEngine.Object asset = AssetDatabaseFacade.LoadMainAssetAtPath(assetPath);
            if (asset == null || !AssetDatabaseFacade.Contains(asset) ||
                !string.Equals(AssetDatabaseFacade.GetAssetPath(asset), assetPath, StringComparison.Ordinal))
                throw new FileNotFoundException("AssetDatabase did not materialize the exact physical/source asset: " + assetPath);
            if (!string.Equals(AssetDatabaseFacade.AssetPathToGUID(assetPath), row.guid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The imported asset GUID differs from the physical descriptor: " + assetPath);
            if (!AssetDatabaseFacade.TryGetGUIDAndLocalFileIdentifier(asset, out string objectGuid, out long localFileId) ||
                !string.Equals(objectGuid, row.guid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The imported persistent object GUID/local ID differs from the physical descriptor: " + assetPath);
            return new AssetReference(assetPath, row.guid, localFileId, row.assetSha256, row.metaSha256, asset);
        }

        private static string ReadMetaGuid(string path)
        {
            string text = StrictUtf8.GetString(File.ReadAllBytes(path));
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            return string.Empty;
        }

        private static string RequirePayloadSha256(BundleManifestData manifest,
            string sourcePath, string role)
        {
            BundlePayloadRecord[] rows = manifest.payloads.Where(row => row != null &&
                string.Equals(row.sourcePath, sourcePath, StringComparison.Ordinal) &&
                string.Equals(row.role, role, StringComparison.Ordinal)).ToArray();
            if (rows.Length != 1 || !IsLowerSha256(rows[0].sha256))
                throw new InvalidDataException("The physical bundle does not retain one exact payload for " + sourcePath);
            return rows[0].sha256;
        }

        private static void VerifyProjectDependencySnapshot(string projectRoot,
            BundleManifestData manifest)
        {
            bool verifyProjectPackages = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                Application.platform == RuntimePlatform.LinuxEditor;
            var externalPackagePaths = new HashSet<string>(StringComparer.Ordinal);
            if (verifyProjectPackages && manifest.packageDependencies != null)
                foreach (F10DiagnosticLinuxPackageDependency package in manifest.packageDependencies)
                    foreach (F10DiagnosticLinuxPackageDependencyFile file in package.files ?? Array.Empty<F10DiagnosticLinuxPackageDependencyFile>())
                        externalPackagePaths.Add(file.projectRelativePath);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BundleDependencyRecord dependency in manifest.dependencies)
            {
                if (dependency == null || !seen.Add(dependency.assetPath) || string.IsNullOrWhiteSpace(dependency.guid) ||
                    !IsLowerSha256(dependency.assetSha256) || !IsLowerSha256(dependency.metaSha256))
                    throw new InvalidDataException("The exact bundle dependency set contains an invalid or duplicate asset record.");
                bool isProjectAsset = dependency.assetPath.StartsWith("Assets/", StringComparison.Ordinal);
                bool isProjectPackage = verifyProjectPackages && dependency.assetPath.StartsWith("Packages/", StringComparison.Ordinal) &&
                    !externalPackagePaths.Contains(dependency.assetPath);
                if (!isProjectAsset && !isProjectPackage) continue;
                string fullAssetPath = Path.Combine(projectRoot, dependency.assetPath.Replace('/', Path.DirectorySeparatorChar));
                string fullMetaPath = fullAssetPath + ".meta";
                if (!File.Exists(fullAssetPath) || !File.Exists(fullMetaPath))
                    throw new FileNotFoundException("A measured dependency or its .meta is absent from the disposable copy: " + dependency.assetPath);
                if (!string.Equals(Sha256(File.ReadAllBytes(fullAssetPath)), dependency.assetSha256, StringComparison.Ordinal) ||
                    !string.Equals(Sha256(File.ReadAllBytes(fullMetaPath)), dependency.metaSha256, StringComparison.Ordinal))
                    throw new IOException("A disposable-copy dependency changed after physical export: " + dependency.assetPath);
                string guid = AssetDatabaseFacade.AssetPathToGUID(dependency.assetPath);
                if (string.IsNullOrWhiteSpace(guid) || !string.Equals(guid, dependency.guid, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadMetaGuid(fullMetaPath), dependency.guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("AssetDatabase GUID and exact .meta/bundle GUID disagree: " + dependency.assetPath);
            }
        }

        private static void VerifyLinuxBundlePackageDependencies(BundleManifestData manifest)
        {
            string[] packagePaths = manifest.dependencies.Where(row => row != null && row.assetPath != null &&
                    row.assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                .Select(row => row.assetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (manifest.packageDependencies == null)
                throw new InvalidDataException("The V7 bundle omitted the Linux BuiltIn package dependency allowlist and statx evidence.");
            var externalPackagePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxPackageDependency package in manifest.packageDependencies)
            {
                if (package == null || package.files == null)
                    throw new InvalidDataException("The V7 BuiltIn package evidence contains an incomplete package row.");
                foreach (F10DiagnosticLinuxPackageDependencyFile file in package.files)
                {
                    if (file == null || string.IsNullOrWhiteSpace(file.projectRelativePath) ||
                        !externalPackagePaths.Add(file.projectRelativePath))
                        throw new InvalidDataException("The V7 BuiltIn package evidence contains a missing or duplicate dependency path.");
                }
            }
            if (!externalPackagePaths.IsSubsetOf(new HashSet<string>(packagePaths, StringComparer.Ordinal)))
                throw new InvalidDataException("The V7 BuiltIn package evidence contains a dependency outside the bundle Packages/ closure.");
            var localPackagePaths = new HashSet<string>(packagePaths.Where(path => !externalPackagePaths.Contains(path)), StringComparer.Ordinal);
            F10DiagnosticLinuxRuntimeEvidenceReader.ValidateV7PackageDependencies(
                externalPackagePaths, manifest.packageDependencies);
            F10DiagnosticLinuxRuntimeEvidenceReader.ValidateV7LocalPackageDependencies(localPackagePaths);
            var packageFiles = manifest.packageDependencies
                .SelectMany(row => row.files ?? Array.Empty<F10DiagnosticLinuxPackageDependencyFile>())
                .ToDictionary(row => row.projectRelativePath, StringComparer.Ordinal);
            if (!new HashSet<string>(packageFiles.Keys, StringComparer.Ordinal).SetEquals(externalPackagePaths))
                throw new InvalidDataException("The V7 BuiltIn package evidence does not cover its exact external Packages/ dependency set.");
            foreach (BundleDependencyRecord dependency in manifest.dependencies.Where(row => row != null &&
                         row.assetPath != null && externalPackagePaths.Contains(row.assetPath)))
            {
                if (!packageFiles.TryGetValue(dependency.assetPath, out F10DiagnosticLinuxPackageDependencyFile file) ||
                    !string.Equals(file.sha256, dependency.assetSha256, StringComparison.Ordinal) ||
                    !string.Equals(file.metaSha256, dependency.metaSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("The V7 package statx/allowlist bytes differ from the exact bundled asset or .meta payload: " + dependency.assetPath);
            }
        }

        public sealed class CopyVerification
        {
            internal CopyVerification(string sourceRoot, string projectRoot, string markerPath, string isolationChecks,
                string v7RevisionId = null, string v7BundleManifestSha256 = null)
            {
                SourceRoot = sourceRoot;
                ProjectRoot = projectRoot;
                MarkerPath = markerPath;
                IsolationChecks = isolationChecks;
                V7RevisionId = v7RevisionId;
                V7BundleManifestSha256 = v7BundleManifestSha256;
            }
            public string SourceRoot { get; }
            public string ProjectRoot { get; }
            public string MarkerPath { get; }
            public string IsolationChecks { get; }
            internal string V7RevisionId { get; }
            internal string V7BundleManifestSha256 { get; }
            public object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "disposableProjectRoot", ProjectRoot }, { "identityChecks", IsolationChecks },
                { "markerPath", MarkerPath }, { "sourceProjectRoot", SourceRoot }
            };
        }

        public sealed class AssetReference
        {
            internal AssetReference(string assetPath, string guid, long localFileId,
                string assetSha256, string metaSha256, UnityEngine.Object asset)
            { AssetPath = assetPath; Guid = guid; LocalFileId = localFileId; AssetSha256 = assetSha256; MetaSha256 = metaSha256; Asset = asset; }
            public string AssetPath { get; }
            public string Guid { get; }
            public long LocalFileId { get; }
            public string AssetSha256 { get; }
            public string MetaSha256 { get; }
            public UnityEngine.Object Asset { get; }
        }

        public sealed class DependencyReference
        {
            internal DependencyReference(string assetPath, string guid, string assetSha256, string metaSha256)
            { AssetPath = assetPath; Guid = guid; AssetSha256 = assetSha256; MetaSha256 = metaSha256; }
            public string AssetPath { get; }
            public string Guid { get; }
            public string AssetSha256 { get; }
            public string MetaSha256 { get; }
        }

        private static class AssetDatabaseFacade
        {
            internal static UnityEngine.Object LoadMainAssetAtPath(string path) => AssetDatabase.LoadMainAssetAtPath(path);
            internal static bool Contains(UnityEngine.Object asset) => AssetDatabase.Contains(asset);
            internal static string GetAssetPath(UnityEngine.Object asset) => AssetDatabase.GetAssetPath(asset);
            internal static string AssetPathToGUID(string path) => AssetDatabase.AssetPathToGUID(path);
            internal static bool TryGetGUIDAndLocalFileIdentifier(UnityEngine.Object asset, out string guid, out long localFileId) =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localFileId);
        }

        [Serializable]
        private sealed class BundleManifestData
        {
            public string schema;
            public string revisionId;
            public bool installable;
            public string[] rootAssetPaths;
            public string[] missingDependencyIds;
            public BundlePayloadRecord[] payloads;
            public BundleDependencyRecord[] dependencies;
            public F10DiagnosticLinuxPackageDependency[] packageDependencies;
        }

        [Serializable]
        private sealed class BundlePayloadRecord
        {
            public string path;
            public string sourcePath;
            public string role;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class BundleDependencyRecord
        {
            public string assetPath;
            public string assetPayloadPath;
            public string metaPayloadPath;
            public string guid;
            public string assetSha256;
            public string metaSha256;
        }

        private static readonly Regex RevisionPattern = new Regex("^[0-9]{8}T[0-9]{13}Z-[0-9a-f]{32}$",
            RegexOptions.CultureInvariant);
        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static string CanonicalPath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static bool PathsEqual(string left, string right) => string.Equals(CanonicalPath(left), CanonicalPath(right), PathComparison);
        private static string ToLowerHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        private sealed class FileRecord
        {
            internal FileRecord(string path, long length, string sha256) { Path = path; Length = length; Sha256 = sha256; }
            internal string Path { get; }
            internal long Length { get; }
            internal string Sha256 { get; }
        }

private static CopyVerification VerifyDisposableCopy(string projectRoot)
        {
            EnsureWindows();
            string copyParent = CanonicalPath(Directory.GetParent(projectRoot).FullName);
            string sourceRoot = CanonicalPath(Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "..", "..")));
            string approvedCopiesRoot = CanonicalPath(Path.Combine(sourceRoot, DisposableCopiesRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(Path.GetFileName(sourceRoot), ExpectedLiveCheckoutName, StringComparison.Ordinal) ||
                !PathsEqual(copyParent, approvedCopiesRoot) || !IsApprovedCopyName(Path.GetFileName(projectRoot)))
                throw new InvalidOperationException("Export is available only from a named direct child disposable copy under the approved Logs copy root.");
            if (!Directory.Exists(sourceRoot) || !(File.Exists(Path.Combine(sourceRoot, ".git")) || Directory.Exists(Path.Combine(sourceRoot, ".git"))))
                throw new InvalidOperationException("The source checkout with its .git marker was not found above the approved Logs copy root.");
            if (File.Exists(Path.Combine(projectRoot, ".git")) || Directory.Exists(Path.Combine(projectRoot, ".git")))
                throw new InvalidOperationException("The disposable copy must not contain Git metadata.");

            RejectReparsePointsFromVolumeRoot(sourceRoot);
            RejectReparsePointsFromVolumeRoot(projectRoot);
            RejectReparsePointsFromVolumeRoot(approvedCopiesRoot);
            RejectReparsePoint(Path.Combine(sourceRoot, ".git"));
            foreach (string relativeRoot in UnityWriteRoots)
            {
                string sourcePath = Path.Combine(sourceRoot, relativeRoot);
                string copyPath = Path.Combine(projectRoot, relativeRoot);
                HashSet<string> sourceIds = Directory.Exists(sourcePath)
                    ? EnumeratePhysicalFileIdentities(sourcePath, rejectHardLinks: true,
                        excludedDirectory: string.Equals(relativeRoot, "Logs", StringComparison.Ordinal) ? projectRoot : null)
                    : new HashSet<string>(StringComparer.Ordinal);
                if (Directory.Exists(copyPath))
                {
                    HashSet<string> copyIds = EnumeratePhysicalFileIdentities(copyPath, rejectHardLinks: true);
                    string shared = copyIds.FirstOrDefault(sourceIds.Contains);
                    if (shared != null)
                        throw new InvalidOperationException("Disposable project shares physical file identity with the source checkout under " + relativeRoot + ".");
                }
            }

            string markerPath = Path.Combine(projectRoot, MarkerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(markerPath)) throw new FileNotFoundException("Disposable-copy identity marker is required.", markerPath);
            RejectReparsePoint(markerPath);
            byte[] markerBytes = File.ReadAllBytes(markerPath);
            DisposableCopyCertificate certificate = JsonUtility.FromJson<DisposableCopyCertificate>(StrictUtf8.GetString(markerBytes));
            if (certificate == null || certificate.schemaVersion != 1 ||
                !string.Equals(certificate.status, "verified", StringComparison.Ordinal) ||
                !PathsEqual(certificate.sourceRoot, sourceRoot) || !PathsEqual(certificate.copyRoot, projectRoot) ||
                !certificate.reparsePointsRejected || !certificate.fileIdentityChecksPassed || !certificate.f02ImportStagingAbsent ||
                certificate.files == null || certificate.files.Length == 0 || certificate.excluded == null ||
                !certificate.excluded.Contains(".git", StringComparer.Ordinal) ||
                !certificate.excluded.Contains("Library", StringComparer.Ordinal))
                throw new InvalidDataException("The copy certificate does not attest the exact approved source/copy roots and isolation checks.");
            var certificatePaths = new HashSet<string>(StringComparer.Ordinal);
            var certificateSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var certificateCopyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DisposableCopyFileRecord row in certificate.files)
            {
                if (row == null || !IsSafeCertificatePath(row.path) || !certificatePaths.Add(row.path) ||
                    !IsSha256(row.sha256) || string.IsNullOrWhiteSpace(row.sourceFileId) ||
                    string.IsNullOrWhiteSpace(row.copyFileId) || string.Equals(row.sourceFileId, row.copyFileId, StringComparison.Ordinal) ||
                    !certificateSourceIds.Add(row.sourceFileId) || !certificateCopyIds.Add(row.copyFileId))
                    throw new InvalidDataException("The copy certificate has an invalid, duplicate, or shared file-identity record.");
            }
            if (certificateSourceIds.Overlaps(certificateCopyIds))
                throw new InvalidDataException("The copy certificate contains shared source/copy physical file identities.");

            foreach (string requiredOverlayPath in new[]
            {
                "Assets/Luckbound/Editor/F10TechnicalPhysicalDescriptorExporter.cs",
                "Assets/Luckbound/Editor/F10TechnicalPhysicalDescriptorExporter.cs.meta",
                "Assets/Luckbound/Scripts/App/F10V7VerifiedPhysicalInputs.cs",
                "Assets/Luckbound/Scripts/App/F10V7VerifiedPhysicalInputs.cs.meta"
            })
            {
                DisposableCopyFileRecord row = certificate.files.FirstOrDefault(item => item.path == requiredOverlayPath);
                if (row == null)
                {
                    string requiredOwner = requiredOverlayPath.StartsWith("Assets/Luckbound/Scripts/App/F10V7VerifiedPhysicalInputs.cs", StringComparison.Ordinal)
                        ? "the App-owned verified-input verifier or its .meta"
                        : "the integrated V7 exporter or its .meta";
                    throw new InvalidDataException("The copy certificate predates " + requiredOwner + ": " + requiredOverlayPath);
                }
                string sourceFile = Path.Combine(sourceRoot, requiredOverlayPath.Replace('/', Path.DirectorySeparatorChar));
                string copyFile = Path.Combine(projectRoot, requiredOverlayPath.Replace('/', Path.DirectorySeparatorChar));
                FileRecord sourceBytes = ReadStrictFileRecord(requiredOverlayPath, sourceFile);
                FileRecord copyBytes = ReadStrictFileRecord(requiredOverlayPath, copyFile);
                PhysicalFileInformation sourceIdentity = ReadPhysicalFileInformation(sourceFile);
                PhysicalFileInformation copyIdentity = ReadPhysicalFileInformation(copyFile);
                if (!string.Equals(sourceBytes.Sha256, row.sha256, StringComparison.Ordinal) ||
                    !string.Equals(copyBytes.Sha256, row.sha256, StringComparison.Ordinal) ||
                    sourceIdentity.LinkCount != 1 || copyIdentity.LinkCount != 1 ||
                    string.Equals(sourceIdentity.Identity, copyIdentity.Identity, StringComparison.Ordinal))
                {
                    string copyRole = requiredOverlayPath.StartsWith("Assets/Luckbound/Scripts/App/F10V7VerifiedPhysicalInputs.cs", StringComparison.Ordinal)
                        ? "The copied App verifier or its .meta no longer matches the source certificate: "
                        : "The copied exporter or its .meta no longer matches the source certificate: ";
                    throw new InvalidDataException(copyRole + requiredOverlayPath);
                }
            }

            string receiptPath = Path.Combine(projectRoot, PreflightReceiptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(receiptPath))
                throw new FileNotFoundException("Coordinator-issued external preflight receipt is required before Unity can export.", receiptPath);
            RejectReparsePoint(receiptPath);
            byte[] receiptBytes = File.ReadAllBytes(receiptPath);
            CopyPreflightReceipt receipt = JsonUtility.FromJson<CopyPreflightReceipt>(StrictUtf8.GetString(receiptBytes));
            string certificateSha256 = Sha256Hex(markerBytes);
            if (receipt == null || receipt.schemaVersion != 1 ||
                !string.Equals(receipt.status, "verified-at-launch", StringComparison.Ordinal) ||
                !PathsEqual(receipt.sourceRoot, sourceRoot) || !PathsEqual(receipt.copyRoot, projectRoot) ||
                !string.Equals(receipt.certificateSha256, certificateSha256, StringComparison.Ordinal) ||
                !receipt.externalPreflightVerified || !receipt.writersExcluded || receipt.fileCount != certificate.files.Length ||
                !IsRecentReceiptTimestamp(receipt.issuedAtUtc))
                throw new InvalidDataException("External preflight receipt is missing, stale, mismatched, or does not attest writer exclusion.");

            return new CopyVerification(sourceRoot, projectRoot, MarkerRelativePath,
                "Approved nested copy path and matching coordinator receipt checked; source/copy write-root file identities, hard-link counts, Git exclusion and reparse points checked.");
        }

private static bool IsApprovedCopyName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length != 34 || !name.StartsWith("copy-", StringComparison.Ordinal) ||
                name[13] != 'T' || name[20] != 'Z' || name[21] != '-') return false;
            for (int index = 5; index < 13; index++) if (!char.IsDigit(name[index])) return false;
            for (int index = 14; index < 20; index++) if (!char.IsDigit(name[index])) return false;
            for (int index = 22; index < 34; index++) if (!Uri.IsHexDigit(name[index])) return false;
            return true;
        }

private static bool IsRecentReceiptTimestamp(string timestamp)
        {
            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime issuedAtUtc)) return false;
            TimeSpan age = DateTime.UtcNow - issuedAtUtc;
            return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(30);
        }

private static HashSet<string> EnumeratePhysicalFileIdentities(string root, bool rejectHardLinks, string excludedDirectory = null)
        {
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                RejectReparsePoint(directory);
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Disposable isolation rejects reparse points: " + entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!string.IsNullOrEmpty(excludedDirectory) && PathsEqual(entry, excludedDirectory)) continue;
                        pending.Push(entry);
                        continue;
                    }

                    PhysicalFileInformation information = ReadPhysicalFileInformation(entry);
                    if (rejectHardLinks && information.LinkCount != 1)
                        throw new InvalidOperationException("Disposable isolation rejects files with hard links: " + entry);
                    identities.Add(information.Identity);
                }
            }
            return identities;
        }

private static void RejectReparsePointsFromVolumeRoot(string path)
        {
            string full = CanonicalPath(path);
            string volumeRoot = Path.GetPathRoot(full);
            string relative = full.Substring(volumeRoot.Length);
            string current = volumeRoot;
            RejectReparsePoint(current);
            foreach (string segment in relative.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!File.Exists(current) && !Directory.Exists(current))
                    throw new DirectoryNotFoundException("Isolation path segment does not exist: " + current);
                RejectReparsePoint(current);
            }
        }

private static void RejectReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("Isolation path does not exist.", path);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Disposable isolation rejects reparse points: " + path);
        }

private static PhysicalFileInformation ReadPhysicalFileInformation(string path)
        {
            using (SafeFileHandle handle = CreateFile(path, 0, FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero, FileMode.Open, 0x02000000 | 0x00200000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("Could not read physical file identity: " + path, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                if (!GetFileInformationByHandle(handle, out ByHandleFileInformation info))
                    throw new IOException("Could not read physical file identity: " + path, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                string id = info.VolumeSerialNumber.ToString("X8", CultureInfo.InvariantCulture) + ":" +
                    info.FileIndexHigh.ToString("X8", CultureInfo.InvariantCulture) + info.FileIndexLow.ToString("X8", CultureInfo.InvariantCulture);
                return new PhysicalFileInformation(id, info.LinkCount);
            }
        }

private static void EnsureWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || Path.DirectorySeparatorChar != '\\')
                throw new PlatformNotSupportedException("Physical-copy identity verification is implemented for Windows only; export fails closed elsewhere.");
        }

private static string ReadProjectUnityVersion(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "ProjectSettings/ProjectVersion.txt");
            string text = StrictUtf8.GetString(File.ReadAllBytes(path));
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                const string prefix = "m_EditorVersion: ";
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line.Substring(prefix.Length).Trim();
            }
            throw new InvalidDataException("ProjectSettings/ProjectVersion.txt is missing m_EditorVersion.");
        }

private static FileRecord ReadStrictFileRecord(string logicalPath, string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(logicalPath)) throw new ArgumentException("A logical file path is required.", nameof(logicalPath));
            if (!File.Exists(absolutePath)) throw new FileNotFoundException("Required evidence dependency is missing.", absolutePath);
            RejectReparsePoint(absolutePath);
            byte[] bytes = File.ReadAllBytes(absolutePath);
            return new FileRecord(logicalPath.Replace('\\', '/'), bytes.LongLength, Sha256Hex(bytes));
        }

internal static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (SHA256 sha = SHA256.Create())
                return ToLowerHex(sha.ComputeHash(bytes));
        }

private static bool IsSafeBundlePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\\') >= 0 || Path.IsPathRooted(path) ||
                !path.StartsWith("payload/", StringComparison.Ordinal)) return false;
            return path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != ".." &&
                !segment.EndsWith(".", StringComparison.Ordinal) && !segment.EndsWith(" ", StringComparison.Ordinal) &&
                segment.All(character => character >= 0x20 && "<>:\"|?*".IndexOf(character) < 0));
        }

private static bool IsSafeCertificatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\\') >= 0 || Path.IsPathRooted(path)) return false;
            return path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != ".." &&
                segment.All(character => character >= 0x20 && "<>:\"|?*".IndexOf(character) < 0));
        }

private static bool IsSha256(string value) => !string.IsNullOrEmpty(value) && value.Length == 64 && value.All(Uri.IsHexDigit);

private static IEnumerable<string> EnumerateFilesNoReparse(string root)
        {
            if (!Directory.Exists(root)) yield break;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                RejectReparsePoint(directory);
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Physical inventory rejects reparse points: " + entry);
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else yield return entry;
                }
            }
        }

private static string MakeProjectRelative(string projectRoot, string absolutePath)
        {
            string prefix = CanonicalPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = CanonicalPath(absolutePath);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escaped the disposable project root: " + absolutePath);
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

private static bool IsLowerSha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

private static IEnumerable<string> RequiredSourcePaths()
        {
            yield return RoutePath;
            foreach (RoomSource room in Rooms) yield return RoomJsonPath(room);
            yield return C08Path;
            yield return CustodianPath;
        }

private static IEnumerable<string> RequiredPrefabPaths(F10PhysicalDescriptor descriptor)
        {
            foreach (RoomSource room in Rooms) yield return DiagnosticRoomRoot + "/" + room.PrefabFileName;
            yield return PlayerPrefabPath;
            yield return CachePrefabPath;
            yield return SlimePrefabPath;
            yield return HuesoguardiaPrefabPath;
            yield return CustodianPrefabPath;
            yield return CustodianWavePrefabPath;
            foreach (F10PhysicalPrefabRow projectile in descriptor.physicalPrefabs.Where(row => row != null &&
                         row.role != null && row.role.StartsWith("projectile.", StringComparison.Ordinal)))
                yield return projectile.assetPath;
        }

private static void ValidateRequiredRoots(IEnumerable<string> rootAssetPaths,
            IEnumerable<string> physicalPrefabPaths)
        {
            var roots = new HashSet<string>(rootAssetPaths ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string required in RequiredSourcePaths())
                if (!roots.Contains(required)) throw new FileNotFoundException("Physical bundle omitted required source root: " + required);
            foreach (string required in physicalPrefabPaths ?? Array.Empty<string>())
                if (!roots.Contains(required)) throw new FileNotFoundException("Physical bundle omitted required prefab root: " + required);
        }

private static void ValidatePhysicalDescriptor(F10PhysicalDescriptor descriptor,
            BundleManifestData manifest)
        {
            if (!string.Equals(descriptor.unityDependencyHashUse, "diagnostic-only", StringComparison.Ordinal))
                throw new InvalidDataException("Unity dependency hashes must remain diagnostic-only.");
            if (descriptor.installable || manifest.installable)
                throw new InvalidDataException("Diagnostic physical evidence must retain installable=false.");
            var roomRows = descriptor.rooms.Where(row => row != null).ToDictionary(row => row.prefabPath, StringComparer.Ordinal);
            var prefabRows = descriptor.physicalPrefabs.Where(row => row != null).ToDictionary(row => row.role, StringComparer.Ordinal);
            foreach (RoomSource source in Rooms)
            {
                string prefabPath = DiagnosticRoomRoot + "/" + source.PrefabFileName;
                if (!roomRows.TryGetValue(prefabPath, out F10PhysicalRoomRow row) || !row.validated ||
                    row.definition == null || !string.Equals(row.definition.templateId, source.TemplateId, StringComparison.Ordinal))
                    throw new InvalidDataException("The generated room prefab has no exact JSON-derived validation record: " + prefabPath);
            }

            RequirePhysicalPrefab(prefabRows, "c08.slime", SlimePrefabPath, "exclusiveAdapterIdentity");
            RequirePhysicalPrefab(prefabRows, "c08.huesoguardia", HuesoguardiaPrefabPath, "exclusiveAdapterIdentity");
            RequirePhysicalPrefab(prefabRows, "custodian.boss", CustodianPrefabPath, "exclusiveAdapterIdentity");
            RequirePhysicalPrefab(prefabRows, "custodian.wave", CustodianWavePrefabPath, "custodianWavePhysicalAdapter");
            RequirePhysicalPrefab(prefabRows, "player.f10", PlayerPrefabPath, "physicalPlayerComponentsMeasured");
            RequirePhysicalPrefab(prefabRows, "cache.exploration", CachePrefabPath, "physicalCachePrefabMeasured");

            string[] projectilePaths = descriptor.physicalPrefabs.Where(row => row != null &&
                    !string.IsNullOrWhiteSpace(row.role) && row.role.StartsWith("projectile.", StringComparison.Ordinal))
                .Select(row => row.assetPath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
            if (projectilePaths.Length == 0)
                throw new FileNotFoundException("No materialized projectile prefab and GUID/hash record exists; no projectile was inferred.");
            foreach (F10PhysicalPrefabRow projectile in descriptor.physicalPrefabs.Where(row => row != null &&
                         row.role != null && row.role.StartsWith("projectile.", StringComparison.Ordinal)))
                if (!projectile.projectilePhysicalComponentsMeasured || !projectile.hasExpectedAdapterComponent)
                    throw new InvalidDataException("A projectile prefab lacks its measured physical components: " + projectile.assetPath);

            string[] expectedAssetPaths = RequiredSourcePaths()
                .Concat(Rooms.Select(room => DiagnosticRoomRoot + "/" + room.PrefabFileName))
                .Concat(new[] { PlayerPrefabPath, CachePrefabPath, SlimePrefabPath, HuesoguardiaPrefabPath,
                    CustodianPrefabPath, CustodianWavePrefabPath })
                .Concat(projectilePaths).Distinct(StringComparer.Ordinal).ToArray();
            ValidateRequiredRoots(manifest.rootAssetPaths, expectedAssetPaths);

            var missingIds = new HashSet<string>(descriptor.missingPhysicalDependencies
                .Where(row => row != null).Select(row => row.dependencyId), StringComparer.Ordinal);
            if (missingIds.Contains("cache.exploration") || missingIds.Contains("projectile.prefab"))
                throw new FileNotFoundException("A required physical asset is reported missing by the exact descriptor.");
            // These remain non-installable observations. Their absence is retained in provenance;
            // this catalog does not claim runtime cache contact or Custodian confinement.
            if (!missingIds.Contains("cache.exploration.runtime") || !missingIds.Contains("custodian.confinement"))
                throw new InvalidDataException("The descriptor must preserve the unobserved cache and Custodian physical-evidence gaps.");

            var dependencyPaths = new HashSet<string>(manifest.dependencies.Where(row => row != null)
                .Select(row => row.assetPath), StringComparer.Ordinal);
            var rootRows = descriptor.assetRoots.Where(row => row != null).ToDictionary(row => row.rootPath, StringComparer.Ordinal);
            foreach (string path in expectedAssetPaths)
            {
                if (!dependencyPaths.Contains(path)) throw new FileNotFoundException("The bundle has no GUID/hash dependency record for " + path);
                F10PhysicalRootRow root;
                BundleDependencyRecord dependency = manifest.dependencies.SingleOrDefault(row =>
                    row != null && string.Equals(row.assetPath, path, StringComparison.Ordinal));
                if (!rootRows.TryGetValue(path, out root) || !string.Equals(root.assetPath, path, StringComparison.Ordinal) ||
                    dependency == null || string.IsNullOrWhiteSpace(root.guid) ||
                    !string.Equals(root.guid, dependency.guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The exact asset-root GUID differs between descriptor and bundle: " + path);
            }

            var expectedMissingEvidence = new HashSet<string>(new[]
                { "cache.exploration.runtime", "custodian.confinement" }, StringComparer.Ordinal);
            if (!missingIds.SetEquals(expectedMissingEvidence) || manifest.missingDependencyIds == null ||
                !new HashSet<string>(manifest.missingDependencyIds, StringComparer.Ordinal).SetEquals(expectedMissingEvidence))
                throw new InvalidDataException("The descriptor's physical-evidence gaps changed; preserve the unobserved cache/confinement state exactly.");
        }

private static void RequirePhysicalPrefab(Dictionary<string, F10PhysicalPrefabRow> rows,
            string role, string exactPath, string requiredMeasurement)
        {
            if (!rows.TryGetValue(role, out F10PhysicalPrefabRow row) ||
                !string.Equals(row.assetPath, exactPath, StringComparison.Ordinal) ||
                !row.hasExpectedAdapterComponent || !ReadMeasurement(row, requiredMeasurement))
                throw new FileNotFoundException("The exact physical prefab is missing or unmeasured for role " + role + ": " + exactPath);
        }

private static bool ReadMeasurement(F10PhysicalPrefabRow row, string field)
        {
            switch (field)
            {
                case "exclusiveAdapterIdentity": return row.exclusiveAdapterIdentity;
                case "custodianWavePhysicalAdapter": return row.custodianWavePhysicalAdapter;
                case "physicalPlayerComponentsMeasured": return row.physicalPlayerComponentsMeasured;
                case "physicalCachePrefabMeasured": return row.physicalCachePrefabMeasured;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

private sealed class RoomSource
        {
            internal RoomSource(string nodeId, string templateId, ExpeditionRouteNodeKind nodeKind,
                string jsonFileName, string prefabFileName, bool isExtraction)
            { NodeId = nodeId; TemplateId = templateId; NodeKind = nodeKind; JsonFileName = jsonFileName; PrefabFileName = prefabFileName; IsExtraction = isExtraction; }
            internal string NodeId { get; }
            internal string TemplateId { get; }
            internal ExpeditionRouteNodeKind NodeKind { get; }
            internal string JsonFileName { get; }
            internal string PrefabFileName { get; }
            internal bool IsExtraction { get; }
        }

[Serializable] private sealed class F10PhysicalDescriptor
        {
            public string schema;
            public string revisionId;
            public bool installable;
            public string unityDependencyHashUse;
            public F10PhysicalRoomRow[] rooms;
            public F10PhysicalPrefabRow[] physicalPrefabs;
            public F10PhysicalMissingDependencyRow[] missingPhysicalDependencies;
            public F10PhysicalRootRow[] assetRoots;
            public F10PhysicalDependencyRow[] dependencies;
        }

[Serializable] private sealed class F10PhysicalRoomRow
        {
            public string prefabPath;
            public bool validated;
            public F10PhysicalRoomDefinitionRow definition;
        }

[Serializable] private sealed class F10PhysicalRoomDefinitionRow
        {
            public string templateId;
        }

[Serializable] private sealed class F10PhysicalPrefabRow
        {
            public string assetPath;
            public string role;
            public bool hasExpectedAdapterComponent;
            public bool exclusiveAdapterIdentity;
            public bool custodianWavePhysicalAdapter;
            public bool physicalPlayerComponentsMeasured;
            public bool physicalCachePrefabMeasured;
            public bool projectilePhysicalComponentsMeasured;
        }

[Serializable] private sealed class F10PhysicalMissingDependencyRow
        {
            public string dependencyId;
            public bool installable;
            public bool observed;
            public string reason;
        }

[Serializable] private sealed class F10PhysicalRootRow
        {
            public string assetPath;
            public string guid;
            public string rootPath;
        }

[Serializable] private sealed class F10PhysicalDependencyRow
        {
            public string assetPath;
            public string guid;
        }

private sealed class DisposableCopyCertificate
        {
            public int schemaVersion;
            public string status;
            public string sourceRoot;
            public string copyRoot;
            public bool reparsePointsRejected;
            public bool fileIdentityChecksPassed;
            public bool f02ImportStagingAbsent;
            public string[] excluded;
            public DisposableCopyFileRecord[] files;
        }

private sealed class DisposableCopyFileRecord
        {
            public string path;
            public string sha256;
            public string sourceFileId;
            public string copyFileId;
        }

private sealed class CopyPreflightReceipt
        {
            public int schemaVersion;
            public string status;
            public string sourceRoot;
            public string copyRoot;
            public string certificateSha256;
            public bool externalPreflightVerified;
            public bool writersExcluded;
            public string issuedAtUtc;
            public int fileCount;
        }

private sealed class PhysicalFileInformation
        {
            internal PhysicalFileInformation(string identity, uint linkCount) { Identity = identity; LinkCount = linkCount; }
            internal string Identity { get; }
            internal uint LinkCount { get; }
        }

private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint LinkCount;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, FileShare shareMode,
            IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

[DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);
    }
}
#endif
