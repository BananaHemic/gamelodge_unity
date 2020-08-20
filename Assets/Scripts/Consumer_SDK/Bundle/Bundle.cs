using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Newtonsoft.Json.Linq;

public class Bundle
{
    public string ID
    {
        get
        {
            return MetaData.ID;
        }
    }
    public string Name
    {
        get
        {
            return MetaData.Name;
        }
    }
    public string Description
    {
        get
        {
            return MetaData.Description;
        }
    }
    public ModelPermission Permission
    {
        get
        {
            return MetaData.Permission;
        }
    }
    public BundleMetaData MetaData;
    /// <summary>
    /// The materials included in this bundle
    /// </summary>
    public MaterialInfo[] MaterialInfos;
    /// <summary>
    /// The shaders included in this bundle
    /// </summary>
    public ShaderInfo[] ShaderInfos;
    /// <summary>
    /// A mapping from bundle ID (ID is out of all items across SubBundles)
    /// to the containing SubBundle, and it's index
    /// </summary>
    public readonly List<BundleItem> AllBundleItems;

    private readonly SubBundle PrefabBundle;
    private readonly SubBundle ModelBundle;
    private readonly SubBundle MaterialBundle;
    private readonly SubBundle ShaderBundle;
    private readonly SubBundle SoundBundle;
    private readonly SubBundle TextureBundle;
    private readonly SubBundle ScriptableObjectBundle;

    /// <summary>
    /// Our tree data structure for keeping track of which items are in which folders
    /// </summary>
    private readonly ModelTree _modelTreeRoot = new ModelTree("/", null);

    const string BundleKey = "json";
    const string PrefabKey = "pr";
    const string ModelKey = "mo";
    const string MaterialKey = "mat";
    const string ShaderKey = "sh";
    const string SoundKey = "so";
    const string TextureKey = "t";
    const string ScriptableObjectKey = "sco";
    const string MaterialInfoKey = "matInfo";
    const string ShaderInfoKey = "shadeInfo";

    public Bundle(BundleMetaData metaData, SubBundle prefabs, SubBundle models, SubBundle materials, SubBundle shaders, SubBundle sounds, SubBundle textures, SubBundle scriptableObjects,
        MaterialInfo[] materialInfos, ShaderInfo[] shaderInfos)
    {
        MetaData = metaData;
        PrefabBundle = prefabs;
        ModelBundle = models;
        MaterialBundle = materials;
        ShaderBundle = shaders;
        SoundBundle = sounds;
        TextureBundle = textures;
        ScriptableObjectBundle = scriptableObjects;
        MaterialInfos = materialInfos;
        ShaderInfos = shaderInfos;

        int numElements = 0
            + PrefabBundle.BundleItems.Count
            + ModelBundle.BundleItems.Count
            + ShaderBundle.BundleItems.Count
            + SoundBundle.BundleItems.Count
            + TextureBundle.BundleItems.Count
            + ScriptableObjectBundle.BundleItems.Count;
        AllBundleItems = new List<BundleItem>(numElements);

        ushort bundleIdx = 0;
        // Add each item to our internal store
        for (ushort i = 0; i < PrefabBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(PrefabBundle.BundleItems[i], PrefabBundle, bundleIdx++);
        for (ushort i = 0; i < MaterialBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(MaterialBundle.BundleItems[i], MaterialBundle,bundleIdx++);
        for (ushort i = 0; i < ShaderBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(ShaderBundle.BundleItems[i], ShaderBundle, bundleIdx++);
        for (ushort i = 0; i < SoundBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(SoundBundle.BundleItems[i], SoundBundle, bundleIdx++);
        for (ushort i = 0; i < TextureBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(TextureBundle.BundleItems[i], TextureBundle, bundleIdx++);
        for (ushort i = 0; i < ScriptableObjectBundle.BundleItems.Count; i++)
            AddItemFromSubBundle(ScriptableObjectBundle.BundleItems[i], ScriptableObjectBundle, bundleIdx++);
    }
    public SubBundle GetBundleForType(SubBundle.SubBundleType subBundleType)
    {
        switch (subBundleType)
        {
            case SubBundle.SubBundleType.Prefab:
                return PrefabBundle;
            case SubBundle.SubBundleType.Material:
                return MaterialBundle;
            case SubBundle.SubBundleType.Model:
                return ModelBundle;
            case SubBundle.SubBundleType.Shader:
                return ShaderBundle;
            case SubBundle.SubBundleType.Sound:
                return SoundBundle;
            case SubBundle.SubBundleType.Texture:
                return TextureBundle;
            case SubBundle.SubBundleType.ScriptableObject:
                return ScriptableObjectBundle;
        }
        Debug.LogError("Unhandled bundle type! " + subBundleType);
        return null;
    }
    /// <summary>
    /// Adds an item from an uploaded SubBundle to our internal store
    /// </summary>
    /// <param name="address"></param>
    /// <param name="subBundle"></param>
    /// <param name="bundleIndex"></param>
    private void AddItemFromSubBundle(BundleItem bundleItem, SubBundle subBundle, ushort bundleIndex)
    {
        bundleItem.SetBundleIndex(bundleIndex);
        AddElementToTree(bundleItem.Address, bundleIndex, bundleItem);
        AllBundleItems.Add(bundleItem);
    }
    private void AddElementToTree(string address, ushort bundleIndex, BundleItem bundleItem)
    {
        ModelTree treeToAddTo = _modelTreeRoot;
        // Get all folders, and add them to the tree
        List<string> folders = bundleItem.GetContainingFolders();
        for(int i = 0; i < folders.Count;i++)
        {
            // Add or get the tree for this folder
            treeToAddTo = treeToAddTo.GetOrAddFolder(folders[i]);
        }
        //Debug.Log("Done getting folders, model name is " + modelName);
        // Finally, add it to the tree
        treeToAddTo.AddModel(bundleItem.GetAssetName(), bundleIndex, bundleItem);
    }
    public List<ModelTreeElement> GetElementsInFolder(List<string> parentFolders)
    {
        ModelTree tree = _modelTreeRoot;
        int idx = 0;
        while(parentFolders != null && parentFolders.Count > idx)
        {
            string folderName = parentFolders[idx++];
            ModelTreeElement element = tree.GetChild(folderName);
            if(element == null)
            {
                Debug.LogError("No folder named " + folderName);
                return null;
            }
            tree = element.Tree;
            if(tree == null)
            {
                Debug.LogError("Element, not folder, named " + folderName);
                return null;
            }
        }
        return tree.GetAllChildren();
    }

    public StringBuilder ToJson(bool includeMetadata)
    {
        StringBuilder sb = new StringBuilder();
        bool isFirst = true;
        sb.Append("{");
        if (includeMetadata)
        {
            MetaData.ToJson(sb, false);
            sb.Append(",\"");
            sb.Append(BundleKey);
            sb.Append("\":{");
        }

        PrefabBundle.ToJson(PrefabKey, sb);
        sb.Append(",");
        ModelBundle.ToJson(ModelKey, sb);
        sb.Append(",");
        MaterialBundle.ToJson(MaterialKey, sb);
        sb.Append(",");
        ShaderBundle.ToJson(ShaderKey, sb);
        sb.Append(",");
        SoundBundle.ToJson(SoundKey, sb);
        sb.Append(",");
        TextureBundle.ToJson(TextureKey, sb);
        sb.Append(",");
        ScriptableObjectBundle.ToJson(ScriptableObjectKey, sb);
        sb.Append(",");

        if(ShaderInfos != null)
        {
            if(!isFirst)
                sb.Append(",");
            isFirst = false;
            sb.Append("\"");
            sb.Append(ShaderInfoKey);
            sb.Append("\":[");
            for(int i = 0; i < ShaderInfos.Length; i++)
            {
                ShaderInfos[i].ToJson(sb);
                if (i != ShaderInfos.Length - 1)
                    sb.Append(",");
            }
            sb.Append("]");
        }
        if(MaterialInfos != null)
        {
            if(!isFirst)
                sb.Append(",");
            isFirst = false;
            sb.Append("\"");
            sb.Append(MaterialInfoKey);
            sb.Append("\":[");
            for(int i = 0; i < MaterialInfos.Length; i++)
            {
                MaterialInfos[i].ToJson(sb);
                if (i != MaterialInfos.Length - 1)
                    sb.Append(",");
            }
            sb.Append("]");
        }
        sb.Append("}");
        if(includeMetadata)
            sb.Append("}");
        return sb;
    }

    public static Bundle FromJson(JObject json, BundleMetaData existingMetaData=null)
    {
        if (existingMetaData == null)
            existingMetaData = BundleMetaData.FromJson(json);

        //TODO clean this up. We have this because most API interactions have the json node
        // as a sub key
        json = (JObject)json[BundleKey];

        SubBundle prefabBundle = new SubBundle(existingMetaData.ID, (JObject)json[PrefabKey], SubBundle.SubBundleType.Prefab);
        SubBundle modelBundle = new SubBundle(existingMetaData.ID, (JObject)json[ModelKey], SubBundle.SubBundleType.Model);
        SubBundle materialBundle = new SubBundle(existingMetaData.ID, (JObject)json[MaterialKey], SubBundle.SubBundleType.Material);
        SubBundle shaderBundle = new SubBundle(existingMetaData.ID, (JObject)json[ShaderKey], SubBundle.SubBundleType.Shader);
        SubBundle soundBundle = new SubBundle(existingMetaData.ID, (JObject)json[SoundKey], SubBundle.SubBundleType.Sound);
        SubBundle textureBundle = new SubBundle(existingMetaData.ID, (JObject)json[TextureKey], SubBundle.SubBundleType.Texture);
        SubBundle scriptableObjectBundle = json[ScriptableObjectKey] == null ? new SubBundle(existingMetaData.ID, SubBundle.SubBundleType.ScriptableObject)
            : new SubBundle(existingMetaData.ID, (JObject)json[ScriptableObjectKey], SubBundle.SubBundleType.ScriptableObject);

        JArray shaderInfosJArray = json.Value<JArray>(ShaderInfoKey);
        ShaderInfo[] shaderInfos = new ShaderInfo[shaderInfosJArray.Count];
        for(int i = 0; i < shaderInfosJArray.Count; i++)
        {
            JToken jToken = shaderInfosJArray[i];
            shaderInfos[i] = new ShaderInfo(jToken);
        }

        JArray materialInfosJArray = json.Value<JArray>(MaterialInfoKey);
        MaterialInfo[] materialInfos = new MaterialInfo[materialInfosJArray.Count];
        for(ushort i = 0; i < materialInfosJArray.Count; i++)
        {
            JToken jToken = materialInfosJArray[i];
            materialInfos[i] = new MaterialInfo(jToken, existingMetaData.ID, i, shaderInfos);
        }

        //string[] models = val.Value<string[]>(ModelsKey);
        Bundle bundle = new Bundle(existingMetaData, prefabBundle, modelBundle, materialBundle, shaderBundle, soundBundle, textureBundle, scriptableObjectBundle, materialInfos, shaderInfos);
        return bundle;
    }
}
