using System;

namespace Luckbound.Core.Inventory
{
    /// <summary>Data-defined weapon and armor granted when a class starts a fresh expedition.</summary>
    public sealed class StartingLoadout
    {
        public StartingLoadout(string classId, string weaponItemId, string armorItemId)
        {
            if (string.IsNullOrWhiteSpace(classId) || !classId.StartsWith("class.", StringComparison.Ordinal))
                throw new ArgumentException("A starting loadout requires a stable class ID.", nameof(classId));
            if (string.IsNullOrWhiteSpace(weaponItemId)) throw new ArgumentException("A starting weapon ID is required.", nameof(weaponItemId));
            if (string.IsNullOrWhiteSpace(armorItemId)) throw new ArgumentException("A starting armor ID is required.", nameof(armorItemId));

            ClassId = classId;
            WeaponItemId = weaponItemId;
            ArmorItemId = armorItemId;
        }

        public string ClassId { get; }
        public string WeaponItemId { get; }
        public string ArmorItemId { get; }
    }
}
