using System.Collections;
using System.Collections.Generic;
using DarkRift;
#if UNITY
using UnityEngine;
#endif

public class DRUserPose : IDarkRiftSerializable
{
    public struct PoseInfo
    {
        public Vector3 Origin;
        public Vector3 HeadPos;
        public Vector3 LHandPos;
        public Vector3 RHandPos;
        public bool HasLHand;
        public bool HasRHand;
        public Quaternion HeadRot;
        public Quaternion LHandRot;
        public Quaternion RHandRot;
    }
    // How far away from the origin we should allow
    // head/hands to be
    public static readonly float MaxSignedDistance = 1.2f;
    public static readonly float MaxAbsDistance = 2 * 1.2f;
    public int PoseAvailabilityFlags { get; private set; }
    const int PoseSingleFlags = 0;
    const int PoseThreePointsFlag = 0
        | (1 << 0)
        | (1 << 1);

    private Quat _headRot = new Quat();
    private Quat _lHandRot = new Quat();
    private Quat _rHandRot = new Quat();
    private bool _hasLHand;
    private bool _hasRHand;
    private const int NumDimensions = 1 + 3 * 2;
    private readonly float[] _positions = new float[NumDimensions];

    public DRUserPose() { }
    public bool UpdateFrom(PoseInfo poseInfo)
    {
        bool didClamp = false;
        int i = 0;
        float dist, clampedDist;
        // Head Y Pos
        dist = poseInfo.HeadPos.y - poseInfo.Origin.y;
        clampedDist = DRCompat.Clamp(dist, 0, MaxAbsDistance);
        didClamp |= clampedDist != dist;
        _positions[i++] = clampedDist;
        _hasLHand = poseInfo.HasLHand;
        _hasRHand = poseInfo.HasRHand;

        // Each bit controls which hands are currently in use
        PoseAvailabilityFlags = 0
            | (_hasLHand ? (1 << 0) : 0)
            | (_hasRHand ? (1 << 1) : 0);

        // Left Hand pos
        for (int vIdx = 0; vIdx < 3; vIdx++)
        {
            if (_hasLHand)
            {
                dist = poseInfo.LHandPos[vIdx] - poseInfo.Origin[vIdx];
                // Y uses absolute distance, as it's always above origin
                clampedDist = vIdx == 1 ? DRCompat.Clamp(dist, 0, MaxAbsDistance) : DRCompat.Clamp(dist, -MaxSignedDistance, MaxSignedDistance);
                didClamp |= clampedDist != dist;
                // If this is using a signed distance, we add to force it to be an unsigned difference
                if (vIdx != 1)
                    clampedDist += MaxSignedDistance;
            }
            else
            {
                clampedDist = 0;
            }
            _positions[i++] = clampedDist;
        }
        // Right Hand pos
        for (int vIdx = 0; vIdx < 3; vIdx++)
        {
            if (_hasRHand)
            {
                dist = poseInfo.RHandPos[vIdx] - poseInfo.Origin[vIdx];
                // Y uses absolute distance, as it's always above origin
                clampedDist = vIdx == 1 ? DRCompat.Clamp(dist, 0, MaxAbsDistance) : DRCompat.Clamp(dist, -MaxSignedDistance, MaxSignedDistance);
                didClamp |= clampedDist != dist;
                // If this is using a signed distance, we add to force it to be an unsigned difference
                if (vIdx != 1)
                    clampedDist += MaxSignedDistance;
            }
            else
            {
                clampedDist = 0;
            }
            _positions[i++] = clampedDist;
        }
        // Rotations
        _headRot.UpdateFrom(poseInfo.HeadRot);
        _lHandRot.UpdateFrom(poseInfo.LHandRot);
        _rHandRot.UpdateFrom(poseInfo.RHandRot);

        return didClamp;
    }
    public PoseInfo GetPoseInfo(Vector3 origin)
    {
        int i = 0;
        return new PoseInfo
        {
            Origin = origin,
            HeadPos = origin + new Vector3(0, _positions[i++], 0),
            LHandPos = origin + new Vector3(_positions[i++] - MaxSignedDistance, _positions[i++], _positions[i++] - MaxSignedDistance),
            RHandPos = origin + new Vector3(_positions[i++] - MaxSignedDistance, _positions[i++], _positions[i++] - MaxSignedDistance),
            HasLHand = _hasLHand,
            HasRHand = _hasRHand,
            HeadRot = _headRot.ToQuaternion(),
            LHandRot = _lHandRot.ToQuaternion(),
            RHandRot = _rHandRot.ToQuaternion(),
        };
    }
    private void SerializePose(DarkRiftWriter writer)
    {
        //int posA = writer.Position;
        // Serialize all 7 floats as ints with 12 bit precision
        // this uses 12 bytes total and has room for 4 flags
        bool hasExtraByte = false;
        byte extraByte = 0;
        for (int i = 0; i < _positions.Length; i++)
        {
            // Skip data if it's not relevant for us
            if (i >= 1 && i <= 3 && !_hasLHand)
                continue;
            if (i >= 4 && i <= 6 && !_hasRHand)
                continue;

            // Float -> ushort
            int val = DRCompat.RoundToInt((1 << 12 - 1) * _positions[i] / MaxAbsDistance);
            //if (i == 0)
            //Debug.Log("h: " + val);
            //if (i == 3)
            //Debug.Log("lz in " + val);
            if (val > (1 << 12) - 1)
                DRCompat.LogError("Too large, got " + val + " largest allowed" + ((1 << 12) - 1));
            if (hasExtraByte)
            {
                byte leftByte = (byte)(val >> 8);
                byte rightByte = (byte)val;
                leftByte |= extraByte;
                writer.Write(leftByte);
                writer.Write(rightByte);
                hasExtraByte = false;
            }
            else
            {
                // Write the first 8 bits now,
                // store the last 4 bits for later
                byte leftByte = (byte)(val >> 4);
                byte rightByte = (byte)(val << 4);
                writer.Write(leftByte);
                extraByte = rightByte;
                hasExtraByte = true;
            }
        }
        // Now we'll have an extra byte with only 4 bits used. The rightmost 4 bits could contain flags if we want
        if (hasExtraByte)
            writer.Write(extraByte);
        // Serialize rotations
        writer.Write(_headRot);
        if (_hasLHand)
            writer.Write(_lHandRot);
        if (_hasRHand)
            writer.Write(_rHandRot);
        //DRCompat.Log("serialized " + (writer.Position - posA));
    }
    private void DeserializePose(DarkRiftReader reader)
    {
        //int posA = reader.Position;
        //int len = reader.Length;
        //DRCompat.Log("will read from " + posA + " to " + len + " num " + (len - posA) + " flags " + PoseAvailabilityFlags);
        _hasLHand = (PoseAvailabilityFlags & (1 << 0)) != 0;
        _hasRHand = (PoseAvailabilityFlags & (1 << 1)) != 0;
        // Deserialize the 12 bytes of positions
        bool hasExtraByte = false;
        byte extraByte = 0;
        for (int i = 0; i < _positions.Length; i++)
        {
            // Skip data if it's not relevant for us
            if (i >= 1 && i <= 3 && !_hasLHand)
                continue;
            if (i >= 4 && i <= 6 && !_hasRHand)
                continue;

            byte first = reader.ReadByte();
            ushort val;
            if (!hasExtraByte)
            {
                extraByte = reader.ReadByte();
                hasExtraByte = true;
                val = (ushort)((first << 4) | (extraByte >> 4));
            }
            else
            {
                // Mask out the first 4 bits of extraByte, they
                // belong to a different val
                extraByte &= (1 << 4) - 1;
                val = (ushort)((extraByte << 8) | first);
                hasExtraByte = false;
            }
            //if (i == 0)
            //Debug.Log("h out " + val);
            //if (i == 3)
            //Debug.Log("lz out " + val);
            // ushort -> float
            float fVal = ((float)val * MaxAbsDistance) / (float)(1 << 12 - 1);
            _positions[i] = fVal;
        }

        // Deserialize rotations
        reader.ReadSerializableInto(ref _headRot);
        if (_hasLHand)
            reader.ReadSerializableInto(ref _lHandRot);
        if (_hasRHand)
            reader.ReadSerializableInto(ref _rHandRot);
        //DRCompat.Log("read " + (reader.Position - posA));
    }
    private static void ReadPose(int poseAvailabilityFlags, DarkRiftReader reader)
    {
        bool hasLHand = (poseAvailabilityFlags & (1 << 0)) != 0;
        bool hasRHand = (poseAvailabilityFlags & (1 << 1)) != 0;
        // Deserialize the 12 bytes of positions
        bool hasExtraByte = false;
        byte extraByte = 0;
        for (int i = 0; i < NumDimensions; i++)
        {
            // Skip data if it's not relevant for us
            if (i >= 1 && i <= 3 && !hasLHand)
                continue;
            if (i >= 4 && i <= 6 && !hasRHand)
                continue;

            byte first = reader.ReadByte();
            ushort val;
            if (!hasExtraByte)
            {
                extraByte = reader.ReadByte();
                hasExtraByte = true;
                val = (ushort)((first << 4) | (extraByte >> 4));
            }
            else
            {
                // Mask out the first 4 bits of extraByte, they
                // belong to a different val
                extraByte &= (1 << 4) - 1;
                val = (ushort)((extraByte << 8) | first);
                hasExtraByte = false;
            }
        }

        // Deserialize rotations
        reader.ReadSerializable<Quat>();
        if (hasLHand)
            reader.ReadSerializable<Quat>();
        if (hasRHand)
            reader.ReadSerializable<Quat>();
    }
    public static void ClearPose(int tag, DarkRiftReader reader)
    {
        if (tag == ServerTags.UserPose_Single)
            ReadPose(PoseSingleFlags, reader);
        else if (tag == ServerTags.UserPose_ThreePoints)
            ReadPose(PoseThreePointsFlag, reader);
        else if (tag == ServerTags.UserPose_Full)
        {
            int poseFlags = reader.DecodeInt32();
            ReadPose(poseFlags, reader);
        }
        else
        {
            DRCompat.LogError("Can't clear pose with unhandled tag " + tag);
        }
    }
    public void Serialize_Full(DarkRiftWriter writer)
    {
        // Write the flag that we need
        writer.EncodeInt32(PoseAvailabilityFlags);
        SerializePose(writer);
    }
    public void Serialize(DarkRiftWriter writer, out byte tag)
    {
        //DRCompat.Log("Writing pose flags " + PoseAvailabilityFlags);
        if (PoseAvailabilityFlags == PoseSingleFlags)
            tag = ServerTags.UserPose_Single;
        else if (PoseAvailabilityFlags == PoseThreePointsFlag)
            tag = ServerTags.UserPose_ThreePoints;
        else
            tag = ServerTags.UserPose_Full;
        SerializePose(writer);
    }
    public void Deserialize_Full(DarkRiftReader reader)
    {
        PoseAvailabilityFlags = reader.DecodeInt32();
        //DRCompat.Log("Read pose flags " + PoseAvailabilityFlags);
        DeserializePose(reader);
    }
    public void Deserialize_Single(DarkRiftReader reader)
    {
        PoseAvailabilityFlags = PoseSingleFlags;
        DeserializePose(reader);
    }
    public void Deserialize_ThreePoints(DarkRiftReader reader)
    {
        PoseAvailabilityFlags = PoseThreePointsFlag;
        DeserializePose(reader);
    }
    public void Deserialize(DarkRiftReader reader, int tag)
    {
        if (tag == ServerTags.UserPose_Single)
            Deserialize_Single(reader);
        else if (tag == ServerTags.UserPose_ThreePoints)
            Deserialize_ThreePoints(reader);
        else if (tag == ServerTags.UserPose_Full)
            Deserialize_Full(reader);
        else
            DRCompat.LogError("Unhandled pose tag #" + tag);
    }
    public void Serialize(SerializeEvent e)
    {
        Serialize_Full(e.Writer);
    }
    public void Deserialize(DeserializeEvent e)
    {
        Deserialize_Full(e.Reader);
    }
}
