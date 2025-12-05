using UnityEngine;

/// <summary>
/// Handles free-flight movement with WASD for pitch/yaw control.
/// Player moves forward automatically, using input to steer.
/// Transitions to FallState when flight time is exhausted or launch released.
/// </summary>
public class FlyState : IPlayerState
{
    private readonly PlayerStateMachine _sm;
    private const float LAND_ROTATE_SPEED = 6f;
    private const float GROUND_CHECK_DISTANCE = 0.6f;
    private bool _isPreparingToLand;

    public FlyState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Set up controller for flight
        _sm.SetFlightControllerMode();
        _isPreparingToLand = false;

        // Clear vertical velocity to prevent gravity carryover
        _sm.Velocity.y = 0f;

        // Set animator
        _sm.Animator.SetBool("IsFlying", true);
        _sm.Animator.SetBool("IsFalling", false);
        _sm.Animator.SetBool("IsHovering", false);
        _sm.Animator.SetBool("IsJumping", false);
        _sm.Animator.ResetTrigger("Landing");
    }

    public void Exit()
    {
        _sm.Animator.SetBool("IsFlying", false);
    }

    public void Tick()
    {
        // Update animator with flight blend value
        _sm.UpdateLocomotionAnimator(2.5f, false);

        // Check for landing
        CheckForLanding();

        // Check state transitions
        CheckStateTransitions();
    }

    public void FixedTick()
    {
        // Only fly if we have flight time remaining
        if (_sm.CurrentFlightTime < _sm.MaxFlightTime)
        {
            _sm.CurrentFlightTime += Time.fixedDeltaTime;
            HandleFlight();
        }
        else
        {
            // Flight exhausted
            _sm.FlightExhausted = true;
        }
    }

    private void HandleFlight()
    {
        Vector2 moveInput = _sm.MoveInput;

        // PITCH & YAW CONTROL
        float targetPitch = moveInput.y * _sm.PitchSpeed;
        float targetYaw = moveInput.x * _sm.YawSpeed;

        // Smooth rotation changes
        _sm.CurrentPitch = Mathf.Lerp(_sm.CurrentPitch, targetPitch, Time.fixedDeltaTime * 5f);
        _sm.CurrentYaw = Mathf.Lerp(_sm.CurrentYaw, targetYaw, Time.fixedDeltaTime * 5f);

        // Apply rotation to transform
        _sm.transform.Rotate(
            _sm.CurrentPitch * Time.fixedDeltaTime, 
            _sm.CurrentYaw * Time.fixedDeltaTime, 
            0f, 
            Space.Self
        );

        // FORWARD PROPULSION - accelerate to flight speed
        _sm.CurrentFlightSpeed = Mathf.MoveTowards(
            _sm.CurrentFlightSpeed, 
            _sm.FlightSpeed, 
            _sm.FlightAcceleration * Time.fixedDeltaTime
        );

        // Move forward in the character's facing direction
        Vector3 forwardMovement = _sm.transform.forward * _sm.CurrentFlightSpeed;

        // Apply movement
        _sm.Controller.Move(forwardMovement * Time.fixedDeltaTime);

        // Reset speed if we hit ground
        if (_sm.Controller.isGrounded)
        {
            _sm.CurrentFlightSpeed = 0f;
        }
    }

    private void CheckForLanding()
    {
        // Only check for landing if moving downward
        bool movingDownward = _sm.Velocity.y < 0 || _sm.transform.forward.y < 0;
        
        if (!movingDownward) return;

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

        // Flight exhausted: transition to fall or hover
        if (_sm.FlightExhausted)
        {
            if (_sm.IsHoverPressed && !_sm.HoverExhausted)
            {
                _sm.ChangeState(_sm.HoverState);
            }
            else
            {
                _sm.ChangeState(_sm.FallState);
            }
            return;
        }

        // Launch released: transition to fall or hover
        if (!_sm.IsLaunchPressed)
        {
            if (_sm.IsHoverPressed && !_sm.HoverExhausted)
            {
                _sm.ChangeState(_sm.HoverState);
            }
            else
            {
                _sm.ChangeState(_sm.FallState);
            }
            return;
        }

        // Grounded unexpectedly
        if (_sm.IsGrounded)
        {
            _sm.ChangeState(_sm.WalkState);
            return;
        }
    }
}
