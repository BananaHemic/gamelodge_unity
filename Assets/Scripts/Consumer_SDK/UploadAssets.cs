using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crosstales.FB;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

public class UploadAssets : MonoBehaviour
{
#if UNITY_EDITOR
    public Color PreviewBackgroundColor = new Color(0.378f, 0.378f, 0.378f, 0.25f);
    public Camera PreviewCamera;
    public TextMeshPro PreviewTitleText;
    public TMP_Text ErrorText;

    // File Types
    public Toggle PrefabToggle;
    public Toggle MaterialToggle;
    public Toggle ShaderToggle;
    public Toggle TextureToggle;
    public Toggle SoundToggle;
    public Toggle ScriptableObjectToggle;
    // Info
    public Toggle OutlineToggle;
    public Toggle SexToggle;
    public Toggle GoreToggle;
    public TMP_InputField TitleInputField;
    public TMP_InputField DescriptionInputField;
    public TMP_InputField TagsInputField;
    public TMP_InputField CreditInputField;
    // Upload Directory
    public Button AddFilesButton;
    public Button AddFolderButton;
    public Button ClearFilesButton;
    public Button UploadButton;
    public TextMeshProUGUI AddedAssetList;
    // Preview
    public RawImage PreviewImage;
    // Settings
    public float LayoutObjectRowWidth = 10;
    public bool UseProductionApi = true;

    const int PreviewImageWidth = 256;
    const int PreviewImageHeight = 144;

    public enum AssetType
    {
        Prefab,
        Material,
        Model,
        Shader,
        Sound,
        Texture,
        ScriptableObject
    }

    private struct AddedAsset
    {
        /// <summary>
        /// The full path of the asset
        /// </summary>
        public string Path;
        /// <summary>
        /// The path of the asset, starting with
        /// Assets/
        /// </summary>
        public string AssetDir;
        /// <summary>
        /// The path of the asset, from within Assets/
        /// </summary>
        public string AddressDir;
        /// <summary>
        /// The filename of the asset
        /// </summary>
        public string Name;
        public AssetType CurrentAssetType;
    }
    private struct AssetWithInfo
    {
        public AddedAsset Asset;
        public ModelAABB ModelAABB;
        public List<int> MaterialIndexes;
        public List<string> AttachedScripts;
    }
    private struct PreviewInfo
    {
        public string Path;
        public string Address;
    }
    private struct AddedMaterial
    {
        public Material Mat;
        public int ShaderIndex;
        public string Path;
        public string Address;
    }
    private struct AddedShader
    {
        public Shader Shade;
        //public string Path;
        //public string Address;
    }
    private readonly List<AddedAsset> _addedAssets = new List<AddedAsset>();
    private readonly List<GameObject> _autoInstantiatedObject = new List<GameObject>();

    private static readonly string[] PrefabExtensions = new string[] { "prefab" };
    private static readonly string[] MaterialExtensions = new string[] { "mat" };
    private static readonly string[] ShaderExtensions = new string[] { "shader" };
    private static readonly string[] TextureExtensions = new string[] { "png", "jpg", "jpeg" };
    private static readonly string[] ScriptableObjectsExtensions = new string[] { "asset" };
    private static readonly string[] SoundExtensions = new string[] { "wav", "mp3", "ogg", "aif" };
    private bool _hasProperEditorSettings = false;
    private bool _hasVerifiedBundlesFolder = false;

    void Start()
    {
        RenderTexture renderTexture = new RenderTexture(PreviewImageWidth, PreviewImageHeight, 0)
        {
            antiAliasing = 4
        };
        PreviewCamera.targetTexture = renderTexture;
        PreviewImage.texture = renderTexture;

        if (QualitySettings.activeColorSpace != ColorSpace.Linear)
        {
            ErrorText.text = "Color space must be set to linear!";
            _hasProperEditorSettings = false;
        }
        else if (!PlayerSettings.virtualRealitySupported)
        {
            ErrorText.text = "Virtual Reality supported must be turned on!";
            _hasProperEditorSettings = false;
        }
        else if(PlayerSettings.stereoRenderingPath != StereoRenderingPath.SinglePass)
        {
            // We need this check because Unity fails to use materials that are loaded from an assetbundle
            // that was created using multiview mode. So we need to make sure the uploader
            // has single pass on.
            ErrorText.text = "Stereo rendering mode must be set to SinglePass!";
            _hasProperEditorSettings = false;
        }
        else
        {
            _hasProperEditorSettings = true;
        }
        RefreshButtons();
    }
    public void Upload()
    {
        //if(_uploader != null)
        //{
        //    Debug.LogWarning("Dropping upload, we're still uploading");
        //    return;
        //}

        // Generate preview images for all prefabs, and get the AABB/Material Indexes for each one

        // The output, processed asset
        List<AssetWithInfo> assetInfos = new List<AssetWithInfo>();
        // Info for the preview image
        List<PreviewInfo> previews = new List<PreviewInfo>();
        // The materials/shaders included in this bundle. These are separate, because models will silently include materials/shaders
        List<AddedMaterial> addedMaterials;
        List<AddedShader> addedShaders;
        SavePreviewImagesAndAABBsForAllAssets(PreviewBackgroundColor, _addedAssets, assetInfos, previews, out addedMaterials, out addedShaders);
        Debug.Log("Total number of added materials: " + addedMaterials.Count);

        for (int i = 0; i < addedMaterials.Count; i++)
        {
            var mat = addedMaterials[i];
            mat.Address = mat.Address.Replace('\\', '/');
        }

        if(addedMaterials.Count > ushort.MaxValue)
        {
            Debug.LogError("Too many added material!");
            return;
        }

        string bundleID = CreateRandomS3KeyString(32);
        // Go from AddedShader -> ShaderInfo
        List<ShaderInfo> shaderInfos = new List<ShaderInfo>();
        foreach (var addedShader in addedShaders)
            shaderInfos.Add(new ShaderInfo(addedShader.Shade));
        // Go from AddedMaterial -> MaterialInfo
        List<MaterialInfo> materialInfos = new List<MaterialInfo>();
        for(ushort i = 0; i < addedMaterials.Count;i++)
            materialInfos.Add(new MaterialInfo(bundleID, addedMaterials[i].Address, addedMaterials[i].Mat, i, addedMaterials[i].ShaderIndex));

        // Join the prefab and preview paths together
        List<string> allPaths = new List<string>();
        List<string> allAddresses = new List<string>();
        foreach (var asset in assetInfos)
        {
            allPaths.Add(asset.Asset.Path);
            allAddresses.Add(asset.Asset.AddressDir.Replace('\\', '/'));
        }
        List<string> previewFilePaths = new List<string>(); // We need this to remove all the added previews
        foreach(var preview in previews)
        {
            allPaths.Add(preview.Path);
            allAddresses.Add(preview.Address == null ? null : preview.Address.Replace('\\', '/'));
            previewFilePaths.Add(preview.Path);
        }
        try
        {
            foreach(var addedMat in addedMaterials)
            {
                // We may have already included this material, in which case we can
                // skip adding it again
                if (allPaths.Contains(addedMat.Path))
                {
                    Debug.Log("Skipping " + addedMat.Path);
                    continue;
                }
                allPaths.Add(addedMat.Path);
                allAddresses.Add(addedMat.Address.Replace('\\','/'));
            }

            //foreach (var path in allPaths)
                //Debug.Log("path: " + path);
            //foreach (var address in allAddresses)
                //Debug.Log("address: " + address);

            // Turn the assets into subbundles of the appropriate type
            SubBundle prefabBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Prefab);
            SubBundle modelBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Model);
            SubBundle materialBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Material);
            SubBundle shaderBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Shader);
            SubBundle soundBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Sound);
            SubBundle textureBundle = new SubBundle(bundleID, SubBundle.SubBundleType.Texture);
            SubBundle scriptableObjectBundle = new SubBundle(bundleID, SubBundle.SubBundleType.ScriptableObject);
            foreach(var asset in assetInfos)
            {
                SubBundle bundleToAddTo = null;
                List<List<Vector3>> smoothNormals = null;

                switch (asset.Asset.CurrentAssetType)
                {
                    case AssetType.Prefab:
                        bundleToAddTo = prefabBundle;
                        smoothNormals = GetSmoothNormalsForMesh(asset);
                        break;
                    case AssetType.Model:
                        bundleToAddTo = modelBundle;
                        smoothNormals = GetSmoothNormalsForMesh(asset);
                        break;
                    case AssetType.Material:
                        bundleToAddTo = materialBundle;
                        break;
                    case AssetType.Shader:
                        bundleToAddTo = shaderBundle;
                        break;
                    case AssetType.Sound:
                        bundleToAddTo = soundBundle;
                        break;
                    case AssetType.Texture:
                        bundleToAddTo = textureBundle;
                        break;
                    case AssetType.ScriptableObject:
                        bundleToAddTo = scriptableObjectBundle;
                        break;
                }
                bundleToAddTo.AddElement(asset.Asset.AddressDir.Replace('\\', '/'), asset.ModelAABB, asset.MaterialIndexes, smoothNormals, asset.AttachedScripts);
            }

            string previewImgLocation = SaveCameraImageToFile(PreviewCamera, bundleID);
            string dateTime = DateTime.UtcNow.ToLongDateString();
            Debug.Log("datetime: " + dateTime);
            BundleMetaData metaData = new BundleMetaData(bundleID, TitleInputField.text, DescriptionInputField.text, ModelPermission.Open, 0, 0, SexToggle.isOn, GoreToggle.isOn, CreditInputField.text, TagsInputField.text, dateTime);
            Bundle bundle = new Bundle(metaData, prefabBundle, modelBundle, materialBundle, shaderBundle, soundBundle, textureBundle, scriptableObjectBundle, materialInfos.ToArray(), shaderInfos.ToArray());
            Debug.Log(bundle.ToJson(true).ToString());
            BuildAssetsEditorWindow.Create(bundle, allPaths, allAddresses, previewFilePaths, previewImgLocation, UseProductionApi);
            EditorApplication.isPlaying = false;
        } catch(Exception e)
        {
            Debug.LogError(e.ToString());
            Debug.LogWarning("Clearing out preview due to exception");
            Debug.LogError("Please ensure that all the auto-generated png files were removed!");
            int assetDirLen = Application.dataPath.Length - "Assets".Length;
            foreach(string imgPath in previewFilePaths)
            {
                if (imgPath == null)
                    continue;
                //File.Delete(previewImagePath);
                string directoryRelativeToAssets = imgPath.Substring(assetDirLen);
                Debug.Log("Removing: " + directoryRelativeToAssets);
                AssetDatabase.DeleteAsset(directoryRelativeToAssets);
            }
        }
    }
    private static void SavePreviewImagesAndAABBsForAllAssets(Color backgroundColor, List<AddedAsset> assets, List<AssetWithInfo> assetInfos, List<PreviewInfo> previews,
        out List<AddedMaterial> addedMaterials, out List<AddedShader> addedShaders)
    {
        addedMaterials = new List<AddedMaterial>();
        addedShaders = new List<AddedShader>();
        List<Material> materials = new List<Material>();
        List<Shader> shaders = new List<Shader>();

        PreviewGenerator previewGenerator = new PreviewGenerator(128, 128, backgroundColor);

        int assetDirLenth = "Assets/".Length;

        //TODO HACK
        AssetPreview.SetPreviewTextureCacheSize(Math.Max(1024, assets.Count + 64));

        for (int i = 0; i < assets.Count; i++)
        {
            AddedAsset asset = assets[i];
            // We have to instantiate the object, generate a texture preview, save the preview
            ModelAABB modelAABB;
            GameObject objInstance;
            bool hasImage;
            Texture2D previewImage = Asset2Image(asset, previewGenerator, out modelAABB, out objInstance, out hasImage);

            if (hasImage)
            {
                string previewFilePath =  asset.Path + ".png";
                string previewAddress = asset.AddressDir + ".png";
                previews.Add(new PreviewInfo
                {
                    Address = previewAddress,
                    Path = previewFilePath
                });
                if(previewImage == null)
                    Debug.LogError("No preview image generated for " + asset.AssetDir);
                //Debug.Log("w: " + previewImage.width + " h: " + previewImage.height);
                byte[] bytes = previewImage.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(previewImage);

                if(bytes == null)
                {
                    Debug.LogError("No bytes generated!");
                    continue;
                }

                //Debug.Log("Encoded to " + bytes.Length + " bytes");
                //Debug.Log("Will write to: " + previewFileName);
                FileStream file = File.Open(previewFilePath, FileMode.Create);
                BinaryWriter binary = new BinaryWriter(file);
                binary.Write(bytes);
                file.Close();
                //Debug.Log("Done writing");
                UnityEngine.Object.DestroyImmediate(previewImage);
            }
            else
            {
                previews.Add(new PreviewInfo
                {
                    Address = null,
                    Path = null
                });
            }

            List<int> modelMaterialIndexes = new List<int>();
            // Get all materials that this asset requires
            // For Prefab/Model, we pull from mesh renderer
            // For Material, we add directly
            List<Material> materialsToAdd = new List<Material>();
            if (objInstance != null)
            {
                // Get all materials for this object, and add them to the list
                // if they're unique
                MeshRenderer[] meshRenderers = objInstance.GetComponentsInChildren<MeshRenderer>();
                for(int j = 0; j < meshRenderers.Length; j++)
                {
                    MeshRenderer renderer = meshRenderers[j];
                    for(int k = 0; k < renderer.sharedMaterials.Length; k++)
                    {
                        var mat = renderer.sharedMaterials[k];
                        if(mat == null)
                        {
                            Debug.LogWarning("Null mat on " + asset.AddressDir + " renderer #" + j + " mat #" + k);
                            continue;
                        }
                        materialsToAdd.Add(mat);
                    }

                }
            }
            Debug.Log("For model/prefab, there are: " + materialsToAdd.Count + " non-unique mats");
            if (asset.CurrentAssetType == AssetType.Material)
            {
                Material assetMat = AssetDatabase.LoadAssetAtPath(asset.AssetDir, typeof(Material)) as Material;
                if(assetMat != null)
                    materialsToAdd.Add(assetMat);
                else
                    Debug.LogWarning("Asset marked as material, but was not loaded at " + asset.AssetDir);
            }

            // For each material we want to add, verify that it is not already added
            foreach (Material mat in materialsToAdd)
            {
                int idx = materials.IndexOf(mat);
                if (idx >= 0)
                {
                    // If the material is already in this bundle,
                    // then just record the material index
                    if(!modelMaterialIndexes.Contains(idx))
                        modelMaterialIndexes.Add(idx);
                    continue;
                }
                int shaderIdx = shaders.IndexOf(mat.shader);
                if (shaderIdx == -1)
                {
                    shaders.Add(mat.shader);
                    addedShaders.Add(new AddedShader
                    {
                        Shade = mat.shader
                    });
                    shaderIdx = addedShaders.Count - 1;
                }
                string path = AssetDatabase.GetAssetPath(mat);
                string address = path.Substring(assetDirLenth);
                Debug.Log("Adding (child)" + mat.name + " from " + path + "\nAddress: " + address);
                addedMaterials.Add(new AddedMaterial
                {
                    Mat = mat,
                    Path = Application.dataPath + "/" + address,
                    Address = address,
                    ShaderIndex = shaderIdx
                });
                materials.Add(mat);
                modelMaterialIndexes.Add(materials.Count - 1);
            }
            Debug.Log("Asset has indexes: " + modelMaterialIndexes.ToPrettyString());

            // Get all the scripts attached to this object
            List<string> attachedScripts = new List<string>();
            if(objInstance != null)
            {
                MonoBehaviour[] scripts = objInstance.GetComponents<MonoBehaviour>();
                foreach(var script in scripts)
                    attachedScripts.Add(script.GetType().ToString());
            }
            if(asset.CurrentAssetType == AssetType.ScriptableObject)
            {
                ScriptableObject scriptObj = AssetDatabase.LoadAssetAtPath(asset.AssetDir, typeof(ScriptableObject)) as ScriptableObject;
                if (scriptObj != null)
                {
                    string scriptName = scriptObj.GetType().ToString();
                    Debug.Log("SO has script " + scriptName);
                    attachedScripts.Add(scriptName);
                }
                else
                    Debug.LogError("No ScriptableObject at " + asset.AssetDir);
            }

            assetInfos.Add(new AssetWithInfo
            {
                Asset = asset,
                ModelAABB = modelAABB,
                MaterialIndexes = modelMaterialIndexes,
                AttachedScripts = attachedScripts,
            });
        }
        previewGenerator.Dispose();
    }
    private string[] GetFileExtensions()
    {
        List<string> extensions = new List<string>();
        if (PrefabToggle.isOn)
            extensions.AddRange(PrefabExtensions);
        if (MaterialToggle.isOn)
            extensions.AddRange(MaterialExtensions);
        if (ShaderToggle.isOn)
            extensions.AddRange(ShaderExtensions);
        if (TextureToggle.isOn)
            extensions.AddRange(TextureExtensions);
        if (SoundToggle.isOn)
            extensions.AddRange(SoundExtensions);
        if (ScriptableObjectToggle.isOn)
            extensions.AddRange(ScriptableObjectsExtensions);
        return extensions.ToArray();
    }
    private AssetType AssetPath2AssetType(string path)
    {
        if (EndsWithOneOf(path, PrefabExtensions))
            return AssetType.Prefab;
        if (EndsWithOneOf(path, MaterialExtensions))
            return AssetType.Material;
        if (EndsWithOneOf(path, ShaderExtensions))
            return AssetType.Shader;
        if (EndsWithOneOf(path, TextureExtensions))
            return AssetType.Texture;
        if (EndsWithOneOf(path, SoundExtensions))
            return AssetType.Sound;
        if (EndsWithOneOf(path, ScriptableObjectsExtensions))
            return AssetType.ScriptableObject;

        Debug.LogError("Unknown file extension for " + path);
        return AssetType.Prefab;
    }
    public void OpenNativeFileBrowser_File()
    {
        string assetDir = Application.dataPath;
        //string assetDir = Application.dataPath + "\\3rd-Party\\MultistoryDungeons\\Multistory Dungeons\\Prefabs";
        Debug.Log("using file extens " + GetFileExtensions().Length);
        string[] addedPaths = FileBrowser.OpenFiles("Add Files For Upload", assetDir, new ExtensionFilter[] { new ExtensionFilter("", GetFileExtensions())});
        //string[] addedPaths = FileBrowser.OpenFiles("Add Files For Upload", assetDir, new string[] { sb.ToString() });
        Debug.Log("Added " + addedPaths.Length + " files");
        int assetDirIdx = Application.dataPath.LastIndexOf('/') + 1;
        string firstFriendlyName = null;
        string assetDirEscaped = Application.dataPath.Replace("/", "\\");
        foreach(var str in addedPaths)
        {
            if (!str.StartsWith(assetDirEscaped))
            {
                Debug.LogError("The assets need to be under the project's Assets directory. Try dragging the file into the Unity project window");
                continue;
            }
            // All single-added files are added into the root folder
            int folderDirIdx = str.LastIndexOf('\\');
            string fileName = str.Substring(folderDirIdx + 1);
            AddedAsset addedAsset = new AddedAsset()
            {
                Path = str.Replace('\\', '/'),
                AssetDir = str.Substring(assetDirIdx),
                AddressDir = str.Substring(assetDirIdx + "Assets/".Length),
                Name = fileName,
                CurrentAssetType = AssetPath2AssetType(str)
            };
            _addedAssets.Add(addedAsset);

            if(firstFriendlyName == null)
            {
                int end = str.LastIndexOf('.');
                firstFriendlyName = end >= 0 ? str.Substring(folderDirIdx + 1, end - folderDirIdx - 1) : str.Substring(folderDirIdx);
            }
        }
        if (string.IsNullOrEmpty(TitleInputField.text))
            TitleInputField.text = firstFriendlyName;
        RefreshAddedFiles();
        RefreshButtons();
    }
    private static bool EndsWithOneOf(string potential, string[] ends)
    {
        for(int i = 0; i < ends.Length; i++)
        {
            if (potential.EndsWith(ends[i]))
                return true;
        }
        return false;
    }
    public void OpenNativeFileBrowser_Folder()
    {
        string assetDir = Application.dataPath;
        //string assetDir = Application.dataPath + "\\3rd-Party\\MultistoryDungeons\\Multistory Dungeons\\Prefabs";
        string addedFolder = FileBrowser.OpenSingleFolder("Add Folder For Upload", assetDir);
        if (string.IsNullOrEmpty(addedFolder))
            return;
        string assetDirEscaped = Application.dataPath.Replace("/", "\\");
        if (!addedFolder.StartsWith(assetDirEscaped))
        {
            Debug.Log(addedFolder);
            Debug.Log(assetDirEscaped);
            Debug.LogError("The assets need to be under the project's Assets directory. Try dragging the file into the Unity project window");
            return;
        }
        Debug.Log("Added folder " + addedFolder);
        // Get all paths for the folder
        string[] potentialPaths = Directory.GetFiles(addedFolder, "*", SearchOption.AllDirectories);
        Debug.Log(potentialPaths.Length + " potentials");
        string[] extensions = GetFileExtensions();
        int assetDirIdx = Application.dataPath.LastIndexOf('/') + 1;
        int numPreExisting = _addedAssets.Count;
        foreach(var potentialPath in potentialPaths)
        {
            if (!EndsWithOneOf(potentialPath, extensions))
            {
                Debug.Log("Dropping path " + potentialPath);
                continue;
            }

            int folderDirIdx = potentialPath.LastIndexOf('\\');
            string fileName = potentialPath.Substring(folderDirIdx + 1);
            AddedAsset addedAsset = new AddedAsset()
            {
                Path = potentialPath.Replace('\\', '/'),
                AssetDir = potentialPath.Substring(assetDirIdx),
                AddressDir = potentialPath.Substring(assetDirIdx + "Assets/".Length),
                Name = fileName,
                CurrentAssetType = AssetPath2AssetType(potentialPath)
            };
            _addedAssets.Add(addedAsset);
            //Debug.Log(potentialPath + " does end with one of: " + EndsWithOneOf(potentialPath, extensions));
        }
        Debug.Log("Added " + (_addedAssets.Count - numPreExisting) + " files");
        if (string.IsNullOrEmpty(TitleInputField.text))
        {
            int topDirIdx = addedFolder.LastIndexOf('\\');
            TitleInputField.text = addedFolder.Substring(topDirIdx + 1);
        }
        RefreshAddedFiles();
        RefreshButtons();
    }
    public void ClearAll()
    {
        _addedAssets.Clear();
        RefreshAddedFiles();
        RefreshButtons();
    }
    public void OnTitleTextChanged()
    {
        RefreshButtons();
    }
    public void OnDescriptionChanged()
    {
        RefreshButtons();
    }
    public void OnTagsChanged()
    {
        RefreshButtons();
    }
    public void OnCreditChanged()
    {
        RefreshButtons();
    }
    private void RefreshAddedFiles()
    {
        StringBuilder sb = new StringBuilder();
        foreach(var asset in _addedAssets)
        {
            //string path = asset.Path;
            //int topDirEnd = path.IndexOf(asset.AssetDir);
            //Debug.Log("path: " + path + " top end " + topDirEnd);
            //string fileName = topDirEnd >= 0 ? path.Substring(topDirEnd + 1) : path;
            //Debug.Log("path: " + path + " file " + fileName + " top end " + topDirEnd);
            //sb.Append(fileName);
            sb.Append(asset.AddressDir);
            sb.Append("\n");
        }
        AddedAssetList.text = sb.ToString();
    }
    public void RefreshButtons()
    {
        ClearFilesButton.interactable = _addedAssets.Count > 0;
        UploadButton.interactable = _addedAssets.Count > 0
            && !string.IsNullOrEmpty(TitleInputField.text)
            && !string.IsNullOrEmpty(DescriptionInputField.text)
            && _hasProperEditorSettings;
        //AddFilesButton.interactable = !string.IsNullOrEmpty(DirectoryInputField.text);
    }
    private string SaveCameraImageToFile(Camera snapshotCam, string bundleID)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = snapshotCam.targetTexture;

        snapshotCam.Render();

        Texture2D Image = new Texture2D(snapshotCam.targetTexture.width, snapshotCam.targetTexture.height);
        Image.ReadPixels(new Rect(0, 0, snapshotCam.targetTexture.width, snapshotCam.targetTexture.height), 0, 0);
        Image.Apply();
        RenderTexture.active = currentRT;

        var Bytes = Image.EncodeToPNG();
        Destroy(Image);

        int assetDirLen = Application.dataPath.Length - "Assets".Length;
        // Make the Bundles/ folder if needed
        if (!_hasVerifiedBundlesFolder)
        {
            string bundlesFolderDir = string.Format("{0}Bundles/", Application.dataPath.Substring(0, assetDirLen));
            if (!Directory.Exists(bundlesFolderDir))
            {
                Debug.Log("Making Bundles folder \"" + bundlesFolderDir + "\"");
                Directory.CreateDirectory(bundlesFolderDir);
            }
            _hasVerifiedBundlesFolder = true;
        }
        string filePath = string.Format("{0}Bundles/{1}.png", Application.dataPath.Substring(0, assetDirLen), bundleID);
        Debug.Log("Saving to: " + filePath);
        File.WriteAllBytes(filePath, Bytes);
        return filePath;
    }
    private static Texture2D Asset2Image(AddedAsset asset, PreviewGenerator previewGenerator, out ModelAABB modelAABB, out GameObject inst, out bool hasImg)
    {
        Texture2D previewImage = null;
        int attemptNum = 0;

        switch (asset.CurrentAssetType)
        {
            case AssetType.Prefab:
            case AssetType.Model:
                hasImg = true;
                inst = AssetDatabase.LoadAssetAtPath(asset.AssetDir, typeof(GameObject)) as GameObject;
                if(inst == null)
                    Debug.LogError("No instance " + asset.Path + ", " + asset.AssetDir + ", " + asset.Name);
                //Debug.Log(instance == null ? "null instance!" : "instance #: " + instance.GetInstanceID());
                // Get asset preview is asyncronous, so we keep polling until it has the image we want
                while (previewImage == null)
                {
                    previewImage = previewGenerator.Generate(inst);
                    if (previewImage == null)
                        System.Threading.Thread.Sleep(50);
                    attemptNum++;
                }
                AABB aabb = CalculateAABB.GetHierarchyAABB(inst);
                // Account for the object scale
                modelAABB = new ModelAABB(aabb, inst.transform.localScale);
                return previewImage;
            case AssetType.Material:
            case AssetType.Shader:
            case AssetType.Sound:
            case AssetType.Texture:
                hasImg = true;
                // Get asset preview is asyncronous, so we keep polling until it has the image we want
                UnityEngine.Object instance = AssetDatabase.LoadAssetAtPath(asset.AssetDir, typeof(UnityEngine.Object));
                if (instance == null)
                    Debug.LogError("Null instance for asset preview?");
                while (previewImage == null && attemptNum < 30)
                {
                    previewImage = AssetPreview.GetAssetPreview(instance);
                    if (previewImage == null)
                    {
                        Debug.Log("loading...");
                        System.Threading.Thread.Sleep(50);
                    }
                    attemptNum++;
                }
                modelAABB = new ModelAABB(AABB.GetInvalid(), Vector3.one);
                inst = null;
                return previewImage;
            case AssetType.ScriptableObject:
                modelAABB = new ModelAABB(AABB.GetInvalid(), Vector3.one);
                hasImg = false;
                inst = null;
                return null;
            default:
                throw new Exception("unhandled asset type");
        }
    }
    /// <summary>
    /// Called from Unity button
    /// </summary>
    public void GenerateTemplatePreviewImage()
    {
        Vector3 pos = Vector3.zero;

        foreach (var obj in _autoInstantiatedObject)
            Destroy(obj);
        _autoInstantiatedObject.Clear();

        //Debug.Log(projectDirLen);
        float maxZ = 0;
        float sumX = 0;

        foreach(var asset in _addedAssets)
        {
            // Don't instantiate if this is a non-instantiateable thing
            if (asset.CurrentAssetType != AssetType.Prefab)
                continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath(asset.AssetDir, typeof(GameObject)) as GameObject;
            GameObject instance = Instantiate(prefab);
            instance.transform.position = pos;
            AABB aabb = CalculateAABB.GetHierarchyAABB(instance);
            Vector3 offset = 2 * aabb.Extents;
            //Debug.Log(aabb.Extents.ToPrettyString());
            maxZ = Mathf.Max(maxZ, offset.z);
            sumX += offset.x;
            offset.y = 0;
            offset.z = 0;
            pos += offset;
            // Once we hit the end of one row, roll over
            if(sumX >= LayoutObjectRowWidth)
            {
                pos += new Vector3(0, 0, maxZ);
                maxZ = 0;
                sumX = 0;
                pos.x = 0;
            }
            _autoInstantiatedObject.Add(instance);
        }

        if(!string.IsNullOrEmpty(TitleInputField.text))
            PreviewTitleText.text = TitleInputField.text;
    }
    private List<List<Vector3>> GetSmoothNormalsForMesh(AssetWithInfo assetInfo)
    {
        if (!OutlineToggle.isOn)
            return new List<List<Vector3>>();
        string path = assetInfo.Asset.AssetDir;
        Debug.Log("Will get " + path);
        GameObject inst = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
        MeshFilter[] meshFilters = inst.GetComponentsInChildren<MeshFilter>();
        List<List<Vector3>> smoothNormals = new List<List<Vector3>>(meshFilters.Length);
        Debug.Log("Asset " + path + " has num filters: " + meshFilters.Length);
        //TODO this breaks when uploading built-in meshes
        for(int i = 0; i < meshFilters.Length;i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;
            if(mesh == null)
            {
                Debug.LogWarning("Mesh filter without shared mesh! " + path);
                continue;
            }
            string meshPath = AssetDatabase.GetAssetPath(mesh);
            // Some mesh assets end are .asset files. I have no idea why
            // someone would have an asset like that, nor do I know how
            // they make it. So, if we see an asset like that, just log
            // a warning and upload without normals
            if (meshPath.EndsWith(".asset"))
            {
                Debug.LogWarning("The file \"" + meshPath + "\" can't be uploaded because it is a .asset file. This object will not have an outline");
                smoothNormals.Add(new List<Vector3>());
                continue;
            }

            // Make sure that the mesh is readable
            ModelImporter modelImporter = ModelImporter.GetAtPath(meshPath) as ModelImporter;
            if(modelImporter == null)
            {
                Debug.LogError("Failed to get model importer! For mesh at " + meshPath);
                throw new System.Exception();
            }
            if (!modelImporter.isReadable)
            {
                Debug.Log("Will set model to readable, at path " + path);
                modelImporter.isReadable = true;
                modelImporter.SaveAndReimport();
            }

            //Debug.Log("vtx count: " + mesh.vertexCount);
            smoothNormals.Add(SmoothNormals(mesh));
            Debug.Log("Got UV3 to smooth normals, len " + smoothNormals[i].Count);
        }
        return smoothNormals;
    }
    private static List<Vector3> SmoothNormals(Mesh mesh)
    {
        // Group vertices by location
        var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);
        // Copy normals to a new list
        var smoothNormals = new List<Vector3>(mesh.normals);
        // Average normals for grouped vertices
        foreach (var group in groups)
        {
            // Skip single vertices
            if (group.Count() == 1)
                continue;

            // Calculate the average normal
            var smoothNormal = Vector3.zero;

            foreach (var pair in group)
                smoothNormal += mesh.normals[pair.Value];

            smoothNormal.Normalize();

            // Assign smooth normal to each vertex
            foreach (var pair in group)
                smoothNormals[pair.Value] = smoothNormal;
        }
        return smoothNormals;
    }
#endif
    public static string CreateRandomS3KeyString(int stringLength)
    {
        const string allowedChars = "abcdefghijkmnopqrstuvwxyz0123456789";
        char[] chars = new char[stringLength];

        for (int i = 0; i < stringLength; i++)
            chars[i] = allowedChars[UnityEngine.Random.Range(0, allowedChars.Length)];

        return new string(chars);
    }
}
