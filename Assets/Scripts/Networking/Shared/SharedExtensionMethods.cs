using System;
using System.Collections.Generic;
#if UNITY
using UnityEngine;
#endif

public static class SharedExtensionMethods
{
    public static void WriteUshort(this byte[] ray, ushort num, ref int offset)
    {
        ray[offset++] = (byte)num;
        ray[offset++] = (byte)(num >> 8);
    }
    public static ushort ReadUshort(this byte[] ray, ref int offset)
    {
        ushort res = (ushort)ray[offset++];
        res += (ushort)(ray[offset++] << 8);
        return res;
    }
    public static void DeserializeVec3(this DarkRift.DarkRiftReader reader, ref Vector3 vec)
    {
        vec.x = reader.ReadSingle();
        vec.y = reader.ReadSingle();
        vec.z = reader.ReadSingle();
    }
    public static Vector3 DeserializeVec3(this DarkRift.DarkRiftReader reader)
    {
        return new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
    }
}
