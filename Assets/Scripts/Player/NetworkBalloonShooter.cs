using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using FirstGearGames.SmoothCameraShaker;
using System.Collections;
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

    [Header("Audio")]
    public AudioSource playerAudioSource;
    public AudioClip positivePowerupSound; 
    public AudioClip negativePowerupSound; 
    public AudioClip throwSound; // --- NEW: The physical throw/whoosh sound ---

    [Header("Throw Mechanics")]
    public float minThrowForce = 10f;
    public float maxThrowForce = 40f;
    public float maxChargeTime = 1.5f;
    public float upwardThrowBias = 2f; 
    public float throwCooldown = 0.65f; 
    public float windupTime = 0.25f; 
    public float inputBufferWindow = 0.25f; 
    public float throwAnimationDelay = 0.15f; 

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public float maxTrajectoryDistance = 5f; 
    public LayerMask collisionLayer;
    public int trajectoryResolution = 30;
    public float trajectoryTimeStep = 0.1f;

    [Header("Active Powerups")]
    public float rapidFireTimer = 0f;
    public float magnetTimer = 0f;
    public float clusterTimer = 0f; 

    [Header("Active Hazards")]
    public float butterFingersTimer = 0f;
    public float butterFingersWaveSpeed = 4f; 
    public float leadBalloonTimer = 0f; 
    public float brainScrambleTimer = 0f;

    [Header("Multiplayer Visual Sync")]
    public NetworkVariable<bool> isPreparingThrowNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> syncedShadeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("State (Read Only)")]
    public bool isWindingUp = false; 
    public bool isCharging = false;
    public float currentChargeTime = 0f;
    private float windupStartTime = 0f; 
    private float lastThrowTime = -100f; 
    private float lastClickTime = -100f; 

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
        if (IsOwner) syncedShadeIndex.Value = Random.Range(0, 1000); 
    }

    void Update()
    {
        if (!IsOwner || !cameraTransform || !throwPoint) return;

        bool prep = isWindingUp || isCharging;
        if (isPreparingThrowNet.Value != prep) isPreparingThrowNet.Value = prep;

        if (rapidFireTimer > 0) rapidFireTimer -= Time.deltaTime;
        if (magnetTimer > 0) magnetTimer -= Time.deltaTime;
        if (clusterTimer > 0) clusterTimer -= Time.deltaTime; 
        if (butterFingersTimer > 0) butterFingersTimer -= Time.deltaTime; 
        if (leadBalloonTimer > 0) leadBalloonTimer -= Time.deltaTime; 
        if (brainScrambleTimer > 0) brainScrambleTimer -= Time.deltaTime; 

        if (PowerupHUD.Instance != null)
        {
            PowerupHUD.Instance.UpdatePowerupDisplay("Rapid Fire", rapidFireTimer);
            PowerupHUD.Instance.UpdatePowerupDisplay("Magnet", magnetTimer);
            PowerupHUD.Instance.UpdatePowerupDisplay("Cluster", clusterTimer);
            PowerupHUD.Instance.UpdatePowerupDisplay("Lead Balloon", leadBalloonTimer);
            PowerupHUD.Instance.UpdatePowerupDisplay("Butter Fingers", butterFingersTimer);
            PowerupHUD.Instance.UpdatePowerupDisplay("Brain Scramble", brainScrambleTimer);
        }

        if ((playerInteract != null && playerInteract.IsHovering()) || 
            (playerMovement != null && playerMovement.isSprinting))
        {
            if (isWindingUp || isCharging) CancelCharge();
            return;
        }

        HandleChargeInput();
    }

    private void HandleChargeInput()
    {
        float currentCooldown = (rapidFireTimer > 0) ? throwCooldown * 0.25f : throwCooldown;
        float currentWindup = (rapidFireTimer > 0) ? windupTime * 0.25f : windupTime;
        float chargeSpeedMultiplier = (rapidFireTimer > 0) ? 3f : 1f;

        if (Input.GetKeyDown(KeyCode.Mouse0)) lastClickTime = Time.time;
        bool hasBufferedInput = (Time.time - lastClickTime) <= inputBufferWindow;

        if (hasBufferedInput && Input.GetKey(KeyCode.Mouse0) && !isWindingUp && !isCharging && Time.time >= lastThrowTime + currentCooldown)
        {
            isWindingUp = true;
            windupStartTime = Time.time;
            lastClickTime = -100f; 
            
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

        if (!Input.GetKey(KeyCode.Mouse0) && (isWindingUp || isCharging)) CancelCharge();
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

        // --- NEW: Trigger the throw sound across the network ---
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            PlayThrowSoundRpc();
        }

        float chargePercent = currentChargeTime / maxChargeTime;
        bool isLead = leadBalloonTimer > 0;
        
        float actualMaxForce = isLead ? minThrowForce * 0.5f : maxThrowForce;
        float finalForce = Mathf.Lerp(minThrowForce, actualMaxForce, chargePercent);

        if (throwShakeProfile != null) CameraShakerHandler.Shake(throwShakeProfile);

        Vector3 throwDirection = cameraTransform.forward;
        throwDirection.y += (upwardThrowBias / finalForce); 
        throwDirection.Normalize();

        GetCurrentBalloonColors(out Color bColor, out Color pColor);
        
        bool useCluster = clusterTimer > 0;

        StartCoroutine(ThrowWithDelayRoutine(throwPoint.position, throwDirection, finalForce, bColor, pColor, magnetTimer > 0, useCluster, isLead));
        
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

    private IEnumerator ThrowWithDelayRoutine(Vector3 spawnPosition, Vector3 direction, float force, Color bColor, Color pColor, bool isMagnetic, bool isCluster, bool isLead)
    {
        yield return new WaitForSeconds(throwAnimationDelay);
        ThrowServerRpc(spawnPosition, direction, force, bColor, pColor, isMagnetic, isCluster, isLead);
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        float chargePercent = currentChargeTime / maxChargeTime;
        
        float actualMaxForce = (leadBalloonTimer > 0) ? minThrowForce * 0.5f : maxThrowForce;
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
    public void ThrowServerRpc(Vector3 spawnPosition, Vector3 direction, float force, Color bColor, Color pColor, bool isMagnetic, bool isCluster, bool isLead, ServerRpcParams rpcParams = default)
    {
        int balloonsToSpawn = isCluster ? 3 : 1;
        List<Collider> spawnedColliders = new List<Collider>();

        if (isLead)
        {
            bColor = new Color(0.6f, 0.6f, 0.65f); 
            pColor = new Color(0.4f, 0.4f, 0.45f); 
        }

        for (int i = 0; i < balloonsToSpawn; i++)
        {
            Vector3 finalDirection = direction;
            
            if (isCluster)
            {
                if (i == 1) finalDirection = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-25f, -5f), 0) * direction;
                else if (i == 2) finalDirection = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(5f, 25f), 0) * direction;
            }

            GameObject balloonInstance = Instantiate(balloonPrefab, spawnPosition, Quaternion.LookRotation(finalDirection));
            Collider[] newCols = balloonInstance.GetComponentsInChildren<Collider>();
            
            foreach (Collider newCol in newCols)
            {
                foreach (Collider existing in spawnedColliders) Physics.IgnoreCollision(newCol, existing);
                spawnedColliders.Add(newCol);
            }

            NetworkObject networkObj = balloonInstance.GetComponent<NetworkObject>();
            if (balloonInstance.TryGetComponent(out BalloonProjectile proj))
            {
                proj.syncedBalloonColor.Value = bColor;
                proj.syncedParticleColor.Value = pColor;
                proj.isMagnetic.Value = isMagnetic; 
                proj.isLeadBalloon.Value = isLead;
            }

            if (networkObj != null) networkObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

            Rigidbody rb = balloonInstance.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(finalDirection * force, ForceMode.Impulse);
        }
    }

    // ==========================================
    // AUDIO HELPERS & RPCs
    // ==========================================

    [Rpc(SendTo.Everyone)]
    private void PlayThrowSoundRpc()
    {
        if (playerAudioSource != null && throwSound != null)
        {
            // Using a slightly randomized pitch makes rapid-fire throws sound organic, not robotic
            playerAudioSource.pitch = Random.Range(0.9f, 1.1f);
            playerAudioSource.PlayOneShot(throwSound);
            
            // Reset pitch back to normal so it doesn't mess up the powerup sounds
            playerAudioSource.pitch = 1f; 
        }
    }

    private void PlayPowerupSound(bool isPositive)
    {
        if (!IsOwner) return;

        if (playerAudioSource != null)
        {
            AudioClip clipToPlay = isPositive ? positivePowerupSound : negativePowerupSound;
            if (clipToPlay != null)
            {
                playerAudioSource.PlayOneShot(clipToPlay);
            }
        }
    }

    // ==========================================
    // TARGETED CLIENT RPCs
    // ==========================================

    [ClientRpc] 
    public void ApplyButterFingersClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        butterFingersTimer += duration; 
        PlayPowerupSound(false);
    }
    
    [ClientRpc] 
    public void ApplyRapidFireClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        rapidFireTimer += duration; 
        PlayPowerupSound(true);
    }
    
    [ClientRpc] 
    public void ApplyMagnetClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        magnetTimer += duration; 
        PlayPowerupSound(true);
    }
    
    [ClientRpc] 
    public void ApplyBrainScrambleClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        brainScrambleTimer += duration; 
        PlayPowerupSound(false);
    }
    
    [ClientRpc] 
    public void ApplyLeadBalloonClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        leadBalloonTimer += duration; 
        PlayPowerupSound(false);
    }
    
    [ClientRpc] 
    public void ApplyClusterClientRpc(float duration, ClientRpcParams rpcParams = default) 
    { 
        clusterTimer += duration; 
        PlayPowerupSound(true);
    }
}