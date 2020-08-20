using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorldPanel : BasePanel<WorldPanel>
{
    public Button GraphicsSettingsButton;
    public Button PlayerSettingsButton;
    public Button LoadButton;
    public Button SaveButton;
    public WorldSettingsViewWorldPanel WorldSettingsViewWorldPanelScript;
    public LoadGamesViewWorldPanel LoadGamesViewWorldPanelScript;
    public SaveGamesViewWorldPanel SaveGamesViewWorldPanelScript;
    //public MaterialSettings MaterialSettingsObj;
    //public CodeUI CodeUIObj;
    public CanvasToggle WorldSettingsPanel;
    public CanvasToggle PlayerSettingsPanel;
    public CanvasToggle LoadPanel;
    public CanvasToggle SavePanel;
    public WorldMode CurrentMode { get; private set; }
    public enum WorldMode
    {
        WorldSettings,
        PlayerSettings,
        Load,
        Save
    }

    private bool _hasRefreshed = false;

    private void Start()
    {
        if(!_hasRefreshed)
            RefreshForMode();
    }
    public void OnGraphicsSettingsButtonClicked()
    {
        CurrentMode = WorldMode.WorldSettings;
        RefreshForMode();
    }
    public void OnPlayerSettingsButtonClicked()
    {
        CurrentMode = WorldMode.PlayerSettings;
        RefreshForMode();
    }
    public void OnLoadButtonClicked()
    {
        CurrentMode = WorldMode.Load;
        RefreshForMode();
    }
    public void OnSaveButtonClicked()
    {
        CurrentMode = WorldMode.Save;
        RefreshForMode();
    }
    protected override void RefreshForMode()
    {
        _hasRefreshed = true;
        // Buttons
        GraphicsSettingsButton.interactable = CurrentMode != WorldMode.WorldSettings;
        PlayerSettingsButton.interactable = CurrentMode != WorldMode.PlayerSettings;
        LoadButton.interactable = CurrentMode != WorldMode.Load;
        SaveButton.interactable = CurrentMode != WorldMode.Save;
        // Scripts
        LoadGamesViewWorldPanelScript.enabled = CurrentMode == WorldMode.Load;
        SaveGamesViewWorldPanelScript.enabled = CurrentMode == WorldMode.Save;
        //MaterialSettingsObj.gameObject.SetActive(CurrentMode == ObjectMode.Material);
        //CodeUIObj.gameObject.SetActive(CurrentMode == ObjectMode.Code);

        // Canvas / GraphicRayCaster
        WorldSettingsPanel.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == WorldMode.WorldSettings,
            _isInteractable && CurrentMode == WorldMode.WorldSettings);
        PlayerSettingsPanel.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == WorldMode.PlayerSettings,
            _isInteractable && CurrentMode == WorldMode.PlayerSettings);
        LoadPanel.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == WorldMode.Load,
            _isInteractable && CurrentMode == WorldMode.Load);
        SavePanel.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == WorldMode.Save,
            _isInteractable && CurrentMode == WorldMode.Save);

        if(CurrentMode == WorldMode.WorldSettings)
        {
            WorldSettingsViewWorldPanelScript.OnPanelSelected();
        }else if(CurrentMode == WorldMode.PlayerSettings)
        {

        } else if(CurrentMode == WorldMode.Load)
        {
            LoadGamesViewWorldPanelScript.OnPanelSelected();
        }else if(CurrentMode == WorldMode.Save)
        {
            SaveGamesViewWorldPanelScript.OnPanelSelected();
        }
    }
}
