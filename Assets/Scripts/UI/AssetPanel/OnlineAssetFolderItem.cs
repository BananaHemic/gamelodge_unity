using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;

public class OnlineAssetFolderItem : BaseAssetFolderItem
{
    public Slider ProgressBar;
    private bool _isDownloading;

    public override void Init(BundleMetaData metaData)
    {
        base.Init(metaData);
        ProgressBar.gameObject.SetActive(false);
        base._button.interactable = !BundleManager.Instance.HasDownloadedBundle(_metaData.ID);
    }
    public void OnClicked()
    {
        if (_isDownloading)
        {
            Debug.Log("Dropping OnlineAssetFolder click, we're downloading");
            return;
        }
        Debug.Log("Clicked online asset bundle for " + _metaData.ID);
        // Download the asset bundle
        BundleManager.Instance.DownloadBundle(_metaData.ID, OnAssetBundleDownloaded, OnDownloadProgress);
        _isDownloading = true;
    }
    void OnAssetBundleDownloaded(Bundle modelBundle)
    {
        ProgressBar.gameObject.SetActive(false);
        // We now have this in the local hard drive, so we can make this non-interactable
        _button.interactable = false;
    }
    void OnDownloadProgress(float progress)
    {
        ProgressBar.gameObject.SetActive(true);
        ProgressBar.value = progress;
    }
}
