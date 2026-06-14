using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerBooth : NetworkBehaviour
{
    [Header("Mode Assignment")]
    [Tooltip("Which game mode does this specific booth belong to?")]
    public NetworkMatchManager.GameMode boothMode = NetworkMatchManager.GameMode.ShootingGallery;

    [Header("Visuals")]
    public Renderer floorHighlight;
    public Color idleColor = new Color(1f, 1f, 1f, 0.1f);   
    public Color readyColorMultiplier = new Color(0.5f, 1.5f, 0.5f, 1f); 

    [Header("Booth Settings")]
    public Transform boothCenter;

    public NetworkVariable<ulong> assignedClientId = new NetworkVariable<ulong>(999);
    public NetworkVariable<Color> assignedColor = new NetworkVariable<Color>(Color.white);

    private NetworkVariable<bool> isBoothOccupied = new NetworkVariable<bool>(false);
    
    private List<NetworkPlayerMovement> playersPhysicallyInside = new List<NetworkPlayerMovement>();
    private List<NetworkPlayerMovement> playersLockedAndReady = new List<NetworkPlayerMovement>();

    public override void OnNetworkSpawn()
    {
        isBoothOccupied.OnValueChanged += (oldVal, newVal) => UpdateVisuals();
        assignedColor.OnValueChanged += (oldVal, newVal) => UpdateVisuals();
        assignedClientId.OnValueChanged += (oldVal, newVal) => UpdateVisuals(); 
        
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += HandleStateChange;
            NetworkMatchManager.Instance.currentGameMode.OnValueChanged += (oldMode, newMode) => UpdateVisuals();
        }
        
        UpdateVisuals(); 
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged -= HandleStateChange;
            NetworkMatchManager.Instance.currentGameMode.OnValueChanged -= (oldMode, newMode) => UpdateVisuals();
        }
    }

    private void HandleStateChange(NetworkMatchManager.MatchState oldState, NetworkMatchManager.MatchState newState)
    {
        UpdateVisuals();

        if (!IsServer) return;

        // --- THE FIX: Completely wipe all internal states when leaving the Waiting phase ---
        if (newState == NetworkMatchManager.MatchState.Active || newState == NetworkMatchManager.MatchState.Lobby)
        {
            foreach (var player in playersLockedAndReady)
            {
                if (player != null) player.SetBoothLock(false, null);
            }
            playersLockedAndReady.Clear();
            
            // This is the variable that was getting stuck!
            isBoothOccupied.Value = false; 
        }

        if (NetworkMatchManager.Instance.currentGameMode.Value != boothMode) return;

        // If a new match starts and players are already standing in the booth, lock them!
        if (newState == NetworkMatchManager.MatchState.WaitingForPositions)
        {
            foreach (var player in playersPhysicallyInside)
            {
                if (player != null && !playersLockedAndReady.Contains(player))
                {
                    if (player.OwnerClientId == assignedClientId.Value) LockAndReadyPlayer(player);
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        if (NetworkMatchManager.Instance == null) return;

        bool isActiveMode = (NetworkMatchManager.Instance.currentGameMode.Value == boothMode);
        if (floorHighlight != null) floorHighlight.enabled = isActiveMode;

        if (!isActiveMode) return; 

        var state = NetworkMatchManager.Instance.currentState.Value;
        
        if (assignedClientId.Value != 999 && (state == NetworkMatchManager.MatchState.WaitingForPositions || state == NetworkMatchManager.MatchState.Countdown))
        {
            Color displayColor = isBoothOccupied.Value ? (assignedColor.Value * readyColorMultiplier) : assignedColor.Value;
            displayColor.a = 0.6f; 
            ApplyColor(displayColor);
        }
        else
        {
            ApplyColor(idleColor);
        }
    }

    private void ApplyColor(Color c)
    {
        if (floorHighlight != null)
        {
            floorHighlight.material.color = c;
            if (floorHighlight.material.HasProperty("_BaseColor"))
                floorHighlight.material.SetColor("_BaseColor", c);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; 
        
        if (NetworkMatchManager.Instance.currentGameMode.Value != boothMode) return;
        
        if (other.TryGetComponent(out NetworkPlayerMovement player))
        {
            if (!playersPhysicallyInside.Contains(player)) 
                playersPhysicallyInside.Add(player);

            if (NetworkMatchManager.Instance.currentState.Value == NetworkMatchManager.MatchState.WaitingForPositions)
            {
                if (player.OwnerClientId == assignedClientId.Value) LockAndReadyPlayer(player);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return; 
        if (NetworkMatchManager.Instance.currentGameMode.Value != boothMode) return;
        
        if (other.TryGetComponent(out NetworkPlayerMovement player))
        {
            if (playersPhysicallyInside.Contains(player)) 
                playersPhysicallyInside.Remove(player);

            if (NetworkMatchManager.Instance.currentState.Value == NetworkMatchManager.MatchState.WaitingForPositions)
            {
                if (player.OwnerClientId == assignedClientId.Value) UnlockAndUnreadyPlayer(player);
            }
        }
    }

    private void LockAndReadyPlayer(NetworkPlayerMovement player)
    {
        if (!playersLockedAndReady.Contains(player))
        {
            playersLockedAndReady.Add(player);
            isBoothOccupied.Value = true;
            
            NetworkMatchManager.Instance.SetPlayerReady(player.OwnerClientId, true);

            Transform targetTransform = boothCenter != null ? boothCenter : transform; 
            player.SetBoothLock(true, targetTransform);
        }
    }

    private void UnlockAndUnreadyPlayer(NetworkPlayerMovement player)
    {
        if (playersLockedAndReady.Contains(player))
        {
            playersLockedAndReady.Remove(player);
            isBoothOccupied.Value = (playersLockedAndReady.Count > 0);
            
            NetworkMatchManager.Instance.SetPlayerReady(player.OwnerClientId, false);
            player.SetBoothLock(false, null);
        }
    }
}