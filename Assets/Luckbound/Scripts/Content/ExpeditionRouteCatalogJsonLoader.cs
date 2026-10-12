using System;
using System.Collections.Generic;
using Luckbound.Core.Expeditions;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads and validates the designed room pools used by seeded expedition routes.</summary>
    public static class ExpeditionRouteCatalogJsonLoader
    {
        public static ExpeditionRouteCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Expedition route catalog JSON cannot be empty.", nameof(json));

            ExpeditionRouteCatalogData data;
            try
            {
                data = JsonUtility.FromJson<ExpeditionRouteCatalogData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Expedition route catalog JSON could not be parsed.", exception);
            }

            IReadOnlyList<string> errors = Validate(data);
            if (errors.Count > 0) throw new FormatException("Invalid expedition route catalog: " + string.Join("; ", errors));

            var sectors = new List<ExpeditionRouteSectorCatalog>(data.sectors.Length);
            foreach (ExpeditionRouteSectorData sector in data.sectors)
            {
                sectors.Add(new ExpeditionRouteSectorCatalog(
                    sector.sectorId,
                    sector.combatTemplateIds,
                    sector.explorationTemplateIds,
                    sector.treasureTemplateId,
                    sector.pactTemplateId,
                    sector.narrativeTemplateId,
                    sector.guardianTemplateId));
            }

            return new ExpeditionRouteCatalog(sectors, data.restTemplateId, data.finalBossTemplateId);
        }

        public static IReadOnlyList<string> Validate(ExpeditionRouteCatalogData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("document is missing");
                return errors.AsReadOnly();
            }

            if (data.schemaVersion != 1) errors.Add("schemaVersion must be 1");
            if (data.generationVersion != ExpeditionRouteGenerator.GenerationVersion)
            {
                errors.Add("generationVersion must match the supported route generator version " + ExpeditionRouteGenerator.GenerationVersion);
            }

            ExpeditionRouteSectorData[] sourceSectors = data.sectors ?? Array.Empty<ExpeditionRouteSectorData>();
            var sectors = new List<ExpeditionRouteSectorCatalog>(sourceSectors.Length);
            for (int index = 0; index < sourceSectors.Length; index++)
            {
                ExpeditionRouteSectorData sector = sourceSectors[index];
                if (sector == null)
                {
                    errors.Add("sectors[" + index + "] is missing");
                    continue;
                }

                sectors.Add(new ExpeditionRouteSectorCatalog(
                    sector.sectorId,
                    sector.combatTemplateIds,
                    sector.explorationTemplateIds,
                    sector.treasureTemplateId,
                    sector.pactTemplateId,
                    sector.narrativeTemplateId,
                    sector.guardianTemplateId));
            }

            var catalog = new ExpeditionRouteCatalog(sectors, data.restTemplateId, data.finalBossTemplateId);
            foreach (string error in catalog.Validate()) errors.Add(error);
            return errors.AsReadOnly();
        }
    }

    [Serializable]
    public sealed class ExpeditionRouteCatalogData
    {
        public int schemaVersion;
        public int generationVersion;
        public string restTemplateId;
        public string finalBossTemplateId;
        public ExpeditionRouteSectorData[] sectors;
    }

    [Serializable]
    public sealed class ExpeditionRouteSectorData
    {
        public string sectorId;
        public string[] combatTemplateIds;
        public string[] explorationTemplateIds;
        public string treasureTemplateId;
        public string pactTemplateId;
        public string narrativeTemplateId;
        public string guardianTemplateId;
    }
}
