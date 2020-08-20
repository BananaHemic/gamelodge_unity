using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Some Behavior keys need to be used on both the
/// server and the client, so we put them here
/// </summary>
public static class SharedBehaviorKeys
{
    public const int DestroyOnDepossessKey_Character = 0;

    /// <summary>
    /// Behavior IDs
    /// </summary>
    public const ushort PhysicsBehaviorID = 0;
    public const ushort GrabbableBehaviorID = 1;
    public const ushort AudioPlayerBehaviorID = 2;
    public const ushort SpawnPointBehaviorID = 3;
    public const ushort CollisionTypeBehaviorID = 4;
    public const ushort PhysSoundBehaviorID = 5;
    public const ushort ConfigurableJointBehaviorID = 6;
    public const ushort CharacterBehaviorID = 7;
    public const ushort RagdollBehaviorID = 8;
    public const ushort VirtualCameraBehaviorID = 9;
    public const ushort LineRendererBehaviorID = 10;
    public const ushort MovingPlatformBehaviorID = 11;
    public const ushort HealthPlayformBehaviorID = 12;

    public const int NumBehaviors = 13;
}
