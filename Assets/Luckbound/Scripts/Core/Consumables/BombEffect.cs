using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Consumables
{
    /// <summary>A canonical actor position supplied by Gameplay after broad-phase filtering.</summary>
    public readonly struct BombTarget
    {
        public BombTarget(ActorId actorId, decimal x, decimal y)
        {
            if (!actorId.IsValid) throw new ArgumentException("A bomb target requires a valid actor ID.", nameof(actorId));
            ActorId = actorId;
            X = x;
            Y = y;
        }

        public ActorId ActorId { get; }

        public decimal X { get; }

        public decimal Y { get; }
    }

    /// <summary>Immutable set of actors affected by one bomb detonation.</summary>
    public sealed class BombEffectResolution
    {
        private readonly IReadOnlyList<ActorId> _targetActorIds;

        internal BombEffectResolution(ActorId[] targetActorIds)
        {
            _targetActorIds = Array.AsReadOnly((ActorId[])targetActorIds.Clone());
        }

        public int Damage => BombEffect.Damage;

        public decimal Radius => BombEffect.Radius;

        /// <summary>Unique IDs sorted by GUID so result order does not depend on physics enumeration order.</summary>
        public IReadOnlyList<ActorId> TargetActorIds => _targetActorIds;
    }

    public static class BombEffect
    {
        public const int Damage = 40;
        public const decimal Radius = 2m;

        public static BombEffectResolution ResolveTargets(decimal centerX, decimal centerY, IEnumerable<BombTarget> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            decimal radiusSquared = Radius * Radius;
            var included = new HashSet<ActorId>();
            foreach (BombTarget candidate in candidates)
            {
                if (!candidate.ActorId.IsValid)
                    throw new ArgumentException("Bomb candidates must have valid actor IDs.", nameof(candidates));

                decimal deltaX;
                decimal deltaY;
                try
                {
                    deltaX = candidate.X - centerX;
                    deltaY = candidate.Y - centerY;
                }
                catch (OverflowException)
                {
                    // Decimal overflow means the positions are far beyond the two-unit blast radius.
                    continue;
                }

                if (deltaX < -Radius || deltaX > Radius || deltaY < -Radius || deltaY > Radius) continue;
                if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    included.Add(candidate.ActorId);
            }

            var sorted = new List<ActorId>(included);
            sorted.Sort((left, right) => left.Value.CompareTo(right.Value));
            return new BombEffectResolution(sorted.ToArray());
        }
    }
}
