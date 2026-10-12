using System;
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
        private Func<string, GameObject> _prefabResolver;
        private Transform _sessionPlayer;
        private GameObject _activeRoomRoot;
        private RoomLifetimeScope _activeScope;
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

        public void Initialize(ExpeditionRoutePlan route, Func<string, GameObject> prefabResolver, Transform sessionPlayer)
        {
            if (_initialized) throw new InvalidOperationException("ExpeditionRoomTransitionController can only be initialized once.");
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (prefabResolver == null) throw new ArgumentNullException(nameof(prefabResolver));
            if (sessionPlayer == null) throw new ArgumentNullException(nameof(sessionPlayer));

            _route = route;
            _prefabResolver = prefabResolver;
            _sessionPlayer = sessionPlayer;
            _traversal = new ExpeditionRouteTraversal(route);
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
            if (!_initialized || _disposed || _transitioning || !IsCurrentRoom(sourceRoomId) ||
                !_traversal.CanTransitionTo(targetNodeId))
            {
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
                if (!TryAlignConnectedRoom(targetNodeId, nextRoot, out Vector3 playerSpawn))
                {
                    DiscardStagedRoom(nextRoot, nextScope);
                    return false;
                }

                if (!_traversal.TryTransitionTo(targetNodeId))
                {
                    DiscardStagedRoom(nextRoot, nextScope);
                    return false;
                }

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RetireActiveRoom();
            RoomActivated = null;
            _prefabResolver = null;
            _sessionPlayer = null;
            _traversal = null;
            _route = null;
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
                stagedScope.Initialize(RoomInstanceId.New(), node.TemplateId);
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
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            else
            {
                _sessionPlayer.position = position;
            }
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
    }
}
