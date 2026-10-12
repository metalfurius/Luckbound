using Luckbound.Content;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Gameplay.Inventory
{
    /// <summary>Creates a displaced pickup below an inactive owner before an inventory swap.</summary>
    public sealed class LootPickupDropStager2D : MonoBehaviour
    {
        [SerializeField] private LootPickup2D _dropPrefab;
        [SerializeField] private Transform _worldDropRoot;

        private Transform _inactiveStagingRoot;

        public void Configure(LootPickup2D dropPrefab, Transform worldDropRoot = null)
        {
            _dropPrefab = dropPrefab;
            _worldDropRoot = worldDropRoot;
        }

        public bool TryStageDrop(
            InventoryItemStack displacedItem,
            ItemCatalog catalog,
            Vector3 worldPosition,
            ItemCollector2D replacingCollector,
            out IStagedLootDrop stagedDrop)
        {
            stagedDrop = null;
            Transform worldRoot = _worldDropRoot;
            if (displacedItem == null || catalog == null || _dropPrefab == null || replacingCollector == null ||
                worldRoot == null || !worldRoot.gameObject.activeInHierarchy) return false;
            EnsureInactiveRoot();

            LootPickup2D instance = null;
            try
            {
                instance = Instantiate(_dropPrefab, _inactiveStagingRoot);
                instance.transform.position = worldPosition;
                instance.Initialize(displacedItem, catalog);
                instance.IgnoreCollectorUntilClear(replacingCollector);
                stagedDrop = new StagedDrop(instance, worldRoot);
                return true;
            }
            catch (System.Exception)
            {
                if (instance != null) Destroy(instance.gameObject);
                return false;
            }
        }

        private void EnsureInactiveRoot()
        {
            if (_inactiveStagingRoot != null) return;
            var root = new GameObject("InactiveLootDropStaging");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            _inactiveStagingRoot = root.transform;
        }

        private sealed class StagedDrop : IStagedLootDrop
        {
            private readonly LootPickup2D _pickup;
            private readonly Transform _worldRoot;
            private bool _finished;

            public StagedDrop(LootPickup2D pickup, Transform worldRoot)
            {
                _pickup = pickup;
                _worldRoot = worldRoot;
            }

            public void Commit()
            {
                if (_finished) return;
                if (_pickup == null || _worldRoot == null) throw new System.InvalidOperationException("Staged world drop lost its owner before commit.");
                _pickup.transform.SetParent(_worldRoot, true);
                _finished = true;
                _pickup.gameObject.SetActive(true);
            }

            public void Dispose()
            {
                if (_finished) return;
                _finished = true;
                if (_pickup != null) Destroy(_pickup.gameObject);
            }
        }
    }
}
