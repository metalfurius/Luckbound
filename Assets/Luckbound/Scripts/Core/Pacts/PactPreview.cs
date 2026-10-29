using System;
using System.Collections.Generic;

namespace Luckbound.Core.Pacts
{
    public enum PactOperationStatus
    {
        ReadyToAccept = 0,
        Accepted = 1,
        AlreadyAccepted = 2,
        RejectedAtCapacity = 3,
        RejectedUnknownPact = 4,
        Removed = 5,
        NotAccepted = 6
    }

    /// <summary>Immutable outcome of previewing or changing an expedition's accepted pacts.</summary>
    public sealed class PactActionResult
    {
        internal PactActionResult(PactOperationStatus status, PactPreview preview)
        {
            Status = status;
            Preview = preview;
        }

        public PactOperationStatus Status { get; }
        public PactPreview Preview { get; }
        public bool Succeeded => Status == PactOperationStatus.Accepted || Status == PactOperationStatus.Removed;
    }

    /// <summary>Immutable combined pact effects and health values for a specific accepted-pact set.</summary>
    public sealed class PactPreview
    {
        private readonly string[] _pactIds;
        private readonly IReadOnlyList<string> _pactIdView;

        internal PactPreview(IEnumerable<string> pactIds, int baseMaximumHealth, int currentHealth)
        {
            if (pactIds == null) throw new ArgumentNullException(nameof(pactIds));
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (currentHealth < 0 || currentHealth > baseMaximumHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));

            _pactIds = new List<string>(pactIds).ToArray();
            Array.Sort(_pactIds, StringComparer.Ordinal);
            _pactIdView = Array.AsReadOnly(_pactIds);
            Modifiers = PactModifierTotals.Sum(_pactIds);
            BaseMaximumHealth = baseMaximumHealth;
            MaximumHealth = Modifiers.ApplyMaximumHealth(baseMaximumHealth);
            ProjectedCurrentHealth = Math.Min(currentHealth, MaximumHealth);
        }

        public IReadOnlyList<string> PactIds => _pactIdView;
        public PactModifierTotals Modifiers { get; }
        public int BaseMaximumHealth { get; }
        public int MaximumHealth { get; }
        public int ProjectedCurrentHealth { get; }

        public int PreviewGoldReward(int baseReward) => Modifiers.ApplyGoldReward(baseReward);
        public int PreviewSkillCost(int baseCost) => Modifiers.ApplySkillCost(baseCost);
        public decimal PreviewHorizontalSpeed(decimal baseSpeed) => Modifiers.ApplyHorizontalSpeed(baseSpeed);
        public int PreviewDefense(int baseDefense) => Modifiers.ApplyDefense(baseDefense);
        public decimal PreviewSkillCooldown(decimal baseCooldownSeconds) =>
            Modifiers.ApplySkillCooldown(baseCooldownSeconds);
        public int PreviewPurchasePrice(int basePrice) => Modifiers.ApplyPurchasePrice(basePrice);
    }
}
