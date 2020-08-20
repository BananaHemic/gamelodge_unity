using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class RangedFloatPropertyDisplay : BasePropertyDisplay
{
    public Slider PropertySlider;
    public TMP_InputField PropertyInputField;

    private FieldInfo _fieldInfo;
    private float _lastValue;
    private bool _hasInit = false;

    public void Init(FieldInfo fieldInfo, float min, float max, ComponentCard parentCard, BaseBehavior behavior)
    {
        _fieldInfo = fieldInfo;
        PropertySlider.minValue = min;
        PropertySlider.maxValue = max;
        UpdateValueFromBehavior(behavior);
        UpdateDisplayFromValueChange();
        PropertySlider.onValueChanged.AddListener(OnSliderValueChange);
        PropertyInputField.onEndEdit.AddListener(OnInputFieldValueChange);
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
        _lastValue = (float)_fieldInfo.GetValue(baseBehavior);
    }
    protected override void UpdateDisplayFromValueChange()
    {
        PropertySlider.value = _lastValue;
        PropertyInputField.text = _lastValue.ToString();
    }
    public void OnInputFieldValueChange(string newText)
    {
        if (!_hasInit)
            return;
        if (IsChanging)
            return;
        //Debug.Log("Input val " + newText);
        float res;
        if(float.TryParse(newText, out res))
        {
            _lastValue = res;
            base.OnValueChanged(true);
        }
        else
        {
            Debug.LogError("Failed to parse input text: " + newText);
        }
    }
    public void OnSliderValueChange(float newValue)
    {
        if (!_hasInit)
            return;
        if (IsChanging)
            return;
        //Debug.Log("Slider val " + newValue);
        _lastValue = newValue;
        base.OnValueChanged(false);
    }
    protected override void ResetState()
    {
        _hasInit = false;
        _fieldInfo = null;
    }
}
