using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Replaces the active room only after the target route node has been staged successfully.</summary>
    public sealed class ExpeditionRoomTransitionController : MonoBehaviour, IDisposable
    {
        private ExpeditionRoutePlan _route;
        private ExpeditionRouteTraversal _traversal;
        private RunId _runId;
        private Func<string, GameObject> _prefabResolver;
        private Transform _sessionPlayer;
        private GameObject _activeRoomRoot;
        private RoomLifetimeScope _activeScope;
        private PreparedRoomTransition _preparedTransition;
        private bool _initialized;
        private bool _transitioning;
        private bool _disposed;

        public event Action<RoomLifetimeScope> RoomActivated;

        public string CurrentNodeId => _traversal == null ? string.Empty : _traversal.CurrentNodeId;
        public RoomInstanceId CurrentRoomId => _activeScope == null ? default : _activeScope.RoomInstanceId;
        public GameObject ActiveRoomRoot => _activeRoomRoot;
        public RoomLifetimeScope ActiveScope => _activeScope;
        public int ActiveRoomCount => _activeRoomRoot != null && _activeRoomRoot.activeInHierarchy ? 1 : 0;
        public bool IsDisposed => _disposed;
        public string LastTransitionError { get; private set; } = string.Empty;

        public void Initialize(RunId runId, ExpeditionRoutePlan route, Func<string, GameObject> prefabResolver,
            Transform sessionPlayer)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            Initialize(runId, route, prefabResolver, sessionPlayer, new[] { route.MandatoryNodeIds[0] });
        }

        public void Initialize(RunId runId, ExpeditionRoutePlan route, Func<string, GameObject> prefabResolver,
            Transform sessionPlayer, IEnumerable<string> visitedNodeIds)
        {
            if (_initialized) throw new InvalidOperationException("ExpeditionRoomTransitionController can only be initialized once.");
            if (!runId.IsValid) throw new ArgumentException("Room transitions require a valid expedition RunId.", nameof(runId));
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (prefabResolver == null) throw new ArgumentNullException(nameof(prefabResolver));
            if (sessionPlayer == null) throw new ArgumentNullException(nameof(sessionPlayer));

            _runId = runId;
            _route = route;
            _prefabResolver = prefabResolver;
            _sessionPlayer = sessionPlayer;
            _traversal = new ExpeditionRouteTraversal(route, visitedNodeIds);
            ExpeditionRouteNode initialNode = FindNode(_traversal.CurrentNodeId);
            if (!TryStageRoom(initialNode, out GameObject roomRoot, out RoomLifetimeScope scope))
            {
                throw new InvalidOperationException(LastTransitionError);
            }

            if (!TryGetPlayerSpawn(roomRoot, out Vector3 playerSpawn))
            {
                DiscardStagedRoom(roomRoot, scope);
                throw new InvalidOperationException(LastTransitionError);
            }

            _initialized = true;
            _activeRoomRoot = roomRoot;
            _activeScope = scope;
            PlaceSessionPlayer(playerSpawn);
            roomRoot.SetActive(true);
            RaiseRoomActivated(scope);
        }

        public bool IsCurrentRoom(RoomInstanceId roomInstanceId)
        {
            return !_disposed && _activeScope != null && !_activeScope.IsDisposed &&
                _activeScope.RoomInstanceId == roomInstanceId;
        }

        public bool TryApplyToCurrentRoom(RoomInstanceId sourceRoomId, Action<RoomLifetimeScope> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!IsCurrentRoom(sourceRoomId) || _activeScope.IsDisposed) return false;

            action(_activeScope);
            return true;
        }

        public bool TryTransition(RoomInstanceId sourceRoomId, string targetNodeId)
        {
            if (!TryPrepareTransition(sourceRoomId, targetNodeId, out PreparedRoomTransition prepared))
                return false;
            using (prepared) return TryCommitPreparedTransition(prepared);
        }

        /// <summary>
        /// Stages and aligns a connected destination while retaining the current traversal, player and room.
        /// Dispose the returned lease to cancel it, or commit it after the durable route update succeeds.
        /// </summary>
        public bool TryPrepareTransition(RoomInstanceId sourceRoomId, string targetNodeId,
            out PreparedRoomTransition prepared)
        {
            prepared = null;
            if (!_initialized || _disposed || _transitioning || _preparedTransition != null ||
                !IsCurrentRoom(sourceRoomId) || !_traversal.CanTransitionTo(targetNodeId))
            {
                LastTransitionError = "The source room is stale or the requested destination is not a route edge.";
                return false;
            }
            if (_sessionPlayer == null)
            {
                LastTransitionError = "The expedition session player is no longer available.";
                return false;
            }

            ExpeditionRouteNode targetNode = FindNode(targetNodeId);
            _transitioning = true;
            try
            {
                if (!TryStageRoom(targetNode, out GameObject nextRoot, out RoomLifetimeScope nextScope)) return false;
                try
                {
                    if (!TryAlignConnectedRoom(targetNodeId, nextRoot, out Vector3 playerSpawn))
                    {
                        DiscardStagedRoom(nextRoot, nextScope);
                        return false;
                    }

                    prepared = new PreparedRoomTransition(this, sourceRoomId, _traversal.CurrentNodeId,
                        targetNodeId, nextRoot, nextScope, playerSpawn);
                    _preparedTransition = prepared;
                    LastTransitionError = string.Empty;
                    return true;
                }
                catch (Exception exception)
                {
                    DiscardStagedRoom(nextRoot, nextScope);
                    LastTransitionError = "Could not align room " + targetNodeId + ": " + exception.Message;
                    return false;
                }
            }
            finally
            {
                _transitioning = false;
            }
        }

        /// <summary>Publishes a prepared destination after its matching durable route update has succeeded.</summary>
        public bool TryCommitPreparedTransition(PreparedRoomTransition prepared)
        {
            if (prepared == null || _disposed || _transitioning ||
                !ReferenceEquals(_preparedTransition, prepared) || !prepared.IsPending ||
                !IsCurrentRoom(prepared.SourceRoomId) ||
                !string.Equals(_traversal.CurrentNodeId, prepared.SourceNodeId, StringComparison.Ordinal) ||
                !_traversal.CanTransitionTo(prepared.TargetNodeId) ||
                prepared.StagedRoomRoot == null || prepared.StagedScope == null || prepared.StagedScope.IsDisposed)
            {
                LastTransitionError = "The prepared room transition is no longer valid.";
                if (prepared != null && ReferenceEquals(_preparedTransition, prepared))
                    CancelPreparedTransition(prepared);
                return false;
            }

            _transitioning = true;
            try
            {
                if (!_traversal.TryTransitionTo(prepared.TargetNodeId))
                {
                    LastTransitionError = "The requested destination is no longer a route edge.";
                    CancelPreparedTransition(prepared);
                    return false;
                }

                GameObject nextRoot = prepared.StagedRoomRoot;
                RoomLifetimeScope nextScope = prepared.StagedScope;
                Vector3 playerSpawn = prepared.PlayerSpawn;
                prepared.MarkCommitted();
                _preparedTransition = null;

                RetireActiveRoom();
                PlaceSessionPlayer(playerSpawn);
                _activeRoomRoot = nextRoot;
                _activeScope = nextScope;
                nextRoot.SetActive(true);
                RaiseRoomActivated(nextScope);
                LastTransitionError = string.Empty;
                return true;
            }
            finally
            {
                _transitioning = false;
            }
        }

        private void CancelPreparedTransition(PreparedRoomTransition prepared)
        {
            if (prepared == null || !prepared.IsPending) return;
            if (ReferenceEquals(_preparedTransition, prepared)) _preparedTransition = null;
            DiscardStagedRoom(prepared.StagedRoomRoot, prepared.StagedScope);
            prepared.MarkCancelled();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_preparedTransition != null)
                CancelPreparedTransition(_preparedTransition);
            RetireActiveRoom();
            RoomActivated = null;
            _prefabResolver = null;
            _sessionPlayer = null;
            _traversal = null;
            _route = null;
            _runId = default;
        }

        private ExpeditionRouteNode FindNode(string nodeId)
        {
            return _route.Nodes.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
        }

        private bool TryStageRoom(ExpeditionRouteNode node, out GameObject roomRoot, out RoomLifetimeScope scope)
        {
            roomRoot = null;
            scope = null;
            GameObject prefab;
            try
            {
                prefab = _prefabResolver(node.TemplateId);
            }
            catch (Exception exception)
            {
                LastTransitionError = "Room prefab lookup failed for template " + node.TemplateId + ": " + exception.Message;
                return false;
            }
            if (prefab == null)
            {
                LastTransitionError = "No room prefab is registered for template " + node.TemplateId + ".";
                return false;
            }

            GameObject stagedRoot = new GameObject("RoomInstance." + node.NodeId);
            stagedRoot.SetActive(false);
            stagedRoot.transform.SetParent(transform, false);
            stagedRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            try
            {
                GameObject content = Instantiate(prefab, stagedRoot.transform, false);
                content.name = "RoomContent." + node.TemplateId;
                content.transform.localPosition = Vector3.zero;
                content.transform.localRotation = Quaternion.identity;
                content.transform.localScale = Vector3.one;
                RoomLifetimeScope stagedScope = stagedRoot.AddComponent<RoomLifetimeScope>();
                stagedScope.Initialize(ExpeditionRoomIdentity.ForNode(_runId, node.NodeId), node.TemplateId);
                roomRoot = stagedRoot;
                scope = stagedScope;
                LastTransitionError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                LastTransitionError = "Could not stage room " + node.NodeId + ": " + exception.Message;
                Destroy(stagedRoot);
                return false;
            }
        }

        private bool TryGetPlayerSpawn(GameObject roomRoot, out Vector3 playerSpawn)
        {
            RoomPositionMarker2D[] markers = roomRoot.GetComponentsInChildren<RoomPositionMarker2D>(true)
                .Where(marker => marker.Kind == "player_spawn").ToArray();
            if (markers.Length != 1)
            {
                playerSpawn = Vector3.zero;
                LastTransitionError = "Room " + _traversal.CurrentNodeId + " must expose exactly one player_spawn marker.";
                return false;
            }

            playerSpawn = markers[0].transform.position;
            return true;
        }

        private bool TryAlignConnectedRoom(string targetNodeId, GameObject nextRoot, out Vector3 playerSpawn)
        {
            playerSpawn = Vector3.zero;
            ExpeditionRouteConnection connection = _route.Connections.FirstOrDefault(candidate =>
                string.Equals(candidate.FromNodeId, _traversal.CurrentNodeId, StringComparison.Ordinal) &&
                string.Equals(candidate.ToNodeId, targetNodeId, StringComparison.Ordinal));
            if (connection == null)
            {
                LastTransitionError = "No route connection exists from " + _traversal.CurrentNodeId + " to " + targetNodeId + ".";
                return false;
            }

            RoomPortMarker2D sourcePort = FindPort(_activeRoomRoot, connection.FromPortId);
            RoomPortMarker2D targetPort = FindPort(nextRoot, connection.ToPortId);
            if (sourcePort == null || targetPort == null || sourcePort.Type != "exit" || targetPort.Type != "entry" ||
                sourcePort.Side != "east" || targetPort.Side != "west")
            {
                LastTransitionError = "Route connection ports are missing or have incompatible types for " + targetNodeId + ".";
                return false;
            }

            Vector3 sourceExitWorldPosition = sourcePort.transform.position;
            nextRoot.transform.position += sourceExitWorldPosition - targetPort.transform.position;
            Vector3 inward = nextRoot.transform.right;
            playerSpawn = targetPort.transform.position + inward * 0.5f;
            return true;
        }

        private static RoomPortMarker2D FindPort(GameObject roomRoot, string portId)
        {
            return roomRoot.GetComponentsInChildren<RoomPortMarker2D>(true).SingleOrDefault(marker =>
                string.Equals(marker.PortId, portId, StringComparison.Ordinal));
        }

        private void PlaceSessionPlayer(Vector3 position)
        {
            Rigidbody2D body = _sessionPlayer.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = new Vector2(position.x, position.y);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            _sessionPlayer.SetPositionAndRotation(position, _sessionPlayer.rotation);
            Physics2D.SyncTransforms();
        }

        private void RetireActiveRoom()
        {
            RoomLifetimeScope retiredScope = _activeScope;
            GameObject retiredRoot = _activeRoomRoot;
            _activeScope = null;
            _activeRoomRoot = null;
            if (retiredScope != null) retiredScope.Dispose();
            if (retiredRoot != null)
            {
                retiredRoot.SetActive(false);
                Destroy(retiredRoot);
            }
        }

        private void DiscardStagedRoom(GameObject roomRoot, RoomLifetimeScope scope)
        {
            if (scope != null) scope.Dispose();
            if (roomRoot != null) Destroy(roomRoot);
        }

        private void RaiseRoomActivated(RoomLifetimeScope scope)
        {
            RoomActivated?.Invoke(scope);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public sealed class PreparedRoomTransition : IDisposable
        {
            private ExpeditionRoomTransitionController _owner;
            private bool _pending = true;

            internal PreparedRoomTransition(ExpeditionRoomTransitionController owner, RoomInstanceId sourceRoomId,
                string sourceNodeId, string targetNodeId, GameObject stagedRoomRoot, RoomLifetimeScope stagedScope,
                Vector3 playerSpawn)
            {
                _owner = owner;
                SourceRoomId = sourceRoomId;
                SourceNodeId = sourceNodeId;
                TargetNodeId = targetNodeId;
                StagedRoomRoot = stagedRoomRoot;
                StagedScope = stagedScope;
                PlayerSpawn = playerSpawn;
            }

            public RoomInstanceId SourceRoomId { get; }
            public string SourceNodeId { get; }
            public string TargetNodeId { get; }
            public bool IsPending => _pending;
            public bool IsStagedActive => StagedRoomRoot != null && StagedRoomRoot.activeInHierarchy;
            public bool IsStagedDisposed => StagedScope == null || StagedScope.IsDisposed;
            internal GameObject StagedRoomRoot { get; }
            internal RoomLifetimeScope StagedScope { get; }
            internal Vector3 PlayerSpawn { get; }

            public void Dispose()
            {
                ExpeditionRoomTransitionController owner = _owner;
                if (owner != null) owner.CancelPreparedTransition(this);
            }

            internal void MarkCommitted()
            {
                _pending = false;
                _owner = null;
            }

            internal void MarkCancelled()
            {
                _pending = false;
                _owner = null;
            }
        }
    }
}
