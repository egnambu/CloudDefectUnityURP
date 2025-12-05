using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Manages switching between Free-Look and Aiming camera modes for TPS gameplay
/// </summary>
public class TPSCameraManager : MonoBehaviour
{
    [Header("Virtual Camera References")]
    [SerializeField] private CinemachineCamera freeLookCamera;
    [SerializeField] private CinemachineCamera aimingCamera;

    [Header("Camera Priorities")]
    [SerializeField] private int activePriority = 10;
    [SerializeField] private int inactivePriority = 0;

    [Header("Camera Settings")]
    [SerializeField] private float freeLookSensitivity = 2f;
    [SerializeField] private float aimingSensitivity = 1f;

    private bool isAiming = false;
    private CinemachineOrbitalFollow freeLookOrbital;
    private CinemachineOrbitalFollow aimingOrbital;

    private void Start()
    {
        // Lock the cursor to the center of the screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Get Orbital Follow components for mouse control
        if (freeLookCamera != null)
        {
            freeLookOrbital = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
        }

        if (aimingCamera != null)
        {
            aimingOrbital = aimingCamera.GetComponent<CinemachineOrbitalFollow>();
        }

        // Start in free-look mode
        SetFreeLookMode();
    }

    private void Update()
    {
        // Check for aim input
        var inputMgr = InputBindingManager.Instance;
        if (inputMgr == null) return;

        bool aimInput = inputMgr.GetAction(GameAction.AimDownSights);

        if (aimInput != isAiming)
        {
            isAiming = aimInput;

            if (isAiming)
            {
                SetAimingMode();
            }
            else
            {
                SetFreeLookMode();
            }
        }

    }

    private void SetFreeLookMode()
    {
        if (freeLookCamera != null)
        {
            freeLookCamera.Priority.Value = activePriority;
        }

        if (aimingCamera != null)
        {
            aimingCamera.Priority.Value = inactivePriority;
        }
    }

    private void SetAimingMode()
    {
        if (aimingCamera != null)
        {
            aimingCamera.Priority.Value = activePriority;

            // // Sync rotation from free-look to aiming camera
            // if (freeLookOrbital != null && aimingOrbital != null)
            // {
            //     aimingOrbital.HorizontalAxis.Value = freeLookOrbital.HorizontalAxis.Value;
            //     aimingOrbital.VerticalAxis.Value = freeLookOrbital.VerticalAxis.Value;
            // }
        }

        if (freeLookCamera != null)
        {
            freeLookCamera.Priority.Value = inactivePriority;
        }
    }

    public bool IsAiming => isAiming;

    public Vector3 GetCameraForward()
    {
        if (Camera.main != null)
        {
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0;
            forward.Normalize();
            return forward;
        }
        return Vector3.forward;
    }

    public Vector3 GetCameraRight()
    {
        if (Camera.main != null)
        {
            Vector3 right = Camera.main.transform.right;
            right.y = 0;
            right.Normalize();
            return right;
        }
        return Vector3.right;
    }
}
