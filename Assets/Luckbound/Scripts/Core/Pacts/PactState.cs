using System;
using System.Collections.Generic;

namespace Luckbound.Core.Pacts
{
    /// <summary>Run-local accepted-pact state with deterministic previews and health clamping.</summary>
    public sealed class PactState
    {
        public const int MaximumPactsPerExpedition = 2;

        private readonly List<string> _acceptedPactIds = new List<string>();

        public PactState(int baseMaximumHealth, int currentHealth)
        {
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (currentHealth < 0 || currentHealth > baseMaximumHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));

            BaseMaximumHealth = baseMaximumHealth;
            CurrentHealth = currentHealth;
        }

        public int BaseMaximumHealth { get; }
        public int MaximumHealth => PactModifierTotals.Sum(_acceptedPactIds).ApplyMaximumHealth(BaseMaximumHealth);
        public int CurrentHealth { get; private set; }
        public IReadOnlyList<string> AcceptedPactIds => Array.AsReadOnly(_acceptedPactIds.ToArray());
        public PactModifierTotals Modifiers => PactModifierTotals.Sum(_acceptedPactIds);

        /// <summary>Calculates the full combined result without changing accepted pacts or health.</summary>
        public PactActionResult PreviewAcceptance(string pactId)
        {
            if (!PactIds.IsKnown(pactId))
                return new PactActionResult(PactOperationStatus.RejectedUnknownPact, null);
            if (ContainsPact(pactId))
                return new PactActionResult(PactOperationStatus.AlreadyAccepted, CreatePreview(_acceptedPactIds));
            if (_acceptedPactIds.Count >= MaximumPactsPerExpedition)
                return new PactActionResult(PactOperationStatus.RejectedAtCapacity, CreatePreview(_acceptedPactIds));

            var proposed = new List<string>(_acceptedPactIds) { pactId };
            return new PactActionResult(PactOperationStatus.ReadyToAccept, CreatePreview(proposed));
        }

        public PactActionResult Accept(string pactId)
        {
            PactActionResult result = PreviewAcceptance(pactId);
            if (result.Status != PactOperationStatus.ReadyToAccept) return result;

            _acceptedPactIds.Add(pactId);
            _acceptedPactIds.Sort(StringComparer.Ordinal);
            CurrentHealth = result.Preview.ProjectedCurrentHealth;
            return new PactActionResult(PactOperationStatus.Accepted, CreatePreview(_acceptedPactIds));
        }

        public PactActionResult Remove(string pactId)
        {
            if (!PactIds.IsKnown(pactId))
                return new PactActionResult(PactOperationStatus.RejectedUnknownPact, CreatePreview(_acceptedPactIds));
            int index = _acceptedPactIds.FindIndex(id => string.Equals(id, pactId, StringComparison.Ordinal));
            if (index < 0)
                return new PactActionResult(PactOperationStatus.NotAccepted, CreatePreview(_acceptedPactIds));

            _acceptedPactIds.RemoveAt(index);
            PactPreview preview = CreatePreview(_acceptedPactIds);
            CurrentHealth = preview.ProjectedCurrentHealth;
            return new PactActionResult(PactOperationStatus.Removed, preview);
        }

        /// <summary>Synchronizes health from the owning combat state while respecting pact-adjusted maximum health.</summary>
        public void SetCurrentHealth(int currentHealth)
        {
            if (currentHealth < 0 || currentHealth > MaximumHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            CurrentHealth = currentHealth;
        }

        public PactSnapshot CreateSnapshot()
        {
            return new PactSnapshot(PactSnapshot.CurrentVersion, BaseMaximumHealth, CurrentHealth, _acceptedPactIds);
        }

        public static PactState Restore(PactSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var restored = new PactState(snapshot.BaseMaximumHealth, snapshot.CurrentHealth);
            foreach (string pactId in snapshot.AcceptedPactIds)
                restored._acceptedPactIds.Add(pactId);
            restored._acceptedPactIds.Sort(StringComparer.Ordinal);

            if (restored.CurrentHealth > restored.MaximumHealth)
                throw new FormatException("Pact snapshot health exceeds its pact-adjusted maximum.");
            return restored;
        }

        private bool ContainsPact(string pactId)
        {
            return _acceptedPactIds.Exists(id => string.Equals(id, pactId, StringComparison.Ordinal));
        }

        private PactPreview CreatePreview(IEnumerable<string> pactIds)
        {
            return new PactPreview(pactIds, BaseMaximumHealth, CurrentHealth);
        }
    }
}
