using System.Collections;
using System.Globalization;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Presentation;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerDamageTests
    {
        [UnityTest]
        public IEnumerator DamageStartsProtectionKnockbackAndStunAndSuppressesActions()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C04", "C04_ProtectedKnockback", 22360679))
            {
                PlayerFixture fixture = CreatePlayer("C04_Protected", maximumHealth: 100);
                yield return new WaitForFixedUpdate();
                CombatStats stats = fixture.Motor.CombatStats;
                DirectImpactOutcome first = fixture.Motor.ReceiveDirectImpact(
                    HitId.New(),
                    Request(stats, 15m),
                    impactTick: 1,
                    knockbackVelocity: new Vector2(-6f, 3f),
                    stunDurationTicks: 6);
                Vector2 velocityAfterHit = fixture.Motor.Velocity;
                DirectImpactOutcome repeated = fixture.Motor.ReceiveDirectImpact(
                    HitId.New(),
                    Request(stats, 15m),
                    impactTick: 2,
                    knockbackVelocity: new Vector2(6f, 3f),
                    stunDurationTicks: 6);

                fixture.Input.SetMoveX(1f);
                fixture.Input.Press(PlayerAction.Attack);
                fixture.Input.Press(PlayerAction.Dodge);
                fixture.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(1, "ReceiveDirectImpact", "damage=15; knockback=(-6,3); stunTicks=6");
                evidence.RecordInput(2, "PlayerCommand", "move right + attack + dodge + jump during stun");
                evidence.RecordEvent("healthAfterFirst=" + first.CurrentHealth + "; protectedUntil=" + first.ProtectedUntilTick +
                    "; velocityAtImpact=" + velocityAfterHit);
                evidence.RecordEvent("repeatedStatus=" + repeated.Status + "; healthAfterRepeated=" + repeated.CurrentHealth);
                evidence.RecordAssertion("The accepted impact applies health loss and the supplied knockback",
                    first.Status == DirectImpactStatus.Applied && first.CurrentHealth == 85 && velocityAfterHit == new Vector2(-6f, 3f),
                    "health=85; velocity=(-6,3)", "health=" + first.CurrentHealth + "; velocity=" + velocityAfterHit);
                evidence.RecordAssertion("A separate impact inside 27 protection ticks is prevented",
                    repeated.Status == DirectImpactStatus.PreventedByInvulnerability && repeated.CurrentHealth == 85,
                    "prevented; health=85", repeated.Status + "; health=" + repeated.CurrentHealth);

                yield return new WaitForFixedUpdate();
                bool stunnedVisual = fixture.Motor.CaptureVisualState().State == PlayerVisualState.Stunned;
                yield return new WaitForFixedUpdate();
                evidence.RecordEvent("stunRemaining=" + fixture.Motor.StunTicksRemaining + "; dodgeActivations=" + fixture.Motor.DodgeActivationCount +
                    "; attackActivations=" + fixture.Motor.MeleeAttackActivationCount + "; jumps=" + fixture.Motor.SuccessfulJumpCount);
                evidence.RecordAssertion("Stun is presented and consumes movement actions without activating them",
                    stunnedVisual && fixture.Motor.IsStunned && fixture.Motor.DodgeActivationCount == 0 &&
                    fixture.Motor.MeleeAttackActivationCount == 0 && fixture.Motor.SuccessfulJumpCount == 0,
                    "stunned; zero dodge, attack, and jump activations",
                    "visualStunned=" + stunnedVisual + "; remaining=" + fixture.Motor.StunTicksRemaining +
                    "; dodge=" + fixture.Motor.DodgeActivationCount + "; attack=" + fixture.Motor.MeleeAttackActivationCount +
                    "; jump=" + fixture.Motor.SuccessfulJumpCount);
                int exitCode = evidence.Complete();

                fixture.Destroy();
                yield return null;

                Assert.That(exitCode, Is.Zero, "C04 nonlethal impact scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator LethalImpactStopsMovementAndAllFurtherActions()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C04", "C04_DeathBlocksActions", 24494897))
            {
                PlayerFixture fixture = CreatePlayer("C04_Dead", maximumHealth: 10);
                CombatStats stats = fixture.Motor.CombatStats;
                DirectImpactOutcome lethal = fixture.Motor.ReceiveDirectImpact(
                    HitId.New(),
                    Request(stats, 50m),
                    impactTick: 0,
                    knockbackVelocity: Vector2.zero);
                fixture.Input.SetMoveX(1f);
                fixture.Input.Press(PlayerAction.Attack);
                fixture.Input.Press(PlayerAction.Dodge);
                fixture.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(0, "ReceiveDirectImpact", "damage=50 against 10 health");
                evidence.RecordInput(1, "PlayerCommand", "move right + attack + dodge + jump after death");
                evidence.RecordEvent("damage=" + lethal.DamageApplied + "; health=" + lethal.CurrentHealth + "; deathOccurred=" + lethal.DeathOccurred);

                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Vector2 finalVelocity = fixture.Motor.Velocity;
                PlayerVisualState visualState = fixture.Motor.CaptureVisualState().State;
                evidence.RecordEvent("visual=" + visualState + "; velocity=" + finalVelocity.ToString("F3") +
                    "; dodge=" + fixture.Motor.DodgeActivationCount + "; attack=" + fixture.Motor.MeleeAttackActivationCount +
                    "; jump=" + fixture.Motor.SuccessfulJumpCount);
                evidence.RecordAssertion("Lethal damage reaches zero health and sets death exactly once",
                    lethal.DeathOccurred && lethal.IsDead && lethal.CurrentHealth == 0,
                    "dead; health=0; deathOccurred", lethal.IsDead + "; health=" + lethal.CurrentHealth + "; deathOccurred=" + lethal.DeathOccurred);
                evidence.RecordAssertion("A dead player cannot move, dodge, attack, jump, or leave the Dead state",
                    fixture.Motor.IsDead && visualState == PlayerVisualState.Dead &&
                    Mathf.Abs(finalVelocity.x) <= 0.01f && fixture.Motor.DodgeActivationCount == 0 &&
                    fixture.Motor.MeleeAttackActivationCount == 0 && fixture.Motor.SuccessfulJumpCount == 0,
                    "Dead; horizontal speed=0; zero action activations",
                    visualState + "; vx=" + finalVelocity.x.ToString("F3", CultureInfo.InvariantCulture) +
                    "; dodge=" + fixture.Motor.DodgeActivationCount + "; attack=" + fixture.Motor.MeleeAttackActivationCount +
                    "; jump=" + fixture.Motor.SuccessfulJumpCount);
                int exitCode = evidence.Complete();

                fixture.Destroy();
                yield return null;

                Assert.That(exitCode, Is.Zero, "C04 death scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        private static PlayerFixture CreatePlayer(string name, int maximumHealth)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            var floor = new GameObject(name + "_Ground");
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(30f, 1f);

            var actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject(name + "_Player");
            player.transform.position = new Vector2(0f, 1.5f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            motor.Initialize(actorId, input, 1 << groundLayer, new CombatStats(maximumHealth, 50, 0));
            Physics2D.SyncTransforms();
            return new PlayerFixture(floor, player, motor, input);
        }

        private static DamageCalculationRequest Request(CombatStats stats, decimal baseDamage)
        {
            return new DamageCalculationRequest(
                baseDamage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: stats,
                targetIsInvulnerable: false);
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(GameObject floor, GameObject player, PlayerMotor2D motor, PlayerInputBuffer input)
            {
                Floor = floor;
                Player = player;
                Motor = motor;
                Input = input;
            }

            public GameObject Floor { get; }

            public GameObject Player { get; }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }

            public void Destroy()
            {
                Object.Destroy(Player);
                Object.Destroy(Floor);
            }
        }
    }
}
