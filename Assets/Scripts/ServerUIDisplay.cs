using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ServerUIDisplay : MonoBehaviour
{
    public TextMeshProUGUI playerCountText, pingText;

    void Update()
    {
        playerCountText.gameObject.SetActive(NetworkManager.Singleton.IsServer);
        if (NetworkManager.Singleton.IsServer) 
        {
            playerCountText.text = $"Players: {NetworkManager.Singleton.ConnectedClients.Count}";
        }

        ulong ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.NetworkConfig.NetworkTransport.ServerClientId);
        pingText.text = $"Ping: {ping}ms";
    }
}
