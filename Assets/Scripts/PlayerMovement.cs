using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [FormerlySerializedAs("groundCheck")]
    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck1;  
    [SerializeField] private Transform groundCheck2;  
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
        HandleMovement();
        HandleJump();
    }

    private void HandleMovement()
    {
        var _moveDirection = new Vector2(playerInput.MovementInput.x, 0);
        rb.linearVelocity = new Vector2(_moveDirection.x * moveSpeed, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        isGrounded = IsGrounded();

        
        if (playerInput.JumpInput && (isGrounded || jumpCount < maxJumps))
            Jump();
        
        if (isGrounded)
            jumpCount = 0;
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); 
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpCount++;
    }
    private bool IsGrounded()
    {
        var _hit1 = Physics2D.Raycast(groundCheck1.position, Vector2.down, groundCheckRadius, groundLayer);
        var _hit2 = Physics2D.Raycast(groundCheck2.position, Vector2.down, groundCheckRadius, groundLayer);
        
        return _hit1.collider || _hit2.collider  ;
    }
}