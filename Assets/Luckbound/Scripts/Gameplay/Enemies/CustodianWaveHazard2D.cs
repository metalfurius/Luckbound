using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Enemies
{
    /// <summary>Short-lived physical floor wave; jumping, movement and dodge can avoid its trigger.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CustodianWaveHazard2D : MonoBehaviour
    {
        private Rigidbody2D _body;
        private Collider2D _collider;
        private PlayerMotor2D _player;
        private ActorId _sourceActorId;
        private int _damage;
        private int _lifetimeTicks;
        private int _ageTicks;
        private float _direction;
        private float _speed;
        private float _minimumX;
        private float _maximumX;
        private bool _initialized;

        public int AgeTicks => _ageTicks;
        public ActorId SourceActorId => _sourceActorId;
        public bool HasHitPlayer { get; private set; }

        public void Initialize(PlayerMotor2D player, ActorId sourceActorId, int damage, float direction,
            float speed, int lifetimeTicks, float minimumX, float maximumX)
        {
            if (_initialized) throw new InvalidOperationException("CustodianWaveHazard2D can only be initialized once.");
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!sourceActorId.IsValid) throw new ArgumentException("A wave requires a valid source actor.", nameof(sourceActorId));
            if (damage <= 0 || speed <= 0f || lifetimeTicks <= 0 || minimumX >= maximumX || Mathf.Abs(direction) < 0.01f)
                throw new ArgumentOutOfRangeException(nameof(damage), "Wave damage, speed, lifetime, bounds and direction must be valid.");
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null) throw new InvalidOperationException("Custodian wave needs physics components.");
            _player = player;
            _sourceActorId = sourceActorId;
            _damage = damage;
            _direction = Mathf.Sign(direction);
            _speed = speed;
            _lifetimeTicks = lifetimeTicks;
            _minimumX = minimumX;
            _maximumX = maximumX;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _collider.isTrigger = true;
            _initialized = true;
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f) return;
            _ageTicks++;
            if (_ageTicks >= _lifetimeTicks || transform.position.x < _minimumX || transform.position.x > _maximumX)
            {
                Destroy(gameObject);
                return;
            }
            _body.MovePosition(_body.position + Vector2.right * (_direction * _speed * Time.fixedDeltaTime));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || HasHitPlayer || other == null) return;
            PlayerMotor2D player = other.GetComponentInParent<PlayerMotor2D>();
            if (player != _player || player.IsDead || player.CombatStats == null) return;
            HasHitPlayer = true;
            DamageCalculationRequest request = new DamageCalculationRequest(
                _damage, 1m, 1m, 0m, 0, 0m, player.CombatStats, targetIsInvulnerable: false);
            player.ReceiveDirectImpact(
                HitId.New(),
                request,
                player.SimulationTick,
                new Vector2(_direction * 2f, 0.5f));
            _collider.enabled = false;
            Destroy(gameObject);
        }
    }
}
