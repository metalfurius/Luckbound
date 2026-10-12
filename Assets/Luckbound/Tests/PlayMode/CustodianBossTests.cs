using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class CustodianBossTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();
        private bool _leftHeld;
        private bool _rightHeld;

        [Test]
        public void D06BossJsonMaintainsThePlanValuesAndRejectsAChangedAttack()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Encounters/CustodianBoss");
            Assert.That(source, Is.Not.Null);
            var definition = CustodianBossJsonLoader.Load(source.text);
            Assert.That(definition.MaximumHealth, Is.EqualTo(900));
            Assert.That(definition.Defense, Is.EqualTo(3));
            Assert.That(definition.TelegraphTicks, Is.GreaterThanOrEqualTo(33));
            Assert.That(definition.PhaseTransitionTicks, Is.EqualTo(120));
            Assert.That(definition.PhaseOneRecoveryTicks, Is.GreaterThan(definition.PhaseTwoRecoveryTicks));
            Assert.That(definition.CutDamage, Is.EqualTo(14));
            Assert.That(definition.WaveDamage, Is.EqualTo(12));
            Assert.That(definition.ChargeDamage, Is.EqualTo(16));
            Assert.That(definition.PhaseTwoWaveCount, Is.EqualTo(2));

            string changedAttack = source.text.Replace("\"chargeDamage\": 16", "\"chargeDamage\": 15");
            Assert.Throws<System.FormatException>(() => CustodianBossJsonLoader.Load(changedAttack));
        }

        [UnityTest]
        public IEnumerator D06PlayerReachesTheCustodianAndWinsWithRealCombatThroughBothPhases()
        {
            using (var evidence = new ScenarioEvidenceRecorder("D06", "D06_RealCombatCustodianTwoPhases", 31415927))
            {
                _pressedKeys.Clear();
                _leftHeld = false;
                _rightHeld = false;
                _keyboard = InputSystem.AddDevice<Keyboard>();
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
                yield return null;

                Button bossButton = GameObject.Find("StartCustodianArenaButton")?.GetComponent<Button>();
                bool menuEntryAvailable = bossButton != null && bossButton.interactable;
                if (menuEntryAvailable) bossButton.onClick.Invoke();
                evidence.RecordInput(0, "MenuButton", "ENFRENTAR · CUSTODIO (D06)");
                for (int frame = 0; frame < 120 && SceneManager.GetActiveScene().name != "CustodianArena"; frame++) yield return null;
                for (int frame = 0; frame < 180 && Object.FindAnyObjectByType<CustodianArenaComposition>() == null; frame++) yield return null;

                CustodianArenaComposition arena = Object.FindAnyObjectByType<CustodianArenaComposition>();
                Assert.That(arena, Is.Not.Null, "The D06 arena must load from its visible menu entry.");
                Assert.That(menuEntryAvailable, Is.True);
                Assert.That(arena.Boss, Is.Not.Null);
                PlayerMotor2D player = arena.PlayerMotor;
                CustodianBoss2D boss = arena.Boss;
                Assert.That(boss.MaximumHealth, Is.EqualTo(900));
                Assert.That(boss.CurrentHealth, Is.EqualTo(900));
                Assert.That(boss.Defense, Is.EqualTo(3));
                evidence.RecordAssertion("The menu entry launches the configured boss arena",
                    menuEntryAvailable && boss.CurrentHealth == 900 && boss.Defense == 3,
                    "Custodian Arena; 900 health; 3 defense",
                    "scene=" + SceneManager.GetActiveScene().name + "; hp=" + boss.CurrentHealth + "; defense=" + boss.Defense);

                evidence.RecordInput(1, "Keyboard", "A/D approach; J melee; Space jump through attack tells; F potion");
                int simulatedTicks = 0;
                int lastAttackTick = -100;
                int lastJumpTick = -1000;
                int lastPotionTick = -1000;
                int jumpHoldTicks = 0;
                int jumpCount = 0;
                int maximumObservedTelegraphTicks = 0;
                int minimumObservedTelegraphStartTicks = int.MaxValue;
                int lastTelegraphStartCount = 0;
                bool sawPhaseOneRecovery = false;
                bool sawPhaseTwoRecovery = false;
                bool sawPhaseOneCut = false;
                bool sawPhaseOneWave = false;
                bool sawPhaseOneCharge = false;
                bool sawPhaseTransition = false;
                bool phaseTransitionSuspendedAttacks = true;
                bool phaseTransitionCompleted = false;
                int transitionCutCount = 0;
                int transitionWaveCount = 0;
                int transitionChargeCount = 0;
                int transitionWaveSpawnCount = 0;
                bool attackHeld = false;
                bool potionHeld = false;

                while (simulatedTicks < 12000 && arena.Result == CustodianArenaResult.Ongoing && !player.IsDead && !boss.IsDead)
                {
                    float dx = boss.transform.position.x - player.transform.position.x;
                    if (Mathf.Abs(dx) > 1.4f) SetMove(dx > 0f ? 1f : -1f);
                    else SetMove(0f);

                    if (boss.State == CustodianBehaviorState.Telegraphing)
                    {
                        maximumObservedTelegraphTicks = Mathf.Max(maximumObservedTelegraphTicks, boss.TelegraphTicksRemaining);
                        if (boss.TelegraphStartCount != lastTelegraphStartCount)
                        {
                            minimumObservedTelegraphStartTicks = Mathf.Min(minimumObservedTelegraphStartTicks, boss.TelegraphTicksRemaining);
                            lastTelegraphStartCount = boss.TelegraphStartCount;
                        }
                        if (boss.TelegraphTicksRemaining <= 10 && player.IsGrounded && simulatedTicks - lastJumpTick >= 45)
                        {
                            SetKey(Key.Space, true);
                            jumpHoldTicks = 12;
                            lastJumpTick = simulatedTicks;
                            jumpCount++;
                            evidence.RecordInput(simulatedTicks, "Jump", "Space before " + boss.CurrentPattern + " tell ends");
                        }
                    }
                    if (boss.Phase == CustodianPhase.PhaseOne)
                    {
                        sawPhaseOneCut |= boss.CutPatternCount > 0;
                        sawPhaseOneWave |= boss.WavePatternCount > 0;
                        sawPhaseOneCharge |= boss.ChargePatternCount > 0;
                    }

                    if (jumpHoldTicks > 0)
                    {
                        jumpHoldTicks--;
                        if (jumpHoldTicks == 0) SetKey(Key.Space, false);
                    }

                    if (boss.State == CustodianBehaviorState.PhaseTransition)
                    {
                        if (!sawPhaseTransition)
                        {
                            sawPhaseTransition = true;
                            transitionCutCount = boss.CutPatternCount;
                            transitionWaveCount = boss.WavePatternCount;
                            transitionChargeCount = boss.ChargePatternCount;
                            transitionWaveSpawnCount = boss.WaveSpawnCount;
                            evidence.RecordEvent("Phase transition began with " + boss.PhaseTransitionTicksRemaining + " ticks remaining.");
                        }
                        else
                        {
                            phaseTransitionSuspendedAttacks &= boss.CutPatternCount == transitionCutCount &&
                                boss.WavePatternCount == transitionWaveCount && boss.ChargePatternCount == transitionChargeCount &&
                                boss.WaveSpawnCount == transitionWaveSpawnCount;
                        }
                    }
                    if (boss.Phase == CustodianPhase.PhaseTwo) phaseTransitionCompleted = true;
                    if (boss.Phase == CustodianPhase.PhaseOne && boss.State == CustodianBehaviorState.Recovery &&
                        boss.CurrentRecoveryDurationTicks == 90) sawPhaseOneRecovery = true;
                    if (boss.Phase == CustodianPhase.PhaseTwo && boss.State == CustodianBehaviorState.Recovery &&
                        boss.CurrentRecoveryDurationTicks == 54) sawPhaseTwoRecovery = true;

                    if (Mathf.Abs(dx) <= 1.9f && simulatedTicks - lastAttackTick >= 27)
                    {
                        if (!attackHeld)
                        {
                            SetKey(Key.J, true);
                            attackHeld = true;
                            lastAttackTick = simulatedTicks;
                            if (simulatedTicks - lastAttackTick == 0 && boss.MeleeImpactCount % 10 == 0)
                                evidence.RecordInput(simulatedTicks, "Attack", "J Bellator melee at contact range");
                        }
                    }
                    else if (attackHeld)
                    {
                        SetKey(Key.J, false);
                        attackHeld = false;
                    }

                    if (player.CurrentHealth <= 65 && player.HealthPotionCharges > 0 &&
                        simulatedTicks - lastPotionTick >= 60 && !player.IsPreparingHealthPotion)
                    {
                        SetKey(Key.F, true);
                        potionHeld = true;
                        lastPotionTick = simulatedTicks;
                        evidence.RecordInput(simulatedTicks, "Consumable", "F health potion at " + player.CurrentHealth + " health");
                    }
                    else if (potionHeld && simulatedTicks > lastPotionTick)
                    {
                        SetKey(Key.F, false);
                        potionHeld = false;
                    }

                    yield return new WaitForFixedUpdate();
                    simulatedTicks++;
                    if (attackHeld)
                    {
                        SetKey(Key.J, false);
                        attackHeld = false;
                    }
                }

                for (int frame = 0; frame < 2 && arena.Result == CustodianArenaResult.Ongoing; frame++) yield return null;

                SetMove(0f);
                foreach (Key key in _pressedKeys.ToArray()) SetKey(key, false);

                bool defeatedThroughCombat = boss.IsDead && boss.CurrentHealth == 0 && boss.MeleeImpactCount > 0;
                evidence.RecordEvent("ticks=" + simulatedTicks + "; result=" + arena.Result + "; playerHealth=" + player.CurrentHealth +
                    "; bossHealth=" + boss.CurrentHealth + "; phase=" + boss.Phase + "; meleeImpacts=" + boss.MeleeImpactCount +
                    "; patterns=cut:" + boss.CutPatternCount + ",wave:" + boss.WavePatternCount + ",charge:" + boss.ChargePatternCount +
                    "; phaseTwoWaves=" + boss.PhaseTwoWaveSpawnCount + "; bossHits=" + boss.SuccessfulPlayerHitCount +
                    "; jumps=" + jumpCount);
                evidence.RecordAssertion("Every attack warning remains visible for at least 0.55 seconds",
                    minimumObservedTelegraphStartTicks >= CustodianBoss2D.AttackTelegraphMinimumTicks,
                    "at least " + CustodianBoss2D.AttackTelegraphMinimumTicks + " ticks at 60 Hz",
                    "shortest observed start=" + minimumObservedTelegraphStartTicks + " ticks; initial maximum=" + maximumObservedTelegraphTicks +
                        "; HUD reports the named pattern and remaining time");
                evidence.RecordAssertion("The player sees and survives the three first-phase patterns",
                    sawPhaseOneCut && sawPhaseOneWave && sawPhaseOneCharge && jumpCount > 0,
                    "cut, wave and charge execute after their tells",
                    "phase-one cut=" + sawPhaseOneCut + "; wave=" + sawPhaseOneWave + "; charge=" + sawPhaseOneCharge + "; jumps=" + jumpCount);
                evidence.RecordAssertion("The half-health transition lasts two seconds and suspends attacks",
                    sawPhaseTransition && phaseTransitionCompleted && phaseTransitionSuspendedAttacks,
                    "120 transition ticks with no new boss attack or wave spawn",
                    "transition=" + sawPhaseTransition + "; phaseTwo=" + phaseTransitionCompleted + "; suspended=" + phaseTransitionSuspendedAttacks);
                evidence.RecordAssertion("Phase two emits its additional wave and uses shorter recovery",
                    boss.PhaseTwoWavePatternCount > 0 && boss.PhaseTwoWaveSpawnCount >= 2 && sawPhaseOneRecovery && sawPhaseTwoRecovery,
                    "at least one phase-two double-wave pattern; 90 tick then 54 tick recovery",
                    "phaseTwoPatterns=" + boss.PhaseTwoWavePatternCount + "; extraWaves=" + boss.PhaseTwoWaveSpawnCount +
                        "; phaseOneRecovery=" + sawPhaseOneRecovery + "; phaseTwoRecovery=" + sawPhaseTwoRecovery);
                evidence.RecordAssertion("Physical boss attacks affect player health through combat damage",
                    player.CurrentHealth > 0 && player.CurrentHealth < player.CombatStats.MaximumHealth,
                    "a surviving player with health reduced by a boss attack",
                    "playerHealth=" + player.CurrentHealth + "/" + player.CombatStats.MaximumHealth +
                        "; directBossHits=" + boss.SuccessfulPlayerHitCount);
                evidence.RecordAssertion("Custodian death follows player melee impacts and leaves the player alive",
                    arena.Result == CustodianArenaResult.Victory && defeatedThroughCombat && !player.IsDead,
                    "Victory with zero boss health from queued melee impacts; living player",
                    "result=" + arena.Result + "; dead=" + boss.IsDead + "; health=" + boss.CurrentHealth +
                        "; melee=" + boss.MeleeImpactCount + "; playerHealth=" + player.CurrentHealth);

                int exitCode = evidence.Complete();
                yield return null;
                Assert.That(exitCode, Is.Zero, "D06 real combat scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        private void SetMove(float direction)
        {
            bool left = direction < -0.1f;
            bool right = direction > 0.1f;
            if (_leftHeld != left) { SetKey(Key.A, left); _leftHeld = left; }
            if (_rightHeld != right) { SetKey(Key.D, right); _rightHeld = right; }
        }

        private void SetKey(Key key, bool pressed)
        {
            if (pressed) _pressedKeys.Add(key);
            else _pressedKeys.Remove(key);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_pressedKeys.ToArray()));
        }
    }
}
