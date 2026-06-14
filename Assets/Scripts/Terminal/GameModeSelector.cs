using UnityEngine;
using TMPro;
using DG.Tweening;
using Unity.Netcode;

public class GameModeSelector : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI displayText;
    public CanvasGroup textCanvasGroup; // Optional: Add a CanvasGroup to the text for smooth fading
    
    [Header("Animation Settings")]
    public float scrollDistance = 60f; // How far the text moves up/down
    public float animationDuration = 0.25f;

    private void Start()
    {
        // Set the text immediately on start
        UpdateTextInstantly();
        
        // Listen to network changes so ALL players see the animation when anyone presses the button!
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentGameMode.OnValueChanged += HandleModeChanged;
        }
    }

    private void OnDestroy()
    {
        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.currentGameMode.OnValueChanged -= HandleModeChanged;
        }
    }

    private void UpdateTextInstantly()
    {
        if (NetworkMatchManager.Instance == null) return;
        displayText.text = FormatModeName(NetworkMatchManager.Instance.currentGameMode.Value);
    }

    // Makes the enums look nice on the screen
    private string FormatModeName(NetworkMatchManager.GameMode mode)
    {
        if (mode == NetworkMatchManager.GameMode.ShootingGallery) return "SHOOTING GALLERY";
        if (mode == NetworkMatchManager.GameMode.PvP) return "PVP CLASH";
        return mode.ToString();
    }

    // Fired automatically on all clients when the server updates the variable
    private void HandleModeChanged(NetworkMatchManager.GameMode oldMode, NetworkMatchManager.GameMode newMode)
    {
        // Determine scroll direction
        bool scrolledUp = (int)newMode > (int)oldMode; 
        AnimateScroll(newMode, scrolledUp);
    }

    private void AnimateScroll(NetworkMatchManager.GameMode newMode, bool scrollUp)
    {
        // Kill any active tweens on the text to prevent glitches if a player spams the button
        displayText.transform.DOKill();
        if (textCanvasGroup != null) textCanvasGroup.DOKill();

        float endY = scrollUp ? scrollDistance : -scrollDistance;
        float startY = scrollUp ? -scrollDistance : scrollDistance;

        Sequence seq = DOTween.Sequence();
        
        // 1. Move old text OUT and fade
        seq.Append(displayText.transform.DOLocalMoveY(endY, animationDuration).SetEase(Ease.InBack));
        if (textCanvasGroup != null) seq.Join(textCanvasGroup.DOFade(0, animationDuration));

        // 2. Swap text and snap to the opposite side (invisible)
        seq.AppendCallback(() => 
        {
            displayText.text = FormatModeName(newMode);
            displayText.transform.localPosition = new Vector3(displayText.transform.localPosition.x, startY, displayText.transform.localPosition.z);
        });

        // 3. Move new text IN and fade
        seq.Append(displayText.transform.DOLocalMoveY(0, animationDuration).SetEase(Ease.OutBack));
        if (textCanvasGroup != null) seq.Join(textCanvasGroup.DOFade(1, animationDuration));
    }

    // ==========================================
    // PLAYER INTERACTION BUTTONS
    // ==========================================
    // Hook your PlayerInteract system up to these two functions!
    
    public void InteractScrollUp()
    {
        CycleMode(1);
    }

    public void InteractScrollDown()
    {
        CycleMode(-1);
    }

    private void CycleMode(int direction)
    {
        if (NetworkMatchManager.Instance == null) return;
        if (NetworkMatchManager.Instance.currentState.Value != NetworkMatchManager.MatchState.Lobby) 
        {
            return;
        }
        
        int currentModeInt = (int)NetworkMatchManager.Instance.currentGameMode.Value;
        int totalModes = System.Enum.GetValues(typeof(NetworkMatchManager.GameMode)).Length;

        // Calculate next mode with wrap-around (so pressing up on the last mode goes to the first mode)
        int nextModeInt = (currentModeInt + direction) % totalModes;
        if (nextModeInt < 0) nextModeInt += totalModes;

        NetworkMatchManager.GameMode nextMode = (NetworkMatchManager.GameMode)nextModeInt;

        // Fire the RPC to the server. The server updates the variable, which triggers the animation for everyone!
        NetworkMatchManager.Instance.RequestSetGameModeRpc(nextMode);
    }
}