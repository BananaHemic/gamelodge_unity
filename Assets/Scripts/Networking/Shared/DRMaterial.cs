using System.Collections;
using System.Collections.Generic;
using DarkRift;
#if UNITY
using UnityEngine;
#endif

public class DRMaterial : IDarkRiftSerializable
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
    /// The bundle that this material is from.
    /// Will be null if it was created at runtime
    /// </summary>
    public string BundleID;
    /// <summary>
    /// The material index for this material, within
    /// the bundle. Will be 0 for user created
    /// materials
    /// </summary>
    public ushort MaterialIndex;
    public string Name;

    const int MaxNameLength = byte.MaxValue;

    // All color parameters
    // The propertyIndex -> Current Color
    private readonly Dictionary<int, Col3> _colorProps = new Dictionary<int, Col3>();

    public DRMaterial() { }// Needed for DR
    public DRMaterial(ushort id, string bundleID, ushort materialIndex, string name)
    {
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;
        BundleID = bundleID;
        MaterialIndex = materialIndex;

        if (name.Length > MaxNameLength)
            name = name.Substring(0, MaxNameLength);
        Name = name;
    }
    public ushort GetID()
    {
        if (ObjectID > 0)
            return ObjectID;
        return TemporaryID;
    }
    // Returns if this server copy has changes beyond
    // the assetbundle version
    public bool IsDifferentThanDefault()
    {
        return _colorProps.Count > 0;
    }
    public Dictionary<int, Col3> GetAllColorProps()
    {
        return _colorProps;
    }
    /// <summary>
    /// Sets the color property. This does not store a reference
    /// to color, you are free to change color after this call
    /// </summary>
    /// <param name="propertyIndex"></param>
    /// <param name="color"></param>
    public void SetColorProperty(int propertyIndex, ref Col3 color)
    {
        // Set the color by updating the existing Col3, if possible
        // this is just to reduce allocations
        if(_colorProps.TryGetValue(propertyIndex, out Col3 existingCol3))
            existingCol3.UpdateFrom(color);
        else
            _colorProps.Add(propertyIndex, new Col3(color));
    }
#if UNITY
    public void SetColorProperty(int propertyIndex, Color color)
    {
        // Set the color by updating the existing Col3, if possible
        // this is just to reduce allocations
        if(_colorProps.TryGetValue(propertyIndex, out Col3 existingCol3))
            existingCol3.UpdateFrom(color);
        else
            _colorProps.Add(propertyIndex, new Col3(color));
    }
#endif
    public void Deserialize(DeserializeEvent e)
    {
        // ID
        ushort id = e.Reader.ReadUInt16();
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;
        // BundleID
        BundleID = DRCompat.ReadStringSmallerThan255(e.Reader);
        // Material Index
        MaterialIndex = e.Reader.ReadUInt16();

        // Name
        Name = DRCompat.ReadStringSmallerThan255(e.Reader);
        // Colors
        int numColors = e.Reader.DecodeInt32();
        for (int i = 0; i < numColors; i++)
        {
            int propertyIndex = e.Reader.DecodeInt32();
            Col3 color = e.Reader.ReadSerializable<Col3>();
            _colorProps.Add(propertyIndex, color);
        }
    }

    public void Serialize(SerializeEvent e)
    {
        // ID
        e.Writer.Write(GetID());
        // BundleID
        DRCompat.WriteStringSmallerThen255(e.Writer, BundleID);
        // Material Index
        e.Writer.Write(MaterialIndex);
        // Name
        DRCompat.WriteStringSmallerThen255(e.Writer, Name);
        // Colors
        e.Writer.EncodeInt32(_colorProps.Count);
        foreach (var kvp in _colorProps)
        {
            e.Writer.EncodeInt32(kvp.Key);
            e.Writer.Write(kvp.Value);
        }
    }
}
