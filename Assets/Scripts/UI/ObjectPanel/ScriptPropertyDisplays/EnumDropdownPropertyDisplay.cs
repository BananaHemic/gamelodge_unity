using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class EnumDropdownPropertyDisplay : BasePropertyDisplay
{
    public TMP_Dropdown EnumDropdown;
    public VR_Dropdown VRDropdown;

    private FieldInfo _fieldInfo;
    private object _lastValue;
    private bool _hasInit = false;
    private List<string> _enumNames;
    private System.Array _enumValues;

    public void Init(FieldInfo fieldInfo, ComponentCard parentCard, BaseBehavior behavior)
    {
        _fieldInfo = fieldInfo;
        //TODO We can remove the alloc here by keeping a cache elsewhere of enum->enum names/values
        _enumNames = new List<string>(System.Enum.GetNames(fieldInfo.FieldType));
        _enumValues = System.Enum.GetValues(fieldInfo.FieldType);
        // Configure the VR Dropdown
        VRDropdown.ActiveParent = parentCard.transform.parent;

        EnumDropdown.AddOptions(_enumNames);
        UpdateValueFromBehavior(behavior);
        UpdateDisplayFromValueChange();
        EnumDropdown.onValueChanged.AddListener(OnDropdownValueChange);
        base.Init(fieldInfo.Name, parentCard, behavior);
        _hasInit = true;
    }
    protected override void UpdateBehaviorFromValue(BaseBehavior baseBehavior)
    {
        _fieldInfo.SetValue(baseBehavior, _lastValue);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        _lastValue = _fieldInfo.GetValue(baseBehavior);
    }
    private int GetEnumIndexFromValue(object val)
    {
        for(int i = 0; i < _enumValues.Length; i++)
        {
            if (_enumValues.GetValue(i).Equals(val))
                return i;
        }
        Debug.LogError("Failed to find good enum value!");
        return 0;
    }
    protected override void UpdateDisplayFromValueChange()
    {
        //Debug.Log("Updating EnumDisplay value " + _lastValue);
        // Get the index from the 
        EnumDropdown.value = GetEnumIndexFromValue(_lastValue);
    }
    public void OnDropdownValueChange(int newValIdx)
    {
        if (!_hasInit)
            return;
        if (IsChanging)
            return;
        _lastValue = _enumValues.GetValue(newValIdx);
        base.OnValueChanged(true);
    }
    protected override void ResetState()
    {
        _hasInit = false;
        _fieldInfo = null;
        EnumDropdown.ClearOptions();
    }
}
