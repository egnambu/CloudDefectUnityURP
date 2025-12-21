using UnityEngine;
using Unity.Cinemachine;

public class PilotTypeControllerVariantF : MonoBehaviour
{
        [Header("Debug (Read Only)")]
    [SerializeField] private string currentStateName;
    public IPilotVFState currentState;
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
    public int aimLayerIndex = 2;

    [Header("Hover Settings")]
    public float hoverMaxDuration = 20f;
    public float hoverVelocityDecayRate = 15f; // How fast velocity decays to zero
    public float hoverBobAmplitude = 0.3f; // How much the character bobs up/down
    public float hoverBobFrequency = 1.5f; // Speed of the bob oscillation
    public float hoverMoveSpeed = 4f; // Horizontal movement speed while hovering
    public float hoverVerticalSpeed = 3f; // Vertical speed when using analog trigger
    [HideInInspector] public float hoverTimeRemaining;

    [Header("Flight Settings")]
    public float flightSpeed = 34f;
    public float flightAcceleration = 8f;
    public float pitchSpeed = 180f;
    public float yawSpeed = 180f;
    public float flightMaxDuration = 20f;
    public float flightControllerHeight = 0.2f;
    public Transform controllerPoint; // Optional: point to center the small collider on
    public Transform meshRoot; // For countering animation root motion during flight
    [HideInInspector] public float flightTimeRemaining;

    [HideInInspector] public Pilot1 input;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public Vector2 lookInput;
    [HideInInspector] public bool aimHeld;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool launchHeld;
    [HideInInspector] public bool hoverHeld;
    
    // Analog trigger values (0-1 range)
    [HideInInspector] public float launchTriggerValue; // R2 - flight speed control
    [HideInInspector] public float hoverTriggerValue;  // L2 - hover altitude control

    [HideInInspector] public float smoothMoveX;
    [HideInInspector] public float smoothMoveY;

    [HideInInspector] public float defaultHeight;
    [HideInInspector] public Vector3 defaultCenter;

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

        FreeWalkState = new FreeWalkState(this);
        AimWalkState = new AimWalkState(this);
        JumpPilotState = new JumpPilotState(this);
        FallPilotState = new FallPilotState(this);
        HoverPilotState = new HoverPilotState(this);
        FlightPilotState = new FlightPilotState(this);
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
    void OnDestroy() => input.Dispose();

    void Start()
    {
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


    public void ChangeState(IPilotVFState newState)
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
            animator.SetTrigger("HeavyLanding");
            landingCooldown = 1.25f; // Heavy landing recovery time
        }
        else
        {
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

public interface IPilotVFState
{
    void Enter();
    void Exit();
    void Tick();
    void FixedTick();
}

public class FreeWalkState : IPilotVFState
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
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);

        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.followCam);
        Debug.Log("Entered: FreeWalkState");
    }

    public void Tick()
    {
        // Transition to fall state if not grounded
        if (!ptC.isGrounded)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // During landing recovery - no movement, no state transitions
        if (ptC.landingCooldown > 0f)
        {
            // Force idle animation during landing
            ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, 0f, Time.deltaTime * ptC.animatorLerpRate);
            ptC.animator.SetFloat("MoveX", ptC.smoothMoveX);
            ptC.animator.SetFloat("MoveY", ptC.smoothMoveY);
            
            // Only apply gravity, no horizontal movement
            ptC.controller.Move(ptC.velocity * Time.deltaTime);
            return;
        }

        ptC.CheckStateTransitions();

        Vector3 moveDir = ptC.GetCameraRelativeMovement();

        isSprinting = ptC.launchHeld && ptC.isGrounded;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            ptC.transform.rotation = Quaternion.RotateTowards(
                ptC.transform.rotation,
                targetRotation,
                ptC.turnSpeed * Time.deltaTime
            );
        }

        if (isSprinting && ptC.moveInput.magnitude > 0.01f)
        {
            forwardSpeed = 1.5f;
        }
        else
        {
            forwardSpeed = ptC.moveInput.magnitude;
        }

        ptC.smoothMoveX = Mathf.Lerp(ptC.smoothMoveX, 0f, Time.deltaTime * ptC.animatorLerpRate);
        ptC.smoothMoveY = Mathf.Lerp(ptC.smoothMoveY, forwardSpeed, Time.deltaTime * ptC.animatorLerpRate);

        ptC.animator.SetFloat("MoveX", ptC.smoothMoveX);
        ptC.animator.SetFloat("MoveY", ptC.smoothMoveY);

        // Movement in Tick for smooth high-framerate rendering
        float currentSpeed = isSprinting ? ptC.sprintSpeed : ptC.moveSpeed;
        Vector3 horizontalMove = moveDir * currentSpeed * Time.deltaTime;
        Vector3 verticalMove = ptC.velocity * Time.deltaTime;
        ptC.controller.Move(horizontalMove + verticalMove);
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
        isSprinting = false;
    }

}


public class AimWalkState : IPilotVFState
{
    private readonly PilotTypeController ptC;

    public AimWalkState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        Debug.Log("Entered: AimWalkState");
        ptC.animator.SetBool("IsAiming", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);

        ptC.controller.height = ptC.defaultHeight;
        ptC.controller.center = ptC.defaultCenter;

        ptC.SetCameraPriority(ptC.aimCam);
    }

    public void Tick()
    {
        // Transition to fall state if not grounded
        if (!ptC.isGrounded)
        {
            ptC.ChangeState(ptC.FallPilotState);
            return;
        }

        // Declare camForward at method scope for reuse
        Vector3 camForward;

        // During landing recovery - no movement, no state transitions
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
            
            // Only apply gravity, no horizontal movement
            ptC.controller.Move(ptC.velocity * Time.deltaTime);
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
        ptC.controller.Move(horizontalMove + verticalMove);
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

public class JumpPilotState : IPilotVFState
{
    private readonly PilotTypeController ptC;
    private bool hasAppliedJumpForce;

    public JumpPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
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

public class FallPilotState : IPilotVFState
{
    private readonly PilotTypeController ptC;
    private float fallTime;

    public FallPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
        fallTime = 0f;
        Debug.Log("Entered: FallWalkState");
        ptC.animator.SetBool("IsFalling", true);
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetBool("IsHovering", false);

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

        // Check for landing - only after grace period and with downward velocity
        if (ptC.isGrounded && ptC.airborneGraceTime <= 0f && ptC.velocity.y <= 0f)
        {
            // Trigger landing animation before state change
            ptC.TriggerLanding(ptC.velocity.y);
            
            // Transition to appropriate ground state
            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
    }
}

public class HoverPilotState : IPilotVFState
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
        Debug.Log("Entered: HoverState");
        hoverBobTimer = 0f;
        hasStabilized = false;
        stabilizeGraceTime = 0.15f; // Small grace period on entry
        baseHoverHeight = ptC.transform.position.y;
        currentAimLayerWeight = ptC.animator.GetLayerWeight(1); // Get current weight

        ptC.animator.SetBool("IsHovering", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsAiming", false);
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

        // Check if velocity has stabilized (near zero)
        if (!hasStabilized && Mathf.Abs(ptC.velocity.y) < 0.5f)
        {
            hasStabilized = true;
            baseHoverHeight = ptC.transform.position.y;
        }

        // ANALOG TRIGGER ALTITUDE CONTROL (L2 for hover)
        // 0.55 to 0.85 = hover in place (with bob)
        // < 0.55 = descend
        // > 0.85 = ascend
        float triggerValue = ptC.hoverTriggerValue;
        float neutralZoneLow = 0.55f;
        float neutralZoneHigh = 0.85f;
        
        if (hasStabilized)
        {
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
        else
        {
            // Still stabilizing - decay vertical velocity
            ptC.velocity.y = Mathf.MoveTowards(ptC.velocity.y, 0f, decayFactor);
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
            // Exit if hover button released or time expired
            if (!ptC.hoverHeld || ptC.hoverTimeRemaining <= 0f)
            {
                ptC.ChangeState(ptC.FallPilotState);
                return;
            }

            // Exit if grounded (landed while hovering low)
            if (ptC.isGrounded && hasStabilized)
            {
                // Trigger soft landing since hover slows descent
                ptC.TriggerLanding(ptC.velocity.y);
                
                if (ptC.aimHeld)
                    ptC.ChangeState(ptC.AimWalkState);
                else
                    ptC.ChangeState(ptC.FreeWalkState);
                return;
            }
        }
    }
}

public class FlightPilotState : IPilotVFState
{
    private readonly PilotTypeController ptC;
    private float currentFlightSpeed;
    private float currentPitch;
    private float currentYaw;
    private float stabilizeGraceTime;
    private float landRotateSpeed = 6f;
    private float landingCheckDistance = 1.2f;
    private bool isLanding;

    public FlightPilotState(PilotTypeController controller)
    {
        ptC = controller;
    }

    public void Enter()
    {
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

        // Set animator states
        ptC.animator.SetBool("IsFlying", true);
        ptC.animator.SetBool("IsFalling", false);
        ptC.animator.SetBool("IsHovering", false);
        ptC.animator.SetBool("IsAiming", false);
        ptC.animator.SetTrigger("FlightStart");
        
        // Clear velocity - flight controls movement directly
        ptC.velocity = Vector3.zero;

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
        
        // PITCH & YAW CONTROL from input with speed-based resistance
        float targetPitch = ptC.moveInput.y * ptC.pitchSpeed * turnResistance;
        float targetYaw = ptC.moveInput.x * ptC.yawSpeed * turnResistance;

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
        float smoothX = Mathf.Lerp(ptC.animator.GetFloat("FlightX"), ptC.moveInput.x, Time.deltaTime * ptC.animatorLerpRate);
        float smoothY = Mathf.Lerp(ptC.animator.GetFloat("FlightY"), ptC.moveInput.y, Time.deltaTime * ptC.animatorLerpRate);
        smoothX = Mathf.Clamp(smoothX, -1f, 1f);
        smoothY = Mathf.Clamp(smoothY, -1f, 1f);
        ptC.animator.SetFloat("FlightX", smoothX);
        ptC.animator.SetFloat("FlightY", smoothY);
        //Debug.Log($"FlightX:"+ smoothX + " ,FlightY:" + smoothY );

        // Check for landing - use raycast to detect ground proximity
        CheckForLanding();

        // Counter animation root motion on meshRoot during flight
        if (ptC.meshRoot != null)
        {
            ptC.meshRoot.localPosition = Vector3.zero;
            ptC.meshRoot.localRotation = Quaternion.identity;
        }

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

                // Calculate target rotation: Character should rotate to be upright relative to ground
                Vector3 currentForward = ptC.transform.forward;
                Vector3 groundUp = hit.normal;

                // Project forward onto the ground plane to maintain facing direction
                Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, groundUp).normalized;
                if (projectedForward.sqrMagnitude < 0.01f)
                {
                    projectedForward = Vector3.ProjectOnPlane(ptC.transform.right, groundUp).normalized;
                }

                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundUp);

                // Rotate the main transform towards upright position
                ptC.transform.rotation = Quaternion.Slerp(ptC.transform.rotation, targetRotation, Time.deltaTime * landRotateSpeed);

                // Check angle between current up and target up
                float angle = Vector3.Angle(ptC.transform.up, groundUp);
                Debug.Log($"Angle of Pilot: " + angle);
                // Trigger landing when close enough to ground and nearly upright
                float distanceToGround = hit.distance;
                bool closeToGround = distanceToGround < 0.8f;
                bool isNearlyUpright = angle < 15f;

                if (closeToGround && isNearlyUpright)
                {
                    isLanding = true;
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
            ptC.TriggerLanding(ptC.velocity.y);

            if (ptC.aimHeld)
                ptC.ChangeState(ptC.AimWalkState);
            else
                ptC.ChangeState(ptC.FreeWalkState);
        }
    }
}
