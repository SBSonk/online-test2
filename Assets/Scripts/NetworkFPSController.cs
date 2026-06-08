using Unity.Netcode;
using UnityEngine;

public class NetworkFPSController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("An Empty GameObject placed at head-height inside the player.")]
    public Transform cameraRoot; 
    
    [Header("Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 85f; 

    // Local tracking
    private float currentPitch = 0f;
    private float currentYaw = 0f;

    // NEW: We now sync BOTH directions ourselves using Owner-writable variables!
    public NetworkVariable<float> syncedPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> syncedYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Grab our starting rotation so we don't snap to 0 on spawn
            currentYaw = transform.eulerAngles.y;
            if (cameraRoot != null) currentPitch = cameraRoot.localEulerAngles.x;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            HandleAiming();
        }
        else
        {
            // --- OPPONENT SCREEN ---
            // If this is another player, apply THEIR synced rotations to their body and arms
            transform.rotation = Quaternion.Euler(0f, syncedYaw.Value, 0f);
            
            if (cameraRoot != null)
            {
                cameraRoot.localEulerAngles = new Vector3(syncedPitch.Value, 0f, 0f);
            }
        }
    }

    private void HandleAiming()
    {
        // --- LOCAL SCREEN ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, -maxLookAngle, maxLookAngle);
        
        currentYaw += mouseX;

        // Apply immediately to our own camera and body for that smooth Cinemachine feel!
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        if (cameraRoot != null)
        {
            cameraRoot.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
        }

        // Quietly update the network so everyone else sees us move
        syncedPitch.Value = currentPitch;
        syncedYaw.Value = currentYaw;
    }
}