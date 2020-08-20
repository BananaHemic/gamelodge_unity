using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using DarkRift;
using System;
using System.ComponentModel;
using Miniscript;

public class CollisionTypeBehavior : BaseBehavior
{
    // NB If you re-order this, update Int2CollisionType
    public enum CollisionTypes
    {
        Terrain,
        Bullet,
        PhysicsObject_NonWalkable,
        PhysicsObject_Walkable,
        Grabbed,
        None,
    }

    public CollisionTypes CollisionType;
    // If we've gotten a collision type to use,
    // either from the network, or from the user
    // setting it
    private bool _hasSetCollisionType = false;
    /// <summary>
    /// Have the intrinsic functions been loaded
    /// </summary>
    private static bool _hasLoadedIntrinsics = false;
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedVariable> _userVariables = new List<ExposedVariable>();
    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();
    // Serialization stuff
    const int CollisionTypeKey = 0;
    public static readonly int LayerRequestPriority = 3;

    protected override void ChildInit()
    {
        // Figure out the current CollisionType based on the scene object layer
        if(!_hasSetCollisionType)
            CollisionType = Layer2CollisionType(_sceneObject.Layer);
        //Debug.Log("At init collision type " + _sceneObject.Layer + " collision " + CollisionType);
        _sceneObject.BehaviorRequestedLayer(CollisionType2Layer(CollisionType), this, LayerRequestPriority);
    }
    private static string CollisionType2String(CollisionTypes collisionTypes)
    {
        switch (collisionTypes)
        {
            case CollisionTypes.Terrain:
                return "terrain";
            case CollisionTypes.Bullet:
                return "bullet";
            case CollisionTypes.PhysicsObject_NonWalkable:
                return "physics_nonwalkable";
            case CollisionTypes.PhysicsObject_Walkable:
                return "physics_walkable";
            case CollisionTypes.Grabbed:
                return "grabbed";
            case CollisionTypes.None:
                return "none";
            default:
                return "ERROR";
        }
    }
    private static bool String2CollisionType(string collisionStr, out CollisionTypes collisionType)
    {
        switch (collisionStr)
        {
            case "terrain":
                collisionType = CollisionTypes.Terrain;
                return true;
            case "bullet":
                collisionType = CollisionTypes.Bullet;
                return true;
            case "physics_nonwalkable":
                collisionType = CollisionTypes.PhysicsObject_NonWalkable;
                return true;
            case "physics_walkable":
                collisionType = CollisionTypes.PhysicsObject_Walkable;
                return true;
            case "grabbed":
                collisionType = CollisionTypes.Grabbed;
                return true;
            case "none":
                collisionType = CollisionTypes.None;
                return true;
            default:
                collisionType = CollisionTypes.None;
                return false;
        }
    }
    private static int CollisionType2Layer(CollisionTypes collisionTypes)
    {
        switch (collisionTypes)
        {
            case CollisionTypes.Terrain:
                return GLLayers.TerrainLayerNum;
            case CollisionTypes.Bullet:
                return GLLayers.BulletLayerNum;
            case CollisionTypes.PhysicsObject_NonWalkable:
                return GLLayers.PhysicsObject_NonWalkableLayerNum;
            case CollisionTypes.PhysicsObject_Walkable:
                return GLLayers.PhysicsObject_WalkableLayerNum;
            case CollisionTypes.Grabbed:
                return GLLayers.GrabbedLayerNum;
            case CollisionTypes.None:
                return GLLayers.NoneLayerNum;
            default:
                return GLLayers.NoneLayerNum;
        }
    }
    private static CollisionTypes Layer2CollisionType(int layer)
    {
        switch (layer)
        {
            case GLLayers.TerrainLayerNum:
                return CollisionTypes.Terrain;
            case GLLayers.BulletLayerNum:
                return CollisionTypes.Bullet;
            case GLLayers.PhysicsObject_NonWalkableLayerNum:
                return CollisionTypes.PhysicsObject_NonWalkable;
            case GLLayers.PhysicsObject_WalkableLayerNum:
                return CollisionTypes.PhysicsObject_Walkable;
            case GLLayers.GrabbedLayerNum:
                return CollisionTypes.Grabbed;
            case GLLayers.NoneLayerNum:
                return CollisionTypes.None;
            default:
                Debug.LogError("Unhandled layer type in CollisionTypeBehavior: " + layer);
                return CollisionTypes.Terrain;
        }
    }
    private static int CollisionType2Int(CollisionTypes collisionTypes)
    {
        return (int)collisionTypes;
    }
    private static bool Int2CollisionType(int collisionInt, out CollisionTypes collisionType)
    {
        //TODO I know that we can use Enum.IsDefined instead, but iirc that has some
        // unexpected behavior at times, so for now we just do the naive way
        switch(collisionInt)
        {
            case 0:
                collisionType = CollisionTypes.Terrain;
                return true;
            case 1:
                collisionType = CollisionTypes.Bullet;
                return true;
            case 2:
                collisionType = CollisionTypes.PhysicsObject_NonWalkable;
                return true;
            case 3:
                collisionType = CollisionTypes.PhysicsObject_Walkable;
                return true;
            case 4:
                collisionType = CollisionTypes.Grabbed;
                return true;
            case 5:
                collisionType = CollisionTypes.None;
                return true;
            default:
                collisionType = CollisionTypes.None;
                return false;
        }
    }
    public override void RefreshProperties()
    {
        //Debug.Log("Refreshing properties, bouncy " + Bounciness);
        _sceneObject.BehaviorRequestedLayer(CollisionType2Layer(CollisionType), this, LayerRequestPriority);
    }
    private void OnDestroy()
    {
        if (Orchestrator.Instance == null || Orchestrator.Instance.IsAppClosing)
            return;
        //Debug.Log("Destroying CollisionType object!");
        _sceneObject = null;
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        //TODO use a zero-allocation int -> array function
        // Collision Type
        _serializedBehavior.LocallySetData(CollisionTypeKey, BitConverter.GetBytes(CollisionType2Int(CollisionType)));
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Collision Type
        byte[] collisionTypeArray;
        if (_serializedBehavior.TryReadProperty(CollisionTypeKey, out collisionTypeArray, out int _))
        {
            int collisionInt = BitConverter.ToInt32(collisionTypeArray, 0);
            CollisionTypes collisionType;
            if (!Int2CollisionType(collisionInt, out collisionType))
                Debug.LogError("Failed to handle received collision int of " + collisionInt + "!");
            else
                CollisionType = collisionType;
            _hasSetCollisionType = true;
            //Debug.Log("Receive collision int " + collisionInt + " type " + CollisionType);
        }
    }
    public override void Destroy()
    {
        Destroy(this);
    }
    public override List<ExposedVariable> GetVariables()
    {
        if(_userVariables.Count == 0)
        {
            _userVariables.Add(new ExposedVariable(ValString.Create("collisionType", false), "Which type of collision is the object set to?", CollisionType2String(CollisionType)));
            return _userVariables;
        }
        int i = 0;
        _userVariables[i++].SetString(CollisionType2String(CollisionType));
        return _userVariables;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return _userEvents;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic = Intrinsic.Create("SetCollisionType");
        intrinsic.AddParam("collisionType", CollisionType2String(CollisionTypes.Terrain));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                Debug.LogError("No scene object in intric call!");
                return Intrinsic.Result.Null;
            }

            CollisionTypeBehavior collisionBehavior = sceneObject.GetBehaviorByType<CollisionTypeBehavior>();
            if(collisionBehavior == null)
            {
                Debug.LogError("Collision behavior not present!");
                return Intrinsic.Result.Null;
            }

            // Parse out the collision requested, as either a string or int
            CollisionTypes collisionType;
            Value collisionVal = context.GetVar("collisionType");
            ValNumber collisionValNum = collisionVal as ValNumber;
            if (collisionValNum != null)
            {
                if (!Int2CollisionType((int)collisionValNum.value, out collisionType))
                {
                    Debug.LogWarning("Failed to parse collision type from int " + collisionValNum.value);
                    return Intrinsic.Result.Null;
                }
            } else
            {
                ValString collisionValStr = collisionVal as ValString;
                if (collisionValStr == null)
                    return Intrinsic.Result.Null;
                if (!String2CollisionType(collisionValStr.value, out collisionType))
                {
                    Debug.LogWarning("Failed to parse collision type from str " + collisionValStr.value);
                    return Intrinsic.Result.Null;
                }
            }

            // TODO update UI
            collisionBehavior.CollisionType = collisionType;
            collisionBehavior.RefreshProperties();

            return new Intrinsic.Result(ValNumber.one);
		};
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets which collision layer this object should be", null));
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return false;
    }
    public override bool DoesRequireCollider()
    {
        return CollisionType != CollisionTypes.None;
    }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
}
