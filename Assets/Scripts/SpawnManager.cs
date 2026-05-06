using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    public CinemachineCamera tpsCameraPrefab;

    void InitializePlayerCamera(Transform player)
    {
        CinemachineCamera newCam = Instantiate(tpsCameraPrefab);
        newCam.enabled = false;

        newCam.Target.TrackingTarget = player;
        newCam.enabled = true;

        // assign reference to movement
        GetComponent<NetworkPlayerMovement>().playerCamera = newCam.transform;
    }

    public override void OnNetworkSpawn()
    {
        // assign your camewa
        if (IsOwner) InitializePlayerCamera(transform);

        // move player to spawn

        // subscribe to player count callbacks

    }

    void IncreasePlayerCount()
    {
        
    }

    void DecreasePlayerCount()
    {
        
    }
}
