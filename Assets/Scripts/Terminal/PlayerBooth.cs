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

    [Header("Audio")]
    public AudioSource boothAudioSource;
    public AudioClip readyUpClip;

    [Header("Booth Settings")]
    public Transform boothCenter;

    public NetworkVariable<ulong> assignedClientId = new NetworkVariable<ulong>(999);
    public NetworkVariable<Color> assignedColor = new NetworkVariable<Color>(Color.white);

    private NetworkVariable<bool> isBoothOccupied = new NetworkVariable<bool>(false);
    
    private List<NetworkPlayerMovement> playersPhysicallyInside = new List<NetworkPlayerMovement>();
    private List<NetworkPlayerMovement> playersLockedAndReady = new List<NetworkPlayerMovement>();

    private bool isServerSubscribed = false;
    
    // --- CACHING FOR BULLETPROOF RENDER PERFORMANCE ---
    private MaterialPropertyBlock propBlock;
    private Color lastAppliedColor = Color.clear;

    public override void OnNetworkSpawn()
    {
        // Subscribe to the occupancy variable so all clients hear the sound
        isBoothOccupied.OnValueChanged += HandleOccupancyChanged;
    }

    public override void OnNetworkDespawn()
    {
        isBoothOccupied.OnValueChanged -= HandleOccupancyChanged;

        if (IsServer && isServerSubscribed && NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged -= HandleStateChange;
        }
    }

    private void HandleOccupancyChanged(bool oldVal, bool newVal)
    {
        // If the booth just became occupied (false -> true), play the ready sound
        if (!oldVal && newVal)
        {
            if (boothAudioSource != null && readyUpClip != null)
            {
                boothAudioSource.PlayOneShot(readyUpClip);
            }
        }
    }

    void Update()
    {
        // Wait until the manager exists
        if (NetworkMatchManager.Instance == null) return;

        // 1. SAFELY SUBSCRIBE SERVER LOGIC (No coroutines needed)
        if (IsServer && !isServerSubscribed)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += HandleStateChange;
            isServerSubscribed = true;
        }

        // 2. FORCE VISUALS TO MATCH DATA EXACTLY
        UpdateVisuals();
    }

    private void HandleStateChange(NetworkMatchManager.MatchState oldState, NetworkMatchManager.MatchState newState)
    {
        if (!IsServer) return;

        // Unlock everyone if the match starts or ends
        if (newState == NetworkMatchManager.MatchState.Active || newState == NetworkMatchManager.MatchState.Lobby)
        {
            foreach (var player in playersLockedAndReady)
            {
                if (player != null) player.SetBoothLock(false, null);
            }
            playersLockedAndReady.Clear();
            isBoothOccupied.Value = false; 
        }

        if (NetworkMatchManager.Instance.currentGameMode.Value != boothMode) return;

        // Auto-lock players who are already standing in the booth when the round requests them to get in position
        if (newState == NetworkMatchManager.MatchState.WaitingForPositions)
        {
            // Clean out disconnected/null players just in case
            playersPhysicallyInside.RemoveAll(p => p == null);

            foreach (var player in playersPhysicallyInside)
            {
                if (!playersLockedAndReady.Contains(player))
                {
                    if (player.OwnerClientId == assignedClientId.Value) LockAndReadyPlayer(player);
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        bool isActiveMode = (NetworkMatchManager.Instance.currentGameMode.Value == boothMode);
        
        // Ensure renderer is only on if this mode is active
        if (floorHighlight != null && floorHighlight.enabled != isActiveMode)
        {
            floorHighlight.enabled = isActiveMode;
        }

        if (!isActiveMode) return; 

        var state = NetworkMatchManager.Instance.currentState.Value;
        Color targetColor = idleColor;
        
        // If assigned and waiting for players, calculate the target color
        if (assignedClientId.Value != 999 && (state == NetworkMatchManager.MatchState.WaitingForPositions || state == NetworkMatchManager.MatchState.Countdown))
        {
            targetColor = isBoothOccupied.Value ? (assignedColor.Value * readyColorMultiplier) : assignedColor.Value;
            targetColor.a = 0.6f; 
        }

        // Only push to the graphics card if the color ACTUALLY changed this frame
        if (targetColor != lastAppliedColor)
        {
            lastAppliedColor = targetColor;
            ApplyColor(targetColor);
        }
    }

    private void ApplyColor(Color c)
    {
        if (floorHighlight == null) return;
        
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        
        // Using a PropertyBlock changes the color without cloning the material
        floorHighlight.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", c); // URP / HDRP support
        propBlock.SetColor("_Color", c);     // Standard Shader support
        floorHighlight.SetPropertyBlock(propBlock);
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