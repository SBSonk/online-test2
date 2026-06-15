using Unity.Netcode;
using UnityEngine;

public class PowerupMagnet : CarnivalTarget
{
    [Header("Powerup Settings")]
    [Tooltip("How many seconds the player's balloons home in on targets.")]
    public float effectDuration = 10f;

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
                
                shooter.ApplyMagnetClientRpc(effectDuration, rpcParams); 
            }
        }
    }
}