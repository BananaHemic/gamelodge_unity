using System.Collections;
using System.Collections.Generic;

public class SerializedBundleItemReference
{
    public string VariableName { get; private set; }
    public string BundleID { get; private set; }
    public ushort BundleIndex { get; private set; }

    private byte[] _serialized;
    private bool _isSerializedDirty = true;

    public SerializedBundleItemReference(string name)
    {
        VariableName = name;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(string bundleID, ushort bundleIndex)
    {
        //Debug.Log("Setting reference to: " + bundleID + " #" + bundleIndex);
        BundleID = bundleID;
        BundleIndex = bundleIndex;
        _isSerializedDirty = true;
    }
    public void UpdateFrom(byte[] serialized)
    {
        int offset = 0;
        byte bundleIDLen = serialized[offset++];
        BundleID = serialized.ReadASCII(ref offset, bundleIDLen);
        if (string.IsNullOrEmpty(BundleID))
            BundleIndex = ushort.MaxValue;
        else
            BundleIndex = serialized.ReadUshort(ref offset);
        _isSerializedDirty = true;
        //Debug.Log("Deserialize set reference to: " + BundleID + " #" + BundleIndex);
    }
    // TODO remove allocations here
    public byte[] GetSerialized()
    {
        if (_isSerializedDirty)
        {
            if (string.IsNullOrEmpty(BundleID))
            {
                _serialized = new byte[1];
                _serialized[0] = 0;
                _isSerializedDirty = false;
                return _serialized;
            }
            int len = 1
                + BundleID.Length
                + sizeof(ushort);
            if (_serialized == null
                || _serialized.Length != len)
                _serialized = new byte[len];

            int offset = 0;
            _serialized[offset++] = (byte)BundleID.Length;
            _serialized.WriteASCII(BundleID, ref offset);
            _serialized.WriteUshort(BundleIndex, ref offset);
            _isSerializedDirty = false;
        }
        return _serialized;
    }
}
