using System;

namespace Luckbound.Core.Economy
{
    /// <summary>Persisted stock count for one stable item offer.</summary>
    [Serializable]
    public sealed class MerchantOfferSnapshot
    {
        public string itemId;
        public int rarity;
        public int initialQuantity;
        public int remainingQuantity;

        public MerchantOfferSnapshot Clone()
        {
            return new MerchantOfferSnapshot
            {
                itemId = itemId,
                rarity = rarity,
                initialQuantity = initialQuantity,
                remainingQuantity = remainingQuantity
            };
        }
    }
}
