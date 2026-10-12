using System.Collections.Generic;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Combat
{
    /// <summary>One transient attack window and the actor targets already struck by it.</summary>
    public sealed class MeleeHitWindow
    {
        private readonly HashSet<ActorId> _registeredTargets = new HashSet<ActorId>();

        internal MeleeHitWindow(HitId id, decimal baseDamage, decimal damageMultiplier)
        {
            Id = id;
            BaseDamage = baseDamage;
            DamageMultiplier = damageMultiplier;
        }

        public HitId Id { get; }

        public decimal BaseDamage { get; }

        public decimal DamageMultiplier { get; }

        public int RegisteredTargetCount => _registeredTargets.Count;

        /// <summary>Returns false for invalid IDs and duplicate colliders resolving to the same actor.</summary>
        public bool TryRegisterTarget(ActorId actorId)
        {
            return actorId.IsValid && _registeredTargets.Add(actorId);
        }
    }
}
