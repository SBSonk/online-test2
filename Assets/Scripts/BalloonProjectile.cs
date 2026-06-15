using Unity.Netcode;
using UnityEngine;

public class BalloonProjectile : NetworkBehaviour
{
    public enum HitSoundType { Pop, Bounce, Lead }

    [Header("References")]
    public BalloonVisuals visuals; 
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip popSound;       // Standard hit on a player/target
    public AudioClip bounceSound;    // Squeak/Thud when hitting a wall/floor
    public AudioClip leadHitSound;   // Heavy metallic clank for lead balloons

    [Header("Settings")]
    public float autoDestroyTime = 5f;
    public float damage = 10f;  
    public float knockbackForce = 25f; 
    public float upwardKnockback = 8f; 

    [Header("Magnetism & Modifiers")]
    public NetworkVariable<bool> isMagnetic = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isLeadBalloon = new NetworkVariable<bool>(false); // --- NEW: Tracks if this is heavy! ---
    public float magnetRadius = 8f;
    public float magnetForce = 20f;
    public LayerMask targetLayer;

    public NetworkVariable<Color> syncedBalloonColor = new NetworkVariable<Color>();
    public NetworkVariable<Color> syncedParticleColor = new NetworkVariable<Color>();
    
    private bool hasPopped = false;
    private Rigidbody rb;
    
    private Transform lockedTarget = null; 

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        
        if (IsServer) Invoke(nameof(DestroyBalloon), autoDestroyTime);
        
        syncedBalloonColor.OnValueChanged += (oldVal, newVal) => ApplyVisuals();
        ApplyVisuals(); 

        if (visuals != null) visuals.currentState = BalloonVisuals.BalloonState.InAir;

        IgnoreOwnerCollisions();
    }

    private void Update()
    {
        if (visuals != null && rb != null && !hasPopped)
        {
            visuals.simulatedVelocity = rb.linearVelocity.magnitude;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || hasPopped) return;

        // 1. Magnetism Logic
        if (isMagnetic.Value)
        {
            if (lockedTarget == null)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, magnetRadius, targetLayer);
                float closestDistance = Mathf.Infinity;

                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out CarnivalTarget target))
                    {
                        if (target.targetCategory == CarnivalTarget.TargetCategory.Bomb) continue;

                        float dist = Vector3.Distance(transform.position, hit.transform.position);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            lockedTarget = hit.transform;
                        }
                    }
                }
            }

            // --- NEW: Toggle Gravity based on Homing State ---
            if (lockedTarget != null)
            {
                if (rb.useGravity) rb.useGravity = false; // Turn off gravity when homing
                
                Vector3 direction = (lockedTarget.position - transform.position).normalized;
                rb.AddForce(direction * magnetForce, ForceMode.Acceleration);
            }
            else
            {
                // Ensure gravity is back on if we lose the target
                if (!rb.useGravity) rb.useGravity = true;
            }
        }
        else
        {
            // Ensure gravity is always on if magnetism is off
            if (!rb.useGravity) rb.useGravity = true;
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
        if (visuals != null) 
        {
            visuals.ApplyColor(syncedBalloonColor.Value);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasPopped) return;
        hasPopped = true;

        bool hitValidTarget = false;

        // Check if we hit a player
        if (collision.gameObject.TryGetComponent(out NetworkHasHealth health))
        {
            health.TakeDamage((int)damage, OwnerClientId);
            NotifyHitRpc();
            hitValidTarget = true;
        }

        // Check if we hit a player movement controller (for knockback)
        if (collision.gameObject.TryGetComponent(out NetworkPlayerMovement playerMove))
        {
            Vector3 knockbackDir = transform.forward;
            knockbackDir.y = 0; 
            knockbackDir.Normalize();
            Vector3 finalForce = (knockbackDir * knockbackForce) + (Vector3.up * upwardKnockback);

            playerMove.TakeBalloonHit(finalForce, syncedBalloonColor.Value);
            NotifyHitRpc();
            hitValidTarget = true;
        }

        // Check if we hit a standard target
        if (collision.gameObject.TryGetComponent(out CarnivalTarget target))
        {
            hitValidTarget = true;
        }

        // --- NEW: Determine which sound to play based on the impact ---
        HitSoundType soundToPlay = HitSoundType.Bounce; // Default to wall squeak

        if (isLeadBalloon.Value) 
        {
            soundToPlay = HitSoundType.Lead; // Lead overrides everything
        }
        else if (hitValidTarget)
        {
            soundToPlay = HitSoundType.Pop; // We hit something breakable/damaging!
        }

        // Tell all clients to play the visuals AND the correct sound
        TriggerPopClientRpc(soundToPlay);

        Invoke(nameof(DestroyBalloon), 1f);
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerPopClientRpc(HitSoundType soundType)
    {
        if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        
        if (visuals != null) 
        {
            visuals.TestPop(); 
        }

        // --- NEW: Play the correct sound ---
        if (audioSource != null)
        {
            switch (soundType)
            {
                case HitSoundType.Pop:
                    if (popSound != null) audioSource.PlayOneShot(popSound);
                    break;
                case HitSoundType.Bounce:
                    if (bounceSound != null) audioSource.PlayOneShot(bounceSound);
                    break;
                case HitSoundType.Lead:
                    if (leadHitSound != null) audioSource.PlayOneShot(leadHitSound);
                    break;
            }
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