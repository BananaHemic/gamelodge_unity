using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public class OverridePriority : MonoBehaviour
{
    void Awake()
    {
        using (Process p = Process.GetCurrentProcess()) 
            p.PriorityClass = ProcessPriorityClass.AboveNormal; 
    }
}
