using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class TMP_LinkHandler : MonoBehaviour, IPointerClickHandler
{
    public delegate void LinkSelectedFunc(IExposedProperty clickedProperty);
    public LinkSelectedFunc OnLinkSelected;

    private TMP_Text _text;
    private readonly List<IExposedProperty> _linkProperties = new List<IExposedProperty>();

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }
    public void SetLinks(List<ExposedVariable> exposedProperties)
    {
        _linkProperties.Clear();
        _linkProperties.AddRange(exposedProperties);
    }
    public void SetLinks(List<ExposedFunction> exposedProperties)
    {
        _linkProperties.Clear();
        _linkProperties.AddRange(exposedProperties);
    }
    public void SetLinks(List<ExposedEvent> exposedProperties)
    {
        _linkProperties.Clear();
        _linkProperties.AddRange(exposedProperties);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Pointer click");
        //int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, Input.mousePosition, Orchestrator.Instance.MainCamera);
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, Input.mousePosition, null);
        Debug.Log("click idx " + linkIndex);
        if (linkIndex != -1)
        { // was a link clicked?
            TMP_LinkInfo linkInfo = _text.textInfo.linkInfo[linkIndex];

            Debug.Log("Selected link with ID " + linkInfo.GetLinkID());
            int idx = int.Parse(linkInfo.GetLinkID());
            if (OnLinkSelected != null)
                OnLinkSelected(_linkProperties[idx]);
        }
    }
}
