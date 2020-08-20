using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;

public abstract class SceneObjectORBundleItemReferencePropertyDisplay : BasePropertyDisplay
{
    public TextMeshProUGUI SelectedName;
    public Sprite OptionSprite;

    private SerializedSceneObjectORBundleItemReference _orReference;

    private readonly List<SceneObject> _runtimeSceneObjectList = new List<SceneObject>();
    private readonly List<BundleItem> _bundleItemsToSelect = new List<BundleItem>();
    private SerializedSceneObjectORBundleItemReference.SerializeMode _currentMode;
    private BundleItem _bundleItem;
    private SceneObject _selectedSceneObject;
    private string _prevBundleID = null;
    private ushort _prevBundleIndex = ushort.MaxValue;
    private SceneObject _prevSceneObject = null;
    private bool _isLoadingBundleItem = false;
    private int _currentLoadID;
    private uint _currentLoadReceipt;
    // Does the option popup have a default option (this effects indexing in the callback)
    private bool _hasDefaultOption;

    protected abstract string GetDefaultOption();
    protected abstract string GetSelectOptionTitleText();
    protected abstract SubBundle.SubBundleType GetSubBundleType();
    protected abstract string GetRequiredBundleItemScript();
    /// <summary>
    /// Gets all the runtime instances that we may want to be able to select
    /// </summary>
    /// <param name="sceneObjects"></param>
    /// <returns></returns>
    protected virtual void GetAllRuntimeInstances(List<SceneObject> sceneObjects) { }

    public void Init(FieldInfo fieldInfo, ComponentCard componentCard, BaseBehavior baseBehavior)
    {
        _orReference = baseBehavior.GetSceneObjectORBundleItemReference(fieldInfo.Name);
        _currentMode = SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem;
        if(_orReference == null)
        {
            Debug.LogError("No bundle reference! for field " + fieldInfo.Name);
        }
        else
        {
            //Debug.Log("At init, reference has " + _bundleItemReference.BundleID + " # " + _bundleItemReference.BundleIndex);
        }
        UpdateValueFromBehavior(baseBehavior);
        UpdateDisplayFromValueChange();
        base.Init(fieldInfo.Name, componentCard, baseBehavior);
    }
    protected override void UpdateBehaviorFromValue(BaseBehavior baseBehavior)
    {
        if (_isLoadingBundleItem)
            Debug.LogWarning("Should not be updating value when we're loading bundle item!");

        if (_currentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.SceneObject)
            _orReference.UpdateFrom(_selectedSceneObject);
        else
            _orReference.UpdateFrom(_bundleItem.ContainingSubBundle.ContainingBundle, _bundleItem.BundleIndex);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        SerializedSceneObjectORBundleItemReference.SerializeMode newMode = _orReference != null ? _orReference.CurrentMode : SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem;
        string newID = _orReference != null ? _orReference.BundleID : null;
        ushort newIndex = _orReference != null ? _orReference.BundleIndex : ushort.MaxValue;
        SceneObject newSceneObject = _orReference != null ? _orReference.SceneObjectReference : null;

        // If something changed, we need to clear some loaded stuff
        if(_currentMode != newMode
            || _prevBundleID != newID
            || _prevBundleIndex != newIndex
            || _prevSceneObject != newSceneObject)
        {
            _currentMode = newMode;
            _bundleItem = null;
            _selectedSceneObject = null;
            _bundleItemsToSelect.Clear();
            _runtimeSceneObjectList.Clear();
            _prevBundleID = newID;
            _prevBundleIndex = newIndex;
            _prevSceneObject = newSceneObject;
            // Cancel any outstanding loads
            if(_currentLoadReceipt != uint.MaxValue)
            {
                Debug.Log("ReferenceProperty canceling load #" + _currentLoadReceipt);
                BundleManager.Instance.CancelLoad(_currentLoadReceipt);
            }
            _currentLoadReceipt = uint.MaxValue;
            if(_currentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem
                && !string.IsNullOrEmpty(newID))
            {
                // If this is a non-null reference, load the bundle item
                _isLoadingBundleItem = true;
                //Debug.Log("Loading bundleItem for " + newID + " #" + newIndex + " was " + _prevBundleID + " #" + _prevBundleIndex);
                int loadID = ++_currentLoadID;
                _currentLoadReceipt = BundleManager.Instance.LoadBundleItem(_orReference.BundleID, _orReference.BundleIndex, loadID, OnBundleItemLoaded);
            } else
                _isLoadingBundleItem = false;
        }
    }
    protected override void UpdateDisplayFromValueChange()
    {
        //Debug.Log("Updating display value");
        if(_orReference == null)
        {
            Debug.LogError("No bundle item reference!");
            SelectedName.text = GetDefaultOption() ?? "null";
            return;
        }

        if(_orReference.CurrentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem)
        {
            if(string.IsNullOrEmpty(_orReference.BundleID))
                SelectedName.text = GetDefaultOption() ?? "null";
            else if(_isLoadingBundleItem)
                // Get the item referenced, asyncronously
                SelectedName.text = "loading...";
            else
                SelectedName.text = _bundleItem.GetAssetName();
        }
        else
        {
            if(_orReference.SceneObjectReference == null)
                SelectedName.text = GetDefaultOption() ?? "null";
            else
                SelectedName.text = _orReference.SceneObjectReference.Name;
        }
    }
    private void OnBundleItemLoaded(int loadID, BundleItem bundleItem)
    {
        if(loadID != _currentLoadID)
        {
            Debug.LogWarning("Dropping bundle item load, was #" + loadID + " expected " + _currentLoadID);
            return;
        }
        _isLoadingBundleItem = false;
        if(bundleItem == null)
        {
            Debug.LogWarning("ReferencePropertyDisplay failed to load bundle item");
            return;
        }
        // Make sure this is for the current reference, and is not stale
        if(_orReference == null
            || _orReference.CurrentMode != SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem
            || _orReference.BundleID != bundleItem.ContainingSubBundle.ContainingBundle
            || _orReference.BundleIndex != bundleItem.BundleIndex)
        {
            Debug.LogWarning("Dropping stale bundle item load!");
            return;
        }

        _bundleItem = bundleItem;
        UpdateDisplayFromValueChange();
    }
    private void OnOptionSelected(bool wasCancel, int callbackID)
    {
        if (wasCancel)
        {
            _bundleItemsToSelect.Clear();
            _runtimeSceneObjectList.Clear();
            return;
        }
        //if (_bundleItemsToSelect == null)
            //return;
        if (_orReference == null)
        {
            Debug.LogWarning("No bundle item reference?");
            return;
        }

        if (_hasDefaultOption && callbackID == 0)// 0 means null
        {
            _currentMode = SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem;
            _bundleItem = null;
            _selectedSceneObject = null;
        } else if (callbackID <= _runtimeSceneObjectList.Count)
        {
            // This is a runtime reference
            _currentMode = SerializedSceneObjectORBundleItemReference.SerializeMode.SceneObject;
            int idx = _hasDefaultOption ? callbackID - 1 : callbackID;
            _selectedSceneObject = _runtimeSceneObjectList[idx];
            _bundleItem = null;
        } else
        {
            // This is a bundle item reference
            int bundleNum = callbackID - _runtimeSceneObjectList.Count;
            if (_hasDefaultOption)
                bundleNum--;
            _bundleItem = _bundleItemsToSelect[bundleNum];
            _currentMode = SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem;
        }

        if(_currentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem)
            Debug.Log("Selected asset " + (_bundleItem == null ? "null" : _bundleItem.GetAssetName()) + " for reference");
        else
            Debug.Log("Selected obj " + (_selectedSceneObject == null ? "null" : _selectedSceneObject.Name) + " for reference");
        _bundleItemsToSelect.Clear();
        _runtimeSceneObjectList.Clear();
        base.OnValueChanged(true);
    }
    public void OnOpenOptionsClicked()
    {
        BundleManager.Instance.GetAllDownloadedBundleItemsOfType(_bundleItemsToSelect, GetSubBundleType(), GetRequiredBundleItemScript());
        string defaultOption = GetDefaultOption();
        int numItems = defaultOption == null ? _bundleItemsToSelect.Count : _bundleItemsToSelect.Count + 1;
        List<string> optionTexts;
        List<int> callbackData;
        List<Sprite> images;
        OptionPopup.Instance.GetListsToLoadInto(out optionTexts, out callbackData, out images);

        // First option is null option
        if (defaultOption != null)
        {
            optionTexts.Add(defaultOption);
            callbackData.Add(0);
            _hasDefaultOption = true;
        }
        else
            _hasDefaultOption = false;

        int optionIndex = _hasDefaultOption ? 1 : 0;
        // Load the the sceneobjects that may be relevant
        GetAllRuntimeInstances(_runtimeSceneObjectList);
        for(int i = 0; i < _runtimeSceneObjectList.Count; i++)
        {
            optionTexts.Add(_runtimeSceneObjectList[i].Name);
            callbackData.Add(optionIndex);
            optionIndex++;
        }
        // Load the relevant bundle items
        for(int i = 0; i < _bundleItemsToSelect.Count; i++)
        {
            optionTexts.Add(_bundleItemsToSelect[i].GetAssetName());
            callbackData.Add(optionIndex);
            optionIndex++;
        }

        OptionPopup.Instance.LoadOptions(GetSelectOptionTitleText(), optionTexts, OptionSprite, OnOptionSelected, callbackData);
    }
    protected override void ResetState()
    {
        Debug.Log("Reset state for property display");
        _orReference = null;
        _currentMode = SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem;
        _selectedSceneObject = null;
        _prevBundleID = null;
        _prevBundleIndex = ushort.MaxValue;
        _bundleItemsToSelect.Clear();
        _runtimeSceneObjectList.Clear();
        _isLoadingBundleItem = false;
        // Cancel any outstanding loads
        if(_currentLoadReceipt != uint.MaxValue)
        {
            Debug.Log("ReferenceProperty reset state cancelling load #" + _currentLoadReceipt);
            BundleManager.Instance.CancelLoad(_currentLoadReceipt);
        }
        _currentLoadReceipt = uint.MaxValue;
    }
}
