using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenericSingleton<T> : MonoBehaviour where T : Component
{
    private static T _instance;
    public static T Instance
    {
        get { return _instance; }
    }

    protected virtual void Awake()
    {
        if (_instance != null)
        {
            Debug.LogError("Destroying instance of type: " + typeof(T).FullName, this);
            Destroy(_instance);
        }
        _instance = this as T;
    }
}