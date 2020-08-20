using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes sure that the Persistent GameObject only exists once
/// even though it's present in every scene. This is done so that
/// we can just play a scene, and it will already be connected
/// and configured; There's no need to go through the Lobby when testing
/// </summary>
public class PersistentObjectManager : MonoBehaviour
{
    private static bool _hasPersistenObject;
    void Awake()
    {
        //Debug.Log("Awake");
        if (!_hasPersistenObject)
        {
            DontDestroyOnLoad(this.gameObject);
            _hasPersistenObject = true;
        }
        else
        {
            //Destroy(this.gameObject);
            DestroyImmediate(this.gameObject);
            //Debug.Log("Removing self");
        }
    }

    //private void Update()
    //{
    //    if(Input.GetKey(KeyCode.L))
    //        SceneManager.LoadScene(0);
    //}
}
