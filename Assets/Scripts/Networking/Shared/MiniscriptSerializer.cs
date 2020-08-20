using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY
using Miniscript;
using UnityEngine;
#endif

public static class MiniscriptSerializer
{
    const byte SByteType = 0;
    const byte ByteType = 1;
    const byte ShortType = 2;
    const byte UShortType = 3;
    const byte IntType = 4;
    const byte UIntType = 5;
    const byte FloatType = 6;
    const byte DoubleType = 7;
    const byte ShortStringType = 8;
    const byte LargeStringType = 9;
    // TODO List support for bytes, string, amorphous, ValQuaternion, ValSceneObject, etc.

    /// <summary>
    /// Used by the server to just copy over Miniscript values, without
    /// it needing to understand their meaning
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="writer"></param>
    public static void DeserializeInto(DarkRiftReader reader, DarkRiftWriter writer)
    {
        byte topTag = reader.ReadByte();
        writer.Write(topTag);
        switch (topTag)
        {
            case SByteType:
                writer.Write(reader.ReadSByte());
                return;
            case ByteType:
                writer.Write(reader.ReadByte());
                return;
            case ShortType:
                writer.Write(reader.ReadInt16());
                return;
            case UShortType:
                writer.Write(reader.ReadUInt16());
                return;
            case IntType:
                writer.Write(reader.ReadInt32());
                return;
            case UIntType:
                writer.Write(reader.ReadUInt32());
                return;
            case FloatType:
                writer.Write(reader.ReadSingle());
                return;
            case DoubleType:
                writer.Write(reader.ReadDouble());
                return;
            default:
                DRCompat.LogError("Failed to handle message type: " + topTag);
                return;
        }
    }
#if UNITY
    public static void Serialize(DarkRiftWriter writer, Value val)
    {
        ValNumber num = val as ValNumber;
        if(num != null)
        {
            SerializeNumber(writer, num.value);
            return;
        }
        if (val == null || val == ValNull.instance)
            return;

        // TODO
        Debug.LogError("Not serializing value type: " + val.GetBaseMiniscriptType());
    }
    public static void SerializeNumber(DarkRiftWriter writer, double num)
    {
        if(num == Math.Floor(num))
        {
            // This is a non-floating point number
            if(num < 0)
            {
                if (num > sbyte.MinValue)
                {
                    SerializeSByte(writer, (sbyte)num);
                    return;
                }
                if (num > short.MinValue)
                {
                    SerializeShort(writer, (short)num);
                    return;
                }
                if(num > int.MinValue)
                {
                    SerializeInt(writer, (int)num);
                    return;
                }
                // This is a very large number, just use float/double
            }
            else
            {
                if(num < byte.MaxValue)
                {
                    SerializeByte(writer, (byte)num);
                    return;
                }
                if(num < ushort.MaxValue)
                {
                    SerializeUShort(writer, (ushort)num);
                    return;
                }
                if(num < uint.MaxValue)
                {
                    SerializeUInt(writer, (uint)num);
                    return;
                }
                // This is a very large number, just use float/double
            }
        }

        float fVal = (float)num;
        if(fVal - num < 0.01)
        {
            // This is approximately a float
            SerializeFloat(writer, fVal);
            return;
        }
        // Last resort, we need to use a double
        SerializeDouble(writer, num);
        return;
    }
    public static int ApproximateSizeForValue(Value val)
    {
        if (val is ValNumber)
            return sizeof(double); // Best to overestimate
        ValString str = val as ValString;
        if (str != null)
            return (str.value.Length < byte.MaxValue ? 1 : 4);
        if (val == null || val == ValNull.instance)
            return 0;
        Debug.LogError("Unhandled type " + val.GetBaseMiniscriptType());
        return 32;
    }
    public static Value Deserialize(DarkRiftReader reader)
    {
        byte topTag = reader.ReadByte();
        switch (topTag)
        {
            case SByteType:
                return ValNumber.Create(reader.ReadSByte());
            case ByteType:
                return ValNumber.Create(reader.ReadByte());
            case ShortType:
                return ValNumber.Create(reader.ReadInt16());
            case UShortType:
                return ValNumber.Create(reader.ReadUInt16());
            case IntType:
                return ValNumber.Create(reader.ReadInt32());
            case UIntType:
                return ValNumber.Create(reader.ReadUInt32());
            case FloatType:
                return ValNumber.Create(reader.ReadSingle());
            case DoubleType:
                return ValNumber.Create(reader.ReadDouble());
            default:
                Debug.LogError("Failed to handle message type: " + topTag);
                return ValNull.instance;
        }
    }
    public static void SerializeSByte(DarkRiftWriter writer, sbyte val)
    {
        writer.Write(SByteType);
        writer.Write(val);
    }
    public static void SerializeShort(DarkRiftWriter writer, short val)
    {
        writer.Write(ShortType);
        writer.Write(val);
    }
    public static void SerializeInt(DarkRiftWriter writer, int val)
    {
        writer.Write(IntType);
        writer.Write(val);
    }
    public static void SerializeByte(DarkRiftWriter writer, byte val)
    {
        writer.Write(ByteType);
        writer.Write(val);
    }
    public static void SerializeUShort(DarkRiftWriter writer, ushort val)
    {
        writer.Write(UShortType);
        writer.Write(val);
    }
    public static void SerializeUInt(DarkRiftWriter writer, uint val)
    {
        writer.Write(UIntType);
        writer.Write(val);
    }
    public static void SerializeFloat(DarkRiftWriter writer, float val)
    {
        writer.Write(FloatType);
        writer.Write(val);
    }
    public static void SerializeDouble(DarkRiftWriter writer, double val)
    {
        writer.Write(DoubleType);
        writer.Write(val);
    }
#endif
}
