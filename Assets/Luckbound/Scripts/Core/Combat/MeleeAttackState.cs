using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Combat
{
    /// <summary>Actor-owned basic melee timing and combo state; call once per increasing simulation tick.</summary>
    public sealed class MeleeAttackState
    {
        private MeleeAttackProfile _profile;
        private readonly MeleeComboSequence _combo;
        private MeleeAttackProfile _currentAttackProfile;
        private long _lastEvaluatedTick = -1;
        private long _attackStartTick = -1;
        private long _nextAttackTick;
        private decimal _currentDamageMultiplier;
        private bool _attackCancelled;
        private MeleeHitWindow _hitWindow;

        public MeleeAttackState(MeleeAttackProfile profile, MeleeComboSequence combo)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _combo = combo ?? throw new ArgumentNullException(nameof(combo));
        }

        /// <summary>Changes the profile for future swings while keeping an accepted swing's timing intact.</summary>
        public void ConfigureProfile(MeleeAttackProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _profile = profile;
        }

        public void Interrupt()
        {
            _attackCancelled = true;
            _hitWindow = null;
        }

        public MeleeAttackSnapshot Tick(long tick, bool attackPressed, bool dodgeAccepted)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            }

            if (tick <= _lastEvaluatedTick)
            {
                throw new InvalidOperationException("Melee attack state must be evaluated once per increasing simulation tick.");
            }

            _lastEvaluatedTick = tick;

            if (dodgeAccepted)
            {
                _attackCancelled = true;
                _hitWindow = null;
            }

            bool attackActive = _attackStartTick >= 0 && _currentAttackProfile != null &&
                !_attackCancelled &&
                tick < _attackStartTick + _currentAttackProfile.AttackIntervalTicks;
            bool startedThisTick = false;

            if (!attackActive && !dodgeAccepted && attackPressed && tick >= _nextAttackTick)
            {
                _attackStartTick = tick;
                _currentAttackProfile = _profile;
                _nextAttackTick = tick + _currentAttackProfile.AttackIntervalTicks;
                _attackCancelled = false;
                _hitWindow = null;
                _currentDamageMultiplier = _combo.AcceptAttack(tick);
                attackActive = true;
                startedThisTick = true;
            }

            bool windowOpen = false;
            if (attackActive)
            {
                long elapsedTicks = tick - _attackStartTick;
                long windowEndTick = (long)_currentAttackProfile.DamageWindowStartTick + _currentAttackProfile.DamageWindowDurationTicks;
                windowOpen = elapsedTicks >= _currentAttackProfile.DamageWindowStartTick && elapsedTicks < windowEndTick;
                if (windowOpen && _hitWindow == null)
                {
                    _hitWindow = new MeleeHitWindow(HitId.New(), _currentAttackProfile.BaseDamage, _currentDamageMultiplier);
                }
            }

            if (!windowOpen)
            {
                _hitWindow = null;
            }

            return new MeleeAttackSnapshot(
                wasStartedThisTick: startedThisTick,
                isAttacking: attackActive,
                isDamageWindowOpen: windowOpen,
                baseDamage: attackActive ? _currentAttackProfile.BaseDamage : 0m,
                damageMultiplier: attackActive ? _currentDamageMultiplier : 0m,
                movementSpeedMultiplier: attackActive ? _currentAttackProfile.MovementSpeedMultiplier : 1m,
                hitWindow: windowOpen ? _hitWindow : null);
        }
    }
}
