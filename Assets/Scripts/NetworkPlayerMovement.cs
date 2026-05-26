using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public Transform playerCamera;
    public float moveSpeed = 8f; 
    public float jumpForce = 5f; 
    
    [Header("Ground Detection")]
    public Transform groundCheck; // Create an empty GameObject at the player's feet and assign it here
    public LayerMask groundLayer; // Set this to your floor layer
    public float groundDistance = 0.2f;

    private Rigidbody rb;
    private Vector2 input;
    private bool jumpInput;
    private Vector3 camForward;
    private Vector3 camRight;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate; 
    }

    void Update()
    {
        if (!IsOwner || !playerCamera) return;

        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        // Read jump input locally
        jumpInput = Input.GetKeyDown(KeyCode.Space);
        
        camForward = playerCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        camRight = playerCamera.right;
        camRight.y = 0;
        camRight.Normalize();

        if (IsServer) 
        {
            ApplyMovement(input, camForward, camRight, jumpInput);
        }
        else 
        {
            MovePlayerServerRpc(input, camForward, camRight, jumpInput);
        }
    }

    void ApplyMovement(Vector2 dir, Vector3 forward, Vector3 right, bool wantsToJump)
    {
        // 1. Calculate horizontal movement
        Vector3 moveDirection = (forward * dir.y + right * dir.x).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;

        // 2. Preserve vertical velocity (so gravity still pulls us down)
        targetVelocity.y = rb.linearVelocity.y;

        // 3. Server-Authoritative Ground Check
        // We do this on the server so hacked clients can't lie about being on the ground
        bool isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundLayer);

        // 4. Apply Jump
        if (wantsToJump && isGrounded)
        {
            // Override the Y velocity for an instant upward burst
            targetVelocity.y = jumpForce;
        }

        // Apply everything to the Rigidbody
        rb.linearVelocity = targetVelocity;
    }

    [ServerRpc]
    void MovePlayerServerRpc(Vector2 dir, Vector3 forward, Vector3 right, bool jump)
    {
        ApplyMovement(dir, forward, right, jump);
    }
}