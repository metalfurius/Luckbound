using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Sessions
{
    /// <summary>
    /// Mutable state owned by one game session. Id must remain stable for the actor's lifetime.
    /// </summary>
    public interface IActorState
    {
        ActorId Id { get; }
    }
}
