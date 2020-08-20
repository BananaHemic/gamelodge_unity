using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AvatarControlsUI : GenericSingleton<AvatarControlsUI>
{
    public Slider BrowDownL;
    public Slider BrowDownR;
    public Slider BrowUpL;
    public Slider BrowUpR;

    const float BlendScaleFactor = 100f;
    const int BrowDownL_Idx = 17;
    const int BrowDownR_Idx = 19;
    const int BrowUpL_Idx = 16;
    const int BrowUpR_Idx = 18;

    protected override void Awake()
    {
        base.Awake();
        // TODO auto-create blend shape sliders, other than phoneme ones
        // Based on what the occupied user object has
        // TODO Send unreliable while changing
    }
    public void OnBrowDownLValueChange(float val)
    {
        val *= 100;
        UserManager.Instance.LocalUserDisplay.SetUserBlend(BrowDownL_Idx, val, true);
    }
    public void OnBrowDownRValueChange(float val)
    {
        val *= 100;
        UserManager.Instance.LocalUserDisplay.SetUserBlend(BrowDownR_Idx, val, true);
    }
    public void OnBrowUpLValueChange(float val)
    {
        val *= 100;
        UserManager.Instance.LocalUserDisplay.SetUserBlend(BrowUpL_Idx, val, true);
    }
    public void OnBrowUpRValueChange(float val)
    {
        val *= 100;
        UserManager.Instance.LocalUserDisplay.SetUserBlend(BrowUpR_Idx, val, true);
    }
}
