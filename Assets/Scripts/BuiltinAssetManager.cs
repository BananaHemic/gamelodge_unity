using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuiltinAssetManager : GenericSingleton<BuiltinAssetManager>
{
    public GameObject DummyPrefab;
    private BundleItem DummyBundleItem;
    public Bundle BuiltinBundle;

    public const string BuiltinBundleID = "__builtin";
    public const ushort DummyPrefabID = 0;
    

    protected override void Awake()
    {
        base.Awake();

        BundleMetaData metaData = new BundleMetaData(BuiltinBundleID, "builtin", "The stuff that's builtin", ModelPermission.Open, 0, 0, false, false, "Gamelodge", "", "");
        SubBundle prefabBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Prefab);
        SubBundle modelBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Model);
        SubBundle materialBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Material);
        SubBundle shaderBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Shader);
        SubBundle soundBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Sound);
        SubBundle textureBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.Texture);
        SubBundle scriptableObjectBundle = new SubBundle(BuiltinBundleID, SubBundle.SubBundleType.ScriptableObject);


        prefabBundle.AddElement("", new ModelAABB(new AABB(Vector3.zero, Vector3.one), Vector3.one), new List<int>(), new List<List<Vector3>>(), null);
        DummyBundleItem = prefabBundle.BundleItems[0];
        BuiltinBundle = new Bundle(metaData, prefabBundle, modelBundle, materialBundle, shaderBundle, soundBundle, textureBundle, scriptableObjectBundle, new MaterialInfo[]{ }, new ShaderInfo[] { });
    }
    public GameObject InstantiateObjectFromBundleIndex(ushort bundleIndex, Transform parent, out BundleItem bundleItem)
    {
        switch (bundleIndex)
        {
            case DummyPrefabID:
                //if (DummyBundleItem == null)
                    //DummyBundleItem = BundleItemFromObject(DummyPrefab);
                bundleItem = DummyBundleItem;
                var obj = GameObject.Instantiate(DummyPrefab, parent);
                obj.transform.localPosition = Vector3.zero;
                return obj;
            default:
                Debug.LogError("Unhandled builtin bundle Idx " + bundleIndex);
                bundleItem = null;
                return null;
        }
    }
}
