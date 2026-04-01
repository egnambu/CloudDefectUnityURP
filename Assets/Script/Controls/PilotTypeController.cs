using UnityEngine;
using Unity.Cinemachine;
using Unity.VisualScripting;

namespace MadeInJupiter.Controls
{
public class PilotTypeController : MonoBehaviour
{
    [Header("Debug (Read Only)")]
    [SerializeField] private string currentStateName;
    public IPilotState currentState;
    public Vector3 velocity;
    public bool isGrounded;
    [SerializeField] float jumpBufferTime = 0.1f;
    float jumpBufferCounter = 0f;
    public float landingCooldown;
    [HideInInspector] public float airborneGraceTime; // Prevents immediate ground detection after jumping

    [Header("Input Settings")]
    public bool isUsingGamepad;
    public float gamepadLookSensitivity = 1000f;
    public float mouseLookSensitivity = 0.1f;
    [Range(0f, 0.2f)] public float stickDeadzone = 0.1f;

    [Header("Core Components")]
    public CharacterController controller;
    public Transform cam;
    public Animator animator;

    [Header("Cinemachine Cameras")]
    public CinemachineCamera followCam;
    public CinemachineCamera aimCam;
    public CinemachineCamera flightCam;
    [Tooltip("Optional: Dedicated camera for AimFlight state. If not set, uses flightCam.")]
    public CinemachineCamera aimFlightCam;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 10f;
    public float turnSpeed = 720f;
    public float aimRotationSpeed = 10f;
    public float gravity = -20f;

    [Header("Ground Check")]
    public LayerMask groundLayerMask;
    public float groundCheckRadius = 0.3f;
    public Transform groundCheckPoint;

    [Header("Animator Settings")]
    public float animatorLerpRate = 12f;
    public float aimBlendSpeed = 12f;
    public int aimLayerIndex = 1;

    [Header("Hover Settings")]
    public float hoverMaxDuration = 50f;
    public float hoverVelocityDecayRate = 35f; // How fast velocity decays to zero
    public float hoverBobAmplitude = 0.3f; // How much the character bobs up/down
    public float hoverBobFrequency = 1.5f; // Speed of the bob oscillation
    public float hoverMoveSpeed = 4f; // Horizontal movement speed while hovering
    public float hoverVerticalSpeed = 3f; // Vertical speed when using analog trigger
    [HideInInspector] public float hoverTimeRemaining;

    [Header("Flight Settings")]
    public float flightSpeed = 54f;
    public float flightAcceleration = 12f;
    public float pitchSpeed = 280f;
    public float yawSpeed = 280f;
    public float flightMaxDuration = 120f;
    public float flightControllerHeight = 0.2f;
    public Transform controllerPoint; // Optional: point to center the small collider on
    public Transform meshRoot; // For countering animation root motion during flight
    [HideInInspector] public float flightTimeRemaining;

    [Header("Flight Control Options")]
    public bool invertFlightPitch = true;
    [Tooltip("How fast keyboard input ramps up to full value (NFS-style smooth steering)")]
    public float keyboardFlightRampUpSpeed = 2.5f;
    [Tooltip("How fast keyboard input ramps back to zero when released")]
    public float keyboardFlightRampDownSpeed = 4f;

    [HideInInspector] public Pilot1 input;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public Vector2 lookInput;
    [HideInInspector] public bool aimHeld;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool sprintPressed;
    [HideInInspector] public bool launchHeld;
    [HideInInspector] public bool hoverHeld;

    // Analog trigger values (0-1 range)
    [HideInInspector] public float launchTriggerValue; // R2 - flight speed control
    [HideInInspector] public float hoverTriggerValue;  // L2 - hover altitude control

    [HideInInspector] public float smoothMoveX;
    [HideInInspector] public float smoothMoveY;

    [HideInInspector] public float defaultHeight;
    [HideInInspector] public Vector3 defaultCenter;

    /// <summary>Shared platform tracker used by all grounded states.</summary>
    public MovingPlatformHandler platformHandler { get; private set; }

    public FreeWalkState FreeWalkState { get; private set; }
    public AimWalkState AimWalkState { get; private set; }
    public JumpPilotState JumpPilotState { get; private set; }
    public FallPilotState FallPilotState { get; private set; }
    public HoverPilotState HoverPilotState { get; private set; }
    public FlightPilotState FlightPilotState { get; private set; }


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
        animator = GetComponent<Animator>();
        input = new Pilot1();
        platformHandler = new MovingPlatformHandler();

        FreeWalkState = new FreeWalkState(this);
        AimWalkState = new AimWalkState(this);
        JumpPilotState = new JumpPilotState(this);
        FallPilotState = new FallPilotState(this);
        HoverPilotState = new HoverPilotState(this);
        FlightPilotState = new FlightPilotState(this);
    }

    void OnEnable() => input.Enable();
    void OnDisable()
    {
        input.Disable();
        // Restore cursor when disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    void OnDestroy()
    {
        input.Dispose();
        // Restore cursor when destroyed
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        // Lock and hide cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultHeight = controller.height;
        defaultCenter = controller.center;
        hoverTimeRemaining = hoverMaxDuration;
        flightTimeRemaining = flightMaxDuration;

        if (animator != null) animator.applyRootMotion = false;
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

    void ReadInput()
    {
        moveInput = input.PlayerA.Move.ReadValue<Vector2>();
        lookInput = input.PlayerA.Look.ReadValue<Vector2>();
        aimHeld = input.PlayerA.Aim.IsPressed();
        jumpPressed = input.PlayerA.Jump.IsPressed();
        sprintPressed = input.PlayerA.Sprint.IsPressed();

        // Read analog trigger values from input actions (preserves analog pressure)
        launchTriggerValue = input.PlayerA.Launch.ReadValue<float>();
        hoverTriggerValue = input.PlayerA.Hover.ReadValue<float>();

        // Detect if using gamepad - check multiple controls to properly detect device
        var moveControl = input.PlayerA.Move.activeControl;
        var launchControl = input.PlayerA.Launch.activeControl;
        var hoverControl = input.PlayerA.Hover.activeControl;

        bool moveIsGamepad = moveControl != null && moveControl.device is UnityEngine.InputSystem.Gamepad;
        bool launchIsGamepad = launchControl != null && launchControl.device is UnityEngine.InputSystem.Gamepad;
        bool hoverIsGamepad = hoverControl != null && hoverControl.device is UnityEngine.InputSystem.Gamepad;

        isUsingGamepad = moveIsGamepad || launchIsGamepad || hoverIsGamepad;

        // Keyboard gives 0 or 1 for hover (binary), gamepad gives 0-1 analog range
        // For keyboard hover, use 0.5 as neutral hover position to maintain altitude
        if (!hoverIsGamepad && hoverTriggerValue > 0f)
        {
            hoverTriggerValue = 0.5f; // Neutral hover for keyboard
        }

        // Determine if triggers are held (threshold for activation)
        launchHeld = launchTriggerValue > 0.1f;
        hoverHeld = hoverTriggerValue > 0.1f;

        // Debug: Show trigger values when in hover or flight state
        if (currentState == HoverPilotState || currentState == FlightPilotState)
        {
            Debug.Log($"L2 (Hover): {hoverTriggerValue:F2} | R2 (Launch): {launchTriggerValue:F2} | HoverIsGamepad: {hoverIsGamepad}");
        }

        // Apply deadzone to stick input
        if (moveInput.magnitude < stickDeadzone)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            // Remap input to use full range after deadzone
            moveInput = moveInput.normalized * ((moveInput.magnitude - stickDeadzone) / (1f - stickDeadzone));
        }
    }


    public void ChangeState(IPilotState newState)
    {
        if (newState == null) return;

        currentState?.Exit();

        currentState = newState;
        currentStateName = newState.GetType().Name;
        currentState.Enter();
    }
    public void UpdateGroundedStatus()
    {
        isGrounded = Physics.CheckSphere(
            groundCheckPoint.position,
            groundCheckRadius,
            groundLayerMask,
            QueryTriggerInteraction.Ignore
        );
    }

    public void SetCameraPriority(CinemachineCamera activeCamera)
    {
        followCam.Priority = 10;
        aimCam.Priority = 10;
        flightCam.Priority = 10;
        if (aimFlightCam != null) aimFlightCam.Priority = 10;
        activeCamera.Priority = 20;
    }

    public void CheckStateTransitions()
    {
        if (jumpBufferCounter > 0 && isGrounded)
        {
            ChangeState(JumpPilotState);
            jumpBufferCounter = 0f;
            return;
        }
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

        if (jumpPressed)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;
    }

    public Vector3 GetCameraRelativeMovement()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        // Get camera directions (flattened to horizontal plane)
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return camForward * inputDir.z + camRight * inputDir.x;
    }

    public void ApplyGravity()
    {
        if (isGrounded)
        {
            if (velocity.y < 0f)
            {
                velocity.y = -4f;
            }
        }
        else
        {
            velocity.y += gravity * Time.fixedDeltaTime;
            animator.SetBool("IsFalling", true);
        }
    }

    public void TriggerLanding(float fallVelocity)
    {
        float fallingSpeed = Mathf.Abs(fallVelocity);
        Debug.Log($"Falling Speed:" + fallingSpeed);
        if (fallingSpeed > 10f)
        {
            Debug.Log($"HeavyLanding:" + fallVelocity);
            animator.SetTrigger("HeavyLanding");
            landingCooldown = 1.25f; // Heavy landing recovery time
        }
        else
        {
            Debug.Log($"LightLanding:" + fallVelocity);
            animator.SetTrigger("LightLanding");
            landingCooldown = 0.4f; // Light landing recovery time
        }
    }

    public void ApplyJumpForce()
    {
        velocity.y = 10f;
        //Debug.Log("Force has been applied");
    }

    void OnDrawGizmos()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}

public interface IPilotState
{
    void Enter();
    void Exit();
    void Tick();
    void FixedTick();
}

public class FreeWalkState : IPilotState
{
    private readonly PilotTypeController ptC;
    private float forwardSpeed;
    private bool isSprinting;

    public FreeWalkState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        // CRITICAL: Reset ALL animator bools to prevent state conflicts
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetBool("IsFlying", false); // Must explicitly set false!

        // Reset flight and hover blend parameters
        ptC.animator.SetFloat("FlightX", 0f);
        ptC.animator.SetFloat("FlightY", 0f);
        ptC.animator.SetFloat("HoverX", 0f);
        ptC.animator.SetFloat("HoverY", 0f);

        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.followCam);
        Debug.Log("Entered: FreeWalkState");
    }

    public void Tick()
    {
        // 1. Detect platform and compute position + rotation deltas
        ptC.platformHandler.UpdateBeforeMove(
            ptC.transform,
            ptC.groundCheckPoint.position,
            ptC.groundLayerMask);

        Vector3 platformDelta = ptC.platformHandler.PositionDelta;
        float platformYaw = ptC.platformHandler.YawDelta;

        // Apply platform yaw BEFORE any other rotation so the player orbits with the platform
        if (Mathf.Abs(platformYaw) > 0.001f)
        {
            ptC.transform.Rotate(0f, platformYaw, 0f, Space.World);
        }

        // Transition to fall state if not grounded
        if (!ptC.isGrounded)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // During landing recovery — no movement input, but keep riding the platform
        if (ptC.landingCooldown > 0f)
        {
            ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.animator.SetFloat("MoveX", ptC.smoothMoveX);
            ptC.animator.SetFloat("MoveY", ptC.smoothMoveY);

            // platformDelta is already a world-space offset — do NOT multiply by deltaTime
            ptC.controller.Move(ptC.velocity * Time.deltaTime + platformDelta);
            ptC.platformHandler.UpdateAfterMove(ptC.transform);
            return;
        }

        ptC.CheckStateTransitions();

        Vector3 moveDir = ptC.GetCameraRelativeMovement();

        isSprinting = ptC.sprintPressed && ptC.isGrounded && ptC.moveInput.magnitude > 0.01f;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ptC.transform.rotation = Quaternion.RotateTowards(
                ptC.transform.rotation,
                targetRotation,
                ptC.turnSpeed * Time.deltaTime
            );
        }

        forwardSpeed = isSprinting && ptC.moveInput.magnitude > 0.01f ? 1.5f : ptC.moveInput.magnitude;

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, forwardSpeed, Time.deltaTime * ptC.animatorLerpRate);
        ptC.animator.SetFloat("MoveX", ptC.smoothMoveX);
        ptC.animator.SetFloat("MoveY", ptC.smoothMoveY);

        float currentSpeed = isSprinting ? ptC.sprintSpeed : ptC.moveSpeed;
        Vector3 horizontalMove = moveDir * currentSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;

        // platformDelta is already a world-space offset — do NOT multiply by deltaTime
        ptC.controller.Move(horizontalMove + verticalMove + platformDelta);
        ptC.platformHandler.UpdateAfterMove(ptC.transform);
    }

public void FixedTick()
{
    ptC.ApplyGravity();
    ptC.hoverTimeRemaining = Mathf.MoveTowards(ptC.hoverTimeRemaining, ptC.hoverMaxDuration, Time.fixedDeltaTime * 5f);
    ptC.flightTimeRemaining = Mathf.MoveTowards(ptC.flightTimeRemaining, ptC.flightMaxDuration, Time.fixedDeltaTime * 5f);
}

public void Exit()
{
    isSprinting = false;
}
}

public class AimWalkState : IPilotState
{
    private readonly PilotTypeController ptC;

    public AimWalkState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        Debug.Log("Entered: AimWalkState");

        // CRITICAL: Reset ALL animator bools to prevent state conflicts
        ptC.animator.SetBool("IsAiming", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetBool("IsFlying", false); // Must explicitly set false!

        // Reset flight and hover blend parameters
        ptC.animator.SetFloat("FlightX", 0f);
        ptC.animator.SetFloat("FlightY", 0f);
        ptC.animator.SetFloat("HoverX", 0f);
        ptC.animator.SetFloat("HoverY", 0f);

        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.aimCam);
    }

    public void Tick()
    {
        // 1. Detect platform and compute position + rotation deltas
        ptC.platformHandler.UpdateBeforeMove(
            ptC.transform,
            ptC.groundCheckPoint.position,
            ptC.groundLayerMask);

        Vector3 platformDelta = ptC.platformHandler.PositionDelta;
        float platformYaw = ptC.platformHandler.YawDelta;

        // Apply platform yaw before aim rotation (aim Slerp will blend on top)
        if (Mathf.Abs(platformYaw) > 0.001f)
        {
            ptC.transform.Rotate(0f, platformYaw, 0f, Space.World);
        }

        // Transition to fall state if not grounded
        if (!ptC.isGrounded)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // Declare camForward at method scope for reuse
        Vector3 camForward;

        // During landing recovery — no movement input, but keep riding the platform
        if (ptC.landingCooldown > 0f)
        {
            // Still allow aim rotation during landing
            camForward = ptC.cam.forward;
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

            // Force idle animation during landing
            ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.animator.SetFloat("AimWalkX", ptC.smoothMoveX);
            ptC.animator.SetFloat("AimWalkY", ptC.smoothMoveY);

            // platformDelta is already a world-space offset — do NOT multiply by deltaTime
            ptC.controller.Move(ptC.velocity * Time.deltaTime + platformDelta);
            ptC.platformHandler.UpdateAfterMove(ptC.transform);
            return;
        }

        ptC.CheckStateTransitions();

        camForward = ptC.cam.forward;
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

        Vector3 inputDir = new Vector3(ptC.moveInput.x, 0f, ptC.moveInput.y);
        Vector3 worldDir = ptC.cam.forward * inputDir.z + ptC.cam.right * inputDir.x;
        Vector3 localDir = ptC.transform.InverseTransformDirection(worldDir);

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, localDir.x, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, localDir.z, Time.deltaTime * ptC.animatorLerpRate);

        ptC.animator.SetFloat("AimWalkX", ptC.smoothMoveX);
        ptC.animator.SetFloat("AimWalkY", ptC.smoothMoveY);

        // Movement in Tick for smooth high-framerate rendering
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;

        // platformDelta is already a world-space offset — do NOT multiply by deltaTime
        ptC.controller.Move(horizontalMove + verticalMove + platformDelta);
        ptC.platformHandler.UpdateAfterMove(ptC.transform);
    }

    public void FixedTick()
    {
        // Physics calculations only
        ptC.ApplyGravity();

        // Regenerate hover and flight time while grounded
        ptC.hoverTimeRemaining = Mathf.MoveTowards(ptC.hoverTimeRemaining, ptC.hoverMaxDuration, Time.fixedDeltaTime * 5f);
        ptC.flightTimeRemaining = Mathf.MoveTowards(ptC.flightTimeRemaining, ptC.flightMaxDuration, Time.fixedDeltaTime * 5f);
    }

    public void Exit()
    {
        ptC.animator.SetBool("IsAiming", false);
    }


}

public class JumpPilotState : IPilotState
{
    private readonly PilotTypeController ptC;
    private bool hasAppliedJumpForce;

    public JumpPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        ptC.platformHandler.ClearPlatform();
        Debug.Log("Entered: JumpWalkState");
        hasAppliedJumpForce = false;
        ptC.animator.SetTrigger("Jump");
        ptC.airborneGraceTime = 0.2f; // Grace period to prevent immediate ground detection
        //ptC.SetCameraPriority(ptC.followCam);
    }



    public void FixedTick()
    {
        // Physics calculations only - gravity accumulation
        ptC.velocity.y += ptC.gravity * Time.fixedDeltaTime;
    }

    public void Tick()
    {
        // Decrement grace time
        if (ptC.airborneGraceTime > 0f)
        {
            ptC.airborneGraceTime -= Time.deltaTime;
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

        // Movement in Tick for smooth high-framerate rendering
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * 0.5f * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);

        // Check for flight transition (launch held and has time remaining)
        if (ptC.launchHeld && ptC.flightTimeRemaining > 0f)
        {
            ptC.ChangeState(ptC.FlightPilotState);
            return;
        }

        // Check for hover transition (must have time remaining)
        if (ptC.hoverHeld && ptC.hoverTimeRemaining > 0f)
        {
            ptC.ChangeState(ptC.HoverPilotState);
            return;
        }

        // Check if animator has transitioned to fall state
        AnimatorStateInfo stateInfo = ptC.animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Fall") || stateInfo.IsTag("Fall"))
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // Transition to fall state when starting to descend
        if (ptC.velocity.y <= 0f)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }
    }

    public void Exit()
    {

        ptC.animator.ResetTrigger("Jump");
    }

}

public class FallPilotState : IPilotState
{
    private readonly PilotTypeController ptC;
    private float fallTime;
    private float landingCheckDistance = 1.5f;
    private float landRotateSpeed = 8f;
    private bool isLanding;

    public FallPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        ptC.platformHandler.ClearPlatform();
        fallTime = 0f;
        isLanding = false;
        Debug.Log("Entered: FallWalkState");

        // CRITICAL: Reset ALL animator bools to prevent state conflicts
        ptC.animator.SetBool("IsFalling", true);
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetBool("IsFlying", false); // Must explicitly set false!

        // Reset flight blend parameters
        ptC.animator.SetFloat("FlightX", 0f);
        ptC.animator.SetFloat("FlightY", 0f);

        ptC.SetCameraPriority(ptC.followCam);

        // Set a small grace time if we don't already have one (walking off ledge)
        if (ptC.airborneGraceTime <= 0f)
        {
            ptC.airborneGraceTime = 0.1f;
        }
    }

    public void Exit()
    {
        ptC.animator.SetBool("IsFalling", false);
        isLanding = false;
        // Note: Landing animations are now triggered by ground states on entry
        // This prevents false landings when transitioning to hover
    }

    public void FixedTick()
    {
        // Physics calculations only - gravity accumulation
        ptC.velocity.y += ptC.gravity * Time.fixedDeltaTime;
    }

    public void Tick()
    {
        fallTime += Time.deltaTime;

        // Decrement grace time
        if (ptC.airborneGraceTime > 0f)
        {
            ptC.airborneGraceTime -= Time.deltaTime;
        }

        // Rotation towards movement direction (only if not close to landing)
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

        // Movement in Tick for smooth high-framerate rendering
        Vector3 horizontalMove = moveDir * ptC.moveSpeed * 0.5f * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);

        // Check for flight transition (launch held and has time remaining)
        if (ptC.launchHeld && ptC.flightTimeRemaining > 0f)
        {
            ptC.ChangeState(ptC.FlightPilotState);
            return;
        }

        // Check for hover transition (must have time remaining)
        if (ptC.hoverHeld && ptC.hoverTimeRemaining > 0f)
        {
            ptC.ChangeState(ptC.HoverPilotState);
            return;
        }

        // Check for landing with perpendicular rotation
        CheckForLanding();
    }

    private void CheckForLanding()
    {
        // Only check after grace period
        if (ptC.airborneGraceTime > 0f) return;

        if (!isLanding)
        {
            RaycastHit hit;
            if (Physics.Raycast(ptC.controller.transform.position, Vector3.down, out hit, landingCheckDistance, ptC.groundLayerMask))
            {
                Debug.DrawRay(ptC.controller.transform.position, Vector3.down * landingCheckDistance, Color.cyan);

                // Calculate target rotation to be fully upright
                Vector3 projectedForward = ptC.transform.forward;
                projectedForward.y = 0f;

                if (projectedForward.sqrMagnitude < 0.01f)
                {
                    // If looking straight up/down, use right vector instead
                    projectedForward = ptC.transform.right;
                    projectedForward.y = 0f;
                }
                projectedForward.Normalize();

                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, Vector3.up);

                // Rotate towards upright - faster when closer to ground
                float distanceToGround = hit.distance;
                float rotationMultiplier = Mathf.Lerp(3f, 1f, distanceToGround / landingCheckDistance);
                ptC.transform.rotation = Quaternion.Slerp(ptC.transform.rotation, targetRotation, Time.deltaTime * landRotateSpeed * rotationMultiplier);

                // Check if close enough to land
                float angle = Vector3.Angle(ptC.transform.up, Vector3.up);
                bool closeToGround = distanceToGround < 0.6f;
                bool isNearlyUpright = angle < 20f;

                if (closeToGround && isNearlyUpright && ptC.velocity.y <= 0f)
                {
                    isLanding = true;

                    // Snap to fully upright before transitioning
                    ptC.transform.rotation = targetRotation;

                    ptC.TriggerLanding(ptC.velocity.y);

                    if (ptC.aimHeld)
                        ptC.ChangeState(ptC.AimWalkState);
                    else
                        ptC.ChangeState(ptC.FreeWalkState);
                }
            }
            else
            {
                Debug.DrawRay(ptC.controller.transform.position, Vector3.down * landingCheckDistance, Color.blue);
            }
        }

        // Fallback: Also check if grounded via controller (for edges/slopes)
        if (ptC.isGrounded && ptC.velocity.y <= 0f && !isLanding)
        {
            isLanding = true;

            // Snap to upright
            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;
            if (projectedForward.sqrMagnitude > 0.01f)
            {
                projectedForward.Normalize();
                ptC.transform.rotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            }

            ptC.TriggerLanding(ptC.velocity.y);

            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
    }
}

public class HoverPilotState : IPilotState
{
    private readonly PilotTypeController ptC;
    private float hoverBobTimer;
    private float baseHoverHeight;
    private bool hasStabilized; // Tracks if initial velocity has decayed
    private float stabilizeGraceTime; // Prevents immediate exit issues
    private float currentAimLayerWeight; // For smooth aim layer blending

    public HoverPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        ptC.platformHandler.ClearPlatform();
        Debug.Log("Entered: HoverState");
        hoverBobTimer = 0f;
        hasStabilized = false;
        stabilizeGraceTime = 0.15f; // Small grace period on entry
        baseHoverHeight = ptC.transform.position.y;
        currentAimLayerWeight = ptC.animator.GetLayerWeight(1); // Get current weight

        // CRITICAL: Reset ALL animator bools to prevent state conflicts
        ptC.animator.SetBool("IsHovering", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetBool("IsFlying", false); // Must explicitly set false!

        // Reset flight and walk blend parameters
        ptC.animator.SetFloat("FlightX", 0f);
        ptC.animator.SetFloat("FlightY", 0f);
        ptC.animator.SetFloat("MoveX", 0f);
        ptC.animator.SetFloat("MoveY", 0f);
        ptC.animator.SetFloat("AimWalkX", 0f);
        ptC.animator.SetFloat("AimWalkY", 0f);

        ptC.animator.SetTrigger("HoverStart");
        ptC.SetCameraPriority(ptC.aimCam);
    }

    public void Exit()
    {
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetFloat("HoverX", 0f);
        ptC.animator.SetFloat("HoverY", 0f);
        ptC.animator.ResetTrigger("HoverStart");
        // Reset aim layer weight on exit
        ptC.animator.SetLayerWeight(1, 0f);
    }

    public void FixedTick()
    {
        // Physics calculations only - velocity decay for horizontal
        float decayFactor = ptC.hoverVelocityDecayRate * Time.fixedDeltaTime;
        ptC.velocity.x = Mathf.MoveTowards(ptC.velocity.x, 0f, decayFactor);
        ptC.velocity.z = Mathf.MoveTowards(ptC.velocity.z, 0f, decayFactor);

        // Always decay vertical velocity aggressively when entering hover
        if (!hasStabilized)
        {
            // Rapidly decay vertical velocity to stabilize - use very aggressive decay
            float verticalDecay = decayFactor * 4f; // 4x decay rate for vertical
            ptC.velocity.y = Mathf.MoveTowards(ptC.velocity.y, 0f, verticalDecay);

            // Check if velocity has stabilized (near zero)
            if (Mathf.Abs(ptC.velocity.y) < 0.5f && Mathf.Abs(ptC.velocity.x) < 0.5f && Mathf.Abs(ptC.velocity.z) < 0.5f)
            {
                hasStabilized = true;
                baseHoverHeight = ptC.transform.position.y;
            }
        }
        else
        {
            // 0.55 to 0.85 = hover in place (with bob) : < 0.55 = descend : > 0.85 = ascend
            float triggerValue = ptC.hoverTriggerValue;
            float neutralZoneLow = 0.55f;
            float neutralZoneHigh = 0.85f;

            if (triggerValue < neutralZoneLow)
            {
                // Descending - map 0.55 to 0 => 0 to -1 vertical input
                float descendInput = (neutralZoneLow - triggerValue) / neutralZoneLow;
                ptC.velocity.y = Mathf.MoveTowards(ptC.velocity.y, -ptC.hoverVerticalSpeed * descendInput, decayFactor * 3f);
                // Update base height for when returning to neutral
                baseHoverHeight = ptC.transform.position.y;
            }
            else if (triggerValue > neutralZoneHigh)
            {
                // Ascending - map 0.85 to 1 => 0 to 1 vertical input
                float ascendInput = (triggerValue - neutralZoneHigh) / (1f - neutralZoneHigh);
                ptC.velocity.y = Mathf.MoveTowards(ptC.velocity.y, ptC.hoverVerticalSpeed * ascendInput, decayFactor * 3f);
                // Update base height for when returning to neutral
                baseHoverHeight = ptC.transform.position.y;
            }
            else
            {
                // Neutral zone - apply bob effect
                hoverBobTimer += Time.fixedDeltaTime;
                float bobOffset = Mathf.Sin(hoverBobTimer * ptC.hoverBobFrequency * Mathf.PI * 2f) * ptC.hoverBobAmplitude;

                // Calculate bob velocity to maintain smooth oscillation
                float targetY = baseHoverHeight + bobOffset;
                float currentY = ptC.transform.position.y;
                ptC.velocity.y = (targetY - currentY) * 5f; // Smooth follow
            }
        }
    }

    public void Tick()
    {
        // Decrement grace time
        if (stabilizeGraceTime > 0f)
        {
            stabilizeGraceTime -= Time.deltaTime;
        }

        // Consume hover time
        ptC.hoverTimeRemaining -= Time.deltaTime;
        ptC.hoverTimeRemaining = Mathf.Max(0f, ptC.hoverTimeRemaining);

        // Rotation only in Tick - Face camera forward direction (like aim state)
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

        // Update animator blend tree parameters (like aim state)
        Vector3 inputDir = new Vector3(ptC.moveInput.x, 0f, ptC.moveInput.y);
        Vector3 worldDir = ptC.cam.forward * inputDir.z + ptC.cam.right * inputDir.x;
        Vector3 localDir = ptC.transform.InverseTransformDirection(worldDir);

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, localDir.x, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, localDir.z, Time.deltaTime * ptC.animatorLerpRate);
        ptC.animator.SetFloat("HoverX", ptC.smoothMoveX);
        ptC.animator.SetFloat("HoverY", ptC.smoothMoveY);

        // Smoothly blend aim layer based on aimHeld
        float targetAimWeight = ptC.aimHeld ? 1f : 0f;
        currentAimLayerWeight = Mathf.Lerp(currentAimLayerWeight, targetAimWeight, Time.deltaTime * ptC.aimBlendSpeed);
        ptC.animator.SetLayerWeight(1, currentAimLayerWeight);

        // Movement in Tick for smooth high-framerate rendering
        Vector3 moveDir = ptC.GetCameraRelativeMovement();
        Vector3 horizontalMove = moveDir * ptC.hoverMoveSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);

        // Exit conditions (only after grace period)
        if (stabilizeGraceTime <= 0f)
        {
            // Check for ground contact FIRST (highest priority when grounded)
            if (ptC.isGrounded)
            {
                // Store velocity before resetting for landing animation
                float landingVelocity = ptC.velocity.y;

                // Reset velocity before transitioning - prevents carrying fall speed
                ptC.velocity = Vector3.zero;

                // Trigger landing animation based on how fast we were falling
                ptC.TriggerLanding(landingVelocity);
                Debug.Log("TriggerLanding in Hover with velocity: " + landingVelocity);

                if (ptC.aimHeld)
                    ptC.ChangeState(ptC.AimWalkState);
                else
                    ptC.ChangeState(ptC.FreeWalkState);
                return;
            }

            // Transition to flight if launch is pressed (prevents hover->fall->flight cycle)
            if (ptC.launchHeld && ptC.flightTimeRemaining > 0f)
            {
                ptC.ChangeState(ptC.FlightPilotState);
                return;
            }

            // Exit if hover button released or time expired
            if (!ptC.hoverHeld || ptC.hoverTimeRemaining <= 0f)
            {
                ptC.ChangeState(ptC.FallPilotState);
                return;
            }
        }
    }
}

public class FlightPilotState : IPilotState
{
    private readonly PilotTypeController ptC;
    private float currentFlightSpeed;
    private float currentPitch;
    private float currentYaw;
    private float stabilizeGraceTime;
    private float landRotateSpeed = 6f;
    private float landingCheckDistance = 1.2f;
    private bool isLanding;

    // NFS-style smoothed keyboard input (interpolates binary 0/1 to feel like analog)
    private float smoothedPitchInput;
    private float smoothedYawInput;

    // Aim layer blending (like HoverPilotState)
    private float currentAimLayerWeight;

    public FlightPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        ptC.platformHandler.ClearPlatform();
        Debug.Log("Entered: FlightPilotState");
        currentFlightSpeed = 0f;
        currentPitch = 0f;
        currentYaw = 0f;
        stabilizeGraceTime = 0.15f;
        isLanding = false;

        // Resize controller for flight (smaller collider)
        ptC.controller.height = ptC.flightControllerHeight;
        if (ptC.controllerPoint != null)
        {
            ptC.controller.center = ptC.transform.InverseTransformPoint(ptC.controllerPoint.position);
        }
        else
        {
            ptC.controller.center = new Vector3(0, 0.1f, 0);
        }

        // CRITICAL: Reset ALL animator bools to prevent state conflicts
        // This fixes the "walking in air" bug when rapidly switching states
        ptC.animator.SetBool("IsFlying", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetBool("IsAiming", false);

        // Reset movement blend parameters to prevent carrying over walk animation values
        ptC.animator.SetFloat("MoveX", 0f);
        ptC.animator.SetFloat("MoveY", 0f);
        ptC.animator.SetFloat("AimWalkX", 0f);
        ptC.animator.SetFloat("AimWalkY", 0f);
        ptC.animator.SetFloat("HoverX", 0f);
        ptC.animator.SetFloat("HoverY", 0f);

        ptC.animator.SetTrigger("FlightStart");

        // Clear velocity - flight controls movement directly
        ptC.velocity = Vector3.zero;

        // Reset smooth move values on controller
        ptC.smoothMoveX = 0f;
        ptC.smoothMoveY = 0f;

        // Reset smoothed keyboard input
        smoothedPitchInput = 0f;
        smoothedYawInput = 0f;

        // Initialize aim layer weight (like HoverPilotState)
        currentAimLayerWeight = ptC.animator.GetLayerWeight(1);

        ptC.SetCameraPriority(ptC.flightCam);
    }

    public void Exit()
    {
        // Restore controller dimensions
        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        // Reset animator states
        ptC.animator.SetBool("IsFlying", false);
        ptC.animator.ResetTrigger("FlightStart");
        ptC.animator.SetFloat("FlightX", 0f);
        ptC.animator.SetFloat("FlightY", 0f);

        // Reset aim layer weight on exit (like HoverPilotState)
        ptC.animator.SetLayerWeight(1, 0f);

        // Reset flight speed
        currentFlightSpeed = 0f;
        isLanding = false;

        Debug.Log("Exited: FlightPilotState");
    }

    public void FixedTick()
    {
        // Calculate speed-based turn resistance (0 = full agility, 1 = max resistance)
        float speedFactor = Mathf.Clamp01(currentFlightSpeed / ptC.flightSpeed);
        float turnResistance = Mathf.Lerp(1f, 0.3f, speedFactor); // At max speed, 70% harder to turn

        // NFS-style input smoothing for keyboard (binary input feels like analog)
        // For gamepad: use raw analog input directly
        // For keyboard: smoothly interpolate towards target, creating steering ramp
        float rawPitchInput = ptC.invertFlightPitch ? -ptC.moveInput.y : ptC.moveInput.y;
        float rawYawInput = ptC.moveInput.x;

        if (ptC.isUsingGamepad)
        {
            // Gamepad already has analog input, use directly
            smoothedPitchInput = rawPitchInput;
            smoothedYawInput = rawYawInput;
        }
        else
        {
            // Keyboard: NFS 2013 style - ramp up when pressing, ramp down when releasing
            // When pressing opposite direction (e.g., left while fully right), use ramp up speed
            // for responsive counter-steering
            float pitchRampSpeed = Mathf.Abs(rawPitchInput) > 0.01f
                ? ptC.keyboardFlightRampUpSpeed
                : ptC.keyboardFlightRampDownSpeed;
            float yawRampSpeed = Mathf.Abs(rawYawInput) > 0.01f
                ? ptC.keyboardFlightRampUpSpeed
                : ptC.keyboardFlightRampDownSpeed;

            smoothedPitchInput = Mathf.MoveTowards(smoothedPitchInput, rawPitchInput, pitchRampSpeed * Time.fixedDeltaTime);
            smoothedYawInput = Mathf.MoveTowards(smoothedYawInput, rawYawInput, yawRampSpeed * Time.fixedDeltaTime);
        }

        // PITCH & YAW CONTROL using smoothed input with speed-based resistance
        float targetPitch = smoothedPitchInput * ptC.pitchSpeed * turnResistance;
        float targetYaw = smoothedYawInput * ptC.yawSpeed * turnResistance;

        // Smooth rotation changes
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.fixedDeltaTime * 5f);
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.fixedDeltaTime * 5f);

        // Apply rotation to transform
        ptC.transform.Rotate(currentPitch * Time.fixedDeltaTime, currentYaw * Time.fixedDeltaTime, 0f, Space.Self);

        // ANALOG TRIGGER SPEED CONTROL (R2 for flight)
        // Speed is proportional to trigger pressure: 20% press = 20% speed, etc.
        float targetSpeed = ptC.flightSpeed * ptC.launchTriggerValue;
        currentFlightSpeed = Mathf.MoveTowards(currentFlightSpeed, targetSpeed, ptC.flightAcceleration * Time.fixedDeltaTime);

        // Move forward in the character's facing direction
        Vector3 forwardMovement = ptC.transform.forward * currentFlightSpeed;

        // Apply movement
        ptC.controller.Move(forwardMovement * Time.fixedDeltaTime);

        // Consume flight time
        ptC.flightTimeRemaining -= Time.fixedDeltaTime;
        ptC.flightTimeRemaining = Mathf.Max(0f, ptC.flightTimeRemaining);
    }

    public void Tick()
    {
        // Decrement grace time
        if (stabilizeGraceTime > 0f)
        {
            stabilizeGraceTime -= Time.deltaTime;
        }

        // Update animator blend tree parameters
        // FlightX = yaw (left/right), FlightY = pitch (up/down forward motion)
        // Use smoothed input values so animations match the smoothed steering
        float smoothX = Mathf.Lerp(ptC.animator.GetFloat("FlightX"), smoothedYawInput, Time.deltaTime * ptC.animatorLerpRate);
        float smoothY = Mathf.Lerp(ptC.animator.GetFloat("FlightY"), smoothedPitchInput, Time.deltaTime * ptC.animatorLerpRate);
        smoothX = Mathf.Clamp(smoothX, -1f, 1f);
        smoothY = Mathf.Clamp(smoothY, -1f, 1f);
        ptC.animator.SetFloat("FlightX", smoothX);
        ptC.animator.SetFloat("FlightY", smoothY);

        // Smoothly blend aim layer based on aimHeld (like HoverPilotState)
        // This allows aiming while flying without changing state
        float targetAimWeight = ptC.aimHeld ? 1f : 0f;
        currentAimLayerWeight = Mathf.Lerp(currentAimLayerWeight, targetAimWeight, Time.deltaTime * ptC.aimBlendSpeed);
        ptC.animator.SetLayerWeight(1, currentAimLayerWeight);

        // Check for landing - use raycast to detect ground proximity
        CheckForLanding();

        // Exit conditions (only after grace period)
        if (stabilizeGraceTime <= 0f)
        {
            // Exit if launch button released or time expired
            if (!ptC.launchHeld || ptC.flightTimeRemaining <= 0f)
            {
                ptC.ChangeState(ptC.FallPilotState);
                return;
            }

            // Transition to hover if hover button pressed
            if (ptC.hoverHeld && ptC.hoverTimeRemaining > 0f)
            {
                ptC.ChangeState(ptC.HoverPilotState);
                return;
            }
        }
    }

    
    private void CheckForLanding()
    {

        if (!isLanding)
        {
            RaycastHit hit;
            if (Physics.Raycast(ptC.controller.transform.position, Vector3.down, out hit, landingCheckDistance, ptC.groundLayerMask))
            {
                Debug.DrawRay(ptC.controller.transform.position, Vector3.down * landingCheckDistance, Color.red);
                Debug.DrawRay(hit.point, hit.normal * 0.6f, Color.yellow);

                // Calculate target rotation: Character should rotate to be fully upright (perpendicular to ground)
                Vector3 currentForward = ptC.transform.forward;
                Vector3 groundUp = Vector3.up; // Use world up for flat ground alignment

                // Project forward onto the horizontal plane to maintain facing direction
                Vector3 projectedForward = currentForward;
                projectedForward.y = 0f;
                projectedForward.Normalize();

                if (projectedForward.sqrMagnitude < 0.01f)
                {
                    // If looking straight up/down, use right vector instead
                    projectedForward = ptC.transform.right;
                    projectedForward.y = 0f;
                    projectedForward.Normalize();
                }

                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundUp);

                // Rotate the main transform towards upright position - use faster rotation when closer to ground
                float distanceToGround = hit.distance;
                float rotationMultiplier = Mathf.Lerp(3f, 1f, distanceToGround / landingCheckDistance);
                ptC.transform.rotation = Quaternion.Slerp(ptC.transform.rotation, targetRotation, Time.deltaTime * landRotateSpeed * rotationMultiplier);

                // Check angle between current up and world up (perpendicular check)
                float angle = Vector3.Angle(ptC.transform.up, Vector3.up);
                Debug.Log($"Angle of Pilot: " + angle);

                // Trigger landing when close enough to ground and nearly upright
                bool closeToGround = distanceToGround < 0.8f;
                bool isNearlyUpright = angle < 15f;

                if (closeToGround && isNearlyUpright)
                {
                    isLanding = true;

                    // Snap to fully upright before transitioning
                    ptC.transform.rotation = targetRotation;

                    ptC.TriggerLanding(ptC.velocity.y);

                    // Transition to appropriate ground state
                    if (ptC.aimHeld)
                        ptC.ChangeState(ptC.AimWalkState);
                    else
                        ptC.ChangeState(ptC.FreeWalkState);
                }
            }
            else
            {
                Debug.DrawRay(ptC.controller.transform.position, Vector3.down * landingCheckDistance, Color.green);
            }
        }

        // Also check if grounded via controller
        if (ptC.isGrounded && stabilizeGraceTime <= 0f)
        {
            isLanding = true;

            // Snap to upright when landing via ground check
            Vector3 projectedForward = ptC.transform.forward;
            projectedForward.y = 0f;
            if (projectedForward.sqrMagnitude > 0.01f)
            {
                projectedForward.Normalize();
                ptC.transform.rotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            }

            ptC.TriggerLanding(ptC.velocity.y);

            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
        
    }
}
}
