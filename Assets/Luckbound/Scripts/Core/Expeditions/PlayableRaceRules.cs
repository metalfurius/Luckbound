using System;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Pure, immutable rules for the four playable races.</summary>
    public static class PlayableRaceRules
    {
        private const int OrciniOutgoingKnockbackPercent = 110;
        private const int AlvariSkillCostPercent = 95;

        public const string HumanRaceId = "race.human";
        public const string OrciniRaceId = "race.orcini";
        public const string AlvariRaceId = "race.alvari";
        public const string DvergarRaceId = "race.dvergar";

        public const decimal OrciniOutgoingKnockbackMultiplier = OrciniOutgoingKnockbackPercent / 100m;
        public const decimal AlvariSkillCostMultiplier = AlvariSkillCostPercent / 100m;

        /// <summary>Returns whether the ID names one of the four playable races.</summary>
        public static bool IsSupportedRace(string raceId)
        {
            return string.Equals(raceId, HumanRaceId, StringComparison.Ordinal) ||
                   string.Equals(raceId, OrciniRaceId, StringComparison.Ordinal) ||
                   string.Equals(raceId, AlvariRaceId, StringComparison.Ordinal) ||
                   string.Equals(raceId, DvergarRaceId, StringComparison.Ordinal);
        }

        /// <summary>Returns the race's outgoing-knockback multiplier.</summary>
        public static decimal GetOutgoingKnockbackMultiplier(string raceId)
        {
            ValidateRaceId(raceId);
            return string.Equals(raceId, OrciniRaceId, StringComparison.Ordinal)
                ? OrciniOutgoingKnockbackMultiplier
                : 1m;
        }

        /// <summary>Returns the race's skill-cost multiplier.</summary>
        public static decimal GetSkillCostMultiplier(string raceId)
        {
            ValidateRaceId(raceId);
            return string.Equals(raceId, AlvariRaceId, StringComparison.Ordinal)
                ? AlvariSkillCostMultiplier
                : 1m;
        }

        /// <summary>
        /// Applies the Alvari discount to a positive skill cost. Integer costs use half-up rounding,
        /// matching the permanent-stat calculation in PermanentUpgradeRules.
        /// </summary>
        public static int ApplySkillCost(string raceId, int baseCost)
        {
            ValidateRaceId(raceId);
            if (baseCost <= 0) throw new ArgumentOutOfRangeException(nameof(baseCost), "A skill cost must be positive.");

            if (!string.Equals(raceId, AlvariRaceId, StringComparison.Ordinal)) return baseCost;

            long numerator = (long)baseCost * AlvariSkillCostPercent + 50L;
            return (int)(numerator / 100L);
        }

        /// <summary>Returns true only for Dvergar, whose bonus applies to material pickups from the world.</summary>
        public static bool HasWorldPickupBonus(string raceId)
        {
            ValidateRaceId(raceId);
            return string.Equals(raceId, DvergarRaceId, StringComparison.Ordinal);
        }

        /// <summary>Returns the starting Fortune charges for a race.</summary>
        public static int GetStartingFortuneCharges(string raceId)
        {
            ValidateRaceId(raceId);
            return string.Equals(raceId, HumanRaceId, StringComparison.Ordinal) ? 2 : 1;
        }

        private static void ValidateRaceId(string raceId)
        {
            if (raceId == null) throw new ArgumentNullException(nameof(raceId));
            if (!IsSupportedRace(raceId)) throw new ArgumentException("Unknown playable race ID.", nameof(raceId));
        }
    }
}
