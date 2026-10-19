using System.Collections;
using System.Globalization;
using Luckbound.App;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>Exercises the B07 menu-to-portal flow through real Input System and 2D physics updates.</summary>
    public sealed class MovementDemoFlowTests : LuckboundInputTestFixture
    {
        private const int ScenarioSeed = 31415926;
        private static readonly float[] BarrierCenters = { -20f, 0f, 20f };

        [UnityTest]
        public IEnumerator MenuDemoClearsThreeBarriersDodgesActivatesPortalAndReturnsToMenu()
        {
            UnityRandom.State previousRandomState = UnityRandom.state;
            var evidence = new ScenarioEvidenceRecorder("B07", "B07_MovementDemoFlow", ScenarioSeed);
            Keyboard keyboard = null;
            Mouse mouse = null;

            try
            {
                UnityRandom.InitState(ScenarioSeed);
                Time.timeScale = 1f;
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.Update();

                evidence.RecordEvent("Load the Menu scene and use its actual demo button binding.");
                evidence.RecordInput(0, "LoadScene", "Menu");
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
                for (int frame = 0; frame < 20 && SceneManager.GetActiveScene().name != "Menu"; frame++)
                {
                    yield return null;
                }

                GameObject startButtonObject = GameObject.Find("StartMovementDemoButton");
                Button startButton = startButtonObject == null ? null : startButtonObject.GetComponent<Button>();
                evidence.RecordAssertion("Menu exposes a playable demo button", startButton != null,
                    "StartMovementDemoButton with Button", startButton == null ? "missing" : "present");
                if (startButton == null)
                {
                    int missingButtonExitCode = evidence.Complete();
                    Assert.That(missingButtonExitCode, Is.Zero, "The menu button assertion failed. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                // This scenario invokes the real Button callback directly; pointer/navigation input is covered separately.
                DisableSceneUiInputModules();
                evidence.RecordInput(1, "MenuButton", "StartMovementDemoButton.onClick");
                startButton.onClick.Invoke();
                for (int frame = 0; frame < 20 && SceneManager.GetActiveScene().name != "Expedition"; frame++)
                {
                    yield return null;
                }

                evidence.RecordAssertion("Demo button opens Expedition",
                    SceneManager.GetActiveScene().name == "Expedition", "Expedition",
                    SceneManager.GetActiveScene().name);

                MovementDemoComposition composition = null;
                for (int frame = 0; frame < 20 && composition == null; frame++)
                {
                    composition = Object.FindAnyObjectByType<MovementDemoComposition>();
                    if (composition == null) yield return null;
                }

                evidence.RecordAssertion("Expedition composes the player and movement course",
                    composition != null && composition.PlayerMotor != null,
                    "MovementDemoComposition with initialized PlayerMotor2D",
                    composition == null ? "composition missing" : composition.PlayerMotor == null ? "player not initialized" : "initialized");
                if (composition == null || composition.PlayerMotor == null)
                {
                    int missingCompositionExitCode = evidence.Complete();
                    Assert.That(missingCompositionExitCode, Is.Zero, "The composition assertion failed. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                DisableSceneUiInputModules();
                SetKeyboardState(keyboard);
                AudioListener[] listeners = Object.FindObjectsByType<AudioListener>();
                string listenerDescription = string.Join(", ", System.Array.ConvertAll(listeners,
                    listener => listener.gameObject.scene.name + "/" + listener.gameObject.name +
                        (listener.enabled && listener.gameObject.activeInHierarchy ? " (active)" : " (inactive)")));
                evidence.RecordEvent("AudioListener instances after entering Expedition: " + listenerDescription + ".");
                evidence.RecordAssertion("Expedition has one active audio listener",
                    listeners.Length == 1 && listeners[0].enabled && listeners[0].gameObject.activeInHierarchy,
                    "one listener", listeners.Length + ": " + listenerDescription);

                PlayerMotor2D motor = composition.PlayerMotor;
                int finalJumpCount = 0;
                int finalDodgeCount = 0;
                float fixedDelta = Time.fixedDeltaTime;
                evidence.RecordAssertion("Course uses the B05 60 Hz simulation step",
                    Mathf.Abs(fixedDelta - (1f / 60f)) <= 0.000001f,
                    "1/60 s", fixedDelta.ToString("F9", CultureInfo.InvariantCulture) + " s");

                for (int frame = 0; frame < 10; frame++) yield return new WaitForFixedUpdate();

                SetKeyboardState(keyboard, Key.D);
                evidence.RecordInput(2, "MoveX", "D held");

                bool courseTraversed = true;
                int evidenceFrame = 3;
                for (int barrierIndex = 0; barrierIndex < BarrierCenters.Length; barrierIndex++)
                {
                    float center = BarrierCenters[barrierIndex];
                    yield return MoveToX(composition, center - 3.2f, 400);
                    float launchX = composition.PlayerObject.transform.position.x;
                    if (launchX < center - 3.2f)
                    {
                        evidence.RecordAssertion("Reach launch point before barrier " + (barrierIndex + 1), false,
                            "x >= " + (center - 3.2f).ToString("F1", CultureInfo.InvariantCulture),
                            launchX.ToString("F3", CultureInfo.InvariantCulture));
                        courseTraversed = false;
                        break;
                    }

                    if (barrierIndex == 2)
                    {
                        yield return MoveToX(composition, center - 3.2f, 400);
                    }

                    int jumpsBefore = motor.SuccessfulJumpCount;
                    evidence.RecordEvent("Jump toward physical static barrier " + (barrierIndex + 1) +
                        " at x=" + launchX.ToString("F3", CultureInfo.InvariantCulture) + ".");
                    evidence.RecordInput(evidenceFrame++, "Jump", "D + Space held for 18 fixed ticks");
                    SetKeyboardState(keyboard, Key.D, Key.Space);
                    for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
                    SetKeyboardState(keyboard, Key.D);
                    evidence.RecordInput(evidenceFrame++, "Jump", "Space released; D remains held");

                    yield return MoveToX(composition, center + 2.5f, 180);
                    float clearedX = composition.PlayerObject.transform.position.x;
                    bool clearedBarrier = clearedX >= center + 2.5f;
                    bool jumpRegistered = motor.SuccessfulJumpCount == jumpsBefore + 1;
                    evidence.RecordAssertion("Physically clear barrier " + (barrierIndex + 1), clearedBarrier,
                        "player center x >= " + (center + 2.5f).ToString("F1", CultureInfo.InvariantCulture),
                        clearedX.ToString("F3", CultureInfo.InvariantCulture));
                    evidence.RecordAssertion("Jump " + (barrierIndex + 1) + " starts in Gameplay",
                        jumpRegistered, "jump counter increments once",
                        jumpsBefore + " -> " + motor.SuccessfulJumpCount);
                    if (!clearedBarrier || !jumpRegistered)
                    {
                        courseTraversed = false;
                        break;
                    }

                    if (barrierIndex == 1)
                    {
                        yield return MoveToX(composition, 10f, 240);
                        evidence.RecordInput(evidenceFrame++, "Dodge", "D + Left Shift pressed");
                        SetKeyboardState(keyboard, Key.D, Key.LeftShift);
                        yield return new WaitForFixedUpdate();
                        SetKeyboardState(keyboard, Key.D);
                        evidence.RecordInput(evidenceFrame++, "Dodge", "Left Shift released; D remains held");
                        evidence.RecordAssertion("Dodge action activates in the course",
                            motor.DodgeActivationCount >= 1, "at least one activation",
                            motor.DodgeActivationCount.ToString(CultureInfo.InvariantCulture));
                        evidence.RecordEvent("dodge position=" +
                            composition.PlayerObject.transform.position.x.ToString("F3", CultureInfo.InvariantCulture) +
                            ", activation count=" + motor.DodgeActivationCount + ".");
                    }
                }

                bool threeJumps = motor.SuccessfulJumpCount >= 3;
                bool oneDodge = motor.DodgeActivationCount >= 1;
                finalJumpCount = motor.SuccessfulJumpCount;
                finalDodgeCount = motor.DodgeActivationCount;
                evidence.RecordAssertion("Three real jumps are registered", courseTraversed && threeJumps,
                    "SuccessfulJumpCount >= 3", motor.SuccessfulJumpCount.ToString(CultureInfo.InvariantCulture));
                evidence.RecordAssertion("One real dodge is registered", courseTraversed && oneDodge,
                    "DodgeActivationCount >= 1", motor.DodgeActivationCount.ToString(CultureInfo.InvariantCulture));

                if (courseTraversed && threeJumps && oneDodge)
                {
                    evidence.RecordInput(evidenceFrame++, "MoveX", "Continue holding D to the portal");
                    yield return MoveToX(composition, 40f, 1200);
                    for (int frame = 0; frame < 10 && !composition.IsCourseComplete; frame++)
                    {
                        yield return new WaitForFixedUpdate();
                        yield return null;
                    }

                    evidence.RecordEvent("portal entered=" + composition.IsInsideGoal +
                        ", course complete=" + composition.IsCourseComplete + ".");
                    evidence.RecordAssertion("Player reaches the real portal trigger",
                        composition.IsInsideGoal, "inside MovementDemoGoal trigger",
                        composition.IsInsideGoal ? "inside" : "outside at x=" +
                        composition.PlayerObject.transform.position.x.ToString("F3", CultureInfo.InvariantCulture));
                    evidence.RecordAssertion("Portal verifies movement objectives",
                        composition.IsCourseComplete, "course complete after three jumps and one dodge",
                        composition.IsCourseComplete ? "complete" : "incomplete");

                    if (composition.IsCourseComplete)
                    {
                        float completedPositionX = composition.PlayerObject.transform.position.x;
                        int completedJumpCount = motor.SuccessfulJumpCount;
                        int completedDodgeCount = motor.DodgeActivationCount;
                        evidence.RecordInput(evidenceFrame, "MenuButton", "ReturnToMenuButton.onClick");
                        GameObject returnButtonObject = GameObject.Find("ReturnToMenuButton");
                        Button returnButton = returnButtonObject == null ? null : returnButtonObject.GetComponent<Button>();
                        evidence.RecordAssertion("Course presents a return button", returnButton != null,
                            "ReturnToMenuButton with Button", returnButton == null ? "missing" : "present");
                        if (returnButton != null)
                        {
                            // Clear held movement before the Menu UI module enables its actions again.
                            evidence.RecordInput(evidenceFrame++, "MoveX", "release all keys before returning to Menu");
                            SetKeyboardState(keyboard);
                            if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState());
                            InputSystem.Update();
                            yield return null;
                            DisableSceneUiInputModules();
                            returnButton.onClick.Invoke();
                        }

                        for (int frame = 0; frame < 20 && SceneManager.GetActiveScene().name != "Menu"; frame++)
                        {
                            yield return null;
                        }
                        DisableSceneUiInputModules();
                        evidence.RecordAssertion("Return action loads the Menu scene",
                            SceneManager.GetActiveScene().name == "Menu", "Menu",
                            SceneManager.GetActiveScene().name);

                        evidence.RecordEvent("completed at x=" + completedPositionX.ToString("F3", CultureInfo.InvariantCulture) +
                            ", jumps=" + completedJumpCount + ", dodges=" + completedDodgeCount + ".");
                    }
                }

                evidence.RecordEvent("final scene=" + SceneManager.GetActiveScene().name +
                    ", jumps=" + finalJumpCount + ", dodges=" + finalDodgeCount + ".");
                int exitCode = evidence.Complete();
                Assert.That(exitCode, Is.Zero, "The physical B07 flow failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                UnityRandom.state = previousRandomState;
                Time.timeScale = 1f;
            }
        }

        private static IEnumerator MoveToX(MovementDemoComposition composition, float targetX, int maximumFixedTicks)
        {
            for (int tick = 0; tick < maximumFixedTicks; tick++)
            {
                if (composition == null || composition.PlayerObject == null ||
                    composition.PlayerObject.transform.position.x >= targetX)
                {
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }
        }

        private static void SetKeyboardState(Keyboard keyboard, params Key[] pressedKeys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressedKeys));
        }

        private static void DisableSceneUiInputModules()
        {
            InputSystemUIInputModule[] modules = Object.FindObjectsByType<InputSystemUIInputModule>();
            foreach (InputSystemUIInputModule module in modules)
            {
                if (module != null && module.enabled) module.enabled = false;
            }
        }
    }
}
