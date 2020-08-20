using System;
using System.Collections;
using System.Collections.Generic;
using DarkRift;

/// <summary>
/// Contains all the data of a behavior, serialized
/// into a DarkRift friendly format.
/// It contains three main data structures:
/// ServerData: the data known to exist on the server
/// PendingAckData: the data set locally that has not yet been verified
/// DirtyLocalData: the data set locally that has not yet been sent to the server
///
/// We store behaviors as a KVP, so that there's a
/// simple way to only update one part of a behavior
/// 
/// On the client, this is managed and owned by a BaseBehavior
/// 
/// The serialization is
/// 1) header (byte)
///     Contains if the script is built-in, or a user script
/// 2) BehaviorID (ushort)
/// 3) Number of keys changed (var int)
/// 4) Key ID (var int)
/// 5) (If flags set in header) flags (byte)
/// 6) Value length (var int)
/// 7) Value (raw bytes)
///
/// The flags are for informing the server that it may need to
/// pre-process the value. For example, it will need to ensure that
/// all objectIDs are using the non-temporary version
/// </summary>
public class SerializedBehavior : IDarkRiftSerializable
{
    public delegate void HandleFlags(int key, int flags, byte[] data, object callbackObj);
    /// <summary>
    /// This implies that the data _begins_ with a
    /// SceneObject. The server should make sure it's not
    /// a temp ID
    /// </summary>
    public const int SceneObjectFlag = 1;

    private struct Datum
    {
        public byte[] Data;
        /// <summary>
        /// Flags used to notify the server that some additional processing is needed for the following
        /// value. E.g. The datum is a SceneObject and the server needs to correct the ID
        /// Encoded as a varint, NB as a result the last bit cannot be used as a flag
        /// </summary>
        public int Flags;
    }

    /// <summary>
    /// Is this behavior for a UserScript?
    /// If not, it's a premade C# script
    /// </summary>
    public bool IsUserScript;
    public ushort BehaviorID { get; private set; }

    /// <summary>
    /// The serialized data, known to exist on the
    /// server. This is kept in sync with the server
    /// </summary>
    private readonly Dictionary<int, Datum> _serverData = new Dictionary<int, Datum>();
    /// <summary>
    /// The serialized data, for the locally set
    /// parameters, that are different from what
    /// the server has or is going to have
    /// </summary>
    private readonly Dictionary<int, Datum> _dirtyLocalData = new Dictionary<int, Datum>();
    /// <summary>
    /// The serialized data, which we've sent
    /// to the server, but have not yet received an
    /// update for
    /// </summary>
    private readonly Dictionary<int, Datum> _pendingAckData = new Dictionary<int, Datum>();
    /// <summary>
    /// Temporary Queue, here to reduce allocations
    /// </summary>
    private readonly Queue<KeyValuePair<int, Datum>> _workingQueue = new Queue<KeyValuePair<int, Datum>>();

    public int NumDirtyKeys { get { return _dirtyLocalData.Count; } }
    public int NumServerKeys { get { return _serverData.Count; } }
    public bool AreAnyFlagsSet { get; private set; }

    public SerializedBehavior()
    {
        // This constructor is only used when we're deserializing
    }
    public SerializedBehavior(bool isBehaviorNetworked, ushort behaviorID)
    {
        IsUserScript = isBehaviorNetworked;
        BehaviorID = behaviorID;
    }
    /// <summary>
    /// If this behavior and another behavior are using the same script
    /// </summary>
    /// <param name="otherBehavior"></param>
    public bool AreSameBehaviorScript(SerializedBehavior otherBehavior)
    {
        return otherBehavior.IsUserScript == IsUserScript
            && otherBehavior.BehaviorID == BehaviorID;
    }

    /// <summary>
    /// Whether we need to send out a parameter
    /// update the the server
    /// </summary>
    /// <returns></returns>
    public bool NeedsUpdateToServer()
    {
        return _dirtyLocalData.Count > 0;
    }
    /// <summary>
    /// Set serialized data for a certain,
    /// as a result of a local change on the client
    /// </summary>
    /// <param name="key"></param>
    /// <param name="newData">Serialized data, you can re-use this array</param>
    /// <returns>Whether the data was dirty</returns>
    //TODO overloads for int/float/bool to prevent GC allocs
    public bool LocallySetData(int key, byte[] newData, int flags=0)
    {
        Datum datum;
        bool isDirty;
        if (_pendingAckData.TryGetValue(key, out datum))
        {
            // If we're currently awaiting an update from
            // the server for this param, compare the data to there
            isDirty = !AreArraysEqual(datum.Data, newData) || datum.Flags != flags;
        }
        else if (_serverData.TryGetValue(key, out datum))
        {
            // Otherwise, compare to the data that's currently present
            // in the server
            isDirty = !AreArraysEqual(datum.Data, newData) || datum.Flags != flags;
        }
        else
        {
            // If the data key is not in the server, and it not pending
            // ack, then it's certainly dirty
            isDirty = true;
        }

        if (!isDirty)
            return false;

        if (flags != 0)
            AreAnyFlagsSet = true;

        // We copy newData if it's different, this way the
        // caller can always use the same array when calling here
        // TODO this should really be without alloc
        byte[] dirtyData = new byte[newData.Length];
        Buffer.BlockCopy(newData, 0, dirtyData, 0, newData.Length);
        // Save this value as dirty data
        _dirtyLocalData[key] = new Datum
        {
            Data = dirtyData,
            Flags = flags
        };
        return true;
    }
    /// <summary>
    /// Get the data for a property.
    /// First uses the dirty local data,
    /// then the un-acked data, then
    /// the server data
    /// DO NOT REUSE THIS ARRAY
    /// it needs to be kept here, to
    /// make sure the dirty calculation works
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryReadProperty(int key, out byte[] value, out int flags)
    {
        Datum datum;
        if (_dirtyLocalData.TryGetValue(key, out datum))
        {
            value = datum.Data;
            flags = datum.Flags;
            return true;
        }
        if (_pendingAckData.TryGetValue(key, out datum))
        {
            value = datum.Data;
            flags = datum.Flags;
            return true;
        }
        if (_serverData.TryGetValue(key, out datum))
        {
            value = datum.Data;
            flags = datum.Flags;
            return true;
        }
        value = null;
        flags = 0;
        return false;
    }
    /// <summary>
    /// When we have sent an update to
    /// the server.
    /// </summary>
    public void OnUpdateSentToServer()
    {
        // We move all data that was dirty, into pending Ack
        foreach (var kvp in _dirtyLocalData)
            _pendingAckData[kvp.Key] = kvp.Value;
        _dirtyLocalData.Clear();
    }
    /// <summary>
    /// Writes out an update for this behavior
    /// If unreliable update, we send out only the recently dirty data
    /// If reliable update, we send out everything that hasn't yet been
    ///     acknowledged by the server
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="sendMode"></param>
    public void WriteUpdateToServer(DarkRiftWriter writer, bool hasFlags, SendMode sendMode)
    {
        if (sendMode == SendMode.Unreliable)
        {
            // Num keys
            writer.EncodeInt32(_dirtyLocalData.Count);
            foreach (var dirty in _dirtyLocalData)
            {
                // Param that changed
                writer.EncodeInt32(dirty.Key);
                // The flags for this key
                if (hasFlags)
                    writer.EncodeInt32(dirty.Value.Flags);
                else if (dirty.Value.Flags != 0)
                    DRCompat.LogError("Unable to send flags! " + dirty.Value.Flags);
                // Value Length
                byte[] data = dirty.Value.Data;
                writer.EncodeInt32(data.Length);
                // The value
                for (int j = 0; j < data.Length; j++)
                    writer.Write(data[j]);
            }
        }
        else
        {
            //TODO this is wrong. Other clients need to receive the ending behavior
            // update, so we need to send out updates for everything that wasn't acked
            // over TCP

            // for unreliable, send out everything that has not yet been
            // acknowledged by the server, both dirty and pending Ack
            // this may send out redundant info, but it's important that
            // everyone ends up having the same parameters for everything
            // Info in dirty local takes priority over the data in un-Acked
            _workingQueue.Clear();
            foreach (var localData in _dirtyLocalData)
                _workingQueue.Enqueue(localData);
            foreach (var pendingAck in _pendingAckData)
            {
                if (!_dirtyLocalData.ContainsKey(pendingAck.Key))
                    _workingQueue.Enqueue(pendingAck);
            }

            // Num keys
            writer.EncodeInt32(_workingQueue.Count);
            while(_workingQueue.Count > 0)
            {
                var toSend = _workingQueue.Dequeue();
                // Param that changed
                writer.EncodeInt32(toSend.Key);
                // The flags for this key
                if (hasFlags)
                    writer.EncodeInt32(toSend.Value.Flags);
                else if (toSend.Value.Flags != 0)
                    DRCompat.LogError("Unable to send flags! " + toSend.Value.Flags);
                // Value Length
                byte[] data = toSend.Value.Data;
                writer.EncodeInt32(data.Length);
                // The value
                for (int j = 0; j < data.Length; j++)
                    writer.Write(data[j]);
            }
        }
    }

    /// <summary>
    /// Integrates an update into our database.
    /// Optionally provide a writer to write
    /// everything that we read into
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="writer"></param>
    public void UpdateParamsFromUpdate(DarkRiftReader reader, bool hasFlags, DarkRiftWriter writer, HandleFlags handleFlag, object callbackObj=null)
    {
        // Num keys
        int numKeysChanged = reader.DecodeInt32();
        writer?.EncodeInt32(numKeysChanged);
        AreAnyFlagsSet = hasFlags;

        for (int i = 0; i < numKeysChanged; i++)
        {
            // Read the param that changed (var int)
            int keyChanged = reader.DecodeInt32();
            writer?.EncodeInt32(keyChanged);
            // Read the flags for this key (byte)
            int recvFlags;
            if (hasFlags)
                recvFlags = reader.DecodeInt32();
            else
                recvFlags = 0;
            // Read the value length
            int valLen = reader.DecodeInt32();
            // Read the value
            byte[] serverArray = new byte[valLen];
            for (int j = 0; j < valLen; j++)
                serverArray[j] = reader.ReadByte();

            // Allow the server to use the flags to manipulate the data
            if (recvFlags != 0 && handleFlag != null)
                handleFlag(keyChanged, recvFlags, serverArray, callbackObj);

            // Now that we have the flags+data, and we've possibly used the flags,
            // we can now write that into the writer
            if (writer != null)
            {
                if(hasFlags)
                    writer.EncodeInt32(recvFlags);
                writer.EncodeInt32(valLen);
                for (int j = 0; j < valLen; j++)
                    writer.Write(serverArray[j]);
            }

            Datum datum = new Datum
            {
                Data = serverArray,
                Flags = recvFlags
            };
            // Save the value internally
            _serverData[keyChanged] = datum;
            // Remove the data from pending ack, if it was indeed the data that we sent
            if (_pendingAckData.TryGetValue(keyChanged, out Datum pendingDatum))
            {
                if (AreArraysEqual(serverArray, pendingDatum.Data) && pendingDatum.Flags == recvFlags)
                {
                    // The server has acknowledged the data that we sent
                    _pendingAckData.Remove(keyChanged);
                }
            }
        }
    }
    public void HandleAllFlags(HandleFlags handleFlag, object callbackObj = null)
    {
        foreach (var kvp in _serverData)
        {
            if (kvp.Value.Flags == 0)
                continue;
            handleFlag(kvp.Key, kvp.Value.Flags, kvp.Value.Data, callbackObj);
        }
    }
    /// <summary>
    /// Called when we just sent a creation message for this behavior
    /// We can move all data from dirty into server
    /// </summary>
    public void OnServerCreation()
    {
        // Move data from dirty into server
        foreach (var kvp in _dirtyLocalData)
            _serverData.Add(kvp.Key, kvp.Value);
        _dirtyLocalData.Clear();
    }
    /// <summary>
    /// Clears the reader for the length from one SerializedBehavior update
    /// used when we received a behavior update for a SerializedBehavior that
    /// we don't have
    /// </summary>
    /// <param name="reader"></param>
    public static void ClearReaderForUpdate(DarkRiftReader reader, bool hasFlags)
    {
        // Num keys
        int numKeysChanged = reader.DecodeInt32();
        for (int i = 0; i < numKeysChanged; i++)
        {
            // Read the param that changed (var int)
            reader.DecodeInt32();
            // Read the flags for this data
            if(hasFlags)
                reader.DecodeInt32();
            // Read the value length
            int valLen = reader.DecodeInt32();
            // Read the value
            for (int j = 0; j < valLen; j++)
                reader.ReadByte();
        }
    }
    public static void ParseHeader(byte header, out bool isUserScript, out bool hasFlags)
    {
        isUserScript = (header & 1) != 0;
        hasFlags = (header & (1 << 1)) != 0;
    }
    public static byte MakeHeader(bool isUserScript, bool hasFlags)
    {
        byte header = isUserScript ? (byte)1 : (byte)0;
        if (hasFlags)
            header |= (1 << 1);
        return header;
    }
    public void Deserialize(DeserializeEvent e)
    {
        // Is Behavior Networked
        byte header = e.Reader.ReadByte();
        ParseHeader(header, out IsUserScript, out bool hasFlags);
        AreAnyFlagsSet = hasFlags;
        // Behavior ID
        BehaviorID = e.Reader.ReadUInt16();
        // Num Key/Values
        int numKVP = e.Reader.DecodeInt32();
        _serverData.Clear();

        for (int i = 0; i < numKVP; i++)
        {
            // Key ID
            int keyID = e.Reader.DecodeInt32();
            // Flags
            int flag = hasFlags ? e.Reader.DecodeInt32() : 0;
            // Value Len
            int valueLen = e.Reader.DecodeInt32();
            // Value
            byte[] serverData = new byte[valueLen];
            for (int j = 0; j < valueLen; j++)
                serverData[j] = e.Reader.ReadByte();

            _serverData.Add(keyID, new Datum
            {
                Data = serverData,
                Flags = flag
            });
        }
    }
    public void Serialize(SerializeEvent e)
    {
        // Is Behavior Networked
        // And do we have keys with flags
        bool hasFlags = AreAnyFlagsSet;
        byte header = MakeHeader(IsUserScript, hasFlags);
        e.Writer.Write(header);
        // Behavior ID
        e.Writer.Write(BehaviorID);

        // When the client makes a SerializedBehavior
        // we then want the server to take in our local
        // dirty data, instead of serverData (which would
        // be empty)
        Dictionary<int, Datum> data = _serverData.Count == 0 && _dirtyLocalData.Count > 0
            ? _dirtyLocalData
            : _serverData;

        // Num Key/Values
        e.Writer.EncodeInt32(data.Count);

        foreach (var kvp in data)
        {
            // Key ID
            e.Writer.EncodeInt32(kvp.Key);
            // Flags
            if (hasFlags)
                e.Writer.EncodeInt32(kvp.Value.Flags);
            // Value Len
            byte[] ray = kvp.Value.Data;
            int valueLen = ray.Length;
            e.Writer.EncodeInt32(valueLen);
            // Value
            for (int i = 0; i < valueLen; i++)
                e.Writer.Write(ray[i]);
        }
    }
    private static bool AreArraysEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }
    /// <summary>
    /// Create a new serialized behavior with the same data
    /// </summary>
    /// <returns></returns>
    public SerializedBehavior Duplicate()
    {
        SerializedBehavior serializedBehavior = new SerializedBehavior
        {
            IsUserScript = IsUserScript,
            BehaviorID = BehaviorID
        };
        foreach (var kvp in _serverData)
        {
            serializedBehavior._serverData.Add(kvp.Key, 
                new Datum{
                    Data = (byte[])kvp.Value.Data.Clone(),
                    Flags = kvp.Value.Flags
                });
            if (kvp.Value.Flags != 0)
                serializedBehavior.AreAnyFlagsSet = true;
        }
        return serializedBehavior;
    }
}
