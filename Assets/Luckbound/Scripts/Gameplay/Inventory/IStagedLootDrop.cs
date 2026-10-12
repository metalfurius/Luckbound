using System;

namespace Luckbound.Gameplay.Inventory
{
    /// <summary>A world drop prepared inactive until its matching Core transaction succeeds.</summary>
    public interface IStagedLootDrop : IDisposable
    {
        void Commit();
    }
}
