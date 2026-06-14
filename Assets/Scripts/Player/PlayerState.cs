// --- PlayerState.cs ---
// Manages the player's score and active powerups/penalties
using Unity.Netcode;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    public NetworkVariable<int> score = new NetworkVariable<int>(0);
    public NetworkVariable<bool> hasDoublePoints = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> hasReversedControls = new NetworkVariable<bool>(false);

    private float doublePointsTimer = 0f;
    private float reverseControlsTimer = 0f;

    private void Update()
    {
        if (!IsServer) return;

        // Handle Powerup Timers
        if (hasDoublePoints.Value)
        {
            doublePointsTimer -= Time.deltaTime;
            if (doublePointsTimer <= 0) hasDoublePoints.Value = false;
        }

        if (hasReversedControls.Value)
        {
            reverseControlsTimer -= Time.deltaTime;
            if (reverseControlsTimer <= 0) hasReversedControls.Value = false;
        }
    }

    public void AddScore(int amount)
    {
        if (!IsServer) return;

        // Apply double points modifier if the amount is positive
        if (amount > 0 && hasDoublePoints.Value)
        {
            amount *= 2;
        }

        score.Value += amount;
    }

    public void ActivateDoublePoints(float duration)
    {
        if (!IsServer) return;
        hasDoublePoints.Value = true;
        doublePointsTimer = duration;
    }

    public void ActivateReverseControls(float duration)
    {
        if (!IsServer) return;
        hasReversedControls.Value = true;
        reverseControlsTimer = duration;
    }
}