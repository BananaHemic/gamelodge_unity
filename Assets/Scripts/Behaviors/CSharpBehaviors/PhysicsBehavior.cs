using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using DarkRift;
using System;
using Miniscript;

public class PhysicsBehavior : BaseBehavior
{
    /// <summary>
    /// Serialized, displayed properties
    /// </summary>
    [Range(0,1f)]
    public float Bounciness = 0.5f;
    [Range(0,1f)]
    public float StaticFriction = 0.6f;
    [Range(0,1f)]
    public float DynamicFriction = 0.6f;

    public float Mass = 1f;
    public bool CanUsersWalkOn = false;
    public bool UseGravity = true;
    public CollisionDetectionMode CollisionDetectionMode = CollisionDetectionMode.Discrete;

    private PhysicMaterial _physicsMaterial;
    private List<GameObject> _currentlyCollidingObjs;

    //const float DefaultMaxPenetration = 20f; //TODO this should be in the inspector maybe
    const float DefaultMaxPenetration = 2000f; //TODO this should be in the inspector maybe

    /// <summary>
    /// Have the intrinsic functions been loaded
    /// </summary>
    private static bool _hasLoadedIntrinsics = false;
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedVariable> _userVariables = new List<ExposedVariable>();
    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();
    private static readonly ValString OnCollisionEventStrVal = ValString.Create("OnCollision", false);
    private static readonly ValString ForceParamName = ValString.Create("force");
    public static readonly ValString ForceMagParamName = ValString.Create("forceMag");
    public static readonly ValString RadiusParamName = ValString.Create("radius");
    private static readonly ValString TorqueParamName = ValString.Create("torque");
    private static readonly ValString ForceModeParamName = ValString.Create("forceMode");
    private static readonly ValString RelativeParamName = ValString.Create("relative");
    private static readonly ValString WorldLocalParamName = ValString.Create("worldLocal");
    private static readonly ValString DragParamName = ValString.Create("drag");
    private static readonly ValString BouncinessParamName = ValString.Create("bounciness");
    public static readonly ValString UpwardsParamName = ValString.Create("upwardModifier");
    private static readonly Collider[] _workingGOList = new Collider[32];
    private static readonly HashSet<ushort> _workingHashSet = new HashSet<ushort>();
    // Serialization stuff
    const int BouncinessKey = 0;
    const int StaticFrictionKey = 1;
    const int DynamicFrictionKey = 2;
    const int CanUsersWalkOnKey = 3;
    const int UseGravityKey = 4;
    const int MassKey = 5;
    const int CollisionDetectionModeKey = 6;
    private readonly byte[] _collisionModeSerializationArray = new byte[1];
    public static readonly int LayerRequestPriority = 1;

    protected override void ChildInit()
    {
        //Debug.Log("Physics behavior start! On " + gameObject.name, this);
        _sceneObject.Rigidbody.maxDepenetrationVelocity = DefaultMaxPenetration;
        //TODO would be nice to cache this and share it across instances
        _physicsMaterial = new PhysicMaterial
        {
            bounciness = Bounciness,
            staticFriction = StaticFriction,
            dynamicFriction = DynamicFriction,
            bounceCombine = PhysicMaterialCombine.Maximum,
            frictionCombine = PhysicMaterialCombine.Maximum
        };
        _sceneObject.BehaviorRequestedLayer(CanUsersWalkOn ? GLLayers.PhysicsObject_WalkableLayerNum : GLLayers.PhysicsObject_NonWalkableLayerNum, this, LayerRequestPriority);
        _sceneObject.Rigidbody.useGravity = UseGravity;
        _sceneObject.Rigidbody.mass = Mass;
        _sceneObject.Rigidbody.collisionDetectionMode = CollisionDetectionMode;
        //NB you'll want to update RefreshProperties too
        UpdateColliders();
        _sceneObject.OnCollidersChange += UpdateColliders;
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return true;
    }
    public override bool DoesRequireRigidbody()
    {
        return true;
    }
    public override bool DoesRequireCollider()
    {
        return true;
    }
    private void UpdateColliders()
    {
        //Debug.Log("Setting " + (_sceneObject.ModelColliders == null ? -1 : _sceneObject.ModelColliders.Count) + " colliders");
        if (_sceneObject.ModelColliders != null)
        {
            foreach(Collider collider in _sceneObject.ModelColliders)
                collider.sharedMaterial = _physicsMaterial;
        }
        if(_sceneObject.AABBCollider != null)
            _sceneObject.AABBCollider.SetPhysicsMaterial(_physicsMaterial);
    }
    public override void RefreshProperties()
    {
        //Debug.Log("Refreshing properties, bouncy " + Bounciness + " for #" + _sceneObject.GetID());
        _physicsMaterial.bounciness = Bounciness;
        _physicsMaterial.staticFriction = StaticFriction;
        _physicsMaterial.dynamicFriction = DynamicFriction;
        _sceneObject.BehaviorRequestedLayer(CanUsersWalkOn ? GLLayers.PhysicsObject_WalkableLayerNum : GLLayers.PhysicsObject_NonWalkableLayerNum, this, LayerRequestPriority);
        _sceneObject.Rigidbody.useGravity = UseGravity;
        _sceneObject.Rigidbody.mass = Mass;
        _sceneObject.Rigidbody.collisionDetectionMode = CollisionDetectionMode;
        //Debug.Log("refresh " + Mass);
    }
    private void OnCollisionEnter(Collision collision)
    {
        //Debug.Log("On collision enter!");
        _sceneObject.InvokeEventOnBehaviors(OnCollisionEventStrVal);
        if (_currentlyCollidingObjs == null)
            _currentlyCollidingObjs = new List<GameObject>(2) { collision.gameObject };
        else
            _currentlyCollidingObjs.Add(collision.gameObject);
    }
    private void OnCollisionExit(Collision collision)
    {
        _currentlyCollidingObjs.Remove(collision.gameObject);
    }
    private void OnEnable()
    {
        //Debug.Log("Physics enabled");
    }
    private void OnDisable()
    {
        //Debug.Log("Physics disabled");
    }
    private void OnDestroy()
    {
        if (Orchestrator.Instance == null || Orchestrator.Instance.IsAppClosing)
            return;
        //Debug.Log("Destroying Physics object!");
        _physicsMaterial = null;
        if(_sceneObject != null)
            UpdateColliders();
        if(_sceneObject != null)
            _sceneObject.OnCollidersChange -= UpdateColliders;
        _sceneObject = null;
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        //TODO use a zero-allocation float -> array function
        // Bounciness
        _serializedBehavior.LocallySetData(BouncinessKey, BitConverter.GetBytes(Bounciness));
        // Static Friction
        _serializedBehavior.LocallySetData(StaticFrictionKey, BitConverter.GetBytes(StaticFriction));
        // Dynamic Friction
        _serializedBehavior.LocallySetData(DynamicFrictionKey, BitConverter.GetBytes(DynamicFriction));
        // Can Users Walk On
        _serializedBehavior.LocallySetData(CanUsersWalkOnKey, BitConverter.GetBytes(CanUsersWalkOn));
        // Use Gravity
        _serializedBehavior.LocallySetData(UseGravityKey, BitConverter.GetBytes(UseGravity));
        // Mass
        _serializedBehavior.LocallySetData(MassKey, BitConverter.GetBytes(Mass));
        // Collision Detection Mode
        _collisionModeSerializationArray[0] = (byte)CollisionDetectionMode;
        _serializedBehavior.LocallySetData(CollisionDetectionModeKey, _collisionModeSerializationArray);
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Bounciness
        byte[] bouncinessArray;
        if(_serializedBehavior.TryReadProperty(BouncinessKey, out bouncinessArray, out int _))
            Bounciness = BitConverter.ToSingle(bouncinessArray, 0);
        // Static Friction
        byte[] staticFrictionArray;
        if(_serializedBehavior.TryReadProperty(StaticFrictionKey, out staticFrictionArray, out int _))
            StaticFriction = BitConverter.ToSingle(staticFrictionArray, 0);
        // Dynamic Friction
        byte[] dynamicFrictionArray;
        if(_serializedBehavior.TryReadProperty(DynamicFrictionKey, out dynamicFrictionArray, out int _))
            DynamicFriction = BitConverter.ToSingle(dynamicFrictionArray, 0);
        // Can Users Walk On
        byte[] canUsersWalkOnArray;
        if(_serializedBehavior.TryReadProperty(CanUsersWalkOnKey, out canUsersWalkOnArray, out int _))
            CanUsersWalkOn = BitConverter.ToBoolean(canUsersWalkOnArray, 0);
        // Use Gravity
        byte[] useGravityArray;
        if(_serializedBehavior.TryReadProperty(UseGravityKey, out useGravityArray, out int _))
            UseGravity = BitConverter.ToBoolean(useGravityArray, 0);
        // Mass
        byte[] massArray;
        if(_serializedBehavior.TryReadProperty(MassKey, out massArray, out int _))
            Mass = BitConverter.ToSingle(massArray, 0);
        // Collision Detection Mode
        byte[] collisionArray;
        if (_serializedBehavior.TryReadProperty(CollisionDetectionModeKey, out collisionArray, out int _))
            CollisionDetectionMode = (CollisionDetectionMode)collisionArray[0];
    }
    public override void Destroy()
    {
        Destroy(this);
    }
    public override List<ExposedVariable> GetVariables()
    {
        if(_userVariables.Count == 0)
        {
            _userVariables.Add(new ExposedVariable(ValString.velocityStr, "How fast this object is moving", _sceneObject.Rigidbody.velocity));
            _userVariables.Add(new ExposedVariable(ValString.angularVelocityStr, "How fast this object is rotating", _sceneObject.Rigidbody.angularVelocity));
            return _userVariables;
        }
        int i = 0;
        _userVariables[i++].SetVector3(_sceneObject.Rigidbody.velocity);
        _userVariables[i++].SetVector3(_sceneObject.Rigidbody.angularVelocity);
        //Debug.Log("Set angular vel " + _sceneObject.Rigidbody.angularVelocity + " for " + _sceneObject.Name);
        return _userVariables;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedEvent> GetEvents()
    {
        if(_userEvents.Count == 0)
        {
            _userEvents.Add(new ExposedEvent(OnCollisionEventStrVal, "Runs when the object hits another object", null));
            return _userEvents;
        }
        return _userEvents;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic = Intrinsic.Create("SetBounciness");
        intrinsic.AddParam(BouncinessParamName.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets how bouncy this object should be", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetBounciness call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValNumber bouncy = context.GetVar(BouncinessParamName) as ValNumber;
            if (bouncy == null)
                return Intrinsic.Result.Null;
            float bounciness = (float)bouncy.value;
            //Debug.Log("Setting bounciness to " + bounciness);

            // TODO update UI
            physicsBehavior.Bounciness = bounciness;
            physicsBehavior.RefreshProperties();

            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("SetVelocity");
        intrinsic.AddParam(ValString.xStr.value, 0.0);
        intrinsic.AddParam(ValString.yStr.value, 0.0);
        intrinsic.AddParam(ValString.zStr.value, 0.0);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the velocity of this object in world space", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetVelocity call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Value velX = context.GetVar(ValString.xStr);
            if (velX == null)
                return Intrinsic.Result.Null;
            Value velY = context.GetVar(ValString.yStr);
            Value velZ = context.GetVar(ValString.zStr);
            // The initial position can be either a number, or a ValMap
            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(velX, velY, velZ, null, out double numOut, out Vector2 vec2Out, out Vector3 vec3Out, out Quaternion quatOut);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetVelocity!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Rigidbody rigidbody = sceneObject.Rigidbody;
            if (rigidbody == null)
                return Intrinsic.Result.Null;
            rigidbody.velocity = vec3Out;
            sceneObject.NetObj.BehaviorRequestNoLongerAtRest();

            return new Intrinsic.Result(ValNumber.one);
		};
        intrinsic = Intrinsic.Create("SetAngularVelocity");
        intrinsic.AddParam(ValString.xStr.value, 0.0);
        intrinsic.AddParam(ValString.yStr.value, 0.0);
        intrinsic.AddParam(ValString.zStr.value, 0.0);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the angular velocity of this object in world space. (This receives a Vector3)", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetVelocity call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Value velX = context.GetVar(ValString.xStr);
            if (velX == null)
                return Intrinsic.Result.Null;
            Value velY = context.GetVar(ValString.yStr);
            Value velZ = context.GetVar(ValString.zStr);
            // The initial position can be either a number, or a ValMap
            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(velX, velY, velZ, null, out double numOut, out Vector2 vec2Out, out Vector3 vec3Out, out Quaternion quatOut);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad input for SetAngularVelocity!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Rigidbody rigidbody = sceneObject.Rigidbody;
            if (rigidbody == null)
                return Intrinsic.Result.Null;
            rigidbody.angularVelocity = vec3Out;
            sceneObject.NetObj.BehaviorRequestNoLongerAtRest();

            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("AddForce");
        intrinsic.AddParam(ForceParamName.value);
        intrinsic.AddParam(ValString.positionStr.value);
        intrinsic.AddParam(ForceModeParamName.value, "force");
        intrinsic.AddParam(RelativeParamName.value, ValNumber.zero);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Adds a force to this object. You can also pass the position you want the force to be applied to, and the type of force to apply (force/acceleration/impulse/velocity)", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in AddForce call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the force vector
            Value forceVal = context.GetVar(ForceParamName);
            if (forceVal == null)
            {
                UserScriptManager.LogToCode(context, "No force input for AddForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(forceVal, out double numOut, out Vector2 vec2Out, out Vector3 forceVector, out Quaternion quatOut);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad force input for AddForce! Got type " + vecType, UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            // Parse the position vector
            Vector3 position;
            bool hasPosition;
            Value posVal = context.GetVar(ValString.positionStr);
            if (posVal == null)
            {
                position = Vector3.zero;
                hasPosition = false;
            }
            else
            {
                hasPosition = true;
                vecType = UserScriptManager.ParseVecInput(posVal, out numOut, out vec2Out, out position, out quatOut);
                if(vecType != UserScriptManager.VecType.Vector3)
                {
                    UserScriptManager.LogToCode(context, "Bad position input for AddForce!", UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }
            }
            // Parse the force mode
            ForceMode forceMode;
            Value forceModeVal = context.GetVar(ForceModeParamName);
            if (forceModeVal == null)
                forceMode = ForceMode.Force;
            else
            {
                ValString forceModeStr = forceModeVal as ValString;
                if(forceModeStr == null)
                {
                    UserScriptManager.LogToCode(context, "Bad force mode input for AddForce!", UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }

                if (string.Equals(forceModeStr.value, "force", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Force;
                else if (string.Equals(forceModeStr.value, "acceleration", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Acceleration;
                else if (string.Equals(forceModeStr.value, "impulse", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Impulse;
                else if (string.Equals(forceModeStr.value, "velocityChange", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.VelocityChange;
                else
                {
                    UserScriptManager.LogToCode(context, "Bad force mode input for AddForce! Unknown force type " + forceModeStr.value, UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }
            }

            Rigidbody rigidbody = sceneObject.Rigidbody;
            if (rigidbody == null)
            {
                UserScriptManager.LogToCode(context, "No rigidbody for AddForce", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            bool isRelative = UserScriptManager.ParseTruth(context.GetVar(RelativeParamName));

            if (hasPosition)
            {
                if (isRelative)
                {
                    UserScriptManager.LogToCode(context, "Relative force cannot accept position", UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }
                rigidbody.AddForceAtPosition(forceVector, position, forceMode);
                //Debug.Log("Applying force to " + rigidbody.gameObject.name + " " + forceVector.ToPrettyString() + " at " + position.ToPrettyString() + " mode " + forceMode);
            }
            else
            {
                if (isRelative)
                    rigidbody.AddRelativeForce(forceVector, forceMode);
                else
                    rigidbody.AddForce(forceVector, forceMode);
                Debug.Log("Applying force to " + rigidbody.gameObject.name + " " + forceVector.ToPrettyString() + " mode " + forceMode);
            }
            sceneObject.NetObj.BehaviorRequestNoLongerAtRest();
            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("AddTorque");
        intrinsic.AddParam(TorqueParamName.value);
        intrinsic.AddParam(ForceModeParamName.value, "force");
        intrinsic.AddParam(RelativeParamName.value, ValNumber.zero);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Adds a torque to this object. You can also pass the type of force to apply (force/acceleration/impulse/velocity)", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in AddTorque call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the torque vector
            Value torqueVal = context.GetVar(TorqueParamName);
            if (torqueVal == null)
            {
                UserScriptManager.LogToCode(context, "No torque input for AddTorque!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(torqueVal, out double numOut, out Vector2 vec2Out, out Vector3 forceVector, out Quaternion quatOut);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad torque input for AddTorque! Got type " + vecType, UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            // Parse the force mode
            ForceMode forceMode;
            Value forceModeVal = context.GetVar(ForceModeParamName);
            if (forceModeVal == null)
                forceMode = ForceMode.Force;
            else
            {
                ValString forceModeStr = forceModeVal as ValString;
                if(forceModeStr == null)
                {
                    UserScriptManager.LogToCode(context, "Bad torque mode input for AddTorque!", UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }

                if (string.Equals(forceModeStr.value, "force", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Force;
                else if (string.Equals(forceModeStr.value, "acceleration", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Acceleration;
                else if (string.Equals(forceModeStr.value, "impulse", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.Impulse;
                else if (string.Equals(forceModeStr.value, "velocityChange", StringComparison.OrdinalIgnoreCase))
                    forceMode = ForceMode.VelocityChange;
                else
                {
                    UserScriptManager.LogToCode(context, "Bad torque mode input for AddTorque! Unknown force type " + forceModeStr.value, UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }
            }

            Rigidbody rigidbody = sceneObject.Rigidbody;
            if (rigidbody == null)
            {
                UserScriptManager.LogToCode(context, "No rigidbody for AddTorque", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            bool relative = UserScriptManager.ParseTruth(context.GetVar(RelativeParamName));

            if (relative)
            {
                //Debug.Log("relative torque " + forceVector + " mode " + forceMode);
                rigidbody.AddRelativeTorque(forceVector, forceMode);
            }
            else
            {
                //Debug.Log("Torque " + forceVector + " mode " + forceMode);
                rigidbody.AddTorque(forceVector, forceMode);
            }
            sceneObject.NetObj.BehaviorRequestNoLongerAtRest();
            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("GetMass");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Get the current mass of this object", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetMass call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(ValNumber.Create(physicsBehavior.Mass));
		};
        intrinsic = Intrinsic.Create("GetInertiaTensor");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Get the inertia tensor of this object. This is defines how much resistance there is to rotation", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetInertiaTensor call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(new ValVector3(sceneObject.Rigidbody.inertiaTensor));
		};
        intrinsic = Intrinsic.Create("GetInertiaTensorRotation");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Get the rotation of the inertia tensor of this object. Generally, you will want to multiply this rotation by the inertia tensor", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetInertiaTensorRotation call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(new ValQuaternion(sceneObject.Rigidbody.inertiaTensorRotation));
		};
        intrinsic = Intrinsic.Create("GetCenterOfMass");
        intrinsic.AddParam(WorldLocalParamName.value, ValString.Create("local", false));
        _userFunctions.Add(new ExposedFunction(intrinsic, "Get the center of mass of this object", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetCenterOfMass call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Vector3 com;
            // Turn to global if the user wants
            ValString worldLocalParam = context.GetVar(WorldLocalParamName) as ValString;
            if(worldLocalParam != null && worldLocalParam.value == "world")
            {
                // Convert to the world space vector
                //Debug.Log("Using world space com");
                com = sceneObject.Rigidbody.worldCenterOfMass;
            }else
                com = sceneObject.Rigidbody.centerOfMass;

            return new Intrinsic.Result(new ValVector3(com));
		};
        intrinsic = Intrinsic.Create("SetMaxAngularVelocity");
        intrinsic.AddParam(ValString.angularVelocityStr.value, ValNumber.Create(7));
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the maximum angular velocity for this rigidbody", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetMaxAngularVelocity call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValNumber maxVel = context.GetVar(ValString.angularVelocityStr) as ValNumber;
            if(maxVel == null)
            {
                UserScriptManager.LogToCode(context, "Bad max angular velocity for SetMaxAngularVelocity!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            sceneObject.Rigidbody.maxAngularVelocity = (float)maxVel.value;
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("SetAngularDrag");
        intrinsic.AddParam(DragParamName.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the angular drag for this rigidbody", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetAngularDrag call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present! Obj #" + sceneObject.GetID(), UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValNumber dragNum = context.GetVar(DragParamName) as ValNumber;
            if(dragNum == null)
            {
                UserScriptManager.LogToCode(context, "Bad angular drag for SetAngularDrag!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            sceneObject.Rigidbody.angularDrag = (float)dragNum.value;
            return Intrinsic.Result.True;
		};

        intrinsic = Intrinsic.Create("IsColliding");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns whether or not this object is currently colliding with anything else", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in IsColliding call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return physicsBehavior._currentlyCollidingObjs != null && physicsBehavior._currentlyCollidingObjs.Count > 0
                ? Intrinsic.Result.True
                : Intrinsic.Result.False;
		};

        intrinsic = Intrinsic.Create("OverlapsSphere");
        intrinsic.AddParam(ValString.positionStr.value);
        intrinsic.AddParam(ValString.lenStr.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Returns the objects that are within a sphere's distance away", null));
        intrinsic.code = (context, partialResult) => {

            ValVector3 vec3 = context.GetVar(ValString.positionStr) as ValVector3;
            if(vec3 == null)
            {
                UserScriptManager.LogToCode(context, "No position in OverlapsSphere call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValNumber radius = context.GetVar(ValString.lenStr) as ValNumber;
            if(radius == null)
            {
                UserScriptManager.LogToCode(context, "No radius in OverlapsSphere call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // TODO this should be exposed
            int layerMask = GLLayers.AllCanRaycast;
            int numHit = Physics.OverlapSphereNonAlloc(vec3.Vector3, (float)radius.value, _workingGOList, layerMask);
            if (numHit == 0)
                return Intrinsic.Result.Null;

            _workingHashSet.Clear();
            ValList list = ValList.Create(numHit);
            for(int i = 0; i < numHit; i++)
            {
                //Debug.Log("hit " + _workingGOList[i])
                // Get the sceneobject from the collider
                SceneObject sceneObject = _workingGOList[i].transform.GetSceneObjectFromTransform();
                if (sceneObject == null)
                    continue;
                // Avoid duplicate sceneobjects
                if (!_workingHashSet.Add(sceneObject.GetID()))
                    continue;
                list.Add(new ValSceneObject(sceneObject));
            }
            return new Intrinsic.Result(list);
		};

        intrinsic = Intrinsic.Create("AddExplosionForce");
        intrinsic.AddParam(ForceMagParamName.value);
        intrinsic.AddParam(ValString.positionStr.value);
        intrinsic.AddParam(RadiusParamName.value);
        intrinsic.AddParam(UpwardsParamName.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Adds an explosion force to this object", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in AddExplosionForce call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the force magnitude
            ValNumber forceVal = context.GetVar(ForceMagParamName) as ValNumber;
            if (forceVal == null)
            {
                UserScriptManager.LogToCode(context, "No force input for AddExplosionForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the position vector
            Value posVal = context.GetVar(ValString.positionStr);
            if (posVal == null)
            {
                UserScriptManager.LogToCode(context, "No position input for AddExplosionForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(posVal, out _, out _, out Vector3 position, out _);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad position input for AddForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the radius
            ValNumber radiusVal = context.GetVar(RadiusParamName) as ValNumber;
            if (radiusVal == null)
            {
                UserScriptManager.LogToCode(context, "No radius input for AddExplosionForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            // Parse the upwards modifier
            ValNumber upwardsVal = context.GetVar(UpwardsParamName) as ValNumber;
            if (upwardsVal == null)
            {
                UserScriptManager.LogToCode(context, "No upwards input for AddExplosionForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            PhysicsBehavior physicsBehavior = sceneObject.GetBehaviorByType<PhysicsBehavior>();
            if(physicsBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Physics behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            Rigidbody rigidbody = sceneObject.Rigidbody;
            if (rigidbody == null)
            {
                UserScriptManager.LogToCode(context, "No rigidbody for AddExplosionForce", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            rigidbody.AddExplosionForce((float)forceVal.value, position, (float)radiusVal.value, (float)upwardsVal.value);
            sceneObject.NetObj.BehaviorRequestNoLongerAtRest();
            return new Intrinsic.Result(ValNumber.one);
		};
    }
}
