using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class AssetPanel : BasePanel<AssetPanel>
{
    /// <summary>
    /// Where items that are being dragged into the scen
    /// should be parented to
    /// </summary>
    public RectTransform ItemDragContainer;
    public RectTransform AssetTopBarButtonContainer;
    public TextMeshProUGUI AssetTopBarHeader;
    public TextMeshProUGUI NoDownloadedAssetsText;
    public ScrollRect AssetScrollView;
    public RectTransform ItemContainer;
    public GameObject LoadingIcon;
    public RectTransform AddAssetsIcon;
    public RectTransform CancelAssetsIcon;
    public DirectoryButtons TopDirectoryButtons;
    public GridLayoutGroup CenterGrid;

    public GameObject OnlineAssetFolderItemPrefab;
    public GameObject LocalAssetFolderItemPrefab;
    public GameObject AssetItemPrefab;
    public GameObject AssetFolderPrefab;

    public AssetMode CurrentMode { get; private set; }
    private Coroutine _listOnlineBundles;
    private readonly List<OnlineAssetFolderItem> _onlineAssetFolderItems = new List<OnlineAssetFolderItem>();
    private readonly List<LocalAssetFolderItem> _localAssetFolderItems = new List<LocalAssetFolderItem>();
    private readonly List<GameObject> _modelFolderItems = new List<GameObject>();

    const string ListAssetBundlesEndpoint = "list-assetbundle";
    const string LocalAssetsHeaderText = "Downloaded Bundles";
    const string OnlineBundlesHeaderText = "Online Bundles";
    private readonly Vector2 BundleGridDimensions = new Vector2(100f, 56.25f);
    private readonly Vector2 ItemGridDimensions = new Vector2(56.25f, 56.25f);
    /// <summary>
    /// We place a dummy transform in the scroll hierarchy
    /// so that the grid is preserved
    /// </summary>
    private RectTransform _dummyChild;
    /// <summary>
    /// The list of folders that we currently have open. So if we're in
    /// Prefabs/Items/Props
    /// The list would be those three strings
    /// </summary>
    private readonly List<string> _currentFolders = new List<string>();
    private Bundle _bundle;
    private bool _hasRefreshed = false;

    public enum AssetMode
    {
        LocalAssetBundles, // Looking at the downloaded asset bundles
        InsideAssetBundle, // Looking at the folders inside an asset bundle
        OnlineAssetBundles // Looking at online asset bundles
    }

    void Start()
    {
        if(!_hasRefreshed)
            RefreshForMode();
    }

    public void OpenAssetBundle(Bundle modelBundle)
    {
        CurrentMode = AssetMode.InsideAssetBundle;
        RefreshForMode();
        _bundle = modelBundle;
        _currentFolders.Clear();
        RefreshInsideAssetListing();
    }

    private IEnumerator SetButtons()
    {
        // There's a bug where the buttons don't display well so... we turn it off and on again...
        // Thanks Unity!

        TopDirectoryButtons.ClearButtons();
        //yield return null;
        var b = TopDirectoryButtons.AddButton("All Bundles", AllBundlesTopButtonClicked);
        var b2 = TopDirectoryButtons.AddButton(_bundle.Name, BundleFolderTopButtonClicked);
        foreach (var folder in _currentFolders)
            TopDirectoryButtons.AddButton(folder, BundleFolderTopButtonClicked);

        //yield return new WaitForEndOfFrame();
        yield return null;
        b.SetActive(false);
        b2.SetActive(false);
        yield return null;
        b.SetActive(true);
        b2.SetActive(true);
    }
    private void RefreshInsideAssetListing()
    {
        // Refresh the top directory buttons
        //TopDirectoryButtons.ClearButtons();
        //TopDirectoryButtons.AddButton("All Bundles", AllBundlesTopButtonClicked);
        //TopDirectoryButtons.AddButton(_bundle.Name, BundleFolderTopButtonClicked);
        //foreach (var folder in _currentFolders)
        //    TopDirectoryButtons.AddButton(folder, BundleFolderTopButtonClicked);
        //AssetPanelTopBar.gameObject.SetActive(false);
        StartCoroutine(SetButtons());

        foreach (var obj in _modelFolderItems)
            SimplePool.Instance.DespawnUI(obj);
        _modelFolderItems.Clear();

        List<ModelTreeElement> itemsInCurrentFolder = _bundle.GetElementsInFolder(_currentFolders);
        //for(int i = 0; i < itemsInCurrentFolder.Count; i++)
        for(int i = itemsInCurrentFolder.Count - 1; i >= 0; i--)
        {
            ModelTreeElement treeElement = itemsInCurrentFolder[i];

            if (treeElement.IsModel)
            {
                GameObject asset = SimplePool.Instance.SpawnUI(AssetItemPrefab, ItemContainer);
                _modelFolderItems.Add(asset);
                ModelFolderItem folderItem = asset.GetComponent<ModelFolderItem>();
                folderItem.Init(treeElement.Name, treeElement.BundleItem, treeElement.BundleIndex);
            }
            else
            {
                GameObject asset = SimplePool.Instance.SpawnUI(AssetFolderPrefab, ItemContainer);
                _modelFolderItems.Add(asset);
                ModelFolder folder = asset.GetComponent<ModelFolder>();
                folder.Init(treeElement.Name);
            }
        }
    }
    private void AllBundlesTopButtonClicked(string junk)
    {
        CurrentMode = AssetMode.LocalAssetBundles;
        RefreshForMode();
    }
    private void BundleFolderTopButtonClicked(string folderName)
    {
        // If we're already in this folder, don't bother doing anything
        if (_currentFolders.Count == 0 && folderName == "/")
            return;
        if (_currentFolders.Count > 0 && folderName == _currentFolders[_currentFolders.Count - 1])
            return;
        // Move up folders, until we hit the one listed
        while(_currentFolders.Count > 0 && _currentFolders[_currentFolders.Count - 1] != folderName)
            _currentFolders.RemoveAt(_currentFolders.Count - 1);
        // Now refresh the listing
        RefreshInsideAssetListing();
    }
    public void AssetFolderClicked(string folderName)
    {
        _currentFolders.Add(folderName);
        RefreshInsideAssetListing();
    }
    protected override void RefreshForMode()
    {
        _hasRefreshed = true;
        if (CurrentMode != AssetMode.OnlineAssetBundles)
        {
            if(_listOnlineBundles != null)
                StopCoroutine(_listOnlineBundles);
            _listOnlineBundles = null;

            for (int i = 0; i < _onlineAssetFolderItems.Count; i++)
                SimplePool.Instance.DespawnUI(_onlineAssetFolderItems[i].gameObject);
            _onlineAssetFolderItems.Clear();
        }
        if(CurrentMode != AssetMode.LocalAssetBundles)
        {
            for (int i = 0; i < _localAssetFolderItems.Count; i++)
                SimplePool.Instance.DespawnUI(_localAssetFolderItems[i].gameObject);
            _localAssetFolderItems.Clear();
        }
        if(CurrentMode != AssetMode.InsideAssetBundle)
        {
            TopDirectoryButtons.ClearButtons();
            for (int i = 0; i < _modelFolderItems.Count; i++)
                SimplePool.Instance.DespawnUI(_modelFolderItems[i].gameObject);
            _modelFolderItems.Clear();
            _currentFolders.Clear();
            _bundle = null;
        }

        if(CurrentMode == AssetMode.LocalAssetBundles)
        {
            AssetTopBarHeader.gameObject.SetActive(true);
            AssetTopBarHeader.text = LocalAssetsHeaderText;
            List<Bundle> modelBundles = BundleManager.Instance.GetDownloadedBundles();
            CancelAssetsIcon.gameObject.SetActive(false);
            AddAssetsIcon.gameObject.SetActive(true);
            LoadingIcon.gameObject.SetActive(false);
            CenterGrid.cellSize = BundleGridDimensions;
            if(modelBundles.Count == 0)
            {
                NoDownloadedAssetsText.gameObject.SetActive(true);
                AssetScrollView.gameObject.SetActive(false);
                return;
            }
            AssetScrollView.gameObject.SetActive(true);
            NoDownloadedAssetsText.gameObject.SetActive(false);

            for (int i = 0; i < _localAssetFolderItems.Count; i++)
                SimplePool.Instance.DespawnUI(_localAssetFolderItems[i].gameObject);
            _localAssetFolderItems.Clear();

            foreach(var modelBundle in modelBundles)
            {
                GameObject localBundleFolder = SimplePool.Instance.SpawnUI(LocalAssetFolderItemPrefab, ItemContainer);
                var localFolder = localBundleFolder.GetComponent<LocalAssetFolderItem>();
                _localAssetFolderItems.Add(localFolder);
                localFolder.Init(modelBundle);
            }
            //Debug.Log("We have " + modelBundles.Count + " model bundles downloaded");
        }
        else if(CurrentMode == AssetMode.OnlineAssetBundles)
        {
            AssetTopBarHeader.gameObject.SetActive(true);
            AssetTopBarHeader.text = OnlineBundlesHeaderText;
            CancelAssetsIcon.gameObject.SetActive(true);
            AddAssetsIcon.gameObject.SetActive(false);
            NoDownloadedAssetsText.gameObject.SetActive(false);
            AssetScrollView.gameObject.SetActive(true);
            CenterGrid.cellSize = BundleGridDimensions;

            if(_listOnlineBundles != null)
            {
                LoadingIcon.gameObject.SetActive(true);
                AssetScrollView.gameObject.SetActive(false);
            }
            else
            {
                LoadingIcon.gameObject.SetActive(false);
                AssetScrollView.gameObject.SetActive(true);
            }
        }else if (CurrentMode == AssetMode.InsideAssetBundle)
        {
            AssetTopBarHeader.gameObject.SetActive(false);
            CancelAssetsIcon.gameObject.SetActive(false);
            AddAssetsIcon.gameObject.SetActive(false);
            NoDownloadedAssetsText.gameObject.SetActive(false);
            AssetScrollView.gameObject.SetActive(true);
            CenterGrid.cellSize = ItemGridDimensions;
        }
    }
    public void CancelImageClicked()
    {
        if(CurrentMode == AssetMode.OnlineAssetBundles)
        {
            Debug.Log("Leaving online assets due to use cancel click");
            CurrentMode = AssetMode.LocalAssetBundles;
            RefreshForMode();
        }
    }

    public void ListOnlineAssetBundles()
    {
        CurrentMode = AssetMode.OnlineAssetBundles;
        if (_listOnlineBundles != null)
            Debug.Log("Dropping list online request, we have one outstanding");
        else
            _listOnlineBundles = StartCoroutine(ListOnlineBundlesRoutine());
        RefreshForMode();
    }
    IEnumerator ListOnlineBundlesRoutine()
    {
        string listEndpoint = string.Format("{0}:{1}/list-assetbundle", GLVars.Instance.APIAddress, GLVars.Instance.APIPort);
        UnityWebRequest request = UnityWebRequest.Get(listEndpoint);
        yield return request.SendWebRequest();
        Debug.Log("Got asset bundle list from " + listEndpoint);
        if (request.isNetworkError)
        {
            Debug.LogError("Network error when listing online bundles: " + request.error);
            _listOnlineBundles = null;
            yield break;
        }
        if (request.isHttpError)
        {
            Debug.LogError("HTTP error when listing online bundles: " + request.error);
            _listOnlineBundles = null;
            yield break;
        }
        for (int i = 0; i < _onlineAssetFolderItems.Count; i++)
            SimplePool.Instance.DespawnUI(_onlineAssetFolderItems[i].gameObject);
        _onlineAssetFolderItems.Clear();

        JArray allOnlineBundles = JArray.Parse(request.downloadHandler.text);

        Debug.Log("Listing " + allOnlineBundles.Count + " bundles");
        AssetScrollView.gameObject.SetActive(true);

        for(int i = 0; i < allOnlineBundles.Count; i++)
        {
            JToken onlineBundleToken = allOnlineBundles[i];
            BundleMetaData metaData = BundleMetaData.FromJson((JObject)onlineBundleToken);
            GameObject folderObj = SimplePool.Instance.SpawnUI(OnlineAssetFolderItemPrefab, ItemContainer);
            OnlineAssetFolderItem folderItem = folderObj.GetComponent<OnlineAssetFolderItem>();
            _onlineAssetFolderItems.Add(folderItem);
            folderItem.Init(metaData);
        }
        _listOnlineBundles = null;
        RefreshForMode();
    }
    public void GetDummyModelFolderItem(Transform dummyParent, int siblingIndex)
    {
        if(_dummyChild == null)
        {
            _dummyChild = new GameObject("dummy", typeof(RectTransform)).transform as RectTransform;
            _dummyChild.transform.SetParent(dummyParent, true);
        }
        else
        {
            _dummyChild.gameObject.SetActive(true);
        }
        _dummyChild.SetSiblingIndex(siblingIndex);
    }
    public void ReturnDummyModelFolderItem()
    {
        _dummyChild.gameObject.SetActive(false);
    }
}
