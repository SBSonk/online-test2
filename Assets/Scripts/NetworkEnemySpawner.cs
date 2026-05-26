using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkEnemySpawner : NetworkBehaviour
{
    public NetworkHasHealth enemyPrefab;

    public NetworkVariable<int> maxEnemyCount = new NetworkVariable<int>(4);

    public Vector2 timeBetweenSpawns = new Vector2(1, 4);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(timeBetweenSpawns.x, timeBetweenSpawns.y));
        }
    }
}
