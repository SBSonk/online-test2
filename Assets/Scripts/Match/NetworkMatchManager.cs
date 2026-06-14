using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using NaughtyAttributes;
using System.Collections.Generic;

public class NetworkMatchManager : NetworkBehaviour
{
    public static NetworkMatchManager Instance;

    public enum GameMode { ShootingGallery, PvP }
    public enum MatchState { Lobby, WaitingForPositions, Countdown, Active }

    [Header("Match State (Synced)")]
    public NetworkVariable<MatchState> currentState = new NetworkVariable<MatchState>(MatchState.Lobby);
    public NetworkVariable<float> matchTimer = new NetworkVariable<float>(60f);
    public NetworkVariable<float> countdownTimer = new NetworkVariable<float>(3f);
    public NetworkVariable<int> playersReadyCount = new NetworkVariable<int>(0);
    private HashSet<ulong> readyPlayerIds = new HashSet<ulong>();

    [Header("Lobby Settings (Synced)")]
    public NetworkVariable<GameMode> currentGameMode = new NetworkVariable<GameMode>(GameMode.ShootingGallery);
    public NetworkVariable<float> matchDurationSetting = new NetworkVariable<float>(60f);
    public NetworkVariable<bool> powerupsEnabled = new NetworkVariable<bool>(true);

    [Header("Spawner Settings (Synced)")]
    public NetworkVariable<float> spawnIntervalSetting = new NetworkVariable<float>(1.5f);
    public NetworkVariable<float> targetSpeedSetting = new NetworkVariable<float>(3.0f);
    public NetworkVariable<int> chaosLevelSetting = new NetworkVariable<int>(0);

    [Header("Events")]
    public UnityEvent OnMatchStart;
    public UnityEvent OnMatchEnd;
    public UnityEvent OnSettingsChanged;
    public UnityEvent OnWaitingForPositions;
    public UnityEvent<int> OnCountdownTick;

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
        powerupsEnabled.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        spawnIntervalSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        targetSpeedSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        chaosLevelSetting.OnValueChanged += (o, n) => OnSettingsChanged?.Invoke();
        currentState.OnValueChanged += HandleStateChanged;
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
                TriggerCountdownTickRpc(currentSecond);
            }
            if (countdownTimer.Value <= 0) StartActiveGame();
        }
        else if (currentState.Value == MatchState.Active)
        {
            matchTimer.Value -= Time.deltaTime;
            if (matchTimer.Value <= 0) EndMatch();
        }
    }

    // ==========================================
    // BOOTH & MATCH FLOW
    // ==========================================

    public void SetPlayerReady(ulong clientId, bool isReady)
    {
        if (!IsServer) return;
        if (isReady) readyPlayerIds.Add(clientId);
        else readyPlayerIds.Remove(clientId);

        playersReadyCount.Value = readyPlayerIds.Count;

        if (currentState.Value == MatchState.WaitingForPositions && readyPlayerIds.Count >= NetworkManager.ConnectedClients.Count)
        {
            StartCountdown();
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
        readyPlayerIds.Clear();
        playersReadyCount.Value = 0;
        currentState.Value = MatchState.WaitingForPositions;
    }

    private void StartCountdown()
    {
        countdownTimer.Value = 3f;
        lastCountdownSecond = 3;
        currentState.Value = MatchState.Countdown;
        TriggerCountdownTickRpc(3);
    }

    private void StartActiveGame()
    {
        matchTimer.Value = matchDurationSetting.Value;
        currentState.Value = MatchState.Active;
    }

    private void EndMatch() => currentState.Value = MatchState.Lobby;

    // ==========================================
    // SETTINGS RPCs
    // ==========================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetGameModeRpc(GameMode newMode) { if (currentState.Value == MatchState.Lobby) currentGameMode.Value = newMode; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetDurationRpc(float newDuration) { if (currentState.Value == MatchState.Lobby) matchDurationSetting.Value = newDuration; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTogglePowerupsRpc() { if (currentState.Value == MatchState.Lobby) powerupsEnabled.Value = !powerupsEnabled.Value; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpeedRpc(float newSpeed) { if (currentState.Value == MatchState.Lobby) targetSpeedSetting.Value = newSpeed; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetChaosRpc(int level) { if (currentState.Value == MatchState.Lobby) chaosLevelSetting.Value = level; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetSpawnRateRpc(float newRate) { if (currentState.Value == MatchState.Lobby) spawnIntervalSetting.Value = newRate; }

    [Rpc(SendTo.Everyone)]
    private void TriggerCountdownTickRpc(int second) => OnCountdownTick?.Invoke(second);
}