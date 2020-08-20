using System.Collections;
using System.Collections.Generic;
using DarkRift;
using System;

public static class DRExtensions
{
    public static void RemoveBySwap<T>(List<T> list, int index)
    {
        list[index] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
    }
    /// <summary>
    /// Gets the magnitude difference between two uints
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="isAGreater">Is A greater than or equal to B</param>
    /// <returns></returns>
    public static uint SafeDifference(uint a, uint b, out bool isAGreater)
    {
        if (a >= b)
        {
            isAGreater = true;
            return (a - b);
        }
        isAGreater = false;
        return b - a;
    }
    public static void EncodeInt32(this DarkRiftWriter writer, int value)
    {
        if (writer == null)
            throw new ArgumentNullException("writer");
        if (value < 0)
            throw new ArgumentOutOfRangeException("value", value, "value must be 0 or greater");

        bool first = true;
        while (first || value > 0)
        {
            first = false;
            byte lower7bits = (byte)(value & 0x7f);
            value >>= 7;
            if (value > 0)
                lower7bits |= 128;
            writer.Write(lower7bits);
        }
    }
    public static int DecodeInt32(this DarkRiftReader reader)
    {
        if (reader == null)
            throw new ArgumentNullException("reader");

        bool more = true;
        int value = 0;
        int shift = 0;
        while (more)
        {
            byte lower7bits = reader.ReadByte();
            more = (lower7bits & 128) != 0;
            value |= (lower7bits & 0x7f) << shift;
            shift += 7;
        }
        return value;
    }
    /// <summary>
    /// 
    /// S3 naming rules:
    /// The bucket name can be between 3 and 63 characters long, and can contain only lower-case characters, numbers, periods, and dashes.
    /// Each label in the bucket name must start with a lowercase letter or number.
    /// The bucket name cannot contain underscores, end with a dash, have consecutive periods, or use dashes adjacent to periods
    /// </summary>
    /// <param name="stringLength"></param>
    /// <param name="rd"></param>
    /// <returns></returns>
    public static string CreateRandomS3KeyString(int stringLength, Random rd)
    {
        const string allowedChars = "abcdefghijkmnopqrstuvwxyz0123456789";
        char[] chars = new char[stringLength];

        for (int i = 0; i < stringLength; i++)
            chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];

        return new string(chars);
    }
}
