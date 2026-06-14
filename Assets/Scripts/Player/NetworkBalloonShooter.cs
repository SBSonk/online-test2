using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using FirstGearGames.SmoothCameraShaker;

public class NetworkBalloonShooter : NetworkBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform throwPoint; 
    public GameObject balloonPrefab;
    public PlayerColorManager colorManager; 
    public NetworkHandsAnimator handAnimator; 
    public PlayerInteract playerInteract; 

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

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public float maxTrajectoryDistance = 5f; 
    public LayerMask collisionLayer;
    public int trajectoryResolution = 30;
    public float trajectoryTimeStep = 0.1f;

    [Header("State (Read Only)")]
    public bool isWindingUp = false; 
    public bool isCharging = false;
    public float currentChargeTime = 0f;
    private float windupStartTime = 0f; 
    private float lastThrowTime = -100f; 

    [Header("Events")]
    public UnityEvent OnStartCharge;
    public UnityEvent OnThrow;

    void OnApplicationFocus(bool focus)
    {
        if (!focus) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        PickNextShade();
    }

    public void PickNextShade()
    {
        currentShadeIndex = Random.Range(0, 1000); 
    }

    void Update()
    {
        if (!IsOwner || !cameraTransform || !throwPoint) return;
        HandleChargeInput();
    }

    private void HandleChargeInput()
    {
        // Only allow shooting if the state is ACTIVE!
        if (NetworkMatchManager.Instance != null && NetworkMatchManager.Instance.currentState.Value != NetworkMatchManager.MatchState.Active)
        {
            if (isWindingUp || isCharging) CancelCharge();
            return;
        }

        if (playerInteract != null && playerInteract.IsHovering())
        {
            if (isWindingUp || isCharging) CancelCharge();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Mouse0) && Time.time >= lastThrowTime + throwCooldown)
        {
            isWindingUp = true;
            windupStartTime = Time.time;
            if (handAnimator != null) handAnimator.SetThrowWindup(true); 
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (isWindingUp)
            {
                if (Time.time >= windupStartTime + windupTime)
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
                currentChargeTime += Time.deltaTime;
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0, maxChargeTime);
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
        float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);

        if (throwShakeProfile != null)
        {
            CameraShakerHandler.Shake(throwShakeProfile);
        }

        Vector3 throwDirection = cameraTransform.forward;
        throwDirection.y += (upwardThrowBias / finalForce); 
        throwDirection.Normalize();

        Color bColor = Color.white;
        Color pColor = Color.white;

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

        ThrowServerRpc(throwPoint.position, throwDirection, finalForce, bColor, pColor);
        
        PickNextShade(); 
        currentChargeTime = 0f;
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        float chargePercent = currentChargeTime / maxChargeTime;
        float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);

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
    public void ThrowServerRpc(Vector3 spawnPosition, Vector3 direction, float force, Color bColor, Color pColor, ServerRpcParams rpcParams = default)
    {
        GameObject balloonInstance = Instantiate(balloonPrefab, spawnPosition, Quaternion.LookRotation(direction));
        NetworkObject networkObj = balloonInstance.GetComponent<NetworkObject>();
        
        if (balloonInstance.TryGetComponent(out BalloonProjectile proj))
        {
            proj.syncedBalloonColor.Value = bColor;
            proj.syncedParticleColor.Value = pColor;
        }

        if (networkObj != null) networkObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

        Rigidbody rb = balloonInstance.GetComponent<Rigidbody>();
        if (rb != null) rb.AddForce(direction * force, ForceMode.Impulse);
    }
}