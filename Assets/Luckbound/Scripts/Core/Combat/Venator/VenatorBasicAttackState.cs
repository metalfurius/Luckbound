using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Tick-driven timing for the Venator's free shortbow attack.</summary>
    public sealed class VenatorBasicAttackState
    {
        private readonly VenatorBasicAttackProfile _profile;
        private long _lastTick = -1;
        private long _attackStartTick = -1;
        private long _nextAttackTick;
        private bool _cancelled;

        public VenatorBasicAttackState(VenatorBasicAttackProfile profile = null)
        {
            _profile = profile ?? VenatorBasicAttackProfile.Shortbow;
        }

        public void Interrupt()
        {
            _cancelled = true;
        }

        public VenatorBasicAttackSnapshot Tick(long tick, bool attackPressed, bool actionAllowed, bool interrupted)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            if (tick <= _lastTick) throw new InvalidOperationException("Venator basic attack state must be evaluated once per increasing simulation tick.");

            _lastTick = tick;
            if (interrupted || !actionAllowed) _cancelled = true;

            bool active = _attackStartTick >= 0 && !_cancelled && tick < _nextAttackTick;
            bool startedThisTick = false;
            if (!active && actionAllowed && !interrupted && attackPressed && tick >= _nextAttackTick)
            {
                _attackStartTick = tick;
                _nextAttackTick = tick + _profile.AttackIntervalTicks;
                _cancelled = false;
                active = true;
                startedThisTick = true;
            }

            return new VenatorBasicAttackSnapshot(
                wasStartedThisTick: startedThisTick,
                isAttacking: active,
                projectile: startedThisTick ? _profile.Projectile : null,
                movementSpeedMultiplier: active ? _profile.MovementSpeedMultiplier : 1m);
        }
    }
}
