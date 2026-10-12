using Luckbound.Content;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Gameplay.Inventory
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LootPickup2D : MonoBehaviour
    {
        private InventoryItemStack _item;

        private Collider2D _pickupCollider;
        private ItemCollector2D _ignoredCollector;
        private bool _consumed;

        public InventoryItemStack Item => _item;
        public bool IsConsumed => _consumed;

        public void Initialize(InventoryItemStack item, ItemCatalog catalog)
        {
            if (item == null) throw new System.ArgumentNullException(nameof(item));
            if (catalog == null || !catalog.TryGet(item.ItemId, out _)) throw new System.ArgumentException("Pickup item must resolve from the supplied catalog.", nameof(item));
            _item = item;
            _consumed = false;
            EnsureTriggerCollider();
        }

        public void IgnoreCollectorUntilClear(ItemCollector2D collector)
        {
            _ignoredCollector = collector;
        }

        public void MarkCollected()
        {
            if (_consumed) return;
            _consumed = true;
            if (_pickupCollider != null) _pickupCollider.enabled = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed || _item == null) return;
            ItemCollector2D collector = other.GetComponentInParent<ItemCollector2D>();
            if (collector == null || collector == _ignoredCollector) return;
            collector.TryCollect(this);
        }

        private void FixedUpdate()
        {
            if (_ignoredCollector == null) return;
            Collider2D[] collectorColliders = _ignoredCollector.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D collectorCollider in collectorColliders)
            {
                if (collectorCollider != null && _pickupCollider != null && _pickupCollider.IsTouching(collectorCollider)) return;
            }
            _ignoredCollector = null;
        }

        private void EnsureTriggerCollider()
        {
            if (_pickupCollider == null) _pickupCollider = GetComponent<Collider2D>();
            if (_pickupCollider != null) _pickupCollider.isTrigger = true;
        }
    }
}
