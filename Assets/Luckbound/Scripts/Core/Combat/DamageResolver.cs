using System;

namespace Luckbound.Core.Combat
{
    /// <summary>
    /// Pure deterministic rules for direct damage. It does not mutate actor health.
    /// </summary>
    public static class DamageResolver
    {
        public static DamageResolution Resolve(DamageCalculationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.TargetIsInvulnerable)
            {
                return new DamageResolution(0, true);
            }

            decimal damage = request.BaseDamage
                * request.RarityMultiplier
                * request.UpgradeMultiplier
                * (1m + request.OffensiveBonusPercent);

            long defenseAfterModifiers = (long)request.TargetStats.Defense + request.TargetDefenseBonus - request.DefensePenetration;
            int effectiveDefense = defenseAfterModifiers <= 0L
                ? 0
                : defenseAfterModifiers >= int.MaxValue ? int.MaxValue : (int)defenseAfterModifiers;
            damage = Math.Max(0m, damage - effectiveDefense);

            decimal reduction = Math.Min(1m, request.IncomingDamageReductionPercent);
            damage *= 1m - reduction;

            decimal roundedDamage = decimal.Round(damage, 0, MidpointRounding.AwayFromZero);
            int finalDamage = roundedDamage >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)roundedDamage);
            return new DamageResolution(finalDamage, false);
        }
    }
}
