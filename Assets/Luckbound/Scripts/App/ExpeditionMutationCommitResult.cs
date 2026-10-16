namespace Luckbound.App
{
    /// <summary>Returns an expedition mutation only after its complete candidate snapshot is durably committed.</summary>
    public sealed class ExpeditionMutationCommitResult<T>
    {
        public ExpeditionMutationCommitResult(T outcome, bool persisted, bool pending, string message)
        {
            Outcome = outcome;
            Persisted = persisted;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        public T Outcome { get; }
        public bool Persisted { get; }
        public bool Pending { get; }
        public string Message { get; }
    }
}
