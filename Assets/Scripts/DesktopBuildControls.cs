using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesktopBuildControls : GenericSingleton<DesktopBuildControls>
{
    public GameObject RLDRoot;

    protected override void Awake()
    {
        base.Awake();
        ConfigureRLD();
        Orchestrator.OnModeChange += OnPlayBuildModeChange;
        VRSDKUtils.OnVRModeChanged += ConfigureRLD;
    }
    private void OnPlayBuildModeChange(Orchestrator.Modes toMode)
    {
        ConfigureRLD();
        if(VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
            && toMode == Orchestrator.Modes.BuildMode)
        {
            Orchestrator.Instance.MainCamera.transform.localPosition = Vector3.zero;
            Orchestrator.Instance.MainCamera.transform.localRotation = Quaternion.identity;
        }
    }
    private void ConfigureRLD()
    {
        RLDRoot.SetActive(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode
            && VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop);
    }
}
