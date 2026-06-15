using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections; 

public class NetworkMatchManager : NetworkBehaviour
{
    public static NetworkMatchManager Instance;

    public enum GameMode { ShootingGallery, PvP }
    public enum MatchState { Lobby, WaitingForPositions, Countdown, Active }
    
    public enum PowerupMode { Off, On, Chaos }

    [Header("Match State (Synced)")]
    public NetworkVariable<MatchState> currentState = new NetworkVariable<MatchState>(MatchState.Lobby);
    public NetworkVariable<float> matchTimer = new NetworkVariable<float>(60f);
    public NetworkVariable<float> countdownTimer = new NetworkVariable<float>(3f);
    public NetworkVariable<int> playersReadyCount = new NetworkVariable<int>(0);
    private HashSet<ulong> readyPlayerIds = new HashSet<ulong>();

    [Header("UI & Announcer References")]
    public GameAnnouncer announcer;
    public DiegeticLeaderboard leaderboard;

    [Header("Music Settings")]
    public AudioSource musicSource;
    public float crossfadeDuration = 1.0f;
    
    [Header("Lobby Music")]
    public AudioClip lobbyMusic;
    [Range(0f, 1f)] public float lobbyVolume = 0.5f;

    [Header("Waiting Music")]
    public AudioClip waitingMusic; 
    [Range(0f, 1f)] public float waitingVolume = 0.6f;

    [Header("Active Match Music")]
    public AudioClip activeMusic;  
    [Range(0f, 1f)] public float activeVolume = 0.8f;

    // --- Audio Ducking and Crossfade Variables ---
    private AudioSource secondaryMusicSource;
    private Coroutine crossfadeCoroutine;
    private Coroutine duckingCoroutine;
    
    private float currentTrackBaseVolume = 0f;
    private float secondaryTrackBaseVolume = 0f;
    private float duckingMultiplier = 1f; 
    private AudioClip currentMusicClip;

    [Header("Minigame Settings")]
    public int firstArrivalBonusPoints = 250;
    private bool isFirstArrivalAwarded = false; 

    [Header("Lobby Settings (Synced)")]
    public NetworkVariable<GameMode> currentGameMode = new NetworkVariable<GameMode>(GameMode.ShootingGallery);
    public NetworkVariable<float> matchDurationSetting = new NetworkVariable<float>(60f);
    public NetworkVariable<PowerupMode> powerupModeSetting = new NetworkVariable<PowerupMode>(PowerupMode.On);

    [Header("Spawner Settings (Synced)")]
    public NetworkVariable<float> spawnIntervalSetting = new NetworkVariable<float>(1.5f);
    public NetworkVariable<float> targetSpeedSetting = new NetworkVariable<float>(3.0f);

    [Header("Booth Assignment")]
    public PlayerColorSet[] colorSets; 

    [Header("Events")]
    public UnityEvent OnMatchStart;
    public UnityEvent OnMatchEnd;
    public UnityEvent OnSettingsChanged;
    public UnityEvent OnWaitingForPositions;

    private bool isMatchEnding = false;
    private int lastCountdownSecond = 3;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (musicSource != null)
        {
            secondaryMusicSource = gameObject.AddComponent<AudioSource>();
            secondaryMusicSource.spatialBlend = musicSource.spatialBlend;
            secondaryMusicSource.loop = true;
            secondaryMusicSource.volume = 0f;
        }
    }

    public override void OnNetworkSpawn()
    {
        currentGameMode.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        matchDurationSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        powerupModeSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke(); 
        spawnIntervalSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        targetSpeedSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        
        currentState.OnValueChanged += HandleStateChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectionChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientConnectionChanged;
        }

        UpdateMusicForState(currentState.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectionChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientConnectionChanged;
        }
    }

    private void HandleClientConnectionChanged(ulong clientId)
    {
        StartCoroutine(DelayedLeaderboardUpdate());
    }

    private IEnumerator DelayedLeaderboardUpdate()
    {
        yield return new WaitForSeconds(0.5f);
        UpdateLeaderboardRpc();
    }

    private void HandleStateChanged(MatchState oldState, MatchState newState)
    {
        if (newState == MatchState.WaitingForPositions) OnWaitingForPositions?.Invoke();
        if (newState == MatchState.Active) OnMatchStart?.Invoke();
        if (newState == MatchState.Lobby && oldState == MatchState.Active) OnMatchEnd?.Invoke();

        UpdateMusicForState(newState);
    }

    // ==========================================
    // MUSIC LOGIC (CROSSFADE & DUCKING)
    // ==========================================
    private void UpdateMusicForState(MatchState state)
    {
        if (musicSource == null || secondaryMusicSource == null) return;

        AudioClip nextClip = null;
        float targetVolume = 1f;

        switch (state)
        {
            case MatchState.Lobby: 
                nextClip = lobbyMusic; 
                targetVolume = lobbyVolume;
                break;
            case MatchState.WaitingForPositions:
            case MatchState.Countdown: 
                nextClip = waitingMusic; 
                targetVolume = waitingVolume;
                break;
            case MatchState.Active: 
                nextClip = activeMusic; 
                targetVolume = activeVolume;
                break;
        }

        if (currentMusicClip == null)
        {
            currentMusicClip = nextClip;
            musicSource.clip = nextClip;
            musicSource.Play();
            currentTrackBaseVolume = targetVolume;
        }
        else if (nextClip != null && currentMusicClip != nextClip)
        {
            currentMusicClip = nextClip;
            
            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(nextClip, targetVolume));
        }
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float targetVolume)
    {
        AudioSource fadingOut = musicSource;
        AudioSource fadingIn = secondaryMusicSource;

        fadingIn.clip = newClip;
        fadingIn.time = 0f;
        secondaryTrackBaseVolume = 0f; 
        fadingIn.Play();

        float t = 0f;
        float startVolOut = currentTrackBaseVolume;

        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float percent = t / crossfadeDuration;
            
            // Crossfade math now respects the specific volume set in the inspector
            secondaryTrackBaseVolume = Mathf.Lerp(0f, targetVolume, percent);
            currentTrackBaseVolume = Mathf.Lerp(startVolOut, 0f, percent);
            
            yield return null;
        }

        fadingOut.Stop();

        musicSource = fadingIn;
        secondaryMusicSource = fadingOut;
        
        currentTrackBaseVolume = targetVolume;
        secondaryTrackBaseVolume = 0f;
    }

    private void DuckMusicFor(float duration)
    {
        if (duckingCoroutine != null) StopCoroutine(duckingCoroutine);
        duckingCoroutine = StartCoroutine(DuckingRoutine(duration));
    }

    private IEnumerator DuckingRoutine(float duration)
    {
        float targetDuckMult = 0.2f; 
        float fadeOutTime = 0.15f;   
        float fadeInTime = 0.5f;     

        float t = 0f;
        float startMult = duckingMultiplier;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            duckingMultiplier = Mathf.Lerp(startMult, targetDuckMult, t / fadeOutTime);
            yield return null;
        }
        duckingMultiplier = targetDuckMult;

        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            duckingMultiplier = Mathf.Lerp(targetDuckMult, 1f, t / fadeInTime);
            yield return null;
        }
        duckingMultiplier = 1f;
    }

    private void Update()
    {
        // 1. APPLY AUDIO VOLUMES LOCALLY FOR EVERYONE
        if (musicSource != null) musicSource.volume = currentTrackBaseVolume * duckingMultiplier;
        if (secondaryMusicSource != null) secondaryMusicSource.volume = secondaryTrackBaseVolume * duckingMultiplier;

        // 2. SERVER LOGIC ONLY
        if (!IsServer) return;

        if (currentState.Value == MatchState.Countdown)
        {
            countdownTimer.Value -= Time.deltaTime;
            int currentSecond = Mathf.CeilToInt(countdownTimer.Value);
            
            if (currentSecond != lastCountdownSecond && currentSecond > 0)
            {
                lastCountdownSecond = currentSecond;
                TriggerAnnouncerMessageRpc(currentSecond.ToString(), 0.8f);
            }
            if (countdownTimer.Value <= 0) StartActiveGame();
        }
        else if (currentState.Value == MatchState.Active)
        {
            if (!isMatchEnding)
            {
                matchTimer.Value -= Time.deltaTime;
                if (matchTimer.Value <= 0) 
                {
                    isMatchEnding = true;
                    EndMatch();
                }
            }
        }
    }

    // ==========================================
    // BOOTH & MATCH FLOW
    // ==========================================

    private void AssignBoothsToPlayers()
    {
        if (!IsServer) return;

        PlayerBooth[] allBooths = FindObjectsByType<PlayerBooth>(FindObjectsSortMode.None);
        List<PlayerBooth> activeBooths = new List<PlayerBooth>();

        foreach (var booth in allBooths)
        {
            booth.assignedClientId.Value = 999; 
            if (booth.boothMode == currentGameMode.Value)
            {
                activeBooths.Add(booth);
            }
        }

        int boothIndex = 0;
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (boothIndex >= activeBooths.Count) break;
            
            var booth = activeBooths[boothIndex];
            booth.assignedClientId.Value = clientId;
            
            if (colorSets.Length > 0)
            {
                int safeIndex = (int)(clientId % (ulong)colorSets.Length);
                booth.assignedColor.Value = colorSets[safeIndex].playerColor;
            }
            boothIndex++;
        }
    }

    private void ResetBooths()
    {
        PlayerBooth[] allBooths = FindObjectsByType<PlayerBooth>(FindObjectsSortMode.None);
        foreach (var booth in allBooths) 
        {
            booth.assignedClientId.Value = 999;
        }
    }

    public void SetPlayerReady(ulong clientId, bool isReady)
    {
        if (!IsServer) return;

        if (isReady)
        {
            readyPlayerIds.Add(clientId);
            if (currentState.Value == MatchState.WaitingForPositions && !isFirstArrivalAwarded)
            {
                isFirstArrivalAwarded = true;
                AwardArrivalBonus(clientId);
            }
        }
        else readyPlayerIds.Remove(clientId);

        playersReadyCount.Value = readyPlayerIds.Count;
        if (currentState.Value == MatchState.WaitingForPositions && readyPlayerIds.Count >= NetworkManager.ConnectedClients.Count)
        {
            StartCountdown();
        }
    }

    private void AwardArrivalBonus(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out NetworkPlayerScore scoreSystem))
            {
                scoreSystem.AddPoints(firstArrivalBonusPoints);
            }
        }
    }

    public void RequestStartMatch()
    {
        if (IsServer) StartWaitingPhase();
        else StartWaitingPhaseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StartWaitingPhaseServerRpc() => StartWaitingPhase();

    private void StartWaitingPhase()
    {
        if (currentState.Value != MatchState.Lobby) return;
        
        isMatchEnding = false; 
        
        readyPlayerIds.Clear();
        playersReadyCount.Value = 0;
        isFirstArrivalAwarded = false; 

        AssignBoothsToPlayers(); 
        currentState.Value = MatchState.WaitingForPositions;

        UpdateLeaderboardRpc();
        TriggerAnnouncerSequenceRpc("GET,TO,YOUR,BOOTHS!", 0.4f);
    }
    
    private void StartCountdown()
    {
        countdownTimer.Value = 3f;
        lastCountdownSecond = 3;
        currentState.Value = MatchState.Countdown;
        TriggerAnnouncerMessageRpc("3", 0.8f);
    }

    private void StartActiveGame()
    {
        matchTimer.Value = matchDurationSetting.Value;
        currentState.Value = MatchState.Active;
        TriggerAnnouncerMessageRpc("GO!", 1.5f);
    }

    private void EndMatch() 
    {
        var allScores = FindObjectsByType<NetworkPlayerScore>();
        foreach(var score in allScores) score.IncrementGamesPlayed();

        TriggerAnnouncerMessageRpc("THAT'S ALL FOLKS!", 3f);
        UpdateLeaderboardRpc();

        StartCoroutine(EndMatchRoutine());
    }

    private IEnumerator EndMatchRoutine()
    {
        // Wait for the announcer message
        yield return new WaitForSeconds(3f);

        // --- NEW: Smoothly fade out the active music before transitioning ---
        if (musicSource != null)
        {
            StartCoroutine(FadeOutCurrentMusic(1.5f));
        }

        yield return new WaitForSeconds(1.5f);

        ResetBooths();
        currentState.Value = MatchState.Lobby;
        // musicSource will naturally be replaced by the Lobby music when 
        // the currentState OnValueChanged callback triggers UpdateMusicForState!
    }

    // --- NEW: Helper to fade out before switching tracks ---
    private IEnumerator FadeOutCurrentMusic(float duration)
    {
        float startVol = currentTrackBaseVolume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            currentTrackBaseVolume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        currentTrackBaseVolume = 0f;
    }

    // ==========================================
    // RPCs
    // ==========================================

    [Rpc(SendTo.Everyone)]
    private void TriggerAnnouncerSequenceRpc(string commaSeparatedWords, float delay) 
    {
        if(announcer != null) 
        {
            string[] words = commaSeparatedWords.Split(',');
            float totalDuration = (words.Length * delay) + 0.5f;
            DuckMusicFor(totalDuration);
            announcer.AnnounceSequence(words, delay);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerAnnouncerMessageRpc(string message, float duration) 
    {
        DuckMusicFor(duration);
        if(announcer != null) announcer.ShowMessage(message, duration);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateLeaderboardRpc()
    {
        if(leaderboard != null) leaderboard.RefreshScores();
    }

    // ==========================================
    // SETTINGS RPCs
    // ==========================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetGameModeRpc(GameMode newMode) { if (currentState.Value == MatchState.Lobby) currentGameMode.Value = newMode; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetDurationRpc(float newDuration) { if (currentState.Value == MatchState.Lobby) matchDurationSetting.Value = newDuration; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetPowerupModeRpc(PowerupMode newMode) { if (currentState.Value == MatchState.Lobby) powerupModeSetting.Value = newMode; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpeedRpc(float newSpeed) { if (currentState.Value == MatchState.Lobby) targetSpeedSetting.Value = newSpeed; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpawnRateRpc(float newRate) { if (currentState.Value == MatchState.Lobby) spawnIntervalSetting.Value = newRate; }
}