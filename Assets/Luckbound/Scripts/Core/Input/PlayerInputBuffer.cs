using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Input
{
    public sealed class PlayerInputBuffer : IPlayerInputSource
    {
        private const PlayerAction AllActions =
            PlayerAction.Jump | PlayerAction.Attack | PlayerAction.Dodge | PlayerAction.Skill1 |
            PlayerAction.Skill2 | PlayerAction.Interact | PlayerAction.Consumable |
            PlayerAction.Inventory | PlayerAction.Map | PlayerAction.Pause;

        private readonly ActorId _actorId;
        private PlayerAction _pressed;
        private PlayerAction _held;
        private PlayerAction _released;
        private long _lastConsumedTick = -1;
        private float _moveX;

        public PlayerInputBuffer(ActorId actorId)
        {
            if (!actorId.IsValid)
            {
                throw new ArgumentException("A player input buffer requires a valid actor identifier.", nameof(actorId));
            }

            _actorId = actorId;
        }

        public ActorId ActorId => _actorId;

        public void SetMoveX(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Movement input must be finite.");
            }

            _moveX = Math.Max(-1f, Math.Min(1f, value));
        }

        public void Press(PlayerAction action)
        {
            ValidateSingleAction(action);
            if ((_held & action) != 0)
            {
                return;
            }

            _held |= action;
            _pressed |= action;
        }

        public void Release(PlayerAction action)
        {
            ValidateSingleAction(action);
            if ((_held & action) == 0)
            {
                return;
            }

            _held &= ~action;
            _released |= action;
        }

        public void ReleaseAll()
        {
            foreach (PlayerAction action in SingleActions())
            {
                Release(action);
            }

            _moveX = 0f;
        }

        public PlayerCommand Consume(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            }

            if (tick <= _lastConsumedTick)
            {
                throw new InvalidOperationException("A player input buffer can only be consumed once per increasing tick.");
            }

            _lastConsumedTick = tick;
            var command = new PlayerCommand(_actorId, tick, _moveX, _pressed, _held, _released);
            _pressed = PlayerAction.None;
            _released = PlayerAction.None;
            return command;
        }

        private static void ValidateSingleAction(PlayerAction action)
        {
            int value = (int)action;
            if (action == PlayerAction.None || (action & AllActions) != action || (value & (value - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(action), action, "Exactly one defined player action is required.");
            }
        }

        private static PlayerAction[] SingleActions()
        {
            return new[]
            {
                PlayerAction.Jump, PlayerAction.Attack, PlayerAction.Dodge, PlayerAction.Skill1,
                PlayerAction.Skill2, PlayerAction.Interact, PlayerAction.Consumable,
                PlayerAction.Inventory, PlayerAction.Map, PlayerAction.Pause
            };
        }
    }
}
