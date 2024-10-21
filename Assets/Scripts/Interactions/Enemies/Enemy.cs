using System;
using UnityEngine;

public class Enemy : Damageable
{
    public int resistance = 5;
    public EnemyState currentState = EnemyState.Idle;
    public BoxCollider2D areaOfInfluence;

    private void Start()
    {
        areaOfInfluence = GetComponent<BoxCollider2D>(); 
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

    public virtual void HandleAttackState()
    {
    }

    public virtual void HandleIdleState() 
    {
    }

    public virtual void HandlePatrolState() 
    {
    }

    public virtual void HandleChaseState() 
    {
    }
    public virtual void HandleFleeState() 
    {
    }
    public virtual void HandleDieState() 
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

    public virtual void OnPlayerEnterArea()
    {
        if (currentState is EnemyState.Idle or EnemyState.Patrol)
        {
            currentState = EnemyState.Chase;
        }
    }

    public virtual void OnPlayerExitArea()
    {
        if (currentState == EnemyState.Chase)
        {
            currentState = EnemyState.Idle; // Or back to Patrol
        }
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