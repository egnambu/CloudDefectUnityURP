using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Locomotion state enum - replaces multiple boolean parameters.
/// Only ONE state can be active at a time, eliminating race conditions.
/// </summary>
public enum LocomotionStateType
{
    Grounded = 0,
    Jumping = 1,
    Falling = 2,
    Hovering = 3,
    Flying = 4
}

/// <summary>
/// Landing type enum - determines which landing animation to play.
/// Set BEFORE transitioning to Grounded state.
/// </summary>
public enum LandingType
{
    None = 0,
    Light = 1,
    Heavy = 2
}

/// <summary>
/// PilotTypeController V2 - Refactored to use enum-based animator state management.
/// 
/// Key improvements over V1:
/// - Single LocomotionState integer replaces 5 boolean parameters
/// - No more race conditions between animator transitions
/// - Explicit state transitions prevent "idle in air" bugs
/// - Landing type set before state change for proper transition priority
/// </summary>
public class PilotTypeControllerV2 : MonoBehaviour
{
    #region Debug
    [Header("Debug (Read Only)")]
    [SerializeField] private string currentStateName;
    [SerializeField] private LocomotionStateType currentLocomotionState;
    public IPilotStateV2 currentState;
    public Vector3 velocity;
    public bool isGrounded;
    #endregion

    #region Timing
    [SerializeField] private float jumpBufferTime = 0.1f;
    private float jumpBufferCounter = 0f;
    public float landingCooldown;
    [HideInInspector] public float airborneGraceTime;
    #endregion

    #region Input Settings
    [Header("Input Settings")]
    public bool isUsingGamepad;
    public float gamepadLookSensitivity = 1000f;
    public float mouseLookSensitivity = 0.1f;
    [Range(0f, 0.2f)] public float stickDeadzone = 0.1f;
    #endregion

    #region Core Components
    [Header("Core Components")]
    public CharacterController controller;
    public Transform cam;
    public Animator animator;
    #endregion

    #region Cinemachine
    [Header("Cinemachine Cameras")]
    public CinemachineCamera followCam;
    public CinemachineCamera aimCam;
    public CinemachineCamera flightCam;
    [Tooltip("Optional: Dedicated camera for AimFlight state. If not set, uses flightCam.")]
    public CinemachineCamera aimFlightCam;
    #endregion

    #region Movement Settings
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 10f;
    public float turnSpeed = 720f;
    public float aimRotationSpeed = 10f;
    public float gravity = -20f;
    #endregion

    #region Ground Check
    [Header("Ground Check")]
    public LayerMask groundLayerMask;
    public float groundCheckRadius = 0.3f;
    public Transform groundCheckPoint;
    #endregion

    #region Animator Settings
    [Header("Animator Settings")]
    public float animatorLerpRate = 12f;
    public float aimBlendSpeed = 12f;
    public int aimLayerIndex = 1;
    
    // Animator parameter hashes for performance
    private static readonly int HASH_LOCOMOTION_STATE = Animator.StringToHash("LocomotionState");
    private static readonly int HASH_LANDING_TYPE = Animator.StringToHash("LandingType");
    private static readonly int HASH_IS_AIMING = Animator.StringToHash("IsAiming");
    private static readonly int HASH_MOVE_X = Animator.StringToHash("MoveX");
    private static readonly int HASH_MOVE_Y = Animator.StringToHash("MoveY");
    private static readonly int HASH_AIM_X = Animator.StringToHash("AimX");
    private static readonly int HASH_AIM_Y = Animator.StringToHash("AimY");
    private static readonly int HASH_HOVER_X = Animator.StringToHash("HoverX");
    private static readonly int HASH_HOVER_Y = Animator.StringToHash("HoverY");
    private static readonly int HASH_FLIGHT_X = Animator.StringToHash("FlightX");
    private static readonly int HASH_FLIGHT_Y = Animator.StringToHash("FlightY");
    #endregion

    #region Hover Settings
    [Header("Hover Settings")]
    public float hoverMaxDuration = 50f;
    public float hoverVelocityDecayRate = 35f;
    public float hoverBobAmplitude = 0.3f;
    public float hoverBobFrequency = 1.5f;
    public float hoverMoveSpeed = 4f;
    public float hoverVerticalSpeed = 3f;
    [HideInInspector] public float hoverTimeRemaining;
    #endregion

    #region Flight Settings
    [Header("Flight Settings")]
    public float flightSpeed = 54f;
    public float flightAcceleration = 12f;
    public float pitchSpeed = 280f;
    public float yawSpeed = 280f;
    public float flightMaxDuration = 120f;
    public float flightControllerHeight = 0.2f;
    public Transform controllerPoint;
    public Transform meshRoot;
    [HideInInspector] public float flightTimeRemaining;

    [Header("Flight Control Options")]
    public bool invertFlightPitch = true;
    public float keyboardFlightRampUpSpeed = 2.5f;
    public float keyboardFlightRampDownSpeed = 4f;
    #endregion

    #region Input State
    [HideInInspector] public Pilot1 input;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public Vector2 lookInput;
    [HideInInspector] public bool aimHeld;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool sprintPressed;
    [HideInInspector] public bool launchHeld;
    [HideInInspector] public bool hoverHeld;
    [HideInInspector] public float launchTriggerValue;
    [HideInInspector] public float hoverTriggerValue;
    #endregion

    #region Smoothed Values
    [HideInInspector] public float smoothMoveX;
    [HideInInspector] public float smoothMoveY;
    [HideInInspector] public float defaultHeight;
    [HideInInspector] public Vector3 defaultCenter;
    #endregion

    #region State Instances
    public FreeWalkStateV2 FreeWalkState { get; private set; }
    public AimWalkStateV2 AimWalkState { get; private set; }
    public JumpPilotStateV2 JumpPilotState { get; private set; }
    public FallPilotStateV2 FallPilotState { get; private set; }
    public HoverPilotStateV2 HoverPilotState { get; private set; }
    public FlightPilotStateV2 FlightPilotState { get; private set; }
    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
        animator = GetComponent<Animator>();
        input = new Pilot1();

        // Initialize state instances
        FreeWalkState = new FreeWalkStateV2(this);
        AimWalkState = new AimWalkStateV2(this);
        JumpPilotState = new JumpPilotStateV2(this);
        FallPilotState = new FallPilotStateV2(this);
        HoverPilotState = new HoverPilotStateV2(this);
        FlightPilotState = new FlightPilotStateV2(this);
    }

    void OnEnable() => input.Enable();

    void OnDisable()
    {
        input.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        input.Dispose();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultHeight = controller.height;
        defaultCenter = controller.center;
        hoverTimeRemaining = hoverMaxDuration;
        flightTimeRemaining = flightMaxDuration;

        if (animator != null) animator.applyRootMotion = false;

        // Initialize animator state
        SetLocomotionState(LocomotionStateType.Grounded);
        ClearLandingType();
        SetAiming(false);

        ChangeState(FreeWalkState);
    }

    void Update()
    {
        ReadInput();
        currentState?.Tick();

        if (landingCooldown > 0f)
        {
            landingCooldown -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        UpdateGroundedStatus();
        currentState?.FixedTick();
    }

    #endregion

    #region Input Reading

    void ReadInput()
    {
        moveInput = input.PlayerA.Move.ReadValue<Vector2>();
        lookInput = input.PlayerA.Look.ReadValue<Vector2>();
        aimHeld = input.PlayerA.Aim.IsPressed();
        jumpPressed = input.PlayerA.Jump.IsPressed();
        sprintPressed = input.PlayerA.Sprint.IsPressed();

        launchTriggerValue = input.PlayerA.Launch.ReadValue<float>();
        hoverTriggerValue = input.PlayerA.Hover.ReadValue<float>();

        // Detect gamepad usage
        var moveControl = input.PlayerA.Move.activeControl;
        var launchControl = input.PlayerA.Launch.activeControl;
        var hoverControl = input.PlayerA.Hover.activeControl;

        bool moveIsGamepad = moveControl != null && moveControl.device is UnityEngine.InputSystem.Gamepad;
        bool launchIsGamepad = launchControl != null && launchControl.device is UnityEngine.InputSystem.Gamepad;
        bool hoverIsGamepad = hoverControl != null && hoverControl.device is UnityEngine.InputSystem.Gamepad;

        isUsingGamepad = moveIsGamepad || launchIsGamepad || hoverIsGamepad;

        // Keyboard hover uses neutral 0.5 value
        if (!hoverIsGamepad && hoverTriggerValue > 0f)
        {
            hoverTriggerValue = 0.5f;
        }

        launchHeld = launchTriggerValue > 0.1f;
        hoverHeld = hoverTriggerValue > 0.1f;

        // Apply deadzone
        if (moveInput.magnitude < stickDeadzone)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            moveInput = moveInput.normalized * ((moveInput.magnitude - stickDeadzone) / (1f - stickDeadzone));
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// Changes the current gameplay state. Handles exit/enter callbacks.
    /// </summary>
    public void ChangeState(IPilotStateV2 newState)
    {
        if (newState == null) return;

        currentState?.Exit();
        currentState = newState;
        currentStateName = newState.GetType().Name;
        currentState.Enter();
    }

    #endregion

    #region Animator State Management

    /// <summary>
    /// Sets the locomotion state in the animator. This is the PRIMARY state control.
    /// Only call this when actually changing locomotion mode.
    /// </summary>
    public void SetLocomotionState(LocomotionStateType newState)
    {
        currentLocomotionState = newState;
        animator.SetInteger(HASH_LOCOMOTION_STATE, (int)newState);
        
        // Reset blend parameters when changing major states
        if (newState == LocomotionStateType.Grounded)
        {
            animator.SetFloat(HASH_HOVER_X, 0f);
            animator.SetFloat(HASH_HOVER_Y, 0f);
            animator.SetFloat(HASH_FLIGHT_X, 0f);
            animator.SetFloat(HASH_FLIGHT_Y, 0f);
        }
    }

    /// <summary>
    /// Gets the current locomotion state.
    /// </summary>
    public LocomotionStateType GetLocomotionState() => currentLocomotionState;

    /// <summary>
    /// Sets the landing type. Call this BEFORE setting LocomotionState to Grounded.
    /// The animator uses this to determine which landing animation to play.
    /// </summary>
    public void SetLandingType(LandingType type)
    {
        animator.SetInteger(HASH_LANDING_TYPE, (int)type);
    }

    /// <summary>
    /// Determines landing type based on fall velocity and sets it.
    /// Call this BEFORE transitioning to grounded state.
    /// </summary>
    public void SetLandingTypeFromVelocity(float fallVelocity)
    {
        float fallingSpeed = Mathf.Abs(fallVelocity);
        
        if (fallingSpeed > 10f)
        {
            SetLandingType(LandingType.Heavy);
            landingCooldown = 1.25f;
            Debug.Log($"[Landing] Heavy landing at velocity: {fallingSpeed:F1}");
        }
        else
        {
            SetLandingType(LandingType.Light);
            landingCooldown = 0.4f;
            Debug.Log($"[Landing] Light landing at velocity: {fallingSpeed:F1}");
        }
    }

    /// <summary>
    /// Clears the landing type. Call after landing animation completes.
    /// </summary>
    public void ClearLandingType()
    {
        animator.SetInteger(HASH_LANDING_TYPE, (int)LandingType.None);
    }

    /// <summary>
    /// Sets the aiming state. Controls the Aim layer blend weight.
    /// </summary>
    public void SetAiming(bool isAiming)
    {
        animator.SetBool(HASH_IS_AIMING, isAiming);
    }

    /// <summary>
    /// Sets ground locomotion blend parameters.
    /// </summary>
    public void SetMoveBlend(float x, float y)
    {
        animator.SetFloat(HASH_MOVE_X, x);
        animator.SetFloat(HASH_MOVE_Y, y);
    }

    /// <summary>
    /// Sets aim walk blend parameters.
    /// </summary>
    public void SetAimBlend(float x, float y)
    {
        animator.SetFloat(HASH_AIM_X, x);
        animator.SetFloat(HASH_AIM_Y, y);
    }

    /// <summary>
    /// Sets hover blend parameters.
    /// </summary>
    public void SetHoverBlend(float x, float y)
    {
        animator.SetFloat(HASH_HOVER_X, x);
        animator.SetFloat(HASH_HOVER_Y, y);
    }

    /// <summary>
    /// Sets flight blend parameters.
    /// </summary>
    public void SetFlightBlend(float x, float y)
    {
        animator.SetFloat(HASH_FLIGHT_X, x);
        animator.SetFloat(HASH_FLIGHT_Y, y);
    }

    #endregion

    #region Physics & Checks

    public void UpdateGroundedStatus()
    {
        isGrounded = Physics.CheckSphere(
            groundCheckPoint.position,
            groundCheckRadius,
            groundLayerMask,
            QueryTriggerInteraction.Ignore
        );
    }

    public void ApplyGravity()
    {
        if (isGrounded)
        {
            if (velocity.y < 0f)
            {
                velocity.y = -4f; // Small downward force to keep grounded
            }
        }
        else
        {
            velocity.y += gravity * Time.fixedDeltaTime;
        }
    }

    public void ApplyJumpForce()
    {
        velocity.y = 10f;
    }

    public Vector3 GetCameraRelativeMovement()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return camForward * inputDir.z + camRight * inputDir.x;
    }

    #endregion

    #region Camera

    public void SetCameraPriority(CinemachineCamera activeCamera)
    {
        followCam.Priority = 10;
        aimCam.Priority = 10;
        flightCam.Priority = 10;
        if (aimFlightCam != null) aimFlightCam.Priority = 10;
        activeCamera.Priority = 20;
    }

    #endregion

    #region State Transition Helpers

    /// <summary>
    /// Checks for grounded state transitions (jump, aim toggle).
    /// Call only from grounded states.
    /// </summary>
    public void CheckGroundedStateTransitions()
    {
        // Jump buffer
        if (jumpBufferCounter > 0 && isGrounded)
        {
            ChangeState(JumpPilotState);
            jumpBufferCounter = 0f;
            return;
        }

        // Aim toggle
        if (aimHeld)
        {
            if (currentState != AimWalkState)
                ChangeState(AimWalkState);
        }
        else
        {
            if (currentState != FreeWalkState)
                ChangeState(FreeWalkState);
        }

        // Update jump buffer
        if (jumpPressed)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;
    }

    /// <summary>
    /// Checks for aerial state transitions (hover, flight).
    /// Call from jump/fall states.
    /// </summary>
    public bool CheckAerialStateTransitions()
    {
        // Flight takes priority
        if (launchHeld && flightTimeRemaining > 0f)
        {
            ChangeState(FlightPilotState);
            return true;
        }

        // Hover
        if (hoverHeld && hoverTimeRemaining > 0f)
        {
            ChangeState(HoverPilotState);
            return true;
        }

        return false;
    }

    #endregion

    #region Gizmos

    void OnDrawGizmos()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }

    #endregion
}

#region State Interface

public interface IPilotStateV2
{
    void Enter();
    void Exit();
    void Tick();
    void FixedTick();
}

#endregion

#region Grounded States

public class FreeWalkStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;
    private float forwardSpeed;
    private bool isSprinting;

    public FreeWalkStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        // Set locomotion state - this drives animator transitions
        ptC.SetLocomotionState(LocomotionStateType.Grounded);
        ptC.SetAiming(false);

        // Restore collider
        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.followCam);
        Debug.Log("[State] Entered: FreeWalkStateV2");
    }

    public void Tick()
    {
        // Transition to fall if not grounded
        if (!ptC.isGrounded && ptC.airborneGraceTime <= 0f)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // During landing recovery - minimal updates
        if (ptC.landingCooldown > 0f)
        {
            // Smooth to idle
            ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.SetMoveBlend(ptC.smoothMoveX, ptC.smoothMoveY);

            // Only gravity movement
            ptC.controller.Move(ptC.velocity * Time.deltaTime);
            return;
        }

        // Clear landing type after recovery
        ptC.ClearLandingType();

        ptC.CheckGroundedStateTransitions();

        Vector3 moveDir = ptC.GetCameraRelativeMovement();

        // Sprint check
        isSprinting = ptC.sprintPressed && ptC.isGrounded && ptC.moveInput.magnitude > 0.01f;

        // Rotation
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ptC.transform.rotation = Quaternion.RotateTowards(
                ptC.transform.rotation,
                targetRotation,
                ptC.turnSpeed * Time.deltaTime
            );
        }

        // Animation blend values
        forwardSpeed = isSprinting ? 1.5f : ptC.moveInput.magnitude;

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, forwardSpeed, Time.deltaTime * ptC.animatorLerpRate);
        ptC.SetMoveBlend(ptC.smoothMoveX, ptC.smoothMoveY);

        // Movement
        float currentSpeed = isSprinting ? ptC.sprintSpeed : ptC.moveSpeed;
        Vector3 horizontalMove = moveDir * currentSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);
    }

    public void FixedTick()
    {
        ptC.ApplyGravity();

        // Regenerate hover/flight time while grounded
        ptC.hoverTimeRemaining = Mathf.MoveTowards(ptC.hoverTimeRemaining, ptC.hoverMaxDuration, Time.fixedDeltaTime * 5f);
        ptC.flightTimeRemaining = Mathf.MoveTowards(ptC.flightTimeRemaining, ptC.flightMaxDuration, Time.fixedDeltaTime * 5f);
    }

    public void Exit()
    {
        isSprinting = false;
    }
}

public class AimWalkStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;

    public AimWalkStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        ptC.SetLocomotionState(LocomotionStateType.Grounded);
        ptC.SetAiming(true);

        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.aimCam);
        Debug.Log("[State] Entered: AimWalkStateV2");
    }

    public void Tick()
    {
        // Transition to fall if not grounded
        if (!ptC.isGrounded && ptC.airborneGraceTime <= 0f)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // Face camera direction
        Vector3 camForward = ptC.cam.forward;
        camForward.y = 0f;

        if (ptC.landingCooldown > 0f)
        {
            if (camForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);
                ptC.transform.rotation = Quaternion.Slerp(
                    ptC.transform.rotation,
                    targetRotation,
                    Time.deltaTime * ptC.aimRotationSpeed
                );
            }

            // Smooth to idle
            ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.SetAimBlend(ptC.smoothMoveX, ptC.smoothMoveY);

            ptC.controller.Move(ptC.velocity * Time.deltaTime);
            return;
        }

        ptC.ClearLandingType();
        ptC.CheckGroundedStateTransitions();

        // Rotation towards camera
        if (camForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);
            ptC.transform.rotation = Quaternion.Slerp(
                ptC.transform.rotation,
                targetRotation,
                Time.deltaTime * ptC.aimRotationSpeed
            );
        }

        // Local-space movement for strafing animation
        Vector3 inputDir = new Vector3(ptC.moveInput.x, 0f, ptC.moveInput.y);
        Vector3 worldDir = ptC.cam.forward * inputDir.z + ptC.cam.right * inputDir.x;
        Vector3 localDir = ptC.transform.InverseTransformDirection(worldDir);

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, localDir.x, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, localDir.z, Time.deltaTime * ptC.animatorLerpRate);
        ptC.SetAimBlend(ptC.smoothMoveX, ptC.smoothMoveY);

        // Movement
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);
    }

    public void FixedTick()
    {
        ptC.ApplyGravity();

        ptC.hoverTimeRemaining = Mathf.MoveTowards(ptC.hoverTimeRemaining, ptC.hoverMaxDuration, Time.fixedDeltaTime * 5f);
        ptC.flightTimeRemaining = Mathf.MoveTowards(ptC.flightTimeRemaining, ptC.flightMaxDuration, Time.fixedDeltaTime * 5f);
    }

    public void Exit()
    {
        ptC.SetAiming(false);
    }
}

#endregion

#region Aerial States

public class JumpPilotStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;
    private bool hasAppliedJumpForce;

    public JumpPilotStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        hasAppliedJumpForce = false;
        ptC.airborneGraceTime = 0.2f;

        ptC.SetLocomotionState(LocomotionStateType.Jumping);
        ptC.ClearLandingType();

        Debug.Log("[State] Entered: JumpPilotStateV2");
    }

    public void Tick()
    {
        if (ptC.airborneGraceTime > 0f)
        {
            ptC.airborneGraceTime -= Time.deltaTime;
        }

        // Apply jump force once
        if (!hasAppliedJumpForce)
        {
            ptC.ApplyJumpForce();
            hasAppliedJumpForce = true;
        }

        // Rotation
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ptC.transform.rotation = Quaternion.RotateTowards(
                ptC.transform.rotation,
                targetRotation,
                ptC.turnSpeed * 0.5f * Time.deltaTime
            );
        }

        // Movement
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * 0.5f * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);

        // Check for aerial transitions
        if (ptC.CheckAerialStateTransitions())
            return;

        // Transition to fall when descending
        if (ptC.velocity.y <= 0f)
        {
            ptC.ChangeState(ptC.FallPilotState);
        }
    }

    public void FixedTick()
    {
        ptC.velocity.y += ptC.gravity * Time.fixedDeltaTime;
    }

    public void Exit()
    {
    }
}

public class FallPilotStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;
    private float fallTime;
    private const float LANDING_CHECK_DISTANCE = 1.5f;
    private const float LAND_ROTATE_SPEED = 8f;
    private bool isLanding;

    public FallPilotStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        fallTime = 0f;
        isLanding = false;

        ptC.SetLocomotionState(LocomotionStateType.Falling);

        ptC.SetCameraPriority(ptC.followCam);

        if (ptC.airborneGraceTime <= 0f)
        {
            ptC.airborneGraceTime = 0.1f;
        }

        Debug.Log("[State] Entered: FallPilotStateV2");
    }

    public void Tick()
    {
        fallTime += Time.deltaTime;

        if (ptC.airborneGraceTime > 0f)
        {
            ptC.airborneGraceTime -= Time.deltaTime;
        }

        // Rotation
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        if (moveDir.sqrMagnitude > 0.01f && !isLanding)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ptC.transform.rotation = Quaternion.RotateTowards(
                ptC.transform.rotation,
                targetRotation,
                ptC.turnSpeed * 0.5f * Time.deltaTime
            );
        }

        // Movement
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * 0.5f * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);

        // Check for aerial transitions
        if (ptC.CheckAerialStateTransitions())
            return;

        // Check for landing
        CheckForLanding();
    }

    public void FixedTick()
    {
        ptC.velocity.y += ptC.gravity * Time.fixedDeltaTime;
    }

    public void Exit()
    {
        isLanding = false;
    }

    private void CheckForLanding()
    {
        if (ptC.airborneGraceTime > 0f) return;

        if (isLanding) return;

        // Raycast check for approaching ground
        if (Physics.Raycast(ptC.controller.transform.position, Vector3.down, out RaycastHit hit, LANDING_CHECK_DISTANCE, ptC.groundLayerMask))
        {
            Debug.DrawRay(ptC.controller.transform.position, Vector3.down * LANDING_CHECK_DISTANCE, Color.cyan);

            // Calculate upright rotation
            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;

            if (projectedForward.sqrMagnitude < 0.01f)
            {
                projectedForward = ptC.transform.right;
                projectedForward.y = 0f;
            }
            projectedForward.Normalize();

            Quaternion uprightRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            ptC.transform.rotation = Quaternion.Slerp(ptC.transform.rotation, uprightRotation, Time.deltaTime * LAND_ROTATE_SPEED);

            float angleFromUpright = Quaternion.Angle(ptC.transform.rotation, uprightRotation);
            bool isNearlyUpright = angleFromUpright < 15f;
            bool closeToGround = hit.distance < 0.8f;

            if (closeToGround && isNearlyUpright && ptC.velocity.y <= 0f)
            {
                isLanding = true;

                // Set landing type BEFORE changing locomotion state
                ptC.SetLandingTypeFromVelocity(ptC.velocity.y);
                ptC.SetLocomotionState(LocomotionStateType.Grounded);

                if (ptC.aimHeld)
                    ptC.ChangeState(ptC.AimWalkState);
                else
                    ptC.ChangeState(ptC.FreeWalkState);
            }
        }

        // Backup ground check
        if (ptC.isGrounded && ptC.velocity.y <= 0f && !isLanding)
        {
            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;
            if (projectedForward.sqrMagnitude > 0.01f)
            {
                Quaternion uprightRotation = Quaternion.LookRotation(projectedForward.normalized, Vector3.up);
                ptC.transform.rotation = uprightRotation;
            }

            isLanding = true;

            ptC.SetLandingTypeFromVelocity(ptC.velocity.y);
            ptC.SetLocomotionState(LocomotionStateType.Grounded);

            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
    }
}

public class HoverPilotStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;
    private float hoverBobTimer;
    private float baseHoverHeight;
    private bool hasStabilized;
    private float stabilizeGraceTime;
    private float currentAimLayerWeight;

    public HoverPilotStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        hasStabilized = false;
        stabilizeGraceTime = 0.3f;
        hoverBobTimer = 0f;
        baseHoverHeight = ptC.transform.position.y;
        currentAimLayerWeight = 0f;

        ptC.SetLocomotionState(LocomotionStateType.Hovering);

        // Restore collider
        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.followCam);
        Debug.Log("[State] Entered: HoverPilotStateV2");
    }

    public void Tick()
    {
        ptC.hoverTimeRemaining -= Time.deltaTime;

        if (stabilizeGraceTime > 0f)
        {
            stabilizeGraceTime -= Time.deltaTime;
        }

        // Face camera direction
        Vector3 camForward = ptC.cam.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);
            ptC.transform.rotation = Quaternion.Slerp(
                ptC.transform.rotation,
                targetRotation,
                Time.deltaTime * ptC.aimRotationSpeed
            );
        }

        // Aim layer blending
        float targetAimWeight = ptC.aimHeld ? 1f : 0f;
        currentAimLayerWeight = Mathf.MoveTowards(currentAimLayerWeight, targetAimWeight, Time.deltaTime * ptC.aimBlendSpeed);
        ptC.animator.SetLayerWeight(ptC.aimLayerIndex, currentAimLayerWeight);
        ptC.SetAiming(ptC.aimHeld);

        // Hover blend based on input
        Vector3 localDir = ptC.transform.InverseTransformDirection(ptC.GetCameraRelativeMovement());
        float smoothX = Mathf.Lerp(ptC.animator.GetFloat("HoverX"), localDir.x, Time.deltaTime * ptC.animatorLerpRate);
        float smoothY = Mathf.Lerp(ptC.animator.GetFloat("HoverY"), localDir.z, Time.deltaTime * ptC.animatorLerpRate);
        ptC.SetHoverBlend(smoothX, smoothY);

        // Horizontal movement
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        Vector3 horizontalMove = moveDir * ptC.hoverMoveSpeed * Time.deltaTime;
        ptC.controller.Move(horizontalMove + ptC.velocity * Time.deltaTime);

        // State transitions (after grace period)
        if (stabilizeGraceTime <= 0f)
        {
            // Landing check
            if (ptC.isGrounded)
            {
                ptC.SetLandingType(LandingType.Light);
                ptC.SetLocomotionState(LocomotionStateType.Grounded);

                if (ptC.aimHeld)
                    ptC.ChangeState(ptC.AimWalkState);
                else
                    ptC.ChangeState(ptC.FreeWalkState);
                return;
            }

            // Flight transition
            if (ptC.launchHeld && ptC.flightTimeRemaining > 0f)
            {
                ptC.ChangeState(ptC.FlightPilotState);
                return;
            }

            // Exit hover
            if (!ptC.hoverHeld || ptC.hoverTimeRemaining <= 0f)
            {
                ptC.ChangeState(ptC.FallPilotState);
                return;
            }
        }
    }

    public void FixedTick()
    {
        // Velocity decay during stabilization
        if (!hasStabilized)
        {
            ptC.velocity = Vector3.MoveTowards(ptC.velocity, Vector3.zero, ptC.hoverVelocityDecayRate * Time.fixedDeltaTime);

            if (ptC.velocity.magnitude < 0.5f)
            {
                hasStabilized = true;
                baseHoverHeight = ptC.transform.position.y;
            }
        }
        else
        {
            // Vertical control based on trigger
            float triggerValue = ptC.hoverTriggerValue;
            float neutralZoneLow = 0.4f;
            float neutralZoneHigh = 0.6f;

            if (triggerValue < neutralZoneLow)
            {
                // Descend
                float descendStrength = Mathf.InverseLerp(neutralZoneLow, 0f, triggerValue);
                ptC.velocity.y = -ptC.hoverVerticalSpeed * descendStrength;
            }
            else if (triggerValue > neutralZoneHigh)
            {
                // Ascend
                float ascendStrength = Mathf.InverseLerp(neutralZoneHigh, 1f, triggerValue);
                ptC.velocity.y = ptC.hoverVerticalSpeed * ascendStrength;
            }
            else
            {
                // Neutral - bob
                hoverBobTimer += Time.fixedDeltaTime * ptC.hoverBobFrequency;
                float bobOffset = Mathf.Sin(hoverBobTimer * Mathf.PI * 2f) * ptC.hoverBobAmplitude;
                float targetHeight = baseHoverHeight + bobOffset;
                float heightDiff = targetHeight - ptC.transform.position.y;
                ptC.velocity.y = heightDiff * 5f;
            }
        }
    }

    public void Exit()
    {
        ptC.animator.SetLayerWeight(ptC.aimLayerIndex, 0f);
        ptC.SetAiming(false);
    }
}

public class FlightPilotStateV2 : IPilotStateV2
{
    private readonly PilotTypeControllerV2 ptC;
    private float currentFlightSpeed;
    private float currentPitch;
    private float currentYaw;
    private float stabilizeGraceTime;
    private const float LAND_ROTATE_SPEED = 6f;
    private const float LANDING_CHECK_DISTANCE = 1.2f;
    private bool isLanding;

    // NFS-style smoothed keyboard input
    private float smoothedPitchInput;
    private float smoothedYawInput;

    // Aim layer blending
    private float currentAimLayerWeight;

    public FlightPilotStateV2(PilotTypeControllerV2 controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        stabilizeGraceTime = 0.3f;
        isLanding = false;
        currentFlightSpeed = ptC.flightSpeed * 0.5f;
        smoothedPitchInput = 0f;
        smoothedYawInput = 0f;
        currentAimLayerWeight = 0f;

        ptC.SetLocomotionState(LocomotionStateType.Flying);

        // Shrink collider for flight
        if (ptC.controllerPoint != null)
        {
            ptC.controller.center = ptC.transform.InverseTransformPoint(ptC.controllerPoint.position);
        }
        else
        {
            ptC.controller.center = new Vector3(0f, ptC.flightControllerHeight * 0.5f, 0f);
        }
        ptC.controller.height = ptC.flightControllerHeight;

        // Get initial orientation from camera
        Vector3 camForward = ptC.cam.forward;
        if (camForward.sqrMagnitude > 0.01f)
        {
            ptC.transform.rotation = Quaternion.LookRotation(camForward);
        }
        currentPitch = ptC.transform.eulerAngles.x;
        currentYaw = ptC.transform.eulerAngles.y;

        // Normalize pitch
        if (currentPitch > 180f) currentPitch -= 360f;

        ptC.SetCameraPriority(ptC.flightCam);
        Debug.Log("[State] Entered: FlightPilotStateV2");
    }

    public void Tick()
    {
        ptC.flightTimeRemaining -= Time.deltaTime;

        if (stabilizeGraceTime > 0f)
        {
            stabilizeGraceTime -= Time.deltaTime;
        }

        // Aim layer blending
        float targetAimWeight = ptC.aimHeld ? 1f : 0f;
        currentAimLayerWeight = Mathf.MoveTowards(currentAimLayerWeight, targetAimWeight, Time.deltaTime * ptC.aimBlendSpeed);
        ptC.animator.SetLayerWeight(ptC.aimLayerIndex, currentAimLayerWeight);
        ptC.SetAiming(ptC.aimHeld);

        // Flight blend based on input
        float blendX = Mathf.Clamp(smoothedYawInput, -1f, 1f);
        float blendY = Mathf.Clamp(-smoothedPitchInput, -1f, 1f);
        ptC.SetFlightBlend(blendX, blendY);

        // Move in facing direction
        Vector3 flightDirection = ptC.transform.forward;
        ptC.controller.Move(flightDirection * currentFlightSpeed * Time.deltaTime);

        // Counter root motion if mesh root exists
        if (ptC.meshRoot != null)
        {
            ptC.meshRoot.localPosition = Vector3.zero;
            ptC.meshRoot.localRotation = Quaternion.identity;
        }

        // Landing check
        CheckForLanding();

        // State transitions (after grace period)
        if (stabilizeGraceTime <= 0f)
        {
            // Exit flight
            if (!ptC.launchHeld || ptC.flightTimeRemaining <= 0f)
            {
                ptC.ChangeState(ptC.FallPilotState);
                return;
            }

            // Hover transition
            if (ptC.hoverHeld && ptC.hoverTimeRemaining > 0f)
            {
                ptC.ChangeState(ptC.HoverPilotState);
                return;
            }
        }
    }

    public void FixedTick()
    {
        // Speed control based on launch trigger
        float targetSpeed = Mathf.Lerp(ptC.flightSpeed * 0.3f, ptC.flightSpeed, ptC.launchTriggerValue);
        currentFlightSpeed = Mathf.MoveTowards(currentFlightSpeed, targetSpeed, ptC.flightAcceleration * Time.fixedDeltaTime);

        float pitchInput;
        float yawInput;

        if (ptC.isUsingGamepad)
        {
            pitchInput = ptC.lookInput.y;
            yawInput = ptC.lookInput.x;
        }
        else
        {
            // NFS-style keyboard smoothing
            float rawPitch = ptC.lookInput.y;
            float rawYaw = ptC.lookInput.x;

            if (Mathf.Abs(rawPitch) > 0.01f)
            {
                smoothedPitchInput = Mathf.MoveTowards(smoothedPitchInput, rawPitch, ptC.keyboardFlightRampUpSpeed * Time.fixedDeltaTime);
            }
            else
            {
                smoothedPitchInput = Mathf.MoveTowards(smoothedPitchInput, 0f, ptC.keyboardFlightRampDownSpeed * Time.fixedDeltaTime);
            }

            if (Mathf.Abs(rawYaw) > 0.01f)
            {
                smoothedYawInput = Mathf.MoveTowards(smoothedYawInput, rawYaw, ptC.keyboardFlightRampUpSpeed * Time.fixedDeltaTime);
            }
            else
            {
                smoothedYawInput = Mathf.MoveTowards(smoothedYawInput, 0f, ptC.keyboardFlightRampDownSpeed * Time.fixedDeltaTime);
            }

            pitchInput = smoothedPitchInput;
            yawInput = smoothedYawInput;
        }

        // Apply inversion
        if (ptC.invertFlightPitch)
        {
            pitchInput = -pitchInput;
        }

        // Update rotation
        currentPitch += pitchInput * ptC.pitchSpeed * Time.fixedDeltaTime;
        currentYaw += yawInput * ptC.yawSpeed * Time.fixedDeltaTime;

        // Clamp pitch
        currentPitch = Mathf.Clamp(currentPitch, -80f, 80f);

        ptC.transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    public void Exit()
    {
        // Restore collider
        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.animator.SetLayerWeight(ptC.aimLayerIndex, 0f);
        ptC.SetAiming(false);

        // Reset mesh root
        if (ptC.meshRoot != null)
        {
            ptC.meshRoot.localPosition = Vector3.zero;
            ptC.meshRoot.localRotation = Quaternion.identity;
        }
    }

    private void CheckForLanding()
    {
        if (isLanding) return;

        if (Physics.Raycast(ptC.controller.transform.position, Vector3.down, out RaycastHit hit, LANDING_CHECK_DISTANCE, ptC.groundLayerMask))
        {
            Debug.DrawRay(ptC.controller.transform.position, Vector3.down * LANDING_CHECK_DISTANCE, Color.cyan);

            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;

            if (projectedForward.sqrMagnitude < 0.01f)
            {
                projectedForward = ptC.transform.right;
                projectedForward.y = 0f;
            }
            projectedForward.Normalize();

            Quaternion uprightRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            ptC.transform.rotation = Quaternion.Slerp(ptC.transform.rotation, uprightRotation, Time.deltaTime * LAND_ROTATE_SPEED);

            float angleFromUpright = Quaternion.Angle(ptC.transform.rotation, uprightRotation);
            bool isNearlyUpright = angleFromUpright < 15f;
            bool closeToGround = hit.distance < 0.6f;

            if (closeToGround && isNearlyUpright)
            {
                isLanding = true;

                // Determine landing type based on flight speed
                if (currentFlightSpeed > ptC.flightSpeed * 0.7f)
                {
                    ptC.SetLandingType(LandingType.Heavy);
                    ptC.landingCooldown = 1.25f;
                }
                else
                {
                    ptC.SetLandingType(LandingType.Light);
                    ptC.landingCooldown = 0.4f;
                }

                ptC.SetLocomotionState(LocomotionStateType.Grounded);

                if (ptC.aimHeld)
                    ptC.ChangeState(ptC.AimWalkState);
                else
                    ptC.ChangeState(ptC.FreeWalkState);
            }
        }

        // Backup ground check
        if (ptC.isGrounded && stabilizeGraceTime <= 0f && !isLanding)
        {
            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;
            if (projectedForward.sqrMagnitude > 0.01f)
            {
                Quaternion uprightRotation = Quaternion.LookRotation(projectedForward.normalized, Vector3.up);
                ptC.transform.rotation = uprightRotation;
            }

            isLanding = true;

            ptC.SetLandingType(LandingType.Light);
            ptC.SetLocomotionState(LocomotionStateType.Grounded);

            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
    }
}

#endregion
