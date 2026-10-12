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
            : this(route, new[] { initialNodeId ?? (route == null ? null : route.MandatoryNodeIds.FirstOrDefault()) })
        {
        }

        public ExpeditionRouteTraversal(ExpeditionRoutePlan route, IEnumerable<string> visitedNodeIds)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            IReadOnlyList<string> errors = route.Validate();
            if (errors.Count > 0) throw new ArgumentException("Cannot traverse an invalid route: " + string.Join("; ", errors), nameof(route));
            if (visitedNodeIds == null) throw new ArgumentNullException(nameof(visitedNodeIds));

            _route = route;
            string[] history = visitedNodeIds.ToArray();
            if (history.Length == 0 || history.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("A route traversal must contain its starting node.", nameof(visitedNodeIds));
            if (!string.Equals(history[0], route.MandatoryNodeIds[0], StringComparison.Ordinal))
                throw new ArgumentException("A route traversal must begin at the mandatory start node.", nameof(visitedNodeIds));
            if (history.Distinct(StringComparer.Ordinal).Count() != history.Length)
                throw new ArgumentException("A route traversal cannot visit a node more than once.", nameof(visitedNodeIds));
            for (int index = 1; index < history.Length; index++)
            {
                bool edgeExists = route.Connections.Any(connection =>
                    string.Equals(connection.FromNodeId, history[index - 1], StringComparison.Ordinal) &&
                    string.Equals(connection.ToNodeId, history[index], StringComparison.Ordinal));
                if (!edgeExists)
                    throw new ArgumentException("Saved route history contains a transition that does not exist: " +
                        history[index - 1] + " -> " + history[index] + ".", nameof(visitedNodeIds));
            }

            if (!route.Nodes.Any(node => string.Equals(node.NodeId, history[history.Length - 1], StringComparison.Ordinal)))
                throw new ArgumentException("The current saved route node does not exist.", nameof(visitedNodeIds));
            CurrentNodeId = history[history.Length - 1];
            _visitedNodeIds.AddRange(history);
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
