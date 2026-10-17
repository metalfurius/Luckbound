using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Composes a confirmed Bellator run onto its player and world-pickup components.</summary>
    public static class ExpeditionBellatorRunBinder
    {
        public static void Bind(
            ExpeditionRunConfiguration run,
            ActorId actorId,
            IPlayerInputSource inputSource,
            LayerMask groundLayers,
            LayerMask enemyLayers,
            int defense,
            PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector,
            ItemCatalog itemCatalog,
            WeaponCatalog weaponCatalog,
            SkillCatalog skillCatalog,
            LootPickupDropStager2D dropStager = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (!string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("Only a confirmed Bellator run can be bound by this composition.", nameof(run));

            WeaponProfile equippedWeaponProfile = ValidateDependencies(run, actorId, inputSource, groundLayers,
                defense, playerMotor, itemCollector, itemCatalog, weaponCatalog, skillCatalog);

            var combatStats = new CombatStats(run.StartingHealth, run.StartingEnergy, defense);

            playerMotor.Initialize(
                actorId,
                inputSource,
                groundLayers,
                combatStats,
                enemyLayers,
                raceId: run.RaceId);
            playerMotor.ConfigureEquippedWeapon(equippedWeaponProfile);
            playerMotor.ConfigureBellator(skillCatalog, run.SkillIds);
            itemCollector.Configure(run.Inventory, itemCatalog, dropStager, raceId: run.RaceId);
        }

        private static WeaponProfile ValidateDependencies(
            ExpeditionRunConfiguration run,
            ActorId actorId,
            IPlayerInputSource inputSource,
            LayerMask groundLayers,
            int defense,
            PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector,
            ItemCatalog itemCatalog,
            WeaponCatalog weaponCatalog,
            SkillCatalog skillCatalog)
        {
            if (!actorId.IsValid) throw new ArgumentException("A valid actor identifier is required.", nameof(actorId));
            if (inputSource == null) throw new ArgumentNullException(nameof(inputSource));
            if (inputSource is PlayerInputBuffer inputBuffer && inputBuffer.ActorId != actorId)
                throw new ArgumentException("The input buffer must belong to the actor being composed.", nameof(inputSource));
            if (groundLayers.value == 0) throw new ArgumentException("At least one ground layer is required.", nameof(groundLayers));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (playerMotor == null) throw new ArgumentNullException(nameof(playerMotor));
            if (itemCollector == null) throw new ArgumentNullException(nameof(itemCollector));
            if (playerMotor.gameObject != itemCollector.gameObject)
                throw new ArgumentException("The player motor and item collector must belong to the same actor.", nameof(itemCollector));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));
            if (weaponCatalog == null) throw new ArgumentNullException(nameof(weaponCatalog));
            if (skillCatalog == null) throw new ArgumentNullException(nameof(skillCatalog));
            if (run.Inventory == null || !string.Equals(run.Inventory.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("A Bellator run must carry its own Bellator expedition inventory.", nameof(run));
            if (!ReferenceEquals(run.Inventory.Catalog, itemCatalog))
                throw new ArgumentException("Pass the already-loaded item catalog owned by the run inventory.", nameof(itemCatalog));
            if (!ExpeditionRunConfiguration.IsKnownRaceId(run.RaceId))
                throw new ArgumentException("The confirmed run must contain a known race identifier.", nameof(run));
            if (playerMotor.ActorId.IsValid || playerMotor.IsBellator || playerMotor.IsVenator || playerMotor.IsMagus)
                throw new InvalidOperationException("The player motor must be uninitialized before a run is bound.");
            if (playerMotor.GetComponent<Rigidbody2D>() == null || playerMotor.GetComponent<Collider2D>() == null)
                throw new InvalidOperationException("The player motor requires a Rigidbody2D and Collider2D before composition.");
            if (Mathf.Abs(Physics2D.gravity.x) > 0.01f || Physics2D.gravity.y >= -0.01f)
                throw new InvalidOperationException("Player composition requires downward vertical 2D gravity.");

            if (run.Inventory.Weapon == null || run.Inventory.Armor == null || run.Inventory.QuickConsumable == null)
                throw new ArgumentException("A Bellator run must contain its currently equipped weapon, armor, and quick consumable.", nameof(run));

            InventoryItemStack equippedWeapon = run.Inventory.Weapon;
            if (!itemCatalog.TryGet(equippedWeapon.ItemId, out ItemDefinition weaponItem) ||
                weaponItem.Kind != ItemKind.Weapon || !weaponItem.IsCompatibleWithClass(run.ClassId))
            {
                throw new ArgumentException("The run's equipped weapon must be a compatible weapon item.", nameof(run));
            }
            if (!weaponCatalog.TryGet(equippedWeapon.ItemId, out WeaponProfile equippedWeaponProfile))
                throw new ArgumentException("The run's equipped weapon must exist in the authoritative weapon catalog.", nameof(weaponCatalog));
            if (!string.Equals(equippedWeaponProfile.ItemId, equippedWeapon.ItemId, StringComparison.Ordinal) ||
                !string.Equals(equippedWeaponProfile.ClassId, run.ClassId, StringComparison.Ordinal) ||
                !string.Equals(equippedWeaponProfile.ClassId, run.Inventory.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The authoritative weapon profile must belong to the equipped item and run class.", nameof(weaponCatalog));
            }

            // Validate the complete weapon traits and runtime mapping before Initialize mutates player components.
            _ = WeaponBasicAttackProfile.Create(equippedWeaponProfile);

            // Constructing the loadout here checks the complete Bellator catalog and both selected IDs
            // before Initialize mutates the player motor.
            _ = new BellatorSkillLoadout(skillCatalog, run.SkillIds);

            if (run.StartingHealth <= 0 || run.StartingEnergy < 0)
                throw new ArgumentException("The confirmed run contains invalid starting combat values.", nameof(run));

            return equippedWeaponProfile;
        }
    }
}
