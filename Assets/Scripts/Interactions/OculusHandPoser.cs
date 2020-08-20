using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class OculusHandPoser : MonoBehaviour
{
    public HandPoser HandPoser;
    public SteamVR_Skeleton_Pose OpenPose;
    public SteamVR_Skeleton_Pose TriggerPose;
    public SteamVR_Skeleton_Pose GripPose;
    public SteamVR_Skeleton_Pose ThumbPose;
    public SteamVR_Skeleton_HandMask GripMask = new SteamVR_Skeleton_HandMask();

    const int NumBones = 31;
    private readonly Vector3[] _positions = new Vector3[NumBones];
    private readonly Quaternion[] _rotations = new Quaternion[NumBones];

    public void GetBonePositionRotations(Transform handTransform, out Vector3[] positions, out Quaternion[] rotations)
    {
        positions = _positions;
        rotations = _rotations;
        OVRInput.Controller controllerType = HandPoser.MyControllerType == ControllerAbstraction.ControllerType.LEFTHAND
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
        SteamVR_Skeleton_Pose_Hand gripHand = HandPoser.MyControllerType == ControllerAbstraction.ControllerType.LEFTHAND
            ? GripPose.leftHand : GripPose.rightHand;
        SteamVR_Skeleton_Pose_Hand restHand = HandPoser.MyControllerType == ControllerAbstraction.ControllerType.LEFTHAND
            ? OpenPose.leftHand : OpenPose.rightHand;

        handTransform.localPosition = restHand.position;
        handTransform.localRotation = restHand.rotation;

        float gripBlend = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controllerType);

        // Blend when applicable
        for (int boneIndex = 0; boneIndex < NumBones; boneIndex++)
        {
            int fingerIndex = SteamVR_Skeleton_JointIndexes.GetFingerForBone(boneIndex) + 1;
            if (GripMask.GetFinger(fingerIndex) && gripBlend > 0)
            {
                rotations[boneIndex] = Quaternion.Slerp(restHand.boneRotations[boneIndex], gripHand.boneRotations[boneIndex], gripBlend);
                positions[boneIndex] = Vector3.Lerp(restHand.bonePositions[boneIndex], gripHand.bonePositions[boneIndex], gripBlend);
            }
            else
            {
                rotations[boneIndex] = restHand.boneRotations[boneIndex];
                positions[boneIndex] = restHand.bonePositions[boneIndex];
            }
        }
        //return new Vector3[GripPose.rightHand.bonePositions.Length];
        //Debug.Log("Meta " + GripPose.rightHand.boneRotations[SteamVR_Skeleton_JointIndexes.middleProximal].eulerAngles.ToPrettyString());
    }
}
