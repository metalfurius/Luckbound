using System;

namespace Luckbound.Core.Economy
{
    /// <summary>One rest stop's fixed, expedition-bound merchant stock.</summary>
    [Serializable]
    public sealed class MerchantStockSnapshot
    {
        public string roomInstanceId;
        public MerchantOfferSnapshot[] offers = Array.Empty<MerchantOfferSnapshot>();

        public MerchantStockSnapshot Clone()
        {
            return new MerchantStockSnapshot
            {
                roomInstanceId = roomInstanceId,
                offers = offers == null ? null : Array.ConvertAll(offers, offer => offer == null ? null : offer.Clone())
            };
        }
    }
}
