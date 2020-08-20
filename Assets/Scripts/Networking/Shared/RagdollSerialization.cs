using System.Collections;
using System.Collections.Generic;
using DarkRift;
#if UNITY
using UnityEngine;
#endif

public struct RagdollMain
{
    // Head
    public Vector3 HeadPos;
    public Quaternion HeadRot;
    public Vector3 HeadVel;
    public Vector3 HeadAngVel;
    // UpperTorso
    public Vector3 UpperTorsoPos;
    public Quaternion UpperTorsoRot;
    public Vector3 UpperTorsoVel;
    public Vector3 UpperTorsoAngVel;
    // LowerTorso
    public Vector3 LowerTorsoPos;
    public Quaternion LowerTorsoRot;
    public Vector3 LowerTorsoVel;
    public Vector3 LowerTorsoAngVel;
    // UpperArmL
    public Vector3 UpperArmLPos;
    public Quaternion UpperArmLRot;
    public Vector3 UpperArmLVel;
    public Vector3 UpperArmLAngVel;
    // UpperArmR
    public Vector3 UpperArmRPos;
    public Quaternion UpperArmRRot;
    public Vector3 UpperArmRVel;
    public Vector3 UpperArmRAngVel;
    // UpperLegL
    public Vector3 UpperLegLPos;
    public Quaternion UpperLegLRot;
    public Vector3 UpperLegLVel;
    public Vector3 UpperLegLAngVel;
    // UpperLegR
    public Vector3 UpperLegRPos;
    public Quaternion UpperLegRRot;
    public Vector3 UpperLegRVel;
    public Vector3 UpperLegRAngVel;
}

public static class RagdollSerialization {
    public static void Serialize(ref RagdollMain ragdoll, DarkRiftWriter writer)
    {
        // Head
        ragdoll.HeadPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.HeadRot);
        ragdoll.HeadVel.Serialize(writer);
        ragdoll.HeadAngVel.Serialize(writer);
        // UpperTorso
        ragdoll.UpperTorsoPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.UpperTorsoRot);
        ragdoll.UpperTorsoVel.Serialize(writer);
        ragdoll.UpperTorsoAngVel.Serialize(writer);
        // LowerTorso
        ragdoll.LowerTorsoPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.LowerTorsoRot);
        ragdoll.LowerTorsoVel.Serialize(writer);
        ragdoll.LowerTorsoAngVel.Serialize(writer);
        // UpperArmL
        ragdoll.UpperArmLPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.UpperArmLRot);
        ragdoll.UpperArmLVel.Serialize(writer);
        ragdoll.UpperArmLAngVel.Serialize(writer);
        // UpperArmR
        ragdoll.UpperArmRPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.UpperArmRRot);
        ragdoll.UpperArmRVel.Serialize(writer);
        ragdoll.UpperArmRAngVel.Serialize(writer);
        // UpperLegL
        ragdoll.UpperLegLPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.UpperLegLRot);
        ragdoll.UpperLegLVel.Serialize(writer);
        ragdoll.UpperLegLAngVel.Serialize(writer);
        // UpperLegR
        ragdoll.UpperLegRPos.Serialize(writer);
        writer.WriteCompressedRotation(ragdoll.UpperLegRRot);
        ragdoll.UpperLegRVel.Serialize(writer);
        ragdoll.UpperLegRAngVel.Serialize(writer);
    }
    public static void Deserialize(out RagdollMain ragdoll, DarkRiftReader reader)
    {
        // Head
        ragdoll = new RagdollMain()
        {
            HeadPos = reader.DeserializeVec3(),
            HeadRot = reader.ReadCompressedRotation(),
            HeadVel = reader.DeserializeVec3(),
            HeadAngVel = reader.DeserializeVec3(),
            // UpperTorso
            UpperTorsoPos = reader.DeserializeVec3(),
            UpperTorsoRot = reader.ReadCompressedRotation(),
            UpperTorsoVel = reader.DeserializeVec3(),
            UpperTorsoAngVel = reader.DeserializeVec3(),
            // LowerTorso
            LowerTorsoPos = reader.DeserializeVec3(),
            LowerTorsoRot = reader.ReadCompressedRotation(),
            LowerTorsoVel = reader.DeserializeVec3(),
            LowerTorsoAngVel = reader.DeserializeVec3(),
            // UpperArmL
            UpperArmLPos = reader.DeserializeVec3(),
            UpperArmLRot = reader.ReadCompressedRotation(),
            UpperArmLVel = reader.DeserializeVec3(),
            UpperArmLAngVel = reader.DeserializeVec3(),
            // UpperArmR
            UpperArmRPos = reader.DeserializeVec3(),
            UpperArmRRot = reader.ReadCompressedRotation(),
            UpperArmRVel = reader.DeserializeVec3(),
            UpperArmRAngVel = reader.DeserializeVec3(),
            // UpperLegL
            UpperLegLPos = reader.DeserializeVec3(),
            UpperLegLRot = reader.ReadCompressedRotation(),
            UpperLegLVel = reader.DeserializeVec3(),
            UpperLegLAngVel = reader.DeserializeVec3(),
            // UpperLegR
            UpperLegRPos = reader.DeserializeVec3(),
            UpperLegRRot = reader.ReadCompressedRotation(),
            UpperLegRVel = reader.DeserializeVec3(),
            UpperLegRAngVel = reader.DeserializeVec3()
        };
    }
}
