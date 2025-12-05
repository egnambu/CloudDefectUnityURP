using UnityEngine;

/// <summary>
/// Handles grounded movement using WASD.
/// Player can walk, sprint, aim, and transition to jumping or flying.
/// </summary>
public class WalkState : IPlayerState
{
    private readonly PlayerStateMachine _sm;

    public WalkState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Reset flight timers when entering grounded state
        _sm.ResetFlightTimers();
        _sm.ResetControllerMode();

        // Clear any airborne animator states
        _sm.Animator.SetBool("IsFlying", false);
        _sm.Animator.SetBool("IsFalling", false);
        _sm.Animator.SetBool("IsHovering", false);
        _sm.Animator.SetBool("IsJumping", false);
        _sm.Animator.ResetTrigger("Landing");
    }

    public void Exit()
    {
        // Nothing special to clean up
    }

    public void Tick()
    {
        // Handle jump buffer
        if (_sm.IsJumpPressed)
        {
            _sm.JumpBufferCounter = _sm.JumpBufferTime;
        }
        else
        {
            _sm.JumpBufferCounter -= Time.deltaTime;
        }

        // Check for state transitions
        CheckStateTransitions();

        // Update animator
        float forwardSpeed = CalculateForwardSpeed();
        _sm.UpdateLocomotionAnimator(forwardSpeed, _sm.IsAimPressed);
    }

    public void FixedTick()
    {
        Move();
        ApplyGroundedGravity();
    }

    private void Move()
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

        // Calculate move direction relative to camera
        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        // Handle rotation
        if (_sm.IsAimPressed)
        {
            // When aiming, face camera direction
            if (camForward.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(camForward);
                _sm.transform.rotation = Quaternion.Slerp(
                    _sm.transform.rotation, 
                    targetRotation, 
                    Time.fixedDeltaTime * 10f
                );
            }
        }
        else
        {
            // When not aiming, face movement direction
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                _sm.transform.rotation = Quaternion.RotateTowards(
                    _sm.transform.rotation, 
                    targetRotation, 
                    _sm.TurnSpeed * Time.fixedDeltaTime
                );
            }
        }

        // Apply movement
        if (moveDir.sqrMagnitude > 0.01f)
        {
            bool isSprinting = _sm.IsLaunchPressed && _sm.LandingCooldown <= 0f;
            float currentSpeed = isSprinting ? _sm.SprintSpeed : _sm.MoveSpeed;
            _sm.Controller.Move(moveDir * currentSpeed * Time.fixedDeltaTime);
        }
    }

    private void ApplyGroundedGravity()
    {
        // Keep player grounded
        if (_sm.Velocity.y < 0)
        {
            _sm.Velocity.y = -2f;
        }

        _sm.Controller.Move(_sm.Velocity * Time.fixedDeltaTime);
    }

    private float CalculateForwardSpeed()
    {
        bool isSprinting = _sm.IsLaunchPressed && _sm.LandingCooldown <= 0f;

        if (isSprinting && _sm.MoveInput.magnitude > 0.01f)
        {
            return 1.5f; // Sprint blend value
        }
        
        return _sm.MoveInput.magnitude; // Normal walk blend value
    }

    private void CheckStateTransitions()
    {
        // Jump: if jump buffer active and grounded
        if (_sm.JumpBufferCounter > 0f && _sm.IsGrounded)
        {
            _sm.ChangeState(_sm.JumpState);
            return;
        }

        // Flight: if launch pressed and not grounded (edge case - walked off ledge while holding launch)
        if (!_sm.IsGrounded && _sm.IsLaunchPressed && _sm.LandingCooldown <= 0f)
        {
            _sm.ChangeState(_sm.FlyState);
            return;
        }

        // Fall: if not grounded and not trying to fly
        if (!_sm.IsGrounded)
        {
            _sm.ChangeState(_sm.FallState);
            return;
        }
    }
}
