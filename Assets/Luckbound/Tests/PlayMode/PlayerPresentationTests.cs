using System.Collections;
using System.Globalization;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Presentation;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerPresentationTests
    {
        private const int Seed = 31415926;

        [UnityTest]
        public IEnumerator PixelPerfectCameraFollowsAndRebindsRoomBounds()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B06", "B06_CameraBounds", Seed))
            {
                var outputObject = new GameObject(
                    "B06_OutputCamera",
                    typeof(Camera),
                    typeof(AudioListener),
                    typeof(PixelPerfectCamera),
                    typeof(CinemachineBrain),
                    typeof(RoomCameraRig2D));
                outputObject.tag = "MainCamera";
                outputObject.transform.position = new Vector3(0f, 0f, -10f);
                Camera output = outputObject.GetComponent<Camera>();
                output.aspect = 16f / 9f;
                RoomCameraRig2D rig = outputObject.GetComponent<RoomCameraRig2D>();

                GameObject roomOne = CreateRoom("B06_RoomOne", Vector2.zero, new Vector2(40f, 30f));
                GameObject target = new GameObject("B06_CameraTarget");
                target.transform.position = Vector3.zero;
                rig.Bind(target.transform, roomOne.GetComponent<BoxCollider2D>());
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 30; frame++) yield return null;
                Vector3 startCameraPosition = output.transform.position;
                target.transform.position = new Vector3(100f, 100f, 0f);
                evidence.RecordInput(30, "CameraTarget", "teleport test target to (100,100)");
                for (int frame = 0; frame < 90; frame++) yield return null;

                Vector3 firstRoomCameraPosition = output.transform.position;
                bool followedTarget = Vector2.Distance(startCameraPosition, firstRoomCameraPosition) > 5f;
                BoxCollider2D roomOneBounds = roomOne.GetComponent<BoxCollider2D>();
                bool firstRoomConfined = CameraViewInside(output, roomOneBounds);
                string firstRoomBoundsActual = CameraBoundsDescription(output, roomOneBounds);

                GameObject roomTwo = CreateRoom("B06_RoomTwo", new Vector2(100f, 80f), new Vector2(40f, 30f));
                rig.RebindRoom(roomTwo.GetComponent<BoxCollider2D>());
                target.transform.position = new Vector3(200f, 200f, 0f);
                evidence.RecordInput(120, "ActiveRoom", "rebind to bounds centered at (100,80)");
                evidence.RecordInput(120, "CameraTarget", "teleport test target to (200,200)");
                for (int frame = 0; frame < 90; frame++) yield return null;

                Vector3 secondRoomCameraPosition = output.transform.position;
                BoxCollider2D roomTwoBounds = roomTwo.GetComponent<BoxCollider2D>();
                bool secondRoomConfined = CameraViewInside(output, roomTwoBounds);
                string secondRoomBoundsActual = CameraBoundsDescription(output, roomTwoBounds);
                PixelPerfectCamera pixelPerfect = rig.PixelPerfect;
                CinemachineFollow follow = rig.VirtualCamera.GetComponent<CinemachineFollow>();
                Vector3 damping = follow.TrackerSettings.PositionDamping;
                bool designResolutionCorrect = pixelPerfect.refResolutionX == 320 && pixelPerfect.refResolutionY == 180 &&
                    pixelPerfect.assetsPPU == 16 &&
                    pixelPerfect.gridSnapping == PixelPerfectCamera.GridSnapping.UpscaleRenderTexture &&
                    pixelPerfect.cropFrame == PixelPerfectCamera.CropFrame.Windowbox;
                bool designViewCorrect = Mathf.Abs(output.aspect - 16f / 9f) <= 0.001f &&
                    Mathf.Abs(output.orthographicSize * output.aspect * 2f - 20f) <= 0.02f &&
                    Mathf.Abs(output.orthographicSize * 2f - 11.25f) <= 0.02f;
                bool dampingCorrect = Mathf.Approximately(damping.x, 0.12f) && Mathf.Approximately(damping.y, 0.15f);

                evidence.RecordEvent("fixedDeltaTime=" + Time.fixedDeltaTime.ToString("F6", CultureInfo.InvariantCulture));
                evidence.RecordEvent("referenceResolution=" + pixelPerfect.refResolutionX + "x" + pixelPerfect.refResolutionY);
                evidence.RecordEvent("assetsPPU=" + pixelPerfect.assetsPPU);
                evidence.RecordEvent("cameraAspect=" + output.aspect.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("orthographicSize=" + output.orthographicSize.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("visibleWorldSize=" + (output.orthographicSize * output.aspect * 2f).ToString("F2", CultureInfo.InvariantCulture) +
                    "x" + (output.orthographicSize * 2f).ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordEvent("damping=" + damping.x.ToString("F2", CultureInfo.InvariantCulture) + "," + damping.y.ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordEvent("startCamera=" + Format(startCameraPosition));
                evidence.RecordEvent("firstRoomCamera=" + Format(firstRoomCameraPosition));
                evidence.RecordEvent("secondRoomCamera=" + Format(secondRoomCameraPosition));
                evidence.RecordAssertion("Project physics tick is 60 Hz",
                    Mathf.Abs(Time.fixedDeltaTime - 1f / 60f) <= 0.000001f, "fixedDeltaTime=1/60",
                    Time.fixedDeltaTime.ToString("F6", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Pixel-perfect output is 320x180 at 16 PPU", designResolutionCorrect,
                    "320x180, 16 PPU, upscale render texture, windowbox", pixelPerfect.gridSnapping + "/" + pixelPerfect.cropFrame);
                evidence.RecordAssertion("16:9 design view covers 20x11.25 world units", designViewCorrect,
                    "camera aspect=16:9 and visible size=20x11.25 units",
                    output.aspect.ToString("F3", CultureInfo.InvariantCulture) + "; " +
                    (output.orthographicSize * output.aspect * 2f).ToString("F2", CultureInfo.InvariantCulture) + "x" +
                    (output.orthographicSize * 2f).ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Cinemachine uses the locked follow damping", dampingCorrect,
                    "x=0.12, y=0.15", damping.x.ToString("F2", CultureInfo.InvariantCulture) + "," + damping.y.ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Camera follows a moving target", followedTarget, ">5 world units of camera movement",
                    Vector2.Distance(startCameraPosition, firstRoomCameraPosition).ToString("F2", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Camera view stays inside the first room", firstRoomConfined,
                    "all four viewport edges inside B06_RoomOne", firstRoomBoundsActual);
                evidence.RecordAssertion("Room rebinding refreshes the confiner bounds", secondRoomConfined,
                    "all four viewport edges inside B06_RoomTwo", secondRoomBoundsActual);
                int result = evidence.Complete();

                Object.Destroy(outputObject);
                Object.Destroy(roomOne);
                Object.Destroy(roomTwo);
                Object.Destroy(target);
                yield return null;

                Assert.That(result, Is.EqualTo(0), "Camera scenario must record a successful run.");
            }
        }

        [UnityTest]
        public IEnumerator AnimatorFollowsRealIdleMoveJumpAndDodgeCommands()
        {
            using (var evidence = new ScenarioEvidenceRecorder("B06", "B06_PlayerVisualStates", Seed))
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                GameObject floor = CreateBox("B06_Ground", new Vector2(0f, -0.5f), new Vector2(80f, 1f), groundLayer);
                GameObject player = new GameObject("B06_Player");
                player.transform.position = new Vector2(0f, 1.5f);
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.75f, 1.5f);

                PlayerInputBuffer input = new PlayerInputBuffer(ActorId.New());
                PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
                motor.Initialize(input.ActorId, input, 1 << groundLayer);
                Animator animator = player.AddComponent<Animator>();
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Luckbound/PlayerVisualStates");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PlayerVisualStatePresenter presenter = player.AddComponent<PlayerVisualStatePresenter>();
                presenter.Initialize(motor);
                Physics2D.SyncTransforms();

                for (int frame = 0; frame < 60; frame++) yield return new WaitForFixedUpdate();
                for (int frame = 0; frame < 3; frame++) yield return null;
                bool groundedAtIdle = motor.IsGrounded;
                bool idleState = presenter.CurrentState == PlayerVisualState.Idle && AnimatorIs(animator, "Idle");
                string idleActual = AnimatorStateName(animator) + "/" + presenter.CurrentState;

                input.SetMoveX(1f);
                evidence.RecordInput(60, "MoveX", "1");
                for (int frame = 0; frame < 12; frame++) yield return new WaitForFixedUpdate();
                for (int frame = 0; frame < 3; frame++) yield return null;
                bool movingState = presenter.CurrentState == PlayerVisualState.Moving && AnimatorIs(animator, "Moving");
                string movingActual = AnimatorStateName(animator) + "/" + presenter.CurrentState;
                float horizontalSpeed = motor.Velocity.x;

                input.Press(PlayerAction.Jump);
                evidence.RecordInput(72, "Jump", "Pressed");
                yield return new WaitForFixedUpdate();
                input.Release(PlayerAction.Jump);
                yield return new WaitForFixedUpdate();
                for (int frame = 0; frame < 3; frame++) yield return null;
                bool airborneState = !motor.IsGrounded && presenter.CurrentState == PlayerVisualState.Airborne && AnimatorIs(animator, "Airborne");
                string airborneActual = AnimatorStateName(animator) + "/" + presenter.CurrentState;

                input.Press(PlayerAction.Dodge);
                evidence.RecordInput(74, "Dodge", "Pressed while airborne");
                yield return new WaitForFixedUpdate();
                bool dodgeActivated = motor.IsDodging;
                input.Release(PlayerAction.Dodge);
                for (int frame = 0; frame < 3; frame++) yield return null;
                bool dodgingState = presenter.CurrentState == PlayerVisualState.Dodging && AnimatorIs(animator, "Dodging");
                string dodgeActual = AnimatorStateName(animator) + "/" + presenter.CurrentState;

                evidence.RecordEvent("groundedAtIdle=" + groundedAtIdle);
                evidence.RecordEvent("horizontalSpeed=" + horizontalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("jumpVelocity=" + motor.Velocity.y.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordEvent("dodgeActivated=" + dodgeActivated);
                evidence.RecordEvent("idleAnimatorState=" + idleActual);
                evidence.RecordEvent("movingAnimatorState=" + movingActual);
                evidence.RecordEvent("airborneAnimatorState=" + airborneActual);
                evidence.RecordEvent("dodgeAnimatorState=" + dodgeActual);
                evidence.RecordAssertion("Real grounded idle input selects Idle", groundedAtIdle && idleState,
                    "grounded and Animator state Idle", "grounded=" + groundedAtIdle + "; state=" + idleActual);
                evidence.RecordAssertion("Real move input selects Moving", movingState && horizontalSpeed > 0f,
                    "Animator state Moving and positive horizontal velocity", "state=" + movingActual + "; vx=" + horizontalSpeed.ToString("F3", CultureInfo.InvariantCulture));
                evidence.RecordAssertion("Real jump input selects Airborne", airborneState,
                    "motor airborne and Animator state Airborne", "grounded=" + motor.IsGrounded + "; state=" + airborneActual);
                evidence.RecordAssertion("Real dodge input selects Dodging", dodgeActivated && dodgingState,
                    "motor dodging and Animator state Dodging", "active=" + motor.IsDodging + "; state=" + dodgeActual);
                int result = evidence.Complete();

                Object.Destroy(player);
                Object.Destroy(floor);
                yield return null;

                Assert.That(result, Is.EqualTo(0), "Visual-state scenario must record a successful run.");
            }
        }

        private static bool AnimatorIs(Animator animator, string stateName)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }

        private static string AnimatorStateName(Animator animator)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            string[] names = { "Idle", "Moving", "Airborne", "Attacking", "Dodging", "Stunned", "Dead" };
            foreach (string name in names)
            {
                if (state.IsName(name)) return name;
            }

            return "unknown:" + state.fullPathHash;
        }

        private static GameObject CreateRoom(string name, Vector2 center, Vector2 size)
        {
            var room = new GameObject(name);
            room.transform.position = center;
            BoxCollider2D bounds = room.AddComponent<BoxCollider2D>();
            bounds.size = size;
            return room;
        }

        private static GameObject CreateBox(string name, Vector2 center, Vector2 size, int layer)
        {
            var gameObject = new GameObject(name);
            gameObject.layer = layer;
            gameObject.transform.position = center;
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            return gameObject;
        }

        private static bool CameraViewInside(Camera camera, Collider2D bounds)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 center = camera.transform.position;
            Bounds room = bounds.bounds;
            const float tolerance = 0.1f;
            return center.x - halfWidth >= room.min.x - tolerance &&
                center.x + halfWidth <= room.max.x + tolerance &&
                center.y - halfHeight >= room.min.y - tolerance &&
                center.y + halfHeight <= room.max.y + tolerance;
        }

        private static string CameraBoundsDescription(Camera camera, Collider2D bounds)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 center = camera.transform.position;
            Bounds room = bounds.bounds;
            return "camera center=" + Format(center) + "; halfSize=" + halfWidth.ToString("F2", CultureInfo.InvariantCulture) +
                "," + halfHeight.ToString("F2", CultureInfo.InvariantCulture) + "; room=" +
                Format(room.min) + ".." + Format(room.max);
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                value.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                value.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }
}
