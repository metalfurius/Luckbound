using System.Collections;
using System.Globalization;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerMotorTests
    {
        [UnityTest]
        public IEnumerator WalksOnGroundAcceleratesBrakesAndCannotPassThroughWall()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B03", "B03_WalkWall", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateBox("B03_Ground", new Vector2(8f, -0.5f), new Vector2(40f, 1f), groundLayer);
                GameObject wall = CreateBox("B03_Wall", new Vector2(8f, 1.5f), new Vector2(1f, 4f), 0);
                var actorId = ActorId.New();
                var input = new PlayerInputBuffer(actorId);
                var player = new GameObject("B03_Player");
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

                for (int frame = 0; frame < 30; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool groundedBeforeMoving = motor.IsGrounded;
                input.SetMoveX(1f);
                evidence.RecordInput(30, "MoveX", "1");
                float maximumHorizontalSpeed = 0f;
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumHorizontalSpeed = Mathf.Max(maximumHorizontalSpeed, Mathf.Abs(body.linearVelocity.x));
                }

                float speedBeforeBraking = body.linearVelocity.x;
                input.SetMoveX(0f);
                evidence.RecordInput(40, "MoveX", "0");
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float speedAfterBraking = body.linearVelocity.x;
                input.SetMoveX(1f);
                evidence.RecordInput(48, "MoveX", "1");
                float maximumX = player.transform.position.x;
                for (int frame = 0; frame < 100; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumX = Mathf.Max(maximumX, player.transform.position.x);
                }

                float finalX = player.transform.position.x;
                float wallLeft = wall.transform.position.x - wall.GetComponent<BoxCollider2D>().size.x * 0.5f;
                bool stayedGrounded = motor.IsGrounded;
                bool wallStoppedActor = maximumX < wallLeft - 0.02f;
                bool actorReachedWall = finalX > wallLeft - 1.0f;

                evidence.RecordEvent("groundedBeforeMoving=" + groundedBeforeMoving);
                evidence.RecordEvent("maximumHorizontalSpeed=" + maximumHorizontalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("speedBeforeBraking=" + speedBeforeBraking.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("speedAfterBraking=" + speedAfterBraking.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("maximumX=" + maximumX.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("finalX=" + finalX.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Actor starts grounded", groundedBeforeMoving, "true", groundedBeforeMoving.ToString());
                evidence.RecordAssertion("Ground movement reaches configured speed", maximumHorizontalSpeed >= 5.9f,
                    ">= 5.9 u/s for 6 u/s target", maximumHorizontalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Releasing movement brakes the actor", Mathf.Abs(speedAfterBraking) <= 0.1f,
                    "absolute speed <= 0.1 u/s", speedAfterBraking.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Actor stays grounded on the course", stayedGrounded, "true", stayedGrounded.ToString());
                evidence.RecordAssertion("Static wall blocks the collider", wallStoppedActor && actorReachedWall,
                    "wall left=" + wallLeft.ToString("F3", CultureInfo.InvariantCulture),
                    "maxX=" + maximumX.ToString("F3", CultureInfo.InvariantCulture) +
                    "; finalX=" + finalX.ToString("F3", CultureInfo.InvariantCulture));
                int result = evidence.Complete();

                Object.Destroy(player);
                Object.Destroy(floor);
                Object.Destroy(wall);
                yield return null;

                Assert.That(result, Is.EqualTo(0), "Scenario evidence must record a successful physical movement run.");
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
