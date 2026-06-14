using Unity.Netcode;
using UnityEngine;

public class HazardLeadBalloon : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        NetworkBalloonShooter[] allShooters = FindObjectsByType<NetworkBalloonShooter>();
        
        foreach (var shooter in allShooters)
        {
            if (shooter.OwnerClientId != shooterClientId)
            {
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { shooter.OwnerClientId } }
                };
                
                // Give their next 3 shots the extreme gravity debuff
                shooter.ApplyLeadBalloonClientRpc(3, rpcParams);
            }
        }
    }
}