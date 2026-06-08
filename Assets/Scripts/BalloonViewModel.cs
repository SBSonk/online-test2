using System.Collections;
using UnityEngine;

public class BalloonViewModel : MonoBehaviour
{
    [Header("References")]
    public NetworkBalloonShooter shooter;
    public BalloonVisuals viewmodelVisuals;
    
    [Header("Settings")]
    public float reloadTime = 0.5f;

    private void Start()
    {
        if (shooter != null)
        {
            shooter.OnStartCharge.AddListener(BeginCharge);
            shooter.OnThrow.AddListener(HandleThrow);
        }
        Invoke(nameof(ApplyColorToFakeBalloon), 0.1f);
    }

    private void OnDestroy()
    {
        if (shooter != null)
        {
            shooter.OnStartCharge.RemoveListener(BeginCharge);
            shooter.OnThrow.RemoveListener(HandleThrow);
        }
    }

    private void Update()
    {
        if (shooter == null || viewmodelVisuals == null) return;
        if (shooter.isCharging)
        {
            viewmodelVisuals.simulatedChargeLevel = shooter.GetChargePercentage();
        }
    }

    private void BeginCharge()
    {
        viewmodelVisuals.currentState = BalloonVisuals.BalloonState.Charging;
    }

    private void HandleThrow()
    {
        if (viewmodelVisuals.balloonMesh != null)
        {
            viewmodelVisuals.balloonMesh.gameObject.SetActive(false);
        }
        StartCoroutine(ReloadRoutine());
    }

    private void ApplyColorToFakeBalloon()
    {
        if (shooter.colorManager == null || viewmodelVisuals.balloonMesh == null) return;

        int mainThemeIndex = shooter.colorManager.colorIndex.Value;
        var sets = shooter.colorManager.colorSets;

        if (sets.Length > 0)
        {
            var shades = sets[mainThemeIndex % sets.Length].balloonShades;
            if (shades.Length > 0)
            {
                // NEW: Grab the color and paint the existing material
                Color nextColor = shades[shooter.currentShadeIndex % shades.Length].balloonColor;
                if (viewmodelVisuals.balloonMesh.TryGetComponent(out Renderer rend))
                {
                    rend.material.color = nextColor;
                }
            }
        }
    }

    private IEnumerator ReloadRoutine()
    {
        yield return new WaitForSeconds(reloadTime);
        
        viewmodelVisuals.currentState = BalloonVisuals.BalloonState.Idle;
        viewmodelVisuals.simulatedChargeLevel = 0f;
        
        ApplyColorToFakeBalloon();

        if (viewmodelVisuals.balloonMesh != null)
        {
            Transform meshTransform = viewmodelVisuals.balloonMesh;
            meshTransform.gameObject.SetActive(true);
            
            Vector3 targetScale = meshTransform.localScale;
            meshTransform.localScale = Vector3.zero;
            
            float timer = 0f;
            float scaleDuration = 0.15f;
            
            while (timer < scaleDuration)
            {
                timer += Time.deltaTime;
                meshTransform.localScale = Vector3.Lerp(Vector3.zero, targetScale, timer / scaleDuration);
                yield return null;
            }
            meshTransform.localScale = targetScale;
        }
    }
}