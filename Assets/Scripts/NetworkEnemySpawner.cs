using System.Collections;
using System.Linq;
using TMPro; // Added for UI
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class NetworkEnemySpawner : NetworkBehaviour
{
    public NetworkHasHealth enemyPrefab;
    public NetworkVariable<int> maxEnemyCount = new NetworkVariable<int>(4);
    public Vector2 timeBetweenSpawns = new Vector2(1, 4);

    [Header("UI References")]
    public TextMeshProUGUI enemyCountText; // Assign this in the inspector

    // State
    public NetworkVariable<int> enemiesAlive = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    ObjectPool<NetworkHasHealth> enemyPool;
    Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        enemiesAlive.OnValueChanged += UpdateEnemyCountUI;
        UpdateEnemyCountUI(0, enemiesAlive.Value); 

        if (!IsServer) return;

        InitializeSpawner();
        StartCoroutine(SpawnLoop());
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        enemiesAlive.OnValueChanged -= UpdateEnemyCountUI;
    }

    void UpdateEnemyCountUI(int previousValue, int newValue)
    {
        if (enemyCountText != null)
        {
            enemyCountText.text = $"Enemies Alive: {newValue}";
        }
    }

    void InitializeSpawner()
    {
        GameObject[] spawns = GameObject.FindGameObjectsWithTag("EnemySpawnPoint");
        spawnPoints = spawns.Select(go => go.transform).ToArray();

        // Object Pooling
        enemyPool = new ObjectPool<NetworkHasHealth>(SpawnEnemy, OnTakeFromPool, OnReleaseToPool, OnDestroyPoolObject);
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(timeBetweenSpawns.x, timeBetweenSpawns.y));

            if (enemiesAlive.Value >= maxEnemyCount.Value) continue;

            Vector3 spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].position;

            NetworkHasHealth enemy = enemyPool.Get();
            enemy.transform.position = spawnPos;
            enemy.NetworkObject.Spawn();
        }
    }

    NetworkHasHealth SpawnEnemy() 
    {
        NetworkHasHealth enemy = Instantiate(enemyPrefab);
        enemy.onDeath.AddListener(() => enemyPool.Release(enemy));
        return enemy;
    }

    void OnTakeFromPool(NetworkHasHealth enemy)
    {
        enemy.InitializeSpawn();
        enemiesAlive.Value++;
    }

    void OnReleaseToPool(NetworkHasHealth enemy)
    {
        enemy.NetworkObject.Despawn(false);
        enemiesAlive.Value--;
    }

    void OnDestroyPoolObject(NetworkHasHealth enemy)
    {
        enemy.NetworkObject.Despawn();
    }
}