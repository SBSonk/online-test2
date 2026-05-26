using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    void InitializePlayerCamera(Transform player)
    {
        CinemachineCamera playerCam = GameObject.FindGameObjectWithTag("PlayerCamera").GetComponent<CinemachineCamera>();
        playerCam.enabled = false;

        playerCam.Target.TrackingTarget = player;
        playerCam.enabled = true;

        // assign reference to movement
        GetComponent<NetworkPlayerMovement>().playerCamera = playerCam.transform;
        GetComponent<NetworkPlayerShooting>().cameraTransform = playerCam.transform;
    }

    void InitializeHUD()
    {
        FindAnyObjectByType<HUDManager>().Initialize(GetComponent<NetworkHasHealth>(), GetComponent<NetworkPlayerShooting>());
    }

    public override void OnNetworkSpawn()
    {
        // assign your camewa
        if (IsOwner)
        {
            InitializeHUD();
            InitializePlayerCamera(transform);
        }

        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        // move player to spawn
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        transform.position = spawnPoints[Random.Range(0, spawnPoints.Length)].transform.position;

        // Initialize health
        GetComponent<NetworkHasHealth>().InitializeSpawn();
    }
}
