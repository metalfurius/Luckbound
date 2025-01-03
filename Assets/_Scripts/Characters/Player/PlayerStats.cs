using UnityEngine;

public class PlayerStats : Damageable
{
    public int mana = 50;
    public int resistance = 10;
    public BaseData playerAnimations;

    public override void TakeDamage(int amount)
    {
        var _actualDamage = amount - resistance; 
        health -= Randomizer.GetRandomizedInt(_actualDamage);

        if (health <= 0)
        {
            Die();
        }
    }

    protected override void Die()
    {
        base.Die();
        Debug.Log("Player has died!"); 
    }
}