using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Tick-driven timing for the Magus's free ember-staff basic attack.</summary>
    public sealed class MagusBasicAttackState
    {
        private readonly MagusBasicAttackProfile _profile;
        private long _lastTick = -1;
        private long _nextAttackTick;
        private bool _cancelled;

        public MagusBasicAttackState(MagusBasicAttackProfile profile = null)
        {
            _profile = profile ?? MagusBasicAttackProfile.EmberStaff;
        }

        public void Interrupt()
        {
            _cancelled = true;
        }

        public MagusBasicAttackSnapshot Tick(long tick, bool attackPressed, bool actionAllowed, bool interrupted)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            if (tick <= _lastTick) throw new InvalidOperationException("Magus basic attack state must be evaluated once per increasing simulation tick.");

            _lastTick = tick;
            if (interrupted || !actionAllowed) _cancelled = true;

            bool active = !_cancelled && tick < _nextAttackTick;
            bool startedThisTick = false;
            if (!active && actionAllowed && !interrupted && attackPressed && tick >= _nextAttackTick)
            {
                _nextAttackTick = tick + _profile.AttackIntervalTicks;
                _cancelled = false;
                active = true;
                startedThisTick = true;
            }

            return new MagusBasicAttackSnapshot(
                wasStartedThisTick: startedThisTick,
                isAttacking: active,
                baseDamage: startedThisTick ? _profile.BaseDamage : 0m,
                projectileElement: startedThisTick ? _profile.ProjectileElement : string.Empty,
                projectile: startedThisTick ? _profile.Projectile : null,
                movementSpeedMultiplier: active ? _profile.MovementSpeedMultiplier : 1m);
        }
    }
}
