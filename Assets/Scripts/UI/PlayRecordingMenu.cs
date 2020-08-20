using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class PlayRecordingMenu : CanvasToggleListener
{
    public Image TopButtonImage;
    public Button PlayRecordingButton;
    public Button StopRecordingButton;
    public Button RestartRecordingButton;
    public TMP_Text RecordingFilenameText;
    public Toggle PreserveScriptsToggle;
    public Toggle PreserveObjectsToggle;
    public CanvasToggle RevertDialogContainer;
    public CanvasToggleListener[] UIItemsToUpdate;
    public CanvasToggle[] CompetingContainers;

    const char ColonChar = ':';
    const char SpecialColonChar = '꞉';

    void Awake()
    {
        SetMenuOpen(false);
    }
    public bool GetShouldKeepScriptChanges()
    {
        return PreserveScriptsToggle.isOn;
    }
    public void SetRecordingFilename(string filename)
    {
        // Don't list the top directory if it's in the
        // normal spot
        if (filename.StartsWith(GameRecordingManager.Instance.RecordingFolderPath))
            filename = filename.Substring(GameRecordingManager.Instance.RecordingFolderPath.Length + 1);
        // Take out the weird : character
        RecordingFilenameText.text = filename.Replace(SpecialColonChar, ColonChar);
    }
    public string GetRecordingFilename(out bool isFullPath)
    {
        string filename = RecordingFilenameText.text;
        if (string.IsNullOrEmpty(filename))
        {
            // Look in the recording file, and just play the most recent one
            var directory = new DirectoryInfo(GameRecordingManager.Instance.RecordingFolderPath);
            // No recording file, and nothing selected, just return null
            if (!directory.Exists)
            {
                isFullPath = false;
                return null;
            }
            // Get the latest by write date
            FileInfo latestRecording = directory.GetFiles()
             .OrderByDescending(f => f.LastWriteTime)
             .First();

            Debug.Log("Playing " + latestRecording.Name);
            SetRecordingFilename(latestRecording.Name);
            isFullPath = true;
            return latestRecording.FullName;
        }
        else
        {
            isFullPath = System.IO.Path.IsPathRooted(filename);
            //Debug.Log("path rooted " + isFullPath);
            //Debug.Log(filename);
            // Add the file extension
            if (!filename.EndsWith(GameRecordingManager.RecordingFileExtension))
                filename = filename + GameRecordingManager.RecordingFileExtension;
            // Replace colon with a windows friendly char
            // But make sure not to fuck with the driver ID
            filename = filename.ReplaceAfter(ColonChar, SpecialColonChar, "C:\"".Length);
        }

        return filename;
    }
    public void RefreshFromRecordingStateChange(GameRecordingManager.RecordingState recordingState)
    {
        switch (recordingState)
        {
            case GameRecordingManager.RecordingState.None:
                PlayRecordingButton.gameObject.SetActive(true);
                StopRecordingButton.gameObject.SetActive(false);
                RestartRecordingButton.gameObject.SetActive(false);
                PlayRecordingButton.interactable = true;
                TopButtonImage.color = PlayRecordingButton.colors.normalColor;
                break;
            case GameRecordingManager.RecordingState.Recording:
            case GameRecordingManager.RecordingState.ServerUserPlayingRecording:
                PlayRecordingButton.gameObject.SetActive(true);
                StopRecordingButton.gameObject.SetActive(false);
                RestartRecordingButton.gameObject.SetActive(false);
                PlayRecordingButton.interactable = false;
                TopButtonImage.color = PlayRecordingButton.colors.disabledColor;
                break;
            case GameRecordingManager.RecordingState.PlayingRecording:
                PlayRecordingButton.gameObject.SetActive(false);
                StopRecordingButton.gameObject.SetActive(true);
                RestartRecordingButton.gameObject.SetActive(true);
                PlayRecordingButton.interactable = true;
                TopButtonImage.color = PlayRecordingButton.colors.normalColor;
                break;
        }
    }
    public override void RefreshFromCanvasToggleChange()
    {
        // Don't bother if we're already off
        if (!RevertDialogContainer.IsVisible)
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
            RevertDialogContainer.SetOn(false);
    }
    public void SetMenuOpen(bool isOpen)
    {
        RevertDialogContainer.SetOn(isOpen);
        for(int i = 0; i < UIItemsToUpdate.Length; i++)
            UIItemsToUpdate[i].RefreshFromCanvasToggleChange();
    }
    public void OnOpenRevertMenuClicked()
    {
        if (RevertDialogContainer.IsVisible)
            SetMenuOpen(false);
        else
            SetMenuOpen(true);
    }
    public void OnOpenFileExplorerClicked()
    {
        if(VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            Debug.LogWarning("File explorer in VR currently not supported");
            return;
        }

        const string FileExplorerTitle = "Select Record To Open";
        //Crosstales.FB.FileBrowser.OpenFilesAsync(OnFileSelected, FileExplorerTitle, GameRecordingManager.Instance.RecordingFolderPath, false, GameRecordingManager.RecordingFileExtensions);
        Crosstales.FB.FileBrowser.OpenFilesAsync(OnFileSelected, FileExplorerTitle, GameRecordingManager.Instance.RecordingFolderPath, true, GameRecordingManager.RecordingFileExtensions);
    }
    private void OnFileSelected(string[] selectedFiles)
    {
        //Debug.Log("File selected");
        if (selectedFiles == null || selectedFiles.Length == 0)
            return;
        //Debug.Log("Num selected " + selectedFiles.Length);
        Debug.Log("Selected " + selectedFiles[0]);
        SetRecordingFilename(selectedFiles[0]);
        if (GameRecordingManager.Instance.CurrentState == GameRecordingManager.RecordingState.PlayingRecording)
        {
            Debug.Log("Ending play recording, we opened a new file");
            GameRecordingManager.Instance.StopPlayingRecording(false);
        }
    }
}
