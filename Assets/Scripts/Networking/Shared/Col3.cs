using DarkRift;
using System;
using System.Collections.Generic;
#if UNITY
using UnityEngine;
#endif

public class Col3 : IDarkRiftSerializable
{
    public const int MessageLen = 3;
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }

    public Col3() { }
    public Col3(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }
    public Col3(float red, float green, float blue)
    {
        Red = (byte)(red * byte.MaxValue);
        Green = (byte)(green * byte.MaxValue);
        Blue = (byte)(blue * byte.MaxValue);
    }
    public Col3(Col3 other)
    {
        Red = other.Red;
        Green = other.Green;
        Blue = other.Blue;
    }
    public void UpdateFrom(Col3 other)
    {
        Red = other.Red;
        Green = other.Green;
        Blue = other.Blue;
    }
#if UNITY
    public Col3(Color color)
    {
        UpdateFrom(color);
    }
    public void UpdateFrom(Color color)
    {
        Red = (byte)Mathf.RoundToInt(color.r * byte.MaxValue);
        Green = (byte)Mathf.RoundToInt(color.g * byte.MaxValue);
        Blue = (byte)Mathf.RoundToInt(color.b * byte.MaxValue);
    }
    public Color ToColor()
    {
        return new Color((float)Red / byte.MaxValue, (float)Green / byte.MaxValue, (float)Blue / byte.MaxValue);
    }
#endif
    public void Deserialize(DeserializeEvent e)
    {
        Red = e.Reader.ReadByte();
        Green = e.Reader.ReadByte();
        Blue = e.Reader.ReadByte();
    }

    public void Serialize(SerializeEvent e)
    {
        e.Writer.Write(Red);
        e.Writer.Write(Green);
        e.Writer.Write(Blue);
    }
}