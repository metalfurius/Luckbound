#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

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
                if (File.Exists(current) || Directory.Exists(current))
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
