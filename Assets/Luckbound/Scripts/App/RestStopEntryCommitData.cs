using Luckbound.Core.Economy;

namespace Luckbound.App
{
    /// <summary>Durable result of entering a rest room, including its fixed merchant stock.</summary>
    public sealed class RestStopEntryCommitData
    {
        public RestStopEntryCommitData(RestStopOutcome restOutcome, MerchantStockSnapshot merchantStock)
        {
            RestOutcome = restOutcome;
            MerchantStock = merchantStock == null ? null : merchantStock.Clone();
        }

        public RestStopOutcome RestOutcome { get; }
        public MerchantStockSnapshot MerchantStock { get; }
    }
}
