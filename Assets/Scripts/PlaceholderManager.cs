using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceholderManager : GenericSingleton<PlaceholderManager>
{
    public static readonly string PlaceholderGameObjectName = "LoadingBox";
    public delegate void OnLoadedPlaceholderBox(GameObject placeholder);
    public delegate void OnLoadedPlaceholderModel(GameObject placeholder);
    public GameObject PlaceholderBoxPrefab;

    private readonly AutoKeyDictionary<OnLoadedPlaceholderModel> _pendingPlaceholderModelLoads = new AutoKeyDictionary<OnLoadedPlaceholderModel>();

    public GameObject LoadPlaceholderBox(Transform parent, BundleItem bundleItem, int layer)
    {
        GameObject box = SimplePool.Instance.Spawn(PlaceholderBoxPrefab);
        if (parent != null)
            box.transform.parent = parent;
        box.transform.localPosition = bundleItem.AABBInfo.Center;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = bundleItem.AABBInfo.Extents * 2f;
        box.layer = layer;
        box.name = PlaceholderGameObjectName;
        return box;
    }
    public void LoadPlaceholderModel(BundleItem bundleItem, OnLoadedPlaceholderModel onLoadedModel)
    {
        int loadID = _pendingPlaceholderModelLoads.Add(onLoadedModel);
        // If we're loading a model, we first query the
        // model AABB and return a loading box. Then
        // we begin the actual model
        BundleManager.Instance.LoadGameObjectFromBundle(bundleItem, null, loadID, OnLoadedBundleItemModel);
    }
    private void OnLoadedBundleItemModel(int loadID, GameObject loadedModel)
    {
        OnLoadedPlaceholderModel onLoaded;
        if(!_pendingPlaceholderModelLoads.TryGetValue(loadID, out onLoaded))
        {
            Debug.LogError("No placeholder load for model, in model callback. Load ID: " + loadID);
            return;
        }
        if(loadedModel == null)
        {
            Debug.LogError("Failed loading model. Load ID #" + loadID);
            onLoaded(null);
            return;
        }
        onLoaded(loadedModel);
    }
    public void ReturnPlaceholderBox(GameObject placeholder)
    {
        SimplePool.Instance.Despawn(placeholder);
    }
    public void ReturnPlaceholderModel(GameObject model)
    {
        GameObject.Destroy(model);
    }
}
