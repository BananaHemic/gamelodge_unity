using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectHierarchy : GenericSingleton<ObjectHierarchy>, ILoopScrollDataSource, ILoopScrollPrefabSource
{
    public GameObject HierarchyPrefab;
    public RectTransform HierarchObjectContainer;
    public GameObject NoObjectsText;
    public LoopVerticalScrollRect ScrollRect;

    protected override void Awake()
    {
        base.Awake();
        ScrollRect.Init(this, this);
        SceneObjectManager.OnSceneObjectAdded += OnSceneObjectAdded;
        SceneObjectManager.OnSceneObjectRemoved += OnSceneObjectRemoved;
        SceneObjectManager.OnSceneNameChange += OnSceneObjectNameChange;
        Orchestrator.OnDoneLoadingObjectsFromGameState += OnOrchestratorDoneLoadingObjects;
    }
    public void ClearObjects()
    {
        ScrollRect.ClearCells();
        NoObjectsText.SetActive(true);
    }
    void OnSceneObjectAdded(SceneObject sceneObject)
    {
        //Debug.Log("Adding a sceneobject. Reload UI: " + (!Orchestrator.Instance.IsAddingObjectsFromGameState));
        ScrollRect.AddItem(sceneObject, !Orchestrator.Instance.IsAddingObjectsFromGameState);
        NoObjectsText.SetActive(false);
    }
    void OnSceneObjectRemoved(SceneObject removedSceneObj)
    {
        // TODO have RLD provide a list of sceneobjects
        // to remove, when we remove more than one. Then
        // just update the UI once
        //Debug.Log("Removing a sceneobject");
        ScrollRect.RemoveItem(removedSceneObj, true);
        NoObjectsText.SetActive(ScrollRect.Count == 0);
    }
    void OnOrchestratorDoneLoadingObjects()
    {
        //Debug.Log("Refilling cells");
        ScrollRect.RefillCells();
    }
    void OnSceneObjectNameChange(SceneObject sceneObject)
    {
        if(ScrollRect.TryGetActiveObject(sceneObject, out GameObject activeGO))
        {
            HierarchyObjectElement element = activeGO.GetComponent<HierarchyObjectElement>();
            element.RefreshName();
        }
    }
    public GameObject GetObject(Transform parent)
    {
        return SimplePool.Instance.SpawnUI(HierarchyPrefab, parent);
    }
    public void ReturnObject(GameObject go)
    {
        //Debug.Log("returning " + go.name);
        HierarchyObjectElement hierarchyObject = go.GetComponent<HierarchyObjectElement>();
        hierarchyObject.DeInit();
        SimplePool.Instance.DespawnUI(go);
    }
    public void ProvideData(GameObject go, int idx, object userData)
    {
        HierarchyObjectElement hierarchyObject = go.GetComponent<HierarchyObjectElement>();
        hierarchyObject.Init(userData as SceneObject);
        //Debug.LogWarning("Would provide data for ", transform.gameObject);
    }
}
