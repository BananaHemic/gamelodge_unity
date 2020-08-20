using DarkRift;
using System;
using System.Collections.Generic;
using System.Text;
#if UNITY
using UnityEngine;
#endif

public class DRUser : IDarkRiftSerializable
{
    public enum GrabbingBodyPart
    {
        None,
        Head,
        LeftHand,
        RightHand,
        LeftHandPrimary_RightHandSecondary,
        RightHandPrimary_LeftHandSecondary,
        Body,
    }
    public struct GrabbedObject
    {
        public ushort ObjectID;
        public GrabbingBodyPart BodyPart;
        public Vector3 RelativePos;
        public Quaternion RelativeRot;
    }
    public enum UserType
    {
        Real, // Normal user controlled by a person
        Recorded, // User who is being emulated via the playback of a recording
    }

    public string DisplayName { get; private set; }
    // We do separate vars here so that we can use these as refs
    public Vec3 Position { get { return _position; } }
    public Quat Rotation { get { return _rotation; } }
    public bool IsSprintDown { get; set; }
    public bool IsGrounded { get; set; }
    public Vec3 BaseVelocity { get { return _velocity; } }
    public Vec2 InputMovement { get { return _inputMovement; } }
    public ushort StandingOnObject = ushort.MaxValue;
    //public int UpdateTimeTicks { get; set; }// When the fixed update occurred on the sender, measured in ticks
    public DRUserPose UserPose { get { return _userPose; } } // How the head / hands are oriented
    public DRUserBlends UserBlends { get { return _userBlends; } } // Non-phoneme blends on the avatar
    public UserType TypeOfUser { get; set; }
    /// <summary>
    /// The objects that are currently being grabbed by this user
    /// It will only contain the non-temporary IDs
    /// </summary>
    public List<GrabbedObject> GrabbedObjects { get; private set; }
    /// <summary>
    /// The ID of the object that this user is currently possessing
    /// or MaxValue if no object is being possessed
    /// </summary>
    public ushort PossessedObj = ushort.MaxValue;

    /// <summary>
    /// What time have we last received a ping from this client?
    /// This value is only used on the server, it is not serialized
    /// </summary>
    public long TimeOfLastRecvPing { get; private set; }

    // Internal state
    private Vec3 _position;
    private Quat _rotation;
    private Vec3 _velocity;
    private Vec2 _inputMovement;
    private DRUserPose _userPose;
    private DRUserBlends _userBlends;

    private static Vec3 _workingVec3 = new Vec3();
    private static Quat _workingQuat = new Quat();

    public ushort ID { get; set; }
    public bool IsInPlayMode;

    public DRUser() { }
    public DRUser(string displayName, Vec3 position, Quat rotation, ushort id, bool isInPlayMode)
    {
        DisplayName = displayName;
        ID = id;
        _position = position;
        _rotation = rotation;
        IsInPlayMode = isInPlayMode;
        _velocity = new Vec3(0, 0, 0);
        _inputMovement = new Vec2(0, 0);
        IsGrounded = false;
        IsSprintDown = false;
        _userPose = new DRUserPose();
        TypeOfUser = UserType.Real;
    }
    public void UserGrabObject(ushort objectID, GrabbingBodyPart bodyPart, Vector3 relPos, Quaternion relRot)
    {
        if (objectID < byte.MaxValue)
            DRCompat.LogError("Temporary IDs are not allowed in GrabbedObjects!");

        if (GrabbedObjects == null)
            GrabbedObjects = new List<GrabbedObject>(2);
        else
        {
            // If we already have this object marked as grabbed, remove that entry
            for (int i = 0; i < GrabbedObjects.Count; i++)
            {
                if (GrabbedObjects[i].ObjectID == objectID)
                {
                    DRExtensions.RemoveBySwap(GrabbedObjects, i);
                    break;
                }
            }
        }
        GrabbedObjects.Add(new GrabbedObject
        {
            ObjectID = objectID,
            BodyPart = bodyPart,
            RelativePos = relPos,
            RelativeRot = relRot,
        });
    }
    public void UserReleaseObject(ushort objectID)
    {
        if (GrabbedObjects == null)
        {
            DRCompat.LogError("Can't release #" + objectID + " user #" + ID + " has no objects grabbed");
            return;
        }

        for (int i = 0; i < GrabbedObjects.Count; i++)
        {
            if (GrabbedObjects[i].ObjectID == objectID)
            {
                DRExtensions.RemoveBySwap(GrabbedObjects, i);
                return;
            }
        }
        DRCompat.LogError("Can't release #" + objectID + " user #" + ID + " has no such object grabbed");
    }
    public void SetBlendShape(int idx, float val)
    {
        if (_userBlends == null)
            _userBlends = new DRUserBlends(idx, val);
        else
            _userBlends.SetBlend(idx, val);
    }
    public void IntegrateSpawnInfo(ushort objectID)
    {
        //Position.UpdateFrom(spawnInfo.SpawnLocation);
        //Rotation.UpdateFrom(spawnInfo.SpawnOrientation);
        //PlayAvatarBundleID = spawnInfo.AvatarBundleID;
        //PlayAvatarBundleIndex = spawnInfo.AvatarBundleIndex;
        IsInPlayMode = true;
        PossessedObj = objectID;
    }
    // Called only for the local client
    // because local can't get that info by parsing
    // out player movement
    public void LocalPlayerEnterBuildMode()
    {
        IsInPlayMode = false;
    }
    public void DeserializePlayerMovement_Build(DarkRiftReader reader)
    {
        IsInPlayMode = false;
        reader.ReadSerializableInto(ref _position);
        reader.ReadSerializableInto(ref _rotation);
        //TODO scale
        IsGrounded = false;
        IsSprintDown = false;
        StandingOnObject = ushort.MaxValue;
    }
    public void DeserializePlayerMovement_Play(DarkRiftReader reader, byte tag)
    {
        IsInPlayMode = true;
        reader.ReadSerializableInto(ref _position);
        reader.ReadSerializableInto(ref _rotation);
        //UpdateTimeTicks = reader.ReadInt32();
        if (tag == ServerTags.PlayerMovement_Play_Grounded)
        {
            StandingOnObject = ushort.MaxValue;
            reader.ReadSerializableInto(ref _inputMovement);
            IsSprintDown = reader.ReadBoolean();
            IsGrounded = true;
        } else if(tag == ServerTags.PlayerMovement_Play_Grounded_OnObject)
        {
            StandingOnObject = reader.ReadUInt16();
            reader.ReadSerializableInto(ref _inputMovement);
            IsSprintDown = reader.ReadBoolean();
            IsGrounded = true;
        }
        else if (tag == ServerTags.PlayerMovement_Play_NotGrounded)
        {
            StandingOnObject = ushort.MaxValue;
            reader.ReadSerializableInto(ref _inputMovement);
            reader.ReadSerializableInto(ref _velocity);
            IsSprintDown = false;
            IsGrounded = false;
        }
    }
    public void SerializePlayerMovement_Build(DarkRiftWriter writer, out byte tag)
    {
        writer.Write(_position);
        writer.Write(_rotation);
        //TODO scale
        tag = ServerTags.PlayerMovement_Build;
    }
    public byte GetPlayMovementTagToUse()
    {
        if (IsGrounded)
        {
            if(StandingOnObject == ushort.MaxValue)
                return ServerTags.PlayerMovement_Play_Grounded;
            else
                return ServerTags.PlayerMovement_Play_Grounded_OnObject;
        }
        else
            return ServerTags.PlayerMovement_Play_NotGrounded;
    }
    public void SerializePlayerMovement_Play(DarkRiftWriter writer, byte tag)
    {
        writer.Write(_position);
        writer.Write(_rotation);
        // When we're grounded, we send position, rot, and the input vector defining how much the person is moving
        if (tag == ServerTags.PlayerMovement_Play_Grounded)
        {
            writer.Write(_inputMovement);
            writer.Write(IsSprintDown);
        } else if (tag == ServerTags.PlayerMovement_Play_Grounded_OnObject)
        {
            writer.Write(StandingOnObject);
            writer.Write(_inputMovement);
            writer.Write(IsSprintDown);
        }else if (tag == ServerTags.PlayerMovement_Play_NotGrounded)
        {
            // When we're ungrounded (like jumping) we send the same stuff as grounded AND a velocity for how fast
            // the character is moving
            writer.Write(_inputMovement);
            writer.Write(_velocity);
        }
    }
    public static void ClearPlayerMovementMessage_Build(DarkRiftReader reader)
    {
        reader.ReadSerializable<Vec3>();
        reader.ReadSerializable<Quat>();
        //TODO scale
    }
    public static void ClearPlayerMovementMessage_Play(DarkRiftReader reader, byte tag)
    {
        // position
        reader.ReadSerializable<Vec3>();
        // rotation
        reader.ReadSerializable<Quat>();
        // The object we're on
        if (tag == ServerTags.PlayerMovement_Play_Grounded_OnObject)
            reader.ReadUInt16();
        // Input (vec2)
        reader.ReadSerializable<Vec2>();
        // Is Sprint Down (bool)
        if (tag == ServerTags.PlayerMovement_Play_Grounded
            || tag == ServerTags.PlayerMovement_Play_Grounded_OnObject)
            reader.ReadBoolean();
        // Base Velocity
        if (tag == ServerTags.PlayerMovement_Play_NotGrounded)
            reader.ReadSerializable<Vec3>();
    }
    public static void ReadFlags(byte flags, out bool isInPlay, out bool hasBlend)
    {
        isInPlay = (flags & 1) != 0;
        hasBlend = (flags & (1 << 1)) != 0;
    }
    public static byte WriteFlags(bool isInPlay, bool hasBlend)
    {
        byte flags = isInPlay ? (byte)1 : (byte)0;
        if (hasBlend)
            flags |= (1 << 1);
        return flags;
    }
    public void Serialize(SerializeEvent e)
    {
        e.Writer.Write(DisplayName);
        e.Writer.Write(ID);
        bool hasBlend = _userBlends != null && _userBlends.Count > 0;
        byte flags = WriteFlags(IsInPlayMode, hasBlend);
        e.Writer.Write(flags);
        e.Writer.Write((byte)TypeOfUser);
        // The object that we're possessing
        e.Writer.Write(PossessedObj);
        // Serialize the grabbed objects
        int numGrabbed = GrabbedObjects == null ? 0 : GrabbedObjects.Count;
        e.Writer.EncodeInt32(numGrabbed);
        for (int i = 0; i < numGrabbed; i++)
        {
            var grabObj = GrabbedObjects[i];
            e.Writer.Write(grabObj.ObjectID);
            e.Writer.Write((byte)grabObj.BodyPart);
            _workingVec3.UpdateFrom(grabObj.RelativePos);
            e.Writer.Write(_workingVec3);
            _workingQuat.UpdateFrom(grabObj.RelativeRot);
            e.Writer.Write(_workingQuat);
        }

        byte tag;
        if (IsInPlayMode)
        {
            tag = GetPlayMovementTagToUse();
            e.Writer.Write(tag);
            SerializePlayerMovement_Play(e.Writer, tag);
        }
        else
        {
            SerializePlayerMovement_Build(e.Writer, out tag);
        }
        e.Writer.Write(_userPose);

        if (hasBlend)
            e.Writer.Write(_userBlends);
    }
    public static DRUser DeserializeWithVersion(DarkRiftReader reader, int version, DRUser existing=null)
    {
        if (existing == null)
            existing = new DRUser();

        if (version >= 3)
            existing.DisplayName = reader.ReadString();
        else
            existing.DisplayName = "";
        existing.ID = reader.ReadUInt16();
        byte flags = reader.ReadByte();
        ReadFlags(flags, out existing.IsInPlayMode, out bool hasBlend);
        existing.TypeOfUser = (UserType)reader.ReadByte();

        if (existing._position == null)
            existing._position = new Vec3();
        if (existing._rotation == null)
            existing._rotation = new Quat();
        if (existing._velocity == null)
            existing._velocity = new Vec3();
        if (existing._inputMovement == null)
            existing._inputMovement = new Vec2();

        // The object that we're possessing
        existing.PossessedObj = reader.ReadUInt16();
        // Deserialize the grabbed objects
        int numGrabbed = reader.DecodeInt32();
        if (existing.GrabbedObjects == null)
            existing.GrabbedObjects = new List<GrabbedObject>(numGrabbed);
        else
            existing.GrabbedObjects.Clear();
        for (int i = 0; i < numGrabbed; i++)
        {
            ushort objectID = reader.ReadUInt16();
            byte bodyPart = reader.ReadByte();
            reader.ReadSerializableInto(ref _workingVec3);
            Vector3 relPos = _workingVec3.ToVector3();
            reader.ReadSerializableInto(ref _workingQuat);
            Quaternion relRot = _workingQuat.ToQuaternion();

            existing.GrabbedObjects.Add(new GrabbedObject()
            {
                ObjectID = objectID,
                BodyPart = (DRUser.GrabbingBodyPart)bodyPart,
                RelativePos = relPos,
                RelativeRot = relRot,
            });
        }

        if (existing.IsInPlayMode)
        {
            byte tag = reader.ReadByte();
            existing.DeserializePlayerMovement_Play(reader, tag);
        }
        else
        {
            existing.DeserializePlayerMovement_Build(reader);
        }
        if (existing.UserPose != null)
            reader.ReadSerializableInto(ref existing._userPose);
        else
            existing._userPose = reader.ReadSerializable<DRUserPose>();

        if (hasBlend)
        {
            if (existing._userBlends != null)
                reader.ReadSerializableInto(ref existing._userBlends);
            else
                existing._userBlends = reader.ReadSerializable<DRUserBlends>();
        }
        else
        {
            if (existing._userBlends != null)
                existing._userBlends.Clear();
        }
        return existing;
    }
    public void Deserialize(DeserializeEvent e)
    {
        DeserializeWithVersion(e.Reader, DRGameState.ApplicationVersion, this);
    }
    public void OnReceivedPing()
    {
        TimeOfLastRecvPing = DateTime.Now.Ticks;
    }
}
