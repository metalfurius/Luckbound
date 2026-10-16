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
        public FortuneRerollReceiptSnapshot[] rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>();
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

    /// <summary>Durable replay data for a reroll transaction, including its original rejected result.</summary>
    [Serializable]
    public sealed class FortuneRerollReceiptSnapshot
    {
        public string transactionId;
        public string offerId;
        public int status;
        public string contextId;
        public string[] optionIds = Array.Empty<string>();
        public string selectedOptionId;
        public int rerollCount;
        public bool claimed;
        public int remainingCharges;
        public bool hasOffer;
    }
}
