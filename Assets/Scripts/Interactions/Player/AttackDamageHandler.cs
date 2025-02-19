using UnityEngine;

public class AttackDamageHandler : MonoBehaviour
{
    private int _currentDamage;
    private ContactFilter2D _enemyFilter;

    private void Awake()
    {
        _enemyFilter = new ContactFilter2D();
        _enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
        _enemyFilter.useTriggers = true;
    }

    public void SetCurrentDamage(int damage)
    {
        _currentDamage = damage;
    }

    public void CheckColliderDamage(string colliderName)
    {
        // Find the collider child by name
        Transform colliderTransform = transform.Find(colliderName);
        if (colliderTransform == null)
        {
            Debug.LogError($"Attack collider {colliderName} not found!");
            return;
        }

        EdgeCollider2D attackCollider = colliderTransform.GetComponent<EdgeCollider2D>();
        if (attackCollider == null)
        {
            Debug.LogError($"No EdgeCollider2D found on {colliderName}!");
            return;
        }

        // Temporarily enable collider if needed
        bool wasEnabled = attackCollider.enabled;
        attackCollider.enabled = true;

        // Detect overlaps
        Collider2D[] results = new Collider2D[10];
        int hitCount = attackCollider.Overlap(_enemyFilter, results);

        for (int i = 0; i < hitCount; i++)
        {
            Damageable damageable = results[i].GetComponent<Damageable>();
            damageable?.TakeDamage(_currentDamage);
        }

        // Restore original state
        attackCollider.enabled = wasEnabled;
    }
}