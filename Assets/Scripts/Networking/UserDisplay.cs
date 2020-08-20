using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRiftAudio;
using DarkRift;

/// <summary>
/// Handles the display and networking for a player
/// Also creates the scripts for sending / displaying pose data
/// </summary>
public class UserDisplay : MonoBehaviour, IRealtimeObject
{
    public DRUser DRUserObj { get; private set; }

    public AudioSource AudioSource;
    public UserPoseDisplay PoseDisplay;
    public UsernameDisplay UsernameDisplay;

    private bool _hasInit = false;
    private bool _isLocal;
    private GameObject _buildNetworkModel;
    /// <summary>
    /// The parameters for where we should be going
    /// when we're a network user in build mode
    /// </summary>
    private Vector3 _buildModeTargetPos;
    private Quaternion _buildModeTargetRot;
    private Vector3 _buildModePrevPos;
    private Quaternion _buildModePrevRot;
    // This uses realtime since startup, b/c we want to be able to move
    // around in build mode
    private float _buildModeTimeLastRecvInput = 0;
    private ValUser _valUser;
    public CharacterBehavior PossessedBehavior;
    // Non-local things
    public DarkRiftAudioPlayer NetworkAudioPlayer { get; private set; }
    // only used in play mode, is RenderUnscaledTime
    private float _lastRecvInputTime;
    private Coroutine _checkThatWeHaveRecentInput;

    // Local things
    // This time is RenderUnscaledTime in play mode, and Realtime in build mode
    private float _lastSendTime = float.MinValue;
    private bool _wasInPlayMode = false;
    private bool _didLocalHitObject = false;
    // Testing things
    private Coroutine _testingUpdateSelfPosition;

    // For avoiding GC
    private readonly Vec3 _workingPos = new Vec3();
    private readonly Quat _workingRot = new Quat();

    public void Init(DRUser user, bool isLocal)
    {
        if (_hasInit)
            Debug.LogError("Double init NetworkPlayerDisplay!");
        _hasInit = true;
        DRUserObj = user;
        _isLocal = isLocal;
        ReevaluateUsernameDisplay();
#if UNITY_EDITOR
        name = isLocal ? "LocalNetworkPlayer" : "NetworkPlayer #" + DRUserObj.ID;
#endif

        // For other users, add an audio player
        if (!_isLocal)
        {
            NetworkAudioPlayer = AudioSource.gameObject.AddComponent<DarkRiftAudioPlayer>();
            NetworkAudioPlayer.Initialize(DarkRiftConnection.Instance.GetAudioClient(), DRUserObj.ID);
            _lastRecvInputTime = TimeManager.Instance.RenderUnscaledTime;
            _checkThatWeHaveRecentInput = StartCoroutine(CheckTooLongWithoutNetworkInput());
            Vector3 pos = DRUserObj.Position.ToVector3();
            Quaternion rot = DRUserObj.Rotation.ToQuaternion();

            if (!user.IsInPlayMode)
            {
                transform.localPosition = pos;
                transform.localRotation = rot;
                _buildModePrevPos = transform.localPosition;
                _buildModePrevRot = transform.localRotation;
                _buildModeTargetPos = transform.localPosition;
                _buildModeTargetRot = transform.localRotation;
                if (_buildNetworkModel == null)
                    _buildNetworkModel = UserManager.Instance.CreateBuildNetworkModel(this);
                _buildNetworkModel.SetActive(true);
            }
        }
        else
        {
            ControllerAbstraction.OnControllerPoseUpdate_Pose += LocalUpdate;
            ControllerAbstraction.OnObjectsEnqueueNetworkMessages += PossiblySendCharacterUpdate;
        }
        if (user.IsInPlayMode)
            BeginPossess();
        PoseDisplay.Init(DRUserObj, this, isLocal);
        //Debug.Log("Init real, net player " + _networkPlayer);
    }
    public void ReevaluateUsernameDisplay()
    {
        // Either in build mode, or in play mode with a
        // character behavior that wants a username
        if(!_isLocal
            && (!DRUserObj.IsInPlayMode
            || PossessedBehavior == null
            || PossessedBehavior.ShowUsername))
        {
            if (UsernameDisplay == null)
                UsernameDisplay = UsernameManager.Instance.GetUsernameDisplay(this);
            else if(!UsernameDisplay.gameObject.activeSelf)
                UsernameDisplay.gameObject.SetActive(true);
        }
        else
        {
            UsernameDisplay?.gameObject.SetActive(false);
        }
    }
    public void BeginPossess()
    {
        Debug.LogWarning("Beginning possess now");
        // Get the Character behavior
        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(DRUserObj.PossessedObj, out SceneObject possessedObj))
        {
            Debug.LogError("Failed to get the possessed sceneobject for user #" + DRUserObj.ID + " obj #" + DRUserObj.PossessedObj);
            return;
        }
        PossessedBehavior = possessedObj.GetBehaviorByType<CharacterBehavior>();
        if(PossessedBehavior == null)
        {
            Debug.LogError("Possessed object #" + possessedObj.GetID() + " has no character behavior!");
            return;
        }
        PossessedBehavior.OnPossessBegin(_isLocal, DRUserObj);
        ReevaluateUsernameDisplay();
    }
    public void EndPossess()
    {
        DRUserObj.PossessedObj = ushort.MaxValue;
        if (PossessedBehavior != null)
            PossessedBehavior.OnDepossess();
        PossessedBehavior = null;
        ReevaluateUsernameDisplay();
    }
    public void OnServerUserGrabObject(ushort objectID, DRUser.GrabbingBodyPart bodyPart, Vector3 relPos, Quaternion relRot)
    {
        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(objectID, out SceneObject sceneObject))
        {
            Debug.LogError("Can't handle user grab object, no object #" + objectID);
            return;
        }

        PoseDisplay.UserGrabObjectServer(sceneObject, bodyPart, relPos, relRot);
    }
    public void OnServerUserReleaseObject(ushort objectID)
    {
        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(objectID, out SceneObject sceneObject))
        {
            Debug.LogError("Can't handle user release object, no object #" + objectID);
            return;
        }

        PoseDisplay.UserReleaseObjectServer(sceneObject);
    }
    public void SetUserBlend(int idx, float val, bool notifyServer)
    {
        DRUserObj.SetBlendShape(idx, val);
        if (PossessedBehavior != null)
            PossessedBehavior.SetBlendShape(idx, val);
        if (notifyServer)
            DarkRiftConnection.Instance.UpdateUserBlendShape(DRUserObj, idx, val);
    }
    public ValUser GetValUser()
    {
        if (_valUser == null)
            _valUser = new ValUser(DRUserObj);
        return _valUser;
    }
    public void UpdateFromPoseMessage(DarkRiftReader reader, byte tag)
    {
        DRUserObj.UserPose.Deserialize(reader, tag);
        //Vector3 origin = BuildPlayManager.Instance.IsSpawnedInPlayMode ? transform.localPosition : _playNetworkModel.transform.localPosition;
        PoseDisplay.ApplyNewPoseDataFromNetwork(DRUserObj.UserPose.GetPoseInfo(Vector3.zero));
    }
    public void UpdateFromUserSpawn(ushort objID)
    {
        Debug.Log("Recv spawn info, said to use object ID " + objID);
        DRUserObj.IntegrateSpawnInfo(objID);
        BeginPossess();

        if (_isLocal)
            return;
        
        UpdateNetworkPlayCharacter();
    }
    public void UpdateFromPlayerMovementBuildMessage(DarkRiftReader reader)
    {
        //if(reader.Length - reader.Position != 28)
            //Debug.LogError("Recv build movement, len " + (reader.Length - reader.Position), this);
        DRUserObj.DeserializePlayerMovement_Build(reader);
        bool hasPrevVal = _buildModeTimeLastRecvInput > 0;
        // Fully move to where we were moving previously
        // if we have something
        if (hasPrevVal)
        {
            _buildModePrevPos = transform.localPosition;
            _buildModePrevRot = transform.localRotation;
        }
        _buildModeTargetPos = DRUserObj.Position.ToVector3();
        _buildModeTargetRot = DRUserObj.Rotation.ToQuaternion();

        //Debug.Log("Build pos " + _buildModePrevPos.ToPrettyString());
        //Debug.Log("Time since last msg " + (_buildModeTimeLastRecvInput > 0 ? Time.unscaledTime - _buildModeTimeLastRecvInput : -1).ToString());
        _buildModeTimeLastRecvInput = Time.realtimeSinceStartup;
        // If we have no previous message, just jump to the recv pos
        if (!hasPrevVal)
        {
            _buildModePrevPos = _buildModeTargetPos;
            _buildModePrevRot = _buildModeTargetRot;
        }

        //Debug.Log("Recv new build mode pos " + _buildModeTargetPos.ToPrettyString());
        if (_buildNetworkModel == null)
            _buildNetworkModel = UserManager.Instance.CreateBuildNetworkModel(this);
        _buildNetworkModel.SetActive(true);
    }
    private void UpdateNetworkPlayCharacter()
    {
        Vector3 pos = DRUserObj.Position.ToVector3();
        Quaternion rot = DRUserObj.Rotation.ToQuaternion();

        // If this user is on an object, then we have to
        // un-transform the position from the relative pos/rot
        if(DRUserObj.StandingOnObject != ushort.MaxValue)
        {
            if(!SceneObjectManager.Instance.TryGetSceneObjectByID(DRUserObj.StandingOnObject, out SceneObject groundObject))
            {
                Debug.LogError("Failed to find standing object #" + DRUserObj.StandingOnObject);
                return;
            }

            //Vector3 prevPos = pos;
            pos = groundObject.transform.TransformPoint(pos);
            rot = groundObject.transform.rotation * rot;
            //Debug.Log("Changed position from " + prevPos.ToPrettyString() + " -> " + pos.ToPrettyString());
        }
        if (PossessedBehavior != null)
            PossessedBehavior.IntegrateNetworkInputs(pos, DRUserObj.InputMovement.ToVector2(), DRUserObj.IsGrounded, DRUserObj.IsSprintDown, DRUserObj.BaseVelocity.ToVector3());
        transform.localRotation = rot;

        // Clear/turn off the build mode stuff
        if(_buildNetworkModel != null)
            _buildNetworkModel.SetActive(false);
        _buildModeTimeLastRecvInput = 0;
    }
    public void UpdateFromPlayerMovementPlayMessage(DarkRiftReader reader, byte tag, bool isLocalRecordedMessage)
    {
        DRUserObj.DeserializePlayerMovement_Play(reader, tag);
        // If this is a recorded message, then we'll have to update the objectID
        if(isLocalRecordedMessage && DRUserObj.StandingOnObject != ushort.MaxValue)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(DRUserObj.StandingOnObject, out ushort currentID))
                Debug.LogError("Failed to recording correct objectID: " + DRUserObj.StandingOnObject);
            else
            {
                //Debug.Log("Changing object ID from #" + DRUserObj.StandingOnObject + "-> #" + currentID);
                DRUserObj.StandingOnObject = currentID;
            }
        }
        _lastRecvInputTime = TimeManager.Instance.RenderUnscaledTime;
        UpdateNetworkPlayCharacter();
    }
    private IEnumerator CheckTooLongWithoutNetworkInput()
    {
        bool didSetToZero = false;
        float zeroRecvTime = float.NaN;
        while (true)
        {
            yield return null;
            if (!DRUserObj.IsInPlayMode)
                continue;
            if (PossessedBehavior == null || DRUserObj == null)
                continue;
            // Don't set to zero again if we just recently did so, as we haven't recv anything new
            if (didSetToZero && _lastRecvInputTime == zeroRecvTime)
                continue;
            // Wait until we receive _something_
            //Debug.Log("Time since last input: " + (Time.realtimeSinceStartup - _lastRecvInputTime));
            if(TimeManager.Instance.RenderUnscaledTime - _lastRecvInputTime > 2.1f * (1f / RealtimeNetworkUpdater.Instance.PlayModeMinSendRateHz))
            {
                didSetToZero = true;
                zeroRecvTime = _lastRecvInputTime;
                Debug.Log("Too long without input from character, will set to 0 input. Waited " + (TimeManager.Instance.RenderUnscaledTime - _lastRecvInputTime));
                Vector2 zero2 = Vector2.zero;
                zero2.ToVec2(DRUserObj.InputMovement);
                Vector3 zero3 = Vector3.zero;
                zero3.ToVec3(DRUserObj.BaseVelocity);
                PossessedBehavior.IntegrateNetworkInputs(PossessedBehavior.PreviousPosition, Vector2.zero, DRUserObj.IsGrounded, DRUserObj.IsSprintDown, Vector3.zero);
            }
            else
            {
                didSetToZero = false;
            }
        }
    }
    private void PossiblySendCharacterUpdate()
    {
        bool isInPlayMode = BuildPlayManager.Instance.IsSpawnedInPlayMode;
        float sendInterval = isInPlayMode ? 1f / RealtimeNetworkUpdater.Instance.PlayModeMinSendRateHz : 1f / RealtimeNetworkUpdater.Instance.BuildModeMinSendRateHz;

        bool hasPendingData = false;
        // Load the character data if we're in play mode
        if (isInPlayMode)
        {
            if(PossessedBehavior == null)
            {
                Debug.LogError("Can't send update, no Character behavior!");
                return;
            }
            hasPendingData |= DRUserObj.IsGrounded != PossessedBehavior.IsGrounded;
            hasPendingData |= DRUserObj.IsSprintDown != PossessedBehavior.IsSprintDown;
            hasPendingData |= (DRUserObj.InputMovement.ToVector2() - PossessedBehavior.PreviousMoveInputVector).sqrMagnitude > 0.0001f;
        }
        float time = Time.realtimeSinceStartup;
        // Reset the clock if we switch to/from play mode
        if (_wasInPlayMode != isInPlayMode)
            _lastSendTime = float.MinValue;
        _wasInPlayMode = isInPlayMode;

        //Debug.Log("Send interval " + sendInterval);
        // Wait for the time when we need to send
        if (time - _lastSendTime < sendInterval
            && !hasPendingData
            && !_didLocalHitObject)
            return;
        _didLocalHitObject = false;
        //Debug.Log("Send, time elapsed " + (Time.realtimeSinceStartup - _lastSendTime));
        _lastSendTime = time;

        DarkRiftWriter writer = DarkRiftWriter.Create();
        byte tag;
        if (!BuildPlayManager.Instance.IsSpawnedInPlayMode)
        {
            //TODO we'll need scale for build mode
            transform.localPosition.ToVec3(DRUserObj.Position);
            transform.localRotation.ToQuat(DRUserObj.Rotation);
            DRUserObj.StandingOnObject = ushort.MaxValue;
            DRUserObj.SerializePlayerMovement_Build(writer, out tag);
        }
        else
        {
            // Load all data into network player, it will decide what to serialize
            DRUserObj.IsGrounded = PossessedBehavior.IsGrounded;
            DRUserObj.IsSprintDown = PossessedBehavior.IsSprintDown;
            Vector3 position = PossessedBehavior.Position;
            Quaternion rotation = transform.localRotation;
            SceneObject groundObject = PossessedBehavior.GetObjectCharacterIsOn();

            if(groundObject != null && groundObject.GetBehaviorByType<MovingPlatformBehavior>() != null)
            {
                DRUserObj.StandingOnObject = groundObject.GetID();
                // If we're on an object, then we send out the relative position to the object
                position = groundObject.transform.InverseTransformPoint(position);
                rotation = Quaternion.Inverse(groundObject.transform.rotation) * rotation;
            }
            else
            {
                DRUserObj.StandingOnObject = ushort.MaxValue;
            }
            position.ToVec3(DRUserObj.Position);
            rotation.ToQuat(DRUserObj.Rotation);//TODO only need one number here

            PossessedBehavior.PreviousVelocity.ToVec3(DRUserObj.BaseVelocity);
            PossessedBehavior.PreviousMoveInputVector.ToVec2(DRUserObj.InputMovement);

            tag = DRUserObj.GetPlayMovementTagToUse();
            DRUserObj.SerializePlayerMovement_Play(writer, tag);
            //Debug.Log("Sending " + _networkPlayer.UpdateTimeTicks);
        }
        //Debug.Log("Updating self position " + transform.localPosition.ToPrettyString());
        //Debug.Log("player move " + writer.Length);
        RealtimeNetworkUpdater.Instance.EnqueueUnreliableUpdate(this, writer, tag, uint.MaxValue);
    }
    private IEnumerator Testing_SendCharacterUpdates()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            PossiblySendCharacterUpdate();
            if(PoseDisplay != null)
                PoseDisplay.PossiblyUploadNewPose();
        }
    }
    public bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority)
    {
        tag = byte.MaxValue;
        priority = uint.MaxValue;
        return false;
    }
    public void ClearPriority()
    {
        // no need to clear priority, position updates have the highest possible priority
    }
    /// <summary>
    /// Update our position, after the controllers have all processed their positions
    /// </summary>
    public void LocalUpdate()
    {
        Transform followTransform = UserObject.Instance.transform;
        if (!DRUserObj.IsInPlayMode)
        {
            transform.localPosition = followTransform.localPosition;
            transform.localRotation = followTransform.localRotation;
            transform.localScale = followTransform.localScale;
        }
        else
        {
            transform.localRotation = followTransform.localRotation;
        }
        PoseDisplay.LocalUpdate();
    }
    public void Destroy()
    {
        if(NetworkAudioPlayer != null)
        {
            NetworkAudioPlayer.Destroy();
            NetworkAudioPlayer = null;
        }
        if (UsernameDisplay != null)
        {
            UsernameManager.Instance.ReturnUsernameDisplay(UsernameDisplay);
            UsernameDisplay = null;
        }
    }
    /// <summary>
    /// FixedUpdate, called after the character update
    /// </summary>
    public void GL_FixedUpdate()
    {
        if (DRUserObj.IsInPlayMode)
        {
            if(PossessedBehavior == null)
            {
                Debug.LogWarning("No play model", this);
                return;
            }
            transform.localPosition = PossessedBehavior.transform.localPosition;
            //Debug.Log("UserDisplay updated to " + transform.localPosition);
        }
    }
    void Update()
    {
        if (PoseDisplay != null)
            PoseDisplay.UpdatePose();

        if (_isLocal)
            return;
        if(DRUserObj == null)
        {
            Debug.Log("no net player");
            return;
        }
        // Build mode updates in update, play mode updates in fixed update after CC
        if(!DRUserObj.IsInPlayMode)
        {
            float progress = (TimeManager.Instance.RenderUnscaledDeltaTime + Time.realtimeSinceStartup - _buildModeTimeLastRecvInput) / (1f / RealtimeNetworkUpdater.Instance.BuildModeMinSendRateHz);
            transform.localPosition = Vector3.Lerp(_buildModePrevPos, _buildModeTargetPos, progress);
            transform.localRotation = Quaternion.Slerp(_buildModePrevRot, _buildModeTargetRot, progress);
        }
    }
}
