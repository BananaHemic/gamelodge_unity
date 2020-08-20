using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A UI element that can be toggled on and off, and normally
/// has it's own children CanvasToggles that need to be notified in
/// RefreshForMode
/// </summary>
/// <typeparam name="T"></typeparam>
[RequireComponent(typeof(CanvasToggle))]
public abstract class BasePanel<T> : GenericSingleton<T> where T : Component
{
    private CanvasToggle _canvasToggle;
    protected bool _isVisible;
    protected bool _isInteractable;

    protected override void Awake()
    {
        base.Awake();
        _canvasToggle = GetComponent<CanvasToggle>();
    }
    public void SetOn(bool isOn)
    {
        _isVisible = isOn;
        _isInteractable = isOn;
        _canvasToggle.SetOn(isOn);
        RefreshForMode();
    }
    public void SetVisibilityAndInteractability(bool visible, bool interactable)
    {
        _isVisible = visible;
        _isInteractable = interactable;
        _canvasToggle.SetVisibilityAndInteractability(visible, interactable);
        RefreshForMode();
    }
    protected abstract void RefreshForMode();
}
