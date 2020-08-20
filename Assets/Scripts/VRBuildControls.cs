using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRBuildControls : GenericSingleton<VRBuildControls>
{
    public float NonHitLaserDistance = 2f;
    public float MaxScale = 1000f;
    public float MinScale = 0.001f;
    public float FrictionFactor = 0.001f;
    public float MinSlideVelocitySqr = 0.00001f;
    public float SlideYMaxFactor = 4f;
    public float ThumbstickMoveSpeed = 4f;
    public float MinGrabTime = 0.1f;
    public bool SupportGrab = false;

    Vector3 _grabUserPosL, _grabUserPosR;
    float _userScale;
    Vector3 _grabPosL, _grabPosR;
    bool _wasGrabbingL, _wasGrabbingR;
    Vector3 _slideVel;
    bool _isSliding;

    const int NumPosSamples = 5;
    private readonly PositionQueue _positionQueue = new PositionQueue(NumPosSamples);
    private SceneObject _selectedObject;
    private SceneObject _hoveredObject;
    private bool _holdingRotateLock = false;
    private float _grabStartTimeL;
    private float _grabStartTimeR;

    protected override void Awake()
    {
        base.Awake();
        Orchestrator.OnModeChange += OnSceneModeChange;
        ControllerAbstraction.OnControllerPoseUpdate_User += OnPoseUpdate;
        VRSDKUtils.OnVRModeChanged += OnVRModeChange;
    }
    void OnSceneModeChange(Orchestrator.Modes mode)
    {
        if (mode != Orchestrator.Modes.BuildMode)
            BuildSelectOff();
    }
    void OnVRModeChange()
    {
        // If we're now in desktop, un-select stuff
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
            BuildSelectOff();
        else
        {
            // Reset our orientation if entering VR mode
            transform.localRotation = Quaternion.identity;
        }
    }
    void OnPoseUpdate()
    {
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
            return;
        if (Orchestrator.Instance.CurrentMode != Orchestrator.Modes.BuildMode)
            return;

        BuildMove();
    }
    public void BuildSelectOff()
    {
        if(_hoveredObject != null)
            _hoveredObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
        _hoveredObject = null;
        if(_selectedObject != null)
            _selectedObject.EndBuildSelect();
        _selectedObject = null;
        if (_holdingRotateLock)
            ControlLock.Instance.ReturnLock(ControlLock.ControlType.XJoystick_Right);
        _holdingRotateLock = false;
    }
    private void TrySelectingNewObject()
    {
        if (!UIManager.Instance.CursorController.OpenBuildSelectObject())
            return;
        Ray cursorRay = UIManager.Instance.GetCursorRay();
        // Do nothing if the cursor is already hitting the UI
        Vector3 junk;
        if (UIManager.Instance.IsCursorHoveringUI(cursorRay, out junk))
            return;
        UIManager.Instance.LaserPointer.laserBeamBehavior = VRLaserPointer.LaserBeamBehavior.On;
        // Raycast out into the scene
        if (!Physics.Raycast(cursorRay, out RaycastHit hit, 1000f, GLLayers.BuildGrabbableLayerMask))
        {
            UIManager.Instance.LaserPointer.SetCursorStartDest(cursorRay.origin, cursorRay.origin + cursorRay.direction * NonHitLaserDistance, false);
            return;
        }
        // Draw the laser
        UIManager.Instance.LaserPointer.SetCursorStartDest(cursorRay.origin, hit.point, false);
        // Get the scene object
        Transform parent = hit.transform;
        SceneObject sceneObject = null;
        while(sceneObject == null && !parent.CompareTag(GLLayers.SceneObjectTag))
            parent = parent.parent;
        if(parent != null)
            sceneObject = parent.gameObject.GetComponent<SceneObject>();
        if(sceneObject == null)
        {
            Debug.LogError("Hit object " + hit.transform.gameObject.name + ", but no scene object found!");
            return;
        }
        if (UIManager.Instance.CursorController.GetBuildGrabObject())
        {
            // Select the object
            if (!sceneObject.BuildSelect())
            {
                Debug.LogError("Try grab failed in build controls!");
                return;
            }
            _selectedObject = sceneObject;
            if (_hoveredObject != null
                && _hoveredObject != _selectedObject)
                _hoveredObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
        }
        // Highlight the object
        _hoveredObject = sceneObject;
        _hoveredObject.SetObjectOutline(ObjectOutline.OutlineState.BuildHover);
    }
    public void HandleBuildSelect()
    {
        if(_selectedObject == null)
        {
            SceneObject prevHovered = _hoveredObject;
            _hoveredObject = null;
            TrySelectingNewObject();
            if (prevHovered != null && prevHovered != _hoveredObject)
                prevHovered.SetObjectOutline(ObjectOutline.OutlineState.Off);
        }
        if (_selectedObject == null)
            return;
        // Handle if our grab failed
        if(_selectedObject.CurrentGrabState != SceneObject.GrabState.PendingGrabbedBySelf
            && _selectedObject.CurrentGrabState != SceneObject.GrabState.GrabbedBySelf)
        {
            Debug.LogWarning("Grab failed for obj #" + _selectedObject.GetID() + " ungrabbing in build");
            _selectedObject = null;
            return;
        }
        // Handle if we're no longer holding trigger
        if (!UIManager.Instance.CursorController.GetBuildGrabObject())
        {
            BuildSelectOff();
            return;
        }
        Ray cursorRay = UIManager.Instance.GetCursorRay();
        if (!Physics.Raycast(cursorRay, out RaycastHit hit, 1000f, GLLayers.BuildGrabbableLayerMask | GLLayers.TableLayerMask))
            return;
        Vector3 worldPos = hit.point;
        if(_selectedObject.BundleItem != null)
        {
            var aabb = _selectedObject.BundleItem.AABBInfo;
            var delta = new Vector3(0, (aabb.Extents.y - aabb.Center.y) * SceneObjectManager.Instance.RootObject.localScale.y, 0);
            worldPos += delta;
        }
        else
        {
            Debug.LogError("Obj has no AABB! Obj #" + _selectedObject.GetID() + " " + _selectedObject.Name);
        }
        // Get a lock on the control, if we don't have one
        if (!_holdingRotateLock)
            _holdingRotateLock = ControlLock.Instance.TryLock(ControlLock.ControlType.XJoystick_Right);
        _selectedObject.transform.position = worldPos;
        float controllerX = UIManager.Instance.CursorController.GetRotateObjectAxis().x;
        if(_holdingRotateLock)
            _selectedObject.transform.Rotate(Vector3.up * controllerX * UIManager.Instance.RotateSpeedMax * Time.unscaledDeltaTime);
    }
    private void BuildMove() {
        var lController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND];
        var rController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND];

        if (!SupportGrab)
        {
            // If we're not doing a grab, then we can just use the thumbsticks to locomote
            float forwardBack = ControllerAbstraction.Instances[0].GetForwardBackMove();
            float leftRight = ControllerAbstraction.Instances[0].GetLeftRightMove();
            float upDown = ControllerAbstraction.Instances[0].GetUpDownMove();
            float snapTurnLeftRight = ControllerAbstraction.Instances[0].GetSnapTurn();

            // If we're actually moving via controller, then stop the slide
            _isSliding = false;
            // Get left/right rotation
            Quaternion rot = ControllerAbstraction.Instances[0].GetRotation();
            Vector3 euler = new Vector3(0, rot.eulerAngles.y, 0);
            rot = Quaternion.Euler(euler);
            // Move based on the thumbstick input
            transform.position += transform.localScale.x * ThumbstickMoveSpeed * Time.unscaledDeltaTime * (rot * new Vector3(leftRight, upDown, forwardBack));

            //Debug.Log("Build move #" + Time.frameCount);
            return;
        }
        Vector3 newLPos, newRPos;
        Quaternion newLRot, newRRot;
        bool lControllerGripping = lController.GetBuildGrabMove();
        bool rControllerGripping = rController.GetBuildGrabMove();
        if (lControllerGripping && rControllerGripping)
        {
            // Handle the user scaling the scene up and down
            //Debug.Log("Double grab");
            lController.GetLocalPositionRotation(out newLPos, out newLRot);
            rController.GetLocalPositionRotation(out newRPos, out newRRot);
            if(!_wasGrabbingL)
                _grabStartTimeL = Time.realtimeSinceStartup;
            if(!_wasGrabbingR)
                _grabStartTimeR = Time.realtimeSinceStartup;
            if(_wasGrabbingL && _wasGrabbingR)
            {
                float initialDist = (_grabPosL - _grabPosR).magnitude;
                float newDist = (newLPos - newRPos).magnitude;
                float newScale = _userScale * (initialDist / newDist);
                if (float.IsNaN(newScale))
                    return;
                //float newScale = _userScale * (newDist / initialDist);
                //Debug.Log("scale dist was " + initialDist + " now " + newDist);
                newScale = Mathf.Clamp(newScale, MinScale, MaxScale);
                Vector3 headPos = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.HEAD].GetPosition();
                transform.localScale = new Vector3(newScale, newScale, newScale);
                _userScale = transform.localScale.y;
                // The user wants to scale the scene up/down, but they want to feel like
                // their position hasn't changed. So we take their eye distance, and
                // move the top transform position so that their eye position
                // remains unchanged
                Vector3 newHeadPos = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.HEAD].GetPosition();
                transform.position -= (newHeadPos - headPos);
                //Debug.Log("Diff " + (newPos - _userPos).ToPrettyString());

                _grabPosL = newLPos;
                _grabPosR = newRPos;
            }
            else
            {
                _grabPosL = newLPos;
                _grabPosR = newRPos;
                _userScale = transform.localScale.y;
                _isSliding = false;
                _positionQueue.Clear();
            }
        }else if(_wasGrabbingL && _wasGrabbingR)
        {
            // end of double grab, reset state
            _wasGrabbingL = false;
            _wasGrabbingR = false;
            _isSliding = false;
            _positionQueue.Clear();
            return;
        }
        else if (lControllerGripping)
        {
            lController.GetPositionAndRotation(out newLPos, out newLRot);
            if (!_wasGrabbingL)
            {
                //Debug.Log("L grab");
                _grabPosL = newLPos;
                _grabUserPosL = transform.position;
                _isSliding = false;
                _grabStartTimeL = Time.realtimeSinceStartup;
            }
            else
            {
                // This is a grab and move
                Vector3 controllerDelta = newLPos - _grabPosL;
                transform.localPosition = _grabUserPosL - controllerDelta;
                Vector3 newWorldPos = _grabUserPosL - controllerDelta;
                transform.position = newWorldPos;
                _grabUserPosL = newWorldPos;
            }
            _positionQueue.Add(transform.position);
        }
        else if (rControllerGripping)
        {
            rController.GetPositionAndRotation(out newRPos, out newRRot);
            if (!_wasGrabbingR)
            {
                //Debug.Log("R grab");
                _grabPosR = newRPos;
                _grabUserPosR = transform.position;
                _isSliding = false;
                _grabStartTimeR = Time.realtimeSinceStartup;
            }
            else
            {
                // This is a grab and move
                Vector3 controllerDelta = newRPos - _grabPosR;
                Vector3 newWorldPos = _grabUserPosR - controllerDelta;
                transform.position = newWorldPos;
                _grabUserPosR = newWorldPos;
            }
            _positionQueue.Add(transform.position);
        }else if (_wasGrabbingL || _wasGrabbingR)
        {
            // If we were grabbing L/R, and we're not now
            // we see if we need to begin sliding the user
            if (_positionQueue.Count() >= NumPosSamples)
            {
                float grabTimeL = Time.realtimeSinceStartup - _grabStartTimeL;
                float grabTimeR = Time.realtimeSinceStartup - _grabStartTimeR;
                if ((_wasGrabbingL && grabTimeL < MinGrabTime)
                    || (_wasGrabbingR && grabTimeR < MinGrabTime))
                {
                    Debug.Log("Stopping sliding, too short of a grab");
                    _isSliding = false;
                }
                else
                {
                    //Debug.Log("Will now begin slide");
                    _isSliding = true;
                    _slideVel = _positionQueue.ReadMedianVelocity();
                    //Debug.Log("Got sliding vel " + _slideVel);

                    // If the movement was mainly in Y
                    // don't slide in X/Z
                    if (Mathf.Abs(_slideVel.y) > Mathf.Abs(SlideYMaxFactor * _slideVel.x)
                        && Mathf.Abs(_slideVel.y) > Mathf.Abs(SlideYMaxFactor * _slideVel.z))
                        _slideVel = Vector3.zero;
                    else
                        _slideVel.y = 0;
                }
            }
        }
        else
        {
            // If we're not doing a grab, then we can just use the thumbsticks to locomote
            float forwardBack = ControllerAbstraction.Instances[0].GetForwardBackMove();
            float leftRight = ControllerAbstraction.Instances[0].GetLeftRightMove();
            float upDown = ControllerAbstraction.Instances[0].GetUpDownMove();
            float snapTurnLeftRight = ControllerAbstraction.Instances[0].GetSnapTurn();

            // If we're actually moving via controller, then stop the slide
            if (Mathf.Abs(forwardBack) > 0.01f
                || Mathf.Abs(leftRight) > 0.01f
                || (Mathf.Abs(snapTurnLeftRight) < 0.5f && Mathf.Abs(upDown) > 0.01f)
                )
            {
                _isSliding = false;
                // Get left/right rotation
                Quaternion rot = ControllerAbstraction.Instances[0].GetRotation();
                Vector3 euler = new Vector3(0, rot.eulerAngles.y, 0);
                rot = Quaternion.Euler(euler);
                // Move based on the thumbstick input
                transform.position += transform.localScale.x * ThumbstickMoveSpeed * Time.unscaledDeltaTime * (rot * new Vector3(leftRight, upDown, forwardBack));
            }
        }

        if (_isSliding)
        {
            if(_slideVel.sqrMagnitude < MinSlideVelocitySqr)
            {
                Debug.Log("Stopping sliding");
                _isSliding = false;
            }
            else
            {
                // Slow down the slide speed
                transform.position += _slideVel * Time.unscaledDeltaTime;
                _slideVel = _slideVel - FrictionFactor * _slideVel * Time.unscaledDeltaTime;
            }
        }
        _wasGrabbingL = lControllerGripping;
        _wasGrabbingR = rControllerGripping;
    }
}
