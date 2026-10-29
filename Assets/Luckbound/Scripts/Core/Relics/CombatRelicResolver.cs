using System;

namespace Luckbound.Core.Relics
{
    /// <summary>Stable IDs for the eight combat relic definitions in the product plan.</summary>
    public static class CombatRelicIds
    {
        public const string UmbraFang = "relic.umbra_fang";
        public const string ShatteredClock = "relic.shattered_clock";
        public const string ArrowFeather = "relic.arrow_feather";
        public const string InlaidEmber = "relic.inlaid_ember";
        public const string FirstEchoMedallion = "relic.first_echo_medallion";
        public const string BlackNeedle = "relic.black_needle";
        public const string HunterRing = "relic.hunter_ring";
        public const string OakMace = "relic.oak_mace";

        /// <summary>Returns whether the ID names one of the eight combat relics.</summary>
        public static bool IsSupported(string relicId)
        {
            return string.Equals(relicId, UmbraFang, StringComparison.Ordinal) ||
                string.Equals(relicId, ShatteredClock, StringComparison.Ordinal) ||
                string.Equals(relicId, ArrowFeather, StringComparison.Ordinal) ||
                string.Equals(relicId, InlaidEmber, StringComparison.Ordinal) ||
                string.Equals(relicId, FirstEchoMedallion, StringComparison.Ordinal) ||
                string.Equals(relicId, BlackNeedle, StringComparison.Ordinal) ||
                string.Equals(relicId, HunterRing, StringComparison.Ordinal) ||
                string.Equals(relicId, OakMace, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Immutable combat-relic values. Percentages are signed modifiers to the named base value.
    /// This value contains effect parameters only; consumers own event and per-target state.
    /// </summary>
    public readonly struct CombatRelicModifiers
    {
        internal CombatRelicModifiers(
            int basicAttackDamageBonusPercent,
            int basicAttackIntervalModifierPercent,
            int projectileSpeedBonusPercent,
            int basicAttackBurnDamagePerSecond,
            int basicAttackBurnDurationSeconds,
            int firstImpactPerEnemyDamageBonusPercent,
            int defensePenetration,
            int killHealAmount,
            int killHealMinimumIntervalMilliseconds,
            int knockbackBonusPercent)
        {
            BasicAttackDamageBonusPercent = basicAttackDamageBonusPercent;
            BasicAttackIntervalModifierPercent = basicAttackIntervalModifierPercent;
            ProjectileSpeedBonusPercent = projectileSpeedBonusPercent;
            BasicAttackBurnDamagePerSecond = basicAttackBurnDamagePerSecond;
            BasicAttackBurnDurationSeconds = basicAttackBurnDurationSeconds;
            FirstImpactPerEnemyDamageBonusPercent = firstImpactPerEnemyDamageBonusPercent;
            DefensePenetration = defensePenetration;
            KillHealAmount = killHealAmount;
            KillHealMinimumIntervalMilliseconds = killHealMinimumIntervalMilliseconds;
            KnockbackBonusPercent = knockbackBonusPercent;
        }

        public int BasicAttackDamageBonusPercent { get; }
        /// <summary>Signed change to the basic-attack interval; the Shattered Clock is -8.</summary>
        public int BasicAttackIntervalModifierPercent { get; }
        public int ProjectileSpeedBonusPercent { get; }
        public int BasicAttackBurnDamagePerSecond { get; }
        public int BasicAttackBurnDurationSeconds { get; }
        /// <summary>Apply only to the first impact against each enemy; tracking is external.</summary>
        public int FirstImpactPerEnemyDamageBonusPercent { get; }
        public int DefensePenetration { get; }
        public int KillHealAmount { get; }
        /// <summary>Minimum spacing between kill-heal events, in milliseconds; tracking is external.</summary>
        public int KillHealMinimumIntervalMilliseconds { get; }
        public int KnockbackBonusPercent { get; }

        internal CombatRelicModifiers Add(CombatRelicModifiers other)
        {
            return new CombatRelicModifiers(
                checked(BasicAttackDamageBonusPercent + other.BasicAttackDamageBonusPercent),
                checked(BasicAttackIntervalModifierPercent + other.BasicAttackIntervalModifierPercent),
                checked(ProjectileSpeedBonusPercent + other.ProjectileSpeedBonusPercent),
                checked(BasicAttackBurnDamagePerSecond + other.BasicAttackBurnDamagePerSecond),
                checked(BasicAttackBurnDurationSeconds + other.BasicAttackBurnDurationSeconds),
                checked(FirstImpactPerEnemyDamageBonusPercent + other.FirstImpactPerEnemyDamageBonusPercent),
                checked(DefensePenetration + other.DefensePenetration),
                checked(KillHealAmount + other.KillHealAmount),
                checked(KillHealMinimumIntervalMilliseconds + other.KillHealMinimumIntervalMilliseconds),
                checked(KnockbackBonusPercent + other.KnockbackBonusPercent));
        }
    }

    /// <summary>Purely resolves the modifiers from the two equipped combat-relic definition IDs.</summary>
    public static class CombatRelicResolver
    {
        /// <summary>
        /// Resolves exactly two equipment slots. A null ID is an empty slot. Unknown IDs and
        /// duplicate definitions are rejected. No actor, target, timer, or mutable game state is read.
        /// </summary>
        public static CombatRelicModifiers Resolve(string relicOneId, string relicTwoId)
        {
            ValidateSlot(relicOneId, nameof(relicOneId));
            ValidateSlot(relicTwoId, nameof(relicTwoId));

            if (relicOneId != null && string.Equals(relicOneId, relicTwoId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The same combat relic definition cannot occupy both equipment slots.",
                    nameof(relicTwoId));
            }

            CombatRelicModifiers first = GetModifiers(relicOneId);
            CombatRelicModifiers second = GetModifiers(relicTwoId);
            return first.Add(second);
        }

        private static void ValidateSlot(string relicId, string parameterName)
        {
            if (relicId != null && !CombatRelicIds.IsSupported(relicId))
            {
                throw new ArgumentException("Relic ID must name a supported combat relic.", parameterName);
            }
        }

        private static CombatRelicModifiers GetModifiers(string relicId)
        {
            switch (relicId)
            {
                case CombatRelicIds.UmbraFang:
                    return new CombatRelicModifiers(10, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                case CombatRelicIds.ShatteredClock:
                    return new CombatRelicModifiers(0, -8, 0, 0, 0, 0, 0, 0, 0, 0);
                case CombatRelicIds.ArrowFeather:
                    return new CombatRelicModifiers(0, 0, 20, 0, 0, 0, 0, 0, 0, 0);
                case CombatRelicIds.InlaidEmber:
                    return new CombatRelicModifiers(0, 0, 0, 2, 3, 0, 0, 0, 0, 0);
                case CombatRelicIds.FirstEchoMedallion:
                    return new CombatRelicModifiers(0, 0, 0, 0, 0, 25, 0, 0, 0, 0);
                case CombatRelicIds.BlackNeedle:
                    return new CombatRelicModifiers(0, 0, 0, 0, 0, 0, 2, 0, 0, 0);
                case CombatRelicIds.HunterRing:
                    return new CombatRelicModifiers(0, 0, 0, 0, 0, 0, 0, 2, 500, 0);
                case CombatRelicIds.OakMace:
                    return new CombatRelicModifiers(0, 0, 0, 0, 0, 0, 0, 0, 0, 25);
                case null:
                    return default;
                default:
                    throw new ArgumentException("Relic ID must name a supported combat relic.", nameof(relicId));
            }
        }
    }
}
