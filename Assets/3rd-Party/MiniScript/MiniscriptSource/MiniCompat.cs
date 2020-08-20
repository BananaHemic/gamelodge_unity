using System.Collections;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
using System;

public static class MiniCompat
{
    public static void Log(string msg)
    {
#if UNITY_5_3_OR_NEWER
        Debug.Log(msg);
#else
        Console.WriteLine(msg);
#endif
    }
    public static void LogWarning(string wrn)
    {
#if UNITY_5_3_OR_NEWER
        Debug.LogWarning(wrn);
#else
        Console.WriteLine("WRN " + wrn);
#endif
    }
    public static void LogError(string err)
    {
#if UNITY_5_3_OR_NEWER
        Debug.LogError(err);
#else
        Console.WriteLine("ERR " + err);
#endif
    }
}
