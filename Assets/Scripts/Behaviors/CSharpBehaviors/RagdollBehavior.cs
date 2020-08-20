using DarkRift;
using Miniscript;
using RootMotion;
using RootMotion.Dynamics;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RagdollBehavior : BaseBehavior
{
    public enum RagdollType
    {
        Colliders,
        CollidersJoints,
        ColliderJoints_FollowAnim
    }
    public RagdollType Mode;

    private static bool _hasLoadedIntrinsics = false;
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    //private static readonly List<ExposedVariable> _userVariables = new List<ExposedVariable>();
    //private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();

    [SerializeField]
    private BipedReferences _bipedReferences;
    private BipedRagdollReferences _ragdollReferences;
    //[SerializeField]
    //private List<Collider> _addedColliders;
    //private GameObject _pupperMasterObj;
    private PuppetMaster _puppetMaster;
    private BehaviourPuppet _puppetBehavior;
    //private bool _hasAddedColliders = false;
    private bool _hasInit = false;
    private RagdollType _initMode;

    const int LayerRequestPriority = 1;
    const int UpperTorsoIdx = 1;

    private static readonly ValString ModeValStr = ValString.Create("mode", false);

    // Serialization stuff
    const int ModeKey = 0;
    private readonly byte[] _modeSerializationArray = new byte[1];

    private Rigidbody _headRB;
    private Rigidbody _upperTorsoRB;
    private Rigidbody _lowerTorsoRB;
    private Rigidbody _upperArmLRB;
    private Rigidbody _upperArmRRB;
    private Rigidbody _upperLegLRB;
    private Rigidbody _upperLegRRB;

    protected override void ChildInit()
    {
        _sceneObject.BehaviorRequestedLayer(GLLayers.RagdollLayerNum, this, LayerRequestPriority);
        if (!_sceneObject.IsLoadingModel)
            OnModelLoaded();
    }
    public override void OnModelLoaded()
    {
        BipedReferences.AutoDetectParams detectParams = new BipedReferences.AutoDetectParams(false, false);
        bool didDetect = BipedReferences.AutoDetectReferences(ref _bipedReferences, _sceneObject.Model.transform, detectParams);
        if (!didDetect)
        {
            Debug.LogError("Failed to detect biped references!");
            return;
        }
        _ragdollReferences = BipedRagdollReferences.FromBipedReferences(_bipedReferences);
        Animator anim = _sceneObject.Model.GetComponent<Animator>();
        if(anim != null)
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // The Cowboy upload from PolygonWestern has the hips inactive for
        // some reason, so we force them on as needed
        if (!_ragdollReferences.hips.gameObject.activeSelf)
        {
            Debug.LogWarning("Turning on hips object");
            _ragdollReferences.hips.gameObject.SetActive(true);
        }
        SkinnedMeshRenderer skinnedMesh = _sceneObject.Model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMesh != null)
            skinnedMesh.updateWhenOffscreen = true;
        Debug.Log("Model loaded, did detect: " + didDetect);
        RefreshProperties();
    }
    public override void RefreshProperties()
    {
        // Wait until the model is loaded
        if (_sceneObject.IsLoadingModel)
            return;

        if (_hasInit && _initMode == Mode)
        {
            Debug.LogWarning("Skipping possible extra Ragdoll init");
            return;
        }
        _hasInit = true;
        _initMode = Mode;

        BipedRagdollCreator.Options options = BipedRagdollCreator.AutodetectOptions(_ragdollReferences);
        options.hands = false;
        options.feet = false;
        options.headCollider = RagdollCreator.ColliderType.Box;

        bool addJoints = Mode == RagdollType.CollidersJoints
            || Mode == RagdollType.ColliderJoints_FollowAnim;
        bool kinematic = Mode == RagdollType.Colliders;
        // Create the ragdoll
        BipedRagdollCreator.Create(_ragdollReferences, options, addJoints, kinematic);
        if(Mode == RagdollType.ColliderJoints_FollowAnim)
        {
            if (_puppetMaster == null)
            {
                //_pupperMasterObj = new GameObject("puppetMasterRagdoll", typeof(PuppetMaster));
                //_pupperMasterObj.transform.SetParent(_sceneObject.transform, false);
                //_puppetMaster = _pupperMasterObj.GetComponent<PuppetMaster>();
                //_puppetMaster = PuppetMaster.SetUp(_pupperMasterObj.transform, _sceneObject.Model.transform, GLLayers.OtherUser_PlayLayerNum, GLLayers.RagdollLayerNum);
                Debug.Log("Setting up puppet " + Time.frameCount);
                _sceneObject.Model.GetComponent<Animator>().updateMode = AnimatorUpdateMode.AnimatePhysics;
                
                _puppetMaster = PuppetMaster.SetUp(_sceneObject.Model.transform, GLLayers.OtherUser_PlayLayerNum, GLLayers.RagdollLayerNum);
                Debug.Log("Added puppet master ", _puppetMaster.gameObject);
                //_puppetMaster.muscleSpring = 30f;
                //_puppetMaster.muscleDamper = 3f;
                _puppetMaster.muscleSpring = 5f;
                _puppetMaster.muscleDamper = 0.5f;
                _puppetMaster.pinDistanceFalloff = 0f;
                _puppetMaster.angularLimits = true;
                //_puppetMaster.internalCollisions = true;
                _puppetMaster.internalCollisions = false;
                Transform t = _puppetMaster.transform.parent.Find("Behaviours");
                
                _puppetBehavior = t.gameObject.AddComponent<BehaviourPuppet>();
                _puppetBehavior.collisionResistanceMultipliers = new BehaviourPuppet.CollisionResistanceMultiplier[0];
                var muscleProps = new BehaviourPuppet.MuscleProps
                {
                    unpinParents = 0.75f,
                    unpinChildren = 0.75f,
                    minMappingWeight = 0f,
                    maxMappingWeight = 1f,
                    regainPinSpeed = 1f,
                    collisionResistance = 1f,
                    knockOutDistance = 0.5f
                };
                _puppetBehavior.defaults = muscleProps;
                _puppetBehavior.groupOverrides = new BehaviourPuppet.MusclePropsGroup[0];
                _puppetBehavior.groundLayers = GLLayers.TableLayerMask | GLLayers.TerrainLayerMask | GLLayers.PhysicsObject_WalkableLayerMask;
                _puppetBehavior.collisionLayers = GLLayers.PhysicsObject_NonWalkableLayerMask | GLLayers.GrabbedLayerMask;
                _puppetBehavior.collisionResistance = new Weight(100f);

                // Gets the created rigidbodies
                // TODO we should change puppet master to use the bipedreferences and
                // return created RBs
                _headRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.head.name).GetComponent<Rigidbody>();
                _upperTorsoRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.spine[UpperTorsoIdx].name).GetComponent<Rigidbody>();
                _lowerTorsoRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.pelvis.name).GetComponent<Rigidbody>();
                _upperArmLRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.leftUpperArm.name).GetComponent<Rigidbody>();
                _upperArmRRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.rightUpperArm.name).GetComponent<Rigidbody>();
                _upperLegLRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.leftThigh.name).GetComponent<Rigidbody>();
                _upperLegRRB = _puppetMaster.transform.FindDeepChild_Breadth(_bipedReferences.rightThigh.name).GetComponent<Rigidbody>();

                // TODO remove me
                BoxCollider headCol = _headRB.gameObject.GetComponent<BoxCollider>();
                headCol.center = new Vector3(0, 0.16f, 0);
                headCol.size = new Vector3(0.4f, 0.4f, 0.4f);

                var getUpProne = new BehaviourBase.AnimatorEvent
                {
                    animationState = "GetUpProne",
                    crossfadeTime = 0.2f,
                    resetNormalizedTime = true
                };
                _puppetBehavior.onGetUpProne.animations = new BehaviourBase.AnimatorEvent[] { getUpProne };

                var getUpSupine = new BehaviourBase.AnimatorEvent
                {
                    animationState = "GetUpSupine",
                    crossfadeTime = 0.2f,
                    resetNormalizedTime = true
                };
                _puppetBehavior.onGetUpSupine.animations = new BehaviourBase.AnimatorEvent[] { getUpSupine };

                var onLoseBalance = new BehaviourBase.AnimatorEvent
                {
                    animationState = "Fall",
                    crossfadeTime = 0.7f,
                    resetNormalizedTime = false
                };
                _puppetBehavior.onLoseBalance.animations = new BehaviourBase.AnimatorEvent[] { onLoseBalance };
                if (_puppetBehavior.onLoseBalance.unityEvent == null)
                    _puppetBehavior.onLoseBalance.unityEvent = new UnityEngine.Events.UnityEvent();
                _puppetBehavior.onLoseBalance.unityEvent.AddListener(OnPuppetLoseBalance);


                _puppetBehavior.puppetMaster = _puppetMaster;
                //puppet.Initiate();
            }
        }
        else
        {
            if (_puppetMaster != null)
                Destroy(_puppetMaster);
            _puppetMaster = null;
        }
    }
    public void LocalUnpin()
    {
        if(_puppetBehavior != null)
            _puppetBehavior.SetState(BehaviourPuppet.State.Unpinned);
    }
    public void IntegrateRagdollHitPose(ref RagdollMain ragdollData)
    {
        if(_puppetBehavior != null)
            _puppetBehavior.SetState(BehaviourPuppet.State.Unpinned);

        //Rigidbody _headRB = _bipedReferences.head.GetComponent<Rigidbody>();
        //Rigidbody _upperTorsoRB = _bipedReferences.spine[UpperTorsoIdx].GetComponent<Rigidbody>();
        //Rigidbody _lowerTorsoRB = _bipedReferences.spine[LowerTorsoIdx].GetComponent<Rigidbody>();
        //Rigidbody _upperArmLRB = _bipedReferences.leftUpperArm.GetComponent<Rigidbody>();
        //Rigidbody _upperArmRRB = _bipedReferences.rightUpperArm.GetComponent<Rigidbody>();
        //Rigidbody _upperLegLRB = _bipedReferences.leftThigh.GetComponent<Rigidbody>();
        //Rigidbody _upperLegRRB = _bipedReferences.rightThigh.GetComponent<Rigidbody>();

        // Head
        _headRB.MovePosition(ragdollData.HeadPos);
        _headRB.MoveRotation(ragdollData.HeadRot);
        _headRB.velocity = ragdollData.HeadVel;
        _headRB.angularVelocity = ragdollData.HeadAngVel;
        // UpperTorso
        _upperTorsoRB.MovePosition(ragdollData.UpperTorsoPos);
        _upperTorsoRB.MoveRotation(ragdollData.UpperTorsoRot);
        _upperTorsoRB.velocity = ragdollData.UpperTorsoVel;
        _upperTorsoRB.angularVelocity = ragdollData.UpperTorsoAngVel;
        // LowerTorso
        _lowerTorsoRB.MovePosition(ragdollData.LowerTorsoPos);
        _lowerTorsoRB.MoveRotation(ragdollData.LowerTorsoRot);
        _lowerTorsoRB.velocity = ragdollData.LowerTorsoVel;
        _lowerTorsoRB.angularVelocity = ragdollData.LowerTorsoAngVel;
        // UpperArmL
        _upperArmLRB.MovePosition(ragdollData.UpperArmLPos);
        _upperArmLRB.MoveRotation(ragdollData.UpperArmLRot);
        _upperArmLRB.velocity = ragdollData.UpperArmLVel;
        _upperArmLRB.angularVelocity = ragdollData.UpperArmLAngVel;
        // UpperArmR
        _upperArmRRB.MovePosition(ragdollData.UpperArmRPos);
        _upperArmRRB.MoveRotation(ragdollData.UpperArmRRot);
        _upperArmRRB.velocity = ragdollData.UpperArmRVel;
        _upperArmRRB.angularVelocity = ragdollData.UpperArmRAngVel;
        // UpperLegL
        _upperLegLRB.MovePosition(ragdollData.UpperLegLPos);
        _upperLegLRB.MoveRotation(ragdollData.UpperLegLRot);
        _upperLegLRB.velocity = ragdollData.UpperLegLVel;
        _upperLegLRB.angularVelocity = ragdollData.UpperLegLAngVel;
        // UpperLegR
        _upperLegRRB.MovePosition(ragdollData.UpperLegRPos);
        _upperLegRRB.MoveRotation(ragdollData.UpperLegRRot);
        _upperLegRRB.velocity = ragdollData.UpperLegRVel;
        _upperLegRRB.angularVelocity = ragdollData.UpperLegRAngVel;
    }
    private void OnPuppetLoseBalance()
    {
        // TODO detect if we own this object
        Debug.LogWarning("puppet lost balance!");
        if (!_sceneObject.DoWeOwn)
            return;
        // send out a big update of the puppet state
        IntoRagdollMain(out RagdollMain ragdollData);
        DarkRiftWriter writer = DarkRiftWriter.Create(512);
        writer.Write(_sceneObject.GetID());
        RagdollSerialization.Serialize(ref ragdollData, writer);
        RealtimeNetworkUpdater.Instance.EnqueueUnreliableUpdate(null, writer, ServerTags.Ragdoll_Main, RealtimeNetworkUpdater.Instance.RagdollPoseHitPriority);
        // TODO we should retry sending this for a few frames, if we didn't get the chance to the first time around
    }
    private void IntoRagdollMain(out RagdollMain ragdollData)
    {
        // TODO there should be a better storage for this stuff
        //Rigidbody _headRB = _bipedReferences.head.GetComponent<Rigidbody>();
        //Rigidbody _upperTorsoRB = _bipedReferences.spine[UpperTorsoIdx].GetComponent<Rigidbody>();
        //Rigidbody _lowerTorsoRB = _bipedReferences.spine[LowerTorsoIdx].GetComponent<Rigidbody>();
        //Rigidbody _upperArmLRB = _bipedReferences.leftUpperArm.GetComponent<Rigidbody>();
        //Rigidbody _upperArmRRB = _bipedReferences.rightUpperArm.GetComponent<Rigidbody>();
        //Rigidbody _upperLegLRB = _bipedReferences.leftThigh.GetComponent<Rigidbody>();
        //Rigidbody _upperLegRRB = _bipedReferences.rightThigh.GetComponent<Rigidbody>();
        ragdollData = new RagdollMain
        {
            // Head
            HeadPos = _headRB.transform.localPosition,
            HeadRot = _headRB.transform.localRotation,
            HeadVel = _headRB.velocity,
            HeadAngVel = _headRB.angularVelocity,
            // UpperTorso
            UpperTorsoPos = _upperTorsoRB.transform.localPosition,
            UpperTorsoRot = _upperTorsoRB.transform.localRotation,
            UpperTorsoVel = _upperTorsoRB.velocity,
            UpperTorsoAngVel = _upperTorsoRB.angularVelocity,
            // LowerTorso
            LowerTorsoPos = _lowerTorsoRB.transform.localPosition,
            LowerTorsoRot = _lowerTorsoRB.transform.localRotation,
            LowerTorsoVel = _lowerTorsoRB.velocity,
            LowerTorsoAngVel = _lowerTorsoRB.angularVelocity,
            // UpperArmL
            UpperArmLPos = _upperLegLRB.transform.localPosition,
            UpperArmLRot = _upperArmLRB.transform.localRotation,
            UpperArmLVel = _upperArmLRB.velocity,
            UpperArmLAngVel = _upperArmLRB.angularVelocity,
            // UpperArmR
            UpperArmRPos = _upperArmRRB.transform.localPosition,
            UpperArmRRot = _upperArmRRB.transform.localRotation,
            UpperArmRVel = _upperArmRRB.velocity,
            UpperArmRAngVel = _upperArmRRB.angularVelocity,
            // UpperLegL
            UpperLegLPos = _upperLegLRB.transform.localPosition,
            UpperLegLRot = _upperLegLRB.transform.localRotation,
            UpperLegLVel = _upperLegLRB.velocity,
            UpperLegLAngVel = _upperLegLRB.angularVelocity,
            // UpperLegR
            UpperLegRPos = _upperLegRRB.transform.localPosition,
            UpperLegRRot = _upperLegRRB.transform.localRotation,
            UpperLegRVel = _upperLegRRB.velocity,
            UpperLegRAngVel = _upperLegRRB.angularVelocity,
        };
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Mode
        byte[] modeArray;
        if (_serializedBehavior.TryReadProperty(ModeKey, out modeArray, out int _))
            Mode = (RagdollType)modeArray[0];
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        // Mode
        _modeSerializationArray[0] = (byte)Mode;
        _serializedBehavior.LocallySetData(ModeKey, _modeSerializationArray);
    }
    public override void Destroy()
    {
        //if (_hasAddedColliders)
        //{
            //_hasAddedColliders = false;
            //for(int i = 0; i < _addedColliders.Count; i++)
                //Destroy(_addedColliders[i]);
            //_addedColliders.Clear();
        //}
        if (_puppetMaster != null)
            Destroy(_puppetMaster);
        _puppetMaster = null;
    }
    public override bool DoesRequireCollider() { return false; }

    public override bool DoesRequirePosRotScaleSyncing() { return true; }

    public override bool DoesRequireRigidbody() { return false; }

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
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic = Intrinsic.Create("ExplodeRagdoll");
        intrinsic.AddParam(PhysicsBehavior.ForceMagParamName.value);
        intrinsic.AddParam(ValString.positionStr.value);
        intrinsic.AddParam(PhysicsBehavior.RadiusParamName.value);
        intrinsic.AddParam(PhysicsBehavior.UpwardsParamName.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Adds an explosion force to this object", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in ExplodeRagdoll call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the force magnitude
            ValNumber forceVal = context.GetVar(PhysicsBehavior.ForceMagParamName) as ValNumber;
            if (forceVal == null)
            {
                UserScriptManager.LogToCode(context, "No force input for ExplodeRagdoll!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the position vector
            Value posVal = context.GetVar(ValString.positionStr);
            if (posVal == null)
            {
                UserScriptManager.LogToCode(context, "No position input for ExplodeRagdoll!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            UserScriptManager.VecType vecType = UserScriptManager.ParseVecInput(posVal, out _, out _, out Vector3 position, out _);
            if(vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad position input for AddForce!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Parse the radius
            ValNumber radiusVal = context.GetVar(PhysicsBehavior.RadiusParamName) as ValNumber;
            if (radiusVal == null)
            {
                UserScriptManager.LogToCode(context, "No radius input for ExplodeRagdoll!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            // Parse the upwards modifier
            ValNumber upwardsVal = context.GetVar(PhysicsBehavior.UpwardsParamName) as ValNumber;
            if (upwardsVal == null)
            {
                UserScriptManager.LogToCode(context, "No upwards input for ExplodeRagdoll!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            RagdollBehavior ragdollBehavior = sceneObject.RagdollBehavior;
            if(ragdollBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Ragdoll behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            Rigidbody[] rigids = ragdollBehavior.gameObject.GetComponentsInChildren<Rigidbody>();
            ragdollBehavior.LocalUnpin();
            Debug.LogError("Ragdoll rigids " + rigids.Length);
            foreach(var r in rigids)
                r.AddExplosionForce((float)forceVal.value, position, (float)radiusVal.value, (float)upwardsVal.value);

            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("SetRagdollMode");
        intrinsic.AddParam(ModeValStr.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Set ragdoll mode", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetRagdollMode call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ValString modeVal = context.GetVar(ModeValStr) as ValString;
            if (modeVal == null)
            {
                UserScriptManager.LogToCode(context, "No mode for SetRagdollMode!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            RagdollBehavior ragdollBehavior = sceneObject.RagdollBehavior;
            if(ragdollBehavior == null)
            {
                UserScriptManager.LogToCode(context, "Ragdoll behavior not present!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            RagdollType mode;
            if (modeVal.value == "colliders")
                mode = RagdollType.Colliders;
            else if (modeVal.value == "joints")
                mode = RagdollType.CollidersJoints;
            else if (modeVal.value == "followAnim")
                mode = RagdollType.ColliderJoints_FollowAnim;
            else
            {
                UserScriptManager.LogToCode(context, "Unknown mode type " + modeVal.value, UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            ragdollBehavior.Mode = mode;
            ragdollBehavior.RefreshProperties();
            return Intrinsic.Result.True;
		};
    }
    //private void AddColliders()
    //{
    //    _hasAddedColliders = true;
    //    if (_addedColliders == null)
    //        _addedColliders = new List<Collider>();

    //    // Head
    //    CapsuleCollider headCollider = _bipedReferences.head.gameObject.AddComponent<CapsuleCollider>();
    //    headCollider.center = new Vector3(0, 0.15f, 0.05f);
    //    headCollider.radius = 0.1f;
    //    headCollider.height = 0.4f;
    //    _addedColliders.Add(headCollider);

    //    BoxCollider hipCollider = _bipedReferences.pelvis.gameObject.AddComponent<BoxCollider>();
    //    hipCollider.center = Vector3.zero;
    //    hipCollider.size = new Vector3(0.37f, 0.3f, 0.25f);
    //    _addedColliders.Add(hipCollider);

    //    BoxCollider spine0Collider = _bipedReferences.spine[0].gameObject.AddComponent<BoxCollider>();
    //    spine0Collider.center = new Vector3(0, 0.23f, 0);
    //    spine0Collider.size = new Vector3(0.38f, 0.48f, 0.3f);
    //    _addedColliders.Add(spine0Collider);

    //    CapsuleCollider rArmCollider = _bipedReferences.rightUpperArm.gameObject.AddComponent<CapsuleCollider>();
    //    rArmCollider.center = new Vector3(0, 0.15f, 0.01f);
    //    rArmCollider.radius = 0.06f;
    //    rArmCollider.height = 0.3f;
    //    rArmCollider.direction = 1;
    //    _addedColliders.Add(rArmCollider);

    //    CapsuleCollider rForeArmCollider = _bipedReferences.rightForearm.gameObject.AddComponent<CapsuleCollider>();
    //    rForeArmCollider.center = new Vector3(0, 0.12f, 0.01f);
    //    rForeArmCollider.radius = 0.06f;
    //    rForeArmCollider.height = 0.25f;
    //    rForeArmCollider.direction = 1;
    //    _addedColliders.Add(rForeArmCollider);

    //    BoxCollider rHandCollider = _bipedReferences.rightHand.gameObject.AddComponent<BoxCollider>();
    //    rHandCollider.center = new Vector3(0, 0.1f, 0.01f);
    //    rHandCollider.size = new Vector3(0.1f, 0.2f, 0.06f);
    //    _addedColliders.Add(rHandCollider);

    //    CapsuleCollider lArmCollider = _bipedReferences.leftUpperArm.gameObject.AddComponent<CapsuleCollider>();
    //    lArmCollider.center = new Vector3(0, 0.15f, 0.01f);
    //    lArmCollider.radius = 0.06f;
    //    lArmCollider.height = 0.3f;
    //    lArmCollider.direction = 1;
    //    _addedColliders.Add(lArmCollider);

    //    CapsuleCollider lForeArmCollider = _bipedReferences.leftForearm.gameObject.AddComponent<CapsuleCollider>();
    //    lForeArmCollider.center = new Vector3(0, 0.12f, 0.01f);
    //    lForeArmCollider.radius = 0.06f;
    //    lForeArmCollider.height = 0.25f;
    //    lForeArmCollider.direction = 1;
    //    _addedColliders.Add(lForeArmCollider);

    //    BoxCollider lHandCollider = _bipedReferences.leftHand.gameObject.AddComponent<BoxCollider>();
    //    lHandCollider.center = new Vector3(0, 0.1f, 0.01f);
    //    lHandCollider.size = new Vector3(0.1f, 0.2f, 0.06f);
    //    _addedColliders.Add(lHandCollider);

    //    CapsuleCollider rThighCollider = _bipedReferences.rightThigh.gameObject.AddComponent<CapsuleCollider>();
    //    rThighCollider.center = new Vector3(0, 0.24f, 0);
    //    rThighCollider.radius = 0.07f;
    //    rThighCollider.height = 0.4f;
    //    rThighCollider.direction = 1;
    //    _addedColliders.Add(rThighCollider);

    //    CapsuleCollider rCalfCollider = _bipedReferences.rightCalf.gameObject.AddComponent<CapsuleCollider>();
    //    rCalfCollider.center = new Vector3(0, 0.2f, 0);
    //    rCalfCollider.radius = 0.05f;
    //    rCalfCollider.height = 0.3f;
    //    rCalfCollider.direction = 1;
    //    _addedColliders.Add(rCalfCollider);

    //    BoxCollider rFootCollider = _bipedReferences.rightFoot.gameObject.AddComponent<BoxCollider>();
    //    rFootCollider.center = new Vector3(0, 0.03f, 0.015f);
    //    rFootCollider.size = new Vector3(0.1f, 0.16f, 0.1f);
    //    _addedColliders.Add(rFootCollider);

    //    CapsuleCollider lThighCollider = _bipedReferences.leftThigh.gameObject.AddComponent<CapsuleCollider>();
    //    lThighCollider.center = new Vector3(0, 0.24f, 0);
    //    lThighCollider.radius = 0.07f;
    //    lThighCollider.height = 0.4f;
    //    lThighCollider.direction = 1;
    //    _addedColliders.Add(lThighCollider);

    //    CapsuleCollider lCalfCollider = _bipedReferences.leftCalf.gameObject.AddComponent<CapsuleCollider>();
    //    lCalfCollider.center = new Vector3(0, 0.2f, 0);
    //    lCalfCollider.radius = 0.05f;
    //    lCalfCollider.height = 0.3f;
    //    lCalfCollider.direction = 1;
    //    _addedColliders.Add(lCalfCollider);

    //    BoxCollider lFootCollider = _bipedReferences.leftFoot.gameObject.AddComponent<BoxCollider>();
    //    lFootCollider.center = new Vector3(0, 0.03f, 0.015f);
    //    lFootCollider.size = new Vector3(0.1f, 0.16f, 0.1f);
    //    _addedColliders.Add(lFootCollider);

    //}
}
