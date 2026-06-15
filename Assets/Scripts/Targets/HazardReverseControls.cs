using Unity.Netcode;
using UnityEngine;

public class HazardReverseControls : CarnivalTarget
{
    [Header("Hazard Settings")]
    [Tooltip("How many seconds the opponent's mouse is inverted.")]
    public float effectDuration = 5f;

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
                
                shooter.ApplyBrainScrambleClientRpc(effectDuration, rpcParams);
            }
        }
    }
}