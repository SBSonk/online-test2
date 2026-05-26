using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    public CinemachineCamera tpsCameraPrefab;

    void InitializePlayerCamera(Transform player)
    {
        CinemachineCamera playerCam = GameObject.FindGameObjectWithTag("PlayerCamera").GetComponent<CinemachineCamera>();
        playerCam.enabled = false;

        playerCam.Target.TrackingTarget = player;
        playerCam.enabled = true;

        // assign reference to movement
        GetComponent<NetworkPlayerMovement>().playerCamera = playerCam.transform;
    }

    public override void OnNetworkSpawn()
    {
        // assign your camewa
        if (IsOwner) InitializePlayerCamera(transform);

        // move player to spawn
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        transform.position = spawnPoints[Random.Range(0, spawnPoints.Length)].transform.position;
    }
}
