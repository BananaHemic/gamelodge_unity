using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Serializes EITHER a runtime reference to a sceneobject,
/// OR a bundle item
/// </summary>
public class SerializedSceneObjectORBundleItemReference
{
    public enum SerializeMode
    {
        SceneObject,
        BundleItem
    };
    public string VariableName { get; private set; }
    public string BundleID { get; private set; }
    public ushort BundleIndex { get; private set; }
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
                Debug.LogError("Failed to find object with ID #" + _sceneObjectID + " for serialized ref");
                return null;
            }
            _loadedSceneObject = sceneObject;
            return _loadedSceneObject;
        }
    }

    public SerializeMode CurrentMode { get; private set; }
    private byte[] _serialized;
    private bool _isSerializedDirty = true;
    /// <summary>
    /// We get the ID from the network, but the object
    /// may not have fully loaded. So we keep the ID
    /// and get the object when needed
    /// </summary>
    private ushort _sceneObjectID = ushort.MaxValue;
    private SceneObject _loadedSceneObject;

    public SerializedSceneObjectORBundleItemReference(string name)
    {
        VariableName = name;
        CurrentMode = SerializeMode.BundleItem;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(SceneObject sceneObject)
    {
        //Debug.Log("Setting reference to: " + bundleID + " #" + bundleIndex);
        CurrentMode = SerializeMode.SceneObject;
        _loadedSceneObject = sceneObject;
        _sceneObjectID = ushort.MaxValue;
        BundleID = null;
        BundleIndex = ushort.MaxValue;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(string bundleID, ushort bundleIndex)
    {
        //Debug.Log("Setting reference to: " + bundleID + " #" + bundleIndex);
        CurrentMode = SerializeMode.BundleItem;
        BundleID = bundleID;
        BundleIndex = bundleIndex;
        _sceneObjectID = ushort.MaxValue;
        _loadedSceneObject = null;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(byte[] serialized, int flags)
    {
        int offset = 0;
        if((flags & SerializedBehavior.SceneObjectFlag) != 0)
        {
            CurrentMode = SerializeMode.SceneObject;
            _sceneObjectID = serialized.ReadUshort(ref offset);
            BundleID = null;
            BundleIndex = ushort.MaxValue;
        }
        else
        {
            CurrentMode = SerializeMode.BundleItem;
            _sceneObjectID = ushort.MaxValue;
            _loadedSceneObject = null;
            byte bundleIDLen = serialized[offset++];
            BundleID = serialized.ReadASCII(ref offset, bundleIDLen);
            if (bundleIDLen == 0)
                BundleIndex = ushort.MaxValue;
            else
                BundleIndex = serialized.ReadUshort(ref offset);
        }
        _isSerializedDirty = true;
        //Debug.Log("Deserialize set reference to: " + BundleID + " #" + BundleIndex);
    }
    // TODO remove allocations here
    public byte[] GetSerialized(out int setFlags)
    {
        setFlags = CurrentMode == SerializeMode.BundleItem
            ? 0 : SerializedBehavior.SceneObjectFlag;

        if (_isSerializedDirty)
        {
            int offset = 0;
            if(CurrentMode == SerializeMode.BundleItem)
            {
                if (string.IsNullOrEmpty(BundleID))
                {
                    _serialized = new byte[1];
                    _serialized[0] = 0;
                    _isSerializedDirty = false;
                    return _serialized;
                }
                // Ensure the array size is correct
                int len = 1
                    + BundleID.Length
                    + sizeof(ushort);
                if (_serialized == null
                    || _serialized.Length != len)
                    _serialized = new byte[len];
                // Write the data
                _serialized[offset++] = (byte)BundleID.Length;
                _serialized.WriteASCII(BundleID, ref offset);
                _serialized.WriteUshort(BundleIndex, ref offset);
            }
            else
            {
                // Ensure the array size is correct
                if (_serialized == null
                    || _serialized.Length != sizeof(ushort))
                    _serialized = new byte[sizeof(ushort)];
                // Write the data
                _serialized.WriteUshort(SceneObjectReference.GetID(), ref offset);
            }
            _isSerializedDirty = false;
        }
        return _serialized;
    }
}
