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
using UnityEngine.Tilemaps;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Luckbound.Editor
{
    /// <summary>
    /// Linux-only, create-only materialization entry point for the frozen F10 disposable project.
    /// It measures the real generator delta and live AssetDatabase dependency closures; Python later
    /// binds those outputs to a complete statx inventory of the same temporary project tree.
    /// </summary>
    public static class F10DiagnosticLinuxMaterializationReceiptExporter
    {
        private const string ReceiptRelativePath = "Logs/F10V6Resume-20261120/materialization-linux-diagnostic.json";
        private const string UnityVersion = "6000.6.10f1";
        private static readonly string[] InventoryRoots = { "Assets", "Packages", "ProjectSettings" };
        private static readonly string[] RequiredExternalInventoryPaths =
        {
            "docs/art/F01_bellator_32x48.png",
            "docs/art/F01_master_reference.md",
            "docs/art/records/ART-F01-BELLATOR-32X48.v1.json"
        };
        private static readonly string[] RequiredAssetPaths =
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
        private static readonly string[] GeneratedDirectoryMetaPaths =
        {
            "Assets/Luckbound/Editor/Diagnostic/F10.meta",
            "Assets/Luckbound/Editor/Diagnostic/Shared.meta",
            "Assets/Luckbound/Editor/Diagnostic/F10GoldCache.meta"
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
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [MenuItem("Luckbound/Technical Diagnostic/Materialize F10 Linux Diagnostic Receipt (Disposable Copy Only)")]
        private static void RunFromMenu() => MaterializeAndMeasure();

        /// <summary>Unity -executeMethod target. Does not touch the live checkout or replace prior evidence.</summary>
        public static void MaterializeAndMeasure()
        {
            EnsureLinuxEditor();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("F10 Linux materialization is only allowed outside Play Mode.");

            string projectRoot = CanonicalPath(Directory.GetParent(Application.dataPath).FullName);
            string receiptPath = ProjectPath(projectRoot, ReceiptRelativePath);
            if (File.Exists(receiptPath) || Directory.Exists(receiptPath))
                throw new IOException("The fixed Linux materialization receipt already exists; prior evidence was preserved.");
            string receiptDirectory = Path.GetDirectoryName(receiptPath);
            if (string.IsNullOrEmpty(receiptDirectory) || !Directory.Exists(receiptDirectory))
                throw new DirectoryNotFoundException("Python must create the fixed receipt directory before the measured snapshot.");
            RejectSymlinkAncestors(receiptDirectory);

            F10TechnicalDiagnosticPrefabGenerator.PreflightOnly();
            F10TechnicalDiagnosticPlayerPrefabGenerator.PreflightOnly();
            F10PhysicalGoldCacheDiagnosticPrefabGenerator.PreflightOnly();
            VerifySceneDestinationAbsent(projectRoot);

            Dictionary<string, LinuxFileRow> beforeFiles = CaptureProjectFiles(projectRoot);
            HashSet<string> beforeDirectories = CaptureProjectDirectories(projectRoot);
            F10DiagnosticSceneGenerator.Generate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            VerifyImportedAssets(projectRoot);

            Dictionary<string, LinuxFileRow> afterFiles = CaptureProjectFiles(projectRoot);
            HashSet<string> afterDirectories = CaptureProjectDirectories(projectRoot);
            string[] createdDirectories = afterDirectories.Except(beforeDirectories, StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] expectedDirectories =
            {
                "Assets/Luckbound/Editor/Diagnostic/F10",
                "Assets/Luckbound/Editor/Diagnostic/F10GoldCache",
                "Assets/Luckbound/Editor/Diagnostic/Shared"
            };
            if (!createdDirectories.SequenceEqual(expectedDirectories, StringComparer.Ordinal))
                throw new InvalidDataException("The F10 generator created an unexpected directory delta; no receipt was written.");

            string[] expectedOutputs = RequiredAssetPaths.SelectMany(path => new[] { path, path + ".meta" })
                .Concat(GeneratedDirectoryMetaPaths).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] addedPaths = afterFiles.Keys.Except(beforeFiles.Keys, StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (!addedPaths.SequenceEqual(expectedOutputs, StringComparer.Ordinal))
                throw new InvalidDataException("The F10 generator produced an unexpected file delta; outputs were preserved without a receipt.");

            foreach (KeyValuePair<string, LinuxFileRow> baseline in beforeFiles)
            {
                if (!afterFiles.TryGetValue(baseline.Key, out LinuxFileRow actual) ||
                    !RowsEqual(baseline.Value, actual))
                    throw new InvalidDataException("Materialization changed or removed immutable copy content/identity: " + baseline.Key);
            }

            LinuxFileRow[] outputs = RequiredAssetPaths.SelectMany(path => new[] { path, path + ".meta" })
                .OrderBy(path => path, StringComparer.Ordinal).Select(path => afterFiles[path]).ToArray();
            LinuxFileRow[] directoryMetas = GeneratedDirectoryMetaPaths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => afterFiles[path]).ToArray();
            F10DiagnosticLinuxDependencyClosure[] closures = CaptureAssetDatabaseClosures(projectRoot, afterFiles,
                out string[] dependencyPaths, out LinuxFileRow[] dependencyFiles,
                out F10DiagnosticLinuxPackageDependency[] packageDependencies);
            F10DiagnosticLinuxGeneratorFile[] generators = MeasureGeneratorSources(projectRoot);

            var receipt = new F10DiagnosticLinuxMaterializationReceipt
            {
                schema = "ci-linux-materialization-diagnostic",
                schemaVersion = 1,
                status = "verified",
                projectRoot = projectRoot,
                unityVersion = Application.unityVersion,
                outputs = outputs,
                generatedDirectoryMetas = directoryMetas,
                generatorFiles = generators,
                dependencyClosures = closures,
                dependencyPaths = dependencyPaths,
                dependencyFiles = dependencyFiles,
                packageDependencies = packageDependencies
            };

            VerifyReceiptRows(projectRoot, receipt, afterFiles);
            WriteCreateOnly(receiptPath, JsonUtility.ToJson(receipt, true));
            Debug.Log("F10 Linux diagnostic materialization measured by Unity and AssetDatabase: " + ReceiptRelativePath);
        }

        private static void EnsureLinuxEditor()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || Path.DirectorySeparatorChar != '/' ||
                Application.platform != RuntimePlatform.LinuxEditor || !string.Equals(Application.unityVersion,
                    UnityVersion, StringComparison.Ordinal))
                throw new PlatformNotSupportedException("This exporter requires Unity 6000.6.10f1 running as LinuxEditor.");
        }

        private static void VerifySceneDestinationAbsent(string projectRoot)
        {
            const string scenePath = "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity";
            string full = ProjectPath(projectRoot, scenePath);
            if (File.Exists(full) || Directory.Exists(full) || File.Exists(full + ".meta") ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                throw new IOException("The F10 diagnostic scene destination is occupied; existing content was preserved.");
        }

        private static void VerifyImportedAssets(string projectRoot)
        {
            foreach (string path in RequiredAssetPaths)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !AssetDatabase.Contains(asset) ||
                    !string.Equals(AssetDatabase.GetAssetPath(asset), path, StringComparison.Ordinal) ||
                    !File.Exists(ProjectPath(projectRoot, path)) || !File.Exists(ProjectPath(projectRoot, path + ".meta")))
                    throw new InvalidDataException("An output was not imported at its exact AssetDatabase path: " + path);
            }
            if (!(AssetDatabase.LoadMainAssetAtPath(RequiredAssetPaths[6]) is Tile))
                throw new InvalidDataException("The shared diagnostic asset is not an imported Tile.");
        }

        private static Dictionary<string, LinuxFileRow> CaptureProjectFiles(string projectRoot)
        {
            var result = new Dictionary<string, LinuxFileRow>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rootName in InventoryRoots)
            {
                string root = ProjectPath(projectRoot, rootName);
                if (!Directory.Exists(root)) throw new DirectoryNotFoundException("A fixed inventory root is missing: " + rootName);
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop();
                    RejectSymlink(directory);
                    foreach (string child in Directory.EnumerateFileSystemEntries(directory))
                    {
                        RejectSymlink(child);
                        if (Directory.Exists(child)) { pending.Push(child); continue; }
                        string relative = RelativePath(projectRoot, child);
                        LinuxFileRow row = MeasureFile(projectRoot, relative);
                        if (row.linkCount != 1 || !identities.Add(PhysicalIdentity(row)))
                            throw new IOException("The disposable project contains a hard link or repeated inode: " + relative);
                        if (!result.TryAdd(relative, row))
                            throw new InvalidDataException("The project inventory contains a duplicate path: " + relative);
                    }
                }
            }
            foreach (string relative in RequiredExternalInventoryPaths)
            {
                LinuxFileRow row = MeasureFile(projectRoot, relative);
                if (row.linkCount != 1 || !identities.Add(PhysicalIdentity(row)))
                    throw new IOException("The disposable project contains a hard link or repeated inode: " + relative);
                if (!result.TryAdd(relative, row))
                    throw new InvalidDataException("The project inventory contains a duplicate path: " + relative);
            }
            return result;
        }

        private static HashSet<string> CaptureProjectDirectories(string projectRoot)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rootName in InventoryRoots)
            {
                string root = ProjectPath(projectRoot, rootName);
                if (!Directory.Exists(root)) throw new DirectoryNotFoundException("A fixed inventory root is missing: " + rootName);
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop();
                    RejectSymlink(directory);
                    result.Add(RelativePath(projectRoot, directory));
                    foreach (string child in Directory.EnumerateDirectories(directory)) pending.Push(child);
                }
            }
            return result;
        }

        private static F10DiagnosticLinuxDependencyClosure[] CaptureAssetDatabaseClosures(string projectRoot,
            IDictionary<string, LinuxFileRow> inventory, out string[] dependencyPaths,
            out LinuxFileRow[] dependencyFiles, out F10DiagnosticLinuxPackageDependency[] packageDependencies)
        {
            var closures = new List<F10DiagnosticLinuxDependencyClosure>(RequiredAssetPaths.Length);
            var inProjectDependencies = new SortedSet<string>(StringComparer.Ordinal);
            var externalPackageDependencies = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string assetPath in RequiredAssetPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null || !AssetDatabase.Contains(asset) ||
                    !string.Equals(AssetDatabase.GetAssetPath(asset), assetPath, StringComparison.Ordinal))
                    throw new InvalidDataException("AssetDatabase omitted a required F10 root asset: " + assetPath);

                string[] actual = AssetDatabase.GetDependencies(assetPath, true)
                    .Select(NormalizeAssetPath).Concat(new[] { assetPath })
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                closures.Add(new F10DiagnosticLinuxDependencyClosure { assetPath = assetPath, dependencyPaths = actual });
                foreach (string dependency in actual)
                {
                    if (inventory.ContainsKey(dependency)) inProjectDependencies.Add(dependency);
                    else if (dependency.StartsWith("Packages/", StringComparison.Ordinal)) externalPackageDependencies.Add(dependency);
                    else throw new InvalidDataException("An AssetDatabase dependency is not in the copy or a package: " + dependency);
                }
            }

            dependencyPaths = inProjectDependencies.ToArray();
            dependencyFiles = dependencyPaths.Select(path => inventory[path]).ToArray();
            packageDependencies = CaptureExternalPackageDependencies(externalPackageDependencies);
            return closures.ToArray();
        }

        private static F10DiagnosticLinuxPackageDependency[] CaptureExternalPackageDependencies(IEnumerable<string> paths)
        {
            var groups = new SortedDictionary<string, PackageDependencyBuild>(StringComparer.Ordinal);
            foreach (string assetPath in paths)
            {
                PackageInfo info = PackageInfo.FindForAssetPath(assetPath);
                if (info == null || info.source != PackageSource.BuiltIn || string.IsNullOrWhiteSpace(info.resolvedPath) ||
                    string.IsNullOrWhiteSpace(info.assetPath) || !assetPath.StartsWith(info.assetPath + "/", StringComparison.Ordinal))
                    throw new InvalidDataException("An external AssetDatabase dependency is not a measured Editor BuiltIn package: " + assetPath);
                string key = info.name + "\0" + info.resolvedPath;
                if (!groups.TryGetValue(key, out PackageDependencyBuild group))
                {
                    group = new PackageDependencyBuild(info);
                    groups.Add(key, group);
                }
                string relative = assetPath.Substring(info.assetPath.Length + 1);
                string full = Path.GetFullPath(Path.Combine(info.resolvedPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                string rootPrefix = CanonicalPath(info.resolvedPath) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(rootPrefix, StringComparison.Ordinal) || !File.Exists(full))
                    throw new InvalidDataException("A BuiltIn package dependency escaped its exact resolved root: " + assetPath);
                group.Add(assetPath, relative, full);
            }
            return groups.Values.Select(value => value.ToEvidence()).ToArray();
        }

        private static F10DiagnosticLinuxGeneratorFile[] MeasureGeneratorSources(string projectRoot)
        {
            return GeneratorSourcePaths.OrderBy(path => path, StringComparer.Ordinal).Select(path =>
            {
                string full = ProjectPath(projectRoot, path);
                if (!File.Exists(full)) throw new FileNotFoundException("A required F10 generator source is missing.", full);
                return new F10DiagnosticLinuxGeneratorFile { path = path, sha256 = HashFile(full) };
            }).ToArray();
        }

        private static void VerifyReceiptRows(string projectRoot, F10DiagnosticLinuxMaterializationReceipt receipt,
            IDictionary<string, LinuxFileRow> inventory)
        {
            foreach (LinuxFileRow row in receipt.outputs.Concat(receipt.generatedDirectoryMetas).Concat(receipt.dependencyFiles))
            {
                LinuxFileRow actual = MeasureFile(projectRoot, row.path);
                if (!RowsEqual(row, actual) || !inventory.TryGetValue(row.path, out LinuxFileRow recorded) || !RowsEqual(row, recorded))
                    throw new InvalidDataException("A Linux receipt row failed physical readback: " + row.path);
            }
            foreach (F10DiagnosticLinuxGeneratorFile generator in receipt.generatorFiles)
                if (!string.Equals(HashFile(ProjectPath(projectRoot, generator.path)), generator.sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("A generator source hash changed before receipt write: " + generator.path);
        }

        private static LinuxFileRow MeasureFile(string projectRoot, string relative)
        {
            string full = ProjectPath(projectRoot, relative);
            StatxFileMetadata physical = ReadStatx(full, requireRegularFile: true);
            if (physical.linkCount != 1)
                throw new IOException("Linux materialization refuses hard-linked files: " + relative);
            return new LinuxFileRow
            {
                path = relative,
                sha256 = HashFile(full),
                length = physical.length,
                deviceMajor = physical.deviceMajor,
                deviceMinor = physical.deviceMinor,
                inode = physical.inode,
                linkCount = physical.linkCount
            };
        }

        private static StatxFileMetadata ReadStatx(string path, bool requireRegularFile)
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Linux statx parsing requires a little-endian runner.");
            RejectSymlink(path);
            byte[] buffer = new byte[256];
            const uint statxBasicStats = 0x7ff;
            const uint statxInoAndNlink = 0x104;
            int result = Statx(-100, Path.GetFullPath(path), 0x100, statxBasicStats, buffer);
            if (result != 0) throw new IOException("Linux statx failed for: " + path,
                Marshal.GetExceptionForHR(Marshal.GetLastWin32Error()));
            uint mask = BitConverter.ToUInt32(buffer, 0);
            if ((mask & statxInoAndNlink) != statxInoAndNlink)
                throw new IOException("Linux statx did not provide inode/link count: " + path);
            ushort mode = BitConverter.ToUInt16(buffer, 28);
            if (requireRegularFile && (mode & 0xf000) != 0x8000)
                throw new IOException("Linux inventory contains a non-regular file: " + path);
            return new StatxFileMetadata
            {
                linkCount = BitConverter.ToUInt32(buffer, 16),
                mode = mode,
                inode = checked((long)BitConverter.ToUInt64(buffer, 32)),
                length = checked((long)BitConverter.ToUInt64(buffer, 40)),
                deviceMajor = BitConverter.ToUInt32(buffer, 136),
                deviceMinor = BitConverter.ToUInt32(buffer, 140)
            };
        }

        private static void WriteCreateOnly(string path, string text)
        {
            if (File.Exists(path) || Directory.Exists(path))
                throw new IOException("Create-only Linux receipt destination is occupied; prior bytes were preserved.");
            byte[] bytes = StrictUtf8.GetBytes(text ?? throw new ArgumentNullException(nameof(text)));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            StatxFileMetadata metadata = ReadStatx(path, requireRegularFile: true);
            if (metadata.linkCount != 1 || metadata.length != bytes.LongLength ||
                !string.Equals(HashFile(path), HashBytes(bytes), StringComparison.Ordinal))
                throw new IOException("Linux materialization receipt failed create-only physical readback; it was preserved.");
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOf(':') >= 0)
                throw new InvalidDataException("AssetDatabase returned an unsafe project-relative dependency path.");
            string normalized = path.Replace('\\', '/');
            if (normalized.Split('/').Any(segment => segment.Length == 0 || segment == "." || segment == ".."))
                throw new InvalidDataException("AssetDatabase returned an unsafe dependency path: " + path);
            return normalized;
        }

        private static string ProjectPath(string root, string relative)
        {
            string normalized = NormalizeAssetPath(relative);
            string full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = CanonicalPath(root) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Linux receipt path escaped the disposable project root: " + relative);
            return full;
        }

        private static string RelativePath(string root, string fullPath)
        {
            string prefix = CanonicalPath(root) + Path.DirectorySeparatorChar;
            string full = CanonicalPath(fullPath);
            if (!full.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Linux materialization path escaped the disposable project: " + fullPath);
            return full.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void RejectSymlink(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Expected path is missing.", path);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linux materialization rejects symbolic links: " + path);
        }

        private static void RejectSymlinkAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || Directory.Exists(current)) RejectSymlink(current);
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal)) break;
                current = parent;
            }
        }

        private static bool RowsEqual(LinuxFileRow left, LinuxFileRow right) => left != null && right != null &&
            string.Equals(left.path, right.path, StringComparison.Ordinal) &&
            string.Equals(left.sha256, right.sha256, StringComparison.Ordinal) && left.length == right.length &&
            left.deviceMajor == right.deviceMajor && left.deviceMinor == right.deviceMinor &&
            left.inode == right.inode && left.linkCount == right.linkCount;

        private static string PhysicalIdentity(LinuxFileRow row) => row.deviceMajor.ToString(CultureInfo.InvariantCulture) + ":" +
            row.deviceMinor.ToString(CultureInfo.InvariantCulture) + ":" + row.inode.ToString(CultureInfo.InvariantCulture);

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return ToLowerHex(sha.ComputeHash(stream));
        }

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return ToLowerHex(sha.ComputeHash(bytes));
        }

        private static string ToLowerHex(IEnumerable<byte> bytes) =>
            string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

        private static string CanonicalPath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full) ?? string.Empty;
            while (full.Length > root.Length && (full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                full.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)))
                full = full.Substring(0, full.Length - 1);
            return full;
        }

        [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
        private static extern int Statx(int directoryFileDescriptor,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, [Out] byte[] buffer);

        [Serializable]
        private sealed class F10DiagnosticLinuxMaterializationReceipt
        {
            public string schema;
            public int schemaVersion;
            public string status;
            public string projectRoot;
            public string unityVersion;
            public LinuxFileRow[] outputs;
            public LinuxFileRow[] generatedDirectoryMetas;
            public F10DiagnosticLinuxGeneratorFile[] generatorFiles;
            public F10DiagnosticLinuxDependencyClosure[] dependencyClosures;
            public string[] dependencyPaths;
            public LinuxFileRow[] dependencyFiles;
            public F10DiagnosticLinuxPackageDependency[] packageDependencies;
        }

        [Serializable]
        private sealed class LinuxFileRow
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
        private sealed class F10DiagnosticLinuxGeneratorFile { public string path; public string sha256; }
        [Serializable]
        private sealed class F10DiagnosticLinuxDependencyClosure { public string assetPath; public string[] dependencyPaths; }

        [Serializable]
        private sealed class F10DiagnosticLinuxPackageDependency
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
        private sealed class F10DiagnosticLinuxPackageDependencyFile
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

        private sealed class PackageDependencyBuild
        {
            private readonly PackageInfo info;
            private readonly SortedDictionary<string, F10DiagnosticLinuxPackageDependencyFile> files =
                new SortedDictionary<string, F10DiagnosticLinuxPackageDependencyFile>(StringComparer.Ordinal);
            private readonly SortedSet<string> assetPaths = new SortedSet<string>(StringComparer.Ordinal);

            internal PackageDependencyBuild(PackageInfo packageInfo) { info = packageInfo; }

            internal void Add(string assetPath, string relativePath, string fullPath)
            {
                LinuxFileRow measured = MeasureExternalFile(fullPath, assetPath);
                files[relativePath] = new F10DiagnosticLinuxPackageDependencyFile
                {
                    relativePath = relativePath,
                    projectRelativePath = assetPath,
                    roles = new[] { "asset-database-dependency" },
                    assetPaths = new[] { assetPath },
                    sha256 = measured.sha256,
                    length = measured.length,
                    identity = PhysicalIdentity(measured),
                    linkCount = measured.linkCount
                };
                assetPaths.Add(assetPath);
            }

            internal F10DiagnosticLinuxPackageDependency ToEvidence() => new F10DiagnosticLinuxPackageDependency
            {
                packageName = info.name,
                packageVersion = info.version,
                packageSource = info.source.ToString(),
                packageId = info.packageId,
                packageAssetPath = info.assetPath,
                resolvedRootKind = "builtin-editor",
                resolvedRootRelativePath = "@editor/Data/Resources/PackageManager/BuiltInPackages/" + info.name,
                resolvedRootAbsolutePath = Path.GetFullPath(info.resolvedPath),
                resolvedRootIdentity = DirectoryPhysicalIdentity(info.resolvedPath),
                dependencyAssetPaths = assetPaths.ToArray(),
                files = files.Values.ToArray()
            };
        }

        private static LinuxFileRow MeasureExternalFile(string path, string projectRelativePath)
        {
            StatxFileMetadata physical = ReadStatx(path, requireRegularFile: true);
            if (physical.linkCount != 1) throw new IOException("Linux package evidence refuses hard-linked files: " + path);
            return new LinuxFileRow
            {
                path = projectRelativePath,
                sha256 = HashFile(path),
                length = physical.length,
                deviceMajor = physical.deviceMajor,
                deviceMinor = physical.deviceMinor,
                inode = physical.inode,
                linkCount = physical.linkCount
            };
        }

        private static string DirectoryPhysicalIdentity(string path)
        {
            RejectSymlink(path);
            StatxFileMetadata metadata = ReadStatx(path, requireRegularFile: false);
            if ((metadata.mode & 0xf000) != 0x4000)
                throw new IOException("A measured Linux package root is not a plain directory: " + path);
            return metadata.deviceMajor.ToString(CultureInfo.InvariantCulture) + ":" +
                metadata.deviceMinor.ToString(CultureInfo.InvariantCulture) + ":" +
                metadata.inode.ToString(CultureInfo.InvariantCulture);
        }

        private struct StatxFileMetadata
        {
            internal uint deviceMajor, deviceMinor, linkCount;
            internal ushort mode;
            internal long inode, length;
        }
    }
}
#endif
