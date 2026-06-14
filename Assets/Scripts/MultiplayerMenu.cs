using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class MultiplayerMenu : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject menuUI;
    [Tooltip("Players enter the IP address here to Host or Join. Leave blank to use localhost.")]
    [SerializeField] private TMP_InputField ipAddressInput; 
    [SerializeField] private TMP_Text statusText;
    
    [Tooltip("Assign a Text element on your permanent gameplay HUD here")]
    [SerializeField] private TMP_Text inGameIpText; 

    [Header("Network Settings")]
    [SerializeField] private string defaultIP = "127.0.0.1"; // Localhost
    [SerializeField] private ushort port = 7777; // Default Netcode port

    // --- NEW: Helper method to grab the IP from the input, or fallback to default ---
    private string GetIpAddress()
    {
        if (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text))
        {
            return ipAddressInput.text.Trim();
        }
        return defaultIP;
    }

    public void StartHost()
    {
        string ipAddress = GetIpAddress();
        SetStatus("Starting local host on " + ipAddress + "...");

        // Grab the transport and configure it for local UDP
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, port);
        transport.UseWebSockets = false; // Must be false for local/desktop testing

        bool started = NetworkManager.Singleton.StartHost();

        if (started)
        {
            if (inGameIpText != null) inGameIpText.text = "Hosting on: " + ipAddress; 

            SetStatus("Host started on " + ipAddress + ":" + port);
            HideMenu();
        }
        else
        {
            SetStatus("Failed to start Host.");
        }
    }

    public void StartClient()
    {
        string ipAddress = GetIpAddress();
        SetStatus("Joining session at " + ipAddress + "...");

        // Configure transport with the target IP
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, port);
        transport.UseWebSockets = false;

        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            if (inGameIpText != null) inGameIpText.text = "Connected to: " + ipAddress;

            SetStatus("Client started.");
            HideMenu();
        }
        else
        {
            SetStatus("Failed to start Client.");
        }
    }

    public void StartServer()
    {
        string ipAddress = GetIpAddress();
        SetStatus("Starting dedicated server on " + ipAddress + "...");
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, port);
        transport.UseWebSockets = false;

        bool started = NetworkManager.Singleton.StartServer();
        
        if (started)
        {
            SetStatus("Server started on " + ipAddress + ":" + port);
            HideMenu();
        }
        else
        {
            SetStatus("Failed to start Server.");
        }
    }

    private void HideMenu()
    {
        if (menuUI != null)
        {
            menuUI.SetActive(false);
        }
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}