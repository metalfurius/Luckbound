using System.Threading.Tasks;
using Luckbound.Core.Identifiers;

namespace Luckbound.Gameplay.Inventory
{
    /// <summary>Durable App-owned Fortune Brew mutation requested by the player runtime.</summary>
    public interface IPlayerFortuneBrewRuntime
    {
        Task<PlayerFortuneBrewCommitOutcome> ConsumeFortuneBrewAsync(
            ItemInstanceId itemInstanceId,
            TransactionId transactionId);
    }
}
