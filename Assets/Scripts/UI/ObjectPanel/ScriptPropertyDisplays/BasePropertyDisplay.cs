using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public abstract class BasePropertyDisplay : MonoBehaviour
{
    public TextMeshProUGUI PropertyNameText;

    protected string propertyName;
    private ComponentCard _parentCard;
    private BaseBehavior _behavior;
    protected bool IsChanging { get; private set; }
    private  Coroutine _sendVariableUpdate;
    private int _frameOnLastUpdate;
    const float TimeStationaryForServerUpdate = 0.3f;

    protected void Init(string propertyName, ComponentCard componentCard, BaseBehavior baseBehavior)
    {
        _parentCard = componentCard;
        _behavior = baseBehavior;
        this.propertyName = propertyName;
        //Debug.Log("Ranged float init " + this.propertyName);
        PropertyNameText.SetText(propertyName);
    }
    // Update the behavior, using the display's current state
    protected abstract void UpdateBehaviorFromValue(BaseBehavior baseBehavior);
    // Update the display's internal value, from the value stored within behavior
    protected abstract void UpdateValueFromBehavior(BaseBehavior baseBehavior);
    // Update the display's UI
    protected abstract void UpdateDisplayFromValueChange();

    protected void OnValueChanged(bool immediateReliableUpdate, bool updateUI=true, bool updateServer=true)
    {
        if (IsChanging)
            return;
        IsChanging = true;
        // Actually update the behavior
        UpdateBehaviorFromValue(_behavior);

        _frameOnLastUpdate = Time.frameCount;
        if (!immediateReliableUpdate && updateServer)
        {
            // If not reliable now, do a reliable update in a bit
            if (_sendVariableUpdate == null)
                _sendVariableUpdate = StartCoroutine(UpdateServerWhenDoneEditing());
        }
        else
        {
            // If a reliable now, no need to do one later
            if (_sendVariableUpdate != null)
                StopCoroutine(_sendVariableUpdate);
            _sendVariableUpdate = null;
        }
        // Notify the script of this change
        if(updateServer)
            _behavior.OnPropertiesChange(true, immediateReliableUpdate);
        if (updateUI)
            UpdateDisplayFromValueChange();
        IsChanging = false;
    }
    public void Refresh()
    {
        IsChanging = true;
        UpdateValueFromBehavior(_behavior);
        UpdateDisplayFromValueChange();
        IsChanging = false;
    }
    //protected abstract void Send
    IEnumerator UpdateServerWhenDoneEditing()
    {
        float timeStationary = 0;
        while (timeStationary < TimeStationaryForServerUpdate)
        {
            yield return null;
            // Coroutines run after Update() so this should be fine
            // Other than the first run, which seems to run syncronously with the caller
            if (_frameOnLastUpdate == Time.frameCount)
                timeStationary = 0;
            else
                timeStationary += Time.unscaledDeltaTime;
        }
        OnValueChanged(true, false);
    }
    public void DeInit()
    {
        if (_sendVariableUpdate != null)
        {
            // Send the update now, b/c the coroutine won't fire anyway
            OnValueChanged(true, false);
        }
        _sendVariableUpdate = null;
        ResetState();
    }
    /// <summary>
    /// Cleanup everything
    /// </summary>
    protected abstract void ResetState();
}
