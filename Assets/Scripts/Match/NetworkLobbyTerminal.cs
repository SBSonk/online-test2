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
        // 1. Keep your existing listeners for duration/target settings
        matchTimeSetting.OnValueChanged += (oldVal, newVal) => UpdateScreen();
        maxTargetsSetting.OnValueChanged += (oldVal, newVal) => UpdateScreen();
        
        // 2. UPDATED: Listen to the MatchState machine instead of the boolean
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += (oldState, newState) => UpdateScreen();
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

        // We no longer need to manually copy settings here! 
        // The matchDurationSetting is already updated via the UI_SetDuration RPCs on the bulletin board.
        
        NetworkTargetSpawner spawner = FindAnyObjectByType<NetworkTargetSpawner>();
        if (spawner != null) 
        {
            // NOTE: Make sure your NetworkTargetSpawner actually has a 'maxTargetCount' variable!
            // If it doesn't, you'll need to add it or remove this line.
            spawner.maxTargetCount.Value = maxTargetsSetting.Value;
        }

        // Start the game! The NetworkMatchManager will automatically pull the correct synchronized settings.
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
    if (timeText != null) timeText.text = $"{NetworkMatchManager.Instance.matchDurationSetting.Value} SEC";
    if (targetsText != null) targetsText.text = $"{maxTargetsSetting.Value} MAX";

    if (statusText != null && NetworkMatchManager.Instance != null)
    {
        var state = NetworkMatchManager.Instance.currentState.Value;

        switch (state)
        {
            case NetworkMatchManager.MatchState.Lobby:
                statusText.text = "READY TO START";
                statusText.color = Color.green;
                break;
            case NetworkMatchManager.MatchState.WaitingForPositions:
                statusText.text = "GET TO YOUR BOOTHS!";
                statusText.color = Color.yellow;
                break;
            case NetworkMatchManager.MatchState.Countdown:
                statusText.text = "GET READY...";
                statusText.color = Color.yellow;
                break;
            case NetworkMatchManager.MatchState.Active:
                statusText.text = "MATCH IN PROGRESS!";
                statusText.color = Color.red;
                break;
        }
    }
}
}