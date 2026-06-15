using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Linq; // Needed for OrderByDescending

public class ServerUIDisplay : NetworkBehaviour
{
    [Header("UI References")]
    public NetworkVariable<int> playerCount = new NetworkVariable<int>();
    public TextMeshProUGUI playerCountText, pingText;
    
    // --- NEW: Added condensed leaderboard reference ---
    public TextMeshProUGUI condensedLeaderboardText; 

    public override void OnNetworkSpawn()
    {
        if (IsServer) {
            playerCount.Value = 0;
            NetworkManager.OnClientConnectedCallback += PlayerConnected;
            NetworkManager.OnClientDisconnectCallback += PlayerDisconnected;
        } 

        playerCount.OnValueChanged += UpdatePlayerCount;
        UpdatePlayerCount(0, playerCount.Value);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -= PlayerConnected;
            NetworkManager.OnClientDisconnectCallback -= PlayerDisconnected;
        }

        if (!IsServer) 
            playerCount.OnValueChanged -= UpdatePlayerCount;
    }

    void PlayerConnected(ulong _) => playerCount.Value++;
    void PlayerDisconnected(ulong _) => playerCount.Value--;

    void Update()
    {
        if (!NetworkManager.IsListening) return;

        // Update Ping
        ulong ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.NetworkConfig.NetworkTransport.ServerClientId);
        pingText.text = $"Ping: {ping}ms";

        // --- NEW: Refresh condensed leaderboard every frame ---
        // (For efficiency in a production game, you might want to refresh this only 
        // once per second or via an event, but this works fine for small lobbies!)
        RefreshCondensedLeaderboard();
    }

    void UpdatePlayerCount(int prev, int newCount)
    {
        playerCountText.text = $"Players: {newCount}";
    }

    private void RefreshCondensedLeaderboard()
    {
        if (condensedLeaderboardText == null) return;

        // Find all player scores
        var allScores = FindObjectsByType<NetworkPlayerScore>();
        var sorted = allScores.OrderByDescending(p => p.currentScore.Value); // Only show Top 3

        string sb = "TOP SCORES\n";
        foreach (var player in sorted)
        {
            string name = $"P{player.OwnerClientId}";
            
            // Highlight the local player
            string suffix = player.IsOwner ? " (YOU)" : "";
            
            // Optional: Get color from the same manager you used in DiegeticLeaderboard
            if (player.TryGetComponent(out PlayerColorManager cm) && cm.colorSets.Length > 0)
            {
                name = cm.colorSets[cm.colorIndex.Value % cm.colorSets.Length].themeName;
            }

            sb += $"{name}{suffix}: {player.currentScore.Value}\n";
        }

        condensedLeaderboardText.text = sb;
    }
}