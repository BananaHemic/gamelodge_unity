using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DisableImageFromButtonInteractable : MonoBehaviour
{
    public Button[] Buttons;
    public Image ImageToHide;
    private bool _isHidden = false;

    private bool AreAnyButtonsNotInteractable()
    {
        for (int i = 0; i < Buttons.Length; i++)
        {
            if (!Buttons[i].interactable)
                return true;
        }
        return false;
    }
    void Update()
    {
        bool shouldBeHidden = AreAnyButtonsNotInteractable();
        if (shouldBeHidden == _isHidden)
            return;
        ImageToHide.enabled = !shouldBeHidden;
        _isHidden = shouldBeHidden;
    }
}
