using Unity.Netcode;
using UnityEngine;

public class HazardButterFingers : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        // Find every player in the game
        NetworkBalloonShooter[] allShooters = FindObjectsByType<NetworkBalloonShooter>();
        
        foreach (var shooter in allShooters)
        {
            // If this is NOT the person who popped the balloon (an opponent)
            if (shooter.OwnerClientId != shooterClientId)
            {
                // Target the ClientRpc specifically to their machine
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { shooter.OwnerClientId } }
                };
                
                // Give them 10 seconds of the oscillating wave minigame!
                shooter.ApplyButterFingersClientRpc(10f, rpcParams);
            }
        }
    }
}