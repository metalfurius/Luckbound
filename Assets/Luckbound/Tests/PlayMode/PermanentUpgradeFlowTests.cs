using System;
using System.IO;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PermanentUpgradeFlowTests
    {
        [UnityTest]
        public System.Collections.IEnumerator E06_TavernPurchaseIsPersistedAndAppliedOnlyToTheNextExpedition()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE06", Guid.NewGuid().ToString("N"));
            var backingStore = new FileSaveStore(saveDirectory, null);
            var faultStore = new FailOnceProfileWriteStore(backingStore);
            var evidence = new ScenarioEvidenceRecorder("E06", "E06_PermanentUpgrades", 618033);
            try
            {
                ProfileSaveData seededProfile = ProfileSaveData.CreateNewProfile();
                seededProfile.availableRenown = 510;
                Task<SaveWriteResult> seed = new SaveRepository(backingStore).SaveProfileAsync(seededProfile);
                while (!seed.IsCompleted) yield return null;
                Assert.That(seed.Result.Succeeded, Is.True, seed.Result.Message);

                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => faultStore;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                evidence.RecordInput(0, "LoadScene", "Bootstrap with seeded profile and isolated storage");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++) yield return null;
                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                evidence.RecordAssertion("Profile loads with the prepared Renown balance", app != null && app.CanStartNewExpedition &&
                    app.Profile.availableRenown == 510, "510 Renown and no active expedition",
                    app == null ? "missing app" : "balance=" + app.Profile.availableRenown + "; " + app.ProfileLoadStatus);
                Assert.That(app != null && app.CanStartNewExpedition, Is.True);

                evidence.RecordInput(1, "LoadScene", "Enter Hub where purchases are permitted");
                SceneManager.LoadScene("Hub", LoadSceneMode.Single);
                for (int frame = 0; frame < 60 && (SceneManager.GetActiveScene().name != "Hub" ||
                    UnityEngine.Object.FindAnyObjectByType<HubPreparationController>() == null); frame++) yield return null;
                Button openUpgrades = FindButton("OpenPermanentUpgradesButton");
                Button healthUpgrade = FindButton("PermanentHealthUpgradeButton");
                evidence.RecordAssertion("Tavern exposes the permanent-upgrade panel and health purchase", openUpgrades != null && healthUpgrade != null,
                    "OpenPermanentUpgradesButton and PermanentHealthUpgradeButton", openUpgrades == null || healthUpgrade == null ? "missing" : "present");
                Assert.That(openUpgrades != null && healthUpgrade != null, Is.True);
                evidence.RecordInput(2, "UIButton", "OpenPermanentUpgradesButton.onClick");
                openUpgrades.onClick.Invoke();
                evidence.RecordInput(3, "UIButton", "PermanentHealthUpgradeButton.onClick; storage fails once");
                faultStore.FailNextProfileWrite();
                healthUpgrade.onClick.Invoke();
                for (int frame = 0; frame < 60 && !app.HasPendingPermanentUpgradePurchase; frame++) yield return null;
                bool failedWriteDidNotCharge = app.HasPendingPermanentUpgradePurchase && app.Profile.availableRenown == 510 &&
                    app.Profile.healthUpgradeLevel == 0 && app.PendingPermanentUpgradeKind == PermanentUpgradeKind.MaximumHealth;
                evidence.RecordAssertion("A failed durable write leaves the live profile uncharged", failedWriteDidNotCharge,
                    "510 Renown, health level 0, purchase pending", "balance=" + app.Profile.availableRenown +
                    "; health=" + app.Profile.healthUpgradeLevel + "; pending=" + app.HasPendingPermanentUpgradePurchase);
                Assert.That(failedWriteDidNotCharge, Is.True);

                evidence.RecordInput(4, "UIButton", "Repeat the same health button to retry its pending transaction");
                healthUpgrade.onClick.Invoke();
                for (int frame = 0; frame < 120 && app.HasPendingPermanentUpgradePurchase; frame++) yield return null;
                bool retryCommitted = !app.HasPendingPermanentUpgradePurchase && app.Profile.availableRenown == 450 &&
                    app.Profile.healthUpgradeLevel == 1 && app.Profile.progressionPurchaseTransactions.Length == 1;
                evidence.RecordAssertion("Retry commits one receipt and deducts the exact 60 Renown once", retryCommitted,
                    "450 Renown, health level 1, one persisted transaction", "balance=" + app.Profile.availableRenown +
                    "; health=" + app.Profile.healthUpgradeLevel + "; receipts=" + app.Profile.progressionPurchaseTransactions.Length);
                Assert.That(retryCommitted, Is.True);
                string committedTransactionId = app.Profile.progressionPurchaseTransactions[0].transactionId;

                evidence.RecordInput(5, "ProcessRestart", "Reload Bootstrap using the same durable profile");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++) yield return null;
                app = ApplicationBootstrapper.Instance;
                Assert.That(app != null && app.SaveDataReady, Is.True);
                SceneManager.LoadScene("Hub", LoadSceneMode.Single);
                for (int frame = 0; frame < 60 && SceneManager.GetActiveScene().name != "Hub"; frame++) yield return null;
                Task<ProgressionPurchaseCommitResult> replay = app.PurchasePermanentUpgradeAsync(
                    PermanentUpgradeKind.MaximumHealth, TransactionId.Parse(committedTransactionId));
                while (!replay.IsCompleted) yield return null;
                bool duplicateWasIdempotent = replay.Result.Persisted && replay.Result.Purchase.Status ==
                    PermanentUpgradePurchaseStatus.AlreadyProcessed && app.Profile.availableRenown == 450 &&
                    app.Profile.healthUpgradeLevel == 1 && app.Profile.progressionPurchaseTransactions.Length == 1;
                evidence.RecordAssertion("A replay after process restart returns the saved receipt without charging again",
                    duplicateWasIdempotent, "450 Renown and one health level", "balance=" + app.Profile.availableRenown +
                    "; health=" + app.Profile.healthUpgradeLevel + "; receipts=" + app.Profile.progressionPurchaseTransactions.Length);
                Assert.That(duplicateWasIdempotent, Is.True, replay.Result.Message);

                PermanentUpgradeKind[] remainingPurchases =
                {
                    PermanentUpgradeKind.MaximumHealth,
                    PermanentUpgradeKind.MaximumEnergy,
                    PermanentUpgradeKind.MaximumEnergy,
                    PermanentUpgradeKind.AdditionalHealthPotion
                };
                int purchaseIndex = 6;
                foreach (PermanentUpgradeKind kind in remainingPurchases)
                {
                    evidence.RecordInput(purchaseIndex++, "Simulated purchase intent", kind.ToString());
                    Task<ProgressionPurchaseCommitResult> purchase = app.PurchasePermanentUpgradeAsync(kind, TransactionId.New());
                    while (!purchase.IsCompleted) yield return null;
                    Assert.That(purchase.Result.Persisted, Is.True, purchase.Result.Message);
                }
                ProfileSaveData profile = app.Profile;
                evidence.RecordAssertion("All permanent upgrades spend exactly 510 Renown and reach their limits",
                    profile.availableRenown == 0 && profile.healthUpgradeLevel == 2 && profile.energyUpgradeLevel == 2 &&
                    profile.additionalPotionUpgradeLevel == 1 && profile.progressionPurchaseTransactions.Length == 5,
                    "balance 0; health 2; energy 2; potion 1; five receipts",
                    "balance=" + profile.availableRenown + "; health=" + profile.healthUpgradeLevel +
                    "; energy=" + profile.energyUpgradeLevel + "; potion=" + profile.additionalPotionUpgradeLevel);
                Assert.That(profile.availableRenown, Is.Zero);

                evidence.RecordInput(purchaseIndex, "New expedition", "Create a fresh Bellator run from the purchased profile");
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" },
                    profile.healthUpgradeLevel, profile.energyUpgradeLevel, profile.additionalPotionUpgradeLevel);
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                bool nextRunHasUpgrades = begin.Result.Succeeded && run.StartingHealth == 110 && run.StartingEnergy == 55 &&
                    run.StartingHealthPotions == 3 && app.ActiveExpedition.maximumHealth == 110 &&
                    app.ActiveExpedition.maximumEnergy == 55 && app.ActiveExpedition.healthPotionCharges == 3;
                evidence.RecordAssertion("Only the next new expedition receives the purchased health, energy and potion bonuses",
                    nextRunHasUpgrades, "110 health, 55 energy and 3 starting potions", begin.Result.Message);
                Assert.That(nextRunHasUpgrades, Is.True);

                Task<ProgressionPurchaseCommitResult> blockedDuringRun = app.PurchasePermanentUpgradeAsync(
                    PermanentUpgradeKind.MaximumHealth, TransactionId.New());
                while (!blockedDuringRun.IsCompleted) yield return null;
                bool noMidRunPurchase = !blockedDuringRun.Result.Persisted && blockedDuringRun.Result.Purchase == null &&
                    app.Profile.availableRenown == 0 && app.Profile.healthUpgradeLevel == 2 && app.ActiveExpedition.maximumHealth == 110;
                evidence.RecordAssertion("Purchases remain unavailable after a run has begun", noMidRunPurchase,
                    "unchanged profile and active run snapshot", blockedDuringRun.Result.Message);
                Assert.That(noMidRunPurchase, Is.True);

                Task<ExpeditionSettlementCommitResult> cleanup = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!cleanup.IsCompleted) yield return null;
                Assert.That(cleanup.Result.Persisted, Is.True, cleanup.Result.Message);
                int failures = evidence.Complete();
                Assert.That(failures, Is.Zero, "E06 permanent upgrade evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindAnyObjectByType<Button>(FindObjectsInactive.Include) is Button button && button.name == name
                ? button
                : FindAllButtons().Find(item => item.name == name);
        }

        private static System.Collections.Generic.List<Button> FindAllButtons()
        {
            return new System.Collections.Generic.List<Button>(UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private sealed class FailOnceProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _failNextProfileWrite;

            public FailOnceProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && _failNextProfileWrite)
                {
                    _failNextProfileWrite = false;
                    throw new IOException("simulated profile storage failure");
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
