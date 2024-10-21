using UnityEngine;

public class Slime : Enemy
{
    public float jumpForce = 5f;
    public float jumpInterval = 2f; 
    public int attackDamage = 10;
    private Transform playerTarget;
    private Rigidbody2D rb;
    private float nextJumpTime;

    public override void Start()
    {
        base.Start();
        playerTarget = GameObject.FindGameObjectWithTag("Player").transform;
        rb = GetComponent<Rigidbody2D>(); 
        nextJumpTime = Time.time; 
    }

    protected override void HandleChaseState()
    {
        if (Time.time > nextJumpTime)
        {
            JumpTowardsPlayer();
            nextJumpTime = Time.time + Randomizer.GetRandomizedInt(jumpInterval);
        }
    }

    private void JumpTowardsPlayer()
    {
        if (playerTarget)
        {
            Vector2 _direction = (playerTarget.position - transform.position).normalized;
            rb.AddForce((Vector2.up + _direction) * Randomizer.GetRandomizedInt(jumpForce), ForceMode2D.Impulse);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Hit");
            var _player = collision.gameObject.GetComponent<PlayerStats>();
            if (_player)
            {
                _player.TakeDamage(attackDamage);
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