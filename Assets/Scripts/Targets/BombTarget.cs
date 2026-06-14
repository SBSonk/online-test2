using Unity.Netcode;
using UnityEngine;

public class BombTarget : CarnivalTarget
{
    [Header("Bomb Visuals")]
    public GameObject explosionParticlePrefab;
    public AudioClip explosionSound;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Safety check: Ensure the base score is ALWAYS negative, 
        // even if someone accidentally types "50" in the Inspector instead of "-50".
        if (baseScoreValue > 0)
        {
            baseScoreValue = -baseScoreValue;
        }
    }

    // This overrides the empty virtual method from CarnivalTarget
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        TriggerExplosionEffectsRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerExplosionEffectsRpc()
    {
        // Spawn the explosion particles
        if (explosionParticlePrefab != null)
        {
            Instantiate(explosionParticlePrefab, transform.position, Quaternion.identity);
        }

        // Play the explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        // Since you are using FirstGearGames Camera Shaker, 
        // you could also call a massive camera shake right here!
    }
}