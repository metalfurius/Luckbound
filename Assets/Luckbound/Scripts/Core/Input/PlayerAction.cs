using System;

namespace Luckbound.Core.Input
{
    [Flags]
    public enum PlayerAction : ushort
    {
        None = 0,
        Jump = 1 << 0,
        Attack = 1 << 1,
        Dodge = 1 << 2,
        Skill1 = 1 << 3,
        Skill2 = 1 << 4,
        Interact = 1 << 5,
        Consumable = 1 << 6,
        Inventory = 1 << 7,
        Map = 1 << 8,
        Pause = 1 << 9
    }
}
