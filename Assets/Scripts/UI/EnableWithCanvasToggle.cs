using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class EnableWithCanvasToggle : CanvasToggleListener
{
    public bool EnableWhenCanvasesOff;
    public CanvasToggle[] Toggles;
    public CanvasToggle[] ParentToggles;

    private Image _mainImage;
    private bool _hasInit = false;

    void Start()
    {
        _mainImage = GetComponent<Image>();
        _hasInit = true;
        RefreshFromCanvasToggleChange();
    }
    public override void RefreshFromCanvasToggleChange()
    {
        if (!_hasInit)
            return;
        bool areAllOff = true;
        for(int i = 0; i < Toggles.Length; i++)
        {
            if (Toggles[i].IsVisible)
            {
                // Check parent toggles, if there is one
                if(ParentToggles.Length <= i || ParentToggles[i].IsVisible)
                {
                    areAllOff = false;
                    break;
                }
            }
        }
        _mainImage.enabled = areAllOff ? EnableWhenCanvasesOff : !EnableWhenCanvasesOff;
    }
}
