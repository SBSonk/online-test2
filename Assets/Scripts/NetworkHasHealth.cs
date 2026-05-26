using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkHasHealth : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>(100);
    public NetworkVariable<int> maxHealth = new NetworkVariable<int>(100);

    public UnityEvent<int> onHealthChange;
    public UnityEvent onDeath;

    [Header("Visuals")]
    public DamagePopup damagePopupPrefab;
    public Transform popupSpawnPoint;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        health.OnValueChanged += HandleHealthChange;

        if (IsServer) InitializeSpawn();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        health.OnValueChanged -= HandleHealthChange;
    }

    void HandleHealthChange(int prev, int curr)
    {
        onHealthChange?.Invoke(curr);
    }

    protected virtual void HandleDeath()
    {
        onDeath?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return; 

        health.Value -= amount;

        ShowDamagePopupClientRpc(amount);

        if (health.Value <= 0) HandleDeath();
    }

    [ClientRpc]
    private void ShowDamagePopupClientRpc(int damageAmount)
    {
        if (damagePopupPrefab == null) return; 

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position;

        DamagePopup popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        popup.Initialize(damageAmount);
    }

    public void InitializeSpawn()
    {
        health.Value = maxHealth.Value;
    }
}