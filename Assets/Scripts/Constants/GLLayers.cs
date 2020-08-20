using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GLLayers
{
    // Layers
    public const int TableLayerNum = 10;
    public const int TableLayerMask = 1 << TableLayerNum;
    public const int TerrainLayerNum = 11;
    public const int TerrainLayerMask = 1 << TerrainLayerNum;
    public const int PhysicsObject_NonWalkableLayerNum = 12;
    public const int PhysicsObject_NonWalkableLayerMask = 1 << PhysicsObject_NonWalkableLayerNum;
    public const int RagdollLayerNum = 13;
    public const int RagdollLayerMask = 1 << RagdollLayerNum;
    public const int LocalUser_PlayLayerNum = 15;
    public const int LocalUser_PlayLayerMask = 1 << LocalUser_PlayLayerNum;
    public const int OtherUser_PlayLayerNum = 16;
    public const int OtherUser_PlayLayerMask = 1 << OtherUser_PlayLayerNum;
    public const int DefaultLayerNum = 0;
    public const int DefaultLayerMask = 1 << DefaultLayerNum;
    public const int IgnoreSelectLayerNum = 9; // This layer isn't selected
    public const int IgnoreSelectLayerMask = 1 << IgnoreSelectLayerNum;
    public const int BuildGrabbableLayerNum = 17; // This layer is to help the build mode know when it's grabbing something
    public const int BuildGrabbableLayerMask = 1 << BuildGrabbableLayerNum;
    public const int GrabbedLayerNum = 18; // This layer is for objects that we're currently grabbing
    public const int GrabbedLayerMask = 1 << GrabbedLayerNum;
    public const int PhysicsObject_WalkableLayerNum = 19; // This layer is for physics object that we are able to walk on
    public const int PhysicsObject_WalkableLayerMask = 1 << PhysicsObject_WalkableLayerNum;
    public const int BulletLayerNum = 20;
    public const int BulletLayerMask = 1 << BulletLayerNum;
    /// <summary>
    /// We want a layer int to correpond to "no colliders at all"
    /// So we use layer 3, which unity has reserved for god know why
    /// </summary>
    public const int NoneLayerNum = 3;

    // Groups of layers
    /// <summary>
    /// Everything that we can grab while in play mode
    /// </summary>
    public const int AllGrabbable_Play = TerrainLayerMask | PhysicsObject_NonWalkableLayerMask | PhysicsObject_WalkableLayerMask;
    /// <summary>
    /// Everything that we can put new objects on top of
    /// </summary>
    public const int AllCanDragOnto_Build = TableLayerMask | TerrainLayerMask | PhysicsObject_NonWalkableLayerMask | PhysicsObject_WalkableLayerMask | BuildGrabbableLayerMask;
    /// <summary>
    /// All layers that we're able to take ownership of due to a collision
    /// </summary>
    public const int AllObjectWithTakeableOwnership = PhysicsObject_NonWalkableLayerMask | PhysicsObject_WalkableLayerMask | RagdollLayerMask | BulletLayerMask;
    /// <summary>
    /// All the objects that we can reasonably raycast
    /// </summary>
    public const int AllCanRaycast = 0
        | TableLayerMask
        | TerrainLayerMask
        | PhysicsObject_NonWalkableLayerMask 
        | RagdollLayerMask 
        | LocalUser_PlayLayerMask
        | OtherUser_PlayLayerMask
        | GrabbedLayerMask
        | PhysicsObject_WalkableLayerMask
        | BulletLayerMask;

    // Tags
    public const string DefaultTag = "Default";
    public const string SceneObjectTag = "SceneObject";
    public const string GrabbableTag = "Grabbable";
    public const string BuildGrabbableTag = "BuildGrabbable";
}
