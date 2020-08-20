//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR;

public class HandPoser : MonoBehaviour
{
    public static HandPoser[] Instances = new HandPoser[3];

    [Tooltip("If not set, will try to auto assign this based on 'Skeleton' + inputSource")]
    /// <summary>The action this component will use to update the model. Must be a Skeleton type action.</summary>
    public SteamVR_Action_Skeleton skeletonAction;

    /// <summary>The device this action should apply to. Any if the action is not device specific.</summary>
    [Tooltip("The device this action should apply to. Any if the action is not device specific.")]
    public SteamVR_Input_Sources InputSource;
    public ControllerAbstraction.ControllerType MyControllerType;

    /// <summary>The range of motion you'd like the hand to move in. With controller is the best estimate of the fingers wrapped around a controller. Without is from a flat hand to a fist.</summary>
    [Tooltip("The range of motion you'd like the hand to move in. With controller is the best estimate of the fingers wrapped around a controller. Without is from a flat hand to a fist.")]
    public EVRSkeletalMotionRange rangeOfMotion = EVRSkeletalMotionRange.WithoutController;

    /// <summary>The root Transform of the skeleton. Needs to have a child (wrist) then wrist should have children in the order thumb, index, middle, ring, pinky</summary>
    [Tooltip("This needs to be in the order of: root -> wrist -> thumb, index, middle, ring, pinky")]
    public Transform skeletonRoot;
    public Transform WristAttach;
    public Transform IndexAttach;

    public Transform HandModel;

    /// <summary>Check this to not set the positions of the bones. This is helpful for differently scaled skeletons.</summary>
    [Tooltip("Check this to not set the positions of the bones. This is helpful for differently scaled skeletons.")]
    public bool onlySetRotations = false;

    /// <summary>
    /// How much of a blend to apply to the transform positions and rotations.
    /// Set to 0 for the transform orientation to be set by an animation.
    /// Set to 1 for the transform orientation to be set by the skeleton action.
    /// </summary>
    [Range(0, 1)]
    [Tooltip("Modify this to blend between animations setup on the hand")]
    public float _percentBlendDefault = 1f;

    /// <summary>Can be set to mirror the bone data across the x axis</summary>
    public MirrorType mirroring;

    [Tooltip("The fallback SkeletonPoser to drive hand animation when no skeleton data is available")]
    /// <summary>The fallback SkeletonPoser to drive hand animation when no skeleton data is available</summary>
    public SteamVR_Skeleton_Poser fallbackPoser;

    [Tooltip("The fallback action to drive finger curl values when no skeleton data is available")]
    /// <summary>The fallback SkeletonPoser to drive hand animation when no skeleton data is available</summary>
    public SteamVR_Action_Single fallbackCurlAction;

    public OculusHandPoser OculusPoser;

    /// <summary>
    /// Is the skeleton action bound?
    /// </summary>
    public bool skeletonAvailable { get { return skeletonAction != null && skeletonAction.activeBinding; } }

    /// <summary>The current skeletonPoser we're getting pose data from</summary>
    protected SteamVR_Skeleton_Poser _currentGrabbedPoser;
    /// <summary>The current pose snapshot</summary>
    protected SteamVR_Skeleton_PoseSnapshot _currentGrabbedPoseSnapshot = null;

    /// <summary>Returns whether this action is bound and the action set is active</summary>
    public bool isActive { get { return skeletonAction.GetActive(); } }

    #region hand_parts
    /// <summary>An array of five 0-1 values representing how curled a finger is. 0 being straight, 1 being fully curled. 0 being thumb, 4 being pinky</summary>
    public float[] fingerCurls
    {
        get
        {
            if (skeletonAvailable)
            {
                return skeletonAction.GetFingerCurls();
            }
            else
            {
                //fallback, return array where each finger curl is just the fallback curl action value
                float[] curls = new float[5];
                for (int i = 0; i < 5; i++)
                {
                    curls[i] = fallbackCurlAction.GetAxis(InputSource);
                }
                return curls;
            }
        }
    }
    /// <summary>An 0-1 value representing how curled a finger is. 0 being straight, 1 being fully curled.</summary>
    public float thumbCurl
    {
        get
        {
            if (skeletonAvailable)
                return skeletonAction.GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum.thumb);
            else
                return fallbackCurlAction.GetAxis(InputSource);
        }
    }
    /// <summary>An 0-1 value representing how curled a finger is. 0 being straight, 1 being fully curled.</summary>
    public float indexCurl
    {
        get
        {
            if (skeletonAvailable)
                return skeletonAction.GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum.index);
            else
                return fallbackCurlAction.GetAxis(InputSource);
        }
    }
    /// <summary>An 0-1 value representing how curled a finger is. 0 being straight, 1 being fully curled.</summary>
    public float middleCurl
    {
        get
        {
            if (skeletonAvailable)
                return skeletonAction.GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum.middle);
            else
                return fallbackCurlAction.GetAxis(InputSource);
        }
    }
    /// <summary>An 0-1 value representing how curled a finger is. 0 being straight, 1 being fully curled.</summary>
    public float ringCurl
    {
        get
        {
            if (skeletonAvailable)
                return skeletonAction.GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum.ring);
            else
                return fallbackCurlAction.GetAxis(InputSource);
        }
    }
    /// <summary>An 0-1 value representing how curled a finger is. 0 being straight, 1 being fully curled.</summary>
    public float pinkyCurl
    {
        get
        {
            if (skeletonAvailable)
                return skeletonAction.GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum.pinky);
            else
                return fallbackCurlAction.GetAxis(InputSource);
        }
    }
    public Transform root { get { return bones[SteamVR_Skeleton_JointIndexes.root]; } }
    public Transform wrist { get { return bones[SteamVR_Skeleton_JointIndexes.wrist]; } }
    public Transform indexMetacarpal { get { return bones[SteamVR_Skeleton_JointIndexes.indexMetacarpal]; } }
    public Transform indexProximal { get { return bones[SteamVR_Skeleton_JointIndexes.indexProximal]; } }
    public Transform indexMiddle { get { return bones[SteamVR_Skeleton_JointIndexes.indexMiddle]; } }
    public Transform indexDistal { get { return bones[SteamVR_Skeleton_JointIndexes.indexDistal]; } }
    public Transform indexTip { get { return bones[SteamVR_Skeleton_JointIndexes.indexTip]; } }
    public Transform middleMetacarpal { get { return bones[SteamVR_Skeleton_JointIndexes.middleMetacarpal]; } }
    public Transform middleProximal { get { return bones[SteamVR_Skeleton_JointIndexes.middleProximal]; } }
    public Transform middleMiddle { get { return bones[SteamVR_Skeleton_JointIndexes.middleMiddle]; } }
    public Transform middleDistal { get { return bones[SteamVR_Skeleton_JointIndexes.middleDistal]; } }
    public Transform middleTip { get { return bones[SteamVR_Skeleton_JointIndexes.middleTip]; } }
    public Transform pinkyMetacarpal { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyMetacarpal]; } }
    public Transform pinkyProximal { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyProximal]; } }
    public Transform pinkyMiddle { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyMiddle]; } }
    public Transform pinkyDistal { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyDistal]; } }
    public Transform pinkyTip { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyTip]; } }
    public Transform ringMetacarpal { get { return bones[SteamVR_Skeleton_JointIndexes.ringMetacarpal]; } }
    public Transform ringProximal { get { return bones[SteamVR_Skeleton_JointIndexes.ringProximal]; } }
    public Transform ringMiddle { get { return bones[SteamVR_Skeleton_JointIndexes.ringMiddle]; } }
    public Transform ringDistal { get { return bones[SteamVR_Skeleton_JointIndexes.ringDistal]; } }
    public Transform ringTip { get { return bones[SteamVR_Skeleton_JointIndexes.ringTip]; } }
    public Transform thumbMetacarpal { get { return bones[SteamVR_Skeleton_JointIndexes.thumbMetacarpal]; } } //doesn't exist - mapped to proximal
    public Transform thumbProximal { get { return bones[SteamVR_Skeleton_JointIndexes.thumbProximal]; } }
    public Transform thumbMiddle { get { return bones[SteamVR_Skeleton_JointIndexes.thumbMiddle]; } }
    public Transform thumbDistal { get { return bones[SteamVR_Skeleton_JointIndexes.thumbDistal]; } }
    public Transform thumbTip { get { return bones[SteamVR_Skeleton_JointIndexes.thumbTip]; } }
    public Transform thumbAux { get { return bones[SteamVR_Skeleton_JointIndexes.thumbAux]; } }
    public Transform indexAux { get { return bones[SteamVR_Skeleton_JointIndexes.indexAux]; } }
    public Transform middleAux { get { return bones[SteamVR_Skeleton_JointIndexes.middleAux]; } }
    public Transform ringAux { get { return bones[SteamVR_Skeleton_JointIndexes.ringAux]; } }
    public Transform pinkyAux { get { return bones[SteamVR_Skeleton_JointIndexes.pinkyAux]; } }
    /// <summary>An array of all the finger proximal joint transforms</summary>
    public Transform[] proximals { get; protected set; }
    /// <summary>An array of all the finger middle joint transforms</summary>
    public Transform[] middles { get; protected set; }
    /// <summary>An array of all the finger distal joint transforms</summary>
    public Transform[] distals { get; protected set; }
    /// <summary>An array of all the finger tip transforms</summary>
    public Transform[] tips { get; protected set; }
    /// <summary>An array of all the finger aux transforms</summary>
    public Transform[] auxs { get; protected set; }
    #endregion

    protected Coroutine blendRoutine;
    protected Coroutine rangeOfMotionBlendRoutine;
    protected Coroutine attachRoutine;
    protected Transform[] bones;
    private bool _didSubscribe = false;

    /// <summary>
    /// Get the accuracy level of the skeletal tracking data.
    /// <para/>* Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads.
    /// <para/>* Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl
    /// <para/>* Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.
    /// </summary>
    public EVRSkeletalTrackingLevel skeletalTrackingLevel
    {
        get
        {
            if (skeletonAvailable)
            {
                return skeletonAction.skeletalTrackingLevel;
            }
            else
            {
                return EVRSkeletalTrackingLevel.VRSkeletalTracking_Estimated;
            }
        }
    }

    /// <summary>Returns true if we are in the process of blending the skeletonBlend field (between animation and bone data)</summary>
    public bool isBlending
    {
        get
        {
            return blendRoutine != null;
        }
    }

    public SteamVR_ActionSet actionSet
    {
        get
        {
            return skeletonAction.actionSet;
        }
    }

    public SteamVR_ActionDirections direction
    {
        get
        {
            return skeletonAction.direction;
        }
    }

    protected virtual void Awake()
    {
        if(Instances[(int)MyControllerType] != null)
        {
            Debug.LogError("Destroying HandPoser", Instances[(int)MyControllerType]);
            Destroy(Instances[(int)MyControllerType]);
        }
        Instances[(int)MyControllerType] = this;

        AssignBonesArray();

        proximals = new Transform[] { thumbProximal, indexProximal, middleProximal, ringProximal, pinkyProximal };
        middles = new Transform[] { thumbMiddle, indexMiddle, middleMiddle, ringMiddle, pinkyMiddle };
        distals = new Transform[] { thumbDistal, indexDistal, middleDistal, ringDistal, pinkyDistal };
        tips = new Transform[] { thumbTip, indexTip, middleTip, ringTip, pinkyTip };
        auxs = new Transform[] { thumbAux, indexAux, middleAux, ringAux, pinkyAux };

        VRSDKUtils.OnVRModeChanged += OnVRModeChange;
    }
    private void OnVRModeChange()
    {
        if(VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
        {
            if (_didSubscribe)
            {
                //Debug.LogWarning("UNSUB");
                ControllerAbstraction.Instances[(int)MyControllerType].OnGrabbedSomethingNew -= SetGrabbedObject;
                ControllerAbstraction.OnControllerPoseUpdate_General -= UpdateSkeleton;
                HandModel.gameObject.SetActive(false);
                _didSubscribe = false;
                return;
            }
            Debug.LogError("Changed VR mode to desktop, but didn't unsubscribe?");
        }
        else
        {
            if (!_didSubscribe)
            {
                //Debug.LogWarning("SUB");
                ControllerAbstraction.Instances[(int)MyControllerType].OnGrabbedSomethingNew += SetGrabbedObject;
                ControllerAbstraction.OnControllerPoseUpdate_General += UpdateSkeleton;
                HandModel.gameObject.SetActive(true);
                _didSubscribe = true;
            }
        }
    }
    protected virtual void CheckSkeletonAction()
    {
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.OpenVR
            && skeletonAction == null)
            skeletonAction = SteamVR_Input.GetAction<SteamVR_Action_Skeleton>("Skeleton" + InputSource.ToString());
    }
    protected virtual void AssignBonesArray()
    {
        bones = skeletonRoot.GetComponentsInChildren<Transform>();
    }
    protected virtual void OnEnable()
    {
        CheckSkeletonAction();
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
        {
            HandModel.gameObject.SetActive(false);
            return;
        }
        if (!_didSubscribe)
        {
            //Debug.LogWarning("SUB");
            HandModel.gameObject.SetActive(true);
            ControllerAbstraction.Instances[(int)MyControllerType].OnGrabbedSomethingNew += SetGrabbedObject;
            ControllerAbstraction.OnControllerPoseUpdate_General += UpdateSkeleton;
            _didSubscribe = true;
        }

    }

    protected virtual void OnDisable()
    {
        if (_didSubscribe)
        {
            //Debug.LogWarning("UNSUB");
            ControllerAbstraction.Instances[(int)MyControllerType].OnGrabbedSomethingNew -= SetGrabbedObject;
            ControllerAbstraction.OnControllerPoseUpdate_General -= UpdateSkeleton;
            HandModel.gameObject.SetActive(false);
            _didSubscribe = false;
        }
    }

    protected virtual void UpdateSkeleton()
    {
        // NB HandPosers are moved by UserHandGrabbedDisplay!
        if (skeletonAction == null && VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.OpenVR)
        {
            Debug.LogWarning("Waiting on skeleton action");
            return;
        }

        // Move the WristAttach/IndexAttach to the internal wrist/index
        // We have to do this because the model itself can have negative scale,
        // and we want to avoid that
        WristAttach.position = wrist.position;
        WristAttach.rotation = wrist.rotation;
        //IndexAttach.position = indexMetacarpal.position;
        // Index bone is facing the wrong way, so we reorient it here
        //IndexAttach.rotation = indexMetacarpal.rotation *
        //(MyControllerType == ControllerAbstraction.ControllerType.LEFTHAND ? Quaternion.Euler(0, -90f, 0) : Quaternion.Euler(0, 90f, 0));


        if (rangeOfMotionBlendRoutine == null)
        {
            if(skeletonAction != null)
                skeletonAction.SetRangeOfMotion(rangeOfMotion); //this may be a frame behind
            UpdateSkeletonTransforms();
        }
    }
    public void GetLocalPositionAndRotation(out Vector3 localPos, out Quaternion localRot)
    {
        localPos = TrackingSpace.Instance.transform.InverseTransformPoint(WristAttach.position);
        localRot = Quaternion.Inverse(TrackingSpace.Instance.transform.rotation) * WristAttach.rotation;
    }
    protected void SetGrabbedObject(IGrabbable grabbable)
    {
        //Debug.Log("Grabbed object!");
        if(grabbable == null)
        {
            _currentGrabbedPoser = null;
            _currentGrabbedPoseSnapshot = null;
        }
        else
        {
            // Get the hand pose from the object
            BaseGrabbable baseGrabbable = grabbable as BaseGrabbable;
            _currentGrabbedPoser = baseGrabbable == null ? null : baseGrabbable.GetHandPose();
            if (_currentGrabbedPoser == null)
                _currentGrabbedPoseSnapshot = null;
            else
            {
                _currentGrabbedPoseSnapshot = skeletonAction == null
                    ? _currentGrabbedPoser.GetBlendedPoseOculus(InputSource)
                    : _currentGrabbedPoser.GetBlendedPose(skeletonAction, InputSource);
            }
        }
        // Blend our hand to this pose
        _percentBlendDefault =  _currentGrabbedPoser == null ? 1 : 0;
        //Debug.Log("Grabbed " + _currentGrabbedPoser + " now blend amount: " + _percentBlendDefault);
    }
    public virtual void UpdateSkeletonTransforms()
    {
        Vector3[] bonePositions;
        Quaternion[] boneRotations;
        GetBonePositionRotations(out bonePositions, out boneRotations);
        if (bonePositions == null || boneRotations == null)
            return;
        //Debug.Log("Hand update #" + Time.frameCount);
        //Debug.Log("Bone " + bonePositions[15].ToPrettyString());
        //Debug.Log("Recv " + boneRotations[SteamVR_Skeleton_JointIndexes.middleProximal].eulerAngles.ToPrettyString() + " curr "
            //+ bones[SteamVR_Skeleton_JointIndexes.middleProximal].localRotation.eulerAngles.ToPrettyString());

        if (_percentBlendDefault <= 0)
        {
            if (_currentGrabbedPoser != null)
            {
                SteamVR_Skeleton_Pose_Hand mainPose = _currentGrabbedPoser.skeletonMainPose.GetHand(InputSource);
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (bones[boneIndex] == null)
                        continue;

                    if ((boneIndex == SteamVR_Skeleton_JointIndexes.wrist && mainPose.ignoreWristPoseData) ||
                        (boneIndex == SteamVR_Skeleton_JointIndexes.root && mainPose.ignoreRootPoseData))
                    {
                        SetBonePosition(boneIndex, bonePositions[boneIndex]);
                        SetBoneRotation(boneIndex, boneRotations[boneIndex]);
                    }
                    else
                    {
                        SetBonePosition(boneIndex, _currentGrabbedPoseSnapshot.bonePositions[boneIndex]);
                        SetBoneRotation(boneIndex, _currentGrabbedPoseSnapshot.boneRotations[boneIndex]);
                    }
                }
            }
            else
            {
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    SetBonePosition(boneIndex, _currentGrabbedPoseSnapshot.bonePositions[boneIndex]);
                    SetBoneRotation(boneIndex, _currentGrabbedPoseSnapshot.boneRotations[boneIndex]);

                }
            }
        }
        else if (_percentBlendDefault >= 1)
        {
            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            {
                if (bones[boneIndex] == null)
                    continue;

                SetBonePosition(boneIndex, bonePositions[boneIndex]);
                SetBoneRotation(boneIndex, boneRotations[boneIndex]);
            }
        }
        else
        {
            return;
            //for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            //{
            //    if (bones[boneIndex] == null)
            //        continue;

            //    if (_currentGrabbedPoser != null)
            //    {
            //        SteamVR_Skeleton_Pose_Hand mainPose = _currentGrabbedPoser.skeletonMainPose.GetHand(InputSource);

            //        if ((boneIndex == SteamVR_Skeleton_JointIndexes.wrist && mainPose.ignoreWristPoseData) ||
            //            (boneIndex == SteamVR_Skeleton_JointIndexes.root && mainPose.ignoreRootPoseData))
            //        {
            //            SetBonePosition(boneIndex, bonePositions[boneIndex]);
            //            SetBoneRotation(boneIndex, boneRotations[boneIndex]);
            //        }
            //        else
            //        {
            //            //Quaternion poseRotation = GetBlendPoseForBone(boneIndex, boneRotations[boneIndex]);

            //            SetBonePosition(boneIndex, Vector3.Lerp(_currentGrabbedPoseSnapshot.bonePositions[boneIndex], bonePositions[boneIndex], _percentBlendDefault));
            //            SetBoneRotation(boneIndex, Quaternion.Lerp(_currentGrabbedPoseSnapshot.boneRotations[boneIndex], boneRotations[boneIndex], _percentBlendDefault));
            //            //SetBoneRotation(boneIndex, GetBlendPoseForBone(boneIndex, boneRotations[boneIndex]));
            //        }
            //    }
            //    else
            //    {
            //        if (_currentGrabbedPoseSnapshot == null)
            //        {
            //            SetBonePosition(boneIndex, Vector3.Lerp(bones[boneIndex].localPosition, bonePositions[boneIndex], _percentBlendDefault));
            //            SetBoneRotation(boneIndex, Quaternion.Lerp(bones[boneIndex].localRotation, boneRotations[boneIndex], _percentBlendDefault));
            //        }
            //        else
            //        {
            //            SetBonePosition(boneIndex, Vector3.Lerp(_currentGrabbedPoseSnapshot.bonePositions[boneIndex], bonePositions[boneIndex], _percentBlendDefault));
            //            SetBoneRotation(boneIndex, Quaternion.Lerp(_currentGrabbedPoseSnapshot.boneRotations[boneIndex], boneRotations[boneIndex], _percentBlendDefault));
            //        }
            //    }
            //}
        }
    }

    /// <summary>
    /// Permanently sets the range of motion for this component.
    /// </summary>
    /// <param name="newRangeOfMotion">
    /// The new range of motion to be set.
    /// WithController being the best estimation of where fingers are wrapped around the controller (pressing buttons, etc).
    /// WithoutController being a range between a flat hand and a fist.</param>
    /// <param name="blendOverSeconds">How long you want the blend to the new range of motion to take (in seconds)</param>
    public void SetRangeOfMotion(EVRSkeletalMotionRange newRangeOfMotion, float blendOverSeconds = 0.1f)
    {
        if (rangeOfMotion != newRangeOfMotion)
        {
            RangeOfMotionBlend(newRangeOfMotion, blendOverSeconds);
        }
    }

    /// <summary>
    /// Blend from the current skeletonBlend amount to full bone data. (skeletonBlend = 1)
    /// </summary>
    /// <param name="overTime">How long you want the blend to take (in seconds)</param>
    public void BlendToSkeleton(float overTime = 0.1f)
    {
        //if (_currentGrabbedPoser != null)
            //_currentGrabbedPoseSnapshot = _currentGrabbedPoser.GetBlendedPose(this);
        _currentGrabbedPoser = null;
        BlendTo(1, overTime);
    }

    /// <summary>
    /// Blend from the current skeletonBlend amount to pose animation. (skeletonBlend = 0)
    /// Note: This will ignore the root position and rotation of the pose.
    /// </summary>
    /// <param name="overTime">How long you want the blend to take (in seconds)</param>
    public void BlendToPoser(SteamVR_Skeleton_Poser poser, float overTime = 0.1f)
    {
        if (poser == null)
            return;

        _currentGrabbedPoser = poser;
        BlendTo(0, overTime);
    }

    /// <summary>
    /// Blend from the current skeletonBlend amount to a specified new amount.
    /// </summary>
    /// <param name="blendToAmount">The amount of blend you want to apply.
    /// 0 being fully set by animations, 1 being fully set by bone data from the action.</param>
    /// <param name="overTime">How long you want the blend to take (in seconds)</param>
    public void BlendTo(float blendToAmount, float overTime)
    {
        if (blendRoutine != null)
            StopCoroutine(blendRoutine);

        if (this.gameObject.activeInHierarchy)
            blendRoutine = StartCoroutine(DoBlendRoutine(blendToAmount, overTime));
    }


    protected IEnumerator DoBlendRoutine(float blendToAmount, float overTime)
    {
        float startTime = TimeManager.Instance.RenderUnscaledTime;
        float endTime = startTime + overTime;

        float startAmount = _percentBlendDefault;

        while (TimeManager.Instance.RenderUnscaledTime < endTime)
        {
            yield return null;
            _percentBlendDefault = Mathf.Lerp(startAmount, blendToAmount, (TimeManager.Instance.RenderUnscaledTime - startTime) / overTime);
        }

        _percentBlendDefault = blendToAmount;
        blendRoutine = null;
    }

    protected void RangeOfMotionBlend(EVRSkeletalMotionRange newRangeOfMotion, float blendOverSeconds)
    {
        if (rangeOfMotionBlendRoutine != null)
            StopCoroutine(rangeOfMotionBlendRoutine);

        EVRSkeletalMotionRange oldRangeOfMotion = rangeOfMotion;
        rangeOfMotion = newRangeOfMotion;

        if (this.gameObject.activeInHierarchy)
        {
            rangeOfMotionBlendRoutine = StartCoroutine(DoRangeOfMotionBlend(oldRangeOfMotion, newRangeOfMotion, blendOverSeconds));
        }
    }

    protected IEnumerator DoRangeOfMotionBlend(EVRSkeletalMotionRange oldRangeOfMotion, EVRSkeletalMotionRange newRangeOfMotion, float overTime)
    {
        float startTime = TimeManager.Instance.RenderUnscaledTime;
        float endTime = startTime + overTime;

        //Vector3[] oldBonePositions;
        //Quaternion[] oldBoneRotations;

        //Vector3[] newBonePositions;
        //Quaternion[] newBoneRotations;

        while (TimeManager.Instance.RenderUnscaledTime < endTime)
        {
            yield return null;
            float lerp = (TimeManager.Instance.RenderUnscaledTime - startTime) / overTime;

            //if (skeletonBlend > 0)
            //{
            //    skeletonAction.SetRangeOfMotion(oldRangeOfMotion);
            //    skeletonAction.UpdateValueWithoutEvents();
            //    oldBonePositions = (Vector3[])GetBonePositions().Clone();
            //    oldBoneRotations = (Quaternion[])GetBoneRotations().Clone();

            //    skeletonAction.SetRangeOfMotion(newRangeOfMotion);
            //    skeletonAction.UpdateValueWithoutEvents();
            //    newBonePositions = GetBonePositions();
            //    newBoneRotations = GetBoneRotations();

            //    for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            //    {
            //        if (bones[boneIndex] == null)
            //            continue;

            //        if (SteamVR_Utils.IsValid(newBoneRotations[boneIndex]) == false || SteamVR_Utils.IsValid(oldBoneRotations[boneIndex]) == false)
            //        {
            //            continue;
            //        }

            //        Vector3 blendedRangeOfMotionPosition = Vector3.Lerp(oldBonePositions[boneIndex], newBonePositions[boneIndex], lerp);
            //        Quaternion blendedRangeOfMotionRotation = Quaternion.Lerp(oldBoneRotations[boneIndex], newBoneRotations[boneIndex], lerp);

            //        if (skeletonBlend < 1)
            //        {
            //            if (blendPoser != null)
            //            {

            //                SetBonePosition(boneIndex, Vector3.Lerp(blendSnapshot.bonePositions[boneIndex], blendedRangeOfMotionPosition, skeletonBlend));
            //                SetBoneRotation(boneIndex, Quaternion.Lerp(GetBlendPoseForBone(boneIndex, blendedRangeOfMotionRotation), blendedRangeOfMotionRotation, skeletonBlend));
            //            }
            //            else
            //            {
            //                SetBonePosition(boneIndex, Vector3.Lerp(bones[boneIndex].localPosition, blendedRangeOfMotionPosition, skeletonBlend));
            //                SetBoneRotation(boneIndex, Quaternion.Lerp(bones[boneIndex].localRotation, blendedRangeOfMotionRotation, skeletonBlend));
            //            }
            //        }
            //        else
            //        {
            //            SetBonePosition(boneIndex, blendedRangeOfMotionPosition);
            //            SetBoneRotation(boneIndex, blendedRangeOfMotionRotation);
            //        }
            //    }
            //}
        }

        rangeOfMotionBlendRoutine = null;
    }


    public virtual void SetBonePosition(int boneIndex, Vector3 localPosition)
    {
        if (onlySetRotations == false) //ignore position sets if we're only setting rotations
            bones[boneIndex].localPosition = localPosition;
    }

    public virtual void SetBoneRotation(int boneIndex, Quaternion localRotation)
    {
        bones[boneIndex].localRotation = localRotation;
    }

    /// <summary>
    /// Gets the transform for a bone by the joint index. Joint indexes specified in: SteamVR_Skeleton_JointIndexes
    /// </summary>
    /// <param name="joint">The joint index of the bone. Specified in SteamVR_Skeleton_JointIndexes</param>
    public virtual Transform GetBone(int joint)
    {
        if (bones == null || bones.Length == 0)
            Awake();

        return bones[joint];
    }

    /// <summary>
    /// Gets the position of the transform for a bone by the joint index. Joint indexes specified in: SteamVR_Skeleton_JointIndexes
    /// </summary>
    /// <param name="joint">The joint index of the bone. Specified in SteamVR_Skeleton_JointIndexes</param>
    /// <param name="local">true to get the localspace position for the joint (position relative to this joint's parent)</param>
    public Vector3 GetBonePosition(int joint, bool local = false)
    {
        if (local)
            return bones[joint].localPosition;
        else
            return bones[joint].position;
    }

    /// <summary>
    /// Gets the rotation of the transform for a bone by the joint index. Joint indexes specified in: SteamVR_Skeleton_JointIndexes
    /// </summary>
    /// <param name="joint">The joint index of the bone. Specified in SteamVR_Skeleton_JointIndexes</param>
    /// <param name="local">true to get the localspace rotation for the joint (rotation relative to this joint's parent)</param>
    public Quaternion GetBoneRotation(int joint, bool local = false)
    {
        if (local)
            return bones[joint].localRotation;
        else
            return bones[joint].rotation;
    }

    protected void GetBonePositionRotations(out Vector3[] positions, out Quaternion[] rotations)
    {
        if (OculusPoser != null && VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Oculus)
            OculusPoser.GetBonePositionRotations(HandModel, out positions, out rotations);
        else if (skeletonAvailable)
        {
            positions = skeletonAction.GetBonePositions();
            rotations = skeletonAction.GetBoneRotations();
        }
        else
        {
            if (skeletonAction == null)
            {
                positions = null;
                rotations = null;
                return;
            }
            positions = null;
            rotations = null;
            // This causes null exceptions for some reason
            //fallback to getting skeleton pose from skeletonPoser
            //var pose = fallbackPoser.GetBlendedPose(skeletonAction, InputSource);
            //positions = pose.bonePositions;
            //rotations = pose.boneRotations;
            return;
        }

        // Mirror as needed
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.OpenVR
            && (mirroring == MirrorType.LeftToRight || mirroring == MirrorType.RightToLeft))
        {
            for (int boneIndex = 0; boneIndex < positions.Length; boneIndex++)
            {
                positions[boneIndex] = MirrorPosition(boneIndex, positions[boneIndex]);
                rotations[boneIndex] = MirrorRotation(boneIndex, rotations[boneIndex]);
            }
        }
    }
    protected static readonly Quaternion rightFlipAngle = Quaternion.AngleAxis(180, Vector3.right);

    public static Vector3 MirrorPosition(int boneIndex, Vector3 rawPosition)
    {
        if (boneIndex == SteamVR_Skeleton_JointIndexes.wrist || IsMetacarpal(boneIndex))
        {
            rawPosition.Scale(new Vector3(-1, 1, 1));
        }
        else if (boneIndex != SteamVR_Skeleton_JointIndexes.root)
        {
            rawPosition = rawPosition * -1;
        }

        return rawPosition;
    }

    public static Quaternion MirrorRotation(int boneIndex, Quaternion rawRotation)
    {
        if (boneIndex == SteamVR_Skeleton_JointIndexes.wrist)
        {
            rawRotation.y = rawRotation.y * -1;
            rawRotation.z = rawRotation.z * -1;
        }

        if (IsMetacarpal(boneIndex))
        {
            rawRotation = rightFlipAngle * rawRotation;
        }

        return rawRotation;
    }

    protected virtual void UpdatePose()
    {
        if (skeletonAction == null)
            return;

        Vector3 skeletonPosition = skeletonAction.GetLocalPosition();
        Quaternion skeletonRotation = skeletonAction.GetLocalRotation();
        if (this.transform.parent != null)
        {
            skeletonPosition = this.transform.parent.TransformPoint(skeletonPosition);
            skeletonRotation = this.transform.parent.rotation * skeletonRotation;
        }
        this.transform.position = skeletonPosition;
        this.transform.rotation = skeletonRotation;
    }

    protected static bool IsMetacarpal(int boneIndex)
    {
        return (boneIndex == SteamVR_Skeleton_JointIndexes.indexMetacarpal ||
            boneIndex == SteamVR_Skeleton_JointIndexes.middleMetacarpal ||
            boneIndex == SteamVR_Skeleton_JointIndexes.ringMetacarpal ||
            boneIndex == SteamVR_Skeleton_JointIndexes.pinkyMetacarpal ||
            boneIndex == SteamVR_Skeleton_JointIndexes.thumbMetacarpal);
    }

    public enum MirrorType
    {
        None,
        LeftToRight,
        RightToLeft
    }
}