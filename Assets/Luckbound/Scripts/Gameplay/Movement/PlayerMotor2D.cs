using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using UnityEngine;

namespace Luckbound.Gameplay.Movement
{
    /// <summary>
    /// Moves a dynamic actor through Unity 2D physics using one actor-scoped command per fixed tick.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        public const float MaximumHorizontalSpeed = 6f;
        public const float GroundAcceleration = 60f;
        public const float GroundBraking = 80f;
        public const float AirControl = 0.85f;
        public const float JumpHeight = 3f;
        public const float TimeToJumpApex = 0.4f;
        public const float AscendingGravity = 37.5f;
        public const float DescendingGravity = 56.25f;
        public const float MaximumFallSpeed = 22f;
        public const float CoyoteDuration = 0.1f;
        public const float JumpBufferDuration = 0.12f;
        public const float JumpCutMultiplier = 0.5f;

        private const float GroundProbeDistance = 0.08f;
        private const float GroundNormalThreshold = 0.65f;
        private const float GroundedVerticalSpeedLimit = 0.05f;
        private const float MovementDeadZone = 0.001f;

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

        private ActorId _actorId;
        private Collider2D _collider;
        private ContactFilter2D _groundContactFilter;
        private bool _initialized;
        private IPlayerInputSource _inputSource;
        private PhysicsMaterial2D _movementMaterial;
        private PhysicsMaterial2D _previousMaterial;
        private Rigidbody2D _body;
        private float _coyoteTimeRemaining;
        private float _jumpBufferRemaining;
        private long _tick;

        public bool IsGrounded { get; private set; }

        public float CoyoteTimeRemaining => _coyoteTimeRemaining;

        public float JumpBufferRemaining => _jumpBufferRemaining;

        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        public void Initialize(ActorId actorId, IPlayerInputSource inputSource, LayerMask groundLayers)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("PlayerMotor2D requires a valid actor identifier.", nameof(actorId));
            }

            if (inputSource == null)
            {
                throw new ArgumentNullException(nameof(inputSource));
            }

            if (groundLayers.value == 0)
            {
                throw new ArgumentException("PlayerMotor2D requires at least one ground layer.", nameof(groundLayers));
            }

            if (Physics2D.gravity.y >= -0.01f)
            {
                throw new InvalidOperationException("PlayerMotor2D requires downward 2D gravity on the Y axis.");
            }

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null)
            {
                throw new InvalidOperationException("PlayerMotor2D requires a Rigidbody2D and an independent Collider2D.");
            }

            _actorId = actorId;
            _inputSource = inputSource;
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _groundContactFilter = new ContactFilter2D();
            _groundContactFilter.SetLayerMask(groundLayers);
            _groundContactFilter.useTriggers = false;

            _movementMaterial = new PhysicsMaterial2D("Luckbound Actor Surface")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum,
                bounceCombine = PhysicsMaterialCombine2D.Minimum
            };
            _previousMaterial = _collider.sharedMaterial;
            _collider.sharedMaterial = _movementMaterial;

            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_movementMaterial == null)
            {
                return;
            }

            if (_collider != null && _collider.sharedMaterial == _movementMaterial)
            {
                _collider.sharedMaterial = _previousMaterial;
            }

            Destroy(_movementMaterial);
            _movementMaterial = null;
            _previousMaterial = null;
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            PlayerCommand command = _inputSource.Consume(_tick++);
            if (command.ActorId != _actorId)
            {
                throw new InvalidOperationException("The input command belongs to a different actor.");
            }

            IsGrounded = UpdateGrounded();
            float deltaTime = Time.fixedDeltaTime;
            if (command.WasPressed(PlayerAction.Jump))
            {
                _jumpBufferRemaining = JumpBufferDuration;
            }
            else if (_jumpBufferRemaining > 0f)
            {
                _jumpBufferRemaining = Mathf.Max(0f, _jumpBufferRemaining - deltaTime);
            }

            if (IsGrounded)
            {
                _coyoteTimeRemaining = CoyoteDuration;
            }
            else if (_coyoteTimeRemaining > 0f)
            {
                _coyoteTimeRemaining = Mathf.Max(0f, _coyoteTimeRemaining - deltaTime);
            }

            float input = command.MoveX;
            float targetSpeed = input * MaximumHorizontalSpeed;
            float acceleration = Mathf.Abs(input) > MovementDeadZone ? GroundAcceleration : GroundBraking;
            if (!IsGrounded)
            {
                acceleration *= AirControl;
            }

            Vector2 velocity = _body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * deltaTime);

            if (_jumpBufferRemaining > 0f && _coyoteTimeRemaining > 0f)
            {
                velocity.y = Mathf.Sqrt(2f * AscendingGravity * JumpHeight);
                _jumpBufferRemaining = 0f;
                _coyoteTimeRemaining = 0f;
                IsGrounded = false;
            }

            if (command.WasReleased(PlayerAction.Jump) && velocity.y > 0f)
            {
                velocity.y *= JumpCutMultiplier;
            }

            ApplyVerticalGravity(ref velocity, deltaTime);
            _body.linearVelocity = velocity;
        }

        private void ApplyVerticalGravity(ref Vector2 velocity, float deltaTime)
        {
            float worldGravity = -Physics2D.gravity.y;
            if (velocity.y > 0f)
            {
                _body.gravityScale = AscendingGravity / worldGravity;
                return;
            }

            float nextFallSpeed = velocity.y - DescendingGravity * deltaTime;
            if (nextFallSpeed < -MaximumFallSpeed)
            {
                velocity.y = -MaximumFallSpeed;
                _body.gravityScale = 0f;
                return;
            }

            _body.gravityScale = DescendingGravity / worldGravity;
        }

        private bool UpdateGrounded()
        {
            if (_body.linearVelocity.y > GroundedVerticalSpeedLimit)
            {
                return false;
            }

            int hitCount = _collider.Cast(
                Vector2.down,
                _groundContactFilter,
                _groundHits,
                GroundProbeDistance);

            for (int index = 0; index < hitCount; index++)
            {
                if (_groundHits[index].normal.y >= GroundNormalThreshold)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
