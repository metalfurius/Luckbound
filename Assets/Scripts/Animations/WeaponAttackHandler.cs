using UnityEngine;
using System.Collections.Generic;

namespace _Scripts.Animations
{
    public class WeaponAttackHandler : MonoBehaviour
    {
        private BoxCollider2D _weaponCollider;
        private HashSet<Enemy> _hitEnemiesThisFrame = new HashSet<Enemy>();
        private LayerMask _enemyLayer;
        private float _currentFrameDamage;

        public void Initialize(BoxCollider2D weaponCollider, LayerMask enemyLayer)
        {
            _weaponCollider = weaponCollider;
            _enemyLayer = enemyLayer;
            _weaponCollider.enabled = false;
        }

        public void StartAttackFrame(float damage)
        {
            _currentFrameDamage = damage;
            _weaponCollider.enabled = true;
            _hitEnemiesThisFrame.Clear();
        }

        public void EndAttackFrame()
        {
            _weaponCollider.enabled = false;
        }

        private void Update()
        {
            if (!_weaponCollider.enabled) return;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _enemyLayer,
                useTriggers = true
            };

            var results = new List<Collider2D>();
            if (_weaponCollider.Overlap(filter, results) > 0)
            {
                foreach (var result in results)
                {
                    if (result == null) continue;
                
                    var enemy = result.GetComponent<Enemy>();
                    if (enemy != null && _hitEnemiesThisFrame.Add(enemy))
                    {
                        enemy.TakeDamage((int)_currentFrameDamage);
                    }
                }
            }
        }
    }
}