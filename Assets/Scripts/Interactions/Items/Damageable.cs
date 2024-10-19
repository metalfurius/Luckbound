using UnityEngine;

public class Damageable : MonoBehaviour
{
    public int health;

    public virtual void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject); 
    }
}