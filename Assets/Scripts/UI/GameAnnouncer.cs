using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

public class GameAnnouncer : MonoBehaviour
{
    public TMP_Text announcerText;
    public CanvasGroup canvasGroup;

    // Use this for "GET", "TO", "YOUR", "BOOTHS!"
    public void AnnounceSequence(string[] words, float delayBetweenWords)
    {
        // Stop any active tweens/routines to prevent overlapping glitches
        StopAllCoroutines();
        canvasGroup.DOKill();
        announcerText.transform.DOKill();
        
        StartCoroutine(WordByWordRoutine(words, delayBetweenWords));
    }

    private IEnumerator WordByWordRoutine(string[] words, float delay)
    {
        canvasGroup.alpha = 1f;
        foreach (string word in words)
        {
            announcerText.text = word;
            // Pop effect for each word
            announcerText.transform.localScale = Vector3.zero;
            announcerText.transform.DOScale(1.5f, 0.2f).SetEase(Ease.OutBack);
            
            yield return new WaitForSeconds(delay);
        }
        
        // Fade out after the sequence finishes
        canvasGroup.DOFade(0f, 0.5f).SetDelay(0.5f);
    }

    // Use this for Countdown Numbers and the Final Message
    public void ShowMessage(string message, float displayDuration)
    {
        StopAllCoroutines();
        canvasGroup.DOKill();
        announcerText.transform.DOKill();

        announcerText.text = message;
        announcerText.transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        announcerText.transform.DOPunchScale(Vector3.one * 0.2f, 0.5f);

        // Fade out automatically after the display duration
        canvasGroup.DOFade(0f, 0.5f).SetDelay(displayDuration);
    }
}