using UnityEngine;

/// <summary>
/// Handles the jumping state.
/// Player transitions here from WalkState when jump is pressed.
/// Can transition to FlyState, HoverState, or FallState.
/// </summary>
public class JumpState : IPlayerState
{
    private readonly PlayerStateMachine _sm;
    private float _jumpTimer;
    private const float MIN_JUMP_TIME = 0.1f; // Minimum time before can transition out

    public JumpState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Apply jump force
        _sm.Velocity.y = _sm.JumpForce;
        _sm.JumpBufferCounter = 0f;
        _jumpTimer = 0f;

        // Set animator
        _sm.Animator.SetBool("IsJumping", true);
        _sm.Animator.SetBool("IsFalling", false);
        _sm.Animator.SetBool("IsFlying", false);
        _sm.Animator.SetBool("IsHovering", false);
    }

    public void Exit()
    {
        _sm.Animator.SetBool("IsJumping", false);
    }

    public void Tick()
    {
        _jumpTimer += Time.deltaTime;

        // Update animator
        _sm.UpdateLocomotionAnimator(0f, _sm.IsAimPressed);

        // Check state transitions (only after minimum jump time)
        if (_jumpTimer > MIN_JUMP_TIME)
        {
            CheckStateTransitions();
        }
    }

    public void FixedTick()
    {
        // Apply air control movement
        ApplyAirMovement();

        // Apply gravity
        _sm.Velocity.y += _sm.Gravity * Time.fixedDeltaTime;
        _sm.Controller.Move(_sm.Velocity * Time.fixedDeltaTime);
    }

    private void ApplyAirMovement()
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
            float airControlSpeed = _sm.MoveSpeed * 0.5f; // 50% air control
            _sm.Controller.Move(moveDir * airControlSpeed * Time.fixedDeltaTime);

            // Rotate towards movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            _sm.transform.rotation = Quaternion.RotateTowards(
                _sm.transform.rotation,
                targetRotation,
                _sm.TurnSpeed * 0.5f * Time.fixedDeltaTime
            );
        }
    }

    private void CheckStateTransitions()
    {
        // Landed: if grounded and velocity going down
        if (_sm.IsGrounded && _sm.Velocity.y <= 0)
        {
            _sm.ChangeState(_sm.WalkState);
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

        // Fall: if velocity going down (apex reached)
        if (_sm.Velocity.y < 0)
        {
            _sm.ChangeState(_sm.FallState);
            return;
        }
    }
}
