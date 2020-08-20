using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System;

public class ValSceneObject : ValCustom
{
    public readonly SceneObject SceneObject;
    private List<BehaviorInfo> _attachedBehaviors;

    private static bool _hasLoadedIntrinsics = false;

    public ValSceneObject(SceneObject sceneObject) : base(false)
    {
        SceneObject = sceneObject;
    }
    protected override void ResetState()
    {
    }
    protected override void ReturnToPool()
    {
    }
    public override bool Resolve(string identifier, out Value val)
    {
        //Debug.Log("Resolving " + identifier);
        // See if there's a function for this identifier
        var functions = SceneObject.GetAllAvailableUserFunctions();
        for(int i = 0; i < functions.Count; i++)
        {
            var func = functions[i];
            //Debug.Log("name: " + func.Name);
            if(func.Name == identifier)
            {
                val = func.Intrinsic.GetFunc();
                //Intrinsic.Result res = func.Intrinsic.code(ctx, default);
                //val = res.result;
                return true;
            }
        }

        // We turn the SceneObject variables into functions
        // this way we can just keep checking it without having to
        // get a new ValSceneObject each time
        var variables = SceneObject.GetAllExposedVariables();
        for(int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if(variable.Name == identifier)
            {
                val = variable.Value;
                return true;
            }
        }

        Debug.LogWarning("Failed to resolve " + identifier);
        val = null;
        return false;
    }
    public override Value Lookup(Value key) { return null; }
    public override double Equality(Value rhs, int recursionDepth = 16)
    {
        return rhs is ValSceneObject && ((ValSceneObject)rhs).SceneObject == SceneObject ? 1 : 0;
    }
    public override int Hash(int recursionDepth = 16)
    {
        return SceneObject.GetHashCode();
    }
    public override string ToString(Machine vm)
    {
        return "SceneObject#" + SceneObject.GetID();
    }
    public static void InitIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;
    }
}
