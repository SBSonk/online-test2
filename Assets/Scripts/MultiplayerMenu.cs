using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class MultiplayerMenu : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject menuUI;
    [Tooltip("Players enter the Host's IPv4 address here. Leave blank to use localhost.")]
    [SerializeField] private TMP_InputField ipAddressInput; 
    [SerializeField] private TMP_Text statusText;
    
    [Tooltip("Assign a Text element on your permanent gameplay HUD here")]
    [SerializeField] private TMP_Text inGameIpText; 

    [Header("Network Settings")]
    [SerializeField] private string defaultIP = "127.0.0.1"; // Localhost
    [SerializeField] private ushort port = 7777; // Default Netcode port

    public void StartHost()
    {
        SetStatus("Starting local host...");

        // Grab the transport and configure it for local UDP
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(defaultIP, port);
        transport.UseWebSockets = false; // Must be false for local/desktop testing

        bool started = NetworkManager.Singleton.StartHost();

        if (started)
        {
            if (inGameIpText != null) inGameIpText.text = "Hosting on: " + defaultIP; 

            SetStatus("Host started on " + defaultIP + ":" + port);
            HideMenu();
        }
        else
        {
            SetStatus("Failed to start Host.");
        }
    }

    public void StartClient()
    {
        SetStatus("Joining local session...");

        // Use the IP typed in the input, or default to 127.0.0.1 if left blank
        string ipAddress = defaultIP;
        if (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text))
        {
            ipAddress = ipAddressInput.text.Trim();
        }

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
        SetStatus("Starting dedicated local server...");
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(defaultIP, port);
        transport.UseWebSockets = false;

        bool started = NetworkManager.Singleton.StartServer();
        
        if (started)
        {
            SetStatus("Server started on " + defaultIP + ":" + port);
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