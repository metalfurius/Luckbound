using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Small main-thread driver; LateUpdate drains only after Unity's automatic FixedUpdate physics batch.</summary>
    [DisallowMultipleComponent]
    internal sealed class RoomEncounterDriver2D : MonoBehaviour
    {
        private RoomEncounterRuntime _runtime;
        private bool _wasInitialized;

        internal void Initialize(RoomEncounterRuntime runtime)
        {
            if (_wasInitialized) throw new System.InvalidOperationException("Room encounter driver can only be initialized once.");
            if (runtime == null) throw new System.ArgumentNullException(nameof(runtime));
            _wasInitialized = true;
            _runtime = runtime;
            enabled = true;
        }

        internal void Detach(RoomEncounterRuntime runtime)
        {
            if (!ReferenceEquals(_runtime, runtime)) return;
            _runtime = null;
            enabled = false;
        }

        private void LateUpdate()
        {
            RoomEncounterRuntime runtime = _runtime;
            if (runtime != null) runtime.DrainAfterPhysics();
        }

        private void OnDisable()
        {
            RoomEncounterRuntime runtime = _runtime;
            if (runtime != null) runtime.NotifyDriverUnavailable(this);
        }

        private void OnDestroy()
        {
            RoomEncounterRuntime runtime = _runtime;
            if (runtime != null) runtime.NotifyDriverUnavailable(this);
            _runtime = null;
        }
    }
}
