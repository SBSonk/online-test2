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

    [Header("Base Target Prefabs")]
    public GameObject normalTargetPrefab;
    public GameObject goldenTargetPrefab;
    public GameObject bombTargetPrefab;

    [Header("Powerup Prefabs")]
    [Tooltip("Standard buffs like Rapid Fire, Cluster, etc.")]
    public GameObject[] normalPowerups;
    [Tooltip("Wacky hazards like Lead Balloon, Butter Fingers, etc. Only spawns in Chaos Mode!")]
    public GameObject[] chaosPowerups; 

    [Header("Spawn Settings (Chances)")]
    public Vector3 targetRotationOffset = new Vector3(0f, 90f, 0f);
    public float baseGoldenChance = 0.1f;
    public float baseBombChance = 0.2f;
    public float basePowerupChance = 0.15f; // --- NEW: Chance for a powerup to spawn! ---

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

        // 2. ASSIGN VARIABLES FIRST (Before spawning over the network)
        if (obj.TryGetComponent(out TargetMovement movement) && obj.TryGetComponent(out CarnivalTarget targetObj))
        {
            // Set the Size
            CarnivalTarget.TargetSize rolledSize = RollTargetSize(prefab);
            targetObj.targetSize.Value = rolledSize;

            // Set the Speed based on the size
            float baseSpeed = NetworkMatchManager.Instance.targetSpeedSetting.Value;
            movement.assignedSpeed.Value = baseSpeed * GetSpeedModifier(rolledSize);

            // Set the travel direction and distance
            movement.travelDirection.Value = (oppositeRoot.position - chosenRoot.position).normalized;
            movement.maxTravelDistance.Value = Vector3.Distance(leftSpawnRoot.position, rightSpawnRoot.position) + 2f;
        }

        // 3. NOW SPAWN IT (This bundles all the values above into the initial network payload)
        obj.GetComponent<NetworkObject>().Spawn(true);
    }
    private GameObject GetPrefabFromClass()
    {
        var pMode = NetworkMatchManager.Instance.powerupModeSetting.Value;

        // If powerups are OFF, override the whole pool and just return normal targets
        if (pMode == NetworkMatchManager.PowerupMode.Off) 
            return normalTargetPrefab;

        float roll = Random.value;
        
        if (roll <= baseGoldenChance) return goldenTargetPrefab;
        if (roll <= baseGoldenChance + baseBombChance) return bombTargetPrefab;
        
        // --- NEW: Powerup Pool Logic ---
        if (roll <= baseGoldenChance + baseBombChance + basePowerupChance)
        {
            bool hasNormal = normalPowerups != null && normalPowerups.Length > 0;
            bool hasChaos = chaosPowerups != null && chaosPowerups.Length > 0;
            bool isChaosMode = (pMode == NetworkMatchManager.PowerupMode.Chaos);

            // If we are in Chaos Mode and have hazards, do a 50/50 split between Buffs and Hazards
            if (isChaosMode && hasChaos && hasNormal)
            {
                if (Random.value > 0.5f) 
                    return chaosPowerups[Random.Range(0, chaosPowerups.Length)];
                else 
                    return normalPowerups[Random.Range(0, normalPowerups.Length)];
            }
            // Otherwise, just roll from whatever lists are available
            else if (isChaosMode && hasChaos) return chaosPowerups[Random.Range(0, chaosPowerups.Length)];
            else if (hasNormal) return normalPowerups[Random.Range(0, normalPowerups.Length)];
        }

        return normalTargetPrefab;
    }

    // Assigns appropriate sizes based on target type
    private CarnivalTarget.TargetSize RollTargetSize(GameObject prefab)
    {
        if (prefab == goldenTargetPrefab) 
            return Random.value > 0.5f ? CarnivalTarget.TargetSize.Small : CarnivalTarget.TargetSize.Regular;
        
        if (prefab == bombTargetPrefab)
            return (CarnivalTarget.TargetSize)Random.Range(1, 4); 

        // --- NEW: Keep powerups at a consistent, readable size so players know what they are shooting at! ---
        if (IsPowerup(prefab)) 
            return CarnivalTarget.TargetSize.Regular; 
        
        return (CarnivalTarget.TargetSize)Random.Range(0, 4); 
    }

    // A quick helper to check if a prefab is inside either of our arrays
    private bool IsPowerup(GameObject prefab)
    {
        if (normalPowerups != null)
            foreach (var p in normalPowerups) if (p == prefab) return true;
            
        if (chaosPowerups != null)
            foreach (var p in chaosPowerups) if (p == prefab) return true;
            
        return false;
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