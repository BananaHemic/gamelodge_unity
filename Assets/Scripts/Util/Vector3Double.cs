using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Vector3Double
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Z { get; private set; }

    public Vector3Double(Vector3 vec)
    {
        X = vec.x;
        Y = vec.y;
        Z = vec.z;
    }
    public Vector3Double(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    public static Vector3Double operator +(Vector3Double lhs, Vector3Double rhs)
    {
        return new Vector3Double(
            lhs.X + rhs.X,
            lhs.Y + rhs.Y,
            lhs.Z + rhs.Z);
    }
    public static Vector3Double operator -(Vector3Double lhs, Vector3Double rhs)
    {
        return new Vector3Double(
            lhs.X - rhs.X,
            lhs.Y - rhs.Y,
            lhs.Z - rhs.Z);
    }
    public static Vector3Double operator /(Vector3Double lhs, double rhs)
    {
        return new Vector3Double(
            lhs.X / rhs,
            lhs.Y / rhs,
            lhs.Z / rhs);
    }
    public Vector3 ToVector3()
    {
        return new Vector3((float)X, (float)Y, (float)Z);
    }
}
