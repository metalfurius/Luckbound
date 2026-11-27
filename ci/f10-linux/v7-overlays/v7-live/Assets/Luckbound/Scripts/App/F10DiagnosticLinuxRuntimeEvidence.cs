#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Luckbound.App
{
    [Serializable]
    internal sealed class F10DiagnosticLinuxRuntimeReceipt
    {
        public string schema;
        public int schemaVersion;
        public string status;
        public string unityVersion;
        public string baseOverlayManifestSha256;
        public string linuxOverlayManifestPath;
        public string linuxOverlayManifestSha256;
        public string snapshotSha256;
        public string sourceSnapshotSha256;
        public string projectRoot;
        public string baseSnapshotPath;
        public string sourceInventoryPath;
        public string sourceZipSha256;
        public string sourceInventorySha256;
        public string sourceZipRuntimePath;
        public string sourceInventoryRuntimePath;
        public string runnerTempRoot;
        public string sourceCheckoutRoot;
        public string workflowName;
        public string jobName;
        public string repository;
        public string runId;
        public string runAttempt;
        public string workflowRef;
        public string runnerImageOS;
        public string runnerImageVersion;
        public string copyInventorySha256;
        public F10DiagnosticLinuxInventoryFile[] copyInventory;
        public string materializationReceiptPath;
        public string materializationReceiptSha256;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxSourceInventory
    {
        public string schema;
        public F10DiagnosticLinuxSourceInventoryFile[] files;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxSourceInventoryFile
    {
        public string path;
        public string sha256;
        public long sizeBytes;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxInventoryFile
    {
        public string path;
        public string sha256;
        public long length;
        public uint deviceMajor;
        public uint deviceMinor;
        public long inode;
        public uint linkCount;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxMaterializationReceipt
    {
        public string schema;
        public int schemaVersion;
        public string status;
        public string projectRoot;
        public string unityVersion;
        public F10DiagnosticLinuxInventoryFile[] outputs;
        public F10DiagnosticLinuxInventoryFile[] generatedDirectoryMetas;
        public F10DiagnosticLinuxGeneratorFile[] generatorFiles;
        public F10DiagnosticLinuxDependencyClosure[] dependencyClosures;
        public string[] dependencyPaths;
        public F10DiagnosticLinuxInventoryFile[] dependencyFiles;
        public F10DiagnosticLinuxPackageDependency[] packageDependencies;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7PreflightReceipt
    {
        public string schema;
        public int schemaVersion;
        public string route;
        public string status;
        public string projectRoot;
        public string unityVersion;
        public string sourceZipPath;
        public string sourceZipSha256;
        public string sourceInventoryPath;
        public string sourceInventorySha256;
        public string sourceZipRuntimePath;
        public string sourceInventoryRuntimePath;
        public string sourceSnapshotSha256;
        public string overlayRootPath;
        public string overlayManifestPath;
        public string overlayManifestSha256;
        public string snapshotSha256;
        public string copyInventorySha256;
        public string runnerTempRoot;
        public string sourceCheckoutRoot;
        public string workflowName;
        public string jobName;
        public string repository;
        public string runId;
        public string runAttempt;
        public string workflowRef;
        public F10DiagnosticLinuxInventoryFile[] inputInventory;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7ExportReceipt
    {
        public string schema;
        public int schemaVersion;
        public string route;
        public string status;
        public string projectRoot;
        public string unityVersion;
        public string sourceZipSha256;
        public string sourceInventorySha256;
        public string sourceSnapshotSha256;
        public string overlayManifestPath;
        public string overlayManifestSha256;
        public string preflightReceiptPath;
        public string preflightReceiptSha256;
        public string inputSnapshotSha256;
        public string inputInventorySha256;
        public string finalInventorySha256;
        public string bundleRootRelativePath;
        public string revisionId;
        public string bundleManifestSha256;
        public F10DiagnosticLinuxInventoryFile[] finalInventory;
        public F10DiagnosticLinuxInventoryFile[] outputFiles;
        public string runnerTempRoot;
        public string sourceCheckoutRoot;
        public string workflowName;
        public string jobName;
        public string repository;
        public string runId;
        public string runAttempt;
        public string workflowRef;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7OverlayManifest
    {
        public string schema;
        public int schemaVersion;
        public string baseOverlayManifestSha256;
        public F10DiagnosticLinuxV7BaseSnapshot baseSnapshot;
        public F10DiagnosticLinuxV7DeletionRow[] deletions;
        public F10DiagnosticLinuxV7OverlayRow[] overlays;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7BaseSnapshot
    {
        public string path;
        public string sourceZipSha256;
        public string sourceInventoryPath;
        public string sourceInventorySha256;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7DeletionRow
    {
        public string destination;
        public string preimageSha256;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7OverlayRow
    {
        public string source;
        public string destination;
        public string sha256;
        public string preimageSha256;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxV7BundleManifestMarker
    {
        public string schema;
        public string revisionId;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxGeneratorFile
    {
        public string path;
        public string sha256;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxDependencyClosure
    {
        public string assetPath;
        public string[] dependencyPaths;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxPackageDependency
    {
        public string packageName;
        public string packageVersion;
        public string packageSource;
        public string packageId;
        public string registryUrl;
        public string packageAssetPath;
        public string resolvedRootKind;
        public string resolvedRootRelativePath;
        public string resolvedRootAbsolutePath;
        public string resolvedRootIdentity;
        public string lockEntrySource;
        public string lockEntryVersion;
        public string lockEntryUrl;
        public string lockEntryHash;
        public string lockFileSha256;
        public string[] dependencyAssetPaths;
        public F10DiagnosticLinuxPackageDependencyFile[] files;
    }

    [Serializable]
    internal sealed class F10DiagnosticLinuxPackageDependencyFile
    {
        public string relativePath;
        public string projectRelativePath;
        public string[] roles;
        public string[] assetPaths;
        public string sha256;
        public long length;
        public string identity;
        public uint linkCount;
        public string metaSha256;
        public long metaLength;
        public string metaIdentity;
        public uint metaLinkCount;
    }

    internal sealed class F10DiagnosticLinuxRuntimeEvidence
    {
        internal F10DiagnosticLinuxRuntimeEvidence(string projectRoot, string receiptSha256,
            F10DiagnosticLinuxRuntimeReceipt receipt, IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory)
        {
            ProjectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            ReceiptSha256 = receiptSha256 ?? throw new ArgumentNullException(nameof(receiptSha256));
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        internal string ProjectRoot { get; }
        internal string ReceiptSha256 { get; }
        internal F10DiagnosticLinuxRuntimeReceipt Receipt { get; }
        internal IDictionary<string, F10DiagnosticLinuxInventoryFile> Inventory { get; }
        internal string SnapshotSha256 => Receipt.snapshotSha256;
        internal string SourceSnapshotSha256 => Receipt.sourceSnapshotSha256;
        internal string CopyInventorySha256 => Receipt.copyInventorySha256;
        internal string MaterializationReceiptSha256 => Receipt.materializationReceiptSha256;
    }

    internal sealed class F10DiagnosticLinuxV7ExportEvidence
    {
        internal F10DiagnosticLinuxV7ExportEvidence(F10DiagnosticLinuxV7PreflightReceipt preflight,
            string preflightReceiptSha256, F10DiagnosticLinuxV7ExportReceipt exportReceipt,
            string exportReceiptSha256)
        {
            Preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
            PreflightReceiptSha256 = preflightReceiptSha256 ?? throw new ArgumentNullException(nameof(preflightReceiptSha256));
            ExportReceipt = exportReceipt;
            ExportReceiptSha256 = exportReceiptSha256;
        }

        internal F10DiagnosticLinuxV7PreflightReceipt Preflight { get; }
        internal string PreflightReceiptSha256 { get; }
        internal F10DiagnosticLinuxV7ExportReceipt ExportReceipt { get; }
        internal string ExportReceiptSha256 { get; }
        internal bool HasCompletedExport => ExportReceipt != null;
    }

    /// <summary>
    /// Reads the Linux-only, ephemeral CI evidence. GitHub context is correlated with the receipt,
    /// but does not authenticate the runner or replace the separate workflow trust review.
    /// The copy/inventory checks assume the one cooperative writer recorded by the wrapper; they
    /// are not a hostile-race defense and do not claim an operating-system lock.
    /// </summary>
    internal static class F10DiagnosticLinuxRuntimeEvidenceReader
    {
        internal const string ReceiptRelativePath = "Logs/F10V6Resume-20261120/ci-linux-diagnostic.json";
        internal const string MaterializationReceiptRelativePath = "Logs/F10V6Resume-20261120/materialization-linux-diagnostic.json";
        internal const string LinuxOverlayManifestRelativePath = "Logs/F10V6LinuxAcceptance-20261121/linux-overlay-manifest.json";
        private const string FrozenSourceZipRelativePath = "Logs/F10LinuxSourceSnapshot-20261127/source.zip";
        private const string FrozenSourceInventoryRelativePath = "Logs/F10LinuxSourceSnapshot-20261127/source-inventory.json";
        internal const string ExpectedUnityVersion = "6000.6.10f1";
        internal const string ExpectedWorkflowName = "F10 V6 Linux Diagnostic";
        internal const string ExpectedJobName = "f10-v6-linux-acceptance";
        internal const string V7PreflightReceiptRelativePath = "Logs/F10V7DiagnosticOpening-20261120/linux-v7-preflight.json";
        internal const string V7OverlayManifestRelativePath = "Logs/F10V7DiagnosticOpening-20261120/linux-v7-overlay-manifest.json";
        internal const string V7ExportReceiptRelativePath = "Logs/F10V7DiagnosticOpening-20261120/linux-v7-export-receipt.json";
        internal const string V7BundleRootRelativePath = "Logs/F10V7PhysicalExport-20261116/candidate/revisions";
        internal const string V7ManifestSha256EnvironmentVariable = "LUCKBOUND_F10_V7_LINUX_MANIFEST_SHA256";
        private const string V7ProjectRoot = "/tmp/luckbound-run/project";
        private const string V7ReadOnlyMountRoot = "/luckbound";
        private const string V7PreflightSchema = "luckbound.f10.v7.linux-preflight.v1";
        private const string V7ExportReceiptSchema = "luckbound.f10.v7.linux-export-receipt.v1";
        private const string V7OverlayManifestSchema = "luckbound.f10.v7.linux-overlay.v1";
        private const string V7BundleSchema = "luckbound.f10.v7.physical-bundle.v1";
        private const string V7ExpectedExportStatus = "technical-candidate";
        private static readonly string[] V7ExpectedDeletionDestinations =
        {
            "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs",
            "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs.meta",
            "Assets/Luckbound/Editor/F10DiagnosticMaterializationReceiptExporter.cs",
            "Assets/Luckbound/Editor/F10DiagnosticMaterializationReceiptExporter.cs.meta",
            "Assets/Luckbound/Scripts/App/F10DiagnosticMaterializationReceiptValidator.cs",
            "Assets/Luckbound/Scripts/App/F10DiagnosticMaterializationReceiptValidator.cs.meta",
            "Assets/Luckbound/Scripts/App/F10DiagnosticPackageDependencyEvidence.cs",
            "Assets/Luckbound/Scripts/App/F10DiagnosticPackageDependencyEvidence.cs.meta",
            "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs",
            "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs.meta",
            "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticMaterializationPackageDependencyTests.cs",
            "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticMaterializationPackageDependencyTests.cs.meta",
            "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticPackageDependencyEvidenceTests.cs",
            "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticPackageDependencyEvidenceTests.cs.meta",
            "Assets/Luckbound/Tests/PlayMode/F10V6ResumeAcceptanceTests.cs",
            "Assets/Luckbound/Tests/PlayMode/F10V6ResumeAcceptanceTests.cs.meta",
            "Assets/Luckbound/Tests/PlayMode/SaveRepositoryF10DiagnosticResumeConflictTests.cs",
            "Assets/Luckbound/Tests/PlayMode/SaveRepositoryF10DiagnosticResumeConflictTests.cs.meta"
        };
        internal const string BaseOverlayManifestSha256 = "d918873b7206f8091a4d286cb8e5be94be2d0c45d2df60de85e46cf9e390139c";
        private const string FrozenSourceZipSha256 = "0d710dc65065a1435897db61a9bee2124fe8ed1989e97c738b5ee15ea9b47f0e";
        private const string FrozenSourceInventorySha256 = "6aca3aa53a27dcba77893b24495041e16a47fb188d9cf727dbcaa2b16a4d99be";
        private const int FrozenSourceFileCount = 4953;
        private const string MaterializationSchema = "ci-linux-materialization-diagnostic";
        private const string SnapshotSchema = "ci-linux-diagnostic";
        private const string BuiltInUniversalPackageName = "com.unity.render-pipelines.universal";
        private const string PackageLockRelativePath = "Packages/packages-lock.json";
        private const string BuiltInUniversalRootRelativePath = "@editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal";
        private const int ReceiptSchemaVersion = 1;
        private const int AtFileDescriptorCurrentWorkingDirectory = -100;
        private const int StatxSyncType = 0;
        private const uint StatxBasicStats = 0x7ff;
        private const uint StatxRequiredMask = 0x305; // STATX_TYPE | STATX_NLINK | STATX_INO | STATX_SIZE
        private const ushort StatxRegularFileMode = 0x8000;
        private const ushort StatxDirectoryMode = 0x4000;
        private const ushort StatxFileTypeMask = 0xf000;
        private const int StatxBufferLength = 256;

        private static readonly string[] InventoryRootNames = { "Assets", "Packages", "ProjectSettings" };
        private static readonly string[] RequiredExternalInventoryPaths =
        {
            "docs/art/F01_bellator_32x48.png",
            "docs/art/F01_master_reference.md",
            "docs/art/records/ART-F01-BELLATOR-32X48.v1.json"
        };
        private static readonly string[] MaterializedOutputPaths =
        {
            "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10TutorialDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioCombatC1Diagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioExplorationE1Diagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioGuardianDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10SafeExtractionDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset",
            "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab"
        };
        private static readonly string[] GeneratorSourcePaths =
        {
            "Assets/Luckbound/Editor/F10DiagnosticSceneGenerator.cs",
            "Assets/Luckbound/Editor/F10TechnicalDiagnosticPrefabGenerator.cs",
            "Assets/Luckbound/Editor/F10RoomTemplatePrefabGenerator.cs",
            "Assets/Luckbound/Editor/F10TechnicalDiagnosticPlayerPrefabGenerator.cs",
            "Assets/Luckbound/Editor/F10PhysicalGoldCacheDiagnosticPrefabGenerator.cs",
            "Assets/Luckbound/Editor/F10DiagnosticLinuxMaterializationReceiptExporter.cs"
        };
        private static readonly string[] GeneratedDirectoryMetaPaths =
        {
            "Assets/Luckbound/Editor/Diagnostic/F10.meta",
            "Assets/Luckbound/Editor/Diagnostic/Shared.meta",
            "Assets/Luckbound/Editor/Diagnostic/F10GoldCache.meta"
        };
        private static readonly string[] V7GeneratedAssetPaths =
        {
            "Assets/Luckbound/Editor/Diagnostic/F10/F10TutorialDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioCombatC1Diagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioExplorationE1Diagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioGuardianDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10/F10SafeExtractionDiagnostic.prefab",
            "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset",
            "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab",
            "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab"
        };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Regex RevisionPattern = new Regex(
            @"^[0-9]{8}T[0-9]{13}Z-[0-9a-f]{32}$", RegexOptions.CultureInvariant);

        private sealed class ContentSnapshotFile
        {
            internal ContentSnapshotFile(string path, string sha256, long length)
            {
                Path = path;
                Sha256 = sha256;
                Length = length;
            }

            internal string Path { get; }
            internal string Sha256 { get; }
            internal long Length { get; }
        }

        internal static F10DiagnosticLinuxRuntimeEvidence ValidateForRuntime(string projectRoot,
            bool verifyWholeInventory = true)
        {
            EnsureLinuxRuntime();
            string root = CanonicalPath(projectRoot);
            EnsureExistingDirectoryAndAncestorsArePlain(root);
            ValidateRunnerContext(root);

            string receiptPath = ProjectPath(root, ReceiptRelativePath);
            EnsurePlainFileAndAncestors(receiptPath);
            string receiptSha256 = HashFile(receiptPath);
            F10DiagnosticLinuxRuntimeReceipt receipt = ReadJson<F10DiagnosticLinuxRuntimeReceipt>(receiptPath,
                "The fixed ci-linux-diagnostic receipt could not be parsed.");
            ValidateRuntimeReceipt(root, receipt);
            ValidateLinuxOverlayManifest(root, receipt);

            IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory = ValidateInventoryShape(receipt.copyInventory);
            string computedInventorySha256 = ComputeInventorySha256(receipt.copyInventory);
            if (!string.Equals(computedInventorySha256, receipt.copyInventorySha256, StringComparison.Ordinal))
                throw new InvalidDataException("The copy inventory digest does not match its actual receipt rows.");
            ValidateSnapshotDigests(receipt, inventory);
            if (verifyWholeInventory)
                VerifyWholeProjectInventory(root, inventory);

            ValidateMaterialization(root, receipt, inventory);
            return new F10DiagnosticLinuxRuntimeEvidence(root, receiptSha256, receipt, inventory);
        }

        /// <summary>
        /// Validates only the V7 Linux export preflight. This receipt is measured input correlation,
        /// not a V6 receipt, gameplay authorization, installable certificate, or review result.
        /// </summary>
        internal static F10DiagnosticLinuxV7ExportEvidence ValidateForV7Export(string projectRoot)
        {
            EnsureLinuxRuntime();
            string root = CanonicalPath(projectRoot);
            EnsureExistingDirectoryAndAncestorsArePlain(root);
            RejectV6ReceiptsAsV7Authority(root);
            F10DiagnosticLinuxV7ExportEvidence evidence = ReadAndValidateV7Preflight(root);
            IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory = ValidateInventoryShape(
                evidence.Preflight.inputInventory);
            VerifyWholeProjectInventory(root, inventory);
            ValidateV7ExportDestinationsAreAbsent(root);
            return evidence;
        }

        /// <summary>
        /// Revalidates the later V7 export receipt and exact generated delta. V6 runtime or
        /// materialization receipts are deliberately never read as authority for this path.
        /// </summary>
        internal static F10DiagnosticLinuxV7ExportEvidence ValidateForV7Opening(string projectRoot)
        {
            EnsureLinuxRuntime();
            string root = CanonicalPath(projectRoot);
            EnsureExistingDirectoryAndAncestorsArePlain(root);
            RejectV6ReceiptsAsV7Authority(root);
            F10DiagnosticLinuxV7ExportEvidence preflightEvidence = ReadAndValidateV7Preflight(root);
            F10DiagnosticLinuxV7PreflightReceipt preflight = preflightEvidence.Preflight;
            string receiptPath = ProjectPath(root, V7ExportReceiptRelativePath);
            EnsurePlainFileAndAncestors(receiptPath);
            if (InspectRegularFile(receiptPath).LinkCount != 1)
                throw new InvalidDataException("The create-only V7 export receipt must not be hard-linked.");
            string receiptSha256 = HashFile(receiptPath);
            F10DiagnosticLinuxV7ExportReceipt receipt = ReadJson<F10DiagnosticLinuxV7ExportReceipt>(receiptPath,
                "The V7 Linux export receipt could not be parsed.");
            if (receipt == null || !string.Equals(receipt.schema, V7ExportReceiptSchema, StringComparison.Ordinal) ||
                receipt.schemaVersion != 1 || !string.Equals(receipt.route, "v7-export", StringComparison.Ordinal) ||
                !string.Equals(receipt.status, V7ExpectedExportStatus, StringComparison.Ordinal) ||
                !PathEquals(receipt.projectRoot, root) || !string.Equals(receipt.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal) ||
                !string.Equals(receipt.sourceZipSha256, FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.sourceInventorySha256, FrozenSourceInventorySha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.sourceSnapshotSha256, preflight.sourceSnapshotSha256, StringComparison.Ordinal) ||
                !PathEquals(receipt.overlayManifestPath, V7OverlayManifestRelativePath) ||
                !string.Equals(receipt.overlayManifestSha256, preflight.overlayManifestSha256, StringComparison.Ordinal) ||
                !PathEquals(receipt.preflightReceiptPath, V7PreflightReceiptRelativePath) ||
                !string.Equals(receipt.preflightReceiptSha256, preflightEvidence.PreflightReceiptSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.inputSnapshotSha256, preflight.snapshotSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.inputInventorySha256, preflight.copyInventorySha256, StringComparison.Ordinal) ||
                !IsSha256(receipt.finalInventorySha256) || !IsSha256(receipt.bundleManifestSha256) ||
                !string.Equals(receipt.bundleRootRelativePath, V7BundleRootRelativePath, StringComparison.Ordinal) ||
                !RevisionPattern.IsMatch(receipt.revisionId ?? string.Empty))
                throw new InvalidDataException("The post-export V7 receipt is incomplete, stale, or bound to another preflight.");
            ValidateV7Context(preflight, root);
            ValidateV7Context(receipt, preflight);

            IDictionary<string, F10DiagnosticLinuxInventoryFile> before = ValidateInventoryShape(preflight.inputInventory);
            IDictionary<string, F10DiagnosticLinuxInventoryFile> after = ValidateInventoryShape(receipt.finalInventory);
            if (!string.Equals(ComputeInventorySha256(receipt.finalInventory), receipt.finalInventorySha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 final inventory digest does not match its measured rows.");
            VerifyWholeProjectInventory(root, after);

            HashSet<string> generatedPaths = V7GeneratedPaths();
            var addedPaths = new HashSet<string>(after.Keys.Except(before.Keys, StringComparer.Ordinal), StringComparer.Ordinal);
            if (!addedPaths.SetEquals(generatedPaths) || before.Keys.Except(after.Keys, StringComparer.Ordinal).Any())
                throw new InvalidDataException("The V7 post-export project delta differs from the exact eight-asset output allowlist.");
            foreach (KeyValuePair<string, F10DiagnosticLinuxInventoryFile> pair in before)
                if (!after.TryGetValue(pair.Key, out F10DiagnosticLinuxInventoryFile row) ||
                    !InventoryRowsEqual(pair.Value, row))
                    throw new InvalidDataException("The V7 exporter changed a frozen input's bytes or Linux physical identity: " + pair.Key);

            F10DiagnosticLinuxInventoryFile[] expectedOutputRows = generatedPaths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => after.TryGetValue(path, out F10DiagnosticLinuxInventoryFile row)
                    ? row : throw new InvalidDataException("The V7 final inventory omitted an allowlisted output: " + path))
                .ToArray();
            if (receipt.outputFiles == null || receipt.outputFiles.Length != expectedOutputRows.Length)
                throw new InvalidDataException("The V7 export receipt does not list exactly the generated assets and metadata.");
            var outputRows = new Dictionary<string, F10DiagnosticLinuxInventoryFile>(StringComparer.Ordinal);
            var outputIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxInventoryFile output in receipt.outputFiles)
            {
                string path = NormalizeRelativePath(output == null ? null : output.path);
                if (output == null || !generatedPaths.Contains(path) || !IsSha256(output.sha256) || output.length < 0 ||
                    output.inode <= 0 || output.linkCount != 1 ||
                    !outputIdentities.Add(PhysicalIdentityKey(output.deviceMajor, output.deviceMinor, output.inode)) ||
                    outputRows.ContainsKey(path))
                    throw new InvalidDataException("A V7 output receipt row is malformed, duplicated, linked, or outside the exact output allowlist: " + path);
                outputRows.Add(path, output);
            }
            if (!new HashSet<string>(outputRows.Keys, StringComparer.Ordinal).SetEquals(generatedPaths))
                throw new InvalidDataException("The V7 receipt output allowlist differs from the eight generated assets and fixed metadata.");
            foreach (F10DiagnosticLinuxInventoryFile row in expectedOutputRows)
                if (!outputRows.TryGetValue(row.path, out F10DiagnosticLinuxInventoryFile measured) ||
                    !InventoryRowsEqual(row, measured))
                    throw new InvalidDataException("The V7 receipt output bytes/identity differ from the post-export inventory: " + row.path);

            List<ContentSnapshotFile> inputRows = after.Values.Where(row => !generatedPaths.Contains(row.path))
                .Select(row => new ContentSnapshotFile(row.path, row.sha256, row.length)).ToList();
            if (!string.Equals(ComputeContentSnapshotSha256(inputRows), preflight.snapshotSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 output tree no longer reconstructs the exact pre-export input snapshot.");

            string revisionPath = ProjectPath(root, V7BundleRootRelativePath + "/revision-" + receipt.revisionId);
            EnsureExistingDirectoryAndAncestorsArePlain(revisionPath);
            ValidateOnlyOneV7BundleRevision(root, revisionPath, receipt.revisionId);
            string bundleManifestPath = Path.Combine(revisionPath, "bundle-manifest.json");
            EnsurePlainFileAndAncestors(bundleManifestPath);
            if (InspectRegularFile(bundleManifestPath).LinkCount != 1)
                throw new InvalidDataException("The measured V7 bundle manifest must not be hard-linked.");
            if (!string.Equals(HashFile(bundleManifestPath), receipt.bundleManifestSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 bundle-manifest bytes differ from the post-export receipt.");
            F10DiagnosticLinuxV7BundleManifestMarker marker = ReadJson<F10DiagnosticLinuxV7BundleManifestMarker>(
                bundleManifestPath, "The V7 bundle manifest could not be parsed.");
            if (marker == null || !string.Equals(marker.schema, V7BundleSchema, StringComparison.Ordinal) ||
                !string.Equals(marker.revisionId, receipt.revisionId, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 bundle manifest does not identify the exact receipt revision.");

            return new F10DiagnosticLinuxV7ExportEvidence(preflight, preflightEvidence.PreflightReceiptSha256,
                receipt, receiptSha256);
        }

        private static F10DiagnosticLinuxV7ExportEvidence ReadAndValidateV7Preflight(string root)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("LUCKBOUND_F10_LINUX_ROUTE"), "v7-export", StringComparison.Ordinal))
                throw new InvalidOperationException("The V7 Editor entry point requires the explicit v7-export supervisor route.");
            if (!PathEquals(root, V7ProjectRoot) ||
                !PathEquals(RequiredEnvironment("LUCKBOUND_F10_LINUX_PROJECT_ROOT"), V7ProjectRoot))
                throw new InvalidDataException("V7 Linux provenance is restricted to the fixed temporary project mount.");
            string currentRoot = CanonicalPath(Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName));
            if (!PathEquals(root, currentRoot))
                throw new InvalidDataException("V7 Linux provenance must use the exact project whose AssetDatabase is loaded.");
            string receiptPath = ProjectPath(root, V7PreflightReceiptRelativePath);
            EnsurePlainFileAndAncestors(receiptPath);
            if (InspectRegularFile(receiptPath).LinkCount != 1)
                throw new InvalidDataException("The create-only V7 preflight receipt must not be hard-linked.");
            string receiptSha256 = HashFile(receiptPath);
            F10DiagnosticLinuxV7PreflightReceipt receipt = ReadJson<F10DiagnosticLinuxV7PreflightReceipt>(receiptPath,
                "The create-only V7 Linux preflight receipt could not be parsed.");
            if (receipt == null || !string.Equals(receipt.schema, V7PreflightSchema, StringComparison.Ordinal) ||
                receipt.schemaVersion != 1 || !string.Equals(receipt.route, "v7-export", StringComparison.Ordinal) ||
                !string.Equals(receipt.status, "measured", StringComparison.Ordinal) || !PathEquals(receipt.projectRoot, root) ||
                !string.Equals(receipt.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal) ||
                !IsSha256(receipt.sourceZipSha256) || !string.Equals(receipt.sourceZipSha256, FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !IsSha256(receipt.sourceInventorySha256) || !string.Equals(receipt.sourceInventorySha256, FrozenSourceInventorySha256, StringComparison.Ordinal) ||
                !IsSha256(receipt.sourceSnapshotSha256) || !IsSha256(receipt.overlayManifestSha256) ||
                !IsSha256(receipt.snapshotSha256) || !IsSha256(receipt.copyInventorySha256) ||
                !PathEquals(receipt.sourceZipPath, FrozenSourceZipRelativePath) ||
                !PathEquals(receipt.sourceInventoryPath, FrozenSourceInventoryRelativePath) ||
                !PathEquals(receipt.overlayManifestPath, V7OverlayManifestRelativePath))
                throw new InvalidDataException("The V7 Linux preflight receipt has the wrong schema, route, source snapshot, or fixed paths.");
            if (!string.Equals(Environment.GetEnvironmentVariable(V7ManifestSha256EnvironmentVariable),
                    receipt.overlayManifestSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The preflight receipt does not match the exact V7 overlay-manifest SHA frozen for this CI invocation.");
            ValidateV7Context(receipt, root);

            string zipPath = CanonicalPath(receipt.sourceZipRuntimePath);
            string sourceInventoryPath = CanonicalPath(receipt.sourceInventoryRuntimePath);
            string overlayRoot = CanonicalPath(receipt.overlayRootPath);
            if (!IsPathEqualOrWithin(zipPath, V7ReadOnlyMountRoot) ||
                !IsPathEqualOrWithin(sourceInventoryPath, V7ReadOnlyMountRoot) ||
                !IsPathEqualOrWithin(overlayRoot, V7ReadOnlyMountRoot) ||
                PathsOverlap(zipPath, root) || PathsOverlap(sourceInventoryPath, root) || PathsOverlap(overlayRoot, root))
                throw new InvalidDataException("The frozen ZIP, inventory, and overlay sources must remain in the fixed read-only mount outside the project.");
            EnsurePlainFileAndAncestors(zipPath);
            EnsurePlainFileAndAncestors(sourceInventoryPath);
            EnsureExistingDirectoryAndAncestorsArePlain(overlayRoot);
            F10DiagnosticLinuxFileMetadata zipMetadata = InspectRegularFile(zipPath);
            F10DiagnosticLinuxFileMetadata inventoryMetadata = InspectRegularFile(sourceInventoryPath);
            if (zipMetadata.LinkCount != 1 || inventoryMetadata.LinkCount != 1 ||
                !string.Equals(HashFile(zipPath), FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !string.Equals(HashFile(sourceInventoryPath), FrozenSourceInventorySha256, StringComparison.Ordinal))
                throw new InvalidDataException("The frozen V6 source ZIP/inventory changed, is linked, or differs from the immutable base snapshot.");

            F10DiagnosticLinuxSourceInventory sourceInventory = ReadJson<F10DiagnosticLinuxSourceInventory>(
                sourceInventoryPath, "The frozen V6 source inventory could not be parsed for V7 overlay validation.");
            if (sourceInventory == null || !string.Equals(sourceInventory.schema, "luckbound.f10.logical-source.v1", StringComparison.Ordinal) ||
                sourceInventory.files == null || sourceInventory.files.Length != FrozenSourceFileCount)
                throw new InvalidDataException("The V7 overlay is not based on the exact frozen 4,953-file V6 source inventory.");
            var expected = new SortedDictionary<string, ContentSnapshotFile>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxSourceInventoryFile row in sourceInventory.files)
            {
                string path = NormalizeRelativePath(row == null ? null : row.path);
                if (!IsAllowedInventoryPath(path) || !IsSha256(row.sha256) || row.sizeBytes < 0 ||
                    expected.ContainsKey(path))
                    throw new InvalidDataException("The frozen V6 source inventory contains an invalid V7 base row: " + path);
                expected.Add(path, new ContentSnapshotFile(path, row.sha256, row.sizeBytes));
            }
            foreach (string requiredPath in RequiredExternalInventoryPaths)
                if (!expected.ContainsKey(requiredPath))
                    throw new InvalidDataException("The frozen source inventory is missing required F01 provenance input: " + requiredPath);
            if (!string.Equals(ComputeContentSnapshotSha256(expected.Values), receipt.sourceSnapshotSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 preflight source snapshot digest differs from the frozen V6 inventory.");

            string overlayManifestPath = ProjectPath(root, V7OverlayManifestRelativePath);
            EnsurePlainFileAndAncestors(overlayManifestPath);
            if (InspectRegularFile(overlayManifestPath).LinkCount != 1)
                throw new InvalidDataException("The root-frozen V7 overlay-manifest copy must not be hard-linked.");
            byte[] manifestBytes = File.ReadAllBytes(overlayManifestPath);
            if (!string.Equals(Sha256Bytes(manifestBytes), receipt.overlayManifestSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 in-project overlay-manifest bytes differ from the root-frozen invocation hash.");
            F10DiagnosticLinuxV7OverlayManifest manifest = ReadJson<F10DiagnosticLinuxV7OverlayManifest>(
                overlayManifestPath, "The V7 overlay manifest could not be parsed.");
            if (manifest == null || !string.Equals(manifest.schema, V7OverlayManifestSchema, StringComparison.Ordinal) ||
                manifest.schemaVersion != 1 || !string.Equals(manifest.baseOverlayManifestSha256, BaseOverlayManifestSha256, StringComparison.Ordinal) ||
                manifest.baseSnapshot == null || !string.Equals(manifest.baseSnapshot.path, FrozenSourceZipRelativePath, StringComparison.Ordinal) ||
                !string.Equals(manifest.baseSnapshot.sourceZipSha256, FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !string.Equals(manifest.baseSnapshot.sourceInventoryPath, FrozenSourceInventoryRelativePath, StringComparison.Ordinal) ||
                !string.Equals(manifest.baseSnapshot.sourceInventorySha256, FrozenSourceInventorySha256, StringComparison.Ordinal) ||
                manifest.deletions == null || manifest.deletions.Length != V7ExpectedDeletionDestinations.Length ||
                manifest.overlays == null || manifest.overlays.Length == 0)
                throw new InvalidDataException("The V7 overlay manifest does not identify the exact frozen V6 source and accepted d918 base.");

            var destinations = new HashSet<string>(StringComparer.Ordinal);
            var expectedDeletionDestinations = new HashSet<string>(V7ExpectedDeletionDestinations, StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxV7DeletionRow row in manifest.deletions)
            {
                string destination = NormalizeRelativePath(row == null ? null : row.destination);
                if (row == null || !expectedDeletionDestinations.Remove(destination) || !destinations.Add(destination) ||
                    !IsSha256(row.preimageSha256) || !expected.TryGetValue(destination, out ContentSnapshotFile preimage) ||
                    !string.Equals(preimage.Sha256, row.preimageSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A V7 deletion is not one of the exact frozen ZIP files with its required original preimage: " + destination);
                expected.Remove(destination);
            }
            if (expectedDeletionDestinations.Count != 0)
                throw new InvalidDataException("The V7 deletion manifest omitted one or more of the exact frozen ZIP paths.");

            HashSet<string> generatedAssetPaths = V7GeneratedPaths();
            foreach (F10DiagnosticLinuxV7OverlayRow row in manifest.overlays)
            {
                string destination = NormalizeRelativePath(row == null ? null : row.destination);
                string source = NormalizeRelativePath(row == null ? null : row.source);
                if (row == null ||
                    !(destination.StartsWith("Assets/", StringComparison.Ordinal) ||
                      string.Equals(destination, PackageLockRelativePath, StringComparison.Ordinal)) ||
                    !destinations.Add(destination) || !IsSha256(row.sha256) ||
                    (row.preimageSha256 != null && !IsSha256(row.preimageSha256)) || generatedAssetPaths.Contains(destination))
                    throw new InvalidDataException("The V7 overlay contains a malformed, repeated, deletion-overlapping, out-of-root, or generated-output destination: " + destination);
                string sourcePath = Path.GetFullPath(Path.Combine(overlayRoot,
                    source.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathEqualOrWithin(sourcePath, overlayRoot) || PathEquals(sourcePath, overlayRoot))
                    throw new InvalidDataException("A V7 overlay source escaped the fixed read-only overlay mount: " + source);
                EnsurePlainFileAndAncestors(sourcePath);
                F10DiagnosticLinuxFileMetadata sourceMetadata = InspectRegularFile(sourcePath);
                string sourceHash = HashFile(sourcePath);
                if (sourceMetadata.LinkCount != 1 || !string.Equals(sourceHash, row.sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A V7 overlay source changed after its manifest was frozen: " + source);
                if (expected.TryGetValue(destination, out ContentSnapshotFile preimage))
                {
                    if (row.preimageSha256 == null || !string.Equals(preimage.Sha256, row.preimageSha256, StringComparison.Ordinal))
                        throw new InvalidDataException("A V7 overlay preimage differs from the frozen source ZIP: " + destination);
                }
                else if (row.preimageSha256 != null)
                    throw new InvalidDataException("A V7 overlay declared a preimage for a path absent from the frozen source ZIP: " + destination);
                expected[destination] = new ContentSnapshotFile(destination, sourceHash, sourceMetadata.Length);
            }

            IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory = ValidateInventoryShape(receipt.inputInventory);
            if (!string.Equals(ComputeInventorySha256(receipt.inputInventory), receipt.copyInventorySha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 preflight physical inventory hash does not match its exact rows.");
            var actualContent = new SortedDictionary<string, ContentSnapshotFile>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, F10DiagnosticLinuxInventoryFile> row in inventory)
                actualContent.Add(row.Key, new ContentSnapshotFile(row.Key, row.Value.sha256, row.Value.length));
            if (!ContentSnapshotRowsEqual(expected, actualContent) ||
                !string.Equals(ComputeContentSnapshotSha256(actualContent.Values), receipt.snapshotSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The V7 preflight inventory is not the exact frozen V6 source plus the hashed V7 overlay union.");
            return new F10DiagnosticLinuxV7ExportEvidence(receipt, receiptSha256, null, null);
        }

        private static void RejectV6ReceiptsAsV7Authority(string projectRoot)
        {
            foreach (string relative in new[] { ReceiptRelativePath, MaterializationReceiptRelativePath })
            {
                string path = ProjectPath(projectRoot, relative);
                EnsurePlainAncestors(path);
                if (ExistsAsAnyEntry(path))
                    throw new InvalidDataException("A V6 Linux receipt is present; V6 receipt bytes cannot authorize V7 export or opening.");
            }
        }

        private static void ValidateV7Context(F10DiagnosticLinuxV7PreflightReceipt receipt, string projectRoot)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), "Linux", StringComparison.Ordinal) ||
                !string.Equals(Environment.GetEnvironmentVariable("GITHUB_WORKFLOW"), receipt.workflowName, StringComparison.Ordinal) ||
                !string.Equals(Environment.GetEnvironmentVariable("GITHUB_JOB"), receipt.jobName, StringComparison.Ordinal))
                throw new InvalidOperationException("V7 CI workflow/job fields must correlate with the running Linux context.");
            if (!PathEquals(receipt.runnerTempRoot, RequiredEnvironment("RUNNER_TEMP")) ||
                !PathEquals(receipt.sourceCheckoutRoot, RequiredEnvironment("GITHUB_WORKSPACE")) ||
                !PathEquals(receipt.sourceCheckoutRoot, Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")) ||
                PathsOverlap(projectRoot, receipt.sourceCheckoutRoot) ||
                !string.Equals(receipt.repository, RequiredEnvironment("GITHUB_REPOSITORY"), StringComparison.Ordinal) ||
                !string.Equals(receipt.runId, RequiredEnvironment("GITHUB_RUN_ID"), StringComparison.Ordinal) ||
                !string.Equals(receipt.runAttempt, RequiredEnvironment("GITHUB_RUN_ATTEMPT"), StringComparison.Ordinal) ||
                !string.Equals(receipt.workflowRef, RequiredEnvironment("GITHUB_WORKFLOW_REF"), StringComparison.Ordinal))
                throw new InvalidDataException("V7 CI metadata does not correlate with the exact runner, checkout, or temporary project.");
            RequireMeasuredProjectVersion(projectRoot, ExpectedUnityVersion);
        }

        private static void ValidateV7Context(F10DiagnosticLinuxV7ExportReceipt receipt,
            F10DiagnosticLinuxV7PreflightReceipt preflight)
        {
            if (!string.Equals(receipt.runnerTempRoot, preflight.runnerTempRoot, StringComparison.Ordinal) ||
                !string.Equals(receipt.sourceCheckoutRoot, preflight.sourceCheckoutRoot, StringComparison.Ordinal) ||
                !string.Equals(receipt.workflowName, preflight.workflowName, StringComparison.Ordinal) ||
                !string.Equals(receipt.jobName, preflight.jobName, StringComparison.Ordinal) ||
                !string.Equals(receipt.repository, preflight.repository, StringComparison.Ordinal) ||
                !string.Equals(receipt.runId, preflight.runId, StringComparison.Ordinal) ||
                !string.Equals(receipt.runAttempt, preflight.runAttempt, StringComparison.Ordinal) ||
                !string.Equals(receipt.workflowRef, preflight.workflowRef, StringComparison.Ordinal))
                throw new InvalidDataException("The post-export V7 receipt does not correlate with its create-only preflight context.");
        }

        private static void RequireMeasuredProjectVersion(string projectRoot, string expectedVersion)
        {
            if (!string.Equals(Application.unityVersion, expectedVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("V7 Linux evidence requires Editor " + expectedVersion + ".");
            string versionPath = ProjectPath(projectRoot, "ProjectSettings/ProjectVersion.txt");
            EnsurePlainFileAndAncestors(versionPath);
            string version = ReadProjectUnityVersion(versionPath);
            if (!string.Equals(version, expectedVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("V7 Linux evidence requires ProjectVersion.txt and Editor " + expectedVersion + ".");
        }

        private static bool ContentSnapshotRowsEqual(IDictionary<string, ContentSnapshotFile> left,
            IDictionary<string, ContentSnapshotFile> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            foreach (KeyValuePair<string, ContentSnapshotFile> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out ContentSnapshotFile other) || other == null ||
                    !string.Equals(pair.Value.Sha256, other.Sha256, StringComparison.Ordinal) ||
                    pair.Value.Length != other.Length) return false;
            }
            return true;
        }

        private static HashSet<string> V7GeneratedPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string asset in V7GeneratedAssetPaths)
            {
                paths.Add(asset);
                paths.Add(asset + ".meta");
            }
            foreach (string meta in GeneratedDirectoryMetaPaths) paths.Add(meta);
            return paths;
        }

        private static void ValidateV7ExportDestinationsAreAbsent(string projectRoot)
        {
            foreach (string path in V7GeneratedPaths())
            {
                string full = ProjectPath(projectRoot, path);
                EnsurePlainAncestors(full);
                if (ExistsAsAnyEntry(full))
                    throw new IOException("A V7 output path is already occupied; existing bytes were preserved: " + path);
            }
        }

        private static void ValidateOnlyOneV7BundleRevision(string projectRoot, string expectedRevisionDirectory,
            string revisionId)
        {
            string revisionsRoot = ProjectPath(projectRoot, V7BundleRootRelativePath);
            EnsureExistingDirectoryAndAncestorsArePlain(revisionsRoot);
            string[] entries = Directory.GetFileSystemEntries(revisionsRoot);
            if (entries.Length != 1 || !PathEquals(entries[0], expectedRevisionDirectory) ||
                (File.GetAttributes(entries[0]) & FileAttributes.Directory) == 0 ||
                !string.Equals(Path.GetFileName(entries[0]), "revision-" + revisionId, StringComparison.Ordinal))
                throw new InvalidDataException("V7 opening requires exactly one measured bundle revision in the fixed revisions root.");
            EnsureExistingDirectoryAndAncestorsArePlain(entries[0]);
        }

        private static string Sha256Bytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (SHA256 sha = SHA256.Create()) return ToLowerHex(sha.ComputeHash(bytes));
        }

        internal static F10DiagnosticLinuxFileMetadata InspectRegularFile(string path) =>
            InspectFileSystemEntry(path, requireDirectory: false);

        private static F10DiagnosticLinuxFileMetadata InspectDirectory(string path) =>
            InspectFileSystemEntry(path, requireDirectory: true);

        private static F10DiagnosticLinuxFileMetadata InspectFileSystemEntry(string path, bool requireDirectory)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A Linux file path is required.", nameof(path));
            if (requireDirectory) EnsurePlainDirectoryAndAncestors(path);
            else EnsurePlainFileAndAncestors(path);
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("The Linux diagnostic statx reader requires a little-endian runner.");

            byte[] buffer = new byte[StatxBufferLength];
            int result = Statx(AtFileDescriptorCurrentWorkingDirectory, Path.GetFullPath(path), StatxSyncType,
                StatxBasicStats, buffer);
            if (result != 0)
                throw new IOException("Could not measure the actual Linux statx identity for: " + path,
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));

            uint mask = BitConverter.ToUInt32(buffer, 0);
            if ((mask & StatxRequiredMask) != StatxRequiredMask)
                throw new IOException("Linux statx omitted inode/link-count data required for: " + path);
            ushort mode = BitConverter.ToUInt16(buffer, 28);
            ushort expectedMode = requireDirectory ? StatxDirectoryMode : StatxRegularFileMode;
            if ((mode & StatxFileTypeMask) != expectedMode)
                throw new IOException("A diagnostic entry has the wrong filesystem type: " + path);

            uint linkCount = BitConverter.ToUInt32(buffer, 16);
            uint deviceMajor = BitConverter.ToUInt32(buffer, 136);
            uint deviceMinor = BitConverter.ToUInt32(buffer, 140);
            long inode = checked((long)BitConverter.ToUInt64(buffer, 32));
            long length = checked((long)BitConverter.ToUInt64(buffer, 40));
            if (linkCount == 0 || inode <= 0 || length < 0)
                throw new IOException("Linux statx returned an invalid filesystem identity: " + path);
            return new F10DiagnosticLinuxFileMetadata(deviceMajor, deviceMinor, inode, length, linkCount);
        }

        private static void ValidateRuntimeReceipt(string projectRoot, F10DiagnosticLinuxRuntimeReceipt receipt)
        {
            if (receipt == null || !string.Equals(receipt.schema, SnapshotSchema, StringComparison.Ordinal) ||
                receipt.schemaVersion != ReceiptSchemaVersion || !string.Equals(receipt.status, "verified", StringComparison.Ordinal) ||
                !string.Equals(receipt.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal) ||
                !string.Equals(receipt.baseOverlayManifestSha256, BaseOverlayManifestSha256, StringComparison.Ordinal) ||
                !IsSha256(receipt.linuxOverlayManifestSha256) ||
                !IsSha256(receipt.snapshotSha256) || !IsSha256(receipt.sourceSnapshotSha256) ||
                string.Equals(receipt.snapshotSha256, receipt.sourceSnapshotSha256, StringComparison.Ordinal) ||
                !PathEquals(receipt.baseSnapshotPath, FrozenSourceZipRelativePath) ||
                !PathEquals(receipt.sourceInventoryPath, FrozenSourceInventoryRelativePath) ||
                !string.Equals(receipt.sourceZipSha256, FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.sourceInventorySha256, FrozenSourceInventorySha256, StringComparison.Ordinal) ||
                !IsSha256(receipt.copyInventorySha256) || !IsSha256(receipt.materializationReceiptSha256) ||
                !PathEquals(receipt.projectRoot, projectRoot) || !PathEquals(receipt.materializationReceiptPath,
                    MaterializationReceiptRelativePath) ||
                !PathEquals(receipt.linuxOverlayManifestPath, LinuxOverlayManifestRelativePath) ||
                !string.Equals(receipt.workflowName, ExpectedWorkflowName, StringComparison.Ordinal) ||
                !string.Equals(receipt.jobName, ExpectedJobName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(receipt.repository) || string.IsNullOrWhiteSpace(receipt.runId) ||
                string.IsNullOrWhiteSpace(receipt.runAttempt) || string.IsNullOrWhiteSpace(receipt.workflowRef))
                throw new InvalidDataException("The ci-linux-diagnostic receipt is incomplete or does not identify the fixed V6 Linux overlay.");

            if (!string.Equals(receipt.unityVersion, Application.unityVersion, StringComparison.Ordinal))
                throw new InvalidDataException("The running Editor version differs from the measured ci-linux-diagnostic receipt.");
            if (!string.Equals(receipt.runnerImageOS, "Linux", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(receipt.runnerImageVersion))
                throw new InvalidDataException("The ci-linux-diagnostic receipt has no measured Linux runner image identity.");
            string declaredImageVersion = Environment.GetEnvironmentVariable("ImageVersion");
            string measuredImageVersion = string.IsNullOrWhiteSpace(declaredImageVersion)
                ? ReadLinuxOsReleaseImageVersion()
                : declaredImageVersion;
            if (!string.Equals(receipt.runnerImageVersion, measuredImageVersion, StringComparison.Ordinal))
                throw new InvalidDataException("The running Linux image identity differs from the ci-linux-diagnostic receipt.");
            if (!PathEquals(receipt.runnerTempRoot, RequiredEnvironment("RUNNER_TEMP")) ||
                !PathEquals(receipt.sourceCheckoutRoot, RequiredEnvironment("GITHUB_WORKSPACE")))
                throw new InvalidDataException("The ci-linux-diagnostic receipt was measured from another runner or source checkout root.");
            string projectVersion = ReadProjectUnityVersion(ProjectPath(projectRoot, "ProjectSettings/ProjectVersion.txt"));
            if (!string.Equals(projectVersion, receipt.unityVersion, StringComparison.Ordinal))
                throw new InvalidDataException("ProjectVersion.txt differs from the measured ci-linux-diagnostic Editor version.");

            RequireEnvironmentEquals("GITHUB_WORKFLOW", receipt.workflowName);
            RequireEnvironmentEquals("GITHUB_JOB", receipt.jobName);
            RequireEnvironmentEquals("GITHUB_REPOSITORY", receipt.repository);
            RequireEnvironmentEquals("GITHUB_RUN_ID", receipt.runId);
            RequireEnvironmentEquals("GITHUB_RUN_ATTEMPT", receipt.runAttempt);
            RequireEnvironmentEquals("GITHUB_WORKFLOW_REF", receipt.workflowRef);
        }

        private static void ValidateLinuxOverlayManifest(string projectRoot, F10DiagnosticLinuxRuntimeReceipt receipt)
        {
            string path = ProjectPath(projectRoot, LinuxOverlayManifestRelativePath);
            EnsurePlainFileAndAncestors(path);
            if (!string.Equals(HashFile(path), receipt.linuxOverlayManifestSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The frozen Linux overlay manifest changed after the runner measured it.");
        }

        private static void ValidateSnapshotDigests(F10DiagnosticLinuxRuntimeReceipt receipt,
            IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory)
        {
            if (string.IsNullOrWhiteSpace(receipt.sourceZipRuntimePath) ||
                string.IsNullOrWhiteSpace(receipt.sourceInventoryRuntimePath) ||
                !Path.IsPathRooted(receipt.sourceZipRuntimePath) || !Path.IsPathRooted(receipt.sourceInventoryRuntimePath))
                throw new InvalidDataException("The mounted frozen source archive and inventory require absolute runtime paths.");
            string frozenZipPath = CanonicalPath(receipt.sourceZipRuntimePath);
            string inventoryPath = CanonicalPath(receipt.sourceInventoryRuntimePath);
            if (PathsOverlap(frozenZipPath, receipt.projectRoot) || PathsOverlap(inventoryPath, receipt.projectRoot))
                throw new InvalidDataException("The mounted frozen source artifacts overlap the temporary Linux diagnostic project.");
            EnsurePlainFileAndAncestors(frozenZipPath);
            EnsurePlainFileAndAncestors(inventoryPath);
            if (!string.Equals(HashFile(frozenZipPath), FrozenSourceZipSha256, StringComparison.Ordinal) ||
                !string.Equals(HashFile(inventoryPath), FrozenSourceInventorySha256, StringComparison.Ordinal))
                throw new InvalidDataException("The frozen Nov 20 source archive or inventory bytes differ from their pinned SHA-256.");

            F10DiagnosticLinuxSourceInventory sourceInventory = ReadJson<F10DiagnosticLinuxSourceInventory>(inventoryPath,
                "The frozen source inventory could not be parsed.");
            if (!string.Equals(sourceInventory.schema, "luckbound.f10.logical-source.v1", StringComparison.Ordinal) ||
                sourceInventory.files == null || sourceInventory.files.Length != FrozenSourceFileCount)
                throw new InvalidDataException("The frozen source inventory does not match the exact 4,953-file base contract.");
            var sourceRows = new List<ContentSnapshotFile>(sourceInventory.files.Length);
            var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxSourceInventoryFile row in sourceInventory.files)
            {
                string path = NormalizeRelativePath(row == null ? null : row.path);
                if (!IsAllowedInventoryPath(path) || !IsSha256(row.sha256) || row.sizeBytes < 0 ||
                    !sourcePaths.Add(path))
                    throw new InvalidDataException("A frozen source inventory row is malformed, duplicated, or outside its approved roots: " + path);
                sourceRows.Add(new ContentSnapshotFile(path, row.sha256, row.sizeBytes));
            }
            foreach (string requiredExternalPath in RequiredExternalInventoryPaths)
                if (!sourcePaths.Contains(requiredExternalPath))
                    throw new InvalidDataException("The frozen source inventory is missing an F01 source input: " + requiredExternalPath);
            string measuredSourceSnapshotSha256 = ComputeContentSnapshotSha256(sourceRows);
            if (!string.Equals(measuredSourceSnapshotSha256, receipt.sourceSnapshotSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The frozen source inventory content differs from sourceSnapshotSha256.");

            var generatedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string output in MaterializedOutputPaths)
            {
                generatedPaths.Add(output);
                generatedPaths.Add(output + ".meta");
            }
            foreach (string directoryMeta in GeneratedDirectoryMetaPaths)
                generatedPaths.Add(directoryMeta);
            var preMaterializationRows = new List<ContentSnapshotFile>();
            foreach (KeyValuePair<string, F10DiagnosticLinuxInventoryFile> pair in inventory)
            {
                if (generatedPaths.Contains(pair.Key)) continue;
                F10DiagnosticLinuxInventoryFile row = pair.Value;
                preMaterializationRows.Add(new ContentSnapshotFile(row.path, row.sha256, row.length));
            }
            string measuredCopySnapshotSha256 = ComputeContentSnapshotSha256(preMaterializationRows);
            if (!string.Equals(measuredCopySnapshotSha256, receipt.snapshotSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The pre-materialization copy snapshot differs from snapshotSha256.");
        }

        private static string ComputeContentSnapshotSha256(IEnumerable<ContentSnapshotFile> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var text = new StringBuilder();
            foreach (ContentSnapshotFile row in rows.OrderBy(value => value.Path, StringComparer.Ordinal))
            {
                if (row == null || !IsSha256(row.Sha256) || row.Length < 0)
                    throw new InvalidDataException("A content-only Linux snapshot row is incomplete.");
                text.Append(row.Path).Append('\n')
                    .Append(row.Sha256).Append('\n')
                    .Append(row.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            using (SHA256 sha = SHA256.Create())
                return ToLowerHex(sha.ComputeHash(StrictUtf8.GetBytes(text.ToString())));
        }

        private static void ValidateRunnerContext(string projectRoot)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), "Linux", StringComparison.Ordinal) ||
                !string.Equals(Environment.GetEnvironmentVariable("GITHUB_WORKFLOW"), ExpectedWorkflowName, StringComparison.Ordinal) ||
                !string.Equals(Environment.GetEnvironmentVariable("GITHUB_JOB"), ExpectedJobName, StringComparison.Ordinal))
                throw new InvalidOperationException("The diagnostic runtime is restricted to its fixed Linux GitHub Actions job context.");

            string runnerTempRoot = RequiredEnvironment("RUNNER_TEMP");
            string sourceCheckoutRoot = RequiredEnvironment("GITHUB_WORKSPACE");
            if (!PathEquals(CanonicalPath(runnerTempRoot), runnerTempRoot) ||
                !PathEquals(CanonicalPath(sourceCheckoutRoot), sourceCheckoutRoot) ||
                !IsPathEqualOrWithin(projectRoot, runnerTempRoot) || PathEquals(projectRoot, runnerTempRoot) ||
                PathsOverlap(projectRoot, sourceCheckoutRoot))
                throw new InvalidOperationException("The diagnostic project must be a new, separate project below RUNNER_TEMP and outside GITHUB_WORKSPACE.");

            string configuredProjectRoot = Environment.GetEnvironmentVariable("LUCKBOUND_F10_LINUX_PROJECT_ROOT");
            if (string.IsNullOrWhiteSpace(configuredProjectRoot) || !PathEquals(configuredProjectRoot, projectRoot))
                throw new InvalidOperationException("The exact temporary project root is not bound to this Editor process.");
            EnsureExistingDirectoryAndAncestorsArePlain(runnerTempRoot);
        }

        private static void ValidateMaterialization(string projectRoot, F10DiagnosticLinuxRuntimeReceipt receipt,
            IDictionary<string, F10DiagnosticLinuxInventoryFile> inventory)
        {
            if (receipt.copyInventory == null)
                throw new InvalidDataException("The measured copy inventory is required.");
            string materializationPath = ProjectPath(projectRoot, MaterializationReceiptRelativePath);
            EnsurePlainFileAndAncestors(materializationPath);
            string measuredMaterializationHash = HashFile(materializationPath);
            if (!string.Equals(measuredMaterializationHash, receipt.materializationReceiptSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The materialization receipt hash changed after the CI wrapper measured it.");

            F10DiagnosticLinuxMaterializationReceipt materialization = ReadJson<F10DiagnosticLinuxMaterializationReceipt>(
                materializationPath, "The fixed Linux materialization receipt could not be parsed.");
            if (materialization == null || !string.Equals(materialization.schema, MaterializationSchema, StringComparison.Ordinal) ||
                materialization.schemaVersion != ReceiptSchemaVersion || !string.Equals(materialization.status, "verified", StringComparison.Ordinal) ||
                !PathEquals(materialization.projectRoot, projectRoot) ||
                !string.Equals(materialization.unityVersion, receipt.unityVersion, StringComparison.Ordinal) ||
                materialization.outputs == null || materialization.generatedDirectoryMetas == null ||
                materialization.generatorFiles == null ||
                materialization.dependencyClosures == null || materialization.dependencyPaths == null ||
                materialization.dependencyFiles == null || materialization.packageDependencies == null)
                throw new InvalidDataException("The Linux materialization receipt is incomplete or belongs to another project/version.");

            var expectedOutputPaths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string output in MaterializedOutputPaths)
            {
                expectedOutputPaths.Add(output);
                expectedOutputPaths.Add(output + ".meta");
            }
            var outputRows = new Dictionary<string, F10DiagnosticLinuxInventoryFile>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxInventoryFile output in materialization.outputs)
            {
                string path = NormalizeRelativePath(output == null ? null : output.path);
                if (!outputRows.TryAdd(path, output))
                    throw new InvalidDataException("The Linux materialization receipt repeats an output path: " + path);
                VerifyMeasuredFileRow(projectRoot, output, inventory, requireCopyInventoryRow: true);
            }
            if (!expectedOutputPaths.SetEquals(outputRows.Keys))
                throw new InvalidDataException("The Linux materialization receipt does not list exactly the nine output assets and their metadata files.");

            var generatedDirectoryMetaRows = new Dictionary<string, F10DiagnosticLinuxInventoryFile>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxInventoryFile generatedMeta in materialization.generatedDirectoryMetas)
            {
                string path = NormalizeRelativePath(generatedMeta == null ? null : generatedMeta.path);
                if (!generatedDirectoryMetaRows.TryAdd(path, generatedMeta))
                    throw new InvalidDataException("The Linux materialization receipt repeats a generated directory meta: " + path);
                VerifyMeasuredFileRow(projectRoot, generatedMeta, inventory, requireCopyInventoryRow: true);
            }
            if (!new HashSet<string>(GeneratedDirectoryMetaPaths, StringComparer.Ordinal).SetEquals(generatedDirectoryMetaRows.Keys))
                throw new InvalidDataException("The Linux materialization receipt does not list exactly the three generated directory metas.");

            var generatorPaths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxGeneratorFile generator in materialization.generatorFiles)
            {
                string path = NormalizeRelativePath(generator == null ? null : generator.path);
                if (!IsSha256(generator.sha256) || !generatorPaths.Add(path))
                    throw new InvalidDataException("The Linux materialization receipt contains an invalid or duplicate generator source.");
                if (!inventory.TryGetValue(path, out F10DiagnosticLinuxInventoryFile row) ||
                    !string.Equals(row.sha256, generator.sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A generator source hash differs from the measured project inventory: " + path);
            }
            foreach (string requiredGenerator in GeneratorSourcePaths)
                if (!generatorPaths.Contains(requiredGenerator))
                    throw new InvalidDataException("A fixed F10 materialization generator is absent from the receipt: " + requiredGenerator);
            if (!new HashSet<string>(GeneratorSourcePaths, StringComparer.Ordinal).SetEquals(generatorPaths))
                throw new InvalidDataException("The Linux materialization receipt lists an unapproved generator source.");

            var union = new SortedSet<string>(StringComparer.Ordinal);
            var packageUnion = new SortedSet<string>(StringComparer.Ordinal);
            var closureAssets = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxDependencyClosure closure in materialization.dependencyClosures)
            {
                string assetPath = NormalizeRelativePath(closure == null ? null : closure.assetPath);
                if (!MaterializedOutputPaths.Contains(assetPath, StringComparer.Ordinal) ||
                    !closureAssets.Add(assetPath) || closure.dependencyPaths == null)
                    throw new InvalidDataException("The Linux materialization receipt has an invalid per-output AssetDatabase closure.");
                var perOutput = new HashSet<string>(StringComparer.Ordinal);
                foreach (string rawDependency in closure.dependencyPaths)
                {
                    string dependency = NormalizeRelativePath(rawDependency);
                    if (!perOutput.Add(dependency))
                        throw new InvalidDataException("An AssetDatabase dependency is duplicated in its closure: " + dependency);
                    if (inventory.ContainsKey(dependency)) union.Add(dependency);
                    else if (dependency.StartsWith("Packages/", StringComparison.Ordinal)) packageUnion.Add(dependency);
                    else throw new InvalidDataException("An AssetDatabase dependency is outside the measured project or a package: " + dependency);
                }
            }
            if (!new HashSet<string>(MaterializedOutputPaths, StringComparer.Ordinal).SetEquals(closureAssets))
                throw new InvalidDataException("The Linux materialization receipt omits an output's real AssetDatabase dependency closure.");
            var reportedUnion = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string rawDependency in materialization.dependencyPaths)
            {
                string dependency = NormalizeRelativePath(rawDependency);
                if (!reportedUnion.Add(dependency))
                    throw new InvalidDataException("The Linux materialization receipt repeats a dependency union path.");
            }
            if (!union.SetEquals(reportedUnion))
                throw new InvalidDataException("The project-local AssetDatabase dependency closure differs from the reported copy-inventory union.");

            var dependencyFileRows = new Dictionary<string, F10DiagnosticLinuxInventoryFile>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxInventoryFile dependencyFile in materialization.dependencyFiles)
            {
                string path = NormalizeRelativePath(dependencyFile == null ? null : dependencyFile.path);
                if (dependencyFileRows.ContainsKey(path))
                    throw new InvalidDataException("The materialization dependency inventory repeats a path: " + path);
                dependencyFileRows.Add(path, dependencyFile);
                VerifyMeasuredFileRow(projectRoot, dependencyFile, inventory, requireCopyInventoryRow: true);
            }
            if (!new HashSet<string>(reportedUnion, StringComparer.Ordinal).SetEquals(dependencyFileRows.Keys))
                throw new InvalidDataException("The measured dependency file rows do not cover exactly the AssetDatabase dependency union.");

            ValidatePackageDependencies(packageUnion, materialization.packageDependencies);
        }

        private static void ValidatePackageDependencies(ISet<string> dependencyAssetPaths,
            F10DiagnosticLinuxPackageDependency[] packageDependencies)
        {
            var expected = new Dictionary<string, LinuxPackageDependencyGroup>(StringComparer.Ordinal);
            foreach (string assetPath in dependencyAssetPaths)
            {
                PackageInfo info = PackageInfo.FindForAssetPath(assetPath);
                if (info == null || info.source != PackageSource.BuiltIn || string.IsNullOrWhiteSpace(info.name) ||
                    string.IsNullOrWhiteSpace(info.assetPath) || string.IsNullOrWhiteSpace(info.resolvedPath) ||
                    !assetPath.StartsWith(info.assetPath + "/", StringComparison.Ordinal))
                    throw new InvalidDataException("A package closure path no longer resolves to its exact Editor BuiltIn package: " + assetPath);
                string root = CanonicalPath(info.resolvedPath);
                string key = info.name + "\0" + root;
                if (!expected.TryGetValue(key, out LinuxPackageDependencyGroup group))
                {
                    group = new LinuxPackageDependencyGroup(info, root);
                    expected.Add(key, group);
                }
                else if (!PackageInfoMatches(group.Info, info))
                    throw new InvalidDataException("PackageInfo changed while validating the live AssetDatabase closure: " + assetPath);
                group.AssetPaths.Add(assetPath);
            }

            if (packageDependencies == null || packageDependencies.Length != expected.Count)
                throw new InvalidDataException("The Linux receipt package rows do not cover exactly the live package closure.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxPackageDependency row in packageDependencies)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.packageName) ||
                    string.IsNullOrWhiteSpace(row.resolvedRootAbsolutePath))
                    throw new InvalidDataException("A Linux package dependency row is incomplete.");
                string root = CanonicalPath(row.resolvedRootAbsolutePath);
                string key = row.packageName + "\0" + root;
                if (!seen.Add(key) || !expected.TryGetValue(key, out LinuxPackageDependencyGroup group))
                    throw new InvalidDataException("A Linux package dependency row is duplicate or outside the live package closure: " + row.packageName);
                PackageInfo info = group.Info;
                string expectedRootRelative = "@editor/Data/Resources/PackageManager/BuiltInPackages/" + info.name;
                if (!string.Equals(row.packageVersion, info.version, StringComparison.Ordinal) ||
                    !string.Equals(row.packageSource, info.source.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(row.packageId ?? string.Empty, info.packageId ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(row.packageAssetPath, info.assetPath, StringComparison.Ordinal) ||
                    !string.Equals(row.resolvedRootKind, "builtin-editor", StringComparison.Ordinal) ||
                    !string.Equals(row.resolvedRootRelativePath, expectedRootRelative, StringComparison.Ordinal) ||
                    !PathEquals(row.resolvedRootAbsolutePath, group.ResolvedRoot) ||
                    !string.Equals(row.resolvedRootIdentity, InspectDirectory(group.ResolvedRoot).Identity, StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(row.registryUrl) || !string.IsNullOrEmpty(row.lockEntrySource) ||
                    !string.IsNullOrEmpty(row.lockEntryVersion) || !string.IsNullOrEmpty(row.lockEntryUrl) ||
                    !string.IsNullOrEmpty(row.lockEntryHash) || !string.IsNullOrEmpty(row.lockFileSha256) ||
                    row.dependencyAssetPaths == null || row.dependencyAssetPaths.Length != group.AssetPaths.Count ||
                    !new HashSet<string>(row.dependencyAssetPaths, StringComparer.Ordinal).SetEquals(group.AssetPaths))
                    throw new InvalidDataException("A Linux BuiltIn package row differs from live PackageInfo, root, or AssetDatabase paths: " + row.packageName);

                ValidateLinuxPackageFiles(group, row.files);
            }
        }

        internal static object[] CaptureV7PackageDependencies(IEnumerable<string> dependencyAssetPaths)
        {
            EnsureLinuxRuntime();
            string[] paths = (dependencyAssetPaths ?? throw new ArgumentNullException(nameof(dependencyAssetPaths)))
                .Select(NormalizeRelativePath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string projectRoot = CanonicalPath(Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName));
            var externalPaths = new HashSet<string>(StringComparer.Ordinal);
            var localPaths = new HashSet<string>(StringComparer.Ordinal);
            var groups = new Dictionary<string, LinuxPackageDependencyGroup>(StringComparer.Ordinal);
            foreach (string assetPath in paths)
            {
                PackageInfo info = PackageInfo.FindForAssetPath(assetPath);
                if (info == null || string.IsNullOrWhiteSpace(info.name) ||
                    string.IsNullOrWhiteSpace(info.assetPath) || string.IsNullOrWhiteSpace(info.resolvedPath) ||
                    !assetPath.StartsWith(info.assetPath + "/", StringComparison.Ordinal))
                    throw new InvalidDataException("A V7 dependency is not bound to its exact PackageInfo asset path: " + assetPath);
                string root = CanonicalPath(info.resolvedPath);
                if (IsV7ProjectLocalPackageRoot(info, projectRoot))
                {
                    localPaths.Add(assetPath);
                    continue;
                }
                if (info.source != PackageSource.BuiltIn)
                    throw new InvalidDataException("An external V7 package dependency is not an Editor BuiltIn package: " + assetPath);
                externalPaths.Add(assetPath);
                string key = info.name + "\0" + root;
                if (!groups.TryGetValue(key, out LinuxPackageDependencyGroup group))
                {
                    group = new LinuxPackageDependencyGroup(info, root);
                    groups.Add(key, group);
                }
                else if (!PackageInfoMatches(group.Info, info))
                    throw new InvalidDataException("PackageInfo changed while capturing the V7 live AssetDatabase closure: " + assetPath);
                group.AssetPaths.Add(assetPath);

                string relative = assetPath.Substring(info.assetPath.Length + 1);
                string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathEqualOrWithin(fullPath, root) || PathEquals(fullPath, root))
                    throw new InvalidDataException("A V7 BuiltIn package dependency escaped its exact resolved root: " + assetPath);
                EnsurePlainFileAndAncestors(fullPath);
                string metaPath = fullPath + ".meta";
                EnsurePlainFileAndAncestors(metaPath);
                F10DiagnosticLinuxFileMetadata assetMetadata = InspectRegularFile(fullPath);
                F10DiagnosticLinuxFileMetadata metaMetadata = InspectRegularFile(metaPath);
                if (assetMetadata.LinkCount != 1 || metaMetadata.LinkCount != 1)
                    throw new InvalidDataException("V7 BuiltIn package dependencies reject hard-linked asset or .meta bytes: " + assetPath);
                F10DiagnosticLinuxPackageDependencyFile file = new F10DiagnosticLinuxPackageDependencyFile
                {
                    relativePath = relative,
                    projectRelativePath = assetPath,
                    roles = new[] { "asset-database-dependency" },
                    assetPaths = new[] { assetPath },
                    sha256 = HashFile(fullPath),
                    length = assetMetadata.Length,
                    identity = assetMetadata.Identity,
                    linkCount = assetMetadata.LinkCount,
                    metaSha256 = HashFile(metaPath),
                    metaLength = metaMetadata.Length,
                    metaIdentity = metaMetadata.Identity,
                    metaLinkCount = metaMetadata.LinkCount
                };
                group.Files.Add(assetPath, file);
            }

            F10DiagnosticLinuxPackageDependency[] rows = groups.Values.OrderBy(group => group.Info.name, StringComparer.Ordinal)
                .Select(group => new F10DiagnosticLinuxPackageDependency
                {
                    packageName = group.Info.name,
                    packageVersion = group.Info.version,
                    packageSource = group.Info.source.ToString(),
                    packageId = group.Info.packageId,
                    packageAssetPath = group.Info.assetPath,
                    resolvedRootKind = "builtin-editor",
                    resolvedRootRelativePath = BuiltInUniversalRootRelativePath.Substring(0,
                        BuiltInUniversalRootRelativePath.LastIndexOf('/') + 1) + group.Info.name,
                    resolvedRootAbsolutePath = group.ResolvedRoot,
                    resolvedRootIdentity = InspectDirectory(group.ResolvedRoot).Identity,
                    dependencyAssetPaths = group.AssetPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                    files = group.Files.Values.OrderBy(file => file.projectRelativePath, StringComparer.Ordinal).ToArray()
                }).ToArray();
            ValidateV7LocalPackageDependencies(localPaths);
            ValidatePackageDependencies(externalPaths, rows);
            ValidateV7PackageDependencyMetadata(rows);
            return rows.Select(PackageDependencyToJson).Cast<object>().ToArray();
        }

        internal static void ValidateV7LocalPackageDependencies(IEnumerable<string> dependencyAssetPaths)
        {
            EnsureLinuxRuntime();
            string projectRoot = CanonicalPath(Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName));
            foreach (string assetPath in (dependencyAssetPaths ?? throw new ArgumentNullException(nameof(dependencyAssetPaths)))
                .Select(NormalizeRelativePath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                PackageInfo info = PackageInfo.FindForAssetPath(assetPath);
                if (info == null || string.IsNullOrWhiteSpace(info.assetPath) || string.IsNullOrWhiteSpace(info.resolvedPath) ||
                    !assetPath.StartsWith(info.assetPath + "/", StringComparison.Ordinal) ||
                    !IsV7ProjectLocalPackageRoot(info, projectRoot))
                    throw new InvalidDataException("A V7 project-local package dependency is not bound to its exact in-project PackageInfo root: " + assetPath);
                string packageRoot = CanonicalPath(info.resolvedPath);
                EnsureExistingDirectoryAndAncestorsArePlain(packageRoot);
                string relative = assetPath.Substring(info.assetPath.Length + 1);
                string fullPath = Path.GetFullPath(Path.Combine(packageRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathEqualOrWithin(fullPath, packageRoot) || PathEquals(fullPath, packageRoot))
                    throw new InvalidDataException("A V7 local package dependency escaped its exact project package root: " + assetPath);
                string metaPath = fullPath + ".meta";
                EnsurePlainFileAndAncestors(fullPath);
                EnsurePlainFileAndAncestors(metaPath);
                F10DiagnosticLinuxFileMetadata assetMetadata = InspectRegularFile(fullPath);
                F10DiagnosticLinuxFileMetadata metaMetadata = InspectRegularFile(metaPath);
                if (assetMetadata.LinkCount != 1 || metaMetadata.LinkCount != 1)
                    throw new InvalidDataException("V7 project-local package dependencies reject hard-linked asset or .meta bytes: " + assetPath);
            }
        }

        private static bool IsV7ProjectLocalPackageRoot(PackageInfo info, string projectRoot)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.assetPath) || string.IsNullOrWhiteSpace(info.resolvedPath))
                return false;
            string resolvedRoot = CanonicalPath(info.resolvedPath);
            if (!IsPathEqualOrWithin(resolvedRoot, projectRoot) || PathEquals(resolvedRoot, projectRoot))
                return false;
            string expectedRoot = CanonicalPath(Path.Combine(projectRoot,
                info.assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!PathEquals(resolvedRoot, expectedRoot))
                throw new InvalidDataException("A project-local PackageInfo resolved root differs from its exact Packages asset path: " + info.assetPath);
            return true;
        }

        internal static void ValidateV7PackageDependencies(ISet<string> dependencyAssetPaths,
            F10DiagnosticLinuxPackageDependency[] packageDependencies)
        {
            EnsureLinuxRuntime();
            ValidatePackageDependencies(dependencyAssetPaths, packageDependencies);
            ValidateV7PackageDependencyMetadata(packageDependencies);
        }

        private static void ValidateV7PackageDependencyMetadata(F10DiagnosticLinuxPackageDependency[] packageDependencies)
        {
            if (packageDependencies == null) throw new InvalidDataException("V7 Linux package evidence is missing.");
            foreach (F10DiagnosticLinuxPackageDependency package in packageDependencies)
            {
                foreach (F10DiagnosticLinuxPackageDependencyFile row in package.files)
                {
                    string assetPath = NormalizeRelativePath(row == null ? null : row.projectRelativePath);
                    if (!IsSha256(row.metaSha256) || row.metaLength < 0 || row.metaLinkCount != 1 ||
                        string.IsNullOrWhiteSpace(row.metaIdentity))
                        throw new InvalidDataException("The V7 BuiltIn package .meta evidence is incomplete: " + assetPath);
                    string root = CanonicalPath(package.resolvedRootAbsolutePath);
                    string metaPath = Path.GetFullPath(Path.Combine(root,
                        (row.relativePath + ".meta").Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsPathEqualOrWithin(metaPath, root) || PathEquals(metaPath, root))
                        throw new InvalidDataException("A V7 BuiltIn package .meta path escaped its resolved root: " + assetPath);
                    EnsurePlainFileAndAncestors(metaPath);
                    F10DiagnosticLinuxFileMetadata metadata = InspectRegularFile(metaPath);
                    if (metadata.LinkCount != row.metaLinkCount || metadata.Length != row.metaLength ||
                        !string.Equals(metadata.Identity, row.metaIdentity, StringComparison.Ordinal) ||
                        !string.Equals(HashFile(metaPath), row.metaSha256, StringComparison.Ordinal))
                        throw new InvalidDataException("A V7 BuiltIn package .meta changed its measured statx identity or bytes: " + assetPath);
                }
            }
        }

        private static object PackageDependencyToJson(F10DiagnosticLinuxPackageDependency row)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "packageName", row.packageName }, { "packageVersion", row.packageVersion },
                { "packageSource", row.packageSource }, { "packageId", row.packageId ?? string.Empty },
                { "registryUrl", row.registryUrl ?? string.Empty }, { "packageAssetPath", row.packageAssetPath },
                { "resolvedRootKind", row.resolvedRootKind }, { "resolvedRootRelativePath", row.resolvedRootRelativePath },
                { "resolvedRootAbsolutePath", row.resolvedRootAbsolutePath }, { "resolvedRootIdentity", row.resolvedRootIdentity },
                { "lockEntrySource", row.lockEntrySource ?? string.Empty }, { "lockEntryVersion", row.lockEntryVersion ?? string.Empty },
                { "lockEntryUrl", row.lockEntryUrl ?? string.Empty }, { "lockEntryHash", row.lockEntryHash ?? string.Empty },
                { "lockFileSha256", row.lockFileSha256 ?? string.Empty },
                { "dependencyAssetPaths", row.dependencyAssetPaths },
                { "files", row.files.Select(file => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "relativePath", file.relativePath }, { "projectRelativePath", file.projectRelativePath },
                        { "roles", file.roles }, { "assetPaths", file.assetPaths }, { "sha256", file.sha256 },
                        { "length", file.length }, { "identity", file.identity }, { "linkCount", file.linkCount },
                        { "metaSha256", file.metaSha256 }, { "metaLength", file.metaLength },
                        { "metaIdentity", file.metaIdentity }, { "metaLinkCount", file.metaLinkCount }
                    }).ToArray() }
            };
        }

        private static void ValidateLinuxPackageFiles(LinuxPackageDependencyGroup group,
            F10DiagnosticLinuxPackageDependencyFile[] files)
        {
            if (files == null || files.Length != group.AssetPaths.Count)
                throw new InvalidDataException("The BuiltIn package file inventory does not cover its exact AssetDatabase dependency paths.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxPackageDependencyFile row in files)
            {
                string assetPath = NormalizeRelativePath(row == null ? null : row.projectRelativePath);
                if (!seen.Add(assetPath) || !group.AssetPaths.Contains(assetPath) ||
                    !string.Equals(row.relativePath, assetPath.Substring(group.Info.assetPath.Length + 1), StringComparison.Ordinal) ||
                    row.roles == null || !row.roles.SequenceEqual(new[] { "asset-database-dependency" }, StringComparer.Ordinal) ||
                    row.assetPaths == null || !row.assetPaths.SequenceEqual(new[] { assetPath }, StringComparer.Ordinal) ||
                    !IsSha256(row.sha256) || row.length < 0 || row.linkCount != 1 || string.IsNullOrWhiteSpace(row.identity))
                    throw new InvalidDataException("A BuiltIn package file row is malformed or not bound to its AssetDatabase path: " + assetPath);
                string fullPath = Path.GetFullPath(Path.Combine(group.ResolvedRoot,
                    row.relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathEqualOrWithin(fullPath, group.ResolvedRoot) || PathEquals(fullPath, group.ResolvedRoot))
                    throw new InvalidDataException("A BuiltIn package file path escaped its resolved root: " + assetPath);
                EnsurePlainFileAndAncestors(fullPath);
                F10DiagnosticLinuxFileMetadata metadata = InspectRegularFile(fullPath);
                if (metadata.LinkCount != row.linkCount || metadata.Length != row.length ||
                    !string.Equals(metadata.Identity, row.identity, StringComparison.Ordinal) ||
                    !string.Equals(HashFile(fullPath), row.sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A live BuiltIn package dependency file differs from its measured Linux identity/hash/size: " + assetPath);
            }
        }

        private static bool PackageInfoMatches(PackageInfo left, PackageInfo right)
        {
            return left != null && right != null &&
                string.Equals(left.name, right.name, StringComparison.Ordinal) &&
                string.Equals(left.version, right.version, StringComparison.Ordinal) &&
                left.source == right.source &&
                string.Equals(left.packageId ?? string.Empty, right.packageId ?? string.Empty, StringComparison.Ordinal) &&
                string.Equals(left.assetPath, right.assetPath, StringComparison.Ordinal) &&
                PathEquals(left.resolvedPath, right.resolvedPath);
        }

        private sealed class LinuxPackageDependencyGroup
        {
            internal LinuxPackageDependencyGroup(PackageInfo info, string resolvedRoot)
            {
                Info = info;
                ResolvedRoot = resolvedRoot;
            }

            internal PackageInfo Info { get; }
            internal string ResolvedRoot { get; }
            internal ISet<string> AssetPaths { get; } = new SortedSet<string>(StringComparer.Ordinal);
            internal IDictionary<string, F10DiagnosticLinuxPackageDependencyFile> Files { get; } =
                new SortedDictionary<string, F10DiagnosticLinuxPackageDependencyFile>(StringComparer.Ordinal);
        }

        private static IDictionary<string, F10DiagnosticLinuxInventoryFile> ValidateInventoryShape(
            F10DiagnosticLinuxInventoryFile[] rows)
        {
            if (rows == null || rows.Length == 0)
                throw new InvalidDataException("The full Linux copy inventory is required.");
            var byPath = new Dictionary<string, F10DiagnosticLinuxInventoryFile>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticLinuxInventoryFile row in rows)
            {
                string path = NormalizeRelativePath(row == null ? null : row.path);
                if (!IsAllowedInventoryPath(path) || !IsSha256(row.sha256) || row.length < 0 ||
                    row.deviceMajor > uint.MaxValue || row.deviceMinor > uint.MaxValue || row.inode <= 0 || row.linkCount != 1 ||
                    byPath.ContainsKey(path))
                    throw new InvalidDataException("A Linux copy inventory row is malformed, duplicated, or outside the fixed inventory roots: " + path);
                string identity = PhysicalIdentityKey(row.deviceMajor, row.deviceMinor, row.inode);
                if (!identities.Add(identity))
                    throw new InvalidDataException("Linux copy inventory files share an inode identity: " + path);
                byPath.Add(path, row);
            }

            foreach (string requiredExternalPath in RequiredExternalInventoryPaths)
                if (!byPath.ContainsKey(requiredExternalPath))
                    throw new InvalidDataException("A required F01 source input is absent from the measured Linux inventory: " + requiredExternalPath);
            return byPath;
        }

        private static void VerifyWholeProjectInventory(string projectRoot,
            IDictionary<string, F10DiagnosticLinuxInventoryFile> expected)
        {
            var actualPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rootName in InventoryRootNames)
            {
                string rootPath = ProjectPath(projectRoot, rootName);
                EnsureExistingDirectoryAndAncestorsArePlain(rootPath);
                var pending = new Stack<string>();
                pending.Push(rootPath);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop();
                    EnsureExistingDirectoryAndAncestorsArePlain(directory);
                    foreach (string childDirectory in Directory.GetDirectories(directory))
                    {
                        EnsurePlainDirectoryAndAncestors(childDirectory);
                        pending.Push(childDirectory);
                    }
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        string relative = ToRelativePath(projectRoot, file);
                        VerifyActualInventoryEntry(projectRoot, relative, expected, actualPaths);
                    }
                }
            }
            foreach (string relative in RequiredExternalInventoryPaths)
                VerifyActualInventoryEntry(projectRoot, relative, expected, actualPaths);
            if (!actualPaths.SetEquals(expected.Keys))
                throw new InvalidDataException("The current protected project tree does not match the measured full copy inventory.");
        }

        private static void VerifyActualInventoryEntry(string projectRoot, string relativePath,
            IDictionary<string, F10DiagnosticLinuxInventoryFile> expected, ISet<string> actualPaths)
        {
            string relative = NormalizeRelativePath(relativePath);
            if (!actualPaths.Add(relative))
                throw new InvalidDataException("The current protected project inventory repeats a path: " + relative);
            if (!expected.TryGetValue(relative, out F10DiagnosticLinuxInventoryFile row))
                throw new InvalidDataException("The current protected project contains an unmeasured path: " + relative);
            string fullPath = ProjectPath(projectRoot, relative);
            VerifyMeasuredFileRow(projectRoot, row, expected, requireCopyInventoryRow: true);
            if (!string.Equals(HashFile(fullPath), row.sha256, StringComparison.Ordinal) ||
                new FileInfo(fullPath).Length != row.length)
                throw new InvalidDataException("A protected project file differs from its measured copy inventory: " + relative);
        }

        private static void VerifyMeasuredFileRow(string projectRoot, F10DiagnosticLinuxInventoryFile row,
            IDictionary<string, F10DiagnosticLinuxInventoryFile> copyInventory, bool requireCopyInventoryRow)
        {
            if (row == null || !IsSha256(row.sha256) || row.length < 0 || row.inode <= 0 || row.linkCount != 1)
                throw new InvalidDataException("A measured Linux file row is incomplete.");
            string relative = NormalizeRelativePath(row.path);
            string fullPath = ProjectPath(projectRoot, relative);
            EnsurePlainFileAndAncestors(fullPath);
            F10DiagnosticLinuxFileMetadata metadata = InspectRegularFile(fullPath);
            if (!string.Equals(HashFile(fullPath), row.sha256, StringComparison.Ordinal) || metadata.Length != row.length ||
                metadata.DeviceMajor != row.deviceMajor || metadata.DeviceMinor != row.deviceMinor ||
                metadata.Inode != row.inode || metadata.LinkCount != row.linkCount)
                throw new InvalidDataException("A Linux file differs from its measured identity/hash/size row: " + relative);
            if (requireCopyInventoryRow &&
                (!copyInventory.TryGetValue(relative, out F10DiagnosticLinuxInventoryFile inventoryRow) ||
                 !InventoryRowsEqual(inventoryRow, row)))
                throw new InvalidDataException("A materialized output/dependency is not bound to the full copy inventory: " + relative);
        }

        internal static string ComputeInventorySha256(IEnumerable<F10DiagnosticLinuxInventoryFile> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var text = new StringBuilder();
            foreach (F10DiagnosticLinuxInventoryFile row in rows.OrderBy(value => value.path, StringComparer.Ordinal))
            {
                if (row == null) throw new InvalidDataException("The Linux inventory contains a null row.");
                text.Append(row.path).Append('\n')
                    .Append(row.sha256).Append('\n')
                    .Append(row.length.ToString(CultureInfo.InvariantCulture)).Append('\n')
                    .Append(row.deviceMajor.ToString(CultureInfo.InvariantCulture)).Append('\n')
                    .Append(row.deviceMinor.ToString(CultureInfo.InvariantCulture)).Append('\n')
                    .Append(row.inode.ToString(CultureInfo.InvariantCulture)).Append('\n')
                    .Append(row.linkCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            using (SHA256 sha = SHA256.Create())
                return ToLowerHex(sha.ComputeHash(StrictUtf8.GetBytes(text.ToString())));
        }

        internal static string PhysicalIdentityKey(uint deviceMajor, uint deviceMinor, long inode)
        {
            return deviceMajor.ToString(CultureInfo.InvariantCulture) + ":" +
                deviceMinor.ToString(CultureInfo.InvariantCulture) + ":" +
                inode.ToString(CultureInfo.InvariantCulture);
        }

        private static bool InventoryRowsEqual(F10DiagnosticLinuxInventoryFile left, F10DiagnosticLinuxInventoryFile right)
        {
            return left != null && right != null &&
                string.Equals(left.path, right.path, StringComparison.Ordinal) &&
                string.Equals(left.sha256, right.sha256, StringComparison.Ordinal) &&
                left.length == right.length && left.deviceMajor == right.deviceMajor &&
                left.deviceMinor == right.deviceMinor && left.inode == right.inode && left.linkCount == right.linkCount;
        }

        private static bool IsAllowedInventoryPath(string path)
        {
            if (RequiredExternalInventoryPaths.Contains(path, StringComparer.Ordinal)) return true;
            foreach (string root in InventoryRootNames)
                if (string.Equals(path, root, StringComparison.Ordinal) || path.StartsWith(root + "/", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static T ReadJson<T>(string path, string error) where T : class
        {
            try
            {
                T value = JsonUtility.FromJson<T>(File.ReadAllText(path, StrictUtf8));
                if (value == null) throw new InvalidDataException(error);
                return value;
            }
            catch (Exception exception)
            {
                throw new FormatException(error, exception);
            }
        }

        private static string ReadProjectUnityVersion(string path)
        {
            EnsurePlainFileAndAncestors(path);
            string text = StrictUtf8.GetString(File.ReadAllBytes(path));
            const string prefix = "m_EditorVersion: ";
            string line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
            if (line == null) throw new InvalidDataException("ProjectVersion.txt does not declare m_EditorVersion.");
            return line.Substring(prefix.Length).Trim();
        }

        private static string ReadLinuxOsReleaseImageVersion()
        {
            string id = null;
            string version = null;
            foreach (string line in File.ReadAllLines("/usr/lib/os-release", StrictUtf8))
            {
                if (line.StartsWith("ID=", StringComparison.Ordinal)) id = line.Substring(3).Trim().Trim('"');
                else if (line.StartsWith("VERSION_ID=", StringComparison.Ordinal)) version = line.Substring(11).Trim().Trim('"');
            }
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
                throw new InvalidDataException("The actual Linux runner ID/VERSION_ID could not be measured from os-release.");
            return id + ":" + version;
        }

        private static void RequireEnvironmentEquals(string name, string expected)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(name), expected, StringComparison.Ordinal))
                throw new InvalidOperationException("The GitHub Actions environment differs from the diagnostic receipt field: " + name);
        }

        private static string RequiredEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("A required Linux CI environment variable is missing: " + name);
            return value;
        }

        private static void EnsureLinuxRuntime()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || Path.DirectorySeparatorChar != '/' ||
                Application.platform != RuntimePlatform.LinuxEditor)
                throw new PlatformNotSupportedException("The F10 V6 Linux diagnostic runtime is available only in the Linux Editor.");
        }

        private static void EnsurePlainFileAndAncestors(string path)
        {
            EnsurePlainAncestors(path);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(path) & FileAttributes.Directory) != 0)
                throw new IOException("A required Linux diagnostic file is missing or not a plain file: " + path);
        }

        private static void EnsurePlainDirectoryAndAncestors(string path)
        {
            EnsurePlainAncestors(path);
            if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(path) & FileAttributes.Directory) == 0)
                throw new IOException("A required Linux diagnostic directory is missing or not plain: " + path);
        }

        private static void EnsurePlainAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (ExistsAsAnyEntry(current))
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Linux diagnostic paths cannot contain symbolic links: " + current);
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current)) break;
                current = parent;
            }
        }

        private static bool ExistsAsAnyEntry(string path)
        {
            try
            {
                File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static void EnsureExistingDirectoryAndAncestorsArePlain(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                EnsurePlainDirectoryAndAncestors(current);
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current)) break;
                current = parent;
            }
        }

        private static string ProjectPath(string projectRoot, string relativePath)
        {
            string relative = NormalizeRelativePath(relativePath);
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathEqualOrWithin(fullPath, projectRoot) || PathEquals(fullPath, projectRoot))
                throw new InvalidDataException("A Linux diagnostic path escaped its fixed project root.");
            return fullPath;
        }

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0)
                throw new InvalidDataException("A Linux diagnostic inventory path is not a fixed relative path.");
            string[] segments = path.Split('/');
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."))
                throw new InvalidDataException("A Linux diagnostic inventory path contains an unsafe segment.");
            return string.Join("/", segments);
        }

        private static string ToRelativePath(string root, string path)
        {
            string prefix = CanonicalPath(root) + Path.DirectorySeparatorChar;
            string full = CanonicalPath(path);
            if (!full.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("A measured project file escaped the temporary copy: " + path);
            return NormalizeRelativePath(full.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/'));
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return ToLowerHex(sha.ComputeHash(stream));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            return string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character =>
                character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        }

        private static bool PathsOverlap(string first, string second)
        {
            return IsPathEqualOrWithin(first, second) || IsPathEqualOrWithin(second, first);
        }

        private static bool IsPathEqualOrWithin(string candidate, string parent)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(parent)) return false;
            string fullCandidate = CanonicalPath(candidate);
            string fullParent = CanonicalPath(parent);
            if (string.Equals(fullCandidate, fullParent, StringComparison.Ordinal)) return true;
            return fullCandidate.StartsWith(fullParent.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? fullParent : fullParent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool PathEquals(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
            return string.Equals(CanonicalPath(first), CanonicalPath(second), StringComparison.Ordinal);
        }

        private static string CanonicalPath(string path)
        {
            string full = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            string root = Path.GetPathRoot(full) ?? string.Empty;
            while (full.Length > root.Length &&
                (full[full.Length - 1] == Path.DirectorySeparatorChar || full[full.Length - 1] == Path.AltDirectorySeparatorChar))
                full = full.Substring(0, full.Length - 1);
            return full;
        }

        private static bool InventoryRowsEqual(F10DiagnosticLinuxInventoryFile a, F10DiagnosticLinuxInventoryFile b, bool unused)
        {
            return InventoryRowsEqual(a, b);
        }

        [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
        private static extern int Statx(int dirfd,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pathname,
            int flags, uint mask, [Out] byte[] statxBuffer);
    }

    internal struct F10DiagnosticLinuxFileMetadata
    {
        internal F10DiagnosticLinuxFileMetadata(uint deviceMajor, uint deviceMinor, long inode, long length, uint linkCount)
        {
            DeviceMajor = deviceMajor;
            DeviceMinor = deviceMinor;
            Inode = inode;
            Length = length;
            LinkCount = linkCount;
        }

        internal uint DeviceMajor { get; }
        internal uint DeviceMinor { get; }
        internal long Inode { get; }
        internal long Length { get; }
        internal uint LinkCount { get; }
        internal string Identity => F10DiagnosticLinuxRuntimeEvidenceReader.PhysicalIdentityKey(DeviceMajor, DeviceMinor, Inode);
    }
}
#endif
