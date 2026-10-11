using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Input
{
    public readonly struct PlayerCommand
    {
        internal PlayerCommand(
            ActorId actorId,
            long tick,
            float moveX,
            PlayerAction pressed,
            PlayerAction held,
            PlayerAction released)
        {
            ActorId = actorId;
            Tick = tick;
            MoveX = moveX;
            Pressed = pressed;
            Held = held;
            Released = released;
        }

        public ActorId ActorId { get; }

        public long Tick { get; }

        public float MoveX { get; }

        public PlayerAction Pressed { get; }

        public PlayerAction Held { get; }

        public PlayerAction Released { get; }

        public bool WasPressed(PlayerAction action)
        {
            return (Pressed & action) == action && action != PlayerAction.None;
        }

        public bool IsHeld(PlayerAction action)
        {
            return (Held & action) == action && action != PlayerAction.None;
        }

        public bool WasReleased(PlayerAction action)
        {
            return (Released & action) == action && action != PlayerAction.None;
        }
    }
}
