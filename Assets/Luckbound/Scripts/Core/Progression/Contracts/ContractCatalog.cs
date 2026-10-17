using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Luckbound.Core.Progression.Contracts
{
    /// <summary>The E10 slice of stable contract content, sourced from plan section 3.14.</summary>
    public static class ContractCatalog
    {
        public const string Ct01 = "CT01";
        public const string Ct02 = "CT02";
        public const string Ct03 = "CT03";
        public const string Ct04 = "CT04";

        public const string GuildRecognitionUnlockId = "recognition.guild";

        private static readonly ReadOnlyCollection<ContractDefinition> Definitions = Array.AsReadOnly(new[]
        {
            new ContractDefinition(Ct01, ContractGoalType.CombatRoomsCompleted, 3,
                "contract.ct01.title", "contract.ct01.objective",
                new ContractReward(20, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.bellator.trail_saber")
                })),
            new ContractDefinition(Ct02, ContractGoalType.CustodiansDefeated, 1,
                "contract.ct02.title", "contract.ct02.objective",
                new ContractReward(30, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.GuildRecognition, GuildRecognitionUnlockId)
                })),
            new ContractDefinition(Ct03, ContractGoalType.BoneguardsDefeated, 10,
                "contract.ct03.title", "contract.ct03.objective",
                new ContractReward(30, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.PlayableRace, "race.orcini")
                })),
            new ContractDefinition(Ct04, ContractGoalType.ItemsCraftedOrUpgraded, 3,
                "contract.ct04.title", "contract.ct04.objective",
                new ContractReward(30, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.PlayableRace, "race.dvergar"),
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "armor.bellator.guild_cuirass")
                }))
        });

        private static readonly Dictionary<string, ContractDefinition> ById = Definitions
            .ToDictionary(definition => definition.Id, StringComparer.Ordinal);

        public static IReadOnlyList<ContractDefinition> ImplementedContracts => Definitions;

        public static bool TryGet(string contractId, out ContractDefinition definition)
        {
            if (contractId == null)
            {
                definition = null;
                return false;
            }

            return ById.TryGetValue(contractId, out definition);
        }

        public static ContractDefinition Get(string contractId)
        {
            if (contractId == null) throw new ArgumentNullException(nameof(contractId));
            if (!ById.TryGetValue(contractId, out ContractDefinition definition))
                throw new ArgumentException("Unknown contract ID.", nameof(contractId));
            return definition;
        }
    }
}
