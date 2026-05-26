using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkHasHealth : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>(100);
    public NetworkVariable<int> maxHealth = new NetworkVariable<int>(100);

    public UnityEvent<int> onHealthChange;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        health.OnValueChanged += HandleHealthChange;
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
        NetworkObject.Despawn();
    }

    [ServerRpc()]
    public void TakeDamageServerRpc(int amount)
    {
        health.Value -= amount;

        if (health.Value <= 0) HandleDeath();
    }
}
 