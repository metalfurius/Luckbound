using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Rooms;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Luckbound.Editor
{
    /// <summary>
    /// Writes a create-only physical descriptor from an explicitly identified Windows disposable copy.
    /// This exporter does not publish or install the diagnostic content.
    /// </summary>
    public static class F10TechnicalPhysicalDescriptorExporter
    {
        private const string ExpectedLiveCheckoutName = "Luckbound";
        private const string DisposableCopiesRootRelativePath = "Logs/F10DisposableArt-20261116/copies";
        private const string MarkerRelativePath = "Logs/F10DisposableArt-20261116/copy-certificate.json";
        private const string PreflightReceiptRelativePath = "Logs/F10DisposableArt-20261116/copy-preflight-receipt.json";
        private const string OutputRelativePath = "Logs/F10V7PhysicalExport-20261116/candidate/revisions";
        private const string ExpectedUnityVersion = "6000.6.7f1";
        private const string PhysicalCacheDependency = "cache.exploration";
        private const string PhysicalProjectileDependency = "projectile.prefab";
        private const string PhysicalCustodianConfinementDependency = "custodian.confinement";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly string[] RoomDefinitionPaths =
        {
            "Assets/Resources/Luckbound/Data/Rooms/F10TutorialRoom.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioCombatC1Room.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioExplorationE1Room.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioGuardianRoom.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10SafeExtractionRoom.json"
        };

        private static readonly string[] RoomPrefabNames =
        {
            "F10TutorialDiagnostic.prefab",
            "F10PatioCombatC1Diagnostic.prefab",
            "F10PatioExplorationE1Diagnostic.prefab",
            "F10PatioGuardianDiagnostic.prefab",
            "F10SafeExtractionDiagnostic.prefab"
        };

        private static readonly string[] ProjectInventoryRoots = { "Assets", "Packages", "ProjectSettings" };
        private static readonly string[] UnityWriteRoots =
        {
            "Assets", "Packages", "ProjectSettings", "Library", "Temp", "UserSettings", "Logs"
        };

        [MenuItem("Luckbound/Technical Diagnostic/Export Physical Descriptor (Disposable Copy Only)")]
        private static void ExportFromMenu()
        {
            Export();
        }

        /// <summary>
        /// Explicit entry point for the separately measured Linux V7 export route.
        /// The resulting descriptor remains a technical candidate until independent review.
        /// </summary>
        public static void ExecuteV7LinuxDiagnosticExport()
        {
            if (Application.platform != RuntimePlatform.LinuxEditor)
                throw new InvalidOperationException("The V7 Linux diagnostic export entry point is Linux Editor only.");
            Export();
        }

        /// <summary>Exports into a new immutable revision after all isolation gates pass.</summary>
        public static string Export()
        {
            string projectRoot = CanonicalPath(Directory.GetParent(Application.dataPath).FullName);
            DisposableCopyIdentity identity = VerifyDisposableCopy(projectRoot);
            VerifyRevisionRoot(projectRoot);
            VerifyGeneratorDestinationsAreAbsent(projectRoot);
            string revisionsPath = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Application.platform == RuntimePlatform.LinuxEditor && Directory.EnumerateFileSystemEntries(revisionsPath).Any())
                throw new IOException("The V7 Linux export requires an empty create-only revision root; preserving existing revisions.");

            string revisionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string outputPath = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar), "revision-" + revisionId);
            if (Directory.Exists(outputPath) || File.Exists(outputPath))
                throw new IOException("Physical revision already exists; preserving it without overwrite: " + outputPath);
            Directory.CreateDirectory(outputPath);

            Dictionary<string, FileRecord> frozenInventory = CaptureProjectInventory(projectRoot);
            string frozenInventoryDigest = DigestInventory(frozenInventory);
            Dictionary<string, AssemblyRecord> assemblyManifest = CaptureAssemblyManifest(projectRoot);
            List<CompiledAssemblyRecord> compiledAssemblies = CaptureCompiledAssemblyManifest(projectRoot);
            ValidateRequiredProductionLoaders();

            IReadOnlyList<GameObject> generated = F10TechnicalDiagnosticPrefabGenerator.GenerateAll();
            GameObject generatedPlayer = F10TechnicalDiagnosticPlayerPrefabGenerator.Generate();
            GameObject generatedCache = F10PhysicalGoldCacheDiagnosticPrefabGenerator.Generate();

            Dictionary<string, FileRecord> postGenerationInventory = CaptureProjectInventory(projectRoot);
            ValidateOnlyExpectedDiagnosticFilesWereAdded(frozenInventory, postGenerationInventory);
            ValidateFrozenInventoryUnchanged(projectRoot, frozenInventory);

            string[] roomPrefabPaths = generated.Select(AssetDatabase.GetAssetPath).ToArray();
            if (roomPrefabPaths.Length != RoomPrefabNames.Length || roomPrefabPaths.Any(string.IsNullOrEmpty))
                throw new InvalidOperationException("The production F10 generator did not return the complete five-prefab set.");

            string[] projectilePrefabPaths = FindMaterializedProjectilePrefabs();
            List<object> assetRoots = BuildAssetRootRows(roomPrefabPaths, generatedPlayer, generatedCache, projectilePrefabPaths);
            List<object> dependencyRows = CaptureRootDependencyClosures(assetRoots);
            List<CanonicalBundleDependency> bundleDependencies = CanonicalizeBundleDependencies(dependencyRows);
            object[] packageDependencies = CaptureV7PackageDependencyEvidence(dependencyRows);
            List<object> roomRows = CaptureGeneratedRooms(roomPrefabPaths);
            List<object> physicalPrefabRows = CapturePhysicalPrefabs(generatedPlayer, generatedCache, projectilePrefabPaths);
            List<object> missingPhysicalDependencies = BuildMissingPhysicalDependencies(generatedCache, projectilePrefabPaths);
            bool installable = missingPhysicalDependencies.Count == 0;

            var sourceManifestEnvelope = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assemblies", assemblyManifest.Values.OrderBy(row => row.Name, StringComparer.Ordinal).Select(row => row.ToJson()).ToArray() },
                { "compiledAssemblies", compiledAssemblies.OrderBy(row => row.Name, StringComparer.Ordinal).Select(row => row.ToJson()).ToArray() },
                { "unityEditorVersion", Application.unityVersion },
                { "unityProjectVersion", ReadProjectUnityVersion(projectRoot) },
                { "schema", "luckbound.f10.v7.physical-export.assemblies.v1" }
            };
            byte[] assemblyManifestBytes = CanonicalJson.Serialize(sourceManifestEnvelope);
            string assemblyManifestDigest = Sha256Hex(assemblyManifestBytes);
            byte[] inventoryBytes = CanonicalJson.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "digestAlgorithm", "SHA-256" },
                { "entries", frozenInventory.Values.OrderBy(row => row.Path, StringComparer.Ordinal).Select(row => row.ToJson()).ToArray() },
                { "inventorySha256", frozenInventoryDigest },
                { "roots", ProjectInventoryRoots },
                { "schema", "luckbound.f10.physical-export.frozen-inventory.v1" }
            });

            var evidence = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assetRoots", assetRoots.OrderBy(row => ((Dictionary<string, object>)row)["rootPath"].ToString(), StringComparer.Ordinal).ToArray() },
                { "assemblyManifestSha256", assemblyManifestDigest },
                { "custodianConfinement", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "recordedMissingDependency", missingPhysicalDependencies.Any(row => ((Dictionary<string, object>)row)["dependencyId"].ToString() == PhysicalCustodianConfinementDependency) },
                        { "boundaryInstallerSourcePath", "Assets/Luckbound/Scripts/App/F10GuardianPhysicalBoundaryInstaller.cs" },
                        { "boundaryInstallerSourceMeasured", true },
                        { "runtimeSpawnInsideBoundsObserved", false },
                        { "continuousPhysicalConfinementObserved", false }
                    }
                },
                { "dependencies", dependencyRows.OrderBy(row => ((Dictionary<string, object>)row)["assetPath"].ToString(), StringComparer.Ordinal).ToArray() },
                { "disposableCopy", identity.ToJson() },
                { "exportedAtUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) },
                { "frozenInventorySha256", frozenInventoryDigest },
                { "installable", installable },
                { "missingPhysicalDependencies", missingPhysicalDependencies },
                { "physicalCache", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "assetPath", F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath },
                        { "prefabMaterializedAndMeasured", generatedCache != null && AssetDatabase.Contains(generatedCache) },
                        { "roomPlacementAndPickupObserved", false }
                    }
                },
                { "physics2DGravity", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "x", Physics2D.gravity.x }, { "y", Physics2D.gravity.y }
                    }
                },
                { "rooms", roomRows.OrderBy(row => ((Dictionary<string, object>)row)["prefabPath"].ToString(), StringComparer.Ordinal).ToArray() },
                { "physicalPrefabs", physicalPrefabRows.OrderBy(row => ((Dictionary<string, object>)row)["assetPath"].ToString(), StringComparer.Ordinal).ToArray() },
                { "revisionId", revisionId },
                { "schema", "luckbound.f10.v7.physical-export.descriptor.v1" },
                { "unityDependencyHashUse", "diagnostic-only" },
                { "unityEditorVersion", Application.unityVersion },
                { "unityProjectVersion", ReadProjectUnityVersion(projectRoot) },
                { "validationClaims", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "generatorInvoked", true },
                        { "roomTemplatePrefabValidatorInvokedForAllFive", true },
                        { "unityPhysicsSimulationExecuted", false },
                        { "victoryOrRewardClaimed", false }
                    }
                }
            };

            byte[] evidenceBytes = CanonicalJson.Serialize(evidence);
            Dictionary<string, FileRecord> finalInventory = CaptureProjectInventory(projectRoot);
            ValidateOnlyExpectedDiagnosticFilesWereAdded(frozenInventory, finalInventory);
            ValidateFrozenInventoryUnchanged(projectRoot, frozenInventory);

            List<BundlePayloadRecord> payloads = MaterializeBundlePayload(projectRoot, outputPath,
                assetRoots, bundleDependencies, assemblyManifest, compiledAssemblies,
                assemblyManifestBytes, inventoryBytes, evidenceBytes);
            BundleManifestData bundleManifest = CreateBundleManifest(revisionId, payloads,
                assetRoots, bundleDependencies, missingPhysicalDependencies, installable, packageDependencies);
            byte[] bundleManifestBytes = CanonicalJson.Serialize(bundleManifest.ToJson());
            WriteNewFile(Path.Combine(outputPath, "bundle-manifest.json"), bundleManifestBytes);
            return outputPath;
        }

        internal static FileRecord ReadStrictFileRecord(string logicalPath, string absolutePath)
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

        internal static void VerifyDisposableWorkspaceForAcceptance()
        {
            string projectRoot = CanonicalPath(Directory.GetParent(Application.dataPath).FullName);
            F10V7VerifiedPhysicalInputs.VerifyWorkspaceForAcceptance(projectRoot);
        }

        internal static void VerifyOwnedDependencyFileForAcceptanceMutation(string absolutePath)
        {
            RejectReparsePoint(absolutePath);
            PhysicalFileInformation information = ReadPhysicalFileInformation(absolutePath);
            if (information.LinkCount != 1)
                throw new IOException("Acceptance harness refuses to mutate a dependency fixture with hard links: " + absolutePath);
        }

        private static DisposableCopyIdentity VerifyDisposableCopy(string projectRoot)
        {
            F10V7VerifiedPhysicalInputs.CopyVerification copy = Application.platform == RuntimePlatform.LinuxEditor
                ? F10V7VerifiedPhysicalInputs.VerifyV7ExportPreflight(projectRoot)
                : F10V7VerifiedPhysicalInputs.VerifyWorkspaceForAcceptance(projectRoot);
            return new DisposableCopyIdentity(copy.SourceRoot, copy.ProjectRoot, copy.MarkerPath, copy.IsolationChecks);
        }

        private static object[] CaptureV7PackageDependencyEvidence(IReadOnlyList<object> dependencyRows)
        {
            string[] packagePaths = dependencyRows.Cast<Dictionary<string, object>>()
                .SelectMany(row => ((IEnumerable<object>)row["dependencyEntries"]).Cast<Dictionary<string, object>>())
                .Select(row => row["assetPath"].ToString())
                .Where(path => path.StartsWith("Packages/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (Application.platform != RuntimePlatform.LinuxEditor)
                return Array.Empty<object>();
            return F10V7VerifiedPhysicalInputs.CaptureV7LinuxPackageDependencies(packagePaths);
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

        private static void VerifyRevisionRoot(string projectRoot)
        {
            string revisionsPath = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string candidateRoot = Path.Combine(projectRoot, "Logs/F10V7PhysicalExport-20261116/candidate");
            if (!Directory.Exists(candidateRoot) || !PathsEqual(CanonicalPath(Directory.GetParent(revisionsPath).FullName), CanonicalPath(candidateRoot)))
                throw new DirectoryNotFoundException("The staged candidate directory must already exist in the disposable copy.");
            RejectReparsePointsFromVolumeRoot(candidateRoot);
            if (File.Exists(revisionsPath))
                throw new IOException("Physical revision root is an occupied file; preserving it: " + revisionsPath);
            if (!Directory.Exists(revisionsPath)) Directory.CreateDirectory(revisionsPath);
            RejectReparsePointsFromVolumeRoot(revisionsPath);
        }

        private static void VerifyGeneratorDestinationsAreAbsent(string projectRoot)
        {
            string diagnosticRoot = Path.Combine(projectRoot, F10TechnicalDiagnosticPrefabGenerator.DiagnosticRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(diagnosticRoot))
                throw new IOException("Technical diagnostic root is occupied by a file; preserving it.");
            if (Directory.Exists(diagnosticRoot))
            {
                RejectReparsePoint(diagnosticRoot);
                HashSet<string> approvedExistingFiles = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs",
                    "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs.meta",
                    "Assets/Luckbound/Editor/Diagnostic/F01_BellatorScaleReference.png",
                    "Assets/Luckbound/Editor/Diagnostic/F01_BellatorScaleReference.png.meta"
                };
                foreach (string entry in Directory.EnumerateFileSystemEntries(diagnosticRoot))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
                        throw new IOException("Diagnostic root contains an unexpected directory or reparse point; preserving it: " + entry);
                    string relativePath = MakeProjectRelative(projectRoot, entry);
                    if (!approvedExistingFiles.Contains(relativePath))
                        throw new IOException("Diagnostic root contains unapproved pre-existing content; preserving it: " + relativePath);
                }
            }
            string tilePath = Path.Combine(projectRoot, F10TechnicalDiagnosticPrefabGenerator.DiagnosticTilePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(tilePath) || File.Exists(tilePath + ".meta") || AssetDatabase.LoadAssetAtPath<Tile>(F10TechnicalDiagnosticPrefabGenerator.DiagnosticTilePath) != null)
                throw new IOException("Diagnostic Tile destination is not empty; preserving existing assets.");
            foreach (string name in RoomPrefabNames)
            {
                string assetPath = F10TechnicalDiagnosticPrefabGenerator.DiagnosticF10Folder + "/" + name;
                string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath) || File.Exists(fullPath + ".meta") || AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                    throw new IOException("Diagnostic prefab destination is not empty; preserving existing asset: " + assetPath);
            }
            string playerPath = F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath;
            string playerFullPath = Path.Combine(projectRoot, playerPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(playerFullPath) || File.Exists(playerFullPath + ".meta") || AssetDatabase.LoadAssetAtPath<GameObject>(playerPath) != null)
                throw new IOException("Diagnostic player destination is not empty; preserving existing asset: " + playerPath);
            string cacheFolder = F10PhysicalGoldCacheDiagnosticPrefabGenerator.DiagnosticFolder;
            string cachePath = F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath;
            string cacheFullPath = Path.Combine(projectRoot, cachePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(Path.Combine(projectRoot, cacheFolder.Replace('/', Path.DirectorySeparatorChar))) ||
                File.Exists(cacheFullPath) || File.Exists(cacheFullPath + ".meta") || AssetDatabase.LoadAssetAtPath<GameObject>(cachePath) != null)
                throw new IOException("Diagnostic cache destination is not empty; preserving existing asset: " + cachePath);
        }

        private static void ValidateRequiredProductionLoaders()
        {
            LoadAssetAtPath<TextAsset>(F10TechnicalDiagnosticPrefabGenerator.RouteDefinitionPath);
            foreach (string path in RoomDefinitionPaths) LoadAssetAtPath<TextAsset>(path);
            LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/C08Arena.json");
            LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json");

            TextAsset route = LoadAssetAtPath<TextAsset>(F10TechnicalDiagnosticPrefabGenerator.RouteDefinitionPath);
            ExpeditionSampleRouteJsonLoader.Load(route.text);
            foreach (string path in RoomDefinitionPaths)
                RoomTemplateJsonLoader.Load(LoadAssetAtPath<TextAsset>(path).text);
            CombatEncounterJsonLoader.Load(LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/C08Arena.json").text);
            CustodianBossJsonLoader.Load(LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json").text);
        }

        private static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null || !AssetDatabase.Contains(asset) || !string.Equals(AssetDatabase.GetAssetPath(asset), assetPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Required imported source asset is missing or not at its exact path: " + assetPath);
            string absolute = ProjectPath(assetPath);
            if (!File.Exists(absolute) || !File.Exists(absolute + ".meta"))
                throw new FileNotFoundException("Required source asset bytes and .meta must both exist.", absolute);
            return asset;
        }

        private static Dictionary<string, FileRecord> CaptureProjectInventory(string projectRoot)
        {
            var rows = new Dictionary<string, FileRecord>(StringComparer.Ordinal);
            foreach (string root in ProjectInventoryRoots)
            {
                string fullRoot = Path.Combine(projectRoot, root);
                if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException("Frozen project inventory root is missing: " + root);
                foreach (string file in EnumerateFilesNoReparse(fullRoot))
                {
                    string logical = MakeProjectRelative(projectRoot, file);
                    rows.Add(logical, ReadStrictFileRecord(logical, file));
                }
            }
            return rows;
        }

        private static Dictionary<string, AssemblyRecord> CaptureAssemblyManifest(string projectRoot)
        {
            string[] sourceRoots = { "Assets", "Packages" };
            string[] asmdefPaths = sourceRoots.SelectMany(root => EnumerateFilesNoReparse(Path.Combine(projectRoot, root)))
                .Where(path => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .Select(path => MakeProjectRelative(projectRoot, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var definitions = new List<AssemblyDefinitionRecord>();
            foreach (string path in asmdefPaths)
            {
                string text = StrictUtf8.GetString(File.ReadAllBytes(ProjectPath(path)));
                AssemblyDefinitionData data = JsonUtility.FromJson<AssemblyDefinitionData>(text);
                if (data == null || string.IsNullOrWhiteSpace(data.name))
                    throw new InvalidDataException("Assembly definition could not be identified: " + path);
                FileRecord file = ReadStrictFileRecord(path, ProjectPath(path));
                definitions.Add(new AssemblyDefinitionRecord(path, data.name, file.Sha256));
            }

            var rows = new Dictionary<string, AssemblyRecord>(StringComparer.Ordinal);
            string[] sourceFiles = sourceRoots.SelectMany(root => EnumerateFilesNoReparse(Path.Combine(projectRoot, root)))
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(path => MakeProjectRelative(projectRoot, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string source in sourceFiles)
            {
                string assemblyName = ResolveAssemblyName(source, definitions);
                if (!rows.TryGetValue(assemblyName, out AssemblyRecord record))
                {
                    AssemblyDefinitionRecord definition = definitions.Where(item => item.Name == assemblyName)
                        .OrderByDescending(item => item.Path.Length).FirstOrDefault();
                    record = new AssemblyRecord(assemblyName, definition == null ? string.Empty : definition.Path,
                        definition == null ? string.Empty : definition.Sha256);
                    rows.Add(assemblyName, record);
                }
                record.Sources.Add(ReadStrictFileRecord(source, ProjectPath(source)));
            }

            foreach (AssemblyDefinitionRecord definition in definitions)
            {
                if (!rows.ContainsKey(definition.Name))
                    rows.Add(definition.Name, new AssemblyRecord(definition.Name, definition.Path, definition.Sha256));
            }
            return rows;
        }

        private static List<CompiledAssemblyRecord> CaptureCompiledAssemblyManifest(string projectRoot)
        {
            string assemblyRoot = Path.Combine(projectRoot, "Library/ScriptAssemblies");
            if (!Directory.Exists(assemblyRoot))
                throw new DirectoryNotFoundException("Unity compiled assemblies are not materialized under Library/ScriptAssemblies.");
            List<CompiledAssemblyRecord> rows = Directory.EnumerateFiles(assemblyRoot, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Select(path =>
                {
                    string logicalPath = MakeProjectRelative(projectRoot, path);
                    FileRecord record = ReadStrictFileRecord(logicalPath, path);
                    return new CompiledAssemblyRecord(Path.GetFileNameWithoutExtension(path), logicalPath, record.Length, record.Sha256);
                }).ToList();
            if (rows.Count == 0)
                throw new InvalidDataException("Unity compiled no script assemblies; refusing to emit an unmeasured assembly manifest.");
            return rows;
        }

        /// <summary>
        /// Verifies a versioned bundle against the manifest digest pinned by its caller. A true return value
        /// means every bundled byte and asset/meta pair matches; registryEligible is separate and requires
        /// the manifest to declare installable with no missing physical evidence.
        /// </summary>
        public static bool ValidateBundleIntegrity(string bundleDirectory, string expectedManifestSha256,
            out bool registryEligible, out string error)
        {
            return F10V7VerifiedPhysicalInputs.ValidateBundleIntegrity(
                bundleDirectory, expectedManifestSha256, out registryEligible, out error);
        }

        /// <summary>Reads a bundle only when its pinned manifest and all dependency bytes are valid and installable.</summary>
        public static BundleManifestData ReadInstallableBundleForRegistry(string bundleDirectory, string expectedManifestSha256)
        {
            if (!ValidateBundleIntegrity(bundleDirectory, expectedManifestSha256, out bool registryEligible, out string error))
                throw new InvalidDataException("Physical bundle verification failed: " + error);
            if (!registryEligible)
                throw new InvalidOperationException("Physical bundle is intact but cannot be offered to the registry because physical evidence is incomplete.");
            string manifestPath = Path.Combine(bundleDirectory, "bundle-manifest.json");
            return JsonUtility.FromJson<BundleManifestData>(StrictUtf8.GetString(File.ReadAllBytes(manifestPath)));
        }

        private static bool IsSafeBundlePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\\') >= 0 || Path.IsPathRooted(path) ||
                !path.StartsWith("payload/", StringComparison.Ordinal)) return false;
            return path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != ".." &&
                !segment.EndsWith(".", StringComparison.Ordinal) && !segment.EndsWith(" ", StringComparison.Ordinal) &&
                segment.All(character => character >= 0x20 && "<>:\"|?*".IndexOf(character) < 0));
        }

        private static string ResolveAssemblyName(string sourcePath, IReadOnlyList<AssemblyDefinitionRecord> definitions)
        {
            string bestName = null;
            int bestLength = -1;
            foreach (AssemblyDefinitionRecord definition in definitions)
            {
                string directory = definition.Path.Substring(0, definition.Path.LastIndexOf('/'));
                if ((sourcePath.StartsWith(directory + "/", StringComparison.Ordinal) || sourcePath == directory) && directory.Length > bestLength)
                {
                    bestName = definition.Name;
                    bestLength = directory.Length;
                }
            }
            if (bestName != null) return bestName;
            return sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ? "Assembly-CSharp" : "PackageSource/Unscoped";
        }

        private static void ValidateOnlyExpectedDiagnosticFilesWereAdded(
            IReadOnlyDictionary<string, FileRecord> before, IReadOnlyDictionary<string, FileRecord> after)
        {
            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "Assets/Luckbound/Editor/Diagnostic.meta",
                "Assets/Luckbound/Editor/Diagnostic/Shared.meta",
                "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset",
                "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset.meta",
                "Assets/Luckbound/Editor/Diagnostic/F10.meta"
            };
            foreach (string name in RoomPrefabNames)
            {
                string path = "Assets/Luckbound/Editor/Diagnostic/F10/" + name;
                allowed.Add(path);
                allowed.Add(path + ".meta");
            }
            string playerPath = F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath;
            allowed.Add(playerPath);
            allowed.Add(playerPath + ".meta");
            string cachePath = F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath;
            allowed.Add("Assets/Luckbound/Editor/Diagnostic/F10GoldCache.meta");
            allowed.Add(cachePath);
            allowed.Add(cachePath + ".meta");
            foreach (string added in after.Keys.Except(before.Keys, StringComparer.Ordinal))
            {
                if (!allowed.Contains(added))
                    throw new IOException("Generator wrote outside the exact create-only output set; preserving all files: " + added);
            }
            foreach (string removed in before.Keys.Except(after.Keys, StringComparer.Ordinal))
                throw new IOException("Generator removed a pre-existing project file; export halted: " + removed);
        }

        private static void ValidateFrozenInventoryUnchanged(string projectRoot, IReadOnlyDictionary<string, FileRecord> frozen)
        {
            foreach (FileRecord row in frozen.Values)
            {
                FileRecord current = ReadStrictFileRecord(row.Path, ProjectPath(row.Path));
                if (current.Length != row.Length || !string.Equals(current.Sha256, row.Sha256, StringComparison.Ordinal))
                    throw new IOException("A frozen Assets/Packages/ProjectSettings input changed during export: " + row.Path);
            }
        }

        private static string DigestInventory(IReadOnlyDictionary<string, FileRecord> inventory)
        {
            byte[] bytes = CanonicalJson.Serialize(inventory.Values.OrderBy(row => row.Path, StringComparer.Ordinal)
                .Select(row => row.ToJson()).ToArray());
            return Sha256Hex(bytes);
        }

        private static List<object> BuildAssetRootRows(IReadOnlyList<string> generatedRoomPaths,
            GameObject generatedPlayer, GameObject generatedCache, IReadOnlyList<string> projectilePrefabPaths)
        {
            var paths = new List<string>(generatedRoomPaths);
            paths.Add(CombatArenaScaffoldGenerator.SlimePrefabPath);
            paths.Add(CombatArenaScaffoldGenerator.HuesoguardiaPrefabPath);
            paths.Add(CustodianArenaScaffoldGenerator.BossPrefabPath);
            paths.Add(CustodianArenaScaffoldGenerator.WavePrefabPath);
            paths.Add(F10TechnicalDiagnosticPrefabGenerator.StonePrototypeSpritePath);
            paths.Add(F10TechnicalDiagnosticPrefabGenerator.MovementDemoPlayerSourcePath);
            paths.Add(F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath);
            paths.Add(F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath);
            paths.AddRange(projectilePrefabPaths);
            paths.Add("Assets/Luckbound/Scripts/App/F10GuardianPhysicalBoundaryInstaller.cs");
            paths.Add("Assets/Luckbound/Scripts/App/F10ExplorationGoldCacheBinding2D.cs");
            paths.Add("Assets/Luckbound/Scripts/Gameplay/Inventory/ExplorationGoldCachePickup2D.cs");
            paths.Add("Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json");
            paths.Add("Assets/Resources/Luckbound/Data/Encounters/C08Arena.json");
            paths.Add(F10TechnicalDiagnosticPrefabGenerator.RouteDefinitionPath);
            paths.AddRange(RoomDefinitionPaths);

            if (generatedPlayer == null || !string.Equals(AssetDatabase.GetAssetPath(generatedPlayer), F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath, StringComparison.Ordinal))
                throw new InvalidOperationException("The actual F10 diagnostic player prefab was not materialized at its declared path.");
            if (generatedCache == null || !string.Equals(AssetDatabase.GetAssetPath(generatedCache), F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath, StringComparison.Ordinal))
                throw new InvalidOperationException("The actual F10 diagnostic cache prefab was not materialized at its declared path.");

            var rows = new List<object>();
            foreach (string path in paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !AssetDatabase.Contains(asset) || !string.Equals(AssetDatabase.GetAssetPath(asset), path, StringComparison.Ordinal))
                    throw new InvalidOperationException("Asset root is missing from the imported disposable project: " + path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid)) throw new InvalidOperationException("Asset root has no GUID: " + path);
                rows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "assetPath", path }, { "guid", guid }, { "kind", asset.GetType().FullName }, { "rootPath", path }
                });
            }
            return rows;
        }

        private static List<object> CaptureRootDependencyClosures(IReadOnlyList<object> assetRoots)
        {
            var rows = new List<object>();
            foreach (Dictionary<string, object> root in assetRoots.Cast<Dictionary<string, object>>())
            {
                string rootPath = root["rootPath"].ToString();
                string[] dependencies = AssetDatabase.GetDependencies(rootPath, true)
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (!dependencies.Contains(rootPath, StringComparer.Ordinal))
                    throw new InvalidOperationException("Unity dependency closure omitted its root asset: " + rootPath);

                var entries = new List<object>();
                foreach (string dependency in dependencies) entries.Add(CaptureDependencyEntry(dependency));
                rows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "assetPath", rootPath },
                    { "dependencyEntries", entries },
                    { "unityDependencyHash", AssetDatabase.GetAssetDependencyHash(rootPath).ToString() },
                    { "unityDependencyHashIsDiagnosticOnly", true }
                });
            }
            return rows;
        }

        private static List<CanonicalBundleDependency> CanonicalizeBundleDependencies(IReadOnlyList<object> dependencyRows)
        {
            if (dependencyRows == null) throw new ArgumentNullException(nameof(dependencyRows));

            var dependencies = new SortedDictionary<string, CanonicalBundleDependency>(StringComparer.Ordinal);
            foreach (Dictionary<string, object> closure in dependencyRows.Cast<Dictionary<string, object>>())
            {
                foreach (Dictionary<string, object> dependency in ((IEnumerable<object>)closure["dependencyEntries"]).Cast<Dictionary<string, object>>())
                {
                    string assetPath = dependency["assetPath"] as string;
                    ValidateBundleDependencyAssetPath(assetPath);
                    string guid = dependency["guid"] as string;
                    if (string.IsNullOrWhiteSpace(guid) || !string.Equals(guid, guid.Trim(), StringComparison.Ordinal))
                        throw new InvalidDataException("A dependency has an invalid GUID for asset path: " + assetPath);

                    BundleDependencyFileRecord assetBytes = ReadBundleDependencyFileRecord(dependency, "assetBytes", assetPath, assetPath);
                    BundleDependencyFileRecord metaBytes = ReadBundleDependencyFileRecord(dependency, "metaBytes", assetPath + ".meta", assetPath);
                    var candidate = new CanonicalBundleDependency(assetPath, guid, assetBytes, metaBytes);

                    if (dependencies.TryGetValue(assetPath, out CanonicalBundleDependency existing))
                    {
                        if (!existing.HasSamePhysicalRecords(candidate))
                            throw new InvalidDataException("Root dependency descriptors disagree for asset path: " + assetPath);
                        continue;
                    }
                    dependencies.Add(assetPath, candidate);
                }
            }

            return dependencies.Values.ToList();
        }

        private static void ValidateBundleDependencyAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !(assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.StartsWith("Packages/", StringComparison.Ordinal)) ||
                assetPath.IndexOf('\\') >= 0 ||
                !IsSafeBundlePath("payload/assets/" + assetPath) ||
                !IsSafeBundlePath("payload/assets/" + assetPath + ".meta"))
                throw new InvalidDataException("Dependency path is unsafe or non-canonical: " + (assetPath ?? "<missing>"));
        }

        private static BundleDependencyFileRecord ReadBundleDependencyFileRecord(Dictionary<string, object> dependency,
            string key, string expectedPath, string assetPath)
        {
            if (!dependency.TryGetValue(key, out object value) || !(value is Dictionary<string, object> record))
                throw new InvalidDataException("Dependency file record is missing for asset path: " + assetPath);
            if (!record.TryGetValue("path", out object pathValue) || !(pathValue is string path) ||
                !string.Equals(path, expectedPath, StringComparison.Ordinal))
                throw new InvalidDataException("Dependency file record path disagrees for asset path: " + assetPath);
            if (!record.TryGetValue("sha256", out object shaValue) || !(shaValue is string sha256) ||
                sha256.Length != 64 || sha256.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
                throw new InvalidDataException("Dependency file record SHA-256 is invalid for asset path: " + assetPath);
            if (!record.TryGetValue("bytes", out object bytesValue) || !(bytesValue is long bytes) || bytes < 0)
                throw new InvalidDataException("Dependency file record byte length is missing or invalid for asset path: " + assetPath);
            return new BundleDependencyFileRecord(path, sha256, bytes);
        }

        private static List<BundlePayloadRecord> MaterializeBundlePayload(string projectRoot, string bundleRoot,
            IReadOnlyList<object> assetRoots, IReadOnlyList<CanonicalBundleDependency> dependencies,
            IReadOnlyDictionary<string, AssemblyRecord> assemblies, IReadOnlyList<CompiledAssemblyRecord> compiledAssemblies,
            byte[] assemblyManifestBytes, byte[] inventoryBytes, byte[] descriptorBytes)
        {
            var rows = new List<BundlePayloadRecord>();
            var destinationPaths = new HashSet<string>(StringComparer.Ordinal);
            var sourceFiles = new HashSet<string>(StringComparer.Ordinal);

            AddPayloadBytes(bundleRoot, rows, destinationPaths, "payload/evidence/assembly-sources.json",
                "assembly-manifest", "generated:assembly-sources.json", assemblyManifestBytes);
            AddPayloadBytes(bundleRoot, rows, destinationPaths, "payload/evidence/frozen-inventory.json",
                "frozen-inventory", "generated:frozen-inventory.json", inventoryBytes);
            AddPayloadBytes(bundleRoot, rows, destinationPaths, "payload/evidence/physical-descriptor.json",
                "physical-descriptor", "generated:physical-descriptor.json", descriptorBytes);

            foreach (CanonicalBundleDependency dependency in dependencies)
            {
                AddMeasuredPayloadFile(projectRoot, bundleRoot, rows, destinationPaths,
                    dependency.AssetPath, "payload/assets/" + dependency.AssetPath, "asset-dependency",
                    dependency.AssetSha256, dependency.AssetBytes);
                AddMeasuredPayloadFile(projectRoot, bundleRoot, rows, destinationPaths,
                    dependency.AssetPath + ".meta", "payload/assets/" + dependency.AssetPath + ".meta", "asset-meta",
                    dependency.MetaSha256, dependency.MetaBytes);
            }

            foreach (AssemblyRecord assembly in assemblies.Values.OrderBy(row => row.Name, StringComparer.Ordinal))
            {
                if (!string.IsNullOrEmpty(assembly.DefinitionPath))
                    AddAssemblySourcePayload(projectRoot, bundleRoot, rows, destinationPaths, sourceFiles, assembly.DefinitionPath, "assembly-definition");
                foreach (FileRecord source in assembly.Sources)
                    AddAssemblySourcePayload(projectRoot, bundleRoot, rows, destinationPaths, sourceFiles, source.Path, "assembly-source");
            }
            foreach (CompiledAssemblyRecord assembly in compiledAssemblies)
            {
                AddMeasuredPayloadFile(projectRoot, bundleRoot, rows, destinationPaths,
                    assembly.Path, "payload/assemblies/" + Path.GetFileName(assembly.Path), "compiled-assembly", assembly.Sha256);
            }
            return rows.OrderBy(row => row.path, StringComparer.Ordinal).ToList();
        }

        private static void AddAssemblySourcePayload(string projectRoot, string bundleRoot, List<BundlePayloadRecord> rows,
            HashSet<string> destinationPaths, HashSet<string> sourceFiles, string sourcePath, string role)
        {
            if (sourceFiles.Add(sourcePath))
                AddMeasuredPayloadFile(projectRoot, bundleRoot, rows, destinationPaths,
                    sourcePath, "payload/source/" + sourcePath, role, null);
            string metaPath = sourcePath + ".meta";
            if (sourceFiles.Add(metaPath))
                AddMeasuredPayloadFile(projectRoot, bundleRoot, rows, destinationPaths,
                    metaPath, "payload/source/" + metaPath, "assembly-source-meta", null);
        }

        private static void AddMeasuredPayloadFile(string projectRoot, string bundleRoot, List<BundlePayloadRecord> rows,
            HashSet<string> destinationPaths, string sourcePath, string destinationPath, string role, string expectedSha256,
            long? expectedBytes = null)
        {
            string fullSourcePath = sourcePath.StartsWith("Packages/", StringComparison.Ordinal)
                ? GetPhysicalAssetOrMetaPath(sourcePath)
                : ProjectPath(sourcePath);
            FileRecord measured = ReadStrictFileRecord(sourcePath, fullSourcePath);
            if (!string.IsNullOrEmpty(expectedSha256) && !string.Equals(measured.Sha256, expectedSha256, StringComparison.Ordinal))
                throw new IOException("A materialized input changed since its descriptor measurement: " + sourcePath);
            if (expectedBytes.HasValue && measured.Length != expectedBytes.Value)
                throw new IOException("A materialized input byte length changed since its descriptor measurement: " + sourcePath);
            byte[] bytes = File.ReadAllBytes(fullSourcePath);
            AddPayloadBytes(bundleRoot, rows, destinationPaths, destinationPath, role, sourcePath, bytes, measured);
        }

        private static void AddPayloadBytes(string bundleRoot, List<BundlePayloadRecord> rows,
            HashSet<string> destinationPaths, string destinationPath, string role, string sourcePath, byte[] bytes,
            FileRecord measured = null)
        {
            if (!IsSafeBundlePath(destinationPath) || !destinationPaths.Add(destinationPath))
                throw new InvalidDataException("Bundle payload path is unsafe or duplicated: " + destinationPath);
            string fullDestinationPath = Path.Combine(bundleRoot, destinationPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Directory.GetParent(fullDestinationPath).FullName);
            string digest = Sha256Hex(bytes);
            long length = bytes.LongLength;
            if (measured != null && (length != measured.Length || !string.Equals(digest, measured.Sha256, StringComparison.Ordinal)))
                throw new IOException("Bundle bytes do not match their captured source record: " + sourcePath);
            WriteNewFile(fullDestinationPath, bytes);
            RejectReparsePointsFromVolumeRoot(fullDestinationPath);
            rows.Add(new BundlePayloadRecord
            {
                path = destinationPath,
                sourcePath = sourcePath,
                role = role,
                bytes = length,
                sha256 = digest
            });
        }

        private static BundleManifestData CreateBundleManifest(string revisionId, IReadOnlyList<BundlePayloadRecord> payloads,
            IReadOnlyList<object> assetRoots, IReadOnlyList<CanonicalBundleDependency> canonicalDependencies,
            IReadOnlyList<object> missingDependencies, bool installable, object[] packageDependencies)
        {
            var dependencies = new Dictionary<string, BundleDependencyRecord>(StringComparer.Ordinal);
            foreach (CanonicalBundleDependency dependency in canonicalDependencies)
            {
                dependencies.Add(dependency.AssetPath, new BundleDependencyRecord
                {
                    assetPath = dependency.AssetPath,
                    assetPayloadPath = "payload/assets/" + dependency.AssetPath,
                    metaPayloadPath = "payload/assets/" + dependency.AssetPath + ".meta",
                    guid = dependency.Guid,
                    assetSha256 = dependency.AssetSha256,
                    metaSha256 = dependency.MetaSha256
                });
            }
            return new BundleManifestData
            {
                schema = "luckbound.f10.v7.physical-bundle.v1",
                revisionId = revisionId,
                installable = installable,
                rootAssetPaths = assetRoots.Select(row => ((Dictionary<string, object>)row)["rootPath"].ToString())
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                missingDependencyIds = missingDependencies.Select(row => ((Dictionary<string, object>)row)["dependencyId"].ToString())
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                payloads = payloads.OrderBy(row => row.path, StringComparer.Ordinal).ToArray(),
                dependencies = dependencies.Values.OrderBy(row => row.assetPath, StringComparer.Ordinal).ToArray(),
                packageDependencies = packageDependencies ?? Array.Empty<object>()
            };
        }

        internal static object CaptureDependencyEntry(string assetPath)
        {
            if (!(assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.StartsWith("Packages/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Dependency path is outside Assets/Packages and cannot be frozen: " + assetPath);
            string fullPath = GetPhysicalAssetPath(assetPath);
            FileRecord assetBytes = ReadStrictFileRecord(assetPath, fullPath);
            FileRecord metaBytes = ReadStrictFileRecord(assetPath + ".meta", fullPath + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid) || !string.Equals(ReadMetaGuid(metaBytes, fullPath + ".meta"), guid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("AssetDatabase GUID and exact .meta bytes disagree for " + assetPath + ".");

            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (objects == null || objects.Length == 0)
                throw new InvalidOperationException("Dependency closure entry has no imported objects: " + assetPath);
            var objectRows = new List<object>();
            foreach (UnityEngine.Object assetObject in objects.Where(item => item != null)
                .OrderBy(item => GetLocalFileId(item), Comparer<long>.Default)
                .ThenBy(item => item.GetType().FullName, StringComparer.Ordinal).ThenBy(item => item.name, StringComparer.Ordinal))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetObject, out string objectGuid, out long localFileId) ||
                    !string.Equals(objectGuid, guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Dependency object has no stable GUID/localFileId: " + assetPath + " (" + assetObject.name + ").");
                objectRows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "localFileId", localFileId }, { "name", assetObject.name ?? string.Empty },
                    { "references", CaptureSerializedReferences(assetObject) },
                    { "type", assetObject.GetType().FullName }
                });
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assetBytes", assetBytes.ToJson() }, { "assetPath", assetPath }, { "guid", guid },
                { "metaBytes", metaBytes.ToJson() }, { "objects", objectRows },
                { "unityDependencyHash", AssetDatabase.GetAssetDependencyHash(assetPath).ToString() },
                { "unityDependencyHashIsDiagnosticOnly", true }
            };
        }

        private static long GetLocalFileId(UnityEngine.Object assetObject)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetObject, out _, out long localFileId))
                throw new InvalidDataException("Unity asset object has no stable local file ID: " + assetObject.name);
            return localFileId;
        }

        private static List<object> CaptureSerializedReferences(UnityEngine.Object serializedObjectTarget)
        {
            var result = new List<object>();
            var serialized = new SerializedObject(serializedObjectTarget);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                UnityEngine.Object reference = property.objectReferenceValue;
                if (reference == null) continue;
                string referencePath = AssetDatabase.GetAssetPath(reference);
                string referenceGuid = string.IsNullOrEmpty(referencePath) ? string.Empty : AssetDatabase.AssetPathToGUID(referencePath);
                long referenceLocalId = 0;
                if (EditorUtility.IsPersistent(reference) &&
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out referenceGuid, out referenceLocalId))
                    throw new InvalidDataException("Persistent serialized reference has no GUID/localFileId: " + reference.name);
                result.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "guid", referenceGuid ?? string.Empty },
                    { "localFileId", referenceLocalId },
                    { "name", reference.name ?? string.Empty },
                    { "path", referencePath ?? string.Empty },
                    { "persistent", EditorUtility.IsPersistent(reference) },
                    { "propertyPath", property.propertyPath },
                    { "type", reference.GetType().FullName }
                });
            }
            return result.OrderBy(row => ((Dictionary<string, object>)row)["propertyPath"].ToString(), StringComparer.Ordinal).ToList();
        }

        private static List<object> CaptureGeneratedRooms(IReadOnlyList<string> prefabPaths)
        {
            var rows = new List<object>();
            foreach (string prefabPath in prefabPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null) throw new InvalidOperationException("Generated room prefab could not be loaded: " + prefabPath);
                try
                {
                    RoomTemplatePrefab2D validator = root.GetComponent<RoomTemplatePrefab2D>();
                    if (validator == null) throw new InvalidOperationException("Generated room has no RoomTemplatePrefab2D: " + prefabPath);
                    RoomTemplateData definition = validator.ValidateGeneratedPrefab();
                    Tilemap map = validator.GroundTilemap;
                    var cells = new List<object>();
                    for (int y = 0; y < definition.heightTiles; y++)
                    {
                        for (int x = 0; x < definition.widthTiles; x++)
                        {
                            Vector3Int cellPosition = new Vector3Int(x, y, 0);
                            Tile tile = map.GetTile<Tile>(cellPosition);
                            char sourceCell = RoomTemplateJsonLoader.GetCell(definition, x, y);
                            if ((sourceCell == '#') != (tile != null))
                                throw new InvalidOperationException("Validated room Tilemap changed while exporting: " + prefabPath + " @ " + x + "," + y);
                            cells.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "tileGuid", tile == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)) },
                                { "tileLocalFileId", tile == null ? 0L : GetLocalFileId(tile) },
                                { "tileX", x }, { "tileY", y }, { "value", sourceCell.ToString() }
                            });
                        }
                    }
                    List<object> hierarchy = CaptureHierarchy(root.transform);
                    List<object> ports = CaptureRoomPorts(root);
                    List<object> markers = CaptureRoomPositionMarkers(root);
                    rows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "definition", new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "heightTiles", definition.heightTiles }, { "templateId", definition.templateId },
                                { "tileSize", definition.tileSize }, { "widthTiles", definition.widthTiles }
                            }
                        },
                        { "geometryCells", cells }, { "hierarchy", hierarchy },
                        { "markerCount", markers.Count }, { "markers", markers },
                        { "portCount", ports.Count }, { "ports", ports },
                        { "prefabPath", prefabPath },
                        { "roomTemplatePrefabValidator", "Luckbound.Gameplay.Rooms.RoomTemplatePrefab2D.ValidateGeneratedPrefab" },
                        { "validated", true }
                    });
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return rows;
        }

        private static List<object> CaptureRoomPorts(GameObject root)
        {
            return root.GetComponentsInChildren<RoomPortMarker2D>(true).Select(marker => (object)new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "clearanceTiles", marker.ClearanceTiles }, { "portId", marker.PortId }, { "side", marker.Side },
                { "tileX", marker.TilePosition.x }, { "tileY", marker.TilePosition.y }, { "type", marker.Type },
                { "transform", CaptureLocalTransform(marker.transform) }, { "hierarchyPath", GetHierarchyPath(marker.transform) }
            }).OrderBy(row => ((Dictionary<string, object>)row)["portId"].ToString(), StringComparer.Ordinal).ToList();
        }

        private static List<object> CaptureRoomPositionMarkers(GameObject root)
        {
            return root.GetComponentsInChildren<RoomPositionMarker2D>(true).Select(marker => (object)new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "hierarchyPath", GetHierarchyPath(marker.transform) }, { "kind", marker.Kind },
                { "markerId", marker.MarkerId }, { "tileX", marker.TilePosition.x }, { "tileY", marker.TilePosition.y },
                { "transform", CaptureLocalTransform(marker.transform) }
            }).OrderBy(row => ((Dictionary<string, object>)row)["markerId"].ToString(), StringComparer.Ordinal).ToList();
        }

        private static List<object> CaptureHierarchy(Transform root)
        {
            var rows = new List<object>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true).OrderBy(GetHierarchyPath, StringComparer.Ordinal))
            {
                GameObject gameObject = transform.gameObject;
                var components = new List<object>();
                foreach (Component component in gameObject.GetComponents<Component>().Where(item => item != null)
                    .OrderBy(item => item.GetType().FullName, StringComparer.Ordinal))
                    components.Add(CaptureComponent(component));
                rows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "activeSelf", gameObject.activeSelf }, { "components", components },
                    { "hierarchyPath", GetHierarchyPath(transform) }, { "layer", gameObject.layer },
                    { "layerName", LayerMask.LayerToName(gameObject.layer) }, { "localTransform", CaptureLocalTransform(transform) },
                    { "name", gameObject.name }, { "tag", gameObject.tag }
                });
            }
            return rows;
        }

        private static object CaptureComponent(Component component)
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assembly", component.GetType().Assembly.GetName().Name },
                { "type", component.GetType().FullName }, { "serializedReferences", CaptureSerializedReferences(component) }
            };
            if (component is Behaviour behaviour) row.Add("enabled", behaviour.enabled);
            if (component is Collider2D collider)
            {
                row.Add("collider", CaptureCollider(collider));
            }
            if (component is Rigidbody2D body)
            {
                row.Add("rigidbody2D", new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "angularDamping", body.angularDamping }, { "bodyType", body.bodyType.ToString() },
                    { "collisionDetectionMode", body.collisionDetectionMode.ToString() },
                    { "constraints", (int)body.constraints }, { "gravityScale", body.gravityScale },
                    { "interpolation", body.interpolation.ToString() }, { "linearDamping", body.linearDamping },
                    { "mass", body.mass }, { "simulated", body.simulated }, { "sleepMode", body.sleepMode.ToString() }
                });
            }
            if (component is TilemapCollider2D tilemapCollider)
            {
                row.Add("tilemapCollider", new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "compositeOperation", tilemapCollider.compositeOperation.ToString() },
                    { "extrusionFactor", tilemapCollider.extrusionFactor },
                    { "maximumTileChangeCount", tilemapCollider.maximumTileChangeCount }
                });
            }
            if (component is Tilemap tilemap)
            {
                row.Add("tilemap", new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "cellSize", Vector(tilemap.cellSize) }, { "tileAnchor", Vector(tilemap.tileAnchor) },
                    { "tileCount", tilemap.GetUsedTilesCount() }
                });
            }
            return row;
        }

        private static object CaptureCollider(Collider2D collider)
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "density", collider.density }, { "enabled", collider.enabled }, { "isTrigger", collider.isTrigger },
                { "offset", Vector(collider.offset) }, { "usedByEffector", collider.usedByEffector },
                { "compositeOperation", collider.compositeOperation.ToString() }
            };
            if (collider is BoxCollider2D box) row.Add("size", Vector(box.size));
            if (collider is CapsuleCollider2D capsule)
            {
                row.Add("direction", capsule.direction.ToString()); row.Add("size", Vector(capsule.size));
            }
            if (collider is CircleCollider2D circle) row.Add("radius", circle.radius);
            if (collider is EdgeCollider2D edge)
            {
                row.Add("edgeRadius", edge.edgeRadius); row.Add("points", edge.points.Select(Vector).ToArray());
            }
            if (collider is PolygonCollider2D polygon)
            {
                var paths = new List<object>();
                for (int index = 0; index < polygon.pathCount; index++)
                    paths.Add(polygon.GetPath(index).Select(Vector).ToArray());
                row.Add("paths", paths);
            }
            return row;
        }

        private static object CaptureLocalTransform(Transform transform)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "localPosition", Vector(transform.localPosition) }, { "localRotation", QuaternionValue(transform.localRotation) },
                { "localScale", Vector(transform.localScale) }
            };
        }

        private static List<object> CapturePhysicalPrefabs(GameObject generatedPlayer, GameObject generatedCache,
            IReadOnlyList<string> projectilePrefabPaths)
        {
            if (generatedPlayer == null || generatedCache == null)
                throw new InvalidOperationException("The real diagnostic player and cache prefab generations are required before physical capture.");

            var specs = new List<PrefabSpec>
            {
                new PrefabSpec("c08.slime", CombatArenaScaffoldGenerator.SlimePrefabPath, "Slime2D"),
                new PrefabSpec("c08.huesoguardia", CombatArenaScaffoldGenerator.HuesoguardiaPrefabPath, "Huesoguardia2D"),
                new PrefabSpec("custodian.boss", CustodianArenaScaffoldGenerator.BossPrefabPath, "CustodianBoss2D"),
                new PrefabSpec("custodian.wave", CustodianArenaScaffoldGenerator.WavePrefabPath, "CustodianWaveHazard2D"),
                new PrefabSpec("player.f10", F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath, "PlayerMotor2D"),
                new PrefabSpec("cache.exploration", F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath, "ExplorationGoldCachePickup2D")
            };
            foreach (string projectilePath in projectilePrefabPaths)
                specs.Add(new PrefabSpec("projectile." + Path.GetFileNameWithoutExtension(projectilePath), projectilePath, "Projectile2D"));

            return specs.Select(spec =>
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
                if (root == null) throw new InvalidOperationException("Required physical actor prefab is missing: " + spec.AssetPath);
                List<object> hierarchy = CaptureHierarchy(root.transform);
                bool hasExpected = root.GetComponentsInChildren<Component>(true).Any(component => component != null && component.GetType().Name == spec.ExpectedComponentName);
                var result = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "assetPath", spec.AssetPath }, { "expectedAdapterComponent", spec.ExpectedComponentName },
                    { "hasExpectedAdapterComponent", hasExpected }, { "hierarchy", hierarchy },
                    { "prefabRootLayer", root.layer }, { "prefabRootLayerName", LayerMask.LayerToName(root.layer) },
                    { "role", spec.Role }
                };
                if (!hasExpected) throw new InvalidOperationException("Expected adapter component is absent from prefab: " + spec.AssetPath);
                if (spec.Role == "player.f10")
                {
                    bool physicalPlayer = root.GetComponent<Rigidbody2D>() != null && root.GetComponent<Collider2D>() != null &&
                        root.GetComponent<SpriteRenderer>() != null && root.GetComponent<Animator>() != null;
                    result.Add("physicalPlayerComponentsMeasured", physicalPlayer);
                    if (!physicalPlayer) throw new InvalidOperationException("Diagnostic player prefab lacks measured Rigidbody2D/Collider2D/visual components.");
                }
                if (spec.Role == "cache.exploration")
                {
                    ExplorationGoldCachePickup2D[] pickups = root.GetComponentsInChildren<ExplorationGoldCachePickup2D>(true);
                    BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
                    bool measuredCache = !root.activeSelf && pickups.Length == 1 && ReferenceEquals(pickups[0].gameObject, root) &&
                        trigger != null && trigger.enabled && trigger.isTrigger && trigger.size == Vector2.one &&
                        root.GetComponents<Collider2D>().Length == 1 && root.GetComponent<Rigidbody2D>() == null;
                    result.Add("physicalCachePrefabMeasured", measuredCache);
                    result.Add("roomPlacementAndPickupObserved", false);
                    if (!measuredCache) throw new InvalidOperationException("Exploration cache prefab did not match the measured inactive trigger contract.");
                }
                if (spec.Role.StartsWith("projectile.", StringComparison.Ordinal))
                {
                    Projectile2D[] projectiles = root.GetComponentsInChildren<Projectile2D>(true);
                    bool measuredProjectile = projectiles.Length == 1 && ReferenceEquals(projectiles[0].gameObject, root) &&
                        root.GetComponent<Rigidbody2D>() != null && root.GetComponent<Collider2D>() != null;
                    result.Add("projectilePhysicalComponentsMeasured", measuredProjectile);
                    if (!measuredProjectile) throw new InvalidOperationException("Projectile prefab lacks a unique root Projectile2D/Rigidbody2D/Collider2D.");
                }
                if (spec.Role == "custodian.boss")
                {
                    Slime2D[] slimes = root.GetComponentsInChildren<Slime2D>(true);
                    Huesoguardia2D[] boneguards = root.GetComponentsInChildren<Huesoguardia2D>(true);
                    CustodianBoss2D[] custodians = root.GetComponentsInChildren<CustodianBoss2D>(true);
                    bool exclusiveAdapterIdentity = custodians.Length == 1 && ReferenceEquals(custodians[0].gameObject, root) &&
                        slimes.Length == 0 && boneguards.Length == 0 && root.GetComponent<Rigidbody2D>() != null &&
                        root.GetComponent<Collider2D>() != null && root.GetComponent<SpriteRenderer>() != null;
                    result.Add("exclusiveAdapterIdentity", exclusiveAdapterIdentity);
                    if (!exclusiveAdapterIdentity) throw new InvalidOperationException("Custodian prefab does not have one exclusive root adapter identity.");
                }
                if (spec.Role == "c08.slime")
                {
                    Slime2D[] slimes = root.GetComponentsInChildren<Slime2D>(true);
                    bool exclusive = slimes.Length == 1 && ReferenceEquals(slimes[0].gameObject, root) &&
                        root.GetComponentsInChildren<Huesoguardia2D>(true).Length == 0 &&
                        root.GetComponentsInChildren<CustodianBoss2D>(true).Length == 0 &&
                        root.GetComponent<Rigidbody2D>() != null && root.GetComponent<Collider2D>() != null;
                    result.Add("exclusiveAdapterIdentity", exclusive);
                    if (!exclusive) throw new InvalidOperationException("Slime prefab does not have one exclusive root adapter identity.");
                }
                if (spec.Role == "c08.huesoguardia")
                {
                    Huesoguardia2D[] boneguards = root.GetComponentsInChildren<Huesoguardia2D>(true);
                    bool exclusive = boneguards.Length == 1 && ReferenceEquals(boneguards[0].gameObject, root) &&
                        root.GetComponentsInChildren<Slime2D>(true).Length == 0 &&
                        root.GetComponentsInChildren<CustodianBoss2D>(true).Length == 0 &&
                        root.GetComponent<Rigidbody2D>() != null && root.GetComponent<Collider2D>() != null;
                    result.Add("exclusiveAdapterIdentity", exclusive);
                    if (!exclusive) throw new InvalidOperationException("Huesoguardia prefab does not have one exclusive root adapter identity.");
                }
                if (spec.Role == "custodian.wave")
                {
                    CustodianWaveHazard2D[] waves = root.GetComponentsInChildren<CustodianWaveHazard2D>(true);
                    bool validWavePhysics = waves.Length == 1 && ReferenceEquals(waves[0].gameObject, root) &&
                        root.GetComponent<Rigidbody2D>() != null && root.GetComponent<Collider2D>() != null &&
                        root.GetComponent<Collider2D>().isTrigger;
                    bool exclusiveAdapterIdentity = validWavePhysics && root.GetComponentsInChildren<Slime2D>(true).Length == 0 &&
                        root.GetComponentsInChildren<Huesoguardia2D>(true).Length == 0 &&
                        root.GetComponentsInChildren<CustodianBoss2D>(true).Length == 0;
                    result.Add("custodianWavePhysicalAdapter", validWavePhysics);
                    result.Add("exclusiveAdapterIdentity", exclusiveAdapterIdentity);
                    if (!validWavePhysics) throw new InvalidOperationException("Custodian wave prefab lacks the root trigger physics adapter.");
                    if (!exclusiveAdapterIdentity) throw new InvalidOperationException("Custodian wave prefab has an ambiguous actor-adapter identity.");
                }
                return (object)result;
            }).ToList();
        }

        private static List<object> BuildMissingPhysicalDependencies(GameObject generatedCache,
            IReadOnlyList<string> projectilePrefabPaths)
        {
            var missing = new List<object>();
            if (generatedCache == null || !AssetDatabase.Contains(generatedCache) ||
                !File.Exists(ProjectPath(F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath)) ||
                !File.Exists(ProjectPath(F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath + ".meta")))
            {
                missing.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "dependencyId", PhysicalCacheDependency },
                    { "installable", false },
                    { "observed", false },
                    { "reason", "No materialized cache prefab and .meta bytes were available to measure." }
                });
            }
            missing.Add(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "dependencyId", "cache.exploration.runtime" },
                { "installable", false },
                { "observed", false },
                { "reason", "Prefab bytes and trigger components are measured, but room placement, contact, and durable pickup were not observed in a running expedition." }
            });
            if (projectilePrefabPaths == null || projectilePrefabPaths.Count == 0)
            {
                missing.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "dependencyId", PhysicalProjectileDependency },
                    { "installable", false },
                    { "observed", false },
                    { "reason", "No materialized projectile prefab was found in Assets/Luckbound/Prefabs; no prefab or hash was inferred." }
                });
            }
            missing.Add(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "dependencyId", PhysicalCustodianConfinementDependency },
                { "installable", false },
                { "observed", false },
                { "reason", "The boundary installer source is measured, but boss spawn and continuous collider confinement have not been observed in a running Guardian room." }
            });
            return missing;
        }

        private static string[] FindMaterializedProjectilePrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Luckbound/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .Where(path =>
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return prefab != null && prefab.GetComponentsInChildren<Projectile2D>(true).Length > 0;
                })
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static string ReadMetaGuid(FileRecord meta, string absolutePath)
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (!string.Equals(Sha256Hex(bytes), meta.Sha256, StringComparison.Ordinal))
                throw new IOException("Meta bytes changed while capturing the dependency record: " + meta.Path);
            string text = StrictUtf8.GetString(bytes);
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                const string prefix = "guid: ";
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line.Substring(prefix.Length).Trim();
            }
            throw new InvalidDataException("Meta file has no GUID field: " + meta.Path);
        }

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

        private static void WriteNewFile(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string ProjectPath(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar));

        private static string GetPhysicalAssetPath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return ProjectPath(assetPath);
            if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                throw new InvalidOperationException("Only project Assets/Packages dependencies can be frozen: " + assetPath);
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            if (package == null || string.IsNullOrWhiteSpace(package.name) || string.IsNullOrWhiteSpace(package.version) ||
                string.IsNullOrWhiteSpace(package.resolvedPath) || string.IsNullOrWhiteSpace(package.assetPath) ||
                !assetPath.StartsWith(package.assetPath + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("PackageInfo does not identify the exact V7 package dependency path: " + assetPath);
            string packageRoot = CanonicalPath(package.resolvedPath);
            string projectRoot = CanonicalPath(Directory.GetParent(Application.dataPath).FullName);
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (packageRoot.StartsWith(projectPrefix, pathComparison))
            {
                string expectedProjectPackageRoot = CanonicalPath(Path.Combine(projectRoot,
                    package.assetPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!string.Equals(packageRoot, expectedProjectPackageRoot, pathComparison))
                    throw new InvalidOperationException("Project-local PackageInfo resolved outside its exact Packages asset path: " + assetPath);
                string localPhysical = CanonicalPath(Path.Combine(packageRoot,
                    assetPath.Substring(package.assetPath.Length + 1).Replace('/', Path.DirectorySeparatorChar)));
                string localPrefix = packageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!localPhysical.StartsWith(localPrefix, pathComparison) || string.Equals(localPhysical, packageRoot, pathComparison))
                    throw new InvalidOperationException("Project-local package dependency escaped its exact PackageInfo root: " + assetPath);
                RejectReparsePointsFromVolumeRoot(localPhysical);
                return localPhysical;
            }
            if (package.source != UnityEditor.PackageManager.PackageSource.BuiltIn)
                throw new InvalidOperationException("An external package dependency must be an Editor BuiltIn package: " + assetPath);
            string editorBuiltInRoot = CanonicalPath(Path.Combine(EditorApplication.applicationContentsPath,
                "Resources/PackageManager/BuiltInPackages", package.name));
            if (!string.Equals(packageRoot, editorBuiltInRoot, pathComparison))
                throw new InvalidOperationException("PackageInfo resolved outside the exact Editor BuiltIn package root: " + assetPath);
            string physical = CanonicalPath(Path.Combine(packageRoot,
                assetPath.Substring(package.assetPath.Length + 1).Replace('/', Path.DirectorySeparatorChar)));
            string packagePrefix = packageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!physical.StartsWith(packagePrefix, pathComparison) || string.Equals(physical, packageRoot, pathComparison))
                throw new InvalidOperationException("Resolved BuiltIn package dependency escaped its exact package root: " + assetPath);
            RejectReparsePointsFromVolumeRoot(physical);
            return physical;
        }

        private static string GetPhysicalAssetOrMetaPath(string assetPath)
        {
            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal) &&
                assetPath.EndsWith(".meta", StringComparison.Ordinal))
            {
                string assetPathWithoutMeta = assetPath.Substring(0, assetPath.Length - ".meta".Length);
                return GetPhysicalAssetPath(assetPathWithoutMeta) + ".meta";
            }
            return GetPhysicalAssetPath(assetPath);
        }

        private static string MakeProjectRelative(string projectRoot, string absolutePath)
        {
            string prefix = CanonicalPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = CanonicalPath(absolutePath);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escaped the disposable project root: " + absolutePath);
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

        private static string CanonicalPath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static bool PathsEqual(string left, string right) => string.Equals(CanonicalPath(left), CanonicalPath(right), StringComparison.OrdinalIgnoreCase);
        private static object Vector(Vector2 value) => new[] { value.x, value.y };
        private static object Vector(Vector3 value) => new[] { value.x, value.y, value.z };
        private static object QuaternionValue(Quaternion value) => new[] { value.x, value.y, value.z, value.w };
        private static string ToLowerHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

        [Serializable]
        private sealed class AssemblyDefinitionData { public string name; }

        [Serializable]
        public sealed class BundleManifestData
        {
            public string schema;
            public string revisionId;
            public bool installable;
            public string[] rootAssetPaths;
            public string[] missingDependencyIds;
            public BundlePayloadRecord[] payloads;
            public BundleDependencyRecord[] dependencies;
            public object[] packageDependencies;

            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "dependencies", dependencies.Select(row => row.ToJson()).ToArray() },
                { "installable", installable }, { "missingDependencyIds", missingDependencyIds },
                { "packageDependencies", packageDependencies ?? Array.Empty<object>() },
                { "payloads", payloads.Select(row => row.ToJson()).ToArray() }, { "revisionId", revisionId },
                { "rootAssetPaths", rootAssetPaths }, { "schema", schema }
            };
        }

        [Serializable]
        public sealed class BundlePayloadRecord
        {
            public string path;
            public string sourcePath;
            public string role;
            public long bytes;
            public string sha256;

            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "bytes", bytes }, { "path", path }, { "role", role }, { "sha256", sha256 }, { "sourcePath", sourcePath }
            };
        }

        [Serializable]
        public sealed class BundleDependencyRecord
        {
            public string assetPath;
            public string assetPayloadPath;
            public string metaPayloadPath;
            public string guid;
            public string assetSha256;
            public string metaSha256;

            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assetPath", assetPath }, { "assetPayloadPath", assetPayloadPath }, { "assetSha256", assetSha256 },
                { "guid", guid }, { "metaPayloadPath", metaPayloadPath }, { "metaSha256", metaSha256 }
            };
        }

        private sealed class CanonicalBundleDependency
        {
            internal CanonicalBundleDependency(string assetPath, string guid, BundleDependencyFileRecord assetBytes,
                BundleDependencyFileRecord metaBytes)
            {
                AssetPath = assetPath;
                Guid = guid;
                AssetFile = assetBytes;
                MetaFile = metaBytes;
            }

            internal string AssetPath { get; }
            internal string Guid { get; }
            internal BundleDependencyFileRecord AssetFile { get; }
            internal BundleDependencyFileRecord MetaFile { get; }
            internal string AssetSha256 => AssetFile.Sha256;
            internal long AssetBytes => AssetFile.Bytes;
            internal string MetaSha256 => MetaFile.Sha256;
            internal long MetaBytes => MetaFile.Bytes;

            internal bool HasSamePhysicalRecords(CanonicalBundleDependency other)
            {
                return other != null &&
                    string.Equals(AssetPath, other.AssetPath, StringComparison.Ordinal) &&
                    string.Equals(Guid, other.Guid, StringComparison.Ordinal) &&
                    AssetFile.HasSameRecord(other.AssetFile) && MetaFile.HasSameRecord(other.MetaFile);
            }
        }

        private sealed class BundleDependencyFileRecord
        {
            internal BundleDependencyFileRecord(string path, string sha256, long bytes)
            { Path = path; Sha256 = sha256; Bytes = bytes; }

            internal string Path { get; }
            internal string Sha256 { get; }
            internal long Bytes { get; }

            internal bool HasSameRecord(BundleDependencyFileRecord other)
            {
                return other != null &&
                    string.Equals(Path, other.Path, StringComparison.Ordinal) &&
                    string.Equals(Sha256, other.Sha256, StringComparison.Ordinal) &&
                    Bytes == other.Bytes;
            }
        }

        private sealed class CompiledAssemblyRecord
        {
            internal CompiledAssemblyRecord(string name, string path, long length, string sha256)
            { Name = name; Path = path; Length = length; Sha256 = sha256; }
            internal string Name { get; }
            internal string Path { get; }
            internal long Length { get; }
            internal string Sha256 { get; }
            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assemblyName", Name }, { "bytes", Length }, { "path", Path }, { "sha256", Sha256 }
            };
        }

        private sealed class DisposableCopyIdentity
        {
            internal DisposableCopyIdentity(string sourceRoot, string projectRoot, string markerPath, string isolationChecks)
            { SourceRoot = sourceRoot; ProjectRoot = projectRoot; MarkerPath = markerPath; IsolationChecks = isolationChecks; }
            private string SourceRoot { get; }
            private string ProjectRoot { get; }
            private string MarkerPath { get; }
            private string IsolationChecks { get; }
            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "disposableProjectRoot", ProjectRoot }, { "identityChecks", IsolationChecks },
                { "markerPath", MarkerPath }, { "sourceProjectRoot", SourceRoot }
            };
        }

        internal sealed class FileRecord
        {
            internal FileRecord(string path, long length, string sha256) { Path = path; Length = length; Sha256 = sha256; }
            internal string Path { get; }
            internal long Length { get; }
            internal string Sha256 { get; }
            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "bytes", Length }, { "path", Path }, { "sha256", Sha256 }
            };
        }

        private sealed class AssemblyDefinitionRecord
        {
            internal AssemblyDefinitionRecord(string path, string name, string sha256) { Path = path; Name = name; Sha256 = sha256; }
            internal string Path { get; }
            internal string Name { get; }
            internal string Sha256 { get; }
        }

        private sealed class AssemblyRecord
        {
            internal AssemblyRecord(string name, string definitionPath, string definitionSha256)
            { Name = name; DefinitionPath = definitionPath; DefinitionSha256 = definitionSha256; }
            internal string Name { get; }
            internal string DefinitionPath { get; }
            private string DefinitionSha256 { get; }
            internal List<FileRecord> Sources { get; } = new List<FileRecord>();
            internal object ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "assemblyDefinitionPath", DefinitionPath }, { "assemblyDefinitionSha256", DefinitionSha256 },
                { "assemblyName", Name }, { "sources", Sources.OrderBy(row => row.Path, StringComparer.Ordinal).Select(row => row.ToJson()).ToArray() }
            };
        }

        private sealed class PrefabSpec
        {
            internal PrefabSpec(string role, string assetPath, string expectedComponentName)
            { Role = role; AssetPath = assetPath; ExpectedComponentName = expectedComponentName; }
            internal string Role { get; }
            internal string AssetPath { get; }
            internal string ExpectedComponentName { get; }
        }

        private sealed class PhysicalFileInformation
        {
            internal PhysicalFileInformation(string identity, uint linkCount) { Identity = identity; LinkCount = linkCount; }
            internal string Identity { get; }
            internal uint LinkCount { get; }
        }

        [StructLayout(LayoutKind.Sequential)]
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

        private static string GetHierarchyPath(Transform transform)
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", segments.ToArray());
        }

        private static class CanonicalJson
        {
            internal static byte[] Serialize(object value)
            {
                var builder = new StringBuilder(16 * 1024);
                WriteValue(builder, value);
                return StrictUtf8.GetBytes(builder.ToString());
            }

            private static void WriteValue(StringBuilder builder, object value)
            {
                if (value == null) { builder.Append("null"); return; }
                if (value is string text) { WriteString(builder, text); return; }
                if (value is bool boolean) { builder.Append(boolean ? "true" : "false"); return; }
                if (value is byte || value is short || value is int || value is long || value is uint || value is ulong)
                { builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
                if (value is float single)
                {
                    if (float.IsNaN(single) || float.IsInfinity(single)) throw new InvalidDataException("Canonical JSON cannot contain non-finite numbers.");
                    builder.Append(single.ToString("R", CultureInfo.InvariantCulture)); return;
                }
                if (value is double dbl)
                {
                    if (double.IsNaN(dbl) || double.IsInfinity(dbl)) throw new InvalidDataException("Canonical JSON cannot contain non-finite numbers.");
                    builder.Append(dbl.ToString("R", CultureInfo.InvariantCulture)); return;
                }
                if (value is decimal dec) { builder.Append(dec.ToString(CultureInfo.InvariantCulture)); return; }
                if (value is IDictionary<string, object> dictionary)
                {
                    builder.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, object> pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        WriteString(builder, pair.Key);
                        builder.Append(':');
                        WriteValue(builder, pair.Value);
                    }
                    builder.Append('}');
                    return;
                }
                if (value is System.Collections.IEnumerable sequence)
                {
                    builder.Append('[');
                    bool first = true;
                    foreach (object item in sequence)
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        WriteValue(builder, item);
                    }
                    builder.Append(']');
                    return;
                }
                throw new InvalidDataException("Unsupported canonical JSON value type: " + value.GetType().FullName);
            }

            private static void WriteString(StringBuilder builder, string value)
            {
                builder.Append('"');
                foreach (char character in value)
                {
                    switch (character)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            else builder.Append(character);
                            break;
                    }
                }
                builder.Append('"');
            }
        }
    }
}
