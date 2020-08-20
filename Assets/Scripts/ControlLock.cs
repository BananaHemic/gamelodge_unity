using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlLock : GenericSingleton<ControlLock>
{
    public enum ControlType
    {
        XJoystick_Right
    }
    private readonly HashSet<ControlType> _activeLocks = new HashSet<ControlType>();

    public bool TryLock(ControlType controlType)
    {
        return _activeLocks.Add(controlType);
    }
    public void ReturnLock(ControlType controlType)
    {
        if (!_activeLocks.Remove(controlType))
            Debug.LogError("Failed to remove lock of type " + controlType);
    }
    public bool IsLocked(ControlType controlType)
    {
        return _activeLocks.Contains(controlType);
    }
}
