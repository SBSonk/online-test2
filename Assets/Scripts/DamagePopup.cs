using UnityEngine;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class DamagePopup : MonoBehaviour
{
    public TMP_Text textMesh;
    private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    public float floatHeight = 1.5f;
    public float animationDuration = 1.2f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void InitializeScore(int scoreAmount, Color popupColor)
    {
        if (textMesh == null) return;

        // 1. Set the color
        textMesh.color = popupColor;

        // 2. Format the score text
        if (scoreAmount > 0)
        {
            textMesh.text = "+" + scoreAmount.ToString("N0");
        }
        else if (scoreAmount < 0)
        {
            textMesh.text = scoreAmount.ToString("N0"); 
        }
        else
        {
            textMesh.text = "+0"; 
        }

        // 3. Trigger the DOTween Sequence
        AnimateAndDestroy();
    }

    private void AnimateAndDestroy()
    {
        // Start small and pop to full size
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        // Float upwards
        transform.DOMoveY(transform.position.y + floatHeight, animationDuration).SetEase(Ease.OutQuad);

        // Fade out starting halfway through the animation
        canvasGroup.alpha = 1f;
        canvasGroup.DOFade(0f, animationDuration * 0.5f).SetDelay(animationDuration * 0.5f)
            .OnComplete(() => {
                // Completely destroy the object when the fade is done to free memory!
                Destroy(gameObject);
            });
    }
}