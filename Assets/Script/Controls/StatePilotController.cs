using UnityEngine;
using Unity.Cinemachine;
using System;
using System.Collections;
using NUnit.Framework;

public class StatePilotController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 10f; // Add this for sprint speed
    public float turnSpeed = 720f;

    [Header("Flight")]
    public float flightSpeed = 34f;
    public float flightAcceleration = 8f;
    public float pitchSpeed = 180f;
    public float yawSpeed = 180f;
    public float flightGravity = -2f;
    public float maxFlightTime = 20f;
    public float maxHoverTime = 10f;

    [Header("Ground Check")]
    public LayerMask groundLayerMask;

    public CharacterController controller;
    public Transform cam;
    public CinemachineCamera followCam;
    public CinemachineCamera aimCam;
    public CinemachineCamera flightCam;
    public Animator animator;
    public Pilot1 input;
    public Vector2 moveInput;
    public Transform aimTarget;
    public float rotationSpeed = 400f;

    private bool isAiming;
    private bool isJumping;
    private bool isLaunching;
    private bool isHovering;

    [SerializeField] float jumpBufferTime = 0.1f;
    float jumpBufferCounter = 0f;
    private float forwardSpeed;

    // Smooth values
    private float smoothMoveX;
    private float smoothMoveY;
    private float aimBlendWeight = 0f;
    public float aimBlendSpeed = 12f;

    private Vector3 velocity;
    private object wasGrounded;
    [SerializeField] private bool grounded;
    private float gravity = -20f;

    // Flight state
    private float currentFlightSpeed = 0f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private bool isSprinting;
    public Transform meshRoot;
    //Used to recenter the controller for UE Flight Mannequins 
    public Transform controllerPoint;
    float landRotateSpeed = 6f;
    float defaultHeight;
    Vector3 defaultCenter;
    private bool isInFlightMode = false;
    private bool isLanding = false;
    private float landingCooldown = 0f;
    private float landingCooldownTime = 1f;
    
    // Flight/Hover timers
    private float currentFlightTime;
    private float currentHoverTime;
    private bool flightExhausted;
    private bool hoverExhausted;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
        animator = GetComponentInChildren<Animator>();
        input = new Pilot1();
    }

    void Start()
    {
        defaultHeight = controller.height;
        defaultCenter = controller.center;
        
        // Disable root motion - important for UE4 skeletons that may have residual root movement
        if (animator) animator.applyRootMotion = false;
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
    void OnDestroy() => input.Dispose();

    void Update()
    {
        moveInput = input.PlayerA.Move.ReadValue<Vector2>();
        isAiming = input.PlayerA.Aim.IsPressed();
        isJumping = input.PlayerA.Jump.IsPressed();
        isLaunching = input.PlayerA.Launch.IsPressed();
        isHovering = input.PlayerA.Hover.IsPressed();

        // Update landing cooldown
        if (landingCooldown > 0f)
        {
            landingCooldown -= Time.deltaTime;
        }

        if (input.PlayerA.Jump.IsPressed())
        {
            if (grounded)
                jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        SwitchToAimCam(isAiming || isLaunching);
        UpdateAnimator();
        UpdateAimLayer();

        // Check if in any airborne animation state that needs flight controller adjustments
        bool inFlightAnimation = animator.GetCurrentAnimatorStateInfo(1).IsName("Flight");
        bool inFallingAnimation = animator.GetCurrentAnimatorStateInfo(1).IsName("Falling") || animator.GetBool("IsFalling");
        
        if (inFlightAnimation || inFallingAnimation)
        {
            // Set flight mode controller dimensions only once
            if (!isInFlightMode)
            {
                controller.height = 0.2f;
                // Convert world position to local offset relative to the controller
                controller.center = transform.InverseTransformPoint(controllerPoint.position);
                isInFlightMode = true;
                isLanding = false;
                animator.ResetTrigger("Landing"); // Reset trigger on entering flight
            }

            RaycastHit hit;
            // Only check for landing if we're moving downward and not already landing
            // Check transform.forward.y for flight direction since velocity.y is cleared during flight
            bool movingDownward = velocity.y < 0 || transform.forward.y < 0;
            if (!isLanding && movingDownward && Physics.Raycast(controller.transform.position, Vector3.down, out hit, 0.6f, groundLayerMask))
            {
                Debug.DrawRay(controller.transform.position, Vector3.down * 2f, Color.red);
                Debug.DrawRay(hit.point, hit.normal * 0.6f, Color.yellow); // Visualize ground normal
                
                // Calculate target rotation: Character should rotate to be upright relative to ground
                // Keep current yaw (facing direction) but align up vector with ground normal
                Vector3 currentForward = transform.forward;
                Vector3 groundUp = hit.normal;
                
                // Project forward onto the ground plane to maintain facing direction
                Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, groundUp).normalized;
                if (projectedForward.sqrMagnitude < 0.01f)
                {
                    projectedForward = Vector3.ProjectOnPlane(transform.right, groundUp).normalized;
                }
                
                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundUp);
                
                // Rotate the main transform (not meshRoot) towards upright position
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * landRotateSpeed);
                
                // Check angle between current up and target up
                float angle = Vector3.Angle(transform.up, groundUp);
                Debug.Log($"Landing angle remaining: {angle:F1}° | Ground normal: {groundUp} | Transform up: {transform.up}");
                
                // Draw debug for current up vs target up
                Debug.DrawRay(transform.position, transform.up * 2f, Color.blue); // Current up
                Debug.DrawRay(transform.position, groundUp * 2f, Color.green); // Target up (ground normal)
                
                if (angle < 8f)
                {
                    // Reset controller dimensions when landing
                    controller.height = defaultHeight;
                    controller.center = defaultCenter;
                    isInFlightMode = false;
                    isLanding = true;
                    landingCooldown = landingCooldownTime;
                    animator.SetTrigger("Landing");
                    Debug.Log($"IsLanding True");
                }
            }
            else
            {
                Debug.DrawRay(controller.transform.position, Vector3.down * 2f, Color.green);
            }
            
            animator.SetBool("IsLanding", isLanding);
            Debug.Log($"meshRoot.rotation:" + meshRoot.rotation);
        }
        else
        {
            // Reset controller dimensions when exiting flight states
            if (isInFlightMode)
            {
                controller.height = defaultHeight;
                controller.center = defaultCenter;
                isInFlightMode = false;
            }
            isLanding = false;
            animator.ResetTrigger("Landing"); // Clear landing trigger when not in flight states
        }
    }

    void FixedUpdate()
    {
        // Update sprint state based on Launch button (only if not in cooldown)
        isSprinting = isLaunching && grounded && landingCooldown <= 0f;

        // Reset timers and triggers when grounded
        if (grounded)
        {
            currentFlightTime = 0f;
            currentHoverTime = 0f;
            flightExhausted = false;
            hoverExhausted = false;
            animator.ResetTrigger("Landing"); // Clear landing trigger when grounded
        }

        // Flight logic: only count time while actively flying
        bool hasFlightTime = currentFlightTime < maxFlightTime;
        bool wantsToFly = isLaunching && !grounded && landingCooldown <= 0f;

        if (wantsToFly && hasFlightTime)
        {
            // Only count flight time while actually flying
            currentFlightTime += Time.fixedDeltaTime;
            HandleFlight();
        }
        else if (!grounded && !hasFlightTime)
        {
            // Flight time exhausted - fall or hover
            flightExhausted = true;
            Move();
            ApplyGravity(); // Will hover if holding hover input
        }
        else
        {
            // Not flying - normal movement (flight time does NOT count down here)
            Move();
            ApplyGravity();
        }
    }

    void Move()
    {
        wasGrounded = grounded;
        grounded = controller.isGrounded;

        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);

        // Camera-relative movement
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        if (isAiming)
        {
            if (camForward.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(camForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        // Move horizontally with sprint speed if sprinting
        if (moveDir.sqrMagnitude > 0.01f)
        {
            float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
            controller.Move(moveDir * currentSpeed * Time.deltaTime);
        }
    }

    void ApplyGravity()
    {
        if (grounded)
        {
            if (velocity.y < 0)
                velocity.y = -2f;

            // Clear falling state when grounded
            animator.SetBool("IsFalling", false);
            animator.SetBool("IsHovering", false);

            if (jumpBufferCounter > 0f)
            {
                animator.SetBool("IsJumping", true);
                jumpBufferCounter = 0f;
                StartCoroutine(ResetJumpBool());
            }
        }
        else if (!grounded && isHovering && !hoverExhausted && currentHoverTime < maxHoverTime)
        {
            // Hovering - suspend in air
            currentHoverTime += Time.fixedDeltaTime;
            if (currentHoverTime >= maxHoverTime)
            {
                hoverExhausted = true;
            }
            velocity.y = 0f;
            animator.SetBool("IsHovering", true);
            animator.SetBool("IsFalling", false);
        }
        else
        {
            // Falling
            animator.SetBool("IsHovering", false);
            animator.SetBool("IsFalling", true);
            velocity.y += gravity * Time.fixedDeltaTime;
        }

        controller.Move(velocity * Time.fixedDeltaTime);
    }

    public void ApplyJumpForce()
    {
        velocity.y = 10f;
    }

    void HandleFlight()
    {
        // PITCH & YAW CONTROL
        float targetPitch = moveInput.y * pitchSpeed;
        float targetYaw = moveInput.x * yawSpeed;

        // Smooth rotation changes
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.fixedDeltaTime * 5f);
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.fixedDeltaTime * 5f);

        // Apply rotation to transform
        transform.Rotate(currentPitch * Time.fixedDeltaTime, currentYaw * Time.fixedDeltaTime, 0f, Space.Self);

        // FORWARD PROPULSION - accelerate to flight speed
        currentFlightSpeed = Mathf.MoveTowards(currentFlightSpeed, flightSpeed, flightAcceleration * Time.fixedDeltaTime);

        // Move forward in the character's facing direction
        // The pitch controls up/down via transform.forward - no additional gravity during active flight
        Vector3 forwardMovement = transform.forward * currentFlightSpeed;

        // Clear velocity.y to prevent gravity from before flight affecting us
        velocity.y = 0f;

        // Apply movement - flight direction is fully controlled by pitch
        controller.Move(forwardMovement * Time.fixedDeltaTime);

        // Reset speed if we hit ground
        if (controller.isGrounded)
        {
            currentFlightSpeed = 0f;
        }
    }

    // LateUpdate to counter any animation root motion on meshRoot during flight
    void LateUpdate()
    {
        if (meshRoot == null) return;
        
        if (isInFlightMode)
        {
            // Keep meshRoot aligned with transform during flight (prevents UE4 animation root jitter)
            meshRoot.localPosition = Vector3.zero;
            meshRoot.localRotation = Quaternion.identity; // Reset local rotation so transform rotation controls everything
        }
        else if (grounded && !isLanding)
        {
            // When grounded, ensure transform is upright
            Quaternion uprightRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, uprightRotation, Time.deltaTime * landRotateSpeed * 2f);
        }
    }

    void UpdateAnimator()
    {
        if (!animator) return;

        const float lerpRate = 12f;

        if (isAiming)
        {
            animator.SetBool("IsAiming", true);

            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 local = transform.InverseTransformDirection(
                cam.forward * inputDir.z + cam.right * inputDir.x
            );

            smoothMoveX = Mathf.Lerp(smoothMoveX, local.x, Time.deltaTime * lerpRate);
            smoothMoveY = Mathf.Lerp(smoothMoveY, local.z, Time.deltaTime * lerpRate);
            animator.SetFloat("AimX", smoothMoveX);
            animator.SetFloat("AimY", smoothMoveY);
        }
        else
        {
            animator.SetBool("IsAiming", false);
            
            // Flight animation: only if actively flying (has time and holding launch)
            bool activelyFlying = isLaunching && !grounded && !flightExhausted;
            animator.SetBool("IsFlying", activelyFlying);

            if (isLaunching)
            {

                // Flight animation
                forwardSpeed = 2.5f;
            }
            else if (isSprinting)
            {
                // Sprint animation - use 1.5f for sprint blend when moving
                // Only multiply by magnitude if there's input, otherwise keep it at base value
                if (moveInput.magnitude > 0.01f)
                {
                    forwardSpeed = 1.5f;
                }
                else
                {
                    forwardSpeed = 0f;
                }
            }
            else
            {
                // Normal walk animation
                forwardSpeed = moveInput.magnitude;
            }

            smoothMoveX = Mathf.Lerp(smoothMoveX, 0f, Time.deltaTime * lerpRate);
            smoothMoveY = Mathf.Lerp(smoothMoveY, forwardSpeed, Time.deltaTime * lerpRate);
            animator.SetFloat("MoveX", smoothMoveX);
            animator.SetFloat("MoveY", smoothMoveY);
        }
    }

    void SwitchToAimCam(bool aimingOrFlying)
    {
        // Cinemachine 3.x: Higher priority = active camera
        // Using larger gap between priorities for clearer transitions
        if (isLaunching && !grounded)
        {
            flightCam.Priority = 20;
            followCam.Priority = 10;
            aimCam.Priority = 10;
        }
        else if (isAiming)
        {
            aimCam.Priority = 20;
            followCam.Priority = 10;
            flightCam.Priority = 10;
        }
        else
        {
            followCam.Priority = 20;
            aimCam.Priority = 10;
            flightCam.Priority = 10;
        }
    }

    void UpdateAimLayer()
    {
        int aimLayerIndex = 2;
        float target = isAiming ? 1f : 0f;
        aimBlendWeight = Mathf.Lerp(aimBlendWeight, target, Time.deltaTime * aimBlendSpeed);
        animator.SetLayerWeight(aimLayerIndex, aimBlendWeight);
    }

    private IEnumerator ResetJumpBool()
    {
        yield return new WaitForSeconds(0.1f);
        animator.SetBool("IsJumping", false);
    }
}