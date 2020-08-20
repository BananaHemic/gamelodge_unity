using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditSettingsPanel : BasePanel<EditSettingsPanel>
{
    private bool _hasRefreshed = false;
    void Start()
    {
        if(!_hasRefreshed)
            RefreshForMode();
    }
    protected override void RefreshForMode()
    {
        _hasRefreshed = true;
    }
}
