using Unity.Netcode;
using UnityEngine;

public class PlayerInteract : NetworkBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public LayerMask interactableLayer;
    public KeyCode interactKey = KeyCode.E;
    public float interactBufferTime = 0.15f; 

    [Header("References")]
    public Transform playerCamera;
    public GameObject playerSelf;
    public NetworkHandsAnimator handAnimator; 
    
    // --- NEW: Reference to the shooter script for interlocks ---
    public NetworkBalloonShooter balloonShooter; 

    private Interactable currentInteractable;
    private float _lastInteractPressTime = -100f; 

    // --- NEW: Expose the hover state so the shooter script can read it ---
    public bool IsHovering()
    {
        return currentInteractable != null;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // --- NEW: INTERLOCK ---
        // If the player is currently winding up or holding a balloon, don't let them interact!
        if (balloonShooter != null && (balloonShooter.isWindingUp || balloonShooter.isCharging))
        {
            ClearInteractable(); // Force the hand to drop if you turn toward a button while charging
            return;
        }

        if (Input.GetKeyDown(interactKey))
        {
            _lastInteractPressTime = Time.time;
        }

        CheckForInteractable();

        if (currentInteractable != null)
        {
            if (Time.time - _lastInteractPressTime <= interactBufferTime)
            {
                currentInteractable.Interact(playerSelf); 
                
                if (handAnimator != null) handAnimator.TriggerPress();
                
                _lastInteractPressTime = -100f; 
            }
        }
    }

    private void CheckForInteractable()
    {
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
                    
                    if (handAnimator != null) handAnimator.SetHovering(true); 
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
            
            if (handAnimator != null) handAnimator.SetHovering(false); 
        }
    }
}