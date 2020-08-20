using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaceDisplay : MonoBehaviour
{
    //private GameObject _faceModel;
    //private string _faceAssetID;
    //private bool _isLoading;

    //// Keep two face Datas, lerp between them
    //// using the time when they arrived
    //private Dictionary<string, float> _faceValuesA;
    //private Dictionary<string, float> _faceValuesB;
    //private float _faceTimeA;
    //private float _faceTimeB;
    //private bool _isLocalUser;
    ////Character Setup that Plugin is Driving
    //private LiveCharacterSetup _character = new LiveCharacterSetup();

    //void Start()
    //{
        
    //}

    //public void AddSerializedFaceData(byte[] data)
    //{
    //    Dictionary<string, float> deserialized = new Dictionary<string, float>(); //TODO save on GC!
    //    if(!SendAndReceiveFaceData.Instance.DeserializeAnimationValues(data, deserialized))
    //    {
    //        Debug.LogError("Failed to deserialize face data");
    //        return;
    //    }

    //    AddFaceData(deserialized);
    //}
    //public void AddFaceData(Dictionary<string, float> animValues)
    //{
    //    // If we have no A, load into there
    //    if(_faceValuesA == null)
    //    {
    //        _faceValuesA = animValues;
    //        _faceTimeA = Time.unscaledTime;
    //        return;
    //    }
    //    // If we have no B, load into there
    //    if(_faceValuesB == null)
    //    {
    //        _faceValuesB = animValues;
    //        _faceTimeB = Time.unscaledTime;
    //        return;
    //    }

    //    // If we have both, move B->A and load into B
    //    _faceValuesA = _faceValuesB;
    //    _faceTimeA = _faceTimeB;

    //    _faceValuesB = animValues;
    //    _faceTimeB = Time.unscaledTime;
    //}

    //public void Init(string faceAssetInstanceID, bool isLocalUser)
    //{
    //    if (_isLoading)
    //    {
    //        Debug.LogWarning("Loading a new faceID while we're still pending. Have " + _faceAssetID + " loading " + faceAssetInstanceID);
    //        //TODO cancel the last load
    //    }
    //    _isLoading = true;
    //    _isLocalUser = isLocalUser;

    //    _faceAssetID = faceAssetInstanceID;
    //    FaceAssetManager.Instance.SpawnFaceModel(_faceAssetID, isLocalUser, transform, OnFaceAssetLoaded);
    //}

    //private void OnFaceAssetLoaded(GameObject newFaceModel, LiveCharacterSetupFile setupFile)
    //{
    //    _isLoading = false;
    //    if(newFaceModel == null)
    //    {
    //        Debug.LogError("Failed to instantiate face model");
    //        return;
    //    }
    //    Debug.Log("Loaded face model");

    //    _faceModel = newFaceModel;
    //    //_faceModel.transform.parent = transform;
    //    //_faceModel.transform.localScale = Vector3.one;
    //    //_faceModel.transform.localPosition = Vector3.zero;
    //    //_faceModel.transform.localRotation = Quaternion.identity;
    //    _character.Init(setupFile.Expressions, setupFile.Controls);
    //}

    //private void UpdateFaceDisplay()
    //{
    //    if (_faceModel == null)
    //        return;
    //    if (_faceValuesA == null)
    //        return;
    //    // Lerp the face data
    //    //TODO

    //    // apply the face data
    //    Dictionary<string, Vector4> rigValues = _character.ConstructRigValues(_faceValuesA, _character.GetNeutralControlValues());
    //    LiveUnityInterface.ApplyControlValues(this.gameObject, rigValues);
    //}
    //void Update()
    //{
    //    UpdateFaceDisplay();
    //}
}
