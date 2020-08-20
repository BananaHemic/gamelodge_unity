using System.Collections;
using System.Collections.Generic;
using Unity.Labs.SuperScience;
using UnityEngine;

public class PlayGrabbable : BaseGrabbable
{
    public GrabbableBehavior GrabbableBehavior { get; private set; }
    public static int LayerRequestPriority = PhysicsBehavior.LayerRequestPriority + 1;
    private PhysicsTracker _physicsTracker = null;
    private bool _needsPhysicsTrackerInit = false;

    public void Init(SceneObject sceneObject, GrabbableBehavior grabbable)
    {
        SetSceneObject(sceneObject);
        GrabbableBehavior = grabbable;
    }
    protected override  void OnCanGrab()
    {
        base.OnCanGrab();
        SceneObject.SetObjectOutline(ObjectOutline.OutlineState.GameHover);
    }
    protected override void OnCannotGrab()
    {
        base.OnCannotGrab();
        SceneObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
    }
    public override bool OnLocalGrabStart(int controllers)
    {
        bool wasIdle = IsIdle;
        if (!base.OnLocalGrabStart(controllers))
            return false;
        if(wasIdle)
        {
            //Debug.Log("Grab start, setting to grabbed layer");
            SceneObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
        }

        if (GrabbableBehavior != null)
            GrabbableBehavior.OnGrabStart(GrabbedBodyPart);
        _needsPhysicsTrackerInit = true;
        return true;
    }
    /// <summary>
    /// Called every frame that we're grabbed, AFTER the other scripts have moved the object
    /// </summary>
    public override void OnLocalGrabUpdate()
    {
        if (_needsPhysicsTrackerInit)
        {
            if (_physicsTracker != null)
                _physicsTracker.Reset(transform.localPosition, transform.localRotation, Vector3.zero, Vector3.zero);
            else
                _physicsTracker = new PhysicsTracker(transform.localPosition, transform.localRotation, Vector3.zero, Vector3.zero);
            _needsPhysicsTrackerInit = false;
            return;
        }
        _physicsTracker.Update(transform.localPosition, transform.localRotation, Time.smoothDeltaTime);
    }
    public override void OnLocalGrabEnd(ControllerAbstraction.ControllerType detachedController)
    {
        base.OnLocalGrabEnd(detachedController);
        if (GrabbableBehavior != null)
        {
            GrabbableBehavior.OnGrabEnd(GrabbedBodyPart);

            if(GrabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.ObjectFollowsHand
                && BuildPlayManager.Instance.IsSpawnedInPlayMode)
            {
                // We considered using the controller velocity to motivate the object's
                // velocity, but that ended up being rather unstable when moving your
                // hand along an arc. Not sure why, perhaps because we sample the velocity
                // after the user has let the grab go. TODO try using a one-frame-late
                // velocity, or averaging some part controller velocities
                //UserManager.Instance.GetLocalUserDisplay().PoseDisplay.HandDisplay.GetInstantPositionRotationOfObject(this, out Vector3 pos, out Quaternion rot, out Vector3 velocity, out Vector3 angVel);

                // If we have an RB, then we'll want to update the velocity/angular velocity
                Rigidbody rb = SceneObject.Rigidbody;
                if(rb != null)
                {
                    rb.velocity = _physicsTracker.Velocity;
                    rb.angularVelocity = _physicsTracker.AngularVelocity;
                    //rb.velocity = velocity;
                    //rb.angularVelocity = angVel;
                }
            }
        }
    }
}
