using Unity.Netcode;
using UnityEngine;

public class DoublePointsTarget : CarnivalTarget
{
    [Tooltip("The base value of this target. When hit, it doubles this.")]
    public int basePoints = 100;

    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        // 1. Get the player who shot the target
        if (NetworkManager.ConnectedClients.TryGetValue(shooterClientId, out NetworkClient client))
        {
            // 2. Find their scoring component
            // Change 'NetworkPlayerScore' to whatever script holds your 'AddPoints' method
            if (client.PlayerObject.TryGetComponent(out NetworkPlayerScore scoreScript))
            {
                // 3. Apply the double points instantly
                // We add the points here, so ensure your default hit 
                // logic doesn't add points again, OR set your default 
                // target points to 0 for this specific prefab.
                scoreScript.AddPoints(basePoints * 2);
            }
        }
    }
}