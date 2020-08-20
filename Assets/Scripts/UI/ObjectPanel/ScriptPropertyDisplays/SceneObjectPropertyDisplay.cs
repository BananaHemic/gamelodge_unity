using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;

public abstract class SceneObjectPropertyDisplay : BasePropertyDisplay
{
    public TextMeshProUGUI SelectedName;
    public Sprite OptionSprite;

    private SerializedSceneObjectReference _sceneObjectReference;
    private SceneObject _selectedSceneObject;
    private readonly List<SceneObject> _sceneObjectsToSelect = new List<SceneObject>();
    private bool _hasNullOption = false;

    protected abstract string GetDefaultOption();
    protected abstract string GetSelectOptionTitleText();
    protected abstract string GetRequiredScript();

    public void Init(FieldInfo fieldInfo, ComponentCard componentCard, BaseBehavior baseBehavior)
    {
        _sceneObjectReference = baseBehavior.GetSceneObjectReference(fieldInfo.Name);
        if (_sceneObjectReference == null)
        {
            Debug.LogError("No SceneObject reference! for field " + fieldInfo.Name);
        }
        //else
            //Debug.Log("At init, reference has " + _bundleItemReference.BundleID + " # " + _bundleItemReference.BundleIndex);
        UpdateValueFromBehavior(baseBehavior);
        UpdateDisplayFromValueChange();
        base.Init(fieldInfo.Name, componentCard, baseBehavior);
    }
    protected override void UpdateBehaviorFromValue(BaseBehavior baseBehavior)
    {
        _sceneObjectReference.UpdateFrom(_selectedSceneObject);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        SceneObject newSceneObject = _sceneObjectReference?.SceneObjectReference;

        if (newSceneObject == _selectedSceneObject)
            return;

        UpdateDisplayFromValueChange();
    }
    protected override void UpdateDisplayFromValueChange()
    {
        //Debug.Log("Updating display value");
        if (_selectedSceneObject == null)
        {
            SelectedName.text = GetDefaultOption() ?? "null";
            return;
        }
        SelectedName.text = _selectedSceneObject.Name;
    }
    private void OnOptionSelected(bool wasCancel, int callbackID)
    {
        if (wasCancel)
        {
            _sceneObjectsToSelect.Clear();
            return;
        }
        if (_sceneObjectReference == null)
        {
            Debug.LogWarning("No bundle item reference?");
            return;
        }

        if (_hasNullOption)
        {
            if (callbackID == 0) // 0 means null
                _selectedSceneObject = null;
            else
                _selectedSceneObject = _sceneObjectsToSelect[callbackID - 1];
        }
        else
        {
            _selectedSceneObject = _sceneObjectsToSelect[callbackID];
        }
        Debug.Log("Selected " + (_selectedSceneObject == null ? "null" : _selectedSceneObject.Name) + " for reference");
        _sceneObjectsToSelect.Clear();
        base.OnValueChanged(true);
    }
    /// <summary>
    /// Determine which SceneObjects we actually want to select
    /// Children should override and replace this method to find only the SceneObjects that they care
    /// about.
    /// </summary>
    /// <param name="allSceneObjects"></param>
    /// <param name="selectedSceneObjects"></param>
    protected virtual void SelectSceneObjects(List<SceneObject> allSceneObjects, List<SceneObject> selectedSceneObjects)
    {
        selectedSceneObjects.AddRange(allSceneObjects);
    }
    public void OnOpenOptionsClicked()
    {
        List<SceneObject> allSceneObjects = SceneObjectManager.Instance.GetAllSceneObjects();
        SelectSceneObjects(allSceneObjects, _sceneObjectsToSelect);
        string defaultOption = GetDefaultOption();
        //int numItems = defaultOption == null ? _bundleItemsToSelect.Count : _bundleItemsToSelect.Count + 1;
        List<string> optionTexts;
        List<int> callbackData;
        List<Sprite> images;
        OptionPopup.Instance.GetListsToLoadInto(out optionTexts, out callbackData, out images);

        // First option is null option
        if (defaultOption != null)
        {
            optionTexts.Add(defaultOption);
            callbackData.Add(0);
        }

        _hasNullOption = defaultOption != null;
        int optionIndex = _hasNullOption ? 1 : 0;
        for (int i = 0; i < _sceneObjectsToSelect.Count; i++)
        {
            optionTexts.Add(_sceneObjectsToSelect[i].Name);
            callbackData.Add(optionIndex);
            optionIndex++;
        }
        OptionPopup.Instance.LoadOptions(GetSelectOptionTitleText(), optionTexts, OptionSprite, OnOptionSelected, callbackData);
    }
    protected override void ResetState()
    {
        Debug.Log("Reset state for property display");
        _sceneObjectReference = null;
    }
}
