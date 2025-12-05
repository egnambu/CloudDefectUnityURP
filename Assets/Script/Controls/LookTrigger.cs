using UnityEngine;
public class LookTrigger : MonoBehaviour
{
    [Tooltip("How long NPCs should look at this item")]
    public float lookDuration = 3f;
    public Transform looktarget;
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"OnTriggerEnter called by: {other.gameObject.name}");

        // Check if the entering object has a HeadLookSimpleIK component
        HeadLookSimpleIK headLook = other.GetComponent<HeadLookSimpleIK>();
        if (headLook != null)
        {
            Debug.Log($"HeadLookSimpleIK found on {other.gameObject.name}. Triggering LookAt for {lookDuration} seconds.");
            // Tell that NPC to look at this object
            headLook.LookAt(looktarget, lookDuration);
        }
        else
        {
            Debug.Log($"No HeadLookSimpleIK found on {other.gameObject.name}.");
        }
    }
}
