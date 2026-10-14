using Luckbound.Content;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Gameplay.Inventory
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldMaterialPickup2D : MonoBehaviour
    {
        private Collider2D _pickupCollider;
        private bool _consumed;

        public MaterialKind MaterialKind { get; private set; }
        public int Amount { get; private set; }
        public bool IsConsumed => _consumed;

        public void Initialize(MaterialKind materialKind, int amount, ItemCatalog catalog)
        {
            if (!System.Enum.IsDefined(typeof(MaterialKind), materialKind)) throw new System.ArgumentOutOfRangeException(nameof(materialKind));
            if (amount <= 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (catalog == null || catalog.GetMaterial(materialKind) == null)
                throw new System.ArgumentException("World material pickup must resolve through the supplied catalog.", nameof(catalog));

            MaterialKind = materialKind;
            Amount = amount;
            _consumed = false;
            EnsureTriggerCollider();
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
            if (_consumed || Amount <= 0) return;
            ItemCollector2D collector = other.GetComponentInParent<ItemCollector2D>();
            if (collector != null) collector.TryCollectWorldMaterial(this);
        }

        private void EnsureTriggerCollider()
        {
            if (_pickupCollider == null) _pickupCollider = GetComponent<Collider2D>();
            if (_pickupCollider != null) _pickupCollider.isTrigger = true;
        }
    }
}
