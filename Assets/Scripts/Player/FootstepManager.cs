using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode; 
using Random = UnityEngine.Random;

public class FootstepManager : NetworkBehaviour 
{
    public float rayDistance = 2f; 
    public LayerMask groundLayer;

    [Header("Timings")]
    public float walkTime = 0.5f;
    public float sprintTime = 0.3f; 
    private float currentStepInterval;

    [Header("Audio Sources")]
    public AudioSource leftFootSrc, rightFootSrc;
    
    public event Action OnStep;

    // Network Variables - Owner writes, everyone reads. Updates instantly for the owner!
    public NetworkVariable<bool> isMovingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isSprintingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Clips")]
    [SerializeField] private FloorSounds[] floorTypes;
    
    private FloorType currentFloor = FloorType.Tile;
    private bool stepRight = false;
    private Coroutine footstepCoroutine;

    public override void OnNetworkSpawn()
    {
        isSprintingNet.OnValueChanged += HandleSprintStateChanged;
        
        // ====================================================================
        // THE LOCAL AUDIO FIX:
        // Forces local player footsteps to be 2D (0.0) so they don't get 
        // phased out by the camera. Remote players stay 3D (1.0) for panning.
        // ====================================================================
        if (leftFootSrc != null) leftFootSrc.spatialBlend = IsOwner ? 0f : 1f;
        if (rightFootSrc != null) rightFootSrc.spatialBlend = IsOwner ? 0f : 1f;

        UpdateLocalInterval();
        footstepCoroutine = StartCoroutine(FootstepLoop());
    }

    public override void OnNetworkDespawn()
    {
        isSprintingNet.OnValueChanged -= HandleSprintStateChanged;
        
        if (footstepCoroutine != null)
        {
            StopCoroutine(footstepCoroutine);
        }
    }

    private void HandleSprintStateChanged(bool oldVal, bool newVal)
    {
        UpdateLocalInterval();
    }

    private void UpdateLocalInterval()
    {
        currentStepInterval = isSprintingNet.Value ? sprintTime : walkTime;
    }

    void Update()
    {
        CheckForFloorTypes();
    }

    void CheckForFloorTypes()
    {
        Vector3 rayOffset = new Vector3(0, 0.25f, 0); 
        
        // CRITICAL CHECK: Make sure your 'groundLayer' dropdown in the Inspector 
        // does NOT include your Player's layer, otherwise you'll raycast hit yourself!
        if (Physics.Raycast(transform.position + rayOffset, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
        {
            SetFloor(hit.transform.TryGetComponent(out SetFloorType floor) ? floor.floorToSet : FloorType.Grass);
        }
    }

    void SetFloor(FloorType f) => currentFloor = f;

    FloorSounds GetFloorSounds(FloorType floorType)
    {
        foreach (FloorSounds f in floorTypes)
        {
            if (f.type == floorType) return f;
        }
        return null;
    } 

    IEnumerator FootstepLoop()
    {
        while (true)
        {
            // Direct, unified check of the state variable
            yield return new WaitUntil(() => isMovingNet.Value);

            AudioSource audioSource = stepRight ? rightFootSrc : leftFootSrc;
            FloorSounds currentSounds = GetFloorSounds(currentFloor);

            if (currentSounds != null && audioSource != null)
            {
                AudioClip clip = currentSounds.GetRandomStepClip();
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                    OnStep?.Invoke();
                }
            }
            
            stepRight = !stepRight;

            yield return new WaitForSeconds(currentStepInterval);
        }
    }

    // ==========================================
    // SYNC METHODS (Called by your movement script)
    // ==========================================

    public void SetFootsteps(bool setEnabled) 
    {
        if (IsOwner) 
        {
            isMovingNet.Value = setEnabled;
        }
    }

    public void SetSprintState(bool sprinting)
    {
        if (IsOwner) 
        {
            isSprintingNet.Value = sprinting;
            UpdateLocalInterval(); 
        }
    }
}

// ==========================================
// DATA STRUCTURES
// ==========================================

[Serializable]
public enum FloorType { Grass, Gravel, Metal, Rock, Tile, Water, Wood }

[Serializable]
public class FloorSounds
{
    public FloorType type;
    [SerializeField] AudioClip[] walkClips;

    public AudioClip GetRandomStepClip()
    {
        if (walkClips == null || walkClips.Length == 0) return null;
        return walkClips[Random.Range(0, walkClips.Length)];
    }
}