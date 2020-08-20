using DarkRift;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DRFileMarker : IDarkRiftSerializable
{
    public float Timestamp { get; private set; }
    /// <summary>
    /// How many bytes are in the following message?
    /// There may be sub messages in the following message, they
    /// will all share the same file marker info
    /// </summary>
    public int DataLength { get; private set; }
    public MessageDirection MessageDir { get; private set; }
    public SendMode SendType { get; private set; }

    public DRFileMarker() { }
    public DRFileMarker(float timestamp, int dataLength, SendMode sendMode, MessageDirection msgDir)
    {
        Timestamp = timestamp;
        DataLength = dataLength;
        MessageDir = msgDir;
        SendType = sendMode;
    }
    public void Update(float timestamp, int dataLength, SendMode sendMode, MessageDirection msgDir)
    {
        Timestamp = timestamp;
        DataLength = dataLength;
        MessageDir = msgDir;
        SendType = sendMode;
    }
    public void Deserialize(DeserializeEvent e)
    {
        Timestamp = e.Reader.ReadSingle();
        DataLength = e.Reader.ReadInt32();
        MessageDir = (MessageDirection)e.Reader.ReadByte();
        SendType = (SendMode)e.Reader.ReadByte();
    }
    public void Serialize(SerializeEvent e)
    {
        e.Writer.Write(Timestamp);
        e.Writer.Write(DataLength);
        e.Writer.Write((byte)MessageDir);
        e.Writer.Write((byte)SendType);
    }
}
