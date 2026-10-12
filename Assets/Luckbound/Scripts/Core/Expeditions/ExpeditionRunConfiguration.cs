using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Randomness;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Immutable choices and freshly-created state that define one expedition run.</summary>
    public sealed class ExpeditionRunConfiguration
    {
        public const string HumanRaceId = "race.human";
        public const int InitialHealth = 100;
        public const int InitialEnergy = 50;
        public const int InitialHealthPotions = 2;
        public const int InitialFortunaCharges = 2;

        private readonly ReadOnlyCollection<string> _skillIds;
        private readonly ReadOnlyCollection<ItemInstanceId> _startingEquipmentInstanceIds;

        public ExpeditionRunConfiguration(
            RunId runId,
            string classId,
            string raceId,
            IEnumerable<string> skillIds,
            uint seed,
            ExpeditionRoutePlan route,
            ExpeditionInventory inventory,
            IEnumerable<ItemInstanceId> startingEquipmentInstanceIds = null)
        {
            if (!runId.IsValid) throw new ArgumentException("A fresh expedition requires a valid RunId.", nameof(runId));
            if (classId != "class.bellator" && classId != "class.venator" && classId != "class.magus")
                throw new ArgumentException("An expedition requires one of the three playable classes.", nameof(classId));
            if (!string.Equals(raceId, HumanRaceId, StringComparison.Ordinal))
                throw new ArgumentException("Only the human race is unlocked for a new profile.", nameof(raceId));
            if (skillIds == null) throw new ArgumentNullException(nameof(skillIds));
            string[] selectedSkills = skillIds.ToArray();
            if (selectedSkills.Length != 2 || selectedSkills.Any(string.IsNullOrWhiteSpace) ||
                selectedSkills.Distinct(StringComparer.Ordinal).Count() != 2)
                throw new ArgumentException("An expedition requires exactly two distinct skill IDs.", nameof(skillIds));
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (route.Seed != Xorshift32.NormalizeSeed(seed))
                throw new ArgumentException("The route seed must match the run seed.", nameof(route));
            if (route.Validate().Count != 0) throw new ArgumentException("The expedition route must pass its graph invariants.", nameof(route));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!string.Equals(inventory.ClassId, classId, StringComparison.Ordinal))
                throw new ArgumentException("The expedition inventory must belong to the selected class.", nameof(inventory));

            RunId = runId;
            ClassId = classId;
            RaceId = raceId;
            _skillIds = Array.AsReadOnly(selectedSkills);
            Seed = route.Seed;
            Route = route;
            Inventory = inventory;

            ItemInstanceId[] startingEquipment = startingEquipmentInstanceIds == null
                ? Array.Empty<ItemInstanceId>()
                : startingEquipmentInstanceIds.ToArray();
            if (startingEquipment.Any(instanceId => !instanceId.IsValid) ||
                startingEquipment.Distinct().Count() != startingEquipment.Length)
                throw new ArgumentException("Starting equipment instance IDs must be valid and unique.", nameof(startingEquipmentInstanceIds));
            _startingEquipmentInstanceIds = Array.AsReadOnly(startingEquipment);
        }

        public RunId RunId { get; }
        public string ClassId { get; }
        public string RaceId { get; }
        public IReadOnlyList<string> SkillIds => _skillIds;
        public uint Seed { get; }
        public ExpeditionRoutePlan Route { get; }
        public ExpeditionInventory Inventory { get; }
        public IReadOnlyList<ItemInstanceId> StartingEquipmentInstanceIds => _startingEquipmentInstanceIds;
        public int StartingHealth => InitialHealth;
        public int StartingEnergy => InitialEnergy;
        public int StartingFortunaCharges => InitialFortunaCharges;
    }
}
