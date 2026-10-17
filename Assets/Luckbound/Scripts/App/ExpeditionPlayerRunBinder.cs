using System;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Composes a confirmed run onto a fresh player actor for its selected class.</summary>
    public static class ExpeditionPlayerRunBinder
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
            ProjectilePool2D projectilePool = null,
            RoomProjectileScope projectileScope = null,
            RoomLifetimeScope roomLifetimeScope = null,
            LootPickupDropStager2D dropStager = null)
        {
            BindingPlan plan = Validate(
                run,
                actorId,
                inputSource,
                groundLayers,
                enemyLayers,
                defense,
                playerMotor,
                itemCollector,
                itemCatalog,
                weaponCatalog,
                skillCatalog,
                projectilePool,
                projectileScope,
                roomLifetimeScope);

            if (string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                ExpeditionBellatorRunBinder.Bind(
                    run,
                    actorId,
                    inputSource,
                    groundLayers,
                    plan.EnemyLayers,
                    defense,
                    playerMotor,
                    itemCollector,
                    itemCatalog,
                    weaponCatalog,
                    skillCatalog,
                    dropStager);
                return;
            }

            playerMotor.Initialize(
                actorId,
                inputSource,
                groundLayers,
                plan.CombatStats,
                plan.EnemyLayers,
                run.RaceId,
                plan.ArmorResolution);
            playerMotor.ConfigureEquippedWeapon(plan.EquippedWeapon);

            if (string.Equals(run.ClassId, VenatorSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                playerMotor.ConfigureVenator(
                    skillCatalog,
                    run.SkillIds,
                    projectilePool,
                    projectileScope,
                    roomLifetimeScope,
                    plan.EnemyLayers);
            }
            else
            {
                playerMotor.ConfigureMagus(
                    skillCatalog,
                    run.SkillIds,
                    projectilePool,
                    projectileScope,
                    roomLifetimeScope,
                    plan.EnemyLayers);
            }

            itemCollector.Configure(run.Inventory, itemCatalog, dropStager, run.RaceId);
        }

        private static BindingPlan Validate(
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
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.ClassId != BellatorSkillLoadout.ClassId &&
                run.ClassId != VenatorSkillLoadout.ClassId &&
                run.ClassId != MagusSkillLoadout.ClassId)
            {
                throw new ArgumentException("The confirmed run must select one of the three playable classes.", nameof(run));
            }
            if (!ExpeditionRunConfiguration.IsKnownRaceId(run.RaceId))
                throw new ArgumentException("The confirmed run must contain a known race identifier.", nameof(run));
            if (!actorId.IsValid) throw new ArgumentException("A valid actor identifier is required.", nameof(actorId));
            if (inputSource == null) throw new ArgumentNullException(nameof(inputSource));
            if (inputSource is PlayerInputBuffer inputBuffer && inputBuffer.ActorId != actorId)
                throw new ArgumentException("The input buffer must belong to the actor being composed.", nameof(inputSource));
            if (groundLayers.value == 0)
                throw new ArgumentException("At least one ground layer is required.", nameof(groundLayers));
            LayerMask effectiveEnemyLayers = enemyLayers.value != 0 ? enemyLayers : LayerMask.GetMask("Enemies");
            if (effectiveEnemyLayers.value == 0)
                throw new ArgumentException("At least one enemy layer is required for the player's attacks.", nameof(enemyLayers));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (playerMotor == null) throw new ArgumentNullException(nameof(playerMotor));
            if (itemCollector == null) throw new ArgumentNullException(nameof(itemCollector));
            if (playerMotor.gameObject != itemCollector.gameObject)
                throw new ArgumentException("The player motor and item collector must belong to the same actor.", nameof(itemCollector));
            if (!playerMotor.isActiveAndEnabled || !itemCollector.isActiveAndEnabled || !playerMotor.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The player actor and its composition components must be active.");
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));
            if (weaponCatalog == null) throw new ArgumentNullException(nameof(weaponCatalog));
            if (skillCatalog == null) throw new ArgumentNullException(nameof(skillCatalog));

            ExpeditionInventory inventory = run.Inventory;
            if (inventory == null || !string.Equals(inventory.ClassId, run.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("The run must carry its own inventory for the selected class.", nameof(run));
            if (!ReferenceEquals(inventory.Catalog, itemCatalog))
                throw new ArgumentException("Pass the already-loaded item catalog owned by the run inventory.", nameof(itemCatalog));

            ValidateEquippedItems(run, itemCatalog);

            if (playerMotor.ActorId.IsValid || playerMotor.CombatStats != null ||
                playerMotor.IsBellator || playerMotor.IsVenator || playerMotor.IsMagus)
            {
                throw new InvalidOperationException("The player motor must be uninitialized before a run is bound.");
            }
            if (itemCollector.Inventory != null || itemCollector.Catalog != null)
                throw new InvalidOperationException("The item collector must be unconfigured before a run is bound.");
            Rigidbody2D body = playerMotor.GetComponent<Rigidbody2D>();
            Collider2D collider = playerMotor.GetComponent<Collider2D>();
            if (body == null || collider == null || collider.attachedRigidbody != body)
                throw new InvalidOperationException("The player motor requires a Rigidbody2D and an attached Collider2D before composition.");
            if (Mathf.Abs(Physics2D.gravity.x) > 0.01f || Physics2D.gravity.y >= -0.01f)
                throw new InvalidOperationException("Player composition requires downward vertical 2D gravity.");

            ArmorResolution armorResolution = ResolveEquippedArmor(run, itemCatalog);
            CombatStats combatStats = CreateArmorCombatStats(defense, armorResolution);
            WeaponProfile equippedWeapon = PlayerMotor2D.ResolveEquippedWeaponProfile(run, weaponCatalog);
            ValidateSkillLoadout(run, skillCatalog);

            if (!string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                ValidateRoomProjectileDependencies(
                    projectilePool,
                    projectileScope,
                    roomLifetimeScope);
            }

            return new BindingPlan(combatStats, equippedWeapon, effectiveEnemyLayers, armorResolution);
        }

        internal static ArmorResolution ResolveEquippedArmor(ExpeditionRunConfiguration run, ItemCatalog itemCatalog)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));
            if (run.Inventory == null || !string.Equals(run.Inventory.ClassId, run.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("The run must carry its own inventory for the selected class.", nameof(run));
            if (!ReferenceEquals(run.Inventory.Catalog, itemCatalog))
                throw new ArgumentException("Pass the already-loaded item catalog owned by the run inventory.", nameof(itemCatalog));

            InventoryItemStack equippedArmor = run.Inventory.Armor;
            if (equippedArmor == null || !equippedArmor.InstanceId.IsValid ||
                !itemCatalog.TryGet(equippedArmor.ItemId, out ItemDefinition armorItem) ||
                !string.Equals(armorItem.Id, equippedArmor.ItemId, StringComparison.Ordinal) ||
                armorItem.Kind != ItemKind.Armor ||
                !ArmorCatalog.Standard.TryGet(equippedArmor.ItemId, out ArmorProfile profile) ||
                !string.Equals(profile.ItemId, equippedArmor.ItemId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The run's equipped armor must match an exact item ID in the item and standard armor catalogs.",
                    nameof(run));
            }

            return ArmorStatsResolver.Resolve(
                profile,
                equippedArmor.UpgradeLevel,
                run.StartingHealth,
                run.StartingEnergy,
                run.StartingHealth,
                run.StartingEnergy);
        }

        internal static CombatStats CreateArmorCombatStats(int baseDefense, ArmorResolution armorResolution)
        {
            if (armorResolution == null) throw new ArgumentNullException(nameof(armorResolution));
            int totalDefense = checked(baseDefense + armorResolution.Defense);
            return new CombatStats(armorResolution.MaximumHealth, armorResolution.MaximumEnergy, totalDefense);
        }

        private static void ValidateEquippedItems(ExpeditionRunConfiguration run, ItemCatalog itemCatalog)
        {
            ExpeditionInventory inventory = run.Inventory;
            if (inventory.Weapon == null || inventory.Armor == null || inventory.QuickConsumable == null)
            {
                throw new ArgumentException("A confirmed run must carry its equipped weapon, armor, and quick consumable.", nameof(run));
            }

            ValidateItem(inventory.Weapon, ItemKind.Weapon, itemCatalog, run.ClassId, requireWeaponCompatibility: true);
            ValidateItem(inventory.Armor, ItemKind.Armor, itemCatalog, run.ClassId, requireWeaponCompatibility: false);
            ValidateItem(inventory.QuickConsumable, ItemKind.Consumable, itemCatalog, run.ClassId, requireWeaponCompatibility: false);
            if (inventory.RelicOne != null)
                ValidateItem(inventory.RelicOne, ItemKind.Relic, itemCatalog, run.ClassId, requireWeaponCompatibility: false);
            if (inventory.RelicTwo != null)
                ValidateItem(inventory.RelicTwo, ItemKind.Relic, itemCatalog, run.ClassId, requireWeaponCompatibility: false);
        }

        private static void ValidateItem(
            InventoryItemStack stack,
            ItemKind expectedKind,
            ItemCatalog itemCatalog,
            string classId,
            bool requireWeaponCompatibility)
        {
            if (stack == null || !stack.InstanceId.IsValid || string.IsNullOrWhiteSpace(stack.ItemId) ||
                !itemCatalog.TryGet(stack.ItemId, out ItemDefinition definition) ||
                !string.Equals(definition.Id, stack.ItemId, StringComparison.Ordinal) ||
                definition.Kind != expectedKind ||
                (requireWeaponCompatibility && !definition.IsCompatibleWithClass(classId)))
            {
                throw new ArgumentException("The run contains an equipped item with an invalid ID, category, or class compatibility.", nameof(classId));
            }
        }

        private static void ValidateSkillLoadout(ExpeditionRunConfiguration run, SkillCatalog skillCatalog)
        {
            if (string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
                _ = new BellatorSkillLoadout(skillCatalog, run.SkillIds);
            else if (string.Equals(run.ClassId, VenatorSkillLoadout.ClassId, StringComparison.Ordinal))
                _ = new VenatorSkillLoadout(skillCatalog, run.SkillIds);
            else
                _ = new MagusSkillLoadout(skillCatalog, run.SkillIds);
        }

        private static void ValidateRoomProjectileDependencies(
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope)
        {
            if (projectilePool == null) throw new ArgumentNullException(nameof(projectilePool));
            if (!projectilePool.isActiveAndEnabled || !projectilePool.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Class projectiles require an active projectile pool.");
            if (!projectilePool.IsInitialized)
                throw new InvalidOperationException("Class projectiles require an initialized projectile pool.");
            if (projectileScope == null) throw new ArgumentNullException(nameof(projectileScope));
            if (!projectileScope.isActiveAndEnabled || projectileScope.IsDisposed || !projectileScope.RoomEntityId.IsValid ||
                projectileScope.Pool != projectilePool)
            {
                throw new ArgumentException("Class projectiles require an initialized live room scope attached to the supplied pool.", nameof(projectileScope));
            }
            if (roomLifetimeScope == null) throw new ArgumentNullException(nameof(roomLifetimeScope));
            if (!roomLifetimeScope.isActiveAndEnabled || roomLifetimeScope.IsDisposed ||
                !roomLifetimeScope.RoomInstanceId.IsValid || string.IsNullOrWhiteSpace(roomLifetimeScope.TemplateId))
            {
                throw new ArgumentException("Class projectiles require an initialized active room lifetime scope.", nameof(roomLifetimeScope));
            }
            if (projectileScope.transform != roomLifetimeScope.transform &&
                !projectileScope.transform.IsChildOf(roomLifetimeScope.transform))
            {
                throw new ArgumentException("The room projectile scope must live under the room lifetime scope.", nameof(projectileScope));
            }
        }

        private readonly struct BindingPlan
        {
            public BindingPlan(
                CombatStats combatStats,
                WeaponProfile equippedWeapon,
                LayerMask enemyLayers,
                ArmorResolution armorResolution)
            {
                CombatStats = combatStats;
                EquippedWeapon = equippedWeapon;
                EnemyLayers = enemyLayers;
                ArmorResolution = armorResolution;
            }

            public CombatStats CombatStats { get; }
            public WeaponProfile EquippedWeapon { get; }
            public LayerMask EnemyLayers { get; }
            public ArmorResolution ArmorResolution { get; }
        }
    }
}
