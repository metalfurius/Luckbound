using Luckbound.Core.Crafting;

namespace Luckbound.App
{
    /// <summary>Reports a crafting operation only after its expedition snapshot is durably committed.</summary>
    public sealed class CraftingCommitResult
    {
        public CraftingCommitResult(CraftingOutcome outcome, bool persisted, bool pending, string message)
        {
            Outcome = outcome;
            Persisted = persisted;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        public CraftingOutcome Outcome { get; }
        public bool Persisted { get; }
        public bool Pending { get; }
        public string Message { get; }
    }
}
