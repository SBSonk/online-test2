using Unity.Netcode;
using UnityEngine;

public class PvPSpawner : NetworkBehaviour
{
    [Header("Spawn Volume")]
    public BoxCollider spawnVolume; // A box covering your PvP arena

    [Header("Prefabs")]
    public GameObject powerupPrefab; // e.g., Magnet, Rapid Fire, Cluster
    public GameObject hazardPrefab;  // e.g., Lead Balloon, Butter Fingers
    public GameObject normalTargetPrefab;

    [Header("Spawn Logic")]
    public float spawnInterval = 2f;
    [Range(0, 1)] public float powerupChance = 0.5f;
    [Range(0, 1)] public float hazardChance = 0.3f;

    private float spawnTimer;

    private void Update()
    {
        if (!IsServer) return;
        if (NetworkMatchManager.Instance.currentState.Value != NetworkMatchManager.MatchState.Active) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0)
        {
            spawnTimer = spawnInterval;
            SpawnLure();
        }
    }

    private void SpawnLure()
    {
        // 1. Pick a random point inside the volume
        Vector3 randomPos = new Vector3(
            Random.Range(spawnVolume.bounds.min.x, spawnVolume.bounds.max.x),
            spawnVolume.bounds.min.y, // Keep them at a specific height or range
            Random.Range(spawnVolume.bounds.min.z, spawnVolume.bounds.max.z)
        );

        // 2. Pick a prefab based on PvP rarity
        GameObject prefab = SelectPrefab();

        // 3. Spawn
        GameObject obj = Instantiate(prefab, randomPos, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn(true);

        // --- THE FIX: Turn on the drift ---
        if (obj.TryGetComponent(out TargetMovement movement))
        {
            movement.isDrifting.Value = true; // <--- TURN ON THE DRIFT
            
            movement.assignedSpeed.Value = 1.0f;
            movement.travelDirection.Value = new Vector3(Random.Range(-0.2f, 0.2f), 0, 1f).normalized;
        }

        obj.GetComponent<NetworkObject>().Spawn(true);
    }

    private GameObject SelectPrefab()
    {
        float roll = Random.value;
        if (roll < powerupChance) return powerupPrefab;
        if (roll < powerupChance + hazardChance) return hazardPrefab;
        return normalTargetPrefab;
    }
}