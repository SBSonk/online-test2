using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkHandsAnimator : NetworkBehaviour
{
    public NetworkAnimator networkAnimator;

    public void SetWalking(bool state)
    {
        if (!IsOwner || networkAnimator == null) return;
        networkAnimator.Animator.SetBool("IsWalking", state);
    }

    public void SetHovering(bool state)
    {
        if (!IsOwner || networkAnimator == null) return;
        networkAnimator.Animator.SetBool("IsHovering", state);
    }

    public void SetThrowWindup(bool state)
    {
        if (!IsOwner || networkAnimator == null) return;
        networkAnimator.Animator.SetBool("IsWindingUp", state);
    }

    public void TriggerThrowRelease()
    {
        if (!IsOwner || networkAnimator == null) return;
        networkAnimator.SetTrigger("Release");
    }

    public void TriggerPress()
    {
        if (!IsOwner || networkAnimator == null) return;
        networkAnimator.SetTrigger("Press");
    }
}