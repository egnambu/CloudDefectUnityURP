using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FP_Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float baseSpeed = 7f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float crouchMultiplier = 0.5f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedCheckDistance = 0.5f;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool grounded;

    // Local Input States
    private Vector3 moveInput;
    private bool jumpPressed;
    private bool sprintHeld;
    private bool crouchHeld;
    private bool isAiming;

    [SerializeField] float jumpBufferTime = 0.1f;
    float jumpBufferCounter = 0f;
    private Animator animator;
    private TPSCameraManager cameraManager;

    // Smoothed animator values
    private float smoothMoveX;
    private float smoothMoveY;
    private float smoothSpeed;
    private Vector3 rawInput;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
        
        controller.center = new Vector3(0, 1f, 0);
        controller.height = 2f;
        controller.radius = 0.4f;

        animator = GetComponentInChildren<Animator>();
        cameraManager = FindFirstObjectByType<TPSCameraManager>();
        
        if (cameraManager == null)
        {
            Debug.LogWarning("[FP_Movement] TPSCameraManager not found in scene!");
        }
    }

    private void Update()
    {
        GatherInput();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        Simulate();
    }

    private void GatherInput()
    {
        var inputMgr = InputBindingManager.Instance;
        if (inputMgr == null) return;

        rawInput = Vector3.zero;

        // Movement keys
        if (inputMgr.GetAction(GameAction.MoveForward)) rawInput.z += 1;
        if (inputMgr.GetAction(GameAction.MoveBackward)) rawInput.z -= 1;
        if (inputMgr.GetAction(GameAction.MoveRight)) rawInput.x += 1;
        if (inputMgr.GetAction(GameAction.MoveLeft)) rawInput.x -= 1;

        moveInput = rawInput;
        if (moveInput.sqrMagnitude > 1) moveInput.Normalize();

        if (inputMgr.GetActionDown(GameAction.Jump))
        { jumpBufferCounter = jumpBufferTime; }
        else
        { jumpBufferCounter -= Time.deltaTime; }

        sprintHeld = inputMgr.GetAction(GameAction.Sprint);
        crouchHeld = inputMgr.GetAction(GameAction.Crouch);
        isAiming = inputMgr.GetAction(GameAction.AimDownSights);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Interpolate towards target values for smooth visual updates
        const float lerpRate = 12f;

        float currentSpeed = baseSpeed;
        if (sprintHeld && !crouchHeld) currentSpeed *= sprintMultiplier;
        if (crouchHeld) currentSpeed *= crouchMultiplier;

        float targetSpeed = currentSpeed / baseSpeed;
        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * lerpRate);

        smoothMoveX = Mathf.Lerp(smoothMoveX, rawInput.x * targetSpeed, Time.deltaTime * lerpRate);
        smoothMoveY = Mathf.Lerp(smoothMoveY, rawInput.z * targetSpeed, Time.deltaTime * lerpRate);

        animator.SetFloat("MoveX", smoothMoveX);
        animator.SetFloat("MoveY", smoothMoveY);
        animator.SetFloat("Speed", smoothSpeed);
    }

    private void Simulate()
    {
        // Ground check
        grounded = controller.isGrounded;
        float currentSpeed = baseSpeed;
        if (sprintHeld && !crouchHeld) currentSpeed *= sprintMultiplier;
        if (crouchHeld) currentSpeed *= crouchMultiplier;

        // Get camera directions for movement
        Vector3 cameraForward = cameraManager != null ? cameraManager.GetCameraForward() : Camera.main.transform.forward;
        Vector3 cameraRight = cameraManager != null ? cameraManager.GetCameraRight() : Camera.main.transform.right;
        
        cameraForward.y = 0;
        cameraForward.Normalize();
        cameraRight.y = 0;
        cameraRight.Normalize();

        Vector3 moveDir;
        if (isAiming)
        {
            // When aiming, strafe relative to camera
            moveDir = cameraForward * moveInput.z + cameraRight * moveInput.x;
            // Rotate character to face camera forward
            if (cameraForward.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(cameraForward), Time.fixedDeltaTime * 10f);
            }
        }
        else
        {
            // When not aiming, free movement and turn to face direction
            moveDir = cameraForward * moveInput.z + cameraRight * moveInput.x;
            if (moveDir.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.fixedDeltaTime * 10f);
            }
        }

        if (moveDir.sqrMagnitude > 1) moveDir.Normalize();
        Vector3 horizontalVelocity = moveDir * currentSpeed;
        
        // Gravity and Jump
        if (grounded)
        {
            if (velocity.y < 0)
                velocity.y = -2f; // small downward bias for sticking to ground

            if (jumpBufferCounter > 0f)
            {
                animator.SetBool("IsJumping", true);
                jumpBufferCounter = 0f;
                StartCoroutine(ResetJumpBool());
            }
                
        }
        else
        {
            velocity.y += gravity * Time.fixedDeltaTime;
        }

        // Final movement
        Vector3 totalMove = (horizontalVelocity + Vector3.up * velocity.y) * Time.fixedDeltaTime;
        controller.Move(totalMove);
    }

    private IEnumerator ResetJumpBool()
    {
        yield return new WaitForSeconds(0.1f);
        animator.SetBool("IsJumping", false);
    }

    public void ApplyJumpForce()
    {
        velocity.y = jumpForce;
    }
}