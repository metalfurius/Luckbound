using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class JumpControlTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [UnityTest]
        public IEnumerator FullAndReleasedJumpsMatchTheMovementContract()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B04", "B04_JumpProfile", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                CreateBox("B04_JumpFloor", new Vector2(2f, -0.5f), new Vector2(24f, 1f), groundLayer);
                ActorFixture fullJump = CreateActor("B04_FullJump", new Vector2(0f, 1.5f), groundLayer);
                ActorFixture shortJump = CreateActor("B04_ShortJump", new Vector2(4f, 1.5f), groundLayer);
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 30; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float fullStartY = fullJump.GameObject.transform.position.y;
                float shortStartY = shortJump.GameObject.transform.position.y;
                bool bothGrounded = fullJump.Motor.IsGrounded && shortJump.Motor.IsGrounded;
                fullJump.Input.Press(PlayerAction.Jump);
                shortJump.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(30, "Jump", "Pressed; hold full jump");
                evidence.RecordInput(30, "Jump", "Pressed; release after one fixed tick");

                yield return new WaitForFixedUpdate();
                float upwardSpeedBeforeCut = shortJump.Motor.Velocity.y;
                shortJump.Input.Release(PlayerAction.Jump);
                float maximumFullHeight = fullJump.GameObject.transform.position.y - fullStartY;
                float maximumShortHeight = shortJump.GameObject.transform.position.y - shortStartY;
                float upwardSpeedAfterCut = float.NaN;
                int fullApexTick = 0;

                for (int frame = 0; frame < 36; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    if (frame == 0)
                    {
                        upwardSpeedAfterCut = shortJump.Motor.Velocity.y;
                    }

                    maximumFullHeight = Mathf.Max(maximumFullHeight, fullJump.GameObject.transform.position.y - fullStartY);
                    maximumShortHeight = Mathf.Max(maximumShortHeight, shortJump.GameObject.transform.position.y - shortStartY);
                    if (fullApexTick == 0 && fullJump.Motor.Velocity.y <= 0f)
                    {
                        fullApexTick = frame + 2;
                    }
                }

                float apexSeconds = fullApexTick * Time.fixedDeltaTime;
                evidence.RecordEvent("bothGrounded=" + bothGrounded);
                evidence.RecordEvent("fullJumpHeight=" + maximumFullHeight.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("shortJumpHeight=" + maximumShortHeight.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("upwardSpeedBeforeCut=" + upwardSpeedBeforeCut.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("upwardSpeedAfterCut=" + upwardSpeedAfterCut.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("fullJumpApexSeconds=" + apexSeconds.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Both actors begin grounded", bothGrounded, "true", bothGrounded.ToString());
                evidence.RecordAssertion("Held jump reaches the planned height",
                    maximumFullHeight >= PlayerMotor2D.JumpHeight - 0.3f && maximumFullHeight <= PlayerMotor2D.JumpHeight + 0.3f,
                    "3.0 ± 0.3 units", maximumFullHeight.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Held jump reaches apex in the planned time",
                    apexSeconds >= PlayerMotor2D.TimeToJumpApex - 0.05f && apexSeconds <= PlayerMotor2D.TimeToJumpApex + 0.05f,
                    "0.40 ± 0.05 seconds", apexSeconds.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Releasing jump shortens the arc", maximumShortHeight < maximumFullHeight * 0.6f,
                    "short height < 60% of full height", maximumShortHeight.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Jump release halves ascending speed", upwardSpeedAfterCut > 0f &&
                    upwardSpeedAfterCut < upwardSpeedBeforeCut * 0.55f &&
                    upwardSpeedAfterCut > upwardSpeedBeforeCut * 0.4f,
                    "0.40–0.55 of the pre-release speed after one gravity step",
                    upwardSpeedAfterCut.ToString("F3", CultureInfo.InvariantCulture));
                int result = evidence.Complete();

                Assert.That(result, Is.EqualTo(0), "Jump profile scenario must record a successful run.");
            }
        }

        [UnityTest]
        public IEnumerator JumpPressedShortlyBeforeLandingIsBuffered()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B04", "B04_JumpBuffer", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                CreateBox("B04_BufferFloor", new Vector2(32f, -0.5f), new Vector2(8f, 1f), groundLayer);
                ActorFixture actor = CreateActor("B04_BufferActor", new Vector2(32f, 1.15f), groundLayer);
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 2; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool wasAirborneAtPress = !actor.Motor.IsGrounded && actor.Motor.Velocity.y < 0f;
                float verticalSpeedAtPress = actor.Motor.Velocity.y;
                float pressFixedTime = Time.fixedTime;
                actor.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(2, "Jump", "Pressed before landing");

                bool jumpedFromBuffer = false;
                float bufferedJumpSeconds = float.PositiveInfinity;
                float highestY = actor.GameObject.transform.position.y;
                for (int frame = 0; frame < 15; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    highestY = Mathf.Max(highestY, actor.GameObject.transform.position.y);
                    if (!jumpedFromBuffer && actor.Motor.Velocity.y > 1f)
                    {
                        jumpedFromBuffer = true;
                        bufferedJumpSeconds = Time.fixedTime - pressFixedTime;
                    }
                }

                evidence.RecordEvent("airborneAtPress=" + wasAirborneAtPress);
                evidence.RecordEvent("verticalSpeedAtPress=" + verticalSpeedAtPress.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("bufferedJumpSeconds=" + bufferedJumpSeconds.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("highestYAfterBufferedPress=" + highestY.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Input arrives while actor is descending", wasAirborneAtPress, "airborne with negative vertical speed",
                    "grounded=" + actor.Motor.IsGrounded + "; speed=" + verticalSpeedAtPress.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Buffered input jumps on landing", jumpedFromBuffer, "vertical speed becomes positive after landing",
                    "jumped=" + jumpedFromBuffer + "; y=" + highestY.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Buffered jump occurs inside the configured window",
                    bufferedJumpSeconds <= PlayerMotor2D.JumpBufferDuration + 0.0001f,
                    "within 0.12 seconds",
                    bufferedJumpSeconds.ToString("F3", CultureInfo.InvariantCulture));
                int result = evidence.Complete();

                Assert.That(result, Is.EqualTo(0), "Jump buffer scenario must record a successful run.");
            }
        }

        [UnityTest]
        public IEnumerator CoyoteWindowAcceptsRecentAndRejectsExpiredJumpInput()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B04", "B04_Coyote", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                CreateBox("B04_CoyotePlatform", new Vector2(0f, -0.5f), new Vector2(3f, 1f), groundLayer);
                CreateBox("B04_LatePlatform", new Vector2(20f, -0.5f), new Vector2(3f, 1f), groundLayer);
                CreateBox("B04_CoyoteSafetyFloor", new Vector2(0f, -5.5f), new Vector2(10f, 1f), 0);
                CreateBox("B04_LateSafetyFloor", new Vector2(20f, -5.5f), new Vector2(10f, 1f), 0);
                ActorFixture recentActor = CreateActor("B04_RecentCoyoteActor", new Vector2(0f, 1.5f), groundLayer);
                ActorFixture lateActor = CreateActor("B04_ExpiredCoyoteActor", new Vector2(20f, 1.5f), groundLayer);
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 30; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                recentActor.Input.SetMoveX(1f);
                lateActor.Input.SetMoveX(1f);
                evidence.RecordInput(30, "MoveX", "1 on both ledges");
                int ledgeExitTicks = 0;
                while ((recentActor.Motor.IsGrounded || lateActor.Motor.IsGrounded) && ledgeExitTicks < 80)
                {
                    yield return new WaitForFixedUpdate();
                    ledgeExitTicks++;
                }

                bool bothLeftLedges = !recentActor.Motor.IsGrounded && !lateActor.Motor.IsGrounded;
                recentActor.Input.SetMoveX(0f);
                lateActor.Input.SetMoveX(0f);
                for (int frame = 0; frame < 2; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float recentCoyoteRemaining = recentActor.Motor.CoyoteTimeRemaining;
                recentActor.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(30 + ledgeExitTicks + 2, "Jump", "Pressed within coyote window");
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float recentJumpSpeedAfterInput = recentActor.Motor.Velocity.y;
                bool recentJumpAccepted = recentJumpSpeedAfterInput > 5f;
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float expiredCoyoteRemaining = lateActor.Motor.CoyoteTimeRemaining;
                lateActor.Input.Press(PlayerAction.Jump);
                evidence.RecordInput(40 + ledgeExitTicks, "Jump", "Pressed after coyote window expired");
                float greatestLateVerticalSpeed = float.MinValue;
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    greatestLateVerticalSpeed = Mathf.Max(greatestLateVerticalSpeed, lateActor.Motor.Velocity.y);
                }

                bool lateJumpRejected = greatestLateVerticalSpeed <= 0f;
                evidence.RecordEvent("ledgeExitTicks=" + ledgeExitTicks);
                evidence.RecordEvent("recentCoyoteRemaining=" + recentCoyoteRemaining.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("recentJumpSpeedAfterInput=" + recentJumpSpeedAfterInput.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("expiredCoyoteRemaining=" + expiredCoyoteRemaining.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("greatestLateVerticalSpeed=" + greatestLateVerticalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Both actors leave their platforms", bothLeftLedges, "both airborne before jump inputs",
                    "recentGrounded=" + recentActor.Motor.IsGrounded + "; lateGrounded=" + lateActor.Motor.IsGrounded);
                evidence.RecordAssertion("Jump remains available during coyote grace", recentCoyoteRemaining > 0f && recentCoyoteRemaining <= PlayerMotor2D.CoyoteDuration,
                    "remaining time in (0, 0.10] seconds", recentCoyoteRemaining.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Recent ledge jump is accepted", recentJumpAccepted, "upward speed > 5 u/s",
                    recentJumpSpeedAfterInput.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Expired coyote time is zero", expiredCoyoteRemaining <= 0f, "0 seconds remaining",
                    expiredCoyoteRemaining.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Late ledge jump is rejected", lateJumpRejected, "vertical speed stays non-positive",
                    greatestLateVerticalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                int result = evidence.Complete();

                Assert.That(result, Is.EqualTo(0), "Coyote scenario must record a successful run.");
            }
        }

        [UnityTearDown]
        public IEnumerator DestroyScenarioObjects()
        {
            foreach (GameObject gameObject in _createdObjects)
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            _createdObjects.Clear();
            yield return null;
        }

        private ActorFixture CreateActor(string actorName, Vector2 position, int groundLayer)
        {
            var gameObject = new GameObject(actorName);
            _createdObjects.Add(gameObject);
            gameObject.transform.position = position;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var collider = gameObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            var input = new PlayerInputBuffer(ActorId.New());
            var motor = gameObject.AddComponent<PlayerMotor2D>();
            motor.Initialize(input.ActorId, input, 1 << groundLayer);
            return new ActorFixture(gameObject, motor, input);
        }

        private GameObject CreateBox(string objectName, Vector2 position, Vector2 size, int layer)
        {
            var gameObject = new GameObject(objectName);
            _createdObjects.Add(gameObject);
            gameObject.layer = layer;
            gameObject.transform.position = position;
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            return gameObject;
        }

        private sealed class ActorFixture
        {
            public ActorFixture(GameObject gameObject, PlayerMotor2D motor, PlayerInputBuffer input)
            {
                GameObject = gameObject;
                Motor = motor;
                Input = input;
            }

            public GameObject GameObject { get; }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }
        }
    }
}
