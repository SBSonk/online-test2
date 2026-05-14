using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ServerUIDisplay : NetworkBehaviour
{
    public NetworkVariable<int> playerCount = new NetworkVariable<int>();
    public TextMeshProUGUI playerCountText, pingText;

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

        NetworkManager.OnClientConnectedCallback -= PlayerConnected;
        NetworkManager.OnClientDisconnectCallback -= PlayerDisconnected;

        if (!IsServer) 
            playerCount.OnValueChanged -= UpdatePlayerCount;
    }

    void PlayerConnected(ulong _) => playerCount.Value++;
    void PlayerDisconnected(ulong _) => playerCount.Value--;

    void Update()
    {
        if (!NetworkManager.IsListening) return;

        ulong ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.NetworkConfig.NetworkTransport.ServerClientId);
        pingText.text = $"Ping: {ping}ms";
    }

    void UpdatePlayerCount(int prev, int newCount)
    {
        playerCountText.text = $"Players: {newCount}";
    }
}
