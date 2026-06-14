using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // <--- The magic namespace

public class PowerupAnnouncement : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;

    public void Show(string pName, string desc, Sprite icon)
    {
        nameText.text = pName;
        descriptionText.text = desc;
        if (icon != null) iconImage.sprite = icon;

        AnimateSequence();
    }

    private void AnimateSequence()
    {
        // 0. Reset to starting state instantly
        rectTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        // Create a new DOTween Sequence to chain our animations
        Sequence seq = DOTween.Sequence();

        // 1. Pop In
        // Ease.OutBack makes it scale slightly past 1.0 and snap back, giving it a juicy "pop"
        seq.Append(rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
        seq.Join(canvasGroup.DOFade(1f, 0.2f));

        // 2. Hold on screen
        seq.AppendInterval(1.25f);

        // 3. Swoop down and fade out
        // Calculate the target Y position (down 200 pixels)
        float targetY = rectTransform.anchoredPosition.y - 200f;
        
        // Ease.InBack makes it pull up slightly before whipping downward into the HUD
        seq.Append(rectTransform.DOAnchorPosY(targetY, 0.4f).SetEase(Ease.InBack));
        seq.Join(rectTransform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack));
        seq.Join(canvasGroup.DOFade(0f, 0.3f));

        // 4. Cleanup
        seq.OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        // Safety check: kill the tweens if the object is destroyed early (e.g., scene change)
        rectTransform.DOKill();
        canvasGroup.DOKill();
    }
}