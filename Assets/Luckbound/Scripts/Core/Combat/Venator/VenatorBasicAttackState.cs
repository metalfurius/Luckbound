using System;
using Luckbound.Core.Input;

namespace Luckbound.Core.Combat
{
    /// <summary>Tick-driven timing for the Venator's free shortbow attack.</summary>
    public sealed class VenatorBasicAttackState
    {
        private VenatorBasicAttackProfile _profile;
        private long _lastTick = -1;
        private long _attackStartTick = -1;
        private long _nextAttackTick;
        private bool _cancelled;
        private bool _chargePending;
        private bool _chargeWasHeld;

        public VenatorBasicAttackState(VenatorBasicAttackProfile profile = null)
        {
            _profile = profile ?? VenatorBasicAttackProfile.Shortbow;
        }

        public bool IsCharging => _chargePending;

        /// <summary>Changes future shots without resetting the accepted-shot cooldown.</summary>
        public void ConfigureProfile(VenatorBasicAttackProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (_chargePending)
            {
                _chargePending = false;
                _chargeWasHeld = false;
            }
            _profile = profile;
        }

        public void Interrupt()
        {
            _cancelled = true;
            _chargePending = false;
            _chargeWasHeld = false;
        }

        public VenatorBasicAttackSnapshot Tick(PlayerCommand command, bool actionAllowed, bool interrupted)
        {
            return Tick(
                command.Tick,
                command.WasPressed(PlayerAction.Attack),
                actionAllowed,
                interrupted,
                command.IsHeld(PlayerAction.Attack),
                command.WasReleased(PlayerAction.Attack));
        }

        public VenatorBasicAttackSnapshot Tick(
            long tick,
            bool attackPressed,
            bool actionAllowed,
            bool interrupted,
            bool attackHeld = false,
            bool attackReleased = false)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            if (tick <= _lastTick) throw new InvalidOperationException("Venator basic attack state must be evaluated once per increasing simulation tick.");

            _lastTick = tick;
            if (interrupted || !actionAllowed)
            {
                _cancelled = true;
                _chargePending = false;
                _chargeWasHeld = false;
            }

            bool active = _attackStartTick >= 0 && !_cancelled && tick < _nextAttackTick;
            bool startedThisTick = false;
            ProjectileDefinition projectile = null;
            if (_chargePending)
            {
                if (attackReleased)
                {
                    projectile = StartAttack(tick, _chargeWasHeld);
                    active = true;
                    startedThisTick = true;
                    _chargePending = false;
                    _chargeWasHeld = false;
                }
                else if (attackHeld)
                {
                    _chargeWasHeld = true;
                }
            }
            else if (!active && actionAllowed && !interrupted && attackPressed && tick >= _nextAttackTick)
            {
                if (_profile.ChargeDamageBonusPercent > 0 && attackHeld && !attackReleased)
                {
                    // A hold/release sequence charges the optional shot. No duration threshold is
                    // inferred because the weapon definition specifies only its damage bonus.
                    _chargePending = true;
                    _chargeWasHeld = true;
                }
                else
                {
                    projectile = StartAttack(tick, charged: false);
                    active = true;
                    startedThisTick = true;
                }
            }

            return new VenatorBasicAttackSnapshot(
                wasStartedThisTick: startedThisTick,
                isAttacking: active,
                isCharging: _chargePending,
                projectile: projectile,
                movementSpeedMultiplier: active ? _profile.MovementSpeedMultiplier : 1m);
        }

        private ProjectileDefinition StartAttack(long tick, bool charged)
        {
            _attackStartTick = tick;
            _nextAttackTick = tick + _profile.AttackIntervalTicks;
            _cancelled = false;
            return _profile.CreateProjectile(charged);
        }
    }
}
