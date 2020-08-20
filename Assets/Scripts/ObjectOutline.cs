using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ObjectOutline : MonoBehaviour
{
    // Storage of the meshes that have already has smooth normals created
    private static HashSet<Mesh> _registeredMeshes = new HashSet<Mesh>();
    // Property IDs for configuring materials
    private static bool _hasPropIDs = false;
    private static int _outlineWidthPropID;
    private static int _outlineColorPropID;
    private static int _zTestPropID;

    public OutlineState CurrentState { get; private set; }

    public enum OutlineState
    {
        Off,
        BuildHover,
        GameHover,
        OutlineWhenOccluded
    }

    private enum Mode
    {
        OutlineAll,
        OutlineVisible,
        OutlineHidden,
        OutlineAndSilhouette,
        SilhouetteOnly
    }

    [SerializeField]
    private Mode _outlineMode;

    [SerializeField]
    private Color outlineColor = Color.white;

    [SerializeField, Range(0f, 10f)]
    private float outlineWidth = 4f;

    // TODO create smooth normals in uploader
    //[SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
    //+ "Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    //private bool precomputeOutline;
    //[SerializeField, HideInInspector]
    //private List<Mesh> bakeKeys = new List<Mesh>();
    //[SerializeField, HideInInspector]
    //private List<ListVector3> bakeValues = new List<ListVector3>();

    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<MeshFilter> _meshFilters = new List<MeshFilter>();
    private readonly List<SkinnedMeshRenderer> _skinnedMeshRenderers = new List<SkinnedMeshRenderer>();
    private Material _outlineMaskMaterial;
    private Material _outlineFillMaterial;
    private bool _isLoadingBundleItem;
    private SceneObject _sceneObject;
    private BundleItem _loadedBundleItem;
    private bool _hasAddedRenderers = false;
    private bool _hasValidSmoothNormals = true;

    void Awake()
    {
        // Instantiate outline materials
        _outlineMaskMaterial = FileCache.Instance.GetMaterial("OutlineMask");
        _outlineFillMaterial = FileCache.Instance.GetMaterial("OutlineFill");
        //outlineMaskMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineMask"));
        //outlineFillMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineFill"));

        //outlineMaskMaterial.name = "OutlineMask (Instance)";
        //outlineFillMaterial.name = "OutlineFill (Instance)";
        CurrentState = OutlineState.Off;
    }
    private void AddRenderers()
    {
        if (_sceneObject.IsLoadingModel)
            Debug.LogError("Can't add renderers while loading on #" + _sceneObject.GetID(), _sceneObject.gameObject);
        if (_hasAddedRenderers)
            return;
        _hasAddedRenderers = true;
        foreach (var renderer in _renderers)
        {
            // Append outline shaders
            var materials = renderer.sharedMaterials;
            var appendedMaterials = new Material[materials.Length + 2];
            for (int i = 0; i < materials.Length; i++)
                appendedMaterials[i] = materials[i];
            appendedMaterials[materials.Length] = _outlineMaskMaterial;
            appendedMaterials[materials.Length + 1] = _outlineFillMaterial;
            renderer.materials = appendedMaterials;
        }
    }
    private void RemoveRenderers()
    {
        if (!_hasAddedRenderers)
            return;
        _hasAddedRenderers = false;
        foreach (var renderer in _renderers)
        {
            // If we're switching to off, remove the extra materials
            var materials = renderer.sharedMaterials;
            if(materials.Length < 2)
            {
                Debug.LogError("Can't remove materials, we only have " + materials.Length + " on #" + _sceneObject.GetID(), _sceneObject.gameObject);
                return;
            }
            var originalMaterials = new Material[materials.Length - 2];
            for (int i = 0; i < originalMaterials.Length; i++)
                originalMaterials[i] = materials[i];
            renderer.materials = originalMaterials;
        }
    }
    public void SetHighlightState(OutlineState state)
    {
        if (CurrentState == state)
            return;
        if (_sceneObject.IsLoadingModel)
            return;
        if (!_hasValidSmoothNormals)
            return;
        switch (state)
        {
            case OutlineState.BuildHover:
            case OutlineState.GameHover:
                _outlineMode = Mode.OutlineAll;
                AddRenderers();
                break;
            case OutlineState.OutlineWhenOccluded:
                _outlineMode = Mode.OutlineHidden;
                AddRenderers();
                break;
            case OutlineState.Off:
                RemoveRenderers();
                break;
            default:
                Debug.LogError("Unhandled OutlineState " + state);
                return;
        }
        CurrentState = state;
        UpdateMaterialProperties();
    }
    public void Init(SceneObject sceneObject)
    {
        _sceneObject = sceneObject;
    }
    public void AddObjectModel(GameObject obj)
    {
        obj.GetComponentsInChildren<Renderer>(_renderers);
        obj.GetComponentsInChildren<MeshFilter>(_meshFilters);
        obj.GetComponentsInChildren<SkinnedMeshRenderer>(_skinnedMeshRenderers);
        // Retrieve or generate smooth normals
        LoadSmoothNormals();
    }
    void OnDestroy()
    {
        // Destroy material instances
        //Destroy(_outlineMaskMaterial);
        //Destroy(_outlineFillMaterial);
    }
    void LoadSmoothNormals()
    {
        // if we're still loading the bundle item, we can drop this
        if (_isLoadingBundleItem)
            return;

        // Retrieve or generate smooth normals
        for(int i = 0; i < _meshFilters.Count;i++)
        {
            MeshFilter meshFilter = _meshFilters[i];
            // Skip if smooth normals have already been adopted
            if (_registeredMeshes.Contains(meshFilter.sharedMesh))
                continue;

            if (_loadedBundleItem == null)
            {
                // Load the pre-computed at upload time smooth normals
                // from the bundle item
                _isLoadingBundleItem = true;
                //Debug.Log("Will load bundle item before setting outline");
                BundleManager.Instance.LoadBundleItem(_sceneObject.BundleID, _sceneObject.BundleIndex, 0, OnLoadedBundleItem);
                return;
            }

            // Sometimes the smooth normals were not calculated
            if(_loadedBundleItem.SmoothNormals == null
                || _loadedBundleItem.SmoothNormals.Count <= i)
            {
                _registeredMeshes.Add(meshFilter.sharedMesh);
                _hasValidSmoothNormals = false;
                continue;
            }

            List<Vector3> smoothNormals = _loadedBundleItem.SmoothNormals[i];
            // Store smooth normals in UV3
            meshFilter.sharedMesh.SetUVs(3, smoothNormals);
            //Debug.Log("Done configuring smooth normals for this mesh");
            _registeredMeshes.Add(meshFilter.sharedMesh);
        }

        if(!_hasValidSmoothNormals && CurrentState != OutlineState.Off)
            RemoveRenderers();

        // Clear UV3 on skinned mesh renderers
        //foreach (var skinnedMeshRenderer in _skinnedMeshRenderers)
        //{
        //    if (_registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
        //    {
        //        skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
        //    }
        //}
    }
    void OnLoadedBundleItem(int loadID, BundleItem bundleItem)
    {
        if (!_isLoadingBundleItem)
            Debug.LogError("Loaded bundle item even though we weren't marked as loading");
        _isLoadingBundleItem = false;
        if(bundleItem == null)
        {
            Debug.LogError("Failed to load bundle item for outline");
            return;
        }
        _loadedBundleItem = bundleItem;
        //Debug.Log("Bundle Item loaded, will now apply normals");
        LoadSmoothNormals();
    }
    void UpdateMaterialProperties()
    {
        // Get the property IDs if needed
        if (!_hasPropIDs)
        {
            _outlineColorPropID = Shader.PropertyToID("_OutlineColor");
            _outlineWidthPropID = Shader.PropertyToID("_OutlineWidth");
            _zTestPropID = Shader.PropertyToID("_ZTest");
            _hasPropIDs = true;
        }

        // Apply properties according to mode
        _outlineFillMaterial.SetColor(_outlineColorPropID, outlineColor);

        switch (_outlineMode)
        {
            case Mode.OutlineAll:
                _outlineMaskMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Always);
                _outlineFillMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Always);
                _outlineFillMaterial.SetFloat(_outlineWidthPropID, outlineWidth);
                break;

            case Mode.OutlineVisible:
                _outlineMaskMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Always);
                _outlineFillMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                _outlineFillMaterial.SetFloat(_outlineWidthPropID, outlineWidth);
                break;

            case Mode.OutlineHidden:
                _outlineMaskMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Always);
                _outlineFillMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Greater);
                _outlineFillMaterial.SetFloat(_outlineWidthPropID, outlineWidth);
                break;

            case Mode.OutlineAndSilhouette:
                _outlineMaskMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                _outlineFillMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Always);
                _outlineFillMaterial.SetFloat(_outlineWidthPropID, outlineWidth);
                break;

            case Mode.SilhouetteOnly:
                _outlineMaskMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                _outlineFillMaterial.SetFloat(_zTestPropID, (float)UnityEngine.Rendering.CompareFunction.Greater);
                _outlineFillMaterial.SetFloat(_outlineWidthPropID, 0);
                break;
        }
    }
}
