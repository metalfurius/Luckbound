using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Fortune
{
    public enum FortuneOperationStatus
    {
        Succeeded = 0,
        AlreadyApplied = 1,
        RejectedInvalidRequest = 2,
        RejectedInsufficientOptions = 3,
        RejectedNoCharges = 4,
        RejectedAtCapacity = 5,
        RejectedUnknownOffer = 6,
        RejectedInvalidChoice = 7,
        RejectedConflict = 8,
        RejectedNoChoice = 9
    }

    public enum FortuneRerollAvailability
    {
        Available = 0,
        NoCharges = 1,
        InsufficientOptions = 2,
        UnknownOffer = 3,
        OfferLocked = 4
    }

    /// <summary>Immutable view of the current options and choice for one reward context.</summary>
    public sealed class FortuneOffer
    {
        private readonly ReadOnlyCollection<string> _optionIds;

        internal FortuneOffer(string offerId, string contextId, IEnumerable<string> optionIds,
            string selectedOptionId, int rerollCount, bool isClaimed)
        {
            OfferId = offerId;
            ContextId = contextId;
            _optionIds = Array.AsReadOnly(new List<string>(optionIds).ToArray());
            SelectedOptionId = selectedOptionId ?? string.Empty;
            RerollCount = rerollCount;
            IsClaimed = isClaimed;
        }

        public string OfferId { get; }
        public string ContextId { get; }
        public IReadOnlyList<string> OptionIds => _optionIds;
        public string SelectedOptionId { get; }
        public int RerollCount { get; }
        public bool IsClaimed { get; }
        public string ClaimId => "fortune.claim." + OfferId;
    }

    public sealed class FortuneOfferResult
    {
        internal FortuneOfferResult(FortuneOperationStatus status, FortuneOffer offer)
        {
            Status = status;
            Offer = offer;
        }

        public FortuneOperationStatus Status { get; }
        public FortuneOffer Offer { get; }
        public bool HasOffer => Offer != null;
    }

    public sealed class FortuneRerollResult
    {
        internal FortuneRerollResult(FortuneOperationStatus status, FortuneOffer offer, int remainingCharges)
        {
            Status = status;
            Offer = offer;
            RemainingCharges = remainingCharges;
        }

        public FortuneOperationStatus Status { get; }
        public FortuneOffer Offer { get; }
        public int RemainingCharges { get; }
        public bool Succeeded => Status == FortuneOperationStatus.Succeeded || Status == FortuneOperationStatus.AlreadyApplied;
    }

    public sealed class FortuneChoiceResult
    {
        internal FortuneChoiceResult(FortuneOperationStatus status, FortuneOffer offer)
        {
            Status = status;
            Offer = offer;
        }

        public FortuneOperationStatus Status { get; }
        public FortuneOffer Offer { get; }
        public bool Succeeded => Status == FortuneOperationStatus.Succeeded || Status == FortuneOperationStatus.AlreadyApplied;
    }

    public sealed class FortuneClaimResult
    {
        internal FortuneClaimResult(FortuneOperationStatus status, string claimId, string offerId, string optionId)
        {
            Status = status;
            ClaimId = claimId ?? string.Empty;
            OfferId = offerId ?? string.Empty;
            OptionId = optionId ?? string.Empty;
        }

        public FortuneOperationStatus Status { get; }
        public string ClaimId { get; }
        public string OfferId { get; }
        public string OptionId { get; }
        public bool Succeeded => Status == FortuneOperationStatus.Succeeded || Status == FortuneOperationStatus.AlreadyApplied;
    }

    public sealed class FortuneChargeResult
    {
        internal FortuneChargeResult(FortuneOperationStatus status, int currentCharges)
        {
            Status = status;
            CurrentCharges = currentCharges;
        }

        public FortuneOperationStatus Status { get; }
        public int CurrentCharges { get; }
        public bool Succeeded => Status == FortuneOperationStatus.Succeeded || Status == FortuneOperationStatus.AlreadyApplied;
    }
}
