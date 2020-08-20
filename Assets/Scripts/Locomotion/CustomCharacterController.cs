using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;


public class CustomCharacterController : MonoBehaviour, ICharacterController
{
    public delegate void CharacterDelegate();
    public delegate void CharacterRigidbodyDelegate(Rigidbody r);
    public CharacterDelegate OnVelocityUpdate;
    public CharacterRigidbodyDelegate OnHitObjectWhenMoving;

    public enum CharacterState
    {
        Default,
    }
    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }
    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool CrouchDown;
        public bool CrouchUp;
        public bool SprintDown;
    }
    public struct AICharacterInputs
    {
        public Vector3 MoveVector;
        public Vector3 LookVector;
    }
    public struct NetworkCharacterInputs
    {
        /// <summary>
        /// Is this the initial sample when starting,
        /// or when transitioning from build->play?
        /// </summary>
        public bool IsFirstSample;
        public Vector2 MoveVector;
        public Quaternion CameraRotation;
        public Vector3 Position;
        public bool IsGrounded;
        public Vector3 BaseVelocity;// Only used if not grounded
        //public int FrameNum;
        //public int NetworkTicks;
        public bool SprintDown; //TODO
    }
    public enum BonusOrientationMethod
    {
        None,
        TowardsGravity,
        TowardsGroundSlopeAndGravity,
    }

    public KinematicCharacterMotor Motor;
    public Vector3 PreviousVelocity { get; private set; }
    // The velocity after tracking space / position correction are applied
    public Vector3 PreviousFinalVelocity { get; private set; }
    // The first applied velocity. Normally this is the same as Velocity,
    // except when there's a rollback. This is just the last applied velocity for sum velocity
    public Vector3 PreviousLastAddedVelocity { get; private set; }
    // The velocity that we last had at the beginning of UpdateVelocity
    public Vector3 PreviousInitialVelocity { get; private set; }
    public Vector3 PreviousPosition { get; private set; }
    public Vector3 PreviousMoveInputVector { get; private set; }
    public float PreviousTime { get; private set; }
    //public int PreviousTicks { get; private set; }
    public Quaternion PreviousRotation { get; private set; }
    public bool PreviousIsGrounded { get; private set; }
    public bool PreviousIsSprintDown { get; private set; }

    [Header("Stable Movement")]
    public float RunSpeed = 10f;
    public float WalkSpeed = 4f;
    // How forward do we need to be to
    // be able to sprint
    public float MinForwardToRun = 0.9f;
    public float StableMovementSharpness = 15f;
    public float OrientationSharpness = 10f;
    /*
    public float ForwardSpeed = 5.65f;
    public float BackwardSpeed = 1.42f;
    public float LeftRightSpeed = 1f;
    */
    public OrientationMethod CamOrientationMethod = OrientationMethod.TowardsCamera;

    [Header("Air Movement")]
    public float MaxAirMoveSpeed = 15f;
    public float AirAccelerationSpeed = 15f;
    public float Drag = 0.1f;

    [Header("Jumping")]
    public bool AllowJumpingWhenSliding = false;
    public float JumpUpSpeed = 10f;
    public float JumpScalableForwardSpeed = 10f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0f;

    [Header("Misc")]
    public List<Collider> IgnoredColliders = new List<Collider>();
    public BonusOrientationMethod BonusGravityOrientationMethod = BonusOrientationMethod.None;
    public float BonusOrientationSharpness = 10f;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public Transform MeshRoot;
    public Transform CameraFollowPoint;
    public bool TrackingSpaceCorrect = false;
    public bool IsLocal = false;

    public CharacterState CurrentCharacterState { get; private set; }

    private Collider[] _probedColliders = new Collider[8];
    private RaycastHit[] _probedHits = new RaycastHit[8];
    [SerializeField]
    private bool _inputSprint;
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;
    private Vector3 _internalVelocityAdd = Vector3.zero;
    private bool _shouldBeCrouching = false;
    private bool _isCrouching = false;
    private bool _hasPendingLocalInput = false;
    /// <summary>
    /// The delta between where the camera is and where
    /// the character controller is. This should ideally
    /// be 0, so we add a velocity to the controller to
    /// move it towards the VR camera. We then move the
    /// VR holder in the opposite vector
    /// </summary>
    private Vector3 _cameraControllerOffset;
    private bool _wasLastVelocityUpdateSetCamPosition = false;

    /// <summary>
    /// For network users, if the position we received
    /// was not far from where we think it is, we apply
    /// an extra velocity in that direction, over a period
    /// of frames
    /// </summary>
    private Vector3 _networkPositionDelta;
    private bool _applyExtraPosDelta = false;
    private bool _didNetworkSetBaseVelocityThisFrame = false;
    private Vector3 _networkSentBaseVelocity;

    //private Vector3 lastInnerNormal = Vector3.zero;
    //private Vector3 lastOuterNormal = Vector3.zero;
    private bool _hasInit = false;

    private void Start()
    {
        //Debug.Log("Custom character start");
        Init();
    }
    public void Init()
    {
        if (_hasInit)
            return;
        _hasInit = true;

        // Handle initial state
        TransitionToState(CharacterState.Default);

        // Assign the characterController to the motor
        Motor.CharacterController = this;
    }

    /// <summary>
    /// This is called every frame by ExamplePlayer in order to tell the character what its inputs are
    /// </summary>
    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        _hasPendingLocalInput = true;
        // Clamp input
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        }
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Move and look inputs
                    _moveInputVector = cameraPlanarRotation * moveInputVector;

                    switch (CamOrientationMethod)
                    {
                        case OrientationMethod.TowardsCamera:
                            _lookInputVector = cameraPlanarDirection;
                            break;
                        case OrientationMethod.TowardsMovement:
                            _lookInputVector = _moveInputVector.normalized;
                            break;
                    }

                    // Jumping input
                    if (inputs.JumpDown)
                    {
                        _timeSinceJumpRequested = 0f;
                        _jumpRequested = true;
                    }

                    _inputSprint = inputs.SprintDown;

                    // Crouching input
                    if (inputs.CrouchDown)
                    {
                        _shouldBeCrouching = true;

                        if (!_isCrouching)
                        {
                            _isCrouching = true;
                            Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                            MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                        }
                    }
                    else if (inputs.CrouchUp)
                    {
                        _shouldBeCrouching = false;
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// This is called every frame by the AI script in order to tell the character what its inputs are
    /// </summary>
    public void SetInputs(ref AICharacterInputs inputs)
    {
        _moveInputVector = inputs.MoveVector;
        _lookInputVector = inputs.LookVector;
    }
    #region old_version
    //bool w = false;
    //int frameNum = 0;
    //// Values needed to figure out the time for when an input refers to
    //private float _timeOnInitialTick = float.NaN;
    //private int _initialTick = -1;
    //private readonly Queue<PreviousState> _previousStates = new Queue<PreviousState>();
    //private readonly Queue<NetworkCharacterInputs> _networkInputs = new Queue<NetworkCharacterInputs>();
    //private int GetCurrentNetworkTick()
    //{
    //    if (float.IsNaN(_timeOnInitialTick))
    //    {
    //        Debug.LogError("Can't get network tick, no inital val");
    //        return -1;
    //    }
    //    return _initialTick + Time2NetTick(GetCurrentTime(), _timeOnInitialTick);
    //}
    //private bool GetPositionForTickTime(int tickTime, out PreviousState bestFit)
    //{
    //    bestFit = new PreviousState { };
    //    if(_previousStates.Count == 0)
    //    {
    //        //Debug.LogError("No position state!");
    //        return false;
    //    }
    //    // We get the previous state that's the closest
    //    int minAbsDist = int.MaxValue;
    //    int bestDist = int.MaxValue;
    //    while(_previousStates.Count > 0)
    //    {
    //        PreviousState previousState = _previousStates.Peek();
    //        int dist = previousState.NetworkTick - tickTime;
    //        int absDist = Math.Abs(dist);
    //        //Debug.Log("Tick #" + previousState.InterpolatedNetworkTick + " has dist " + dist);
    //        if (dist < minAbsDist)
    //        {
    //            minAbsDist = absDist;
    //            bestFit = previousState;
    //            bestDist = dist;
    //            _previousStates.Dequeue();
    //            //Debug.Log("New best tick #" + previousState.InterpolatedNetworkTick);
    //        }
    //        else
    //        {
    //            //Debug.Log("Found good tick #" + previousState.InterpolatedNetworkTick);
    //            return true;
    //        }
    //    }
    //    // If we hit here, then the remote is referring to a sample that has not yet
    //    // occurred, or one that just happened this frame.
    //    //Debug.Log("Out of samples " + _previousStates.Count + " best tick has dist " + bestDist);
    //    if (minAbsDist < GetAverageTickIncreasePerFrame() / 2)
    //        return true;
    //    return false;
    //}
    #endregion
    private bool _hasPendingNetworkInput = false;
    private NetworkCharacterInputs _currentNetworkInput;

    private int GetAverageTickIncreasePerFrame()
    {
        return Mathf.RoundToInt(720f / VRSDKUtils.Instance.GetDeviceDisplayFps());
    }
    public static int Time2NetTick(float currentTime, float initalTime)
    {
        return (int)Math.Floor((currentTime - initalTime) * 720.0);
    }
    private float GetCurrentTime()
    {
        //return Time.fixedUnscaledTime;
        return TimeManager.Instance.PhysicsTime;
    }
    public void SetInputs(ref NetworkCharacterInputs inputs)
    {
        _hasPendingNetworkInput = true;
        _currentNetworkInput = inputs;
    }
    #region old_version
    // Handling of the input from the network. Called in fixedupdate
    // so that we know the exact tick for this frame
    //private void HandleInputFromNetwork() {
    //    // No new stuff from the network,
    //    // just use the same stuff as last time
    //    if (_networkInputs.Count == 0)
    //        return;
    //    NetworkCharacterInputs inputs = _networkInputs.Peek();
    //    bool isFirstSample = float.IsNaN(_timeOnInitialTick);
    //    // Setup the network tick if we haven't already
    //    if (isFirstSample)
    //    {
    //        //Debug.Log("On first sample, have in buffer " + _networkInputs.Count);
    //        // Clear the buffer of inputs, as this
    //        // if our first frame
    //        while (_networkInputs.Count > 0)
    //            inputs = _networkInputs.Dequeue();
    //        // is our first frame
    //        _timeOnInitialTick = GetCurrentTime();
    //        _initialTick = inputs.NetworkTicks;
    //    }
    //    // How many frames behind from the sender are we allowed to have?
    //    // Higher values mean more latency, but more stability
    //    const int MaxLatencyFrames = 4;
    //    // How many inputs can we buffer before we drop? (This is relevant during load)
    //    const int MaxBufferedInputs = 5; 
    //    // How many frames ahead are we allowed to be?
    //    // Being more ahead means that we need to rollback
    //    // the whole system to integrate updates
    //    const int MaxFramesAhead = 2; 
    //    int currentTick = GetCurrentNetworkTick();
    //    if (_networkInputs.Count > MaxBufferedInputs)
    //    {
    //        //Debug.LogWarning("Large buffer of network inputs! " + _networkInputs.Count + " we will drop data and reset our skew");
    //        while (_networkInputs.Count > MaxBufferedInputs / 2)
    //        {
    //            inputs = _networkInputs.Dequeue();
    //            //Debug.Log("In large buffer, tick: " + inputs.NetworkTicks);
    //        }
    //        int prevTick = currentTick;
    //        _timeOnInitialTick = GetCurrentTime();
    //        _initialTick = inputs.NetworkTicks;
    //        currentTick = GetCurrentNetworkTick();
    //        //Debug.LogWarning("Tick was " + prevTick + " now " + currentTick);
    //    }

    //    int tickDelta = inputs.NetworkTicks - currentTick;
    //    // Sometimes we'll receive input that's too early
    //    // it's from a fixedUpdate that we haven't reached yet,
    //    // so when that happens we just ignore and check again
    //    if(tickDelta > GetAverageTickIncreasePerFrame() / 2)
    //    {
    //        //Debug.LogWarning("Recv future input, got #" + inputs.NetworkTicks + " but we're on " + currentTick + " delta " + (inputs.NetworkTicks - currentTick) + " num in buffer: " + _networkInputs.Count);
    //        //if (true)
    //        if (tickDelta > MaxLatencyFrames * GetAverageTickIncreasePerFrame())
    //        {
    //            // If there's too much latency between when we are and when the sender is
    //            // then we want to move ourself forward in time a bit, but not all the way
    //            // this is because there could have been a momentary speedup on the sending side
    //            // and we don't want to adjust to a tick that's too far ahead in time
    //            int prevTick = currentTick;
    //            _timeOnInitialTick = GetCurrentTime();
    //            _initialTick = inputs.NetworkTicks - (MaxLatencyFrames / 2) * GetAverageTickIncreasePerFrame();
    //            currentTick = GetCurrentNetworkTick();
    //            // NB this will make things sorta off if the GetAverageTickIncrease is wrong
    //            //Debug.LogWarning("Moving clock forward due to receiving old data, was " + prevTick + " now " + currentTick + " recv " + inputs.NetworkTicks + " but using " + _initialTick);
    //        }
    //        // Wait for the next fixed update to see if it's time
    //        return;
    //    } else if (tickDelta < -GetAverageTickIncreasePerFrame() * MaxFramesAhead)
    //    {
    //        // If our clock is really far ahead of the sender's, then we should move
    //        // our clock back to match
    //        int prevTick = currentTick;
    //        _timeOnInitialTick = GetCurrentTime();
    //        _initialTick = inputs.NetworkTicks;
    //        currentTick = GetCurrentNetworkTick();
    //        //Debug.LogWarning("Moving clock back to match sender. Was #" + prevTick + " now " + currentTick);
    //    }
    //    else if(Math.Abs(tickDelta) < GetAverageTickIncreasePerFrame() / 4 && tickDelta > 1)
    //    {
    //        //TODO we should have some handling for if the ticks are a little bit off.
    //        // This is needed because, e.g. the frame timing is 12 ticks if we're off by
    //        // 6 ticks there's ambiguity about which frame a tick corresponds to, and there
    //        // will be stutter as some frames are correctly aligned, and others are off by
    //        // one. The difficulty in doing this is that the system might be running physics
    //        // at a different rate
    //    }
    //    // Now that we know that we're going to be using this sample, we can dequeue it
    //    if(_networkInputs.Count > 0)
    //        _networkInputs.Dequeue();
    //    //Debug.Log("Rcv #" + inputs.NetworkTicks + " our tick " + currentTick + " num in buffer " + _networkInputs.Count);

    //    _moveInputVector = new Vector3(inputs.MoveVector.x, 0, inputs.MoveVector.y);
    //    _inputSprint = inputs.SprintDown;
    //    frameNum = inputs.FrameNum;
    //    _lookInputVector = _moveInputVector.normalized;
    //    // Calculate camera direction and rotation on the character plane
    //    Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
    //    if (cameraPlanarDirection.sqrMagnitude == 0f)
    //        cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
    //    _lookInputVector = cameraPlanarDirection;
    //    bool didGroundChange = false;
    //    if(inputs.IsGrounded != Motor.GroundingStatus.IsStableOnGround)
    //    {
    //        didGroundChange = true;
    //        //Debug.Log((IsLocal ? "Local " : "Network ") + "Change in grounding, " + Motor.GroundingStatus.IsStableOnGround + "->" + inputs.IsGrounded);
    //        if (!inputs.IsGrounded)
    //        {
    //            Motor.ForceUnground();
    //            Motor.SetPosition(inputs.Position, false);
    //            _networkPositionChangeFramesRemaining = 0;
    //        }
    //        else
    //        {
    //            Motor.SetPosition(inputs.Position, false);
    //            _networkPositionChangeFramesRemaining = 0;
    //        }
    //    }

    //    // Only use the base velocity if not on the ground
    //    if (!inputs.IsGrounded)
    //    {
    //        _networkSentBaseVelocity = inputs.BaseVelocity;
    //        _didNetworkSetBaseVelocityThisFrame = true;
    //    }
    //    // If this is the first sample, just move there directly
    //    if (isFirstSample)
    //    {
    //        Motor.SetPosition(inputs.Position, true);
    //        return;
    //    }
    //    //if (currentTick > inputs.NetworkTicks)
    //        //Debug.LogWarning("Decreasing time tick! Have " + currentTick + " recv " + inputs.NetworkTicks + " delta " + (currentTick - inputs.NetworkTicks));
    //    PreviousState previousState;
    //    bool isMessageStale = GetPositionForTickTime(inputs.NetworkTicks, out previousState);
    //    if (isMessageStale)
    //    {
    //        //Debug.Log("For tick #" + inputs.NetworkTicks + " we were at: " + previousState.Position.ToPrettyString() + " on tick " + previousState.NetworkTick + " current tick: " + currentTick + " curr pos: " + transform.localPosition.ToPrettyString());
    //        if((_moveInputVector - PreviousMoveInputVector).sqrMagnitude > 0.0001f)
    //        {
    //            int numFramesSkipped = Mathf.RoundToInt((currentTick - inputs.NetworkTicks) / (float)GetAverageTickIncreasePerFrame());
    //            //Debug.LogWarning("We got a stale frame where input changed, move was " + PreviousMoveInputVector.ToPrettyString() + " now " + _moveInputVector.ToPrettyString() + " num in buffer: " + _previousStates.Count + " num ticks stale: " + numFramesSkipped);

    //            // If the input changed, this means that our last fixed update was wrong, because we were using the old movement vector. So we:
    //            // 1) Move back the distance that we moved last time
    //            // 2) Move according to the input, but with a velocity times how many frames we missed plus one
    //            // so if we were moving (1,0) and received a new message (0,1) that's from one frame ago, we velocity move (-1,0) and 2 * (0,1)
    //            _internalVelocityAdd = -PreviousLastAddedVelocity;
    //            // If we have more than one frame skipped, we need to also roll back the distance that we moved there
    //            if (numFramesSkipped > 1)
    //            {
    //                while(_previousStates.Count > 0)
    //                {
    //                    PreviousState state = _previousStates.Dequeue();
    //                    Debug.LogWarning("Rolling back velocity " + state.LastAddedVelocity);
    //                    _internalVelocityAdd -= state.LastAddedVelocity;
    //                }
    //            }
    //            _velocityMultiplier = numFramesSkipped + 1;
    //            _hasOverrideInitialVelocity = true;
    //            _overrideInitialVelocity = previousState.InitialVelocity;
    //        }
    //    }
    //    else
    //    {
    //        //Debug.Log("Best pos was " + previousState.Position.ToPrettyString() + " but will use local position");
    //        previousState.Position = transform.localPosition;
    //    }
    //    // If we recv this while we're still paused waiting on
    //    // object to load, we should just set positions directly
    //    if (Orchestrator.Instance != null && Orchestrator.Instance.IsPausedWaitingForObjectLoad)
    //    {
    //        Debug.Log("Moving immediately, as we're pending physics");
    //        Motor.SetPosition(inputs.Position, true);
    //        _networkPositionChangeFramesRemaining = 0;
    //        return;
    //    }
    //    // If the grounding changed, we manually set the position,
    //    // so there's no need to do the whole position correction
    //    // via velocity method
    //    if (didGroundChange)
    //        return;

    //    // If we're referencing a past frame, and that past frame used a rollback thing (and thus has a non-1 velocityModifier) then comparing the
    //    // positions doesn't make sense, so we skip that
    //    if(isMessageStale && previousState.VelocityMultiplier != 1)
    //    {
    //        //Debug.LogWarning("Skipping delta check, net is referencing a past frame that used a rollback");
    //        return;
    //    }

    //    Vector3 positionDelta = inputs.Position - previousState.Position;
    //    float positionDeltaMag = positionDelta.sqrMagnitude;
    //    if(inputs.MoveVector.sqrMagnitude > 0.001f || w)
    //    {
    //        w = true;
    //        //Debug.Log("net recv input " + inputs.MoveVector.ToPrettyString() + " expected pos: " + inputs.Position.ToPrettyString());
    //        //if (positionDeltaMag > 0.0001f)
    //            //Debug.LogError("Delta position " + positionDeltaMag + " we are at " + transform.position.ToPrettyString() + " should be " + inputs.Position.ToPrettyString() + " recv tick #" + inputs.NetworkTicks + " local tick #" + currentTick);
    //        //else
    //            //Debug.Log("Delta position " + positionDeltaMag + " we are at " + transform.position.ToPrettyString()  + " should be " + inputs.Position.ToPrettyString() + " recv tick #" + inputs.NetworkTicks + " local tick #" + currentTick);
    //    }
    //    if (positionDeltaMag < 0.0001f)
    //    {
    //        // Do nothing
    //        //Debug.Log("Position of network character is good");
    //        _networkPositionChangeFramesRemaining = 0;
    //    }
    //    else if(positionDeltaMag < 99991.5f)
    //    {
    //        //Debug.Log("Will use velocity for correction to get to " + inputs.Position.ToPrettyString());
    //        _networkPositionDelta = positionDelta;
    //        if (inputs.IsGrounded)// If in mid-air we correct for y position displacement
    //            _networkPositionDelta.y = 0;
    //        _networkPositionChangeFramesRemaining = NetworkPositionNumFramesForChange;
    //    }else if(positionDeltaMag < 1.5f)
    //    {
    //        Debug.LogWarning("Large position, will move with interpolation");
    //        Motor.SetPosition(inputs.Position, false);
    //        _networkPositionChangeFramesRemaining = 0;
    //    }
    //    else
    //    {
    //        Debug.LogWarning("Large position delta of " + positionDeltaMag + ", will move without interpolation");
    //        Motor.SetPosition(inputs.Position, true);
    //        _networkPositionChangeFramesRemaining = 0;
    //    }
    //}
    #endregion
    private void HandleInputFromNetwork() {
        // No new stuff from the network,
        // just use the same stuff as last time
        // We handle if it's been too long without input
        // elsewhere
        if (!_hasPendingNetworkInput)
            return;
        _hasPendingNetworkInput = false;

        _moveInputVector = new Vector3(_currentNetworkInput.MoveVector.x, 0, _currentNetworkInput.MoveVector.y);
        _inputSprint = _currentNetworkInput.SprintDown;

        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(_currentNetworkInput.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
            cameraPlanarDirection = Vector3.ProjectOnPlane(_currentNetworkInput.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        _lookInputVector = cameraPlanarDirection;
        bool didGroundChange = false;
        if(_currentNetworkInput.IsGrounded != Motor.GroundingStatus.IsStableOnGround)
        {
            didGroundChange = true;
            //Debug.Log((IsLocal ? "Local " : "Network ") + "Change in grounding, " + Motor.GroundingStatus.IsStableOnGround + "->" + inputs.IsGrounded);
            if (!_currentNetworkInput.IsGrounded)
            {
                Motor.ForceUnground();
                Motor.SetPosition(_currentNetworkInput.Position, false);
            }
            else
            {
                Motor.SetPosition(_currentNetworkInput.Position, false);
            }
        }

        // Only use the base velocity if not on the ground
        if (!_currentNetworkInput.IsGrounded)
        {
            _networkSentBaseVelocity = _currentNetworkInput.BaseVelocity;
            _didNetworkSetBaseVelocityThisFrame = true;
        }
        // If this is the first sample, just move there directly
        if (_currentNetworkInput.IsFirstSample)
        {
            //Debug.Log("Moving directly due to first sample");
            Motor.SetPosition(_currentNetworkInput.Position, true);
            return;
        }
        // If we recv this while we're still paused waiting on
        // object to load, we should just set positions directly
        if (Orchestrator.Instance != null && Orchestrator.Instance.IsPausedWaitingForObjectLoad)
        {
            //Debug.Log("Moving immediately, as we're pending physics");
            Motor.SetPosition(_currentNetworkInput.Position, true);
            return;
        }
        // If the grounding changed, we manually set the position,
        // so there's no need to do the whole position correction
        // via velocity method
        if (didGroundChange)
            return;

        Vector3 positionDelta = _currentNetworkInput.Position - Motor.TransientPosition;
        float positionDeltaMag = positionDelta.sqrMagnitude;
        _networkPositionDelta = positionDelta;
        if (_currentNetworkInput.IsGrounded)// If in mid-air we correct for y position displacement
            _networkPositionDelta.y = 0;
        if(positionDeltaMag > 1.5f)
        {
            Debug.LogWarning("Large position, will move without interpolation");
            Motor.SetPosition(_currentNetworkInput.Position, false);
            _applyExtraPosDelta = false;
        }
        else
        {
            _applyExtraPosDelta = _networkPositionDelta.sqrMagnitude > 0.0001f;
        }
    }
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (!IsLocal)
            HandleInputFromNetwork();

        PreviousTime = GetCurrentTime();
        //PreviousTicks = GetCurrentNetworkTick();

        // We use the previous velocity here for things like stopping smoothly
        // but we also have other velocities applied that shouldn't be included
        // in those calculations. So, we compare the provided velocity to the
        // velocity we had last frame. If they're similar, then we use the
        // pre-correction velocity for the rest of these calculations. Otherwise,
        // we use what was provided. We expect that the provided velocity will
        // only be different if we collided with something, or hit the ground.
        // If there were multiple velocities added (like when there's a rollback)
        // we use the most previously applied velocity
        if ((currentVelocity - PreviousFinalVelocity).sqrMagnitude < 0.001f)
            currentVelocity = PreviousLastAddedVelocity;

        PreviousInitialVelocity = currentVelocity;

        // Ground movement
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            //Debug.Log("on ground");
            float currentVelocityMagnitude = currentVelocity.magnitude;

            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;
            if (currentVelocityMagnitude > 0f && Motor.GroundingStatus.SnappingPrevented)
            {
                // Take the normal from where we're coming from
                Vector3 groundPointToCharacter = Motor.TransientPosition - Motor.GroundingStatus.GroundPoint;
                if (Vector3.Dot(currentVelocity, groundPointToCharacter) >= 0f)
                {
                    effectiveGroundNormal = Motor.GroundingStatus.OuterGroundNormal;
                }
                else
                {
                    effectiveGroundNormal = Motor.GroundingStatus.InnerGroundNormal;
                }
            }

            // Reorient velocity on slope
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            // Calculate target velocity
            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
            //Debug.Log("moveInput: " + _moveInputVector.ToPrettyString()
                //+ " inR: " + inputRight.ToPrettyString()
                //+ "net: " + _currentNetworkInput.MoveVector.ToPrettyString());
            // Separate move into forward/back and left/right components
            Vector3 forwardDir = new Vector3(_lookInputVector.x, 0, _lookInputVector.z).normalized;
            Vector3 rightDir = Vector3.Cross(forwardDir, Motor.CharacterUp);
            float forward = Vector3.Dot(reorientedInput, forwardDir);

            //Debug.Log("input: " + reorientedInput.ToPrettyString()
            //+ " forward: " + forwardDir.ToPrettyString()
            //+ " camRot: " + (_currentNetworkInput.CameraRotation * Vector3.forward).ToPrettyString()
            //+ " pos: " + transform.position.ToPrettyString());
            //Debug.DrawRay(transform.position, reorientedInput, Color.red);
            //Debug.DrawRay(transform.position, forwardDir, Color.blue);
            //Debug.DrawRay(transform.position, _currentNetworkInput.CameraRotation * Vector3.forward, Color.green);
            //float leftRight = Vector3.Dot(reorientedInput, rightDir);

            // Apply sprint if we're moving mostly forward
            Vector3 targetMovementVelocity = reorientedInput * (_inputSprint && forward > MinForwardToRun ? RunSpeed : WalkSpeed);
            //Debug.Log("sprint " + _inputSprint);
            /* Strafe speed sucks
            //Vector3 forwardComponent = forward * ((forward > 1) ? ForwardSpeed : BackwardSpeed) * forwardDir;
            //Vector3 leftRightComponent =  rightDir * leftRight;
            Vector3 forwardComponent = Mathf.Abs(forward) * ((forward > 0) ? ForwardSpeed : BackwardSpeed) * reorientedInput;
            Vector3 leftRightComponent =  Mathf.Abs(leftRight) * LeftRightSpeed * reorientedInput;
            Vector3 targetMovementVelocity = forwardComponent + leftRightComponent;
            Debug.Log("forward " + forward + " lr " + leftRight + " final " + targetMovementVelocity.ToPrettyString() + " input " + reorientedInput);
            Debug.Log("look: " + _lookInputVector.ToPrettyString());
            //Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;
            */

            //if (_moveInputVector.sqrMagnitude > 0)
                //Debug.Log("Target movement vel " + targetMovementVelocity + (_inputSprint ? " running " : " walking ") + "forward " + forward + " look input " + _lookInputVector);

            // Smooth movement Velocity
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
        }
        // Air movement
        else
        {
            // Add move input
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;

                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                // Limit air velocity from inputs
                if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                {
                    // clamp addedVel to make total vel not exceed max vel on inputs plane
                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                }
                else
                {
                    // Make sure added vel doesn't go in the direction of the already-exceeding velocity
                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                    {
                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                    }
                }

                // Prevent air-climbing sloped walls
                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    if (Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpenticularObstructionNormal);
                    }
                }

                // Apply added velocity
                currentVelocity += addedVelocity;
            }

            // Gravity
            currentVelocity += Gravity * deltaTime;

            // Drag
            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
        }

        // Handle jumping
        _jumpedThisFrame = false;
        _timeSinceJumpRequested += deltaTime;
        if (_jumpRequested)
        {
            // See if we actually are allowed to jump
            if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
            {
                // Calculate jump direction before ungrounding
                Vector3 jumpDirection = Motor.CharacterUp;
                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                {
                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                }

                // Makes the character skip ground probing/snapping on its next update. 
                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                Motor.ForceUnground();

                // Add to the return velocity and reset jump state
                currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
                _jumpRequested = false;
                _jumpConsumed = true;
                _jumpedThisFrame = true;
            }
        }
        if (_didNetworkSetBaseVelocityThisFrame)
        {
            currentVelocity = _networkSentBaseVelocity;
            _didNetworkSetBaseVelocityThisFrame = false;
        }

        PreviousLastAddedVelocity = currentVelocity;

        PreviousMoveInputVector = _moveInputVector;
        PreviousPosition = transform.localPosition;
        PreviousRotation = transform.localRotation;
        PreviousVelocity = currentVelocity;

        // Take into account additive velocity
        if (_internalVelocityAdd.sqrMagnitude > 0f)
        {
            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }
        //if(_moveInputVector.sqrMagnitude > 0)
        //{
            //Debug.Log((IsLocal ? "Local " : "Network ") + " input " + _moveInputVector.ToPrettyString()
                //+ " pos " + transform.position.ToPrettyString() + " grounded: " + Motor.GroundingStatus.IsStableOnGround + " vel: " + currentVelocity.ToPrettyString()
                //+ " delTime " + deltaTime
                //);
        //}

        // Add an extra velocity to compensate for position differences
        // between the network copy that we've received, and the transform
        // we simulate
        if (_applyExtraPosDelta)
        {
            currentVelocity += _networkPositionDelta / deltaTime;
            _applyExtraPosDelta = false;
            //Debug.LogWarning("Positional correction " + _networkPositionDelta.ToPrettyString());
        }

        // Move the user obj to where the player transform is now
        // This is so that our cam-player delta is correctly calculated
        // Otherwise the delta we calculate would be stale, b/c the player
        // obj moved at the beginning of FixedUpdate
        if (TrackingSpaceCorrect)
        {
            if (TrackingSpace.Instance.FinishMove())
                VRCameraController.Instance.UpdateFromInputs();
            // If we have multiple fixed updates before one 
            if (!_hasPendingLocalInput)
            {
                _wasLastVelocityUpdateSetCamPosition = false;
                //Debug.Log("Extra fixed update, not moving");
                return;
            }
            _hasPendingLocalInput = false;
            // Add a velocity to move the character controller to be under the VR camera
            _cameraControllerOffset = (ControllerAbstraction.Instances[0].GetPositionFixedUpdate() - CameraFollowPoint.position);
            _cameraControllerOffset.y = 0;
            //Debug.Log("Using delta " + _cameraControllerOffset.ToPrettyString() + " mag " + _cameraControllerOffset.sqrMagnitude
                //+ " cam: " + ControllerAbstraction.Instances[0].GetPositionFixedUpdate().ToPrettyString()
                //+ " we " + CameraFollowPoint.position);
            if (deltaTime > 0 && _cameraControllerOffset.sqrMagnitude > 0.0001f)
                //if (deltaTime > 0 && _cameraControllerOffset.sqrMagnitude > 1e-5f)
            {
                currentVelocity += _cameraControllerOffset / deltaTime;
                _wasLastVelocityUpdateSetCamPosition = true;
                //Debug.LogWarning("Adding a velocity " + currentVelocity + " from pos delta " + _cameraControllerOffset + " mag2: " + _cameraControllerOffset.sqrMagnitude + " pos " + transform.position + " delT: " + deltaTime);
            }
            else
            {
                _wasLastVelocityUpdateSetCamPosition = false;
                //Debug.Log("No additional vel, mag2: " + cameraControllerOffset.sqrMagnitude);
            }
        }

        PreviousFinalVelocity = currentVelocity;
        PreviousIsGrounded = Motor.GroundingStatus.IsStableOnGround;
        PreviousIsSprintDown = _inputSprint;
        if (OnVelocityUpdate != null)
            OnVelocityUpdate();
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called after the character has finished its movement update
    /// </summary>
    public void AfterCharacterUpdate(float deltaTime)
    {
        // Move the VR holder back
        if (TrackingSpaceCorrect)
        {
            //Debug.Log("delta charac pre #" + Time.frameCount + ": " + (ControllerAbstraction.Instances[0].GetPosition() - CameraFollowPoint.position).sqrMagnitude);
            //Debug.Log("We character moved " + (transform.position - _velPos) + " mag " + (transform.position - _velPos).sqrMagnitude + " delTime: " + deltaTime + " pos " + transform.position);
            if (_wasLastVelocityUpdateSetCamPosition)
            {
                //Debug.LogError("Character Moving by " + _cameraControllerOffset.ToPrettyString() + " mag " + _cameraControllerOffset.magnitude);
                //TrackingSpace.Instance.transform.position -= _cameraControllerOffset;
                TrackingSpace.Instance.SetCameraOffset(_cameraControllerOffset);
                Vector3 del = (ControllerAbstraction.Instances[0].GetPositionFixedUpdate() - CameraFollowPoint.position);
                //Debug.Log("The delta is now: " + del.ToPrettyString());
            }
            else
            {
                TrackingSpace.Instance.SetCameraOffset(Vector3.zero);
            }
        }
        _cameraControllerOffset = Vector3.zero;
        //Debug.Log("delta charac post #" + Time.frameCount + ": " + (ControllerAbstraction.Instances[0].GetPosition() - CameraFollowPoint.position).sqrMagnitude);

        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Handle jump-related values
                    {
                        // Handle jumping pre-ground grace period
                        if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                        {
                            _jumpRequested = false;
                        }

                        if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                        {
                            // If we're on a ground surface, reset jumping values
                            if (!_jumpedThisFrame)
                            {
                                _jumpConsumed = false;
                            }
                            _timeSinceLastAbleToJump = 0f;
                        }
                        else
                        {
                            // Keep track of time since we were last able to jump (for grace period)
                            _timeSinceLastAbleToJump += deltaTime;
                        }
                    }

                    // Handle uncrouching
                    if (_isCrouching && !_shouldBeCrouching)
                    {
                        // Do an overlap test with the character's standing height to see if there are any obstructions
                        Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                        if (Motor.CharacterOverlap(
                            Motor.TransientPosition,
                            Motor.TransientRotation,
                            _probedColliders,
                            Motor.CollidableLayers,
                            QueryTriggerInteraction.Ignore) > 0)
                        {
                            // If obstructions, just stick to crouching dimensions
                            Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                        }
                        else
                        {
                            // If no obstructions, uncrouch
                            MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                            _isCrouching = false;
                        }
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// Handles movement state transitions and enter/exit callbacks
    /// </summary>
    public void TransitionToState(CharacterState newState)
    {
        CharacterState tmpInitialState = CurrentCharacterState;
        OnStateExit(tmpInitialState, newState);
        CurrentCharacterState = newState;
        OnStateEnter(newState, tmpInitialState);
    }

    /// <summary>
    /// Event when entering a state
    /// </summary>
    public void OnStateEnter(CharacterState state, CharacterState fromState)
    {
        switch (state)
        {
            case CharacterState.Default:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// Event when exiting a state
    /// </summary>
    public void OnStateExit(CharacterState state, CharacterState toState)
    {
        switch (state)
        {
            case CharacterState.Default:
                {
                    break;
                }
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // Handle landing and leaving ground
        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLanded();
        }
        else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLeaveStableGround();
        }
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        if (IgnoredColliders.Count == 0)
        {
            return true;
        }

        if (IgnoredColliders.Contains(coll))
        {
            return false;
        }

        return true;
    }


    private Quaternion _tmpTransientRot;

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called before the character begins its movement update
    /// </summary>
    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its rotation should be right now. 
    /// This is the ONLY place where you should set the character's rotation
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                    {
                        // Smoothly interpolate from current to target look direction
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                        // Set the current rotation (which will be used by the KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                    }

                    Vector3 currentUp = (currentRotation * Vector3.up);
                    if (BonusGravityOrientationMethod == BonusOrientationMethod.TowardsGravity)
                    {
                        // Rotate from current up to invert gravity
                        Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                        currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                    }
                    else if (BonusGravityOrientationMethod == BonusOrientationMethod.TowardsGroundSlopeAndGravity)
                    {
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);

                            Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                            // Move the position to create a rotation around the bottom hemi center instead of around the pivot
                            Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
                        }
                        else
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                    }
                    else
                    {
                        Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                        currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                    }
                    break;
                }
        }
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        Rigidbody r = hitCollider.attachedRigidbody;
        if (r)
        {
            Vector3 relativeVel = Vector3.Project(r.velocity, hitNormal) - Vector3.Project(Motor.Velocity, hitNormal);
            //Debug.Log("Character hit " + hitCollider.gameObject.name);
            if (OnHitObjectWhenMoving != null)
                OnHitObjectWhenMoving(r);
        }
    }

    public void AddVelocity(Vector3 velocity)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    _internalVelocityAdd += velocity;
                    break;
                }
        }
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    protected void OnLanded()
    {
    }

    protected void OnLeaveStableGround()
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
}