using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all the pre-compiled C# behaviors
/// </summary>
public class CSharpBehaviorManager : GenericSingleton<CSharpBehaviorManager>
{
    public Sprite PhysicsSprite;
    public Sprite GrabbableSprite;
    public Sprite AudioPlayerSprite;
    public Sprite SpawnPlayerSprite;
    public Sprite CollisionTypeSprite;
    public Sprite PhysSoundSprite;
    public Sprite CharacterSprite;
    public Sprite RagdollSprite;
    public Sprite VirtualCameraSprite;
    public Sprite LineRendererSprite;
    public Sprite MovingPlatformSprite;
    public Sprite HealthSprite;

    public CSharpBehaviorInfo CharacterBehaviorInfo { get; private set; }

    // The built-in types
    private CSharpBehaviorInfo[] _cSharpBehaviors = new CSharpBehaviorInfo[SharedBehaviorKeys.NumBehaviors];

    protected override void Awake()
    {
        base.Awake();
        // Init all the built-in Behavior Data
        _cSharpBehaviors[SharedBehaviorKeys.PhysicsBehaviorID] = new CSharpBehaviorInfo(typeof(PhysicsBehavior), "Physics", SharedBehaviorKeys.PhysicsBehaviorID, PhysicsSprite);
        _cSharpBehaviors[SharedBehaviorKeys.GrabbableBehaviorID] = new CSharpBehaviorInfo(typeof(GrabbableBehavior), "Grabbable", SharedBehaviorKeys.GrabbableBehaviorID, GrabbableSprite);
        _cSharpBehaviors[SharedBehaviorKeys.AudioPlayerBehaviorID] = new CSharpBehaviorInfo(typeof(AudioPlayerBehavior), "Audio Player", SharedBehaviorKeys.AudioPlayerBehaviorID, AudioPlayerSprite);
        _cSharpBehaviors[SharedBehaviorKeys.SpawnPointBehaviorID] = new CSharpBehaviorInfo(typeof(SpawnPointBehavior), "Spawn Point", SharedBehaviorKeys.SpawnPointBehaviorID, SpawnPlayerSprite);
        _cSharpBehaviors[SharedBehaviorKeys.CollisionTypeBehaviorID] = new CSharpBehaviorInfo(typeof(CollisionTypeBehavior), "Collision Type", SharedBehaviorKeys.CollisionTypeBehaviorID, CollisionTypeSprite);
        _cSharpBehaviors[SharedBehaviorKeys.PhysSoundBehaviorID] = new CSharpBehaviorInfo(typeof(PhysSoundBehavior), "Collision Sounds", SharedBehaviorKeys.PhysSoundBehaviorID, PhysSoundSprite);
        _cSharpBehaviors[SharedBehaviorKeys.ConfigurableJointBehaviorID] = new CSharpBehaviorInfo(typeof(ConfigurableJointBehavior), "Configurable Joint", SharedBehaviorKeys.ConfigurableJointBehaviorID, PhysSoundSprite);
        CharacterBehaviorInfo = new CSharpBehaviorInfo(typeof(CharacterBehavior), "Character", SharedBehaviorKeys.CharacterBehaviorID, CharacterSprite);
        _cSharpBehaviors[SharedBehaviorKeys.CharacterBehaviorID] = CharacterBehaviorInfo;
        _cSharpBehaviors[SharedBehaviorKeys.RagdollBehaviorID] = new CSharpBehaviorInfo(typeof(RagdollBehavior), "Ragdoll", SharedBehaviorKeys.RagdollBehaviorID, RagdollSprite);
        _cSharpBehaviors[SharedBehaviorKeys.VirtualCameraBehaviorID] = new CSharpBehaviorInfo(typeof(VirtualCameraBehavior), "Virtual Camera", SharedBehaviorKeys.VirtualCameraBehaviorID, VirtualCameraSprite);
        _cSharpBehaviors[SharedBehaviorKeys.LineRendererBehaviorID] = new CSharpBehaviorInfo(typeof(LineRendererBehavior), "Line Renderer", SharedBehaviorKeys.LineRendererBehaviorID, LineRendererSprite);
        _cSharpBehaviors[SharedBehaviorKeys.MovingPlatformBehaviorID] = new CSharpBehaviorInfo(typeof(MovingPlatformBehavior), "Moving Platform", SharedBehaviorKeys.MovingPlatformBehaviorID, MovingPlatformSprite);
        _cSharpBehaviors[SharedBehaviorKeys.HealthPlayformBehaviorID] = new CSharpBehaviorInfo(typeof(HealthBehavior), "Health", SharedBehaviorKeys.HealthPlayformBehaviorID, HealthSprite);
    }
    public CSharpBehaviorInfo GetScriptByName(string scriptName)
    {
        for(int i = 0; i < _cSharpBehaviors.Length; i++)
        {
            CSharpBehaviorInfo potential = _cSharpBehaviors[i];
            if (potential.Name == scriptName)
                return potential;
        }
        return null;
    }
    public BehaviorInfo GetBehaviorInfoFromID(ushort id)
    {
        foreach(var behavior in _cSharpBehaviors)
        {
            if (behavior.BehaviorID == id)
                return behavior;
        }
        Debug.LogError("Behavior Data not found for " + id + " make sure it's not a networked script");
        return null;
    }

    public CSharpBehaviorInfo[] GetAllCSharpBehaviors()
    {
        return _cSharpBehaviors;
    }
}
