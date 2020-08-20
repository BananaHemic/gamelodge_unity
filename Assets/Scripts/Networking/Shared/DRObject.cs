using DarkRift;
using System.Collections;
using System.Collections.Generic;
using System.Text;
#if UNITY
using UnityEngine;
#endif

public class DRObject : IDarkRiftSerializable
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
    /// The bundle ID for the model attached, if there is one
    /// </summary>
    public string BundleID;
    /// <summary>
    /// The index of the model attached to this object, within the Bundle (-1 if none)
    /// </summary>
    public ushort ModelIndex;
    /// <summary>
    /// All behaviors attached to this object
    /// </summary>
    public List<SerializedBehavior> DRBehaviors;
    /// <summary>
    /// The name of the object
    /// Max length of 255
    /// </summary>
    public string Name;
    /// <summary>
    /// Is the object enabled
    /// </summary>
    public bool IsEnabled;
    /// <summary>
    /// The ID of the user who is currently grabbing this object
    /// It will be max_value if no one is grabbing it. This may return
    /// the ID of the user grabbing it, or the user we anticipate will be
    /// grabbing it
    /// </summary>
    public ushort GrabbedBy
    {
        get { return IsAnticipatingGrabbedBy ? _anticipatedGrabbedBy : _grabbedBy; }
        set { IsAnticipatingGrabbedBy = false; _grabbedBy = value; }
    }
    public bool IsAnticipatingGrabbedBy { get; private set; }
    private ushort _grabbedBy;
    private ushort _anticipatedGrabbedBy;
    public static readonly ushort NoneGrabbing = ushort.MaxValue;
    private static Vec3 _workingVec3 = new Vec3();
    private static Quat _workingQuat = new Quat();

    /// <summary>
    /// Local Pos/Rot/Scale/Vel/AngVel
    /// </summary>
    public Vec3 Position;
    public Quat Rotation;
    public Vec3 Scale;
    public Vec3 Velocity;
    public Vec3 AngularVelocity;
    public bool AtRest;

    /// <summary>
    /// The ID of the user who is in charge of simulating this object
    /// </summary>
    public ushort OwnerID
    {
        get { return IsAnticipatingOwner ? _anticipatedOwner : _ownerID; }
        set { IsAnticipatingOwner = false; _ownerID = value; }
    }
    /// <summary>
    /// The OwnerID that we know that the server is using
    /// </summary>
    public ushort ServerOwnerID { get { return _ownerID; } }
    public bool IsAnticipatingOwner { get; private set; }
    private ushort _ownerID;
    private ushort _anticipatedOwner;
    /// <summary>
    /// The known time of ownership for this object.
    /// It's set when someone releases an object or hits
    /// an object that that is being grabbed, and
    /// can be transferred from object to object when
    /// two ungrabbed objects collide (the owner with the larger ownership
    /// time takes control
    /// </summary>
    public uint OwnershipTime
    {
        get { return IsAnticipatingOwnershipTime ? _anticipatedOwnershipTime : _ownershipTime; }
        set { IsAnticipatingOwnershipTime = false; _ownershipTime = value; }
    }

    public bool IsAnticipatingOwnershipTime { get; private set; }
    private uint _anticipatedOwnershipTime;
    private uint _ownershipTime;

    public DRObject() { }
    public DRObject(ushort id, ushort ownerID, string bundleID, string name, ushort modelIdx, Vec3 position, Quat rotation, Vec3 scale, uint ownershipTime, List<SerializedBehavior> drBehaviors)
    {
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;
        OwnerID = ownerID;
        GrabbedBy = NoneGrabbing;
        _ownershipTime = ownershipTime;

        Name = name;
        BundleID = bundleID;
        ModelIndex = modelIdx;
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Velocity = new Vec3();
        AngularVelocity = new Vec3();
        AtRest = true;
        IsEnabled = true;
        DRBehaviors = drBehaviors != null ? drBehaviors : new List<SerializedBehavior>();
    }
    public ushort GetID()
    {
        if (ObjectID > 0)
            return ObjectID;
        return TemporaryID;
    }
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }
    public bool AddBehavior(SerializedBehavior serializedBehavior)
    {
        if (DRBehaviors == null)
            DRBehaviors = new List<SerializedBehavior>();
        else
        {
            // Make sure there are no existing behaviors with the same script
            for (int i = 0; i < DRBehaviors.Count; i++)
            {
                if (DRBehaviors[i].AreSameBehaviorScript(serializedBehavior))
                    return false;
            }
        }
        DRBehaviors.Add(serializedBehavior);
        return true;
    }
    public bool RemoveBehavior(bool isUserScript, ushort behaviorID)
    {
        for (int i = 0; i < DRBehaviors.Count; i++)
        {
            if (DRBehaviors[i].IsUserScript != isUserScript
                || DRBehaviors[i].BehaviorID != behaviorID)
                continue;
            DRBehaviors.RemoveAt(i);
            return true;
        }
        return false;
    }
    public SerializedBehavior GetBehavior(bool isUserScript, ushort behaviorID)
    {
        for (int i = 0; i < DRBehaviors.Count; i++)
        {
            SerializedBehavior serializedBehavior = DRBehaviors[i];
            if (serializedBehavior.IsUserScript == isUserScript
                && serializedBehavior.BehaviorID == behaviorID)
                return serializedBehavior;
        }
        return null;
    }
    public bool SetName(string name)
    {
        if (name == null)
            return false;
        if (name.Length > byte.MaxValue)
            return false;
        Name = name;
        return true;
    }
    public void DeserializePos(DarkRiftReader reader)
    {
        reader.ReadSerializableInto(ref Position);
    }
    public void DeserializeRot(DarkRiftReader reader)
    {
        reader.ReadSerializableInto(ref Rotation);
    }
    public void DeserializeScale(DarkRiftReader reader)
    {
        reader.ReadSerializableInto(ref Scale);
    }
    public void DeserializeVelocity(DarkRiftReader reader)
    {
        reader.ReadSerializableInto(ref Velocity);
    }
    public void DeserializeAngularVelocity(DarkRiftReader reader)
    {
        reader.ReadSerializableInto(ref AngularVelocity);
    }
    public static void DeserializeGrabPhysicsPosRotVel(DarkRiftReader reader, out byte grabbingBodyPart, 
        out Vector3 grabPos, out Quaternion grabRot, out Vector3 velWorld, out Vector3 angVelWorld)
    {
        // What body part is doing the grabbing
        grabbingBodyPart = reader.ReadByte();
        // The local grab position
        reader.ReadSerializableInto(ref _workingVec3);
        grabPos = _workingVec3.ToVector3();
        // The local grab rotation
        reader.ReadSerializableInto(ref _workingQuat);
        grabRot = _workingQuat.ToQuaternion();
        // The world velocity
        reader.ReadSerializableInto(ref _workingVec3);
        velWorld = _workingVec3.ToVector3();
        // The world angular velocity
        reader.ReadSerializableInto(ref _workingVec3);
        angVelWorld = _workingVec3.ToVector3();
    }
    public void SetAtRest(bool atRest)
    {
        AtRest = atRest;
    }
    public void SerializePos(DarkRiftWriter writer)
    {
        writer.Write(Position);
    }
    public void SerializeRot(DarkRiftWriter writer)
    {
        writer.Write(Rotation);
    }
    public void SerializeScale(DarkRiftWriter writer)
    {
        writer.Write(Scale);
    }
    public void SerializeVelocity(DarkRiftWriter writer)
    {
        writer.Write(Velocity);
    }
    public void SerializeAngularVelocity(DarkRiftWriter writer)
    {
        writer.Write(AngularVelocity);
    }
    public static void ClearReader_Position(DarkRiftReader reader)
    {
        reader.ReadSerializable<Vec3>();
    }
    public static void ClearReader_Rotation(DarkRiftReader reader)
    {
        reader.ReadSerializable<Quat>();
    }
    public static void ClearReader_Scale(DarkRiftReader reader)
    {
        reader.ReadSerializable<Vec3>();
    }
    public static void ClearReader_Velocity(DarkRiftReader reader)
    {
        reader.ReadSerializable<Vec3>();
    }
    public static void ClearReader_AngularVelocity(DarkRiftReader reader)
    {
        reader.ReadSerializable<Vec3>();
    }
    /// <summary>
    /// Clears the reader for a Grabbed PosRotVel update
    /// NB: this does NOT clear the grabber ID, or the object ID
    /// </summary>
    /// <param name="reader"></param>
    public static void ClearReaderGrabPhysicsPosRotVel(DarkRiftReader reader)
    {
        // What body part is doing the grabbing
        reader.ReadByte();
        // The local grab position
        reader.ReadSerializable<Vec3>();
        // The local grab rotation
        reader.ReadSerializable<Quat>();
        // The world velocity
        reader.ReadSerializable<Vec3>();
        // The world angular velocity
        reader.ReadSerializable<Vec3>();
    }
    /// <summary>
    /// Called by the client when they take ownership
    /// of an object, and have not yet received confirmation from the server
    /// </summary>
    /// <param name="anticipatedOwnershipTime"></param>
    public void SetAnticipatedOwnershipTime(uint anticipatedOwnershipTime)
    {
        IsAnticipatingOwnershipTime = true;
        _anticipatedOwnershipTime = anticipatedOwnershipTime;
    }
    public void SetAnticipatedOwner(ushort anticipatedOwnerID)
    {
        IsAnticipatingOwner = true;
        _anticipatedOwner = anticipatedOwnerID;
    }
    public void SetAnticipatedGrabbedBy(ushort anticipatedGrabbedBy)
    {
        IsAnticipatingGrabbedBy = true;
        _anticipatedGrabbedBy = anticipatedGrabbedBy;
    }
    /// <summary>
    /// Called when we were previously expecting to take
    /// ownership, but the server didn't reply so we forget it
    /// </summary>
    public void GiveUpAnticipatedOwnership()
    {
        IsAnticipatingOwner = false;
    }
    public static void ReadFlags(byte flags, out bool IsAtRest, out bool IsEnabled)
    {
        IsAtRest = (flags & 1) != 0;
        // NB the IsEnabled flag is flipped: 0 if on, 1 if off
        IsEnabled = (flags & (1 << 1)) == 0;
    }
    public static byte WriteFlags(bool IsAtRest, bool IsEnabled)
    {
        byte flags = IsAtRest ? (byte)1 : (byte)0;
        if (!IsEnabled) // NB the IsEnabled flag is flipped: 0 if on, 1 if off
            flags |= 1 << 1;
        return flags;
    }
    public void Deserialize(DeserializeEvent e)
    {
        // ID
        ushort id = e.Reader.ReadUInt16();
        ObjectID = id >= 256 ? id : (ushort)0;
        TemporaryID = id >= 256 ? (ushort)0 : id;

        // Owner
        OwnerID = e.Reader.ReadUInt16();
        // Grabber
        GrabbedBy = e.Reader.ReadUInt16();
        // Owner time
        OwnershipTime = e.Reader.ReadUInt32();
        // Name
        Name = DRCompat.ReadStringSmallerThan255(e.Reader);

        // Pos
        if (Position == null)
            Position = e.Reader.ReadSerializable<Vec3>();
        else
            e.Reader.ReadSerializableInto(ref Position);
        // Rot
        if (Rotation == null)
            Rotation = e.Reader.ReadSerializable<Quat>();
        else
            e.Reader.ReadSerializableInto(ref Rotation);
        // Scale
        if (Scale == null)
            Scale = e.Reader.ReadSerializable<Vec3>();
        else
            e.Reader.ReadSerializableInto(ref Scale);

        // Flags
        byte flags = e.Reader.ReadByte();
        ReadFlags(flags, out AtRest, out IsEnabled);
        // If we're not at rest, then we'll also want to read
        // the velocities
        if (!AtRest)
        {
            if (Velocity == null)
                Velocity = e.Reader.ReadSerializable<Vec3>();
            else
                e.Reader.ReadSerializableInto(ref Velocity);
            if (AngularVelocity == null)
                AngularVelocity = e.Reader.ReadSerializable<Vec3>();
            else
                e.Reader.ReadSerializableInto(ref AngularVelocity);
        }
        else
        {
            Velocity = new Vec3();
            AngularVelocity = new Vec3();
        }

        // BundleID
        BundleID = DRCompat.ReadStringSmallerThan255(e.Reader);

        // ModelIndex
        ModelIndex = e.Reader.ReadUInt16();
        // Num Behaviors
        byte numBehaviors = e.Reader.ReadByte();
        // Behaviors
        if (DRBehaviors == null)
            DRBehaviors = new List<SerializedBehavior>(numBehaviors);
        DRBehaviors.Clear();
        for (int i = 0; i < numBehaviors; i++)
        {
            var newBehavior = e.Reader.ReadSerializable<SerializedBehavior>();
            DRBehaviors.Add(newBehavior);
        }
    }
    public void Serialize(SerializeEvent e)
    {
        // ID
        e.Writer.Write(GetID());

        // Owner
        e.Writer.Write(OwnerID);
        // Grabber
        e.Writer.Write(GrabbedBy);
        // Owner time
        e.Writer.Write(_ownershipTime);
        // Name
        DRCompat.WriteStringSmallerThen255(e.Writer, Name);
        // Pos
        e.Writer.Write(Position);
        // Rot
        e.Writer.Write(Rotation);
        // Scale
        e.Writer.Write(Scale);

        // Flags
        byte flags = WriteFlags(AtRest, IsEnabled);
        e.Writer.Write(flags);
        // At Rest
        // if we're not at rest, we also need to send in
        // the velocities
        if (!AtRest)
        {
            e.Writer.Write(Velocity);
            e.Writer.Write(AngularVelocity);
        }

        // BundleID
        //e.Writer.Write(BundleID, Encoding.ASCII);
        DRCompat.WriteStringSmallerThen255(e.Writer, BundleID);
        // ModelIndex
        e.Writer.Write(ModelIndex);
        // Num Behaviors
        byte numBehaviors = (byte)DRBehaviors.Count;
        e.Writer.Write(numBehaviors);
        // Behaviors
        for (int i = 0; i < numBehaviors; i++)
            e.Writer.Write(DRBehaviors[i]);
    }
}
