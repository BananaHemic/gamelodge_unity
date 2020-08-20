using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeButtons : MonoBehaviour
{
    public CanvasToggle ExpandedTimeButtons;
    private bool _areTimeButtonsOpen;
    public CanvasToggleListener[] UIItemsToUpdate;

    void Start()
    {
        ExpandedTimeButtons.SetOn(_areTimeButtonsOpen);
        Refresh();
    }
    public void ToggleTimeButtonsOpen()
    {
        _areTimeButtonsOpen = !_areTimeButtonsOpen;
        Refresh();
    }
    private void Refresh()
    {
        ExpandedTimeButtons.SetOn(_areTimeButtonsOpen);
        for (int i = 0; i < UIItemsToUpdate.Length; i++)
            UIItemsToUpdate[i].RefreshFromCanvasToggleChange();
    }
}
