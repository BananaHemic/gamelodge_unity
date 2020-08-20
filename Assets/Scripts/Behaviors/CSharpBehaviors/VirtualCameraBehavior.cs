using Cinemachine;
using Miniscript;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirtualCameraBehavior : BaseBehavior
{
    public int Priority = 10;
    public SceneObject LookAtObject;
    public SceneObject FollowObject;

    private CinemachineTargetGroup _createdFollowGroupTarget;
    private CinemachineTargetGroup _createdLookAtGroupTarget;
    private CinemachineComponentBase _addedFollowComponent;
    private CinemachineComponentBase _addedAimComponent;
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static bool _hasLoadedIntrinsics = false;
    private static readonly ValString PriorityVal = ValString.Create("priority", false);
    private static readonly ValString ObjectsVal = ValString.Create("objs", false);
    private readonly SerializedSceneObjectReference _lookAtObjReference = new SerializedSceneObjectReference(nameof(LookAtObject));
    private readonly SerializedSceneObjectReference _followObjReference = new SerializedSceneObjectReference(nameof(FollowObject));
    const int LookAtObjDescriptorKey = 0;
    const int FollowDescriptorKey = 1;
    const int PriorityKey = 2;

    //private CinemachineVirtualCamera _virtualCamera;
    private CinemachineNewVirtualCamera _virtualCamera;
    private bool _waitingForGamestateObjects = false;

    protected override void ChildInit()
    {
        //_virtualCamera = gameObject.AddComponent<CinemachineVirtualCamera>();
        _virtualCamera = gameObject.AddComponent<CinemachineNewVirtualCamera>();
        if (!enabled)
            _virtualCamera.enabled = false;
        base.AddSceneObjectReference(_lookAtObjReference);
        base.AddSceneObjectReference(_followObjReference);
        RefreshProperties();
    }
    private void OnEnable()
    {
        if (_virtualCamera != null)
            _virtualCamera.enabled = true;
    }
    private void OnDisable()
    {
        if (_virtualCamera != null)
            _virtualCamera.enabled = false;
    }
    public override void RefreshProperties()
    {
        // If the game state is loading, then we can't consistently get object
        // references, as they may not have loaded yet. So we instead just ask
        // Orchestrator to notify us when it's done
        if (Orchestrator.Instance.IsAddingObjectsFromGameState)
        {
            if (!_waitingForGamestateObjects)
            {
                Orchestrator.Instance.RefreshBehaviorAfterGamestateLoad(this);
                _waitingForGamestateObjects = true;
            }
            return;
        }

        // Look At
        LookAtObject = _lookAtObjReference.SceneObjectReference;
        if(_virtualCamera.LookAt == null)//TODO remove!
            _virtualCamera.LookAt = LookAtObject?.transform;
        // Follow Obj
        FollowObject = _followObjReference.SceneObjectReference;
        if(_virtualCamera.Follow == null)
            _virtualCamera.Follow = FollowObject?.transform;
        // Priority
        _virtualCamera.Priority = Priority;

        if (!Orchestrator.Instance.IsAddingObjectsFromGameState)
            _waitingForGamestateObjects = false;
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Look At
        byte[] lookAtArray;
        if (_serializedBehavior.TryReadProperty(LookAtObjDescriptorKey, out lookAtArray, out int _))
            _lookAtObjReference.UpdateFrom(lookAtArray);
        // Follow Obj
        byte[] followArray;
        if (_serializedBehavior.TryReadProperty(FollowDescriptorKey, out followArray, out int _))
            _followObjReference.UpdateFrom(followArray);
        // Priority
        byte[] priorityArray;
        if (_serializedBehavior.TryReadProperty(PriorityKey, out priorityArray, out int _))
            Priority = BitConverter.ToInt32(priorityArray, 0);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        // Look At
        _serializedBehavior.LocallySetData(LookAtObjDescriptorKey, _lookAtObjReference.GetSerialized(), SerializedBehavior.SceneObjectFlag);
        // Follow Obj
        _serializedBehavior.LocallySetData(FollowDescriptorKey, _followObjReference.GetSerialized(), SerializedBehavior.SceneObjectFlag);
        // Priority
        _serializedBehavior.LocallySetData(PriorityKey, BitConverter.GetBytes(Priority));
    }
    public override void Destroy()
    {
        if(_virtualCamera != null)
            Destroy(_virtualCamera);
        _virtualCamera = null;

        if(_createdFollowGroupTarget != null)
        {
            Destroy(_createdFollowGroupTarget.gameObject);
            _createdFollowGroupTarget = null;
        }
        if (_createdLookAtGroupTarget != null)
        {
            Destroy(_createdLookAtGroupTarget.gameObject);
            _createdLookAtGroupTarget = null;
        }
    }
    public override bool DoesRequireCollider() { return false; }
    public override bool DoesRequirePosRotScaleSyncing() { return false; }
    public override bool DoesRequireRigidbody() { return false; }
    public override List<ExposedEvent> GetEvents()
    {
        return null;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("SetVirtualPriority");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the priority for when this VirtualCamera is used", null));
        intrinsic.AddParam(PriorityVal.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetVirtualPriority call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValNumber priorityNum = context.GetVar(PriorityVal) as ValNumber;
            if (priorityNum == null)
                return Intrinsic.Result.Null;
            int priority = (int)priorityNum.value;

            // TODO update UI
            camBehavior.Priority = priority;
            camBehavior.RefreshProperties();

            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetFollow");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets what this camera should be following", null));
        intrinsic.AddParam(ObjectsVal.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetFollow call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Value objectsVal = context.GetVar(ObjectsVal);
            ValList objectList = objectsVal as ValList;
            if(objectList != null)
            {
                if (camBehavior._createdFollowGroupTarget == null)
                {
                    GameObject obj = new GameObject("follow");
                    camBehavior._createdFollowGroupTarget = obj.AddComponent<CinemachineTargetGroup>();
                }
                else
                {
                    // TODO
                    camBehavior._createdFollowGroupTarget.m_Targets = new CinemachineTargetGroup.Target[0];
                }
                for (int i = 0; i < objectList.Count; i++)
                {
                    ValSceneObject vSceneObject = objectList[i] as ValSceneObject;
                    if(vSceneObject == null)
                    {
                        UserScriptManager.LogToCode(context, "VirtualCameraBehavior SetFollow received a non-sceneobject!", UserScriptManager.CodeLogType.Error);
                        return Intrinsic.Result.Null;
                    }
                    camBehavior._createdFollowGroupTarget.AddMember(vSceneObject.SceneObject.transform, 1, 0);
                }

                // TODO serialize this, and show in UI
                camBehavior._virtualCamera.Follow = camBehavior._createdFollowGroupTarget.transform;
                Debug.Log("Created follow ", camBehavior._createdFollowGroupTarget.gameObject);
            }
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetLookAt");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets what this camera should be looking at", null));
        intrinsic.AddParam(ObjectsVal.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetLookAt call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Value objectsVal = context.GetVar(ObjectsVal);
            ValList objectList = objectsVal as ValList;
            if(objectList != null)
            {
                if (camBehavior._createdLookAtGroupTarget == null)
                {
                    GameObject obj = new GameObject("look_at");
                    camBehavior._createdLookAtGroupTarget = obj.AddComponent<CinemachineTargetGroup>();
                }
                else
                {
                    // TODO
                    camBehavior._createdLookAtGroupTarget.m_Targets = new CinemachineTargetGroup.Target[0];
                }
                for (int i = 0; i < objectList.Count; i++)
                {
                    ValSceneObject vSceneObject = objectList[i] as ValSceneObject;
                    if(vSceneObject == null)
                    {
                        UserScriptManager.LogToCode(context, "VirtualCameraBehavior SetLookAt received a non-sceneobject!", UserScriptManager.CodeLogType.Error);
                        return Intrinsic.Result.Null;
                    }
                    camBehavior._createdLookAtGroupTarget.AddMember(vSceneObject.SceneObject.transform, 1, 0);
                }

                // TODO serialize this, and show in UI
                camBehavior._virtualCamera.LookAt = camBehavior._createdLookAtGroupTarget.transform;
            }
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetBody");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Set how this camera follows", null));
        intrinsic.AddParam(ObjectsVal.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetBody call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValString bodyTypeVal = context.GetVar(ObjectsVal) as ValString;
            //camBehavior._virtualCamera.InvalidateComponentCache();

            if (bodyTypeVal.value == "transposer")
            {
                // NOTE: normally, is returns false when the object is null, but that does not seem to be happening here.
                // possibly because unity overloads the null comparison? Not sure
                //Debug.Log("null " + (_addedFollowComponent == null) + " is " + (_addedFollowComponent is CinemachineTransposer));
                if(camBehavior._addedFollowComponent == null || !(camBehavior._addedFollowComponent is CinemachineTransposer))
                {
                    if (camBehavior._addedFollowComponent != null)
                        Destroy(camBehavior._addedFollowComponent);
                    camBehavior._addedFollowComponent = camBehavior._virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
                }
                else
                {
                    Debug.Log("already transposer");
                }
            }
            else if (bodyTypeVal.value == "orbitaltransposer")
            {
                if(camBehavior._addedFollowComponent == null || !(camBehavior._addedFollowComponent is CinemachineOrbitalTransposer))
                {
                    if (camBehavior._addedFollowComponent != null)
                        Destroy(camBehavior._addedFollowComponent);
                    camBehavior._addedFollowComponent = camBehavior._virtualCamera.AddCinemachineComponent<CinemachineOrbitalTransposer>();
                    var orb = (CinemachineOrbitalTransposer)camBehavior._addedFollowComponent;
                    orb.m_XAxis.m_MaxSpeed = 10f;
                    orb.m_XAxis.m_Recentering.m_enabled = false;
                    orb.m_XAxis.m_InputAxisName = "Horizontal";
                }
            }
            else
            {
                UserScriptManager.LogToCode(context, "Unhandled body type " + bodyTypeVal.value, UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Debug.Log("added follow: " + (camBehavior._addedFollowComponent != null).ToString(), camBehavior.gameObject);
            //camBehavior._virtualCamera.InvalidateComponentCache();

            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetAim");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Set how this camera looks at the target", null));
        intrinsic.AddParam(ObjectsVal.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetAim call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValString aimTypeVal = context.GetVar(ObjectsVal) as ValString;
            //camBehavior._virtualCamera.InvalidateComponentCache();

            if (aimTypeVal.value == "none")
            { }// TODO
            else if (aimTypeVal.value == "hard")
            {
                if(camBehavior._addedAimComponent == null || !(camBehavior._addedAimComponent is CinemachineHardLookAt))
                {
                    if (camBehavior._addedAimComponent != null)
                        Destroy(camBehavior._addedAimComponent);
                    camBehavior._addedAimComponent = camBehavior._virtualCamera.AddCinemachineComponent<CinemachineHardLookAt>();
                    Debug.Log("Using hard look at");
                }
            }
            else if (aimTypeVal.value == "group")
            {
                if(camBehavior._addedAimComponent == null || !(camBehavior._addedAimComponent is CinemachineGroupComposer))
                {
                    if (camBehavior._addedAimComponent != null)
                        Destroy(camBehavior._addedAimComponent);
                    camBehavior._addedAimComponent = camBehavior._virtualCamera.AddCinemachineComponent<CinemachineGroupComposer>();
                }
            }
            else if (aimTypeVal.value == "composer")
            {
                if(camBehavior._addedAimComponent == null || !(camBehavior._addedAimComponent is CinemachineComposer))
                {
                    if (camBehavior._addedAimComponent != null)
                        Destroy(camBehavior._addedAimComponent);
                    camBehavior._addedAimComponent = camBehavior._virtualCamera.AddCinemachineComponent<CinemachineComposer>();
                    CinemachineComposer composer = (CinemachineComposer)camBehavior._addedAimComponent;
                    composer.m_TrackedObjectOffset = new Vector3(0, 1, 0);
                }
            }

            Debug.Log("has aim " + (camBehavior._addedAimComponent != null));
            //camBehavior._virtualCamera.InvalidateComponentCache();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetFollowOffset");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Set how this camera looks at the target", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetAim call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            VirtualCameraBehavior camBehavior = sceneObject.GetBehaviorByType<VirtualCameraBehavior>();
            if(camBehavior == null)
            {
                UserScriptManager.LogToCode(context, "VirtualCameraBehavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            if(!UserScriptManager.ParseVector3Input(context, out Vector3 vec))
            {
                UserScriptManager.LogToCode(context, "Follow offset needs to be a vec3!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            if(camBehavior._addedFollowComponent == null)
            {
                UserScriptManager.LogToCode(context, "Follow offset needs to have a configured follow type!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            CinemachineTransposer transposer = camBehavior._addedFollowComponent as CinemachineTransposer;
            if(transposer != null)
                transposer.m_FollowOffset = vec;
            else
            {
                CinemachineOrbitalTransposer orbit = camBehavior._addedFollowComponent as CinemachineOrbitalTransposer;
                if(orbit != null)
                    orbit.m_FollowOffset = vec;
            }
            return Intrinsic.Result.True;
		};
    }
}
