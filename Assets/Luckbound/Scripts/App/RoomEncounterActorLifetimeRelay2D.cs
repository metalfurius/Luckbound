using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Observes only root availability/ownership; it never signals or infers defeat.</summary>
    [DisallowMultipleComponent]
    internal sealed class RoomEncounterActorLifetimeRelay2D : MonoBehaviour
    {
        private RoomEncounterActorBinding _binding;
        private bool _armed;

        internal void Initialize(RoomEncounterActorBinding binding)
        {
            if (_binding != null) throw new System.InvalidOperationException("Actor lifetime relay can only bind once.");
            if (binding == null) throw new System.ArgumentNullException(nameof(binding));
            if (!ReferenceEquals(binding.Root, gameObject))
                throw new System.InvalidOperationException("Actor lifetime relay must be attached to the bound root.");
            _binding = binding;
        }

        internal void ArmForActivation()
        {
            if (_binding == null || _armed || gameObject.activeSelf)
                throw new System.InvalidOperationException("Root relay must be armed before its inactive actor activates.");
            _armed = true;
        }

        internal void Detach(RoomEncounterActorBinding binding)
        {
            if (!ReferenceEquals(_binding, binding)) return;
            _armed = false;
            _binding = null;
        }

        private void OnDisable()
        {
            NotifyUnavailable();
        }

        private void OnDestroy()
        {
            NotifyUnavailable();
            _binding = null;
            _armed = false;
        }

        private void OnTransformParentChanged()
        {
            NotifyUnavailable();
        }

        private void NotifyUnavailable()
        {
            if (_armed && _binding != null) _binding.HandleRootUnavailable(this);
        }
    }
}
