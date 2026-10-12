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
    public sealed class PlayerDodgeTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [UnityTest]
        public IEnumerator DodgeUsesItsInvulnerabilityAndCooldownWindowsAndStopsAtWalls()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B05", "B05_Dodge", 31415926))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                CreateBox("B05_Floor", new Vector2(0f, -0.5f), new Vector2(80f, 1f), groundLayer);
                GameObject wall = CreateBox("B05_Wall", new Vector2(2f, 1.5f), new Vector2(0.5f, 4f), 0);
                ActorFixture timingActor = CreateActor("B05_TimingActor", new Vector2(-10f, 1.5f), groundLayer);
                ActorFixture blockedActor = CreateActor("B05_BlockedActor", new Vector2(0f, 1.5f), groundLayer);
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 30; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                bool actorsGrounded = timingActor.Motor.IsGrounded && blockedActor.Motor.IsGrounded;
                bool simulationRunsAtSixtyHertz = Mathf.Abs(Time.fixedDeltaTime - (1f / 60f)) <= 0.000001f;
                timingActor.Input.SetMoveX(1f);
                blockedActor.Input.SetMoveX(1f);
                timingActor.Input.Press(PlayerAction.Dodge);
                blockedActor.Input.Press(PlayerAction.Dodge);
                evidence.RecordInput(30, "MoveX", "1 for both actors");
                evidence.RecordInput(30, "Dodge", "Pressed for both actors");
                float startX = timingActor.GameObject.transform.position.x;

                yield return new WaitForFixedUpdate();
                float dodgeStartedTime = Time.fixedTime;
                bool beganDodge = timingActor.Motor.IsDodging && blockedActor.Motor.IsDodging;
                bool beganInvulnerable = timingActor.Motor.IsInvulnerable && blockedActor.Motor.IsInvulnerable;
                int dodgeTicks = timingActor.Motor.IsDodging ? 1 : 0;
                int invulnerabilityTicks = timingActor.Motor.IsInvulnerable ? 1 : 0;
                float lastDodgeX = timingActor.GameObject.transform.position.x;
                float maximumWallX = blockedActor.GameObject.transform.position.x;
                timingActor.Input.Release(PlayerAction.Dodge);
                blockedActor.Input.Release(PlayerAction.Dodge);
                timingActor.Input.SetMoveX(-1f);
                blockedActor.Input.SetMoveX(-1f);

                int observations = 0;
                while (timingActor.Motor.IsDodging && observations < 20)
                {
                    yield return new WaitForFixedUpdate();
                    observations++;
                    if (timingActor.Motor.IsDodging)
                    {
                        dodgeTicks++;
                        lastDodgeX = timingActor.GameObject.transform.position.x;
                    }

                    if (timingActor.Motor.IsInvulnerable)
                    {
                        invulnerabilityTicks++;
                    }

                    maximumWallX = Mathf.Max(maximumWallX, blockedActor.GameObject.transform.position.x);
                }

                float dodgeDuration = dodgeTicks * Time.fixedDeltaTime;
                float invulnerabilityDuration = invulnerabilityTicks * Time.fixedDeltaTime;
                float unblockedDistance = lastDodgeX - startX;
                float wallLeft = wall.transform.position.x - wall.GetComponent<BoxCollider2D>().size.x * 0.5f;
                float blockedFinalX = blockedActor.GameObject.transform.position.x;
                float cooldownBeforeRetry = timingActor.Motor.DodgeCooldownRemaining;

                timingActor.Input.Press(PlayerAction.Dodge);
                evidence.RecordInput(31 + dodgeTicks, "Dodge", "Pressed during cooldown");
                yield return new WaitForFixedUpdate();
                bool retryWasRejected = !timingActor.Motor.IsDodging;
                float cooldownAfterRetry = timingActor.Motor.DodgeCooldownRemaining;
                timingActor.Input.Release(PlayerAction.Dodge);

                int cooldownTicks = 0;
                while (!timingActor.Motor.IsDodgeReady && cooldownTicks < 60)
                {
                    yield return new WaitForFixedUpdate();
                    cooldownTicks++;
                }

                float cooldownReadyTime = Time.fixedTime;
                float cooldownElapsed = cooldownReadyTime - dodgeStartedTime;
                bool becameReadyAfterCooldown = timingActor.Motor.IsDodgeReady &&
                    cooldownElapsed >= PlayerMotor2D.DodgeCooldownDuration - Time.fixedDeltaTime &&
                    cooldownElapsed <= PlayerMotor2D.DodgeCooldownDuration + Time.fixedDeltaTime;
                timingActor.Input.SetMoveX(0f);
                timingActor.Input.Press(PlayerAction.Dodge);
                evidence.RecordInput(32 + dodgeTicks + cooldownTicks, "Dodge", "Pressed when cooldown expired with no direction input");
                yield return new WaitForFixedUpdate();
                bool reactivatedFacingLeft = timingActor.Motor.IsDodging && timingActor.Motor.Velocity.x < 0f;

                evidence.RecordEvent("fixedDeltaTime=" + Time.fixedDeltaTime.ToString("F6", CultureInfo.InvariantCulture));
                evidence.RecordEvent("actorsGrounded=" + actorsGrounded);
                evidence.RecordEvent("beganDodge=" + beganDodge);
                evidence.RecordEvent("beganInvulnerable=" + beganInvulnerable);
                evidence.RecordEvent("dodgeTicks=" + dodgeTicks);
                evidence.RecordEvent("dodgeDuration=" + dodgeDuration.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("invulnerabilityTicks=" + invulnerabilityTicks);
                evidence.RecordEvent("invulnerabilityDuration=" + invulnerabilityDuration.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("unblockedDistance=" + unblockedDistance.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("maximumWallX=" + maximumWallX.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("wallLeft=" + wallLeft.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("blockedFinalX=" + blockedFinalX.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("cooldownBeforeRetry=" + cooldownBeforeRetry.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("cooldownAfterRetry=" + cooldownAfterRetry.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("cooldownElapsed=" + cooldownElapsed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("reactivatedFacingLeft=" + reactivatedFacingLeft);
                evidence.RecordAssertion("Actors begin grounded", actorsGrounded, "true", actorsGrounded.ToString());
                evidence.RecordAssertion("Project physics tick is 60 Hz", simulationRunsAtSixtyHertz, "fixedDeltaTime=1/60",
                    Time.fixedDeltaTime.ToString("F6", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Dodge starts on input", beganDodge, "both actors enter dodge", beganDodge.ToString());
                evidence.RecordAssertion("Invulnerability starts with dodge", beganInvulnerable, "both actors invulnerable at activation", beganInvulnerable.ToString());
                evidence.RecordAssertion("Dodge lasts for 0.18 seconds", Mathf.Abs(dodgeDuration - PlayerMotor2D.DodgeDuration) <= Time.fixedDeltaTime,
                    "0.18 ± one fixed tick", dodgeDuration.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Invulnerability lasts for 0.12 seconds inside dodge",
                    Mathf.Abs(invulnerabilityDuration - PlayerMotor2D.DodgeInvulnerabilityDuration) <= Time.fixedDeltaTime &&
                    invulnerabilityTicks <= dodgeTicks,
                    "0.12 ± one fixed tick and no longer than dodge", invulnerabilityDuration.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Unblocked dodge follows its locked direction and speed",
                    unblockedDistance >= 2f && unblockedDistance <= 2.35f,
                    "2.16 units ± one 60 Hz step", unblockedDistance.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Static wall blocks the dash", maximumWallX < wallLeft - 0.02f && blockedFinalX > wallLeft - 1f,
                    "capsule stops before wall at x=" + wallLeft.ToString("F3", CultureInfo.InvariantCulture),
                    "maxX=" + maximumWallX.ToString("F3", CultureInfo.InvariantCulture) + "; finalX=" + blockedFinalX.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Cooldown rejects an early retry without refreshing", retryWasRejected &&
                    cooldownBeforeRetry > 0f && cooldownAfterRetry > 0f && cooldownAfterRetry < cooldownBeforeRetry,
                    "retry rejected and remaining cooldown decreases", "before=" + cooldownBeforeRetry.ToString("F3", CultureInfo.InvariantCulture) +
                    "; after=" + cooldownAfterRetry.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Dodge becomes ready after 0.80 seconds", becameReadyAfterCooldown,
                    "0.80 ± one fixed tick", cooldownElapsed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Dodge can restart and uses the last facing direction",
                    reactivatedFacingLeft, "new dash velocity points left", timingActor.Motor.Velocity.x.ToString("F3", CultureInfo.InvariantCulture));
                int result = evidence.Complete();

                Assert.That(result, Is.EqualTo(0), "Dodge scenario must record a successful run.");
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
