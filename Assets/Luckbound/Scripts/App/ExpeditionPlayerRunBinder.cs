using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
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
            LootPickupDropStager2D dropStager = null,
            IPlayerFortuneBrewRuntime fortuneBrewRuntime = null,
            ArmorCatalog armorCatalog = null,
            ExpeditionSaveData checkpoint = null)
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
                roomLifetimeScope,
                armorCatalog,
                checkpoint);

            playerMotor.Initialize(
                actorId,
                inputSource,
                groundLayers,
                plan.CombatStats,
                plan.EnemyLayers,
                run.RaceId,
                plan.ArmorResolution);
            playerMotor.ConfigureEquippedWeapon(run, weaponCatalog);
            playerMotor.ConfigureEquippedArmorInstanceId(run.Inventory.Armor.InstanceId);

            if (string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                playerMotor.ConfigureBellator(skillCatalog, run.SkillIds);
            }
            else if (string.Equals(run.ClassId, VenatorSkillLoadout.ClassId, StringComparison.Ordinal))
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
            playerMotor.RefreshConsumableChargesFromInventory();
            if (fortuneBrewRuntime != null)
                playerMotor.ConfigureFortuneBrewRuntime(fortuneBrewRuntime);
        }

        /// <summary>Binds the committed actor resources without healing or rebuilding them from the current profile.</summary>
        public static void BindFromCheckpoint(
            ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint, ActorId actorId,
            IPlayerInputSource inputSource, LayerMask groundLayers, LayerMask enemyLayers, int defense,
            PlayerMotor2D playerMotor, ItemCollector2D itemCollector, ItemCatalog itemCatalog,
            WeaponCatalog weaponCatalog, SkillCatalog skillCatalog, ProjectilePool2D projectilePool = null,
            RoomProjectileScope projectileScope = null, RoomLifetimeScope roomLifetimeScope = null,
            LootPickupDropStager2D dropStager = null, IPlayerFortuneBrewRuntime fortuneBrewRuntime = null,
            ArmorCatalog armorCatalog = null)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            Bind(run, actorId, inputSource, groundLayers, enemyLayers, defense, playerMotor, itemCollector,
                itemCatalog, weaponCatalog, skillCatalog, projectilePool, projectileScope, roomLifetimeScope,
                dropStager, fortuneBrewRuntime, armorCatalog, checkpoint);
        }

        /// <summary>Publishes a durably committed run inventory to its already-composed live actor.</summary>
        public static void PublishCommittedRunInventory(ExpeditionRunConfiguration committedRun,
            PlayerMotor2D playerMotor, ItemCollector2D itemCollector)
        {
            if (committedRun == null) throw new ArgumentNullException(nameof(committedRun));
            if (committedRun.Inventory == null)
                throw new ArgumentException("A committed run must carry its inventory.", nameof(committedRun));
            if (playerMotor == null) throw new ArgumentNullException(nameof(playerMotor));
            if (itemCollector == null) throw new ArgumentNullException(nameof(itemCollector));
            if (!playerMotor.ActorId.IsValid || playerMotor.CombatStats == null || itemCollector.Inventory == null)
                throw new InvalidOperationException("The player actor and collector must be bound before publishing a run inventory.");

            bool classMatches =
                (committedRun.ClassId == BellatorSkillLoadout.ClassId && playerMotor.IsBellator) ||
                (committedRun.ClassId == VenatorSkillLoadout.ClassId && playerMotor.IsVenator) ||
                (committedRun.ClassId == MagusSkillLoadout.ClassId && playerMotor.IsMagus);
            if (!classMatches || !string.Equals(committedRun.RaceId, playerMotor.RaceId, StringComparison.Ordinal) ||
                !string.Equals(committedRun.RaceId, itemCollector.RaceId, StringComparison.Ordinal))
                throw new InvalidOperationException("The committed run must retain the live actor's class and race.");

            itemCollector.PublishCommittedInventory(committedRun.Inventory);
            playerMotor.RefreshConsumableChargesFromInventory();
        }

        /// <summary>Validates and prepares a live actor projection before its inventory snapshot is saved.</summary>
        public static PreparedCommittedRunPublication PrepareCommittedRunPublication(
            ExpeditionRunConfiguration committedRun, ExpeditionSaveData candidateSave,
            PlayerMotor2D playerMotor, ItemCollector2D itemCollector,
            WeaponCatalog weaponCatalog, ArmorCatalog armorCatalog)
        {
            if (committedRun == null) throw new ArgumentNullException(nameof(committedRun));
            if (candidateSave == null) throw new ArgumentNullException(nameof(candidateSave));
            if (playerMotor == null) throw new ArgumentNullException(nameof(playerMotor));
            if (itemCollector == null) throw new ArgumentNullException(nameof(itemCollector));
            if (weaponCatalog == null) throw new ArgumentNullException(nameof(weaponCatalog));
            if (armorCatalog == null) throw new ArgumentNullException(nameof(armorCatalog));
            if (!string.Equals(candidateSave.runId, committedRun.RunId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(candidateSave.classId, committedRun.ClassId, StringComparison.Ordinal) ||
                !string.Equals(candidateSave.raceId, committedRun.RaceId, StringComparison.Ordinal))
                throw new InvalidOperationException("The candidate checkpoint must belong to the committed run, class, and race.");
            if (!playerMotor.ActorId.IsValid || playerMotor.CombatStats == null || itemCollector.Inventory == null ||
                !playerMotor.isActiveAndEnabled || !itemCollector.isActiveAndEnabled ||
                playerMotor.gameObject != itemCollector.gameObject)
                throw new InvalidOperationException("A live actor and collector must be active before preparing a committed run.");
            if (!ReferenceEquals(committedRun.Inventory.Catalog, itemCollector.Catalog) ||
                !string.Equals(committedRun.Inventory.ClassId, itemCollector.Inventory.ClassId, StringComparison.Ordinal) ||
                !string.Equals(committedRun.ClassId, playerMotor.IsBellator ? BellatorSkillLoadout.ClassId :
                    playerMotor.IsVenator ? VenatorSkillLoadout.ClassId :
                    playerMotor.IsMagus ? MagusSkillLoadout.ClassId : string.Empty, StringComparison.Ordinal) ||
                !string.Equals(committedRun.RaceId, playerMotor.RaceId, StringComparison.Ordinal) ||
                !string.Equals(committedRun.RaceId, itemCollector.RaceId, StringComparison.Ordinal))
                throw new InvalidOperationException("The committed run must retain the live actor's class, race, and item catalog.");

            ValidateEquippedItems(committedRun, committedRun.Inventory.Catalog);
            InventoryItemStack armorStack = committedRun.Inventory.Armor;
            if (!armorCatalog.TryGet(armorStack.ItemId, out ArmorProfile nextArmor) ||
                !string.Equals(nextArmor.ItemId, armorStack.ItemId, StringComparison.Ordinal))
                throw new ArgumentException("The committed armor must resolve from the authoritative armor catalog.", nameof(committedRun));
            ArmorResolution currentArmor = playerMotor.EquippedArmorResolution;
            if (currentArmor == null || candidateSave.maximumHealth != playerMotor.CombatStats.MaximumHealth ||
                candidateSave.maximumEnergy != playerMotor.MaximumEnergy ||
                candidateSave.currentHealth < 0 || candidateSave.currentHealth > playerMotor.CombatStats.MaximumHealth ||
                candidateSave.currentEnergy < 0 || candidateSave.currentEnergy > playerMotor.MaximumEnergy)
                throw new InvalidOperationException("The live actor has no valid armor projection or resource snapshot.");

            ArmorResolution nextArmorResolution = ArmorReplacementResolver.Resolve(currentArmor, nextArmor,
                armorStack.UpgradeLevel, candidateSave.currentHealth, candidateSave.currentEnergy, armorStack.Rarity);
            WeaponProfile nextWeapon = PlayerMotor2D.ResolveEquippedWeaponProfile(committedRun, weaponCatalog);
            _ = WeaponBasicAttackProfile.Create(nextWeapon, committedRun.Inventory.Weapon.Rarity,
                committedRun.Inventory.Weapon.UpgradeLevel);
            int baseDefense = playerMotor.CombatStats.Defense - currentArmor.Defense;
            if (baseDefense < 0) throw new InvalidOperationException("The live actor defense does not include its current armor exactly once.");
            _ = new CombatStats(nextArmorResolution.MaximumHealth, nextArmorResolution.MaximumEnergy,
                checked(baseDefense + nextArmorResolution.Defense));

            return new PreparedCommittedRunPublication(committedRun, playerMotor, itemCollector,
                nextWeapon, nextArmorResolution);
        }

        public sealed class PreparedCommittedRunPublication
        {
            private readonly ExpeditionRunConfiguration _run;
            private readonly PlayerMotor2D _playerMotor;
            private readonly ItemCollector2D _itemCollector;
            private readonly WeaponProfile _weapon;
            private readonly ArmorResolution _armor;
            private bool _published;

            internal PreparedCommittedRunPublication(ExpeditionRunConfiguration run, PlayerMotor2D playerMotor,
                ItemCollector2D itemCollector, WeaponProfile weapon, ArmorResolution armor)
            {
                _run = run;
                _playerMotor = playerMotor;
                _itemCollector = itemCollector;
                _weapon = weapon;
                _armor = armor;
            }

            public ExpeditionRunConfiguration Run => _run;
            public int MaximumHealth => _armor.MaximumHealth;
            public int CurrentHealth => _armor.HealthAfterMaximumClamp;
            public int MaximumEnergy => _armor.MaximumEnergy;
            public int CurrentEnergy => _armor.EnergyAfterMaximumClamp;

            public void ApplyToSave(ExpeditionSaveData candidateSave)
            {
                if (candidateSave == null) throw new ArgumentNullException(nameof(candidateSave));
                if (!string.Equals(candidateSave.runId, _run.RunId.ToString(), StringComparison.Ordinal))
                    throw new InvalidOperationException("The prepared run publication cannot update another expedition checkpoint.");
                candidateSave.inventorySnapshotJson = JsonUtility.ToJson(_run.Inventory.CreateSnapshot());
                candidateSave.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(_run.Inventory);
                candidateSave.maximumHealth = MaximumHealth;
                candidateSave.currentHealth = CurrentHealth;
                candidateSave.maximumEnergy = MaximumEnergy;
                candidateSave.currentEnergy = CurrentEnergy;
            }

            public void Publish()
            {
                if (_published) return;
                _playerMotor.PublishCommittedEquipment(_weapon, _run.Inventory.Weapon.InstanceId,
                    _run.Inventory.Weapon.Rarity, _run.Inventory.Weapon.UpgradeLevel,
                    _run.Inventory.Armor.InstanceId, _armor);
                _itemCollector.PublishCommittedInventory(_run.Inventory);
                _playerMotor.RefreshConsumableChargesFromInventory();
                _published = true;
            }
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
            RoomLifetimeScope roomLifetimeScope,
            ArmorCatalog armorCatalog,
            ExpeditionSaveData checkpoint)
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

            ArmorResolution armorResolution = ResolveEquippedArmor(run, itemCatalog, armorCatalog, checkpoint);
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

        internal static ArmorResolution ResolveEquippedArmor(ExpeditionRunConfiguration run, ItemCatalog itemCatalog,
            ArmorCatalog armorCatalog = null, ExpeditionSaveData checkpoint = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));
            if (run.Inventory == null || !string.Equals(run.Inventory.ClassId, run.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("The run must carry its own inventory for the selected class.", nameof(run));
            if (!ReferenceEquals(run.Inventory.Catalog, itemCatalog))
                throw new ArgumentException("Pass the already-loaded item catalog owned by the run inventory.", nameof(itemCatalog));

            ArmorCatalog resolvedCatalog = armorCatalog ?? LoadArmorCatalog(itemCatalog);
            if (checkpoint != null) ValidateCheckpoint(run, checkpoint);
            InventoryItemStack equippedArmor = run.Inventory.Armor;
            if (equippedArmor == null || !equippedArmor.InstanceId.IsValid ||
                !itemCatalog.TryGet(equippedArmor.ItemId, out ItemDefinition armorItem) ||
                !string.Equals(armorItem.Id, equippedArmor.ItemId, StringComparison.Ordinal) ||
                armorItem.Kind != ItemKind.Armor ||
                !resolvedCatalog.TryGet(equippedArmor.ItemId, out ArmorProfile profile) ||
                !string.Equals(profile.ItemId, equippedArmor.ItemId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The run's equipped armor must match an exact item ID in the item and armor catalogs.",
                    nameof(run));
            }

            int baseMaximumHealth = checkpoint == null ? run.StartingHealth : checkpoint.maximumHealth;
            int baseMaximumEnergy = checkpoint == null ? run.StartingEnergy :
                checked(checkpoint.maximumEnergy - profile.MaximumEnergyBonus);
            if (baseMaximumEnergy <= 0)
                throw new ArgumentException("Saved maximum energy must contain the equipped armor bonus.", nameof(checkpoint));
            return ArmorStatsResolver.Resolve(
                profile,
                equippedArmor.UpgradeLevel,
                baseMaximumHealth,
                baseMaximumEnergy,
                checkpoint == null ? run.StartingHealth : checkpoint.currentHealth,
                checkpoint == null ? run.StartingEnergy : checkpoint.currentEnergy,
                equippedArmor.Rarity);
        }

        private static ArmorCatalog LoadArmorCatalog(ItemCatalog itemCatalog)
        {
            TextAsset data = Resources.Load<TextAsset>("Luckbound/Data/Items/Armors");
            if (data == null) throw new InvalidOperationException("Required armor data is missing: Armors.json.");
            return ArmorCatalogJsonLoader.Load(data.text, itemCatalog);
        }

        private static void ValidateCheckpoint(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint)
        {
            if (!string.Equals(checkpoint.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                checkpoint.seed != run.Seed || checkpoint.generationVersion != run.Route.GenerationVersion ||
                !string.Equals(checkpoint.classId, run.ClassId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.raceId, run.RaceId, StringComparison.Ordinal) ||
                checkpoint.hasFrozenPermanentUpgrades != run.HasKnownStartingLoadout ||
                (run.HasKnownStartingLoadout && (checkpoint.frozenPermanentUpgrades == null ||
                    checkpoint.frozenPermanentUpgrades.healthUpgradeLevel != run.HealthUpgradeLevel ||
                    checkpoint.frozenPermanentUpgrades.energyUpgradeLevel != run.EnergyUpgradeLevel ||
                    checkpoint.frozenPermanentUpgrades.additionalPotionUpgradeLevel != run.AdditionalPotionUpgradeLevel)) ||
                checkpoint.selectedSkillIds == null || !checkpoint.selectedSkillIds.SequenceEqual(run.SkillIds) ||
                !string.Equals(checkpoint.routeSnapshotJson, run.Route.CanonicalForm, StringComparison.Ordinal))
                throw new ArgumentException("The checkpoint must belong to this exact run configuration.", nameof(checkpoint));
            if (checkpoint.maximumHealth <= 0 || checkpoint.maximumEnergy <= 0 ||
                checkpoint.currentHealth < 0 || checkpoint.currentHealth > checkpoint.maximumHealth ||
                checkpoint.currentEnergy < 0 || checkpoint.currentEnergy > checkpoint.maximumEnergy)
                throw new ArgumentException("Saved actor resources must be within their committed maximums.", nameof(checkpoint));

            ExpeditionInventorySnapshot savedInventory;
            try { savedInventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(checkpoint.inventorySnapshotJson); }
            catch (Exception exception)
            {
                throw new ArgumentException("Checkpoint inventory could not be decoded.", nameof(checkpoint), exception);
            }
            if (savedInventory == null || !string.Equals(JsonUtility.ToJson(savedInventory),
                    JsonUtility.ToJson(run.Inventory.CreateSnapshot()), StringComparison.Ordinal))
                throw new ArgumentException("The checkpoint inventory must match the actor's restored run inventory.", nameof(checkpoint));
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
            if (inventory.Weapon == null || inventory.Armor == null)
            {
                throw new ArgumentException("A confirmed run must carry its equipped weapon and armor.", nameof(run));
            }

            ValidateItem(inventory.Weapon, ItemKind.Weapon, itemCatalog, run.ClassId, requireWeaponCompatibility: true);
            ValidateItem(inventory.Armor, ItemKind.Armor, itemCatalog, run.ClassId, requireWeaponCompatibility: false);
            if (inventory.QuickConsumable != null)
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
