using System.Diagnostics.CodeAnalysis;
using UnityEngine;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class PlayerMovement : MonoBehaviour
{
    public PlayerData data;
    private BaseAnimator _animator;
    private PlayerStats _playerStats;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        PlayerInput = GetComponent<PlayerInput>();
        _animator = GetComponent<BaseAnimator>();
        _playerStats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        SetGravityScale(data.gravityScale);
        IsFacingRight = true;
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

        if (_moveInput.x != 0)
        {
            CheckDirectionToFace(_moveInput.x > 0);
            _animator.PlayAnimation(_playerStats.playerAnimations.GetAnimationByName("Run"));
        }else
        {
            _animator.PlayAnimation(_playerStats.playerAnimations.GetAnimationByName("Idle"));
        }

        if (PlayerInput.JumpInputDown) OnJumpInput();

        if (PlayerInput.JumpInputUp) OnJumpUpInput();

        #endregion

        #region COLLISION CHECKS

        if (!IsJumping)
        {
            //Ground Check
            if (Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0, groundLayer) &&
                !IsJumping) //checks if set box overlaps with ground
                LastOnGroundTime = data.coyoteTime; //if so sets the lastGrounded to coyoteTime

            //Right Wall Check
            if (((Physics2D.OverlapBox(frontWallCheckPoint.position, wallCheckSize, 0, groundLayer) && IsFacingRight)
                 || (Physics2D.OverlapBox(backWallCheckPoint.position, wallCheckSize, 0, groundLayer) &&
                     !IsFacingRight)) && !IsWallJumping)
                LastOnWallRightTime = data.coyoteTime;

            //Right Wall Check
            if (((Physics2D.OverlapBox(frontWallCheckPoint.position, wallCheckSize, 0, groundLayer) && !IsFacingRight)
                 || (Physics2D.OverlapBox(backWallCheckPoint.position, wallCheckSize, 0, groundLayer) &&
                     IsFacingRight)) && !IsWallJumping)
                LastOnWallLeftTime = data.coyoteTime;

            //Two checks needed for both left and right walls since whenever the play turns the wall checkPoints swap sides
            LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
        }

        #endregion

        #region JUMP CHECKS

        if (IsJumping && Rb.linearVelocity.y < 0)
        {
            IsJumping = false;

            if (!IsWallJumping)
                _isJumpFalling = true;
        }

        if (IsWallJumping && Time.time - _wallJumpStartTime > data.wallJumpTime) IsWallJumping = false;

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
        {
            _isJumpCut = false;

            if (!IsJumping)
                _isJumpFalling = false;
        }

        //Jump
        if (CanJump() && LastPressedJumpTime > 0)
        {
            IsJumping = true;
            IsWallJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            Jump();
        }
        //WALL JUMP
        else if (CanWallJump() && LastPressedJumpTime > 0)
        {
            IsWallJumping = true;
            IsJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            _wallJumpStartTime = Time.time;
            _lastWallJumpDir = LastOnWallRightTime > 0 ? -1 : 1;

            WallJump(_lastWallJumpDir);
        }

        #endregion

        #region SLIDE CHECKS

        if (CanSlide() &&
            ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
            IsSliding = true;
        else
            IsSliding = false;

        #endregion

        #region GRAVITY

        //Higher gravity if we've released the jump input or are falling
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (Rb.linearVelocity.y < 0 && _moveInput.y < 0)
        {
            //Much higher gravity if holding down
            SetGravityScale(data.gravityScale * data.fastFallGravityMult);
            //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
            Rb.linearVelocity =
                new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            //Higher gravity if jump button released
            SetGravityScale(data.gravityScale * data.jumpCutGravityMult);
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) &&
                 Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
        {
            SetGravityScale(data.gravityScale * data.jumpHangGravityMult);
        }
        else if (Rb.linearVelocity.y < 0)
        {
            //Higher gravity if falling
            SetGravityScale(data.gravityScale * data.fallGravityMult);
            //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFallSpeed));
        }
        else
        {
            //Default gravity if standing on a platform or moving upwards
            SetGravityScale(data.gravityScale);
        }

        #endregion
    }

    private void FixedUpdate()
    {
        //Handle Run
        if (IsWallJumping)
            Run(data.wallJumpRunLerp);
        else
            Run(1);

        //Handle Slide
        if (IsSliding)
            Slide();
    }


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

    #region GENERAL METHODS

    public void SetGravityScale(float scale)
    {
        Rb.gravityScale = scale;
    }

    #endregion

    #region OTHER MOVEMENT METHODS

    private void Slide()
    {
        //Works the same as the Run but only in the y-axis
        //THis seems to work fine, but maybe you'll find a better way to implement a slide into this system
        var speedDif = data.slideSpeed - Rb.linearVelocity.y;
        var movement = speedDif * data.slideAccel;
        //So, we clamp the movement here to prevent any over corrections (these aren't noticeable in the Run)
        //The force applied can't be greater than the (negative) speedDifference * by how many times a second FixedUpdate() is called. For more info research how force are applied to rigid bodies.
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime),
            Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

        Rb.AddForce(movement * Vector2.up);
    }

    #endregion

    #region Variables

    public Rigidbody2D Rb { get; private set; }
    public PlayerInput PlayerInput { get; private set; }
    public bool IsFacingRight { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsWallJumping { get; private set; }
    public bool IsSliding { get; private set; }
    public float LastOnGroundTime { get; private set; }
    public float LastOnWallTime { get; private set; }
    public float LastOnWallRightTime { get; private set; }
    public float LastOnWallLeftTime { get; private set; }
    public float LastPressedJumpTime { get; private set; }

    private bool _isJumpCut;
    private bool _isJumpFalling;
    private float _wallJumpStartTime;
    private int _lastWallJumpDir;
    private Vector2 _moveInput;

    [Header("Checks")] [SerializeField] private Transform groundCheckPoint;

    [SerializeField] private Vector2 groundCheckSize = new(0.49f, 0.03f);

    [Space(5)] [SerializeField] private Transform frontWallCheckPoint;

    [SerializeField] private Transform backWallCheckPoint;
    [SerializeField] private Vector2 wallCheckSize = new(0.5f, 1f);

    [Header("Layers & Tags")] [SerializeField]
    private LayerMask groundLayer;

    #endregion

    #region INPUT CALLBACKS

    //Methods which handle input detected in Update()
    public void OnJumpInput()
    {
        LastPressedJumpTime = data.jumpInputBufferTime;
    }

    public void OnJumpUpInput()
    {
        if (CanJumpCut() || CanWallJumpCut())
            _isJumpCut = true;
    }

    #endregion

    //MOVEMENT METHODS

    #region RUN METHODS

    private void Run(float lerpAmount)
    {
        //Calculate the direction we want to move in and our desired velocity
        var targetSpeed = _moveInput.x * data.runMaxSpeed;
        //We can reduce are control using Lerp() this smooths changes to are direction and speed
        targetSpeed = Mathf.Lerp(Rb.linearVelocity.x, targetSpeed, lerpAmount);

        #region Calculate AccelRate

        float accelRate;

        //Gets an acceleration value based on if we are accelerating (includes turning) 
        //or trying to decelerate (stop). As well as applying a multiplier if we're airborne.
        if (LastOnGroundTime > 0)
            accelRate = Mathf.Abs(targetSpeed) > 0.01f ? data.runAccelAmount : data.runDeccelAmount;
        else
            accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? data.runAccelAmount * data.accelInAir
                : data.runDeccelAmount * data.deccelInAir;

        #endregion

        #region Add Bonus Jump Apex Acceleration

        //Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
        if ((IsJumping || IsWallJumping || _isJumpFalling) &&
            Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
        {
            accelRate *= data.jumpHangAccelerationMult;
            targetSpeed *= data.jumpHangMaxSpeedMult;
        }

        #endregion

        #region Conserve Momentum

        //We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
        if (data.doConserveMomentum && Mathf.Abs(Rb.linearVelocity.x) > Mathf.Abs(targetSpeed) &&
            Mathf.Approximately(Mathf.Sign(Rb.linearVelocity.x), Mathf.Sign(targetSpeed)) &&
            Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
            //Prevent any deceleration from happening, or in other words conserve are current momentum
            //You could experiment with allowing for the player to slightly increase their speed whilst in this "state"
            accelRate = 0;

        #endregion

        //Calculate difference between current velocity and desired velocity
        var speedDif = targetSpeed - Rb.linearVelocity.x;
        //Calculate force along x-axis to apply to thr player

        var movement = speedDif * accelRate;

        //Convert this to a vector and apply to rigid body
        Rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

        /*
         * For those interested here is what AddForce() will do
         * RB.velocity = new Vector2(RB.velocity.x + (Time.fixedDeltaTime  * speedDif * accelRate) / RB.mass, RB.velocity.y);
         * Time.fixedDeltaTime is by default in Unity 0.02 seconds equal to 50 FixedUpdate() calls per second
         */
    }

    private void Turn()
    {
        //stores scale and flips the player along the x-axis, 
        var scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;

        IsFacingRight = !IsFacingRight;
    }

    #endregion

    #region JUMP METHODS

    private void Jump()
    {
        //Ensures we can't call Jump multiple times from one press
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;

        #region Perform Jump

        //We increase the force applied if we are falling
        //This means we'll always feel like we jump the same amount 
        //(setting the player's Y velocity to 0 beforehand will likely work the same, but I find this more elegant :D)
        var force = data.jumpForce;
        if (Rb.linearVelocity.y < 0)
            force -= Rb.linearVelocity.y;

        Rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);

        #endregion
    }

    private void WallJump(int dir)
    {
        //Ensures we can't call Wall Jump multiple times from one press
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        LastOnWallRightTime = 0;
        LastOnWallLeftTime = 0;

        #region Perform Wall Jump

        var force = new Vector2(data.wallJumpForce.x, data.wallJumpForce.y);
        force.x *= dir; //apply force in opposite direction of wall

        if (!Mathf.Approximately(Mathf.Sign(Rb.linearVelocity.x), Mathf.Sign(force.x)))
            force.x -= Rb.linearVelocity.x;

        if (Rb.linearVelocity.y <
            0) //checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity). This ensures the player always reaches our desired jump force or greater
            force.y -= Rb.linearVelocity.y;

        //Unlike in the run we want to use the Impulse mode.
        //The default mode will apply are force instantly ignoring mass
        Rb.AddForce(force, ForceMode2D.Impulse);

        #endregion
    }

    #endregion


    #region CHECK METHODS

    public void CheckDirectionToFace(bool isMovingRight)
    {
        if (isMovingRight != IsFacingRight)
            Turn();
    }

    private bool CanJump()
    {
        return LastOnGroundTime > 0 && !IsJumping;
    }

    private bool CanWallJump()
    {
        return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (!IsWallJumping ||
            (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
    }

    private bool CanJumpCut()
    {
        return IsJumping && Rb.linearVelocity.y > 0;
    }

    private bool CanWallJumpCut()
    {
        return IsWallJumping && Rb.linearVelocity.y > 0;
    }

    public bool CanSlide()
    {
        return LastOnWallTime > 0 && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0;
    }

    #endregion
}