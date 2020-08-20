using KinematicCharacterController;
using Miniscript;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatformBehavior : BaseBehavior, IMoverController
{
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    public Vector3 Position { get { return _physicsMover.TransientPosition; } }
    public Quaternion Rotation { get { return _physicsMover.TransientRotation; } }

    private PhysicsMover _physicsMover;
    private bool _hasTargetPos = false;
    private Vector3 _targetPos;
    private Vector3 _deltaPos;
    private bool _hasTargetRot = false;
    private Quaternion _targetRot;
    private Quaternion _deltaRot = Quaternion.identity;
    //private Quaternion _targetRot;
    const int KinematicRequestPriority = 1; // Just one above the minimum of 0, which is what SceneObject uses

    protected override void ChildInit()
    {
        _physicsMover = _sceneObject.gameObject.AddComponent<PhysicsMover>();
        _physicsMover.MoverController = this;
        // Tell scene object that this RB should be kinematic
        _sceneObject.BehaviorRequestedKinematic(true, this, KinematicRequestPriority);
    }
    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        //Debug.Log("Moving " + (_targetPos - transform.localPosition).x);
        if (_hasTargetPos)
        {
            goalPosition = _targetPos + _deltaPos;
            _hasTargetPos = false;
        }
        else
            goalPosition = _physicsMover.TransientPosition + _deltaPos;
        _deltaPos = Vector3.zero;

        if (_hasTargetRot)
        {
            goalRotation = _deltaRot * _targetRot;
            _hasTargetRot = false;
        }
        else
            goalRotation = _deltaRot * _physicsMover.TransientRotation;
        _deltaRot = Quaternion.identity;
    }
    public void NetworkSetPosition(Vector3 pos)
    {
        // Previously, we used SetPosition, but that caused a lot of jitter.
        // There's still some jitter with this method, but it's reduced
        //_physicsMover.SetPosition(pos);
        //Debug.Log("recv " + pos.ToPrettyString() + " have " + _physicsMover.TransientPosition.ToPrettyString() + " del: " + (pos - _physicsMover.TransientPosition).x + " dt " + (Time.frameCount - _lF));
        _targetPos = pos;
        _hasTargetPos = true;
    }
    public void NetworkSetRotation(Quaternion rot)
    {
        _hasTargetRot = true;
        _targetRot = rot;
    }
    public void ForceUpdateFromTransform()
    {
        _hasTargetPos = true;
        _targetPos = transform.localPosition;
        _hasTargetRot = true;
        _targetRot = transform.localRotation;
    }
    public override void RefreshProperties()
    {
    }
    public override void UpdateParamsFromSerializedObject()
    {
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
    }
    public override bool DoesRequireCollider() { return true; }
    public override bool DoesRequirePosRotScaleSyncing() { return false; }
    public override bool DoesRequireRigidbody() { return true; }
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
    public override void Destroy()
    {
        if(_physicsMover != null)
        {
            Destroy(_physicsMover);
            _physicsMover = null;
        }
        _sceneObject.BehaviorClearRequestKinematic(this, KinematicRequestPriority);
    }
    public static void LoadIntrinsics()
    {
        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("MovePlatform");
        intrinsic.AddParam("x", 0.0);
        intrinsic.AddParam("y", 0.0);
        intrinsic.AddParam("z", 0.0);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Moves the MovingPlatform by a distance", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "Failed to get sceneobject in MovePlatform", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            MovingPlatformBehavior behavior = sceneObject.GetBehaviorByType<MovingPlatformBehavior>();
            if(behavior == null)
            {
                UserScriptManager.LogToCode(context, "No MovingPlatformBehavior for SetPlatformPosition", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            if(!(UserScriptManager.ParseVector3Input(context, out Vector3 vec3Out))) {
                UserScriptManager.LogToCode(context, "Failed to get position in MovePlatform", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            //Debug.Log("del " + vec3Out);

            behavior._deltaPos = vec3Out;
            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("SetPlatformPosition");
        intrinsic.AddParam("x", 0.0);
        intrinsic.AddParam("y", 0.0);
        intrinsic.AddParam("z", 0.0);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Set's the MovingPlatform's position", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "Failed to get sceneobject in SetPlatformPosition", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            MovingPlatformBehavior behavior = sceneObject.GetBehaviorByType<MovingPlatformBehavior>();
            if(behavior == null)
            {
                UserScriptManager.LogToCode(context, "No MovingPlatformBehavior for SetPlatformPosition", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            if(!(UserScriptManager.ParseVector3Input(context, out Vector3 vec3Out))) {
                UserScriptManager.LogToCode(context, "Failed to get position in SetPlatformPosition", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            behavior._hasTargetPos = true;
            behavior._targetPos = vec3Out;
            return new Intrinsic.Result(ValNumber.one);
		};
    }
}
