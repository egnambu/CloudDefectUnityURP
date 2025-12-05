using UnityEngine;

/// <summary>
/// Handles the landing transition from airborne states to grounded.
/// Plays landing animation and prevents immediate re-launch.
/// </summary>
public class LandState : IPlayerState
{
    private readonly PlayerStateMachine _sm;
    private float _landingTimer;
    private const float LANDING_DURATION = 0.5f; // Time in landing state before can move
    private const float LANDING_COOLDOWN_TIME = 1f; // Time before can launch again

    public LandState(PlayerStateMachine stateMachine)
    {
        _sm = stateMachine;
    }

    public void Enter()
    {
        // Reset controller to default
        _sm.ResetControllerMode();
        _landingTimer = 0f;

        // Set landing cooldown to prevent immediate re-launch
        _sm.LandingCooldown = LANDING_COOLDOWN_TIME;

        // Set animator
        _sm.Animator.SetTrigger("Landing");
        _sm.Animator.SetBool("IsLanding", true);
        _sm.Animator.SetBool("IsFlying", false);
        _sm.Animator.SetBool("IsFalling", false);
        _sm.Animator.SetBool("IsHovering", false);
        _sm.Animator.SetBool("IsJumping", false);
    }

    public void Exit()
    {
        _sm.Animator.SetBool("IsLanding", false);
        _sm.Animator.ResetTrigger("Landing");
    }

    public void Tick()
    {
        _landingTimer += Time.deltaTime;

        // Update animator
        _sm.UpdateLocomotionAnimator(0f, false);

        // Check state transitions
        CheckStateTransitions();
    }

    public void FixedTick()
    {
        // Apply gravity to keep grounded
        if (_sm.Velocity.y < 0)
        {
            _sm.Velocity.y = -2f;
        }

        _sm.Controller.Move(_sm.Velocity * Time.fixedDeltaTime);
    }

    private void CheckStateTransitions()
    {
        // Wait for landing animation to complete
        if (_landingTimer < LANDING_DURATION)
        {
            return;
        }

        // Transition to walk when landing complete
        if (_sm.IsGrounded)
        {
            _sm.ChangeState(_sm.WalkState);
            return;
        }

        // If somehow not grounded after landing, fall
        _sm.ChangeState(_sm.FallState);
    }
}
