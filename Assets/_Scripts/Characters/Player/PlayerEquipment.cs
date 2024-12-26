using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private Weapon currentWeapon;

    public Weapon CurrentWeapon => currentWeapon;

    public void EquipWeapon(Weapon newWeapon)
    {
        currentWeapon = newWeapon;
    }

    public void PerformWeaponAttack(BaseAnimator animator)
    {
        if (currentWeapon)
        {
            currentWeapon.PerformAttack(animator);
        }
    }
}