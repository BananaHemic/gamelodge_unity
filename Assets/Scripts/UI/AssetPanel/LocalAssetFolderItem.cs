using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;

public class LocalAssetFolderItem : BaseAssetFolderItem
{
    private Bundle _modelBundle;

    public void Init(Bundle modelBundle)
    {
        _modelBundle = modelBundle;
        base.Init(modelBundle.MetaData);
    }
    public void OnClicked()
    {
        Debug.Log("Clicked local asset bundle for " + _metaData.ID);
        AssetPanel.Instance.OpenAssetBundle(_modelBundle);
    }
}
