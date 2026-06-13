using UnityEngine;

public class ProceduralCameraShaker : MonoBehaviour
{
    [Header("Trauma Settings")]
    [Tooltip("How fast a burst shake fades away.")]
    public float traumaDecay = 1.5f;
    [Tooltip("How erratic the shake is.")]
    public float noiseSpeed = 20f;

    [Header("Intensity Settings")]
    public Vector3 maxPositionalShake = new Vector3(0.2f, 0.2f, 0.1f);
    public Vector3 maxRotationalShake = new Vector3(10f, 10f, 5f);

    private float _trauma;
    private float _baseTrauma; // Used for continuous walking
    private float _seed;

    void Awake()
    {
        // Give each player a unique noise seed
        _seed = Random.value; 
    }

    // Call this for one-off impacts (Throwing, Getting Hit)
    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }

    // Call this to set a continuous rumble (Walking)
    public void SetBaseTrauma(float amount)
    {
        _baseTrauma = Mathf.Clamp01(amount);
    }

    void Update()
    {
        // Decay trauma, but never let it fall below the base trauma floor
        _trauma -= Time.deltaTime * traumaDecay;
        _trauma = Mathf.Clamp(_trauma, _baseTrauma, 1f);

        if (_trauma <= 0.01f)
        {
            // Smoothly return to dead center when standing entirely still
            transform.localPosition = Vector3.Lerp(transform.localPosition, Vector3.zero, Time.deltaTime * 10f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
            return;
        }

        // Squaring the trauma makes the shake fade out smoothly but start violently
        float shakeStrength = _trauma * _trauma;

        // Calculate noise
        float offsetX = maxPositionalShake.x * shakeStrength * GetNoise(_seed + 0);
        float offsetY = maxPositionalShake.y * shakeStrength * GetNoise(_seed + 1);
        float offsetZ = maxPositionalShake.z * shakeStrength * GetNoise(_seed + 2);

        float rotX = maxRotationalShake.x * shakeStrength * GetNoise(_seed + 3);
        float rotY = maxRotationalShake.y * shakeStrength * GetNoise(_seed + 4);
        float rotZ = maxRotationalShake.z * shakeStrength * GetNoise(_seed + 5);

        // Apply to the container
        transform.localPosition = new Vector3(offsetX, offsetY, offsetZ);
        transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);
    }

    private float GetNoise(float seedOffset)
    {
        // Returns a value between -1 and 1
        return (Mathf.PerlinNoise(seedOffset, Time.time * noiseSpeed) * 2f) - 1f;
    }
}