using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BundleItem
{
    public ushort BundleIndex { get; private set; }
    public readonly ushort SubBundleIndex;
    public readonly SubBundle ContainingSubBundle;
    // The model name including the dir from the base (e.g. Doors/GarishBigDoor.prefab)
    // These are also the addressable names in the asset bundle
    public readonly string Address;
    // The AABB info for this item. May be invalid is not applicable
    public readonly ModelAABB AABBInfo;
    /// <summary>
    /// The index(s) of the materials used
    /// by a model. e.g. [3,9,2] is a model used the materials at indexes 3,9,2 in it's three material slots
    /// </summary>
    public readonly List<int> MaterialIndexes;
    /// <summary>
    /// For performance, we upload the smoothed normals
    /// This is a multidimensional array, top array is for
    /// each mesh attached. 
    /// </summary>
    public readonly List<List<Vector3>> SmoothNormals;
    /// <summary>
    /// The scripts that were attached to this when the item
    /// was uploaded. This may be things like AvatarDescriptor
    /// or Animator
    /// </summary>
    public readonly List<string> AttachedScripts;

    private List<string> _containingFolders;
    private string _itemName;

    public BundleItem(ushort subBundleIndex, SubBundle subBundle, string address, ModelAABB modelAABB, List<int> matIndexes, List<List<Vector3>> smoothedNormals, List<string> attachedScripts)
    {
        SubBundleIndex = subBundleIndex;
        ContainingSubBundle = subBundle;
        Address = address;
        AABBInfo = modelAABB;
        MaterialIndexes = matIndexes;
        SmoothNormals = smoothedNormals;
        AttachedScripts = attachedScripts;
    }
    public void SetBundleIndex(ushort bundleIndex)
    {
        if (BundleIndex != 0)
            Debug.LogWarning("Overwriting bundle index, was " + BundleIndex + " now " + bundleIndex);
        BundleIndex = bundleIndex;
    }
    private void InitFoldersAndName()
    {
        _containingFolders = new List<string>(Address.NumInstancesOf('/'));
        int folderNameIndex = 0;
        //Debug.Log("folder init address: " + Address);
        while (true)
        {
            int nameIdx = Address.IndexOf('/', folderNameIndex);
            //Debug.Log("idx " + nameIdx);
            // If no slash found, we hit the spot with the actual model name
            if (nameIdx < 0)
                break;
            //Debug.Log("folder Idx " + folderNameIndex + " nameIdx " + nameIdx);
            string folderName = Address.Substring(folderNameIndex, nameIdx - folderNameIndex);
            _containingFolders.Add(folderName);
            //Debug.Log("Adding folder " + folderName);
            folderNameIndex = nameIdx + 1;
        }
        _itemName = Address.Substring(folderNameIndex);
    }
    public List<string> GetContainingFolders()
    {
        if (_containingFolders != null)
            return _containingFolders;

        InitFoldersAndName();
        return _containingFolders;
    }
    public string GetAssetName()
    {
        if (_itemName != null)
            return _itemName;
        InitFoldersAndName();
        return _itemName;
    }
}
