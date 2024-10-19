using UnityEngine;

public class Enemy : Damageable
{
    public int resistance = 5;

    public override void TakeDamage(int amount)
    {
        var _actualDamage = amount - resistance;
        base.TakeDamage(Randomizer.GetRandomizedInt(_actualDamage));
    }

    protected override void Die()
    {
        Destroy(gameObject);
    }
}