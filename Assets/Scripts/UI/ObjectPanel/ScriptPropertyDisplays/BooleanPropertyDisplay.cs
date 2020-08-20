using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class BooleanPropertyDisplay : BasePropertyDisplay
{
    public Toggle ToggleField;

    private FieldInfo _fieldInfo;
    private bool _lastValue;
    private bool _hasInit = false;

    public void Init(FieldInfo fieldInfo, ComponentCard parentCard, BaseBehavior behavior)
    {
        _fieldInfo = fieldInfo;
        UpdateValueFromBehavior(behavior);
        UpdateDisplayFromValueChange();
        ToggleField.onValueChanged.AddListener(OnToggleValueChange);
        base.Init(fieldInfo.Name, parentCard, behavior);
        _hasInit = true;
    }
    protected override void UpdateBehaviorFromValue(BaseBehavior baseBehavior)
    {
        //Debug.Log("Set behavior " + _fieldInfo.Name + " to " + _lastValue);
        _fieldInfo.SetValue(baseBehavior, _lastValue);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        _lastValue = (bool)_fieldInfo.GetValue(baseBehavior);
    }
    protected override void UpdateDisplayFromValueChange()
    {
        ToggleField.isOn = _lastValue;
    }
    public void OnToggleValueChange(bool newVal)
    {
        if (!_hasInit)
            return;
        if (IsChanging)
            return;
        Debug.Log("Toggle now " + newVal);
        _lastValue = newVal;
        base.OnValueChanged(true);
    }
    protected override void ResetState()
    {
        _hasInit = false;
        _fieldInfo = null;
    }
}
