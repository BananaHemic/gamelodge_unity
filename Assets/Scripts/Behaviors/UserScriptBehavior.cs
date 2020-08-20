using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System.Text;

/// <summary>
/// Represents an instance of a SerializedUserScript
/// Owns and runs the miniscript interpreter for this object
/// </summary>
public class UserScriptBehavior : BaseBehavior
{
    private static bool _hasLoadedIntrinsics = false;
    private CustomMiniscriptInterpreter _interpreter;
    /// <summary>
    /// The backing script for this behavior
    /// This is a shared reference, UserScriptManager owns the
    /// actual object
    /// </summary>
    private DRUserScript _userScript;

    // The list of pending events/values
    private ValList _eventList;
    private ValList _eventValList;

    public bool CanRun { get; private set; }
    public DRUserScript.WhoRuns WhoRuns
    {
        get
        {
            if (_userScript == null)
            {
                Debug.LogError("Can't get who runs, no script!");
                return DRUserScript.WhoRuns.Host;
            }
            return _userScript.WhoRunsScript;
        }
    }

    private readonly List<ValString> _pendingEvents = new List<ValString>();
    private readonly List<Value> _pendingEventValues = new List<Value>();
    /// <summary>
    /// Event function name -> the miniscript function reference
    /// </summary>
    private readonly Dictionary<ValString, Value> _event2Value = new Dictionary<ValString, Value>();
    /// <summary>
    /// The events that the script does not currently implement
    /// </summary>
    private readonly HashSet<ValString> _eventsNotPresent = new HashSet<ValString>();

    //const double UserScriptTimeout = 0.01;
    const double UserScriptTimeout = 0.05;
    //const double UserScriptTimeout = 999f;

    protected override void ChildInit()
    {
        Parser parser = UserScriptManager.Instance.GetParsedCodeForScript(_userScript.GetID());
        if(parser != null)
            Recompile(parser);
        CanRun = true;
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return _userScript.SyncPosRotScale;
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
    public void SetUserScript(DRUserScript userScript)
    {
        _userScript = userScript;
    }
    public void InvokeEvent(ValString eventName, Value val=null)
    {
        _pendingEvents.Add(eventName);
        //if (val == null)
            //val = ValNull.instance;
        _pendingEventValues.Add(val);
    }
    private Value EventName2MiniscriptFunction(ValString eventName)
    {
        // Pull from a cache first
        Value miniscriptFunction;
        if(_event2Value.TryGetValue(eventName, out miniscriptFunction)) {
            //Debug.Log("Used cache function ref");
            return miniscriptFunction;
        }

        // If it's not in the cache, see if we've already tried and failed to pull
        // a function reference for this event
        if (_eventsNotPresent.Contains(eventName))
            return null;

        // Then try to get the function reference from the script
        miniscriptFunction = _interpreter.GetGlobalValue(eventName);

        // If there is no such function, store that fact
        if (miniscriptFunction == null)
            _eventsNotPresent.Add(eventName);
        else // Otherwise, store the function reference
            _event2Value.Add(eventName, miniscriptFunction);
        return miniscriptFunction;
    }
    private void LoadDataIntoMiniscript()
    {
        List<ExposedVariable> exposedVariables = _sceneObject.GetAllExposedVariables();
        for(int i = 0; i < exposedVariables.Count; i++)
        {
            // TODO only add variable if it was in the script
            exposedVariables[i].InsertIntoInterpreter(_interpreter);
        }
    }
    public void Recompile(Parser parsedCode)
    {
        if (_interpreter != null)
            _interpreter.Dispose();
        _interpreter = new CustomMiniscriptInterpreter(parsedCode, OnScriptRuntimeException)
        {
            hostData = this
        };
        //Debug.Log(_codeBuilder.ToString());
        _interpreter.Compile();
        _event2Value.Clear();
        _eventsNotPresent.Clear();
        if (_eventList != null)
            _eventList.Unref();
        _eventList = null;
        if (_eventValList != null)
            _eventValList.Unref();
        _eventValList = null;
    }
    private bool FireEvents(bool onlyRunEvents)
    {
        Value isAtEndVal = null;
        // Check that we've initialized, otherwise
        // there's no point in pushing events
        isAtEndVal = _interpreter.GetGlobalValue(ValString.isAtEndStr);
        if(isAtEndVal == null)
        {
            // Still initializing
            _pendingEvents.Clear();
            _pendingEventValues.Clear();
            return !onlyRunEvents; // Run if this isn't onlyRunEvents, in order to finish init
        }
        ValNumber endValNum = isAtEndVal as ValNumber;
        if(endValNum != null && endValNum.BoolValue() == false)
        {
            // We're somewhere other than the end of the script,
            // drop events but still run
            //TODO we should determine if we're at a yield, or if we're at
            // a timeout, and then only update the variables
            // if we were at a yield
            _pendingEvents.Clear();
            _pendingEventValues.Clear();
            return !onlyRunEvents;
        }

        if(_eventList == null)
        {
            //Debug.Log("Adding event global");
            _eventList = ValList.Create();
            _eventValList = ValList.Create();
            _interpreter.SetGlobalValue(ValString.eventsStr, _eventList);
            _interpreter.SetGlobalValue(ValString.eventValsStr, _eventValList);
        }

        if (_pendingEvents.Count != _pendingEventValues.Count)
            Debug.LogError("Wrong event counts, " + _pendingEvents.Count + " vals " + _pendingEventValues.Count);
        // Turn all pending events into Miniscript functions
        //int numAdded = 0;
        for(int i = 0; i < _pendingEvents.Count; i++)
        {
            var pendingEvent = _pendingEvents[i];
            Value functionHandle = EventName2MiniscriptFunction(pendingEvent);
            if (functionHandle == null)
                continue;
            var pendingEventVal = _pendingEventValues[i];
            _eventList.Add(functionHandle, false);
            _eventValList.Add(pendingEventVal, false);
            //numAdded++;
        }
        _pendingEvents.Clear();
        _pendingEventValues.Clear();

        // Exit early if there are no pending events
        if(_eventList.Count == 0)
        {
            if(isAtEndVal == null)
                isAtEndVal = _interpreter.GetGlobalValue(ValString.isAtEndStr);
            ValNumber isAtEndNum = isAtEndVal as ValNumber;
            if (isAtEndVal != null && isAtEndVal.BoolValue())
            {
                //Debug.Log("Exit early, no events");
                return false;
            }
        }

        return true;
    }
    /// <summary>
    /// Loads events into the scripts, and if appropriate
    /// will actually run miniscript
    /// </summary>
    /// <param name="onlyRunEvents">Only run the interpreter if it's waiting for events</param>
    public void FireEventsAndRun(bool onlyRunEvents)
    {
        if(_interpreter == null)
        {
            _pendingEvents.Clear();
            _pendingEventValues.Clear();
            return;
        }
        if (!ShouldRun())
        {
            _pendingEvents.Clear();
            _pendingEventValues.Clear();
            return;
        }

        bool shouldRun = FireEvents(onlyRunEvents);
        if (!shouldRun)
            return;

        LoadDataIntoMiniscript();
        try
        {
            var endReason = _interpreter.RunUntilDone(UserScriptTimeout);
            if (endReason == Interpreter.EndReason.OutOfTime)
                Debug.LogError("Failed to finish running! " + base._sceneObject.GetID());
        } catch (Miniscript.MiniscriptException err)
        {
            Debug.LogError("Behavior #" + _userScript.GetID() + " error: " + err.ToString());
        }
    }
    public ushort GetScriptID()
    {
        return _userScript.GetID();
    }
    //private void OnScriptOutput(string output)
    public void OnScriptOutput(string output, int line, UserScriptManager.CodeLogType logType)
    {
        // Push the log into CodeUI, if appropriate
        if(CodeUI.Instance != null && CodeUI.Instance.CurrentUserScript == _userScript)
        {
            //Debug.Log("Pushing log into the code UI");
            CodeUI.Instance.AddLogMessage(output, line, LogMessageButton.LogMessageType.PrintMessage);
        }
    }
    private void OnScriptRuntimeException(string error, int line)
    {
        Debug.LogWarning(error + " Ln #" + line + " object #" + _sceneObject);
        // Push the log into CodeUI, if appropriate
        if(CodeUI.Instance != null && CodeUI.Instance.CurrentUserScript == _userScript)
        {
            //Debug.Log("Pushing runtime error into the code UI");
            CodeUI.Instance.AddLogMessage(error + " object " + _sceneObject.name, line, LogMessageButton.LogMessageType.RuntimeException);
        }
    }

    private bool ShouldRun()
    {
        switch (_userScript.WhoRunsScript)
        {
            case DRUserScript.WhoRuns.Owner:
                return _sceneObject.DoWeOwn;
            case DRUserScript.WhoRuns.Host:
                Debug.Log("TODO");
                return true;
            case DRUserScript.WhoRuns.Everyone:
                return true;
            default:
                return false;
        }
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        //throw new System.NotImplementedException();
    }

    public override void UpdateParamsFromSerializedObject()
    {
        //throw new System.NotImplementedException();
    }

    public override void RefreshProperties()
    {
        //throw new System.NotImplementedException();
    }

    public override void Destroy()
    {
        //Debug.Log("Script remove " + name);
        UserScriptManager.Instance.RemoveBehavior(this);
        CanRun = false;
    }

    private void OnDestroy()
    {
        // Called late, here is where we actually dispose
        // stuff. We do it here, because the script might
        // want to be Destroyed, and then do stuff
        if (_interpreter != null)
            _interpreter.Dispose();
        _interpreter = null;
    }

    public override List<ExposedFunction> GetFunctions()
    {
        return null;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return null;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;
    }
}
