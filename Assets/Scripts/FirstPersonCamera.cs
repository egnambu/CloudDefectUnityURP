using UnityEngine;

/// <summary>
/// First-person camera controller that follows and rotates based on mouse input.
/// </summary>
public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public float MouseSensitivity = 10f;

    private float _verticalRotation;
    private float _horizontalRotation;

    private void LateUpdate()
    {
        if (Target == null)
            return;

        transform.position = Target.position;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _verticalRotation -= mouseY * MouseSensitivity;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -70f, 70f);

        _horizontalRotation += mouseX * MouseSensitivity;

        transform.rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0);
    }
}