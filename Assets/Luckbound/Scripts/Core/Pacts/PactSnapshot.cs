using System;
using System.Collections.Generic;
using System.Linq;

namespace Luckbound.Core.Pacts
{
    /// <summary>Validated immutable persistence value for one expedition's pact state.</summary>
    public sealed class PactSnapshot
    {
        public const int CurrentVersion = 1;

        private readonly string[] _acceptedPactIds;
        private readonly IReadOnlyList<string> _acceptedPactIdView;

        public PactSnapshot(
            int snapshotVersion,
            int baseMaximumHealth,
            int currentHealth,
            IEnumerable<string> acceptedPactIds)
        {
            if (snapshotVersion != CurrentVersion)
                throw new ArgumentOutOfRangeException(nameof(snapshotVersion), "Unsupported pact snapshot version.");
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (acceptedPactIds == null) throw new ArgumentNullException(nameof(acceptedPactIds));

            string[] pactIds = acceptedPactIds.ToArray();
            PactModifierTotals modifiers = PactModifierTotals.Sum(pactIds);
            Array.Sort(pactIds, StringComparer.Ordinal);
            int maximumHealth = modifiers.ApplyMaximumHealth(baseMaximumHealth);
            if (currentHealth < 0 || currentHealth > maximumHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth),
                    "Saved health must fit within the maximum health after pact effects.");

            SnapshotVersion = snapshotVersion;
            BaseMaximumHealth = baseMaximumHealth;
            CurrentHealth = currentHealth;
            _acceptedPactIds = pactIds;
            _acceptedPactIdView = Array.AsReadOnly(_acceptedPactIds);
        }

        public int SnapshotVersion { get; }
        public int BaseMaximumHealth { get; }
        public int CurrentHealth { get; }
        public IReadOnlyList<string> AcceptedPactIds => _acceptedPactIdView;
    }
}
