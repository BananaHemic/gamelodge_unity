using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviorDialog : GenericSingleton<BehaviorDialog>
{
    public ComponentAddTransition AddTransition;
    public PropertiesAndBehaviors PropertiesAndCodeObject;
    public RectTransform ButtonContainer;
    public GameObject ButtonPrefab;

    private readonly List<OptionButton> _behaviorButtons = new List<OptionButton>();

    void Start()
    {
        //Debug.Log("Will init behavior dialog");
        UpdateAvailableBehaviors();
    }
    private void UpdateAvailableBehaviors()
    {
        CSharpBehaviorInfo[] csharpBehaviors = CSharpBehaviorManager.Instance.GetAllCSharpBehaviors();
        for(int i = 0; i < csharpBehaviors.Length; i++)
        {
            //Debug.Log("Adding " + behaviorData.Name);
            GameObject newButton = GameObject.Instantiate(ButtonPrefab, ButtonContainer);
            OptionButton addBehaviorButton = newButton.GetComponent<OptionButton>();
            //addBehaviorButton.Init(behaviorData, this);
            _behaviorButtons.Add(addBehaviorButton);
        }
    }
    public void BehaviorClicked(BehaviorInfo behavior)
    {
        //Debug.Log("Will add behavior " + behavior.name);
        PropertiesAndCodeObject.AddComponentToSelectedObject(behavior);
        // Now close, as the user added their component
        AddTransition.CloseComponentAdd();
    }
    public void CloseClicked()
    {
        //Debug.Log("User clicked out");
        AddTransition.CloseComponentAdd();
    }
}
