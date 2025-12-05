using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HeadLookSimpleIK : MonoBehaviour
{
    [Header("Look Behaviour")]
    public float lookWeight = 1f;
    public float rotationSpeed = 8f; // faster blending
    public float duration = 3f;
    
    [Header("Look Limits")]
    public float maxHorizontalAngle = 70f; // max left/right angle
    public float maxVerticalAngle = 60f; // max up/down angle

    private Animator animator;
    private Transform lookTarget;
    private float timer;
    private float currentWeight;
    private Vector3 smoothPos;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void LookAt(Transform target, float durationSeconds)
    {
        lookTarget = target;
        duration = durationSeconds;
        timer = durationSeconds;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        if (lookTarget == null)
        {
            currentWeight = Mathf.MoveTowards(currentWeight, 0f, Time.deltaTime * rotationSpeed);
            
            // Smoothly transition smoothPos back to a natural forward position
            Vector3 naturalLookPos = transform.position + transform.forward * 2f;
            smoothPos = Vector3.Lerp(smoothPos, naturalLookPos, Time.deltaTime * rotationSpeed);
            
            animator.SetLookAtWeight(currentWeight);
            animator.SetLookAtPosition(smoothPos);
            return;
        }

        // countdown
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            // Set smoothPos to current position before nulling target to avoid flicker
            smoothPos = lookTarget.position;
            lookTarget = null;
        }

        // Check if target is within angle limits
        Vector3 directionToTarget = (lookTarget.position - transform.position).normalized;
        Vector3 forward = transform.forward;
        
        float horizontalAngle = Vector3.Angle(new Vector3(forward.x, 0, forward.z), new Vector3(directionToTarget.x, 0, directionToTarget.z));
        float verticalAngle = Mathf.Abs(Mathf.Atan2(directionToTarget.y - forward.y, new Vector2(directionToTarget.x, directionToTarget.z).magnitude) * Mathf.Rad2Deg);
        
        // If target is outside limits, reduce weight or clamp position
        bool withinLimits = horizontalAngle <= maxHorizontalAngle && verticalAngle <= maxVerticalAngle;
        float targetWeight = withinLimits ? lookWeight : 0f;
        
        // smoother transition
        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * rotationSpeed);

        if (withinLimits)
        {
            // optional target smoothing
            smoothPos = Vector3.Lerp(smoothPos, lookTarget.position, Time.deltaTime * 10f);
        }

        animator.SetLookAtWeight(currentWeight);
        animator.SetLookAtPosition(smoothPos);
    }
}
