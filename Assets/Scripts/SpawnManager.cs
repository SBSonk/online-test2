using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    [Header("Camera References (Assign in Prefab)")]
    [Tooltip("Drag the CinemachineCamera component from THIS prefab here")]
    public CinemachineCamera localVirtualCam;

    void InitializePlayerCamera(Transform player)
    {
        if (localVirtualCam == null) 
        {
            Debug.LogError("Virtual Camera reference is missing on the SpawnManager! Drag it in from the prefab.");
            return;
        }

        // 1. Force the local virtual camera to look at and follow this player body
        localVirtualCam.Target.TrackingTarget = player;

        // 2. Turn ON the virtual camera component only for the local owner
        localVirtualCam.enabled = true;

        // 3. Optional: Boost priority to ensure the local brain prioritizes this camera
        localVirtualCam.Priority = 100;

        // Assign camera transform reference to movement and shooter systems
        if (TryGetComponent(out NetworkPlayerMovement movement))
        {
            movement.playerCamera = localVirtualCam.transform;
        }
        
        if (TryGetComponent(out NetworkBalloonShooter shooter))
        {
            shooter.cameraTransform = localVirtualCam.transform;
        }
    }

    void InitializeHUD()
    {
        HUDManager hud = FindAnyObjectByType<HUDManager>();
        if (hud != null)
        {
            hud.Initialize(GetComponent<NetworkBalloonShooter>());
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            InitializeHUD();
            InitializePlayerCamera(transform);
        }
        else
        {
            // CRITICAL: If we don't own this player clone, disable its virtual camera component!
            // This prevents remote players' cameras from talking to our local Cinemachine Brain.
            if (localVirtualCam != null)
            {
                localVirtualCam.enabled = false;
                localVirtualCam.Priority = 0; // Drop priority to zero as an extra safety measure
            }
        }

        if (IsServer)
        {
            SpawnPlayer();
        }
    }

    public void SpawnPlayer()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPoints.Length > 0)
        {
            Vector3 targetPos = spawnPoints[Random.Range(0, spawnPoints.Length)].transform.position;
            targetPos += Vector3.up * 1.5f; 

            if (TryGetComponent(out CharacterController cc))
            {
                cc.enabled = false;
                transform.position = targetPos;
                cc.enabled = true;
            }
            else
            {
                transform.position = targetPos;
            }
        }

        if (TryGetComponent(out NetworkHasHealth health))
        {
            health.InitializeSpawn();
        }
    }
}