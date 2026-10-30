using System;
using System.Collections.Generic;
using Luckbound.Core.Expeditions;

namespace Luckbound.App
{
    /// <summary>
    /// Immutable handoff from a resolved catalog plan to the existing logical encounter roster.
    /// This projection carries no physical-spawn or victory authority.
    /// </summary>
    internal sealed class ExpeditionEncounterPlanProjection
    {
        private ExpeditionEncounterPlanProjection(EncounterCatalogBindingMode mode,
            string planSha256, ExpeditionEncounterRosterPlan roster)
        {
            Mode = mode;
            PlanSha256 = planSha256 ?? throw new ArgumentNullException(nameof(planSha256));
            Roster = roster;
        }

        internal EncounterCatalogBindingMode Mode { get; }
        internal string PlanSha256 { get; }
        internal ExpeditionEncounterRosterPlan Roster { get; }

        internal static ExpeditionEncounterPlanProjection Create(ResolvedEncounterPlanV1 plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            string planSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan);
            if (plan.Mode == EncounterCatalogBindingMode.None)
            {
                if (plan.SelectedVariant != null)
                    throw new InvalidOperationException("A none plan cannot select an encounter variant.");

                return new ExpeditionEncounterPlanProjection(plan.Mode, planSha256, null);
            }

            if (plan.Mode != EncounterCatalogBindingMode.Encounter)
                throw new ArgumentOutOfRangeException(nameof(plan), "Resolved encounter plan mode is not supported.");
            if (plan.SelectedVariant == null)
                throw new InvalidOperationException("An encounter plan must select a variant.");

            var waves = new List<ExpeditionEncounterWavePlan>(plan.SelectedVariant.Waves.Count);
            for (int waveIndex = 0; waveIndex < plan.SelectedVariant.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave wave = plan.SelectedVariant.Waves[waveIndex];
                var spawnKeys = new List<string>(wave.Spawns.Count);
                for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
                {
                    EncounterCatalogSpawn spawn = wave.Spawns[spawnIndex];
                    spawnKeys.Add(plan.GetSpawnIdentity(spawn.SpawnId).Key);
                }

                waves.Add(new ExpeditionEncounterWavePlan(waveIndex + 1, spawnKeys));
            }

            var roster = new ExpeditionEncounterRosterPlan(waves);
            return new ExpeditionEncounterPlanProjection(plan.Mode, planSha256, roster);
        }
    }
}
