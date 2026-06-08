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

    [Header("Audio")]
    public FootstepManager footstepManager;

    private Rigidbody rb;
    
    // Client-side local variables
    private Vector2 currentInput;
    private bool jumpRequested;
    private Vector3 camForward;
    private Vector3 camRight;

    // Server-side tracking variables
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

    void Update()
    {
        // 1. Handle Audio Locally (Runs for all clients so you hear others)
        if (footstepManager != null)
        {
            // Calculate horizontal speed
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            bool isMoving = horizontalVelocity.magnitude > 0.5f; // Threshold to prevent sliding audio when standing still

            // Simple local raycast to see if the player is currently hovering over the ground
            bool isGroundedAudio = Physics.Raycast(transform.position, Vector3.down, raycastLength, groundLayer);

            // Enable footsteps only if moving and grounded
            footstepManager.SetFootsteps(isMoving && isGroundedAudio);
        }

        // 2. Gather Input locally (Only for the owner)
        if (!IsOwner || !playerCamera) return;

        currentInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        // 3. Calculate Camera Vectors
        camForward = playerCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        camRight = playerCamera.right;
        camRight.y = 0;
        camRight.Normalize();

        // 4. Send current state to the Server
        UpdateMovementInputsServerRpc(currentInput, camForward, camRight, jumpRequested);
        
        jumpRequested = false; 
    }

    void FixedUpdate()
    {
        if (IsServer) 
        {
            ApplyPhysicsMovement();
        }
    }

    void ApplyPhysicsMovement()
    {
        bool isGrounded = false;
        
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastLength, groundLayer))
        {
            isGrounded = true;

            float distanceOffset = rideHeight - hit.distance;
            float upwardVelocity = rb.linearVelocity.y;
            
            float springForce = (distanceOffset * rideSpringStrength) - (upwardVelocity * rideSpringDamper);
            
            rb.AddForce(Vector3.up * springForce, ForceMode.Acceleration);
        }

        Vector3 moveDirection = (serverCamForward * serverInput.y + serverCamRight * serverInput.x).normalized;
        Vector3 targetVelocity = moveDirection * maxMoveSpeed;
        
        Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        
        Vector3 velocityDifference = targetVelocity - currentVelocityXZ;
        rb.AddForce(velocityDifference * acceleration, ForceMode.Acceleration);

        if (serverJumpRequested && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            
            serverJumpRequested = false; 
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