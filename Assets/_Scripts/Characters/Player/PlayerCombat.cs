using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Weapon currentWeapon;
    private CustomWeaponAnimator _customWeaponAnimator;
    private PlayerInput _playerInput;
    public BoxCollider2D weaponCollider; // Reference this in inspector

    void Start()
    {
        _customWeaponAnimator = GetComponent<CustomWeaponAnimator>(); 
        _playerInput = GetComponent<PlayerInput>();
        
        // Pass the collider reference to the animator
        if (_customWeaponAnimator != null && weaponCollider != null)
        {
            _customWeaponAnimator.Initialize(weaponCollider);
        }
    }

    void Update()
    {
        if (_playerInput && _playerInput.AttackInput)
        {
            Attack();
        }
    }

    private void Attack() 
    {
        if (currentWeapon && currentWeapon.attackAnimations.Length > 0)
        {
            _customWeaponAnimator.PlayAnimation(currentWeapon.attackAnimations[0]);
        }
    }
}