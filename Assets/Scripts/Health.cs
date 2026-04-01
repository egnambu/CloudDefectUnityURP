using Fusion;
using UnityEngine;

/// <summary>
/// Manages networked player health and processes incoming damage via RPC.
/// </summary>
public class Health : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(HealthChanged))]
    public float NetworkedHealth { get; set; } = 100;

    private void HealthChanged()
    {
        Debug.Log($"Health changed to: {NetworkedHealth}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void DealDamageRpc(float damage)
    {
        // The code inside here will run on the client which owns this object (has state and input authority).
        Debug.Log("Received DealDamageRpc on StateAuthority, modifying Networked variable");
        NetworkedHealth -= damage;
    }
}