using RLD;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

/// <summary>
/// In charge of moving the hands, based on if something is being grabbed
/// and what properties that grabbed object has. Also moves the objects that are being grabbed
/// Clunky name, I know...
/// </summary>
public class UserHandGrabbedDisplay
{
    private readonly bool _isLocal;
    private readonly UserDisplay _userDisplay;
    private readonly UserPoseDisplay _poseDisplay;
    private readonly List<BaseGrabbable> _grabbedObjects;
    private readonly Stack<int> _pendingGrabbedDeletes = new Stack<int>();
    // For local users, we also track the grabbables that we anticipate that
    // the server will let us grab
    private readonly List<BaseGrabbable> _anticipatedGrabbedObjects;

    public UserHandGrabbedDisplay(bool isLocal, UserDisplay userDisplay, UserPoseDisplay poseDisplay)
    {
        _isLocal = isLocal;
        _poseDisplay = poseDisplay;
        _userDisplay = userDisplay;
        // Load grabbed scene objects
        var allGrabbed = _poseDisplay.DrUser.GrabbedObjects;
        int numGrabbed = allGrabbed == null ? 0 : allGrabbed.Count;
        _grabbedObjects = new List<BaseGrabbable>(numGrabbed);
        if (_isLocal)
            _anticipatedGrabbedObjects = new List<BaseGrabbable>();
        //Debug.Log("User has grabbed " + numGrabbed + " objects");
        for (int i = 0; i < numGrabbed; i++)
        {
            DRUser.GrabbedObject grabbedObj = allGrabbed[i];
            //Debug.LogWarning("obj #" + grabbedObj.ObjectID + " pos " + grabbedObj.RelativePos + " rot " + grabbedObj.RelativeRot);
            if(!SceneObjectManager.Instance.TryGetSceneObjectByID(grabbedObj.ObjectID, out SceneObject sceneObject))
            {
                Debug.LogError("Scene object #" + grabbedObj.ObjectID + " grabbed by " + _poseDisplay.DrUser.ID + " not found in init");
                continue;
            }

            BaseGrabbable baseGrabbable = sceneObject.GetGrabbable(poseDisplay.DrUser.IsInPlayMode);
            if(baseGrabbable == null)
            {
                Debug.LogError("null grabbable on " + sceneObject.GetID() + " for user #" + poseDisplay.DrUser.ID + " play " + poseDisplay.DrUser.IsInPlayMode);
                continue;
            }
            baseGrabbable.NetworkSetGrabBodyPart(grabbedObj.BodyPart, grabbedObj.RelativePos, grabbedObj.RelativeRot);
            //Debug.LogError("user " + poseDisplay.DrUser.ID + " play " + poseDisplay.DrUser.IsInPlayMode + " grab " + baseGrabbable);
            _grabbedObjects.Add(baseGrabbable);
        }
    }
    public bool GetInstantPositionRotationOfObject(PlayGrabbable grab, out Vector3 position, out Quaternion rotation,
        out Vector3 velocity, out Vector3 angularVelocity)
    {
        Transform moveTransform;
        Vector3 grabberPos;
        Quaternion grabberRot;

        Vector3 nextGrabPos;
        Quaternion nextGrabRot;
        float dt;

        // If this is the local user, then the pose display hasn't yet updated,
        // so we need to query it now
        if (_isLocal)
        {
            Vector3 trackingSpace = TrackingSpace.Instance == null ? Vector3.zero : TrackingSpace.Instance.transform.localPosition;
            dt = TimeManager.Instance.PhysicsTimestep;
            Vector3 nextRawGrabPos;
            Quaternion nextRawGrabRot;
            Vector3 controllerVel, controllerAngVel, rawGrabPos;
            Quaternion rawGrabRot;
            HandPoser handPoser;
            switch (grab.GrabbedBodyPart)
            {
                case DRUser.GrabbingBodyPart.LeftHand:
                case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    //ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetLocalPositionRotation(out rawGrabPos, out rawGrabRot);
                    ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetLocalPosRotVelImmediate(out rawGrabPos, out rawGrabRot, out controllerVel, out controllerAngVel);
                    handPoser = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND];
                    grabberPos = rawGrabPos + trackingSpace + rawGrabRot * handPoser.WristAttach.localPosition;
                    grabberRot = rawGrabRot * handPoser.WristAttach.localRotation;
                    moveTransform = _userDisplay.PoseDisplay.LHandTransform;
                    nextRawGrabPos = rawGrabPos + controllerVel * dt;
                    nextRawGrabRot = ExtensionMethods.ApplyAngularVelocity(rawGrabRot, controllerAngVel, dt);
                    nextGrabPos = nextRawGrabPos + trackingSpace + nextRawGrabRot * handPoser.WristAttach.localPosition;
                    nextGrabRot = nextRawGrabRot * handPoser.WristAttach.localRotation;
                    //Debug.Log("rot " + grabberRot.ToPrettyString() + " nxt " + nextGrabRot.ToPrettyString());
                    break;
                case DRUser.GrabbingBodyPart.RightHand:
                case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                    ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetLocalPosRotVelImmediate(out rawGrabPos, out rawGrabRot, out controllerVel, out controllerAngVel);
                    handPoser = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND];
                    grabberPos = rawGrabPos + trackingSpace + rawGrabRot * handPoser.WristAttach.localPosition;
                    grabberRot = rawGrabRot * handPoser.WristAttach.localRotation;
                    moveTransform = _userDisplay.PoseDisplay.RHandTransform;
                    nextRawGrabPos = rawGrabPos + controllerVel * dt;
                    nextRawGrabRot = ExtensionMethods.ApplyAngularVelocity(rawGrabRot, controllerAngVel, dt);
                    nextGrabPos = nextRawGrabPos + trackingSpace + nextRawGrabRot * handPoser.WristAttach.localPosition;
                    nextGrabRot = nextRawGrabRot * handPoser.WristAttach.localRotation;
                    //Debug.Log("rot " + grabberRot.ToPrettyString() + " nxt " + nextGrabRot.ToPrettyString());
                    break;
                default:
                    position = Vector3.zero;
                    rotation = Quaternion.identity;
                    velocity = Vector3.zero;
                    angularVelocity = Vector3.zero;
                    return false;
            }
        }
        else
        {
            // If this is non-local, we use the poseDisplay to get the correct
            // position
            switch (grab.GrabbedBodyPart)
            {
                case DRUser.GrabbingBodyPart.Head:
                    moveTransform = _userDisplay.PoseDisplay.HeadTransform;
                    grabberPos = _poseDisplay.HeadPosition;
                    grabberRot = _poseDisplay.HeadRotation;
                    break;
                case DRUser.GrabbingBodyPart.LeftHand:
                    moveTransform = _userDisplay.PoseDisplay.LHandTransform;
                    grabberPos = _poseDisplay.HandPositionL;
                    grabberRot = _poseDisplay.HandRotationL;
                    break;
                case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    moveTransform = _userDisplay.PoseDisplay.LHandTransform;
                    grabberPos = _poseDisplay.HandPositionL;
                    grabberRot = _poseDisplay.HandRotationL;
                    break;
                case DRUser.GrabbingBodyPart.RightHand:
                    moveTransform = _userDisplay.PoseDisplay.RHandTransform;
                    grabberPos = _poseDisplay.HandPositionR;
                    grabberRot = _poseDisplay.HandRotationR;
                    break;
                case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                    moveTransform = _userDisplay.PoseDisplay.RHandTransform;
                    grabberPos = _poseDisplay.HandPositionR;
                    grabberRot = _poseDisplay.HandRotationR;
                    break;
                case DRUser.GrabbingBodyPart.Body:
                    //TODO
                    moveTransform = _poseDisplay.transform;
                    grabberPos = moveTransform.localPosition;
                    grabberRot = moveTransform.localRotation;
                    break;
                case DRUser.GrabbingBodyPart.None:
                default:
                    position = Vector3.zero;
                    rotation = Quaternion.identity;
                    velocity = Vector3.zero;
                    angularVelocity = Vector3.zero;
                    Debug.LogError("Unhandled body part for instPosRot: " + grab.GrabbedBodyPart);
                    return false;
            }
            nextGrabPos = Vector3.zero;
            nextGrabRot = Quaternion.identity;
            dt = 0;
        }
        Vector3 revertPos = moveTransform.localPosition;
        Quaternion revertRot = moveTransform.localRotation;

        moveTransform.localPosition = grabberPos;
        moveTransform.localRotation = grabberRot;

        bool grabModeIsInverse;
        // Using the moved hand transform, and the relative position in grab
        // find the target orientation for the object
        // honestly, I don't have a clear understanding of this, I mainly got it through unit tests
        if(grab.GrabbableBehavior == null || grab.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.ObjectFollowsHand)
        {
            position = moveTransform.TransformPoint(grab.RelPos);
            rotation = moveTransform.rotation * grab.RelRot;
            grabModeIsInverse = false;
        }
        else
        {
            position = moveTransform.TransformPoint(-(Quaternion.Inverse(grab.RelRot) * grab.RelPos));
            rotation = moveTransform.rotation * Quaternion.Inverse(grab.RelRot);
            grabModeIsInverse = true;
        }
        //Debug.LogWarning("pos " + grab.RelPos.ToPrettyString() + " rot " + grab.RelRot.ToPrettyString());

        // If this is a local user, then we have the velocity in controller space, so we just apply the velocity
        // and then using that we get the velocity of the object
        if (_isLocal)
        {
            moveTransform.localPosition = nextGrabPos;
            moveTransform.localRotation = nextGrabRot;
            Vector3 objPosNextFrame;
            Quaternion objRotNextFrame;
            if (grabModeIsInverse)
            {
                objPosNextFrame = moveTransform.TransformPoint(-(Quaternion.Inverse(grab.RelRot) * grab.RelPos));
                objRotNextFrame = moveTransform.rotation * Quaternion.Inverse(grab.RelRot);
            }
            else
            {
                objPosNextFrame = moveTransform.TransformPoint(grab.RelPos);
                objRotNextFrame = moveTransform.rotation * grab.RelRot;
            }

            // Take the difference, and divide by dt to get the velocity
            // The dt is always just the render delta, because that's when we update the pose
            velocity = (objPosNextFrame - position) / dt;
            angularVelocity = ExtensionMethods.GetAngularVelocity(rotation, objRotNextFrame, dt);
        }
        else
        {
            // Now move the move transform to where it was, in order to find out what
            // the previous position / rotation of the grabbed object would be
            bool didGetPrev = _poseDisplay.GetPreviousPoseWorld(grab.GrabbedBodyPart, out Vector3 lastFramePos, out Quaternion lastFrameRot, out dt);
            if (didGetPrev)
            {
                //Debug.Log("dt " + dt);
                Vector3 handPos = moveTransform.position;
                moveTransform.position = lastFramePos;
                moveTransform.rotation = lastFrameRot;
                Vector3 objPosLastFrame;
                Quaternion objRotLastFrame;
                if (grabModeIsInverse)
                {
                    objPosLastFrame = moveTransform.TransformPoint(-(Quaternion.Inverse(grab.RelRot) * grab.RelPos));
                    objRotLastFrame = moveTransform.rotation * Quaternion.Inverse(grab.RelRot);
                }
                else
                {
                    objPosLastFrame = moveTransform.TransformPoint(grab.RelPos);
                    objRotLastFrame = moveTransform.rotation * grab.RelRot;
                }
                //Debug.Log("hand pos " + lastFramePos + "->" + handPos + " obj pos " + objPosLastFrame + "->" + position);

                // Take the difference, and divide by dt to get the velocity
                // The dt is always just the render delta, because that's when we update the pose
                velocity = (position - objPosLastFrame) / dt;
                angularVelocity = ExtensionMethods.GetAngularVelocity(objRotLastFrame, rotation, dt);

                //Debug.Log("Velocity mag " + velocity.magnitude);
            }
            else
            {
                // No previous position to draw from for this body part
                velocity = Vector3.zero;
                angularVelocity = Vector3.zero;
            }
        }

        // revert the changes to moveTransform
        moveTransform.localPosition = revertPos;
        moveTransform.localRotation = revertRot;
        return true;
    }
    public bool GetRealPositionRotationOfHand(PlayGrabbable grab, out Vector3 grabberPos, out Quaternion grabberRot)
    {
        if (_isLocal)
        {
            Vector3 trackingSpace = TrackingSpace.Instance == null ? Vector3.zero : TrackingSpace.Instance.transform.localPosition;
            switch (grab.GrabbedBodyPart)
            {
                case DRUser.GrabbingBodyPart.LeftHand:
                case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetLocalPositionRotationImmediate(out grabberPos, out grabberRot);
                    grabberPos = grabberPos + trackingSpace + grabberRot * HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].WristAttach.localPosition;
                    grabberRot = grabberRot * HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].WristAttach.localRotation;
                    break;
                case DRUser.GrabbingBodyPart.RightHand:
                case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                    ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetLocalPositionRotationImmediate(out grabberPos, out grabberRot);
                    grabberPos = grabberPos + trackingSpace + grabberRot * HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].WristAttach.localPosition;
                    grabberRot = grabberRot * HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].WristAttach.localRotation;
                    break;
                default:
                    grabberPos = Vector3.zero;
                    grabberRot = Quaternion.identity;
                    return false;
            }
        }
        else
        {
            // If this is non-local, we use the poseDisplay to get the correct
            // position
            switch (grab.GrabbedBodyPart)
            {
                case DRUser.GrabbingBodyPart.Head:
                    grabberPos = _poseDisplay.HeadPosition;
                    grabberRot = _poseDisplay.HeadRotation;
                    break;
                case DRUser.GrabbingBodyPart.LeftHand:
                    grabberPos = _poseDisplay.HandPositionL;
                    grabberRot = _poseDisplay.HandRotationL;
                    break;
                case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    grabberPos = _poseDisplay.HandPositionL;
                    grabberRot = _poseDisplay.HandRotationL;
                    break;
                case DRUser.GrabbingBodyPart.RightHand:
                    grabberPos = _poseDisplay.HandPositionR;
                    grabberRot = _poseDisplay.HandRotationR;
                    break;
                case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                    grabberPos = _poseDisplay.HandPositionR;
                    grabberRot = _poseDisplay.HandRotationR;
                    break;
                case DRUser.GrabbingBodyPart.Body:
                    //TODO
                    Transform moveTransform = _poseDisplay.transform;
                    grabberPos = moveTransform.localPosition;
                    grabberRot = moveTransform.localRotation;
                    return true;
                case DRUser.GrabbingBodyPart.None:
                default:
                    grabberPos = Vector3.zero;
                    grabberRot = Quaternion.identity;
                    return false;
            }
        }

        // Convert from local to world
        grabberPos = _poseDisplay.transform.TransformPoint(grabberPos);
        grabberRot = _poseDisplay.transform.rotation * grabberRot;
        return true;
    }
    public bool GetVirtualPositionRotationOfHand(PlayGrabbable grab, out Vector3 grabberPos, out Quaternion grabberRot)
    {
        //TODO it'd be ideal to have the local user update the position/rotation
        // If this is non-local, we use the poseDisplay to get the correct
        // position
        switch (grab.GrabbedBodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                grabberPos = _userDisplay.PoseDisplay.HeadTransform.position;
                grabberRot = _userDisplay.PoseDisplay.HeadTransform.rotation;
                return true;
            case DRUser.GrabbingBodyPart.LeftHand:
                grabberPos = _userDisplay.PoseDisplay.LHandTransform.position;
                grabberRot = _userDisplay.PoseDisplay.LHandTransform.rotation;
                return true;
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                grabberPos = _userDisplay.PoseDisplay.LHandTransform.position;
                grabberRot = _userDisplay.PoseDisplay.LHandTransform.rotation;
                return true;
            case DRUser.GrabbingBodyPart.RightHand:
                grabberPos = _userDisplay.PoseDisplay.RHandTransform.position;
                grabberRot = _userDisplay.PoseDisplay.RHandTransform.rotation;
                return true;
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                grabberPos = _userDisplay.PoseDisplay.RHandTransform.position;
                grabberRot = _userDisplay.PoseDisplay.RHandTransform.rotation;
                return true;
            case DRUser.GrabbingBodyPart.Body:
                //TODO
                grabberPos = _poseDisplay.transform.localPosition;
                grabberRot = _poseDisplay.transform.localRotation;
                return true;
            case DRUser.GrabbingBodyPart.None:
            default:
                grabberPos = Vector3.zero;
                grabberRot = Quaternion.identity;
                return false;
        }
    }
    private void SimulateGrabbedObject(BaseGrabbable grab, int index, ref int hasMovedController, bool onlySetRelOrientation=false)
    {
        if(grab == null)
        {
            Debug.LogWarning("Null grab at " + index);
            _pendingGrabbedDeletes.Push(index);
            return;
        }

        Transform moveTransform = null;
        Vector3 grabberPos = Vector3.zero;
        Quaternion grabberRot = Quaternion.identity;
        int primaryControllerIndex = -1;
        int primaryHandIndex = -1;
        //int secondaryHandIndex = -1;
        //Debug.Log("grabbed by " + grab.GrabbedBodyPart);
        // Get the transform that's grabbing this object
        switch (grab.GrabbedBodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                moveTransform = _userDisplay.PoseDisplay.HeadTransform;
                grabberPos = _poseDisplay.HeadPosition;
                grabberRot = _poseDisplay.HeadRotation;
                primaryControllerIndex = 1 << (int)ControllerAbstraction.ControllerType.HEAD;
                break;
            case DRUser.GrabbingBodyPart.LeftHand:
                moveTransform = _userDisplay.PoseDisplay.LHandTransform;
                primaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.LeftHand;
                grabberPos = _poseDisplay.HandPositionL;
                grabberRot = _poseDisplay.HandRotationL;
                primaryControllerIndex = 1 << (int)ControllerAbstraction.ControllerType.LEFTHAND;
                break;
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                moveTransform = _userDisplay.PoseDisplay.LHandTransform;
                primaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.LeftHand;
                //secondaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.RightHand;
                grabberPos = _poseDisplay.HandPositionL;
                grabberRot = _poseDisplay.HandRotationL;
                primaryControllerIndex = 1 << (int)ControllerAbstraction.ControllerType.LEFTHAND;
                break;
            case DRUser.GrabbingBodyPart.RightHand:
                moveTransform = _userDisplay.PoseDisplay.RHandTransform;
                primaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.RightHand;
                grabberPos = _poseDisplay.HandPositionR;
                grabberRot = _poseDisplay.HandRotationR;
                primaryControllerIndex = 1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND;
                break;
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                moveTransform = _userDisplay.PoseDisplay.RHandTransform;
                primaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.RightHand;
                //secondaryHandIndex = (int)Valve.VR.SteamVR_Input_Sources.LeftHand;
                grabberPos = _poseDisplay.HandPositionR;
                grabberRot = _poseDisplay.HandRotationR;
                primaryControllerIndex = 1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND;
                break;
            case DRUser.GrabbingBodyPart.Body:
                //TODO
                moveTransform = _poseDisplay.transform;
                grabberPos = moveTransform.localPosition;
                grabberRot = moveTransform.localRotation;
                break;
            case DRUser.GrabbingBodyPart.None:
                break;
        }
        if (moveTransform == null)
            return; // Will happen when grabber is none

        // Move the transform for the mover to the position/rotation of the
        // instantaneous hand. We may later overwrite this
        moveTransform.localPosition = grabberPos;
        moveTransform.localRotation = grabberRot;
        if (primaryControllerIndex != -1)
            hasMovedController |= primaryControllerIndex;

        Vector3 relativePosition = grab.RelPos;
        Quaternion relativeRotation = grab.RelRot;
        // See if there is a corresponding hand pose for this body part
        // If we're local. Non-local clients will already have received the
        // local offset from the network
        bool hasHand = false;
        PlayGrabbable playGrabbable = grab as PlayGrabbable;

        // If we don't have a relative position, then we set it now
        if (onlySetRelOrientation)
        {
            // Only local clients should use this, network clients should be using
            // the provided local offset
            if (!_isLocal)
                Debug.LogError("Only SetRelOrientation is only for local clients!");

            Vector3 handPosePos = Vector3.zero;
            Quaternion handPoseRot = Quaternion.identity;
            if (primaryHandIndex != -1)
            {
                var handPose = grab.GetHandPose();
                if(handPose != null)
                {
                    var primaryHand = handPose.skeletonMainPose.GetHand(primaryHandIndex);
                    if(primaryHand != null)
                    {
                        // We need the position of the grabbed object, relative to the wrist
                        // so we get the world space position/rotation and then transform it to the
                        // wrist's local position
                        if(primaryHandIndex > 0 && primaryHandIndex < HandPoser.Instances.Length)
                        {
                            handPosePos = primaryHand.position;
                            handPoseRot = primaryHand.rotation;
                            HandPoser handPoser = HandPoser.Instances[primaryHandIndex];
                            Transform wristTransform = handPoser.WristAttach;
                            Transform handParent = handPoser.HandModel;
                            // Get the position of the object in world space
                            Vector3 objWorldPos = handParent.TransformPoint(primaryHand.position);
                            Quaternion objWorldRot = handParent.rotation * primaryHand.rotation;

                            //Debug.LogWarning("Pose hand pos " + handPosePos.ToPrettyString());

                            // Get the position/rotation of the object, relative to the wrist
                            relativePosition = wristTransform.InverseTransformPoint(objWorldPos);
                            relativeRotation = Quaternion.Inverse(wristTransform.rotation) * objWorldRot;
                            //Debug.LogWarning("Raw rel pos " + relativePosition.ToPrettyString() + " rot " + relativeRotation.ToPrettyString());
                        }

                        hasHand = true;
                        //Debug.Log("Hand rel pos " + objectPosRelHand.ToPrettyString());
                    }
                }
            }
            // If we have a hand, and this is a object-follows-hand, then we know the relative orientation
            if(hasHand && playGrabbable != null && playGrabbable.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.ObjectFollowsHand)
                grab.LocalSetOrientation(relativePosition, relativeRotation);
            else if(playGrabbable != null && playGrabbable.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.HandFollowsObject)
            {
                // If this is a HandFollowsObject, then the relative pos/rot is the pos/rot of the
                // the hand relative to the object
                relativeRotation = Quaternion.Inverse(relativeRotation);
                relativePosition = -(relativeRotation * relativePosition);
                grab.LocalSetOrientation(relativePosition, relativeRotation);
            }
            else
            {
                // Otherwise, we get the orientation based on where the object is relative to the moving transform
                relativePosition = moveTransform.InverseTransformPoint(grab.transform.position);
                relativeRotation = Quaternion.Inverse(moveTransform.rotation) * grab.transform.rotation;
                grab.LocalSetOrientation(relativePosition, relativeRotation);
            }
            return;
        }

        if (playGrabbable != null)
        {
            // If this is a None type behavior, then we do nothing
            if (playGrabbable.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.None)
                return;
            // If this is a HandFollowObject, then we just move the hand
            if (playGrabbable.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.HandFollowsObject)
            {
                //Debug.Log("Rel Pos: " + relativePosition.ToPrettyString());
                // The relative position will be relative to the object
                Vector3 pos = grab.transform.TransformPoint(relativePosition);
                Quaternion rot = grab.transform.rotation * relativeRotation;
                // Move hand to where the object is
                switch (grab.GrabbedBodyPart)
                {
                    case DRUser.GrabbingBodyPart.LeftHand:
                        _userDisplay.PoseDisplay.LHandTransform.position = pos;
                        _userDisplay.PoseDisplay.LHandTransform.rotation = rot;
                        if (_isLocal)
                        {
                            HandPoser handPoser = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND];
                            // The relative position/rotation is for the wrist, so we convert it
                            // so be relative to the base controller
                            Transform wrist = handPoser.WristAttach;
                            Quaternion rotWithoutWrist = relativeRotation * Quaternion.Inverse(wrist.localRotation);
                            Quaternion handRot = grab.transform.rotation * rotWithoutWrist;
                            Vector3 handPos = grab.transform.TransformPoint(relativePosition - rotWithoutWrist * wrist.localPosition);

                            Transform handTransform = handPoser.transform;
                            handTransform.position = handPos;
                            handTransform.rotation = handRot;
                        }
                        hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.LEFTHAND);
                        break;
                    case DRUser.GrabbingBodyPart.RightHand:
                        _userDisplay.PoseDisplay.RHandTransform.position = pos;
                        _userDisplay.PoseDisplay.RHandTransform.rotation = rot;
                        if (_isLocal)
                        {
                            HandPoser handPoser = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND];
                            Transform wrist = handPoser.WristAttach;
                            Quaternion rotWithoutWrist = relativeRotation * Quaternion.Inverse(wrist.localRotation);
                            Quaternion handRot = grab.transform.rotation * rotWithoutWrist;
                            Vector3 handPos = grab.transform.TransformPoint(relativePosition - rotWithoutWrist * wrist.localPosition);

                            Transform handTransform = handPoser.transform;
                            handTransform.position = handPos;
                            handTransform.rotation = handRot;
                        }
                        hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND);
                        break;
                    case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                    case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                        //TODO
                        hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.LEFTHAND);
                        hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND);
                        break;
                }
                return;
            }

            //TODO if this is move instant, and we are grabbing it with both hands,
            // then we will move the non-dominant hand, so we should mark it as
            // having been moved
            if(playGrabbable.GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.ObjectFollowsHand)
            {
                var bodyPart = playGrabbable.GrabbedBodyPart;
                if(bodyPart == DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary)
                    hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND);
                else if(bodyPart == DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary)
                    hasMovedController |= (1 << (int)ControllerAbstraction.ControllerType.LEFTHAND);
            }
        }

        //Debug.Log("Grabbed by " + moveTransform.gameObject.name + " #" + Time.frameCount);
        //Debug.Log("wrist pos " + moveTransform.localPosition.ToPrettyString() + " rot " + moveTransform.localRotation.ToPrettyString());
        //Debug.Log("wrist world pos " + moveTransform.position.ToPrettyString() + " rot " + moveTransform.rotation.ToPrettyString());
        Vector3 worldPos = moveTransform.TransformPoint(relativePosition);
        Quaternion worldRot = moveTransform.rotation * relativeRotation;
        //Debug.Log("obj pos " + worldPos.ToPrettyString() + " rot " + worldRot.ToPrettyString());
        //Debug.Log(objectPosRelHand.ToPrettyString() + " -> " + worldPos.ToPrettyString() + " from " + moveTransform.position.ToPrettyString());

        // Move the object according to the hand
        // Off the shelf, unity doesn't have a great way to move objects
        // with physics. You can either move them with transform.position
        // but then the physics is wrong and perf is bad. Or you can move
        // with MovePosition, but that runs in FixedUpdate so you're prone
        // to stuttering and latency. To solve this, we build a pure-physics
        // representation of the SceneObject with only Colliders and a Rigidbody
        // and move that with MovePostion and then we move immediately move the
        // SceneObject (which is now just renderers) via transform.position.
        if (grab.SceneObject.PhysicsFollower != null)
        {
            grab.SceneObject.PhysicsFollower.OurRigidbody.MovePosition(worldPos);
            grab.SceneObject.PhysicsFollower.OurRigidbody.MoveRotation(worldRot);

            grab.SceneObject.transform.position = worldPos;
            grab.SceneObject.transform.rotation = worldRot;
        }
        else if(grab.SceneObject.Rigidbody != null)
        {
            // I'm not sure if this is ever supposed to happen
            grab.SceneObject.Rigidbody.MovePosition(worldPos);
            grab.SceneObject.Rigidbody.MoveRotation(worldRot);
        }else
        {
            grab.SceneObject.transform.position = worldPos;
            grab.SceneObject.transform.rotation = worldRot;
        }


        // If we're local, update the hand display
        if (_isLocal)
        {
            if(grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.LeftHand
                || grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary)
            {
                Transform lHandTransform = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].transform;
                // We move the hands to the controller position, NOT the PoseDisplay HandPosition
                // this is because that position includes the wrist offset
                Transform lController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetTransform();
                lHandTransform.position = lController.position;
                lHandTransform.rotation = lController.rotation;
            }
            else if(grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.RightHand
                || grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary)
            {
                Transform rHandTransform = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].transform;
                // We move the hands to the controller position, NOT the PoseDisplay HandPosition
                // this is because that position includes the wrist offset
                Transform rController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetTransform();
                rHandTransform.position = rController.position;
                rHandTransform.rotation = rController.rotation;
            }
        }

        // TODO also account for the secondary hand, when in double grab
        if(grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary
            || grab.GrabbedBodyPart == DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary)
        {
            //if (secondaryHandIndex != -1)
            //{
            //    var handPose = grab.GetHandPose();
            //    if(handPose != null)
            //    {
            //        var primaryHand = handPose.skeletonMainPose.GetHand(secondaryHandIndex);
            //        if(primaryHand != null)
            //        {
            //            objectPosRelHand = primaryHand.position;
            //            objectRotRelHand = primaryHand.rotation;
            //        }
            //    }
            //}

            //Vector3 worldPos = moveTransform.TransformPoint(objectPosRelHand);
            //Quaternion worldRot = moveTransform.rotation * objectRotRelHand;
        }
    }
    public void UpdateHandsAndGrabbedObjects()
    {
        // Keep track of which hands we've moved, so that
        // we don't overwrite any changes to the hands.
        // Rn, the only time a hand is moved here is when
        // the grabbable is set to HandFollowsObject
        int hasMovedController = 0;

        for (int i = 0; i < _grabbedObjects.Count; i++)
            SimulateGrabbedObject(_grabbedObjects[i], i, ref hasMovedController);
        // Remove some grabbables
        while (_pendingGrabbedDeletes.Count > 0)
        {
            int i = _pendingGrabbedDeletes.Pop();
            // We should be able to remove by swap
            // because this stack has the largest elements
            // at the top
            _grabbedObjects.RemoveBySwap(i);
        }

        if (_isLocal)
        {
            for (int i = 0; i < _anticipatedGrabbedObjects.Count; i++)
                SimulateGrabbedObject(_anticipatedGrabbedObjects[i], i, ref hasMovedController);

            while (_pendingGrabbedDeletes.Count > 0)
            {
                int i = _pendingGrabbedDeletes.Pop();
                // We should be able to remove by swap
                // because this stack has the largest elements
                // at the top
                _anticipatedGrabbedObjects.RemoveBySwap(i);
            }
        }

        // Move the head/hands that weren't moved for any objects
        if((hasMovedController & (1 << 0)) == 0)
        {
            _userDisplay.PoseDisplay.HeadTransform.localPosition = _poseDisplay.HeadPosition;
            _userDisplay.PoseDisplay.HeadTransform.localRotation = _poseDisplay.HeadRotation;
        }
        if((hasMovedController & (1 << (int)ControllerAbstraction.ControllerType.LEFTHAND)) == 0)
        {
            _userDisplay.PoseDisplay.LHandTransform.localPosition = _poseDisplay.HandPositionL;
            _userDisplay.PoseDisplay.LHandTransform.localRotation = _poseDisplay.HandRotationL;
            if (_isLocal)
            {
                Transform lHandTransform = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].transform;
                // We move the hands to the controller position, NOT the PoseDisplay HandPosition
                // this is because that position includes the wrist offset
                Transform lController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetTransform();
                lHandTransform.position = lController.position;
                lHandTransform.rotation = lController.rotation;
            }
        }
        if((hasMovedController & (1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND)) == 0)
        {
            _userDisplay.PoseDisplay.RHandTransform.localPosition = _poseDisplay.HandPositionR;
            _userDisplay.PoseDisplay.RHandTransform.localRotation = _poseDisplay.HandRotationR;
            if (_isLocal)
            {
                Transform rHandTransform = HandPoser.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].transform;
                Transform rController = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetTransform();
                rHandTransform.position = rController.position;
                rHandTransform.rotation = rController.rotation;
            }
        }
    }
    public void AnticipateLocalUserGrabObject(BaseGrabbable baseGrabbable, DRUser.GrabbingBodyPart bodyPart)
    {
        // Set the relative orientation
        // To do this, we handle the grabbed object like we
        // normally would, with an extra flag to mark that the
        // data in relative position/rotation is junk, then
        // we get the relative position after than simulation
        int hasMoved = 0;
        SimulateGrabbedObject(baseGrabbable, -1, ref hasMoved, true);

        if (baseGrabbable == null)
            Debug.LogError("AnticipateLocalGrab with null grabbable! " + baseGrabbable);
        _grabbedObjects.Add(baseGrabbable);
    }
    /// <summary>
    /// Called when we were in pending grab, but the server gave the
    /// grab to someone else OR when we release grab 
    /// </summary>
    /// <param name="sceneObject"></param>
    public void AnticipateReleasingObject(SceneObject sceneObject)
    {
        // Check the objects that we have grabbed
        for(int i = 0; i < _grabbedObjects.Count; i++)
        {
            var grab = _grabbedObjects[i];
            if(grab != null && grab.SceneObject == sceneObject)
            {
                _grabbedObjects.RemoveBySwap(i);
                return;
            }
        }
        // Check anticipated grabbed
        for(int i = 0; i < _anticipatedGrabbedObjects.Count; i++)
        {
            var grab = _anticipatedGrabbedObjects[i];
            if(grab != null && grab.SceneObject == sceneObject)
            {
                _anticipatedGrabbedObjects.RemoveBySwap(i);
                return;
            }
        }

        Debug.LogError("Can't handle lost anticipate grab, none for " + sceneObject.GetID());
        return;
    }
    public void UserGrabObjectServer(SceneObject sceneObject, DRUser.GrabbingBodyPart bodyPart, Vector3 relPosition, Quaternion relRotation)
    {
        // Try to remove any existing entries for this guy
        for(int i = 0; i < _grabbedObjects.Count; i++)
        {
            var grabbed = _grabbedObjects[i];
            if(grabbed.SceneObject == sceneObject)
            {
                _grabbedObjects.RemoveBySwap(i);
                //TODO not sure if this will normally occur
                break;
            }
        }
        // If this is local, make sure to remove the grabbed
        // object from the anticipated list
        if (_isLocal)
        {
            for(int i = 0; i < _anticipatedGrabbedObjects.Count; i++)
            {
                var grabbed = _anticipatedGrabbedObjects[i];
                if(grabbed.SceneObject == sceneObject)
                {
                    _anticipatedGrabbedObjects.RemoveBySwap(i);
                    //TODO not sure if this will normally occur
                    break;
                }
            }
        }

        BaseGrabbable baseGrabbable = sceneObject.GetGrabbable(_poseDisplay.DrUser.IsInPlayMode);
        if(baseGrabbable == null)
        {
            Debug.LogError("Grabbed object that has no grabbable! Play mode " + _poseDisplay.DrUser.IsInPlayMode);
            return;
        }

        // If this is a local version, then the grabbable will have been updated
        // by ControllerAbstraction. Otherwise, we update it here
        baseGrabbable.NetworkSetGrabBodyPart(bodyPart, relPosition, relRotation);
        _grabbedObjects.Add(baseGrabbable);
    }
    public void UserReleaseObjectServer(SceneObject sceneObject)
    {
        // Try to remove any existing entries for this guy
        for(int i = 0; i < _grabbedObjects.Count; i++)
        {
            var grabbed = _grabbedObjects[i];
            if(grabbed.SceneObject == sceneObject)
            {
                _grabbedObjects.RemoveBySwap(i);
                return;
            }
        }

        // If this is local, then we expect to not find anything
        // But if this is a network user, then this is a bug
        if(!_isLocal)
            Debug.LogError("Can't handle user release of object #" + sceneObject.GetID());
    }
}
