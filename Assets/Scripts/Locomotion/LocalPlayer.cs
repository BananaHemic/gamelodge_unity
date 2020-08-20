using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using Cinemachine;
using IngameDebugConsole;

public class LocalPlayer : MonoBehaviour
{
    public DesktopCameraController DesktopContoller;
    public VRCameraController VRController;
    public CinemachineBrain CinemachineBrain;

    private bool _needsInit = true;
    private bool _debugMode = false;

    private void Awake()
    {
        ControllerAbstraction.OnControllerPoseUpdate_User += UpdateInput;
        Orchestrator.OnModeChange += OnModeChange;
        VRSDKUtils.OnVRModeBeginChange += OnVRModeChange;
        CinemachineBrain.enabled = false;
    }
    private void OnVRModeChange()
    {
        _needsInit = true;
    }
    private void OnModeChange(Orchestrator.Modes mode)
    {
        _needsInit = true;
        CinemachineBrain.enabled = mode == Orchestrator.Modes.PlayMode;
    }
    private void HandleCharacterInput(CharacterBehavior characterBehavior)
    {
        CustomCharacterController.PlayerCharacterInputs characterInputs = new CustomCharacterController.PlayerCharacterInputs
        {
            // Build the CharacterInputs struct
            MoveAxisForward = ControllerAbstraction.Instances[0].GetForwardBackMove(),
            MoveAxisRight = ControllerAbstraction.Instances[0].GetLeftRightMove(),
            CameraRotation = ControllerAbstraction.Instances[0].GetRotation(),
            JumpDown = Input.GetKeyDown(KeyCode.Space),
            CrouchDown = Input.GetKeyDown(KeyCode.C),
            CrouchUp = Input.GetKeyUp(KeyCode.C),
            SprintDown = ControllerAbstraction.Instances[0].GetSprintMove(),
        };
        characterBehavior.IntegrateLocalInputs(ref characterInputs);
    }
    private void UpdateInput()
    {
        if (Orchestrator.Instance.CurrentMode != Orchestrator.Modes.PlayMode)
            return;
        UserDisplay userDisplay = UserManager.Instance.LocalUserDisplay;
        if (userDisplay == null)
            return;
        if (!userDisplay.DRUserObj.IsInPlayMode)
            return;

        var character = userDisplay.PossessedBehavior;
        if(character == null)
        {
            Debug.LogError("Can't integrate local input, no possessed behavior");
            return;
        }
        //if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
            //&& Input.GetMouseButtonDown(0))
            //Cursor.lockState = CursorLockMode.Locked;

        //HandleCameraInput();
        //Vector3 prevPos = Character.transform.position;
        //Debug.Log("Position changed " + (Character.transform.position - prevPos).magnitude);
        switch (VRSDKUtils.Instance.CurrentSDK)
        {
            case VRSDKUtils.SDK.Desktop:
                if (_needsInit)
                {
                    DesktopContoller.Init(character.CharacterController);
                    character.SetTrackingSpaceCorrect(false);
                    _needsInit = false;
                }
                if(!_debugMode)
                    DesktopContoller.UpdateFromInputs();
                break;
            case VRSDKUtils.SDK.OpenVR:
            case VRSDKUtils.SDK.Oculus:
                TrackingSpace.Instance.MoveTrackingSpaceFromController();
                if (_needsInit)
                {
                    VRController.Init(character.CharacterController.Motor);
                    character.SetTrackingSpaceCorrect(true);
                    _needsInit = false;
                }
                VRController.UpdateFromInputs();
                break;
        }
        HandleCharacterInput(character);
        //Debug.Log("Local player now at " + transform.position.ToPrettyString());
        //Debug.Log("delta update input post #" + Time.frameCount + ": " + (ControllerAbstraction.Instances[0].GetPosition() - CustomCharacterController.Instance.CameraFollowPoint.position).sqrMagnitude);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            _debugMode = !_debugMode;
            UIManager.Instance.gameObject.GetComponent<CanvasToggle>().SetOn(!_debugMode);
            DebugLogManager.instance.gameObject.SetActive(!_debugMode);
        }
    }
}