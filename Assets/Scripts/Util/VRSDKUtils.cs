using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Oculus.Platform;
using Valve.VR;
using UnityEngine.VR;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class VRSDKUtils : GenericSingleton<VRSDKUtils>{
    public static Action OnUserDisconnected;
    public static Action OnUserConnected;
    /// <summary>
    /// Called on the frame when we started a VR mode
    /// change
    /// </summary>
    public static Action OnVRModeBeginChange;
    /// <summary>
    /// Called after the switch from desktop->VR
    /// or vice versa has completed
    /// </summary>
    public static Action OnVRModeChanged;
    // When we have a list of the already purchased SKUs
    public static Action<List<string>> OnInitialPurchasesRecv;

    private string _displayName;
    public string DisplayName {
        get { return _displayName; }
        private set {
            _displayName = value;
            HasRecvDisplayName = true;
        }
    }
    public bool HasRecvDisplayName { get; private set; }
    public string ID { get; private set; }
    public bool DisableFixedUpdateSet;

    /// <summary>
    /// This is the SDK user access token, used
    /// to let the API consume purchased items
    /// </summary>
    public string AccessToken { get; private set; }

    public enum SDK
    {
        Desktop,
        OpenVR,
        Oculus
    };
    public enum DEVICE
    {
        Desktop,
        Oculus,
        Vive,
        WMR,
        Gear,
        GO
    }
    private bool _hasRecvSDK;
    private SDK _currentSDK;
    private SDK _prevSDK;
    public SDK CurrentSDK
    {
        get
        {
            if (_hasRecvSDK)
                return _currentSDK;
            if (!UnityEngine.XR.XRSettings.enabled)
            {
                //Debug.Log("VR is turned off atm");
                _currentSDK = SDK.Desktop;
                _hasRecvSDK = true;
                return _currentSDK;
            }
            switch (UnityEngine.XR.XRSettings.loadedDeviceName)
            {
                case "OpenVR":
                    //Debug.Log("Using OpenVR SDK");
                    _currentSDK = SDK.OpenVR;
                    _hasRecvSDK = true;
                    break;
                case "Oculus":
                    //Debug.Log("Using Oculus SDK");
                    _currentSDK = SDK.Oculus;
                    _hasRecvSDK = true;
                    break;
                default:
                    Debug.LogError("Unknown SDK: " + UnityEngine.XR.XRSettings.loadedDeviceName);
                    _currentSDK = SDK.Desktop;
                    break;
            }
            return _currentSDK;
        }
    }

    private DEVICE _currentDevice;
    private bool _hasRecvDev;
    public DEVICE CurrentDevice
    {
        get
        {
            if (_hasRecvDev)
                return _currentDevice;

            if (!UnityEngine.XR.XRSettings.enabled)
            {
                Debug.Log("VR dev is turned off atm");
                _currentDevice = DEVICE.Desktop;
                _hasRecvDev = true;
                return _currentDevice;
            }

#if UNITY_STANDALONE || UNITY_EDITOR
            //NOTE: this returns the phone model on Android
            string dev_str = UnityEngine.XR.XRDevice.model.ToLower();

            //Debug.Log("Got device " + dev_str);
            if (dev_str.Contains("oculus"))
            {
                _currentDevice = DEVICE.Oculus;
            }
            else if (dev_str.Contains("vive"))
                _currentDevice = DEVICE.Vive;
            else
            {
                //Debug.Log("Unexpected device name " + dev_str + " assuming WMR");
                _currentDevice = DEVICE.WMR;
            }
#elif UNITY_ANDROID
            //string dev_str = UnityEngine.XR.XRSettings.loadedDeviceName.ToLower();
            if (OVRPlugin.productName == "Oculus Go")
                _currentDevice = VR_DEVICE.GO;
            else
                _currentDevice = VR_DEVICE.Gear;
#endif

            _hasRecvDev = true;
            return _currentDevice;
        }
    }

    public static readonly string PlatformString =
#if OCULUS_PLATFORM
        "Oculus";
#elif STEAM_PLATFORM
        "Steam";
#else
        "";
#endif
    private readonly YieldInstruction _UserPresentWait = new WaitForSeconds(1f);

    protected override void Awake()
    {
        base.Awake();
#if OCULUS_PLATFORM
        Core.Initialize();
        Entitlements.IsUserEntitledToApplication().OnComplete(OnRecvEntitlement);
#endif
        ConfigureForSDK();
        StartCoroutine(GetUserInfo());
        if(CurrentSDK != SDK.Desktop)
            StartCoroutine(CheckUserPresent());
        else
        {
            // Turn on when we're doing a recording
            //UnityEngine.Application.targetFrameRate = 30;
            //QualitySettings.vSyncCount = 2;
        }
    }
    public float GetDeviceDisplayFps()
    {
        switch (CurrentSDK)
        {
            case SDK.Desktop:
                return 60f; //TODO
            case SDK.Oculus:
            case SDK.OpenVR:
                return UnityEngine.XR.XRDevice.refreshRate;
        }
        return 0;
    }
    private void ConfigureForSDK()
    {
        switch (CurrentSDK)
        {
            case SDK.Desktop:
                QualitySettings.vSyncCount = 1;
                break;
            case SDK.Oculus:
            case SDK.OpenVR:
                QualitySettings.vSyncCount = 0;
                break;
        }
        if (DisableFixedUpdateSet)
            return;
        // We set the fixed update time to the render frequency
        // this smooths things out when we're moving attached to a rigidbody
        TimeManager.Instance.SetPhysicsTimestep(1.0 / GetDeviceDisplayFps());
        //Debug.Log("Fixed time step now: " + Time.fixedDeltaTime);
    }
    private IEnumerator ToggleVRMode()
    {
        SDK newSDK;
        if (CurrentSDK == SDK.Desktop)
        {
            if(_prevSDK == SDK.OpenVR)
            {
                newSDK = SDK.OpenVR;
                UnityEngine.XR.XRSettings.LoadDeviceByName("openvr");
            }
            else
            {
                newSDK = SDK.Oculus;
                UnityEngine.XR.XRSettings.LoadDeviceByName("oculus");
            }
        }
        else
        {
            newSDK = SDK.Desktop;
            UnityEngine.XR.XRSettings.LoadDeviceByName("");
        }
        Debug.Log("SDK: " + CurrentSDK + "->" + newSDK);

        _prevSDK = CurrentSDK;
        _currentSDK = newSDK;
        if (OnVRModeBeginChange != null)
            OnVRModeBeginChange();
        yield return null;
            
        if(newSDK == SDK.Desktop)
        {
            Debug.Log("Turning off VR");
            UnityEngine.XR.XRSettings.enabled = false;
        }
        else
        {
            Debug.Log("Turning on VR");
            UnityEngine.XR.XRSettings.enabled = true;
        }

        _hasRecvDev = false;
        //_hasRecvSDK = false;
        if (OnVRModeChanged != null)
            OnVRModeChanged();
        ConfigureForSDK();
    }
    public void ToggleDesktopVRMode()
    {
        StartCoroutine(ToggleVRMode());
    }
    public IEnumerator GetUserInfo()
    {
#if OCULUS_PLATFORM
        Users.GetLoggedInUser().OnComplete(OnReceivedOculusUsername);
        while (DisplayName == null)
            yield return null;
#elif STEAM_PLATFORM
        while(!SteamManager.Initialized)
            yield return null;
        DisplayName = SteamFriends.GetPersonaName();
        Debug.Log("Got username: " + DisplayName);
        ID = SteamUser.GetSteamID().m_SteamID.ToString();
#else
        DisplayName = "testing_account";
#endif
        GetPurchases();
        GetAccessToken();
    }
    // Currently only used when the api tells us that we need to
    // use a different display name
    public void SetDisplayName(string newDisplayName)
    {
        Debug.Log(DisplayName + "->" + newDisplayName);
        DisplayName = newDisplayName;
    }
#if OCULUS_PLATFORM
    void OnRecvEntitlement(Message msg)
    {
        if (!msg.IsError)
        {
            // User is entitled
            Debug.Log("Entitled");
        }
        else
        {
            Debug.LogError("Entitlement failed! err str: " + msg.GetError().Message);
            UnityEngine.Application.Quit();
        }
    }
    void OnReceivedOculusUsername(Message<Oculus.Platform.Models.User> user) {

        if(user == null)
        {
            Debug.LogError("User null!");
            return;
        }
        if (user.IsError)
        {
            var err = user.GetError();
            string errString = (err == null) ? "Unknown username error" : err.Message;
            Debug.LogError("Has error! " + errString);
        }
        Debug.Log("Got username! " + user.Data.OculusID);
        DisplayName = user.GetUser().OculusID;
        ID = user.Data.ID.ToString();
    }
#endif
    public bool IsUserPresent()
    {
        switch (CurrentSDK)
        {
            case SDK.Desktop:
                return true;
            case SDK.Oculus:
                return OVRManager.instance.isUserPresent;
            case SDK.OpenVR:
                //SteamVR_Controller.Device dev = SteamVR_Controller.Input(0);
                //return dev.GetPress(Valve.VR.EVRButtonId.k_EButton_ProximitySensor);
            default:
                return false;
        }
    }
    public void GetPurchases()
    {
#if OCULUS_PLATFORM
        IAP.GetViewerPurchases().OnComplete(OnReceivedOculusPurchases);
#elif STEAM_PLATFORM
        //Afaik this isn't possible
#endif
    }

#if OCULUS_PLATFORM
    void OnReceivedOculusPurchases(Message<Oculus.Platform.Models.PurchaseList> purchases) {
        if (purchases.IsError)
        {
            var err = purchases.GetError();
            string errString = (err == null) ? "Unknown purchase list error" : err.Message;
            Debug.LogError("Error when getting list of purchases: " + errString);
            return;
        }
        Debug.Log("User has " + purchases.Data.Count + " purchases");
        List<string> skus = new List<string>(purchases.Data.Count);

        for(int i = 0; i < purchases.Data.Count; i++)
        {
            Oculus.Platform.Models.Purchase purchase = purchases.Data[i];
            skus.Add(purchase.Sku);
            Debug.Log("has: \"" + purchase.Sku + "\"");
        }

        if (OnInitialPurchasesRecv != null)
            OnInitialPurchasesRecv(skus);
    }
#endif
    private void GetAccessToken()
    {
#if OCULUS_PLATFORM
        Users.GetAccessToken().OnComplete(OnReceivedAccessToken);
#elif STEAM_PLATFORM
        // Not relevent for steam
#endif
    }
#if OCULUS_PLATFORM
    private void OnReceivedAccessToken(Message<string> msg)
    {
        if (msg.IsError)
        {
            var err = msg.GetError();
            string errString = (err == null) ? "Unknown access token error" : err.Message;
            Debug.LogError("Error when getting access token: " + errString);
            return;
        }

        //Debug.Log("Got access token as " + msg.Data);
        AccessToken = msg.Data;
    }
#endif
    public void Recenter()
    {
        switch (CurrentSDK)
        {
            case SDK.Oculus:
                UnityEngine.XR.InputTracking.Recenter();
                break;
            case SDK.OpenVR:
                Valve.VR.OpenVR.System.ResetSeatedZeroPose();
                break;
        }
    }
    private IEnumerator CheckUserPresent()
    {
        // One the user is present the first time, recenter
        yield return null;
        //Debug.Log("before: " + ControllerAbstraction.Instances[0].GetPosition());
        while (!IsUserPresent())
            yield return null;
        //Debug.LogWarning("HMD connected");
        //Debug.Log("present: " + ControllerAbstraction.Instances[0].GetPosition());
        Recenter();

        if(OnUserConnected != null)
            OnUserConnected();

        yield return null;
        //Debug.Log("after: " + ControllerAbstraction.Instances[0].GetPosition());

        // Then infinitely keep checking if the user is present
        bool isPresent = true;
        while (true)
        {
            bool isPresentNow = IsUserPresent();

            if(isPresentNow && !isPresent)
            {
                //Debug.Log("HMD reconnected");
                // The user just (re)connected
                if (OnUserConnected != null)
                    OnUserConnected();
            } else if(!isPresentNow && isPresent)
            {
                // The use just disconnected
                //Debug.Log("HMD disconnected");
                if (OnUserDisconnected != null)
                    OnUserDisconnected();
            }
            isPresent = isPresentNow;

            // If the user is connected, then wait a bit
            // Otherwise, check every frame
            if (isPresent)
                yield return _UserPresentWait;
            else
                yield return null;
        }
    }
    public bool IsGearOrGo()
    {
        return CurrentDevice == DEVICE.Gear
            || CurrentDevice == DEVICE.GO;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            ToggleDesktopVRMode();
    }
}