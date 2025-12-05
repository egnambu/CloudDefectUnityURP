using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Main state machine that manages player states and provides shared resources.
/// All states receive a reference to this class to access controllers, cameras, and input.
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    #region Components & References
    [Header("Core Components")]
    public CharacterController Controller;
    public Animator Animator;
    public Transform Cam;

    [Header("Cinemachine Cameras")]
    public CinemachineCamera FollowCam;
    public CinemachineCamera AimCam;
    public CinemachineCamera FlightCam;

    [Header("Transform References")]
    public Transform MeshRoot;
    public Transform ControllerPoint;
    public Transform AimTarget;
    #endregion

    #region Movement Settings
    [Header("Ground Movement")]
    public float MoveSpeed = 5f;
    public float SprintSpeed = 10f;
    public float TurnSpeed = 720f;
    public float RotationSpeed = 400f;

    [Header("Jump Settings")]
    public float JumpForce = 10f;
    public float JumpBufferTime = 0.1f;

    [Header("Flight Settings")]
    public float FlightSpeed = 34f;
    public float FlightAcceleration = 8f;
    public float PitchSpeed = 180f;
    public float YawSpeed = 180f;
    public float MaxFlightTime = 20f;

    [Header("Hover Settings")]
    public float MaxHoverTime = 10f;

    [Header("Physics")]
    public float Gravity = -20f;
    public LayerMask GroundLayerMask;
    #endregion

    #region Input System
    public Pilot1 Input;
    public Vector2 MoveInput;
    public bool IsAimPressed;
    public bool IsJumpPressed;
    public bool IsLaunchPressed;
    public bool IsHoverPressed;
    #endregion

    #region State Machine
    [Header("Debug")]
    [SerializeField] private string _currentStateName;
    
    private IPlayerState _currentState;
    
    // All available states
    public WalkState WalkState { get; private set; }
    public JumpState JumpState { get; private set; }
    public FlyState FlyState { get; private set; }
    public HoverState HoverState { get; private set; }
    public FallState FallState { get; private set; }
    public LandState LandState { get; private set; }
    #endregion

    #region Shared State Data
    public Vector3 Velocity;
    public bool IsGrounded;
    public float JumpBufferCounter;
    public float CurrentFlightTime;
    public float CurrentHoverTime;
    public bool FlightExhausted;
    public bool HoverExhausted;
    public float LandingCooldown;
    public float CurrentFlightSpeed;
    public LayerMask groundLayerMask;
    public float groundCheckRadius = 0.3f;
    public float groundCheckDistance = 0.3f;
    public Transform groundCheckPoint;
    public float CurrentPitch;
    public float CurrentYaw;
    public bool IsInFlightMode;

    // Controller default values (saved on start)
    public float DefaultControllerHeight;
    public Vector3 DefaultControllerCenter;

    // Animator blend values
    private float _smoothMoveX;
    private float _smoothMoveY;
    private float _aimBlendWeight;
    public float AimBlendSpeed = 12f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Get components
        Controller = GetComponent<CharacterController>();
        Cam = Camera.main.transform;
        Animator = GetComponentInChildren<Animator>();
        
        // Initialize input
        Input = new Pilot1();

        // Create all states (pass reference to this state machine)
        WalkState = new WalkState(this);
        JumpState = new JumpState(this);
        FlyState = new FlyState(this);
        HoverState = new HoverState(this);
        FallState = new FallState(this);
        LandState = new LandState(this);
    }

    private void Start()
    {
        // Save default controller values
        DefaultControllerHeight = Controller.height;
        DefaultControllerCenter = Controller.center;

        // Disable root motion - important for UE4 skeletons
        if (Animator != null)
        {
            Animator.applyRootMotion = false;
        }

        // Start in Walk state
        ChangeState(WalkState);
    }

    private void OnEnable() => Input.Enable();
    private void OnDisable() => Input.Disable();
    private void OnDestroy() => Input.Dispose();

    private void Update()
    {
        ReadInput();
        UpdateGroundedStatus();
        
        // Let current state handle Update logic
        _currentState?.Tick();

        // Update animator layers and cameras
        UpdateAimLayer();
        UpdateCameras();
    }

    private void FixedUpdate()
    {
        // Let current state handle FixedUpdate logic
        _currentState?.FixedTick();
    }

    private void LateUpdate()
    {
        // Handle mesh root alignment during flight
        if (MeshRoot == null) return;

        if (IsInFlightMode)
        {
            // Keep meshRoot aligned with transform during flight
            MeshRoot.localPosition = Vector3.zero;
            MeshRoot.localRotation = Quaternion.identity;
        }
        else if (IsGrounded)
        {
            // When grounded, ensure transform is upright
            Quaternion uprightRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, uprightRotation, Time.deltaTime * 12f);
        }
    }
    #endregion

    #region State Management
    /// <summary>
    /// Changes to a new state. Calls Exit on current state and Enter on new state.
    /// </summary>
    public void ChangeState(IPlayerState newState)
    {
        if (newState == null) return;

        // Exit current state
        _currentState?.Exit();

        // Switch to new state
        _currentState = newState;
        _currentStateName = newState.GetType().Name;

        // Enter new state
        _currentState.Enter();
    }
    #endregion

    #region Input & Ground Check
    private void ReadInput()
    {
        MoveInput = Input.PlayerA.Move.ReadValue<Vector2>();
        IsAimPressed = Input.PlayerA.Aim.IsPressed();
        IsJumpPressed = Input.PlayerA.Jump.IsPressed();
        IsLaunchPressed = Input.PlayerA.Launch.IsPressed();
        IsHoverPressed = Input.PlayerA.Hover.IsPressed();
    }

    private void UpdateGroundedStatus()
    {
        IsGrounded = Physics.CheckSphere(
            groundCheckPoint.position,
            0.3f,
            groundLayerMask,
            QueryTriggerInteraction.Ignore
        );

        // Decrease landing cooldown
        if (LandingCooldown > 0f)
        {
            LandingCooldown -= Time.deltaTime;
        }
    }
    #endregion

    #region Animator Helpers
    /// <summary>
    /// Updates animator parameters for locomotion.
    /// Call this from states that need locomotion animation.
    /// </summary>
    public void UpdateLocomotionAnimator(float forwardSpeed, bool isAiming)
    {
        if (Animator == null) return;

        const float lerpRate = 12f;

        if (isAiming)
        {
            Animator.SetBool("IsAiming", true);

            Vector3 inputDir = new Vector3(MoveInput.x, 0f, MoveInput.y);
            Vector3 local = transform.InverseTransformDirection(
                Cam.forward * inputDir.z + Cam.right * inputDir.x
            );

            _smoothMoveX = Mathf.Lerp(_smoothMoveX, local.x, Time.deltaTime * lerpRate);
            _smoothMoveY = Mathf.Lerp(_smoothMoveY, local.z, Time.deltaTime * lerpRate);
            Animator.SetFloat("AimX", _smoothMoveX);
            Animator.SetFloat("AimY", _smoothMoveY);
        }
        else
        {
            Animator.SetBool("IsAiming", false);

            _smoothMoveX = Mathf.Lerp(_smoothMoveX, 0f, Time.deltaTime * lerpRate);
            _smoothMoveY = Mathf.Lerp(_smoothMoveY, forwardSpeed, Time.deltaTime * lerpRate);
            Animator.SetFloat("MoveX", _smoothMoveX);
            Animator.SetFloat("MoveY", _smoothMoveY);
        }
    }

    private void UpdateAimLayer()
    {
        if (Animator == null) return;

        int aimLayerIndex = 2;
        float target = IsAimPressed ? 1f : 0f;
        _aimBlendWeight = Mathf.Lerp(_aimBlendWeight, target, Time.deltaTime * AimBlendSpeed);
        Animator.SetLayerWeight(aimLayerIndex, _aimBlendWeight);
    }

    private void UpdateCameras()
    {
        // Cinemachine 3.x: Higher priority = active camera
        if (IsInFlightMode)
        {
            FlightCam.Priority = 20;
            FollowCam.Priority = 10;
            AimCam.Priority = 10;
        }
        else if (IsAimPressed)
        {
            AimCam.Priority = 20;
            FollowCam.Priority = 10;
            FlightCam.Priority = 10;
        }
        else
        {
            FollowCam.Priority = 20;
            AimCam.Priority = 10;
            FlightCam.Priority = 10;
        }
    }
    #endregion

    #region Controller Helpers
    /// <summary>
    /// Sets the character controller to flight mode dimensions.
    /// </summary>
    public void SetFlightControllerMode()
    {
        if (ControllerPoint != null)
        {
            Controller.height = 0.2f;
            Controller.center = transform.InverseTransformPoint(ControllerPoint.position);
        }
        IsInFlightMode = true;
    }

    /// <summary>
    /// Resets the character controller to default dimensions.
    /// </summary>
    public void ResetControllerMode()
    {
        Controller.height = DefaultControllerHeight;
        Controller.center = DefaultControllerCenter;
        IsInFlightMode = false;
    }

    /// <summary>
    /// Resets all flight and hover timers. Call when grounded.
    /// </summary>
    public void ResetFlightTimers()
    {
        CurrentFlightTime = 0f;
        CurrentHoverTime = 0f;
        FlightExhausted = false;
        HoverExhausted = false;
        CurrentFlightSpeed = 0f;
        CurrentPitch = 0f;
        CurrentYaw = 0f;
    }
    #endregion
}
