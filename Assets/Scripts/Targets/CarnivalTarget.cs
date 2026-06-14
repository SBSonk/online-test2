// --- CarnivalTarget.cs (Base Class) ---
// Do not attach this directly; it is the base for the others.
using Unity.Netcode;
using UnityEngine;

public abstract class CarnivalTarget : NetworkBehaviour
{
    [Header("Base Settings")]
    public int scoreValue = 1;

    public void ProcessHit(ulong shooterClientId)
    {
        if (!IsServer) return;

        if (scoreValue != 0)
        {
            ApplyScore(shooterClientId);
        }

        ApplySpecialEffect(shooterClientId);
        
        // Despawn network object
        GetComponent<NetworkObject>().Despawn(true);
    }

    private void ApplyScore(ulong shooterClientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out NetworkClient client))
        {
            if (client.PlayerObject.TryGetComponent(out PlayerState playerState))
            {
                playerState.AddScore(scoreValue);
            }
        }
    }

    protected virtual void ApplySpecialEffect(ulong shooterClientId) { }
}
