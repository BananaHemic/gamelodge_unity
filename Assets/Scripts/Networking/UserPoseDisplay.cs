using System.Collections;
using System.Collections.Generic;
using DarkRift;
using RootMotion.FinalIK;
using UnityEngine;

public class UserPoseDisplay : MonoBehaviour, IRealtimeObject
{
    public UserHandGrabbedDisplay HandDisplay { get; private set; }
    public DRUser DrUser { get; private set; }
    public Transform HeadTransform;
    public Transform LHandTransform;
    public Transform RHandTransform;
    public Transform HeadAttachOffset;
    public Transform LHandAttachOffset;
    public Transform RHandAttachOffset;

    /// <summary>
    /// The interpolation position and rotation of the real person
    /// in local space. Used both for local and network
    /// </summary>
    public Vector3 HeadPosition;
    public Vector3 HandPositionL;
    public Vector3 HandPositionR;
    public Quaternion HeadRotation;
    public Quaternion HandRotationL;
    public Quaternion HandRotationR;
    public bool HasHandL { get; private set; }
    public bool HasHandR { get; private set; }

    /// <summary>
    /// The positions/rotations in _world_ space for the
    /// body parts. These are for the previous frame, and
    /// are used both locally and for network. Needed to
    /// calculate velocities
    /// </summary>
    //public Vector3 PrevBodyPosition { get; private set; }
    //public Quaternion PrevBodyRotation { get; private set; }
    public Vector3 PrevHeadPosition { get; private set; }
    public Vector3 PrevHandPositionL { get; private set; }
    public Vector3 PrevHandPositionR { get; private set; }
    public Quaternion PrevHeadRotation { get; private set; }
    public Quaternion PrevHandRotationL { get; private set; }
    public Quaternion PrevHandRotationR { get; private set; }
    public bool PrevHasHandL { get; private set; }
    public bool PrevHasHandR { get; private set; }

    private bool _isLocal;
    private bool _hasInit;
    private UserDisplay _playerDisplay;
    private LipSync _lipSync;
    // Non-local things
    private float _timeOfRecvCurrentPose;
    private uint _numPosesRecv = 0;
    private DRUserPose.PoseInfo _previousPose;
    public DRUserPose.PoseInfo CurrentPose { get; private set; }
    private float _lastLerpAmount = -1;
    // Local things
    private int _tickOnLastUpdate = -1;
    private float _lastSendTime;

    public void Init(DRUser networkPlayer, UserDisplay playerDisplay, bool isLocal)
    {
        _isLocal = isLocal;
        if (_hasInit)
            Debug.LogError("Double init for NetworkPlayerPoseDisplay!");
        _hasInit = true;
        _playerDisplay = playerDisplay;
        //Debug.Log("Init playerdisp " + _playerDisplay);

        if (_isLocal)
        {
            ControllerAbstraction.OnObjectsEnqueueNetworkMessages += PossiblyUploadNewPose;
            ControllerAbstraction.OnControllerConnected += LocalRefreshVRIKWeights;
            ControllerAbstraction.OnControllerDisconnected += LocalRefreshVRIKWeights;
        }
        DrUser = networkPlayer;
        //Debug.Log("Init non-testing " + networkPlayer.ID);
        HandDisplay = new UserHandGrabbedDisplay(_isLocal, playerDisplay, this);
        _lipSync = gameObject.AddComponent<LipSync>();
        if (isLocal)
            _lipSync.InitLocal(playerDisplay);
        else
            _lipSync.InitNetwork(playerDisplay, _playerDisplay.NetworkAudioPlayer);
    }
    public void UserGrabObjectServer(SceneObject sceneObject, DRUser.GrabbingBodyPart bodyPart, Vector3 relPos, Quaternion relRot)
    {
        // Update DRUser
        DrUser.UserGrabObject(sceneObject.GetID(), bodyPart, relPos, relRot);
        // Update the hand display
        HandDisplay.UserGrabObjectServer(sceneObject, bodyPart, relPos, relRot);
    }
    public void UserReleaseObjectServer(SceneObject sceneObject) 
    {
        // Update DRUser
        DrUser.UserReleaseObject(sceneObject.GetID());
        // Update the hand display
        HandDisplay.UserReleaseObjectServer(sceneObject);
    }
    public void RelativePosRot2World(DRUser.GrabbingBodyPart grabbingBodyPart, Vector3 grabPos, Quaternion grabRot, out Vector3 worldPos, out Quaternion worldRot)
    {
        Transform grabTransform = GrabbingBodyPart2Transform(grabbingBodyPart);
        if(grabTransform == null)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;
            Debug.LogError("No grab transform for RelativePosRot2World " + grabbingBodyPart + " user " + _playerDisplay.DRUserObj.ID);
            return;
        }

        // We temporarily move the grabber transform to where it ideally
        // would be, because that's how it's sent
        bool hasRevert = false;
        Vector3 revertPos = grabTransform.localPosition;
        Quaternion revertRot = grabTransform.localRotation;
        switch (grabbingBodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                grabTransform.localPosition = HeadPosition;
                grabTransform.localRotation = HeadRotation;
                hasRevert = true;
                break;
            case DRUser.GrabbingBodyPart.LeftHand:
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                grabTransform.localPosition = HandPositionL;
                grabTransform.localRotation = HandRotationL;
                hasRevert = true;
                break;
            case DRUser.GrabbingBodyPart.RightHand:
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                grabTransform.localPosition = HandPositionR;
                grabTransform.localRotation = HandRotationR;
                hasRevert = true;
                break;
        }
        worldPos = grabTransform.TransformPoint(grabPos);
        worldRot = grabTransform.rotation * grabRot;

        if (hasRevert)
        {
            grabTransform.localPosition = revertPos;
            grabTransform.localRotation = revertRot;
        }
    }
    public void WorldPosRot2GrabRelative(DRUser.GrabbingBodyPart grabbingBodyPart, Vector3 worldPos, Quaternion worldRot, out Vector3 grabPos, out Quaternion grabRot)
    {
        if (!_isLocal)
            Debug.LogError("WorldPosRot2Grab is only for local users!");

        Transform grabTransform = GrabbingBodyPart2Transform(grabbingBodyPart);
        if(grabTransform == null)
        {
            grabPos = Vector3.zero;
            grabRot = Quaternion.identity;
            Debug.LogError("No grab transform for RelativePosRot2World " + grabbingBodyPart + " user " + _playerDisplay.DRUserObj.ID);
            return;
        }

        bool hasRevert = false;
        Vector3 revertPos = Vector3.zero;
        Quaternion revertRot = Quaternion.identity;
        // If this is a hand, we get a more up-to-date position/rotation
        if(grabbingBodyPart != DRUser.GrabbingBodyPart.Body
            && grabbingBodyPart != DRUser.GrabbingBodyPart.None)
        {
            revertPos = grabTransform.localPosition;
            revertRot = grabTransform.localRotation;
            hasRevert = true;
            Vector3 trackingSpacePos = TrackingSpace.Instance == null ? Vector3.zero : TrackingSpace.Instance.transform.localPosition;
            ControllerAbstraction.ControllerType controllerType;
            switch (grabbingBodyPart)
            {
                case DRUser.GrabbingBodyPart.LeftHand:
                case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    controllerType = ControllerAbstraction.ControllerType.LEFTHAND;
                    break;
                case DRUser.GrabbingBodyPart.RightHand:
                case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                    controllerType = ControllerAbstraction.ControllerType.RIGHTHAND;
                    break;
                case DRUser.GrabbingBodyPart.Head:
                default:
                    controllerType = ControllerAbstraction.ControllerType.HEAD;
                    break;
            }
            GetLocalPosRotForBodyPart(controllerType, trackingSpacePos, out Vector3 handPos, out Quaternion handRot);
            grabTransform.localPosition = handPos;
            grabTransform.localRotation = handRot;
        }

        grabPos = grabTransform.InverseTransformPoint(worldPos);
        grabRot = Quaternion.Inverse(grabTransform.rotation) * worldRot;

        if (hasRevert)
        {
            grabTransform.localPosition = revertPos;
            grabTransform.localRotation = revertRot;
        }
    }
    public Transform GrabbingBodyPart2Transform(DRUser.GrabbingBodyPart grabbingBodyPart)
    {
        if (_playerDisplay.PossessedBehavior == null)
            return null;
        switch (grabbingBodyPart)
        {
            case DRUser.GrabbingBodyPart.LeftHand:
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                return LHandTransform;
            case DRUser.GrabbingBodyPart.RightHand:
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                return RHandTransform;
            case DRUser.GrabbingBodyPart.Body:
                return transform;//TODO is this right?
            case DRUser.GrabbingBodyPart.Head:
                return HeadTransform;
            case DRUser.GrabbingBodyPart.None:
            default:
                return null;
        }
    }
    public void ApplyNewPoseDataFromNetwork(DRUserPose.PoseInfo poseInfo)
    {
        _numPosesRecv++;
        _previousPose = CurrentPose;
        CurrentPose = poseInfo;
        _timeOfRecvCurrentPose = GetCurrentTime();
        // If hand availability changes, refresh
        if (_previousPose.HasLHand != CurrentPose.HasLHand || _previousPose.HasRHand != CurrentPose.HasRHand)
        {
            if(_playerDisplay.PossessedBehavior != null)
                _playerDisplay.PossessedBehavior.NetworkRefreshVRIKWeights(CurrentPose);
        }
        // If we had to extrapolate last frame, then we move the clock forward for this
        // sample, so that the interpolation is a bit smoother
        //if(_lastLerpAmount > 0.9f && _lastLerpAmount < 2f)
        //{
        //Debug.LogWarning("Changing clock by " + TimeManager.Instance.RenderUnscaledDeltaTime);
        //_timeOfRecvCurrentPose -= TimeManager.Instance.RenderUnscaledDeltaTime;
        //}
        //Debug.Log("---- New sample -----");
    }
    float GetCurrentTime()
    {
        // Recorded users use the render time, because they should not have their poses
        // updated when paused. But local and real network players should have their
        // poses updated
        if (!_isLocal && DrUser.TypeOfUser == DRUser.UserType.Recorded)
            return TimeManager.Instance.RenderTime;
        return Time.realtimeSinceStartup;
    }
    private bool GetLocalPosRotForBodyPart(ControllerAbstraction.ControllerType controllerType, Vector3 trackingSpacePos, out Vector3 localPos, out Quaternion localRot)
    {
        ControllerAbstraction controller;
        HandPoser handPoser;
        switch (controllerType)
        {
            case ControllerAbstraction.ControllerType.HEAD:
                ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.HEAD].GetLocalPositionRotation(out localPos, out localRot);
                //Debug.Log("headPos " + localPos + " tracking " + trackingSpacePos);
                HeadPosition = localPos + trackingSpacePos;
                HeadRotation = localRot;
                return true;
            case ControllerAbstraction.ControllerType.LEFTHAND:
            case ControllerAbstraction.ControllerType.RIGHTHAND:
                controller = ControllerAbstraction.Instances[(int)controllerType];
                handPoser = HandPoser.Instances[(int)controllerType];
                controller.GetLocalPositionRotation(out localPos, out localRot);
                localPos = localPos + trackingSpacePos + localRot * handPoser.WristAttach.localPosition;
                localRot = localRot * handPoser.WristAttach.localRotation;
                return controller.IsConnected;
        }
        localPos = Vector3.zero;
        localRot = Quaternion.identity;
        return false;
    }
    public void LocalUpdate()
    {
        // Move our models to the correct position
        Vector3 trackingSpacePos = TrackingSpace.Instance == null ? Vector3.zero : TrackingSpace.Instance.transform.localPosition;

        // Get the position of both hands relative to the User obj, accounting for the position/rotation of the wrist
        GetLocalPosRotForBodyPart(ControllerAbstraction.ControllerType.HEAD, trackingSpacePos, out HeadPosition, out HeadRotation);
        HasHandL = GetLocalPosRotForBodyPart(ControllerAbstraction.ControllerType.LEFTHAND, trackingSpacePos, out HandPositionL, out HandRotationL);
        HasHandR = GetLocalPosRotForBodyPart(ControllerAbstraction.ControllerType.RIGHTHAND, trackingSpacePos, out HandPositionR, out HandRotationR);

        // Update the previous positions/rotations
        // we need this for the velocity calculation
        PrevHeadPosition = transform.TransformPoint(HeadPosition);
        PrevHeadRotation = transform.rotation * HeadRotation;
        PrevHandPositionL = transform.TransformPoint(HandPositionL);
        PrevHandRotationL = transform.rotation * HandRotationL;
        PrevHandPositionR = transform.TransformPoint(HandPositionR);
        PrevHandRotationR = transform.rotation * HandRotationR;
        PrevHasHandL = HasHandL;
        PrevHasHandR = HasHandR;
        _tickOnLastUpdate = TimeManager.Instance.HighResolutionClock;

        if (BuildPlayManager.Instance.IsSpawnedInPlayMode)
        {
            if(_playerDisplay.PossessedBehavior != null)
                _playerDisplay.PossessedBehavior.UpdateAnimation();
        }
        HandDisplay.UpdateHandsAndGrabbedObjects();
    }
    public void PossiblyUploadNewPose()
    {
        if (GetCurrentTime() < _lastSendTime + 1f / RealtimeNetworkUpdater.Instance.PoseSendRateHz)
            return;
        //Debug.Log("Sending out new pose info");
        _lastSendTime = GetCurrentTime();

        DRUserPose.PoseInfo poseInfo = new DRUserPose.PoseInfo()
        {
            Origin = Vector3.zero,
            HeadPos = HeadPosition,
            LHandPos = HandPositionL,
            RHandPos = HandPositionR,
            HasLHand = HasHandL,
            HasRHand = HasHandR,
            HeadRot = HeadRotation,
            LHandRot = HandRotationL,
            RHandRot = HandRotationR,
        };
        DrUser.UserPose.UpdateFrom(poseInfo);
        DarkRiftWriter writer = DarkRiftWriter.Create();
        DrUser.UserPose.Serialize(writer, out byte tag);
        RealtimeNetworkUpdater.Instance.EnqueueUnreliableUpdate(this, writer, tag, RealtimeNetworkUpdater.Instance.UserPosePriority);
    }
    void OnDestroy()
    {
        if (_isLocal)
        {
            ControllerAbstraction.OnObjectsEnqueueNetworkMessages -= PossiblyUploadNewPose;
            ControllerAbstraction.OnControllerConnected -= LocalRefreshVRIKWeights;
            ControllerAbstraction.OnControllerDisconnected -= LocalRefreshVRIKWeights;
        }
    }
    private void LocalRefreshVRIKWeights(ControllerAbstraction.ControllerType junk)
    {

    }
    public bool GetPreviousPoseWorld(DRUser.GrabbingBodyPart bodyPart, out Vector3 prevPosition, out Quaternion prevRotation, out float dt)
    {
        // If we're non-local, the dt is just the render delta
        // This is because that's when the pose is updated
        if (!_isLocal)
            dt = TimeManager.Instance.RenderUnscaledDeltaTime;
        else
        {
            // For local, we use the ticks
            if (_tickOnLastUpdate < 0)
                dt = TimeManager.Instance.RenderUnscaledDeltaTime;
            else
            {
                dt = TimeManager.Instance.SecondsSince(_tickOnLastUpdate);
                //dt = TimeManager.Instance.RenderUnscaledDeltaTime;
            }
        }
        switch (bodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                prevPosition = PrevHeadPosition;
                prevRotation = PrevHeadRotation;
                return true;
            case DRUser.GrabbingBodyPart.LeftHand:
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                prevPosition = PrevHandPositionL;
                prevRotation = PrevHandRotationL;
                return true;
            case DRUser.GrabbingBodyPart.RightHand:
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                //Debug.Log("Prev r hand pos " + PrevHandPositionR + " #" + Time.frameCount);
                prevPosition = PrevHandPositionR;
                prevRotation = PrevHandRotationR;
                return true;
            case DRUser.GrabbingBodyPart.Body:
                //TODO
                prevPosition = Vector3.zero;
                prevRotation = Quaternion.identity;
                break;
            case DRUser.GrabbingBodyPart.None:
            default:
                prevPosition = Vector3.zero;
                prevRotation = Quaternion.identity;
                return false;
        }
        return false;
    }
    public void UpdatePose()
    {
        if (!_hasInit)
        {
            Debug.LogError("Pose update without init");
            return;
        }
        // Local clients update in the PoseUpdate callback
        if (_isLocal)
            return;

        // Don't update poses for network users
        // who are recorded
        if (DrUser.TypeOfUser == DRUser.UserType.Recorded
            && !TimeManager.Instance.IsPlayingOrStepped)
            return;

        // Store the previous pose positions in world space
        // we need this so that we can properly calculate velocities
        // for objects
        PrevHeadPosition = transform.TransformPoint(HeadPosition);
        PrevHeadRotation = transform.rotation * HeadRotation;
        PrevHandPositionL = transform.TransformPoint(HandPositionL);
        PrevHandRotationL = transform.rotation * HandRotationL;
        PrevHandPositionR = transform.TransformPoint(HandPositionR);
        PrevHandRotationR = transform.rotation * HandRotationR;
        PrevHasHandL = HasHandL;
        PrevHasHandR = HasHandR;
        //Debug.Log("updating prev position " + Time.frameCount);

        // For network avatars, lerp positions as needed
        // DarkRiftConnection has it's update run early, before
        // this function, so we should always have up to date
        // info here
        float sendInterval = 1f / RealtimeNetworkUpdater.Instance.PoseSendRateHz;
        float lerpAmount = 1f + (GetCurrentTime() - _timeOfRecvCurrentPose) / sendInterval;
        // If we have less than two samples, don't bother lerping
        if (_numPosesRecv < 2)
            lerpAmount = 1f;
        else if (lerpAmount >= 2.5f)
            lerpAmount = 1f; // Don't extrapolate too far in the future. Just extrapolate one extra send interval
        //Debug.Log(lerpAmount + " delta " + (GetCurrentTime() - _timeOfRecvCurrentPose) + " send interval " + sendInterval);
        _lastLerpAmount = lerpAmount;
        HeadPosition = Vector3.LerpUnclamped(_previousPose.HeadPos, CurrentPose.HeadPos, lerpAmount);
        HeadRotation = Quaternion.SlerpUnclamped(_previousPose.HeadRot, CurrentPose.HeadRot, lerpAmount);

        if(_previousPose.HasLHand && CurrentPose.HasLHand)
        {
            HandPositionL = Vector3.LerpUnclamped(_previousPose.LHandPos, CurrentPose.LHandPos, lerpAmount);
            HandRotationL = Quaternion.SlerpUnclamped(_previousPose.LHandRot, CurrentPose.LHandRot, lerpAmount);
            HasHandL = true;
        }else if (CurrentPose.HasLHand)
        {
            HandPositionL = CurrentPose.LHandPos;
            HandRotationL = CurrentPose.LHandRot;
            HasHandL = true;
        }
        else
        {
            HandPositionL = Vector3.zero;
            HandRotationL = Quaternion.identity;
            HasHandL = false;
        }
        if(_previousPose.HasRHand && CurrentPose.HasRHand)
        {
            HandPositionR = Vector3.LerpUnclamped(_previousPose.RHandPos, CurrentPose.RHandPos, lerpAmount);
            HandRotationR = Quaternion.SlerpUnclamped(_previousPose.RHandRot, CurrentPose.RHandRot, lerpAmount);
            HasHandR = true;
        }else if (CurrentPose.HasRHand)
        {
            HandPositionR = CurrentPose.RHandPos;
            HandRotationR = CurrentPose.RHandRot;
            HasHandR = true;
        }
        else
        {
            HandPositionR = Vector3.zero;
            HandRotationR = Quaternion.identity;
            HasHandR = false;
        }
        // We now have the transform updated from the network, let's run the IK
        if (DrUser.IsInPlayMode)
        {
            if(_playerDisplay.PossessedBehavior != null)
                _playerDisplay.PossessedBehavior.UpdateAnimation();
            //_vrIk.FullExternalSolve();
        }
        HandDisplay.UpdateHandsAndGrabbedObjects();
    }
    public bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority)
    {
        tag = 0;
        priority = 0;
        return false;
    }
    public void ClearPriority()
    {
    }
}
