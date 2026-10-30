using System;
using Luckbound.Core.Pacts;

namespace Luckbound.Core.Relics
{
    /// <summary>Pure calculations for support relic effects received by a player actor.</summary>
    public static class SupportRelicActorRules
    {
        public const decimal MaximumHorizontalSpeed = 8.5m;

        /// <summary>Applies armor, pact, and support-relic speed percentages additively.</summary>
        public static decimal ResolveHorizontalSpeed(
            decimal baseSpeed,
            int armorSpeedPercent,
            PactModifierTotals pacts,
            SupportRelicModifiers support)
        {
            if (baseSpeed < 0m)
                throw new ArgumentOutOfRangeException(nameof(baseSpeed));

            decimal totalPercent = checked(checked(checked(100m + armorSpeedPercent) +
                pacts.HorizontalSpeedBonusPercent) + support.MovementSpeedBonusPercent);
            decimal resolved = checked(baseSpeed * totalPercent) / 100m;
            return Math.Min(MaximumHorizontalSpeed, resolved);
        }

        /// <summary>
        /// Applies armor and support-relic reductions to a received harmful-status duration.
        /// Positive durations retain half-up rounding and a one-tick minimum; full reduction is zero.
        /// </summary>
        public static int ResolveHarmfulDurationTicks(
            int baseTicks,
            int armorReductionPercent,
            SupportRelicModifiers support)
        {
            if (baseTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(baseTicks));
            if (baseTicks == 0)
                return 0;

            int combinedReduction = checked(armorReductionPercent +
                support.HarmfulStatusDurationReductionPercent);
            int reductionPercent = Math.Max(0, Math.Min(100, combinedReduction));
            if (reductionPercent == 100)
                return 0;

            decimal scaledTicks = checked((decimal)baseTicks * (100 - reductionPercent)) / 100m;
            decimal roundedTicks = decimal.Round(scaledTicks, 0, MidpointRounding.AwayFromZero);
            return Math.Max(1, checked((int)roundedTicks));
        }
    }
}
