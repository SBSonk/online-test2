using System.Collections;
using UnityEngine;

public class BalloonViewModel : MonoBehaviour
{
    [Header("References")]
    public NetworkBalloonShooter shooter;
    public BalloonVisuals viewmodelVisuals;
    
    [Header("Settings")]
    public float appearDuration = 0.15f;

    private bool wasPreparingThrow = false;
    private Coroutine scaleCoroutine;
    private Vector3 originalScale;

    private void Start()
    {
        if (viewmodelVisuals != null && viewmodelVisuals.balloonMesh != null)
        {
            originalScale = viewmodelVisuals.balloonMesh.localScale;
            viewmodelVisuals.balloonMesh.gameObject.SetActive(false);
        }

        if (shooter != null)
        {
            shooter.OnThrow.AddListener(HandleThrow);
        }
    }

    private void OnDestroy()
    {
        if (shooter != null)
        {
            shooter.OnThrow.RemoveListener(HandleThrow);
        }
    }

    private void Update()
    {
        if (shooter == null || viewmodelVisuals == null) return;

        // --- THE MULTIPLAYER FIX ---
        // Look at the NetworkVariable instead of the local boolean!
        bool isPreparingThrow = shooter.isPreparingThrowNet.Value;

        if (isPreparingThrow && !wasPreparingThrow)
        {
            ShowBalloon();
        }
        else if (!isPreparingThrow && wasPreparingThrow)
        {
            HideBalloon();
        }

        wasPreparingThrow = isPreparingThrow;

        // Visual wobble (only runs accurately for the local player aiming it)
        if (shooter.IsOwner && shooter.isCharging)
        {
            viewmodelVisuals.currentState = BalloonVisuals.BalloonState.Charging;
            viewmodelVisuals.simulatedChargeLevel = shooter.GetChargePercentage();
        }
        else if (!isPreparingThrow)
        {
            viewmodelVisuals.currentState = BalloonVisuals.BalloonState.Idle;
            viewmodelVisuals.simulatedChargeLevel = 0f;
        }
    }

    private void ShowBalloon()
    {
        ApplyColorToFakeBalloon(); 
        
        if (viewmodelVisuals.balloonMesh != null)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleRoutine(Vector3.zero, originalScale, appearDuration));
        }
    }

    private void HideBalloon()
    {
        if (viewmodelVisuals.balloonMesh != null)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            viewmodelVisuals.balloonMesh.gameObject.SetActive(false);
        }
    }

    private void HandleThrow()
    {
        HideBalloon();
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
                // --- MULTIPLAYER FIX ---
                // Pull the synced color index from the network!
                Color nextColor = shades[shooter.syncedShadeIndex.Value % shades.Length].balloonColor;
                if (viewmodelVisuals.balloonMesh.TryGetComponent(out Renderer rend))
                {
                    rend.material.color = nextColor;
                }
            }
        }
    }

    private IEnumerator ScaleRoutine(Vector3 startScale, Vector3 endScale, float duration)
    {
        Transform meshTransform = viewmodelVisuals.balloonMesh;
        meshTransform.gameObject.SetActive(true);
        
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            meshTransform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);
            yield return null;
        }
        meshTransform.localScale = endScale;
    }
}