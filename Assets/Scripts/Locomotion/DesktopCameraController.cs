using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesktopCameraController : MonoBehaviour
{
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    public float FollowingSharpness = 10000f;

    [Header("Distance")]
    public float DefaultDistance = 6f;
    public float MinDistance = 0f;
    public float MaxDistance = 10f;
    public float DistanceMovementSpeed = 5f;
    public float DistanceMovementSharpness = 10f;

    [Header("Rotation")]
    public bool InvertX = false;
    public bool InvertY = false;
    [Range(-90f, 90f)]
    public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f)]
    public float MinVerticalAngle = -90f;
    [Range(-90f, 90f)]
    public float MaxVerticalAngle = 90f;
    public float RotationSpeed = 1f;
    public float RotationSharpness = 10000f;

    [Header("Obstruction")]
    public float ObstructionCheckRadius = 0.2f;
    public LayerMask ObstructionLayers = -1;
    public float ObstructionSharpness = 10000f;
    public List<Collider> IgnoredColliders = new List<Collider>();

    public Transform CharacterTransform;
    public Transform CameraTransform;

    public Vector3 PlanarDirection { get; set; }
    public float TargetDistance { get; set; }

    private bool _distanceIsObstructed;
    private float _currentDistance;
    private float _targetVerticalAngle;
    private RaycastHit _obstructionHit;
    private int _obstructionCount;
    private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
    private float _obstructionTime;
    private Vector3 _currentFollowPosition;

    private const int MaxObstructions = 32;

    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";

    void Start()
    {
        Orchestrator.OnModeChange += OnModeChange;
        VRSDKUtils.OnVRModeChanged += OnVRModeChange;
    }
    public void Init(CustomCharacterController characterController)
    {
        // We use the character as the listed follow transform, because
        // we later account for the height by using the AvatarDescriptor
        // to figure out the height
        CharacterTransform = characterController.transform;
        PlanarDirection = CharacterTransform.forward;
        _currentFollowPosition = CharacterTransform.position;
    }
    private void OnModeChange(Orchestrator.Modes mode)
    {
        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
            return;
        switch (mode)
        {
            case Orchestrator.Modes.BuildMode:
                Cursor.lockState = CursorLockMode.None;
                break;
            case Orchestrator.Modes.PlayMode:
                Cursor.lockState = CursorLockMode.Locked;
                break;
        }
    }
    private void OnVRModeChange()
    {
        if(VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
            Cursor.lockState = CursorLockMode.None;
    }
    public void UpdateFromInputs()
    {
        if (Orchestrator.Instance.CurrentMode != Orchestrator.Modes.PlayMode)
            return;

        if (VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;

        //if (Cursor.lockState != CursorLockMode.Locked)
            //return;

        float mouseLookAxisUp = Input.GetAxisRaw(MouseYInput);
        float mouseLookAxisRight = Input.GetAxisRaw(MouseXInput);
        float deltaTime = TimeManager.Instance.RenderDeltaTime;
        Vector2 rotationInput = new Vector2(mouseLookAxisRight, mouseLookAxisUp);

        if (InvertX)
            rotationInput.x *= -1f;
        if (InvertY)
            rotationInput.y *= -1f;

        //Debug.Log(rotationInput);
        // Process rotation input
        Quaternion rotationFromInput = Quaternion.Euler(CharacterTransform.up * (rotationInput.x * RotationSpeed));
        PlanarDirection = rotationFromInput * PlanarDirection;
        PlanarDirection = Vector3.Cross(CharacterTransform.up, Vector3.Cross(PlanarDirection, CharacterTransform.up));
        _targetVerticalAngle -= (rotationInput.y * RotationSpeed);
        _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);

        // Process distance input
        //if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
        //{
        //    TargetDistance = _currentDistance;
        //}
        //TargetDistance += zoomInput * DistanceMovementSpeed;
        TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);

        // Find the smoothed follow position
        transform.position = CharacterTransform.position;
        transform.localRotation = Quaternion.identity;
        AvatarDescriptor avatarDescriptor = UserManager.Instance.LocalUserDisplay.PossessedBehavior?.AvatarDescriptor;
        Vector3 headPos = avatarDescriptor != null ? avatarDescriptor.ViewPosition : new Vector3(0, GLVars.DefaultHeight, 0);
        //ControllerAbstraction.Instances[0].GetLocalPositionAndRotation(out Vector3 headPos, out Quaternion headRot);
        _currentFollowPosition = Vector3.Lerp(_currentFollowPosition, CharacterTransform.position + headPos, 1f - Mathf.Exp(-FollowingSharpness * deltaTime));

        // Calculate smoothed rotation
        Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, CharacterTransform.up);
        Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
        Quaternion targetRotation = Quaternion.Slerp(CameraTransform.rotation, planarRot * verticalRot, 1f - Mathf.Exp(-RotationSharpness * deltaTime));

        // Apply rotation
        CameraTransform.rotation = targetRotation;

        // Handle obstructions
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            _obstructionCount = Physics.SphereCastNonAlloc(_currentFollowPosition, ObstructionCheckRadius, -CameraTransform.forward, _obstructions, TargetDistance, ObstructionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < _obstructionCount; i++)
            {
                bool isIgnored = false;
                for (int j = 0; j < IgnoredColliders.Count; j++)
                {
                    if (IgnoredColliders[j] == _obstructions[i].collider)
                    {
                        isIgnored = true;
                        break;
                    }
                }
                for (int j = 0; j < IgnoredColliders.Count; j++)
                {
                    if (IgnoredColliders[j] == _obstructions[i].collider)
                    {
                        isIgnored = true;
                        break;
                    }
                }

                if (!isIgnored && _obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
                {
                    closestHit = _obstructions[i];
                }
            }

            // If obstructions detecter
            if (closestHit.distance < Mathf.Infinity)
            {
                _distanceIsObstructed = true;
                _currentDistance = Mathf.Lerp(_currentDistance, closestHit.distance, 1 - Mathf.Exp(-ObstructionSharpness * deltaTime));
            }
            // If no obstruction
            else
            {
                _distanceIsObstructed = false;
                _currentDistance = Mathf.Lerp(_currentDistance, TargetDistance, 1 - Mathf.Exp(-DistanceMovementSharpness * deltaTime));
            }
        }

        // Find the smoothed camera orbit position
        Vector3 targetPosition = _currentFollowPosition - ((targetRotation * Vector3.forward) * _currentDistance);

        // Handle framing
        targetPosition += CameraTransform.right * FollowPointFraming.x;
        targetPosition += CameraTransform.up * FollowPointFraming.y;

        // Apply position
        CameraTransform.position = targetPosition;
    }
}
