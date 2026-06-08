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

    // NEW: We now sync the exact colors directly! No player lookups required.
    public NetworkVariable<Color> syncedBalloonColor = new NetworkVariable<Color>();
    public NetworkVariable<Color> syncedParticleColor = new NetworkVariable<Color>();
    
    private bool hasPopped = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(DestroyBalloon), autoDestroyTime);
        }

        syncedBalloonColor.OnValueChanged += (oldVal, newVal) => ApplyVisuals();
        
        // Because the shooter script sets these variables before spawning, 
        // the colors are 100% guaranteed to be here on frame 1.
        ApplyVisuals(); 
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
            // Cast damage to int based on our previous health script update
            health.TakeDamage((int)damage, OwnerClientId);
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
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}