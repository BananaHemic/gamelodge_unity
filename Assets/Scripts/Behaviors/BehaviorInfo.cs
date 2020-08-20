using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public abstract class BehaviorInfo
{
    // Serialized in the scene
    public string Name { get; private set; }
    public Sprite DisplaySprite { get; private set; }
    /// <summary>
    /// NB! The interpretation of this ID
    /// will change depending on if this is
    /// a local(i.e. C#) behavior, or one from the network
    /// </summary>
    public ushort BehaviorID { get; protected set; }

    public BehaviorInfo(string name, ushort id, Sprite displaySprite = null)
    {
        Name = name;
        BehaviorID = id;
        DisplaySprite = displaySprite;
    }
    public void SetName(string name)
    {
        Name = name;
    }
    public void SetBehaviorID(ushort id)
    {
        BehaviorID = id;
    }

    /// <summary>
    /// Returns if this behavior is from the
    /// network. Local and network behaviors use
    /// two different IDs
    /// </summary>
    /// <returns></returns>
    public abstract bool IsNetworkedScript();
    public abstract Type GetBehaviorType();
    public abstract BaseBehavior Create(SceneObject sceneObject);
}
