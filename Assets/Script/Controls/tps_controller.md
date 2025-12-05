using UnityEngine;
using Unity.Cinemachine;
public class TPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 720f;

    public CharacterController controller;
    public Transform cam;
    public CinemachineCamera followCam;
    public CinemachineCamera aimCam;
    public Animator animator;
    public Pilot1 input;
    public Vector2 moveInput;
    public Transform aimPivot;
    public Transform aimTarget;
    public float rotationSpeed = 400f;
    private bool isAiming;

    // Smooth values
    private float smoothMoveX;
    private float smoothMoveY;
    private float aimBlendWeight = 0f;

    // Adjust aim blend speed (8–14 recommended)
    public float aimBlendSpeed = 12f;
    private float currentAngle;
    private Vector3 aimPoint;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
        animator = GetComponentInChildren<Animator>();

        input = new Pilot1();
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void OnDestroy()
    {
        input.Dispose();
    }

    void Update()
    {
        moveInput = input.PlayerA.Move.ReadValue<Vector2>();
        isAiming = input.PlayerA.Aim.IsPressed();
        SwitchToAimCam(isAiming);
        Move();
        UpdateAnimator();
        UpdateAimLayer();
    }

    void Move()
    {
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
            // Turn toward camera forward
            if (camForward.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(camForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            // Turn toward movement direction
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        // Move
        if (moveDir.sqrMagnitude > 0.01f)
        {
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }
    void LateUpdate()
    {
        // Base aim point
        
        if (isAiming)
        {
            aimPoint = aimTarget.position + aimTarget.forward * 50f;
        }
        else
        {
            aimPoint = cam.position + cam.forward * 50f;
        }
        Vector3 dir = (aimPoint - aimPivot.position).normalized;

        // Stabilize torso twist
        Vector3 bodyForward = aimPivot.parent.forward;
        float targetAngle = Vector3.SignedAngle(bodyForward, dir, Vector3.up);
        targetAngle = Mathf.Clamp(targetAngle, -60f, 60f);

        // For non-aiming, only rotate if the angle exceeds -20f to 20f
        if (!isAiming && Mathf.Abs(targetAngle) <= 20f)
        {
            targetAngle = 0f;
        }

        // Ease into the rotation: Smoothly lerp the current angle toward the target
        if (isAiming)
        {
            currentAngle = targetAngle;  // No lerp for aiming
        }
        else
        {
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Time.deltaTime * rotationSpeed);
        }


        // Apply the angle to the direction
        dir = Quaternion.AngleAxis(currentAngle, Vector3.up) * bodyForward;

        // Rotate upper body (now using the eased direction)
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        aimPivot.rotation = targetRot;  // Direct assignment since we're already smoothing the angle

        // Debug visualization: Draw lines
        Debug.DrawLine(cam.position, aimPoint, Color.red);  // Line from camera to aim point
        Debug.DrawLine(aimPivot.position, aimPoint, Color.blue);  // Line from aim pivot to aim point (representing look direction)
    }

    void UpdateAnimator()
    {
        if (!animator) return;

        const float lerpRate = 12f;

        if (isAiming)
        {
            animator.SetBool("IsAiming", true);

            // Convert camera-relative input to LOCAL movement axes
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 local = transform.InverseTransformDirection(
                cam.forward * inputDir.z + cam.right * inputDir.x
            );

            // Strafe = X , Forward/Back = Z
            smoothMoveX = Mathf.Lerp(smoothMoveX, local.x, Time.deltaTime * lerpRate);
            smoothMoveY = Mathf.Lerp(smoothMoveY, local.z, Time.deltaTime * lerpRate);
        }
        else
        {
            animator.SetBool("IsAiming", false);

            float forwardSpeed = moveInput.magnitude;
            smoothMoveX = Mathf.Lerp(smoothMoveX, 0f, Time.deltaTime * lerpRate);
            smoothMoveY = Mathf.Lerp(smoothMoveY, forwardSpeed, Time.deltaTime * lerpRate);
        }

        animator.SetFloat("MoveX", smoothMoveX);
        animator.SetFloat("MoveY", smoothMoveY);
    }

    void SwitchToAimCam(bool aiming)
    {
        if (aiming)
        {
            followCam.Priority = 0;
            aimCam.Priority = 10;
        }
        else
        {
            followCam.Priority = 10;
            aimCam.Priority = 0;
        }
    }

    void UpdateAimLayer()
    {
        // Aim layer is index 2 in your controller (Base=0, Movement=1, Aim=2)
        int aimLayerIndex = 2;

        float target = isAiming ? 1f : 0f;

        aimBlendWeight = Mathf.Lerp(aimBlendWeight, target, Time.deltaTime * aimBlendSpeed);

        animator.SetLayerWeight(aimLayerIndex, aimBlendWeight);
    }
}
