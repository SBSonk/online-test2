using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public struct BalloonShade
{
    public Color balloonColor;
    [ColorUsage(true, true)]
    public Color particleColor;
}

[System.Serializable]
public struct PlayerColorSet
{
    public string themeName; 
    public Color playerColor; 
    public BalloonShade[] balloonShades; 
}

public class PlayerColorManager : NetworkBehaviour
{
    [Header("Body References")]
    [SerializeField] private Renderer playerRenderer;
    
    [Header("Face References")]
    [Tooltip("Drag your two eye planes here")]
    [SerializeField] private Renderer[] eyeRenderers;
    [Tooltip("Drag your two eyebrow planes here")]
    [SerializeField] private Renderer[] eyebrowRenderers;

    [Header("Color Settings")]
    public PlayerColorSet[] colorSets;
    public NetworkVariable<int> colorIndex = new NetworkVariable<int>();

    [Tooltip("0 = Same color as body, 1 = Pitch Black")]
    [Range(0f, 1f)] public float eyeDarkness = 0.85f;
    [Tooltip("0 = Same color as body, 1 = Pitch Black")]
    [Range(0f, 1f)] public float eyebrowDarkness = 0.65f;

    public override void OnNetworkSpawn()
    {
        colorIndex.OnValueChanged += HandleColorChanged;

        if (IsServer && colorSets.Length > 0)
        {
            colorIndex.Value = (int)(OwnerClientId % (ulong)colorSets.Length);
        }

        ApplyColor(colorIndex.Value);

        // --- NEW: Hide the face planes from your own 1st-person camera ---
        if (IsOwner)
        {
            foreach (Renderer eye in eyeRenderers)
            {
                if (eye != null) eye.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            foreach (Renderer brow in eyebrowRenderers)
            {
                if (brow != null) brow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        colorIndex.OnValueChanged -= HandleColorChanged;
    }

    private void HandleColorChanged(int oldColorIndex, int newColorIndex)
    {
        ApplyColor(newColorIndex);
    }

    private void ApplyColor(int index)
    {
        if (colorSets.Length == 0) return;
        
        int safeIndex = index % colorSets.Length;
        Color mainColor = colorSets[safeIndex].playerColor;
        
        // 1. Apply Main Body Color
        if (playerRenderer != null)
        {
            playerRenderer.material.color = mainColor;
        }

        // 2. Calculate Darker Shades (Mixes the main color with Black)
        Color eyeColor = Color.Lerp(mainColor, Color.black, eyeDarkness);
        Color eyebrowColor = Color.Lerp(mainColor, Color.black, eyebrowDarkness);

        // 3. Apply to Eyes
        foreach (Renderer eye in eyeRenderers)
        {
            if (eye != null) eye.material.color = eyeColor;
        }

        // 4. Apply to Eyebrows
        foreach (Renderer brow in eyebrowRenderers)
        {
            if (brow != null) brow.material.color = eyebrowColor;
        }
    }
}