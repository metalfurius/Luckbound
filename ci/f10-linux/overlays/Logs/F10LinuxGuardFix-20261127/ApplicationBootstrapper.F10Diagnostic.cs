#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    [Serializable]
    internal sealed class F10DiagnosticSessionManifest
    {
        public int schemaVersion;
        public string status;
        public string copyRoot;
        public string copyEvidenceKind;
        public string copyCertificateSha256;
        public string preflightReceiptSha256;
        public string contentManifestSha256;
        public string sessionRelativeDirectory;
        public string runId;
        public int contentVersion;
        public int generationVersion;
        public uint seed;
        public string routeSnapshotJson;
        public string routeSnapshotSha256;
        public string itemsCatalogSha256;
        public string encounterCatalogSha256;
        public F10DiagnosticContentFile[] contentFiles;
        public string createdUtc;
        public string creatorNonce;

        internal static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        internal static string Sha256(byte[] value)
        {
            using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(value ?? Array.Empty<byte>()));
        }

        internal static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    [Serializable]
    internal sealed class F10DiagnosticContentFile
    {
        public string path;
        public string sha256;
        public string physicalIdentity;
        public uint linkCount;
    }

    internal struct F10DiagnosticPhysicalFileIdentity
    {
        internal F10DiagnosticPhysicalFileIdentity(string key, uint linkCount)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            LinkCount = linkCount;
        }

        internal string Key { get; }
        internal uint LinkCount { get; }
    }

    internal sealed class F10DiagnosticCopyEvidence
    {
        internal F10DiagnosticCopyEvidence(string projectRoot, string copyEvidenceKind,
            string copyCertificateSha256, string preflightReceiptSha256,
            IDictionary<string, string> baseFileHashes)
        {
            ProjectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            CopyEvidenceKind = copyEvidenceKind ?? throw new ArgumentNullException(nameof(copyEvidenceKind));
            CopyCertificateSha256 = copyCertificateSha256 ?? throw new ArgumentNullException(nameof(copyCertificateSha256));
            PreflightReceiptSha256 = preflightReceiptSha256 ?? throw new ArgumentNullException(nameof(preflightReceiptSha256));
            BaseFileHashes = baseFileHashes ?? throw new ArgumentNullException(nameof(baseFileHashes));
        }

        internal string ProjectRoot { get; }
        internal string CopyEvidenceKind { get; }
        internal string CopyCertificateSha256 { get; }
        internal string PreflightReceiptSha256 { get; }
        internal IDictionary<string, string> BaseFileHashes { get; }
    }

    [Serializable]
    internal sealed class F10DiagnosticCopyCertificate
    {
        public int schemaVersion;
        public string status;
        public string sourceRoot;
        public string copyRoot;
        public bool reparsePointsRejected;
        public bool fileIdentityChecksPassed;
        public bool f02ImportStagingAbsent;
        public F10DiagnosticCopyCertificateFile[] files;
    }

    [Serializable]
    internal sealed class F10DiagnosticCopyCertificateFile
    {
        public string path;
        public string sha256;
        public string sourceFileId;
        public string copyFileId;
    }

    [Serializable]
    internal sealed class F10DiagnosticCopyPreflightReceipt
    {
        public int schemaVersion;
        public string status;
        public string sourceRoot;
        public string copyRoot;
        public string certificateSha256;
        public string measuredCertificateSha256;
        public int fileCount;
        public bool externalPreflightVerified;
        public bool writersExcluded;
        public string issuedAtUtc;
    }

    public sealed partial class ApplicationBootstrapper
    {
        private const string F10DiagnosticSceneName = "F10Diagnostic";
        private const string F10DiagnosticRootRelativePath = "Logs/F10V6Resume-20261120";
        private const string F10DiagnosticSessionManifestRelativePath = F10DiagnosticRootRelativePath + "/session-manifest.json";
        private const string F10DiagnosticSessionsRelativePath = F10DiagnosticRootRelativePath + "/sessions";
        private const string F10DiagnosticCopyCertificateRelativePath = "Logs/F10DisposableArt-20261116/copy-certificate.json";
        private const string F10DiagnosticPreflightReceiptRelativePath = F10DiagnosticRootRelativePath + "/copy-preflight-receipt.json";
        private const string F10DiagnosticMaterializationReceiptRelativePath = F10DiagnosticRootRelativePath + "/materialization-receipt.json";
        private const string F10DiagnosticBellatorClassId = "class.bellator";
        private static readonly string[] F10DiagnosticContentPaths =
        {
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10Diagnostic.cs",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10Diagnostic.cs.meta",
            "Assets/Luckbound/Scripts/App/F10DiagnosticLinuxRuntimeEvidence.cs",
            "Assets/Luckbound/Scripts/App/F10DiagnosticSceneRunner.cs",
            "Assets/Luckbound/Scripts/App/F10DiagnosticSceneRunner.cs.meta",
            "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs",
            "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs.meta",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.cs",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10Cache.cs",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10Extraction.cs",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10RoomRewards.cs",
            "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.EncounterReadiness.cs",
            "Assets/Luckbound/Scripts/App/ExpeditionSaveMapper.cs",
            "Assets/Luckbound/Scripts/App/ExpeditionRunConfigurationFactory.cs",
            "Assets/Luckbound/Scripts/App/F10TechnicalEncounterPlanProvider.cs",
            "Assets/Luckbound/Scripts/App/ExpeditionGameplayComposition.cs",
            "Assets/Luckbound/Scripts/App/ExpeditionRoomPrefabRegistry.cs",
            "Assets/Luckbound/Scripts/App/F10ObjectiveDriver2D.cs",
            "Assets/Luckbound/Scripts/App/F10ObjectiveObservation.cs",
            "Assets/Luckbound/Scripts/App/RoomEncounterRuntime.cs",
            "Assets/Luckbound/Scripts/App/F10TutorialAdvanceCoordinator.cs",
            "Assets/Luckbound/Scripts/App/F10C1RuntimeCoordinator.cs",
            "Assets/Luckbound/Scripts/App/F10ExplorationAdvanceCoordinator.cs",
            "Assets/Luckbound/Scripts/App/F10GuardianRuntimeCoordinator.cs",
            "Assets/Luckbound/Scripts/App/F10ExtractionSettlementCoordinator.cs",
            "Assets/Luckbound/Scripts/App/F10ExplorationGoldCacheBinding2D.cs",
            "Assets/Resources/Luckbound/Data/Items/Items.json",
            "Assets/Resources/Luckbound/Data/Skills.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10SampleRoute.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10TutorialRoom.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioCombatC1Room.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioExplorationE1Room.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10PatioGuardianRoom.json",
            "Assets/Resources/Luckbound/Data/Rooms/F10SafeExtractionRoom.json",
            "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json",
            "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json",
            "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png",
            "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab",
            "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab",
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
        private static readonly string[] F10DiagnosticPostMaterializationOutputPaths =
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
        private static readonly string[] F10DiagnosticSkillIds =
        {
            "skill.bellator.circular_cut",
            "skill.bellator.guard"
        };

        private F10DiagnosticSessionWitness _f10DiagnosticSessionWitness;
        private static readonly object F10DiagnosticFactoryKey = new object();

        [StructLayout(LayoutKind.Sequential)]
        private struct F10DiagnosticFileTime
        {
            public uint lowDateTime;
            public uint highDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct F10DiagnosticByHandleFileInformation
        {
            public uint fileAttributes;
            public F10DiagnosticFileTime creationTime;
            public F10DiagnosticFileTime lastAccessTime;
            public F10DiagnosticFileTime lastWriteTime;
            public uint volumeSerialNumber;
            public uint fileSizeHigh;
            public uint fileSizeLow;
            public uint numberOfLinks;
            public uint fileIndexHigh;
            public uint fileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out F10DiagnosticByHandleFileInformation information);

        /// <summary>
        /// Begins one new Editor-only technical run after authenticating the fixed disposable-copy evidence.
        /// Existing sessions are preserved and never adopted as newly created diagnostic provenance.
        /// </summary>
        internal static async Task<ApplicationBootstrapper> BeginF10DiagnosticSessionAsync()
        {
            EnsureDiagnosticPlayContext();
            string projectRoot = GetProjectRoot();
            F10DiagnosticCopyEvidence evidence = VerifyDiagnosticCopyEvidence(projectRoot);
            string manifestPath = ProjectPath(projectRoot, F10DiagnosticSessionManifestRelativePath);
            if (ExistsAsAnyEntry(manifestPath))
                throw new InvalidOperationException("A diagnostic session manifest already exists; it was preserved and cannot be relabeled as new.");
            string sessionsRoot = ProjectPath(projectRoot, F10DiagnosticSessionsRelativePath);
            EnsureNewSessionsRoot(sessionsRoot, Application.persistentDataPath);

            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                F10DiagnosticBellatorClassId,
                ExpeditionRunConfiguration.HumanRaceId,
                F10DiagnosticSkillIds);
            if (run.Route.GenerationVersion != 2)
                throw new InvalidOperationException("The diagnostic route must use its fixed generation 2 definition.");
            string sessionRelativeDirectory = F10DiagnosticSessionsRelativePath + "/" + run.RunId;
            string sessionDirectory = ProjectPath(projectRoot, sessionRelativeDirectory);
            CreateUniqueSessionDirectory(sessionsRoot, sessionDirectory, Application.persistentDataPath);
            var physicalStore = new FileSaveStore(sessionDirectory, null);
            ISaveStore store = new F10DiagnosticGuardedFileSaveStore(physicalStore, sessionDirectory);
            F10DiagnosticSessionManifest manifest = CreateSessionManifest(projectRoot, sessionRelativeDirectory,
                run, evidence, status: "reserved");
            WriteNewManifest(manifestPath, manifest);
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
                if (!PathEquals(physicalStore.GetPrimaryPath(SaveDocument.Profile),
                        Path.Combine(sessionDirectory, FileSaveStore.ProfileFileName)) ||
                    !IsPathEqualOrWithin(Path.GetDirectoryName(physicalStore.GetPrimaryPath(SaveDocument.Profile)), sessionsRoot))
                    throw new InvalidOperationException("The diagnostic FileSaveStore escaped its unique session directory.");

                EnsureDiagnosticSessionStillIsolated(sessionDirectory);
                VerifyDiagnosticCopyEvidence(projectRoot);
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

                VerifyDiagnosticCopyEvidence(projectRoot);
                manifest.status = "active";
                WriteManifest(manifestPath, manifest);
                EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sessionDirectory);
                app._f10DiagnosticSessionWitness = new F10DiagnosticSessionWitness(
                    F10DiagnosticFactoryKey, app, sessionDirectory, physicalStore, store, run, manifest, evidence, resumed: false);
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

        /// <summary>Reopens only the exact active session created by the fixed F10 V6 diagnostic entry.</summary>
        internal static async Task<ApplicationBootstrapper> ResumeF10DiagnosticSessionAsync()
        {
            EnsureDiagnosticPlayContext();
            string projectRoot = GetProjectRoot();
            F10DiagnosticCopyEvidence evidence = VerifyDiagnosticCopyEvidence(projectRoot);
            string manifestPath = ProjectPath(projectRoot, F10DiagnosticSessionManifestRelativePath);
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("No first-process diagnostic session manifest exists; no profile will be imported.");
            F10DiagnosticSessionManifest manifest = ReadSessionManifest(manifestPath);
            VerifySessionManifest(projectRoot, manifest, evidence);

            string sessionDirectory = ProjectPath(projectRoot, manifest.sessionRelativeDirectory);
            EnsureDiagnosticSessionStillIsolated(sessionDirectory);
            if (!File.Exists(Path.Combine(sessionDirectory, FileSaveStore.ProfileFileName)))
                throw new InvalidOperationException("The fixed diagnostic session has no primary profile; it was preserved.");
            var physicalStore = new FileSaveStore(sessionDirectory, null);
            ISaveStore store = new F10DiagnosticGuardedFileSaveStore(physicalStore, sessionDirectory);
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
                    owner = new GameObject("ApplicationBootstrapper (F10 Diagnostic Resume)");
                    app = owner.AddComponent<ApplicationBootstrapper>();
                }
                finally
                {
                    if (ReferenceEquals(EditorSaveStoreFactoryForTests, diagnosticFactory))
                        EditorSaveStoreFactoryForTests = null;
                }

                if (EditorSaveStoreFactoryForTests != null || factoryInvocationCount != 1 || app == null ||
                    !ReferenceEquals(_instance, app))
                    throw new InvalidOperationException("The resumed bootstrapper did not receive its single manifest-bound FileSaveStore.");

                F10DiagnosticResumePermit permit = CreateF10DiagnosticResumePermit(
                    app, physicalStore, store, projectRoot, manifestPath, manifest, evidence);
                SaveRepository.F10DiagnosticResumeAccess access = SaveRepository.CreateF10DiagnosticResumeAccess(
                    store, physicalStore, app, permit);
                SaveLoadResult<ProfileSaveEnvelope> profileResult = await app._saveRepository.LoadF10DiagnosticResumeAsync(access);
                if (profileResult == null || profileResult.Status != SaveLoadStatus.LoadedPrimary ||
                    profileResult.Access != SaveDataAccess.ReadWrite || profileResult.Data == null)
                    throw new InvalidOperationException("The exact V6 diagnostic primary profile did not authenticate.");

                ProfileSaveEnvelope envelope = profileResult.Data;
                app.ProfileLoadStatus = profileResult.Status;
                app.ProfileLoadMessage = profileResult.Message;
                app._profileData = envelope.profile.Clone();
                app._contractProgression = CreateContractProgressionSnapshot(app._profileData);
                app._settlementLedger = new ExpeditionSettlementLedger(app._profileData.availableRenown,
                    app._profileData.processedSettlementRunIds.Select(RunId.Parse));
                app.ActiveRun = ExpeditionSaveMapper.Restore(envelope.activeExpedition, out app._activeExpedition);
                if (app.ActiveRun == null || !string.Equals(app.ActiveRun.RunId.ToString(), manifest.runId, StringComparison.Ordinal))
                    throw new InvalidOperationException("ExpeditionSaveMapper.Restore returned a different run than the first-process session.");
                app._activeExpeditionRevision = 0;
                app._activeExpeditionLoadError = string.Empty;

                SaveLoadResult<SettingsSaveEnvelope> settingsResult = await app._saveRepository.LoadSettingsAsync();
                app.SettingsLoadStatus = settingsResult.Status;
                app.SettingsLoadMessage = settingsResult.Message;
                if (settingsResult.HasUsableData) app._settingsData = settingsResult.Data.settings.Clone();
                app.SaveDataReady = true;

                if (app.ProfileAccess != SaveDataAccess.ReadWrite || !app.CanContinueExpedition ||
                    app.HasPendingSessionMutation || app.HasPendingExpeditionWrite || app.HasPendingSettlement ||
                    app.HasPendingContractProgression || app.HasPendingEconomyMutation)
                    throw new InvalidOperationException("The resumed bootstrapper reconstructed an unsafe or pending session state.");

                VerifyDiagnosticCopyEvidence(projectRoot);
                app._f10DiagnosticSessionWitness = new F10DiagnosticSessionWitness(
                    F10DiagnosticFactoryKey, app, sessionDirectory, physicalStore, store,
                    app.ActiveRun, manifest, evidence, resumed: true);
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
                    PathEquals(witness.CopyRoot, GetProjectRoot()) &&
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
            internal F10DiagnosticSessionWitness(object factoryKey, ApplicationBootstrapper owner, string storeDirectory,
                FileSaveStore store, ISaveStore repositoryStore, ExpeditionRunConfiguration run,
                F10DiagnosticSessionManifest manifest, F10DiagnosticCopyEvidence evidence, bool resumed)
            {
                if (!ReferenceEquals(factoryKey, F10DiagnosticFactoryKey))
                    throw new InvalidOperationException("Only the private F10 diagnostic begin/resume entry may issue a session witness.");
                Owner = owner ?? throw new ArgumentNullException(nameof(owner));
                StoreDirectory = Path.GetFullPath(storeDirectory ?? throw new ArgumentNullException(nameof(storeDirectory)));
                SaveRoot = Path.GetDirectoryName(StoreDirectory);
                Store = store ?? throw new ArgumentNullException(nameof(store));
                RepositoryStore = repositoryStore ?? throw new ArgumentNullException(nameof(repositoryStore));
                RunId = (run ?? throw new ArgumentNullException(nameof(run))).RunId.ToString();
                SessionManifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
                CopyRoot = evidence?.ProjectRoot ?? throw new ArgumentNullException(nameof(evidence));
                CopyCertificateSha256 = evidence.CopyCertificateSha256;
                ContentManifestSha256 = manifest.contentManifestSha256;
                WasResumed = resumed;
                IsPersisted = true;
            }

            internal ApplicationBootstrapper Owner { get; }
            internal string StoreDirectory { get; }
            internal string SaveRoot { get; }
            internal FileSaveStore Store { get; }
            internal ISaveStore RepositoryStore { get; }
            internal string RunId { get; }
            internal string CopyRoot { get; }
            internal string CopyCertificateSha256 { get; }
            internal string ContentManifestSha256 { get; }
            internal F10DiagnosticSessionManifest SessionManifest { get; }
            internal bool WasResumed { get; }
            internal bool IsPersisted { get; }
        }

        internal sealed class F10DiagnosticResumePermit
        {
            internal F10DiagnosticResumePermit(object factoryKey, ApplicationBootstrapper owner, FileSaveStore physicalStore,
                ISaveStore repositoryStore, string projectRoot, string sessionDirectory, string manifestPath,
                string sessionManifestSha256, string runId, int generationVersion, uint seed,
                string routeSnapshotJson, string routeSnapshotSha256, string copyEvidenceKind, string copyCertificateSha256,
                string contentManifestSha256)
            {
                if (!ReferenceEquals(factoryKey, F10DiagnosticFactoryKey))
                    throw new InvalidOperationException("Only the private F10 diagnostic resume issuer may create a resume permit.");
                Owner = owner ?? throw new ArgumentNullException(nameof(owner));
                PhysicalStore = physicalStore ?? throw new ArgumentNullException(nameof(physicalStore));
                RepositoryStore = repositoryStore ?? throw new ArgumentNullException(nameof(repositoryStore));
                ProjectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
                SessionDirectory = Path.GetFullPath(sessionDirectory ?? throw new ArgumentNullException(nameof(sessionDirectory)));
                ManifestPath = Path.GetFullPath(manifestPath ?? throw new ArgumentNullException(nameof(manifestPath)));
                SessionManifestSha256 = sessionManifestSha256 ?? throw new ArgumentNullException(nameof(sessionManifestSha256));
                RunId = runId ?? throw new ArgumentNullException(nameof(runId));
                GenerationVersion = generationVersion;
                Seed = seed;
                RouteSnapshotJson = routeSnapshotJson ?? throw new ArgumentNullException(nameof(routeSnapshotJson));
                RouteSnapshotSha256 = routeSnapshotSha256 ?? throw new ArgumentNullException(nameof(routeSnapshotSha256));
                CopyEvidenceKind = copyEvidenceKind ?? throw new ArgumentNullException(nameof(copyEvidenceKind));
                CopyCertificateSha256 = copyCertificateSha256 ?? throw new ArgumentNullException(nameof(copyCertificateSha256));
                ContentManifestSha256 = contentManifestSha256 ?? throw new ArgumentNullException(nameof(contentManifestSha256));
            }

            internal ApplicationBootstrapper Owner { get; }
            internal FileSaveStore PhysicalStore { get; }
            internal ISaveStore RepositoryStore { get; }
            internal string ProjectRoot { get; }
            internal string SessionDirectory { get; }
            internal string ManifestPath { get; }
            internal string SessionManifestSha256 { get; }
            internal string RunId { get; }
            internal int GenerationVersion { get; }
            internal uint Seed { get; }
            internal string RouteSnapshotJson { get; }
            internal string RouteSnapshotSha256 { get; }
            internal string CopyEvidenceKind { get; }
            internal string CopyCertificateSha256 { get; }
            internal string ContentManifestSha256 { get; }
        }

        internal static bool IsF10DiagnosticScene(string sceneName)
        {
            return string.Equals(sceneName, F10DiagnosticSceneName, StringComparison.Ordinal);
        }

        internal static bool AreDiagnosticSavePathsDisjoint(string sessionDirectory, string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory) || string.IsNullOrWhiteSpace(persistentDataPath))
                return false;
            return !PathsOverlap(sessionDirectory, persistentDataPath);
        }

        internal static F10DiagnosticPhysicalFileIdentity InspectDiagnosticFileIdentity(string path)
        {
            EnsurePlainFileAndAncestors(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                Application.platform == RuntimePlatform.LinuxEditor)
            {
                F10DiagnosticLinuxFileMetadata metadata = F10DiagnosticLinuxRuntimeEvidenceReader.InspectRegularFile(path);
                return new F10DiagnosticPhysicalFileIdentity(metadata.Identity, metadata.LinkCount);
            }
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                Application.platform != RuntimePlatform.WindowsEditor)
                throw new PlatformNotSupportedException("The F10 diagnostic save-leaf identity requires Windows or the measured Linux CI overlay.");

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out F10DiagnosticByHandleFileInformation information))
                    throw new IOException("Could not measure a diagnostic file's physical identity: " + path);
                string key = information.volumeSerialNumber.ToString("x8", CultureInfo.InvariantCulture) + ":" +
                    information.fileIndexHigh.ToString("x8", CultureInfo.InvariantCulture) +
                    information.fileIndexLow.ToString("x8", CultureInfo.InvariantCulture);
                return new F10DiagnosticPhysicalFileIdentity(key, information.numberOfLinks);
            }
        }

        internal static void VerifyF10DiagnosticStoreLeafIntegrity(string sessionDirectory, bool requireProfilePrimary)
        {
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sessionDirectory);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                FileSaveStore.ProfileFileName,
                FileSaveStore.ProfileFileName + ".bak",
                FileSaveStore.ProfileFileName + ".tmp",
                FileSaveStore.ProfileFileName + ".bak.tmp",
                FileSaveStore.SettingsFileName,
                FileSaveStore.SettingsFileName + ".bak",
                FileSaveStore.SettingsFileName + ".tmp",
                FileSaveStore.SettingsFileName + ".bak.tmp"
            };
            var physicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string primaryProfile = Path.Combine(sessionDirectory, FileSaveStore.ProfileFileName);
            foreach (string path in Directory.GetFileSystemEntries(sessionDirectory))
            {
                string name = Path.GetFileName(path);
                if (!allowed.Contains(name))
                    throw new IOException("The diagnostic store contains an unrecognized leaf: " + path);
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
                    throw new IOException("Diagnostic save leaves must be plain files: " + path);
                if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("A temporary save leaf survived outside an active FileSaveStore transaction: " + path);
                F10DiagnosticPhysicalFileIdentity identity = InspectDiagnosticFileIdentity(path);
                if (identity.LinkCount != 1 || !physicalIds.Add(identity.Key))
                    throw new IOException("Diagnostic save leaves share a physical identity or have multiple links: " + path);
            }
            if (requireProfilePrimary && !File.Exists(primaryProfile))
                throw new FileNotFoundException("The exact diagnostic session primary profile is missing.", primaryProfile);
        }

        private static F10DiagnosticResumePermit CreateF10DiagnosticResumePermit(ApplicationBootstrapper owner,
            FileSaveStore physicalStore, ISaveStore repositoryStore, string projectRoot, string manifestPath,
            F10DiagnosticSessionManifest manifest, F10DiagnosticCopyEvidence evidence)
        {
            if (owner == null || physicalStore == null || repositoryStore == null || manifest == null || evidence == null)
                throw new ArgumentNullException("A real bootstrapper, store, manifest and verified copy evidence are required.");
            string sessionDirectory = ProjectPath(projectRoot, manifest.sessionRelativeDirectory);
            return new F10DiagnosticResumePermit(F10DiagnosticFactoryKey, owner, physicalStore, repositoryStore, projectRoot,
                sessionDirectory, manifestPath, HashFile(manifestPath), manifest.runId,
                manifest.generationVersion, manifest.seed, manifest.routeSnapshotJson,
                manifest.routeSnapshotSha256, evidence.CopyEvidenceKind, evidence.CopyCertificateSha256,
                manifest.contentManifestSha256);
        }

        internal static bool IsF10DiagnosticResumePermitValid(F10DiagnosticResumePermit permit,
            ApplicationBootstrapper owner, FileSaveStore physicalStore, ISaveStore repositoryStore)
        {
            try
            {
                if (permit == null || owner == null || !ReferenceEquals(_instance, owner) ||
                    !ReferenceEquals(permit.Owner, owner) || !ReferenceEquals(permit.PhysicalStore, physicalStore) ||
                    !ReferenceEquals(permit.RepositoryStore, repositoryStore) || owner.F10DiagnosticWitness != null ||
                    owner._saveRepository == null || !IsF10DiagnosticScene(SceneManager.GetActiveScene().name) ||
                    !PathEquals(GetProjectRoot(), permit.ProjectRoot) ||
                    !AreDiagnosticSavePathsDisjoint(permit.SessionDirectory, Application.persistentDataPath))
                    return false;

                EnsureDiagnosticSessionStillIsolated(permit.SessionDirectory);
                VerifyF10DiagnosticStoreLeafIntegrity(permit.SessionDirectory, requireProfilePrimary: true);
                F10DiagnosticCopyEvidence evidence = VerifyDiagnosticCopyEvidence(permit.ProjectRoot);
                if (!string.Equals(evidence.CopyEvidenceKind, permit.CopyEvidenceKind, StringComparison.Ordinal) ||
                    !string.Equals(evidence.CopyCertificateSha256, permit.CopyCertificateSha256, StringComparison.Ordinal) ||
                    !PathEquals(permit.ManifestPath, ProjectPath(permit.ProjectRoot, F10DiagnosticSessionManifestRelativePath)) ||
                    !string.Equals(HashFile(permit.ManifestPath), permit.SessionManifestSha256, StringComparison.Ordinal))
                    return false;
                F10DiagnosticSessionManifest manifest = ReadSessionManifest(permit.ManifestPath);
                VerifySessionManifest(permit.ProjectRoot, manifest, evidence);
                return PathEquals(ProjectPath(permit.ProjectRoot, manifest.sessionRelativeDirectory), permit.SessionDirectory) &&
                    string.Equals(manifest.runId, permit.RunId, StringComparison.Ordinal) &&
                    manifest.generationVersion == permit.GenerationVersion && manifest.seed == permit.Seed &&
                    string.Equals(manifest.routeSnapshotJson, permit.RouteSnapshotJson, StringComparison.Ordinal) &&
                    string.Equals(manifest.routeSnapshotSha256, permit.RouteSnapshotSha256, StringComparison.Ordinal) &&
                    string.Equals(manifest.contentManifestSha256, permit.ContentManifestSha256, StringComparison.Ordinal);
            }
            catch { return false; }
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

        private static void EnsureDiagnosticPlayContext()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("The F10 diagnostic session requires Play Mode.");
            if (!IsF10DiagnosticScene(SceneManager.GetActiveScene().name))
                throw new InvalidOperationException("Load the explicit F10Diagnostic technical scene before starting or resuming the session.");
            if (_instance != null)
                throw new InvalidOperationException("A bootstrapper already exists; the diagnostic session will not take it over.");
            if (EditorSaveStoreFactoryForTests != null)
                throw new InvalidOperationException("Another Editor save-store factory is installed; the diagnostic session will not replace it.");
        }

        private static void EnsureNewSessionsRoot(string sessionsRoot, string persistentDataPath)
        {
            string projectRoot = GetProjectRoot();
            string expected = ProjectPath(projectRoot, F10DiagnosticSessionsRelativePath);
            if (!PathEquals(sessionsRoot, expected) || !IsPathEqualOrWithin(expected, projectRoot) ||
                !AreDiagnosticSavePathsDisjoint(expected, persistentDataPath))
                throw new InvalidOperationException("The diagnostic sessions root is not fixed beneath the disposable project Logs directory.");
            EnsurePathAndExistingAncestorsAreNotReparsePoints(persistentDataPath);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(Path.GetDirectoryName(expected));
            if (File.Exists(expected))
                throw new InvalidOperationException("The fixed diagnostic sessions root is occupied by a file; it was preserved.");
            if (!Directory.Exists(expected)) Directory.CreateDirectory(expected);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(expected);
        }

        private static void CreateUniqueSessionDirectory(string sessionsRoot, string sessionDirectory,
            string persistentDataPath)
        {
            if (!IsPathEqualOrWithin(sessionDirectory, sessionsRoot) || PathEquals(sessionDirectory, sessionsRoot) ||
                !AreDiagnosticSavePathsDisjoint(sessionDirectory, persistentDataPath))
                throw new InvalidOperationException("The candidate diagnostic session path failed fixed-root isolation.");
            if (ExistsAsAnyEntry(sessionDirectory))
                throw new InvalidOperationException("The RunId-derived diagnostic session directory already exists; it was preserved.");
            Directory.CreateDirectory(sessionDirectory);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sessionDirectory);
            if (Directory.GetFileSystemEntries(sessionDirectory).Length != 0)
                throw new IOException("The new diagnostic session directory was not empty at creation; its contents were preserved.");
        }

        private static F10DiagnosticSessionManifest CreateSessionManifest(string projectRoot,
            string sessionRelativeDirectory, ExpeditionRunConfiguration run, F10DiagnosticCopyEvidence evidence,
            string status)
        {
            F10DiagnosticContentFile[] files = ComputeDiagnosticContentFiles(projectRoot, evidence.BaseFileHashes);
            string contentManifestSha256 = ComputeContentManifestSha256(files);
            return new F10DiagnosticSessionManifest
            {
                schemaVersion = 1,
                status = status,
                copyRoot = Path.GetFullPath(projectRoot),
                copyEvidenceKind = evidence.CopyEvidenceKind,
                copyCertificateSha256 = evidence.CopyCertificateSha256,
                preflightReceiptSha256 = evidence.PreflightReceiptSha256,
                contentManifestSha256 = contentManifestSha256,
                sessionRelativeDirectory = NormalizeRelativePath(sessionRelativeDirectory),
                runId = run.RunId.ToString(),
                contentVersion = SaveFormat.ContentVersion,
                generationVersion = run.Route.GenerationVersion,
                seed = run.Seed,
                routeSnapshotJson = run.Route.CanonicalForm,
                routeSnapshotSha256 = F10DiagnosticSessionManifest.Sha256(run.Route.CanonicalForm),
                itemsCatalogSha256 = FindContentFileHash(files, "Assets/Resources/Luckbound/Data/Items/Items.json"),
                encounterCatalogSha256 = ComputeEncounterCatalogSha256(files),
                contentFiles = files,
                createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                creatorNonce = Guid.NewGuid().ToString("N")
            };
        }

        private static void VerifySessionManifest(string projectRoot, F10DiagnosticSessionManifest manifest,
            F10DiagnosticCopyEvidence evidence)
        {
            if (manifest == null || manifest.schemaVersion != 1 ||
                !string.Equals(manifest.status, "active", StringComparison.Ordinal) ||
                !PathEquals(manifest.copyRoot, projectRoot) ||
                (!string.Equals(manifest.copyEvidenceKind, evidence.CopyEvidenceKind, StringComparison.Ordinal) &&
                 !(string.IsNullOrEmpty(manifest.copyEvidenceKind) &&
                   string.Equals(evidence.CopyEvidenceKind, "windows-copy-certificate-v1", StringComparison.Ordinal))) ||
                !string.Equals(manifest.copyCertificateSha256, evidence.CopyCertificateSha256, StringComparison.Ordinal) ||
                manifest.contentVersion != 6 || manifest.generationVersion != 2 ||
                string.IsNullOrWhiteSpace(manifest.creatorNonce) || string.IsNullOrWhiteSpace(manifest.createdUtc))
                throw new InvalidOperationException("The fixed session manifest is not an active V6 session created in this certified copy.");

            RunId expectedRunId;
            try { expectedRunId = RunId.Parse(manifest.runId); }
            catch (Exception exception) { throw new FormatException("The fixed session manifest RunId is invalid.", exception); }
            if (!string.Equals(expectedRunId.ToString(), manifest.runId, StringComparison.Ordinal))
                throw new FormatException("The fixed session manifest RunId is not canonical.");

            string expectedSessionRelative = NormalizeRelativePath(F10DiagnosticSessionsRelativePath + "/" + expectedRunId);
            if (!string.Equals(NormalizeRelativePath(manifest.sessionRelativeDirectory), expectedSessionRelative,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("The fixed session manifest points outside the RunId-derived directory.");
            ExpeditionRoutePlan route;
            try { route = ExpeditionRoutePlan.ParseCanonical(manifest.routeSnapshotJson); }
            catch (Exception exception) { throw new FormatException("The fixed session route snapshot is invalid.", exception); }
            if (route.GenerationVersion != manifest.generationVersion || route.Seed != manifest.seed ||
                !string.Equals(F10DiagnosticSessionManifest.Sha256(manifest.routeSnapshotJson),
                    manifest.routeSnapshotSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("The fixed route content, seed or generation changed after process A.");

            F10DiagnosticContentFile[] currentFiles = ComputeDiagnosticContentFiles(projectRoot, evidence.BaseFileHashes);
            string currentContentHash = ComputeContentManifestSha256(currentFiles);
            if (!ContentFilesEqual(manifest.contentFiles, currentFiles) ||
                !string.Equals(currentContentHash, manifest.contentManifestSha256, StringComparison.Ordinal) ||
                !string.Equals(FindContentFileHash(currentFiles, "Assets/Resources/Luckbound/Data/Items/Items.json"),
                    manifest.itemsCatalogSha256, StringComparison.Ordinal) ||
                !string.Equals(ComputeEncounterCatalogSha256(currentFiles), manifest.encounterCatalogSha256,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("The fixed F10 content/catalog manifest changed after process A.");

            string sessionDirectory = ProjectPath(projectRoot, manifest.sessionRelativeDirectory);
            EnsureDiagnosticSessionStillIsolated(sessionDirectory);
        }

        private static F10DiagnosticSessionManifest ReadSessionManifest(string manifestPath)
        {
            EnsurePlainFileAndAncestors(manifestPath);
            F10DiagnosticSessionManifest manifest;
            try { manifest = JsonUtility.FromJson<F10DiagnosticSessionManifest>(File.ReadAllText(manifestPath, Encoding.UTF8)); }
            catch (Exception exception) { throw new FormatException("The fixed diagnostic session manifest could not be parsed.", exception); }
            if (manifest == null) throw new FormatException("The fixed diagnostic session manifest is empty.");
            return manifest;
        }

        private static void WriteNewManifest(string path, F10DiagnosticSessionManifest manifest)
        {
            EnsurePlainAncestors(path);
            string json = JsonUtility.ToJson(manifest, true) + "\n";
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(json);
        }

        private static void WriteManifest(string path, F10DiagnosticSessionManifest manifest)
        {
            EnsurePlainFileAndAncestors(path);
            string json = JsonUtility.ToJson(manifest, true) + "\n";
            string temporaryPath = path + ".tmp";
            if (ExistsAsAnyEntry(temporaryPath))
                throw new IOException("A diagnostic manifest temporary already exists; it was preserved.");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(json);
            try
            {
                if (File.Exists(path)) File.Replace(temporaryPath, path, null, ignoreMetadataErrors: true);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static F10DiagnosticCopyEvidence VerifyDiagnosticCopyEvidence(string projectRoot)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                Application.platform == RuntimePlatform.LinuxEditor)
                return VerifyLinuxDiagnosticCopyEvidence(projectRoot);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                Application.platform != RuntimePlatform.WindowsEditor)
                throw new PlatformNotSupportedException("The F10 diagnostic copy verifier supports the fixed Windows path or its measured Linux CI overlay.");

            return VerifyWindowsDiagnosticCopyEvidence(projectRoot);
        }

        private static F10DiagnosticCopyEvidence VerifyLinuxDiagnosticCopyEvidence(string projectRoot)
        {
            EnsureDiagnosticPathIsDisposable(projectRoot);
            F10DiagnosticLinuxRuntimeEvidence linuxEvidence =
                F10DiagnosticLinuxRuntimeEvidenceReader.ValidateForRuntime(projectRoot);
            var fileHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, F10DiagnosticLinuxInventoryFile> pair in linuxEvidence.Inventory)
                fileHashes.Add(pair.Key, pair.Value.sha256);
            return new F10DiagnosticCopyEvidence(projectRoot, "linux-copy-inventory-v1",
                linuxEvidence.CopyInventorySha256, linuxEvidence.ReceiptSha256, fileHashes);
        }

        private static F10DiagnosticCopyEvidence VerifyWindowsDiagnosticCopyEvidence(string projectRoot)
        {
            RequireMeasuredMaterializationEvidence(projectRoot);
            EnsureDiagnosticPathIsDisposable(projectRoot);
            string certificatePath = ProjectPath(projectRoot, F10DiagnosticCopyCertificateRelativePath);
            string receiptPath = ProjectPath(projectRoot, F10DiagnosticPreflightReceiptRelativePath);
            EnsurePlainFileAndAncestors(certificatePath);
            EnsurePlainFileAndAncestors(receiptPath);
            if (!File.Exists(certificatePath) || !File.Exists(receiptPath))
                throw new InvalidOperationException("The fixed copy certificate or current external preflight receipt is missing.");

            string certificateBytes = File.ReadAllText(certificatePath, Encoding.UTF8);
            string certificateSha256 = HashFile(certificatePath);
            F10DiagnosticCopyCertificate certificate;
            F10DiagnosticCopyPreflightReceipt receipt;
            try
            {
                certificate = JsonUtility.FromJson<F10DiagnosticCopyCertificate>(certificateBytes);
                receipt = JsonUtility.FromJson<F10DiagnosticCopyPreflightReceipt>(File.ReadAllText(receiptPath, Encoding.UTF8));
            }
            catch (Exception exception) { throw new FormatException("The fixed copy evidence could not be parsed.", exception); }

            if (certificate == null || certificate.schemaVersion != 1 ||
                !string.Equals(certificate.status, "verified", StringComparison.Ordinal) ||
                !PathEquals(certificate.copyRoot, projectRoot) || string.IsNullOrWhiteSpace(certificate.sourceRoot) ||
                PathEquals(certificate.sourceRoot, projectRoot) || !certificate.reparsePointsRejected ||
                !certificate.fileIdentityChecksPassed || !certificate.f02ImportStagingAbsent ||
                certificate.files == null || certificate.files.Length == 0)
                throw new InvalidOperationException("The fixed copy certificate does not identify an independent, verified F10 project copy.");

            string sourceRoot = Path.GetFullPath(certificate.sourceRoot);
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(sourceRoot);
            string expectedSourceRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "..", ".."));
            if (!PathEquals(sourceRoot, expectedSourceRoot))
                throw new InvalidOperationException("The copy certificate source root is not the fixed parent of this disposable-copy path.");
            string approvedCopiesRoot = Path.Combine(sourceRoot,
                "Logs/F10DisposableArt-20261116/copies".Replace('/', Path.DirectorySeparatorChar));
            if (!IsPathEqualOrWithin(projectRoot, approvedCopiesRoot) || PathEquals(projectRoot, approvedCopiesRoot))
                throw new InvalidOperationException("The certified project is outside its fixed disposable-copy root.");

            // The receipt records the coordinator's external preflight; it does not by itself grant
            // authority or prove exclusion. The copy certificate and measured materialization
            // validator are independently checked against actual files and identities.
            if (receipt == null || receipt.schemaVersion != 1 ||
                !string.Equals(receipt.status, "verified-at-launch", StringComparison.Ordinal) ||
                !PathEquals(receipt.sourceRoot, sourceRoot) || !PathEquals(receipt.copyRoot, projectRoot) ||
                !string.Equals(ReceiptCertificateSha256(receipt), certificateSha256, StringComparison.Ordinal) ||
                receipt.fileCount != certificate.files.Length || !receipt.externalPreflightVerified || !receipt.writersExcluded ||
                !IsRecentReceipt(receipt.issuedAtUtc))
                throw new InvalidOperationException("The fixed preflight receipt does not match the actual certificate and copy roots.");

            var filesByPath = new Dictionary<string, F10DiagnosticCopyCertificateFile>(StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var copyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (F10DiagnosticCopyCertificateFile file in certificate.files)
            {
                string normalizedPath = file == null ? null : NormalizeRelativePath(file.path);
                if (file == null || !IsSafeRelativePath(file.path) ||
                    string.IsNullOrWhiteSpace(file.sha256) || string.IsNullOrWhiteSpace(file.sourceFileId) ||
                    string.IsNullOrWhiteSpace(file.copyFileId) ||
                    string.Equals(file.sourceFileId, file.copyFileId, StringComparison.Ordinal) ||
                    filesByPath.ContainsKey(normalizedPath) ||
                    !sourceIds.Add(file.sourceFileId) || !copyIds.Add(file.copyFileId))
                    throw new InvalidOperationException("The copy certificate has an invalid or duplicate file identity record.");
                filesByPath.Add(normalizedPath, file);
            }
            if (sourceIds.Overlaps(copyIds))
                throw new InvalidOperationException("The copy certificate reports shared source/copy file identities.");

            var fileHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, F10DiagnosticCopyCertificateFile> pair in filesByPath)
                fileHashes.Add(pair.Key, pair.Value.sha256);
            return new F10DiagnosticCopyEvidence(projectRoot, "windows-copy-certificate-v1", certificateSha256,
                F10DiagnosticSessionManifest.Sha256(File.ReadAllBytes(receiptPath)), fileHashes);
        }

        private static void RequireMeasuredMaterializationEvidence(string projectRoot)
        {
            F10DiagnosticMaterializationReceiptValidator.ValidateForRuntime(projectRoot);
        }

        private static bool VerifyDiagnosticEvidenceStillMatches(string projectRoot, F10DiagnosticSessionManifest manifest)
        {
            try
            {
                F10DiagnosticCopyEvidence evidence = VerifyDiagnosticCopyEvidence(projectRoot);
                if (!string.Equals(evidence.CopyCertificateSha256, manifest.copyCertificateSha256, StringComparison.Ordinal))
                    return false;
                VerifySessionManifest(projectRoot, manifest, evidence);
                return true;
            }
            catch { return false; }
        }

        private static void EnsureDiagnosticPathIsDisposable(string projectRoot)
        {
            EnsureExistingDirectoryAndAncestorsAreNotReparsePoints(projectRoot);
            foreach (string relative in new[]
            {
                ".git",
                "Assets/Luckbound/Editor/F02ImportStaging",
                "Assets/Luckbound/Editor/F02ImportStaging.meta",
                "ProjectSettings/Luckbound/F02ImportRecovery/pending",
                "Library/Luckbound/F02/Transactions"
            })
            {
                string path = ProjectPath(projectRoot, relative);
                EnsurePlainAncestors(path);
                if (ExistsAsAnyEntry(path))
                    throw new InvalidOperationException("F02 or Git control state blocks this disposable diagnostic copy: " + relative);
            }
        }

        private static bool IsRecentReceipt(string value)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime issued))
                return false;
            DateTime utc = issued.ToUniversalTime();
            TimeSpan age = DateTime.UtcNow - utc;
            return age >= TimeSpan.FromMinutes(-1) && age <= TimeSpan.FromMinutes(30);
        }

        private static string ReceiptCertificateSha256(F10DiagnosticCopyPreflightReceipt receipt)
        {
            return string.IsNullOrWhiteSpace(receipt.measuredCertificateSha256)
                ? receipt.certificateSha256
                : receipt.measuredCertificateSha256;
        }

        private static string[] F10DiagnosticManifestPaths()
        {
            var paths = new List<string>();
            foreach (string assetPath in F10DiagnosticContentPaths)
            {
                string normalized = NormalizeRelativePath(assetPath);
                paths.Add(normalized);
                if (!normalized.EndsWith(".meta", StringComparison.Ordinal))
                    paths.Add(normalized + ".meta");
            }
            return paths.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static F10DiagnosticContentFile[] ComputeDiagnosticContentFiles(string projectRoot,
            IDictionary<string, string> baseFileHashes)
        {
            string[] paths = F10DiagnosticManifestPaths();
            var result = new F10DiagnosticContentFile[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                string relative = NormalizeRelativePath(paths[index]);
                string fullPath = ProjectPath(projectRoot, relative);
                EnsurePlainFileAndAncestors(fullPath);
                if (!File.Exists(fullPath) || baseFileHashes == null)
                    throw new InvalidOperationException("The fixed copy manifest is missing a required F10 input: " + relative);
                string hash = HashFile(fullPath);
                bool isMeasuredPostMaterializationOutput = IsF10DiagnosticPostMaterializationOutputPath(relative);
                bool existsInBaseEvidence = baseFileHashes.TryGetValue(relative, out string measuredHash);
                if (!isMeasuredPostMaterializationOutput && !existsInBaseEvidence)
                    throw new InvalidOperationException("The immutable base copy evidence is missing a required F10 input: " + relative);
                if (!isMeasuredPostMaterializationOutput && !string.Equals(hash, measuredHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A fixed F10 input differs from the immutable measured copy evidence: " + relative);
                result[index] = new F10DiagnosticContentFile { path = relative, sha256 = hash };
            }
            return result;
        }

        private static bool IsF10DiagnosticPostMaterializationOutputPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            foreach (string output in F10DiagnosticPostMaterializationOutputPaths)
                if (string.Equals(normalized, output, StringComparison.Ordinal) ||
                    string.Equals(normalized, output + ".meta", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string ComputeContentManifestSha256(F10DiagnosticContentFile[] files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            var text = new StringBuilder();
            foreach (F10DiagnosticContentFile file in files)
                text.Append(file.path).Append('\n').Append(file.sha256).Append('\n');
            return F10DiagnosticSessionManifest.Sha256(text.ToString());
        }

        private static bool ContentFilesEqual(F10DiagnosticContentFile[] left, F10DiagnosticContentFile[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] == null || right[index] == null ||
                    !string.Equals(left[index].path, right[index].path, StringComparison.Ordinal) ||
                    !string.Equals(left[index].sha256, right[index].sha256, StringComparison.Ordinal))
                    return false;
            return true;
        }

        private static string FindContentFileHash(F10DiagnosticContentFile[] files, string path)
        {
            F10DiagnosticContentFile found = files?.SingleOrDefault(file =>
                file != null && string.Equals(file.path, path, StringComparison.Ordinal));
            if (found == null) throw new InvalidOperationException("The fixed content manifest is missing " + path + ".");
            return found.sha256;
        }

        private static string ComputeEncounterCatalogSha256(F10DiagnosticContentFile[] files)
        {
            return F10DiagnosticSessionManifest.Sha256(
                "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json\n" +
                FindContentFileHash(files, "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json") + "\n" +
                "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json\n" +
                FindContentFileHash(files, "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json") + "\n");
        }

        private static void EnsurePlainFileAndAncestors(string path)
        {
            EnsurePlainAncestors(path);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A required diagnostic file is missing or a reparse point: " + path);
        }

        private static void EnsurePlainAncestors(string path)
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

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return F10DiagnosticSessionManifest.ToHex(sha.ComputeHash(stream));
        }

        private static string ProjectPath(string projectRoot, string relativePath)
        {
            if (!IsSafeRelativePath(relativePath))
                throw new ArgumentException("Diagnostic paths must be fixed project-relative paths.", nameof(relativePath));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathEqualOrWithin(fullPath, projectRoot))
                throw new InvalidOperationException("A fixed diagnostic path escaped its project root.");
            return fullPath;
        }

        private static bool IsSafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.IndexOf('\\') >= 0) return false;
            string[] parts = value.Split('/');
            return parts.All(part => !string.IsNullOrWhiteSpace(part) && part != "." && part != "..");
        }

        private static string NormalizeRelativePath(string value)
        {
            if (!IsSafeRelativePath(value)) throw new FormatException("A diagnostic relative path is invalid.");
            return string.Join("/", value.Split('/'));
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
            string projectRoot = GetProjectRoot();
            string sessionsRoot = ProjectPath(projectRoot, F10DiagnosticSessionsRelativePath);
            if (!AreDiagnosticSavePathsDisjoint(storeDirectory, persistentDataPath) ||
                !IsPathEqualOrWithin(storeDirectory, sessionsRoot) || PathEquals(storeDirectory, sessionsRoot))
                throw new IOException("The manifest-bound diagnostic session is no longer under its fixed, disjoint copy path.");
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
