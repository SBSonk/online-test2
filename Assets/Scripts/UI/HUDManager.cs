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
    private float refreshTimer = 0f;
    private Coroutine hitmarkerCoroutine;
    private RectTransform containerRectTransform;

    void Awake()
    {
        if (scoreboardContainer != null)
        {
            containerRectTransform = scoreboardContainer.GetComponent<RectTransform>();
        }
    }

    public void Initialize(NetworkBalloonShooter shooter)
    {
        this.balloonShooter = shooter;
        
        if (hitmarker != null) hitmarker.SetActive(false);
        if (matchTimerText != null) matchTimerText.text = "";
        if (chargeBarFill != null) chargeBarFill.fillAmount = 0f;

        foreach (var textItem in spawnedScoreTexts)
        {
            textItem.gameObject.SetActive(false);
        }

        // Refresh immediately so the scoreboard is visible the moment the player joins
        RefreshScoreboardDisplay();
    }

    private void OnEnable()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += HandleMatchStateChanged;
        }
    }

    private void OnDisable()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged -= HandleMatchStateChanged;
        }
    }

    private void HandleMatchStateChanged(NetworkMatchManager.MatchState oldState, NetworkMatchManager.MatchState newState)
    {
        UpdateStateUI();
    }

    void Update()
    {
        if (NetworkMatchManager.Instance == null) return;

        // Refresh the scoreboard every 0.5 seconds for performance
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 0.5f)
        {
            RefreshScoreboardDisplay();
            refreshTimer = 0f;
        }

        var state = NetworkMatchManager.Instance.currentState.Value;
        float timeRemaining = NetworkMatchManager.Instance.matchTimer.Value;

        // ONLY show the timer if we are in the Active state AND the timer hasn't hit 0 yet
        if (state == NetworkMatchManager.MatchState.Active && timeRemaining > 0)
        {
            UpdateTimer(timeRemaining);
        }
        else
        {
            // Wipe the text instantly if the match is over or we are in the lobby/countdown
            if (matchTimerText != null && matchTimerText.text != "")
            {
                matchTimerText.text = "";
            }
        }
        
        if (balloonShooter != null && chargeBarFill != null)
        {
            chargeBarFill.fillAmount = balloonShooter.GetChargePercentage();
        }
    }

    private void UpdateStateUI()
    {
        var state = NetworkMatchManager.Instance.currentState.Value;

        // Wipe the timer text completely if the game isn't actively running
        if (state != NetworkMatchManager.MatchState.Active)
        {
            if (matchTimerText != null) matchTimerText.text = ""; 
        }

        RefreshScoreboardDisplay();
    }

    public void RefreshScoreboardDisplay()
    {
        if (scoreboardContainer == null || scoreboardTextPrefab == null) return;

        NetworkPlayerScore[] allPlayerScores = Object.FindObjectsByType<NetworkPlayerScore>();
        List<NetworkPlayerScore> sortedScores = new List<NetworkPlayerScore>(allPlayerScores);

        sortedScores.Sort((a, b) => b.currentScore.Value.CompareTo(a.currentScore.Value));

        int totalPlayers = sortedScores.Count;

        while (spawnedScoreTexts.Count < totalPlayers)
        {
            GameObject newObj = Instantiate(scoreboardTextPrefab, scoreboardContainer);
            if (newObj.TryGetComponent(out TextMeshProUGUI tmp))
            {
                spawnedScoreTexts.Add(tmp);
            }
        }

        for (int i = 0; i < spawnedScoreTexts.Count; i++)
        {
            if (i < totalPlayers)
            {
                SetupScoreSlot(spawnedScoreTexts[i], sortedScores[i]);
            }
            else
            {
                spawnedScoreTexts[i].gameObject.SetActive(false);
            }
        }

        // ====================================================================
        // THE OVERLAP FIX:
        // Forces Unity's Canvas and Layout systems to process text sizing data 
        // right now instead of waiting a frame, snapping elements into order.
        // ====================================================================
        if (containerRectTransform != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRectTransform);
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
            matchTimerText.text = seconds.ToString(); 
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
            // FIXED: Isolated to a single tracked coroutine so it doesn't clear layout operations
            if (hitmarkerCoroutine != null) StopCoroutine(hitmarkerCoroutine);
            hitmarkerCoroutine = StartCoroutine(HitAnimation());
        }
    }

    private IEnumerator HitAnimation()
    {
        hitmarker.SetActive(true);
        yield return new WaitForSeconds(hitmarkerStayTime);
        hitmarker.SetActive(false);
        hitmarkerCoroutine = null;
    }
}