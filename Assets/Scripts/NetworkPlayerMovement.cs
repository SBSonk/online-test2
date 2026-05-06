using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    public Transform playerCamera;
    public float moveSpeed = 500;
    
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!IsOwner) return;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        if (IsServer) MovePlayer(input, playerCamera.forward, playerCamera.right);
        else MovePlayerRPC(input, playerCamera.forward, playerCamera.right);
    }

    void MovePlayer(Vector2 dir, Vector3 cameraForward, Vector3 cameraSide)
    {
        // flatten camera dirs
        cameraForward.y = 0;
        cameraSide.y = 0;

        rb.AddForce(cameraSide * dir.x * moveSpeed * Time.deltaTime);
        rb.AddForce(cameraForward * dir.y * moveSpeed * Time.deltaTime);
    }

    [Rpc(SendTo.Server)] // runs this code server side
    void MovePlayerRPC(Vector2 dir, Vector3 cameraForward, Vector3 cameraSide)
    {
        MovePlayer(dir, cameraForward, cameraSide);
    }
}
