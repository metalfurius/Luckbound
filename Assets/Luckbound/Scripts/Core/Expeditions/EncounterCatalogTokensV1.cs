using System;

namespace Luckbound.Core.Expeditions
{
    public static class EncounterCatalogTokensV1
    {
        public static bool TryParseDescriptorKind(string token, out EncounterCatalogDescriptorKind value)
        {
            switch (token)
            {
                case "room": value = EncounterCatalogDescriptorKind.Room; return true;
                case "enemy": value = EncounterCatalogDescriptorKind.Enemy; return true;
                case "cache": value = EncounterCatalogDescriptorKind.Cache; return true;
                default: value = default(EncounterCatalogDescriptorKind); return false;
            }
        }

        public static bool TryParseBindingMode(string token, out EncounterCatalogBindingMode value)
        {
            switch (token)
            {
                case "none": value = EncounterCatalogBindingMode.None; return true;
                case "encounter": value = EncounterCatalogBindingMode.Encounter; return true;
                default: value = default(EncounterCatalogBindingMode); return false;
            }
        }

        public static bool TryParseNodeKind(string token, out ExpeditionRouteNodeKind value)
        {
            switch (token)
            {
                case "combat": value = ExpeditionRouteNodeKind.Combat; return true;
                case "exploration": value = ExpeditionRouteNodeKind.Exploration; return true;
                case "treasure": value = ExpeditionRouteNodeKind.Treasure; return true;
                case "pact": value = ExpeditionRouteNodeKind.Pact; return true;
                case "narrative": value = ExpeditionRouteNodeKind.Narrative; return true;
                case "guardian": value = ExpeditionRouteNodeKind.Guardian; return true;
                case "rest": value = ExpeditionRouteNodeKind.Rest; return true;
                case "final-boss": value = ExpeditionRouteNodeKind.FinalBoss; return true;
                case "optional-combat": value = ExpeditionRouteNodeKind.OptionalCombat; return true;
                case "tutorial": value = ExpeditionRouteNodeKind.Tutorial; return true;
                case "safe-extraction": value = ExpeditionRouteNodeKind.SafeExtraction; return true;
                default: value = default(ExpeditionRouteNodeKind); return false;
            }
        }

        public static bool TryParseRewardKind(string token, out ExpeditionEncounterRewardEnemyKind value)
        {
            switch (token)
            {
                case "normal": value = ExpeditionEncounterRewardEnemyKind.Normal; return true;
                case "elite": value = ExpeditionEncounterRewardEnemyKind.Elite; return true;
                case "guardian": value = ExpeditionEncounterRewardEnemyKind.Guardian; return true;
                default: value = default(ExpeditionEncounterRewardEnemyKind); return false;
            }
        }

        public static string GetToken(EncounterCatalogDescriptorKind value)
        {
            switch (value)
            {
                case EncounterCatalogDescriptorKind.Room: return "room";
                case EncounterCatalogDescriptorKind.Enemy: return "enemy";
                case EncounterCatalogDescriptorKind.Cache: return "cache";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public static string GetToken(EncounterCatalogBindingMode value)
        {
            switch (value)
            {
                case EncounterCatalogBindingMode.None: return "none";
                case EncounterCatalogBindingMode.Encounter: return "encounter";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public static string GetToken(ExpeditionRouteNodeKind value)
        {
            switch (value)
            {
                case ExpeditionRouteNodeKind.Combat: return "combat";
                case ExpeditionRouteNodeKind.Exploration: return "exploration";
                case ExpeditionRouteNodeKind.Treasure: return "treasure";
                case ExpeditionRouteNodeKind.Pact: return "pact";
                case ExpeditionRouteNodeKind.Narrative: return "narrative";
                case ExpeditionRouteNodeKind.Guardian: return "guardian";
                case ExpeditionRouteNodeKind.Rest: return "rest";
                case ExpeditionRouteNodeKind.FinalBoss: return "final-boss";
                case ExpeditionRouteNodeKind.OptionalCombat: return "optional-combat";
                case ExpeditionRouteNodeKind.Tutorial: return "tutorial";
                case ExpeditionRouteNodeKind.SafeExtraction: return "safe-extraction";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public static string GetToken(ExpeditionEncounterRewardEnemyKind value)
        {
            switch (value)
            {
                case ExpeditionEncounterRewardEnemyKind.Normal: return "normal";
                case ExpeditionEncounterRewardEnemyKind.Elite: return "elite";
                case ExpeditionEncounterRewardEnemyKind.Guardian: return "guardian";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
