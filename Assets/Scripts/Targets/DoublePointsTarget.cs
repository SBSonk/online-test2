// --- DoublePointsTarget.cs ---
// Attach to your Double Points Powerup Prefab
using Unity.Netcode;

public class DoublePointsTarget : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        // Removed .Singleton
        if (NetworkManager.ConnectedClients.TryGetValue(shooterClientId, out NetworkClient client))
        {
            if (client.PlayerObject.TryGetComponent(out PlayerState playerState))
            {
                playerState.ActivateDoublePoints(10f); // 10 seconds of double points
            }
        }
    }
}

