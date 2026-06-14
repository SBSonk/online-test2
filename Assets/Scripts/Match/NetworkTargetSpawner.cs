using System.Collections;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class NetworkTargetSpawner : NetworkBehaviour
{
    public NetworkHasHealth targetPrefab; // Renamed from enemy to target
    public NetworkVariable<int> maxTargetCount = new NetworkVariable<int>(4);
    public Vector2 timeBetweenSpawns = new Vector2(1, 4);

    [Header("Scoring")]
    public int pointsPerKill = 10;

    [Header("UI References")]
    public TextMeshProUGUI targetCountText; 

    public NetworkVariable<int> targetsAlive = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    ObjectPool<NetworkHasHealth> targetPool;
    Transform[] spawnPoints;
    private Coroutine spawnCoroutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        targetsAlive.OnValueChanged += UpdateTargetCountUI;
        UpdateTargetCountUI(0, targetsAlive.Value); 

        if (!IsServer) return;

        InitializeSpawner();

        // FIXED: Subscribe to the new State Machine
        NetworkMatchManager.Instance.currentState.OnValueChanged += HandleGameStateChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        targetsAlive.OnValueChanged -= UpdateTargetCountUI;
        
        if (IsServer && NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentState.OnValueChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(NetworkMatchManager.MatchState oldState, NetworkMatchManager.MatchState newState)
    {
        if (newState == NetworkMatchManager.MatchState.Active)
        {
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }
        else if (oldState == NetworkMatchManager.MatchState.Active) // Only stop if we were just active
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            ClearAllTargets(); 
        }
    }

    void UpdateTargetCountUI(int previousValue, int newValue)
    {
        if (targetCountText != null)
        {
            targetCountText.text = $"Targets Active: {newValue}";
        }
    }

    void InitializeSpawner()
    {
        GameObject[] spawns = GameObject.FindGameObjectsWithTag("EnemySpawnPoint");
        spawnPoints = spawns.Select(go => go.transform).ToArray();

        targetPool = new ObjectPool<NetworkHasHealth>(SpawnTarget, OnTakeFromPool, OnReleaseToPool, OnDestroyPoolObject);
    }

    IEnumerator SpawnLoop()
    {
        while (NetworkMatchManager.Instance.currentState.Value == NetworkMatchManager.MatchState.Active)
        {
            yield return new WaitForSeconds(Random.Range(timeBetweenSpawns.x, timeBetweenSpawns.y));

            if (targetsAlive.Value >= maxTargetCount.Value) continue;

            Vector3 spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].position;

            NetworkHasHealth target = targetPool.Get();
            target.transform.position = spawnPos;
            target.NetworkObject.Spawn();
        }
    }

    NetworkHasHealth SpawnTarget() 
    {
        NetworkHasHealth target = Instantiate(targetPrefab);
        
        // NEW: When the target dies, give points to whoever killed it, THEN release to pool
        target.onDeath.AddListener(() => 
        {
            HandleTargetDeath(target);
            targetPool.Release(target);
        });
        
        return target;
    }

    private void HandleTargetDeath(NetworkHasHealth target)
    {
        if (!IsServer) return;

        // Find the player who threw the balloon using the ID saved in the health script
        ulong killerId = target.lastAttackerId;
        NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(killerId);

        if (playerObj != null && playerObj.TryGetComponent(out NetworkPlayerScore scoreHandler))
        {
            scoreHandler.AddPoints(pointsPerKill);
        }
    }

    void OnTakeFromPool(NetworkHasHealth target)
    {
        target.InitializeSpawn();
        targetsAlive.Value++;
    }

    void OnReleaseToPool(NetworkHasHealth target)
    {
        target.NetworkObject.Despawn(false);
        targetsAlive.Value--;
    }

    void OnDestroyPoolObject(NetworkHasHealth target)
    {
        target.NetworkObject.Despawn();
    }

    // Helper method to clean up at the end of the game
    void ClearAllTargets()
    {
        NetworkHasHealth[] activeTargets = FindObjectsByType<NetworkHasHealth>(FindObjectsSortMode.None);
        foreach (var target in activeTargets)
        {
            if (target.NetworkObject.IsSpawned)
            {
                targetPool.Release(target);
            }
        }
    }
}