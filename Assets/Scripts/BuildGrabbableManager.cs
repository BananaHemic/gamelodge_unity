using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildGrabbableManager : GenericSingleton<BuildGrabbableManager>
{
    public GameObject BuildGrabbablePrefab;
    public static readonly string BuildGrabbableName = "BuildGrabbable";

    private readonly List<BuildGrabbable> _allGrabbables = new List<BuildGrabbable>(1024);
    private readonly List<string> _names = new List<string>(1024);
    //private readonly Dictionary<ushort, BuildGrabbable> _sceneID2BuildGrabbable = new Dictionary<ushort, BuildGrabbable>();

    void Start()
    {
        RefreshForMode(Orchestrator.Instance.CurrentMode);
        Orchestrator.OnModeChange += RefreshForMode;
        SceneObjectManager.OnSceneObjectAdded += OnSceneObjectAdded;
        SceneObjectManager.OnSceneObjectRemoved += OnSceneObjectRemoved;
    }
    public void ClearAllBuildGrababbles()
    {
        for(int i = 0; i < _allGrabbables.Count; i++)
        {
            BuildGrabbable grab = _allGrabbables[i];
            if(grab == null)
                Debug.LogError("Null build grabbable: " + _names[i]);
            grab.SceneObject.ClearBuildGrabbable();
            grab.Reset();
            SimplePool.Instance.Despawn(grab.gameObject);
        }
        _allGrabbables.Clear();
        _names.Clear();
    }
    private void RefreshForMode(Orchestrator.Modes mode)
    {
        if(mode == Orchestrator.Modes.BuildMode)
        {
            // Add build grabbables for all scene objects
            var sceneObjects = SceneObjectManager.Instance.GetAllSceneObjects();
            for(int i = 0; i < sceneObjects.Count; i++)
                OnSceneObjectAdded(sceneObjects[i]);
        }
        else if(mode == Orchestrator.Modes.PlayMode)
        {
            for(int i = 0; i < _allGrabbables.Count; i++)
            {
                BuildGrabbable grab = _allGrabbables[i];
                if(grab == null)
                {
                    Debug.LogError("Null build grabbable: " + _names[i]);
                }
                grab.SceneObject.ClearBuildGrabbable();
                grab.Reset();
                SimplePool.Instance.Despawn(grab.gameObject);
            }
            _allGrabbables.Clear();
            _names.Clear();
        }
    }
    private void OnSceneObjectAdded(SceneObject sceneObject)
    {
        if (Orchestrator.Instance.CurrentMode != Orchestrator.Modes.BuildMode)
            return;
        BuildGrabbable buildGrabbable = SimplePool.Instance.Spawn(BuildGrabbablePrefab).GetComponent<BuildGrabbable>();
        buildGrabbable.name = BuildGrabbableName;
        buildGrabbable.Init(sceneObject);
        sceneObject.OnBuildGrabbableSet(buildGrabbable);
        _allGrabbables.Add(buildGrabbable);
        _names.Add(sceneObject.Name);
    }
    private void OnSceneObjectRemoved(SceneObject removedObj)
    {
        //Debug.Log("rmv #" + removedObj.GetID());
        if (Orchestrator.Instance.CurrentMode != Orchestrator.Modes.BuildMode)
            return;

        for(int i = 0; i < _allGrabbables.Count; i++)
        {
            BuildGrabbable buildGrab = _allGrabbables[i];
            if(buildGrab.SceneObject == removedObj)
            {
                // Strictly speaking, the below line could be removed
                // as the scene object is being deleted anyway
                buildGrab.SceneObject.ClearBuildGrabbable();
                buildGrab.Reset();
                SimplePool.Instance.Despawn(buildGrab.gameObject);
                _allGrabbables.RemoveBySwap(i);
                _names.RemoveBySwap(i);
                return;
            }
        }
        Debug.LogError("Failed to remove build grabbable for #" + removedObj.Name);
    }
}
