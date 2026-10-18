using System;
using System.Collections.Generic;
using Luckbound.Core.Expeditions;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads the authoritative fixed route definition for the F10 sample.</summary>
    public static class ExpeditionSampleRouteJsonLoader
    {
        public static ExpeditionSampleRouteDefinition Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Expedition sample route JSON cannot be empty.", nameof(json));

            ExpeditionSampleRouteJsonData data;
            try
            {
                data = JsonUtility.FromJson<ExpeditionSampleRouteJsonData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Expedition sample route JSON could not be parsed.", exception);
            }

            IReadOnlyList<string> errors = Validate(data);
            if (errors.Count > 0)
                throw new FormatException("Invalid expedition sample route: " + string.Join("; ", errors));
            return ToDefinition(data);
        }

        public static IReadOnlyList<string> Validate(ExpeditionSampleRouteJsonData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("document is missing");
                return errors.AsReadOnly();
            }

            ExpeditionSampleRouteDefinition definition = ToDefinition(data);
            foreach (string error in definition.Validate()) errors.Add(error);
            return errors.AsReadOnly();
        }

        private static ExpeditionSampleRouteDefinition ToDefinition(ExpeditionSampleRouteJsonData data)
        {
            var nodes = new List<ExpeditionSampleRouteNodeDefinition>();
            ExpeditionSampleRouteNodeJsonData[] sourceNodes = data.nodes ?? Array.Empty<ExpeditionSampleRouteNodeJsonData>();
            foreach (ExpeditionSampleRouteNodeJsonData node in sourceNodes)
            {
                nodes.Add(node == null ? null : new ExpeditionSampleRouteNodeDefinition(
                    node.nodeId, node.sectorId, node.templateId, ParseKind(node.kind), node.isMandatory,
                    node.mainPathIndex, node.entryPortId, node.exitPortId));
            }

            var connections = new List<ExpeditionSampleRouteConnectionDefinition>();
            ExpeditionSampleRouteConnectionJsonData[] sourceConnections =
                data.connections ?? Array.Empty<ExpeditionSampleRouteConnectionJsonData>();
            foreach (ExpeditionSampleRouteConnectionJsonData connection in sourceConnections)
            {
                connections.Add(connection == null ? null : new ExpeditionSampleRouteConnectionDefinition(
                    connection.fromNodeId, connection.fromPortId, connection.toNodeId, connection.toPortId,
                    connection.isOptionalBranch));
            }

            return new ExpeditionSampleRouteDefinition(data.schemaVersion, data.generationVersion, nodes,
                connections, data.mandatoryNodeIds);
        }

        private static ExpeditionRouteNodeKind ParseKind(string value)
        {
            if (Enum.TryParse(value, ignoreCase: false, out ExpeditionRouteNodeKind kind) &&
                Enum.IsDefined(typeof(ExpeditionRouteNodeKind), kind)) return kind;
            return (ExpeditionRouteNodeKind)(-1);
        }
    }

    [Serializable]
    public sealed class ExpeditionSampleRouteJsonData
    {
        public int schemaVersion;
        public int generationVersion;
        public ExpeditionSampleRouteNodeJsonData[] nodes;
        public ExpeditionSampleRouteConnectionJsonData[] connections;
        public string[] mandatoryNodeIds;
    }

    [Serializable]
    public sealed class ExpeditionSampleRouteNodeJsonData
    {
        public string nodeId;
        public string sectorId;
        public string templateId;
        public string kind;
        public bool isMandatory;
        public int mainPathIndex;
        public string entryPortId;
        public string exitPortId;
    }

    [Serializable]
    public sealed class ExpeditionSampleRouteConnectionJsonData
    {
        public string fromNodeId;
        public string fromPortId;
        public string toNodeId;
        public string toPortId;
        public bool isOptionalBranch;
    }
}