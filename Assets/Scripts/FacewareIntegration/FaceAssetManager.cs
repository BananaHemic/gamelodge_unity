using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FaceAssetManager : GenericSingleton<FaceAssetManager>
{
    //public GameObject catPrefab;
    //public LiveCharacterSetupFile catFaceSettings;

    //public bool SpawnFaceModel(string instanceID, bool asLocalUser, Transform parent, Action<GameObject, LiveCharacterSetupFile> onDone)
    //{
    //    StartCoroutine(SpawnModelRoutine(instanceID, asLocalUser, parent, onDone));
    //    return true;
    //}

    //private IEnumerator SpawnModelRoutine(string instanceID, bool asLocalUser, Transform parent, Action<GameObject, LiveCharacterSetupFile> onDone)
    //{
    //    // 
    //    yield return null;
    //    GameObject newObj = GameObject.Instantiate(catPrefab, parent);
    //    newObj.SetLayerRecursively(asLocalUser ? GLLayers.LocalUser_PlayLayerNum : GLLayers.OtherUser_PlayLayerNum);
    //    if (onDone != null)
    //        onDone(newObj, catFaceSettings);
    //}
}
