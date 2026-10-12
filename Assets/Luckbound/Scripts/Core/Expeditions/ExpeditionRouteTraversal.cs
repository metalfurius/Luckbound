using System;
using System.Collections.Generic;
using System.Linq;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Tracks the logical route cursor and accepts only edges present in the generated plan.</summary>
    public sealed class ExpeditionRouteTraversal
    {
        private readonly ExpeditionRoutePlan _route;
        private readonly List<string> _visitedNodeIds = new List<string>();

        public ExpeditionRouteTraversal(ExpeditionRoutePlan route, string initialNodeId = null)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            IReadOnlyList<string> errors = route.Validate();
            if (errors.Count > 0) throw new ArgumentException("Cannot traverse an invalid route: " + string.Join("; ", errors), nameof(route));

            _route = route;
            string start = initialNodeId ?? route.MandatoryNodeIds[0];
            if (!route.Nodes.Any(node => string.Equals(node.NodeId, start, StringComparison.Ordinal)))
            {
                throw new ArgumentException("Initial route node does not exist: " + start, nameof(initialNodeId));
            }

            CurrentNodeId = start;
            _visitedNodeIds.Add(start);
        }

        public string CurrentNodeId { get; private set; }

        public IReadOnlyList<string> VisitedNodeIds => Array.AsReadOnly(_visitedNodeIds.ToArray());

        public bool CanTransitionTo(string targetNodeId)
        {
            if (string.IsNullOrEmpty(targetNodeId) || string.Equals(CurrentNodeId, targetNodeId, StringComparison.Ordinal))
            {
                return false;
            }

            return _route.Connections.Any(connection =>
                string.Equals(connection.FromNodeId, CurrentNodeId, StringComparison.Ordinal) &&
                string.Equals(connection.ToNodeId, targetNodeId, StringComparison.Ordinal));
        }

        public bool TryTransitionTo(string targetNodeId)
        {
            if (!CanTransitionTo(targetNodeId)) return false;

            CurrentNodeId = targetNodeId;
            _visitedNodeIds.Add(targetNodeId);
            return true;
        }
    }
}
