using UnityEngine;
using System.Collections;

public class Damageable : MonoBehaviour
{
    public int health;
    public int timeToDestroy;

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
        StartCoroutine(DelayedDestroy());
    }
    private IEnumerator DelayedDestroy()
    {
        yield return new WaitForSeconds(timeToDestroy);
        Destroy(gameObject);
    }
}