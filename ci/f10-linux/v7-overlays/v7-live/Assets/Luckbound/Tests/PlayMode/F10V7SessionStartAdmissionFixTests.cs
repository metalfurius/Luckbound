#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    [Parallelizable(ParallelScope.None)]
    public sealed class F10V7SessionStartAdmissionFixTests
    {
        private GameObject _appRoot;
        private object _previousInstance;
        private readonly System.Collections.Generic.List<GameObject> _compositionRoots =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_appRoot != null)
            {
                ApplicationBootstrapper app = _appRoot.GetComponent<ApplicationBootstrapper>();
                if (ReferenceEquals(ReadStaticField(typeof(ApplicationBootstrapper), "_instance"), app))
                    SetStaticField(typeof(ApplicationBootstrapper), "_instance", _previousInstance);
                UnityEngine.Object.DestroyImmediate(_appRoot);
            }
            foreach (GameObject root in _compositionRoots)
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            _compositionRoots.Clear();
        }

        [Test]
        public async Task InstalledBindingPassesPostInstallCurrentnessAndReachesTheF03Gate()
        {
            ActiveV7Fixture fixture = await CreateActiveV7Fixture();
            var store = new MemoryStore { Primary = fixture.Json };
            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> loaded = await repository.LoadProfileAsync();
            ApplicationBootstrapper app = CreateApp(repository, loaded.Status, revision: 23);
            F10V7AppAdmissionResult admission = await app.AdmitActiveV7ForPreproductionAsync(
                fixture.ItemCatalog, fixture.Registry);
            Assert.That(admission.Admitted, Is.True, admission.Message);

            ExpeditionGameplayComposition composition = CreateComposition();
            F10V7SessionStartResult start = await app.StartAdmittedV7SessionAsync(composition);

            Assert.That(start.Started, Is.False);
            Assert.That(start.Message, Does.Contain("F03 pendiente"),
                "The exact binding must install and pass post-install currentness before the expected missing-prefab gate.");
            Assert.That(ReadField(composition, "_f10V7InitializationBinding"), Is.Null,
                "Verified cleanup must remove the temporary binding.");
            Assert.That(ReadField(app, "_f10V7SessionStartAttempted"), Is.EqualTo(false));
            Assert.That(ReadField(app, "_f10V7SessionStartAttemptOwner"), Is.Null);
            Assert.That(ReadField(app, "_f10V7AdmissionContext"), Is.Not.Null,
                "A failed composition must preserve the authenticated admission context and handle.");
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(app.ActiveExpedition, Is.Null);
            Assert.That(app.ActiveExpeditionRevision, Is.EqualTo(23));
        }

        [Test]
        public async Task VerifiedCleanupAllowsRetryWithFreshCompositionAndSameAdmission()
        {
            ActiveV7Fixture fixture = await CreateActiveV7Fixture();
            var store = new MemoryStore { Primary = fixture.Json };
            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> loaded = await repository.LoadProfileAsync();
            ApplicationBootstrapper app = CreateApp(repository, loaded.Status, revision: 31);
            F10V7AppAdmissionResult admission = await app.AdmitActiveV7ForPreproductionAsync(
                fixture.ItemCatalog, fixture.Registry);
            Assert.That(admission.Admitted, Is.True, admission.Message);
            object originalContext = ReadField(app, "_f10V7AdmissionContext");

            ExpeditionGameplayComposition first = CreateComposition();
            F10V7SessionStartResult firstStart = await app.StartAdmittedV7SessionAsync(first);
            Assert.That(firstStart.Started, Is.False);
            Assert.That(firstStart.Message, Does.Contain("F03 pendiente"));
            Assert.That(ReadField(app, "_f10V7SessionStartAttempted"), Is.EqualTo(false));
            Assert.That(ReadField(app, "_f10V7AdmissionContext"), Is.SameAs(originalContext));
            Assert.That(app.ActiveExpeditionRevision, Is.EqualTo(31));

            ExpeditionGameplayComposition retry = CreateComposition();
            F10V7SessionStartResult retryStart = await app.StartAdmittedV7SessionAsync(retry);

            Assert.That(retryStart.Started, Is.False);
            Assert.That(retryStart.Message, Does.Contain("F03 pendiente"),
                "The second fresh composition must reach the setup gate, not be rejected by a stuck attempt latch.");
            Assert.That(ReadField(app, "_f10V7SessionStartAttempted"), Is.EqualTo(false));
            Assert.That(ReadField(app, "_f10V7AdmissionContext"), Is.SameAs(originalContext));
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(app.ActiveExpedition, Is.Null);
            Assert.That(app.ActiveExpeditionRevision, Is.EqualTo(31));
        }

        [Test]
        public void CleanupDeactivatesActorBeforeUnityDeferredDestroy()
        {
            ExpeditionGameplayComposition composition = CreateComposition();
            var actor = new GameObject("V7 rollback actor test");
            actor.transform.SetParent(composition.transform, worldPositionStays: false);
            SetField(composition, "_playerObject", actor);
            var lease = new InspectingLease(() => Assert.That(actor.activeInHierarchy, Is.False,
                "The actor must be inactive before synchronous pause-release listeners run."));
            SetField(composition, "_f10V7InitializationPauseLease", lease);

            MethodInfo cleanup = typeof(ExpeditionGameplayComposition).GetMethod("CleanupRuntime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cleanup, Is.Not.Null);
            cleanup.Invoke(composition, null);

            Assert.That(actor.activeSelf, Is.False);
            Assert.That(actor.activeInHierarchy, Is.False);
            Assert.That(lease.Disposed, Is.True);
            Assert.That(ReadField(composition, "_f10V7CleanupPlayerInactiveVerified"), Is.EqualTo(true));
        }

        [Test]
        public async Task V7RunnerEntryWithoutOpaqueReceiptFailsClosedBeforeV6Start()
        {
            // Partial guard coverage only: this fixture deliberately has no certified physical
            // receipt, so it does not claim the integrated Tutorial/C1/E1 gameplay path.
            var root = new GameObject("F10 V7 runner missing-receipt guard test");
            _compositionRoots.Add(root);
            F10DiagnosticSceneRunner runner = root.AddComponent<F10DiagnosticSceneRunner>();
            runner.enabled = false;
            ApplicationBootstrapper before = ApplicationBootstrapper.Instance;

            F10V7SessionStartResult result = await runner.StartAdmittedV7TechnicalSessionAsync(
                application: null, exactPrimaryJson: null, verifiedInputs: null);

            Assert.That(result.Started, Is.False);
            Assert.That(result.Message, Does.Contain("exact App-admitted primary JSON"));
            Assert.That(runner.enabled, Is.False);
            Assert.That(ReadField(runner, "_v7EntryAttempted"), Is.EqualTo(true));
            Assert.That(ApplicationBootstrapper.Instance, Is.SameAs(before));
            Assert.That(runner.Composition, Is.Null,
                "This guard-only test has no certified physical receipt and must not compose V6 or V7 gameplay.");

            ApplicationBootstrapper app = CreateApp(new SaveRepository(new MemoryStore()),
                SaveLoadStatus.Missing, revision: 2);
            ExpeditionGameplayComposition composition = CreateComposition();
            F10V7SessionStartResult startWithoutReceipt = await app.StartAdmittedV7TechnicalSessionAsync(composition);

            Assert.That(startWithoutReceipt.Started, Is.False);
            Assert.That(startWithoutReceipt.Message, Does.Contain("opaque verified physical-input receipt overload"));
            Assert.That(ReadField(composition, "_f10V7InitializationBinding"), Is.Null);
            Assert.That(ReadField(composition, "_attempted"), Is.EqualTo(false));
            Assert.That(ReadField(app, "_f10V7SessionStartAttempted"), Is.EqualTo(false));
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(app.ActiveExpedition, Is.Null);
        }

        private ApplicationBootstrapper CreateApp(SaveRepository repository, SaveLoadStatus status, long revision)
        {
            _appRoot = new GameObject("F10 V7 start admission fix test App");
            _appRoot.SetActive(false);
            ApplicationBootstrapper app = _appRoot.AddComponent<ApplicationBootstrapper>();
            _previousInstance = ReadStaticField(typeof(ApplicationBootstrapper), "_instance");
            SetStaticField(typeof(ApplicationBootstrapper), "_instance", app);
            SetField(app, "_saveRepository", repository);
            SetField(app, "_activeExpeditionRevision", revision);
            SetProperty(app, "SaveDataReady", true);
            SetProperty(app, "ProfileLoadStatus", status);
            return app;
        }

        private ExpeditionGameplayComposition CreateComposition()
        {
            var root = new GameObject("F10 V7 start admission fix test composition");
            _compositionRoots.Add(root);
            return root.AddComponent<ExpeditionGameplayComposition>();
        }

        private static async Task<ActiveV7Fixture> CreateActiveV7Fixture()
        {
            ExpeditionRunConfiguration run = InvokeFixture<ExpeditionRunConfiguration>("CreateRun", 0x61f09u);
            EncounterCatalogManifest manifest = InvokeFixture<EncounterCatalogManifest>(
                "CreateManifest", 1, "variant.r1", false, false);
            EncounterCatalogRegistryV1 registry = InvokeFixture<EncounterCatalogRegistryV1>(
                "Registry", (object)new[] { manifest });
            PinnedProfileCandidateV7 candidate = InvokeFixture<PinnedProfileCandidateV7>("Initial", run, manifest);
            var seedStore = new MemoryStore();
            var seedRepository = new SaveRepository(seedStore);
            SaveWriteResult seeded = await seedRepository.CommitPinnedV7ForPreproductionAsync(candidate, run, registry);
            Assert.That(seeded.Succeeded, Is.True, seeded.Message);
            Assert.That(seedStore.Primary, Is.Not.Null.And.Not.Empty);
            return new ActiveV7Fixture(run.Inventory.Catalog, registry, seedStore.Primary);
        }

        private static T InvokeFixture<T>(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(SaveV7AtomicRepositoryTests).GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "V7 fixture method is missing: " + methodName);
            return (T)method.Invoke(null, arguments);
        }

        private static object ReadField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object ReadStaticField(Type type, string name) =>
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static void SetStaticField(Type type, string name, object value) =>
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
                .GetSetMethod(nonPublic: true).Invoke(target, new[] { value });

        private sealed class InspectingLease : IDisposable
        {
            private readonly Action _onDispose;
            internal InspectingLease(Action onDispose) => _onDispose = onDispose;
            internal bool Disposed { get; private set; }
            public void Dispose() { Disposed = true; _onDispose(); }
        }

        private sealed class ActiveV7Fixture
        {
            internal ActiveV7Fixture(ItemCatalog itemCatalog, EncounterCatalogRegistryV1 registry, string json)
            {
                ItemCatalog = itemCatalog;
                Registry = registry;
                Json = json;
            }

            internal ItemCatalog ItemCatalog { get; }
            internal EncounterCatalogRegistryV1 Registry { get; }
            internal string Json { get; }
        }

        private sealed class MemoryStore : ISaveStore
        {
            internal string Primary;
            internal string Backup;

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) =>
                Task.FromResult(new SaveStorageSnapshot(Primary, Backup));

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (lastKnownGoodPrimaryJson != null) Backup = lastKnownGoodPrimaryJson;
                Primary = json;
                return Task.CompletedTask;
            }
        }
    }
}
#endif
