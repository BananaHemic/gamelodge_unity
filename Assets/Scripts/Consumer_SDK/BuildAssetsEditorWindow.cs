using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
#if UNITY_EDITOR
using UnityEngine;
using EditorCoroutines;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class BuildAssetsEditorWindow : EditorWindow
{
    private Bundle _modelBundle;
    private List<string> _allPaths;
    private List<string> _allAddresses;
    private List<string> _previewFilePaths;

    private Stage _currentStage = Stage.PendingInit;
    private AssetBundleBuild _build;
    const double TimeWaitingAfterPlayMode = 5.0;
    private bool _useProduction;
    private string _url;
    private string _previewUrl;
    private string _filePath;
    private string _lastError;
    private string _bundlePreviewImageLocation;
    private bool _hasInit = false;
    const string OutputFolder = "Bundles/";

    private enum Stage
    {
        PendingInit,
        Import,
        Build,
        GetUploadUrl,
        UploadBundle,
        UploadPreview,
        NotifyAPIUploadDone,
        Cleanup,
        Done,
        Error,
        NoBuildRequested
    }

    public static void Create(Bundle modelBundle, List<string> allPaths, List<string> allAddresses, List<string> previewFilePaths, string bundlePreviewImageLocation, bool useProd)
    {
        //Debug.Log("Create");
        // Get existing open window or if none, make a new one:
        BuildAssetsEditorWindow window = (BuildAssetsEditorWindow)EditorWindow.GetWindow(typeof(BuildAssetsEditorWindow));
        window._modelBundle = modelBundle;
        window._allPaths = allPaths;
        window._allAddresses = allAddresses;
        window._previewFilePaths = previewFilePaths;
        window._bundlePreviewImageLocation = bundlePreviewImageLocation;
        window._useProduction = useProd;
        window._hasInit = true;
        window.Show();
        //Debug.Log("Create done");
    }

    void Awake()
    {
        //Debug.Log("Editor awake");
        this.StartCoroutine(BuildAndUpload());
        //Debug.Log("Preprocessing done, will now begin proper upload");
    }
    bool Import()
    {
        // We need to import the images
        int assetDirLen = Application.dataPath.Length - "Assets".Length;
        foreach(var imgPath in _previewFilePaths)
        {
            if (imgPath == null)
                continue;
            string directoryRelativeToAssets = imgPath.Substring(assetDirLen);
            //Debug.Log("Importing: " + directoryRelativeToAssets);
            AssetDatabase.ImportAsset(directoryRelativeToAssets, ImportAssetOptions.Default);
        }
        return true;
    }
    bool Build()
    {
        Debug.Log("Will now import");
        //foreach (string path in _allPaths)
            //Debug.Log("Paths: " + path);
        //foreach (string address in _allAddresses)
            //Debug.Log("Address: " + address);
        int assetDirLen = Application.dataPath.Length - "Assets".Length;
        List<string> allPathsRelAssets = new List<string>();
        List<string> allNonNullAddresses = new List<string>();
        for (int i = 0; i < _allPaths.Count; i++)
        {
            if (_allPaths[i] == null)
                continue;
            string relPath = _allPaths[i].Substring(assetDirLen);
            allPathsRelAssets.Add(relPath);
            allNonNullAddresses.Add(_allAddresses[i]);
            Debug.Log("Address: " + _allAddresses[i]);
        }

        _build = new AssetBundleBuild
        {
            assetBundleName = _modelBundle.ID,
            assetBundleVariant = "",
            assetNames = allPathsRelAssets.ToArray(),
            addressableNames = allNonNullAddresses.ToArray()
        };
        // Build the asset bundle, including images
        //int projectDirEnd = Application.dataPath.LastIndexOf('/') + 1;
        //int projectDirEnd = Application.dataPath.LastIndexOf('/');
        //string outputFilePath = Application.dataPath.Substring(0, projectDirEnd) + bundleID + ".bundle";
        //string outputFilePath = Application.dataPath.Substring(0, projectDirEnd) + OutputFolder;
        //string outputFilePath = Application.dataPath.Substring(0, projectDirEnd);
        string outputFilePath = OutputFolder;
        //string outputFilePath = "Assets/Output";
        Debug.Log("Saving to " + outputFilePath);
        AssetBundleBuild[] builds = new AssetBundleBuild[1];
        builds[0] = _build;
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputFilePath, builds, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        Debug.Log("Build done");
        if(manifest == null)
        {
            Debug.LogError("Build asset bundle failed");
            _lastError = "Building Asset Bundle failed";
            return false;
        }
        _filePath = outputFilePath + _build.assetBundleName;
        Debug.Log("Has build manifest, to: " + _filePath);
        return true;
    }
    void Cleanup()
    {
        // Remove all the preview images (so that they don't crowd shit)
        Debug.Log("Will now remove all preview images");
        int assetDirLen = Application.dataPath.Length - "Assets".Length;
        foreach(string imgPath in _previewFilePaths)
        {
            if (imgPath == null)
                continue;
            //File.Delete(previewImagePath);
            string directoryRelativeToAssets = imgPath.Substring(assetDirLen);
            //Debug.Log("Removing: " + directoryRelativeToAssets);
            AssetDatabase.DeleteAsset(directoryRelativeToAssets);
        }
    }
    private IEnumerator GetUploadUrl()
    {
        _url = null;
        _previewUrl = null;
        string hostName =  _useProduction
            ? "http://api.gonzo-vr.com:8080/get-upload-assetbundle-url"
            : "http://127.0.0.1:8080/get-upload-assetbundle-url";
        //WWWForm form = new WWWForm();
        //_modelBundle.MetaData.ApplyToForm(form);
        string modelJson = _modelBundle.ToJson(false).ToString();
        //form.AddField("json", _modelBundle.ToJson(false).ToString());
        List<IMultipartFormSection> sections = new List<IMultipartFormSection>();
        _modelBundle.MetaData.ApplyToForm(sections);
        sections.Add(new MultipartFormDataSection("json", modelJson));
        //UnityWebRequest webReq = UnityWebRequest.Post(hostName, form);
        UnityWebRequest webReq = UnityWebRequest.Post(hostName, sections);
        //webReq.SetRequestHeader("Content-Type", "multipart/form-data");

        yield return webReq.SendWebRequest();

        if (webReq.isNetworkError)
        {
            Debug.LogError("Network error when trying to upload asset: " + webReq.error);
            _lastError = "Network Issue When Retriving Upload URL: " + webReq.error;
            yield break;
        }
        if (webReq.isHttpError)
        {
            Debug.LogError("HTTP error when trying to upload asset: " + webReq.error);
            Debug.LogError(webReq.downloadHandler.text);
            _lastError = "HTTP Response When Retriving Upload URL: " + webReq.downloadHandler.text;
            yield break;
        }

        // Parse out the provided url to upload to
        Debug.Log(webReq.downloadHandler.text);
        JObject json = JObject.Parse(webReq.downloadHandler.text);

        JToken urlToken;
        if(!json.TryGetValue("url", out urlToken))
        {
            Debug.LogError("Failed to parse url from api");
            yield break;
        }

        _url = urlToken.Value<string>();

        JToken previewUrlToken;
        if(!json.TryGetValue("previewUrl", out previewUrlToken))
        {
            Debug.LogError("Failed to parse preview url from api");
            yield break;
        }
        _previewUrl = previewUrlToken.Value<string>();

        Debug.Log("Will upload to " + _url);
    }
    private IEnumerator UploadFile()
    {
        //Debug.Log("Nothing to do for upload");
        //var stream = new FileStream(_filePath,
            //FileMode.Open, FileAccess.Read, FileShare.Read);

        //WWWForm uploadForm = new WWWForm();
        Debug.Log("Will read file bytes from " + _filePath);
        byte[] fileData = File.ReadAllBytes(_filePath);
        Debug.Log("Read " + fileData.Length + " bytes");
        //uploadForm.AddBinaryData("file", fileData);

        //UnityWebRequest uploadReq = new UnityWebRequest(_url, "PUT", uploadForm)
        UnityWebRequest uploadReq = UnityWebRequest.Put(_url, fileData);
        uploadReq.SetRequestHeader("x-amz-acl", "public-read"); // Make sure everyone can read this file
        yield return uploadReq.SendWebRequest();

        if (uploadReq.isNetworkError)
        {
            Debug.LogError("Network error when uploading file");
            Debug.LogError(uploadReq.error);
            _lastError = "Network Issue When Uploading File: " + uploadReq.error;
            yield break;
        }
        if (uploadReq.isHttpError)
        {
            Debug.LogError("HTTP error when uploading file");
            Debug.LogError(uploadReq.downloadHandler.text);
            _lastError = "HTTP Error: " + uploadReq.downloadHandler.text;
            yield break;
        }

        Debug.Log("Upload completed, response: " + uploadReq.downloadHandler.text);
    }
    private IEnumerator UploadPreviewImage()
    {
        Debug.Log("Will read file bytes from " + _bundlePreviewImageLocation);
        byte[] fileData = File.ReadAllBytes(_bundlePreviewImageLocation);
        Debug.Log("Read " + fileData.Length + " bytes");

        UnityWebRequest uploadReq = UnityWebRequest.Put(_previewUrl, fileData);
        uploadReq.SetRequestHeader("x-amz-acl", "public-read"); // Make sure everyone can read this file
        yield return uploadReq.SendWebRequest();

        if (uploadReq.isNetworkError)
        {
            Debug.LogError("Network error when uploading preview file");
            Debug.LogError(uploadReq.error);
            _lastError = "Network Issue When Uploading Preview File: " + uploadReq.error;
            yield break;
        }
        if (uploadReq.isHttpError)
        {
            Debug.LogError("HTTP error when uploading preview file");
            Debug.LogError(uploadReq.downloadHandler.text);
            _lastError = "HTTP Error (Preview): " + uploadReq.downloadHandler.text;
            yield break;
        }

        Debug.Log("Preview upload completed, response: " + uploadReq.downloadHandler.text);
    }

    private IEnumerator NotifyAPIAboutUpload()
    {
        // We want to tell the API that we successfully uploaded the model and preview image
        // This way it know not to show the file if the file isn't yet uploaded
        string hostName = _useProduction
            ? "http://api.gonzo-vr.com:8080/asset-upload-done"
            : "http://127.0.0.1:8080/asset-upload-done";
        WWWForm form = new WWWForm();
        form.AddField("id", _modelBundle.ID);
        UnityWebRequest webReq = UnityWebRequest.Post(hostName, form);
        yield return webReq.SendWebRequest();

        if (webReq.isNetworkError)
        {
            Debug.LogError("Network error when notifying api on done");
            Debug.LogError(webReq.error);
            _lastError = "Network Issue When Notfying API Upload Completed: " + webReq.error;
            yield break;
        }
        if (webReq.isHttpError)
        {
            Debug.LogError("HTTP error when notifying api on done");
            Debug.LogError(webReq.downloadHandler.text);
            _lastError = "HTTP Error (Notify): " + webReq.downloadHandler.text;
            yield break;
        }

        Debug.Log("Notify API on done completed: " + webReq.downloadHandler.text);
    }

    IEnumerator BuildAndUpload()
    {
        _currentStage = Stage.PendingInit;
        while (EditorApplication.isPlaying)
            yield return null;

        if (!_hasInit)
        {
            _currentStage = Stage.NoBuildRequested;
            Repaint();
            yield break;
        }

        yield return new WaitForSeconds(1f);
        Debug.Log("Will now build and upload");
        _currentStage = Stage.Import;
        Debug.Log(_currentStage);
        Repaint();
        yield return new WaitForSeconds(0.1f);
        //yield return null;
        if (!Import())
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.Build;
        Debug.Log(_currentStage);
        Repaint();
        yield return new WaitForSeconds(0.1f);
        //yield return null;
        if (!Build())
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.GetUploadUrl;
        Debug.Log(_currentStage);
        Repaint();
        //yield return null;
        yield return this.StartCoroutine(GetUploadUrl());
        if(_url == null)
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.UploadBundle;
        Repaint();
        Debug.Log(_currentStage);
        yield return this.StartCoroutine(UploadFile());
        if(_lastError != null)
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.UploadPreview;
        Repaint();
        Debug.Log(_currentStage);
        yield return this.StartCoroutine(UploadPreviewImage());
        if(_lastError != null)
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.NotifyAPIUploadDone;
        Repaint();
        Debug.Log(_currentStage);
        yield return this.StartCoroutine(NotifyAPIAboutUpload());
        if(_lastError != null)
        {
            _currentStage = Stage.Error;
            Repaint();
            Cleanup();
            yield break;
        }
        _currentStage = Stage.Cleanup;
        Repaint();
        Cleanup();
        _currentStage = Stage.Done;
        Repaint();
        Debug.Log("Done! :)");
    }

    void OnGUI()
    {
        //Debug.Log("OnGUI Begin");
        switch (_currentStage)
        {
            case Stage.PendingInit:
                GUILayout.Label("Loading...", EditorStyles.boldLabel);
                break;
            case Stage.Import:
                GUILayout.Label("Importing Preview Images", EditorStyles.boldLabel);
                break;
            case Stage.Build:
                GUILayout.Label("Building AssetBundle", EditorStyles.boldLabel);
                break;
            case Stage.GetUploadUrl:
                GUILayout.Label("Retrieving Upload Url", EditorStyles.boldLabel);
                break;
            case Stage.UploadBundle:
                GUILayout.Label("Uploading Assets...", EditorStyles.boldLabel);
                break;
            case Stage.UploadPreview:
                GUILayout.Label("Uploading Preview Image...", EditorStyles.boldLabel);
                break;
            case Stage.NotifyAPIUploadDone:
                GUILayout.Label("Notifying API...", EditorStyles.boldLabel);
                break;
            case Stage.Cleanup:
                GUILayout.Label("Removing temporary files...", EditorStyles.boldLabel);
                break;
            case Stage.Done:
                GUILayout.Label("Upload completed! :)", EditorStyles.boldLabel);
                break;
            case Stage.Error:
                GUILayout.Label("Error: " + _lastError, EditorStyles.boldLabel);
                break;
            case Stage.NoBuildRequested:
                GUILayout.Label("You can close this window", EditorStyles.boldLabel);
                break;
        }
        //Debug.Log("OnGUI Done");
        //myString = EditorGUILayout.TextField("Text Field", myString);
        //groupEnabled = EditorGUILayout.BeginToggleGroup("Optional Settings", groupEnabled);
        //myBool = EditorGUILayout.Toggle("Toggle", myBool);
        //myFloat = EditorGUILayout.Slider("Slider", myFloat, -3, 3);
        //EditorGUILayout.EndToggleGroup();
    }
}
#endif
