using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class UnsignedIntPropertyDisplay : BasePropertyDisplay
{
    public TMP_InputField PropertyInputField;

    private FieldInfo _fieldInfo;
    private uint _lastValue;
    private bool _hasInit = false;

    public void Init(FieldInfo fieldInfo, ComponentCard parentCard, BaseBehavior behavior)
    {
        _fieldInfo = fieldInfo;
        UpdateValueFromBehavior(behavior);
        UpdateDisplayFromValueChange();
        PropertyInputField.onEndEdit.AddListener(OnInputFieldValueChange);
        base.Init(fieldInfo.Name, parentCard, behavior);
        _hasInit = true;
    }
    protected override void UpdateBehaviorFromValue(BaseBehavior baseBehavior)
    {
        Debug.Log("Set uint " + _fieldInfo.Name + " to " + _lastValue);
        _fieldInfo.SetValue(baseBehavior, _lastValue);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        _lastValue = (uint)_fieldInfo.GetValue(baseBehavior);
    }
    protected override void UpdateDisplayFromValueChange()
    {
        PropertyInputField.text = _lastValue.ToString();
    }
    public void OnInputFieldValueChange(string newText)
    {
        if (!_hasInit)
            return;
        if (IsChanging)
            return;
        Debug.Log("Input val " + newText);
        uint res;
        if(uint.TryParse(newText, out res))
        {
            _lastValue = res;
            base.OnValueChanged(true);
        }
        else
        {
            Debug.LogError("Failed to parse input text: " + newText);
        }
    }
    protected override void ResetState()
    {
        _hasInit = false;
        _fieldInfo = null;
    }
}
