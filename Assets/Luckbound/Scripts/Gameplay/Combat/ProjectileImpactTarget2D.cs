using System;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Actor identity adapter placed on a combatant root for projectile collider resolution.</summary>
    public sealed class ProjectileImpactTarget2D : MonoBehaviour
    {
        public ActorId ActorId { get; private set; }

        public bool IsInitialized { get; private set; }

        public void Initialize(ActorId actorId)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("ProjectileImpactTarget2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("Projectile target requires a valid actor identifier.", nameof(actorId));
            }

            ActorId = actorId;
            IsInitialized = true;
        }
    }
}
