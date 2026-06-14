public class PowerupRapidFire : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(shooterClientId, out var client))
        {
            if (client.PlayerObject.TryGetComponent(out NetworkBalloonShooter shooter))
            {
                shooter.rapidFireTimer += 10f; // Add 10 seconds of rapid fire
            }
        }
    }
}