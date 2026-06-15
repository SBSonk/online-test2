using Unity.Netcode;
using UnityEngine;

public class HazardLeadBalloon : CarnivalTarget
{
    [Header("Hazard Settings")]
    [Tooltip("How many seconds the opponent's balloons are incredibly heavy.")]
    public float effectDuration = 6f;

    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        NetworkBalloonShooter[] allShooters = FindObjectsByType<NetworkBalloonShooter>(FindObjectsSortMode.None);
        
        foreach (var shooter in allShooters)
        {
            if (shooter.OwnerClientId != shooterClientId)
            {
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { shooter.OwnerClientId } }
                };
                
                shooter.ApplyLeadBalloonClientRpc(effectDuration, rpcParams);
            }
        }
    }
}