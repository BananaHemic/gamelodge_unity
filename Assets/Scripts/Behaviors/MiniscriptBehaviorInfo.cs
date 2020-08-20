using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniscriptBehaviorInfo : BehaviorInfo
{
    public MiniscriptBehaviorInfo(string name, ushort id, Sprite displaySprite = null) : base(name, id, displaySprite)
    {
    }

    public void UpdateID(ushort newID)
    {
        BehaviorID = newID;
    }
    public override BaseBehavior Create(SceneObject sceneObject)
    {
        UserScriptBehavior userScriptBehavior = UserScriptManager.Instance.InstantiateBehavior(BehaviorID, sceneObject);
        return userScriptBehavior;
    }
    public override Type GetBehaviorType()
    {
        return null;
    }
    public override bool IsNetworkedScript()
    {
        return true;
    }
}
