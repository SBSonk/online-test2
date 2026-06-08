using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Outline))]
public class Interactable : MonoBehaviour
{
    public UnityEvent OnHoverBegin, OnHoverUpdate, OnHoverEnd;
    public UnityEvent OnInteract;
    private Outline outline;
    private bool isHovered = false;

    private void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;
        OnInitialize();
    }

    public void BeginHover()
    {
        if (isHovered) return;
        isHovered = true;

        outline.enabled = true;
        
        HoverStart();
        OnHoverBegin?.Invoke();
    }

    public void UpdateHover()
    {
        if (!isHovered) return;

        HoverUpdate();
        OnHoverUpdate?.Invoke();
    }

    public void EndHover()
    {
        if (!isHovered) return;
        isHovered = false;

        outline.enabled = false;

        HoverEnd();
        OnHoverEnd?.Invoke();
    }

    public virtual void OnInitialize() {}
    public virtual void HoverStart() {}
    public virtual void HoverEnd() {}
    public virtual void HoverUpdate() {}
    public virtual void Interact(GameObject player) 
    {
        OnInteract?.Invoke(); // NEW: This tells the Unity Inspector to fire its events!
    }
}