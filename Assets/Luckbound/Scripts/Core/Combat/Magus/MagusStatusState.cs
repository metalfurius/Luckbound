using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Deterministic 60 Hz burn and freeze timing for one target actor.</summary>
    public sealed class MagusStatusState
    {
        public const int TickRate = 60;
        public const int FreezeProtectionDurationTicks = 120;

        private long _lastTick = -1;
        private decimal _burnDamagePerSecond;
        private long _burnStartsAtTick = -1;
        private long _burnEndsAtTick = -1;
        private long _nextBurnPulseTick = -1;
        private long _freezeStartsAtTick = -1;
        private long _freezeEndsAtTick = -1;
        private long _freezeProtectionEndsAtTick = -1;
        private int _slowPercent;
        private long _slowStartsAtTick = -1;
        private long _slowEndsAtTick = -1;

        public long SlowStartsAtTick => _slowStartsAtTick;

        public long SlowEndsAtTick => _slowEndsAtTick;

        public void ApplyBurn(long tick, decimal damagePerSecond, int durationTicks)
        {
            ValidateApplicationTick(tick);
            if (damagePerSecond <= 0m) throw new ArgumentOutOfRangeException(nameof(damagePerSecond));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            EnsureTickAdditionFits(tick, durationTicks);

            bool alreadyBurning = tick >= _burnStartsAtTick && _burnEndsAtTick >= tick;
            if (alreadyBurning)
            {
                _burnDamagePerSecond = Math.Max(_burnDamagePerSecond, damagePerSecond);
                _burnEndsAtTick = tick + durationTicks;
            }
            else
            {
                EnsureTickAdditionFits(tick, TickRate);
                _burnDamagePerSecond = damagePerSecond;
                _burnStartsAtTick = tick;
                _burnEndsAtTick = tick + durationTicks;
                _nextBurnPulseTick = tick + TickRate;
            }
        }

        public bool ApplyFreeze(long tick, int durationTicks)
        {
            ValidateApplicationTick(tick);
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (tick < _freezeEndsAtTick || tick < _freezeProtectionEndsAtTick) return false;
            EnsureTickAdditionFits(tick, durationTicks + (long)FreezeProtectionDurationTicks);

            _freezeStartsAtTick = tick;
            _freezeEndsAtTick = tick + durationTicks;
            _freezeProtectionEndsAtTick = _freezeEndsAtTick + FreezeProtectionDurationTicks;
            return true;
        }

        /// <summary>Apply a movement slow over the half-open range [tick, tick + durationTicks).</summary>
        public void ApplySlow(long tick, int percent, int durationTicks)
        {
            ValidateApplicationTick(tick);
            if (percent <= 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            EnsureTickAdditionFits(tick, durationTicks);

            long endTick = tick + durationTicks;
            bool alreadySlowed = _slowStartsAtTick >= 0 && tick < _slowEndsAtTick;
            if (alreadySlowed)
            {
                _slowPercent = Math.Max(_slowPercent, percent);
                _slowEndsAtTick = Math.Max(_slowEndsAtTick, endTick);
            }
            else
            {
                _slowPercent = percent;
                _slowStartsAtTick = tick;
                _slowEndsAtTick = endTick;
            }
        }

        public decimal GetMovementSpeedMultiplierAt(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (_slowStartsAtTick < 0 || tick < _slowStartsAtTick || tick >= _slowEndsAtTick) return 1m;
            return 1m - _slowPercent / 100m;
        }

        public MagusStatusSnapshot Tick(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            if (tick <= _lastTick) throw new InvalidOperationException("Magus status state must be evaluated once per increasing simulation tick.");
            _lastTick = tick;

            bool isBurning = tick >= _burnStartsAtTick && tick <= _burnEndsAtTick && _burnEndsAtTick >= 0;
            decimal burnDamageDue = 0m;
            if (isBurning && tick == _nextBurnPulseTick)
            {
                burnDamageDue = _burnDamagePerSecond;
                if (_nextBurnPulseTick <= long.MaxValue - TickRate)
                {
                    _nextBurnPulseTick += TickRate;
                }
                else
                {
                    _nextBurnPulseTick = long.MaxValue;
                }
            }

            bool isFrozen = tick >= _freezeStartsAtTick && tick < _freezeEndsAtTick && _freezeEndsAtTick >= 0;
            bool isFreezeProtected = !isFrozen && tick >= _freezeEndsAtTick && tick < _freezeProtectionEndsAtTick && _freezeProtectionEndsAtTick >= 0;
            decimal burnDamagePerSecond = isBurning ? _burnDamagePerSecond : 0m;
            long burnEndsAtTick = isBurning ? _burnEndsAtTick : -1;
            long freezeEndsAtTick = isFrozen ? _freezeEndsAtTick : -1;
            long freezeProtectionEndsAtTick = isFreezeProtected ? _freezeProtectionEndsAtTick : -1;

            if (tick >= _burnEndsAtTick)
            {
                _burnDamagePerSecond = 0m;
                _burnStartsAtTick = -1;
                _burnEndsAtTick = -1;
                _nextBurnPulseTick = -1;
            }

            if (tick >= _freezeProtectionEndsAtTick && _freezeProtectionEndsAtTick >= 0)
            {
                _freezeStartsAtTick = -1;
                _freezeEndsAtTick = -1;
                _freezeProtectionEndsAtTick = -1;
            }

            return new MagusStatusSnapshot(
                isBurning,
                burnDamagePerSecond,
                burnDamageDue,
                burnEndsAtTick,
                isFrozen,
                isFreezeProtected,
                freezeEndsAtTick,
                freezeProtectionEndsAtTick);
        }

        private void ValidateApplicationTick(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            if (tick < _lastTick) throw new ArgumentOutOfRangeException(nameof(tick), "Status applications cannot be recorded in an earlier simulation tick.");
        }

        private static void EnsureTickAdditionFits(long tick, long duration)
        {
            if (duration < 0 || tick > long.MaxValue - duration)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Status end tick would exceed the simulation tick range.");
            }
        }
    }
}
