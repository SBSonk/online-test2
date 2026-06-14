using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class FootstepManager : MonoBehaviour
{
    public float rayDistance = 2f; 
    public LayerMask groundLayer;

    public float walkTime = 0.5f;

    public AudioSource leftFootSrc, rightFootSrc;
    
    // --- ADDED: Event to broadcast when a step actually happens ---
    public event Action OnStep;

    #region Clips

    [Header("Clips"), SerializeField]
    FloorSounds[] floorTypes;

    #endregion
    
    #region State

    FloorType currentFloor = FloorType.Tile;
    private bool stepRight = false;
    private bool playFootsteps = false;

    #endregion

    private void Start()
    {
        StartCoroutine(FootstepLoop());
    }

    void Update()
    {
        CheckForFloorTypes();
    }

    void CheckForFloorTypes()
    {
        Vector3 rayOffset = new Vector3(0, 0.25f); 
        if (Physics.Raycast(transform.position + rayOffset, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
        {
            if (hit.transform.TryGetComponent(out SetFloorType floor))
            {
                SetFloor(floor.floorToSet);
            }
            else
            {
                SetFloor(FloorType.Grass);
            }
        }
    }

    void SetFloor(FloorType f) => currentFloor = f;

    FloorSounds GetFloorSounds(FloorType floorType)
    {
        foreach (FloorSounds f in floorTypes)
        {
            if (f.type == floorType) 
            {
                return f;
            }
        }
        return null;
    } 

    IEnumerator FootstepLoop()
    {
        while (true)
        {
            if (playFootsteps)
            {                
                AudioSource audioSource = stepRight ? rightFootSrc : leftFootSrc;
                FloorSounds currentSounds = GetFloorSounds(currentFloor);

                if (currentSounds != null)
                {
                    AudioClip clip = currentSounds.GetRandomStepClip();
                    if (clip != null)
                    {
                        audioSource.PlayOneShot(clip);
                        
                        // --- ADDED: Fire the event perfectly synced with the audio ---
                        OnStep?.Invoke();
                    }
                }
            }
            
            yield return new WaitForSeconds(walkTime);

            // Switch Feet
            stepRight = !stepRight;
        }
    }

    public void SetFootsteps(bool setEnabled) 
    {
        playFootsteps = setEnabled;
    }
}

[Serializable]
public enum FloorType
{
    Grass, Gravel, Metal, Rock, Tile, Water, Wood
}

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