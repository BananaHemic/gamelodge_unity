using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserPrefabSpawner : GenericSingleton<UserPrefabSpawner>
{
    public Transform TrackingSpace;
    public GameObject OculusPrefab;
    public GameObject SteamVRPrefab;
    public GameObject DesktopObj;

    private GameObject _oculusObj;
    private GameObject _steamObj;
    private Camera _desktopCamera;
    private Camera _oculusCamera;
    private Camera _steamCamera;
    private GameObject _steamVRPersistentObj;
    private bool _hasCreatedInitial = false;

    protected override void Awake()
    {
        base.Awake();

        if(!_hasCreatedInitial)
            CreatePrefabsIfNeeded();
        _hasCreatedInitial = true;
        VRSDKUtils.OnVRModeChanged += CreatePrefabsIfNeeded;
    }
    public GameObject GetCurrentObject()
    {
        if(!_hasCreatedInitial)
            CreatePrefabsIfNeeded();

        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return DesktopObj;
            case VRSDKUtils.SDK.Oculus:
                return _oculusObj;
            case VRSDKUtils.SDK.OpenVR:
                return _steamObj;
        }
        return null;
    }
    public Camera GetCurrentCamera()
    {
        if(!_hasCreatedInitial)
            CreatePrefabsIfNeeded();

        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                return _desktopCamera;
            case VRSDKUtils.SDK.Oculus:
                return _oculusCamera;
            case VRSDKUtils.SDK.OpenVR:
                return _steamCamera;
        }
        return null;
    }
    void CreatePrefabsIfNeeded()
    {
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                DesktopObj.SetActive(true);
                if (_desktopCamera == null)
                    _desktopCamera = DesktopObj.GetComponent<Camera>();
                if (_oculusObj != null) _oculusObj.SetActive(false);
                if (_steamObj != null) _steamObj.SetActive(false);
                Orchestrator.Instance.OnCameraChanged(_desktopCamera);
                if (_steamVRPersistentObj != null) _steamVRPersistentObj.SetActive(false);
                return;
            case VRSDKUtils.SDK.Oculus:
                DesktopObj.SetActive(false);
                if (_oculusObj == null)
                {
                    _oculusObj = GameObject.Instantiate(OculusPrefab, TrackingSpace);
                    _oculusObj.transform.localPosition = Vector3.zero;
                    _oculusObj.transform.localRotation = Quaternion.identity;
                    OVRCameraRig rig = _oculusObj.GetComponent<OVRCameraRig>();
                    _oculusCamera = rig.centerEyeAnchor.GetComponent<Camera>();
                }
                _oculusObj.SetActive(true);
                if (_steamObj != null) _steamObj.SetActive(false);
                if (_steamVRPersistentObj != null) _steamVRPersistentObj.SetActive(false);
                Orchestrator.Instance.OnCameraChanged(_oculusCamera);
                return;
            case VRSDKUtils.SDK.OpenVR:
                DesktopObj.SetActive(false);
                if (_steamObj == null)
                {
                    _steamObj = GameObject.Instantiate(SteamVRPrefab, TrackingSpace);
                    _steamObj.transform.localPosition = Vector3.zero;
                    _steamObj.transform.localRotation = Quaternion.identity;
                    _steamCamera = _steamObj.GetComponentInChildren<Camera>();
                    // TODO precompiler to support OVR
                    _steamVRPersistentObj = Valve.VR.SteamVR_Behaviour.instance.gameObject;
                }
                else
                {
                    _steamObj.SetActive(true);
                    _steamVRPersistentObj.SetActive(true);
                }
                if (_oculusObj != null) _oculusObj.SetActive(false);
                Orchestrator.Instance.OnCameraChanged(_steamCamera);
                return;
        }
    }
}
