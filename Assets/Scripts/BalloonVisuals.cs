using UnityEngine;
using NaughtyAttributes;

public class BalloonVisuals : MonoBehaviour
{
    public enum BalloonState { Idle, Charging, InAir, Popped }

    [Header("References")]
    [Tooltip("Assign the child object containing the MeshRenderer here. Do NOT scale the root object with the collider.")]
    public Transform balloonMesh;
    public ParticleSystem popParticles;
    
    // --- NEW: Trail Renderer Reference ---
    public TrailRenderer flightTrail; 

    [Header("State Testing")]
    [OnValueChanged("OnStateChanged")]
    public BalloonState currentState = BalloonState.Idle;

    [Header("Charge Settings (Squash & Wobble)")]
    [Range(0f, 1f)]
    public float simulatedChargeLevel = 0f;
    public float chargeWobbleSpeed = 35f;
    public float maxChargeWobble = 0.15f;

    [Header("Flight Settings (Stretch & Flutter)")]
    [Range(0f, 20f)]
    public float simulatedVelocity = 0f;
    public float stretchMultiplier = 0.05f;
    public float maxStretch = 1.8f;
    [Tooltip("How fast the balloon flutters mid-air.")]
    public float flightWobbleSpeed = 40f;
    [Tooltip("How intense the mid-air flutter is.")]
    public float flightWobbleAmount = 0.08f;

    private Vector3 originalScale;

    void Start()
    {
        if (balloonMesh != null)
        {
            originalScale = balloonMesh.localScale;
        }
        
        // Ensure the trail starts off
        if (flightTrail != null) flightTrail.emitting = false;
    }

    void Update()
    {
        if (balloonMesh == null) return;

        // --- NEW: Automatically toggle the trail based on the current state ---
        if (flightTrail != null)
        {
            flightTrail.emitting = (currentState == BalloonState.InAir);
        }

        switch (currentState)
        {
            case BalloonState.Idle:
                // Smoothly return to normal scale if we stop charging
                balloonMesh.localScale = Vector3.Lerp(balloonMesh.localScale, originalScale, Time.deltaTime * 10f);
                break;

            case BalloonState.Charging:
                ApplyChargeWobble();
                break;

            case BalloonState.InAir:
                ApplyVelocityStretchAndWobble();
                break;

            case BalloonState.Popped:
                break;
        }
    }

    // --- NEW: Call this from your BalloonProjectile script when it receives its network color! ---
    public void ApplyColor(Color mainColor)
    {
        // --- THE MISSING PIECE: Actually color the Balloon Mesh! ---
        if (balloonMesh != null && balloonMesh.TryGetComponent(out Renderer renderer))
        {
            renderer.material.color = mainColor;
            
            // URP/HDRP support
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", mainColor);
                
            // HDR Emission Glow support
            if (renderer.material.HasProperty("_EmissionColor"))
                renderer.material.SetColor("_EmissionColor", mainColor);
        }

        // 1. Color the Trail
        if (flightTrail != null)
        {
            flightTrail.startColor = mainColor;
            flightTrail.endColor = new Color(mainColor.r, mainColor.g, mainColor.b, 0f); 
        }

        // 2. Color the Pop Particles
        if (popParticles != null)
        {
            var main = popParticles.main;
            main.startColor = mainColor;
        }
    }

    private void ApplyChargeWobble()
    {
        // Rapid sine wave that gets more intense as the charge level increases
        float currentWobble = Mathf.Sin(Time.time * chargeWobbleSpeed) * maxChargeWobble * simulatedChargeLevel;
        
        // Push down (squash) slightly as the pressure builds
        float squash = 1f - (simulatedChargeLevel * 0.15f); 
        
        balloonMesh.localScale = new Vector3(
            originalScale.x * (squash + currentWobble),
            originalScale.y * squash,
            originalScale.z * (squash - currentWobble)
        );
    }

    private void ApplyVelocityStretchAndWobble()
    {
        // 1. Base Stretch & Squash (Velocity based)
        float stretchAmount = 1f + Mathf.Clamp(simulatedVelocity * stretchMultiplier, 0f, maxStretch - 1f);
        float squashAmount = 1f / Mathf.Sqrt(stretchAmount); 

        // 2. Mid-Air Flutter (Wind resistance instability)
        // We only apply this wobble if the balloon is actually moving
        float flightFlutter = 0f;
        if (simulatedVelocity > 0.1f)
        {
            flightFlutter = Mathf.Sin(Time.time * flightWobbleSpeed) * flightWobbleAmount;
        }

        // We add the flutter to X and subtract it from Y so it constantly shifts its volume slightly
        Vector3 targetScale = new Vector3(
            originalScale.x * (squashAmount + flightFlutter),
            originalScale.y * (squashAmount - flightFlutter),
            originalScale.z * stretchAmount
        );

        balloonMesh.localScale = Vector3.Lerp(balloonMesh.localScale, targetScale, Time.deltaTime * 15f);
    }

    // --- NaughtyAttributes Testing ---

    [Button("Trigger Pop!", EButtonEnableMode.Playmode)]
    public void TestPop()
    {
        currentState = BalloonState.Popped;
        
        if (balloonMesh != null)
        {
            balloonMesh.gameObject.SetActive(false);
        }

        if (popParticles != null)
        {
            popParticles.transform.position = transform.position;
            popParticles.Play();
        }
    }

    [Button("Reset Balloon", EButtonEnableMode.Playmode)]
    public void ResetBalloon()
    {
        currentState = BalloonState.Idle;
        
        if (balloonMesh != null)
        {
            balloonMesh.gameObject.SetActive(true);
            balloonMesh.localScale = originalScale;
        }

        if (flightTrail != null) flightTrail.Clear(); // Clears the old trail lines instantly
    }

    private void OnStateChanged()
    {
        if (currentState != BalloonState.Popped && balloonMesh != null && !balloonMesh.gameObject.activeSelf)
        {
            balloonMesh.gameObject.SetActive(true);
            balloonMesh.localScale = originalScale;
        }
    }
}