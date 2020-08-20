using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSettingsViewWorldPanel : GenericSingleton<WorldSettingsViewWorldPanel>
{
    public TMP_InputField FloorWidthField;
    public TMP_InputField FloorHeightField;
    public Toggle FloorVisibilityToggle;

    const string WidthIdentifier = "widthField_WorldPanel";
    const string HeightIdentifier = "heightField_WorldPanel";

    private bool _isChangingViaNetwork = false;

    public void OnNetworkChangeBoardSize(float width, float height)
    {
        // TODO drop this if we don't have this canvas open
        _isChangingViaNetwork = true;
        // TODO needless string alloc here, micro-optimization with SourceLine
        FloorWidthField.text = width.ToString();
        FloorHeightField.text = height.ToString();
        _isChangingViaNetwork = false;
    }
    public void OnNetworkChangeBoardVisibility(bool visibility)
    {
        // TODO drop this if we don't have this canvas open
        _isChangingViaNetwork = true;
        FloorVisibilityToggle.isOn = visibility;
        _isChangingViaNetwork = false;
    }
    public void OnPanelSelected()
    {
        // Only now should we load the info into the views
    }
    public void OnFloorVisiblityToggle()
    {
        if (_isChangingViaNetwork)
            return;
        Board.Instance.OnLocalChangeToBoardVisibility(FloorVisibilityToggle.isOn);
    }
    public void OnWidthHeightInputEndEdit()
    {
        if (_isChangingViaNetwork)
        {
            Debug.LogWarning("Dropping floor change");
            return;
        }
        if(!float.TryParse(FloorWidthField.text, out float width))
        {
            Debug.LogError("Failed parsing board width \"" + FloorWidthField.text + "\"");
            return;
        }
        if(!float.TryParse(FloorHeightField.text, out float height))
        {
            Debug.LogError("Failed parsing board height \"" + FloorHeightField.text + "\"");
            return;
        }

        Debug.Log("Set the floor width/height to " + width + "x" + height);
        Board.Instance.OnLocalChangeToBoardSize(width, height);
    }
    public void OnWidthInputFieldSelect()
    {
        RLDHelper.Instance.RegisterInputSelected(WidthIdentifier);
    }
    public void OnWidthInputFielDeselect()
    {
        RLDHelper.Instance.RegisterInputDeselected(WidthIdentifier);
    }
    public void OnHeightInputFieldSelect()
    {
        RLDHelper.Instance.RegisterInputSelected(HeightIdentifier);
    }
    public void OnHeightInputFieldDeselect()
    {
        RLDHelper.Instance.RegisterInputDeselected(HeightIdentifier);
    }
}
