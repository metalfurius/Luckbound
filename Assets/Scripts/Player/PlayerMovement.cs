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
	private int _lastWallJumpDir;
	private Vector2 _moveInput;
	private bool _currentOnWall;

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
			CheckDirectionToFace(_moveInput.x > 0);

		if(PlayerInput.JumpInputDown)
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
		}
		else
		{
			playerAnimator.SetBool(IsGrounded, false);
		}

		// Right Wall Check
		var isOnRightWall = (Physics2D.OverlapBox(frontWallCheckPoint.position, wallCheckSize, 0, groundLayer) && IsFacingRight)
		                    || (Physics2D.OverlapBox(backWallCheckPoint.position, wallCheckSize, 0, groundLayer) && !IsFacingRight);
		if (isOnRightWall)
			LastOnWallRightTime = data.coyoteTime;

		// Left Wall Check
		var isOnLeftWall = (Physics2D.OverlapBox(frontWallCheckPoint.position, wallCheckSize, 0, groundLayer) && !IsFacingRight)
		                   || (Physics2D.OverlapBox(backWallCheckPoint.position, wallCheckSize, 0, groundLayer) && IsFacingRight);
		if (isOnLeftWall)
			LastOnWallLeftTime = data.coyoteTime;

		// Update animator based on CURRENT wall contact
		_currentOnWall = isOnRightWall || isOnLeftWall;
		playerAnimator.SetBool(IsOnWall, _currentOnWall); 

		// Update coyote timers
		LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);

		#endregion

		#region JUMP CHECKS
		
		playerAnimator.SetFloat(VerticalVelocity, Rb.linearVelocity.y);
		
		if (IsJumping && Rb.linearVelocity.y < 0)
		{
			IsJumping = false;

			if(!IsWallJumping)
				_isJumpFalling = true;
		}

		if (IsWallJumping && Time.time - _wallJumpStartTime > data.wallJumpTime)
		{
			IsWallJumping = false;
		}

		if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
        {
			_isJumpCut = false;

			if(!IsJumping)
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
			playerAnimator.SetTrigger(Jump1);
		}
		//WALL JUMP
		else if (CanWallJump() && LastPressedJumpTime > 0)
		{
			IsWallJumping = true;
			IsJumping = false;
			_isJumpCut = false;
			_isJumpFalling = false;
			_wallJumpStartTime = Time.time;
			
			if (isOnRightWall)
				_lastWallJumpDir = -1; // Jump left if on right wall
			else if (isOnLeftWall)
				_lastWallJumpDir = 1;  // Jump right if on left wall
			
			WallJump(_lastWallJumpDir);
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
			Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFastFallSpeed));
		}
		else if (_isJumpCut)
		{
			//Higher gravity if jump button released
			SetGravityScale(data.gravityScale * data.jumpCutGravityMult);
			Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Mathf.Max(Rb.linearVelocity.y, -data.maxFallSpeed));
		}
		else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
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
		playerAnimator.SetFloat(Speed, Mathf.Abs(Rb.linearVelocity.x));
		
		//Handle Run
		if (IsWallJumping)
			Run(data.wallJumpRunLerp);
		else
			Run(1);

		//Handle Slide
		if (IsSliding)
			Slide();
    }

    #region INPUT CALLBACKS
	//Methods which handle input detected in Update()
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
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? data.runAccelAmount : data.runDeccelAmount;
		else
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? data.runAccelAmount * data.accelInAir : data.runDeccelAmount * data.deccelInAir;
		#endregion

		#region Add Bonus Jump Apex Acceleration
		//Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
		if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(Rb.linearVelocity.y) < data.jumpHangTimeThreshold)
		{
			accelRate *= data.jumpHangAccelerationMult;
			targetSpeed *= data.jumpHangMaxSpeedMult;
		}
		#endregion

		#region Conserve Momentum
		//We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
		if(data.doConserveMomentum && Mathf.Abs(Rb.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Approximately(Mathf.Sign(Rb.linearVelocity.x), Mathf.Sign(targetSpeed)) && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
		{
			//Prevent any deceleration from happening, or in other words conserve are current momentum
			//You could experiment with allowing for the player to slightly increase their speed whilst in this "state"
			accelRate = 0; 
		}
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
		var spriteRenderer = playerAnimator.GetComponent<SpriteRenderer>();
		spriteRenderer.flipX = !spriteRenderer.flipX;

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

		Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);

		
		#region Perform Wall Jump
		var force = new Vector2(data.wallJumpForce.x, data.wallJumpForce.y);
		force.x *= dir; //apply force in opposite direction of wall

		if (!Mathf.Approximately(Mathf.Sign(Rb.linearVelocity.x), Mathf.Sign(force.x)))
			force.x -= Rb.linearVelocity.x;

		if (Rb.linearVelocity.y < 0) //checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity). This ensures the player always reaches our desired jump force or greater
			force.y -= Rb.linearVelocity.y;

		//Unlike in the run we want to use the Impulse mode.
		//The default mode will apply are force instantly ignoring mass
		CheckDirectionToFace(dir == 1);
		Rb.AddForce(force, ForceMode2D.Impulse);
		#endregion
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
		movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif)  * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

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

	private bool CanSlide()
	{
		// Replace LastOnWallTime > 0 with currentOnWall for instant sliding stop
		return _currentOnWall && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0;
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