using UnityEngine;

/// <summary>
/// Handles hovering in place with WASD for horizontal movement.
/// Space and LeftCtrl can be used for vertical adjustment.
/// Player stays suspended in air while hover time remains.
/// </summary>
public class HoverState : IPlayerState
{
    private readonly PlayerStateMachine _sm;
    private const float HOVER_MOVE_SPEED_MULTIPLIER = 0.7f; // Slower movement while hovering

    public HoverState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Set up controller for hover (same as flight)
        _sm.SetFlightControllerMode();

        // Clear velocity to suspend in air
        _sm.Velocity = Vector3.zero;

        // Set animator
        _sm.Animator.SetBool("IsHovering", true);
        _sm.Animator.SetBool("IsFlying", false);
        _sm.Animator.SetBool("IsFalling", false);
        _sm.Animator.SetBool("IsJumping", false);
    }

    public void Exit()
    {
        _sm.Animator.SetBool("IsHovering", false);
    }

    public void Tick()
    {
        // Update animator
        _sm.UpdateLocomotionAnimator(0f, _sm.IsAimPressed);

        // Check state transitions
        CheckStateTransitions();
    }

    public void FixedTick()
    {
        // Count hover time
        _sm.CurrentHoverTime += Time.fixedDeltaTime;

        // Check if hover exhausted
        if (_sm.CurrentHoverTime >= _sm.MaxHoverTime)
        {
            _sm.HoverExhausted = true;
            return; // Will transition in Tick()
        }

        // Handle hover movement
        HandleHoverMovement();
    }

    private void HandleHoverMovement()
    {
        Vector2 moveInput = _sm.MoveInput;

        // Get camera-relative directions
        Vector3 camForward = _sm.Cam.forward;
        Vector3 camRight = _sm.Cam.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Calculate horizontal movement direction
        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;

        // Handle vertical input
        float verticalInput = 0f;
        
        // Check for vertical input (Jump = up, we could add crouch for down)
        if (_sm.IsJumpPressed)
        {
            verticalInput = 1f; // Move up
        }
        // Note: For LeftCtrl (down), you'd need to add that to your input system
        // For now, we'll just handle up movement

        // Apply movement direction with vertical
        moveDir.y = verticalInput;

        // Rotate towards horizontal movement direction (ignore vertical for rotation)
        Vector3 horizontalDir = new Vector3(moveDir.x, 0, moveDir.z);
        if (horizontalDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDir);
            _sm.transform.rotation = Quaternion.RotateTowards(
                _sm.transform.rotation,
                targetRotation,
                _sm.TurnSpeed * 0.5f * Time.fixedDeltaTime
            );
        }

        // Apply movement
        if (moveDir.sqrMagnitude > 0.01f)
        {
            float hoverSpeed = _sm.MoveSpeed * HOVER_MOVE_SPEED_MULTIPLIER;
            _sm.Controller.Move(moveDir.normalized * hoverSpeed * Time.fixedDeltaTime);
        }

        // Keep suspended (no gravity)
        _sm.Velocity.y = 0f;
    }

    private void CheckStateTransitions()
    {
        // Hover exhausted: fall
        if (_sm.HoverExhausted)
        {
            _sm.ChangeState(_sm.FallState);
            return;
        }

        // Hover released: fall
        if (!_sm.IsHoverPressed)
        {
            // Check if we want to fly instead
            if (_sm.IsLaunchPressed && !_sm.FlightExhausted && _sm.LandingCooldown <= 0f)
            {
                _sm.ChangeState(_sm.FlyState);
            }
            else
            {
                _sm.ChangeState(_sm.FallState);
            }
            return;
        }

        // Want to fly: if launch pressed and has flight time
        if (_sm.IsLaunchPressed && !_sm.FlightExhausted && _sm.LandingCooldown <= 0f)
        {
            _sm.ChangeState(_sm.FlyState);
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
