using Unity.Netcode;
using UnityEngine;

public class TargetMovement : NetworkBehaviour
{
    public NetworkVariable<float> assignedSpeed = new NetworkVariable<float>(3f);
    public NetworkVariable<Vector3> travelDirection = new NetworkVariable<Vector3>(Vector3.right);
    public NetworkVariable<float> maxTravelDistance = new NetworkVariable<float>(50f); 
    
    // --- NEW: Toggle for drifting ---
    public NetworkVariable<bool> isDrifting = new NetworkVariable<bool>(false);

    [Header("Drift Settings")]
    public float driftAmplitude = 0.5f; // How far up/down it bobs
    public float driftFrequency = 2.0f; // How fast it bobs

    private float distanceTraveled = 0f;
    private Vector3 basePosition; // The "center" path of the target

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Initialize the base position at the moment of spawn
        basePosition = transform.position;
    }

    private void Update()
    {
        float step = assignedSpeed.Value * Time.deltaTime;
        
        // 1. Move the base position along the track
        basePosition += travelDirection.Value * step;

        // 2. Apply Drift (if enabled)
        Vector3 finalPosition = basePosition;
        if (isDrifting.Value)
        {
            float bobbing = Mathf.Sin(Time.time * driftFrequency) * driftAmplitude;
            finalPosition += Vector3.up * bobbing;
        }

        transform.position = finalPosition;

        // 3. Cleanup
        if (IsServer)
        {
            distanceTraveled += step;
            if (distanceTraveled >= maxTravelDistance.Value)
            {
                DespawnTarget();
            }
        }
    }

    private void DespawnTarget()
    {
        if (IsSpawned) GetComponent<NetworkObject>().Despawn(true);
    }
}