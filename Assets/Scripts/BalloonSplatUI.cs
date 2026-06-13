using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // --- NEW: Required for DOTween ---

public class BalloonSplatUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Image component that will act as our blueprint. It should be disabled in the scene.")]
    public Image splatTemplate; 

    [Header("Animation Settings")]
    public float splatDuration = 2f; 
    public float fadeSpeed = 1.5f;   
    
    [Tooltip("How far from the center of the screen the splat can randomly appear")]
    public float maxOffsetXY = 350f; 
    
    [Tooltip("How randomly stretched the splat can get so they don't all look identical")]
    public float randomScaleJitter = 0.2f;

    private void Start()
    {
        // Ensure our blueprint is hidden so we only see the clones
        if (splatTemplate != null)
        {
            splatTemplate.gameObject.SetActive(false);
        }
    }

    public void ShowSplat(Color splatColor)
    {
        if (splatTemplate == null) return;

        // 1. Create a brand new clone of the template image
        Image newSplat = Instantiate(splatTemplate, splatTemplate.transform.parent);
        newSplat.gameObject.SetActive(true);

        // 2. Set the color (fully opaque to start)
        splatColor.a = 1f;
        newSplat.color = splatColor;

        // 3. Randomize Position, Rotation, and a little bit of Scale
        RectTransform splatRect = newSplat.GetComponent<RectTransform>();
        
        splatRect.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        
        splatRect.anchoredPosition = new Vector2(
            Random.Range(-maxOffsetXY, maxOffsetXY), 
            Random.Range(-maxOffsetXY, maxOffsetXY)
        );

        float baseScale = 1f + Random.Range(-randomScaleJitter, randomScaleJitter);
        
        // Start tiny so we can "pop" it in
        splatRect.localScale = Vector3.zero; 

        // ==========================================
        // 4. DOTWEEN ANIMATION SEQUENCE
        // ==========================================
        Sequence splatSequence = DOTween.Sequence();
        
        // A. Pop in aggressively (OutBack makes it overshoot and bounce back like a real splat)
        splatSequence.Append(splatRect.DOScale(Vector3.one * baseScale, 0.25f).SetEase(Ease.OutBack));
        
        // B. Wait on screen for the duration of the stun
        splatSequence.AppendInterval(splatDuration);
        
        // C. Fade out smoothly
        splatSequence.Append(newSplat.DOFade(0f, fadeSpeed));
        
        // D. Delete the clone object from the hierarchy so memory doesn't leak!
        splatSequence.OnComplete(() => Destroy(newSplat.gameObject));
    }
}