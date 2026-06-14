// --- TargetMovement.cs ---
using Unity.Netcode;
using UnityEngine;

public class TargetMovement : NetworkBehaviour
{
    private float moveSpeed = 3f;
    private float lifeTime = 10f; // Failsafe to destroy targets that go off-screen

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        
        // Pull speed from the match manager settings
        if (NetworkMatchManager.Instance != null)
        {
            moveSpeed = NetworkMatchManager.Instance.targetSpeedSetting.Value;
        }

        // Destroy after lifetime ends so they don't pile up out of bounds
        Invoke(nameof(DespawnTarget), lifeTime);
    }

    private void Update()
    {
        if (!IsServer) return;
        
        // Move targets to the right. Adjust Vector3.right based on your arena orientation.
        transform.Translate(Vector3.right * (moveSpeed * Time.deltaTime));
    }

    private void DespawnTarget()
    {
        if (IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}