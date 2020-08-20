using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PropertiesAndBehaviors : GenericSingleton<PropertiesAndBehaviors>
{
    public RectTransform Container;
    public RectTransform OpenAddComponentMenuButton;
    public TMP_InputField NameInputField;
    public GameObject TopBar;
    public GameObject NoObjectSelectedText;
    public GameObject ComponentCardPrefab;
    public Toggle IsEnabledToggle;

    public SceneObject SelectedObject { get; private set; }

    private readonly List<ComponentCard> _componentCards = new List<ComponentCard>();
    private readonly Dictionary<BaseBehavior, ComponentCard> _behavior2ComponentCard = new Dictionary<BaseBehavior, ComponentCard>();

    const string NameFieldIdentifier = "propBehavNameField";

    private void OnDisable()
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        // Clear out all the stuff we added
        //foreach (var card in _componentCards)
        //{
        //    card.Reset();
        //    SimplePool.Instance.DespawnUI(card.gameObject);
        //}
        //_componentCards.Clear();
        //_behavior2ComponentCard.Clear();
        //foreach (var button in _behaviorButtons)
        //    GameObject.Destroy(button.gameObject);
        //_behaviorButtons.Clear();

        //OpenAddComponentMenuButton.gameObject.SetActive(false);
        //NameInputField.gameObject.SetActive(false);
    }
    public void OnNameInputFieldSelected()
    {
        RLDHelper.Instance.RegisterInputSelected(NameFieldIdentifier);
    }
    public void OnNameInputFieldDeselected()
    {
        RLDHelper.Instance.RegisterInputDeselected(NameFieldIdentifier);
    }
    private void RefreshForMode()
    {
        //OpenAddComponentMenuButton.gameObject.SetActive(true);
        //NameInputField.gameObject.SetActive(true);
        // Clear all component cards
        foreach (var card in _componentCards)
        {
            card.Reset();
            SimplePool.Instance.DespawnUI(card.gameObject);
        }
        _componentCards.Clear();
        _behavior2ComponentCard.Clear();

        NoObjectSelectedText.SetActive(SelectedObject == null);
        TopBar.SetActive(SelectedObject != null);

        if(SelectedObject != null)
        {
            // Set the name input field
            NameInputField.text = SelectedObject.Name;
            // Set if the object is enabled
            IsEnabledToggle.isOn = SelectedObject.IsEnabled;
            // Add existing component cards
            List<BaseBehavior> behaviors = SelectedObject.GetBehaviors();
            foreach(var behavior in behaviors)
                AddComponentCard(behavior);
        }
        OpenAddComponentMenuButton.gameObject.SetActive(SelectedObject != null);
    }
    public void InitForSelectedObject(SceneObject selectedObject)
    {
        //Debug.Log("properties init for " + (selectedObject == null ? "null" : selectedObject.Name));
        SelectedObject = selectedObject;
        RefreshForMode();
    }
    public void OnNameInputFieldEndChange()
    {
        if(SelectedObject == null)
        {
            Debug.LogError("Can't handle name change, no selected object");
            return;
        }
        if(NameInputField.text == SelectedObject.Name)
        {
            Debug.Log("Name input did not change: " + SelectedObject.Name);
            return;
        }
        Debug.Log("Name change " + SelectedObject.Name + "->" + NameInputField.text);
        SelectedObject.UpdateName(NameInputField.text, true, false);
    }
    public void OnIsActiveToggleChange(bool isActive)
    {
        if(SelectedObject == null)
        {
            Debug.LogError("Can't handle IsActive change, there's no selected object");
            return;
        }
        if (isActive == SelectedObject.IsEnabled)
        {
            Debug.Log("Enabled is same as scene object: " + isActive);
            return;
        }
        SelectedObject.SetEnabled(isActive, true);
    }
    public void AddComponentToSelectedObject(BehaviorInfo behaviorData)
    {
        Debug.Log("Will add component " + (behaviorData == null ? "null" : behaviorData.Name) + " to object " + (SelectedObject == null ? "null" : SelectedObject.GetID().ToString()));
        BaseBehavior behavior = SelectedObject.AddBehavior(behaviorData, true, true, null);
        // Refresh
        RefreshForMode();
    }
    public void RemoveComponentCard(BaseBehavior behavior)
    {
        ComponentCard componentCard;
        if(!_behavior2ComponentCard.TryGetValue(behavior, out componentCard))
        {
            Debug.Log("Not removing component card for " + behavior.GetBehaviorInfo().BehaviorID + " not found");
            return;
        }
        _componentCards.Remove(componentCard);
        _behavior2ComponentCard.Remove(behavior);
        componentCard.Reset();
        SimplePool.Instance.DespawnUI(componentCard.gameObject);
    }
    public void AddComponentCard(BaseBehavior behavior)
    {
        GameObject newCardObj = SimplePool.Instance.SpawnUI(ComponentCardPrefab, Container);
        ComponentCard card = newCardObj.GetComponent<ComponentCard>();
        card.Init(behavior, this);
        _componentCards.Add(card);
        _behavior2ComponentCard.Add(behavior, card);
    }
    public void RefreshComponent(BaseBehavior behavior)
    {
        ComponentCard componentCard;
        if(!_behavior2ComponentCard.TryGetValue(behavior, out componentCard))
        {
            Debug.Log("Not refreshing component card for " + behavior.GetBehaviorInfo().BehaviorID + " not found");
            return;
        }
        componentCard.Refresh();
    }
    public ComponentCard GetComponentCardForBehavior(BaseBehavior behavior)
    {
        return _behavior2ComponentCard[behavior];
    }
    public void RemoveCard(ComponentCard card)
    {
        SelectedObject.RemoveBehavior(card.BehaviorInstance, true);
        _componentCards.Remove(card);
        _behavior2ComponentCard.Remove(card.BehaviorInstance);
        GameObject.Destroy(card.gameObject);
    }
    private void OnAddComponentCallback(bool wasCancel, int callbackID)
    {
        if (wasCancel)
            return;

        bool wasNetworkScript = callbackID >> 8 * sizeof(ushort) == 1;
        ushort behaviorID = (ushort)(callbackID & ushort.MaxValue);
        //Debug.Log("Selected " + (wasNetworkScript ? "network" : "prebuilt") + " script #" + behaviorID);

        BehaviorInfo behaviorInfo = wasNetworkScript ? UserScriptManager.Instance.GetBehaviorInfoFromID(behaviorID) : CSharpBehaviorManager.Instance.GetBehaviorInfoFromID(behaviorID);
        AddComponentToSelectedObject(behaviorInfo);
    }
    public void OnAddComponentButtonClicked()
    {
        //TODO do not show behaviors that have already been added
        CSharpBehaviorInfo[] cSharpBehaviorInfos = CSharpBehaviorManager.Instance.GetAllCSharpBehaviors();
        List<MiniscriptBehaviorInfo> miniscriptBehaviorInfos = UserScriptManager.Instance.GetAllNetworkBehaviors();
        OptionPopup.Instance.GetListsToLoadInto(out List<string> optionTexts, out List<int> callbackData, out List<Sprite> icons);

        int idx = 0;
        for(int i = 0; i < cSharpBehaviorInfos.Length; i++)
        {
            BehaviorInfo behaviorInfo = cSharpBehaviorInfos[i];
            BaseBehavior existing = SelectedObject.GetBehaviorWithID(false, behaviorInfo.BehaviorID);
            if (existing != null)
                continue;
            optionTexts.Add(behaviorInfo.Name);
            icons.Add(behaviorInfo.DisplaySprite);
            //Debug.Log("c# icon: " + behaviorInfo.DisplaySprite);
            int callbackDatum = (behaviorInfo.IsNetworkedScript() ? 1 : 0) << 8 * sizeof(ushort);
            callbackDatum |= behaviorInfo.BehaviorID;
            callbackData.Add(callbackDatum);
            idx++;
        }
        for(int i = 0; i < miniscriptBehaviorInfos.Count; i++)
        {
            BehaviorInfo behaviorInfo = miniscriptBehaviorInfos[i];
            BaseBehavior existing = SelectedObject.GetBehaviorWithID(true, behaviorInfo.BehaviorID);
            if (existing != null)
                continue;
            optionTexts.Add(behaviorInfo.Name);
            icons.Add(UserScriptManager.Instance.UserScriptDisplaySprite);
            int callbackDatum = (behaviorInfo.IsNetworkedScript() ? 1 : 0) << 8 * sizeof(ushort);
            callbackDatum |= behaviorInfo.BehaviorID;
            callbackData.Add(callbackDatum);
            idx++;
        }

        OptionPopup.Instance.LoadOptions("Add component", optionTexts, icons, OnAddComponentCallback, callbackData);
    }
}
