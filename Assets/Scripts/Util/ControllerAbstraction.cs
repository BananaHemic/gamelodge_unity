/*
 * A custom abstraction for different controller inputs
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.XR;

using Valve.VR;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class ControllerAbstraction : MonoBehaviour
{
    public enum ControllerType
    {
        HEAD = 0,
        LEFTHAND = 1,
        RIGHTHAND = 2
    }
    public const int NumControllerTypes = 3;

    public static ControllerAbstraction[] Instances { get; private set; }
    public static Camera[] Cameras { get; private set; }
    // For scripts that do snap turning, or influence other
    // scripts that move the user
    public static Action OnControllerPoseUpdate_SnapTurn;
    // For scripts that control the user
    public static Action OnControllerPoseUpdate_User;
    // For scripts that move the hands
    public static Action OnControllerPoseUpdate_Pose;
    // For scripts that need updated controller and user positions
    public static Action OnControllerPoseUpdate_General;
    // Fires after the two other updates.
    // Notifies scripts that if they have changes to be
    // synced, they should enqueue those changes now
    public static Action OnObjectsEnqueueNetworkMessages;
    // Fires last. Here all enqueued messages actually
    // are sent over the network
    public static Action OnSendNetworkMessagesToWire;
    public AudioClip GrabVibration;
    //public GameObject Cursor;
    public BoxCollider PropCollider;

    /// <summary>
    /// NOTE: This may be called multiple times
    /// Especially on SteamVR
    /// </summary>
    public static Action<ControllerType> OnControllerConnected;
    public static Action<ControllerType> OnControllerDisconnected;

    public Action<Transform> OnHoverEnter;
    public Action<Transform> OnLeftHover;
    public Action<Transform> OnClickedObject;
    public Action OnBackClick;
    public Action OnTouchStart;
    public Action OnTouchEnd;
    public Action OnCanGrab;
    public Action OnCannotGrab;
    public Action<IGrabbable> OnGrabbedSomethingNew;
    // Called from BOTH steam's overlay (like purchasing)
    // Or from Oculus Dash being up
    public Action OnOverlayOn;
    public Action OnOverlayOff;

    public Action<float, bool> OnHoverDistance;

    public ControllerType MyControllerType;
    public bool ShouldProcessHovering {
        get
        {
            return _canHandleHovering && IsConnected;
        }
    }
    private bool _canHandleHovering;

    public bool IsConnected { get; private set; }
    public bool IsTouching { get; private set; }
    public bool IsHovering { get; private set; }
    public bool CanGrab { get; private set; }
    public Transform HoveredTransform { get; private set; }

    private static int _hasProcessedGrabbed;
    private const int AllGrabbed = 0
        | 1 << (int)ControllerType.HEAD
        | 1 << (int)ControllerType.LEFTHAND
        | 1 << (int)ControllerType.RIGHTHAND;
    public IGrabbable CurrentGrabbed { get; private set; }
    private IGrabbable _previousClosestGrabbable;
    private bool _didGrabSomethingNew;
    private int _frameOnLastPoseUpdate;

    const int _trackedLayerMask = 1 << 5; // Only collide with UI
    const int _headLayerMask = 1 | (1 << 5) | (1 << 11); // Only collide with default or UI or Chat
    const int _3DoFLayerMask = 1 | (1 << 5) | (1 << 11); // Only collide with default or UI or Chat
    const int _maxDist = 100;
    private readonly YieldInstruction checkConnectionInterval = new WaitForSeconds(0.1f);
    private readonly Collider[] _workingColliderRay = new Collider[128];

    #region OCULUS
    private OVRCameraRig _cameraRig;
    private readonly OVRInput.Button _interactButton = OVRInput.Button.One
        | OVRInput.Button.PrimaryIndexTrigger;
    private readonly OVRInput.Touch _interactTouch = OVRInput.Touch.One
        | OVRInput.Touch.PrimaryIndexTrigger;
    private OVRInput.Controller _currentController;
    private OVRHaptics.OVRHapticsChannel _channel;
    // Keep a cache of vibration clips
    private readonly Dictionary<byte[], OVRHapticsClip> _byteArray2OVRHaptic = new Dictionary<byte[], OVRHapticsClip>();
    private readonly Dictionary<AudioClip, OVRHapticsClip> _clip2OVRHaptic = new Dictionary<AudioClip, OVRHapticsClip>();
    // If Dash is on. Set by HEAD
    private static bool _isDashActive = false;
    private bool _didSubscripeToUpdatedAnchors;
    private bool _didSubscripeToOpenVROnRecvPose;
    #endregion
    #region VIVE
    SteamVR_Events.Action _trackedDeviceRoleChangedAction;
    // If a Steam Overlay is on. set by HEAD
    private static bool _isGameOverlayActive = false;
    // This is only set/used by HEAD
#if !DISABLESTEAMWORKS
    private Callback<GameOverlayActivated_t> _gameOverlayActivatedCallback;
#endif
    // This is only used on the head, because the normal way returns 0,0,0
    private TrackedDevicePose_t[] _openvrPoses = new TrackedDevicePose_t[1];
    private SteamVR_Input_Sources _steamInputType;
    private SteamVR_Action_Vibration _steamVibration = SteamVR_Input.GetAction<SteamVR_Action_Vibration>("Haptic");
    #endregion
    private VRSDKUtils.SDK _lastSDK;

    void Awake()
    {
        // Add singletons for each controller type
        if (Instances == null)
            Instances = new ControllerAbstraction[3];
        //if (_grabbables == null)
        //_grabbables = new List<Grabbable>();

        if (Instances[(int)MyControllerType] != null)
        {
            Debug.LogError("ControllerAbstraction instance of type " + MyControllerType + " already exists!", this);
        }
        Instances[(int)MyControllerType] = this;
        // Until controllers are connected, only the head can hover
        if (MyControllerType == ControllerType.HEAD)
            _canHandleHovering = true;
        else
            _canHandleHovering = false;
        InitForSDK();
        VRSDKUtils.OnVRModeChanged += InitForSDK;

        //_flexVisual = GetComponent<FlexLaserVisual>();
        //if(UsesCursor())
        //Cursor.SetActive(true);
    }
    private void InitForSDK()
    {
        _lastSDK = VRSDKUtils.Instance.CurrentSDK;
        if (_didSubscripeToUpdatedAnchors)
        {
            _cameraRig.UpdatedAnchors -= CameraRig_UpdatedAnchors;
            _didSubscripeToUpdatedAnchors = false;
        }
        if (_didSubscripeToOpenVROnRecvPose)
        {
            SteamVR_Actions.default_Pose[_steamInputType].onUpdate -= OpenVR_OnRecvPose;
            _didSubscripeToOpenVROnRecvPose = false;
        }
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                _cameraRig = FindObjectOfType<OVRCameraRig>();
                if (MyControllerType == ControllerType.HEAD)
                {
                    _didSubscripeToUpdatedAnchors = true;
                    _cameraRig.UpdatedAnchors += CameraRig_UpdatedAnchors;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        _steamInputType = SteamVR_Input_Sources.Head;
                        break;
                    case ControllerType.LEFTHAND:
                        _steamInputType = SteamVR_Input_Sources.LeftHand;
                        break;
                    case ControllerType.RIGHTHAND:
                        _steamInputType = SteamVR_Input_Sources.RightHand;
                        break;
                }
                SteamVR_Actions.default_Pose[_steamInputType].onUpdate += OpenVR_OnRecvPose;
                _didSubscripeToOpenVROnRecvPose = true;
                if (_trackedDeviceRoleChangedAction == null)
                {
                    _trackedDeviceRoleChangedAction = SteamVR_Events.SystemAction(EVREventType.VREvent_TrackedDeviceRoleChanged, OnSteamVRControllerRoleChanged);
                    _trackedDeviceRoleChangedAction.Enable(true);
                    //Debug.LogWarning("role change subscribe ");
                }
                break;
        }
        LoadController();
    }
    private void ConfirmSDKLoaded()
    {
        if (_lastSDK != VRSDKUtils.Instance.CurrentSDK)
        {
            Debug.Log("Controller abstraction reloading from SDK change");
            InitForSDK();
        }
    }
    private void CheckForStaleState()
    {
        //if(Time.frameCount != _frameOnLastPoseUpdate)
            //Debug.LogWarning("We have a stale state! num frames stale: " + (Time.frameCount - _frameOnLastPoseUpdate));
    }

    // Set up for our steam works and OVR callbacks
    private void OnEnable()
    {
        // Callbacks only need to happen once so we only register for the head
        if (MyControllerType != ControllerType.HEAD)
            return;

        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.OpenVR:
#if !DISABLESTEAMWORKS
                if (!SteamManager.Initialized)
                {
                    Debug.LogError("Steam Manager not initialized?");
                    return;
                }
                if (_gameOverlayActivatedCallback != null)
                {
                    Debug.LogWarning("Not double subscribing to overlay");
                    return;
                }
                Debug.Log("Subscribing to overlay");
                _gameOverlayActivatedCallback = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
#endif
                break;
            case VRSDKUtils.SDK.Oculus:
                OVRManager.InputFocusAcquired += OnOVRInputAcquired;
                OVRManager.InputFocusLost += OnOVRInputLost;
                break;
        }
    }
    // When DASH closes
    private void OnOVRInputAcquired()
    {
        //Debug.Log("Dash off");
        _isDashActive = false;
        if (OnOverlayOff != null)
            OnOverlayOff();
    }
    // When DASH opens
    private void OnOVRInputLost()
    {
        //Debug.Log("Dash on");
        _isDashActive = true;
        if (OnOverlayOn != null)
            OnOverlayOn();
    }
#if !DISABLESTEAMWORKS
    private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
    {
        if (pCallback.m_bActive != 0)
        {
            Debug.Log("Steam Overlay has been activated " + pCallback.m_bActive);
            if (!_isGameOverlayActive)
            {
                if (OnOverlayOn != null)
                    OnOverlayOn();
            }
            _isGameOverlayActive = true;
        }
        else
        {
            Debug.Log("Steam Overlay has been closed");
            if (_isGameOverlayActive)
            {
                if (OnOverlayOff != null)
                    OnOverlayOff();
            }
            _isGameOverlayActive = false;
        }
    }
#endif

    public static bool AreAnyGripping()
    {
        return Instances[0].GetBuildGrabMove()
            || Instances[1].GetBuildGrabMove()
            || Instances[2].GetBuildGrabMove();
    }

    //public static bool AreAnyGrabbingObject()
    //{
    //return Instances[0].IsGrabbingObject()
    //|| Instances[1].IsGrabbingObject()
    //|| Instances[2].IsGrabbingObject();
    //}

    private void OnSteamVRControllerRoleChanged(VREvent_t vrEvent) {
        // This means that a steamVR device has had it's role changed
        // So we need to update what our indexes are
        // Every controller receives this callback
        //Debug.LogWarning("Controller role changed");
        LoadController();
    }

    void LoadController()
    {
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        _currentController = OVRInput.Controller.All;
                        _channel = OVRHaptics.RightChannel;
                        Cameras = OVRManager.instance.GetComponentsInChildren<Camera>();
                        break;
                    case ControllerType.LEFTHAND:
                        _currentController =
                            VRSDKUtils.Instance.IsGearOrGo()
                            ? OVRInput.Controller.LTrackedRemote
                            : OVRInput.Controller.LTouch;
                        _channel = OVRHaptics.LeftChannel;
                        break;
                    case ControllerType.RIGHTHAND:
                        _currentController =
                            VRSDKUtils.Instance.IsGearOrGo()
                            ? OVRInput.Controller.RTrackedRemote
                            : OVRInput.Controller.RTouch;
                        _channel = OVRHaptics.RightChannel;
                        break;
                }
                //Debug.Log("Controller type of: " + MyControllerType + " using: " + _currentController);
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        //_transform = Camera.main.transform;
                        //Cameras = _transform.parent.GetComponentsInChildren<Camera>();
                        //Index = SteamVR_TrackedObject.EIndex.Hmd;
                        break;
                    case ControllerType.LEFTHAND:
                        //var currentControllerL = GameObject.FindWithTag("Steam_Controller_Manager").GetComponent<SteamVR_ControllerManager>().left.GetComponent<SteamVR_TrackedObject>();
                        //if(Index != currentControllerL.index)
                        //Debug.LogWarning("Left index: " + Index + "->" + currentControllerL.index);
                        //Index = currentControllerL.index;
                        //_transform = currentControllerL.transform;
                        break;
                    case ControllerType.RIGHTHAND:
                        //var currentControllerR = GameObject.FindWithTag("Steam_Controller_Manager").GetComponent<SteamVR_ControllerManager>().right.GetComponent<SteamVR_TrackedObject>();
                        //if(Index != currentControllerR.index)
                        //Debug.LogWarning("Right index: " + Index + "->" + currentControllerR.index);
                        //Index = currentControllerR.index;
                        //_transform = currentControllerR.transform;
                        break;

                }
                break;
        }
    }
    public bool Is3DoFTracking()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return VRSDKUtils.Instance.IsGearOrGo();
                    case ControllerType.LEFTHAND:
                        return _currentController == OVRInput.Controller.LTrackedRemote;
                    case ControllerType.RIGHTHAND:
                        return _currentController == OVRInput.Controller.RTrackedRemote;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                return false;
        }
        return false;
    }
    /// <summary>
    /// Called when for some other reason, we need to release our grab of this object
    /// Like, if someone else has grabbed this object
    /// </summary>
    public void ForceUngrab()
    {
        if (CurrentGrabbed != null)
            CurrentGrabbed.OnLocalGrabEnd(MyControllerType);
        CurrentGrabbed = null;
        if (OnGrabbedSomethingNew != null)
            OnGrabbedSomethingNew(null);
    }
    private void GetObjectToGrab()
    {
        // Heads can't grip anything
        if (MyControllerType == ControllerType.HEAD)
            return;

        // If we're grabbing an object, and we still have the grab down,
        // then keep grabbing
        bool isGrabbing = Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode ? GetPlayGrabObject() : GetBuildGrabObject();
        if (isGrabbing && CurrentGrabbed != null)
            return;
        // Each controller abstraction processes whether the device is gripping and what object it's gripping
        // GripEnds are called immediately, grip begins are called when the last controller abstraction is run
        // This allows us to know if multiple controllers are grabbing the same object
        IGrabbable closestGrababble = null;

        // Go through all the colliding grabbed objects and find the closest gripped one
        Vector3 controllerPosition = GetPosition();
        int collisionLayer =  Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode ? GLLayers.AllGrabbable_Play : GLLayers.BuildGrabbableLayerMask;
        int numHit = Physics.OverlapSphereNonAlloc(controllerPosition, UserObject.Instance.transform.localScale.y * 0.15f, _workingColliderRay, collisionLayer);
        // TODO if we didn't hit anything, then check distance grab
        float minDist = float.MaxValue;
        for (int i = 0; i < numHit; i++)
        {
            Collider hit = _workingColliderRay[i];
            // Skip if the obj isn't a grabbable
            if (Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode
                && !hit.CompareTag(GLLayers.GrabbableTag))
                continue;
            if (Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode
                && !hit.CompareTag(GLLayers.BuildGrabbableTag))
                continue;

            Bounds bounds = hit.bounds;
            // Get the actual IGrabbable from the collider
            IGrabbable grabbable = null;
            Transform parent = hit.transform;
            // Get the sceneObject gameObject
            string tag = Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode ? GLLayers.SceneObjectTag : GLLayers.BuildGrabbableTag;
            while (parent != null && !parent.CompareTag(tag))
                parent = parent.parent;
            if(parent == null)
            {
                Debug.LogError("No scene object on " + hit.gameObject.name);
                continue;
            }
            // Get the Grabbable off the object
            if(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode)
                grabbable = parent.GetComponent<BuildGrabbable>();
            else if(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode)
                grabbable = parent.GetComponent<PlayGrabbable>();

            if (grabbable == null)
            {
                Debug.LogError("No grabbable found on " + hit.name);
                continue;
            }
            // If the object we're currently grabbing
            // is still grabbable, then it doesn't matter how close any other
            // potential objects are
            if (grabbable == CurrentGrabbed)
            {
                closestGrababble = CurrentGrabbed;
                //Debug.Log("Dist on #" + Time.frameCount + " is " + (bounds.center - controllerPosition).sqrMagnitude);
                break;
            }
            // Drop if someone else is grabbing this
            if (!grabbable.CanGrab(MyControllerType))
                continue;

            //float dist = b.SqrDistance(ourPosition);
            // Get the distance, inversly scaled with the size of the object
            // so we grab small objects first
            float dist = (bounds.center - controllerPosition).sqrMagnitude * bounds.extents.sqrMagnitude;
            // Keep track of whichever grabbable is closest
            if (dist < minDist)
            {
                minDist = dist;
                closestGrababble = grabbable;
            }
        }
        //if(closestGrababble != null)
        //{
        //Debug.Log("Closest: " + closestGrababble.gameObject.name
        //+ " center: " + closestGrababble.GetBounds().center.ToPrettyString()
        //+ " extents: " + closestGrababble.GetBounds().extents.ToPrettyString());
        //}

        // Handle when OnCanGrab/OnCannotGrab events
        if (CanGrab && closestGrababble == null)
        {
            CanGrab = false;
            if (OnCannotGrab != null)
                OnCannotGrab();
        } else if (!CanGrab && closestGrababble != null)
        {
            CanGrab = true;
            if (OnCanGrab != null)
                OnCanGrab();
        }

        // Tell objects that they can/can't be grabbed
        if (_previousClosestGrabbable != closestGrababble)
        {
            if (_previousClosestGrabbable != null)
                _previousClosestGrabbable.OnCanGrabStateChange(false, MyControllerType);

            if (closestGrababble != null)
                closestGrababble.OnCanGrabStateChange(true, MyControllerType);

            _previousClosestGrabbable = closestGrababble;
        }

        // Grabbed is a misnomer
        // We just have to handle the OnCanGrab/OnCannotGrab events
        // Even if there isn't any gripping
        if (!isGrabbing)
        {
            closestGrababble = null;
            //if (_currentGrabbed != null)
                //Debug.Log("Let go of object " + Time.frameCount);
        }

        _didGrabSomethingNew = (closestGrababble != null) && (closestGrababble != CurrentGrabbed);

        if (CurrentGrabbed != null && closestGrababble != CurrentGrabbed)
        {
            //if (closestGrababble != null)
                //Debug.Log("Hit something new");
            //else
                //Debug.Log("Hitting nothing now #" + Time.frameCount);

            //TODO ideally we would only fire this when the item is fully un-grabbed
            // So after all the controllers have done their processing. This way,
            // if we're grabbing with two controllers and release them at the same time
            // we only have one release network message, instead of a grab then release
            CurrentGrabbed.OnLocalGrabEnd(MyControllerType);
        }

        if (CurrentGrabbed == null && closestGrababble != null)
            Vibrate(GrabVibration, 0.2f, 69f, 0.9f);

        if (CurrentGrabbed != closestGrababble)
        {
            if (OnGrabbedSomethingNew != null)
                OnGrabbedSomethingNew(closestGrababble);
        }
        CurrentGrabbed = closestGrababble;
    }
    private void ProcessGrabbing()
    {
        _hasProcessedGrabbed |= 1 << (int)MyControllerType;
        _didGrabSomethingNew = false;
        GetObjectToGrab();

        if (_hasProcessedGrabbed == AllGrabbed)
        {
            // We want to fire the GrabStart only once on each grabbed object, with all the controllers
            // that are grabbing ig
            // This means all grab events have fired, and we're the last controller which needs to fire all GrabStart events
            _hasProcessedGrabbed = 0;

            for (int i = 0; i < NumControllerTypes; i++)
            {
                IGrabbable i_Grabbed = ControllerAbstraction.Instances[i].CurrentGrabbed;
                if (i_Grabbed == null)
                    continue;
                bool isNewGrab = Instances[i]._didGrabSomethingNew;

                int grabbedIndexes = 1 << i;
                // If another controller has already handled this object, skip
                for (int j = i - 1; j > 0; j--)
                {
                    IGrabbable j_Grabbed = ControllerAbstraction.Instances[j].CurrentGrabbed;
                    if (i_Grabbed == j_Grabbed)
                    {
                        grabbedIndexes = 0;
                        break;
                    }
                }
                if (grabbedIndexes == 0)
                    break;
                // See if any other controllers have seen this object
                for (int j = i + 1; j < NumControllerTypes; j++)
                {
                    IGrabbable j_Grabbed = ControllerAbstraction.Instances[j].CurrentGrabbed;
                    if (i_Grabbed == j_Grabbed)
                    {
                        grabbedIndexes |= 1 << j;
                        isNewGrab |= Instances[j]._didGrabSomethingNew;
                    }
                }
                if (isNewGrab)
                    i_Grabbed.OnLocalGrabStart(grabbedIndexes);
            }
        }
    }
    //public void Vibrate(byte[] vibrationData)
    //{
    //    ConfirmSDKLoaded();
    //    // no need to vibrate on GO/Gear
    //    if (VRSDKUtils.Instance.IsGearOrGo())
    //        return;
    //    switch (VRSDKUtils.Instance.CurrentSDK)
    //    {
    //        case VRSDKUtils.SDK.Oculus:
    //            channel.Clear();
    //            OVRHapticsClip clip;
    //            if (!_byteArray2OVRHaptic.TryGetValue(vibrationData, out clip))
    //            {
    //                clip = new OVRHapticsClip(vibrationData, vibrationData.Length);
    //                _byteArray2OVRHaptic[vibrationData] = clip;
    //            }
    //            channel.Queue(clip);
    //            break;
    //        case VRSDKUtils.SDK.OpenVR:
    //            Debug.LogError("Not implemented! use audioclip vibration");
    //            break;
    //    }
    //}
    public void Vibrate(AudioClip vibrationData, float duration, float frequency, float amplitude)
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                // no need to vibrate on GO/Gear
                if (VRSDKUtils.Instance.IsGearOrGo())
                    return;
                _channel.Clear();
                if (vibrationData == null)
                {
                    Debug.LogError("Can't vibrate null clip!");
                    return;
                }
                OVRHapticsClip clip;
                if (!_clip2OVRHaptic.TryGetValue(vibrationData, out clip))
                {
                    clip = new OVRHapticsClip(vibrationData);
                    _clip2OVRHaptic[vibrationData] = clip;
                    //Debug.Log("Using stored ovr haptic");
                }
                _channel.Queue(clip);
                break;
            case VRSDKUtils.SDK.OpenVR:
                _steamVibration.Execute(0, duration, frequency, amplitude, _steamInputType);
                break;
        }
    }
    public bool GetOpenMenuButton()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                return OVRInput.Get(OVRInput.Button.Two, _currentController);
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.OpenMenu[_steamInputType].state;
            default:
                return false;
        }
    }
    //public bool GetJoystickClick()
    //{
    //    ConfirmSDKLoaded();
    //    switch (VRSDKUtils.Instance.CurrentSDK)
    //    {
    //        case VRSDKUtils.SDK.Oculus:
    //            return OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick
    //                | OVRInput.Button.SecondaryThumbstick, _currentController);
    //        case VRSDKUtils.SDK.OpenVR:
    //            if (Index == SteamVR_TrackedObject.EIndex.None)
    //                return false;
    //            switch (MyControllerType)
    //            {
    //                case ControllerType.LEFTHAND:
    //                case ControllerType.RIGHTHAND:
    //                    return false;
    //                //return SteamVR_Controller.Input((int)Index).GetPress(SteamVR_Controller.ButtonMask.ApplicationMenu);
    //                case ControllerType.HEAD:
    //                    return ControllerAbstraction.Instances[(int)ControllerType.LEFTHAND].GetJoystickClick()
    //                        || ControllerAbstraction.Instances[(int)ControllerType.RIGHTHAND].GetJoystickClick();
    //            }
    //            break;
    //    }
    //    return false;
    //}
    public Vector2 GetRotateObjectAxis()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            case VRSDKUtils.SDK.Oculus:
                return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController);
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.BuildRotateObject[_steamInputType].axis;
        }
        return Vector2.zero;
    }
    public float GetForwardBackMove()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return Input.GetAxisRaw("Vertical");
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetForwardBackMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).y;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetForwardBackMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.BuildLeftRightMove[_steamInputType].axis.y;
                }
                break;
        }
        return 0f;
    } 
    public float GetLeftRightMove()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return Input.GetAxisRaw("Horizontal");
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetLeftRightMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).x;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetLeftRightMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.BuildLeftRightMove[_steamInputType].axis.x;
                }
                break;
        }
        return 0f;
    }
    public float GetUpDownMove()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return 0;
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetUpDownMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).y;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetUpDownMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.BuildUpDownMove[_steamInputType].axis.y;
                }
                break;
        }
        return 0f;
    } 
    public bool GetSprintMove()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetSprintMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Button.PrimaryThumbstick, _currentController);
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.LEFTHAND].GetSprintMove();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.Sprint[_steamInputType].state;
                }
                break;
        }
        return false;
    } 
    public bool GetBuildGrabMove()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return false;
                    case ControllerType.LEFTHAND:
                        return OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger, _currentController) > 0.1;
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger, _currentController) > 0.1;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.BuildGrabMove[_steamInputType].state;
        }
        return false;
    }
    public float GetSnapTurn()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return 0;
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetSnapTurn();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).x;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.SnapTurn[_steamInputType].axis.x;
        }
        return 0;
    }
    public bool GetSnapTurnLeftDown()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return false;
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetSnapTurnLeftDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).x < -0.9f;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetSnapTurnLeftDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        //Debug.Log("Snap val " + SteamVR_Actions._default.SnapTurn[_steamInputType].axis.x);
                        return SteamVR_Actions._default.SnapTurn[_steamInputType].axis.x < -0.9f;
                }
                break;
        }
        return false;
    }
    public bool GetSnapTurnRightDown()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return false;
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetSnapTurnRightDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _currentController).x > 0.9f;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[(int)ControllerType.RIGHTHAND].GetSnapTurnRightDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.SnapTurn[_steamInputType].axis.x > 0.9f;
                }
                break;
        }
        return false;
    }
    public bool IsGrabbingObject()
    {
        return CurrentGrabbed != null;
    }
    public bool OpenBuildSelectObject()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].OpenBuildSelectObject() || Instances[2].OpenBuildSelectObject();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Button.One, _currentController);
                    default:
                        return false;
                }
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.OpenBuildSelectObject[_steamInputType].state;
        }
        return false;
    }
    public bool GetBuildGrabObject()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].GetBuildGrabObject() || Instances[2].GetBuildGrabObject();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, _currentController);
                    default:
                        return false;
                }
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.BuildGrabObject[_steamInputType].state;
        }
        return false;
    }
    public bool GetPlayTrigger()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return false;
                    case ControllerType.LEFTHAND:
                        return OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, _currentController);
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, _currentController);
                }
                return false;
            case VRSDKUtils.SDK.OpenVR:
                //Debug.Log("input " + _steamInputType + ": " + SteamVR_Actions._default.PlayTrigger[_steamInputType].axis);
                return SteamVR_Actions._default.PlayTrigger[_steamInputType].axis > 0.5f;
            case VRSDKUtils.SDK.Desktop:
                //Debug.Log("Check");
                return Input.GetKey(KeyCode.F);//TODO
        }
        return false;
    }
    public bool GetPlayGrabObject()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return false;
                    case ControllerType.LEFTHAND:
                        return OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger, _currentController) > 0.1;
                    case ControllerType.RIGHTHAND:
                        return OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger, _currentController) > 0.1;
                }
                return false;
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions._default.PlayGrabObject[_steamInputType].state;
        }
        return false;
    }
    public bool GetMenuInteractDown()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].GetMenuInteractDown() || Instances[2].GetMenuInteractDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.GetDown(_interactButton, _currentController);
                    default:
                        return false;
                }
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].GetMenuInteractDown() || Instances[2].GetMenuInteractDown();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.MenuInteract[_steamInputType].stateDown;
                    default:
                        return false;
                }
        }
        return false;
    }
    public bool GetMenuInteractUp()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].GetMenuInteractUp() || Instances[2].GetMenuInteractUp();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return OVRInput.GetUp(_interactButton, _currentController);
                    default:
                        return false;
                }
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        return Instances[1].GetMenuInteractUp() || Instances[2].GetMenuInteractUp();
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        return SteamVR_Actions._default.MenuInteract[_steamInputType].stateUp;
                    default:
                        return false;
                }
        }
        return false;
    }
    public bool IsTouchingThisFrame()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                return OVRInput.Get(_interactTouch, _currentController);
            case VRSDKUtils.SDK.OpenVR:
            default:
                /* For now we just say it's always tuoching
                if (Index == SteamVR_TrackedObject.EIndex.None)
                    return false;
                return SteamVR_Controller.Input((int)Index).GetPress(EVRButtonId.k_EButton_SteamVR_Touchpad);
                */
                return false;
                //return IsConnected && SteamVR_Controller.Input((int)Index).hasTracking;
                //return IsConnected;
        }
    }
    public bool IsTracking()
    {
        if (MyControllerType == ControllerType.HEAD)
            return true;
        ConfirmSDKLoaded();

        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                return OVRInput.GetControllerPositionTracked(_currentController);
            case VRSDKUtils.SDK.OpenVR:
            default:
                return false;
                //return IsConnected && SteamVR_Controller.Input((int)Index).hasTracking;
        }
    }
    void ProcessTouching()
    {
        if (IsTouching && !IsTouchingThisFrame())
        {
            IsTouching = false;
            if (OnTouchEnd != null)
                OnTouchEnd();
        }
        if (!IsTouching && IsTouchingThisFrame())
        {
            IsTouching = true;
            if (OnTouchStart != null)
                OnTouchStart();
        }
    }

    void ProcessHovering()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Debug.DrawRay(transform.position, transform.forward);
        RaycastHit hit;
        float length = 5.0f;
        bool isHit = false;
        int hitLayer = MyControllerType == ControllerType.HEAD
            ? _headLayerMask
            : Is3DoFTracking() ? _3DoFLayerMask : _trackedLayerMask;
        if (Physics.Raycast(ray, out hit, _maxDist, hitLayer))
        {
            if (!IsHovering)
            {
                if (OnHoverEnter != null)
                    OnHoverEnter(hit.transform);
            }
            else
            {
                if (hit.transform != HoveredTransform)
                {
                    if (OnLeftHover != null)
                        OnLeftHover(HoveredTransform);
                    if (OnHoverEnter != null)
                        OnHoverEnter(hit.transform);
                }
            }

            length = hit.distance;
            isHit = true;
            IsHovering = true;
            HoveredTransform = hit.transform;
            // If this is a head gaze, update the cursor
            //if (UsesCursor())
            //{
            //Cursor.SetActive(true);
            //Cursor.transform.position = hit.point;
            //}
        }
        else
        {
            if (IsHovering)
            {
                if (OnLeftHover != null)
                    OnLeftHover(HoveredTransform);

                //if (UsesCursor())
                //Cursor.SetActive(false);
            }
            HoveredTransform = null;
            IsHovering = false;
        }

        if (OnHoverDistance != null)
            OnHoverDistance(length, isHit);
    }

    void ProcessClicking()
    {
        if (GetMenuInteractDown())
        {
            if (OnClickedObject != null)
                OnClickedObject(HoveredTransform);
        }
    }

    bool CheckControllerConnected()
    {
        ConfirmSDKLoaded();
        if (MyControllerType == ControllerType.HEAD)
            return true;
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                return OVRInput.IsControllerConnected(_currentController);
            case VRSDKUtils.SDK.OpenVR:
                return SteamVR_Actions.default_Pose[_steamInputType].deviceIsConnected;
        }
        return false;
    }
    public bool OverlapsWithCollider(BoxCollider otherCollider)
    {
        return IsConnected
            && IsTracking()
            && PropCollider != null
            //&& otherCollider.bounds.Intersects(PropCollider.bounds); (low quality, fast method)
            && otherCollider.IntersectsHighQuality(PropCollider);
    }

    // Currently unused, as the steam controller velocity is sorta weird
    public Vector3 GetControllerVelocityWorld()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                return _cameraRig.trackingSpace.TransformVector(OVRInput.GetLocalControllerVelocity(_currentController));
            case VRSDKUtils.SDK.OpenVR:
                return transform.TransformVector(SteamVR_Actions.default_Pose[_steamInputType].velocity);
        }
        return Vector3.zero;
    }

    /// <summary>
    /// Returns this controller should be checked for input
    /// If false, this usually means that a menu is open
    /// </summary>
    /// <returns></returns>
    public bool ShouldHandleInput()
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.OpenVR:
                return !_isGameOverlayActive;
            case VRSDKUtils.SDK.Oculus:
                //Debug.Log("Is dash active: " + _isDashActive);
                return !_isDashActive;
            case VRSDKUtils.SDK.Desktop:
                return MyControllerType == ControllerType.HEAD;
            default:
                Debug.LogError("SDK/Device: " + VRSDKUtils.Instance.CurrentSDK + "/"
                    + VRSDKUtils.Instance.CurrentDevice + " not input supported");
                return true;
        }
    }
    private float GetTimeInFutureIfNextVFrame()
    {
        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.OpenVR)
            return 0;
        float timeSinceLastVsync = 0;
        ulong junk = 0;
        OpenVR.System.GetTimeSinceLastVsync(ref timeSinceLastVsync, ref junk);
        float dispFrameDuration = 1f / VRSDKUtils.Instance.GetDeviceDisplayFps();
        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
        float timefromVsyncToPhoton = OpenVR.System.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_SecondsFromVsyncToPhotons_Float, ref error);
        return dispFrameDuration - timeSinceLastVsync + timefromVsyncToPhoton;
    }
    public void GetLocalPositionRotation(out Vector3 localPos, out Quaternion localRot)
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        localPos = _cameraRig.centerEyeAnchor.localPosition;
                        localRot = _cameraRig.centerEyeAnchor.localRotation;
                        return;
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        localPos = OVRInput.GetLocalControllerPosition(_currentController);
                        localRot = OVRInput.GetLocalControllerRotation(_currentController);
                        return;
                    default:
                        Debug.LogError("Unknown controller type");
                        break;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                float timeInFuture = GetTimeInFutureIfNextVFrame();
                Vector3 vel, angVel;
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        // The normal method returns 0,0,0 for the head
                        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, timeInFuture, _openvrPoses);
                        localPos = _openvrPoses[0].mDeviceToAbsoluteTracking.GetPosition();
                        localRot = _openvrPoses[0].mDeviceToAbsoluteTracking.GetRotation();
                        return;
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        if (!SteamVR_Actions.default_Pose[_steamInputType].deviceIsConnected)
                            break;
                        SteamVR_Actions.default_Pose[_steamInputType].GetPoseAtTimeOffset(timeInFuture, out localPos, out localRot, out vel, out angVel);
                        // Below is an alternative method for getting the pose. For some reason, it
                        // seems to have a higher latency
                    //    InputPoseActionData_t poseActionData = new InputPoseActionData_t();
                    //    OpenVR.Input.GetPoseActionDataForNextFrame(
                    //    SteamVR_Actions.default_Pose[_steamInputType].handle,
                    //    ETrackingUniverseOrigin.TrackingUniverseStanding,
                    //    ref poseActionData,
                    //SteamVR_Action_Pose_Source.poseActionData_size,
                    //    SteamVR_Input_Source.GetHandle(_steamInputType)
                    //        );
                        return;
                }
                break;
            case VRSDKUtils.SDK.Desktop:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        localPos = new Vector3(0, 1.55f, 0);
                        //localPos = Orchestrator.Instance.MainCamera.transform.localPosition;
                        //localPos = Vector3.zero;
                        //localRot = Quaternion.identity;
                        localRot = Orchestrator.Instance.MainCamera.transform.localRotation;
                        return;
                    case ControllerType.LEFTHAND:
                        localPos = new Vector3(-0.3f, 1f, 0);
                        localRot = Quaternion.identity;
                        return;
                    case ControllerType.RIGHTHAND:
                        localPos = new Vector3(0.3f, 1f, 0);
                        localRot = Quaternion.identity;
                        return;
                }
                break;
        }
        localPos = Vector3.zero;
        localRot = Quaternion.identity;
    }
    public void GetLocalPosRotVelImmediate(out Vector3 localPos, out Quaternion localRot, out Vector3 localVel, out Vector3 localAngVel)
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        localPos = OVRInput.GetLocalControllerPosition(_currentController);
                        localRot = OVRInput.GetLocalControllerRotation(_currentController);
                        localVel = OVRInput.GetLocalControllerVelocity(_currentController);
                        localAngVel = OVRInput.GetLocalControllerAngularVelocity(_currentController);
                        return;
                    default:
                        Debug.LogError("Unknown controller type");
                        break;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                switch (MyControllerType)
                {
                    //case ControllerType.HEAD:
                        // The normal method returns 0,0,0 for the head
                        //OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, timeInFuture, _openvrPoses);
                        //localVel = _openvrPoses[0].mDeviceToAbsoluteTracking.GetPosition();
                        //localAngVel = _openvrPoses[0].mDeviceToAbsoluteTracking.GetRotation();
                        //return;
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        if (!SteamVR_Actions.default_Pose[_steamInputType].deviceIsConnected)
                            break;
                        SteamVR_Actions.default_Pose[_steamInputType].GetPoseAtTimeOffset(0, out localPos, out localRot, out localVel, out localAngVel);
                        return;
                }
                break;
        }
        localPos = Vector3.zero;
        localRot = Quaternion.identity;
        localVel = Vector3.zero;
        localAngVel = Vector3.zero;
    }
    public void GetPositionAndRotation(out Vector3 pos, out Quaternion rot)
    {
        ConfirmSDKLoaded();
        CheckForStaleState();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        pos = _cameraRig.centerEyeAnchor.position;
                        rot = _cameraRig.centerEyeAnchor.rotation;
                        return;
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        pos = transform.position;
                        rot = transform.rotation;
                        //pos = cameraRig.trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(_currentController));
                        //rot = cameraRig.trackingSpace.rotation * OVRInput.GetLocalControllerRotation(_currentController);
                        return;
                    default:
                        Debug.LogError("Unknown controller type");
                        break;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
                pos = transform.position;
                rot = transform.rotation;
                return;
        }
        pos = Vector3.zero;
        rot = Quaternion.identity;
    }
    public Vector3 GetPosition()
    {
        ConfirmSDKLoaded();
        CheckForStaleState();
        return transform.position;
    }
    public void GetLocalPositionRotationImmediate(out Vector3 localPos, out Quaternion localRot)
    {
        ConfirmSDKLoaded();
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Oculus:
                switch (MyControllerType)
                {
                    case ControllerType.HEAD:
                        localPos = _cameraRig.GetCamPosition();
                        localRot = _cameraRig.GetCamRotation();
                        return;
                    case ControllerType.LEFTHAND:
                    case ControllerType.RIGHTHAND:
                        localPos = OVRInput.GetLocalControllerPosition(_currentController);
                        localRot = OVRInput.GetLocalControllerRotation(_currentController);
                        return;
                }
                break;
            case VRSDKUtils.SDK.OpenVR:
            case VRSDKUtils.SDK.Desktop:
                GetLocalPositionRotation(out localPos, out localRot);
                return;
        }
        localPos = Vector3.zero;
        localRot = Quaternion.identity;
        return;
    }
    public Vector3 GetPositionFixedUpdate()
    {
        GetLocalPositionRotationImmediate(out Vector3 localPos, out Quaternion localRot);
        //Debug.Log("Recv pos " + localPos.ToPrettyString());
        Vector3 immedPos = transform.parent.TransformPoint(localPos);
        //Debug.Log("Head at pos " + GetPosition().ToPrettyString() + " immediate pos " + immedPos.ToPrettyString());
        return immedPos;
    }
    public Quaternion GetRotation()
    {
        ConfirmSDKLoaded();
        CheckForStaleState();
        return transform.rotation;
    }
    public Transform GetTransform()
    {
        ConfirmSDKLoaded();
        CheckForStaleState();
        return transform;
    }
    public static ControllerType OppositeHandFrom(ControllerType hand)
    {
        switch (hand)
        {
            case ControllerType.LEFTHAND:
                return ControllerType.RIGHTHAND;
            case ControllerType.RIGHTHAND:
                return ControllerType.LEFTHAND;
            default:
                return ControllerType.HEAD;
        }
    }
    private void RefreshIsConnected()
    {
        bool wasConnected = IsConnected;
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                IsConnected = MyControllerType == ControllerType.HEAD;
                break;
            case VRSDKUtils.SDK.Oculus:
                IsConnected = OVRInput.IsControllerConnected(_currentController);
                break;
            case VRSDKUtils.SDK.OpenVR:
                return; // Handled in OpenVR_OnRecvPose
        }

        // Fire updates when our connection state changes
        if (wasConnected == IsConnected)
            return;
        if(wasConnected && !IsConnected)
        {
            if (OnControllerDisconnected != null)
                OnControllerDisconnected(MyControllerType);
            return;
        }
        if(!wasConnected && IsConnected)
        {
            if (OnControllerConnected != null)
                OnControllerConnected(MyControllerType);
            return;
        }
    }
    private void CameraRig_UpdatedAnchors(OVRCameraRig obj)
    {
        // Manually notify all controllers
        for(int i = 0; i < NumControllerTypes; i++)
        {
            if (Instances[i] != null)
                Instances[i].OnPoseUpdate();
        }
        //Debug.Log("pose #" + Time.frameCount);
        //Debug.Log("Head at pos " + GetPosition().ToPrettyString());
        FirePoseUpdates();
        for(int i = 0; i < NumControllerTypes; i++)
        {
            if (Instances[i] != null)
                Instances[i].PostPoseUpdate();
        }
        // Now that all the pose data has been fired, we
        // tell the grabbed objects that they should update
        for(int i = 0; i < NumControllerTypes; i++)
        {
            var inst = Instances[i];
            if (inst != null && inst.CurrentGrabbed != null)
                inst.CurrentGrabbed.OnLocalGrabUpdate();
        }
        if(OnObjectsEnqueueNetworkMessages != null)
            OnObjectsEnqueueNetworkMessages();
        if (OnSendNetworkMessagesToWire != null)
            OnSendNetworkMessagesToWire();
    }
    //private Vector3 _lastPos;
    //private int _lastPosUpdateFrame = -1;
    //public float avg;
    //public int n = 50;
    private void OpenVR_OnRecvPose(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource)
    {
        if(fromSource != _steamInputType)
            Debug.LogError("Bad subscribe, expected " + _steamInputType + " got " + fromSource);
        bool wasConnected = IsConnected;
        IsConnected = fromAction.deviceIsConnected;
        // Fire updates when our connection state changes
        if (wasConnected == IsConnected)
            return;
        if(wasConnected && !IsConnected)
        {
            if (OnControllerDisconnected != null)
                OnControllerDisconnected(MyControllerType);
            return;
        }
        if(!wasConnected && IsConnected)
        {
            if (OnControllerConnected != null)
                OnControllerConnected(MyControllerType);
            return;
        }

        // Below can be used to measure latency
        //Vector3 currPos;
        //GetLocalPositionAndRotation(out currPos, out Quaternion junk);
        //Vector3 currPos = SteamVR_Actions.default_Pose[_steamInputType].localPosition;
        //float del = (_lastPos - currPos).magnitude;
        //if(n < 0)
            //avg += del;
        //n--;
        //if (_lastPosUpdateFrame == Time.frameCount)
            //Debug.Log("In frame delta is " + del + " " + MyControllerType + " avg: " + avg);
        //else
        //Debug.Log("Inter frame delta is " + del + " " + MyControllerType);
        //_lastPosUpdateFrame = Time.frameCount;
        //_lastPos = currPos;
    }
    private void FirePoseUpdates()
    {
        // Notify all listeners
        if (OnControllerPoseUpdate_SnapTurn != null)
            OnControllerPoseUpdate_SnapTurn();
        if (OnControllerPoseUpdate_User != null)
            OnControllerPoseUpdate_User();
        if (OnControllerPoseUpdate_Pose != null)
            OnControllerPoseUpdate_Pose();
        if(OnControllerPoseUpdate_General != null)
            OnControllerPoseUpdate_General();
        // Now that we've moved, and all listeners have moved, we tell PhysX to re-syncronize the tranforms
        // this is needed for any following Physics queries (like Grabbing) to be up-to-date
        Physics.SyncTransforms();
        //if(MyControllerType == ControllerType.HEAD && CustomCharacterController.Instance != null)
            //Debug.LogError("delta controller post #" + Time.frameCount + ": " + (ControllerAbstraction.Instances[0].GetPosition() - CustomCharacterController.Instance.CameraFollowPoint.position).sqrMagnitude);
        //Debug.Log("Updated controller abstraction position: " + _steamInputType + " " + transform.position + " " + Time.frameCount);
    }
    /// <summary>
    /// Called right after we get our new pos/rot
    /// And before any other scripts have been notified
    /// </summary>
    void OnPoseUpdate()
    {
        if (!ShouldHandleInput())
        {
            _frameOnLastPoseUpdate = Time.frameCount;
            return;
        }
        _frameOnLastPoseUpdate = Time.frameCount;
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.OpenVR
            && OpenVR.System == null)
            return;
        RefreshIsConnected();
        //if(MyControllerType == ControllerType.HEAD && CustomCharacterController.Instance != null)
            //Debug.Log("delta controller pre #" + Time.frameCount + ": " + (ControllerAbstraction.Instances[0].GetPosition() - CustomCharacterController.Instance.CameraFollowPoint.position).sqrMagnitude);
        Vector3 pos;
        Quaternion rot;
        GetLocalPositionRotation(out pos, out rot);
        transform.localPosition = pos;
        transform.localRotation = rot;
		ProcessTouching();
    }
    /// <summary>
    /// Called after the post has been set, and after
    /// all other objects have moved. This function is mainly for 
    /// things that require other scripts be updated on the most recent position first
    /// </summary>
    void PostPoseUpdate()
    {
        // If there's no Orchestrator, this is just a test
        if (Orchestrator.Instance == null)
            return;
        ProcessGrabbing();
        if (ShouldProcessHovering)
            ProcessHovering();
		if(IsHovering)
			ProcessClicking();
    }
    void LateUpdate()
	{
        //Vector3 currPos;
        //GetLocalPositionAndRotation(out currPos, out Quaternion junk);
        //float del = (_lastPos - currPos).magnitude;
        //if (_lastPosUpdateFrame == Time.frameCount)
        //Debug.Log("In frame delta is " + del + " " + MyControllerType);
        //else
        //Debug.Log("Inter frame delta is " + del + " " + MyControllerType);
        //_lastPosUpdateFrame = Time.frameCount;
        //_lastPos = currPos;
        //Debug.Log("late update #" + Time.frameCount);
        // Fire pose update here if we're the head and we're in desktop
        // mode
        // OpenVR updates in OnPreCull, so to get around that idiocy, we use
        // LateUpdate, and have our own functions to read the up-to-date
        // positions
        if (MyControllerType == ControllerType.HEAD
            && (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
            || VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.OpenVR))
        {

            for(int i = 0; i < NumControllerTypes; i++)
            {
                if (Instances[i] != null)
                    Instances[i].OnPoseUpdate();
            }
            FirePoseUpdates();
            for(int i = 0; i < NumControllerTypes; i++)
            {
                if (Instances[i] != null)
                    Instances[i].PostPoseUpdate();
            }
            // Now that all the pose data has been fired, we
            // tell the grabbed objects that they should update
            for(int i = 0; i < NumControllerTypes; i++)
            {
                var inst = Instances[i];
                if (inst != null && inst.CurrentGrabbed != null)
                    inst.CurrentGrabbed.OnLocalGrabUpdate();
            }
            if(OnObjectsEnqueueNetworkMessages != null)
                OnObjectsEnqueueNetworkMessages();
            if (OnSendNetworkMessagesToWire != null)
                OnSendNetworkMessagesToWire();
        }
    }
}