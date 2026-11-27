#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class F10V6LinuxBootstrapEvidenceSetup : IPrebuildSetupWithTestData
    {
        internal const string ProcessASelector = "process-a";
        internal const string ProcessBSelector = "process-b";

        public void Setup(TestData testData)
        {
            if (testData == null || testData.TestMode != TestMode.PlayMode ||
                testData.TestPlatform != RuntimePlatform.LinuxEditor)
                throw new InvalidOperationException("The F10 Linux bootstrap receipt requires exactly the Linux Editor PlayMode test run.");

            ITest[] selectedLeaves = testData.TestList == null
                ? Array.Empty<ITest>()
                : testData.TestList.ToArray();
            if (selectedLeaves.Length != 1 || selectedLeaves[0] == null)
                throw new InvalidOperationException("Select exactly one F10 V6 acceptance leaf for each Linux Editor process.");

            string processA = typeof(F10V6ResumeAcceptanceTests).FullName + "." +
                nameof(F10V6ResumeAcceptanceTests.ProcessA_BeginsNewRunClearsC1AndWritesReadOnlyReceipt);
            string processB = typeof(F10V6ResumeAcceptanceTests).FullName + "." +
                nameof(F10V6ResumeAcceptanceTests.ProcessB_ResumesSameC1CheckpointWithoutRepayAndExtracts);
            string selectedName = selectedLeaves[0].FullName;
            string selector;
            if (string.Equals(selectedName, processA, StringComparison.Ordinal))
                selector = ProcessASelector;
            else if (string.Equals(selectedName, processB, StringComparison.Ordinal))
                selector = ProcessBSelector;
            else
                throw new InvalidOperationException("The F10 Linux bootstrap setup rejects any selected test outside the exact Process A or Process B leaf.");

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            F10DiagnosticLinuxRuntimeEvidenceReader.CaptureCurrentBootstrapReceipt(projectRoot, selector, selectedName);
        }
    }

    /// <summary>
    /// Disposable Editor-only two-process acceptance. The two tests must run in fresh Unity processes
    /// against one verified project copy. All progress comes from virtual keyboard input and real physics.
    /// </summary>
    [PrebuildSetupWithTestData(typeof(F10V6LinuxBootstrapEvidenceSetup))]
    public sealed class F10V6ResumeAcceptanceTests
    {
        private const string ScenePath = "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity";
        private const string TutorialNode = "sample.tutorial";
        private const string C1Node = "sample.patio.combat";
        private const string E1Node = "sample.patio.exploration";
        private const string GuardianNode = "sample.patio.guardian";
        private const string ExtractionNode = "sample.safe_extraction";
        private const int StalledFrameLimit = 600;

        private const string ResumeRootRelative = "Logs/F10V6Resume-20261120";
        private const string SessionManifestRelative = ResumeRootRelative + "/session-manifest.json";
        private const string LinuxRuntimeReceiptRelative = ResumeRootRelative + "/ci-linux-diagnostic.json";
        private const string LinuxMaterializationReceiptRelative = ResumeRootRelative + "/materialization-linux-diagnostic.json";
        private const string TestReceiptRelative = ResumeRootRelative + "/acceptance/process-a-receipt.json";

        [UnityTest]
        public IEnumerator ProcessA_BeginsNewRunClearsC1AndWritesReadOnlyReceipt()
        {
            Assert.That(ApplicationBootstrapper.Instance, Is.Null,
                "Process A must start in a fresh Unity Editor process with no live application.");

            string projectRoot = ProjectRoot;
            LinuxDiagnosticReceipt linuxEvidence = ReadLinuxDiagnosticReceipt(projectRoot);
            AssertLinuxDiagnosticReceipt(linuxEvidence, projectRoot, F10V6LinuxBootstrapEvidenceSetup.ProcessASelector);
            Assert.That(File.Exists(Absolute(projectRoot, SessionManifestRelative)), Is.False,
                "Process A refuses to replace an existing session manifest.");
            Assert.That(File.Exists(Absolute(projectRoot, TestReceiptRelative)), Is.False,
                "Process A refuses to replace prior test evidence.");

            var context = new RouteContext();
            try
            {
                yield return OpenDiagnostic(context, F10DiagnosticSessionMode.BeginNew);
                AssertNode(context, TutorialNode, "Process A startup");

                int initialProfileRenown = context.App.Profile.availableRenown;
                string runId = context.App.ActiveRun.RunId.ToString();
                AssertValidRunId(runId);
                Assert.That(context.App.Profile.processedSettlementRunIds ?? Array.Empty<string>(),
                    Does.Not.Contain(runId), "A new run must not already be settled.");
                Assert.That(context.App.HasPendingSettlement, Is.False);

                var evidence = new C1Evidence
                {
                    RunId = runId,
                    InitialProfileRenown = initialProfileRenown
                };

                yield return CompleteTutorial(context);
                yield return ClearC1Physically(context, evidence);

                AssertNode(context, E1Node, "Process A durable C1 checkpoint");
                Assert.That(context.App.ActiveRun, Is.Not.Null);
                Assert.That(context.App.ActiveExpedition, Is.Not.Null);
                Assert.That(context.App.HasPendingSettlement, Is.False);
                Assert.That(context.App.ActiveExpedition.currentNodeId, Is.EqualTo(E1Node));
                Assert.That(context.App.Profile.availableRenown, Is.EqualTo(initialProfileRenown),
                    "The Profile V6 must retain its original Renown before terminal extraction.");

                ProfileSnapshot saved = ReadV6Profile(context, runId);
                AssertSavedC1Checkpoint(saved, evidence, requireC1CurrentRoute: false);
                SessionManifest manifest = ReadAndValidateManifest(projectRoot, runId);
                var receipt = MakeReceipt(projectRoot, saved, manifest, evidence);
                WriteReadOnlyReceipt(projectRoot, receipt);

                TestContext.WriteLine("Process A receipt: " + TestReceiptRelative +
                    "; RunId=" + runId + "; profile SHA-256=" + receipt.profileSha256 +
                    "; C1 claims=" + receipt.c1ClaimedRewardIds.Length +
                    "; gold=" + receipt.c1GoldBefore + "→" + receipt.c1GoldAfter +
                    "; Renown=" + receipt.c1RenownBefore + "→" + receipt.c1RenownAfter + ".");
            }
            finally
            {
                CloseDiagnostic(context);
            }
        }

        [UnityTest]
        public IEnumerator ProcessB_ResumesSameC1CheckpointWithoutRepayAndExtracts()
        {
            Assert.That(ApplicationBootstrapper.Instance, Is.Null,
                "Process B must start in a new Unity Editor process.");

            string projectRoot = ProjectRoot;
            LinuxDiagnosticReceipt linuxEvidence = ReadLinuxDiagnosticReceipt(projectRoot);
            AssertLinuxDiagnosticReceipt(linuxEvidence, projectRoot, F10V6LinuxBootstrapEvidenceSetup.ProcessBSelector);
            string testReceiptPath = Absolute(projectRoot, TestReceiptRelative);
            Assert.That(File.Exists(testReceiptPath), Is.True,
                "Process B requires Process A's read-only test receipt.");
            Assert.That((File.GetAttributes(testReceiptPath) & FileAttributes.ReadOnly) != 0, Is.True,
                "Process A's evidence receipt must remain read-only.");
            string receiptHashBefore = HashFile(testReceiptPath);
            F10V6ResumeTestReceipt receipt = ReadTestReceipt(testReceiptPath);
            CurrentProcessIdentity(out int processId, out long processStartUtcTicks);
            Assert.That(processId != receipt.processId || processStartUtcTicks != receipt.processStartUtcTicks,
                Is.True, "Process B must run in a different Editor process from Process A.");

            var context = new RouteContext();
            try
            {
                // This selects the runner's explicit phase. The session API itself receives no path or RunId.
                yield return OpenDiagnostic(context, F10DiagnosticSessionMode.ResumeExisting);

                string actualRunId = context.App.ActiveRun.RunId.ToString();
                Assert.That(actualRunId, Is.EqualTo(receipt.runId),
                    "The API-selected session must match Process A's evidence RunId.");
                AssertValidRunId(actualRunId);
                AssertNode(context, E1Node, "Process B resumed startup");

                SessionManifest manifest = ReadAndValidateManifest(projectRoot, actualRunId);
                AssertManifestMatchesReceipt(manifest, receipt);
                string expectedProfilePath = SessionProfilePath(projectRoot, actualRunId);
                ProfileSnapshot restoredProfile = ReadV6Profile(context, actualRunId);
                Assert.That(PathEquals(restoredProfile.Path, expectedProfilePath), Is.True,
                    "The resumed profile must be the RunId-scoped profile under this copy.");
                Assert.That(restoredProfile.Sha256, Is.EqualTo(receipt.profileSha256),
                    "Process B must reopen the exact genuine Profile V6 bytes saved by A.");
                AssertSavedC1Checkpoint(restoredProfile, receipt, requireC1CurrentRoute: true);

                Assert.That(context.App.ActiveExpedition.currentNodeId, Is.EqualTo(E1Node));
                Assert.That(context.App.ActiveRun.Inventory.Gold, Is.EqualTo(receipt.c1GoldAfter),
                    "C1 gold must not be paid a second time during restore.");
                Assert.That(context.App.ActiveExpedition.accumulatedRenown, Is.EqualTo(receipt.c1RenownAfter),
                    "C1 Renown must not be paid a second time during restore.");
                CollectionAssert.AreEqual(receipt.c1ClaimedRewardIds,
                    context.App.ActiveExpedition.claimedRewardIds,
                    "The restored C1 claim set must match the durable receipt exactly.");
                Assert.That(context.App.HasPendingSettlement, Is.False);

                // At this point only read/compare operations have occurred; physical route continuation starts below.
                yield return TraverseE1AndExit(context);
                yield return DefeatCustodianPhysically(context);
                yield return ExtractPhysically(context, receipt);

                Assert.That(HashFile(testReceiptPath), Is.EqualTo(receiptHashBefore),
                    "Process B must not rewrite Process A's read-only evidence.");
                Assert.That((File.GetAttributes(testReceiptPath) & FileAttributes.ReadOnly) != 0, Is.True);
            }
            finally
            {
                CloseDiagnostic(context);
            }
        }

        private static IEnumerator OpenDiagnostic(RouteContext context, F10DiagnosticSessionMode mode)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance, Is.Null);
            context.PreviousScene = SceneManager.GetActiveScene();
            context.PreviousCaptureDelta = Time.captureDeltaTime;
            context.Keyboard = InputSystem.AddDevice<Keyboard>();

            // Fixed frame duration only; health, damage, collision, physics and win conditions stay real.
            Time.captureDeltaTime = 1f / 30f;
            F10DiagnosticSceneRunner.SetSessionModeForNextStart(mode);
            context.DiagnosticScene = EditorSceneManager.LoadSceneInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            double sceneLoadDeadline = Time.realtimeSinceStartupAsDouble + 45d;
            for (int frame = 0; frame < 2400 && Time.realtimeSinceStartupAsDouble < sceneLoadDeadline &&
                (!context.DiagnosticScene.IsValid() || !context.DiagnosticScene.isLoaded); frame++)
            {
                yield return null;
                context.DiagnosticScene = SceneManager.GetSceneByPath(ScenePath);
            }
            Assert.That(context.DiagnosticScene.IsValid() && context.DiagnosticScene.isLoaded, Is.True,
                "The canonical diagnostic scene did not finish loading within the bounded wait; handle=" +
                context.DiagnosticScene.handle + ", loadedScenes=" + SceneManager.sceneCount + ".");
            Assert.That(context.DiagnosticScene.path, Is.EqualTo(ScenePath));
            Assert.That(context.DiagnosticScene.name, Is.EqualTo("F10Diagnostic"));
            if (SceneManager.GetActiveScene() != context.DiagnosticScene)
                SceneManager.SetActiveScene(context.DiagnosticScene);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(context.DiagnosticScene),
                "The canonical diagnostic scene must be active before waiting for its runner.");

            double deadline = Time.realtimeSinceStartupAsDouble + 45d;
            for (int frame = 0; frame < 2400 && Time.realtimeSinceStartupAsDouble < deadline; frame++)
            {
                yield return null;
                context.Runner = FindRunner(context.DiagnosticScene);
                if (context.Runner != null &&
                    ((context.Runner.Composition != null && context.Runner.Composition.IsReady) ||
                     !string.IsNullOrEmpty(context.Runner.LastError))) break;
            }

            Assert.That(context.Runner, Is.Not.Null, "The diagnostic runner did not appear within the bounded wait.");
            Assert.That(context.Runner.LastError, Is.Empty);
            Assert.That(context.Runner.Composition, Is.Not.Null);
            Assert.That(context.Runner.Composition.IsReady, Is.True);
            context.App = ApplicationBootstrapper.Instance;
            Assert.That(context.App, Is.Not.Null);
            Assert.That(context.App.ActiveRun, Is.Not.Null);
            Assert.That(context.App.ActiveExpedition, Is.Not.Null);
            context.Player = context.Runner.Composition.PlayerMotor;
            Assert.That(context.Player, Is.Not.Null);
        }

        private static IEnumerator CompleteTutorial(RouteContext context)
        {
            AssertNode(context, TutorialNode, "tutorial start");
            Vector3 initialPosition = context.Player.transform.position;
            float minX = initialPosition.x, maxX = initialPosition.x;
            float minY = initialPosition.y, maxY = initialPosition.y;
            long initialTick = context.Player.SimulationTick;
            int observedFrames = 0, movingCommandFrames = 0, groundedFrames = 0;
            long lastTick = -1;
            long jumpUntil = -1;
            long interactUntil = -1;
            bool interactionRequested = false;
            int stalledFrames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 90d;
            for (int frame = 0; frame < 9000 && Time.realtimeSinceStartupAsDouble < deadline &&
                CurrentNode(context) == TutorialNode; frame++)
            {
                yield return null;
                observedFrames++;
                Vector3 position = context.Player.transform.position;
                minX = Mathf.Min(minX, position.x); maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y); maxY = Mathf.Max(maxY, position.y);
                if (context.Player.IsGrounded) groundedFrames++;
                if (context.Player.HasConsumedCommand && Mathf.Abs(context.Player.LastConsumedCommand.MoveX) > 0.1f)
                    movingCommandFrames++;
                long tick = context.Player.SimulationTick;
                if (tick == lastTick)
                {
                    if (!interactionRequested && ++stalledFrames > StalledFrameLimit) break;
                    continue;
                }
                stalledFrames = 0;
                lastTick = tick;
                Assert.That(context.Player.IsDead, Is.False, "The player died in the tutorial.");
                float x = context.Player.transform.position.x;
                if (!interactionRequested)
                {
                    SetKey(context, Key.D, x < 13f);
                    if (x > 5f && x < 8.5f && context.Player.IsGrounded && tick >= jumpUntil)
                        jumpUntil = tick + 8;
                    SetKey(context, Key.Space, tick < jumpUntil);
                    if (x >= 13f)
                    {
                        interactionRequested = true;
                        interactUntil = tick + 3;
                    }
                }
                SetKey(context, Key.E, interactionRequested && tick < interactUntil);
            }
            Rigidbody2D body = context.Player.GetComponent<Rigidbody2D>();
            string tutorialState = "Tutorial observation: frames=" + observedFrames +
                "; tick=" + initialTick + "->" + context.Player.SimulationTick +
                "; initial=" + initialPosition + "; final=" + context.Player.transform.position +
                "; xRange=" + minX + ".." + maxX + "; yRange=" + minY + ".." + maxY +
                "; groundedFrames=" + groundedFrames + "; movingCommandFrames=" + movingCommandFrames +
                "; consumed=" + context.Player.HasConsumedCommand +
                "; commandMove=" + context.Player.LastConsumedCommand.MoveX +
                "; commandPressed=" + context.Player.LastConsumedCommand.Pressed +
                "; velocity=" + (body == null ? "missing" : body.linearVelocity.ToString()) +
                "; timeScale=" + Time.timeScale + "; fixedDelta=" + Time.fixedDeltaTime +
                "; active=" + context.Player.isActiveAndEnabled +
                "; keyboardEnabled=" + context.Keyboard.enabled + "; dKey=" + context.Keyboard.dKey.isPressed +
                "; focused=" + Application.isFocused + "; runInBackground=" + Application.runInBackground +
                "; inputUpdate=" + InputSystem.settings.updateMode +
                "; background=" + InputSystem.settings.backgroundBehavior +
                "; node=" + CurrentNode(context);
            TestContext.WriteLine(tutorialState);
            ReleaseAll(context);
            Assert.That(interactionRequested, Is.True,
                "The player did not reach the physical tutorial interaction. " + tutorialState);
            yield return WaitForNode(context, C1Node, 60d, 3600);
        }

        private static IEnumerator ClearC1Physically(RouteContext context, C1Evidence evidence)
        {
            evidence.C1GoldBefore = context.App.ActiveRun.Inventory.Gold;
            evidence.C1RenownBefore = context.App.ActiveExpedition.accumulatedRenown;
            evidence.C1ClaimsBefore = Copy(context.App.ActiveExpedition.claimedRewardIds);
            Assert.That(evidence.C1ClaimsBefore.Length, Is.EqualTo(0),
                "A new diagnostic run must have no pre-existing reward claims before C1.");

            var seenSlimes = new HashSet<ActorId>();
            var seenBoneguards = new HashSet<ActorId>();
            var impactTotals = new Dictionary<ActorId, int>();
            var observedWaves = new HashSet<int>();
            F10C1RuntimeCoordinator lastCoordinator = null;
            long c1ActivationGeneration = 0;
            long lastAttack = -1000, lastDodge = -1000, lastPotion = -1000;
            long attackUntil = -1, dodgeUntil = -1, potionUntil = -1, jumpUntil = -1, lastJump = -1000;
            long combatTick = -1;
            int stalledFrames = 0, stalledMotionFrames = 0;
            float previousX = context.Player.transform.position.x;
            double deadline = Time.realtimeSinceStartupAsDouble + 600d;

            for (int frame = 0; frame < 72000 && Time.realtimeSinceStartupAsDouble < deadline &&
                CurrentNode(context) == C1Node; frame++)
            {
                yield return null;
                if (CurrentNode(context) != C1Node) break;
                RoomLifetimeScope scope = context.Runner.Composition.RoomTransitions.ActiveScope;
                F10C1RuntimeCoordinator coordinator = scope == null ? null : scope.GetComponent<F10C1RuntimeCoordinator>();
                if (coordinator == null) continue;
                lastCoordinator = coordinator;
                ExpeditionReadinessCompositionView view = context.Runner.Composition.CaptureReadinessCompositionView();
                RoomEncounterRuntimeContext activeContext = view == null || view.ReadinessRegistration == null ||
                    view.ReadinessRegistration.CurrentActivation == null
                    ? null : view.ReadinessRegistration.CurrentActivation.Context;
                if (activeContext != null)
                {
                    Assert.That(activeContext.NodeId, Is.EqualTo(C1Node));
                    if (c1ActivationGeneration == 0) c1ActivationGeneration = activeContext.ActivationGeneration;
                    else Assert.That(activeContext.ActivationGeneration, Is.EqualTo(c1ActivationGeneration));
                }
                RoomEncounterRuntime runtime = coordinator.Runtime;
                if (coordinator.IsSetupFailed || coordinator.RuntimeStatus == RoomEncounterRuntimeStatus.Faulted)
                    Assert.Fail("C1 runtime fault: " + coordinator.StatusMessage);
                if (runtime == null) continue;

                Slime2D[] slimes = scope.GetComponentsInChildren<Slime2D>(true);
                Huesoguardia2D[] bones = scope.GetComponentsInChildren<Huesoguardia2D>(true);
                foreach (Slime2D slime in slimes)
                {
                    if (slime == null) continue;
                    ActorId id = slime.ActorId;
                    Assert.That(id.IsValid, Is.True);
                    seenSlimes.Add(id);
                    impactTotals[id] = slime.MeleeImpactCount + slime.ProjectileImpactCount;
                }
                foreach (Huesoguardia2D bone in bones)
                {
                    if (bone == null) continue;
                    ActorId id = bone.ActorId;
                    Assert.That(id.IsValid, Is.True);
                    seenBoneguards.Add(id);
                    impactTotals[id] = bone.MeleeImpactCount + bone.ProjectileImpactCount;
                }
                if (coordinator.CurrentWaveNumber > 0) observedWaves.Add(coordinator.CurrentWaveNumber);
                if (runtime.Status != RoomEncounterRuntimeStatus.Armed)
                {
                    stalledFrames = 0;
                    continue;
                }

                long tick = context.Player.SimulationTick;
                if (tick == combatTick)
                {
                    if (++stalledFrames > StalledFrameLimit) break;
                    continue;
                }
                stalledFrames = 0;
                combatTick = tick;
                Assert.That(context.Player.IsDead, Is.False, "The player died during C1 combat.");
                Transform target = slimes.Where(enemy => enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead)
                    .Select(enemy => enemy.transform)
                    .Concat(bones.Where(enemy => enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead)
                        .Select(enemy => enemy.transform))
                    .OrderBy(enemy => Mathf.Abs(enemy.position.x - context.Player.transform.position.x)).FirstOrDefault();
                float dx = target == null ? 0f : target.position.x - context.Player.transform.position.x;
                bool imminent = slimes.Any(enemy => enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead &&
                    (enemy.State == SlimeBehaviorState.Attack || enemy.State == SlimeBehaviorState.Telegraph &&
                     enemy.TelegraphTicksRemaining <= 3) && Mathf.Abs(enemy.transform.position.x - context.Player.transform.position.x) < 3.4f) ||
                    bones.Any(enemy => enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead &&
                    (enemy.State == HuesoguardiaBehaviorState.Attack || enemy.State == HuesoguardiaBehaviorState.Telegraph &&
                     enemy.TelegraphTicksRemaining <= 3) && Mathf.Abs(enemy.transform.position.x - context.Player.transform.position.x) < 3.5f);
                if (imminent && context.Player.IsDodgeReady && tick - lastDodge >= 48)
                {
                    lastDodge = tick;
                    dodgeUntil = tick + 1;
                }
                float move = target == null ? 0f : tick < dodgeUntil ? (dx >= 0f ? -1f : 1f) :
                    Mathf.Abs(dx) > 1.35f ? (dx > 0f ? 1f : -1f) : 0f;
                stalledMotionFrames = move != 0f && context.Player.IsGrounded &&
                    Mathf.Abs(context.Player.transform.position.x - previousX) < 0.02f
                    ? stalledMotionFrames + 1 : 0;
                previousX = context.Player.transform.position.x;
                if (stalledMotionFrames >= 10 && tick - lastJump >= 35)
                {
                    lastJump = tick;
                    jumpUntil = tick + 8;
                    stalledMotionFrames = 0;
                    evidence.JumpInputs++;
                }
                SetKey(context, Key.A, move < 0f);
                SetKey(context, Key.D, move > 0f);
                SetKey(context, Key.Space, tick < jumpUntil);
                SetKey(context, Key.LeftShift, tick < dodgeUntil);
                if (target != null && Mathf.Abs(dx) <= 1.55f && tick - lastAttack >= 29)
                {
                    lastAttack = tick;
                    attackUntil = tick + 1;
                    evidence.AttackInputs++;
                }
                SetKey(context, Key.J, tick < attackUntil);
                if (imminent && tick >= dodgeUntil) evidence.DodgeObservations++;
                if (context.Player.CurrentHealth <= 55 && context.Player.HealthPotionCharges > 0 &&
                    !context.Player.IsPreparingHealthPotion && tick - lastPotion >= 60)
                {
                    lastPotion = tick;
                    potionUntil = tick + 1;
                }
                SetKey(context, Key.F, tick < potionUntil);
            }

            ReleaseAll(context);
            yield return WaitForNode(context, E1Node, 90d, 5400);
            // The coordinator retains its immutable physical clear even if its Unity object was
            // destroyed during the transition. Do not depend on a transient roster counter.
            RoomEncounterClearObservation clear = ReferenceEquals(lastCoordinator, null)
                ? null : lastCoordinator.PhysicalClearObservation;
            Assert.That(clear, Is.Not.Null, "C1 must retain its physical roster-clear observation.");
            Assert.That(clear.RunId, Is.EqualTo(context.App.ActiveRun.RunId));
            Assert.That(clear.NodeId, Is.EqualTo(C1Node));
            Assert.That(clear.TemplateId, Is.EqualTo("room.patio.c1"));
            Assert.That(clear.RoomInstanceId, Is.EqualTo(ExpeditionRoomIdentity.ForNode(clear.RunId, C1Node)));
            Assert.That(c1ActivationGeneration, Is.GreaterThan(0));
            Assert.That(clear.ActivationGeneration, Is.EqualTo(c1ActivationGeneration));
            Assert.That(clear.Actors, Has.Count.EqualTo(4));
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2 },
                clear.Actors.Select(actor => actor.WaveNumber).OrderBy(number => number));
            var defeatedActors = new HashSet<ActorId>(clear.Actors.Select(actor => actor.ActorId));
            Assert.That(defeatedActors.Count, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(seenSlimes.Concat(seenBoneguards), defeatedActors,
                "The physically defeated roster must match the actors observed in C1.");
            evidence.CompletedWaveCount = clear.Actors.Select(actor => actor.WaveNumber).Distinct().Count();
            evidence.SeenSlimeCount = seenSlimes.Count;
            evidence.SeenBoneguardCount = seenBoneguards.Count;
            evidence.AllSlimesDied = seenSlimes.All(defeatedActors.Contains);
            evidence.AllBoneguardsDied = seenBoneguards.All(defeatedActors.Contains);
            evidence.TotalCombatImpacts = impactTotals.Values.Sum();
            evidence.WavesObserved = observedWaves.OrderBy(value => value).ToArray();

            Assert.That(evidence.CompletedWaveCount, Is.EqualTo(2), "C1 must physically clear both waves.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, evidence.WavesObserved,
                "Both physical C1 waves must be observed in the active runtime.");
            Assert.That(evidence.SeenSlimeCount, Is.EqualTo(2));
            Assert.That(evidence.SeenBoneguardCount, Is.EqualTo(2));
            Assert.That(evidence.AllSlimesDied, Is.True, "Every observed Slime must die from real combat.");
            Assert.That(evidence.AllBoneguardsDied, Is.True, "Every observed Huesoguardia must die from real combat.");
            Assert.That(evidence.TotalCombatImpacts, Is.GreaterThan(0));
            Assert.That(evidence.AttackInputs, Is.GreaterThan(0), "The test must issue real attack input.");

            Assert.That(context.App.ActiveRun.Inventory.Gold, Is.EqualTo(evidence.C1GoldBefore + 24));
            Assert.That(context.App.ActiveExpedition.accumulatedRenown, Is.EqualTo(evidence.C1RenownBefore + 5));
            Assert.That(context.App.ActiveExpedition.completedRoomIds, Does.Contain(C1Node));
            evidence.C1GoldAfter = context.App.ActiveRun.Inventory.Gold;
            evidence.C1RenownAfter = context.App.ActiveExpedition.accumulatedRenown;
            evidence.C1ClaimsAfter = Copy(context.App.ActiveExpedition.claimedRewardIds);
            Assert.That(evidence.C1ClaimsAfter.Length, Is.GreaterThan(0));
            Assert.That(evidence.C1ClaimsAfter.All(string.IsNullOrWhiteSpace), Is.False);
            Assert.That(evidence.C1ClaimsAfter.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(evidence.C1ClaimsAfter.Length), "C1 reward claims must be unique.");

            TestContext.WriteLine("C1 physical proof: waves=1,2; Slimes=" + evidence.SeenSlimeCount +
                "; Huesoguardias=" + evidence.SeenBoneguardCount + "; impact count=" +
                evidence.TotalCombatImpacts + "; claims=" + evidence.C1ClaimsAfter.Length + ".");
        }

        private static IEnumerator TraverseE1AndExit(RouteContext context)
        {
            AssertNode(context, E1Node, "Process B resumed E1");
            int renownBefore = context.App.ActiveExpedition.accumulatedRenown;
            string[] claimsBefore = Copy(context.App.ActiveExpedition.claimedRewardIds);
            ExpeditionRunConfiguration run = context.App.ActiveRun;
            ExpeditionRouteNode e1 = run.Route.Nodes.Single(node => node.NodeId == E1Node);
            bool avarice = context.App.ActiveExpedition.pactIds.Contains("pact.avarice", StringComparer.Ordinal);
            ExpeditionEncounterRewardResolution rewards = ExpeditionEncounterRewardPolicy.Resolve(
                new ExpeditionEncounterRewardRequest(run.RunId, run.Seed, run.Route.GenerationVersion,
                    e1.NodeId, e1.TemplateId, e1.SectorId, e1.Kind,
                    Array.Empty<ExpeditionEncounterRewardSpawn>(), avaricePactActive: avarice,
                    hasExplorationCache: true, explorationCacheKey: "cache.exploration"));
            string e1RenownClaim = rewards.RoomPayout.RenownGrantId;
            Assert.That(e1RenownClaim, Is.Not.Null.And.Not.Empty);
            Assert.That(claimsBefore, Does.Not.Contain(e1RenownClaim));
            RoomLifetimeScope scope = context.Runner.Composition.RoomTransitions.ActiveScope;
            Assert.That(scope, Is.Not.Null);
            RoomPositionMarker2D exitMarker = scope.GetComponentsInChildren<RoomPositionMarker2D>(true)
                .Single(marker => marker.MarkerId == "exit.trigger");
            long lastTick = -1, lastJump = -1000, jumpUntil = -1;
            int jumpCount = 0, stalledFrames = 0;
            F10ExplorationAdvanceCoordinator coordinator = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 360d;
            for (int frame = 0; frame < 48000 && Time.realtimeSinceStartupAsDouble < deadline &&
                CurrentNode(context) == E1Node; frame++)
            {
                yield return null;
                if (CurrentNode(context) != E1Node) break;
                coordinator = scope.GetComponent<F10ExplorationAdvanceCoordinator>();
                if (coordinator != null && (coordinator.State == F10ExplorationAdvanceState.SetupFailed ||
                    coordinator.State == F10ExplorationAdvanceState.Failed))
                    Assert.Fail("E1 coordinator failed: " + coordinator.Error);
                if (coordinator != null && (coordinator.State == F10ExplorationAdvanceState.Resolving ||
                    coordinator.State == F10ExplorationAdvanceState.PendingRetry ||
                    coordinator.State == F10ExplorationAdvanceState.Completed))
                {
                    SetKey(context, Key.A, false);
                    SetKey(context, Key.D, false);
                    SetKey(context, Key.Space, false);
                    continue;
                }
                long tick = context.Player.SimulationTick;
                if (tick == lastTick)
                {
                    if (++stalledFrames > StalledFrameLimit) break;
                    continue;
                }
                stalledFrames = 0;
                lastTick = tick;
                Assert.That(context.Player.IsDead, Is.False, "The player died during E1 traversal.");
                float dx = exitMarker.transform.position.x - context.Player.transform.position.x;
                SetKey(context, Key.A, false);
                SetKey(context, Key.D, dx > 0.65f);
                if (dx > 0.65f && context.Player.IsGrounded && tick - lastJump >= 22)
                {
                    lastJump = tick;
                    jumpUntil = tick + 7;
                    jumpCount++;
                }
                SetKey(context, Key.Space, tick < jumpUntil);
            }
            ReleaseAll(context);
            yield return WaitForNode(context, GuardianNode, 90d, 5400);
            Assert.That(jumpCount, Is.GreaterThan(0), "E1 must be traversed with real jump input.");
            for (int frame = 0; frame < 300 && context.App.HasPendingSessionMutation; frame++) yield return null;
            Assert.That(context.App.HasPendingSessionMutation, Is.False);
            Assert.That(context.App.ActiveExpedition.accumulatedRenown, Is.EqualTo(renownBefore + 3));
            Assert.That(context.App.HasPendingSettlement, Is.False);
            ProfileSnapshot saved = ReadV6Profile(context, context.App.ActiveRun.RunId.ToString());
            Assert.That(saved.Envelope.hasActiveExpedition, Is.True);
            Assert.That(saved.Envelope.activeExpedition.currentNodeId, Is.EqualTo(GuardianNode));
            Assert.That(saved.Envelope.activeExpedition.completedRoomIds, Does.Contain(C1Node));
            Assert.That(saved.Envelope.activeExpedition.completedRoomIds, Does.Contain(E1Node));
            ExpeditionRoomCompletionSaveData[] results = saved.Envelope.activeExpedition.completedRoomResults
                .Where(result => result != null && result.roomNodeId == E1Node).ToArray();
            Assert.That(results, Has.Length.EqualTo(1));
            string roomId = ExpeditionRoomIdentity.ForNode(run.RunId, E1Node).ToString();
            Assert.That(results[0].resultId, Is.EqualTo("room-result.v1:" + roomId));
            F10DurableRoomResult result = JsonUtility.FromJson<F10DurableRoomResult>(results[0].resultJson);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.schema, Is.EqualTo(1));
            Assert.That(result.outcome, Is.EqualTo("cleared"));
            Assert.That(result.roomInstanceId, Is.EqualTo(roomId));
            Assert.That(result.sourceNodeId, Is.EqualTo(E1Node));
            Assert.That(result.targetNodeId, Is.EqualTo(GuardianNode));
            Assert.That(result.technicalPayment, Is.EqualTo("f10-room.v1"));
            string[] claimsAfter = saved.Envelope.activeExpedition.claimedRewardIds;
            Assert.That(claimsAfter.Count(id => id == e1RenownClaim), Is.EqualTo(1));
            Assert.That(claimsAfter.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(claimsAfter.Length));
            CollectionAssert.IsSubsetOf(claimsBefore, claimsAfter);
        }

        private static IEnumerator DefeatCustodianPhysically(RouteContext context)
        {
            AssertNode(context, GuardianNode, "Custodian entry");
            RoomLifetimeScope scope = context.Runner.Composition.RoomTransitions.ActiveScope;
            F10GuardianRuntimeCoordinator coordinator = null;
            CustodianBoss2D boss = null;
            double setupDeadline = Time.realtimeSinceStartupAsDouble + 60d;
            for (int frame = 0; frame < 3600 && Time.realtimeSinceStartupAsDouble < setupDeadline; frame++)
            {
                yield return null;
                if (CurrentNode(context) != GuardianNode) break;
                coordinator = scope == null ? null : scope.GetComponent<F10GuardianRuntimeCoordinator>();
                if (coordinator != null && (coordinator.IsSetupFailed ||
                    coordinator.RuntimeStatus == RoomEncounterRuntimeStatus.Faulted))
                    Assert.Fail("Guardian runtime failed: " + coordinator.StatusMessage);
                if (coordinator != null && coordinator.RuntimeStatus == RoomEncounterRuntimeStatus.Armed)
                {
                    RoomEncounterActorBinding[] bindings = coordinator.Runtime.RegisteredBindings.ToArray();
                    boss = bindings.SingleOrDefault(binding => binding.CustodianBoss != null)?.CustodianBoss;
                    if (boss != null) break;
                }
            }
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(coordinator.RuntimeStatus, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.IsDead, Is.False);

            long lastTick = -1, lastAttack = -1000, lastJump = -1000, lastDodge = -1000, lastPotion = -1000;
            long attackUntil = -1, jumpUntil = -1, dodgeUntil = -1, potionUntil = -1;
            int stalledFrames = 0, jumpInputs = 0, receivedHits = 0;
            bool physicallyDefeated = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 720d;
            for (int frame = 0; frame < 60000 && Time.realtimeSinceStartupAsDouble < deadline &&
                CurrentNode(context) == GuardianNode; frame++)
            {
                yield return null;
                if (boss != null) physicallyDefeated |= boss.IsDead && boss.CurrentHealth == 0 && boss.MeleeImpactCount > 0;
                if (CurrentNode(context) != GuardianNode) break;
                if (coordinator.RuntimeStatus != RoomEncounterRuntimeStatus.Armed || boss == null)
                {
                    stalledFrames = 0;
                    continue;
                }
                long tick = context.Player.SimulationTick;
                if (tick == lastTick)
                {
                    if (++stalledFrames > StalledFrameLimit) break;
                    continue;
                }
                stalledFrames = 0;
                lastTick = tick;
                Assert.That(context.Player.IsDead, Is.False, "The player died during Custodian combat.");
                receivedHits = boss.SuccessfulPlayerHitCount;
                if (boss.IsDead) continue;
                float dx = boss.transform.position.x - context.Player.transform.position.x;
                if (boss.State == CustodianBehaviorState.Telegraphing && boss.TelegraphTicksRemaining <= 10 &&
                    context.Player.IsGrounded && tick - lastJump >= 45)
                {
                    lastJump = tick;
                    jumpUntil = tick + 12;
                    jumpInputs++;
                }
                bool imminent = boss.State == CustodianBehaviorState.Telegraphing &&
                    boss.TelegraphTicksRemaining <= 3 && Mathf.Abs(dx) < 3.5f;
                if (imminent && context.Player.IsDodgeReady && tick - lastDodge >= 48)
                {
                    lastDodge = tick;
                    dodgeUntil = tick + 1;
                }
                float move = tick < dodgeUntil ? (dx >= 0f ? -1f : 1f) :
                    Mathf.Abs(dx) > 1.4f ? (dx > 0f ? 1f : -1f) : 0f;
                SetKey(context, Key.A, move < 0f);
                SetKey(context, Key.D, move > 0f);
                SetKey(context, Key.Space, tick < jumpUntil);
                SetKey(context, Key.LeftShift, tick < dodgeUntil);
                if (Mathf.Abs(dx) <= 1.9f && tick - lastAttack >= 27)
                {
                    lastAttack = tick;
                    attackUntil = tick + 1;
                }
                SetKey(context, Key.J, tick < attackUntil);
                if (context.Player.CurrentHealth <= 55 && context.Player.HealthPotionCharges > 0 &&
                    !context.Player.IsPreparingHealthPotion && tick - lastPotion >= 60)
                {
                    lastPotion = tick;
                    potionUntil = tick + 1;
                }
                SetKey(context, Key.F, tick < potionUntil);
            }
            ReleaseAll(context);
            yield return WaitForNode(context, ExtractionNode, 120d, 7200);
            Assert.That(physicallyDefeated, Is.True,
                "Custodian must die from a real player combat impact; no victory may be forced.");
            Assert.That(context.Player.IsDead, Is.False);
            Assert.That(jumpInputs, Is.GreaterThan(0), "The Custodian fight must include real jump input.");
            TestContext.WriteLine("Custodian physical result: jump inputs=" + jumpInputs + "; successful hits received=" + receivedHits + ".");
        }

        private static IEnumerator ExtractPhysically(RouteContext context, F10V6ResumeTestReceipt receipt)
        {
            AssertNode(context, ExtractionNode, "safe extraction entry");
            ProfileSnapshot guardianSaved = ReadV6Profile(context, receipt.runId);
            Assert.That(guardianSaved.Envelope.hasActiveExpedition, Is.True);
            Assert.That(guardianSaved.Envelope.activeExpedition.currentNodeId, Is.EqualTo(ExtractionNode));
            Assert.That(guardianSaved.Envelope.activeExpedition.completedRoomIds, Does.Contain(C1Node));
            Assert.That(guardianSaved.Envelope.activeExpedition.completedRoomIds, Does.Contain(E1Node));
            Assert.That(guardianSaved.Envelope.activeExpedition.completedRoomIds, Does.Contain(GuardianNode));
            Assert.That(guardianSaved.Envelope.activeExpedition.completedRoomResults.Count(result =>
                result != null && result.roomNodeId == GuardianNode), Is.EqualTo(1));

            int accumulatedRenown = context.App.ActiveExpedition.accumulatedRenown;
            ExpeditionInventorySnapshot inventorySnapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                guardianSaved.Envelope.activeExpedition.inventorySnapshotJson);
            ExpeditionInventory preExtractionInventory = ExpeditionInventory.Restore(
                context.App.ActiveRun.Inventory.Catalog, inventorySnapshot);
            int lootRenown = CalculateLootRenown(preExtractionInventory,
                context.App.ActiveRun.StartingEquipmentInstanceIds);
            Assert.That(preExtractionInventory.Gold, Is.EqualTo(context.App.ActiveRun.Inventory.Gold),
                "Loot must be calculated from the durable Guardian checkpoint.");

            RoomLifetimeScope scope = context.Runner.Composition.RoomTransitions.ActiveScope;
            RoomPositionMarker2D marker = scope.GetComponentsInChildren<RoomPositionMarker2D>(true)
                .Single(item => item.MarkerId == "extraction.interact");
            Collider2D playerCollider = context.Player.GetComponent<Collider2D>();
            Assert.That(playerCollider, Is.Not.Null);
            F10ExtractionSettlementCoordinator coordinator = null;
            long lastTick = -1, lastInteract = -1000, interactUntil = -1;
            int stalledFrames = 0;
            bool interactionRequested = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 180d;
            for (int frame = 0; frame < 9000 && Time.realtimeSinceStartupAsDouble < deadline; frame++)
            {
                yield return null;
                if (context.App.ActiveRun == null && !context.App.HasActiveExpedition && !context.App.HasPendingSettlement)
                    break;
                if (context.Runner.Composition == null || CurrentNode(context) != ExtractionNode)
                    Assert.Fail("The composition ended before a durable terminal profile was observed.");
                coordinator = context.Runner.Composition.GetComponent<F10ExtractionSettlementCoordinator>();
                if (coordinator != null && (coordinator.State == F10ExtractionSettlementState.SetupFailed ||
                    coordinator.State == F10ExtractionSettlementState.Failed ||
                    coordinator.State == F10ExtractionSettlementState.PendingRetry))
                    Assert.Fail("Extraction did not settle: " + coordinator.State + " · " + coordinator.Error);
                if (coordinator != null && coordinator.State == F10ExtractionSettlementState.Resolving)
                {
                    ReleaseAll(context);
                    continue;
                }
                long tick = context.Player.SimulationTick;
                if (tick == lastTick)
                {
                    if (++stalledFrames > StalledFrameLimit) break;
                    continue;
                }
                stalledFrames = 0;
                lastTick = tick;
                Assert.That(context.Player.IsDead, Is.False, "The player died before extraction interaction.");
                BoxCollider2D trigger = marker.GetComponentsInChildren<BoxCollider2D>(true)
                    .SingleOrDefault(collider => collider.gameObject.name == "F10ObjectiveTrigger.extraction.interact" &&
                        collider.enabled && collider.isTrigger && collider.gameObject.activeInHierarchy);
                if (coordinator == null || trigger == null)
                {
                    ReleaseAll(context);
                    continue;
                }
                ColliderDistance2D overlap = playerCollider.Distance(trigger);
                float dx = marker.transform.position.x - context.Player.transform.position.x;
                bool inRange = overlap.isValid && overlap.distance <= 0f && context.Player.IsGrounded;
                SetKey(context, Key.A, !inRange && dx < 0f);
                SetKey(context, Key.D, !inRange && dx > 0f);
                if (inRange && tick - lastInteract >= 90)
                {
                    lastInteract = tick;
                    interactUntil = tick + 3;
                    interactionRequested = true;
                }
                SetKey(context, Key.E, inRange && tick < interactUntil);
            }
            ReleaseAll(context);
            Assert.That(interactionRequested, Is.True,
                "The player must physically walk into and interact with the safe-extraction marker.");
            double terminalDeadline = Time.realtimeSinceStartupAsDouble + 60d;
            for (int frame = 0; frame < 3600 && Time.realtimeSinceStartupAsDouble < terminalDeadline &&
                (context.App.ActiveRun != null || context.App.HasActiveExpedition || context.App.HasPendingSettlement); frame++)
                yield return null;

            Assert.That(context.App.ActiveRun, Is.Null);
            Assert.That(context.App.HasActiveExpedition, Is.False);
            Assert.That(context.App.HasPendingSettlement, Is.False);
            ProfileSnapshot terminal = ReadV6Profile(context, receipt.runId);
            Assert.That(terminal.Envelope.hasActiveExpedition, Is.False);
            Assert.That(terminal.Envelope.activeExpedition, Is.Null);
            Assert.That(terminal.Envelope.profile, Is.Not.Null);
            Assert.That((terminal.Envelope.profile.processedSettlementRunIds ?? Array.Empty<string>())
                .Count(id => string.Equals(id, receipt.runId, StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(context.App.SettlementLedger.ProcessedRunIds.Count(id => id.ToString() == receipt.runId), Is.EqualTo(1));
            Assert.That(terminal.Envelope.profile.availableRenown,
                Is.EqualTo(receipt.initialProfileRenown + accumulatedRenown + lootRenown),
                "Terminal Profile V6 must equal initial Renown plus durable room Renown and exact loot conversion.");
            TestContext.WriteLine("Process B terminal result: RunId=" + receipt.runId +
                "; processed once; Renown=" + terminal.Envelope.profile.availableRenown +
                "; profile SHA-256=" + terminal.Sha256 + ".");
        }

        private static ProfileSnapshot ReadV6Profile(RouteContext context, string runId)
        {
            Assert.That(context.App, Is.Not.Null);
            Assert.That(context.App.F10DiagnosticWitness, Is.Not.Null);
            string expected = SessionProfilePath(ProjectRoot, runId);
            string actual = context.App.F10DiagnosticWitness.Store.GetPrimaryPath(SaveDocument.Profile);
            Assert.That(PathEquals(actual, expected), Is.True,
                "SaveRepository must use only the fixed RunId-scoped profile path inside this copy.");
            AssertInsideProject(ProjectRoot, actual);
            Assert.That(File.Exists(expected), Is.True, "The genuine Profile V6 file is missing: " + expected);
            byte[] bytes = File.ReadAllBytes(expected);
            string json = File.ReadAllText(expected);
            Assert.That(SaveJsonCodec.Decode(json, out ProfileSaveEnvelope envelope, out int version),
                Is.EqualTo(DecodeStatus.Valid));
            Assert.That(version, Is.EqualTo(6));
            return new ProfileSnapshot
            {
                Path = expected,
                Json = json,
                Sha256 = HashBytes(bytes),
                Envelope = envelope
            };
        }

        private static LinuxDiagnosticReceipt ReadLinuxDiagnosticReceipt(string projectRoot)
        {
            string path = Absolute(projectRoot, LinuxRuntimeReceiptRelative);
            AssertInsideProject(projectRoot, path);
            Assert.That(File.Exists(path), Is.True, "The fixed Linux diagnostic receipt is missing.");
            LinuxDiagnosticReceipt receipt = JsonUtility.FromJson<LinuxDiagnosticReceipt>(File.ReadAllText(path));
            Assert.That(receipt, Is.Not.Null, "The Linux diagnostic receipt could not be parsed.");
            return receipt;
        }

        private static void AssertLinuxDiagnosticReceipt(LinuxDiagnosticReceipt receipt, string projectRoot,
            string bootstrapSelector)
        {
            Assert.That(F10DiagnosticLinuxRuntimeEvidenceReader.ValidateForRuntime(projectRoot,
                expectedBootstrapSelector: bootstrapSelector), Is.Not.Null,
                "The exact Linux runner receipt, physical copy inventory, and Unity materialization must revalidate.");
            Assert.That(receipt.schema, Is.EqualTo("ci-linux-diagnostic"));
            Assert.That(receipt.schemaVersion, Is.EqualTo(1));
            Assert.That(receipt.status, Is.EqualTo("verified"));
            Assert.That(receipt.unityVersion, Is.EqualTo("6000.6.10f1"));
            Assert.That(receipt.baseOverlayManifestSha256,
                Is.EqualTo("d918873b7206f8091a4d286cb8e5be94be2d0c45d2df60de85e46cf9e390139c"));
            Assert.That(IsSha256(receipt.linuxOverlayManifestSha256), Is.True);
            Assert.That(receipt.baseSnapshotPath,
                Is.EqualTo("Logs/F10LinuxSourceSnapshot-20261127/source.zip"));
            Assert.That(receipt.sourceInventoryPath,
                Is.EqualTo("Logs/F10LinuxSourceSnapshot-20261127/source-inventory.json"));
            Assert.That(receipt.sourceZipSha256,
                Is.EqualTo("0d710dc65065a1435897db61a9bee2124fe8ed1989e97c738b5ee15ea9b47f0e"));
            Assert.That(receipt.sourceInventorySha256,
                Is.EqualTo("6aca3aa53a27dcba77893b24495041e16a47fb188d9cf727dbcaa2b16a4d99be"));
            Assert.That(IsSha256(receipt.snapshotSha256), Is.True);
            Assert.That(IsSha256(receipt.sourceSnapshotSha256), Is.True);
            Assert.That(receipt.snapshotSha256, Is.Not.EqualTo(receipt.sourceSnapshotSha256));
            Assert.That(PathEquals(receipt.projectRoot, projectRoot), Is.True);
            Assert.That(receipt.linuxOverlayManifestPath,
                Is.EqualTo("Logs/F10V6LinuxAcceptance-20261121/linux-overlay-manifest.json"));
            Assert.That(receipt.materializationReceiptPath, Is.EqualTo(LinuxMaterializationReceiptRelative));
            Assert.That(IsSha256(receipt.materializationReceiptSha256), Is.True);
            Assert.That(receipt.copyInventory, Is.Not.Null.And.Not.Empty);
            Assert.That(IsSha256(receipt.copyInventorySha256), Is.True);

            string manifestPath = Absolute(projectRoot, receipt.linuxOverlayManifestPath);
            AssertInsideProject(projectRoot, manifestPath);
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(HashFile(manifestPath), Is.EqualTo(receipt.linuxOverlayManifestSha256));

            string materializationPath = Absolute(projectRoot, receipt.materializationReceiptPath);
            AssertInsideProject(projectRoot, materializationPath);
            Assert.That(File.Exists(materializationPath), Is.True);
            Assert.That(HashFile(materializationPath), Is.EqualTo(receipt.materializationReceiptSha256));
            LinuxMaterializationReceipt materialization = JsonUtility.FromJson<LinuxMaterializationReceipt>(
                File.ReadAllText(materializationPath));
            Assert.That(materialization, Is.Not.Null);
            Assert.That(materialization.schema, Is.EqualTo("ci-linux-materialization-diagnostic"));
            Assert.That(materialization.schemaVersion, Is.EqualTo(1));
            Assert.That(materialization.status, Is.EqualTo("verified"));
            Assert.That(PathEquals(materialization.projectRoot, projectRoot), Is.True);
            Assert.That(materialization.unityVersion, Is.EqualTo(receipt.unityVersion));
            Assert.That(materialization.outputs, Is.Not.Null.And.Length.EqualTo(18));
            Assert.That(materialization.generatedDirectoryMetas, Is.Not.Null.And.Length.EqualTo(3));
            Assert.That(materialization.generatorFiles, Is.Not.Null);
            Assert.That(materialization.generatorFiles.Any(row => row != null &&
                row.path == "Assets/Luckbound/Editor/F10DiagnosticLinuxMaterializationReceiptExporter.cs"), Is.True);
            Assert.That(materialization.dependencyClosures, Is.Not.Null.And.Length.EqualTo(9));
            Assert.That(materialization.dependencyPaths, Is.Not.Null);
            Assert.That(materialization.dependencyFiles, Is.Not.Null);
            Assert.That(materialization.packageDependencies, Is.Not.Null);
        }

        private static void AssertSavedC1Checkpoint(ProfileSnapshot saved, C1Evidence evidence,
            bool requireC1CurrentRoute)
        {
            Assert.That(saved.Envelope.hasActiveExpedition, Is.True);
            ExpeditionSaveData expedition = saved.Envelope.activeExpedition;
            Assert.That(expedition, Is.Not.Null);
            Assert.That(expedition.runId, Is.EqualTo(evidence.RunId));
            Assert.That(expedition.currentNodeId, Is.EqualTo(E1Node));
            Assert.That(expedition.completedRoomIds, Does.Contain(C1Node));
            Assert.That(expedition.completedRoomIds.Count(id => id == C1Node), Is.EqualTo(1));
            ExpeditionRoomCompletionSaveData result = expedition.completedRoomResults.Single(item =>
                item != null && item.roomNodeId == C1Node);
            Assert.That(result.resultId, Does.StartWith("room-result.v1:"));
            StringAssert.Contains("\"outcome\":\"cleared\"", result.resultJson);
            StringAssert.Contains("\"sourceNodeId\":\"sample.patio.combat\"", result.resultJson);
            StringAssert.Contains("\"targetNodeId\":\"sample.patio.exploration\"", result.resultJson);
            string[] claims = Copy(expedition.claimedRewardIds);
            Assert.That(claims.Length, Is.GreaterThan(0));
            Assert.That(claims.All(string.IsNullOrWhiteSpace), Is.False);
            Assert.That(claims.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(claims.Length));
            if (evidence.C1ClaimsAfter != null)
                CollectionAssert.AreEqual(evidence.C1ClaimsAfter, claims);
            if (requireC1CurrentRoute)
                Assert.That(expedition.currentNodeId, Is.EqualTo(E1Node));
        }

        private static void AssertSavedC1Checkpoint(ProfileSnapshot saved, F10V6ResumeTestReceipt receipt,
            bool requireC1CurrentRoute)
        {
            Assert.That(saved.Envelope.hasActiveExpedition, Is.True);
            ExpeditionSaveData expedition = saved.Envelope.activeExpedition;
            Assert.That(expedition, Is.Not.Null);
            Assert.That(expedition.runId, Is.EqualTo(receipt.runId));
            Assert.That(expedition.currentNodeId, Is.EqualTo(receipt.currentNodeId));
            Assert.That(expedition.currentNodeId, Is.EqualTo(E1Node));
            CollectionAssert.AreEqual(receipt.c1CompletedRoomIds, expedition.completedRoomIds);
            Assert.That(expedition.completedRoomIds.Count(id => id == C1Node), Is.EqualTo(1));
            ExpeditionRoomCompletionSaveData result = expedition.completedRoomResults.Single(item =>
                item != null && item.roomNodeId == C1Node);
            Assert.That(result.resultId, Is.EqualTo(receipt.c1ResultId));
            Assert.That(result.resultJson, Is.EqualTo(receipt.c1ResultJson));
            CollectionAssert.AreEqual(receipt.c1ClaimedRewardIds, expedition.claimedRewardIds);
            Assert.That(JsonUtility.ToJson(expedition), Is.EqualTo(receipt.activeExpeditionJson));
            if (requireC1CurrentRoute)
                Assert.That(expedition.currentNodeId, Is.EqualTo(E1Node));
        }

        private static F10V6ResumeTestReceipt MakeReceipt(string projectRoot, ProfileSnapshot saved,
            SessionManifest manifest, C1Evidence evidence)
        {
            ExpeditionSaveData expedition = saved.Envelope.activeExpedition;
            ExpeditionRoomCompletionSaveData result = expedition.completedRoomResults.Single(item =>
                item != null && item.roomNodeId == C1Node);
            string expectedRelativeProfile = SessionProfileRelative(evidence.RunId);
            Assert.That(manifest.sessionRelativeDirectory, Is.EqualTo(SessionDirectoryRelative(evidence.RunId)));
            Assert.That(PathEquals(saved.Path, Absolute(projectRoot, expectedRelativeProfile)), Is.True);
            CurrentProcessIdentity(out int processId, out long processStartUtcTicks);
            return new F10V6ResumeTestReceipt
            {
                schemaVersion = 1,
                processId = processId,
                processStartUtcTicks = processStartUtcTicks,
                runId = evidence.RunId,
                sessionRelativeDirectory = manifest.sessionRelativeDirectory,
                profileRelativePath = expectedRelativeProfile,
                currentNodeId = expedition.currentNodeId,
                c1CompletedRoomIds = Copy(expedition.completedRoomIds),
                c1ResultId = result.resultId,
                c1ResultJson = result.resultJson,
                c1ClaimedRewardIds = Copy(expedition.claimedRewardIds),
                c1GoldBefore = evidence.C1GoldBefore,
                c1GoldAfter = evidence.C1GoldAfter,
                c1RenownBefore = evidence.C1RenownBefore,
                c1RenownAfter = evidence.C1RenownAfter,
                initialProfileRenown = evidence.InitialProfileRenown,
                profileSha256 = saved.Sha256,
                activeExpeditionJson = JsonUtility.ToJson(expedition),
                activeExpeditionSha256 = HashUtf8(JsonUtility.ToJson(expedition)),
                copyRoot = manifest.copyRoot,
                copyCertificateSha256 = manifest.copyCertificateSha256,
                preflightReceiptSha256 = manifest.preflightReceiptSha256,
                contentManifestSha256 = manifest.contentManifestSha256,
                routeSnapshotSha256 = manifest.routeSnapshotSha256,
                itemsCatalogSha256 = manifest.itemsCatalogSha256,
                encounterCatalogSha256 = manifest.encounterCatalogSha256,
                contentVersion = manifest.contentVersion,
                generationVersion = manifest.generationVersion,
                seed = manifest.seed
            };
        }

        private static void WriteReadOnlyReceipt(string projectRoot, F10V6ResumeTestReceipt receipt)
        {
            string path = Absolute(projectRoot, TestReceiptRelative);
            AssertInsideProject(projectRoot, path);
            Assert.That(File.Exists(path), Is.False, "Do not overwrite prior A evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            Assert.That((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0, Is.True);
        }

        private static F10V6ResumeTestReceipt ReadTestReceipt(string path)
        {
            AssertInsideProject(ProjectRoot, path);
            var receipt = JsonUtility.FromJson<F10V6ResumeTestReceipt>(File.ReadAllText(path));
            Assert.That(receipt, Is.Not.Null);
            Assert.That(receipt.schemaVersion, Is.EqualTo(1));
            Assert.That(receipt.processId, Is.GreaterThan(0));
            Assert.That(receipt.processStartUtcTicks, Is.GreaterThan(0L));
            AssertValidRunId(receipt.runId);
            Assert.That(receipt.currentNodeId, Is.EqualTo(E1Node));
            Assert.That(receipt.profileRelativePath, Is.EqualTo(SessionProfileRelative(receipt.runId)));
            Assert.That(receipt.sessionRelativeDirectory, Is.EqualTo(SessionDirectoryRelative(receipt.runId)));
            Assert.That(IsSha256(receipt.profileSha256), Is.True);
            Assert.That(IsSha256(receipt.activeExpeditionSha256), Is.True);
            Assert.That(IsSha256(receipt.copyCertificateSha256), Is.True);
            Assert.That(IsSha256(receipt.preflightReceiptSha256), Is.True);
            Assert.That(IsSha256(receipt.contentManifestSha256), Is.True);
            Assert.That(IsSha256(receipt.routeSnapshotSha256), Is.True);
            Assert.That(IsSha256(receipt.itemsCatalogSha256), Is.True);
            Assert.That(IsSha256(receipt.encounterCatalogSha256), Is.True);
            Assert.That(HashUtf8(receipt.activeExpeditionJson), Is.EqualTo(receipt.activeExpeditionSha256));
            Assert.That(receipt.c1GoldAfter, Is.EqualTo(receipt.c1GoldBefore + 24));
            Assert.That(receipt.c1RenownAfter, Is.EqualTo(receipt.c1RenownBefore + 5));
            Assert.That(receipt.c1ClaimedRewardIds, Is.Not.Empty);
            Assert.That(receipt.c1ClaimedRewardIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(receipt.c1ClaimedRewardIds.Length));
            return receipt;
        }

        private static SessionManifest ReadAndValidateManifest(string projectRoot, string runId)
        {
            AssertValidRunId(runId);
            string path = Absolute(projectRoot, SessionManifestRelative);
            AssertInsideProject(projectRoot, path);
            Assert.That(File.Exists(path), Is.True, "The API-owned session manifest is missing.");
            var manifest = JsonUtility.FromJson<SessionManifest>(File.ReadAllText(path));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.schemaVersion, Is.EqualTo(1));
            Assert.That(manifest.status, Is.EqualTo("active"));
            Assert.That(manifest.runId, Is.EqualTo(runId));
            Assert.That(manifest.copyRoot, Is.Not.Empty);
            Assert.That(PathEquals(manifest.copyRoot, projectRoot), Is.True,
                "The session manifest must be anchored to this exact project copy.");
            Assert.That(manifest.sessionRelativeDirectory, Is.EqualTo(SessionDirectoryRelative(runId)));
            Assert.That(manifest.contentVersion, Is.EqualTo(6));
            Assert.That(manifest.generationVersion, Is.EqualTo(2));
            Assert.That(IsSha256(manifest.copyCertificateSha256), Is.True);
            Assert.That(IsSha256(manifest.preflightReceiptSha256), Is.True);
            Assert.That(IsSha256(manifest.contentManifestSha256), Is.True);
            Assert.That(IsSha256(manifest.routeSnapshotSha256), Is.True);
            Assert.That(IsSha256(manifest.itemsCatalogSha256), Is.True);
            Assert.That(IsSha256(manifest.encounterCatalogSha256), Is.True);
            Assert.That(HashUtf8(manifest.routeSnapshotJson), Is.EqualTo(manifest.routeSnapshotSha256));
            Assert.That(File.Exists(SessionProfilePath(projectRoot, runId)), Is.True,
                "The genuine Profile V6 must exist at its bounded RunId-scoped path.");
            return manifest;
        }

        private static void AssertManifestMatchesReceipt(SessionManifest manifest,
            F10V6ResumeTestReceipt receipt)
        {
            Assert.That(manifest.runId, Is.EqualTo(receipt.runId));
            Assert.That(manifest.sessionRelativeDirectory, Is.EqualTo(receipt.sessionRelativeDirectory));
            Assert.That(PathEquals(manifest.copyRoot, receipt.copyRoot), Is.True);
            Assert.That(manifest.copyCertificateSha256, Is.EqualTo(receipt.copyCertificateSha256));
            Assert.That(manifest.preflightReceiptSha256, Is.EqualTo(receipt.preflightReceiptSha256));
            Assert.That(manifest.contentManifestSha256, Is.EqualTo(receipt.contentManifestSha256));
            Assert.That(manifest.routeSnapshotSha256, Is.EqualTo(receipt.routeSnapshotSha256));
            Assert.That(manifest.itemsCatalogSha256, Is.EqualTo(receipt.itemsCatalogSha256));
            Assert.That(manifest.encounterCatalogSha256, Is.EqualTo(receipt.encounterCatalogSha256));
            Assert.That(manifest.contentVersion, Is.EqualTo(receipt.contentVersion));
            Assert.That(manifest.generationVersion, Is.EqualTo(receipt.generationVersion));
            Assert.That(manifest.seed, Is.EqualTo(receipt.seed));
        }

        private static IEnumerator WaitForNode(RouteContext context, string expected,
            double timeoutSeconds, int maxFrames)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            for (int frame = 0; frame < maxFrames && Time.realtimeSinceStartupAsDouble < deadline &&
                CurrentNode(context) != expected; frame++) yield return null;
            AssertNode(context, expected, "route transition");
        }

        private static F10DiagnosticSceneRunner FindRunner(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                F10DiagnosticSceneRunner runner = root.GetComponentInChildren<F10DiagnosticSceneRunner>(true);
                if (runner != null) return runner;
            }
            return null;
        }

        private static void AssertNode(RouteContext context, string expected, string stage)
        {
            string actual = CurrentNode(context);
            if (context.RouteTrace.Count == 0 || context.RouteTrace[context.RouteTrace.Count - 1] != actual)
            {
                context.RouteTrace.Add(actual);
                TestContext.WriteLine("Observed route node: " + actual + " at " + stage);
            }
            Assert.That(actual, Is.EqualTo(expected),
                "Expected route node " + expected + ", observed " + actual + "; stage=" + stage +
                "; trace=" + string.Join(" -> ", context.RouteTrace));
        }

        private static string CurrentNode(RouteContext context) => context?.Runner == null ||
            context.Runner.Composition == null || context.Runner.Composition.RoomTransitions == null
            ? "<composition unavailable>"
            : context.Runner.Composition.RoomTransitions.CurrentNodeId;

        private static void CloseDiagnostic(RouteContext context)
        {
            if (context == null) return;
            ReleaseAll(context);
            if (context.PreviousCaptureDelta > 0f || context.PreviousCaptureDelta == 0f)
                Time.captureDeltaTime = context.PreviousCaptureDelta;
            if (context.Keyboard != null && context.Keyboard.added) InputSystem.RemoveDevice(context.Keyboard);
            if (context.PreviousScene.IsValid() && context.PreviousScene.isLoaded)
                SceneManager.SetActiveScene(context.PreviousScene);
            if (context.DiagnosticScene.IsValid() && context.DiagnosticScene.isLoaded)
                SceneManager.UnloadSceneAsync(context.DiagnosticScene);
        }

        private static void SetKey(RouteContext context, Key key, bool pressed)
        {
            if (pressed ? context.Keys.Add(key) : context.Keys.Remove(key))
                InputSystem.QueueStateEvent(context.Keyboard, new KeyboardState(context.Keys.ToArray()));
        }

        private static void ReleaseAll(RouteContext context)
        {
            if (context?.Keyboard == null || !context.Keyboard.added) return;
            context.Keys.Clear();
            InputSystem.QueueStateEvent(context.Keyboard, new KeyboardState());
        }

        private static int CalculateLootRenown(ExpeditionInventory inventory,
            IReadOnlyList<ItemInstanceId> startingEquipmentInstanceIds)
        {
            Assert.That(inventory, Is.Not.Null);
            var starting = new HashSet<ItemInstanceId>(startingEquipmentInstanceIds ?? Array.Empty<ItemInstanceId>());
            var counted = new HashSet<ItemInstanceId>();
            long value = inventory.Gold / 20;
            value += inventory.GetMaterialSnapshot().Values.Sum(count => (long)count) / 5;
            IEnumerable<InventoryItemStack> items = inventory.Backpack.Concat(new[]
                { inventory.Weapon, inventory.Armor, inventory.RelicOne, inventory.RelicTwo });
            foreach (InventoryItemStack item in items)
            {
                if (item == null || starting.Contains(item.InstanceId) || !counted.Add(item.InstanceId)) continue;
                ItemDefinition definition = inventory.Catalog.Get(item.ItemId);
                switch (definition.Kind)
                {
                    case ItemKind.Weapon: value += RarityValue(item.Rarity, 5, 8, 12); break;
                    case ItemKind.Armor: value += RarityValue(item.Rarity, 4, 6, 9); break;
                    case ItemKind.Relic: value += 8; break;
                    case ItemKind.Consumable:
                    case ItemKind.Material: break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            return checked((int)value);
        }

        private static int RarityValue(ItemRarity rarity, int common, int rare, int epic)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return common;
                case ItemRarity.Rare: return rare;
                case ItemRarity.Epic: return epic;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Absolute(string root, string relative) => Path.GetFullPath(Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        private static string SessionDirectoryRelative(string runId) =>
            ResumeRootRelative + "/sessions/" + runId;
        private static string SessionProfileRelative(string runId) =>
            SessionDirectoryRelative(runId) + "/luckbound-profile.json";
        private static string SessionProfilePath(string root, string runId) =>
            Absolute(root, SessionProfileRelative(runId));

        private static void AssertInsideProject(string projectRoot, string path)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(path);
            Assert.That(candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True,
                "Access must remain below the disposable project root: " + candidate);
        }

        private static bool PathEquals(string left, string right) =>
            string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        private static void AssertValidRunId(string value)
        {
            Assert.That(RunId.TryParse(value, out RunId parsed), Is.True);
            Assert.That(parsed.ToString(), Is.EqualTo(value));
        }

        private static string[] Copy(string[] values) => values == null ? Array.Empty<string>() : (string[])values.Clone();

        private static string HashFile(string path) => HashBytes(File.ReadAllBytes(path));
        private static string HashUtf8(string value) => HashBytes(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private static bool IsSha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

        private sealed class RouteContext
        {
            public Scene PreviousScene;
            public Scene DiagnosticScene;
            public Keyboard Keyboard;
            public readonly HashSet<Key> Keys = new HashSet<Key>();
            public float PreviousCaptureDelta;
            public F10DiagnosticSceneRunner Runner;
            public ApplicationBootstrapper App;
            public PlayerMotor2D Player;
            public readonly List<string> RouteTrace = new List<string>();
        }

        private sealed class ProfileSnapshot
        {
            public string Path;
            public string Json;
            public string Sha256;
            public ProfileSaveEnvelope Envelope;
        }

        private sealed class C1Evidence
        {
            public string RunId;
            public int InitialProfileRenown;
            public int C1GoldBefore;
            public int C1GoldAfter;
            public int C1RenownBefore;
            public int C1RenownAfter;
            public string[] C1ClaimsBefore;
            public string[] C1ClaimsAfter;
            public int CompletedWaveCount;
            public int SeenSlimeCount;
            public int SeenBoneguardCount;
            public bool AllSlimesDied;
            public bool AllBoneguardsDied;
            public int TotalCombatImpacts;
            public int AttackInputs;
            public int DodgeObservations;
            public int JumpInputs;
            public int[] WavesObserved;
        }

        private static void CurrentProcessIdentity(out int processId, out long processStartUtcTicks)
        {
            using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
            {
                processId = process.Id;
                processStartUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            }
        }

        [Serializable]
        private sealed class F10DurableRoomResult
        {
            public int schema;
            public string outcome;
            public string roomInstanceId;
            public string sourceNodeId;
            public string targetNodeId;
            public string technicalPayment;
        }

        [Serializable]
        private sealed class SessionManifest
        {
            public int schemaVersion;
            public string status;
            public string copyRoot;
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
        }

        [Serializable]
        private sealed class LinuxDiagnosticReceipt
        {
            public string schema;
            public int schemaVersion;
            public string status;
            public string unityVersion;
            public string baseOverlayManifestSha256;
            public string linuxOverlayManifestPath;
            public string linuxOverlayManifestSha256;
            public string baseSnapshotPath;
            public string sourceInventoryPath;
            public string sourceZipSha256;
            public string sourceInventorySha256;
            public string snapshotSha256;
            public string sourceSnapshotSha256;
            public string projectRoot;
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
            public LinuxInventoryRow[] copyInventory;
            public string materializationReceiptPath;
            public string materializationReceiptSha256;
        }

        [Serializable]
        private sealed class LinuxInventoryRow
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
        private sealed class LinuxMaterializationReceipt
        {
            public string schema;
            public int schemaVersion;
            public string status;
            public string projectRoot;
            public string unityVersion;
            public LinuxInventoryRow[] outputs;
            public LinuxInventoryRow[] generatedDirectoryMetas;
            public LinuxGeneratorFile[] generatorFiles;
            public LinuxDependencyClosure[] dependencyClosures;
            public string[] dependencyPaths;
            public LinuxInventoryRow[] dependencyFiles;
            public LinuxPackageDependency[] packageDependencies;
        }

        [Serializable]
        private sealed class LinuxGeneratorFile { public string path; public string sha256; }
        [Serializable]
        private sealed class LinuxDependencyClosure { public string assetPath; public string[] dependencyPaths; }

        [Serializable]
        private sealed class LinuxPackageDependency
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
            public LinuxPackageDependencyFile[] files;
        }

        [Serializable]
        private sealed class LinuxPackageDependencyFile
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

        [Serializable]
        private sealed class F10V6ResumeTestReceipt
        {
            public int schemaVersion;
            public int processId;
            public long processStartUtcTicks;
            public string runId;
            public string sessionRelativeDirectory;
            public string profileRelativePath;
            public string currentNodeId;
            public string[] c1CompletedRoomIds;
            public string c1ResultId;
            public string c1ResultJson;
            public string[] c1ClaimedRewardIds;
            public int c1GoldBefore;
            public int c1GoldAfter;
            public int c1RenownBefore;
            public int c1RenownAfter;
            public int initialProfileRenown;
            public string profileSha256;
            public string activeExpeditionJson;
            public string activeExpeditionSha256;
            public string copyRoot;
            public string copyCertificateSha256;
            public string preflightReceiptSha256;
            public string contentManifestSha256;
            public string routeSnapshotSha256;
            public string itemsCatalogSha256;
            public string encounterCatalogSha256;
            public int contentVersion;
            public int generationVersion;
            public uint seed;
        }
    }
}
#endif
