using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    [Serializable]
    public sealed class ExpeditionRoomPrefabEntry
    {
        public string templateId;
        public GameObject prefab;
    }

    /// <summary>Resolves only explicitly registered rooms and verifies prefab identity against its source JSON.</summary>
    public static class ExpeditionRoomPrefabRegistry
    {
        public static bool TryResolve(string templateId, IEnumerable<ExpeditionRoomPrefabEntry> entries,
            out GameObject prefab, out string error)
        {
            prefab = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                error = "A non-empty room template ID is required.";
                return false;
            }

            ExpeditionRoomPrefabEntry[] matches = (entries ?? Enumerable.Empty<ExpeditionRoomPrefabEntry>())
                .Where(entry => entry != null && string.Equals(entry.templateId, templateId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                error = "No room prefab is registered for template " + templateId + ".";
                return false;
            }
            if (matches.Length != 1)
            {
                error = "More than one room prefab is registered for template " + templateId + ".";
                return false;
            }

            GameObject candidate = matches[0].prefab;
            if (candidate == null)
            {
                error = "The registered room prefab is missing for template " + templateId + ".";
                return false;
            }

            RoomTemplatePrefab2D room = candidate.GetComponent<RoomTemplatePrefab2D>();
            if (room == null)
            {
                error = "The registered prefab for template " + templateId + " has no RoomTemplatePrefab2D definition.";
                return false;
            }

            try
            {
                string sourceTemplateId = room.ValidateGeneratedPrefab().templateId;
                if (!string.Equals(sourceTemplateId, templateId, StringComparison.Ordinal))
                {
                    error = "The registered prefab for template " + templateId +
                        " contains source definition " + sourceTemplateId + ".";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "The registered prefab for template " + templateId + " is invalid: " + exception.Message;
                return false;
            }

            prefab = candidate;
            return true;
        }
    }
}
