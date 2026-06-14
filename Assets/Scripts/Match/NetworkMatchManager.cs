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
    
    // --- NEW: Consolidated Powerup Mode Enum ---
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

    [Header("Minigame Settings")]
    public int firstArrivalBonusPoints = 250;
    private bool isFirstArrivalAwarded = false; 

    [Header("Lobby Settings (Synced)")]
    public NetworkVariable<GameMode> currentGameMode = new NetworkVariable<GameMode>(GameMode.ShootingGallery);
    public NetworkVariable<float> matchDurationSetting = new NetworkVariable<float>(60f);
    
    // --- THE FIX: Replaced powerups/chaos with one consolidated setting ---
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
    }

    public override void OnNetworkSpawn()
    {
        currentGameMode.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        matchDurationSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        powerupModeSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke(); // Hooked up the new mode
        spawnIntervalSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        targetSpeedSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        currentState.OnValueChanged += HandleStateChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectionChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientConnectionChanged;
        }
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
    }

    private void Update()
    {
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

        GameObject[] boothObjects = GameObject.FindGameObjectsWithTag("PlayerBooth");
        foreach (var obj in boothObjects)
        {
            if (obj.TryGetComponent(out PlayerBooth booth))
            {
                booth.assignedClientId.Value = 999; 
            }
        }

        int boothIndex = 0;
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (boothIndex >= boothObjects.Length) break;
            if (boothObjects[boothIndex].TryGetComponent(out PlayerBooth booth))
            {
                booth.assignedClientId.Value = clientId;
                if (colorSets.Length > 0)
                {
                    int safeIndex = (int)(clientId % (ulong)colorSets.Length);
                    booth.assignedColor.Value = colorSets[safeIndex].playerColor;
                }
            }
            boothIndex++;
        }
    }

    private void ResetBooths()
    {
        GameObject[] boothObjects = GameObject.FindGameObjectsWithTag("PlayerBooth");
        foreach (var obj in boothObjects)
        {
            if (obj.TryGetComponent(out PlayerBooth booth)) booth.assignedClientId.Value = 999;
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
        var allScores = FindObjectsByType<NetworkPlayerScore>(FindObjectsSortMode.None);
        foreach(var score in allScores) score.IncrementGamesPlayed();

        TriggerAnnouncerMessageRpc("THAT'S ALL FOLKS!", 3f);
        UpdateLeaderboardRpc();

        StartCoroutine(EndMatchRoutine());
    }

    private IEnumerator EndMatchRoutine()
    {
        yield return new WaitForSeconds(3f);
        ResetBooths();
        currentState.Value = MatchState.Lobby;
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
            announcer.AnnounceSequence(words, delay);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerAnnouncerMessageRpc(string message, float duration) 
    {
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

    // --- THE FIX: Replaced the old toggles with the consolidated Mode RPC ---
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetPowerupModeRpc(PowerupMode newMode) { if (currentState.Value == MatchState.Lobby) powerupModeSetting.Value = newMode; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpeedRpc(float newSpeed) { if (currentState.Value == MatchState.Lobby) targetSpeedSetting.Value = newSpeed; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpawnRateRpc(float newRate) { if (currentState.Value == MatchState.Lobby) spawnIntervalSetting.Value = newRate; }
}