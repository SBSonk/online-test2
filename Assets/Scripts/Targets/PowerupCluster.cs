public class PowerupCluster : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(shooterClientId, out var client))
        {
            if (client.PlayerObject.TryGetComponent(out NetworkBalloonShooter shooter))
            {
                shooter.clusterShotsRemaining += 3; // Give 3 shotgun blasts
            }
        }
    }
}