using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPointBehavior : BaseBehavior
{
    /// <summary>
    /// NB! This is never actually set, only the BundleID / Index is set in the SerializedBundleItemReference
    /// </summary>
    public AvatarField Avatar;
    public uint SpawnOrder;
    public bool CanSpawnMultipleUsers;

    private readonly SerializedSceneObjectORBundleItemReference _avatarReference = new SerializedSceneObjectORBundleItemReference(nameof(Avatar));
    const int AvatarDescriptorKey = 0;
    const int SpawnOrderKey = 1;
    const int CanSpawnMultipleUsersKey = 2;

    private uint _prevSpawnOrder;

    protected override void ChildInit()
    {
        BuildPlayManager.Instance.RegisterSpawnPoint(this);
        _prevSpawnOrder = SpawnOrder;
        base.AddSceneObjectOrBundleItemReference(_avatarReference);
        RefreshProperties();
    }
    public SerializedSceneObjectORBundleItemReference GetAvatarReference()
    {
        return _avatarReference;
    }
    public override void Destroy()
    {
        BuildPlayManager.Instance.DeRegisterSpawnPoint(this);
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return false;
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return null;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return null;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        // Avatar
        byte[] avatarData = _avatarReference.GetSerialized(out int avatarFlags);
        _serializedBehavior.LocallySetData(AvatarDescriptorKey, avatarData, avatarFlags);
        // Spawn Order
        _serializedBehavior.LocallySetData(SpawnOrderKey, BitConverter.GetBytes(SpawnOrder));
        // Can Spawn Multiple Users
        _serializedBehavior.LocallySetData(CanSpawnMultipleUsersKey, BitConverter.GetBytes(CanSpawnMultipleUsers));
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Avatar
        byte[] avatarArray;
        if (_serializedBehavior.TryReadProperty(AvatarDescriptorKey, out avatarArray, out int avatarFlag))
            _avatarReference.UpdateFrom(avatarArray, avatarFlag);
        // Spawn Order
        byte[] priorityArray;
        if(_serializedBehavior.TryReadProperty(SpawnOrderKey, out priorityArray, out int _))
            SpawnOrder = BitConverter.ToUInt32(priorityArray, 0);
        // Can Spawn Multiple Users
        byte[] spawnMultipleArray;
        if(_serializedBehavior.TryReadProperty(CanSpawnMultipleUsersKey, out spawnMultipleArray, out int _))
            CanSpawnMultipleUsers = BitConverter.ToBoolean(spawnMultipleArray, 0);

        // Re-update our internal storage if the spawn order changed
        if (_prevSpawnOrder != SpawnOrder)
        {
            BuildPlayManager.Instance.SpawnPointOrderChange(this);
            _prevSpawnOrder = SpawnOrder;
        }
    }
    public override void RefreshProperties()
    {
        // Re-update our internal storage if the spawn order changed
        if (_prevSpawnOrder != SpawnOrder)
        {
            BuildPlayManager.Instance.SpawnPointOrderChange(this);
            _prevSpawnOrder = SpawnOrder;
        }
    }
    public static void LoadIntrinsics()
    {
    }
}
