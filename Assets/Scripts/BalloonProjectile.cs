using Unity.Netcode;
using UnityEngine;

public class BalloonProjectile : NetworkBehaviour
{
    [Header("References")]
    public GameObject balloonMesh;
    public Renderer balloonRenderer; 
    public ParticleSystem popParticles;
    
    [Header("Settings")]
    public float autoDestroyTime = 5f;
    public float damage = 10f; 
    public float knockbackForce = 25f; 
    public float upwardKnockback = 8f; 

    [Header("Magnetism")]
    public NetworkVariable<bool> isMagnetic = new NetworkVariable<bool>(false);
    public float magnetRadius = 8f;
    public float magnetForce = 20f;
    public LayerMask targetLayer; // Make sure to set this to your Target Layer in Inspector!

    public NetworkVariable<Color> syncedBalloonColor = new NetworkVariable<Color>();
    public NetworkVariable<Color> syncedParticleColor = new NetworkVariable<Color>();
    
    private bool hasPopped = false;
    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        
        if (IsServer) Invoke(nameof(DestroyBalloon), autoDestroyTime);
        
        syncedBalloonColor.OnValueChanged += (oldVal, newVal) => ApplyVisuals();
        ApplyVisuals(); 

        IgnoreOwnerCollisions();
    }

    // --- NEW: Physics Homing Logic ---
    private void FixedUpdate()
    {
        // Only the server calculates physics movement. 
        // This keeps the movement synced for all clients.
        if (!IsServer || !isMagnetic.Value || hasPopped) return;

        // Find targets in range
        Collider[] hits = Physics.OverlapSphere(transform.position, magnetRadius, targetLayer);
        
        Transform closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (var hit in hits)
        {
            // We check for CarnivalTarget (or whatever component your targets use)
            if (hit.TryGetComponent(out CarnivalTarget target))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestTarget = hit.transform;
                }
            }
        }

        // Apply force toward the closest target
        if (closestTarget != null)
        {
            Vector3 direction = (closestTarget.position - transform.position).normalized;
            rb.AddForce(direction * magnetForce, ForceMode.Acceleration);
        }
    }

    private void IgnoreOwnerCollisions()
    {
        NetworkObject ownerPlayer = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);
        if (ownerPlayer != null)
        {
            Collider[] balloonColliders = GetComponentsInChildren<Collider>();
            Collider[] playerColliders = ownerPlayer.GetComponentsInChildren<Collider>();
            foreach (Collider bCol in balloonColliders)
            {
                foreach (Collider pCol in playerColliders)
                {
                    Physics.IgnoreCollision(bCol, pCol, true);
                }
            }
        }
    }

    private void ApplyVisuals()
    {
        if (balloonRenderer != null) 
        {
            balloonRenderer.material.color = syncedBalloonColor.Value;
            if (balloonRenderer.material.HasProperty("_BaseColor"))
                balloonRenderer.material.SetColor("_BaseColor", syncedBalloonColor.Value);
        }
            
        if (popParticles != null)
        {
            var main = popParticles.main;
            main.startColor = syncedParticleColor.Value;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasPopped) return;
        hasPopped = true;

        TriggerPopClientRpc();

        if (collision.gameObject.TryGetComponent(out NetworkHasHealth health))
        {
            health.TakeDamage((int)damage, OwnerClientId);
            NotifyHitRpc();
        }

        if (collision.gameObject.TryGetComponent(out NetworkPlayerMovement playerMove))
        {
            Vector3 knockbackDir = transform.forward;
            knockbackDir.y = 0; 
            knockbackDir.Normalize();
            Vector3 finalForce = (knockbackDir * knockbackForce) + (Vector3.up * upwardKnockback);

            playerMove.TakeBalloonHit(finalForce, syncedBalloonColor.Value);
            NotifyHitRpc();
        }

        Invoke(nameof(DestroyBalloon), 1f);
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerPopClientRpc()
    {
        if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        if (balloonMesh != null) balloonMesh.SetActive(false);
        if (popParticles != null) 
        {
            popParticles.gameObject.SetActive(true);
            popParticles.Play();
        }
    }

    [Rpc(SendTo.Owner)] 
    private void NotifyHitRpc()
    {
        HUDManager hud = FindAnyObjectByType<HUDManager>();
        if (hud != null) hud.ShowHitMarker();
    }

    private void DestroyBalloon()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}