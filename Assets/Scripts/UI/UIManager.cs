using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : GenericSingleton<UIManager>
{
    public Canvas MainCanvas;
    public Canvas BuildModeCanvas;
    public Canvas PlayModeCanvas;
    public RectTransform ContainingRect;
    public CanvasToggle MainCanvasToggle;
    public CanvasGroup MainCanvasGroup;
    public GameObject VRInputModule;
    public GameObject DesktopInputModule;
    public VRInputModule VRInputModuleScript;
    public EventSystem VRInputModuleEventSystem;
    public GameObject DebugConsole;
    public VRLaserPointer LaserPointer;
    public Canvas[] CollidingCanvases;
    public Vector3 Offset;
    public Vector3 Rot = new Vector3(0, -25f, 0);
    public Sprite MaximizeUIIcon;
    public Sprite MinimizeUIIcon;
    public Image MaximizeMinimizeImage;
    public float RotateSpeedMax = 50f;
    public float OpenVRMenuTime = 0.15f;
    public float MaxMenuPosDiff = 0.1f;
    public float MaxMenuRotDiff = 2f;
    public float PercentScreenXOnMaximize = 0.3f;
    /// <summary>
    /// This should be left off. It determines
    /// whether we parent the hand to a controller, or if we
    /// just move it every frame in world space. Moving in world
    /// has better perf, so we leave it as that.
    /// </summary>
    public bool VRParentUI = false;

    public ControllerAbstraction CursorController { get; private set; }
    public MenuState CurrentMenuState { get; private set; }
    public bool IsUIMaximized { get; private set; }

    private RectTransform _screenTransform;
    private Vector2 _screenSpaceSize;
    private Vector3 _screenSpacePos;
    private Coroutine _vrScreenOpenRoutine;
    private SceneDraggable _draggedUIElement;
    private Transform _uiController;
    private RectTransform[] _collidingRects;
    private Vector2 _initialContainerPosition;
    private Vector2 _initialContainerSize;
    private readonly Vector3[] _workingWorldCorners = new Vector3[4];

    public enum MenuState
    {
        Off,
        TurningOn,
        On,
        DraggingUIElement, // Used for click dragging a UI element from a folder onto the scene
    }

    protected override void Awake()
    {
        base.Awake();
        _screenTransform = MainCanvas.GetComponent<RectTransform>();
        _screenSpaceSize = _screenTransform.sizeDelta;
        _screenSpacePos = _screenTransform.position;
        _collidingRects = new RectTransform[CollidingCanvases.Length];
        for (int i = 0; i < CollidingCanvases.Length; i++)
            _collidingRects[i] = CollidingCanvases[i].transform as RectTransform;
        _initialContainerPosition = ContainingRect.anchoredPosition;
        _initialContainerSize = ContainingRect.sizeDelta;
        VRSDKUtils.OnVRModeChanged += RefreshCameraMode;
        RefreshCameraMode();
        ControllerAbstraction.OnControllerPoseUpdate_General += OnPoseUpdate;
    }
    public void ToggleUIMaximizeMinimize()
    {
        // Don't maximize in VR
        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop
            && !IsUIMaximized)
            return;
        IsUIMaximized = !IsUIMaximized;
        RectTransform t = ContainingRect;
        MaximizeMinimizeImage.sprite = IsUIMaximized ? MinimizeUIIcon : MaximizeUIIcon;
        // Configure the containing rect accordingly
        if (IsUIMaximized)
        {
            ContainingRect.anchorMax = Vector2.one;
            Vector2 mainCanvasSize = _screenTransform.sizeDelta;
            //Debug.Log("Main canvas size: " + mainCanvasSize.ToPrettyString());
            Vector2 size = new Vector2((1f - PercentScreenXOnMaximize) * mainCanvasSize.x, 0);
            ContainingRect.sizeDelta = -size;
            ContainingRect.anchoredPosition = -size / 2f;
        }
        else
        {
            ContainingRect.anchorMax = Vector2.zero;
            ContainingRect.anchoredPosition = _initialContainerPosition;
            ContainingRect.sizeDelta = _initialContainerSize;
        }
    }
    private void RefreshCameraMode()
    {
        //Debug.Log("Refreshing UI mode sdk currently: " + VRSDKUtils.Instance.CurrentSDK);
        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            MainCanvas.renderMode = RenderMode.WorldSpace;
            MainCanvas.worldCamera = Orchestrator.Instance.MainCamera;
            CursorController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND];
            //_uiController = ControllerAbstraction.Instances[(int)ControllerAbstraction.OppositeHandFrom(CursorController.MyControllerType)];
            _uiController = HandPoser.Instances[(int)ControllerAbstraction.OppositeHandFrom(CursorController.MyControllerType)].WristAttach;
            //MainCanvas.transform.localPosition = Orchestrator.Instance.MainCamera.transform.position + 4 * Orchestrator.Instance.MainCamera.transform.forward;

            var scale = Orchestrator.Instance.MainCamera.transform.lossyScale / 100f;
            //Debug.Log("Setting scale to " + scale);
            VRInputModule.SetActive(true);
            VRInputModuleScript.enabled = true;
            DesktopInputModule.SetActive(false);
            MainCanvasToggle.SetOn(false);
            //VRInputModuleScript.rayTransform = CursorController.GetTransform();
            VRInputModuleScript.rayTransform = HandPoser.Instances[(int)CursorController.MyControllerType].IndexAttach;
            if (DebugConsole != null)
                DebugConsole.SetActive(false);
            // Force un-maximize on VR
            if (IsUIMaximized)
                ToggleUIMaximizeMinimize();
        }
        else
        {
            CursorController = ControllerAbstraction.Instances[0];
            if(VRParentUI)
                MainCanvas.transform.SetParent(null);
            MainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            MainCanvas.worldCamera = Orchestrator.Instance.MainCamera;
            _screenTransform.sizeDelta = _screenSpaceSize;
            _screenTransform.position = _screenSpacePos;
            VRInputModule.SetActive(false);
            DesktopInputModule.SetActive(true);
            MainCanvasToggle.SetOn(true);
            if (DebugConsole != null)
                DebugConsole.SetActive(true);
            CurrentMenuState = MenuState.Off;
        }
    }
    private void OpenVRMenu(ControllerAbstraction.ControllerType controllerType)
    {
        if (!VRParentUI || CurrentMenuState == MenuState.Off)
            MoveUIToHand();

        if (CurrentMenuState == MenuState.TurningOn
            || CurrentMenuState == MenuState.On)
            return;

        if (CurrentMenuState == MenuState.DraggingUIElement
            && MainCanvasToggle.IsVisible)
            return;

        if (_vrScreenOpenRoutine != null)
            StopCoroutine(_vrScreenOpenRoutine);
        _vrScreenOpenRoutine = StartCoroutine(OpenVRMenuRoutine());
    }
    private void MoveUIToHand()
    {
        if(VRParentUI)
            MainCanvas.transform.SetParent(_uiController, false);
        RectTransform mainRect = (MainCanvas.transform as RectTransform);
        RectTransform innerRect = ContainingRect;
        //Support multiple resolutions / aspect ratios.
        // We can't simply have the same position offset all the time because the
        // rect changes size based on the size of the screen or window it's on.
        // So we have to set the offset taking into consideration the relative
        // size of the rect.
        const float uiScale = 1f / 1000f;
        Vector3 scaledOffset = new Vector3(
            mainRect.sizeDelta.x * uiScale / 2 - Offset.x * innerRect.sizeDelta.x * uiScale,
            mainRect.sizeDelta.y * uiScale / 2 + Offset.y * innerRect.sizeDelta.y * uiScale,
            Offset.z
            );
        // We also need to account for the rotation moving the center of the transform.
        // this additional Vector is the delta between where the center was with an angle of 0
        // and where it is now that the angle is non-0
        scaledOffset -= new Vector3(
            scaledOffset.x - scaledOffset.x * Mathf.Cos(Mathf.Deg2Rad * Rot.y),
            0,
            scaledOffset.x * Mathf.Sin(Mathf.Deg2Rad * Rot.y)
            );

        // If we're not being parented, get the world position
        if (!VRParentUI)
        {
            MainCanvas.transform.localPosition = _uiController.transform.TransformPoint(scaledOffset);
            MainCanvas.transform.localRotation = _uiController.transform.rotation * Quaternion.Euler(Rot);
            Vector3 scale = ControllerAbstraction.Instances[(int)ControllerAbstraction.OppositeHandFrom(CursorController.MyControllerType)].GetTransform().lossyScale;
            MainCanvas.transform.localScale = scale * uiScale;
            //MainCanvas.transform.localScale = _uiController.transform.lossyScale * uiScale;
        }
        else
        {
            MainCanvas.transform.localPosition = scaledOffset;
            MainCanvas.transform.localRotation = Quaternion.Euler(Rot);
            MainCanvas.transform.localScale = new Vector3(uiScale, uiScale, uiScale);
        }
    }
    private IEnumerator OpenVRMenuRoutine()
    {
        VRInputModuleEventSystem.enabled = true;
        LaserPointer.laserBeamBehavior = VRLaserPointer.LaserBeamBehavior.On;
        if(CurrentMenuState != MenuState.DraggingUIElement)
            CurrentMenuState = MenuState.TurningOn;
        MainCanvasToggle.SetVisibilityAndInteractability(true, false);
        float startTime = Time.unscaledTime;
        float endTime = startTime + OpenVRMenuTime;
        while(Time.unscaledTime <= endTime)
        {
            float alpha = (Time.unscaledTime - startTime) / (OpenVRMenuTime);
            MainCanvasGroup.alpha = alpha;
            //Debug.Log("Turning on, alpha " + alpha);
            yield return null;
        }

        MainCanvasGroup.alpha = 1f;
        MainCanvasToggle.SetOn(true);
        _vrScreenOpenRoutine = null;
        //VRInputModuleScript.enabled = true;
        if(CurrentMenuState != MenuState.DraggingUIElement)
            CurrentMenuState = MenuState.On;
    }
    private void TurnOffMenu()
    {
        if(CurrentMenuState == MenuState.DraggingUIElement)
        {
            // We just hide the canvas
            MainCanvasToggle.SetVisibilityAndInteractability(false, true);
            return;
        }
        VRInputModuleEventSystem.enabled = false;
        if(VRParentUI)
            MainCanvas.transform.SetParent(null, false);
        if(CurrentMenuState != MenuState.Off)
        {
            MainCanvasToggle.SetOn(false);
            CurrentMenuState = MenuState.Off;
        }
        if (_vrScreenOpenRoutine != null)
            StopCoroutine(_vrScreenOpenRoutine);
        _vrScreenOpenRoutine = null;
        LaserPointer.laserBeamBehavior = VRLaserPointer.LaserBeamBehavior.Off;
        //VRInputModuleScript.enabled = false;
        //Debug.Log("Turning off menu");
    }
    public Ray GetCursorRay()
    {
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
            return Orchestrator.Instance.MainCamera.ScreenPointToRay(Input.mousePosition);
        //Vector3 controllerPos;
        //Quaternion controllerRot;
        //CursorController.GetPositionAndRotation(out controllerPos, out controllerRot);
        //return new Ray(controllerPos, controllerRot * Vector3.forward);
        Transform t = HandPoser.Instances[(int)CursorController.MyControllerType].IndexAttach;
        return new Ray(t.position, t.forward);
    }
    bool IsPointInsideRectTransform(Vector2 point, RectTransform rt)
    {
        // Get the rectangular bounding box of your UI element
        // Starts bottom left, going clockwise
        rt.GetWorldCorners(_workingWorldCorners);
        float leftSide = _workingWorldCorners[0].x;
        float bottomSide = _workingWorldCorners[0].y;
        float rightSide = _workingWorldCorners[2].x;
        float topSide = _workingWorldCorners[2].x;
        // Check to see if the point is in the calculated bounds
        if (point.x >= leftSide &&
            point.x <= rightSide &&
            point.y >= bottomSide &&
            point.y <= topSide)
        {
            return true;
        }
        return false;
    }
    public bool IsCursorHoveringUI(Ray cursorRay, out Vector3 cursorPosWorld)
    {
        if (!MainCanvasToggle.IsVisible)
        {
            cursorPosWorld = Vector3.zero;
            return false;
        }
        if(VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
        {
            cursorPosWorld = Input.mousePosition;
            for(int i = 0; i < CollidingCanvases.Length;i++)
            {
                RectTransform rec = _collidingRects[i];
                if (CollidingCanvases[i].enabled
                    &&  IsPointInsideRectTransform((Vector2)cursorPosWorld, rec))
                    return true;
            }
        }
        else
        {
            for(int i = 0; i < CollidingCanvases.Length;i++)
            {
                RectTransform rec = _collidingRects[i];
                if (CollidingCanvases[i].enabled
                    && VRRaycaster.RayIntersectsRectTransform(rec, cursorRay, out cursorPosWorld))
                    return true;
            }
        }
        cursorPosWorld = Vector3.zero;
        return false;
    }
    public void BeginDragUIElement(SceneDraggable draggable)
    {
        CurrentMenuState = MenuState.DraggingUIElement;
        _draggedUIElement = draggable;
        VRInputModuleScript.SetUseLaser(false);
    }
    public void EndDragUIElement(SceneDraggable draggable)
    {
        if(CurrentMenuState != MenuState.DraggingUIElement)
        {
            Debug.LogWarning("Drag UI end when not in that menu state! in " + CurrentMenuState);
            return;
        }
        if(_draggedUIElement != draggable)
        {
            Debug.LogError("End drag with wrong UI element!");
            return;
        }
        CurrentMenuState = MenuState.On;
        _draggedUIElement = null;
        VRInputModuleScript.SetUseLaser(true);
    }
    private void HandleDragUI()
    {
        Ray cursorRay = GetCursorRay();
        Vector3 cursorPosWorld;

        if (IsCursorHoveringUI(cursorRay, out cursorPosWorld))
        {
            //Debug.Log("Cursor on UI " + cursorPosWorld);
            // Have the draggable draw itself on the UI
            _draggedUIElement.DrawDragOnUI(cursorPosWorld);
        }
        else
        {
            // Tell the draggable if the user is pointing towards
            // the table/terrain, or just the sky
            if (!Physics.Raycast(cursorRay, out RaycastHit hit, 1000f, GLLayers.AllCanDragOnto_Build))
            {
                // Did not hit table/terrain
                // Draw where mouse is, or where user is pointing
                cursorPosWorld = VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
                    ? Input.mousePosition
                    : cursorRay.direction + cursorRay.origin;
                Quaternion rot = VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
                    ? Quaternion.identity
                    : Quaternion.LookRotation(cursorRay.direction);
                //Debug.Log("Cursor on skybox " + cursorPosWorld);
                _draggedUIElement.DrawDragOnSkybox(cursorPosWorld, rot);
            }
            else
            {
                // Draw on the table
                cursorPosWorld = hit.point;
                //Debug.Log("Cursor on table " + cursorPosWorld);
                _draggedUIElement.DrawDragOnTable(cursorPosWorld);
            }
        }
        //Debug.Log("Drawing cursor, frame #" + Time.frameCount);
        LaserPointer.SetCursorStartDest(cursorRay.origin, cursorPosWorld);
    }
    void OnPoseUpdate()
    {
        //Debug.Log("Is L menu open clicked: " + ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetOpenMenuButton());
        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            //OpenVRMenu(ControllerAbstraction.ControllerType.LEFTHAND);
            //return;
            if (ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetOpenMenuButton())
                OpenVRMenu(ControllerAbstraction.ControllerType.LEFTHAND);
            else if (ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetOpenMenuButton())
                OpenVRMenu(ControllerAbstraction.ControllerType.RIGHTHAND);
            else
                TurnOffMenu();
        }

        if (CurrentMenuState == MenuState.DraggingUIElement)
        {
            HandleDragUI();
            if (Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode
                && VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
                VRBuildControls.Instance.BuildSelectOff();
        }
        else
        {
            if (Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode
                && VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
                VRBuildControls.Instance.HandleBuildSelect();
        }
    }
}
