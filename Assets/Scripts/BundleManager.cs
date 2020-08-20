using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;
using System;
using UnityEngine.Networking;

/// <summary>
/// Manages the loaded Bundles, downloads new bundles
/// Loads models, materials, etc.
/// </summary>
public class BundleManager : GenericSingleton<BundleManager>
{
    public delegate void BundleUpdate(Bundle modelBundle);
    public delegate void BundleUpdateWithAsset(AssetBundle assetBundle, Bundle modelBundle);
    public static event BundleUpdate OnBundleDownloaded;
    public static event BundleUpdateWithAsset OnBundleLoadedIntoMemory;

    /// <summary>
    /// The assetbundles that are currently loaded into ram
    /// </summary>
    private readonly Dictionary<string, AssetBundle> _bundleID2LoadedAssetBundles = new Dictionary<string, AssetBundle>();
    private readonly HashSet<string> _bundlesCurrentlyBeingLoadedIntoRAM = new HashSet<string>();
    private readonly HashSet<string> _bundlesCurrentlyBeingDownloaded = new HashSet<string>();
    private readonly HashSet<uint> _activeLoadReceipts = new HashSet<uint>();
    private readonly List<SceneMaterial> _sceneMaterialsBeingLoaded = new List<SceneMaterial>();

    private BundleDatabase _modelLocalDatabase;
    private uint _prevLoadReceipt;
    private string _bundleFileLocationFormat;
    const string BundleDownloadLocationFormat = "https://gamelodge-assets.s3.amazonaws.com/{0}";

    protected override void Awake()
    {
        base.Awake();
        _bundleFileLocationFormat = Application.persistentDataPath + "/{0}";
        _modelLocalDatabase = new BundleDatabase(_bundleFileLocationFormat);
    }
    public List<Bundle> GetDownloadedBundles()
    {
        return _modelLocalDatabase.GetAllBundles();
    }
    public bool HasDownloadedBundle(string bundleID)
    {
        return _modelLocalDatabase.Contains(bundleID);
    }
    private uint GetNewLoadReceipt()
    {
        const int maxTries = int.MaxValue;
        for(int i = 0; i < maxTries; i++)
        {
            _prevLoadReceipt++;
            if (_prevLoadReceipt == uint.MaxValue) // Max Value means none
                _prevLoadReceipt = 0;
            if (!_activeLoadReceipts.Contains(_prevLoadReceipt))
            {
                _activeLoadReceipts.Add(_prevLoadReceipt);
                //Debug.Log("Have " + _activeLoadReceipts.Count + " active receipts");
                return _prevLoadReceipt;
            }
        }
        Debug.LogError("Failed to find good load receipt!");
        throw new Exception("Out of load receipts!");
    }
    public bool CancelLoad(uint loadReceipt)
    {
        return _activeLoadReceipts.Remove(loadReceipt);
    }
    public void GetAllDownloadedBundleItemsOfType(List<BundleItem> bundleItems, SubBundle.SubBundleType bundleType, string requiredScript)
    {
        bundleItems.Clear();
        List<Bundle> bundles = _modelLocalDatabase.GetAllBundles();
        // Iterate through bundles to know how many we need to handle first
        if(bundleItems.Capacity == 0)
        {
            int numBundleItemsOfType = 0;
            for(int i = 0; i < bundles.Count; i++)
            {
                Bundle bundle = bundles[i];
                SubBundle subBundle = bundle.GetBundleForType(bundleType);
                numBundleItemsOfType += subBundle.BundleItems.Count;
            }
            bundleItems.Capacity = numBundleItemsOfType;
        }

        for(int i = 0; i < bundles.Count; i++)
        {
            Bundle bundle = bundles[i];
            SubBundle subBundle = bundle.GetBundleForType(bundleType);
            // Add the bundle items that fit our requirements
            if(requiredScript == null)
                bundleItems.AddRange(subBundle.BundleItems);
            else
            {
                for(int j = 0; j < subBundle.BundleItems.Count; j++)
                {
                    BundleItem bundleItem = subBundle.BundleItems[j];
                    if (bundleItem.AttachedScripts.Contains(requiredScript))
                        bundleItems.Add(bundleItem);
                }
            }
        }
    }
    /// <summary>
    /// Retrieves a model/prefab from a bundle. If the bundle is not in the database, then 
    /// it will be downloaded from S3
    /// </summary>
    /// <param name="bundleID"></param>
    /// <param name="bundleIndex"></param>
    public uint LoadGameObjectFromBundle(string bundleID, ushort bundleIndex, Transform parent, int loadID, Action<int, GameObject> onDone, Action<int, BundleItem> onBundleItemLoaded=null)
    {
        if(bundleID == BuiltinAssetManager.BuiltinBundleID)
        {
            GameObject newInst = BuiltinAssetManager.Instance.InstantiateObjectFromBundleIndex(bundleIndex, parent, out BundleItem bundleItem);
            if (onBundleItemLoaded != null)
                onBundleItemLoaded(loadID, bundleItem);
            if(onDone != null)
                onDone(loadID, newInst);
            return uint.MaxValue;
        }
        //Debug.Log("Will (GameObject) load #" + bundleIndex + " from " + bundleID);
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadItemFromBundleRoutine<GameObject>(bundleID, bundleIndex, parent, loadID, receipt, true, onDone, onBundleItemLoaded));
        return receipt;
    }
    public uint LoadGameObjectFromBundle(BundleItem bundleItem, Transform parent, int loadID, Action<int, GameObject> onDone)
    {
        //Debug.Log("Will (GameObject) load #" + bundleItem. + " from " + bundleID);
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadItemFromBundleRoutine<GameObject>(bundleItem, parent, loadID, receipt, true, onDone));
        return receipt;
    }
    /// <summary>
    /// Retrieves an audioclip from a bundle. If the bundle is not in the database, then 
    /// it will be downloaded from S3
    /// </summary>
    /// <param name="bundleID"></param>
    /// <param name="bundleIndex"></param>
    public uint LoadAudioClipFromBundle(string bundleID, ushort bundleIndex, int loadID, Action<int, AudioClip> onDone)
    {
        //Debug.Log("Will (audioclip) load #" + bundleIndex + " from " + bundleID);
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadItemFromBundleRoutine<AudioClip>(bundleID, bundleIndex, null, loadID, receipt, true, onDone));
        return receipt;
    }
    public uint LoadItemFromBundle<T>(string bundleID, ushort bundleIndex, int loadID, Action<int, T> onDone) where T : class
    {
        //Debug.Log("Will (" + (typeof(T).ToString()) + ") load #" + bundleIndex + " from " + bundleID);
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadItemFromBundleRoutine<T>(bundleID, bundleIndex, null, loadID, receipt, true, onDone));
        return receipt;
    }
    public uint LoadBundleItem(string bundleID, ushort bundleIndex, int loadID, Action<int, BundleItem> onDone)
    {
        //Debug.Log("Will get bundle item " + bundleID + " #" + bundleIndex);
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadBundleItemRoutine(bundleID, bundleIndex, loadID, receipt, true, onDone));
        return receipt;
    }
    public void DownloadBundle(string bundleID, Action<Bundle> onRecvModelBundle, Action<float> downloadProgress = null)
    {
        StartCoroutine(DownloadBundleRoutine(bundleID, onRecvModelBundle, downloadProgress));
    }
    private IEnumerator DownloadBundleRoutine(string bundleID, Action<Bundle> onRecvModelBundle, Action<float> downloadProgress=null)
    {
        if(bundleID == BuiltinAssetManager.BuiltinBundleID)
        {
            if (onRecvModelBundle != null)
                onRecvModelBundle(BuiltinAssetManager.Instance.BuiltinBundle);
            yield break;
        }
        if (downloadProgress != null)
            downloadProgress(0);
        // Put in a fence, so that we don't download the same bundle multiple times
        while (_bundlesCurrentlyBeingDownloaded.Contains(bundleID))
            yield return null;
        // See if the model is in the database
        Bundle modelBundle;
        string bundleLocalFilePath = Application.streamingAssetsPath + "/" + bundleID;
        if (_modelLocalDatabase.TryGetBundle(bundleID, out modelBundle))
        {
            //Debug.Log("Not downloading, we already have the bundle \"" + bundleID + "\" in the DB");
            if (onRecvModelBundle != null)
                onRecvModelBundle(modelBundle);
            yield break;
        }
        _bundlesCurrentlyBeingDownloaded.Add(bundleID);

        string infoEndpoint = string.Format("{0}:{1}/get-assetbundle?id={2}", GLVars.Instance.APIAddress,
            GLVars.Instance.APIPort, bundleID);
        Debug.Log("Will download bundle info " + bundleID + " from " + infoEndpoint);
        UnityWebRequest webRequest = UnityWebRequest.Get(infoEndpoint);
        webRequest.SendWebRequest();

        // How the info portion of the download affects the progress reported
        const float InfoDownloadPortion = 0.1f;
        while (!webRequest.isDone)
        {
            if (downloadProgress != null)
                downloadProgress(webRequest.downloadProgress * InfoDownloadPortion);
            yield return null;
        }

        if (webRequest.isNetworkError || webRequest.isHttpError)
        {
            Debug.LogError("Error when downloading model info " + webRequest.error);
            if (onRecvModelBundle != null)
                onRecvModelBundle(null);
            _bundlesCurrentlyBeingDownloaded.Remove(bundleID);
            yield break;
        }
        string rawJson = webRequest.downloadHandler.text;
        webRequest.Dispose();

        string downloadEndpoint = string.Format(BundleDownloadLocationFormat, bundleID);
        UnityWebRequest fileRequest = UnityWebRequest.Get(downloadEndpoint);
        //TODO Download into a memory buffer, then unzip in memory and save the unzipped file to the file system
        // this will lead to faster load times, at the expense of more disk usage
        //TODO check if the output file path already exists, and delete it if so
        string savePath = string.Format(_bundleFileLocationFormat, bundleID);
        Debug.Log("Saving new file to: " + savePath);
        fileRequest.downloadHandler = new DownloadHandlerFile(savePath);
        fileRequest.SendWebRequest();
        while (!fileRequest.isDone)
        {
            if (downloadProgress != null)
                downloadProgress(InfoDownloadPortion + fileRequest.downloadProgress * (1 - InfoDownloadPortion));
            yield return null;
        }

        if (fileRequest.isNetworkError || fileRequest.isHttpError)
        {
            Debug.LogError("Error when downloading model file " + fileRequest.error);
            if (onRecvModelBundle != null)
                onRecvModelBundle(null);
            _bundlesCurrentlyBeingDownloaded.Remove(bundleID);
            yield break;
        }

        Debug.Log("bundle:");
        Debug.Log(rawJson);
        JObject json = JObject.Parse(rawJson);
        modelBundle = Bundle.FromJson(json);

        Debug.Log("Finished file dl for " + bundleID);
        _modelLocalDatabase.AddModelToDatabase(modelBundle, null);
        if (OnBundleDownloaded != null)
            OnBundleDownloaded(modelBundle);
        if (onRecvModelBundle != null)
            onRecvModelBundle(modelBundle);
        _bundlesCurrentlyBeingDownloaded.Remove(bundleID);
    }
    private IEnumerator LoadBundleToMemory(string bundleID, Action<AssetBundle> onLoadedAssetBundle)
    {
        // Make sure we only have one request to load an asset bundle at a time
        while (_bundlesCurrentlyBeingLoadedIntoRAM.Contains(bundleID))
            yield return null;

        // Load the assetbundle into memory, if we don't already have it loaded
        AssetBundle assetBundle = null;
        string bundleLocalFilePath = string.Format(_bundleFileLocationFormat, bundleID);
        if(_bundleID2LoadedAssetBundles.TryGetValue(bundleID, out assetBundle))
        {
            //Debug.Log("AssetBundle for " + bundleID + " already loaded");
            if (onLoadedAssetBundle != null)
                onLoadedAssetBundle(assetBundle);
            yield break;
        }

        //Debug.Log("Will load assetBundle " + bundleID + " into memory");
        _bundlesCurrentlyBeingLoadedIntoRAM.Add(bundleID);
        AssetBundleCreateRequest bundleLoadRequest = AssetBundle.LoadFromFileAsync(bundleLocalFilePath);
        yield return bundleLoadRequest;
        _bundlesCurrentlyBeingLoadedIntoRAM.Remove(bundleID);

        assetBundle = bundleLoadRequest.assetBundle;
        if (assetBundle == null)
        {
            Debug.Log("Failed to load AssetBundle!");
            if (onLoadedAssetBundle != null)
                onLoadedAssetBundle(null);
            yield break;
        }
        _bundleID2LoadedAssetBundles.Add(bundleID, assetBundle);
        if (onLoadedAssetBundle != null)
            onLoadedAssetBundle(assetBundle);
        if (OnBundleLoadedIntoMemory != null)
            OnBundleLoadedIntoMemory(assetBundle, _modelLocalDatabase.GetBundle(bundleID));
    }
    private IEnumerator LoadBundleItemRoutine(string bundleID, ushort bundleIndex, int loadID, uint receipt, bool clearReceipt, Action<int, BundleItem> onDone)
    {
        // Get the model, either from the DB, or from AWS
        Bundle bundle = null;
        yield return DownloadBundleRoutine(bundleID, (bun) => bundle = bun);
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadBundleItemRoutine cancelled");
            yield break;
        }

        if(bundle == null)
        {
            Debug.LogError("Download failed");
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(loadID, null);
            yield break;
        }
        // Get the address name for the model
        if(bundleIndex < 0 || bundleIndex > bundle.AllBundleItems.Count)
        {
            Debug.LogError("Improper model index " + bundleIndex + "/" + bundle.AllBundleItems.Count);
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(loadID, null);
            yield break;
        }
        if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
            Debug.LogError("Failed to remove receipt " + receipt);
        if (onDone != null)
            onDone(loadID, bundle.AllBundleItems[bundleIndex]);
    }
    private IEnumerator LoadItemFromBundleRoutine<T>(string bundleID, ushort bundleIndex, Transform parent, int loadID, uint receipt, bool clearReceipt, Action<int, T> onDone, Action<int, BundleItem> onBundleItemLoaded=null) where T : class
    {
        // Get the model, either from the DB, or from AWS
        BundleItem bundleItem = null;
        yield return LoadBundleItemRoutine(bundleID, bundleIndex, loadID, receipt, false, (junk, item) => bundleItem = item);

        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadItemFromBundleRoutine cancelled");
            yield break;
        }

        if (onBundleItemLoaded != null)
            onBundleItemLoaded(loadID, bundleItem);
        if (bundleItem == null)
        {
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(loadID, null);
            yield break;
        }
        yield return LoadItemFromBundleRoutine<T>(bundleItem, parent, loadID, receipt, false, onDone);
        if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
            Debug.LogError("Failed to remove receipt " + receipt);
    }
    private IEnumerator LoadItemFromBundleRoutine<T>(BundleItem bundleItem, Transform parent, int loadID, uint receipt, bool clearReceipt, Action<int, T> onDone) where T: class
    {
        // Load the assetBundle into memory
        AssetBundle assetBundle = null;
        yield return LoadBundleToMemory(bundleItem.ContainingSubBundle.ContainingBundle, (output) => assetBundle = output);
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadItemFromBundleRoutine cancelled");
            yield break;
        }

        if(assetBundle == null)
        {
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(loadID, null);
            yield break;
        }
        // Instantiate the prefab from the asset bundle
        string address = bundleItem.Address;
        //Debug.Log("Will load: " + address);
        //Debug.Log("Has: " + assetBundle.GetAllAssetNames()[0]);
        AssetBundleRequest assetLoadRequest = assetBundle.LoadAssetAsync<T>(address);
        yield return assetLoadRequest;
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadItemFromBundleRoutine cancelled");
            yield break;
        }

        if(assetLoadRequest.asset == null)
        {
            Debug.LogError("BundleItem " + address + " not present in " + bundleItem.ContainingSubBundle.ContainingBundle);
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(loadID, null);
            yield break;
        }

        T loadedAsset = assetLoadRequest.asset as T;
        if(typeof(T) == typeof(GameObject))
        {
            GameObject obj = Instantiate(loadedAsset as GameObject, parent);
            //Debug.Log("loaded obj with rot " + obj.transform.localRotation);
            obj.transform.localPosition = Vector3.zero;
            // We don't set the local rotation, because we want to use that rotation
            // in how we spawn it. When the SceneObject actually loads the object, then
            // it set's the child model to identity rotation
            //obj.transform.localRotation = Quaternion.identity;
            //Debug.Log("Instantiated " + obj.name);
            loadedAsset = obj as T;
        }
        //Debug.LogWarning("Instantiating #" + modelID + " from " + bundleID);
        if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
            Debug.LogError("Failed to remove receipt " + receipt);
        if (onDone != null)
            onDone(loadID, loadedAsset);
    }
    public uint LoadPreviewImage(string bundle, string modelAddress, Action<Texture2D> onDone)
    {
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadPreviewImageRoutine(bundle, modelAddress, receipt, onDone));
        return receipt;
    }
    private IEnumerator LoadPreviewImageRoutine(string bundleID, string modelAddress, uint receipt, Action<Texture2D> onDone)
    {
        // Get the model, either from the DB or from S3
        Bundle modelBundle = null;
        yield return DownloadBundleRoutine(bundleID, (bundle) => modelBundle = bundle);
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadPreviewImageRoutine cancelled");
            yield break;
        }

        if(modelBundle == null)
        {
            Debug.LogError("Download failed");
            if (!_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(null);
            yield break;
        }

        // Load the assetBundle into memory
        AssetBundle assetBundle = null;
        yield return LoadBundleToMemory(bundleID, (output) => assetBundle = output);

        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadPreviewImageRoutine cancelled");
            yield break;
        }

        if(assetBundle == null)
        {
            Debug.LogError("Loading bundle failed #" + bundleID);
            if (!_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(null);
            yield break;
        }

        string prevImageName = modelAddress + ".png";
        //Debug.Log("Will load " + prevImageName);
        AssetBundleRequest request = assetBundle.LoadAssetAsync<Texture2D>(prevImageName);
        while (!request.isDone)
            yield return null;
        if (!_activeLoadReceipts.Remove(receipt))
        {
            Debug.Log("LoadPreviewImageRoutine cancelled");
            yield break;
        }
        if(onDone != null)
            onDone(request.asset == null ? null : request.asset as Texture2D);
    }
    public IEnumerator LoadPreviewImages(string bundleID, List<string> models, Action<string, Texture2D> onDone)
    {
        // Get the model, either from the DB, or from Firebase
        Bundle modelBundle = null;
        yield return DownloadBundleRoutine(bundleID, (bundle) => modelBundle = bundle);
        if(modelBundle == null)
        {
            Debug.LogError("Download failed");
            if (onDone != null)
                onDone(null, null);
            yield break;
        }

        // Load the assetBundle into memory
        AssetBundle assetBundle = null;
        yield return LoadBundleToMemory(bundleID, (output) => assetBundle = output);

        if(assetBundle == null)
        {
            Debug.LogError("Loading bundle failed #" + bundleID);
            if (onDone != null)
                onDone(null, null);
            yield break;
        }
        //string[] allN = assetBundle.GetAllAssetNames();
        //foreach (string n in allN)
            //Debug.Log(n);

        AssetBundleRequest[] requests = new AssetBundleRequest[models.Count];
        bool[] hasNotified = new bool[models.Count];
        for(int i = 0; i < models.Count; i++)
        {
            string modelName = models[i];
            string prevImageName = modelName + ".png";
            //Debug.Log("Will load " + prevImageName);
            requests[i] = assetBundle.LoadAssetAsync<Texture2D>(prevImageName);
        }
        Debug.Log("Dispatched all image requests, will now wait until all complete");
        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            for(int i = 0; i < requests.Length; i++)
            {
                if (!requests[i].isDone)
                {
                    allDone = false;
                    continue;
                }

                if (hasNotified[i])
                    continue;

                if(onDone != null)
                    onDone(models[i], requests[i].asset == null ? null : requests[i].asset as Texture2D);
                hasNotified[i] = true;
            }

            if (!allDone)
                yield return null;
        }
        Debug.Log("All preview loads done");
    }
    public void GetMaterialInfosFromBundle(SceneObject sceneObject, Action<SceneObject, MaterialInfo[]> onDone)
    {
        StartCoroutine(GetMaterialInfosFromBundleRoutine(sceneObject, onDone));
    }
    private IEnumerator GetMaterialInfosFromBundleRoutine(SceneObject sceneObject, Action<SceneObject, MaterialInfo[]> onDone)
    {
        // Get the model, either from the DB, or from AWS
        Bundle bundle = null;
        yield return DownloadBundleRoutine(sceneObject.BundleID, (bun) => bundle = bun);

        if(bundle == null)
        {
            Debug.LogError("Download failed");
            if (onDone != null)
                onDone(sceneObject, null);
            yield break;
        }

        if(sceneObject.BundleIndex >= bundle.AllBundleItems.Count)
        {
            Debug.LogError("Wrong model index! " + sceneObject.BundleIndex + " out of " + bundle.AllBundleItems.Count);
            if (onDone != null)
                onDone(sceneObject, null);
            yield break;
        }

        if (onDone != null)
        {
            List<int> indexes = bundle.AllBundleItems[sceneObject.BundleIndex].MaterialIndexes;
            MaterialInfo[] materialInfos = new MaterialInfo[indexes.Count];
            for (int i = 0; i < indexes.Count; i++)
                materialInfos[i] = bundle.MaterialInfos[indexes[i]];

            onDone(sceneObject, materialInfos);
        }
    }
    public uint LoadMaterial(SceneMaterial sceneMaterial, Action<Material> onDone)
    {
        if(sceneMaterial.LoadedMaterial != null)
        {
            if (onDone != null)
                onDone(sceneMaterial.LoadedMaterial);
            return uint.MaxValue;
        }
        uint receipt = GetNewLoadReceipt();
        StartCoroutine(LoadMaterialRoutine(sceneMaterial, receipt, true, onDone));
        return receipt;
    }
    private IEnumerator LoadMaterialRoutine(SceneMaterial sceneMaterial, uint receipt, bool clearReceipt, Action<Material> onDone)
    {
        Bundle bundle = null;
        yield return DownloadBundleRoutine(sceneMaterial.MaterialInfo.BundleID, (bun) => bundle = bun);
        if(bundle == null)
        {
            Debug.LogError("Download material failed");
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(null);
            yield break;
        }
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadMaterialRoutine cancelled");
            yield break;
        }

        // Load the assetBundle into memory
        AssetBundle assetBundle = null;
        yield return LoadBundleToMemory(sceneMaterial.MaterialInfo.BundleID, (output) => assetBundle = output);
        if(assetBundle == null)
        {
            Debug.LogError("Loading bundle failed #" + sceneMaterial.MaterialInfo.BundleID);
            if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
                Debug.LogError("Failed to remove receipt " + receipt);
            if (onDone != null)
                onDone(null);
            yield break;
        }
        if (!_activeLoadReceipts.Contains(receipt))
        {
            Debug.Log("LoadMaterialRoutine cancelled");
            yield break;
        }

        // In principle, this could be removed. SceneMaterial should only
        // call LoadMaterial once. We keep this in just for safety
        while (_sceneMaterialsBeingLoaded.Contains(sceneMaterial))
            yield return null;

        if(sceneMaterial.LoadedMaterial != null)
        {
            if (onDone != null)
                onDone(sceneMaterial.LoadedMaterial);
            yield break;
        }

        Debug.Log("Loading material from " + sceneMaterial.MaterialInfo.Address);
        _sceneMaterialsBeingLoaded.Add(sceneMaterial);
        AssetBundleRequest request = assetBundle.LoadAssetAsync<Material>(sceneMaterial.MaterialInfo.Address);
        yield return request;
        _sceneMaterialsBeingLoaded.Remove(sceneMaterial);

        if(request.asset == null)
        {
            //TODO
            Debug.LogError("Failed to load material " + sceneMaterial.MaterialInfo.Address);
            var names = assetBundle.GetAllAssetNames();
            foreach(var name in names)
                Debug.Log("Does have: " + name);
            if (clearReceipt)
                _activeLoadReceipts.Remove(receipt);
            if (onDone != null)
                onDone(null);
            yield break;
        }
        if (clearReceipt && !_activeLoadReceipts.Remove(receipt))
            Debug.LogError("Failed to remove receipt " + receipt);
        Material mat = request.asset as Material;
        if(onDone != null)
            onDone(mat);
    }

#if UNITY_EDITOR
    private void OnApplicationQuit()
    {
        // Unload all loaded assets
        foreach(var kvp in _bundleID2LoadedAssetBundles)
        {
            var asset = kvp.Value;
            asset.Unload(true);
        }
        _bundleID2LoadedAssetBundles.Clear();

        if(_modelLocalDatabase != null)
            _modelLocalDatabase.Dispose();
        _modelLocalDatabase = null;
    }
#endif
    //void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.L))
    //    {
    //        Debug.Log("Will load bundle");
    //        string bundleID = "-LnLDWCFJh0TuyORv-w_";
    //        int modelID = 0;
    //        LoadModelFromBundle(bundleID, modelID, null, tmp_done);
    //    }
    //}
}
