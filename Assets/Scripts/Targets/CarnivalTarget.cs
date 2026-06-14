using Unity.Netcode;
using UnityEngine;
using DG.Tweening; // --- NEW: Added so we can animate the popup here! ---

public abstract class CarnivalTarget : NetworkHasHealth
{
    public enum TargetSize { Small, Regular, Large, XL }
    public enum TargetCategory { Standard, Golden, Bomb, Powerup }

    [Header("Score Popups")]
    public TargetCategory targetCategory = TargetCategory.Standard;
    public DamagePopup scorePopupPrefab; 
    public Color standardPopupColor = Color.white;
    public Color goldenPopupColor = new Color(1f, 0.8f, 0f); // Bright Gold/Yellow
    public Color bombPopupColor = new Color(1f, 0.2f, 0.2f); // Red
    public Color powerupPopupColor = new Color(0f, 1f, 1f); // Cyan

    [Header("Size Randomization")]
    [Tooltip("The server will pick a random size from this list when spawning. (e.g., Only put Small/Regular for Golden Targets)")]
    public TargetSize[] allowedSizes = { TargetSize.Small, TargetSize.Regular, TargetSize.Large, TargetSize.XL };

    [Header("Rope Settings")]
    public LineRenderer ropeRenderer;
    public LayerMask wallLayer;
    public float maxRopeDistance = 5f;

    [Header("Base Settings")]
    public int baseScoreValue = 10; 
    public float despawnDelayAfterDeath = 2f; 

    [Header("Networked State")]
    public NetworkVariable<TargetSize> targetSize = new NetworkVariable<TargetSize>(TargetSize.Regular);
    public NetworkVariable<ulong> targetOwnerClientId = new NetworkVariable<ulong>(999);
    public NetworkVariable<Color> targetColor = new NetworkVariable<Color>(Color.white);

    [Header("Visuals & Physics")]
    public Renderer targetRenderer; 
    private Collider targetCollider;
    private TargetMovement movementScript;
    
    private bool isDead = false; 
    private Vector3 originalScale;

    private void Awake()
    {
        targetCollider = GetComponent<Collider>();
        movementScript = GetComponent<TargetMovement>();
        originalScale = transform.localScale; 
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isDead = false;
        if (targetCollider != null) targetCollider.enabled = true;
        if (movementScript != null) movementScript.enabled = true;

        // --- THE FIX: Targets now roll their own random sizes on the Server! ---
        if (IsServer && allowedSizes != null && allowedSizes.Length > 0)
        {
            targetSize.Value = allowedSizes[Random.Range(0, allowedSizes.Length)];
        }

        targetColor.OnValueChanged += (oldColor, newColor) => UpdateVisuals(newColor);
        targetSize.OnValueChanged += (oldSize, newSize) => ApplySizeVisuals(newSize);
        
        UpdateVisuals(targetColor.Value);
        ApplySizeVisuals(targetSize.Value);

        if (ropeRenderer != null) ropeRenderer.enabled = true;
    }

    private void UpdateVisuals(Color color) { if (targetRenderer != null) targetRenderer.material.color = color; }

    private void Update()
    {
        if (!isDead && ropeRenderer != null) UpdateRope();
    }

    private void UpdateRope()
    {
        ropeRenderer.useWorldSpace = true;
        Ray ray = new Ray(transform.position, Vector3.up);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRopeDistance, wallLayer))
        {
            ropeRenderer.positionCount = 2;
            ropeRenderer.SetPosition(0, transform.position); 
            ropeRenderer.SetPosition(1, hit.point);          
        }
        else ropeRenderer.positionCount = 0;
    }

    private void ApplySizeVisuals(TargetSize size)
    {
        float scaleMultiplier = size switch
        {
            TargetSize.Small => 0.6f,
            TargetSize.Regular => 1.0f,
            TargetSize.Large => 1.5f,
            TargetSize.XL => 2.2f,
            _ => 1.0f
        };
        transform.localScale = originalScale * scaleMultiplier;
    }

    protected override void HandleDeath()
    {
        if (isDead) return; 
        base.HandleDeath(); 
        if (!IsServer) return;
        
        isDead = true;

        ulong shooterClientId = lastAttackerId;
        int pointsAwarded = 0;

        if (baseScoreValue != 0) 
        {
            pointsAwarded = ApplyScore(shooterClientId);
        }

        ApplySpecialEffect(shooterClientId);

        TriggerFallSequenceRpc();
        ShowScorePopupRpc(pointsAwarded, targetCategory);
        
        Invoke(nameof(DelayedDespawn), despawnDelayAfterDeath);
    }

    [Rpc(SendTo.Everyone)]
    private void TriggerFallSequenceRpc()
    {
        if (ropeRenderer != null) ropeRenderer.enabled = false;
        if (movementScript != null) movementScript.enabled = false;
        if (targetCollider != null) targetCollider.enabled = false;

        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
    }

    [Rpc(SendTo.Everyone)]
    private void ShowScorePopupRpc(int pointsAwarded, TargetCategory category)
    {
        if (scorePopupPrefab == null) return;

        Vector3 spawnPos = transform.position + (Vector3.up * 0.5f); 

        Color textColor = standardPopupColor;
        switch (category)
        {
            case TargetCategory.Golden: textColor = goldenPopupColor; break;
            case TargetCategory.Bomb: textColor = bombPopupColor; break;
            case TargetCategory.Powerup: textColor = powerupPopupColor; break;
        }

        DamagePopup popup = Instantiate(scorePopupPrefab, spawnPos, Quaternion.identity);
        popup.InitializeScore(pointsAwarded, textColor);

        // --- THE FIX: We animate it and destroy it directly here! ---
        popup.transform.localScale = Vector3.zero;
        popup.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        popup.transform.DOMoveY(popup.transform.position.y + 1.5f, 1.2f).SetEase(Ease.OutQuad);
        
        // Destroys the popup after 1.2 seconds so it doesn't clutter your game
        Destroy(popup.gameObject, 1.2f); 
    }

    private void DelayedDespawn() { if (IsSpawned) GetComponent<NetworkObject>().Despawn(true); }

    // ==========================================
    // SCORE LOGIC
    // ==========================================
    
    private int ApplyScore(ulong shooterClientId)
    {
        float scoreModifier = targetSize.Value switch
        {
            TargetSize.Small => 2.0f,
            TargetSize.Regular => 1.0f,
            TargetSize.Large => 0.8f,
            TargetSize.XL => 0.5f,
            _ => 1.0f
        };

        int finalScore = Mathf.RoundToInt(baseScoreValue * scoreModifier);
        
        if (baseScoreValue > 0 && finalScore < 1) finalScore = 1;
        if (baseScoreValue < 0 && finalScore > -1) finalScore = -1;

        int pointsToAward = finalScore;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out NetworkClient client))
        {
            if (client.PlayerObject.TryGetComponent(out NetworkBalloonShooter shooter))
            {
                if (shooter.doublePointsTimer > 0) pointsToAward *= 2; 
            }

            if (client.PlayerObject.TryGetComponent(out NetworkPlayerScore scoreSystem))
            {
                scoreSystem.AddPoints(pointsToAward);
            }
        }

        return pointsToAward; 
    }

    protected virtual void ApplySpecialEffect(ulong shooterClientId) { }
}