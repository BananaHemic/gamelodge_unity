using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DarkRift;
using Miniscript;
using UnityEngine;

public class GrabbableBehavior : BaseBehavior
{
    // NB If you re-order this, update Int2GrabType
    public enum GrabTypes
    {
        ObjectFollowsHand,
        HandFollowsObject,
        None,
    }

    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();
    private static bool _hasLoadedIntrinsics = false;
    public static readonly int TagRequestPriority = 1;
    public static readonly ValString IndexTriggerEventName = ValString.Create("OnGrabTrigger", false);
    public static readonly ValString IndexTriggerDownEventName = ValString.Create("OnGrabTriggerDown", false);
    public static readonly ValString IndexTriggerUpEventName = ValString.Create("OnGrabTriggerUp", false);
    public static readonly ValString OnGrabStartEventName = ValString.Create("OnGrabStart", false);
    public static readonly ValString OnSecondGrabStartEventName = ValString.Create("OnSecondGrabStart", false);
    public static readonly ValString OnGrabEndEventName = ValString.Create("OnGrabEnd", false);
    public static readonly ValString OnSecondGrabEndEventName = ValString.Create("OnSecondGrabEnd", false);
    public static readonly ValString HandTypeValStr = ValString.Create("hand", false);
    const int GrabTypeKey = 0;

    public GrabTypes GrabType;
    public PlayGrabbable AddedPlayGrabbable { get; private set; }

    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private bool _wasGrabTriggerDown = false;

    protected override void ChildInit()
    {
        base._sceneObject.BehaviorRequestedTag(GLLayers.GrabbableTag, this, TagRequestPriority);
        if (AddedPlayGrabbable == null)
            AddedPlayGrabbable = gameObject.AddComponent<PlayGrabbable>();
        AddedPlayGrabbable.Init(_sceneObject, this);
    }
    private static int GrabType2Int(GrabTypes grabTypes)
    {
        return (int)grabTypes;
    }
    private static bool Int2GrabType(int grabInt, out GrabTypes grabType)
    {
        //TODO I know that we can use Enum.IsDefined instead, but iirc that has some
        // unexpected behavior at times, so for now we just do the naive way
        switch(grabInt)
        {
            case 0:
                grabType = GrabTypes.ObjectFollowsHand;
                return true;
            case 1:
                grabType = GrabTypes.HandFollowsObject;
                return true;
            case 2:
                grabType = GrabTypes.None;
                return true;
            default:
                Debug.LogError("Uknown grab type " + grabInt);
                grabType = GrabTypes.ObjectFollowsHand;
                return false;
        }
    }
    private void OnDestroy()
    {
        if (Orchestrator.Instance == null || Orchestrator.Instance.IsAppClosing)
            return;
        //Debug.Log("Reverting tag to " + _prevTag);
    }
    public override void Destroy()
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        if (AddedPlayGrabbable != null)
            Destroy(AddedPlayGrabbable);
        AddedPlayGrabbable = null;
        Destroy(this);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        //TODO use a zero-allocation int -> array function
        // Grab Type
        _serializedBehavior.LocallySetData(GrabTypeKey, BitConverter.GetBytes(GrabType2Int(GrabType)));
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Grab Type
        byte[] grabTypeArray;
        if (_serializedBehavior.TryReadProperty(GrabTypeKey, out grabTypeArray, out int _))
        {
            int grabInt = BitConverter.ToInt32(grabTypeArray, 0);
            GrabTypes grabType;
            if (!Int2GrabType(grabInt, out grabType))
                Debug.LogError("Failed to handle received grab int of " + grabInt + "!");
            else
                GrabType = grabType;
        }
    }
    public void OnGrabStart(DRUser.GrabbingBodyPart bodyPart)
    {
        if (AddedPlayGrabbable.IsIdle)
            _sceneObject.InvokeEventOnBehaviors(OnGrabStartEventName);
        else
            _sceneObject.InvokeEventOnBehaviors(OnSecondGrabStartEventName);
    }
    public void OnGrabEnd(DRUser.GrabbingBodyPart bodyPart)
    {
        if (AddedPlayGrabbable.IsIdle)
            _sceneObject.InvokeEventOnBehaviors(OnGrabEndEventName);
        else
            _sceneObject.InvokeEventOnBehaviors(OnSecondGrabEndEventName);
    }
    public override List<ExposedEvent> GetEvents()
    {
        if(_userEvents.Count == 0)
        {
            _userEvents.Add(new ExposedEvent(IndexTriggerDownEventName, "Runs when the object is grabbed and the local user STARTS hitting trigger", null));
            _userEvents.Add(new ExposedEvent(IndexTriggerUpEventName, "Runs when the object is grabbed and the local user STOPS hitting trigger", null));
            _userEvents.Add(new ExposedEvent(IndexTriggerEventName, "Runs every frame when the object is grabbed and the local user is hitting trigger", null));
            _userEvents.Add(new ExposedEvent(OnGrabStartEventName, "Called when the object is grabbed", null));
            _userEvents.Add(new ExposedEvent(OnSecondGrabStartEventName, "Called when another controller grabs this object", null));
            _userEvents.Add(new ExposedEvent(OnGrabEndEventName, "Called when the object is no longer grabbed", null));
            _userEvents.Add(new ExposedEvent(OnSecondGrabEndEventName, "Called when the second controller ungrabs", null));
            return _userEvents;
        }
        return _userEvents;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("GetGrabState");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Retrieves the current grab state of this object (\"ungrabbed\", \"pendingGrabbedBySelf\", \"grabbedBySelf\", \"grabbedByOther\", \"pendingUngrabbed\"", "grabState"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetGrabState call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject.GrabState grabState = sceneObject.CurrentGrabState;
            switch (grabState)
            {
                case SceneObject.GrabState.Ungrabbed:
                    return new Intrinsic.Result("ungrabbed");
                case SceneObject.GrabState.PendingGrabbedBySelf:
                    return new Intrinsic.Result("pendingGrabbedBySelf");
                case SceneObject.GrabState.GrabbedBySelf:
                    return new Intrinsic.Result("grabbedBySelf");
                case SceneObject.GrabState.GrabbedByOther:
                    return new Intrinsic.Result("grabbedByOther");
                case SceneObject.GrabState.PendingUngrabbed:
                    return new Intrinsic.Result("pendingUngrabbed");
                default:
                    Debug.LogError("Unhandled grab state! " + grabState);
                    return Intrinsic.Result.Null;
            }
		};

        intrinsic = Intrinsic.Create("GetGrabbingUser");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the user who is currently grabbing this object", "grabbingUser"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetGrabbingUser call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject.GrabState grabState = sceneObject.CurrentGrabState;
            switch (grabState)
            {
                case SceneObject.GrabState.Ungrabbed:
                case SceneObject.GrabState.PendingUngrabbed:
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.PendingGrabbedBySelf:
                case SceneObject.GrabState.GrabbedBySelf:
                    return new Intrinsic.Result(UserManager.Instance.GetLocalValUser());
                case SceneObject.GrabState.GrabbedByOther:
                    return new Intrinsic.Result(UserManager.Instance.GetValUser(sceneObject.GrabbingUser));
                default:
                    Debug.LogError("Unhandled grab state! " + grabState);
                    return Intrinsic.Result.Null;
            }
		};

        // Get the pos/rot of the object if it moved by ObjectFollowsHand
        intrinsic = Intrinsic.Create("GetInstantGrabbedPosRotVel");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the position/rotation/velocity/angularVelocity that the object would have if it was a ObjectFollowsHand movement type. Use this to build your own movement systems", "objPosRotVel"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetInstantGrabbedPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject.GrabState grabState = sceneObject.CurrentGrabState;
            GrabbableBehavior grabbable = sceneObject.GetBehaviorByType<GrabbableBehavior>();
            if(grabbable == null)
            {
                UserScriptManager.LogToCode(context, "No grabbable on object for GetInstantGrabbedPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            PlayGrabbable playGrabbable = grabbable.AddedPlayGrabbable;
            Vector3 position, velocity, angularVelocity;
            Quaternion rotation;
            switch (grabState)
            {
                case SceneObject.GrabState.Ungrabbed:
                case SceneObject.GrabState.PendingUngrabbed:
                    Debug.Log("No one grabbing, state is " + grabState);
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.PendingGrabbedBySelf:
                case SceneObject.GrabState.GrabbedBySelf:
                    if (UserManager.Instance.LocalUserDisplay.PoseDisplay.HandDisplay.GetInstantPositionRotationOfObject(playGrabbable, out position, out rotation, out velocity, out angularVelocity))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation, velocity, angularVelocity));
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.GrabbedByOther:
                    UserDisplay userDisplay;
                    if(!UserManager.Instance.TryGetUserDisplay(sceneObject.GrabbingUser, out userDisplay))
                    {
                        UserScriptManager.LogToCode(context, "No user #" + sceneObject.GrabbingUser + " for GetInstantGrabbedPosRot call!", UserScriptManager.CodeLogType.Warning);
                        return Intrinsic.Result.Null;
                    }
                    if (userDisplay.PoseDisplay.HandDisplay.GetInstantPositionRotationOfObject(playGrabbable, out position, out rotation, out velocity, out angularVelocity))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation, velocity, angularVelocity));
                    Debug.LogWarning("Failed to get instant posrot of obj");
                    return Intrinsic.Result.Null;
                default:
                    Debug.LogError("Unhandled grab state! " + grabState);
                    return Intrinsic.Result.Null;
            }
		};

        intrinsic = Intrinsic.Create("GetVirtualHandPosRot");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the position/rotation of the virtual hand. Normally used in HandFollowsObject or None grabbable type, to determine where the visible hand is", "handPosRot"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetVirtualHandPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject.GrabState grabState = sceneObject.CurrentGrabState;
            GrabbableBehavior grabbable = sceneObject.GetBehaviorByType<GrabbableBehavior>();
            if(grabbable == null)
            {
                UserScriptManager.LogToCode(context, "No grabbable on object for GetVirtualHandPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            PlayGrabbable playGrabbable = grabbable.AddedPlayGrabbable;
            Vector3 position;
            Quaternion rotation;
            switch (grabState)
            {
                case SceneObject.GrabState.Ungrabbed:
                case SceneObject.GrabState.PendingUngrabbed:
                    //Debug.Log("No one grabbing, state is " + grabState);
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.PendingGrabbedBySelf:
                case SceneObject.GrabState.GrabbedBySelf:
                    if (UserManager.Instance.LocalUserDisplay.PoseDisplay.HandDisplay.GetVirtualPositionRotationOfHand(playGrabbable, out position, out rotation))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation));
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.GrabbedByOther:
                    UserDisplay userDisplay;
                    if(!UserManager.Instance.TryGetUserDisplay(sceneObject.GrabbingUser, out userDisplay))
                    {
                        UserScriptManager.LogToCode(context, "No user #" + sceneObject.GrabbingUser + " for GetVirtualHandPosRot call!", UserScriptManager.CodeLogType.Warning);
                        return Intrinsic.Result.Null;
                    }
                    if (userDisplay.PoseDisplay.HandDisplay.GetVirtualPositionRotationOfHand(playGrabbable, out position, out rotation))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation));
                    return Intrinsic.Result.Null;
                default:
                    Debug.LogError("Unhandled grab state! " + grabState);
                    return Intrinsic.Result.Null;
            }
		};
        intrinsic = Intrinsic.Create("GetRealHandPosRot");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the position/rotation of the user's real hand. Normally used in HandFollowsObject or None grabbable type, to determine where the user's actual hand is", "handPosRot"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetRealHandPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject.GrabState grabState = sceneObject.CurrentGrabState;
            GrabbableBehavior grabbable = sceneObject.GetBehaviorByType<GrabbableBehavior>();
            if(grabbable == null)
            {
                UserScriptManager.LogToCode(context, "No grabbable on object for GetRealHandPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            PlayGrabbable playGrabbable = grabbable.AddedPlayGrabbable;
            Vector3 position;
            Quaternion rotation;
            switch (grabState)
            {
                case SceneObject.GrabState.Ungrabbed:
                case SceneObject.GrabState.PendingUngrabbed:
                    //Debug.Log("No one grabbing, state is " + grabState);
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.PendingGrabbedBySelf:
                case SceneObject.GrabState.GrabbedBySelf:
                    if (UserManager.Instance.LocalUserDisplay.PoseDisplay.HandDisplay.GetRealPositionRotationOfHand(playGrabbable, out position, out rotation))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation));
                    return Intrinsic.Result.Null;
                case SceneObject.GrabState.GrabbedByOther:
                    UserDisplay userDisplay;
                    if(!UserManager.Instance.TryGetUserDisplay(sceneObject.GrabbingUser, out userDisplay))
                    {
                        UserScriptManager.LogToCode(context, "No user #" + sceneObject.GrabbingUser + " for GetRealHandPosRot call!", UserScriptManager.CodeLogType.Warning);
                        return Intrinsic.Result.Null;
                    }
                    if (userDisplay.PoseDisplay.HandDisplay.GetRealPositionRotationOfHand(playGrabbable, out position, out rotation))
                        return new Intrinsic.Result(UserScriptManager.IntoMap(position, rotation));
                    return Intrinsic.Result.Null;
                default:
                    Debug.LogError("Unhandled grab state! " + grabState);
                    return Intrinsic.Result.Null;
            }
		};
        intrinsic = Intrinsic.Create("GetGrabRelativePosRot");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the relative position/rotation from the user's hand to the object. If the object is a HandFollowsObject, the relative position is from the object to the user's hand", "grabRelPosRot"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetGrabRelativePosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            GrabbableBehavior grabbable = sceneObject.GetBehaviorByType<GrabbableBehavior>();
            if(grabbable == null)
            {
                UserScriptManager.LogToCode(context, "No grabbable on object for GetGrabRelativePosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PlayGrabbable playGrabbable = grabbable.AddedPlayGrabbable;
            ValMap posRot = ValMap.Create();
            posRot[ValString.positionStr] = new ValVector3(playGrabbable.RelPos);
            posRot[ValString.rotationStr] = new ValQuaternion(playGrabbable.RelRot);
            return new Intrinsic.Result(posRot);
		};
        intrinsic = Intrinsic.Create("GetHandPose");
        intrinsic.AddParam(HandTypeValStr.value, "right");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the model's hand pose for the selected hand. Provide the hand you want (left/right). Returns 0 if there is no hand pose", "handPose"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetHandPose call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            GrabbableBehavior grabbable = sceneObject.GetBehaviorByType<GrabbableBehavior>();
            if(grabbable == null)
            {
                UserScriptManager.LogToCode(context, "No grabbable on object for GetHandPose call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PlayGrabbable playGrabbable = grabbable.AddedPlayGrabbable;
            var handPose = playGrabbable.GetHandPose();
            if(handPose != null)
            {
                ValString selectedHandStr = context.GetVar(HandTypeValStr) as ValString;
                string handStr = selectedHandStr?.value;
                int primaryHandIndex;
                if (handStr != null && handStr == "left")
                    primaryHandIndex = 0;
                else
                    primaryHandIndex = 1;

                var primaryHand = handPose.skeletonMainPose.GetHand(primaryHandIndex);
                if(primaryHand != null)
                {
                    // We need the position of the grabbed object, relative to the wrist
                    // so we get the world space position/rotation and then transform it to the
                    // wrist's local position
                    if(primaryHandIndex > 0 && primaryHandIndex < HandPoser.Instances.Length)
                    {
                        Vector3 handPosePos = primaryHand.position;
                        Quaternion handPoseRot = primaryHand.rotation;

                        ValMap posRot = ValMap.Create();
                        posRot[ValString.positionStr] = new ValVector3(handPosePos);
                        posRot[ValString.rotationStr] = new ValQuaternion(handPoseRot);
                        return new Intrinsic.Result(posRot);
                    }
                }
            }
            return Intrinsic.Result.False;
		};
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return true;
    }
    public override bool DoesRequireCollider()
    {
        return true;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public override void RefreshProperties() { }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
    private void Update()
    {
        bool grabTriggerDownThisFrame = false;
        // If this object is grabbed by us, and we have trigger down,
        // fire the trigger event
        if(AddedPlayGrabbable != null
            && !AddedPlayGrabbable.IsIdle
            && _sceneObject.AreWeGrabbing)
        {
            ControllerAbstraction controller = AddedPlayGrabbable.GetGrabbingController();
            if (controller != null && controller.GetPlayTrigger())
            {
                // Fire events for when the grab-trigger starts
                if (!_wasGrabTriggerDown)
                {
                    _sceneObject.InvokeEventOnBehaviors(IndexTriggerDownEventName);
                    _wasGrabTriggerDown = true;
                }
                _sceneObject.InvokeEventOnBehaviors(IndexTriggerEventName);
                grabTriggerDownThisFrame = true;
            }
        }

        if(_wasGrabTriggerDown && !grabTriggerDownThisFrame)
        {
            _sceneObject.InvokeEventOnBehaviors(IndexTriggerUpEventName);
            _wasGrabTriggerDown = false;
        }
    }
}
