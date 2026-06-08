using Unity.Netcode;
using UnityEngine;

public class PlayerInteract : NetworkBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public LayerMask interactableLayer;
    public KeyCode interactKey = KeyCode.E;
    
    [Tooltip("How long (in seconds) the game remembers your interact key press. Helps catch high-speed drive-by interactions.")]
    public float interactBufferTime = 0.15f; 

    [Header("References")]
    public Transform playerCamera;
    public GameObject playerSelf;

    private Interactable currentInteractable;
    private float _lastInteractPressTime = -100f; 

    private void Update()
    {
        // Prevent local inputs from controlling other players' avatars
        if (!IsOwner) return;

        // 1. Log the input immediately, regardless of what you are looking at
        if (Input.GetKeyDown(interactKey))
        {
            _lastInteractPressTime = Time.time;
        }

        // 2. Perform the Raycast to find what we are looking at
        CheckForInteractable();

        // 3. Buffered Interaction Trigger
        if (currentInteractable != null)
        {
            // If the key was pressed recently enough (within the buffer window)
            if (Time.time - _lastInteractPressTime <= interactBufferTime)
            {
                currentInteractable.Interact(playerSelf); 
                
                // Consume the buffer so it doesn't double-trigger on the next frame
                _lastInteractPressTime = -100f; 
            }
        }
    }

    private void CheckForInteractable()
    {
        // Don't raycast if the camera isn't assigned
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            Interactable hitObject = hit.collider.GetComponent<Interactable>();
            if (!hitObject) hitObject = hit.collider.GetComponentInParent<Interactable>();

            if (hitObject != null)
            {
                if (hitObject != currentInteractable)
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.EndHover();
                    }

                    currentInteractable = hitObject;
                    currentInteractable.BeginHover();
                }
                else
                {
                    currentInteractable.UpdateHover();
                }
            }
            else
            {
                ClearInteractable();
            }
        }
        else
        {
            ClearInteractable();
        }
    }

    private void ClearInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable.EndHover();
            currentInteractable = null;
        }
    }
}