using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildUIManager : BasePanel<BuildUIManager>
{
    public RectTransform TopBar;
    // The top bar modes
    public CanvasToggle ObjectTopBar;
    public CanvasToggle AssetTopBar;
    public CanvasToggle WorldTopBar;
    public CanvasToggle EditSettingsTopBar;
    // The different mode buttons
    public Button ObjectModeButton;
    public Button AssetsModeButton;
    public Button WorldModeButton;
    public Button EditSettingsModeButton;
    public ObjectPanel ObjectPanelScript;
    public AssetPanel AssetPanelScript;
    public WorldPanel WorldPanelScript;
    public EditSettingsPanel EditSettingsPanelScript;

    public enum Build_UI_Modes{
        Object,
        Assets,
        World,
        EditSettings
    }
    public Build_UI_Modes CurrentMode { get; private set; }

    private bool _hasRefreshed = false;

    void Start()
    {
        if(!_hasRefreshed)
            RefreshForMode();
    }
    // Called from the UI's bottom buttons
    public void SwitchToBuildMode(int modeInt)
    {
        Build_UI_Modes mode = (Build_UI_Modes)modeInt;
        CurrentMode = mode;
        RefreshForMode();
    }
    protected override void RefreshForMode()
    {
        //Debug.LogWarning("BuildUIManager refreshing, vis " + _isVisible + " inter " + _isInteractable + " mode " + CurrentMode);
        _hasRefreshed = true;
        // Set buttons interactability
        ObjectModeButton.interactable = CurrentMode != Build_UI_Modes.Object;
        AssetsModeButton.interactable = CurrentMode != Build_UI_Modes.Assets;
        WorldModeButton.interactable = CurrentMode != Build_UI_Modes.World;
        EditSettingsModeButton.interactable = CurrentMode != Build_UI_Modes.EditSettings;

        // Set top bar visibility
        ObjectTopBar.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.Object,
            _isInteractable && CurrentMode == Build_UI_Modes.Object);
        AssetTopBar.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.Assets,
            _isInteractable && CurrentMode == Build_UI_Modes.Assets);
        WorldTopBar.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.World,
            _isInteractable && CurrentMode == Build_UI_Modes.World);
        EditSettingsTopBar.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.EditSettings,
            _isInteractable && CurrentMode == Build_UI_Modes.EditSettings);

        // Set main panel visibility
        ObjectPanelScript.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.Object,
            _isInteractable && CurrentMode == Build_UI_Modes.Object);
        AssetPanelScript.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.Assets,
            _isInteractable && CurrentMode == Build_UI_Modes.Assets);
        WorldPanelScript.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.World,
            _isInteractable && CurrentMode == Build_UI_Modes.World);
        EditSettingsPanelScript.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == Build_UI_Modes.EditSettings,
            _isInteractable && CurrentMode == Build_UI_Modes.EditSettings);
    }
}
