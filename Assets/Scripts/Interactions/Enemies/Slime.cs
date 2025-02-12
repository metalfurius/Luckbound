using UnityEngine;

public class Slime : Enemy
{
    public float jumpForce = 5f;
    public float jumpInterval = 2f; 
    public int attackDamage = 10;
    private Transform _playerTarget;
    private Rigidbody2D _rb;
    private float _nextJumpTime;

    public override void Start()
    {
        base.Start();
        _playerTarget = GameObject.FindGameObjectWithTag("Player").transform;
        _rb = GetComponent<Rigidbody2D>(); 
        _nextJumpTime = Time.time; 
    }

    protected override void HandleChaseState()
    {
        if (Time.time > _nextJumpTime)
        {
            JumpTowardsPlayer();
            _nextJumpTime = Time.time + Randomizer.GetRandomizedInt(jumpInterval);
        }
    }

    private void JumpTowardsPlayer()
    {
        if (_playerTarget)
        {
            Vector2 direction = (_playerTarget.position - transform.position).normalized;
            _rb.AddForce((Vector2.up + direction) * Randomizer.GetRandomizedInt(jumpForce), ForceMode2D.Impulse);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Hit");
            var player = collision.gameObject.GetComponent<PlayerStats>();
            if (player)
            {
                player.TakeDamage(attackDamage);
            }
        }
    }

    protected override void OnPlayerEnterArea()
    {
        currentState = EnemyState.Chase;
    }

    protected override void OnPlayerExitArea()
    {
        currentState = EnemyState.Patrol;
    }
}