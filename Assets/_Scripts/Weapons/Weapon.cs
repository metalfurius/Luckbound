using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;

    public WeaponData WeaponData => weaponData;

    public void PerformAttack(BaseAnimator animator)
    {
        foreach (var animationRequest in weaponData.animations)
        {
            animator.PlayAnimation(animationRequest);
        }
    }
}