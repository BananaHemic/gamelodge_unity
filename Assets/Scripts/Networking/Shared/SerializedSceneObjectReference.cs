using System.Collections;
using System.Collections.Generic;

public class SerializedSceneObjectReference
{
    /// <summary>
    /// The name of the variable that needs to
    /// be populated with this SceneObject
    /// </summary>
    public string VariableName { get; private set; }
    public SceneObject SceneObjectReference {
        get
        {
            if (_loadedSceneObject != null)
                return _loadedSceneObject;
            if (_sceneObjectID == ushort.MaxValue)
                return null;
            // Do a load
            if(!SceneObjectManager.Instance.TryGetSceneObjectByID(_sceneObjectID, out SceneObject sceneObject))
            {
                DRCompat.LogError("Failed to find object with ID #" + _sceneObjectID + " for serialized ref");
                return null;
            }
            _loadedSceneObject = sceneObject;
            return _loadedSceneObject;
        }
    }

    /// <summary>
    /// We get the ID from the network, but the object
    /// may not have fully loaded. So we keep the ID
    /// and get the object when needed
    /// </summary>
    private ushort _sceneObjectID = ushort.MaxValue;
    private SceneObject _loadedSceneObject;

    private byte[] _serialized;
    private bool _isSerializedDirty = true;
    public SerializedSceneObjectReference(string name)
    {
        VariableName = name;
        _sceneObjectID = ushort.MaxValue;
    }
    public void UpdateFrom(SceneObject sceneObject)
    {
        _loadedSceneObject = sceneObject;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(byte[] serialized)
    {
        int offset = 0;
        _sceneObjectID = serialized.ReadUshort(ref offset);
        _isSerializedDirty = true;
    }
    public byte[] GetSerialized()
    {
        if (_isSerializedDirty)
        {
            if (_serialized == null)
                _serialized = new byte[sizeof(ushort)];
            int offset = 0;
            _serialized.WriteUshort(SceneObjectReference != null ? SceneObjectReference.GetID() : ushort.MaxValue, ref offset);
            _isSerializedDirty = false;
        }
        return _serialized;
    }
}
