using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DarkRift;

public class DRGameState : IDarkRiftSerializable
{
    // The Application Version used to create this game state
    // This is used so that we can properly deserialize game states from
    // previous versions
    public int Version { get; private set; }

    public float Width = Default_BoardWidth;
    public float Height = Default_BoardHeight;
    public bool BoardVisible = Default_BoardVisibility;

    const float Default_BoardWidth = 75.5f;
    const float Default_BoardHeight = 45.4f;
    const bool Default_BoardVisibility = true;
    // The version of the application. Every time we make a breaking change
    // to serialization this will increase by one
    public static readonly int ApplicationVersion = 3;
    // Version changes
    // 1 | Added Board Width/Height & Visibility
    // 2 | Added ServerTime to start marker
    // 3 | Added display name to DRUSer

    private readonly Dictionary<ushort, DRObject> _objects = new Dictionary<ushort, DRObject>();
    private readonly Dictionary<ushort, DRMaterial> _materials = new Dictionary<ushort, DRMaterial>();
    private readonly Dictionary<ushort, DRUserScript> _userScripts = new Dictionary<ushort, DRUserScript>();
    private readonly List<DRUserScript> _userScriptList = new List<DRUserScript>();

    public DRGameState()
    {
    }
    public Dictionary<ushort, DRObject> GetAllObjects()
    {
        return _objects;
    }
    public Dictionary<ushort, DRMaterial> GetAllMaterials()
    {
        return _materials;
    }
    public List<DRUserScript> GetAllUserScripts()
    {
        return _userScriptList;
    }
    public void AddObject(ushort id, DRObject dRObject)
    {
        _objects.Add(id, dRObject);
    }
    public bool RemoveObject(ushort id)
    {
        return _objects.Remove(id);
    }
    public bool TryGetObject(ushort id, out DRObject dRObject)
    {
        return _objects.TryGetValue(id, out dRObject);
    }
    public bool TryGetMaterial(ushort id, out DRMaterial drMaterial)
    {
        return _materials.TryGetValue(id, out drMaterial);
    }
    public bool TryGetUserScript(ushort id, out DRUserScript drUserScript)
    {
        return _userScripts.TryGetValue(id, out drUserScript);
    }
    public bool ContainsObjectKey(ushort key)
    {
        return _objects.ContainsKey(key);
    }
    public void AddMaterial(ushort id, DRMaterial drMat)
    {
        _materials.Add(id, drMat);
    }
    public void AddUserScript(ushort id, DRUserScript drUserScript)
    {
        _userScripts.Add(id, drUserScript);
        _userScriptList.Add(drUserScript);
    }
    public bool ContainsMaterialKey(ushort key)
    {
        return _materials.ContainsKey(key);
    }
    public bool ContainsUserScriptKey(ushort key)
    {
        return _userScripts.ContainsKey(key);
    }
    public void SetBoardSize(float width, float height)
    {
        Width = width;
        Height = height;
    }
    public void SetBoardVisibility(bool visible)
    {
        BoardVisible = visible;
    }
    /// <summary>
    /// Returns if there's already a material stored that has the
    /// same BundleID and Material Index. This is used to ensure that
    /// we don't get two drMaterials that are the same
    /// </summary>
    /// <param name="newMat"></param>
    /// <param name="existingIndex"></param>
    /// <returns></returns>
    public bool HasMaterialWithSameBundleAndIndex(DRMaterial newMat, out ushort existingIndex)
    {
        existingIndex = ushort.MaxValue;
        if (string.IsNullOrEmpty(newMat.BundleID))
            return false;

        // TODO it might make sense to keep another data structure to speed this up
        foreach (var kvp in _materials)
        {
            if (kvp.Value.BundleID == newMat.BundleID
                && kvp.Value.MaterialIndex == newMat.MaterialIndex)
            {
                existingIndex = kvp.Key;
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Returns if there's already a user script stored that has the
    /// same BundleID and Index. This is used to ensure that
    /// we don't get two drUserScripts that are the same
    /// </summary>
    /// <param name="user script"></param>
    /// <param name="existingIndex"></param>
    /// <returns></returns>
    public bool HasUserScriptWithSameBundleAndIndex(DRUserScript userScript, out ushort existingIndex)
    {
        existingIndex = ushort.MaxValue;
        if (string.IsNullOrEmpty(userScript.BundleID))
            return false;

        for(int i = 0; i < _userScriptList.Count;i++)
        {
            DRUserScript script = _userScriptList[i];
            if (script.BundleID == userScript.BundleID
                && script.BundleIndex == userScript.BundleIndex)
            {
                existingIndex = script.GetID();
                return true;
            }
        }
        return false;
    }
    public void SetAllObjectsToOwner(ushort ownerID)
    {
        foreach(var obj in _objects)
            obj.Value.OwnerID = ownerID;
    }
    public void Deserialize(DeserializeEvent e)
    {
        Version = e.Reader.DecodeInt32();
        int numObjects = e.Reader.ReadInt32();
        for (int i = 0; i < numObjects; i++)
        {
            DRObject dRObject = e.Reader.ReadSerializable<DRObject>();
            _objects.Add(dRObject.GetID(), dRObject);
        }
        int numMaterials = e.Reader.ReadInt32();
        for (int i = 0; i < numMaterials; i++)
        {
            DRMaterial drMat = e.Reader.ReadSerializable<DRMaterial>();
            _materials.Add(drMat.GetID(), drMat);
        }
        int numUserScripts = e.Reader.ReadInt32();
        for (int i = 0; i < numUserScripts; i++)
        {
            DRUserScript drUserScript = e.Reader.ReadSerializable<DRUserScript>();
            _userScripts.Add(drUserScript.GetID(), drUserScript);
            _userScriptList.Add(drUserScript);
        }

        if (Version < 1)
            return;

        Width = e.Reader.ReadSingle();
        Height = e.Reader.ReadSingle();
        BoardVisible = e.Reader.ReadBoolean();
    }

    public void Serialize(SerializeEvent e)
    {
        e.Writer.EncodeInt32(ApplicationVersion);
        // Objects
        e.Writer.Write(_objects.Count);
        foreach (var kvp in _objects)
            kvp.Value.Serialize(e);
        // Materials
        e.Writer.Write(_materials.Count);
        foreach (var kvp in _materials)
            kvp.Value.Serialize(e);
        // User Scripts
        e.Writer.Write(_userScriptList.Count);
        for (int i = 0; i < _userScriptList.Count; i++)
            e.Writer.Write(_userScriptList[i]);

        e.Writer.Write(Width);
        e.Writer.Write(Height);
        e.Writer.Write(BoardVisible);
    }

    public void ClearAll()
    {
        _objects.Clear();
        _materials.Clear();
        _userScripts.Clear();
        _userScriptList.Clear();
    }
}
