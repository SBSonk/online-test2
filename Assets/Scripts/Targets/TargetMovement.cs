using Unity.Netcode;
using UnityEngine;

public class TargetMovement : NetworkBehaviour
{
    public NetworkVariable<float> assignedSpeed = new NetworkVariable<float>(3f);
    public NetworkVariable<Vector3> travelDirection = new NetworkVariable<Vector3>(Vector3.right);
    public NetworkVariable<float> maxTravelDistance = new NetworkVariable<float>(50f); 

    private float distanceTraveled = 0f;

    private void Update()
    {
        float step = assignedSpeed.Value * Time.deltaTime;
        
        // Move along the track
        transform.Translate(travelDirection.Value * step, Space.World);

        // Cleanup
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