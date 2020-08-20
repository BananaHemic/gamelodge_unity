using Miniscript;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConfigurableJointBehavior : BaseBehavior
{
    public ConfigurableJoint AddedJoint { get; private set; }

    private static readonly List<ExposedEvent> _events = new List<ExposedEvent>();
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    public static readonly ValString BreakEventName = ValString.Create("OnJointBreak", false);
    public static readonly ValString PosRotName = ValString.Create("PosRot", false);
    private static bool _hasLoadedIntrinsics = false;

    public bool ConfiguredInWorldSpace = false;
    public Quaternion _targetRotation = Quaternion.identity;
    private bool _hasPendingTargetRotation = false;
    public Vector3 _targetPosition;
    private Quaternion _startingLocalRot;
    private Quaternion _startingWorldRot;
    protected override void ChildInit()
    {
        AddedJoint = _sceneObject.gameObject.AddComponent<ConfigurableJoint>();
        AddedJoint.rotationDriveMode = RotationDriveMode.Slerp;
        AddedJoint.autoConfigureConnectedAnchor = false;
        AddedJoint.configuredInWorldSpace = ConfiguredInWorldSpace;
        _startingLocalRot = _sceneObject.transform.localRotation;
        _startingWorldRot = _sceneObject.transform.rotation;
        _sceneObject.NetObj.BehaviorRequestNoLongerAtRest();
    }
    public override void RefreshProperties()
    {
        AddedJoint.targetPosition = _targetPosition;
        if (_hasPendingTargetRotation)
        {
            if (ConfiguredInWorldSpace)
                AddedJoint.SetTargetRotation(_targetRotation, _startingWorldRot);
            else
                AddedJoint.SetTargetRotationLocal(_targetRotation, _startingLocalRot);
            _hasPendingTargetRotation = false;
        }
    }
    public override void UpdateParamsFromSerializedObject()
    {
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
    }
    public override List<ExposedEvent> GetEvents()
    {
        if(_events.Count == 0)
        {
            _events.Add(new ExposedEvent(BreakEventName, "Runs when the max force is hit", null));
            return _events;
        }
        return _events;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return true;
    }
    public override bool DoesRequireRigidbody()
    {
        return true;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic = Intrinsic.Create("SetTargetPosRot");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets where the other end of the connecting spring is, in world space", null));
        intrinsic.AddParam(ValString.positionStr.value);
        intrinsic.AddParam(ValString.rotationStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetTargetPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetTargetPosRot behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool posRot = UserScriptManager.ParsePositionRotation(context.GetVar(ValString.positionStr), context.GetVar(ValString.rotationStr), out Vector3 pos, out Quaternion rot);

            if (!posRot)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetTargetPosRot!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO update UI
            jointBehavior._targetPosition = pos;
            jointBehavior._targetRotation = rot;
            jointBehavior._hasPendingTargetRotation = true;
            jointBehavior.RefreshProperties();

            return new Intrinsic.Result(ValNumber.one);
		};
        intrinsic = Intrinsic.Create("GetInitialRot");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the initial rotation of the ConfigurableJoint. This is used for certain operations, as ConfigurableJoint uses it to transform input vectors/quaternions", "initialRot"));
        // TODO world/local
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetTargetPosRot call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetTargetPosRot behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(new ValQuaternion(jointBehavior._startingWorldRot));
		};
        intrinsic = Intrinsic.Create("SetPositionDriveSpring");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how the strong the positional correction is, in X/Y/Z", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetPositionDrive call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetPositionDrive behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 drive);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetPositionDrive!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO update UI
            JointDrive xDrive = jointBehavior.AddedJoint.xDrive;
            JointDrive yDrive = jointBehavior.AddedJoint.yDrive;
            JointDrive zDrive = jointBehavior.AddedJoint.zDrive;
            xDrive.positionSpring = drive.x;
            yDrive.positionSpring = drive.y;
            zDrive.positionSpring = drive.z;
            jointBehavior.AddedJoint.xDrive = xDrive;
            jointBehavior.AddedJoint.yDrive = yDrive;
            jointBehavior.AddedJoint.zDrive = zDrive;

            jointBehavior.RefreshProperties();

            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetRotationSlerpDrive");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how the strong the rotation correction is in all directions", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetRotationalSlerpDrive call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetRotationalSlerpDrive behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValNumber rotDrive = context.GetVar(ValString.xStr) as ValNumber;
            if (rotDrive == null)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetRotationalSlerpDrive!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO update UI
            JointDrive slerpDrive = jointBehavior.AddedJoint.slerpDrive;
            slerpDrive.positionSpring = (float)rotDrive.value;
            jointBehavior.AddedJoint.slerpDrive = slerpDrive;

            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetPositionDriveDamper");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how the strong the velocity correction is, for X/Y/Z", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetPositionDriveDamper call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetPositionDriveDamper behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 drive);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetPositionDriveDamper!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO update UI
            JointDrive xDrive = jointBehavior.AddedJoint.xDrive;
            JointDrive yDrive = jointBehavior.AddedJoint.yDrive;
            JointDrive zDrive = jointBehavior.AddedJoint.zDrive;
            xDrive.positionDamper = drive.x;
            yDrive.positionDamper = drive.y;
            zDrive.positionDamper = drive.z;
            jointBehavior.AddedJoint.xDrive = xDrive;
            jointBehavior.AddedJoint.yDrive = yDrive;
            jointBehavior.AddedJoint.zDrive = zDrive;
            //jointBehavior.RefreshProperties();

            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetRotationSlerpDamper");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how the strong the angular velocity correction is in all directions", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetRotationSlerpDamper call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetRotationSlerpDamper behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValNumber rotDamper = context.GetVar(ValString.xStr) as ValNumber;
            if (rotDamper == null)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetRotationSlerpDamper!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO update UI
            JointDrive slerpDrive = jointBehavior.AddedJoint.slerpDrive;
            slerpDrive.positionDamper = (float)rotDamper.value;
            jointBehavior.AddedJoint.slerpDrive = slerpDrive;

            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetAnchorPoint");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets where the spring is located on our object, in local space", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetAnchorPoint call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetAnchorPoint behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 anchor);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetAnchorPoint!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            jointBehavior.AddedJoint.anchor = anchor;
            // TODO update UI
            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetConnectedAnchor");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets where the other end of the spring is located", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetAnchorPoint call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetAnchorPoint behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 connectedAnchor);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetAnchorPoint!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            jointBehavior.AddedJoint.connectedAnchor = connectedAnchor;
            // TODO update UI
            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetTargetVelocity");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how fast the other end of the spring is moving", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetTargetVelocity call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetTargetVelocity behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 velocity);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetTargetVelocity!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            jointBehavior.AddedJoint.targetVelocity = velocity;
            // TODO update UI
            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetTargetAngularVelocity");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how fast the other end of the spring is rotating", null));
        intrinsic.AddParam(ValString.xStr.value);
        intrinsic.AddParam(ValString.yStr.value);
        intrinsic.AddParam(ValString.zStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetTargetAngularVelocity call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ConfigurableJointBehavior jointBehavior = sceneObject.GetBehaviorByType<ConfigurableJointBehavior>();
            if(jointBehavior == null)
            {
                UserScriptManager.LogToCode(context, "SetTargetAngularVelocity behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            bool didParse = UserScriptManager.ParseVector3Input(context, out Vector3 angularVelocity);
            if (!didParse)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetTargetAngularVelocity!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            jointBehavior.AddedJoint.targetAngularVelocity = angularVelocity;
            // TODO update UI
            //jointBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
    }
    public override void Destroy()
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        if (AddedJoint != null)
            Destroy(AddedJoint);
        AddedJoint = null;
        Destroy(this);
    }
}
