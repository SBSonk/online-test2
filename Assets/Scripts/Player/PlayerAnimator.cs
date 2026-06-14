using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("The NetworkAnimator attached to The_Hands inside your CameraRoot")]
    public NetworkAnimator handNetworkAnimator; 
    
    [Tooltip("The renderer for your capsule so we can hide it from your own camera")]
    public MeshRenderer capsuleRenderer; 

    public override void OnNetworkSpawn()
    {
        if (IsOwner && capsuleRenderer != null)
        {
            // Hide the capsule from your own camera so it doesn't block your view!
            capsuleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    // ==========================================
    // SIMPLE STRING CALLS
    // ==========================================

    public void SetWalking(bool isWalking)
    {
        if (!IsOwner || handNetworkAnimator == null) return;
        handNetworkAnimator.Animator.SetBool("IsWalking", isWalking);
    }

    public void SetCharging(bool isCharging)
    {
        if (!IsOwner || handNetworkAnimator == null) return;
        handNetworkAnimator.Animator.SetBool("IsCharging", isCharging);
    }

    public void TriggerThrow()
    {
        if (!IsOwner || handNetworkAnimator == null) return;
        
        // CRITICAL: Always call SetTrigger on the NetworkAnimator, NOT the base Animator!
        handNetworkAnimator.SetTrigger("Throw"); 
    }

    public void TriggerPress()
    {
        if (!IsOwner || handNetworkAnimator == null) return;
        
        handNetworkAnimator.SetTrigger("Press");
    }
}