using DarkRift;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
///     A primative 2 axis vector.
/// </summary>
public class Vec2 : IDarkRiftSerializable
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vec2()
    {

    }

    public Vec2(float x, float y)
    {
        this.X = x;
        this.Y = y;
    }

    public void Deserialize(DeserializeEvent e)
    {
        this.X = e.Reader.ReadSingle();
        this.Y = e.Reader.ReadSingle();
    }

    public void Serialize(SerializeEvent e)
    {
        e.Writer.Write(X);
        e.Writer.Write(Y);
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(X);
        sb.Append(",");
        sb.Append(Y);
        return sb.ToString();
    }
}
