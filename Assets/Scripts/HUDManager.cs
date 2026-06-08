using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Match UI")]
    public TextMeshProUGUI matchTimerText;

    [Header("Dynamic Scoreboard")]
    [Tooltip("The UI Panel that has a Vertical Layout Group component attached.")]
    public Transform scoreboardContainer; 
    [Tooltip("A prefab containing a single TextMeshProUGUI component.")]
    public GameObject scoreboardTextPrefab; 
    
    // We pool the text objects here so we don't Instantiate/Destroy constantly
    private List<TextMeshProUGUI> spawnedScoreTexts = new List<TextMeshProUGUI>();

    [Header("Player Health (3 Lives)")]
    public Image[] lifeIcons; 
    public Sprite activeLifeSprite; 
    public Sprite lostLifeSprite;   

    [Header("Weapon UI")]
    public Image chargeBarFill; 
    public GameObject hitmarker;
    public float hitmarkerStayTime = 0.1f;

    private NetworkBalloonShooter balloonShooter;

    public void Initialize(NetworkBalloonShooter shooter)
    {
        this.balloonShooter = shooter;
        
        if (hitmarker != null) hitmarker.SetActive(false);
        if (chargeBarFill != null) chargeBarFill.fillAmount = 0f;

        // Hide any leftover scoreboard texts on initialization
        foreach (var textItem in spawnedScoreTexts)
        {
            textItem.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (balloonShooter != null && chargeBarFill != null)
        {
            chargeBarFill.fillAmount = balloonShooter.GetChargePercentage();
        }

        if (NetworkMatchManager.Instance != null && NetworkMatchManager.Instance.isGameActive.Value)
        {
            UpdateTimer(NetworkMatchManager.Instance.matchTimer.Value);
        }
    }

    // --- REWORKED: Dynamic Instantiation & Color Tinting ---
    public void RefreshScoreboardDisplay()
    {
        if (scoreboardContainer == null || scoreboardTextPrefab == null) return;

        NetworkPlayerScore[] allPlayerScores = Object.FindObjectsByType<NetworkPlayerScore>(FindObjectsSortMode.None);
        
        NetworkPlayerScore localPlayer = null;
        List<NetworkPlayerScore> opponents = new List<NetworkPlayerScore>();

        foreach (var pScore in allPlayerScores)
        {
            if (pScore.IsOwner) localPlayer = pScore;
            else opponents.Add(pScore);
        }

        int totalPlayers = (localPlayer != null ? 1 : 0) + opponents.Count;

        // 1. Ensure we have enough Text objects spawned in our pool
        while (spawnedScoreTexts.Count < totalPlayers)
        {
            GameObject newObj = Instantiate(scoreboardTextPrefab, scoreboardContainer);
            if (newObj.TryGetComponent(out TextMeshProUGUI tmp))
            {
                spawnedScoreTexts.Add(tmp);
            }
        }

        // 2. Hide all texts initially
        foreach (var textItem in spawnedScoreTexts)
        {
            textItem.gameObject.SetActive(false);
        }

        int visualSlotIndex = 0;

        // 3. Setup Local Player (Always at the top)
        if (localPlayer != null)
        {
            SetupScoreSlot(spawnedScoreTexts[visualSlotIndex], localPlayer, true);
            visualSlotIndex++;
        }

        // 4. Setup Opponents
        for (int i = 0; i < opponents.Count; i++)
        {
            SetupScoreSlot(spawnedScoreTexts[visualSlotIndex], opponents[i], false);
            visualSlotIndex++;
        }
    }

    private void SetupScoreSlot(TextMeshProUGUI slotText, NetworkPlayerScore playerScore, bool isLocal)
    {
        slotText.gameObject.SetActive(true);

        // Set the text
        string suffix = isLocal ? "(You)" : $"Player {playerScore.OwnerClientId + 1}";
        slotText.text = $"{playerScore.currentScore.Value.ToString("D4")} {suffix}";

        // Grab the player's color manager and tint the text!
        if (playerScore.TryGetComponent(out PlayerColorManager colorManager))
        {
            int pIndex = colorManager.colorIndex.Value;
            var sets = colorManager.colorSets;
            
            if (sets.Length > 0)
            {
                Color themeColor = sets[pIndex % sets.Length].playerColor;
                slotText.color = themeColor;
            }
        }
    }

    // ... (Keep your UpdateTimer, UpdateLives, ShowHitMarker, and HitAnimation methods exactly the same) ...
    public void UpdateTimer(float timeRemaining)
    {
        if (matchTimerText != null)
        {
            int seconds = Mathf.CeilToInt(timeRemaining);
            matchTimerText.text = $"Time: {seconds:D2}s"; 
        }
    }

    public void UpdateLives(int currentLives)
    {
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            lifeIcons[i].sprite = (i < currentLives) ? activeLifeSprite : lostLifeSprite;
        }
    }

    public void ShowHitMarker()
    {
        if (gameObject.activeInHierarchy && hitmarker != null)
        {
            StopAllCoroutines(); 
            StartCoroutine(HitAnimation());
        }
    }

    private IEnumerator HitAnimation()
    {
        hitmarker.SetActive(true);
        yield return new WaitForSeconds(hitmarkerStayTime);
        hitmarker.SetActive(false);
    }
}