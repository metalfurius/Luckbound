using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Weapon currentWeapon;
    public Animator animator;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) Attack();
    }

    private void Attack()
    {
        if (currentWeapon) animator.SetTrigger("Attack");
    }
}