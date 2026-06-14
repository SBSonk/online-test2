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
    
    [Header("Scoreboard Layout Sizing")]
    public float localPlayerFontSize = 32f;
    public float opponentFontSize = 22f;
    
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

        foreach (var textItem in spawnedScoreTexts)
        {
            textItem.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += (oldState, newState) => UpdateStateUI();
        }
    }

    private void OnDisable()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged -= (oldState, newState) => UpdateStateUI();
        }
    }

    void Update()
    {
        if (NetworkMatchManager.Instance == null) return;

        var state = NetworkMatchManager.Instance.currentState.Value;

        // Only update the HUD timer if the match is actively running
        if (state == NetworkMatchManager.MatchState.Active)
        {
            UpdateTimer(NetworkMatchManager.Instance.matchTimer.Value);
        }
        
        if (balloonShooter != null && chargeBarFill != null)
        {
            chargeBarFill.fillAmount = balloonShooter.GetChargePercentage();
        }
    }

    private void UpdateStateUI()
    {
        var state = NetworkMatchManager.Instance.currentState.Value;

        if (state == NetworkMatchManager.MatchState.Lobby)
        {
            matchTimerText.text = "LOBBY";
        }
        else if (state != NetworkMatchManager.MatchState.Active)
        {
            // Clear the text during WaitingForPositions and Countdown. 
            // The GameAnnouncer handles the visuals for these phases now!
            matchTimerText.text = ""; 
        }

        RefreshScoreboardDisplay();
    }

    // --- Real-time Sort & Conditional Text Scaling ---
    public void RefreshScoreboardDisplay()
    {
        if (scoreboardContainer == null || scoreboardTextPrefab == null) return;

        // 1. Gather all scores active across the network
        NetworkPlayerScore[] allPlayerScores = Object.FindObjectsByType<NetworkPlayerScore>(FindObjectsSortMode.None);
        List<NetworkPlayerScore> sortedScores = new List<NetworkPlayerScore>(allPlayerScores);

        // 2. Sort list by highest score first (Descending order)
        sortedScores.Sort((a, b) => b.currentScore.Value.CompareTo(a.currentScore.Value));

        int totalPlayers = sortedScores.Count;

        // 3. Maintain dynamic UI text pooling
        while (spawnedScoreTexts.Count < totalPlayers)
        {
            GameObject newObj = Instantiate(scoreboardTextPrefab, scoreboardContainer);
            if (newObj.TryGetComponent(out TextMeshProUGUI tmp))
            {
                spawnedScoreTexts.Add(tmp);
            }
        }

        // 4. Update elements matching sorted order indices
        for (int i = 0; i < spawnedScoreTexts.Count; i++)
        {
            if (i < totalPlayers)
            {
                SetupScoreSlot(spawnedScoreTexts[i], sortedScores[i]);
            }
            else
            {
                // Deactivate residual layout objects if a player drops out mid-game
                spawnedScoreTexts[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetupScoreSlot(TextMeshProUGUI slotText, NetworkPlayerScore playerScore)
    {
        slotText.gameObject.SetActive(true);
        bool isLocal = playerScore.IsOwner;

        slotText.fontSize = isLocal ? localPlayerFontSize : opponentFontSize;
        slotText.text = isLocal ? $"{playerScore.currentScore.Value.ToString("D4")} - YOU" : $"{playerScore.currentScore.Value.ToString("D4")}";

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