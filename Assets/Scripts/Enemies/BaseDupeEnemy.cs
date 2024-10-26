using UnityEngine;

public class BaseDupeEnemy : Enemy
{
    public int attackBodyDamage = 60;
    private Transform _playerTarget;
    private Rigidbody2D _rb;

    public override void Start()
    {
        base.Start();
        _playerTarget = GameObject.FindGameObjectWithTag("Player").transform;
        _rb = GetComponent<Rigidbody2D>(); 
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Hit");
            var player = collision.gameObject.GetComponent<PlayerStats>();
            if (player)
            {
                player.TakeDamage(attackBodyDamage);
            }
        }
    }
    protected override void HandleAttackState() { }
    protected override void HandleIdleState() { }
    protected override void HandlePatrolState() { }
    protected override void HandleChaseState() { }
    protected override void HandleFleeState() { }
    protected override void HandleDieState() { }
    protected override void OnPlayerEnterArea()
    {
        currentState = EnemyState.Chase;
    }
    protected override void OnPlayerExitArea()
    {
        currentState = EnemyState.Patrol;
    }
}