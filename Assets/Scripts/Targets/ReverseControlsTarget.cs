// --- ReverseControlsTarget.cs ---
// Attach to your Penalty Prefab
public class ReverseControlsTarget : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        // Removed .Singleton
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.ClientId != shooterClientId)
            {
                if (client.PlayerObject.TryGetComponent(out PlayerState opponentState))
                {
                    opponentState.ActivateReverseControls(5f); // 5 seconds of reversed controls
                }
            }
        }
    }
}