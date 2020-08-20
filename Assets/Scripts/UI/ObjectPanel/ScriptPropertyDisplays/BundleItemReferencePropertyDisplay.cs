using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;

public abstract class BundleItemReferencePropertyDisplay : BasePropertyDisplay
{
    public TextMeshProUGUI SelectedName;
    public Sprite OptionSprite;

    private SerializedBundleItemReference _bundleItemReference;

    private readonly List<BundleItem> _bundleItemsToSelect = new List<BundleItem>();
    private BundleItem _bundleItem;
    private string _prevBundleID = null;
    private ushort _prevBundleIndex = ushort.MaxValue;
    private bool _isLoadingBundleItem = false;
    private int _currentLoadID;
    private uint _currentLoadReceipt;

    protected abstract string GetDefaultOption();
    protected abstract string GetSelectOptionTitleText();
    protected abstract SubBundle.SubBundleType GetSubBundleType();
    protected abstract string GetRequiredScript();

    public void Init(FieldInfo fieldInfo, ComponentCard componentCard, BaseBehavior baseBehavior)
    {
        _bundleItemReference = baseBehavior.GetBundleItemReference(fieldInfo.Name);
        if(_bundleItemReference == null)
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

        if (_bundleItem == null)
            _bundleItemReference.UpdateFrom(null, ushort.MaxValue);
        else
            _bundleItemReference.UpdateFrom(_bundleItem.ContainingSubBundle.ContainingBundle, _bundleItem.BundleIndex);
    }
    protected override void UpdateValueFromBehavior(BaseBehavior baseBehavior)
    {
        string newID = _bundleItemReference != null ? _bundleItemReference.BundleID : null;
        ushort newIndex = _bundleItemReference != null ? _bundleItemReference.BundleIndex : ushort.MaxValue;

        //Debug.Log("Got current ref values: " + newID + " #" + newIndex);
        // If the ID or index changed, we need to clear some loaded stuff
        if(_prevBundleID != newID
            || _prevBundleIndex != newIndex)
        {
            _bundleItem = null;
            _bundleItemsToSelect.Clear();
            //Debug.Log("Loading bundleItem for " + newID + " #" + newIndex + " was " + _prevBundleID + " #" + _prevBundleIndex);
            _prevBundleID = newID;
            _prevBundleIndex = newIndex;
            // Cancel any outstanding loads
            if(_currentLoadReceipt != uint.MaxValue)
            {
                Debug.Log("ReferenceProperty canceling load #" + _currentLoadReceipt);
                BundleManager.Instance.CancelLoad(_currentLoadReceipt);
            }
            _currentLoadReceipt = uint.MaxValue;
            // If this is a non-null reference, load the bundle item
            if (!string.IsNullOrEmpty(newID))
            {
                _isLoadingBundleItem = true;
                //Debug.Log("Loading bundleItem for " + newID + " #" + newIndex + " was " + _prevBundleID + " #" + _prevBundleIndex);
                int loadID = ++_currentLoadID;
                _currentLoadReceipt = BundleManager.Instance.LoadBundleItem(_bundleItemReference.BundleID, _bundleItemReference.BundleIndex, loadID, OnBundleItemLoaded);
            }
            else
            {
                _isLoadingBundleItem = false;
            }
        }
    }
    protected override void UpdateDisplayFromValueChange()
    {
        //Debug.Log("Updating display value");
        if(_bundleItemReference == null)
        {
            Debug.LogError("No bundle item reference!");
            SelectedName.text = GetDefaultOption() ?? "null";
            return;
        }

        if(string.IsNullOrEmpty(_bundleItemReference.BundleID))
        {
            SelectedName.text = GetDefaultOption() ?? "null";
        }
        else if(_isLoadingBundleItem)
        {
            // Get the item referenced, asyncronously
            SelectedName.text = "loading...";
        }
        else
        {
            SelectedName.text = _bundleItem.GetAssetName();
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
        if(_bundleItemReference == null
            || _bundleItemReference.BundleID != bundleItem.ContainingSubBundle.ContainingBundle
            || _bundleItemReference.BundleIndex != bundleItem.BundleIndex)
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
            return;
        }
        if (_bundleItemsToSelect == null)
            return;
        if (_bundleItemReference == null)
        {
            Debug.LogWarning("No bundle item reference?");
            return;
        }

        if (callbackID == 0) // 0 means null
            _bundleItem = null;
        else
            _bundleItem = _bundleItemsToSelect[callbackID - 1];
        Debug.Log("Selected " + (_bundleItem == null ? "null" : _bundleItem.GetAssetName()) + " for reference");
        _bundleItemsToSelect.Clear();
        base.OnValueChanged(true);
    }
    public void OnOpenOptionsClicked()
    {
        BundleManager.Instance.GetAllDownloadedBundleItemsOfType(_bundleItemsToSelect, GetSubBundleType(), GetRequiredScript());
        string defaultOption = GetDefaultOption();
        int numItems = defaultOption == null ? _bundleItemsToSelect.Count : _bundleItemsToSelect.Count + 1;
        List<string> optionTexts;
        List<int> callbackData;
        List<Sprite> images;
        OptionPopup.Instance.GetListsToLoadInto(out optionTexts, out callbackData, out images);

        // First option is null option
        if(defaultOption != null)
        {
            optionTexts.Add(defaultOption);
            callbackData.Add(0);
        }

        int optionIndex = defaultOption == null ? 0 : 1;
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
        _bundleItemReference = null;
        _prevBundleID = null;
        _prevBundleIndex = ushort.MaxValue;
        _bundleItemsToSelect.Clear();
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
