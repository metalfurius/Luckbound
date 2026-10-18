using System;
using Luckbound.Core.Armor;
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
    /// <summary>Compatibility entry point for the Bellator run composition.</summary>
    public static class ExpeditionBellatorRunBinder
    {
        public static void Bind(ExpeditionRunConfiguration run, ActorId actorId, IPlayerInputSource inputSource,
            LayerMask groundLayers, LayerMask enemyLayers, int defense, PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector, ItemCatalog itemCatalog, WeaponCatalog weaponCatalog,
            SkillCatalog skillCatalog, LootPickupDropStager2D dropStager = null, ArmorCatalog armorCatalog = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (!string.Equals(run.ClassId, BellatorSkillLoadout.ClassId, StringComparison.Ordinal))
                throw new ArgumentException("Only a confirmed Bellator run can be bound by this composition.", nameof(run));
            ExpeditionPlayerRunBinder.Bind(run, actorId, inputSource, groundLayers, enemyLayers, defense,
                playerMotor, itemCollector, itemCatalog, weaponCatalog, skillCatalog,
                dropStager: dropStager, armorCatalog: armorCatalog);
        }
    }
}
