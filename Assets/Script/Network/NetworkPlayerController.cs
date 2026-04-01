using Fusion;
using UnityEngine;
using Unity.Cinemachine;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Enum-based state machine for the networked player controller.
    /// Only FreeWalk and AimWalk are included (no flight/hover/jump).
    /// </summary>
    public enum PlayerState : byte
    {
        FreeWalk = 0,
        AimWalk  = 1,
    }

    /// <summary>
    /// Networked player controller for Fusion 2.
    /// Extracts FreeWalk and AimWalk movement from the single-player PilotTypeController.
    ///
    /// All movement runs in FixedUpdateNetwork (deterministic, server-authoritative).
    /// Visual-only logic (camera, interpolated animation) runs in Render().
    ///
    /// Player Prefab Requirements:
    ///   - NetworkObject component
    ///   - CharacterController component
    ///   - Animator component
    ///   - This script (NetworkPlayerController)
    ///   - A child transform for ground checking (assign to groundCheckPoint)
    ///
    /// NOTE: Do NOT add NetworkTransform — this script handles position sync
    ///       via CharacterController.Move() running in FixedUpdateNetwork on the
    ///       state authority. Fusion re-simulates on the input authority for prediction.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        // ─── Networked State ────────────────────────────────────────────

        /// <summary>
        /// The current movement state, synced across all peers.
        /// OnChanged callback ensures remote players update their visuals.
        /// </summary>
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public PlayerState CurrentState { get; set; }

        /// <summary>Networked animator blend values for smooth remote interpolation.</summary>
        [Networked] public float NetSmoothMoveX { get; set; }
        [Networked] public float NetSmoothMoveY { get; set; }

        /// <summary>Networked flag: is the player currently sprinting?</summary>
        [Networked] public NetworkBool NetIsSprinting { get; set; }

        /// <summary>Networked vertical velocity for gravity.</summary>
        [Networked] public float NetVelocityY { get; set; }

        /// <summary>Networked grounded status.</summary>
        [Networked] public NetworkBool NetIsGrounded { get; set; }

        // ─── Inspector Settings ─────────────────────────────────────────

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

        [Header("Cinemachine Cameras (Local Player Only)")]
        [Tooltip("Leave empty — cameras are auto-discovered in the scene at runtime.")]
        public CinemachineCamera followCam;
        public CinemachineCamera aimCam;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // ─── Private / Cached ───────────────────────────────────────────

        private CharacterController _cc;
        private Animator _animator;
        private Transform _cam;
        private int _debugTickCount; // Track first N ticks for diagnostics

        // Local-only smoothing values (for Render interpolation)
        private float _renderMoveX;
        private float _renderMoveY;

        // ─── Animator parameter hashes (cached for performance) ─────────

        private static readonly int HashMoveX     = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY     = Animator.StringToHash("MoveY");
        private static readonly int HashAimWalkX  = Animator.StringToHash("AimWalkX");
        private static readonly int HashAimWalkY  = Animator.StringToHash("AimWalkY");
        private static readonly int HashIsAiming  = Animator.StringToHash("IsAiming");
        private static readonly int HashIsFalling = Animator.StringToHash("IsFalling");
        private static readonly int HashIsHovering = Animator.StringToHash("IsHovering");
        private static readonly int HashIsFlying  = Animator.StringToHash("IsFlying");
        private static readonly int HashFlightX   = Animator.StringToHash("FlightX");
        private static readonly int HashFlightY   = Animator.StringToHash("FlightY");
        private static readonly int HashHoverX    = Animator.StringToHash("HoverX");
        private static readonly int HashHoverY    = Animator.StringToHash("HoverY");

        // ─── Lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
        }

        public override void Spawned()
        {
            // Ensure components are valid (Awake may not fire on network-spawned objects)
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_animator == null) _animator = GetComponent<Animator>();

            if (_cc == null)
            {
                Debug.LogError($"[NetworkPlayerController] CharacterController NOT FOUND on {gameObject.name}! Player will not move.");
            }
            else
            {
                // Ensure the CharacterController is enabled
                _cc.enabled = true;
                LogDebug($"CC found — enabled:{_cc.enabled} height:{_cc.height} center:{_cc.center}");
            }

            // ── GroundCheckPoint: create fallback if not assigned on the prefab ──
            if (groundCheckPoint == null)
            {
                CreateFallbackGroundCheck();
            }

            // ── Warn if groundLayerMask is empty (Nothing) ──
            if (groundLayerMask.value == 0)
            {
                Debug.LogWarning("[NetworkPlayerController] groundLayerMask is set to Nothing! " +
                                 "Ground detection will never trigger. Assign the ground layer in the inspector.");
            }

            // ── Local player camera setup ──
            if (HasInputAuthority)
            {
                _cam = Camera.main?.transform;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                // Auto-find Cinemachine cameras in the scene (prefab refs are always null)
                AutoFindCameras();

                // Point the cameras at this player
                SetCameraTargets();

                SetCameraPriority(PlayerState.FreeWalk);
                Debug.Log($"[NetworkPlayerController] Local player spawned. " +
                          $"FollowCam={followCam?.name ?? "NULL"}, AimCam={aimCam?.name ?? "NULL"}");
            }
            else
            {
                LogDebug($"Remote player spawned: {Object.InputAuthority}");
            }

            // Initialize animator state
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                ResetAnimatorBools();
            }

            _debugTickCount = 0;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HasInputAuthority)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // ─── Fusion Tick (Deterministic Simulation) ─────────────────────

        public override void FixedUpdateNetwork()
        {
            // Only process if we can get input (state authority or input authority with prediction)
            if (!GetInput(out NetworkInputData input))
            {
                // Log periodically so the console is not flooded
                if (_debugTickCount < 5 || Runner.Tick % 200 == 0)
                {
                    LogDebug($"GetInput returned FALSE at tick {Runner.Tick}. " +
                             $"HasInputAuth={HasInputAuthority}, HasStateAuth={HasStateAuthority}");
                }
                return;
            }

            // Diagnostic: log first few successful ticks
            _debugTickCount++;
            if (_debugTickCount <= 3)
            {
                LogDebug($"GetInput OK tick={Runner.Tick} move={input.MoveDirection} camYaw={input.CameraYaw:F1} " +
                         $"aim={input.Buttons.IsSet(InputButton.Aim)} sprint={input.Buttons.IsSet(InputButton.Sprint)}");
            }

            // --- Ground check ---
            UpdateGroundedStatus();

            // --- Gravity ---
            ApplyGravity();

            // --- State transitions ---
            bool aimHeld = input.Buttons.IsSet(InputButton.Aim);
            bool sprintHeld = input.Buttons.IsSet(InputButton.Sprint);

            if (aimHeld && CurrentState != PlayerState.AimWalk)
            {
                ChangeState(PlayerState.AimWalk);
            }
            else if (!aimHeld && CurrentState != PlayerState.FreeWalk)
            {
                ChangeState(PlayerState.FreeWalk);
            }

            // --- Movement based on current state ---
            switch (CurrentState)
            {
                case PlayerState.FreeWalk:
                    TickFreeWalk(input, sprintHeld);
                    break;
                case PlayerState.AimWalk:
                    TickAimWalk(input);
                    break;
            }
        }

        // ─── Render (Visual Interpolation — runs every frame) ───────────

        public override void Render()
        {
            if (_animator == null) return;

            // Smoothly interpolate animator values towards networked values
            float dt = Time.deltaTime;

            switch (CurrentState)
            {
                case PlayerState.FreeWalk:
                    _renderMoveX = Mathf.Lerp(_renderMoveX, NetSmoothMoveX, dt * animatorLerpRate);
                    _renderMoveY = Mathf.Lerp(_renderMoveY, NetSmoothMoveY, dt * animatorLerpRate);
                    _animator.SetFloat(HashMoveX, _renderMoveX);
                    _animator.SetFloat(HashMoveY, _renderMoveY);
                    break;

                case PlayerState.AimWalk:
                    _renderMoveX = Mathf.Lerp(_renderMoveX, NetSmoothMoveX, dt * animatorLerpRate);
                    _renderMoveY = Mathf.Lerp(_renderMoveY, NetSmoothMoveY, dt * animatorLerpRate);
                    _animator.SetFloat(HashAimWalkX, _renderMoveX);
                    _animator.SetFloat(HashAimWalkY, _renderMoveY);
                    break;
            }

            // Update camera priority for local player
            if (HasInputAuthority)
            {
                SetCameraPriority(CurrentState);
            }
        }

        // ─── FreeWalk State Tick ────────────────────────────────────────

        private void TickFreeWalk(NetworkInputData input, bool sprintHeld)
        {
            Vector3 moveDir = GetCameraRelativeMovement(input);
            bool hasMoveInput = input.MoveDirection.sqrMagnitude > 0.01f;

            NetIsSprinting = sprintHeld && hasMoveInput && NetIsGrounded;

            // Rotation: face movement direction
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    turnSpeed * Runner.DeltaTime
                );
            }

            // Animator blend values
            float forwardSpeed = NetIsSprinting ? 1.5f : input.MoveDirection.magnitude;
            NetSmoothMoveX = Mathf.Lerp(NetSmoothMoveX, 0f, Runner.DeltaTime * animatorLerpRate);
            NetSmoothMoveY = Mathf.Lerp(NetSmoothMoveY, forwardSpeed, Runner.DeltaTime * animatorLerpRate);

            // Movement
            float currentSpeed = NetIsSprinting ? sprintSpeed : moveSpeed;
            Vector3 horizontalMove = moveDir * currentSpeed * Runner.DeltaTime;
            Vector3 verticalMove = new Vector3(0f, NetVelocityY, 0f) * Runner.DeltaTime;

            _cc.Move(horizontalMove + verticalMove);
        }

        // ─── AimWalk State Tick ─────────────────────────────────────────

        private void TickAimWalk(NetworkInputData input)
        {
            // Rotation: face camera forward (yaw only)
            float cameraYaw = input.CameraYaw;
            Vector3 camForward = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
            camForward.y = 0f;

            if (camForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(camForward.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Runner.DeltaTime * aimRotationSpeed
                );
            }

            // Compute local-space direction for strafing blend tree
            Vector3 inputDir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            Vector3 camRight = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;
            Vector3 worldDir = camForward * inputDir.z + camRight * inputDir.x;
            Vector3 localDir = transform.InverseTransformDirection(worldDir);

            NetSmoothMoveX = Mathf.Lerp(NetSmoothMoveX, localDir.x, Runner.DeltaTime * animatorLerpRate);
            NetSmoothMoveY = Mathf.Lerp(NetSmoothMoveY, localDir.z, Runner.DeltaTime * animatorLerpRate);

            // Movement
            Vector3 moveDir = GetCameraRelativeMovement(input);
            Vector3 horizontalMove = moveDir * moveSpeed * Runner.DeltaTime;
            Vector3 verticalMove = new Vector3(0f, NetVelocityY, 0f) * Runner.DeltaTime;

            _cc.Move(horizontalMove + verticalMove);
        }

        // ─── State Machine Helpers ──────────────────────────────────────

        private void ChangeState(PlayerState newState)
        {
            // Exit current state
            switch (CurrentState)
            {
                case PlayerState.AimWalk:
                    if (_animator != null) _animator.SetBool(HashIsAiming, false);
                    break;
            }

            CurrentState = newState;

            // Enter new state
            switch (newState)
            {
                case PlayerState.FreeWalk:
                    EnterFreeWalk();
                    break;
                case PlayerState.AimWalk:
                    EnterAimWalk();
                    break;
            }

            LogDebug($"State changed to: {newState}");
        }

        private void EnterFreeWalk()
        {
            ResetAnimatorBools();
            NetSmoothMoveX = 0f;
            NetSmoothMoveY = 0f;
            NetIsSprinting = false;
        }

        private void EnterAimWalk()
        {
            ResetAnimatorBools();
            if (_animator != null) _animator.SetBool(HashIsAiming, true);
            NetSmoothMoveX = 0f;
            NetSmoothMoveY = 0f;
        }

        /// <summary>Called on remote peers when CurrentState changes via [OnChangedRender].</summary>
        private void OnStateChanged()
        {
            switch (CurrentState)
            {
                case PlayerState.FreeWalk:
                    ResetAnimatorBools();
                    break;
                case PlayerState.AimWalk:
                    ResetAnimatorBools();
                    if (_animator != null) _animator.SetBool(HashIsAiming, true);
                    break;
            }
        }

        // ─── Camera Utilities (Local Player Only) ───────────────────────

        /// <summary>
        /// Finds Cinemachine cameras that exist in the scene.
        /// Prefab serialized refs are always null because prefabs can't reference scene objects.
        /// </summary>
        private void AutoFindCameras()
        {
            if (followCam != null && aimCam != null) return; // Already assigned

            var allCams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

            foreach (var cam in allCams)
            {
                string camName = cam.gameObject.name.ToLowerInvariant();
                if (followCam == null && (camName.Contains("follow") || camName.Contains("free") || camName.Contains("third")))
                {
                    followCam = cam;
                }
                else if (aimCam == null && camName.Contains("aim"))
                {
                    aimCam = cam;
                }
            }

            // Fallback: assign by discovery order if name matching failed
            if (followCam == null && allCams.Length > 0) followCam = allCams[0];
            if (aimCam == null && allCams.Length > 1) aimCam = allCams[1];

            if (followCam == null || aimCam == null)
            {
                Debug.LogWarning($"[NetworkPlayerController] Could not auto-find all cameras. " +
                                 $"Found: Follow={followCam?.name ?? "NULL"}, Aim={aimCam?.name ?? "NULL"}. " +
                                 $"Total CinemachineCameras in scene: {allCams.Length}");
            }
        }

        /// <summary>
        /// Points the discovered cameras at this player so they follow/look at the local player.
        /// Uses CM3 API: setting Follow sets Target.TrackingTarget internally.
        /// </summary>
        private void SetCameraTargets()
        {
            if (followCam != null)
            {
                followCam.Follow = transform;
                followCam.LookAt = transform;
                LogDebug($"Follow camera '{followCam.name}' now tracking {gameObject.name}");
            }

            if (aimCam != null)
            {
                aimCam.Follow = transform;
                aimCam.LookAt = transform;
                LogDebug($"Aim camera '{aimCam.name}' now tracking {gameObject.name}");
            }
        }

        // ─── Ground Check Utilities ─────────────────────────────────────

        /// <summary>
        /// Creates a child GameObject at the bottom of the CharacterController to use for ground detection.
        /// Called automatically if groundCheckPoint is not assigned on the prefab.
        /// </summary>
        private void CreateFallbackGroundCheck()
        {
            var go = new GameObject("GroundCheckPoint_Auto");
            go.transform.SetParent(transform);

            if (_cc != null)
            {
                float bottomY = _cc.center.y - (_cc.height * 0.5f);
                go.transform.localPosition = new Vector3(0f, bottomY + 0.05f, 0f);
            }
            else
            {
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            groundCheckPoint = go.transform;
            Debug.LogWarning("[NetworkPlayerController] groundCheckPoint was not assigned — created fallback at CharacterController bottom.");
        }

        // ─── Shared Utilities ───────────────────────────────────────────

        private void ResetAnimatorBools()
        {
            if (_animator == null) return;
            _animator.SetBool(HashIsAiming, false);
            _animator.SetBool(HashIsFalling, false);
            _animator.SetBool(HashIsHovering, false);
            _animator.SetBool(HashIsFlying, false);

            // Reset blend parameters from other states
            _animator.SetFloat(HashFlightX, 0f);
            _animator.SetFloat(HashFlightY, 0f);
            _animator.SetFloat(HashHoverX, 0f);
            _animator.SetFloat(HashHoverY, 0f);
        }

        private void UpdateGroundedStatus()
        {
            // Fallback: if groundCheckPoint is somehow still null, use transform base
            Vector3 checkPos;
            if (groundCheckPoint != null)
            {
                checkPos = groundCheckPoint.position;
            }
            else if (_cc != null)
            {
                // Use the bottom of the character controller
                checkPos = transform.position + _cc.center + Vector3.down * (_cc.height * 0.5f - 0.05f);
            }
            else
            {
                checkPos = transform.position + Vector3.up * 0.05f;
            }

            NetIsGrounded = Physics.CheckSphere(
                checkPos,
                groundCheckRadius,
                groundLayerMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private void ApplyGravity()
        {
            if (NetIsGrounded)
            {
                if (NetVelocityY < 0f)
                    NetVelocityY = -2f; // Small downward force to keep grounded
            }
            else
            {
                NetVelocityY += gravity * Runner.DeltaTime;
            }
        }

        /// <summary>
        /// Converts 2D input into a world-space horizontal direction relative to the camera yaw.
        /// Uses the camera yaw from the input struct so it's deterministic across all peers.
        /// </summary>
        private Vector3 GetCameraRelativeMovement(NetworkInputData input)
        {
            Vector3 inputDir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            if (inputDir.sqrMagnitude < 0.001f) return Vector3.zero;

            Quaternion yawRotation = Quaternion.Euler(0f, input.CameraYaw, 0f);
            Vector3 camForward = yawRotation * Vector3.forward;
            Vector3 camRight = yawRotation * Vector3.right;

            return (camForward * inputDir.z + camRight * inputDir.x).normalized
                   * inputDir.magnitude;
        }

        private void SetCameraPriority(PlayerState state)
        {
            if (followCam == null || aimCam == null) return;

            switch (state)
            {
                case PlayerState.FreeWalk:
                    followCam.Priority = 20;
                    aimCam.Priority = 10;
                    break;
                case PlayerState.AimWalk:
                    followCam.Priority = 10;
                    aimCam.Priority = 20;
                    break;
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkPlayerController] {message}");
        }

        // ─── Gizmos ────────────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = NetIsGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}
