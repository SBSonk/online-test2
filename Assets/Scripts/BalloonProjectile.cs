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

    public NetworkVariable<Color> syncedBalloonColor = new NetworkVariable<Color>();
    public NetworkVariable<Color> syncedParticleColor = new NetworkVariable<Color>();
    
    private bool hasPopped = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer) Invoke(nameof(DestroyBalloon), autoDestroyTime);
        
        syncedBalloonColor.OnValueChanged += (oldVal, newVal) => ApplyVisuals();
        ApplyVisuals(); 

        // --- NEW: Ignore collision with the person who threw it! ---
        IgnoreOwnerCollisions();
    }

    private void IgnoreOwnerCollisions()
    {
        // Find the player object that belongs to whoever spawned this balloon
        NetworkObject ownerPlayer = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);
        
        if (ownerPlayer != null)
        {
            // Get all colliders on the balloon (usually just one, but safe to grab all)
            Collider[] balloonColliders = GetComponentsInChildren<Collider>();
            
            // Get all colliders on the player (capsule, hands, etc.)
            Collider[] playerColliders = ownerPlayer.GetComponentsInChildren<Collider>();

            // Tell Unity's physics engine that these specific colliders are ghosts to each other
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
            {
                balloonRenderer.material.SetColor("_BaseColor", syncedBalloonColor.Value);
            }
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