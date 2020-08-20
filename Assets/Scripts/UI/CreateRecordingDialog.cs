using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Text;

public class CreateRecordingDialog : CanvasToggleListener
{
    public Image TopButtonImage;
    public Button CreateRecordingButton;
    public TMP_Text CreateRecordingText;
    public TMP_Text TimeRecordingText;
    public TMP_InputField FilenameInput;
    public CanvasToggle CreateRecordingContainer;
    public CanvasToggleListener[] UIItemsToUpdate;
    public CanvasToggle[] CompetingContainers;

    private Coroutine _updateRecordingTime;
    private readonly StringBuilder _workingSb = new StringBuilder();

    void Awake()
    {
        SetMenuOpen(false);
    }
    public string GetRecordingFilename(out bool isFullPath)
    {
        string filename = FilenameInput.text;
        if (string.IsNullOrEmpty(filename))
        {
            // Default to the date/time
            filename = DateTime.Now.ToString("yyyy_MM_dd_HH꞉mm") + GameRecordingManager.RecordingFileExtension;
            isFullPath = false;
        }
        else
        {
            isFullPath = System.IO.Path.IsPathRooted(filename);
            // Replace colon with a windows friendly char
            filename = filename.Replace(':', '꞉');
            // Add the file extension
            if (!filename.EndsWith(GameRecordingManager.RecordingFileExtension))
                filename = filename + GameRecordingManager.RecordingFileExtension;
        }

        return filename;
    }
    public void RefreshFromRecordingStateChange(GameRecordingManager.RecordingState recordingState)
    {
        switch (recordingState)
        {
            case GameRecordingManager.RecordingState.None:
                TopButtonImage.color = CreateRecordingButton.colors.normalColor;
                CreateRecordingButton.interactable = true;
                CreateRecordingText.text = "Create Recording";
                break;
            case GameRecordingManager.RecordingState.Recording:
                TopButtonImage.color = CreateRecordingButton.colors.normalColor;
                CreateRecordingButton.interactable = true;
                CreateRecordingText.text = "End Recording";
                break;
            case GameRecordingManager.RecordingState.PlayingRecording:
            case GameRecordingManager.RecordingState.ServerUserPlayingRecording:
                TopButtonImage.color = CreateRecordingButton.colors.disabledColor;
                CreateRecordingButton.interactable = false;
                //CreateRecordingText.text = "Create Recording";
                break;
        }
    }
    public override void RefreshFromCanvasToggleChange()
    {
        // Don't bother if we're already off
        if (!CreateRecordingContainer.IsVisible)
            return;
        // If any competing containers are on, turn ourself off
        bool areAnyCompetitorsOn = false;
        for(int i = 0; i < CompetingContainers.Length; i++)
        {
            if (CompetingContainers[i].IsVisible)
            {
                areAnyCompetitorsOn = true;
                break;
            }
        }
        if(areAnyCompetitorsOn)
            CreateRecordingContainer.SetOn(false);
    }
    public void SetMenuOpen(bool isOpen)
    {
        CreateRecordingContainer.SetOn(isOpen);
        for(int i = 0; i < UIItemsToUpdate.Length; i++)
            UIItemsToUpdate[i].RefreshFromCanvasToggleChange();
    }
    public void OnOpenCreateRecordingMenuClicked()
    {
        if (CreateRecordingContainer.IsVisible)
            SetMenuOpen(false);
        else
            SetMenuOpen(true);
    }
    public void StartRecording()
    {
        if(_updateRecordingTime != null)
        {
            Debug.LogError("Double creation of recording updater!");
            return;
        }
        _updateRecordingTime = StartCoroutine(RecordingDisplay());
    }
    private IEnumerator RecordingDisplay()
    {
        float startTime = TimeManager.Instance.RenderTime;
        int numFramesSinceLastSecondChange = 1;
        int lastSec = 0;

        while (true)
        {
            float deltaTime = TimeManager.Instance.RenderTime - startTime;
            int deltaSec = Mathf.FloorToInt(deltaTime);
            int numHours = deltaSec / 3600;
            int numMinutes = (deltaSec % 3600) / 60;
            int numSeconds = deltaSec % 60;
            //float milliseconds = (deltaTime - deltaSec) * 1000f;

            if (numSeconds == lastSec)
                numFramesSinceLastSecondChange++;
            else
            {
                lastSec = numSeconds;
                numFramesSinceLastSecondChange = 0;
            }
            _workingSb.Clear();
            _workingSb.Append(numHours.ToString("00"));
            _workingSb.Append(":");
            _workingSb.Append(numMinutes.ToString("00"));
            _workingSb.Append(":");
            _workingSb.Append(numSeconds.ToString("00"));
            _workingSb.Append(":");
            //_workingSb.Append(milliseconds.ToString("000"));
            _workingSb.Append(numFramesSinceLastSecondChange.ToString("00"));

            TimeRecordingText.text = _workingSb.ToString();
            yield return null;
        }
    }
    public void EndRecording()
    {
        if (_updateRecordingTime != null)
            StopCoroutine(_updateRecordingTime);
        _updateRecordingTime = null;
        //TimeRecordingText.text = "00:00:00";
    }
    public void OnOpenFileExplorerClicked()
    {
        if(VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            Debug.LogWarning("File explorer in VR currently not supported");
            return;
        }

        const string FileExplorerTitle = "Select Save File";
        Crosstales.FB.FileBrowser.SaveFileAsync(OnFileSelected, FileExplorerTitle, GameRecordingManager.Instance.RecordingFolderPath, "recording", GameRecordingManager.RecordingFileExtensions);
    }
    private void OnFileSelected(string selectedFile)
    {
        Debug.Log("File selected");
        if (string.IsNullOrEmpty(selectedFile))
            return;
        Debug.Log("Selected " + selectedFile);
        FilenameInput.text = selectedFile;
    }
}
