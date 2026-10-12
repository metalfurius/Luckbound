using System;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Luckbound.Presentation
{
    /// <summary>Owns the pixel-perfect output camera and binds one player and active room at runtime.</summary>
    [RequireComponent(typeof(Camera), typeof(AudioListener), typeof(PixelPerfectCamera))]
    [RequireComponent(typeof(CinemachineBrain))]
    public sealed class RoomCameraRig2D : MonoBehaviour
    {
        public const int ReferenceWidth = 320;
        public const int ReferenceHeight = 180;
        public const int AssetsPixelsPerUnit = 16;
        public const float OrthographicSize = 5.625f;
        public const float HorizontalDamping = 0.12f;
        public const float VerticalDamping = 0.15f;
        public const float MinimumRoomWidth = 20f;
        public const float MinimumRoomHeight = 11.25f;
        private const float RoomSizeTolerance = 0.01f;

        private Camera _outputCamera;
        private CinemachineCamera _virtualCamera;
        private CinemachineFollow _follow;
        private CinemachineConfiner2D _confiner;
        private PixelPerfectCamera _pixelPerfectCamera;

        public CinemachineCamera VirtualCamera => _virtualCamera;

        public PixelPerfectCamera PixelPerfect => _pixelPerfectCamera;

        private void Awake() => Configure();

        public void Bind(Transform followTarget, Collider2D roomBounds)
        {
            if (followTarget == null)
            {
                throw new ArgumentNullException(nameof(followTarget));
            }

            if (roomBounds == null)
            {
                throw new ArgumentNullException(nameof(roomBounds));
            }

            Configure();
            ValidateRoomBounds(roomBounds);
            _virtualCamera.Follow = followTarget;
            _confiner.BoundingShape2D = roomBounds;
            _confiner.InvalidateBoundingShapeCache();
        }

        public void RebindRoom(Collider2D roomBounds)
        {
            if (roomBounds == null)
            {
                throw new ArgumentNullException(nameof(roomBounds));
            }

            Configure();
            ValidateRoomBounds(roomBounds);
            _confiner.BoundingShape2D = roomBounds;
            _confiner.InvalidateBoundingShapeCache();
        }

        /// <summary>Idempotently applies the project's camera contract to serialized or runtime objects.</summary>
        public void Configure()
        {
            if (_outputCamera == null) _outputCamera = GetComponent<Camera>();
            if (_pixelPerfectCamera == null) _pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
            if (_outputCamera == null || _pixelPerfectCamera == null || GetComponent<CinemachineBrain>() == null)
            {
                throw new InvalidOperationException("RoomCameraRig2D requires Camera, PixelPerfectCamera and CinemachineBrain components.");
            }

            _outputCamera.orthographic = true;
            _outputCamera.orthographicSize = OrthographicSize;
            _pixelPerfectCamera.assetsPPU = AssetsPixelsPerUnit;
            _pixelPerfectCamera.refResolutionX = ReferenceWidth;
            _pixelPerfectCamera.refResolutionY = ReferenceHeight;
            _pixelPerfectCamera.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            _pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            if (_virtualCamera == null)
            {
                _virtualCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (_virtualCamera == null)
            {
                var virtualCameraObject = new GameObject("Room Virtual Camera");
                virtualCameraObject.transform.SetParent(transform, false);
                virtualCameraObject.transform.localPosition = Vector3.zero;
                _virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            }
            _virtualCamera.gameObject.name = "Cinemachine Camera";

            if (_follow == null) _follow = _virtualCamera.GetComponent<CinemachineFollow>();
            if (_follow == null) _follow = _virtualCamera.gameObject.AddComponent<CinemachineFollow>();
            if (_confiner == null) _confiner = _virtualCamera.GetComponent<CinemachineConfiner2D>();
            if (_confiner == null) _confiner = _virtualCamera.gameObject.AddComponent<CinemachineConfiner2D>();

            _follow.FollowOffset = new Vector3(0f, 0f, -10f);
            TrackerSettings tracker = _follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(HorizontalDamping, VerticalDamping, 0f);
            tracker.RotationDamping = Vector3.zero;
            tracker.QuaternionDamping = 0f;
            _follow.TrackerSettings = tracker;
            _confiner.Damping = 0f;
            _confiner.SlowingDistance = 0f;

            LensSettings lens = _virtualCamera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = OrthographicSize;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 1000f;
            _virtualCamera.Lens = lens;
        }

        private void ValidateRoomBounds(Collider2D roomBounds)
        {
            if (!(roomBounds is PolygonCollider2D) && !(roomBounds is CompositeCollider2D) && !(roomBounds is BoxCollider2D))
            {
                throw new ArgumentException("Room bounds must use PolygonCollider2D, CompositeCollider2D or BoxCollider2D.", nameof(roomBounds));
            }

            Bounds bounds = roomBounds.bounds;
            if (bounds.size.x + RoomSizeTolerance < MinimumRoomWidth ||
                bounds.size.y + RoomSizeTolerance < MinimumRoomHeight)
            {
                throw new ArgumentException(
                    "Room bounds must contain at least a 20 by 11.25 unit visible frame; found " +
                    bounds.size.x.ToString("F2") + " by " + bounds.size.y.ToString("F2") + ".",
                    nameof(roomBounds));
            }
        }
    }
}
