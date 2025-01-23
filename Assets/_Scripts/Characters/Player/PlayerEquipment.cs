using _Scripts.Animations;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private Weapon currentWeapon;
    [SerializeField] private LayerMask enemyLayer;
    private WeaponAttackHandler _attackHandler;

    private void Awake()
    {
        _attackHandler = gameObject.AddComponent<WeaponAttackHandler>();
        var componentInChildren = this.transform.Find("ColliderWeapon").GetComponentInChildren<BoxCollider2D>();
        _attackHandler.Initialize(componentInChildren, enemyLayer);
        
        if (currentWeapon != null)
        {
            currentWeapon.Initialize(_attackHandler);
        }
    }

    public void EquipWeapon(Weapon newWeapon)
    {
        currentWeapon = newWeapon;
        currentWeapon.Initialize(_attackHandler);
    }

    public void PerformWeaponAttack(BaseAnimator animator)
    {
        if (currentWeapon)
        {
            currentWeapon.PerformAttack(animator);
        }
    }
}