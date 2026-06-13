using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement (Smooth)")]
    public Transform playerCamera;
    public float maxMoveSpeed = 8f;
    public float acceleration = 15f; 
    public float jumpForce = 7f;

    [Header("Suspension (Stairs/Hover)")]
    public float rideHeight = 1f; 
    public float raycastLength = 1.5f; 
    public float rideSpringStrength = 250f;
    public float rideSpringDamper = 20f;
    public LayerMask groundLayer;

    [Header("Audio & Animation")]
    public FootstepManager footstepManager;
    public NetworkHandsAnimator handAnimator; 

    // --- NEW: Camera Shake Reference ---
    [Header("Camera Effects")]
    public ProceduralCameraShaker cameraShaker; 

    // --- NEW: Stun & Knockback Variables ---
    [Header("Stun Settings")]
    public float stunDuration = 3f;
    public NetworkBalloonShooter balloonShooter;
    public PlayerInteract playerInteract;
    private bool isStunned = false; 

    private Rigidbody rb;
    
    private Vector2 currentInput;
    private bool jumpRequested;
    private Vector3 camForward;
    private Vector3 camRight;

    private Vector2 serverInput;
    private Vector3 serverCamForward;
    private Vector3 serverCamRight;
    private bool serverJumpRequested;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation; 
    }

    // ==========================================
    // STUN & KNOCKBACK LOGIC
    // ==========================================

    public void TakeBalloonHit(Vector3 force, Color splatColor)
    {
        if (!IsServer) return;

        isStunned = true;
        
        rb.constraints = RigidbodyConstraints.None;
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * force.magnitude, ForceMode.Impulse);

        ApplyStunEffectsRpc(splatColor, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));

        Invoke(nameof(RecoverFromStun), stunDuration);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ApplyStunEffectsRpc(Color splatColor, RpcParams rpcParams = default)
    {
        if (balloonShooter != null) balloonShooter.enabled = false;
        if (playerInteract != null) playerInteract.enabled = false;

        BalloonSplatUI splatUI = FindAnyObjectByType<BalloonSplatUI>();
        if (splatUI != null) splatUI.ShowSplat(splatColor);

        // --- NEW: Apply massive explosion shake ---
        if (cameraShaker != null) cameraShaker.AddTrauma(0.85f);
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
    // NORMAL MOVEMENT LOOP
    // ==========================================

    void Update()
    {
        bool isMoving = false;
        bool isGroundedAudio = false;

        if (footstepManager != null || cameraShaker != null)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            isMoving = horizontalVelocity.magnitude > 0.5f; 
            isGroundedAudio = Physics.Raycast(transform.position, Vector3.down, raycastLength, groundLayer);
            
            if (footstepManager != null) footstepManager.SetFootsteps(isMoving && isGroundedAudio);
        }

        // --- NEW: Apply continuous walking rumble ---
        if (cameraShaker != null)
        {
            // Change this:
            cameraShaker.SetBaseTrauma((isMoving && isGroundedAudio && !isStunned) ? 0.08f : 0f);
        }

        if (!IsOwner || !playerCamera) return;

        if (balloonShooter != null && !balloonShooter.enabled) 
        {
            currentInput = Vector2.zero;
            UpdateMovementInputsServerRpc(Vector2.zero, Vector3.forward, Vector3.right, false);
            return;
        }

        currentInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        if (handAnimator != null)
        {
            handAnimator.SetWalking(currentInput.sqrMagnitude > 0.01f);
        }

        if (Input.GetKeyDown(KeyCode.Space)) jumpRequested = true;

        camForward = playerCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        camRight = playerCamera.right;
        camRight.y = 0;
        camRight.Normalize();

        UpdateMovementInputsServerRpc(currentInput, camForward, camRight, jumpRequested);
        
        jumpRequested = false; 
    }

    void FixedUpdate()
    {
        if (IsServer) ApplyPhysicsMovement();
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
            Vector3 moveDirection = (serverCamForward * serverInput.y + serverCamRight * serverInput.x).normalized;
            Vector3 targetVelocity = moveDirection * maxMoveSpeed;
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
    void UpdateMovementInputsServerRpc(Vector2 dir, Vector3 forward, Vector3 right, bool jump)
    {
        serverInput = dir;
        serverCamForward = forward;
        serverCamRight = right;
        if (jump) serverJumpRequested = true;
    }
}