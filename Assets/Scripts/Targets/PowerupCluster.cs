using Unity.Netcode;
using UnityEngine;

public class PowerupCluster : CarnivalTarget
{
    [Header("Powerup Settings")]
    [Tooltip("How many seconds the player throws 3 balloons at once.")]
    public float effectDuration = 6f;

    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var client))
        {
            if (client.PlayerObject.TryGetComponent(out NetworkBalloonShooter shooter))
            {
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { shooterClientId } }
                };
                
                shooter.ApplyClusterClientRpc(effectDuration, rpcParams); 
            }
        }
    }
}