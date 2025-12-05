using UnityEngine;

public class CameraSpring : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public Vector3 followOffset;

    // Internal spring state
    private Vector3 camVelocity;

    // Spring parameters (runtime blended)
    private float stiffness;
    private float damping;

    // Sprint logic
    private bool isSprinting;
    private float sprintTimer;

    // Tunable values
    [Header("Spring Values")]
    public float sprintStartDuration = 0.25f;

    // Sprint start values (camera lags)
    public float stiffnessSprintStart = 4f;
    public float dampingSprintStart = 0.85f;

    // Sprint settled values (camera catches up)
    public float stiffnessSprint = 12f;
    public float dampingSprint = 0.65f;

    // Default walk values
    public float stiffnessWalk = 10f;
    public float dampingWalk = 0.7f;

    void Update()
    {
        Vector3 targetPos = player.position + followOffset;

        // Choose the spring config depending on sprint state
        if (isSprinting)
        {
            sprintTimer += Time.deltaTime;

            if (sprintTimer < sprintStartDuration)
            {
                float t = sprintTimer / sprintStartDuration;
                stiffness = Mathf.Lerp(stiffnessSprintStart, stiffnessSprint, t);
                damping = Mathf.Lerp(dampingSprintStart, dampingSprint, t);
            }
            else
            {
                stiffness = stiffnessSprint;
                damping = dampingSprint;
            }
        }
        else
        {
            stiffness = stiffnessWalk;
            damping = dampingWalk;
        }

        // APPLY SPRING
        Vector3 delta = (targetPos - transform.position);

        camVelocity += delta * stiffness * Time.deltaTime;
        camVelocity *= damping;

        transform.position += camVelocity * Time.deltaTime;
    }

    // PUBLIC API

    public void StartSprint()
    {
        isSprinting = true;
        sprintTimer = 0f;
    }

    public void StopSprint()
    {
        isSprinting = false;
    }
}
