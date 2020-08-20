using KinematicCharacterController;
using RootMotion.Dynamics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Handles the in game time. This does stuff like pausing, which
/// stops the game time from increasing, even though Update is still
/// being called.
/// One motivation behind this script is that in VR the FixedUpdate
/// doesn't seem to always be called before each Update, even though
/// the fixed time step is properly configured. I think that this is
/// due to a floating point issue.
/// </summary>
public class TimeManager : GenericSingleton<TimeManager>
{
    public KinematicCharacterSystem CharacterSystem;
    public bool IsPlaying { get { return _isPlaying; } }
    public bool IsPlayingOrStepped { get { return _isPlaying || _didStepThisFrame; } }
    public float RenderTime { get; private set; }
    public float RenderUnscaledTime { get; private set; }
    public float RenderDeltaTime { get; private set; }
    public float RenderUnscaledDeltaTime { get; private set; }
    public float PhysicsTimestep { get; private set; }
    public float PhysicsTime { get; private set; }
    public bool IsInPhysics { get; private set; }
    public int HighResolutionClock { get { return System.Environment.TickCount; } }

    private bool _isPlaying = true;
    private bool _didStepThisFrame = false;
    private bool _stepOncePending = false;
    private double _physicsUnscaledTimestep_d;
    private double _physicsTimestep_d;
    private double _renderTime = 0;
    private double _renderUnscaledTime = 0;
    private double _physicsTime = 0;
    private double _physicsUnscaledTime = 0;
    private double _timeScale = 1.0;
    const double MaxDeltaTime = 0.2;
    /// <summary>
    /// Multiple scripts can each request to play/pause. We only
    /// play if no scripts are requesting to pause
    /// </summary>
    private readonly List<MonoBehaviour> _pausingScripts = new List<MonoBehaviour>(2);
    protected override void Awake()
    {
        base.Awake();
        _isPlaying = true;
    }
    /// <summary>
    /// Play/pause work on a unanimous basis, where
    /// all requesters need to be set to playing
    /// </summary>
    /// <param name="requester"></param>
    public void Play(MonoBehaviour requester)
    {
        _pausingScripts.RemoveBySwap(requester);
        if(_pausingScripts.Count == 0)
            _isPlaying = true;
    }
    /// <summary>
    /// Play/pause work on a unanimous basis, where
    /// all requesters need to be set to playing
    /// </summary>
    /// <param name="requester"></param>
    public void Pause(MonoBehaviour requester)
    {
        if (!_pausingScripts.Contains(requester))
            _pausingScripts.Add(requester);
        _isPlaying = false;
    }
    public void StepOnce()
    {
        _stepOncePending = true;
    }
    public void SetPhysicsTimestep(double dt)
    {
        _physicsUnscaledTimestep_d = dt;
        _physicsTimestep_d = _timeScale * _physicsUnscaledTimestep_d;
        PhysicsTimestep = (float)_physicsTimestep_d;
    }
    public void SetTimeScale(double scale)
    {
        _timeScale = scale;
        _physicsTimestep_d = _timeScale * _physicsUnscaledTimestep_d;
        PhysicsTimestep = (float)_physicsTimestep_d;
    }
    public float SecondsSince(int prevTicks)
    {
        int delTicks = System.Environment.TickCount - prevTicks;
        // Handle wrap arounds
        if(delTicks < 0)
        {
            Debug.LogError("Tick overflow! was " + prevTicks + " now " + Environment.TickCount);
            // TODO no idea of this is correct
            delTicks += int.MaxValue;
        }

        return delTicks / (float)TimeSpan.TicksPerSecond;
    }
    private void SimulatePhysics()
    {
        IsInPhysics = true;
        _physicsTime += _physicsTimestep_d;
        PhysicsTime = (float)_physicsTime;
        _physicsUnscaledTime += _physicsUnscaledTimestep_d;
        UserScriptManager.Instance.GL_FixedUpdate(PhysicsTimestep);
        CharacterSystem.GL_FixedUpdate(PhysicsTimestep);
        CharacterBehavior.GL_FixedUpdate_Static();
        UserManager.Instance.GL_FixedUpdate(PhysicsTimestep);
        if(PuppetMasterSettings.instance != null)
        {
            var allPuppets = PuppetMasterSettings.instance.puppets;
            for (int i = 0; i < allPuppets.Count; i++)
                allPuppets[i].OnPreSimulate(PhysicsTimestep);
        }
        Physics.Simulate(PhysicsTimestep);
        if(PuppetMasterSettings.instance != null)
        {
            var allPuppets = PuppetMasterSettings.instance.puppets;
            for (int i = 0; i < allPuppets.Count; i++)
                allPuppets[i].OnPostSimulate();
        }
        IsInPhysics = false;
    }
    void Update()
    {
        if (!_isPlaying && !_stepOncePending)
        {
            _didStepThisFrame = false;
            return;
        }

        //if (_stepOncePending)
            //Debug.Log("Step once");

        _didStepThisFrame = _stepOncePending;

        double dt = Time.unscaledDeltaTime;

        // Clamp the delta time. This will make
        // the in-game time run slower than real
        // time when under heavy load
        if (dt > MaxDeltaTime)
        {
            //Debug.LogWarning("Clamping delta time " + dt);
            dt = MaxDeltaTime;
        }

        // TODO we can make this more precise by keeping an int
        // with how many times it was the expected dt, and only
        // use += for unexpected dts. (Addition is more lossy then
        // multiplication for floating point errors)
        _renderTime += (_timeScale * dt);
        _renderUnscaledTime += dt;
        RenderTime = (float)_renderTime;
        RenderUnscaledTime = (float)_renderUnscaledTime;
        RenderDeltaTime = (float)(_timeScale * dt);
        RenderUnscaledDeltaTime = (float)dt;

        // Every render frame must have at least one physics update
        // TODO unless headset is off and vsync is off
        SimulatePhysics();

        // If there was a lag spike for some reason, we have to run
        // the physics system extra to catch up
        // We don't catch up if this is a step-by-frame, just because
        // it's nice to know that you've run just one frame
        if (!_stepOncePending)
        {
            while(_renderTime > _physicsTime)
            {
                //Debug.Log("Catching up " + _renderTime + "->" + _physicsTime);
                SimulatePhysics();
            }
        }
        _stepOncePending = false;

        //if (Input.GetKeyDown(KeyCode.O))
        //{
        //    Debug.Log("sleeping");
        //    Thread.Sleep(500);
        //}
    }
}
