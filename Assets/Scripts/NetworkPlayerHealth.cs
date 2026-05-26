using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerHealth : NetworkHasHealth
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        onDeath.AddListener(InitializeRespawnRpc);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        onDeath.AddListener(InitializeRespawnRpc);
    }

    [Rpc(SendTo.Server)]
    void InitializeRespawnRpc()
    {
        GetComponent<SpawnManager>().SpawnPlayer();
    }
}
