using UnityEngine;

/// <summary>
/// Interface that all player states must implement.
/// Provides a consistent contract for state behavior.
/// </summary>
public interface IPlayerState
{
    /// <summary>
    /// Called once when entering this state.
    /// Use for initialization, setting animator parameters, etc.
    /// </summary>
    void Enter();

    /// <summary>
    /// Called once when exiting this state.
    /// Use for cleanup, resetting animator parameters, etc.
    /// </summary>
    void Exit();

    /// <summary>
    /// Called every frame (from Update).
    /// Use for input handling, state transition checks, and non-physics logic.
    /// </summary>
    void Tick();

    /// <summary>
    /// Called every physics frame (from FixedUpdate).
    /// Use for movement, physics calculations, and applying forces.
    /// </summary>
    void FixedTick();
}
