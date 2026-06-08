using Unity.Netcode;
using UnityEngine;
using TMPro;

public class NetworkLobbyTerminal : NetworkBehaviour
{
    [Header("UI Screen References")]
    [Tooltip("Text elements on your World Space Canvas")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI targetsText;
    public TextMeshProUGUI statusText;

    [Header("Synced Settings")]
    public NetworkVariable<int> matchTimeSetting = new NetworkVariable<int>(60);
    public NetworkVariable<int> maxTargetsSetting = new NetworkVariable<int>(4);

    public override void OnNetworkSpawn()
    {
        // Whenever the host changes a setting, update the screen for everyone
        matchTimeSetting.OnValueChanged += (oldVal, newVal) => UpdateScreen();
        maxTargetsSetting.OnValueChanged += (oldVal, newVal) => UpdateScreen();
        
        // Listen to see if the game started so we can change the status text
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.isGameActive.OnValueChanged += (oldVal, newVal) => UpdateScreen();
        }

        UpdateScreen(); // Initial draw
    }

    // =========================================================
    // BUTTON FUNCTIONS: Hook your Interact Scripts to these!
    // =========================================================

    public void InteractStartMatch()
    {
        // Only let the server/host start the game to prevent griefing
        if (!IsServer) return; 

        // 1. Apply the chosen settings to the real game managers
        NetworkMatchManager.Instance.matchDuration = matchTimeSetting.Value;
        
        NetworkTargetSpawner spawner = Object.FindAnyObjectByType<NetworkTargetSpawner>();
        if (spawner != null) spawner.maxTargetCount.Value = maxTargetsSetting.Value;

        // 2. Start the game!
        NetworkMatchManager.Instance.RequestStartMatch();
    }

    public void InteractIncreaseTime() 
    { 
        if (IsServer) matchTimeSetting.Value = Mathf.Clamp(matchTimeSetting.Value + 15, 30, 300); 
    }
    
    public void InteractDecreaseTime() 
    { 
        if (IsServer) matchTimeSetting.Value = Mathf.Clamp(matchTimeSetting.Value - 15, 30, 300); 
    }
    
    public void InteractIncreaseTargets() 
    { 
        if (IsServer) maxTargetsSetting.Value = Mathf.Clamp(maxTargetsSetting.Value + 1, 1, 10); 
    }
    
    public void InteractDecreaseTargets() 
    { 
        if (IsServer) maxTargetsSetting.Value = Mathf.Clamp(maxTargetsSetting.Value - 1, 1, 10); 
    }

    // =========================================================

    private void UpdateScreen()
    {
        if (timeText != null) timeText.text = $"{matchTimeSetting.Value} SEC";
        if (targetsText != null) targetsText.text = $"{maxTargetsSetting.Value} MAX";

        if (statusText != null && NetworkMatchManager.Instance != null)
        {
            bool isPlaying = NetworkMatchManager.Instance.isGameActive.Value;
            statusText.text = isPlaying ? "MATCH IN PROGRESS..." : "WAITING FOR HOST TO START...";
            statusText.color = isPlaying ? Color.red : Color.green;
        }
    }
}