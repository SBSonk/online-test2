public class PowerupMagnet : CarnivalTarget
{
    protected override void ApplySpecialEffect(ulong shooterClientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(shooterClientId, out var client))
        {
            if (client.PlayerObject.TryGetComponent(out NetworkBalloonShooter shooter))
            {
                shooter.magnetTimer += 15f; // Add 15 seconds of homing balloons
            }
        }
    }
}