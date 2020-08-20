using DarkRift;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Network object is in charge of interpolating / extrapolating
/// states, accounting for dicontinuities from owner changes
/// and the like, and when rigidbodies are added / removed
/// </summary>
public class NetworkObject : MonoBehaviour, IRealtimeObject
{
    public SceneObject SceneObject { get; private set; }
    private DRObject _drObject;

    // Stuff for debugging
#if UNITY_EDITOR
#pragma warning disable CS0414
    [SerializeField]
    private int FrameOnLastRecvInput = -1;
    [SerializeField]
    private int OwnerID;
    [SerializeField]
    private string LastRecvSignal = "null";
#pragma warning restore CS0414
#endif

    private bool _didSubscribe = false;
    [SerializeField]
    private uint _currentPriority = 0;
    private int _frameOfLastCollision;
    [SerializeField]
    private bool _isForcedToRest = false;
    private Coroutine _waitForNewOwnerRoutine;
    private readonly Vec3 _workingVec = new Vec3();
    private readonly Quat _workingQuat = new Quat();
    /// <summary>
    /// The realtime when we began anticipating
    /// that we would take ownership from a collision
    /// -1 otherwise
    /// </summary>
    private float _timeStartAnticipatedOwnership = -1f;
    /// <summary>
    /// How many RTTs of time to wait, until we assume
    /// that we never actually took ownership
    /// </summary>
    const float MaxRTTUntilGiveUpOwnership = 2;
    // The pos/rot that we last received from the server
    // this is used to enforce that the object stay in the same place
    private Vector3 _lastRecvPos;
    private Quaternion _lastRecvRot;
    // The last position and rotation that we sent to the server
    private Vector3 _lastSentPos;
    private Quaternion _lastSentRot;
    /// <summary>
    /// Vars for testing
    /// </summary>
    private bool _isTesting;
    private ushort _testingID;
    private bool _isLocal;

    // How long after we anticipate someone
    // else taking ownership of this object,
    // until we presume that they won't take
    // it. If no one else claims it in that time
    // we take it back.
    const float TimeWaitingForNewOwner = 2f;
    /// <summary>
    /// How large of a tolerance we should allow
    /// for grabbed objects. This is to prevent
    /// excessive MovePosition/Rotation calls
    /// </summary>
    const float UngrabbedPositionTolerance = 0.005f; // 0.5cm
    const float UngrabbedRotationTolerance = 0.1f;

    const float GrabPositionTolerance = 0.05f; // 5cm
    const float GrabRotationTolerance = 5f;
    //const float GrabPositionTolerance = 5f; // 5m
    //const float GrabRotationTolerance = 180f;

    public void Init(SceneObject sceneObject, DRObject drObject)
    {
        //Debug.Log("Network object init " + _hasInit);
        SceneObject = sceneObject;
        _drObject = drObject;
        _didSubscribe = true;
        SceneObject.OnBehaviorsUpdated += OnBehaviorChange;
        SceneObject.OnOwnershipChange += OnOwnershipChange;
        _currentPriority = RealtimeNetworkUpdater.Instance.InitialPriority;
        _isForcedToRest = false;
        RealtimeNetworkUpdater.Instance.RegisterRealtimeObject(this);
    }
    public void EnableTestingMode(ushort testingID, bool isLocal)
    {
#if !UNITY_EDITOR
        Debug.LogError("Testing is only for editor");
#endif
        _isTesting = true;
        _testingID = testingID;
        _isLocal = isLocal;
        _currentPriority = RealtimeNetworkUpdater.Instance.InitialPriority;
        _isForcedToRest = false;
        if(isLocal)
            RealtimeNetworkUpdater.Instance.RegisterRealtimeObject(this);
    }
    /// <summary>
    /// Called when RealtimeUpdated is deciding which objects should have
    /// their message syncronized over the network.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="tag"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority)
    {
        // Debugging stuff
#if UNITY_EDITOR
        OwnerID = _drObject.OwnerID;
#endif
        tag = 0;
        priority = _currentPriority;
        // Do nothing if we don't own this
        if (!_isTesting && _drObject.OwnerID != DarkRiftConnection.Instance.OurID)
        {
            //Debug.Log("Not updating, owned by " + _drObject.OwnerID + " we are " + DarkRiftConnection.Instance.OurID);
            return false;
        }

        //Debug.Log("Networking updating object");
        Rigidbody rb = SceneObject.Rigidbody;
        if(_drObject.GrabbedBy == DarkRiftConnection.Instance.OurID)
        {
            // If this is a none type grab, then we synchronize like normal
            // We only do special handling for object follows hand
            GrabbableBehavior grabbableBehavior = SceneObject.GetBehaviorByType<GrabbableBehavior>();
            // grabbableBehavior will be null if we're grabbing in build mode
            if(grabbableBehavior == null || grabbableBehavior.GrabType == GrabbableBehavior.GrabTypes.ObjectFollowsHand)
                return false;

            if(rb == null)
            {
                Debug.LogError("No RB when grabbed, unhandled!");
                return false; // TODO handle this
            }
            // We're grabbing it, and its position isn't set via ObjectFollowsHand, so we send out a physics update
            priority = Math.Max(priority, RealtimeNetworkUpdater.Instance.MinPhysicsGrabbedPriority);
            tag = ServerTags.TransformObject_GrabPhysicsPosRotVelAngVel;
            writer.Write(_drObject.GetID());
            DRUser.GrabbingBodyPart bodyPart = grabbableBehavior.AddedPlayGrabbable.GrabbedBodyPart;
            writer.Write((byte)bodyPart);
            // Use the relative position / rotation
            UserManager.Instance.LocalUserDisplay.PoseDisplay.WorldPosRot2GrabRelative(bodyPart, rb.position, rb.rotation, out Vector3 grabPos, out Quaternion grabRot);
            writer.Write(grabPos.ToVec3(_workingVec));
            writer.Write(grabRot.ToQuat(_workingQuat));
            writer.Write(rb.velocity.ToVec3(_workingVec));
            writer.Write(rb.angularVelocity.ToVec3(_workingVec));
            _isForcedToRest = false;
            return true;
        }
        if(rb == null)
        {
            tag = ServerTags.TransformObject_PosRot;
            //If we have no RB, then we send out a message only if
            // the position or rotation have changed, or, if some time
            // has elapsed. This way
            if (transform.localPosition == _lastSentPos
                && transform.localRotation == _lastSentRot
                && _currentPriority < RealtimeNetworkUpdater.Instance.MinPriorityForNoChange)
                return false;
        }
        else
        {
            // If we're currently force to sleep, just re-check
            if (rb.IsSleeping())
            {
                if(_isForcedToRest)
                    return false;
                _isForcedToRest = true;
                DarkRiftWriter reliableWriter = DarkRiftWriter.Create();
                if(!_isTesting)
                    reliableWriter.Write(_drObject.GetID());
                else
                    reliableWriter.Write(_testingID);
                reliableWriter.Write(transform.localPosition.ToVec3(_workingVec));
                reliableWriter.Write(transform.localRotation.ToQuat(_workingQuat));
                RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.TransformObject_PosRot_Rest, reliableWriter);
                return false;
            }
            else if(_isForcedToRest)
            {
                //Debug.Log("Network object leaving rest");
                _isForcedToRest = false;
            }

            // If there's a rigidbody, we send out the message with velocity and angular velocity
            // Unless there's a MovingPlatform (which doesn't use velocity)
            tag = SceneObject.MovingPlatformBehavior == null ? ServerTags.TransformObject_PosRotVelAngVel : ServerTags.TransformObject_PosRot;
        }

        // Don't send out updates every frame unless it's really needed
        if (_currentPriority < RealtimeNetworkUpdater.Instance.MinPriority)
            return false;

        if(!_isTesting)
            writer.Write(_drObject.GetID());
        else
            writer.Write(_testingID);
        
        if(SceneObject.MovingPlatformBehavior != null)
        {
            writer.Write(SceneObject.MovingPlatformBehavior.Position.ToVec3(_workingVec));
            writer.Write(SceneObject.MovingPlatformBehavior.Rotation.ToQuat(_workingQuat));
        } else
        {
            writer.Write(transform.localPosition.ToVec3(_workingVec));
            writer.Write(transform.localRotation.ToQuat(_workingQuat));
        }
        // Send velocities if needed
        if(tag == ServerTags.TransformObject_PosRotVelAngVel)
        {
            writer.Write(rb.velocity.ToVec3(_workingVec));
            writer.Write(rb.angularVelocity.ToVec3(_workingVec));
            //Debug.Log("Sending pos rot vel with ID " + _drObject.GetID());
        }

        //Debug.Log("Sending tag " + tag);
        return true;
    }
    public void ServerSentForceRest(bool forceRest)
    {
        if (_isForcedToRest != forceRest)
        {
            Rigidbody rb = SceneObject.Rigidbody;
            if (forceRest && rb != null)
                rb.Sleep();
        }
        _isForcedToRest = forceRest;
    }
    public void ServerSentPosition(Vector3 position, bool forceToRest)
    {
        // Debugging stuff
#if UNITY_EDITOR
        FrameOnLastRecvInput = Time.frameCount;
        LastRecvSignal = "pos";
#endif
        Rigidbody rb = SceneObject.Rigidbody;
        if (rb != null)
        {
            // Moving platform moves via KCC
            if(SceneObject.MovingPlatformBehavior != null)
            {
                SceneObject.MovingPlatformBehavior.NetworkSetPosition(position);
            }
            else if (forceToRest || rb.isKinematic)
            {
                // Always do MovePosition for force to rest
                rb.MovePosition(position);
            }
            else
            {
                // Move Position messes with collisions
                // so we only do a MovePosition when the
                // position is particularly different
                // especially if the object is grabbed
                float delPos = (SceneObject.Rigidbody.position - position).sqrMagnitude;
                float tolerance = SceneObject.IsSomeoneGrabbing ? GrabPositionTolerance : UngrabbedPositionTolerance;

                // TODO this tolerance stuff should be reworked
                if(delPos > tolerance)
                {
                    //Debug.Log("Updating position, del pos " + delPos + " tolerance " + tolerance);
                    rb.MovePosition(position);
                }
                //else
                    //Debug.Log("Dropping grab pos update, del pos " + delPos + " tolerance " + tolerance);
            }
        }
        else
            transform.localPosition = position;
        _lastRecvPos = position;
        //Debug.Log("Updated position to " + position);
    }
    public void ServerSentRotation(Quaternion rotation, bool forceToRest)
    {
        // Debugging stuff
#if UNITY_EDITOR
        FrameOnLastRecvInput = Time.frameCount;
        LastRecvSignal = "rot";
#endif
        Rigidbody rb = SceneObject.Rigidbody;
        if (rb != null)
        {
            if (forceToRest || rb.isKinematic)
            {
                // Always do MoveRotation for force to rest
                rb.MoveRotation(rotation);
            }
            else
            {
                // MoveRotation messes with collisions
                // so we only do it when the
                // rotation is particularly different
                // especially if the object is grabbed
                float delRot = Quaternion.Angle(SceneObject.Rigidbody.rotation, rotation);
                float tolerance = SceneObject.IsSomeoneGrabbing ? GrabRotationTolerance : UngrabbedRotationTolerance;

                // TODO this tolerance stuff should be reworked
                if(delRot > tolerance)
                {
                    //Debug.Log("Updating rotation, del rot " + delRot + " tolerance " + tolerance);
                    rb.MoveRotation(rotation);
                }
                //else
                    //Debug.Log("Dropping rb rot update, del pos " + delRot + " tolerance " + tolerance);
            }
        }
        else
            transform.localRotation = rotation;
        _lastRecvRot = rotation;
    }
    public void ServerSentVelocity(Vector3 velocity)
    {
        // Debugging stuff
#if UNITY_EDITOR
        FrameOnLastRecvInput = Time.frameCount;
        LastRecvSignal = "vel";
#endif
        Rigidbody rb = SceneObject.Rigidbody;
        if(rb != null)
            rb.velocity = velocity;
        //Debug.Log("Vel now " + velocity.ToPrettyString() + " asleep " + rb.IsSleeping() + " obj #" + SceneObject.GetID() + " RB #" + rb.GetInstanceID());
    }
    public void ServerSentAngularVelocity(Vector3 angularVelocity)
    {
        // Debugging stuff
#if UNITY_EDITOR
        FrameOnLastRecvInput = Time.frameCount;
        LastRecvSignal = "angVel";
#endif
        Rigidbody rb = SceneObject.Rigidbody;
        if(rb != null)
            rb.angularVelocity = angularVelocity;
    }
    public void ServerSentGrabPhysicsUpdate(ushort grabberID, byte bodyPartID, Vector3 grabPos, Quaternion grabRot, Vector3 velocity, Vector3 angularVelocity)
    {
        // Debugging stuff
#if UNITY_EDITOR
        FrameOnLastRecvInput = Time.frameCount;
        LastRecvSignal = "grabPhysics";
#endif
        Rigidbody rb = SceneObject.Rigidbody;
        if(rb == null)
        {
            Debug.LogError("We have a NetworkObject, but no RB! On " + SceneObject.GetID());
            return;
        }
        // We have to transform the provided grabPos/Rot into the world space pos/rot
        if(!UserManager.Instance.TryGetUserDisplay(grabberID, out UserDisplay userDisplay))
        {
            Debug.LogError("Failed to handle grab physics, no user #" + grabberID);
            return;
        }
        userDisplay.PoseDisplay.RelativePosRot2World((DRUser.GrabbingBodyPart)bodyPartID, grabPos, grabRot, out Vector3 worldPos, out Quaternion worldRot);

        // We're now definitely no longer at rest
        _isForcedToRest = false;

        ServerSentPosition(worldPos, false);
        ServerSentRotation(worldRot, false);
        //rb.MovePosition(worldPos);
        //rb.MoveRotation(worldRot);
        // velocities are already in world space
        rb.velocity = velocity;
        rb.angularVelocity = angularVelocity;
    }
    // TODO we should really have this setup such that we receive
    // the compressed data. This way our copy closely matches
    // the network copy
    public void ClearPriority()
    {
        _currentPriority = 0;
        if (_isTesting)
            return;
        // Update our object with the value that was to the server
        transform.localPosition.ToVec3(_drObject.Position);
        transform.localRotation.ToQuat(_drObject.Rotation);
        _lastSentPos = transform.localPosition;
        _lastSentRot = transform.localRotation;
    }
    public ushort GetObjectID()
    {
        if (_drObject == null)
            return 0;
        return _drObject.GetID();
    }
    public DRObject GetDRObject()
    {
        return _drObject;
    }
    /// <summary>
    /// Sometimes our best guess is that someone else is going to
    /// take ownership. So, we locally lose ownership, and wait
    /// for the server to tell us that there's a new owner. If no
    /// new owner is announced in a period of time, then we locally
    /// take back ownership
    /// </summary>
    /// <param name="expectedNewOwner"></param>
    public void AnticipateLosingOwnership(ushort expectedNewOwner, uint expectedNewOwnershipTime)
    {
        if (_waitForNewOwnerRoutine != null)
            StopCoroutine(_waitForNewOwnerRoutine);
        _waitForNewOwnerRoutine = StartCoroutine(WaitForNewOwner(expectedNewOwner, expectedNewOwnershipTime));
    }
    IEnumerator WaitForNewOwner(ushort expectedNewOwner, uint expectedNewOwnershipTime)
    {
        // Give up on us taking the ownership, as we now expect
        // someone else to take it
        if(_timeStartAnticipatedOwnership > 0)
        {
            _timeStartAnticipatedOwnership = -1f;
            _drObject.GiveUpAnticipatedOwnership();
        }
        bool anticipatingOwner = _drObject.IsAnticipatingOwner;
        ushort prevOwner = _drObject.OwnerID;
        bool anticipatingOwnershipTime = _drObject.IsAnticipatingOwnershipTime;
        uint prevOwnershipTime = _drObject.OwnershipTime;

        Debug.Log("Anticipated owner for obj #" + _drObject.GetID() + " will have ownership from " + expectedNewOwner + " in the future");
        _drObject.SetAnticipatedOwner(expectedNewOwner);
        _drObject.SetAnticipatedOwnershipTime(expectedNewOwnershipTime);
        float startTime = Time.realtimeSinceStartup;
        while (_drObject.IsAnticipatingOwner && startTime + TimeWaitingForNewOwner < Time.realtimeSinceStartup)
            yield return null;
        if (_drObject.IsAnticipatingOwner)
        {
            // If we're still in anticipation, but we're anticipating something
            // different, then we return. I don't think this would ever happen
            if(_drObject.OwnerID != expectedNewOwner)
            {
                Debug.LogWarning("State changed unexpectedly while waiting for new owner? anticipated "
                    + expectedNewOwner + "/" + expectedNewOwnershipTime + " now: " + _drObject.OwnerID + "/" + _drObject.OwnershipTime);
                yield break;
            }

            if(prevOwner == DarkRiftConnection.Instance.OurID)
                Debug.Log("No one else claimed ownership, so we took it back");
            else
                Debug.Log("No one else claimed ownership, so we gave back control to " + prevOwner);
            // return object to pre-anticipated state
            if (!anticipatingOwner)
                _drObject.OwnerID = prevOwner;
            else
                _drObject.SetAnticipatedOwner(prevOwner);
            if (!anticipatingOwnershipTime)
                _drObject.OwnershipTime = prevOwnershipTime;
            else
                _drObject.SetAnticipatedOwnershipTime(prevOwnershipTime);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if(_frameOfLastCollision != Time.frameCount)
        {
            _frameOfLastCollision = Time.frameCount;
            uint prevPriority = _currentPriority;
            _currentPriority = ExtensionMethods.ClampedAdd(_currentPriority, RealtimeNetworkUpdater.Instance.PriorityIncreasePerCollision);
            //Debug.Log("Collision increased priority " + prevPriority + "->" + _currentPriority);
        }

        // TODO we may want to set the anticipated owner if two objects,
        // neither of which we own collide. We would in that situation
        // have a best-guess that ought to be reasonable most of the
        // time. 

        //Debug.Log("network collision");
        if (SceneObject == null)
            return;

        //Debug.Log("a");
        if (SceneObject.CurrentGrabState == SceneObject.GrabState.GrabbedByOther)
            return;

        bool areWeGrabbing = SceneObject.CurrentGrabState == SceneObject.GrabState.GrabbedBySelf
            || SceneObject.CurrentGrabState == SceneObject.GrabState.PendingGrabbedBySelf;
        bool doWeOwn = _drObject.OwnerID == DarkRiftConnection.Instance.OurID;
        //Debug.Log("grab: " + areWeGrabbing + " own: " + doWeOwn);

        // If we don't own this object return
        if (!areWeGrabbing && !doWeOwn)
            return;

        //Debug.Log(SceneObject.Name + " Hit " + collision.collider.gameObject.layer);
        // If we hit the table top, or another player, return
        if (((1 << collision.collider.gameObject.layer) & GLLayers.AllObjectWithTakeableOwnership) == 0)
            return;

        //Debug.Log("c");
        // If the other object is not networked, return
        NetworkObject otherNetworkObj = collision.collider.gameObject.GetNetworkObjectFromParent();
        if (otherNetworkObj == null)
        {
            Debug.LogWarning("Failed to get NetObj on " + collision.collider.gameObject.name);
            return;
        }

        //Debug.Log("d");
        // If we own the other object, return
        int otherOwner = otherNetworkObj.GetCurrentOwner();
        if (otherOwner == DarkRiftConnection.Instance.OurID)
            return;

        //Debug.Log("e");
        if (otherNetworkObj.IsSomeoneGrabbing())
        {
            // If someone is grabbing the other object,
            // and we're not grabbing this one, we
            // anticipate losing ownership of the object,
            // and immediately begin a timer, but, we add a timer
            // and if no one else claims ownership, we locally
            // resume ownership
            if(!areWeGrabbing)
                AnticipateLosingOwnership(otherNetworkObj.GetCurrentOwner(), DarkRiftPingTime.Instance.ServerTime);
            return;
        }

        // Check to see if we should take ownership of the other object
        // Definitely take ownership if we're grabbing
        if (areWeGrabbing)
        {
            Debug.Log("taking ownership from grab collision");
            otherNetworkObj.TakeOwnershipFromCollision(DarkRiftPingTime.Instance.ServerTime);
            return;
        }

        if (DarkRiftConnection.Instance.CanTakeOwnership(_drObject, otherNetworkObj.GetDRObject()))
            otherNetworkObj.TakeOwnershipFromCollision(_drObject.OwnershipTime);
        else
            AnticipateLosingOwnership(otherNetworkObj.GetCurrentOwner(), otherNetworkObj.GetDRObject().OwnershipTime);
    }
    public void TakeOwnershipFromCollision(uint timeOfNewOwnership)
    {
        Debug.Log("Taking ownership due to collision obj " + _drObject.GetID());
        _drObject.SetAnticipatedOwner(DarkRiftConnection.Instance.OurID);
        _drObject.SetAnticipatedOwnershipTime(timeOfNewOwnership);
        _timeStartAnticipatedOwnership = Time.realtimeSinceStartup;
        DarkRiftConnection.Instance.TakeOwnership(_drObject, timeOfNewOwnership);
        _isForcedToRest = false;
        _currentPriority = RealtimeNetworkUpdater.Instance.InitialPriority;
    }
    public ushort GetCurrentOwner()
    {
        if (_drObject == null)
            return 0;
        return _drObject.OwnerID;
    }
    public bool DoWeOwn()
    {
        return !_isTesting ? GetCurrentOwner() == DarkRiftConnection.Instance.OurID : _isLocal;
    }
    public bool IsSomeoneGrabbing()
    {
        return SceneObject.CurrentGrabState != SceneObject.GrabState.Ungrabbed;
    }
    private void OnDestroy()
    {
        if(SceneObject != null)
        {
            if (_didSubscribe)
            {
                _didSubscribe = false;
                SceneObject.OnBehaviorsUpdated -= OnBehaviorChange;
                SceneObject.OnOwnershipChange -= OnOwnershipChange;
                RealtimeNetworkUpdater.Instance.RemoveRealtimeObject(this);
            }
            SceneObject = null;
        }
        _drObject = null;
    }
    void OnBehaviorChange()
    {
    }
    void OnOwnershipChange(ushort prevOwner, ushort newOwner)
    {
        // We clear the buffer, so that there is a quick jump as we move to
        // a different user's simulation
        Debug.Log("Clearing buffer due to ownership change for obj " + SceneObject.Name + " was #" + prevOwner + " now #" + newOwner);
        _isForcedToRest = false;
        _currentPriority = RealtimeNetworkUpdater.Instance.InitialPriority;
        _timeStartAnticipatedOwnership = -1f;
    }
    /// <summary>
    /// Called when another behavior or script moves this object
    /// </summary>
    public void BehaviorRequestNoLongerAtRest()
    {
        _isForcedToRest = false;
    }
    void Update()
    {
        // Only network update if not paused
        if (!TimeManager.Instance.IsPlayingOrStepped)
            return;
        if(SceneObject == null)
        {
            Debug.LogError("NetworkObject has no SceneObject! " + name, this);
            return;
        }
        if(_timeStartAnticipatedOwnership > 0
            && Time.realtimeSinceStartup - _timeStartAnticipatedOwnership >= Mathf.Max(MaxRTTUntilGiveUpOwnership * DarkRiftPingTime.Instance.PreviousRTT, 10f))
        {
            Debug.LogWarning("giving up on ownership, waited " + (Time.realtimeSinceStartup - _timeStartAnticipatedOwnership));
            _drObject.GiveUpAnticipatedOwnership();
            _timeStartAnticipatedOwnership = -1f;
        }
        bool doWeOwn = DoWeOwn();
        // If someone else is simulating this, and they last set to be asleep
        // just keep is asleep
        if (_isForcedToRest && !doWeOwn)
        {
            // If the object moves at all, then we need
            // to force it back to sleep and move it back to where it was
            Rigidbody rb = SceneObject.Rigidbody;
            if (rb != null && !rb.IsSleeping())
            {
                rb.Sleep();
                rb.MovePosition(_lastRecvPos);
                rb.MoveRotation(_lastRecvRot);
            }
        }
        //uint priority = _currentPriority;
        //only increase priority if we've moved
        if (doWeOwn && !_isForcedToRest)
            _currentPriority = ExtensionMethods.ClampedAdd(_currentPriority, 1);
        else
            _currentPriority = 0;
        //Debug.Log("Update increased priority " + priority + "->" + _currentPriority);
    }
}
