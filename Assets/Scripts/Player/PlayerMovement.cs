using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public PlayerData data;
    #region Variables

    private Rigidbody2D Rb { get; set; }
    private PlayerInput PlayerInput { get; set; }
    private bool IsFacingRight { get; set; }
    private bool IsJumping { get; set; }
    private bool IsWallJumping { get; set; }
    private bool IsSliding { get; set; }
    private float LastOnGroundTime { get; set; }
    private float LastOnWallTime { get; set; }
    private float LastOnWallRightTime { get; set; }
    private float LastOnWallLeftTime { get; set; }
    private float LastPressedJumpTime { get; set; }

    private bool _isJumpCut;
    private bool _isJumpFalling;
    private float _wallJumpStartTime;
    // 0 = none; 1 = last wall jump from right wall; -1 = from left wall.
    private int _lastWallJumpedFrom;
    private Vector2 _moveInput;
    private bool _currentOnWall;
    private SpriteRenderer _spriteRenderer; //Cached SpriteRenderer

    [Header("Checks")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.49f, 0.03f);
    [Space(5)]
    [SerializeField] private Transform frontWallCheckPoint;
    [SerializeField] private Transform backWallCheckPoint;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.5f, 1f);

    [Header("Layers & Tags")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Animator")]
    [SerializeField] private Animator playerAnimator;

    private static readonly int Jump1 = Animator.StringToHash("Jump");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int IsOnWall = Animator.StringToHash("isOnWall");
    private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsJumpHeld = Animator.StringToHash("isJumpHeld");

    #endregion

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        PlayerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        SetGravityScale(data.gravityScale);
        IsFacingRight = true;
        _lastWallJumpedFrom = 0; // no wall jump has occurred yet
        _spriteRenderer = transform.Find("Sprite").GetComponent<SpriteRenderer>(); //Get SpriteRenderer
        if (_spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer not found on child object named 'Sprite'!");
        }
    }

    private void Update()
    {
        #region TIMERS
        LastOnGroundTime -= Time.deltaTime;
        LastOnWallTime -= Time.deltaTime;
        LastOnWallRightTime -= Time.deltaTime;
        LastOnWallLeftTime -= Time.deltaTime;
        LastPressedJumpTime -= Time.deltaTime;
        #endregion

        #region INPUT HANDLER
        _moveInput.x = PlayerInput.MovementInput.x;
        _moveInput.y = PlayerInput.MovementInput.y;

        if (_moveInput.x != 0 && !IsWallJumping) // Only allow turning if not wall jumping
            CheckDirectionToFace(_moveInput.x > 0);

        if (PlayerInput.JumpInputDown)
        {
            OnJumpInput();
            playerAnimator.SetBool(IsJumpHeld, true);
        }

        if (PlayerInput.JumpInputUp)
        {
            OnJumpUpInput();
            playerAnimator.SetBool(IsJumpHeld, false);
        }
        #endregion

        #region COLLISION CHECKS

        // Ground Check
        if (Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0, groundLayer) && !IsJumping)
        {
            LastOnGroundTime = data.coyoteTime;
            playerAnimator.SetBool(IsGrounded, true);
            _lastWallJumpedFrom = 0; // reset wall jump history when grounded
        }
        else
        {
            playerAnimator.SetBool(IsGrounded, false);
        }

        // Wall Check using IsTouchingRightWall and IsTouchingLeftWall
        bool isOnRightWall = IsTouchingRightWall();
        if (isOnRightWall)
            LastOnWallRightTime = data.coyoteTime;

        bool isOnLeftWall = IsTouchingLeftWall();
        if (isOnLeftWall)
            LastOnWallLeftTime = data.coyoteTime;

        // Update animator based on CURRENT wall contact
        _currentOnWall = isOnRightWall || isOnLeftWall;
        playerAnimator.SetBool(IsOnWall, _currentOnWall);

        // Update coyote timers (for wall contact)
        LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);

        // Reset _lastWallJumpedFrom if touching a different wall than last jumped from.
        int currentWallSide = 0;
        if (IsTouchingRightWall())
            currentWallSide = 1;
        else if (IsTouchingLeftWall())
            currentWallSide = -1;
        if (currentWallSide != 0 && currentWallSide != _lastWallJumpedFrom)
        {
            _lastWallJumpedFrom = 0;
        }

        #endregion

        #region JUMP CHECKS

        playerAnimator.SetFloat(VerticalVelocity, Rb.linearVelocity.y);

        if (IsJumping && Rb.linearVelocity.y < 0)
        {
            IsJumping = false;
            if (!IsWallJumping)
                _isJumpFalling = true;
        }

        if (IsWallJumping && Time.time - _wallJumpStartTime > data.wallJumpTime)
        {
            IsWallJumping = false;
        }

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
        {
            _isJumpCut = false;
            _isJumpFalling = false;
        }

        // Normal Jump (if on ground)
        if (CanJump() && LastPressedJumpTime > 0)
        {
            IsJumping = true;
            IsWallJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            Jump();
            playerAnimator.SetTrigger(Jump1);
        }
        // Wall Jump (if in air and touching a wall that is not the same as the one you last jumped from)
        else if (CanWallJump() && LastPressedJumpTime > 0)
        {
            IsWallJumping = true;
            IsJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            _wallJumpStartTime = Time.time;

            int wallJumpDir = 0;
            if (IsTouchingRightWall()) // Check wall again right before jump
            {
                _lastWallJumpedFrom = 1;   // record that you jumped off the right wall
                wallJumpDir = -1;          // force jump left
            }
            else if (IsTouchingLeftWall()) // Check wall again right before jump
            {
                _lastWallJumpedFrom = -1;  // record that you jumped off the left wall
                wallJumpDir = 1;           // force jump right
            }

            WallJump(wallJumpDir);
            playerAnimator.SetTrigger(Jump1);
        }
        #endregion

        #region SLIDE CHECKS
        if (CanSlide() && ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
            IsSliding = true;
        else
            IsSliding = false;
        #endregion

        #region GRAVITY
        // Apply gravity modifications
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (Rb.linearVelocity.y < 0 && _moveInput.y < 0)
        {
            SetGravityScale(data.gravityScale * data.fastFallGravityMult);
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            SetGravityScale(data.gravityScale * data.jumpCutGravityMult);
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFastFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
        {
            SetGravityScale(data.gravityScale * data.jumpHangGravityMult);
        }
        else if (Rb.linearVelocity.y < 0)
        {
            SetGravityScale(data.gravityScale * data.fallGravityMult);
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFastFallSpeed));
        }
        else
        {
            SetGravityScale(data.gravityScale);
        }
        #endregion
    }

    private void FixedUpdate()
    {
        playerAnimator.SetFloat(Speed, Mathf.Abs(Rb.linearVelocity.x));

        // Run handling
        if (IsWallJumping)
            Run(data.wallJumpRunLerp);
        else
            Run(1);

        // Slide handling
        if (IsSliding)
            Slide();
    }

    #region INPUT CALLBACKS
    private void OnJumpInput()
    {
        LastPressedJumpTime = data.jumpInputBufferTime;
    }

    private void OnJumpUpInput()
    {
        if (CanJumpCut() || CanWallJumpCut())
            _isJumpCut = true;
    }
    #endregion

    #region GENERAL METHODS
    private void SetGravityScale(float scale)
    {
        Rb.gravityScale = scale;
    }
    #endregion

    #region RUN METHODS
    private void Run(float lerpAmount)
    {
        float targetSpeed;
        // Calculate target speed based on input.  If wall jumping, ignore horizontal input.
        if (IsWallJumping)
        {
            targetSpeed = 0;
        }
        else
        {
            targetSpeed = _moveInput.x * data.runMaxSpeed;
        }

        targetSpeed = Mathf.Lerp(Rb.linearVelocity.x, targetSpeed, lerpAmount);

        float accelRate;
        if (LastOnGroundTime > 0)
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? data.runAccelAmount : data.runDeccelAmount;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? data.runAccelAmount * data.accelInAir : data.runDeccelAmount * data.deccelInAir;

        if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
        {
            accelRate *= data.jumpHangAccelerationMult;
            targetSpeed *= data.jumpHangMaxSpeedMult;
        }

        if (data.doConserveMomentum && Mathf.Abs(Rb.linearVelocity.x) > Mathf.Abs(targetSpeed) &&
            Mathf.Approximately(Mathf.Sign(Rb.linearVelocity.x), Mathf.Sign(targetSpeed)) &&
            Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
        {
            accelRate = 0;
        }

        var speedDif = targetSpeed - Rb.linearVelocity.x;
        var movement = speedDif * accelRate;
        Rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
    }

    private void Turn()
    {
        IsFacingRight = !IsFacingRight;
        _spriteRenderer.flipX = !IsFacingRight; //Flip the sprite
    }
    #endregion

    #region JUMP METHODS
    private void Jump()
    {
        // Reset jump buffers for a ground jump
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;

        var force = data.jumpForce;
        if (Rb.linearVelocity.y < 0)
            force -= Rb.linearVelocity.y;

        Rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
    }

    private void WallJump(int dir)
    {
        // Reset jump buffers for a wall jump
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        LastOnWallRightTime = 0;
        LastOnWallLeftTime = 0;

        // Reset horizontal velocity to ensure the jump is always propelled away from the wall
        Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);

        // Build the jump force vector (the horizontal force is fixed by the wall jump force and direction)
        Vector2 jumpForce = new Vector2(data.wallJumpForce.x * dir, data.wallJumpForce.y);
        if (Rb.linearVelocity.y < 0)
            jumpForce.y -= Rb.linearVelocity.y;

        //APPLY FORCE BEFORE CHANGING DIRECTION
        Rb.AddForce(jumpForce, ForceMode2D.Impulse);
        CheckDirectionToFace(dir == 1);
    }
    #endregion

    #region OTHER MOVEMENT METHODS
    private void Slide()
    {
        var speedDif = data.slideSpeed - Rb.linearVelocity.y;
        var movement = speedDif * data.slideAccel;
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime),
                                             Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));
        Rb.AddForce(movement * Vector2.up);
    }
    #endregion

    #region CHECK METHODS
    private void CheckDirectionToFace(bool isMovingRight)
    {
        if (isMovingRight != IsFacingRight)
            Turn();
    }

    private bool CanJump()
    {
        return LastOnGroundTime > 0 && !IsJumping;
    }

    // Here we compute the current wall side from the timers and ensure it differs from _lastWallJumpedFrom.
    private bool CanWallJump()
    {
        int currentWallSide = 0;
        if (LastOnWallRightTime > 0)
            currentWallSide = 1;
        else if (LastOnWallLeftTime > 0)
            currentWallSide = -1;

        return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 &&
               currentWallSide != 0 && currentWallSide != _lastWallJumpedFrom;
    }

    private bool CanJumpCut()
    {
        return IsJumping && Rb.linearVelocity.y > 0;
    }

    private bool CanWallJumpCut()
    {
        return IsWallJumping && Rb.linearVelocity.y > 0;
    }

    private bool CanSlide()
    {
        return _currentOnWall && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0;
    }

   private bool IsTouchingRightWall()
    {
            return Physics2D.OverlapBox(frontWallCheckPoint.position, wallCheckSize, 0, groundLayer);
    }

    private bool IsTouchingLeftWall()
    {
            return Physics2D.OverlapBox(backWallCheckPoint.position, wallCheckSize, 0, groundLayer);
    }

    #endregion

    #region EDITOR METHODS
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(frontWallCheckPoint.position, wallCheckSize);
        Gizmos.DrawWireCube(backWallCheckPoint.position, wallCheckSize);
    }
    #endregion
}