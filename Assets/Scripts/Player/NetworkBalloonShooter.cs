using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using FirstGearGames.SmoothCameraShaker;
using System.Collections;
using NaughtyAttributes;
using System.Collections.Generic;

public class NetworkBalloonShooter : NetworkBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform throwPoint; 
    public GameObject balloonPrefab;
    public PlayerColorManager colorManager; 
    public NetworkHandsAnimator handAnimator; 
    public PlayerInteract playerInteract; 
    public NetworkPlayerMovement playerMovement; 

    [Header("Camera Effects")]
    public ShakeData throwShakeProfile; 

    [HideInInspector] public int currentShadeIndex = 0; 

    [Header("Throw Mechanics")]
    public float minThrowForce = 10f;
    public float maxThrowForce = 40f;
    public float maxChargeTime = 1.5f;
    public float upwardThrowBias = 2f; 
    public float throwCooldown = 0.65f; 
    public float windupTime = 0.25f; 
    
    [Tooltip("How long (in seconds) the game remembers an early click before the cooldown finishes.")]
    public float inputBufferWindow = 0.25f; 
    
    [Tooltip("Delay between releasing the button and the balloon actually spawning to sync with the animation.")]
    public float throwAnimationDelay = 0.15f; 

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public float maxTrajectoryDistance = 5f; 
    public LayerMask collisionLayer;
    public int trajectoryResolution = 30;
    public float trajectoryTimeStep = 0.1f;

    [Header("Active Powerups")]
    public float doublePointsTimer = 0f; // Ensure this is here for your Golden Target!
    public float rapidFireTimer = 0f;
    public float magnetTimer = 0f;
    public int clusterShotsRemaining = 0;

    [Header("Active Hazards")]
    public float butterFingersTimer = 0f;
    public float butterFingersWaveSpeed = 4f; 
    public int leadBalloonsRemaining = 0;

    [Header("Multiplayer Visual Sync")]
    public NetworkVariable<bool> isPreparingThrowNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> syncedShadeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("State (Read Only)")]
    public bool isWindingUp = false; 
    public bool isCharging = false;
    public float currentChargeTime = 0f;
    private float windupStartTime = 0f; 
    private float lastThrowTime = -100f; 
    private float lastClickTime = -100f; // --- NEW: Tracks when the user pressed the button ---

    [Header("Events")]
    public UnityEvent OnStartCharge;
    public UnityEvent OnThrow;

    void OnApplicationFocus(bool focus)
    {
        if (!focus) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (IsOwner) PickNextShade();
    }

    public void PickNextShade()
    {
        if (IsOwner)
        {
            syncedShadeIndex.Value = Random.Range(0, 1000); 
        }
    }

    void Update()
    {
        if (!IsOwner || !cameraTransform || !throwPoint) return;

        bool prep = isWindingUp || isCharging;
        if (isPreparingThrowNet.Value != prep) 
        {
            isPreparingThrowNet.Value = prep;
        }

        // 1. Tick down timers
        if (doublePointsTimer > 0) doublePointsTimer -= Time.deltaTime;
        if (rapidFireTimer > 0) rapidFireTimer -= Time.deltaTime;
        if (magnetTimer > 0) magnetTimer -= Time.deltaTime;
        if (butterFingersTimer > 0) butterFingersTimer -= Time.deltaTime; 

        // 2. Update the UI HUD 
        if (PowerupHUD.Instance != null)
        {
            PowerupHUD.Instance.UpdatePowerupDisplay("Rapid Fire", rapidFireTimer, 0, true);
            PowerupHUD.Instance.UpdatePowerupDisplay("Magnet", magnetTimer, 0, true);
            PowerupHUD.Instance.UpdatePowerupDisplay("Cluster", 0, clusterShotsRemaining, false);
            PowerupHUD.Instance.UpdatePowerupDisplay("Lead Balloon", 0, leadBalloonsRemaining, false);
            PowerupHUD.Instance.UpdatePowerupDisplay("Butter Fingers", butterFingersTimer, 0, true);
        }

        // 3. Interlocks
        if ((playerInteract != null && playerInteract.IsHovering()) || 
            (playerMovement != null && playerMovement.isSprinting))
        {
            if (isWindingUp || isCharging) CancelCharge();
            return;
        }

        // 4. Standard throw logic
        HandleChargeInput();
    }

    private void HandleChargeInput()
    {
        float currentCooldown = (rapidFireTimer > 0) ? throwCooldown * 0.25f : throwCooldown;
        float currentWindup = (rapidFireTimer > 0) ? windupTime * 0.25f : windupTime;
        float chargeSpeedMultiplier = (rapidFireTimer > 0) ? 3f : 1f;

        // --- NEW: Register the exact moment the button was pressed ---
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            lastClickTime = Time.time;
        }

        // --- NEW: Check if the click happened within our buffer window ---
        bool hasBufferedInput = (Time.time - lastClickTime) <= inputBufferWindow;

        // --- NEW: Start windup if we have a buffered click, are STILL holding the button, and cooldown is done ---
        if (hasBufferedInput && Input.GetKey(KeyCode.Mouse0) && !isWindingUp && !isCharging && Time.time >= lastThrowTime + currentCooldown)
        {
            isWindingUp = true;
            windupStartTime = Time.time;
            lastClickTime = -100f; // Consume the buffer so it doesn't double-trigger
            
            if (handAnimator != null) handAnimator.SetThrowWindup(true); 
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (isWindingUp)
            {
                if (Time.time >= windupStartTime + currentWindup)
                {
                    isWindingUp = false;
                    isCharging = true;
                    currentChargeTime = 0f;
                    
                    if (trajectoryLine != null) trajectoryLine.enabled = true;
                    OnStartCharge?.Invoke();
                }
            }
            else if (isCharging)
            {
                if (butterFingersTimer > 0)
                {
                    currentChargeTime = Mathf.PingPong(Time.time * butterFingersWaveSpeed, maxChargeTime);
                }
                else
                {
                    currentChargeTime += (Time.deltaTime * chargeSpeedMultiplier);
                    currentChargeTime = Mathf.Clamp(currentChargeTime, 0, maxChargeTime);
                }
                DrawTrajectory();
            }
        }

        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            if (isWindingUp) CancelCharge();
            else if (isCharging) ExecuteThrow();
        }

        if (!Input.GetKey(KeyCode.Mouse0) && (isWindingUp || isCharging))
        {
            CancelCharge();
        }
    }

    private void CancelCharge()
    {
        isWindingUp = false;
        isCharging = false;
        currentChargeTime = 0f;
        
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (handAnimator != null) handAnimator.SetThrowWindup(false);
    }

    private void ExecuteThrow()
    {
        isCharging = false;
        lastThrowTime = Time.time; 
        
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        
        if (handAnimator != null) 
        {
            handAnimator.SetThrowWindup(false);
            handAnimator.TriggerThrowRelease();
        }
        OnThrow?.Invoke(); 

        float chargePercent = currentChargeTime / maxChargeTime;
        
        float actualMaxForce = (leadBalloonsRemaining > 0) ? minThrowForce * 0.5f : maxThrowForce;
        float finalForce = Mathf.Lerp(minThrowForce, actualMaxForce, chargePercent);

        if (throwShakeProfile != null) CameraShakerHandler.Shake(throwShakeProfile);

        Vector3 throwDirection = cameraTransform.forward;
        throwDirection.y += (upwardThrowBias / finalForce); 
        throwDirection.Normalize();

        GetCurrentBalloonColors(out Color bColor, out Color pColor);

        bool useCluster = clusterShotsRemaining > 0;

        StartCoroutine(ThrowWithDelayRoutine(throwPoint.position, throwDirection, finalForce, bColor, pColor, magnetTimer > 0, useCluster));
        
        if (useCluster) clusterShotsRemaining--;
        if (leadBalloonsRemaining > 0) leadBalloonsRemaining--; 
        
        PickNextShade(); 
        currentChargeTime = 0f;
    }

    private void GetCurrentBalloonColors(out Color bColor, out Color pColor)
    {
        bColor = Color.white;
        pColor = Color.white;

        if (colorManager != null && colorManager.colorSets.Length > 0)
        {
            int pIndex = colorManager.colorIndex.Value;
            var sets = colorManager.colorSets;
            var shades = sets[pIndex % sets.Length].balloonShades;
            
            if (shades.Length > 0)
            {
                var specificShade = shades[currentShadeIndex % shades.Length];
                bColor = specificShade.balloonColor;
                pColor = specificShade.particleColor;
            }
        }
    }

    private IEnumerator ThrowWithDelayRoutine(Vector3 spawnPosition, Vector3 direction, float force, Color bColor, Color pColor, bool isMagnetic, bool isCluster)
    {
        yield return new WaitForSeconds(throwAnimationDelay);
        ThrowServerRpc(spawnPosition, direction, force, bColor, pColor, isMagnetic, isCluster);
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        float chargePercent = currentChargeTime / maxChargeTime;
        
        float actualMaxForce = (leadBalloonsRemaining > 0) ? minThrowForce * 0.5f : maxThrowForce;
        float finalForce = Mathf.Lerp(minThrowForce, actualMaxForce, chargePercent);

        Vector3 throwDirection = cameraTransform.forward;
        throwDirection.y += (upwardThrowBias / finalForce); 
        throwDirection.Normalize();

        Vector3 initialVelocity = throwDirection * finalForce; 
        
        trajectoryLine.positionCount = trajectoryResolution;
        Vector3 currentPosition = throwPoint.position;
        trajectoryLine.SetPosition(0, currentPosition);

        float accumulatedDistance = 0f;

        for (int i = 1; i < trajectoryResolution; i++)
        {
            float timeOffset = i * trajectoryTimeStep;
            Vector3 nextPosition = throwPoint.position + (initialVelocity * timeOffset) + (0.5f * Physics.gravity * timeOffset * timeOffset);
            float distanceToNext = Vector3.Distance(currentPosition, nextPosition);

            if (accumulatedDistance + distanceToNext > maxTrajectoryDistance)
            {
                float remainingDistance = maxTrajectoryDistance - accumulatedDistance;
                Vector3 finalPoint = currentPosition + (nextPosition - currentPosition).normalized * remainingDistance;

                if (Physics.Raycast(currentPosition, finalPoint - currentPosition, out RaycastHit finalHit, remainingDistance, collisionLayer))
                {
                    trajectoryLine.positionCount = i + 1;
                    trajectoryLine.SetPosition(i, finalHit.point);
                }
                else
                {
                    trajectoryLine.positionCount = i + 1;
                    trajectoryLine.SetPosition(i, finalPoint);
                }
                break;
            }

            if (Physics.Raycast(currentPosition, nextPosition - currentPosition, out RaycastHit hit, distanceToNext, collisionLayer))
            {
                trajectoryLine.positionCount = i + 1;
                trajectoryLine.SetPosition(i, hit.point);
                break;
            }

            trajectoryLine.SetPosition(i, nextPosition);
            accumulatedDistance += distanceToNext;
            currentPosition = nextPosition;
        }
    }

    public float GetChargePercentage()
    {
        if (!isCharging) return 0f;
        return currentChargeTime / maxChargeTime;
    }

    [ServerRpc]
    public void ThrowServerRpc(Vector3 spawnPosition, Vector3 direction, float force, Color bColor, Color pColor, bool isMagnetic, bool isCluster, ServerRpcParams rpcParams = default)
    {
        int balloonsToSpawn = isCluster ? 3 : 1;
        
        List<Collider> spawnedColliders = new List<Collider>();

        for (int i = 0; i < balloonsToSpawn; i++)
        {
            Vector3 finalDirection = direction;
            if (isCluster && i > 0)
            {
                finalDirection = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-15f, 15f), 0) * direction;
            }

            GameObject balloonInstance = Instantiate(balloonPrefab, spawnPosition, Quaternion.LookRotation(finalDirection));
            
            Collider[] newCols = balloonInstance.GetComponentsInChildren<Collider>();
            
            foreach (Collider newCol in newCols)
            {
                foreach (Collider existing in spawnedColliders)
                {
                    Physics.IgnoreCollision(newCol, existing);
                }
                spawnedColliders.Add(newCol);
            }

            NetworkObject networkObj = balloonInstance.GetComponent<NetworkObject>();
            if (balloonInstance.TryGetComponent(out BalloonProjectile proj))
            {
                proj.syncedBalloonColor.Value = bColor;
                proj.syncedParticleColor.Value = pColor;
                proj.isMagnetic.Value = isMagnetic; 
            }

            if (networkObj != null) networkObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

            Rigidbody rb = balloonInstance.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(finalDirection * force, ForceMode.Impulse);
        }
    }

    [ClientRpc]
    public void ApplyLeadBalloonClientRpc(int amount, ClientRpcParams rpcParams = default)
    {
        leadBalloonsRemaining += amount;
    }

    [ClientRpc]
    public void ApplyButterFingersClientRpc(float duration, ClientRpcParams rpcParams = default)
    {
        butterFingersTimer += duration;
    }
}