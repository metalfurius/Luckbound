using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;  
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Physics Components")]
    private Rigidbody2D rb;

    [Header("Input")]
    private PlayerInput playerInput;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumps = 2;
    private bool isGrounded;
    private int jumpCount;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
    }
    private void Update()
    {
        var _moveDirection = new Vector2(playerInput.MovementInput.x, 0);
        rb.linearVelocity = new Vector2(_moveDirection.x * moveSpeed, rb.linearVelocity.y);
        isGrounded = IsGrounded();

        if (isGrounded)
            jumpCount = 0;
        
        if (playerInput.JumpInput && (isGrounded || jumpCount < maxJumps))
            Jump();
    }
    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); 
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpCount++;
    }
    private bool IsGrounded()
    {
        var _hit = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckRadius, groundLayer);
        return _hit.collider;
    }
}