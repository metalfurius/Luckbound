using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;
using Luckbound.App;
using Luckbound.Core.Saves;

namespace Luckbound.Tests.PlayMode
{
    public sealed class StartupFlowTests
    {
        private const int StartupSeed = 31415926;

        [UnityTest]
        public IEnumerator BootstrapLoadsTheVisibleMenuAndWritesEvidence()
        {
            UnityRandom.State previousRandomState = UnityRandom.state;
            System.Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            string isolatedSaveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundA08", Guid.NewGuid().ToString("N"));
            var isolatedStore = new FileSaveStore(isolatedSaveDirectory, null);
            var evidence = new ScenarioEvidenceRecorder("A08", "startup-menu", StartupSeed);

            try
            {
                UnityRandom.InitState(StartupSeed);
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => isolatedStore;
                evidence.RecordEvent("Starting the application from the Bootstrap scene.");
                evidence.RecordInput(0, "LoadScene", "Bootstrap");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);

                for (int frame = 0; frame < 10 && SceneManager.GetActiveScene().name != "Menu"; frame++)
                {
                    yield return null;
                }

                string activeScene = SceneManager.GetActiveScene().name;
                evidence.RecordAssertion("Bootstrap routes to Menu", activeScene == "Menu", "Menu", activeScene);

                GameObject titleObject = GameObject.Find("LuckboundTitle");
                evidence.RecordAssertion("Menu title exists", titleObject != null, "LuckboundTitle", Describe(titleObject));

                GameObject versionObject = GameObject.Find("MenuVersion");
                Text version = versionObject == null ? null : versionObject.GetComponent<Text>();
                string versionValue = version == null ? "missing" : version.text;
                evidence.RecordAssertion(
                    "Menu version is visible",
                    versionValue == "PREPRODUCCIÓN  ·  0.1.0",
                    "PREPRODUCCIÓN  ·  0.1.0",
                    versionValue);

                yield return CaptureScreenshot(evidence);

                int exitCode = evidence.Complete();
                Assert.That(exitCode, Is.Zero, "The startup scenario failed. Evidence: " + evidence.ReportPath);
                Assert.That(File.Exists(evidence.ReportPath), Is.True);
                Assert.That(File.Exists(evidence.LogPath), Is.True);
            }
            finally
            {
                evidence.Dispose();
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                if (Directory.Exists(isolatedSaveDirectory)) Directory.Delete(isolatedSaveDirectory, recursive: true);
                UnityRandom.state = previousRandomState;
            }
        }

        [UnityTest]
        public IEnumerator FailureProbeReturnsOneAndPreservesEvidence()
        {
            const int seed = 27182818;
            var evidence = new ScenarioEvidenceRecorder("A08", "failure-evidence-probe", seed);

            try
            {
                UnityRandom.InitState(seed);
                evidence.RecordEvent("This fixture intentionally asks for an object that is not in the menu.");
                evidence.RecordInput(0, "LoadScene", "Menu");
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
                yield return null;

                GameObject missingObject = GameObject.Find("A08_ForcedMissingScenarioObject");
                evidence.RecordAssertion(
                    "Deliberately missing fixture object",
                    missingObject != null,
                    "A08_ForcedMissingScenarioObject exists",
                    missingObject == null ? "absent" : "present");

                yield return CaptureScreenshot(evidence);

                int exitCode = evidence.Complete();
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(File.Exists(evidence.ReportPath), Is.True, "Failed scenario report was not preserved.");
                Assert.That(File.Exists(evidence.LogPath), Is.True, "Failed scenario log was not preserved.");

                ScenarioEvidenceReport report = JsonUtility.FromJson<ScenarioEvidenceReport>(
                    File.ReadAllText(evidence.ReportPath));
                Assert.That(report.passed, Is.False);
                Assert.That(report.errors, Is.Not.Empty);
                Assert.That(report.seed, Is.EqualTo(seed));
                Assert.That(report.sourceHash, Is.Not.Empty);
                Assert.That(report.sourceHashVersion, Is.EqualTo(2));
                Assert.That(report.sourceFileCount, Is.GreaterThan(0));
                Assert.That(report.sourceSnapshotCapturedUtc, Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(evidence.EvidenceDirectory, "source-manifest.sha256")), Is.True);
                using (SHA256 sha256 = SHA256.Create())
                {
                    string manifestHash = BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(
                        Path.Combine(evidence.EvidenceDirectory, "source-manifest.sha256"))))
                        .Replace("-", string.Empty).ToLowerInvariant();
                    Assert.That(report.sourceHash, Is.EqualTo(manifestHash));
                }
                Assert.That(File.ReadAllText(evidence.LogPath), Does.Contain("Deliberately missing fixture object"));

                if (string.Equals(Environment.GetEnvironmentVariable("LUCKBOUND_SCENARIO_FAIL_PROBE"), "1", StringComparison.Ordinal))
                {
                    Assert.Fail("A08 deliberate failure probe. Evidence was written to " + evidence.EvidenceDirectory);
                }
            }
            finally
            {
                evidence.Dispose();
            }
        }

        private static IEnumerator CaptureScreenshot(ScenarioEvidenceRecorder evidence)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                evidence.RecordScreenshotStatus(
                    "Unavailable: screenshot capture requires an interactive Unity session; batch/headless tests still retain logs and reports.");
                yield break;
            }

            yield return new WaitForEndOfFrame();

            string captureError = null;
            try
            {
                ScreenCapture.CaptureScreenshot(evidence.ScreenshotCapturePath, 1);
            }
            catch (Exception exception)
            {
                captureError = exception.Message;
            }

            if (captureError != null)
            {
                evidence.RecordScreenshotStatus("Unavailable: " + captureError);
                yield break;
            }

            bool screenshotWritten = false;
            for (int frame = 0; frame < 120; frame++)
            {
                if (File.Exists(evidence.ScreenshotPath) && new FileInfo(evidence.ScreenshotPath).Length > 0)
                {
                    screenshotWritten = true;
                    break;
                }
                yield return null;
            }

            evidence.RecordScreenshotStatus(screenshotWritten
                ? "Captured: " + evidence.ScreenshotPath
                : "Unavailable: capture did not produce a file within 120 frames.");
        }

        private static string Describe(GameObject gameObject)
        {
            return gameObject == null ? "missing" : gameObject.name;
        }
    }
}
