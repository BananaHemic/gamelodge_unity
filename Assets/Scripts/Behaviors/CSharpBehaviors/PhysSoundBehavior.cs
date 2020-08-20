using Miniscript;
using PhysSound;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysSoundBehavior : BaseBehavior
{
    public PhysSoundMaterial SoundMaterial;
    private readonly SerializedBundleItemReference _soundMaterialTypeReference = new SerializedBundleItemReference(nameof(SoundMaterial));
    const int SoundMaterialKey = 0;

    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedVariable> _userVariables = new List<ExposedVariable>();
    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();

    private PhysSoundObject _soundObject;
    private static bool _hasLoadedIntrinsics = false;
    private bool _waitingOnSoundMaterialLoad = false;

    private string _loadedSoundMaterialBundleID;
    private ushort _loadedSoundMaterialBundleIndex;
    private int _currentlyLoadingID;

    protected override void ChildInit()
    {
        base.AddBundleItemReference(_soundMaterialTypeReference);
        RefreshProperties();
    }
    private void AddSoundObject()
    {
        _soundObject = gameObject.AddComponent<PhysSoundObject>();
        _soundObject.ImpactAudio = PhysSoundManager.Instance.AudioPrefab;
        _soundObject.AutoCreateSources = true;
        _soundObject.SoundMaterial = SoundMaterial;
        _soundObject.Initialize();
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
        return true;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return _userEvents;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return _userVariables;
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // SoundMaterial
        byte[] soundMaterialArray;
        if (_serializedBehavior.TryReadProperty(SoundMaterialKey, out soundMaterialArray, out int _))
            _soundMaterialTypeReference.UpdateFrom(soundMaterialArray);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        _serializedBehavior.LocallySetData(SoundMaterialKey, _soundMaterialTypeReference.GetSerialized());
    }
    void OnSoundMaterialLoaded(int loadID, PhysSoundMaterial soundMat)
    {
        if (!_waitingOnSoundMaterialLoad)
            Debug.LogWarning("Clip loaded, but loading flag not set");
        _waitingOnSoundMaterialLoad = false;
        //Debug.Log("Audio clip loaded");
        if (SoundMaterial == soundMat)
        {
            //Debug.Log("Already have that clip selected");
            return;
        }
        if(_currentlyLoadingID != loadID)
        {
            Debug.LogWarning("Dropping sound material load, was load ID #" + loadID + " expected " + _currentlyLoadingID);
            return;
        }
        SoundMaterial = soundMat;
        if (_soundObject == null)
            AddSoundObject();
        else
            _soundObject.SoundMaterial = soundMat;
    }
    public override void RefreshProperties()
    {
        //Debug.Log("AudioPlayer refresh properties " + _audioClipReference.BundleID + " #" + _audioClipReference.BundleIndex);
        if (string.IsNullOrEmpty(_soundMaterialTypeReference.BundleID))
        {
            SoundMaterial = null;
            if(_soundObject != null)
                _soundObject.SoundMaterial = null;
            _loadedSoundMaterialBundleID = null;
            _loadedSoundMaterialBundleIndex = ushort.MaxValue;
        }
        else
        {
            if(_loadedSoundMaterialBundleID != _soundMaterialTypeReference.BundleID
                || _loadedSoundMaterialBundleIndex != _soundMaterialTypeReference.BundleIndex)
            {
                _loadedSoundMaterialBundleID = _soundMaterialTypeReference.BundleID;
                _loadedSoundMaterialBundleIndex = _soundMaterialTypeReference.BundleIndex;
                int loadID = ++_currentlyLoadingID;
                _waitingOnSoundMaterialLoad = true;
                BundleManager.Instance.LoadItemFromBundle<PhysSoundMaterial>(_soundMaterialTypeReference.BundleID, _soundMaterialTypeReference.BundleIndex, loadID, OnSoundMaterialLoaded);
            }
        }
    }
    public override void Destroy()
    {
        if (_soundObject != null)
            GameObject.Destroy(_soundObject);
        _soundObject = null;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;
    }
}
