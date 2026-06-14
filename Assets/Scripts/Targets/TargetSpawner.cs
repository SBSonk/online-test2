using Unity.Netcode;
using UnityEngine;

public class TargetSpawner : NetworkBehaviour
{
    private enum TargetClass { Normal, Golden, Bomb }

    [Header("Spawn Origins (Place in CENTER of target zone)")]
    [Tooltip("Place on the LEFT wall.")]
    public Transform leftSpawnRoot;
    [Tooltip("Place on the RIGHT wall.")]
    public Transform rightSpawnRoot;
    
    [Header("Spawn Grid Configuration")]
    public int targetRows = 3; 
    public int targetDepths = 3; 
    public float rowSpacing = 1.5f; 
    public float depthSpacing = 2f; 

    [Header("Target Prefabs")]
    public GameObject normalTargetPrefab;
    public GameObject goldenTargetPrefab;
    public GameObject bombTargetPrefab;

    [Header("Settings")]
    public float baseGoldenChance = 0.1f;
    public float baseBombChance = 0.2f;

    [Header("Target Mesh Orientation")]
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

        // 1. Pick a side and identify the OPPOSITE side
        bool isLeft = Random.value > 0.5f;
        Transform chosenRoot = isLeft ? leftSpawnRoot : rightSpawnRoot;
        Transform oppositeRoot = isLeft ? rightSpawnRoot : leftSpawnRoot;

        int randomRow = Random.Range(0, targetRows);
        int randomDepth = Random.Range(0, targetDepths);

        float rowOffset = (randomRow - (targetRows - 1) / 2f) * rowSpacing;
        float depthOffset = (randomDepth - (targetDepths - 1) / 2f) * depthSpacing;

        Vector3 spawnPos = chosenRoot.position 
                         + (chosenRoot.up * rowOffset) 
                         + (chosenRoot.forward * depthOffset);

        Quaternion rot = Quaternion.Euler(globalRotation);

        GameObject prefab = GetPrefabFromClass();
        GameObject obj = Instantiate(prefab, spawnPos, rot);

        // 2. Spawn it over the network FIRST
        obj.GetComponent<NetworkObject>().Spawn(true);

        // 3. Assign the variables
        if (obj.TryGetComponent(out TargetMovement movement) && obj.TryGetComponent(out CarnivalTarget targetObj))
        {
            // Set the Size
            CarnivalTarget.TargetSize rolledSize = RollTargetSize(prefab);
            targetObj.targetSize.Value = rolledSize;

            // Set the Speed based on the size
            float baseSpeed = NetworkMatchManager.Instance.targetSpeedSetting.Value;
            movement.assignedSpeed.Value = baseSpeed * GetSpeedModifier(rolledSize);

            // --- THE FIX: FOOLPROOF DIRECTION MATH ---
            // This draws a line straight from the Spawning Root to the Opposite Root!
            movement.travelDirection.Value = (oppositeRoot.position - chosenRoot.position).normalized;
            movement.maxTravelDistance.Value = Vector3.Distance(leftSpawnRoot.position, rightSpawnRoot.position) + 2f;
        }
    }

    private GameObject GetPrefabFromClass()
    {
        // Check if powerups are OFF
        if (NetworkMatchManager.Instance.powerupModeSetting.Value == NetworkMatchManager.PowerupMode.Off) 
            return normalTargetPrefab;

        float roll = Random.value;
        if (roll <= baseGoldenChance) return goldenTargetPrefab;
        if (roll <= baseGoldenChance + baseBombChance) return bombTargetPrefab;
        return normalTargetPrefab;
    }

    // Assigns appropriate sizes based on target type
    private CarnivalTarget.TargetSize RollTargetSize(GameObject prefab)
    {
        if (prefab == goldenTargetPrefab) 
            return Random.value > 0.5f ? CarnivalTarget.TargetSize.Small : CarnivalTarget.TargetSize.Regular;
        if (prefab == bombTargetPrefab)
            return (CarnivalTarget.TargetSize)Random.Range(1, 4); 
        
        return (CarnivalTarget.TargetSize)Random.Range(0, 4); 
    }

    // Smaller targets move faster!
    private float GetSpeedModifier(CarnivalTarget.TargetSize size)
    {
        return size switch
        {
            CarnivalTarget.TargetSize.Small => 1.5f, 
            CarnivalTarget.TargetSize.Regular => 1.0f,
            CarnivalTarget.TargetSize.Large => 0.8f,  
            CarnivalTarget.TargetSize.XL => 0.6f,     
            _ => 1.0f
        };
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
                float rowOffset = (r - (targetRows - 1) / 2f) * rowSpacing;
                float depthOffset = (d - (targetDepths - 1) / 2f) * depthSpacing;

                Vector3 worldPos = root.position + (root.up * rowOffset) + (root.forward * depthOffset);
                Gizmos.DrawSphere(worldPos, 0.2f);
            }
        }
    }
}