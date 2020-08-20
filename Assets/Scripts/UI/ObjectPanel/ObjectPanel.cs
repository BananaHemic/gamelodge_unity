using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectPanel : BasePanel<ObjectPanel>
{
    public Button HierarchyButton;
    public Button PropertiesAndBehaviorsButton;
    public Button MaterialButton;
    public Button CodeButton;
    public ObjectHierarchy ObjectHierarchyObj;
    public PropertiesAndBehaviors PropertiesAndBehaviorsObj;
    public MaterialSettings MaterialSettingsObj;
    public CodeUI CodeUIObj;
    public RectTransform ObjectTopBarButtonContainer;
    public CanvasToggle HierarchyContainer;
    public CanvasToggle PropertiesContainer;
    public CanvasToggle MaterialContainer;
    public CanvasToggle CodeContainer;
    public RectTransform ObjectScrollViewContent;
    public ObjectMode CurrentMode { get; private set; }
    // Stuff to enable/disable with each mode
    public Image[] NonHierarchyModeObjects;
    public Image[] NonPropertiesModeObjects;
    public Image[] NonMaterialModeObjects;
    public Image[] CodeModeObjects;

    private SceneObject _selectedSceneObject;
    private bool _hasRefreshed = false;
    private int _frameOnLastSetSelection = -1;

    public enum ObjectMode
    {
        Hierarchy,
        Properties,
        Material,
        Code
    }

    protected override void Awake()
    {
        base.Awake();
        // RLD may not be alive if we're in VR, so we wait for it here
        if (RLD.RTObjectSelection.Get != null)
            RLD.RTObjectSelection.Get.Changed += OnSceneObjectSelectionChanged;
        else
            StartCoroutine(WaitForRLD());

        SceneObjectManager.OnSceneObjectRemoved += OnSceneObjectRemoved;
    }
    private void Start()
    {
        if(!_hasRefreshed)
            RefreshForMode();
    }
    private IEnumerator WaitForRLD()
    {
        while (RLD.RTObjectSelection.Get == null)
            yield return null;
        RLD.RTObjectSelection.Get.Changed += OnSceneObjectSelectionChanged;
    }
    private void OnSceneObjectRemoved(SceneObject removedObj)
    {
        // If this was a currently selected object, we refresh
        if(_selectedSceneObject != null && _selectedSceneObject == removedObj)
        {
            Debug.Log("Selected object removing, will refresh");
            _selectedSceneObject = null;
            RefreshForMode();
        }
    }
    public void OnHierarchyObjectClicked(SceneObject sceneObject)
    {
        CurrentMode = ObjectMode.Properties;
        _selectedSceneObject = sceneObject;
        _frameOnLastSetSelection = Time.frameCount;
        if(_selectedSceneObject == null)
        {
            Debug.LogWarning("Hierarchy button clicked, but no object selected");
            return;
        }
        if(VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
            && RLD.RTObjectSelection.Get != null)
            RLD.RTObjectSelection.Get.SetSelectedObjects(new List<GameObject>() { sceneObject.gameObject }, true);
        Debug.Log("Hierarchy clicked for " + sceneObject);
        RefreshForMode();
    }
    public void LocallyClearAll()
    {
        ObjectHierarchyObj.ClearObjects();
        PropertiesAndBehaviorsObj.InitForSelectedObject(null);
        MaterialSettingsObj.InitForSelectedObject(null);
        CodeUIObj.InitForSelectedSceneObject(null);
    }
    public void OnHierarchyButtonClicked()
    {
        CurrentMode = ObjectMode.Hierarchy;
        RefreshForMode();
    }
    public void OnPropertiesAndBehaviorsButtonClicked()
    {
        CurrentMode = ObjectMode.Properties;
        RefreshForMode();
    }
    public void OnMaterialButtonClicked()
    {
        CurrentMode = ObjectMode.Material;
        RefreshForMode();
    }
    public void OnCodeButtonClicked()
    {
        CurrentMode = ObjectMode.Code;
        RefreshForMode();
    }
    void OnSceneObjectSelectionChanged(RLD.ObjectSelectionChangedEventArgs args)
    {
        // If we ourselves just set the selected object, we can probably ignore whatever
        // RLD is saying
        if (_frameOnLastSetSelection == Time.frameCount)
            return;
        List<GameObject> selected = RLD.RTObjectSelection.Get.SelectedObjects;
        if (_selectedSceneObject != null && selected.Contains(_selectedSceneObject.gameObject))
            return;

        if (selected.Count == 0)
            return;
        else
            _selectedSceneObject = selected[0].GetComponent<SceneObject>();

        //Debug.Log("Now have selected = " + selected.Count);
        if (CurrentMode == ObjectMode.Properties)
            PropertiesAndBehaviorsObj.InitForSelectedObject(_selectedSceneObject);
        else if (CurrentMode == ObjectMode.Material)
            MaterialSettingsObj.InitForSelectedObject(_selectedSceneObject);
        else if (CurrentMode == ObjectMode.Code)
            CodeUIObj.InitForSelectedSceneObject(_selectedSceneObject);
    }
    protected override void RefreshForMode()
    {
        _hasRefreshed = true;
        //Debug.LogWarning("Object panel refreshing visible " + _isVisible + " inter " + _isInteractable + " mode " + CurrentMode);
        // Buttons
        HierarchyButton.interactable = CurrentMode != ObjectMode.Hierarchy;
        PropertiesAndBehaviorsButton.interactable = CurrentMode != ObjectMode.Properties;
        MaterialButton.interactable = CurrentMode != ObjectMode.Material;
        CodeButton.interactable = CurrentMode != ObjectMode.Code;
        // Scripts
        ObjectHierarchyObj.enabled = CurrentMode == ObjectMode.Hierarchy;
        PropertiesAndBehaviorsObj.enabled = CurrentMode == ObjectMode.Properties;
        MaterialSettingsObj.enabled = CurrentMode == ObjectMode.Material;
        CodeUIObj.enabled = CurrentMode == ObjectMode.Code;
        // Canvas / GraphicRayCaster
        HierarchyContainer.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == ObjectMode.Hierarchy,
            _isInteractable && CurrentMode == ObjectMode.Hierarchy);
        PropertiesContainer.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == ObjectMode.Properties,
            _isInteractable && CurrentMode == ObjectMode.Properties);
        MaterialContainer.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == ObjectMode.Material,
            _isInteractable && CurrentMode == ObjectMode.Material);
        CodeContainer.SetVisibilityAndInteractability(
            _isVisible && CurrentMode == ObjectMode.Code,
            _isInteractable && CurrentMode == ObjectMode.Code);

        foreach (var obj in NonHierarchyModeObjects)
            obj.enabled = (CurrentMode != ObjectMode.Hierarchy);
        foreach (var obj in NonPropertiesModeObjects)
            obj.enabled = (CurrentMode != ObjectMode.Properties);
        foreach (var obj in NonMaterialModeObjects)
            obj.enabled = (CurrentMode != ObjectMode.Material);
        foreach (var obj in CodeModeObjects)
            obj.enabled = (CurrentMode != ObjectMode.Code);

        if(CurrentMode == ObjectMode.Hierarchy)
        {
            List<SceneObject> sceneObjects = SceneObjectManager.Instance.GetAllSceneObjects();
            //ObjectHierarchyObj.InitForObjects(sceneObjects);
        } else if(CurrentMode == ObjectMode.Properties)
        {
            PropertiesAndBehaviorsObj.InitForSelectedObject(_selectedSceneObject);
        }else if(CurrentMode == ObjectMode.Material)
        {
            MaterialSettingsObj.InitForSelectedObject(_selectedSceneObject);
        }else if(CurrentMode == ObjectMode.Code)
        {
            CodeUIObj.InitForSelectedSceneObject(_selectedSceneObject);
        }
        // Close the option popup
        OptionPopup.Instance.Close();
    }
}
