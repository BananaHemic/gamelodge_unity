using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ModelFolder : MonoBehaviour
{
    public TextMeshProUGUI FolderName;
    private string _folderName;

    public void Init(string folderName)
    {
        _folderName = folderName;
        FolderName.text = folderName;
    }
    public void OnClick()
    {
        AssetPanel.Instance.AssetFolderClicked(_folderName);
    }
}
