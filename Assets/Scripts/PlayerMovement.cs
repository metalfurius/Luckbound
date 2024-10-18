using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck1;  
    [SerializeField] private Transform groundCheck2;  
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    
    [FormerlySerializedAs("wallTopRight")]
    [FormerlySerializedAs("wallCheck1")]
    [Header("Wall Check Settings")]
    [SerializeField] private Transform wallTopRightCheck;
    [FormerlySerializedAs("wallCheck2")] [SerializeField] private Transform wallTopLeftCheck;
    [FormerlySerializedAs("wallCheck3")] [SerializeField] private Transform wallBottomLeftCheck;
    [FormerlySerializedAs("wallCheck4")] [SerializeField] private Transform wallBottomRightCheck;
    [SerializeField] private float wallCheckDistance = 0.1f;
    
    [Header("Physics Components")]
    private Rigidbody2D rb;

    [Header("Input")]
    private PlayerInput playerInput;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumps = 2;
    private bool isGrounded;
    private bool isTouchingWall;
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
        isTouchingWall = IsTouchingWall();

        if (playerInput.JumpInput && (isGrounded || isTouchingWall || jumpCount < maxJumps))
            Jump();
        
        if (isGrounded || isTouchingWall)
            jumpCount = 0;
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); 
        
        if (isTouchingWall && !isGrounded)
        {
            var _wallJumpDirection = transform.localScale.x;
            rb.AddForce(new Vector2(-_wallJumpDirection * jumpForce * 0.5f, jumpForce), ForceMode2D.Impulse);
        }
        else
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        
        jumpCount++;
    }

    private bool IsGrounded()
    {
        var _hit1 = Physics2D.Raycast(groundCheck1.position, Vector2.down, groundCheckRadius, groundLayer);
        var _hit2 = Physics2D.Raycast(groundCheck2.position, Vector2.down, groundCheckRadius, groundLayer);
        
        return _hit1.collider || _hit2.collider;
    }

    private bool IsTouchingWall()
    {
        var _hitTopRight = Physics2D.Raycast(wallTopRightCheck.position, Vector2.right, wallCheckDistance, groundLayer);
        var _hitTopLeft = Physics2D.Raycast(wallTopLeftCheck.position, Vector2.left, wallCheckDistance, groundLayer);
        var _hitBottomLeft = Physics2D.Raycast(wallBottomLeftCheck.position, Vector2.left, wallCheckDistance, groundLayer);
        var _hitBottomRight = Physics2D.Raycast(wallBottomRightCheck.position, Vector2.right, wallCheckDistance, groundLayer);
        
        return _hitTopRight.collider || _hitTopLeft.collider || _hitBottomLeft || _hitBottomRight;
    }
}