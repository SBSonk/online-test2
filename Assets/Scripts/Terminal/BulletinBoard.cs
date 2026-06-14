using UnityEngine;
using TMPro;
using Unity.Netcode;

public class BulletinBoard : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The TextMeshPro element that displays the full list of rules.")]
    public TextMeshProUGUI boardText;

    private void Start()
    {
        // Hook into the UnityEvent we set up in the NetworkMatchManager
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnSettingsChanged.AddListener(UpdateBoardUI);
            
            // Force an initial update so the board isn't blank when the game starts
            UpdateBoardUI();
        }
    }

    private void OnDestroy()
    {
        // Always clean up listeners to prevent memory leaks!
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnSettingsChanged.RemoveListener(UpdateBoardUI);
        }
    }

    public void UpdateBoardUI()
    {
        if (NetworkMatchManager.Instance == null || boardText == null) return;

        // 1. Grab all current settings from the network variables
        var mode = NetworkMatchManager.Instance.currentGameMode.Value;
        float duration = NetworkMatchManager.Instance.matchDurationSetting.Value;
        // 1. Grab the new consolidated mode setting
        var pMode = NetworkMatchManager.Instance.powerupModeSetting.Value;
        
        // 2. Derive the old variables from the new enum so the rest of your script works perfectly
        bool powerups = (pMode == NetworkMatchManager.PowerupMode.On || pMode == NetworkMatchManager.PowerupMode.Chaos);
        int chaos = (pMode == NetworkMatchManager.PowerupMode.Chaos) ? 1 : 0;
        
        // 3. Speed and Spawn Rate remain exactly the same
        float speed = NetworkMatchManager.Instance.targetSpeedSetting.Value;
        float spawnRate = NetworkMatchManager.Instance.spawnIntervalSetting.Value;

        // 2. Build the display text dynamically
        string displayText = "<size=120%><b>--- MATCH RULES ---</b></size>\n\n";

        // Universal Settings (Always show these)
        displayText += $"<b>MODE:</b> {(mode == NetworkMatchManager.GameMode.PvP ? "<color=#FF5555>PVP CLASH</color>" : "<color=#55FF55>SHOOTING GALLERY</color>")}\n";
        displayText += $"<b>TIME LIMIT:</b> {duration} SECONDS\n";
        displayText += $"<b>POWERUPS:</b> {(powerups ? "ENABLED" : "DISABLED")}\n\n";

        // 3. Mode-Specific Settings
        if (mode == NetworkMatchManager.GameMode.ShootingGallery)
        {
            displayText += "<size=110%><b>- GALLERY SETTINGS -</b></size>\n";
            displayText += $"TARGET SPEED: {GetSpeedString(speed)}\n";
            displayText += $"SPAWN RATE: {GetSpawnRateString(spawnRate)}\n";
            displayText += $"CHAOS LEVEL: {(chaos == 0 ? "NORMAL" : "HIGH")}\n";
        }
        else if (mode == NetworkMatchManager.GameMode.PvP)
        {
            displayText += "<size=110%><b>- PVP SETTINGS -</b></size>\n";
            displayText += "TARGETS: <color=#888888>OFF</color>\n";
            displayText += "KNOCKBACK: <color=#FFDD55>ACTIVE</color>\n";
            displayText += "STUNS: <color=#FFDD55>ACTIVE</color>\n";
            // You can add more PvP specific settings here later if you expand the game!
        }

        // 4. Apply to the UI
        boardText.text = displayText;
    }

    // --- Helper Formatting Methods ---
    // These turn the raw floats into readable text for the players

    private string GetSpeedString(float speed)
    {
        if (speed <= 2.5f) return "SLOW";
        if (speed >= 4.5f) return "FAST";
        return "NORMAL";
    }

    private string GetSpawnRateString(float rate)
    {
        if (rate <= 1.0f) return "FAST";
        if (rate >= 2.0f) return "SLOW";
        return "NORMAL";
    }
}