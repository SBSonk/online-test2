using UnityEngine;

public class TerminalButton : Interactable
{
    public NetworkLobbyTerminal terminal;
    public enum ButtonType { Start, TimeUp, TimeDown, TargetsUp, TargetsDown }
    public ButtonType myFunction;

    public override void Interact(GameObject player)
    {
        base.Interact(player); // Fires the UnityEvents just in case
        
        if (terminal == null) return;

        switch (myFunction)
        {
            case ButtonType.Start: terminal.InteractStartMatch(); break;
            case ButtonType.TimeUp: terminal.InteractIncreaseTime(); break;
            case ButtonType.TimeDown: terminal.InteractDecreaseTime(); break;
            case ButtonType.TargetsUp: terminal.InteractIncreaseTargets(); break;
            case ButtonType.TargetsDown: terminal.InteractDecreaseTargets(); break;
        }
    }
}