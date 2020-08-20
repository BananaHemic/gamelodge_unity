using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CSharpBehaviorInfo : BehaviorInfo
{
    private readonly Type _type = null;

    public CSharpBehaviorInfo(Type type, string name, ushort id, Sprite displaySprite = null) : base(name, id, displaySprite)
    {
        _type = type;
    }
    public override Type GetBehaviorType()
    {
        return _type;
    }
    public override BaseBehavior Create(SceneObject sceneObject)
    {
        BaseBehavior instance = sceneObject.gameObject.AddComponent(_type) as BaseBehavior;
        //Debug.Log("Set instance " + (instance == null ? "null" : "non-null"));
        return instance;
    }

    public override bool IsNetworkedScript()
    {
        return false;
    }
}
