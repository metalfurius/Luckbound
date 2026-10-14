using System;

namespace Luckbound.Core.Fortune
{
    /// <summary>Plain serializable state needed to resume offers, charges, and claim receipts.</summary>
    [Serializable]
    public sealed class FortuneOfferBookSnapshot
    {
        public int snapshotVersion;
        public int generationVersion;
        public uint seed;
        public string raceId;
        public int charges;
        public FortuneOfferSnapshot[] offers = Array.Empty<FortuneOfferSnapshot>();
        public string[] processedGuardianIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FortuneOfferSnapshot
    {
        public string offerId;
        public string contextId;
        public string[] eligibleOptionIds = Array.Empty<string>();
        public FortuneOfferHistorySnapshot[] history = Array.Empty<FortuneOfferHistorySnapshot>();
        public string selectedOptionId;
        public bool claimed;
    }

    [Serializable]
    public sealed class FortuneOfferHistorySnapshot
    {
        public string[] optionIds = Array.Empty<string>();
    }
}
