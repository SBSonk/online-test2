using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using NaughtyAttributes; // Ensure you have the NaughtyAttributes package imported

public class NetworkMatchManager : NetworkBehaviour
{
    public static NetworkMatchManager Instance;

    public NetworkVariable<bool> isGameActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> matchTimer = new NetworkVariable<float>(60f);

    [Header("Settings")]
    public float matchDuration = 60f;

    [Header("Events")]
    public UnityEvent OnMatchStart;
    public UnityEvent OnMatchEnd;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (!IsServer || !isGameActive.Value) return;

        matchTimer.Value -= Time.deltaTime;

        if (matchTimer.Value <= 0)
        {
            EndMatch();
        }
    }

    // --- DEBUG INSPECTOR BUTTON ---
    [Button("Host: Start Match", EButtonEnableMode.Playmode)]
    public void DebugStartMatchFromInspector()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only the host/server can trigger the match start from the inspector!");
            return;
        }
        RequestStartMatch();
    }

    public void RequestStartMatch()
    {
        if (IsServer)
        {
            StartMatch();
        }
        else
        {
            StartMatchServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartMatchServerRpc()
    {
        StartMatch();
    }

    private void StartMatch()
    {
        if (isGameActive.Value) return; 
        
        matchTimer.Value = matchDuration;
        isGameActive.Value = true;
        
        TriggerMatchStartClientRpc();
    }

    private void EndMatch()
    {
        matchTimer.Value = 0;
        isGameActive.Value = false;
        
        TriggerMatchEndClientRpc();
    }

    [ClientRpc]
    private void TriggerMatchStartClientRpc()
    {
        OnMatchStart?.Invoke();
    }

    [ClientRpc]
    private void TriggerMatchEndClientRpc()
    {
        OnMatchEnd?.Invoke();
    }
}