using UnityEngine;

/// <summary>
/// Handles falling when not flying or hovering.
/// Player has limited air control and is affected by gravity.
/// Can transition to HoverState, FlyState, or LandState.
/// </summary>
public class FallState : IPlayerState
{
    private readonly PlayerStateMachine _sm;
    private const float GROUND_CHECK_DISTANCE = 0.6f;
    private const float LAND_ROTATE_SPEED = 6f;
    private const float AIR_CONTROL_MULTIPLIER = 0.5f;
    private bool _isPreparingToLand;

    public FallState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Set up controller for airborne state
        _sm.SetFlightControllerMode();
        _isPreparingToLand = false;

        // Set animator
        _sm.Animator.SetBool("IsFalling", true);
        _sm.Animator.SetBool("IsFlying", false);
        _sm.Animator.SetBool("IsHovering", false);
        _sm.Animator.SetBool("IsJumping", false);
    }

    public void Exit()
    {
        _sm.Animator.SetBool("IsFalling", false);
    }

    public void Tick()
    {
        // Update animator
        _sm.UpdateLocomotionAnimator(0f, _sm.IsAimPressed);

        // Check for landing
        CheckForLanding();

        // Check state transitions
        CheckStateTransitions();
    }

    public void FixedTick()
    {
        // Apply air control
        ApplyAirControl();

        // Apply gravity
        _sm.Velocity.y += _sm.Gravity * Time.fixedDeltaTime;
        _sm.Controller.Move(_sm.Velocity * Time.fixedDeltaTime);
    }

    private void ApplyAirControl()
    {
        Vector2 moveInput = _sm.MoveInput;
        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);

        // Get camera-relative directions
        Vector3 camForward = _sm.Cam.forward;
        Vector3 camRight = _sm.Cam.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Calculate move direction
        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        // Apply reduced air control
        if (moveDir.sqrMagnitude > 0.01f)
        {
            float airControlSpeed = _sm.MoveSpeed * AIR_CONTROL_MULTIPLIER;
            _sm.Controller.Move(moveDir * airControlSpeed * Time.fixedDeltaTime);

            // Rotate towards movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            _sm.transform.rotation = Quaternion.RotateTowards(
                _sm.transform.rotation,
                targetRotation,
                _sm.TurnSpeed * AIR_CONTROL_MULTIPLIER * Time.fixedDeltaTime
            );
        }
    }

    private void CheckForLanding()
    {
        RaycastHit hit;
        if (Physics.Raycast(
            _sm.Controller.transform.position, 
            Vector3.down, 
            out hit, 
            GROUND_CHECK_DISTANCE, 
            _sm.GroundLayerMask))
        {
            // Calculate target rotation for landing
            Vector3 currentForward = _sm.transform.forward;
            Vector3 groundUp = hit.normal;

            // Project forward onto the ground plane
            Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, groundUp).normalized;
            if (projectedForward.sqrMagnitude < 0.01f)
            {
                projectedForward = Vector3.ProjectOnPlane(_sm.transform.right, groundUp).normalized;
            }

            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundUp);

            // Rotate towards upright position
            _sm.transform.rotation = Quaternion.Slerp(
                _sm.transform.rotation, 
                targetRotation, 
                Time.deltaTime * LAND_ROTATE_SPEED
            );

            // Check if we're upright enough to land
            float angle = Vector3.Angle(_sm.transform.up, groundUp);
            if (angle < 8f)
            {
                _isPreparingToLand = true;
            }

            // Debug visualization
            Debug.DrawRay(_sm.Controller.transform.position, Vector3.down * GROUND_CHECK_DISTANCE, Color.red);
            Debug.DrawRay(hit.point, hit.normal * 0.6f, Color.yellow);
        }
        else
        {
            Debug.DrawRay(_sm.Controller.transform.position, Vector3.down * GROUND_CHECK_DISTANCE, Color.green);
        }
    }

    private void CheckStateTransitions()
    {
        // Landing: if close to ground and aligned
        if (_isPreparingToLand)
        {
            _sm.ChangeState(_sm.LandState);
            return;
        }

        // Flight: if launch pressed and has flight time
        if (_sm.IsLaunchPressed && !_sm.FlightExhausted && _sm.LandingCooldown <= 0f)
        {
            _sm.ChangeState(_sm.FlyState);
            return;
        }

        // Hover: if hover pressed and has hover time
        if (_sm.IsHoverPressed && !_sm.HoverExhausted)
        {
            _sm.ChangeState(_sm.HoverState);
            return;
        }

        // Grounded: direct landing (no animation transition needed)
        if (_sm.IsGrounded)
        {
            _sm.ChangeState(_sm.WalkState);
            return;
        }
    }
}
