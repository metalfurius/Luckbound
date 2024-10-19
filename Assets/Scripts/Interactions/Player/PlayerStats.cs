using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int health = 100; 
    public int mana = 50;
    public int resistance = 10;

    public void TakeDamage(int amount)
    {
        var _actualDamage = amount - resistance; 
        health -= Randomizer.GetRandomizedInt(_actualDamage);

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player has died!"); 
    }
}