using DarkRift;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if UNITY
using UnityEngine;
#endif

namespace DarkRift
{
    public class Quat : IDarkRiftSerializable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public Quat()
        {

        }
        public Quat(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }
        public void UpdateFrom(Quat quat)
        {
            X = quat.X;
            Y = quat.Y;
            Z = quat.Z;
            W = quat.W;
        }
        public void UpdateFrom(Quaternion quaternion)
        {
            X = quaternion.x;
            Y = quaternion.y;
            Z = quaternion.z;
            W = quaternion.w;
        }
        public Quaternion ToQuaternion()
        {
            return new Quaternion(X, Y, Z, W);
        }
        public void Deserialize(DeserializeEvent e)
        {
            this.X = e.Reader.ReadSingle();
            this.Y = e.Reader.ReadSingle();
            this.Z = e.Reader.ReadSingle();
            this.W = e.Reader.ReadSingle();
            //TODO optimize
        }
        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(X);
            e.Writer.Write(Y);
            e.Writer.Write(Z);
            e.Writer.Write(W);
        }
    }
}
