using Unity.Netcode;
using UnityEngine;

public class TargetSpawner : NetworkBehaviour
{
    private enum TargetClass { Normal, Golden, Bomb }

    [Header("Spawn Origins (Place in CENTER of target zone)")]
    [Tooltip("Place on the LEFT wall. Red arrow (X) must point RIGHT (towards the other side).")]
    public Transform leftSpawnRoot;
    [Tooltip("Place on the RIGHT wall. Red arrow (X) must point LEFT (towards the other side).")]
    public Transform rightSpawnRoot;
    
    [Header("Spawn Grid Configuration")]
    [Tooltip("How many vertical levels of targets?")]
    public int targetRows = 3; 
    [Tooltip("How many lanes deep into the background?")]
    public int targetDepths = 3; 
    
    [Tooltip("World Space distance between each row (Vertical/Y)")]
    public float rowSpacing = 1.5f; 
    [Tooltip("World Space distance between each lane (Depth/Z)")]
    public float depthSpacing = 2f; 

    [Header("Target Prefabs")]
    public GameObject normalTargetPrefab;
    public GameObject goldenTargetPrefab;
    public GameObject bombTargetPrefab;

    [Header("Settings")]
    public Vector3 targetRotationOffset = new Vector3(0f, 90f, 0f);
    public float baseGoldenChance = 0.1f;
    public float baseBombChance = 0.2f;

    [Header("Target Mesh Orientation")]
    [Tooltip("A single global rotation offset applied to all targets upon spawning.")]
    public Vector3 globalRotation = new Vector3(0f, 90f, 0f);

    private float spawnTimer;

    private void Update()
    {
        if (!IsServer) return;
        
        if (NetworkMatchManager.Instance.currentState.Value != NetworkMatchManager.MatchState.Active) 
            return;

        spawnTimer -= Time.deltaTime;
        
        if (spawnTimer <= 0)
        {
            spawnTimer = NetworkMatchManager.Instance.spawnIntervalSetting.Value;
            SpawnTarget();
        }
    }

    private void SpawnTarget()
    {
        if (leftSpawnRoot == null || rightSpawnRoot == null) return;

        bool isLeft = Random.value > 0.5f;
        Transform chosenRoot = isLeft ? leftSpawnRoot : rightSpawnRoot;

        int randomRow = Random.Range(0, targetRows);
        int randomDepth = Random.Range(0, targetDepths);

        float rowOffset = (randomRow - (targetRows - 1) / 2f) * rowSpacing;
        float depthOffset = (randomDepth - (targetDepths - 1) / 2f) * depthSpacing;

        Vector3 spawnPos = chosenRoot.position 
                         + (chosenRoot.up * rowOffset) 
                         + (chosenRoot.forward * depthOffset);

        Vector3 travelDir = chosenRoot.right; 
        
        // --- THE FIX: One unified offset applied to the root's rotation ---
        Quaternion rot = Quaternion.Euler(globalRotation);

        GameObject prefab = GetPrefabFromClass();
        GameObject obj = Instantiate(prefab, spawnPos, rot);
        obj.GetComponent<NetworkObject>().Spawn(true);

        if (obj.TryGetComponent(out TargetMovement movement))
        {
            movement.assignedSpeed.Value = NetworkMatchManager.Instance.targetSpeedSetting.Value;
            movement.travelDirection.Value = travelDir;
            movement.maxTravelDistance.Value = Vector3.Distance(leftSpawnRoot.position, rightSpawnRoot.position) + 2f;
        }
    }

    private GameObject GetPrefabFromClass()
    {
        if (!NetworkMatchManager.Instance.powerupsEnabled.Value)
        {
            return normalTargetPrefab;
        }

        float roll = Random.value;
        if (roll <= baseGoldenChance) return goldenTargetPrefab;
        if (roll <= baseGoldenChance + baseBombChance) return bombTargetPrefab;
        return normalTargetPrefab;
    }
    private void OnDrawGizmos()
    {
        if (leftSpawnRoot == null || rightSpawnRoot == null) return;
        
        Gizmos.color = Color.cyan;
        DrawGizmoGrid(leftSpawnRoot);
        Gizmos.color = Color.magenta;
        DrawGizmoGrid(rightSpawnRoot);
    }

    private void DrawGizmoGrid(Transform root)
    {
        for (int r = 0; r < targetRows; r++)
        {
            for (int d = 0; d < targetDepths; d++)
            {
                // Apply the exact same centering math so the editor preview matches the code perfectly
                float rowOffset = (r - (targetRows - 1) / 2f) * rowSpacing;
                float depthOffset = (d - (targetDepths - 1) / 2f) * depthSpacing;

                Vector3 worldPos = root.position 
                                 + (root.up * rowOffset) 
                                 + (root.forward * depthOffset);
                
                Gizmos.DrawSphere(worldPos, 0.2f);
            }
        }
    }
}