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

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (IsServer) MovePlayer(input);
        else MovePlayerRPC(input);
    }

    void MovePlayer(Vector2 dir)
    {
        //if (!playerCamera) return;

        rb.AddForce(transform.right * dir.x * moveSpeed * Time.deltaTime);
        rb.AddForce(transform.forward * dir.y * moveSpeed * Time.deltaTime);
    }

    [Rpc(SendTo.Server)] // runs this code server side
    void MovePlayerRPC(Vector2 dir)
    {
        MovePlayer(dir);
    }
}
