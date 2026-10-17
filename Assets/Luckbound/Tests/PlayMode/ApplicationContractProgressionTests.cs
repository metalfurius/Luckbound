using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ApplicationContractProgressionTests
    {
        [UnityTest]
        public IEnumerator E10_ProfileEventsBeforeAcceptanceRespectTheThreeContractLimit()
        {
            string directory = CreateSaveDirectory();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => new FileSaveStore(directory, null);
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);

                for (int index = 0; index < 2; index++)
                {
                    ContractProgressionCommitResult progress = null;
                    yield return Complete(app.RecordContractProgressEventAsync(ContractProgressEvent.CombatRoomCompleted),
                        value => progress = value);
                    Assert.That(progress.Succeeded, Is.True, progress.Message);
                    Assert.That(progress.Snapshot.GetProgress("CT01"), Is.EqualTo(index + 1));
                }

                ContractProgressionCommitResult accepted = null;
                yield return Complete(app.AcceptContractAsync("CT01"), value => accepted = value);
                Assert.That(accepted.AcceptStatus, Is.EqualTo(ContractAcceptStatus.Accepted));
                Assert.That(accepted.Succeeded, Is.True, accepted.Message);
                Assert.That(accepted.Snapshot.GetProgress("CT01"), Is.EqualTo(2),
                    "Milestones recorded before acceptance must remain available to the contract.");

                ContractProgressionCommitResult finalRoom = null;
                yield return Complete(app.RecordContractProgressEventAsync(ContractProgressEvent.CombatRoomCompleted),
                    value => finalRoom = value);
                Assert.That(finalRoom.Snapshot.GetProgress("CT01"), Is.EqualTo(3));
                Assert.That(finalRoom.Snapshot.IsComplete("CT01"), Is.True);

                ContractProgressionCommitResult second = null;
                ContractProgressionCommitResult third = null;
                ContractProgressionCommitResult fourth = null;
                yield return Complete(app.AcceptContractAsync("CT02"), value => second = value);
                yield return Complete(app.AcceptContractAsync("CT03"), value => third = value);
                yield return Complete(app.AcceptContractAsync("CT04"), value => fourth = value);
                Assert.That(second.Succeeded, Is.True, second.Message);
                Assert.That(third.Succeeded, Is.True, third.Message);
                Assert.That(fourth.AcceptStatus, Is.EqualTo(ContractAcceptStatus.RejectedActiveLimit));
                Assert.That(fourth.Succeeded, Is.False);
                Assert.That(app.ContractProgression.ActiveContractIds.Count, Is.EqualTo(3));
                Assert.That(app.Profile.acceptedContractIds, Is.EquivalentTo(new[] { "CT01", "CT02", "CT03" }));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                DeleteSaveDirectory(directory);
            }
        }

        [UnityTest]
        public IEnumerator E10_ClaimIsIdempotentAndUnlocksSurviveDeathAndRestart()
        {
            string directory = CreateSaveDirectory();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => new FileSaveStore(directory, null);
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return LoadHub();

                for (int index = 0; index < 3; index++)
                {
                    ContractProgressionCommitResult progress = null;
                    yield return Complete(app.RecordContractProgressEventAsync(ContractProgressEvent.ItemCrafted),
                        value => progress = value);
                    Assert.That(progress.Succeeded, Is.True, progress.Message);
                }

                ContractProgressionCommitResult accepted = null;
                yield return Complete(app.AcceptContractAsync("CT04"), value => accepted = value);
                Assert.That(accepted.Succeeded, Is.True, accepted.Message);
                Assert.That(accepted.Snapshot.GetProgress("CT04"), Is.EqualTo(3));

                ContractProgressionCommitResult claimed = null;
                yield return Complete(app.ClaimContractRewardAsync("CT04"), value => claimed = value);
                Assert.That(claimed.ClaimStatus, Is.EqualTo(ContractClaimStatus.Claimed));
                Assert.That(claimed.Succeeded, Is.True, claimed.Message);
                Assert.That(claimed.Reward.Renown, Is.EqualTo(30));
                Assert.That(claimed.Snapshot.AvailableRenown, Is.EqualTo(30));
                Assert.That(claimed.Snapshot.UnlockedRaceIds.Count(id => id == "race.dvergar"), Is.EqualTo(1));
                Assert.That(claimed.Snapshot.UnlockedStartingKitIds.Count(id => id == "armor.bellator.guild_cuirass"), Is.EqualTo(1));

                ContractProgressionCommitResult repeatedClaim = null;
                yield return Complete(app.ClaimContractRewardAsync("CT04"), value => repeatedClaim = value);
                Assert.That(repeatedClaim.ClaimStatus, Is.EqualTo(ContractClaimStatus.AlreadyClaimed));
                Assert.That(repeatedClaim.Succeeded, Is.True);
                Assert.That(repeatedClaim.Reward, Is.Null);
                Assert.That(repeatedClaim.Snapshot.AvailableRenown, Is.EqualTo(30));

                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                ExpeditionSettlementCommitResult death = null;
                yield return Complete(app.SettleActiveRunAsync(ExpeditionOutcome.Death, 12), value => death = value);
                Assert.That(death.Persisted, Is.True, death.Message);
                Assert.That(death.Receipt.RenownAwarded, Is.EqualTo(3));
                Assert.That(app.Profile.availableRenown, Is.EqualTo(33));
                Assert.That(app.Profile.unlockedRaceIds, Does.Contain("race.dvergar"));
                Assert.That(app.Profile.unlockedStartingKitIds, Does.Contain("armor.bellator.guild_cuirass"));

                DestroyApplication();
                yield return null;
                yield return StartApplication(value => app = value);
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ActiveRun, Is.Null, "A settled death must not restore the expedition.");
                Assert.That(app.ContractProgression.IsClaimed("CT04"), Is.True);
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(3));
                Assert.That(app.ContractProgression.AvailableRenown, Is.EqualTo(33));
                Assert.That(app.ContractProgression.UnlockedRaceIds, Does.Contain("race.dvergar"));
                Assert.That(app.ContractProgression.UnlockedStartingKitIds,
                    Does.Contain("armor.bellator.guild_cuirass"));

                yield return LoadHub();
                ContractProgressionCommitResult repeatedAfterRestart = null;
                yield return Complete(app.ClaimContractRewardAsync("CT04"), value => repeatedAfterRestart = value);
                Assert.That(repeatedAfterRestart.ClaimStatus, Is.EqualTo(ContractClaimStatus.AlreadyClaimed));
                Assert.That(repeatedAfterRestart.Reward, Is.Null);
                Assert.That(repeatedAfterRestart.Snapshot.AvailableRenown, Is.EqualTo(33));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                DeleteSaveDirectory(directory);
            }
        }

        [UnityTest]
        public IEnumerator E10_FailedProfileWriteKeepsPublishedStateAndRetriesTheSamePayload()
        {
            string directory = CreateSaveDirectory();
            var store = new FailOnceProfileWriteStore(new FileSaveStore(directory, null));
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                store.FailNextProfileWrite();

                ContractProgressionCommitResult failed = null;
                yield return Complete(app.AcceptContractAsync("CT01"), value => failed = value);
                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True);
                Assert.That(app.HasPendingContractProgression, Is.True);
                Assert.That(app.CanStartNewExpedition, Is.False);
                Assert.That(app.Profile.acceptedContractIds, Does.Not.Contain("CT01"));
                Assert.That(app.ContractProgression.IsAccepted("CT01"), Is.False);
                Assert.That(app.PendingProfileExport, Is.EqualTo(store.FailedProfileJson));

                ContractProgressionCommitResult blocked = null;
                yield return Complete(app.AcceptContractAsync("CT02"), value => blocked = value);
                Assert.That(blocked.Persisted, Is.False);
                Assert.That(blocked.Pending, Is.True);
                Assert.That(app.ContractProgression.IsAccepted("CT02"), Is.False);

                ContractProgressionCommitResult retried = null;
                yield return Complete(app.RetryPendingContractProgressionAsync(), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.AcceptStatus, Is.EqualTo(ContractAcceptStatus.Accepted));
                Assert.That(app.HasPendingContractProgression, Is.False);
                Assert.That(store.FailedProfileJson, Is.EqualTo(store.LastSuccessfulProfileJson),
                    "The repository retry must write the exact payload that previously failed.");
                Assert.That(app.Profile.acceptedContractIds, Does.Contain("CT01"));
                Assert.That(app.ContractProgression.IsAccepted("CT01"), Is.True);

                DestroyApplication();
                yield return null;
                yield return StartApplication(value => app = value);
                Assert.That(app.ContractProgression.IsAccepted("CT01"), Is.True);
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                DeleteSaveDirectory(directory);
            }
        }

        private static string CreateSaveDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "LuckboundE10", Guid.NewGuid().ToString("N"));
        }

        private static IEnumerator StartApplication(Action<ApplicationBootstrapper> ready)
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                yield return null;

            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            ready(ApplicationBootstrapper.Instance);
        }

        private static IEnumerator LoadHub()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            for (int frame = 0; frame < 60 && SceneManager.GetActiveScene().name != "Hub"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static void DestroyApplication()
        {
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
        }

        private static void DeleteSaveDirectory(string directory)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
            if (Directory.Exists(fullDirectory)) Directory.Delete(fullDirectory, recursive: true);
        }

        private sealed class FailOnceProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _failNextProfileWrite;

            public FailOnceProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public string FailedProfileJson { get; private set; }
            public string LastSuccessfulProfileJson { get; private set; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    if (_failNextProfileWrite)
                    {
                        _failNextProfileWrite = false;
                        FailedProfileJson = json;
                        throw new IOException("simulated profile storage failure");
                    }
                    LastSuccessfulProfileJson = json;
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
