using System;
using Luckbound.Core.Consumables;
using Luckbound.Core.Fortune;
using Luckbound.Core.Inventory;

namespace Luckbound.Gameplay.Inventory
{
    /// <summary>Result of the durable Fortune Brew and quick-stack transaction.</summary>
    public sealed class PlayerFortuneBrewCommitOutcome
    {
        public PlayerFortuneBrewCommitOutcome(
            bool persisted,
            FortuneOperationStatus status,
            int currentCharges,
            ExpeditionInventory committedInventory)
        {
            if (!Enum.IsDefined(typeof(FortuneOperationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (currentCharges < 0 || currentCharges > FortuneBrewEffect.MaximumCharges)
                throw new ArgumentOutOfRangeException(nameof(currentCharges));
            if (persisted && committedInventory == null)
                throw new ArgumentNullException(nameof(committedInventory), "A persisted Fortune Brew mutation must provide its committed inventory.");

            Persisted = persisted;
            Status = status;
            CurrentCharges = currentCharges;
            CommittedInventory = committedInventory;
        }

        public bool Persisted { get; }

        public FortuneOperationStatus Status { get; }

        public int CurrentCharges { get; }

        public ExpeditionInventory CommittedInventory { get; }

        public bool Succeeded => Persisted &&
            (Status == FortuneOperationStatus.Succeeded || Status == FortuneOperationStatus.AlreadyApplied);
    }
}
