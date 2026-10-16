using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Stable weapon behavior identifiers used by content and runtime effect adapters.</summary>
    public enum WeaponTraitId
    {
        Balanced = 1,
        RapidCadence = 2,
        GreaterRange = 3,
        GreaterKnockback = 4,
        OptionalChargeDamageBonus = 5,
        PierceOneTarget = 6,
        FireProjectile = 7,
        SlowOnHit = 8,
        IgnoreOneDefense = 9
    }

    /// <summary>An immutable, typed weapon trait. Qualitative traits carry no invented tuning values.</summary>
    public sealed class WeaponTrait
    {
        public WeaponTrait(WeaponTraitId id, int percent = 0, float durationSeconds = 0f)
        {
            if (!Enum.IsDefined(typeof(WeaponTraitId), id)) throw new ArgumentOutOfRangeException(nameof(id));
            if (percent < 0) throw new ArgumentOutOfRangeException(nameof(percent));
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));

            switch (id)
            {
                case WeaponTraitId.OptionalChargeDamageBonus:
                    if (percent <= 0 || durationSeconds != 0f)
                        throw new ArgumentException("Optional charge damage bonus requires a positive percent and no duration.");
                    break;
                case WeaponTraitId.SlowOnHit:
                    if (percent <= 0 || durationSeconds <= 0f)
                        throw new ArgumentException("A slow trait requires a positive percent and duration.");
                    break;
                default:
                    if (percent != 0 || durationSeconds != 0f)
                        throw new ArgumentException("This qualitative or fixed-count trait does not accept numeric tuning.");
                    break;
            }

            Id = id;
            Percent = percent;
            DurationSeconds = durationSeconds;
        }

        public WeaponTraitId Id { get; }
        public string StableId => GetStableId(Id);
        public int Percent { get; }
        public float DurationSeconds { get; }
        public int PiercedTargetCount => Id == WeaponTraitId.PierceOneTarget ? 1 : 0;
        public int DefensePointsIgnored => Id == WeaponTraitId.IgnoreOneDefense ? 1 : 0;

        public static string GetStableId(WeaponTraitId id)
        {
            switch (id)
            {
                case WeaponTraitId.Balanced: return "weapon.trait.balanced";
                case WeaponTraitId.RapidCadence: return "weapon.trait.rapid_cadence";
                case WeaponTraitId.GreaterRange: return "weapon.trait.greater_range";
                case WeaponTraitId.GreaterKnockback: return "weapon.trait.greater_knockback";
                case WeaponTraitId.OptionalChargeDamageBonus: return "weapon.trait.optional_charge_damage_bonus";
                case WeaponTraitId.PierceOneTarget: return "weapon.trait.pierce_one_target";
                case WeaponTraitId.FireProjectile: return "weapon.trait.fire_projectile";
                case WeaponTraitId.SlowOnHit: return "weapon.trait.slow_on_hit";
                case WeaponTraitId.IgnoreOneDefense: return "weapon.trait.ignore_one_defense";
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        public static bool TryParseStableId(string stableId, out WeaponTraitId id)
        {
            switch (stableId)
            {
                case "weapon.trait.balanced": id = WeaponTraitId.Balanced; return true;
                case "weapon.trait.rapid_cadence": id = WeaponTraitId.RapidCadence; return true;
                case "weapon.trait.greater_range": id = WeaponTraitId.GreaterRange; return true;
                case "weapon.trait.greater_knockback": id = WeaponTraitId.GreaterKnockback; return true;
                case "weapon.trait.optional_charge_damage_bonus": id = WeaponTraitId.OptionalChargeDamageBonus; return true;
                case "weapon.trait.pierce_one_target": id = WeaponTraitId.PierceOneTarget; return true;
                case "weapon.trait.fire_projectile": id = WeaponTraitId.FireProjectile; return true;
                case "weapon.trait.slow_on_hit": id = WeaponTraitId.SlowOnHit; return true;
                case "weapon.trait.ignore_one_defense": id = WeaponTraitId.IgnoreOneDefense; return true;
                default: id = default; return false;
            }
        }
    }
}
