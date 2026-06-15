using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkBalloonShooter))]
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
    private NetworkBalloonShooter shooter;

    public NetworkVariable<float> syncedPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> syncedYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        shooter = GetComponent<NetworkBalloonShooter>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Force cursor lock immediately on spawn to prevent the "stuck" bug
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            currentYaw = transform.eulerAngles.y;
            if (cameraRoot != null) currentPitch = cameraRoot.localEulerAngles.x;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            // Only allow aiming if we are the owner AND the game is not paused
            if (!PauseMenu.IsPaused)
            {
                // Ensure cursor is locked while playing
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                
                HandleAiming();
            }
        }
        else
        {
            // Remote player rotation logic for everyone else
            transform.rotation = Quaternion.Euler(0f, syncedYaw.Value, 0f);
            
            if (cameraRoot != null)
            {
                cameraRoot.localEulerAngles = new Vector3(syncedPitch.Value, 0f, 0f);
            }
        }
    }

    private void HandleAiming()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Apply Brain Scramble Inversion if the timer is active
        if (shooter != null && shooter.brainScrambleTimer > 0)
        {
            mouseX *= -1f;
            mouseY *= -1f;
        }

        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, -maxLookAngle, maxLookAngle);
        
        currentYaw += mouseX;

        // Apply local rotation
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        if (cameraRoot != null)
        {
            cameraRoot.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
        }

        // Sync to other players
        syncedPitch.Value = currentPitch;
        syncedYaw.Value = currentYaw;
    }
}