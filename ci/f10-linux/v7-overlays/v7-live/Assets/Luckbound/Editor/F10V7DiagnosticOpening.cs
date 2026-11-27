#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Luckbound.Editor
{
    /// <summary>
    /// Opens a new V7 diagnostic run only in a preflight-verified disposable Unity copy.
    /// The receipt is a provenance seam for a later composition package, not gameplay authority.
    /// </summary>
    internal static class F10V7DiagnosticOpening
    {
        private const string BellatorClassId = "class.bellator";
        private static readonly string[] BellatorSkills =
        {
            "skill.bellator.circular_cut",
            "skill.bellator.guard"
        };

        internal static async Task<F10V7DiagnosticOpeningReceipt> OpenAsync(string physicalRevisionId,
            string expectedBundleManifestSha256)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("The technical V7 opening runs only in Unity Editor Play Mode inside the verified disposable copy.");
            if (SceneManager.GetActiveScene().name == "Bootstrap")
                throw new InvalidOperationException("The diagnostic opening cannot run in Bootstrap, whose automatic V6 loader would start a product scene.");

            // The catalog compatibility facade obtains App's private-constructor verified-input receipt.
            // Its sole VerifyAndLoad path checks this exact current copy before any AssetDatabase reads.
            string projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
            F10V7DiagnosticPhysicalInputs physical = F10V7DiagnosticCatalogBuilder.LoadAndVerifyPhysicalInputs(
                projectRoot, physicalRevisionId, expectedBundleManifestSha256);

            if (ApplicationBootstrapper.Instance != null)
                throw new InvalidOperationException("An ApplicationBootstrapper already owns this Editor process; the diagnostic opener will not take it over.");
            if (ApplicationBootstrapper.EditorSaveStoreFactoryForTests != null)
                throw new InvalidOperationException("Another Editor save-store factory is installed; the diagnostic opener will not replace it.");

            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                BellatorClassId, ExpeditionRunConfiguration.HumanRaceId, BellatorSkills);
            F10V7DiagnosticCatalog catalog = F10V7DiagnosticCatalogBuilder.Build(physical, run);
            string storeDirectory = F10V7DiagnosticOpeningPathPolicy.CreateUniqueStoreDirectory(
                projectRoot, Application.persistentDataPath);
            var store = new FileSaveStore(storeDirectory, null);

            GameObject owner = null;
            ApplicationBootstrapper app = null;
            bool keepAppOwner = false;
            try
            {
                F10V7DiagnosticBootstrapResult bootstrap = await ApplicationBootstrapper.BootstrapF10V7DiagnosticAsync(
                    store, storeDirectory);
                app = bootstrap?.App;
                owner = bootstrap?.Owner;
                // Once the App has loaded the isolated V6 store it must remain alive and fenced
                // even if any later write, primary readback or V7 admission is rejected.
                keepAppOwner = app != null;
                if (bootstrap == null || !bootstrap.ReadyForV7Opening)
                    throw new InvalidOperationException("App-owned V6 bootstrap rejected the diagnostic store: " +
                        (bootstrap == null ? "no result" : bootstrap.Message));

                ExpeditionSaveData versionSixShape = ExpeditionSaveMapper.Create(run);
                ExpeditionSaveDataV7 state = SaveV7CandidateMapper.ToV7(versionSixShape);
                if (state == null) throw new InvalidDataException("The new F10 run did not convert to a V7 expedition DTO.");
                state.contentVersion = 7;
                // ExpeditionSaveMapper.Create emits its legacy typed-shape JSON. Rebuild this new
                // run's checkpoint through Prepare so its V7 encounter entry is an authenticated,
                // typed EncounterPlanPinV1 reference.
                state.roomEntryCheckpointJson = string.Empty;
                ProfileSaveDataV7 profile = SaveV7CandidateMapper.ToV7(ProfileSaveData.CreateNewProfile());
                PinnedProfileCandidateV7 candidate = SaveV7CandidateMapper.Prepare(profile, state, catalog.Pin);
                ValidateNewOpeningCandidate(candidate, run, catalog.Pin);

                // The writer wrapper and later verifier wrapper share the single isolated store.
                // The App's own repository remains unlatched so SafeOpen can mint its real handle.
                var writerRepository = new SaveRepository(store);
                SaveWriteResult committed = await writerRepository.CommitPinnedV7ForPreproductionAsync(
                    candidate, run, catalog.Registry);
                if (committed == null || !committed.Succeeded)
                    throw new IOException("The isolated V7 start candidate was not durably committed: " +
                        (committed == null ? "no result" : committed.Message));

                string committedJson = writerRepository.ExportLoadedProfileJson();
                if (string.IsNullOrWhiteSpace(committedJson))
                    throw new IOException("The writer repository did not retain the exact confirmed V7 JSON after commit.");
                VerifiedV7Primary primary = await ReadFreshAuthenticatedPrimaryAsync(writerRepository, store, committedJson,
                    run, catalog, physical, storeDirectory);
                F10TechnicalPhysicalDescriptorExporter.VerifyDisposableWorkspaceForAcceptance();

                F10V7AppAdmissionResult admission;
                try
                {
                    admission = await app.AdmitActiveV7ForPreproductionAsync(
                        run.Inventory.Catalog, catalog.Registry);
                }
                finally
                {
                    // Admission may release its temporary App block on a rejected attempt.
                    // The diagnostic V6 fence is reasserted for every result/exception.
                    app.ReassertF10V7DiagnosticV6WriteFence();
                }
                if (admission == null || !admission.Admitted)
                {
                    throw new InvalidOperationException("The App did not admit the exact authenticated V7 primary: " +
                        (admission == null ? "no result" : admission.Message));
                }
                if (!app.OwnsExactF10V7AdmissionPrimaryJson(primary.PrimaryJson))
                    throw new InvalidDataException("App-private admission context and its retained handle do not match the exact confirmed primary JSON.");
                if (!ReferenceEquals(ApplicationBootstrapper.Instance, app) || app.ActiveRun != null || app.HasActiveExpedition)
                    throw new InvalidOperationException("V7 admission unexpectedly published a gameplay run before composition binding.");

                return new F10V7DiagnosticOpeningReceipt(app, run, catalog, physical, store,
                    storeDirectory, primary.PrimarySha256, primary.PrimaryJson);
            }
            finally
            {
                if (!keepAppOwner && owner != null)
                {
                    owner.SetActive(false);
                    UnityEngine.Object.Destroy(owner);
                }
            }
        }

        /// <summary>
        /// Opens the authenticated V7 run, then hands only its App owner, exact primary bytes,
        /// and opaque verified physical-input receipt to a disabled runner before Start can run.
        /// </summary>
        internal static async Task<F10V7DiagnosticOpeningReceipt> OpenAndStartAsync(
            string physicalRevisionId, string expectedBundleManifestSha256)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("The technical V7 opening runs only in Unity Editor Play Mode inside the verified disposable copy.");

            var runnerOwner = new GameObject("F10 V7 Diagnostic Runner");
            F10DiagnosticSceneRunner runner = runnerOwner.AddComponent<F10DiagnosticSceneRunner>();
            runner.enabled = false;
            bool started = false;
            try
            {
                F10V7DiagnosticOpeningReceipt receipt = await OpenAsync(
                    physicalRevisionId, expectedBundleManifestSha256);
                F10V7SessionStartResult result = await runner.StartAdmittedV7TechnicalSessionAsync(
                    receipt.App, receipt.PrimaryJson, receipt.PhysicalInputs.VerifiedInputs);
                if (result == null || !result.Started)
                    throw new InvalidOperationException("The admitted V7 composition did not start: " +
                        (result == null ? "no result" : result.Message));
                started = true;
                return receipt;
            }
            finally
            {
                if (!started && runnerOwner != null)
                    UnityEngine.Object.Destroy(runnerOwner);
            }
        }

        private static void ValidateNewOpeningCandidate(PinnedProfileCandidateV7 candidate,
            ExpeditionRunConfiguration run, EncounterPlanPinV1 pin)
        {
            if (candidate == null || run == null || pin == null)
                throw new InvalidDataException("A new V7 run, exact pin, and prepared profile candidate are required.");
            ProfileSaveEnvelopeV7 envelope = candidate.CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            if (envelope.contentVersion != 7 || envelope.activeExpedition == null ||
                envelope.activeExpedition.contentVersion != 7 ||
                !string.Equals(envelope.activeExpedition.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                envelope.activeExpedition.encounterPlanPin == null)
                throw new InvalidDataException("The new V7 candidate does not contain the exact active run and catalog pin.");
            if (!SaveV7JsonCodec.TryDecodeCheckpoint(Encoding.UTF8.GetBytes(envelope.activeExpedition.roomEntryCheckpointJson),
                    out ExpeditionRoomCheckpointSaveDataV7 checkpoint, out SaveV7Error checkpointError) ||
                checkpoint == null || checkpoint.encounterEntry == null ||
                !string.Equals(checkpoint.encounterEntry.nodeId, pin.Entry.NodeId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.encounterEntry.resolvedPlanSha256, pin.Entry.ResolvedPlanSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The new V7 candidate is missing its typed Tutorial encounter checkpoint: " +
                    checkpointError.Code + ": " + checkpointError.Message);
        }

        private static async Task<VerifiedV7Primary> ReadFreshAuthenticatedPrimaryAsync(SaveRepository writerRepository,
            FileSaveStore store,
            string exactCommittedJson,
            ExpeditionRunConfiguration run, F10V7DiagnosticCatalog catalog,
            F10V7DiagnosticPhysicalInputs physical, string storeDirectory)
        {
            if (writerRepository == null) throw new ArgumentNullException(nameof(writerRepository));
            if (store == null) throw new ArgumentNullException(nameof(store));
            F10V7DiagnosticOpeningPathPolicy.EnsureContainedAndDisjoint(
                storeDirectory, physical.ProjectRoot, Application.persistentDataPath);
            string primaryPath = store.GetPrimaryPath(SaveDocument.Profile);
            if (!F10V7DiagnosticOpeningPathPolicy.IsPathWithinStore(primaryPath, storeDirectory))
                throw new InvalidDataException("The V7 primary path escaped the unique diagnostic store directory.");

            // Reuse the writer only for this fresh authenticated readback, then never call it
            // again before App.SafeOpen. This keeps the transition serialized across two repos.
            SaveV7LoadView primaryView = await writerRepository.LoadProfileV7ForPreproductionAsync();
            if (primaryView == null || primaryView.Status != SaveLoadStatus.LoadedPrimary ||
                primaryView.SourceContentVersion != 7 || primaryView.Access != SaveDataAccess.ReadWrite ||
                string.IsNullOrWhiteSpace(primaryView.OriginalJson) ||
                !ApplicationBootstrapper.IsExactF10V7PrimarySnapshot(exactCommittedJson, primaryView.OriginalJson) ||
                !ApplicationBootstrapper.IsExactF10V7PrimarySnapshot(writerRepository.ExportLoadedProfileJson(), primaryView.OriginalJson))
                throw new InvalidDataException("Fresh V7 readback must be the authenticated LoadedPrimary and exactly equal the writer's confirmed JSON.");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(primaryView.OriginalJson);
            if (!SaveV7JsonCodec.TryDecodeProfile(bytes, out ProfileSaveEnvelopeV7 envelope, out SaveV7Error error) ||
                envelope == null || envelope.contentVersion != 7 || envelope.profile == null ||
                envelope.activeExpedition == null || envelope.activeExpedition.contentVersion != 7 ||
                !string.Equals(envelope.activeExpedition.runId, run.RunId.ToString(), StringComparison.Ordinal))
                throw new InvalidDataException("The fresh primary was not an authenticated contentVersion 7 profile for this exact run: " +
                    error.Code + ": " + error.Message);

            if (!SaveV7CandidateMapper.TryRestore(envelope, run.Inventory.Catalog, catalog.Registry,
                    out ExpeditionRunConfiguration restoredRun, out ExpeditionSaveDataV7 restoredState,
                    out ResolvedEncounterPlanV1 entryPlan, out SaveV7Error restoreError) ||
                restoredRun == null || restoredState == null || entryPlan == null ||
                restoredRun.RunId != run.RunId || restoredState.encounterPlanPin == null ||
                !string.Equals(restoredState.encounterPlanPin.catalogSha256, catalog.Pin.CatalogSha256, StringComparison.Ordinal) ||
                !string.Equals(entryPlan.CatalogDigest, catalog.ManifestSha256, StringComparison.Ordinal) ||
                !string.Equals(entryPlan.NodeId, run.Route.MandatoryNodeIds[0], StringComparison.Ordinal))
                throw new InvalidDataException("The authenticated primary did not restore the exact pinned Tutorial run: " +
                    restoreError.Code + ": " + restoreError.Message);
            if (!SaveV7CandidateMapper.TryValidateCommit(envelope, run, catalog.Registry, out SaveV7Error pinError))
                throw new InvalidDataException("The primary's entire pinned F10 route did not restore: " +
                    pinError.Code + ": " + pinError.Message);

            return new VerifiedV7Primary(primaryView.OriginalJson, Sha256(bytes));
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        private sealed class VerifiedV7Primary
        {
            internal VerifiedV7Primary(string primaryJson, string primarySha256)
            { PrimaryJson = primaryJson; PrimarySha256 = primarySha256; }
            internal string PrimaryJson { get; }
            internal string PrimarySha256 { get; }
        }
    }

    /// <summary>
    /// Exact read-only provenance/assets for the future coordinator-to-composition seam.
    /// The App's repository-retained handle remains the authority for session mutations.
    /// </summary>
    internal sealed class F10V7DiagnosticOpeningReceipt
    {
        internal F10V7DiagnosticOpeningReceipt(ApplicationBootstrapper app, ExpeditionRunConfiguration run,
            F10V7DiagnosticCatalog catalog, F10V7DiagnosticPhysicalInputs physical, FileSaveStore store,
            string storeDirectory, string primarySha256, string primaryJson)
        {
            App = app; Run = run; Catalog = catalog; PhysicalInputs = physical; Store = store;
            StoreDirectory = storeDirectory; PrimarySha256 = primarySha256; PrimaryJson = primaryJson;
        }
        internal ApplicationBootstrapper App { get; }
        internal ExpeditionRunConfiguration Run { get; }
        internal F10V7DiagnosticCatalog Catalog { get; }
        internal F10V7DiagnosticPhysicalInputs PhysicalInputs { get; }
        internal FileSaveStore Store { get; }
        internal string StoreDirectory { get; }
        internal string PrimarySha256 { get; }
        internal string PrimaryJson { get; }
        internal string RunId => Run.RunId.ToString();
        internal IReadOnlyList<F10V7DiagnosticAssetReference> ExactAssets => PhysicalInputs.Assets;
        internal IReadOnlyList<F10V7DiagnosticAssetReference> ExactSourceAssets => PhysicalInputs.SourceAssets;
        internal IReadOnlyList<F10V7DiagnosticAssetReference> ExactPrefabAssets => PhysicalInputs.PrefabAssets;
        internal string BundleRevisionId => PhysicalInputs.RevisionId;
        internal string BundleManifestSha256 => PhysicalInputs.BundleManifestSha256;
    }
}
#endif
