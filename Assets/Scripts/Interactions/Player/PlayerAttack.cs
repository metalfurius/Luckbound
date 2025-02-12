using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    private PlayerInput _playerInput;
    private Animator _animator;

    public Transform attackHitboxData;
    public LayerMask enemyLayer;
    [SerializeField] private string isAttacking = "isAttacking"; 

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _animator = this.transform.Find("Sprite").GetComponent<Animator>();
    }

    private void Update()
    {
        if (_playerInput.AttackInput)
        {
            _animator.SetBool(isAttacking, true);
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        
    }
}