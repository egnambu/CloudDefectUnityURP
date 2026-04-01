using UnityEngine;

/// <summary>
/// ========================================================================
/// PILOT PRACTICE CONTROLLER - State Machine + Enum Hybrid Approach
/// ========================================================================
/// 
/// This combines the BEST of both worlds:
/// 1. Interface-based State Machine (organized, scalable, encapsulated)
/// 2. Enum-based Animator Control (prevents boolean explosion)
/// 
/// FEATURES: Walk, Sprint, Jump, Fall, Land
/// 
/// ========================================================================
/// ARCHITECTURE OVERVIEW:
/// ========================================================================
/// 
/// IPracticeState (Interface)
///    ├── PracticeGroundedState   (enum = 0)
///    ├── PracticeJumpingState    (enum = 1)
///    ├── PracticeFallingState    (enum = 2)
///    └── PracticeLandingState    (enum = 3)
/// 
/// Each state class:
///   - Has Enter(), Exit(), Tick(), FixedTick() methods
///   - Owns its private variables (no pollution)
///   - Calls ChangeState() to transition
///   - The enum is set AUTOMATICALLY in ChangeState()
/// 
/// ========================================================================
/// </summary>

// =========================================================================
// ENUM FOR ANIMATOR - Prevents Boolean Explosion
// =========================================================================
// 
// The animator uses a SINGLE integer parameter instead of multiple bools.
// This enum maps directly to that integer.
// 
// Animator transitions check: LocomotionState == X
// Instead of: IsJumping && !IsFalling && !IsGrounded (bug-prone!)

public enum PracticeLocomotionState
{
    Grounded = 0,   // Walking, running, idle - on the ground
    Jumping = 1,    // Going UP after pressing jump
    Falling = 2,    // Going DOWN (gravity taking over)
    Landing = 3     // Brief state when hitting ground (plays landing animation)
}

// =========================================================================
// STATE INTERFACE - Each state implements this
// =========================================================================

public interface IPracticeState
{
    void Enter();      // Called once when entering this state
    void Exit();       // Called once when leaving this state
    void Tick();       // Called every Update() frame
    void FixedTick();  // Called every FixedUpdate() frame (physics)
}

/// <summary>
/// =========================================================================
/// THE MAIN CONTROLLER
/// =========================================================================
/// </summary>
public class PilotPracticeController : MonoBehaviour
{
    // =====================================================================
    // ANIMATOR PARAMETER HASHES (faster than strings)
    // =====================================================================
    
    private static readonly int HASH_LOCOMOTION_STATE = Animator.StringToHash("LocomotionState");
    private static readonly int HASH_MOVE_SPEED = Animator.StringToHash("MoveSpeed");
    
    // =====================================================================
    // INSPECTOR FIELDS
    // =====================================================================
    
    [Header("=== Components ===")]
    public Animator animator;
    public CharacterController controller;
    
    [Header("=== Movement Settings ===")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 8f;
    public float gravity = -20f;
    public float turnSpeed = 10f;
    public float airControlMultiplier = 0.5f;
    
    [Header("=== Ground Check ===")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    
    [Header("=== Landing Settings ===")]
    public float landingDuration = 0.3f;
    
    [Header("=== Debug (Read Only) ===")]
    [SerializeField] private string currentStateName;
    [SerializeField] private PracticeLocomotionState currentStateEnum;
    
    // =====================================================================
    // PUBLIC STATE (accessible by state classes)
    // =====================================================================
    
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool sprintHeld;
    [HideInInspector] public float smoothMoveSpeed;
    
    // =====================================================================
    // STATE MACHINE
    // =====================================================================
    
    public IPracticeState CurrentState { get; private set; }
    
    // Pre-instantiated states (allocated once, reused)
    public PracticeGroundedState GroundedState { get; private set; }
    public PracticeJumpingState JumpingState { get; private set; }
    public PracticeFallingState FallingState { get; private set; }
    public PracticeLandingState LandingState { get; private set; }
    
    // =====================================================================
    // INITIALIZATION
    // =====================================================================
    
    void Awake()
    {
        // Auto-find components
        if (controller == null) controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        
        // Create state instances (pass reference to this controller)
        GroundedState = new PracticeGroundedState(this);
        JumpingState = new PracticeJumpingState(this);
        FallingState = new PracticeFallingState(this);
        LandingState = new PracticeLandingState(this);
    }
    
    void Start()
    {
        // Begin in grounded state
        ChangeState(GroundedState, PracticeLocomotionState.Grounded);
        Debug.Log("[Practice] Controller initialized with State Machine approach.");
    }
    
    // =====================================================================
    // MAIN UPDATE LOOP - Clean and simple!
    // =====================================================================
    
    void Update()
    {
        // 1. Update ground status
        UpdateGroundedStatus();
        
        // 2. Read input
        ReadInput();
        
        // 3. Let current state handle logic
        CurrentState?.Tick();
        
        // 4. Update animator blend parameter
        animator.SetFloat(HASH_MOVE_SPEED, smoothMoveSpeed);
    }
    
    void FixedUpdate()
    {
        CurrentState?.FixedTick();
    }
    
    // =====================================================================
    // INPUT READING
    // =====================================================================
    
    private void ReadInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        moveInput = new Vector2(horizontal, vertical);
        jumpPressed = Input.GetButtonDown("Jump");
        sprintHeld = Input.GetKey(KeyCode.LeftShift);
    }
    
    // =====================================================================
    // GROUND CHECK
    // =====================================================================
    
    public void UpdateGroundedStatus()
    {
        isGrounded = Physics.CheckSphere(
            groundCheckPoint.position,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }
    
    // =====================================================================
    // STATE TRANSITION - The Key Method!
    // =====================================================================
    // 
    // This method:
    // 1. Calls Exit() on old state
    // 2. Sets the new state
    // 3. Updates the animator enum (single integer, no bool explosion!)
    // 4. Calls Enter() on new state
    //
    // The enum parameter ensures animator stays in sync automatically.
    
    public void ChangeState(IPracticeState newState, PracticeLocomotionState stateEnum)
    {
        if (newState == null) return;
        
        // Exit old state
        CurrentState?.Exit();
        
        // Set new state
        CurrentState = newState;
        currentStateEnum = stateEnum;
        currentStateName = newState.GetType().Name;
        
        // Update animator with enum (single integer - clean!)
        animator.SetInteger(HASH_LOCOMOTION_STATE, (int)stateEnum);
        
        // Enter new state
        CurrentState.Enter();
        
        Debug.Log($"[Practice] State Changed -> {currentStateName}");
    }
    
    // =====================================================================
    // HELPER METHODS (used by state classes)
    // =====================================================================
    
    /// <summary>
    /// Returns camera-relative movement direction
    /// </summary>
    public Vector3 GetCameraRelativeMovement()
    {
        Vector3 moveDir = new Vector3(moveInput.x, 0f, moveInput.y);
        
        if (Camera.main != null)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
            moveDir = camForward * moveInput.y + camRight * moveInput.x;
        }
        
        return moveDir;
    }
    
    /// <summary>
    /// Applies gravity to velocity
    /// </summary>
    public void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }
    
    /// <summary>
    /// Moves the character controller
    /// </summary>
    public void MoveCharacter()
    {
        controller.Move(velocity * Time.deltaTime);
    }
    
    // =====================================================================
    // GIZMOS
    // =====================================================================
    
    void OnDrawGizmos()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}

// =========================================================================
// =========================================================================
//
// STATE CLASSES - Each state is self-contained
//
// =========================================================================
// =========================================================================

/// <summary>
/// GROUNDED STATE: Walking, running, or standing still
/// </summary>
public class PracticeGroundedState : IPracticeState
{
    private readonly PilotPracticeController ctrl;
    
    public PracticeGroundedState(PilotPracticeController controller)
    {
        ctrl = controller;
    }
    
    public void Enter()
    {
        // Reset vertical velocity when landing
        ctrl.velocity.y = -2f;
    }
    
    public void Exit()
    {
        // Nothing special needed
    }
    
    public void Tick()
    {
        // Keep grounded
        if (ctrl.velocity.y < 0)
        {
            ctrl.velocity.y = -2f;
        }
        
        // Calculate movement
        Vector3 moveDir = ctrl.GetCameraRelativeMovement();
        float speed = ctrl.sprintHeld ? ctrl.sprintSpeed : ctrl.walkSpeed;
        ctrl.velocity.x = moveDir.x * speed;
        ctrl.velocity.z = moveDir.z * speed;
        
        // Rotate to face movement
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ctrl.transform.rotation = Quaternion.Slerp(
                ctrl.transform.rotation, 
                targetRotation, 
                Time.deltaTime * ctrl.turnSpeed
            );
        }
        
        // Smooth animation blend
        float targetSpeed = ctrl.moveInput.magnitude * (ctrl.sprintHeld ? 1.5f : 1f);
        ctrl.smoothMoveSpeed = Mathf.Lerp(ctrl.smoothMoveSpeed, targetSpeed, Time.deltaTime * 10f);
        
        // Move character
        ctrl.MoveCharacter();
        
        // --- TRANSITIONS ---
        
        // Jump
        if (ctrl.jumpPressed && ctrl.isGrounded)
        {
            ctrl.velocity.y = ctrl.jumpForce;
            ctrl.ChangeState(ctrl.JumpingState, PracticeLocomotionState.Jumping);
            return;
        }
        
        // Walked off edge
        if (!ctrl.isGrounded)
        {
            ctrl.ChangeState(ctrl.FallingState, PracticeLocomotionState.Falling);
            return;
        }
    }
    
    public void FixedTick()
    {
        // Physics handled in Tick for this simple case
    }
}

/// <summary>
/// JUMPING STATE: Character is moving upward
/// </summary>
public class PracticeJumpingState : IPracticeState
{
    private readonly PilotPracticeController ctrl;
    
    public PracticeJumpingState(PilotPracticeController controller)
    {
        ctrl = controller;
    }
    
    public void Enter()
    {
        // Jump force already applied before entering
        ctrl.smoothMoveSpeed = 0f; // Reset for air animation
    }
    
    public void Exit()
    {
        // Nothing special
    }
    
    public void Tick()
    {
        // Apply gravity
        ctrl.ApplyGravity();
        
        // Air control (reduced)
        Vector3 airMove = ctrl.GetCameraRelativeMovement() * ctrl.walkSpeed * ctrl.airControlMultiplier;
        ctrl.velocity.x = airMove.x;
        ctrl.velocity.z = airMove.z;
        
        // Move character
        ctrl.MoveCharacter();
        
        // --- TRANSITION: Start falling ---
        if (ctrl.velocity.y <= 0f)
        {
            ctrl.ChangeState(ctrl.FallingState, PracticeLocomotionState.Falling);
            return;
        }
    }
    
    public void FixedTick()
    {
        // Physics handled in Tick
    }
}

/// <summary>
/// FALLING STATE: Character is moving downward
/// </summary>
public class PracticeFallingState : IPracticeState
{
    private readonly PilotPracticeController ctrl;
    
    public PracticeFallingState(PilotPracticeController controller)
    {
        ctrl = controller;
    }
    
    public void Enter()
    {
        // Already falling, nothing special
    }
    
    public void Exit()
    {
        // Nothing special
    }
    
    public void Tick()
    {
        // Apply gravity
        ctrl.ApplyGravity();
        
        // Air control (reduced)
        Vector3 airMove = ctrl.GetCameraRelativeMovement() * ctrl.walkSpeed * ctrl.airControlMultiplier;
        ctrl.velocity.x = airMove.x;
        ctrl.velocity.z = airMove.z;
        
        // Move character
        ctrl.MoveCharacter();
        
        // --- TRANSITION: Hit ground ---
        if (ctrl.isGrounded)
        {
            ctrl.ChangeState(ctrl.LandingState, PracticeLocomotionState.Landing);
            return;
        }
    }
    
    public void FixedTick()
    {
        // Physics handled in Tick
    }
}

/// <summary>
/// LANDING STATE: Brief recovery when hitting ground
/// </summary>
public class PracticeLandingState : IPracticeState
{
    private readonly PilotPracticeController ctrl;
    private float landingTimer;  // Private to this state - no variable pollution!
    
    public PracticeLandingState(PilotPracticeController controller)
    {
        ctrl = controller;
    }
    
    public void Enter()
    {
        // Initialize timer on entry
        landingTimer = ctrl.landingDuration;
        
        // Stop horizontal movement
        ctrl.velocity.x = 0f;
        ctrl.velocity.z = 0f;
        ctrl.velocity.y = -2f;
        
        ctrl.smoothMoveSpeed = 0f;
    }
    
    public void Exit()
    {
        // Timer is private, no cleanup needed
    }
    
    public void Tick()
    {
        // Keep grounded
        ctrl.velocity.y = -2f;
        ctrl.MoveCharacter();
        
        // Count down
        landingTimer -= Time.deltaTime;
        
        // --- TRANSITION: Landing complete ---
        if (landingTimer <= 0f)
        {
            ctrl.ChangeState(ctrl.GroundedState, PracticeLocomotionState.Grounded);
            return;
        }
    }
    
    public void FixedTick()
    {
        // Nothing needed
    }
}

// =========================================================================
// =========================================================================
//
// ANIMATOR CONTROLLER SETUP GUIDE (unchanged from before)
//
// =========================================================================
// =========================================================================
//
// 1. CREATE PARAMETERS:
//    - LocomotionState (Int) - default: 0
//    - MoveSpeed (Float) - default: 0
//
// 2. CREATE STATES:
//    - Grounded (Blend Tree with Idle/Walk/Run based on MoveSpeed)
//    - Jump (Jump animation clip)
//    - Fall (Falling animation clip)
//    - Land (Landing animation clip)
//
// 3. SET DEFAULT STATE:
//    - Right-click "Grounded" -> Set as Layer Default State
//
// 4. CREATE TRANSITIONS:
//
//    FROM Grounded:
//    ├── TO Jump:     LocomotionState == 1, Duration: 0.1, No Exit Time
//    └── TO Fall:     LocomotionState == 2, Duration: 0.1, No Exit Time
//
//    FROM Jump:
//    └── TO Fall:     LocomotionState == 2, Duration: 0.1, No Exit Time
//
//    FROM Fall:
//    └── TO Land:     LocomotionState == 3, Duration: 0.05, No Exit Time
//
//    FROM Land:
//    └── TO Grounded: LocomotionState == 0, Duration: 0.1, No Exit Time
//
// 5. IMPORTANT SETTINGS ON ALL STATES:
//    - Write Defaults: FALSE (uncheck this!)
//    - Can Transition To Self: FALSE
//
// =========================================================================
//
// WHY STATE MACHINE + ENUM IS THE BEST APPROACH:
//
// 1. ENUM prevents boolean explosion in animator
//    - One integer parameter instead of 4+ bools
//    - Impossible to be in two states at once
//
// 2. STATE MACHINE provides code organization
//    - Each state is its own class
//    - Private variables don't pollute other states
//    - Enter/Exit lifecycle is automatic
//    - Easy to add new states without touching existing code
//
// 3. HYBRID gives you both benefits!
//    - ChangeState() automatically syncs the enum
//    - States focus on logic, enum handles animator
//
// =========================================================================
