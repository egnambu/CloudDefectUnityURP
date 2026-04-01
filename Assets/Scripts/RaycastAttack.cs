using Fusion;
using UnityEngine;

/// <summary>
/// Performs raycast-based attacks dealing damage to objects with a Health component.
/// </summary>
public class RaycastAttack : NetworkBehaviour
{
    public float Damage = 10;

    public PlayerMovement PlayerMovement;

    private void Update()
    {
        if (HasStateAuthority == false)
            return;

        Ray ray = PlayerMovement.Camera.ScreenPointToRay(Input.mousePosition);
        ray.origin += PlayerMovement.Camera.transform.forward;

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            Debug.DrawRay(ray.origin, ray.direction, Color.red, 1f);
            if (Physics.Raycast(ray.origin, ray.direction, out var hit))
            {
                if (hit.transform.TryGetComponent<Health>(out var health))
                {
                    health.DealDamageRpc(Damage);
                }
            }
        }
    }
}