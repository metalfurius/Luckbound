#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        private const string F10DiagnosticSceneName = "F10Diagnostic";
        private const string F10DiagnosticSaveRootRelativePath = "Logs/F10DiagnosticSaves";
        private const string F10DiagnosticBellatorClassId = "class.bellator";
        private static readonly string[] F10DiagnosticSkillIds =
        {
            "skill.bellator.circular_cut",
            "skill.bellator.guard"
        };

        private F10DiagnosticSessionWitness _f10DiagnosticSessionWitness;

        /// <summary>
        /// Begins one non-resumable Editor-only technical run in the explicitly loaded F10Diagnostic scene.
        /// The save repository is still the production V6 repository; only its store is isolated for this instance.
        /// </summary>
        internal static async Task<ApplicationBootstrapper> BeginF10DiagnosticSessionAsync()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("The F10 diagnostic session requires Play Mode.");
            if (!IsF10DiagnosticScene(SceneManager.GetActiveScene().name))
                throw new InvalidOperationException("Load the explicit F10Diagnostic technical scene before starting the session.");
            if (_instance != null)
                throw new InvalidOperationException("A bootstrapper already exists; the diagnostic session will not take it over.");
            if (EditorSaveStoreFactoryForTests != null)
                throw new InvalidOperationException("Another Editor save-store factory is installed; the diagnostic session will not replace it.");

            string projectRoot = GetProjectRoot();
            string saveRoot = Path.Combine(projectRoot,
                F10DiagnosticSaveRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
            EnsureDiagnosticSaveRoot(projectRoot, saveRoot, Application.persistentDataPath);
            string sessionDirectory = CreateUniqueSessionDirectory(saveRoot, Application.persistentDataPath);
            var store = new FileSaveStore(sessionDirectory, null);
            int factoryInvocationCount = 0;
            Func<ISaveStore> diagnosticFactory = () =>
            {
                if (Interlocked.Increment(ref factoryInvocationCount) != 1)
                    throw new InvalidOperationException("The diagnostic save-store factory may provide exactly one store instance.");
                return store;
            };

            GameObject owner = null;
            ApplicationBootstrapper app = null;
            try
            {
                try
                {
                    EditorSaveStoreFactoryForTests = diagnosticFactory;
                    owner = new GameObject("ApplicationBootstrapper (F10 Diagnostic)");
                    app = owner.AddComponent<ApplicationBootstrapper>();
                }
                finally
                {
                    // The hook is borrowed only for the synchronous Awake that constructs this instance.
                    if (ReferenceEquals(EditorSaveStoreFactoryForTests, diagnosticFactory))
                        EditorSaveStoreFactoryForTests = null;
                }

                if (EditorSaveStoreFactoryForTests != null)
                    throw new InvalidOperationException("The Editor save-store factory changed during bootstrap; the diagnostic session stopped before loading saves.");
                if (factoryInvocationCount != 1 || app == null || !ReferenceEquals(_instance, app))
                    throw new InvalidOperationException("The diagnostic bootstrapper did not receive its single isolated FileSaveStore.");
                if (!PathEquals(store.GetPrimaryPath(SaveDocument.Profile),
                        Path.Combine(sessionDirectory, FileSaveStore.ProfileFileName)) ||
                    !IsPathEqualOrWithin(Path.GetDirectoryName(store.GetPrimaryPath(SaveDocument.Profile)), saveRoot))
                    throw new InvalidOperationException("The diagnostic FileSaveStore escaped its unique session directory.");

                EnsureDiagnosticSessionStillIsolated(sessionDirectory);
                try
                {
                    await app.LoadSaveDataAsync();
                }
                finally
                {
                    app.SaveDataReady = true;
                }

                if (app.ProfileLoadStatus != SaveLoadStatus.Missing ||
                    app.SettingsLoadStatus != SaveLoadStatus.Missing ||
                    app.ProfileAccess != SaveDataAccess.ReadWrite ||
                    app.HasActiveExpedition || app.ActiveRun != null || !app.CanStartNewExpedition)
                {
                    throw new InvalidOperationException(
                        "The newly reserved diagnostic store was not empty and writable; its contents were preserved and no run was started.");
                }

                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                    F10DiagnosticBellatorClassId,
                    ExpeditionRunConfiguration.HumanRaceId,
                    F10DiagnosticSkillIds);
                EnsureDiagnosticSessionStillIsolated(sessionDirectory);
                SaveWriteResult begin = await app.BeginExpeditionAsync(run);
                if (begin == null || !begin.Succeeded || !ReferenceEquals(app.ActiveRun, run) || !app.HasActiveExpedition)
                    throw new InvalidOperationException("The technical expedition was not durably started: " +
                        (begin == null ? "no save result" : begin.Message));

                EnsureDiagnosticSessionStillIsolated(sessionDirectory);
                SaveStorageSnapshot storedProfile = await store.ReadAsync(SaveDocument.Profile);
                if (storedProfile == null ||
                    SaveJsonCodec.Decode(storedProfile.PrimaryJson, out ProfileSaveEnvelope persistedProfile,
                        out int persistedContentVersion) != DecodeStatus.Valid ||
                    persistedContentVersion != 6 || SaveFormat.ContentVersion != 6 ||
                    persistedProfile == null || !persistedProfile.hasActiveExpedition ||
                    persistedProfile.activeExpedition == null ||
                    !string.Equals(persistedProfile.activeExpedition.runId, run.RunId.ToString(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The isolated profile did not verify as a V6 save for the exact active diagnostic run; the written data was preserved.");
                }

                EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sessionDirectory);
                app._f10DiagnosticSessionWitness = new F10DiagnosticSessionWitness(
                    app, sessionDirectory, store, run);
                return app;
            }
            catch
            {
                if (app != null) app._f10DiagnosticSessionWitness = null;
                if (ReferenceEquals(_instance, app)) _instance = null;
                if (owner != null)
                {
                    owner.SetActive(false);
                    UnityEngine.Object.Destroy(owner);
                }
                throw;
            }
        }

        /// <summary>True only for the exact run persisted by this instance into its isolated diagnostic folder.</summary>
        internal bool OwnsF10DiagnosticRun(ExpeditionRunConfiguration run)
        {
            F10DiagnosticSessionWitness witness = _f10DiagnosticSessionWitness;
            if (witness == null || !ReferenceEquals(_instance, this) ||
                !ReferenceEquals(witness.Owner, this) || !witness.IsPersisted ||
                run == null || !ReferenceEquals(run, ActiveRun) ||
                !string.Equals(run.RunId.ToString(), witness.RunId, StringComparison.Ordinal))
                return false;

            try
            {
                string persistentDataPath = Application.persistentDataPath;
                EnsurePathAndExistingAncestorsAreNotReparsePoints(persistentDataPath);
                return !PathsOverlap(witness.StoreDirectory, persistentDataPath) &&
                    IsPathEqualOrWithin(witness.StoreDirectory, witness.SaveRoot) &&
                    Directory.Exists(witness.StoreDirectory) &&
                    EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(witness.StoreDirectory);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Internal read-only witness shared with the technical composition entry.</summary>
        internal F10DiagnosticSessionWitness F10DiagnosticWitness => _f10DiagnosticSessionWitness;

        internal sealed class F10DiagnosticSessionWitness
        {
            internal F10DiagnosticSessionWitness(ApplicationBootstrapper owner, string storeDirectory,
                FileSaveStore store, ExpeditionRunConfiguration run)
            {
                Owner = owner ?? throw new ArgumentNullException(nameof(owner));
                StoreDirectory = Path.GetFullPath(storeDirectory ?? throw new ArgumentNullException(nameof(storeDirectory)));
                SaveRoot = Path.GetDirectoryName(StoreDirectory);
                Store = store ?? throw new ArgumentNullException(nameof(store));
                RunId = (run ?? throw new ArgumentNullException(nameof(run))).RunId.ToString();
                IsPersisted = true;
            }

            internal ApplicationBootstrapper Owner { get; }
            internal string StoreDirectory { get; }
            internal string SaveRoot { get; }
            internal FileSaveStore Store { get; }
            internal string RunId { get; }
            internal bool IsPersisted { get; }
        }

        internal static bool IsF10DiagnosticScene(string sceneName)
        {
            return string.Equals(sceneName, F10DiagnosticSceneName, StringComparison.Ordinal);
        }

        /// <summary>Shared create-only gate for both V7 export preflight and V7 opening verification.</summary>
        internal static void EnsureDiagnosticPathIsDisposable(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("The diagnostic project root is required.", nameof(projectRoot));
            string normalizedRoot = Path.GetFullPath(projectRoot);
            string currentRoot = Path.GetFullPath(Directory.GetParent(Path.GetFullPath(Application.dataPath)).FullName);
            if (!PathEquals(normalizedRoot, currentRoot))
                throw new InvalidOperationException("The diagnostic disposable-path guard requires the exact loaded Unity project root.");
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(normalizedRoot);
            foreach (string relative in new[]
            {
                ".git",
                "Assets/Luckbound/Editor/F02ImportStaging",
                "Assets/Luckbound/Editor/F02ImportStaging.meta",
                "ProjectSettings/Luckbound/F02ImportRecovery/pending",
                "Library/Luckbound/F02/Transactions"
            })
            {
                string path = Path.GetFullPath(Path.Combine(normalizedRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathEqualOrWithin(path, normalizedRoot) || PathEquals(path, normalizedRoot))
                    throw new InvalidOperationException("A fixed F02/Git control path escaped the diagnostic project root.");
                EnsureDiagnosticPlainAncestors(path);
                if (ExistsAsAnyEntry(path))
                    throw new InvalidOperationException("F02 or Git control state blocks this disposable diagnostic copy: " + relative);
            }
        }

        private static void EnsureDiagnosticPlainAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (ExistsAsAnyEntry(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Diagnostic paths cannot contain reparse points: " + current);
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current)) break;
                current = parent;
            }
        }

        private static bool ExistsAsAnyEntry(string path)
        {
            if (File.Exists(path) || Directory.Exists(path)) return true;
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        internal static bool AreDiagnosticSavePathsDisjoint(string sessionDirectory, string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory) || string.IsNullOrWhiteSpace(persistentDataPath))
                return false;
            return !PathsOverlap(sessionDirectory, persistentDataPath);
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo assetsDirectory = Directory.GetParent(Path.GetFullPath(Application.dataPath));
            if (assetsDirectory == null)
                throw new InvalidOperationException("Unity's Assets directory has no project parent.");
            string projectRoot = Path.GetFullPath(assetsDirectory.FullName);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(projectRoot);
            return projectRoot;
        }

        private static void EnsureDiagnosticSaveRoot(string projectRoot, string saveRoot, string persistentDataPath)
        {
            string expectedRoot = Path.GetFullPath(Path.Combine(projectRoot,
                F10DiagnosticSaveRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!PathEquals(saveRoot, expectedRoot) || !IsPathEqualOrWithin(expectedRoot, projectRoot) ||
                !AreDiagnosticSavePathsDisjoint(expectedRoot, persistentDataPath))
                throw new InvalidOperationException("The diagnostic save root is not safely isolated under the project Logs directory.");

            EnsurePathAndExistingAncestorsAreNotReparsePoints(persistentDataPath);
            string logsRoot = Path.GetDirectoryName(expectedRoot);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(logsRoot);
            if (File.Exists(expectedRoot))
                throw new InvalidOperationException("The diagnostic save root is occupied by a file; it was preserved.");
            if (!Directory.Exists(expectedRoot))
            {
                Directory.CreateDirectory(expectedRoot);
                if (!Directory.Exists(expectedRoot))
                    throw new IOException("The diagnostic save root could not be created.");
            }
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(expectedRoot);
        }

        private static string CreateUniqueSessionDirectory(string saveRoot, string persistentDataPath)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string sessionDirectory = Path.GetFullPath(Path.Combine(saveRoot, Guid.NewGuid().ToString("N")));
                if (!IsPathEqualOrWithin(sessionDirectory, saveRoot) ||
                    !AreDiagnosticSavePathsDisjoint(sessionDirectory, persistentDataPath))
                    throw new InvalidOperationException("The candidate diagnostic save directory failed path isolation.");
                if (Directory.Exists(sessionDirectory) || File.Exists(sessionDirectory))
                    continue;

                Directory.CreateDirectory(sessionDirectory);
                if (!Directory.Exists(sessionDirectory))
                    throw new IOException("The unique diagnostic save directory could not be created.");
                EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sessionDirectory);
                if (Directory.GetFileSystemEntries(sessionDirectory).Length != 0)
                    throw new IOException("The unique diagnostic save directory was not empty at creation; its contents were preserved.");
                return sessionDirectory;
            }

            throw new IOException("Could not reserve a new unique F10 diagnostic save directory.");
        }

        private static bool EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (!Directory.Exists(current))
                    throw new DirectoryNotFoundException("A required diagnostic path ancestor is missing: " + current);
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Diagnostic save paths cannot contain reparse points: " + current);

                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current)) break;
                current = parent;
            }

            return true;
        }

        private static void EnsurePathAndExistingAncestorsAreNotReparsePoints(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The persistent save path is required.", nameof(path));
            string current = Path.GetFullPath(path);
            bool foundExistingAncestor = false;
            while (!string.IsNullOrEmpty(current))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                    foundExistingAncestor = true;
                    if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                        (attributes & FileAttributes.Directory) == 0)
                        throw new IOException("Persistent save paths cannot contain reparse points or files: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }

                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current)) break;
                current = parent;
            }
            if (!foundExistingAncestor)
                throw new IOException("No existing ancestor of the persistent save path could be verified.");
        }

        private static void EnsureDiagnosticSessionStillIsolated(string storeDirectory)
        {
            string persistentDataPath = Application.persistentDataPath;
            EnsurePathAndExistingAncestorsAreNotReparsePoints(persistentDataPath);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(storeDirectory);
            if (!AreDiagnosticSavePathsDisjoint(storeDirectory, persistentDataPath))
                throw new IOException("Diagnostic and persistent save paths are no longer disjoint.");
        }

        private static bool PathsOverlap(string first, string second)
        {
            return IsPathEqualOrWithin(first, second) || IsPathEqualOrWithin(second, first);
        }

        private static bool IsPathEqualOrWithin(string candidate, string parent)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(parent)) return false;
            string normalizedCandidate = NormalizePath(candidate);
            string normalizedParent = NormalizePath(parent);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(normalizedCandidate, normalizedParent, comparison)) return true;

            string parentWithSeparator = normalizedParent.EndsWith(Path.DirectorySeparatorChar.ToString(), comparison)
                ? normalizedParent
                : normalizedParent + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(parentWithSeparator, comparison);
        }

        private static bool PathEquals(string first, string second)
        {
            return string.Equals(NormalizePath(first), NormalizePath(second),
                Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;
            while (fullPath.Length > root.Length &&
                (fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar ||
                 fullPath[fullPath.Length - 1] == Path.AltDirectorySeparatorChar))
            {
                fullPath = fullPath.Substring(0, fullPath.Length - 1);
            }
            return fullPath;
        }
    }
}
#endif
