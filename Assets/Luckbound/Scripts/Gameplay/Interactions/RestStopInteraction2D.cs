using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Interactions
{
    /// <summary>Raises one stable rest-stop interaction request for a nearby player pressing Interact.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class RestStopInteraction2D : MonoBehaviour
    {
        [SerializeField] private string _roomInstanceId;

        private readonly Dictionary<ActorId, int> _nearbyPlayerColliderCounts = new Dictionary<ActorId, int>();
        private readonly Dictionary<ActorId, long> _lastInteractionTicks = new Dictionary<ActorId, long>();
        private Collider2D _interactionCollider;

        public event Action<RoomInstanceId, ActorId, long> InteractionRequested;

        public bool IsConfigured => RoomInstanceId.TryParse(_roomInstanceId, out _);

        public void Initialize(RoomInstanceId roomInstanceId)
        {
            if (!roomInstanceId.IsValid)
                throw new ArgumentException("Rest-stop interactions require a valid room instance ID.", nameof(roomInstanceId));

            string value = roomInstanceId.ToString();
            if (IsConfigured && !string.Equals(_roomInstanceId, value, StringComparison.Ordinal))
                throw new InvalidOperationException("A rest-stop interaction cannot change its room instance ID after initialization.");

            _roomInstanceId = value;
            EnsureTriggerCollider();
        }

        public bool TryInteract(PlayerCommand command)
        {
            if (!IsConfigured || !command.ActorId.IsValid || !command.WasPressed(PlayerAction.Interact) ||
                !_nearbyPlayerColliderCounts.ContainsKey(command.ActorId))
                return false;

            if (_lastInteractionTicks.TryGetValue(command.ActorId, out long previousTick) && command.Tick <= previousTick)
                return false;

            RoomInstanceId roomInstanceId = RoomInstanceId.Parse(_roomInstanceId);
            _lastInteractionTicks[command.ActorId] = command.Tick;
            InteractionRequested?.Invoke(roomInstanceId, command.ActorId, command.Tick);
            return true;
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMotor2D player = other == null ? null : other.GetComponentInParent<PlayerMotor2D>();
            if (player == null || !player.ActorId.IsValid) return;

            _nearbyPlayerColliderCounts.TryGetValue(player.ActorId, out int count);
            _nearbyPlayerColliderCounts[player.ActorId] = count + 1;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerMotor2D player = other == null ? null : other.GetComponentInParent<PlayerMotor2D>();
            if (player == null || !player.ActorId.IsValid || !_nearbyPlayerColliderCounts.TryGetValue(player.ActorId, out int count))
                return;

            if (count <= 1) _nearbyPlayerColliderCounts.Remove(player.ActorId);
            else _nearbyPlayerColliderCounts[player.ActorId] = count - 1;
        }

        private void EnsureTriggerCollider()
        {
            if (_interactionCollider == null) _interactionCollider = GetComponent<Collider2D>();
            if (_interactionCollider != null) _interactionCollider.isTrigger = true;
        }
    }
}
