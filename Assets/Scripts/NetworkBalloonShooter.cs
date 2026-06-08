using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkBalloonShooter : NetworkBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform throwPoint; 
    public GameObject balloonPrefab;
    public PlayerColorManager colorManager; 

    [HideInInspector] public int currentShadeIndex = 0; 

    [Header("Throw Mechanics")]
    public float minThrowForce = 10f;
    public float maxThrowForce = 40f;
    public float maxChargeTime = 1.5f;
    public float upwardThrowBias = 2f; 
    public float throwCooldown = 0.65f; 

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public float maxTrajectoryDistance = 5f; 
    public LayerMask collisionLayer;
    public int trajectoryResolution = 30;
    public float trajectoryTimeStep = 0.1f;

    [Header("State (Read Only)")]
    public bool isCharging = false;
    public float currentChargeTime = 0f;
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
        if (Input.GetKeyDown(KeyCode.Mouse0) && Time.time >= lastThrowTime + throwCooldown)
        {
            isCharging = true;
            currentChargeTime = 0f;
            if (trajectoryLine != null) trajectoryLine.enabled = true;
            OnStartCharge?.Invoke();
        }

        if (Input.GetKey(KeyCode.Mouse0) && isCharging)
        {
            currentChargeTime += Time.deltaTime;
            currentChargeTime = Mathf.Clamp(currentChargeTime, 0, maxChargeTime);
            DrawTrajectory();
        }

        if (Input.GetKeyUp(KeyCode.Mouse0) && isCharging)
        {
            isCharging = false;
            lastThrowTime = Time.time; 
            
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            OnThrow?.Invoke(); 

            float chargePercent = currentChargeTime / maxChargeTime;
            float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);

            Vector3 throwDirection = cameraTransform.forward;
            throwDirection.y += (upwardThrowBias / finalForce); 
            throwDirection.Normalize();

            // --- NEW: Calculate the exact colors locally ---
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

            // Send the raw colors over the network to bake into the projectile
            ThrowServerRpc(throwPoint.position, throwDirection, finalForce, bColor, pColor);
            
            PickNextShade(); 
            currentChargeTime = 0f;
        }
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

    // --- NEW: The RPC now accepts the specific Unity Colors ---
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

        if (networkObj != null)
        {
            networkObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        }

        Rigidbody rb = balloonInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(direction * force, ForceMode.Impulse);
        }
    }
}