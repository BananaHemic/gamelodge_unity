using DarkRift;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if UNITY
using UnityEngine;
#endif


/// <summary>
///     A primative 3 axis vector.
/// </summary>
namespace DarkRift
{
    public class Vec3 : IDarkRiftSerializable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vec3()
        {

        }
        public Vec3(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
        public void Deserialize(DeserializeEvent e)
        {
            this.X = e.Reader.ReadSingle();
            this.Y = e.Reader.ReadSingle();
            this.Z = e.Reader.ReadSingle();
        }
        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(X);
            e.Writer.Write(Y);
            e.Writer.Write(Z);
        }
        public void UpdateFrom(Vec3 other)
        {
            X = other.X;
            Y = other.Y;
            Z = other.Z;
        }
        public void UpdateFrom(Vector3 other)
        {
            X = other.x;
            Y = other.y;
            Z = other.z;
        }
        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(X);
            sb.Append(",");
            sb.Append(Y);
            sb.Append(",");
            sb.Append(Z);
            return sb.ToString();
        }
    }
}
