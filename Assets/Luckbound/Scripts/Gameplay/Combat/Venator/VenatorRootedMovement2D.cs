using System;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Venator
{
    /// <summary>Physics-backed Root status used by Venator traps for exactly the configured ticks.</summary>
    public sealed class VenatorRootedMovement2D : MonoBehaviour
    {
        private Rigidbody2D _body;
        private RigidbodyConstraints2D _previousConstraints;
        private int _remainingTicks;
        private bool _rooted;

        public int RemainingTicks => _remainingTicks;

        public bool IsRooted => _rooted;

        internal static void Apply(Rigidbody2D body, int durationTicks)
        {
            if (body == null || durationTicks <= 0) return;

            VenatorRootedMovement2D rooted = body.GetComponent<VenatorRootedMovement2D>();
            if (rooted == null) rooted = body.gameObject.AddComponent<VenatorRootedMovement2D>();
            rooted.ApplyRoot(body, durationTicks);
        }

        private void ApplyRoot(Rigidbody2D body, int durationTicks)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));

            if (!_rooted)
            {
                _body = body;
                _previousConstraints = body.constraints;
                _rooted = true;
            }

            _remainingTicks = Math.Max(_remainingTicks, durationTicks);
            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
            body.constraints = _previousConstraints | RigidbodyConstraints2D.FreezePositionX;
        }

        private void FixedUpdate()
        {
            if (!_rooted || Time.timeScale <= 0f) return;

            if (--_remainingTicks <= 0)
            {
                ReleaseRoot();
                Destroy(this);
            }
        }

        private void OnDisable()
        {
            ReleaseRoot();
        }

        private void OnDestroy()
        {
            ReleaseRoot();
        }

        private void ReleaseRoot()
        {
            if (!_rooted) return;

            if (_body != null)
            {
                _body.constraints = _previousConstraints;
            }

            _body = null;
            _remainingTicks = 0;
            _rooted = false;
        }
    }
}
