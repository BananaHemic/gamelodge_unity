using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DarkRift;
using UnityEngine;
using Valve.VR;

public abstract class BaseGrabbable : MonoBehaviour, IGrabbable
{
    protected int _canGrabControllerTypes = 0;
    public bool IsIdle { get { return Controllers == 0; } }
    // The controller(s) that are currently grabbing this
    public int Controllers { get; private set; }
    public DRUser.GrabbingBodyPart GrabbedBodyPart { get; private set; }
    // The position/rotation of this object, relative to the transform
    // that grabbed it. However, if the grabbable is set to HandFollowsObject
    // then this is actually the position of the wrist relative to the positon
    // of the object (i.e. the inverse of what it otherwise is)
    public Vector3 RelPos { get; private set; }
    public Quaternion RelRot { get; private set; }
    // Physics
    public SceneObject SceneObject { get; protected set; }
    private SteamVR_Skeleton_Poser _handPose;

    const int BothControllers = 0
        | 1 << (int)ControllerAbstraction.ControllerType.LEFTHAND
        | 1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND;
    const int LeftController = 0
        | 1 << (int)ControllerAbstraction.ControllerType.LEFTHAND;
    const int RightController = 0
        | 1 << (int)ControllerAbstraction.ControllerType.RIGHTHAND;

    public SceneObject GetSceneObject()
    {
        return SceneObject;
    }
    public virtual bool CanGrab(ControllerAbstraction.ControllerType controllerType)
    {
        if(SceneObject == null)
        {
            Debug.LogError("Can't grab, un-Init grabbable! " + name, this);
            return false;
        }
        return SceneObject.CurrentGrabState != SceneObject.GrabState.GrabbedBySelf 
            && SceneObject.CurrentGrabState != SceneObject.GrabState.PendingGrabbedBySelf
            && SceneObject.CanGrab();
    }
    public void NetworkSetGrabBodyPart(DRUser.GrabbingBodyPart bodyPart, Vector3 relPosition, Quaternion relRotation)
    {
        GrabbedBodyPart = bodyPart;
        RelPos = relPosition;
        RelRot = relRotation;
    }
    /// <summary>
    /// Children must call this method
    /// </summary>
    /// <param name="sceneObject"></param>
    protected void SetSceneObject(SceneObject sceneObject)
    {
        SceneObject = sceneObject;
    }
    public SteamVR_Skeleton_Poser GetHandPose()
    {
        if (_handPose != null)
            return _handPose;

        BundleItem bundleItem = SceneObject.BundleItem;
        // Return if we haven't loaded the item yet
        if (bundleItem == null)
        {
            Debug.Log("no hand, no bundle item");
            return null;
        }

        // Return if the attached object doesn't have the needed script
        if (!bundleItem.AttachedScripts.Contains("Valve.VR." + nameof(SteamVR_Skeleton_Poser)))
        {
            //Debug.Log("no script");
            return null;
        }

        _handPose = SceneObject.Model?.GetComponent<SteamVR_Skeleton_Poser>();
        return _handPose;
    }
    protected virtual void OnCanGrab()
    {
        //Debug.Log("Can grab!");
    }
    protected virtual void OnCannotGrab()
    {
        //Debug.Log("Cannot grab!");
    }
    public void OnCanGrabStateChange(bool canGrab, ControllerAbstraction.ControllerType controllerType)
    {
        // We keep track of which controllers can currently grab us
        // this way we only fire OnCanGrab / OnCannotGrab
        int controllerInt = (int)controllerType;
        int controllerMask = 1 << controllerInt;
        bool couldGrab = _canGrabControllerTypes != 0;

        //Debug.Log("Prev could grab " + couldGrab);
        if (canGrab)
            _canGrabControllerTypes |= controllerMask;
        else
            _canGrabControllerTypes &= ~controllerMask;
        bool canNowGrab = _canGrabControllerTypes != 0;
        //Debug.Log("Now could grab " + canNowGrab);

        if (!couldGrab && canNowGrab)
            OnCanGrab();
        else if (couldGrab && !canNowGrab)
            OnCannotGrab();
    }
    public static DRUser.GrabbingBodyPart ControllersToGrabBodyPart(int prevControllers, int newControllers)
    {
        if (newControllers == LeftController)
            return DRUser.GrabbingBodyPart.LeftHand;
        if (newControllers == RightController)
            return DRUser.GrabbingBodyPart.RightHand;
        if(newControllers == BothControllers)
        {
            if (prevControllers == LeftController)
                return DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary;
            if(prevControllers == RightController)
                return DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary;
            if(prevControllers == 0) // Default to right hand dominant
                return DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary;
        }
        Debug.LogError("Unhandled controller combination was " + prevControllers + " now " + newControllers);
        return DRUser.GrabbingBodyPart.None;
    }
    public virtual bool OnLocalGrabStart(int controllers)
    {
        int prevControllers = Controllers;
        GrabbedBodyPart = ControllersToGrabBodyPart(Controllers, controllers);
        Controllers = controllers;
        // Anticipate that we will be grabbing this object
        // This will set the relative orientation
        UserManager.Instance.LocalUserDisplay.PoseDisplay.HandDisplay.AnticipateLocalUserGrabObject(this, GrabbedBodyPart);

        if (!SceneObject.TryGrab(this))
        {
            Debug.LogError("Failed to try grab in grab start " + controllers + " It should not fail here!");
            return false;
        }
        return true;
    }
    public virtual void OnLocalGrabUpdate() { }
    private static Transform GetLocalTransformForBodyPart(DRUser.GrabbingBodyPart bodyPart)
    {
        switch (bodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                return ControllerAbstraction.Instances[0].GetTransform();
            case DRUser.GrabbingBodyPart.LeftHand:
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                return ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].GetTransform();
            case DRUser.GrabbingBodyPart.RightHand:
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                return ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].GetTransform();
            case DRUser.GrabbingBodyPart.Body:
                //TODO
                break;
            case DRUser.GrabbingBodyPart.None:
                break;
        }
        return null;
    }
    public Transform GetGrabbingTransform()
    {
        return GetLocalTransformForBodyPart(GrabbedBodyPart);
    }
    public ControllerAbstraction GetGrabbingController()
    {
        switch (GrabbedBodyPart)
        {
            case DRUser.GrabbingBodyPart.Head:
                return ControllerAbstraction.Instances[0];
            case DRUser.GrabbingBodyPart.LeftHand:
            case DRUser.GrabbingBodyPart.LeftHandPrimary_RightHandSecondary:
                return ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND];
            case DRUser.GrabbingBodyPart.RightHand:
            case DRUser.GrabbingBodyPart.RightHandPrimary_LeftHandSecondary:
                return ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND];
            case DRUser.GrabbingBodyPart.Body:
                //TODO
                break;
            case DRUser.GrabbingBodyPart.None:
                break;
        }
        return null;
    }

    public void LocalSetOrientation(Vector3 objectPosRelHand, Quaternion objectRotRelHand)
    {
        RelPos = objectPosRelHand;
        RelRot = objectRotRelHand;
    }

    public virtual void OnLocalGrabEnd(ControllerAbstraction.ControllerType detachedController)
    {
        int detachedControllerInt = (int)detachedController;
        int prevControllers = Controllers;
        Controllers = prevControllers & ~(1 << detachedControllerInt);
        //Debug.Log("Grabbable end grab");
        if(Controllers == 0)
            SceneObject.EndGrab();
        else
        {
            GrabbedBodyPart = ControllersToGrabBodyPart(prevControllers, Controllers);
            if (!SceneObject.TryGrab(this))//TODO this might fail for quick double grabs
                Debug.LogError("Failed to handle grab end, controllers was " + prevControllers + " now " + Controllers);
        }
        UserManager.Instance.LocalUserDisplay.PoseDisplay.HandDisplay.AnticipateReleasingObject(this.SceneObject);
    }
}
