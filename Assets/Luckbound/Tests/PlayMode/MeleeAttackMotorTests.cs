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
    public sealed class MeleeAttackMotorTests
    {
        [UnityTest]
        public IEnumerator AttackAllowsMovementAtSixtyFivePercentAndOpensOneHitWindow()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C02", "C02_AttackMovement", 27182818))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateBox("C02_Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), groundLayer);
                var actorId = ActorId.New();
                var input = new PlayerInputBuffer(actorId);
                var player = new GameObject("C02_Bellator");
                player.transform.position = new Vector2(0f, 1.5f);
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.75f, 1.5f);
                var motor = player.AddComponent<PlayerMotor2D>();
                motor.Initialize(actorId, input, 1 << groundLayer);
                Physics2D.SyncTransforms();

                for (int tick = 0; tick < 30; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                input.SetMoveX(1f);
                input.Press(PlayerAction.Attack);
                evidence.RecordInput(30, "MoveX", "1");
                evidence.RecordInput(30, "Attack", "pressed");
                for (int tick = 0; tick < 12; tick++)
                {
                    yield return new WaitForFixedUpdate();
                }

                MeleeHitWindow window = motor.CurrentMeleeHitWindow;
                bool movingDuringAttack = motor.IsAttacking && Mathf.Abs(body.linearVelocity.x) > 3.7f;
                bool attackAnimationState = motor.CaptureVisualState().State == PlayerVisualState.Attacking;
                var targetId = new ActorId(System.Guid.Parse("30000000-0000-4000-8000-000000000003"));
                bool firstColliderAccepted = window != null && window.TryRegisterTarget(targetId);
                bool duplicateColliderRejected = window != null && !window.TryRegisterTarget(targetId);

                evidence.RecordEvent("speedDuringAttack=" + body.linearVelocity.x.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("meleeHitId=" + (window == null ? "missing" : window.Id.ToString()));
                evidence.RecordAssertion("Attack remains active while moving", movingDuringAttack,
                    "attack active and horizontal speed > 3.7 u/s", motor.IsAttacking + "; speed=" + body.linearVelocity.x.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Motor exposes the attack presentation state", attackAnimationState,
                    PlayerVisualState.Attacking.ToString(), motor.CaptureVisualState().State.ToString());
                evidence.RecordAssertion("Attack opens its damage window at tick 6", window != null,
                    "active MeleeHitWindow", window == null ? "missing" : "present");
                evidence.RecordAssertion("One target is accepted once in this window", firstColliderAccepted && duplicateColliderRejected,
                    "first collider accepted; repeated actor ID rejected", firstColliderAccepted + "; duplicate rejected=" + duplicateColliderRejected);
                int exitCode = evidence.Complete();

                Object.Destroy(player);
                Object.Destroy(floor);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C02 attack movement scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        [UnityTest]
        public IEnumerator BasicAttackCanStartWhilePlayerIsAirborne()
        {
            using (var evidence = new ScenarioEvidenceRecorder("C02", "C02_AirAttack", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                var actorId = ActorId.New();
                var input = new PlayerInputBuffer(actorId);
                var player = new GameObject("C02_AirborneBellator");
                player.transform.position = new Vector2(0f, 8f);
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.75f, 1.5f);
                var motor = player.AddComponent<PlayerMotor2D>();
                motor.Initialize(actorId, input, 1 << groundLayer);
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();

                bool airborneBeforeInput = !motor.IsGrounded;
                input.Press(PlayerAction.Attack);
                evidence.RecordInput(1, "Attack", "pressed while airborne");
                yield return new WaitForFixedUpdate();

                evidence.RecordAssertion("Player is airborne before attack", airborneBeforeInput, "not grounded", motor.IsGrounded ? "grounded" : "airborne");
                evidence.RecordAssertion("Attack starts in the air", motor.IsAttacking && motor.MeleeAttackActivationCount == 1,
                    "attacking with one activation", motor.IsAttacking + "; activations=" + motor.MeleeAttackActivationCount);
                int exitCode = evidence.Complete();

                Object.Destroy(player);
                yield return null;

                Assert.That(exitCode, Is.Zero, "C02 air attack scenario failed. Evidence: " + evidence.ReportPath);
            }
        }

        private static GameObject CreateBox(string objectName, Vector2 position, Vector2 size, int layer)
        {
            var gameObject = new GameObject(objectName);
            gameObject.layer = layer;
            gameObject.transform.position = position;
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            return gameObject;
        }
    }
}
