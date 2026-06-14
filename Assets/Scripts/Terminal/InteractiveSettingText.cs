using UnityEngine;
using TMPro;
using Unity.Netcode;

[RequireComponent(typeof(TextMeshProUGUI))]
public class InteractableSettingText : Interactable // Inheriting from your base class!
{
    public enum SettingType
    {
        GameMode,
        MatchDuration,
        Powerups,
        TargetSpeed,
        SpawnRate,
        ChaosLevel
    }

    [Header("Configuration")]
    public SettingType settingToControl;
    
    [Header("Optional Styling")]
    public string prefix = ""; 
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;

    private TextMeshProUGUI myText;
    private Collider myCollider;

    private void Awake()
    {
        myText = GetComponent<TextMeshProUGUI>();
        myCollider = GetComponent<Collider>();
        myText.color = normalColor;
    }

    // Overriding the base class initialization instead of using Start()
    public override void OnInitialize()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnSettingsChanged.AddListener(UpdateDisplay);
            UpdateDisplay();
        }
    }

    private void OnDestroy()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnSettingsChanged.RemoveListener(UpdateDisplay);
        }
    }

    // ==========================================
    // OVERRIDDEN INTERACTION LOGIC
    // ==========================================

    public override void HoverStart()
    {
        // Only allow hover color change if we are in the LOBBY state
        if (NetworkMatchManager.Instance != null && 
            NetworkMatchManager.Instance.currentState.Value == NetworkMatchManager.MatchState.Lobby)
        {
            myText.color = hoverColor;
        }
    }

    public override void HoverEnd()
    {
        // Reset to normalColor, which UpdateDisplay() will automatically correct 
        // to the correct red/green/white depending on the setting state
        myText.color = normalColor;
    }

    public override void Interact(GameObject player)
    {
        base.Interact(player); 

        // Only allow interaction if we are in the LOBBY state
        if (NetworkMatchManager.Instance == null || 
            NetworkMatchManager.Instance.currentState.Value != NetworkMatchManager.MatchState.Lobby) 
        {
            return;
        }

        CycleSetting();
    }

    // ==========================================
    // CYCLING & UI LOGIC
    // ==========================================

    private void CycleSetting()
    {
        var netManager = NetworkMatchManager.Instance;

        switch (settingToControl)
        {
            case SettingType.GameMode:
                var nextMode = netManager.currentGameMode.Value == NetworkMatchManager.GameMode.ShootingGallery 
                    ? NetworkMatchManager.GameMode.PvP 
                    : NetworkMatchManager.GameMode.ShootingGallery;
                netManager.RequestSetGameModeRpc(nextMode);
                break;

            case SettingType.MatchDuration:
                float currentDur = netManager.matchDurationSetting.Value;
                float nextDur = currentDur == 30f ? 60f : (currentDur == 60f ? 120f : 30f);
                netManager.RequestSetDurationRpc(nextDur);
                break;

            case SettingType.Powerups:
                netManager.RequestTogglePowerupsRpc();
                break;

            case SettingType.TargetSpeed:
                float currentSpd = netManager.targetSpeedSetting.Value;
                float nextSpd = currentSpd == 2.0f ? 3.5f : (currentSpd == 3.5f ? 5.0f : 2.0f);
                netManager.RequestSetSpeedRpc(nextSpd);
                break;

            case SettingType.SpawnRate:
                float currentRate = netManager.spawnIntervalSetting.Value;
                // Note: Lower is faster
                float nextRate = currentRate == 1.0f ? 1.5f : (currentRate == 1.5f ? 2.5f : 1.0f);
                netManager.RequestSetSpawnRateRpc(nextRate); // Assuming you added this RPC!
                break;

            case SettingType.ChaosLevel:
                int currentChaos = netManager.chaosLevelSetting.Value;
                netManager.RequestSetChaosRpc(currentChaos == 0 ? 1 : 0);
                break;
        }
    }

    private void UpdateDisplay()
    {
        var netManager = NetworkMatchManager.Instance;
        var mode = netManager.currentGameMode.Value;

        // 1. Handle Visibility (Hide Gallery settings if in PvP mode)
        bool isGallerySetting = settingToControl == SettingType.TargetSpeed || 
                                settingToControl == SettingType.SpawnRate || 
                                settingToControl == SettingType.ChaosLevel;

        if (mode == NetworkMatchManager.GameMode.PvP && isGallerySetting)
        {
            gameObject.SetActive(false);
            return;
        }
        else
        {
            gameObject.SetActive(true);
        }

        // 2. Format Text & Dynamic Colors
        string valueText = "";

        switch (settingToControl)
        {
            case SettingType.GameMode:
                if (mode == NetworkMatchManager.GameMode.PvP)
                {
                    valueText = "MODE: PVP CLASH";
                    normalColor = new Color(1f, 0.33f, 0.33f); // Red
                }
                else
                {
                    valueText = "MODE: SHOOTING GALLERY";
                    normalColor = new Color(0.33f, 1f, 0.33f); // Green
                }
                break;

            case SettingType.MatchDuration:
                float dur = netManager.matchDurationSetting.Value;
                valueText = $"TIME LIMIT: {dur} SECONDS";
                
                if (dur == 30f) normalColor = new Color(1f, 0.5f, 0f); // Orange
                else if (dur == 60f) normalColor = new Color(1f, 1f, 0f); // Yellow
                else normalColor = new Color(0.33f, 1f, 0.33f); // Green
                break;

            case SettingType.Powerups:
                bool powerupsOn = netManager.powerupsEnabled.Value;
                valueText = $"POWERUPS: {(powerupsOn ? "ENABLED" : "DISABLED")}";
                normalColor = powerupsOn ? new Color(0.33f, 1f, 0.33f) : new Color(1f, 0.33f, 0.33f);
                break;

            case SettingType.TargetSpeed:
                float spd = netManager.targetSpeedSetting.Value;
                if (spd <= 2.5f)
                {
                    valueText = "TARGET SPEED: SLOW";
                    normalColor = new Color(0.33f, 0.8f, 1f); // Light Blue
                }
                else if (spd >= 4.5f)
                {
                    valueText = "TARGET SPEED: FAST";
                    normalColor = new Color(1f, 0.33f, 0.33f); // Red
                }
                else
                {
                    valueText = "TARGET SPEED: NORMAL";
                    normalColor = Color.white;
                }
                break;

            case SettingType.SpawnRate:
                float rate = netManager.spawnIntervalSetting.Value;
                if (rate <= 1.0f) // Note: lower time = faster spawn rate
                {
                    valueText = "SPAWN RATE: FAST";
                    normalColor = new Color(1f, 0.33f, 0.33f); // Red
                }
                else if (rate >= 2.0f)
                {
                    valueText = "SPAWN RATE: SLOW";
                    normalColor = new Color(0.33f, 0.8f, 1f); // Light Blue
                }
                else
                {
                    valueText = "SPAWN RATE: NORMAL";
                    normalColor = Color.white;
                }
                break;

            case SettingType.ChaosLevel:
                if (netManager.chaosLevelSetting.Value == 0)
                {
                    valueText = "CHAOS LEVEL: NORMAL";
                    normalColor = Color.white;
                }
                else
                {
                    valueText = "CHAOS LEVEL: HIGH";
                    normalColor = new Color(1f, 0f, 1f); // Magenta
                }
                break;
        }

        // 3. Apply the fully formatted string directly to the UI
        myText.text = valueText;

        // 4. Update the actual text color ONLY if we aren't currently hovering over it
        if (myText.color != hoverColor)
        {
            myText.color = normalColor;
        }
    }
}