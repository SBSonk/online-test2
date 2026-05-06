using UnityEngine;
using Unity.Netcode;

public class MultiplayerMenu : MonoBehaviour
{
    public GameObject connectionUI, gameUI;

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
    }

    void Update()
    {
        bool connected = NetworkManager.Singleton.IsListening;

        connectionUI.SetActive(connected);
        gameUI.SetActive(!connected);
    }
}
