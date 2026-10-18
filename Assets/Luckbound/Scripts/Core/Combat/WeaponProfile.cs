using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Combat
{
    /// <summary>Validated melee hit-box and direct-impact push values from Weapons.json.</summary>
    public sealed class WeaponMeleeParameters
    {
        public WeaponMeleeParameters(float centerOffsetX, float centerOffsetY, float width, float height,
            float knockbackHorizontalSpeed, float knockbackVerticalSpeed, int aiControlSuspensionTicks)
        {
            ValidateFinite(centerOffsetX, nameof(centerOffsetX));
            ValidateFinite(centerOffsetY, nameof(centerOffsetY));
            ValidateFinite(width, nameof(width));
            ValidateFinite(height, nameof(height));
            ValidateFinite(knockbackHorizontalSpeed, nameof(knockbackHorizontalSpeed));
            ValidateFinite(knockbackVerticalSpeed, nameof(knockbackVerticalSpeed));
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));
            if (knockbackHorizontalSpeed < 0f) throw new ArgumentOutOfRangeException(nameof(knockbackHorizontalSpeed));
            if (knockbackVerticalSpeed < 0f) throw new ArgumentOutOfRangeException(nameof(knockbackVerticalSpeed));
            if (aiControlSuspensionTicks < 0) throw new ArgumentOutOfRangeException(nameof(aiControlSuspensionTicks));
            CenterOffsetX = centerOffsetX; CenterOffsetY = centerOffsetY; Width = width; Height = height;
            KnockbackHorizontalSpeed = knockbackHorizontalSpeed; KnockbackVerticalSpeed = knockbackVerticalSpeed;
            AiControlSuspensionTicks = aiControlSuspensionTicks;
        }
        public float CenterOffsetX { get; }
        public float CenterOffsetY { get; }
        public float Width { get; }
        public float Height { get; }
        public float KnockbackHorizontalSpeed { get; }
        public float KnockbackVerticalSpeed { get; }
        public int AiControlSuspensionTicks { get; }
        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Melee weapon values must be finite.");
        }
    }

    /// <summary>Immutable design-time combat values for one equippable weapon item.</summary>
    public sealed class WeaponProfile
    {
        private readonly ReadOnlyCollection<WeaponTrait> _traits;

        public WeaponProfile(
            string itemId,
            string classId,
            int damage,
            float attackIntervalSeconds,
            IEnumerable<WeaponTrait> traits,
            WeaponMeleeParameters meleeParameters = null)
        {
            ValidateWeaponId(itemId);
            ValidateClassId(classId);
            if (meleeParameters != null && !string.Equals(classId, "class.bellator", StringComparison.Ordinal))
                throw new ArgumentException("Only Bellator weapons may declare melee impact parameters.", nameof(meleeParameters));
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (float.IsNaN(attackIntervalSeconds) || float.IsInfinity(attackIntervalSeconds) || attackIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(attackIntervalSeconds));
            if (traits == null) throw new ArgumentNullException(nameof(traits));

            var copiedTraits = new List<WeaponTrait>();
            var uniqueTraits = new HashSet<WeaponTraitId>();
            foreach (WeaponTrait trait in traits)
            {
                if (trait == null) throw new ArgumentException("Weapon traits cannot contain null entries.", nameof(traits));
                if (!uniqueTraits.Add(trait.Id)) throw new ArgumentException("Weapon traits must be unique by stable ID.", nameof(traits));
                copiedTraits.Add(trait);
            }
            if (copiedTraits.Count == 0) throw new ArgumentException("Every weapon profile must declare its design trait.", nameof(traits));

            ItemId = itemId;
            ClassId = classId;
            Damage = damage;
            AttackIntervalSeconds = attackIntervalSeconds;
            MeleeParameters = meleeParameters;
            _traits = copiedTraits.AsReadOnly();
        }

        public string ItemId { get; }
        public string ClassId { get; }
        public int Damage { get; }
        public float AttackIntervalSeconds { get; }
        public WeaponMeleeParameters MeleeParameters { get; }
        public IReadOnlyList<WeaponTrait> Traits => _traits;

        public bool HasTrait(WeaponTraitId id)
        {
            for (int index = 0; index < _traits.Count; index++)
                if (_traits[index].Id == id) return true;
            return false;
        }

        private static void ValidateWeaponId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("weapon.", StringComparison.Ordinal) || value.Length <= 7)
                throw new ArgumentException("Weapon profiles must use a stable weapon. content ID.", nameof(value));
            ValidateStableIdCharacters(value, nameof(value));
        }

        private static void ValidateClassId(string value)
        {
            if (value != "class.bellator" && value != "class.venator" && value != "class.magus")
                throw new ArgumentException("Weapon profiles must name one of the three playable classes.", nameof(value));
        }

        private static void ValidateStableIdCharacters(string value, string parameterName)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '_')
                    throw new ArgumentException("Weapon IDs may contain only lowercase letters, digits, dots, and underscores.", parameterName);
            }
        }
    }

    /// <summary>
    /// Resolves the weapon definition into the values consumed by each class's basic-attack state.
    /// The weapon's explicit melee box and push values remain available to the physical adapter.
    /// </summary>
    public sealed class WeaponBasicAttackProfile
    {
        public const int SimulationTicksPerSecond = 60;
        private const int MeleeDamageWindowStartTick = 6;
        private const int MeleeDamageWindowDurationTicks = 6;
        private const decimal AttackMovementSpeedMultiplier = 0.65m;

        private readonly WeaponProfile _weapon;
        private readonly int _chargeDamageBonusPercent;
        private readonly int _defensePenetration;
        private readonly int _slowPercent;
        private readonly int _slowDurationTicks;
        private readonly int _maximumProjectileTargets;
        private readonly decimal _projectileSpeedUnitsPerSecond;
        private readonly int _projectileLifetimeTicks;
        private readonly string _projectileElement;
        private readonly decimal _rarityMultiplier;
        private readonly decimal _upgradeMultiplier;

        private WeaponBasicAttackProfile(WeaponProfile weapon, ItemRarity rarity, int upgradeLevel)
        {
            _weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            Rarity = rarity;
            UpgradeLevel = upgradeLevel;
            _rarityMultiplier = GetRarityMultiplier(rarity);
            _upgradeMultiplier = 1m + 0.05m * upgradeLevel;
            AttackIntervalTicks = ToTicks(weapon.AttackIntervalSeconds, nameof(weapon.AttackIntervalSeconds));
            _maximumProjectileTargets = 1 + (GetTrait(WeaponTraitId.PierceOneTarget)?.PiercedTargetCount ?? 0);

            WeaponTrait charge = GetTrait(WeaponTraitId.OptionalChargeDamageBonus);
            _chargeDamageBonusPercent = charge != null ? charge.Percent : 0;
            WeaponTrait penetration = GetTrait(WeaponTraitId.IgnoreOneDefense);
            _defensePenetration = penetration != null ? penetration.DefensePointsIgnored : 0;
            WeaponTrait slow = GetTrait(WeaponTraitId.SlowOnHit);
            _slowPercent = slow != null ? slow.Percent : 0;
            _slowDurationTicks = slow != null ? ToTicks(slow.DurationSeconds, nameof(slow.DurationSeconds)) : 0;

            if (string.Equals(weapon.ClassId, "class.venator", StringComparison.Ordinal))
            {
                ProjectileDefinition baseline = VenatorBasicAttackProfile.Shortbow.Projectile;
                _projectileSpeedUnitsPerSecond = baseline.SpeedUnitsPerSecond;
                _projectileLifetimeTicks = baseline.LifetimeTicks;
                _projectileElement = string.Empty;
            }
            else if (string.Equals(weapon.ClassId, "class.magus", StringComparison.Ordinal))
            {
                ProjectileDefinition baseline = MagusBasicAttackProfile.EmberStaff.Projectile;
                _projectileSpeedUnitsPerSecond = baseline.SpeedUnitsPerSecond;
                _projectileLifetimeTicks = baseline.LifetimeTicks;
                _projectileElement = ResolveMagusElement(weapon);
            }
            else
            {
                _projectileSpeedUnitsPerSecond = 0m;
                _projectileLifetimeTicks = 0;
                _projectileElement = string.Empty;
            }
        }

        public WeaponProfile Weapon => _weapon;

        public WeaponMeleeParameters MeleeParameters => _weapon.MeleeParameters;

        public string ItemId => _weapon.ItemId;

        public string ClassId => _weapon.ClassId;

        /// <summary>The rarity stored on the equipped weapon instance.</summary>
        public ItemRarity Rarity { get; }

        /// <summary>The expedition upgrade level stored on the equipped weapon instance.</summary>
        public int UpgradeLevel { get; }

        public decimal RarityMultiplier => _rarityMultiplier;

        public decimal UpgradeMultiplier => _upgradeMultiplier;

        /// <summary>Definition damage after exact instance modifiers, with no intermediate rounding.</summary>
        public decimal BaseDamage => _weapon.Damage * _rarityMultiplier * _upgradeMultiplier;

        public int AttackIntervalTicks { get; }

        public int ChargeDamageBonusPercent => _chargeDamageBonusPercent;

        public int DefensePenetration => _defensePenetration;

        public int SlowPercent => _slowPercent;

        public int SlowDurationTicks => _slowDurationTicks;

        public int MaximumProjectileTargets => _maximumProjectileTargets;

        public string ProjectileElement => _projectileElement;

        public static WeaponBasicAttackProfile Create(
            WeaponProfile weapon,
            ItemRarity rarity = ItemRarity.Common,
            int upgradeLevel = 0)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (!Enum.IsDefined(typeof(ItemRarity), rarity)) throw new ArgumentOutOfRangeException(nameof(rarity));
            if (upgradeLevel < 0 || upgradeLevel > 3) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            ValidateTraitClass(weapon);
            return new WeaponBasicAttackProfile(weapon, rarity, upgradeLevel);
        }

        private static decimal GetRarityMultiplier(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 1m;
                case ItemRarity.Rare: return 1.15m;
                case ItemRarity.Epic: return 1.30m;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        public MeleeAttackProfile CreateMeleeAttackProfile()
        {
            EnsureClass("class.bellator");
            return new MeleeAttackProfile(
                BaseDamage,
                AttackIntervalTicks,
                MeleeDamageWindowStartTick,
                MeleeDamageWindowDurationTicks,
                AttackMovementSpeedMultiplier);
        }

        public VenatorBasicAttackProfile CreateVenatorBasicAttackProfile()
        {
            EnsureClass("class.venator");
            return new VenatorBasicAttackProfile(
                CreateProjectileDefinition(charged: false),
                AttackIntervalTicks,
                _chargeDamageBonusPercent);
        }

        public MagusBasicAttackProfile CreateMagusBasicAttackProfile()
        {
            EnsureClass("class.magus");
            return new MagusBasicAttackProfile(
                CreateProjectileDefinition(charged: false),
                AttackIntervalTicks,
                _projectileElement);
        }

        public ProjectileDefinition CreateProjectileDefinition(bool charged)
        {
            if (_projectileSpeedUnitsPerSecond <= 0m || _projectileLifetimeTicks <= 0)
                throw new InvalidOperationException("Bellator basic attacks are melee and have no projectile definition.");
            if (charged && _chargeDamageBonusPercent <= 0)
                throw new InvalidOperationException("This weapon does not declare an optional charge damage bonus.");

            decimal damage = BaseDamage;
            if (charged) damage *= 1m + _chargeDamageBonusPercent / 100m;
            return new ProjectileDefinition(
                damage,
                _projectileSpeedUnitsPerSecond,
                _projectileLifetimeTicks,
                _maximumProjectileTargets,
                _defensePenetration,
                _slowPercent,
                _slowDurationTicks,
                _projectileElement);
        }

        private WeaponTrait GetTrait(WeaponTraitId id)
        {
            for (int index = 0; index < _weapon.Traits.Count; index++)
                if (_weapon.Traits[index].Id == id) return _weapon.Traits[index];
            return null;
        }

        private void EnsureClass(string classId)
        {
            if (!string.Equals(_weapon.ClassId, classId, StringComparison.Ordinal))
                throw new InvalidOperationException("Weapon " + _weapon.ItemId + " is not compatible with " + classId + ".");
        }

        private static int ToTicks(float seconds, string parameterName)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(parameterName);
            decimal ticks = decimal.Round((decimal)seconds * SimulationTicksPerSecond, 0, MidpointRounding.AwayFromZero);
            if (ticks < 1m || ticks > int.MaxValue) throw new ArgumentOutOfRangeException(parameterName);
            return (int)ticks;
        }

        private static string ResolveMagusElement(WeaponProfile weapon)
        {
            if (weapon.HasTrait(WeaponTraitId.FireProjectile)) return "fire";
            if (weapon.HasTrait(WeaponTraitId.SlowOnHit)) return "frost";
            if (weapon.HasTrait(WeaponTraitId.IgnoreOneDefense)) return "shadow";
            throw new InvalidOperationException("A Magus basic weapon must declare its projectile element trait.");
        }

        private static void ValidateTraitClass(WeaponProfile weapon)
        {
            bool bellator = string.Equals(weapon.ClassId, "class.bellator", StringComparison.Ordinal);
            bool venator = string.Equals(weapon.ClassId, "class.venator", StringComparison.Ordinal);
            bool magus = string.Equals(weapon.ClassId, "class.magus", StringComparison.Ordinal);
            foreach (WeaponTrait trait in weapon.Traits)
            {
                bool compatible = bellator
                    ? trait.Id == WeaponTraitId.Balanced || trait.Id == WeaponTraitId.RapidCadence ||
                      trait.Id == WeaponTraitId.GreaterRange || trait.Id == WeaponTraitId.GreaterKnockback
                    : venator
                        ? trait.Id == WeaponTraitId.RapidCadence || trait.Id == WeaponTraitId.OptionalChargeDamageBonus ||
                          trait.Id == WeaponTraitId.PierceOneTarget
                        : magus && (trait.Id == WeaponTraitId.FireProjectile || trait.Id == WeaponTraitId.SlowOnHit ||
                                    trait.Id == WeaponTraitId.IgnoreOneDefense);
                if (!compatible)
                    throw new ArgumentException("Weapon trait " + trait.StableId + " is not valid for " + weapon.ClassId + ".", nameof(weapon));
            }
        }
    }
}
