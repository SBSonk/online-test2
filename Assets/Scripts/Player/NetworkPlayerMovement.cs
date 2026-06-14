using Unity.Netcode;
using UnityEngine;
using FirstGearGames.SmoothCameraShaker;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement (Smooth)")]
    public Transform playerCamera;
    public float maxMoveSpeed = 8f;
    
    [Header("Sprint Settings")]
    public bool useToggleSprint = false; 
    public float sprintSpeedMultiplier = 1.5f; 
    [Tooltip("How quickly the player accelerates up to max sprint speed or decelerates back to walk speed.")]
    public float sprintBuildUpRate = 10f; 
    public float acceleration = 15f; 
    public float jumpForce = 7f;

    // Tracks the smoothly interpolating speed on the server
    private float currentMaxSpeed;

    [Header("Suspension (Stairs/Hover)")]
    public float rideHeight = 1f; 
    public float raycastLength = 1.5f; 
    public float rideSpringStrength = 250f;
    public float rideSpringDamper = 20f;
    public LayerMask groundLayer;

    [Header("Booth Snapping")]
    public float boothSnapSpeed = 5f;
    private bool isLockedToBooth = false;
    private Transform currentBoothTarget;

    [Header("Audio & Animation")]
    public FootstepManager footstepManager;
    public NetworkHandsAnimator handAnimator; 

    [Header("Camera Shake Profiles")]
    public ShakeData walkShakeProfile;
    public ShakeData runShakeProfile; 
    public ShakeData hitShakeProfile;

    [Header("Game State & Interactions")]
    public PlayerState playerState; 
    public NetworkBalloonShooter balloonShooter;
    public PlayerInteract playerInteract;

    [Header("Stun Settings")]
    public float stunDuration = 3f;
    private bool isStunned = false; 

    private Rigidbody rb;
    private Vector2 currentInput;
    private bool jumpRequested;
    public bool isSprinting { get; private set; } 
    private Vector3 camForward;
    private Vector3 camRight;

    private Vector2 serverInput;
    private Vector3 serverCamForward;
    private Vector3 serverCamRight;
    private bool serverJumpRequested;
    private bool serverSprintRequested; 

    [Header("Cameras & Shakers")]
    public CinemachineCamera vCam; 
    public Transform shakeContainer; 

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation; 
        
        currentMaxSpeed = maxMoveSpeed;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CameraShaker shaker = shakeContainer.GetComponent<CameraShaker>();

        if (IsOwner)
        {
            if (vCam != null) vCam.enabled = true;
            if (shaker != null) shaker.enabled = true; 
        }
        else
        {
            if (vCam != null) vCam.enabled = false;
            if (shaker != null) Destroy(shaker);
        }
    }

    private void OnEnable()
    {
        if (footstepManager != null) footstepManager.OnStep += TriggerStepShake;
    }

    private void OnDisable()
    {
        if (footstepManager != null) footstepManager.OnStep -= TriggerStepShake;
    }

    private void TriggerStepShake()
    {
        if (!IsOwner || isStunned) return;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float currentSpeedRatio = horizontalVelocity.magnitude / maxMoveSpeed;

        if (isSprinting && currentSpeedRatio > 1.1f)
        {
            if (runShakeProfile != null) CameraShakerHandler.Shake(runShakeProfile);
        }
        else
        {
            if (walkShakeProfile != null) CameraShakerHandler.Shake(walkShakeProfile);
        }
    }

    // ==========================================
    // STUN & KNOCKBACK LOGIC
    // ==========================================

    public void TakeBalloonHit(Vector3 force, Color splatColor)
    {
        if (!IsServer) return;

        isStunned = true;
        
        serverSprintRequested = false;
        currentMaxSpeed = maxMoveSpeed; 

        rb.constraints = RigidbodyConstraints.None;
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * force.magnitude, ForceMode.Impulse);

        ApplyStunEffectsRpc(splatColor, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));

        Invoke(nameof(RecoverFromStun), stunDuration);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ApplyStunEffectsRpc(Color splatColor, RpcParams rpcParams = default)
    {
        isSprinting = false; 

        if (balloonShooter != null) balloonShooter.enabled = false;
        if (playerInteract != null) playerInteract.enabled = false;

        BalloonSplatUI splatUI = FindAnyObjectByType<BalloonSplatUI>();
        if (splatUI != null) splatUI.ShowSplat(splatColor);

        if (hitShakeProfile != null) CameraShakerHandler.Shake(hitShakeProfile);

        UpdateMovementInputsServerRpc(Vector2.zero, camForward, camRight, false, false);
    }

    private void RecoverFromStun()
    {
        if (!IsServer) return;
        isStunned = false;

        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        RecoverLocalInputsRpc(RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void RecoverLocalInputsRpc(RpcParams rpcParams = default)
    {
        if (balloonShooter != null) balloonShooter.enabled = true;
        if (playerInteract != null) playerInteract.enabled = true;
    }

    // ==========================================
    // BOOTH LOCK LOGIC
    // ==========================================
    
    public void SetBoothLock(bool locked, Transform boothTarget)
    {
        if (!IsServer) return;

        isLockedToBooth = locked;
        currentBoothTarget = boothTarget;

        if (locked)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            currentMaxSpeed = 0f; 
        }
    }

    // ==========================================
    // NORMAL MOVEMENT LOOP
    // ==========================================

    void Update()
    {
        bool isMoving = false;
        bool isGroundedAudio = false;

        if (footstepManager != null || walkShakeProfile != null)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            isMoving = horizontalVelocity.magnitude > 0.5f; 
            isGroundedAudio = Physics.Raycast(transform.position, Vector3.down, raycastLength, groundLayer);
            
            if (footstepManager != null) 
            {
                footstepManager.SetFootsteps(isMoving && isGroundedAudio);
                footstepManager.SetSprintState(isSprinting); 
            }
        }

        if (!IsOwner || !playerCamera) return;

        if (balloonShooter != null && !balloonShooter.enabled) 
        {
            currentInput = Vector2.zero;
            isSprinting = false;
            UpdateMovementInputsServerRpc(Vector2.zero, Vector3.forward, Vector3.right, false, false);
            return;
        }

        currentInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        bool hasMovementInput = currentInput.sqrMagnitude > 0.01f;
        
        // --- NEW: Check if the weapon is actively being used ---
        bool isShooting = balloonShooter != null && (balloonShooter.isWindingUp || balloonShooter.isCharging);

        // --- FIXED SPRINT INPUT LOGIC ---
        if (useToggleSprint)
        {
            // Only allow turning toggle sprint ON if we aren't shooting
            if (Input.GetKeyDown(KeyCode.LeftShift) && hasMovementInput && !isShooting)
            {
                isSprinting = !isSprinting;
            }
            // Auto-cancel if the player stops walking OR starts charging a balloon
            else if (!hasMovementInput || isShooting)
            {
                isSprinting = false;
            }
        }
        else
        {
            // Must hold shift, have movement input, AND not be shooting
            isSprinting = Input.GetKey(KeyCode.LeftShift) && hasMovementInput && !isShooting;
        }

        // QOL FIX: Also auto-cancel sprint immediately the exact frame the player clicks
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            isSprinting = false;
        }
        
        // --- REVERSE CONTROLS CHECK ---
        if (playerState != null && playerState.hasReversedControls.Value)
        {
            currentInput *= -1f; 
        }

        if (handAnimator != null) handAnimator.SetWalking(hasMovementInput);
        if (Input.GetKeyDown(KeyCode.Space)) jumpRequested = true;

        camForward = playerCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        camRight = playerCamera.right;
        camRight.y = 0;
        camRight.Normalize();

        UpdateMovementInputsServerRpc(currentInput, camForward, camRight, jumpRequested, isSprinting);
        jumpRequested = false; 
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if (isLockedToBooth && currentBoothTarget != null)
        {
            Vector3 targetPosition = Vector3.Lerp(rb.position, currentBoothTarget.position, Time.fixedDeltaTime * boothSnapSpeed);
            rb.MovePosition(targetPosition);

            Quaternion targetRotation = Quaternion.Slerp(rb.rotation, currentBoothTarget.rotation, Time.fixedDeltaTime * boothSnapSpeed);
            rb.MoveRotation(targetRotation);

            rb.useGravity = false; 
            return; 
        }

        rb.useGravity = true;
        ApplyPhysicsMovement();
    }

    void ApplyPhysicsMovement()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastLength, groundLayer))
        {
            float distanceOffset = rideHeight - hit.distance;
            float upwardVelocity = rb.linearVelocity.y;
            float springForce = (distanceOffset * rideSpringStrength) - (upwardVelocity * rideSpringDamper);
            rb.AddForce(Vector3.up * springForce, ForceMode.Acceleration);
        }

        if (!isStunned)
        {
            float targetSpeed = serverSprintRequested ? (maxMoveSpeed * sprintSpeedMultiplier) : maxMoveSpeed;
            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, targetSpeed, sprintBuildUpRate * Time.fixedDeltaTime);

            Vector3 moveDirection = (serverCamForward * serverInput.y + serverCamRight * serverInput.x).normalized;
            Vector3 targetVelocity = moveDirection * currentMaxSpeed;
            Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            
            Vector3 velocityDifference = targetVelocity - currentVelocityXZ;
            rb.AddForce(velocityDifference * acceleration, ForceMode.Acceleration);

            if (serverJumpRequested && Physics.Raycast(transform.position, Vector3.down, raycastLength, groundLayer))
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
                serverJumpRequested = false; 
            }
        }
    }

    [ServerRpc]
    void UpdateMovementInputsServerRpc(Vector2 dir, Vector3 forward, Vector3 right, bool jump, bool sprint)
    {
        serverInput = dir;
        serverCamForward = forward;
        serverCamRight = right;
        if (jump) serverJumpRequested = true;
        serverSprintRequested = sprint;
    }
}