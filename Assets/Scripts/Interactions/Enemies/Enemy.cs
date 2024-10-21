using System;
using UnityEngine;

public class Enemy : Damageable
{
    public int resistance = 5;
    public EnemyState currentState = EnemyState.Idle;
    public BoxCollider2D areaOfInfluence;

    public virtual void Start()
    {
        if (areaOfInfluence)
            areaOfInfluence.isTrigger = true; 
        else
            Debug.LogError("Area of influence collider not found!");
    }

    public virtual void Update()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState();
                break;
            case EnemyState.Patrol:
                HandlePatrolState();
                break;
            case EnemyState.Chase:
                HandleChaseState();
                break;
            case EnemyState.Attack:
                HandleAttackState();
                break;
            case EnemyState.Flee:
                HandleFleeState();
                break;
            case EnemyState.Die:
                HandleDieState();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected virtual void HandleAttackState()
    {
    }

    protected virtual void HandleIdleState() 
    {
    }

    protected virtual void HandlePatrolState() 
    {
    }

    protected virtual void HandleChaseState() 
    {
    }

    protected virtual void HandleFleeState() 
    {
    }

    protected virtual void HandleDieState() 
    {
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) 
        {
            OnPlayerEnterArea(); 
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerExitArea();
        }
    }

    protected virtual void OnPlayerEnterArea()
    {
    }

    protected virtual void OnPlayerExitArea()
    {
    }

    public override void TakeDamage(int amount)
    {
        var _actualDamage = amount - resistance;
        base.TakeDamage(Randomizer.GetRandomizedInt(_actualDamage));
    }

    protected override void Die()
    {
        currentState = EnemyState.Die;
        Destroy(gameObject);
    }
}