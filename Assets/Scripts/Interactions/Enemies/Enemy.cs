using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int health = 50;
    public int resistance = 5;
    public SpriteRenderer spriteRenderer; 

    public virtual void TakeDamage(int amount)
    {
        var _actualDamage = amount - resistance;
        health -= _actualDamage;
        if (health <= 0)
            Die();
    }

    public virtual void Die()
    {
        Destroy(gameObject);
    }
}