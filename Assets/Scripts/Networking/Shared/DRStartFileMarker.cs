using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DRStartFileMarker : IDarkRiftSerializable
{
    /// <summary>
    /// The application version used in this file.
    /// Provided here for when the start marker file
    /// format changes
    /// </summary>
    public int ApplicationVersion { get; private set; }
    /// <summary>
    /// The ID of the user who made this recording
    /// </summary>
    public ushort RecordingUserID { get; private set; }
    /// <summary>
    /// The date that this recording was made
    /// </summary>
    public DateTime DateOfRecording { get; private set; }
    /// <summary>
    /// The unscaledTime of the recording client
    /// </summary>
    public float Timestamp { get; private set; }
    /// <summary>
    /// The time on the server when the recording began
    /// Used to update ownership stuff
    /// </summary>
    public uint ServerTime { get; private set; }

    public DRStartFileMarker() { }
    public void Update(ushort recordingUserID, float timestamp)
    {
        RecordingUserID = recordingUserID;
        DateOfRecording = DateTime.Now;
        Timestamp = timestamp;
        ApplicationVersion = DRGameState.ApplicationVersion;
        ServerTime = DarkRiftPingTime.Instance.ServerTime;
    }
    public void Deserialize(DeserializeEvent e)
    {
        ApplicationVersion = e.Reader.DecodeInt32();
        RecordingUserID = e.Reader.ReadUInt16();
        DateOfRecording = DateTime.FromBinary(e.Reader.ReadInt64());
        Timestamp = e.Reader.ReadSingle();
        if (ApplicationVersion < 2)
        {
            ServerTime = 0;
            return;
        }
        ServerTime = e.Reader.ReadUInt32();
    }
    public void Serialize(SerializeEvent e)
    {
        e.Writer.EncodeInt32(ApplicationVersion);
        e.Writer.Write(RecordingUserID);
        e.Writer.Write(DateOfRecording.ToBinary());
        e.Writer.Write(Timestamp);
        e.Writer.Write(ServerTime);
    }
}
