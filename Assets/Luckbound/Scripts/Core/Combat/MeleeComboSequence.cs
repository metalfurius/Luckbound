using System;
using System.Collections.Generic;

namespace Luckbound.Core.Combat
{
    /// <summary>Per-actor sequence of accepted attacks, advanced only when an attack starts.</summary>
    public sealed class MeleeComboSequence
    {
        private readonly decimal[] _damageMultipliers;
        private readonly int _resetAfterTicks;
        private long _lastAttackTick = -1;
        private int _lastMultiplierIndex = -1;

        public MeleeComboSequence(IReadOnlyList<decimal> damageMultipliers, int resetAfterTicks)
        {
            if (damageMultipliers == null)
            {
                throw new ArgumentNullException(nameof(damageMultipliers));
            }

            if (damageMultipliers.Count == 0)
            {
                throw new ArgumentException("A combo sequence requires at least one multiplier.", nameof(damageMultipliers));
            }

            if (resetAfterTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resetAfterTicks), "Combo reset interval must contain at least one tick.");
            }

            _damageMultipliers = new decimal[damageMultipliers.Count];
            for (int index = 0; index < damageMultipliers.Count; index++)
            {
                decimal multiplier = damageMultipliers[index];
                if (multiplier <= 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(damageMultipliers), "Combo multipliers must be greater than zero.");
                }

                _damageMultipliers[index] = multiplier;
            }

            _resetAfterTicks = resetAfterTicks;
        }

        public static MeleeComboSequence CreateBellator()
        {
            return new MeleeComboSequence(new[] { 1m, 1m, 1.25m }, resetAfterTicks: 60);
        }

        public decimal AcceptAttack(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            }

            if (_lastAttackTick >= 0 && tick <= _lastAttackTick)
            {
                throw new InvalidOperationException("Accepted attacks must use strictly increasing simulation ticks.");
            }

            if (_lastAttackTick < 0 || tick - _lastAttackTick >= _resetAfterTicks)
            {
                _lastMultiplierIndex = 0;
            }
            else
            {
                _lastMultiplierIndex = (_lastMultiplierIndex + 1) % _damageMultipliers.Length;
            }

            _lastAttackTick = tick;
            return _damageMultipliers[_lastMultiplierIndex];
        }
    }
}
