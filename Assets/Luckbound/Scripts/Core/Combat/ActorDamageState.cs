using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Combat
{
    /// <summary>Run-local health, direct-hit protection, and death rules for one actor.</summary>
    public sealed class ActorDamageState
    {
        private readonly HashSet<HitId> _processedHitIds = new HashSet<HitId>();
        private long _lastImpactTick = -1;
        private long _lastDamageTick = -1;

        public ActorDamageState(CombatStats stats, int directImpactProtectionTicks, int? initialHealth = null)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            if (directImpactProtectionTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(directImpactProtectionTicks));
            }

            int health = initialHealth ?? stats.MaximumHealth;
            if (health < 0 || health > stats.MaximumHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(initialHealth), "Initial health must be between zero and maximum health.");
            }

            DirectImpactProtectionTicks = directImpactProtectionTicks;
            CurrentHealth = health;
        }

        public CombatStats Stats { get; }

        public int DirectImpactProtectionTicks { get; }

        public int CurrentHealth { get; private set; }

        public bool IsDead => CurrentHealth <= 0;

        public long ProtectedUntilTick { get; private set; }

        public bool IsDirectImpactProtectedAt(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            return tick > _lastDamageTick && tick < ProtectedUntilTick;
        }

        public DirectImpactOutcome ReceiveDirectImpact(
            HitId hitId,
            DamageCalculationRequest request,
            long tick,
            bool additionalInvulnerability = false)
        {
            if (!hitId.IsValid)
            {
                throw new ArgumentException("A direct impact requires a valid HitId.", nameof(hitId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!ReferenceEquals(request.TargetStats, Stats))
            {
                throw new ArgumentException("Damage request target statistics must belong to this actor.", nameof(request));
            }

            if (tick < 0 || tick < _lastImpactTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Impact ticks must be zero or greater and cannot move backwards.");
            }

            long protectionWindowTicks = DirectImpactProtectionTicks + 1L;
            if (DirectImpactProtectionTicks > 0 && tick > long.MaxValue - protectionWindowTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Impact tick is too large to compute the protection window.");
            }

            _lastImpactTick = tick;
            if (IsDead)
            {
                return CreateIgnoredOutcome(DirectImpactStatus.IgnoredBecauseDead);
            }

            if (!_processedHitIds.Add(hitId))
            {
                return CreateIgnoredOutcome(DirectImpactStatus.DuplicateHit);
            }

            bool invulnerable = additionalInvulnerability || request.TargetIsInvulnerable ||
                (tick != _lastDamageTick && IsDirectImpactProtectedAt(tick));
            DamageResolution resolution = DamageResolver.Resolve(CopyForTarget(request, invulnerable));
            if (resolution.WasPreventedByInvulnerability)
            {
                return new DirectImpactOutcome(
                    DirectImpactStatus.PreventedByInvulnerability,
                    resolution,
                    CurrentHealth,
                    IsDead,
                    deathOccurred: false,
                    protectedUntilTick: ProtectedUntilTick);
            }

            bool wasAlive = !IsDead;
            CurrentHealth = Math.Max(0, CurrentHealth - resolution.Damage);
            _lastDamageTick = tick;
            if (DirectImpactProtectionTicks > 0)
            {
                ProtectedUntilTick = Math.Max(ProtectedUntilTick, tick + protectionWindowTicks);
            }

            return new DirectImpactOutcome(
                DirectImpactStatus.Applied,
                resolution,
                CurrentHealth,
                IsDead,
                deathOccurred: wasAlive && IsDead,
                protectedUntilTick: ProtectedUntilTick);
        }

        private DirectImpactOutcome CreateIgnoredOutcome(DirectImpactStatus status)
        {
            return new DirectImpactOutcome(
                status,
                new DamageResolution(0, wasPreventedByInvulnerability: false),
                CurrentHealth,
                IsDead,
                deathOccurred: false,
                protectedUntilTick: ProtectedUntilTick);
        }

        private static DamageCalculationRequest CopyForTarget(DamageCalculationRequest request, bool targetIsInvulnerable)
        {
            return new DamageCalculationRequest(
                request.BaseDamage,
                request.RarityMultiplier,
                request.UpgradeMultiplier,
                request.OffensiveBonusPercent,
                request.DefensePenetration,
                request.IncomingDamageReductionPercent,
                request.TargetStats,
                targetIsInvulnerable,
                request.TargetDefenseBonus);
        }
    }
}
