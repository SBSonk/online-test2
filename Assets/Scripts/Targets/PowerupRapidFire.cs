using Unity.Netcode;
using UnityEngine;

public class PowerupRapidFire : CarnivalTarget
{
    [Header("Powerup Settings")]
    [Tooltip("How many seconds the player can throw incredibly fast.")]
    public float effectDuration = 8f;

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
                
                shooter.ApplyRapidFireClientRpc(effectDuration, rpcParams); 
            }
        }
    }
}