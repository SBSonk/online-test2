using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerScore : NetworkBehaviour
{
    public NetworkVariable<int> currentScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> gamesPlayed = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        // Keep your existing HUD logic here
        currentScore.OnValueChanged += (oldScore, newScore) => {
            HUDManager hud = Object.FindAnyObjectByType<HUDManager>();
            if (hud != null) hud.RefreshScoreboardDisplay();
        };
    }

    // Server-only: Add points
    public void AddPoints(int points)
    {
        if (!IsServer) return;
        currentScore.Value += points;
    }

    // Server-only: Call this when a match ends
    public void IncrementGamesPlayed()
    {
        if (!IsServer) return;
        gamesPlayed.Value += 1;
    }
}