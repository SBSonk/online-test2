using UnityEngine;
using Unity.Netcode;

public class PlayerBooth : NetworkBehaviour
{
    [Header("Visuals")]
    public Renderer floorHighlight;
    public Color idleColor = new Color(1f, 1f, 1f, 0.1f);   
    public Color waitingColor = new Color(1f, 1f, 0f, 0.5f); 
    public Color readyColor = new Color(0f, 1f, 0f, 0.5f);   

    // Syncs whether this booth is currently occupied across the network
    private NetworkVariable<bool> isBoothOccupied = new NetworkVariable<bool>(false);
    private int playersInside = 0;

    public override void OnNetworkSpawn()
    {
        // Listen for changes to this specific booth's status
        isBoothOccupied.OnValueChanged += (oldVal, newVal) => UpdateVisuals();
        
        // Listen for the global game state changes to toggle highlights on/off
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged += (oldState, newState) => UpdateVisuals();
        }
        
        UpdateVisuals(); // Initial draw
    }

    private void UpdateVisuals()
    {
        if (NetworkMatchManager.Instance == null) return;

        // Only show highlights if we are in the Waiting phase
        if (NetworkMatchManager.Instance.currentState.Value == NetworkMatchManager.MatchState.WaitingForPositions)
        {
            ApplyColor(isBoothOccupied.Value ? readyColor : waitingColor);
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
        if (!IsServer) return; // Only server handles logic
        
        if (other.TryGetComponent(out NetworkPlayerMovement player))
        {
            playersInside++;
            isBoothOccupied.Value = true;
            NetworkMatchManager.Instance.SetPlayerReady(player.OwnerClientId, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return; // Only server handles logic
        
        if (other.TryGetComponent(out NetworkPlayerMovement player))
        {
            playersInside = Mathf.Max(0, playersInside - 1);
            isBoothOccupied.Value = (playersInside > 0);
            NetworkMatchManager.Instance.SetPlayerReady(player.OwnerClientId, false);
        }
    }
}