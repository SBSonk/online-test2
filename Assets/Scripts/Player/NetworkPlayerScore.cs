using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerScore : NetworkBehaviour
{
    public NetworkVariable<int> currentScore = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        // Whenever ANY player's score changes, refresh the local HUD scoreboard stack
        currentScore.OnValueChanged += (oldScore, newScore) => 
        {
            HUDManager localHUD = Object.FindAnyObjectByType<HUDManager>();
            if (localHUD != null)
            {
                localHUD.RefreshScoreboardDisplay();
            }
        };
        
        // Force an initial draw when the player first spawns in
        HUDManager hud = Object.FindAnyObjectByType<HUDManager>();
        if (hud != null) hud.RefreshScoreboardDisplay();
    }

    // ONLY the server is allowed to grant points
    public void AddPoints(int points)
    {
        if (!IsServer) return;
        currentScore.Value += points;
    }
}