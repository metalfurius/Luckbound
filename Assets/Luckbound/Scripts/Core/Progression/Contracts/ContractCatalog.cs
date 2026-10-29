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
        public const string Ct05 = "CT05";
        public const string Ct06 = "CT06";
        public const string Ct07 = "CT07";
        public const string Ct08 = "CT08";
        public const string Ct09 = "CT09";
        public const string Ct10 = "CT10";
        public const string Ct11 = "CT11";
        public const string Ct12 = "CT12";

        public const string GuildRecognitionUnlockId = "recognition.guild";
        public const string GuildOathPlaqueUnlockId = "decoration.guild.oath_plaque";

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
                })),
            new ContractDefinition(Ct05, ContractGoalType.StoryFragmentsFound, 3,
                "contract.ct05.title", "contract.ct05.objective",
                new ContractReward(30, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.PlayableRace, "race.alvari"),
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "armor.runic_mantle")
                })),
            new ContractDefinition(Ct06, ContractGoalType.ExpeditionsExtractedAfterGuardian, 1,
                "contract.ct06.title", "contract.ct06.objective",
                new ContractReward(40, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.venator.long_bow")
                })),
            new ContractDefinition(Ct07, ContractGoalType.MagistersDefeated, 1,
                "contract.ct07.title", "contract.ct07.objective",
                new ContractReward(60, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.venator.ruins_crossbow")
                })),
            new ContractDefinition(Ct08, ContractGoalType.SkillHitsApplied, 30,
                "contract.ct08.title", "contract.ct08.objective",
                new ContractReward(30, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.magus.frost_staff")
                })),
            new ContractDefinition(Ct09, ContractGoalType.PactsAcceptedBetweenExpeditions, 3,
                "contract.ct09.title", "contract.ct09.objective",
                new ContractReward(40, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.magus.shadow_staff")
                })),
            new ContractDefinition(Ct10, ContractGoalType.WeaponsUpgradedToPlusThree, 1,
                "contract.ct10.title", "contract.ct10.objective",
                new ContractReward(50, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "weapon.bellator.umbra_greatsword")
                })),
            new ContractDefinition(Ct11, ContractGoalType.EndingsCompleted, 1,
                "contract.ct11.title", "contract.ct11.objective",
                new ContractReward(80, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.StartingKit, "armor.bark_armor")
                })),
            new ContractDefinition(Ct12, ContractGoalType.StoryFragmentsFound, 12,
                "contract.ct12.title", "contract.ct12.objective",
                new ContractReward(60, new[]
                {
                    new ContractRewardUnlock(ContractUnlockKind.GuildDecoration, GuildOathPlaqueUnlockId)
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
