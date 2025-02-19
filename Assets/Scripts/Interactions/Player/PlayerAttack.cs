using UnityEngine;
using System.Collections;

public class AttackManager : MonoBehaviour
{
    [System.Serializable]
    public class AttackData
    {
        public int damage;              
        public string animationTrigger;
        public AnimationClip animationClip;
    }

    [Header("Combo Settings")]
    [SerializeField] private AttackData[] comboAttacks = new AttackData[3];
    [SerializeField] private float comboWindow = 0.5f;

    private PlayerInput _playerInput;
    private Animator _animator;

    private bool _isAttacking = false;
    private int _currentAttackIndex = 0;
    private float _attackStartTime = 0f;
    private bool _isBuffered = false;
    private bool _pendingAttack = false;
    private Coroutine _attackCoroutine;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _animator = transform.Find("Sprite").GetComponent<Animator>();
    }

    private void Update()
    {
        if (_playerInput.AttackInput)
        {
            if (!_isAttacking)
            {
                StartAttack(_currentAttackIndex);
            }
            else
            {
                float timeSinceStart = Time.time - _attackStartTime;
                if (timeSinceStart <= comboWindow)
                {
                    _isBuffered = true;
                }
                else
                {
                    _pendingAttack = true;
                }
            }
        }
    }

    private void StartAttack(int index)
    {
        if (index >= comboAttacks.Length)
        {
            index = 0;
        }

        AttackData currentAttack = comboAttacks[index];
        if (currentAttack.animationClip == null)
        {
            Debug.LogError($"AnimationClip is not assigned for attack at index {index}");
            return;
        }

        _animator.SetTrigger(currentAttack.animationTrigger);
        _attackStartTime = Time.time;
        _isAttacking = true;
        _isBuffered = false;
        _pendingAttack = false;

        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }
        _attackCoroutine = StartCoroutine(AttackTimer(currentAttack.animationClip.length));
    }

    private IEnumerator AttackTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        OnAttackFinished();
    }

    private void OnAttackFinished()
    {
        if (_isBuffered)
        {
            _currentAttackIndex = (_currentAttackIndex + 1) % comboAttacks.Length;
            StartAttack(_currentAttackIndex);
        }
        else
        {
            _isAttacking = false;
            if (_pendingAttack)
            {
                StartAttack(0);
                _pendingAttack = false;
            }
        }
    }
}