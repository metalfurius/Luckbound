using System.Collections;
using Luckbound.App;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Input;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerHudConsumableTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator HudDisplaysLiveResourcesAndHealthPotionHealsAfterPreparation()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C07", "C07_HudAndHealthPotion", 17320508))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateFloor("C07_PotionGround", groundLayer);
                PlayerFixture player = CreatePlayer("C07_PotionPlayer", new Vector2(0f, 1.5f), groundLayer);
                Text statusText = CreateText("C07_HudText");
                var presenterObject = new GameObject("C07_PlayerHudPresenter");
                PlayerHudPresenter presenter = presenterObject.AddComponent<PlayerHudPresenter>();
                presenter.Initialize(player.Motor, statusText);
                yield return new WaitForFixedUpdate();

                evidence.RecordAssertion("Player starts with full energy shown by the HUD",
                    player.Motor.CurrentEnergy == player.Motor.MaximumEnergy && presenter.CurrentText.Contains("ENERGÍA 50/50"),
                    "energy=50/50", presenter.CurrentText.Replace("\n", " | "));
                player.Motor.SkillUses.SetCurrentEnergy(37);
                DirectImpactOutcome damage = player.Motor.ReceiveDirectImpact(
                    HitId.New(), Request(player.Motor.CombatStats, 40m), player.Motor.SimulationTick, Vector2.zero);
                presenter.Refresh();
                evidence.RecordAssertion("HUD reads current health, energy and initial potion charges",
                    presenter.CurrentText.Contains("VIDA 60/100") && presenter.CurrentText.Contains("ENERGÍA 37/50") &&
                    presenter.CurrentText.Contains("POCIONES 2/3"),
                    "health=60/100; energy=37/50; potions=2/3", presenter.CurrentText.Replace("\n", " | "));

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                evidence.RecordInput(1, "Consumable", "start health potion at 60/100 health");
                evidence.RecordAssertion("Potion preparation leaves health and charges unchanged",
                    player.Motor.IsPreparingHealthPotion && player.Motor.HealthPotionPreparationTicksRemaining ==
                        HealthPotionUseState.PreparationDurationTicks && player.Motor.CurrentHealth == 60 &&
                    player.Motor.HealthPotionCharges == 2,
                    "preparing for 21 ticks; health=60; charges=2",
                    "preparing=" + player.Motor.IsPreparingHealthPotion + "; ticks=" +
                    player.Motor.HealthPotionPreparationTicksRemaining + "; health=" + player.Motor.CurrentHealth +
                    "; charges=" + player.Motor.HealthPotionCharges);

                for (int tick = 0; tick < HealthPotionUseState.PreparationDurationTicks - 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool oneTickRemaining = player.Motor.IsPreparingHealthPotion &&
                    player.Motor.HealthPotionPreparationTicksRemaining == 1 &&
                    player.Motor.CurrentHealth == 60 && player.Motor.HealthPotionCharges == 2;
                yield return new WaitForFixedUpdate();
                evidence.RecordAssertion("Potion heals 25 only when the 21st preparation tick completes",
                    oneTickRemaining && !player.Motor.IsPreparingHealthPotion && player.Motor.CurrentHealth == 85 &&
                    player.Motor.HealthPotionCharges == 1,
                    "no healing after 20 ticks; health=85 and charges=1 at tick 21",
                    "oneTickRemaining=" + oneTickRemaining + "; preparing=" + player.Motor.IsPreparingHealthPotion +
                    "; health=" + player.Motor.CurrentHealth + "; charges=" + player.Motor.HealthPotionCharges);
                presenter.Refresh();
                evidence.RecordAssertion("HUD reflects completed healing and remaining potion charges",
                    presenter.CurrentText.Contains("VIDA 85/100") && presenter.CurrentText.Contains("POCIONES 1/3"),
                    "health=85/100; potions=1/3", presenter.CurrentText.Replace("\n", " | "));

                while (player.Motor.IsInvulnerable)
                {
                    yield return new WaitForFixedUpdate();
                }

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                DirectImpactOutcome interruptingHit = player.Motor.ReceiveDirectImpact(
                    HitId.New(), Request(player.Motor.CombatStats, 10m), player.Motor.SimulationTick + 1, Vector2.zero);
                yield return new WaitForFixedUpdate();
                evidence.RecordAssertion("An accepted hit interrupts the next potion without consuming it",
                    interruptingHit.Status == DirectImpactStatus.Applied && !player.Motor.IsPreparingHealthPotion &&
                    player.Motor.HealthPotionCharges == 1 && player.Motor.CurrentHealth == 75,
                    "hit accepted; preparation cancelled; charges=1; health=75",
                    "hit=" + interruptingHit.Status + "; preparing=" + player.Motor.IsPreparingHealthPotion +
                    "; charges=" + player.Motor.HealthPotionCharges + "; health=" + player.Motor.CurrentHealth);
                while (player.Motor.IsInvulnerable)
                {
                    yield return new WaitForFixedUpdate();
                }

                player.Input.Press(PlayerAction.Consumable);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Consumable);
                player.Input.Press(PlayerAction.Dodge);
                yield return new WaitForFixedUpdate();
                player.Input.Release(PlayerAction.Dodge);
                evidence.RecordAssertion("Accepted dodge interrupts preparation without consuming a charge",
                    !player.Motor.IsPreparingHealthPotion && player.Motor.HealthPotionCharges == 1 &&
                    player.Motor.DodgeActivationCount == 1,
                    "preparation cancelled; charges=1; dodge accepted",
                    "preparing=" + player.Motor.IsPreparingHealthPotion + "; charges=" +
                    player.Motor.HealthPotionCharges + "; dodges=" + player.Motor.DodgeActivationCount);

                evidence.RecordAssertion("Initial health damage was accepted for the live HUD scenario",
                    damage.Status == DirectImpactStatus.Applied,
                    "direct impact applied", damage.Status.ToString());
                int exitCode = evidence.Complete();

                Object.Destroy(presenterObject);
                Object.Destroy(statusText.gameObject);
                player.Destroy();
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C07 HUD and potion scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator PauseAndFocusLossFreezePhysicsUntilResume()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C07", "C07_PauseAndFocusLoss", 22360679))
            {
                Time.timeScale = 1f;
                Text pauseText = CreateText("C07_PauseLabel");
                var pauseObject = new GameObject("C07_PauseController");
                ExpeditionPauseController pause = pauseObject.AddComponent<ExpeditionPauseController>();
                pause.Initialize(pauseText, pauseOnFocusLoss: true);

                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                using (PlayerInputSource pauseInput = PlayerInputSource.CreateDefault(ActorId.New()))
                {
                    pauseInput.PausePressed += pause.TogglePause;

                    int groundLayer = LayerMask.NameToLayer("Ground");
                    GameObject floor = CreateFloor("C07_PauseGround", groundLayer);
                    PlayerFixture player = CreatePlayer("C07_PausePlayer", new Vector2(0f, 1.5f), groundLayer);
                    var probeObject = new GameObject("C07_PausePhysicsProbe");
                    Rigidbody2D probe = probeObject.AddComponent<Rigidbody2D>();
                    probe.gravityScale = 0f;
                    probe.linearVelocity = Vector2.right;
                    yield return new WaitForFixedUpdate();
                    long beforePause = player.Motor.SimulationTick;
                    float probePositionBeforePause = probe.position.x;
                    pause.HandleApplicationFocus(hasFocus: false);
                    evidence.RecordInput(1, "ApplicationFocus", "lost");
                    yield return new WaitForSecondsRealtime(0.1f);
                    long whilePaused = player.Motor.SimulationTick;
                    float probePositionWhilePaused = probe.position.x;
                    evidence.RecordAssertion("Focus loss pauses scaled physics and presents a resume prompt",
                        pause.IsPaused && Time.timeScale == 0f && Physics2D.simulationMode == SimulationMode2D.Script &&
                        whilePaused == beforePause && Mathf.Abs(probePositionWhilePaused - probePositionBeforePause) <= 0.0001f &&
                        pauseText.text.Contains("PAUSA"),
                        "paused; timeScale=0; 2D simulation suspended; player tick and body position unchanged; pause text visible",
                        "paused=" + pause.IsPaused + "; timeScale=" + Time.timeScale + "; before=" + beforePause +
                        "; after=" + whilePaused + "; probeBefore=" + probePositionBeforePause +
                        "; probeAfter=" + probePositionWhilePaused + "; text=" + pauseText.text);

                    evidence.RecordInput(2, "Keyboard", "Escape key press dispatched pause event");
                    Press(keyboard.escapeKey);
                    yield return new WaitForFixedUpdate();
                    long afterResume = player.Motor.SimulationTick;
                    float probePositionAfterResume = probe.position.x;
                    evidence.RecordAssertion("Escape resumes physics through the input event outside FixedUpdate",
                        !pause.IsPaused && Time.timeScale == 1f && Physics2D.simulationMode == SimulationMode2D.FixedUpdate &&
                        afterResume > whilePaused && probePositionAfterResume > probePositionWhilePaused && pauseText.text.Length == 0,
                        "resumed; timeScale=1; 2D simulation and player tick advanced; pause text cleared",
                        "paused=" + pause.IsPaused + "; timeScale=" + Time.timeScale + "; afterResumeTick=" + afterResume +
                        "; probeAfterResume=" + probePositionAfterResume + "; text='" + pauseText.text + "'");
                    int exitCode = evidence.Complete();

                    player.Destroy();
                    Object.Destroy(floor);
                    Object.Destroy(probeObject);
                    Assert.That(exitCode, Is.Zero, "C07 pause scenario failed. Evidence: " + evidence.ReportPath);
                }

                Object.Destroy(pauseObject);
                Object.Destroy(pauseText.gameObject);
                Time.timeScale = 1f;
                yield return null;
            }
        }

        private static PlayerFixture CreatePlayer(string name, Vector2 position, int groundLayer)
        {
            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject(name);
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, 1 << groundLayer);
            Physics2D.SyncTransforms();
            return new PlayerFixture(player, motor, input);
        }

        private static GameObject CreateFloor(string name, int groundLayer)
        {
            var floor = new GameObject(name);
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(30f, 1f);
            return floor;
        }

        private static Text CreateText(string name)
        {
            var textObject = new GameObject(name);
            return textObject.AddComponent<Text>();
        }

        private static DamageCalculationRequest Request(CombatStats target, decimal damage)
        {
            return new DamageCalculationRequest(
                baseDamage: damage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: target,
                targetIsInvulnerable: false);
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(GameObject gameObject, PlayerMotor2D motor, PlayerInputBuffer input)
            {
                GameObject = gameObject;
                Motor = motor;
                Input = input;
            }

            public GameObject GameObject { get; }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }

            public void Destroy()
            {
                Object.Destroy(GameObject);
            }
        }
    }
}
