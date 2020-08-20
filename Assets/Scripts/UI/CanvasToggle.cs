using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
[RequireComponent(typeof(VRRaycaster))]
public class CanvasToggle : MonoBehaviour
{
    private Canvas _canvas;
    private GraphicRaycaster _graphicRaycaster;
    private VRRaycaster _vrRaycaster;

    public bool IsVisible { get; private set; }
    public bool IsInteractable { get; private set; }
    private bool _hasInit;
    private bool _hasSubscribed = false;

    void Start()
    {
        if (!_hasInit)
            Init();
        VRSDKUtils.OnVRModeChanged += Refresh;
        _hasSubscribed = true;
    }
    private void Init()
    {
        _hasInit = true;
        _canvas = GetComponent<Canvas>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
        _vrRaycaster = GetComponent<VRRaycaster>();

        if (_canvas == null)
            Debug.LogError("No canvas! ", this);
        if (_graphicRaycaster == null)
            Debug.LogError("No graphic raycaster! ", this);
        if (_vrRaycaster == null)
            Debug.LogError("No VR raycaster! ", this);
    }
    private void Refresh()
    {
        if (!_hasInit)
            Init();
        if(_canvas == null)
        {
            Debug.LogError("Canvas toggle has no canvas! " + name, this);
            return;
        }
        _canvas.enabled = IsVisible;
        _graphicRaycaster.enabled = IsInteractable && VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop;
        _vrRaycaster.enabled = IsInteractable && VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop;
    }
    public void SetOn(bool on)
    {
        IsVisible = on;
        IsInteractable = on;
        Refresh();
    }
    public void SetVisibilityAndInteractability(bool visible, bool interactable)
    {
        IsVisible = visible;
        IsInteractable = interactable;
        Refresh();
    }
    private void OnDestroy()
    {
        if (_hasSubscribed)
        {
            VRSDKUtils.OnVRModeChanged -= Refresh;
            _hasSubscribed = false;
        }
    }
}
