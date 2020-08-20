using Miniscript;
using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterBehavior : BaseBehavior
{
    public enum LipSyncType
    {
        None,
        Viseme16,
        JawFlap,
    }
    public static List<CharacterBehavior> _allCharacterBehaviors = new List<CharacterBehavior>(16);
    private static bool _hasLoadedIntrinsics = false;
    private readonly static List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly ValString IndexValStr = ValString.Create("idx", false);
    private static readonly ValString ValueValStr = ValString.Create("value", false);

    // Exposed settings
    public bool DestroyOnDepossess = true;
    public bool ShowUsername = true;

    public LipSyncType AvatarLipType { get; private set; }
    public SkinnedMeshRenderer[] AvatarMeshs {get; private set;}
    public AvatarDescriptor AvatarDescriptor { get; private set; }
    /// <summary>
    /// Convenience connection to the Character Controller
    /// </summary>
    public bool IsGrounded { get { return CharacterController.Motor.GroundingStatus.IsStableOnGround; } }
    public bool IsSprintDown { get { return CharacterController.PreviousIsSprintDown; } }
    public Vector3 Position { get { return CharacterController.Motor.TransientPosition; } }
    public Vector3 PreviousPosition { get { return CharacterController.PreviousPosition; } }
    public Vector2 PreviousMoveInputVector { get { return new Vector2(CharacterController.PreviousMoveInputVector.x, CharacterController.PreviousMoveInputVector.z); } }
    public Vector3 PreviousVelocity { get { return CharacterController.PreviousVelocity; } }
    public Transform FollowTransform { get { return CharacterController.CameraFollowPoint; } }
    public bool IsPossessed { get { return _isPossessed; } }

    public CustomCharacterController CharacterController { get; private set; }
    private VRIK _vrIk;
    private Animator _animator;
    private bool _isLoadingModel = true;
    private bool _isLocal;
    private bool _isPossessed = false;
    private DRUser _possessingUser;
    private UserPoseDisplay _possessedPoseDisplay;
    private bool _isFirstSample = true;
    const int LayerRequestPriority = 3;

    // Serialization stuff
    const int DepossessDestroyKey = SharedBehaviorKeys.DestroyOnDepossessKey_Character;
    const int ShowUsernameKey = DepossessDestroyKey + 1;

    protected override void ChildInit()
    {
        //Debug.Log("Character init, will make CharacterController");
        CharacterController = UserManager.Instance.CreatePlayNetworkModel().GetComponent<CustomCharacterController>();
        CharacterController.Init();
        CharacterController.Motor.SetPositionAndRotation(transform.position, transform.rotation, true);
        CharacterController.Motor.BaseVelocity = Vector3.zero;
        CharacterController.OnHitObjectWhenMoving += OnCharacterCollidedWithObject;
        _allCharacterBehaviors.Add(this);

        if (!_sceneObject.IsLoadingModel)
            StartCoroutine(tmp());
            //OnAvatarLoaded(_sceneObject.Model);
    }
    public SceneObject GetObjectCharacterIsOn()
    {
        if (!CharacterController.Motor.GroundingStatus.IsStableOnGround)
            return null;
        Collider hitCollider = CharacterController.Motor.GroundingStatus.GroundCollider;
        // Determine if we're on the table
        if (hitCollider == Board.Instance.MainCollider)
            return null;
        return hitCollider.transform.GetSceneObjectFromTransform();
    }
    IEnumerator tmp()
    {
        // TODO Idk why this is needed. For some reason,
        // the VRIK hands are off 90 degrees when the model
        // immediately loads, so we instead use this. This
        // is currently only relevant for the built in model
        yield return null;
        OnAvatarLoaded(_sceneObject.Model);
    }
    public override void OnModelLoaded()
    {
        OnAvatarLoaded(_sceneObject.Model);
    }
    /// <summary>
    /// Tell all CharacterBehaviors that KCC has just finished running, so they should all now update
    /// </summary>
    public static void GL_FixedUpdate_Static()
    {
        for (int i = 0; i < _allCharacterBehaviors.Count; i++)
            _allCharacterBehaviors[i].GL_FixedUpdate();
    }
    private void GL_FixedUpdate()
    {
        _sceneObject.transform.localPosition = CharacterController.transform.localPosition;
    }
    public void IntegrateNetworkInputs(Vector3 position, Vector2 moveVector, bool isGrounded, bool isSprintDown, Vector3 baseVelocity)
    {
        //Debug.Log("Recv move vec " + moveVector.ToPrettyString() + " base " + baseVelocity.ToPrettyString() + " pos " + position.ToPrettyString());
        Quaternion camRot;
        if (_possessedPoseDisplay != null)
            camRot = _possessedPoseDisplay.HeadTransform.rotation;
        else
        {
            Debug.LogError("No possessed pose display! #" + _sceneObject.GetID());
            camRot = Quaternion.identity;
        }
        CustomCharacterController.NetworkCharacterInputs inputs = new CustomCharacterController.NetworkCharacterInputs
        {
            IsFirstSample = _isFirstSample,
            CameraRotation = camRot,
            Position = position,
            MoveVector = moveVector,
            IsGrounded = isGrounded,
            SprintDown = isSprintDown,
            BaseVelocity = baseVelocity,
        };
        CharacterController.SetInputs(ref inputs);
        _isFirstSample = false;
    }
    public void IntegrateLocalInputs(ref CustomCharacterController.PlayerCharacterInputs localInput)
    {
        CharacterController.SetInputs(ref localInput);
    }
    public void OnPossessBegin(bool isLocal, DRUser possessingUser)
    {
        if(!UserManager.Instance.TryGetUserDisplay(possessingUser.ID, out UserDisplay userDisplay))
        {
            Debug.LogError("Can't add VRIK, no UserDisplay #" + possessingUser.ID);
            return;
        }
        _isLocal = isLocal;
        _isPossessed = true;
        _possessingUser = possessingUser;
        _possessedPoseDisplay = userDisplay.PoseDisplay;
        Debug.Log("Possess #" + _sceneObject.GetID() + " Avatar mesh " + AvatarMeshs);
        if (!_isLoadingModel)
            AddVrIK();
        //_sceneObject.BehaviorRequestedLayer(_isLocal ? GLLayers.LocalUser_PlayLayerNum : GLLayers.OtherUser_PlayLayerNum, this, LayerRequestPriority);
        //_sceneObject.BehaviorRequestedLayer(GLLayers.OtherUser_PlayLayerNum, this, LayerRequestPriority);

        // TODO expose this as an option
        if (_isLocal && AvatarMeshs != null && VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            foreach(var mesh in AvatarMeshs)
                mesh.enabled = false;
            Debug.Log("Hiding mesh");
        }
    }
    public void SetTrackingSpaceCorrect(bool correct)
    {
        CharacterController.TrackingSpaceCorrect = correct;
    }
    public void OnDepossess()
    {
        //Debug.Log("Depossess #" + _sceneObject.GetID());
        // Clear the inputs
        if (!_isLocal)
        {
            Quaternion camRot;
            if (_possessedPoseDisplay != null)
                camRot = _possessedPoseDisplay.HeadTransform.rotation;
            else
            {
                Debug.LogError("No possessed pose display!");
                camRot = Quaternion.identity;
            }
            CustomCharacterController.NetworkCharacterInputs inputs = new CustomCharacterController.NetworkCharacterInputs
            {
                IsFirstSample = _isFirstSample,
                CameraRotation = camRot,
                Position = CharacterController.PreviousPosition,
                MoveVector = Vector2.zero,
                IsGrounded = CharacterController.PreviousIsGrounded,
                SprintDown = false,
                BaseVelocity = CharacterController.PreviousVelocity,
            };
            CharacterController.SetInputs(ref inputs);
        }
        _isPossessed = false;
        _possessingUser = null;
        _isLocal = false;
        if(_vrIk != null)
        {
            Destroy(_vrIk);
            _vrIk = null;
        }
        //_sceneObject.BehaviorClearRequestLayer(this, LayerRequestPriority);
        CharacterController.TrackingSpaceCorrect = false;
        _possessedPoseDisplay = null;
        if (AvatarMeshs != null) {
            foreach(var mesh in AvatarMeshs)
                mesh.enabled = true;
        }
    }
    private void AddVrIK()
    {
        if(_possessedPoseDisplay == null)
            Debug.LogError("Can't add VRIK, no pose display");
        _vrIk = _sceneObject.Model.AddComponent<VRIK>();
        _vrIk.AutoDetectReferences();
        _vrIk.solver.spine.headTarget = _possessedPoseDisplay.HeadAttachOffset;
        _vrIk.solver.leftArm.target = _possessedPoseDisplay.LHandAttachOffset;
        _vrIk.solver.rightArm.target = _possessedPoseDisplay.RHandAttachOffset;
        //_vrIk.UseExternalSolve = true; // We are going to calling solve ourself
        //_vrIk.UseExternalSolve = false;
        _vrIk.solver.locomotion.weight = 0f;
        _vrIk.enabled = true;
        // For whatever reason, some bones on some models point forward along Z, and others point forward along Y
        // so we figure out the rotation of the head with respect to the avatar root
        //Quaternion invRoot = Quaternion.Inverse(transform.rotation);
        Quaternion invRoot = Quaternion.Inverse(_sceneObject.Model.transform.rotation);
        Quaternion headAttachRot = invRoot * _vrIk.references.head.rotation;
        _possessedPoseDisplay.HeadAttachOffset.localRotation = headAttachRot;

        //NB if this ever starts to act weird, double check that the model is in T-Pose
        Quaternion leftHand = Quaternion.Euler(-90f, 90f, 0) * invRoot * _vrIk.references.leftHand.rotation;
        Quaternion rightHand = Quaternion.Euler(-90f, -90f, 0) * invRoot * _vrIk.references.rightHand.rotation;
        _possessedPoseDisplay.LHandAttachOffset.localRotation = leftHand;
        _possessedPoseDisplay.RHandAttachOffset.localRotation = rightHand;

        // We need to figure out the delta between where the head transform is
        // and where the camera should be. So we get the positions relative
        // to this object
        if(AvatarDescriptor != null)
        {
            Vector3 viewLocalPos = AvatarDescriptor.ViewPosition;
            Vector3 headLocalPos = transform.InverseTransformPoint(_vrIk.references.head.position);
            Vector3 delta = viewLocalPos - headLocalPos;
            //Debug.Log("Delta: " + delta.ToPrettyString() + " view pos " + viewLocalPos.ToPrettyString() + " head " + headLocalPos.ToPrettyString());
            _possessedPoseDisplay.HeadAttachOffset.localPosition = -delta;
        }

        if (_isLocal)
            LocalRefreshVRIKWeights(ControllerAbstraction.ControllerType.HEAD);
        else
            NetworkRefreshVRIKWeights(_possessedPoseDisplay.CurrentPose);
    }
    public void LocalRefreshVRIKWeights(ControllerAbstraction.ControllerType junk)
    {
        if (_vrIk == null)
            return;
        _vrIk.solver.leftArm.positionWeight = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].IsConnected ? 1f : 0;
        _vrIk.solver.leftArm.rotationWeight = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.LEFTHAND].IsConnected ? 1f : 0;
        _vrIk.solver.rightArm.positionWeight = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].IsConnected ? 1f : 0;
        _vrIk.solver.rightArm.rotationWeight = ControllerAbstraction.Instances[(int)ControllerAbstraction.ControllerType.RIGHTHAND].IsConnected ? 1f : 0;
    }
    public void NetworkRefreshVRIKWeights(DRUserPose.PoseInfo poseInfo)
    {
        if (_vrIk == null)
            return;
        _vrIk.solver.leftArm.positionWeight = poseInfo.HasLHand ? 1 : 0;
        _vrIk.solver.leftArm.rotationWeight = poseInfo.HasLHand ? 1 : 0;
        _vrIk.solver.rightArm.positionWeight = poseInfo.HasRHand ? 1 : 0;
        _vrIk.solver.rightArm.rotationWeight = poseInfo.HasRHand ? 1 : 0;
    }
    private void OnAvatarLoaded(GameObject model)
    {
        _isLoadingModel = false;
        AvatarDescriptor = model.GetComponent<AvatarDescriptor>();
        AvatarMeshs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        _animator = model.GetComponent<Animator>();
        _animator.enabled = false;
        _animator.applyRootMotion = false;
        _animator.runtimeAnimatorController = UserManager.Instance.DefaultAnimationController;
        _animator.enabled = true;

        if (_isPossessed)
            AddVrIK();

        // TODO this isn't particularly fast, we know it's going to be on one of the immediately children
        // And GetComponentInChildren is depth first
        //AvatarMesh.gameObject.layer = _isLocal ? GLLayers.LocalUser_PlayLayerNum : GLLayers.OtherUser_PlayLayerNum;
        //AvatarMesh = _loadedModel.transform.Find("mouth").GetComponent<SkinnedMeshRenderer>();
        //AvatarMesh = _loadedModel.transform.FindDeepChild_DepthFirst("mouth").GetComponent<SkinnedMeshRenderer>();
        if (AvatarMeshs == null || AvatarMeshs.Length == 0)
            Debug.LogError("Failed to load skinned mesh renderer ", model);
        else
        {
            if (AvatarMeshs[0].sharedMesh.blendShapeCount >= 16)
                AvatarLipType = LipSyncType.Viseme16;
            else
                AvatarLipType = LipSyncType.None;
        }

        // TODO expose this as an option
        if (_isPossessed && _isLocal && VRSDKUtils.Instance.CurrentSDK != VRSDKUtils.SDK.Desktop)
        {
            foreach(var mesh in AvatarMeshs)
                mesh.enabled = false;
        }
    }
    public void UpdateAnimation()
    {
        if (_animator == null)
            return;
        //_animator.Update(Time.deltaTime);
        // Get the velocity that this character is moving at
        Vector3 characterVelocity = CharacterController.PreviousLastAddedVelocity;
        Transform headTransform = _possessedPoseDisplay != null ? _possessedPoseDisplay.HeadTransform : transform;
        float velForward = Vector3.Dot(characterVelocity, headTransform.forward);
        float velRight = Vector3.Dot(characterVelocity, headTransform.right);
        _animator.SetFloat("VelForward", velForward);
        _animator.SetFloat("VelRight", velRight);
        _animator.SetBool("IsGrounded", CharacterController.PreviousIsGrounded);
        bool isMoving = CharacterController.PreviousIsGrounded
            && CharacterController.PreviousMoveInputVector.sqrMagnitude > 0.00001f;
        _animator.SetBool("IsMoving", isMoving);

        //Debug.Log("Character vel " + characterVelocity);
        //Debug.Log("Grounded " + CharacterController.PreviousIsGrounded);
        //Debug.Log("Prev input vec " + CharacterController.PreviousMoveInputVector.ToPrettyString());
        //Debug.Log("vecForward " + velForward);
    }
    /// <summary>
    /// We listen for when a network character has collided with something
    /// because if they've collided with something that we own, we should
    /// anticipate losing ownership. In practice, this rarely happens, as
    /// there is a small amount of additional latency for the character
    /// controller. TODO we might want to remove this entirely, if we find
    /// that false positives are common
    /// </summary>
    /// <param name="r"></param>
    private void OnCharacterCollidedWithObject(Rigidbody rigidbody)
    {
        //Debug.Log("Network character hit object");
        NetworkObject otherNetworkObj = rigidbody.gameObject.GetComponent<NetworkObject>();
        if (otherNetworkObj == null)
            return;

        if (otherNetworkObj.SceneObject.Layer != GLLayers.PhysicsObject_WalkableLayerNum)
            return;

        // If we're controlling this object, then take ownership from collision
        if (_sceneObject.DoWeOwn)
        {
            // If we own the other object, return
            int otherOwner = otherNetworkObj.GetCurrentOwner();
            if (otherOwner == DarkRiftConnection.Instance.OurID)
                return;

            if (otherNetworkObj.IsSomeoneGrabbing())
            {
                Debug.LogError("Someone grabbing obj #" + otherNetworkObj.SceneObject.GetID() + " but was instead in layer " + otherNetworkObj.SceneObject.Layer);
                return;
            }

            Debug.Log("Taking ownership of " + otherNetworkObj.name);
            // Now, we can try to take ownership of the object
            otherNetworkObj.TakeOwnershipFromCollision(DarkRiftPingTime.Instance.ServerTime);
        }
        else
        {
            // If we don't own the other object, return
            int otherOwner = otherNetworkObj.GetCurrentOwner();
            if (otherOwner != DarkRiftConnection.Instance.OurID)
                return;

            if (otherNetworkObj.IsSomeoneGrabbing())
            {
                Debug.LogError("Someone grabbing obj #" + otherNetworkObj.SceneObject.GetID() + " but was instead in layer " + otherNetworkObj.SceneObject.Layer);
                return;
            }

            Debug.Log("Will anticipate losing ownership of " + otherNetworkObj.SceneObject.Name + " by " + _possessingUser.ID);
            // Now, we'll stop sending out updates for this object. Then either we're
            // correct and someone else has taken ownership and will be sending out updates
            // or enough time elapses that we figure that we were wrong, and we re-take
            // ownership of the object
            otherNetworkObj.AnticipateLosingOwnership(_possessingUser.ID, DarkRiftPingTime.Instance.ServerTime);
        }
    }
    public override void RefreshProperties()
    {
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Destroy On Depossess
        byte[] destroyOnDepossess;
        if(_serializedBehavior.TryReadProperty(DepossessDestroyKey, out destroyOnDepossess, out int _))
            DestroyOnDepossess = BitConverter.ToBoolean(destroyOnDepossess, 0);
        // Show Username
        byte[] showUsernameArray;
        if(_serializedBehavior.TryReadProperty(ShowUsernameKey, out showUsernameArray, out int _))
            ShowUsername = BitConverter.ToBoolean(showUsernameArray, 0);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        Debug.Log("Writing Character");
        //TODO use a zero-allocation float -> array function
        // Destroy On Depossess
        _serializedBehavior.LocallySetData(DepossessDestroyKey, BitConverter.GetBytes(DestroyOnDepossess));
        // ShowUsername
        _serializedBehavior.LocallySetData(ShowUsernameKey, BitConverter.GetBytes(ShowUsername));
    }
    public override void Destroy()
    {
        if (_isPossessed)
        {
            if (_isLocal)
                Orchestrator.Instance.SetToMode(Orchestrator.Modes.BuildMode);
            else
                OnDepossess();
        }
        if (CharacterController != null)
            Destroy(CharacterController.gameObject);
        CharacterController = null;
        if(_animator != null)
            _animator.enabled = false;

        _allCharacterBehaviors.RemoveBySwap(this);
    }
    void OnDestroy()
    {
        _allCharacterBehaviors.RemoveBySwap(this);
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return false;
    }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return null;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public void SetBlendShape(int idx, float val)
    {
        AvatarMeshs[0].SetBlendShapeWeight(idx, val);
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("SetCharacterBlendShape");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Configure a blend shape on the user. This will automatically be synchronized across the network", null));
        intrinsic.AddParam(IndexValStr.value);
        intrinsic.AddParam(ValueValStr.value);
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No SceneObject for SetCharacterBlendShape call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            CharacterBehavior characterBehavior = sceneObject.GetBehaviorByType<CharacterBehavior>();
            if(characterBehavior == null)
            {
                UserScriptManager.LogToCode(context, "No CharacterBehavior for SetCharacterBlendShape call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValNumber idxVal = context.GetVar(IndexValStr) as ValNumber;
            if(idxVal == null)
            {
                UserScriptManager.LogToCode(context, "No index for SetCharacterBlendShape call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            ValNumber valueVal = context.GetVar(ValueValStr) as ValNumber;
            if(valueVal == null)
            {
                UserScriptManager.LogToCode(context, "No value for SetCharacterBlendShape call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            if(characterBehavior.AvatarMeshs == null || characterBehavior.AvatarMeshs.Length == 0)
            {
                UserScriptManager.LogToCode(context, "No AvatarMesh for SetCharacterBlendShape call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            int idx = idxVal.IntValue();
            float val = valueVal.FloatValue();
            //Debug.Log("Set #" + idx + " to " + val, characterBehavior.AvatarMesh);
            characterBehavior.AvatarMeshs[0].SetBlendShapeWeight(idx, val);

            if (characterBehavior._possessingUser != null)
                DarkRiftConnection.Instance.UpdateUserBlendShape(characterBehavior._possessingUser, idx, val);

            return Intrinsic.Result.True;
		};
    }

    void Update()
    {
        if (_sceneObject.PossessedBy == ushort.MaxValue)
            UpdateAnimation();
    }
}
