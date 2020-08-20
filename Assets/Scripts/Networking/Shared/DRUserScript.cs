using System.Collections;
using System.Collections.Generic;
using DarkRift;
using System.Text;
using System;

/// <summary>
/// Represents a miniscript file made by a user.
/// These can either belong to a Bundle, or to a specific game
/// </summary>
public class DRUserScript : IDarkRiftSerializable
{
    /// <summary>
    /// The ID for this object. This is assigned by the server.
    /// Valid values from 256 -> ushort_max
    /// </summary>
    public ushort ObjectID;
    /// <summary>
    /// The temporary ID for this object. This is used when the user creates
    /// a new object, and an ID from the server has not yet been received.
    /// We immediately start sending out updates using the temporary ID,
    /// the server will replace the temporary ID with whatever final object ID
    /// it has created.
    /// Valid values [0,255]
    /// </summary>
    public ushort TemporaryID;
    /// <summary>
    /// The bundle that this script is from.
    /// Will be null if it was created at runtime
    /// </summary>
    public string BundleID;
    /// <summary>
    /// The script index for this script, within
    /// the bundle. Will be 0 for user created at runtime
    /// </summary>
    public ushort BundleIndex;
    /// <summary>
    /// Whether this script requests that the attached object have it's position, rotation, and scale
    /// syncronized
    /// </summary>
    public bool SyncPosRotScale { get; private set; }

    const int MaxTitleLength = byte.MaxValue;
    public string Name { get; private set; }
    public WhoRuns WhoRunsScript { get; private set; }
    private readonly StringBuilder _codeBuilder = new StringBuilder();
    private string _codePostScript = null;
    private string _codeWithPostScript = null;
    private string _codeWithoutPostScript = null;

    public enum WhoRuns
    {
        Owner,
        Everyone,
        Host
    };

    public DRUserScript() { }// Needed for DR
    public DRUserScript(ushort id, string name, string code, bool syncPosRotScale, WhoRuns whoRuns, string bundleID = null, ushort bundleIndex = 0)
    {
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;
        BundleID = bundleID;
        BundleIndex = bundleIndex;
        WhoRunsScript = whoRuns;

        if (name.Length > MaxTitleLength)
            name = name.Substring(0, MaxTitleLength);
        Name = name;
        _codeBuilder.Append(code);
        _codeWithoutPostScript = code;
        SyncPosRotScale = syncPosRotScale;
    }
    /// <summary>
    /// Updates everything except the ID
    /// </summary>
    /// <param name="otherScript"></param>
    public void UpdateDataFrom(DRUserScript otherScript)
    {
        BundleID = otherScript.BundleID;
        BundleIndex = otherScript.BundleIndex;
        SyncPosRotScale = otherScript.SyncPosRotScale;
        Name = otherScript.Name;
        WhoRunsScript = otherScript.WhoRunsScript;
        SetCode(otherScript.GetCodeWithoutPostScript());
    }
    public ushort GetID()
    {
        if (ObjectID > 0)
            return ObjectID;
        return TemporaryID;
    }
    public void SetName(string name)
    {
        Name = name;
    }
    public void SetCode(string newCode)
    {
        _codeBuilder.Clear();
        _codeBuilder.Append(newCode);
        _codeWithoutPostScript = newCode;
        _codePostScript = null;
        _codeWithPostScript = null;
    }
    public string GetCodeWithoutPostScript()
    {
        if (_codeWithoutPostScript != null)
            return _codeWithoutPostScript;

        int codeLen = _codePostScript == null ? _codeBuilder.Length : (_codeBuilder.Length - _codePostScript.Length);
        _codeWithoutPostScript = _codeBuilder.ToString(0, codeLen);
        return _codeWithoutPostScript;
    }
    public string GetCodeWithPostScript(string postScript)
    {
        // If this is the same post script as what we last got, then
        // just return the result that's already in the code builder
        if (object.ReferenceEquals(_codePostScript, postScript) && _codeWithPostScript != null)
            return _codeWithPostScript;

        // If we already have a post script stored, then remove it from the end
        if (_codePostScript != null)
            _codeBuilder.Remove(_codeBuilder.Length - _codePostScript.Length, _codePostScript.Length);

        _codeBuilder.Append(postScript);
        _codePostScript = postScript;
        _codeWithPostScript = _codeBuilder.ToString();
        return _codeWithPostScript;
    }
    public void SetSyncPosRotScale(bool syncPosRotScale)
    {
        SyncPosRotScale = syncPosRotScale;
    }
    public void SetWhoRuns(WhoRuns whoRuns)
    {
        WhoRunsScript = whoRuns;
    }
    public static void ClearReaderForDeserializeUpdate(DarkRiftReader reader)
    {
        // BundleID
        DRCompat.ReadStringSmallerThan255(reader);
        // Bundle Index
        reader.ReadUInt16();
        // Sync Pos Rot Scale
        reader.ReadBoolean();
        // Who runs the simulation
        reader.ReadByte();

        // Name
        DRCompat.ReadStringSmallerThan255(reader);

        // Code
        DRCompat.ReadLargeString(reader, null);
    }
    public void Deserialize(DeserializeEvent e)
    {
        // ID
        ushort id = e.Reader.ReadUInt16();
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;

        DeserializeUpdate(e.Reader);
    }
    public void DeserializeUpdate(DarkRiftReader reader)
    {
        // BundleID
        BundleID = DRCompat.ReadStringSmallerThan255(reader);
        // Bundle Index
        BundleIndex = reader.ReadUInt16();
        // Sync Pos Rot Scale
        SyncPosRotScale = reader.ReadBoolean();
        // Who runs the simulation
        WhoRunsScript = (WhoRuns)reader.ReadByte();

        // Name
        Name = DRCompat.ReadStringSmallerThan255(reader);
        // Code
        DRCompat.ReadLargeString(reader, _codeBuilder);
        _codePostScript = null;
        _codeWithPostScript = null;
        _codeWithoutPostScript = null;
    }
    public void Serialize(SerializeEvent e)
    {
        // ID
        e.Writer.Write(GetID());
        // BundleID
        DRCompat.WriteStringSmallerThen255(e.Writer, BundleID);
        // Bundle Index
        e.Writer.Write(BundleIndex);
        // Sync Pos Rot Scale
        e.Writer.Write(SyncPosRotScale);
        // Who runs the simulation
        e.Writer.Write((byte)WhoRunsScript);

        //TODO it would be a nice, small optimization to only serialize the title or code, depending on what changed
        // _name
        DRCompat.WriteStringSmallerThen255(e.Writer, Name);
        // Code
        int codeLen = _codePostScript == null ? _codeBuilder.Length : (_codeBuilder.Length - _codePostScript.Length);
        DRCompat.WriteLargeString(e.Writer, _codeBuilder, codeLen);
    }
}
