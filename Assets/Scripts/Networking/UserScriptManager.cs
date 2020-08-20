using DarkRift;
using Miniscript;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UserScriptManager : GenericSingleton<UserScriptManager>
{
    public Sprite UserScriptDisplaySprite;

    private static bool _hasLoadedIntrinsics = false;
    public enum VecType
    {
        Double,
        Vector2,
        Vector3,
        Quaternion,
        None,
    }
    public enum CodeLogType
    {
        Log,
        Warning,
        Error
    }
    private readonly Dictionary<ushort, DRUserScript> _id2UserScript = new Dictionary<ushort, DRUserScript>();
    private readonly List<DRUserScript> _allUserScripts = new List<DRUserScript>();
    // The storage of the parsed code. The code is taken from the DRUserScript and has PostScriptText
    // appended to it, then parsed and put it here, when needed
    private readonly Dictionary<ushort, Parser> _id2ParsedCode = new Dictionary<ushort, Parser>();
    /// <summary>
    /// The mapping from the networked script ID to the behavior instances running that script
    /// </summary>
    private readonly Dictionary<ushort, List<UserScriptBehavior>> _script2Instances = new Dictionary<ushort, List<UserScriptBehavior>>();
    private readonly List<UserScriptBehavior> _allScriptInstances = new List<UserScriptBehavior>();
    private readonly List<MiniscriptBehaviorInfo> _behaviorInfos = new List<MiniscriptBehaviorInfo>();
    /// <summary>
    /// Sometimes we destroy a script while it's running, so we store it in here
    /// until after the firing is done
    /// </summary>
    private readonly SortedList<int> _behaviorsPendingRemoval = new SortedList<int>();
    private static readonly List<ExposedFunction> _alwaysExposedFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedEvent> _alwaysExposedEvents = new List<ExposedEvent>();
    /// <summary>
    /// If we've added functions from other scripts
    /// </summary>
    private static bool _hasAddedOtherFunctionsToList = false;
    /// <summary>
    /// If we've added events from other scripts
    /// </summary>
    private static bool _hasAddedOtherEventsToList = false;
    // Are we currently running scripts
    private bool _areWeRunningScripts = false;

    public static readonly ValString OnUpdateEventName = ValString.Create("OnUpdate", false);
    public static readonly ValString OnFixedUpdateEventName = ValString.Create("OnFixedUpdate", false);
    public static readonly ValString OnKeyInputEventName = ValString.Create("OnKeyInput", false);
    public static readonly ValString FilterValStr = ValString.Create("filter", false);
    /// <summary>
    /// The code at the end of every file.
    /// This contains:
    /// 1) __isAtEnd: A flag for if we're at the end of the file. If this doesn't exist, we're still
    ///     initializing. If it's false, we're in the middle of an yield. If it's true, then
    ///     we're done and just waiting for a new event
    /// 2) __events: the event ValString that we want to call
    /// 3) __eventVals: the parameter that we want to pass to the function (may be null)
    /// </summary>
    const string PostScriptText = @"
        __events = []
        __eventVals = []
        while true
            __isAtEnd = false
            while __events.len > 0
                __nextEvent = __events.pull
                __nextEventVal = __eventVals.pull
                if __nextEventVal != null then
                    __nextEvent(__nextEventVal)
                else
                    __nextEvent
                end if
            end while
            __isAtEnd = true
            yield
        end while";

    protected override void Awake()
    {
        base.Awake();
        InitializeMiniscriptIntrinsics();
    }
    public List<DRUserScript> GetAllUserScripts()
    {
        return _allUserScripts;
    }
    public void LocallyClearAllUserScripts()
    {
        _id2UserScript.Clear();
        _allUserScripts.Clear();
        _id2ParsedCode.Clear();
        _script2Instances.Clear();
        _allScriptInstances.Clear();
        _behaviorInfos.Clear();
    }
    public DRUserScript CreateNewScript(string name, string code, bool syncPosRotSpeed, DRUserScript.WhoRuns whoRuns)
    {
        DRUserScript userScript = DarkRiftConnection.Instance.CreateDRUserScript(name, code, syncPosRotSpeed, whoRuns);
        if (userScript == null)
            return null;
        _id2UserScript.Add(userScript.GetID(), userScript);
        _allUserScripts.Add(userScript);
        _script2Instances.Add(userScript.GetID(), new List<UserScriptBehavior>());
        _behaviorInfos.Add(new MiniscriptBehaviorInfo(name, userScript.GetID(), UserScriptDisplaySprite));
        return userScript;
    }
    // Retrieve the SceneObject from the calling context, assuming that this function
    // was called as a member of a ValMap
    // e.g. myObj.SomeFunction()
    private static SceneObject GetSceneObjectAsCalledObject(Context context)
    {
        Value self = context.GetLocal(ValString.selfStr);
        if (self == null)
            return null;
        ValSceneObject valSceneObject = self as ValSceneObject;
        if (valSceneObject == null)
            return null;
        return valSceneObject.SceneObject;
    }
    public static UserScriptBehavior GetScriptFromContext(Context context)
    {
        return context.interpreter.hostData as UserScriptBehavior;
    }
    public static SceneObject GetSceneObjectFromContext(Context context)
    {
        // First try to retrieve the SceneObject from the context, assuming that the context
        // was a ValMap object of a SceneObject
        SceneObject sceneObject = GetSceneObjectAsCalledObject(context);
        if (sceneObject != null)
            return sceneObject;
        // Otherwise, assume that the context was as part of a script, and as a result we should
        // use the SceneObject associated with the interpreter
        UserScriptBehavior scriptBehavior = context.interpreter.hostData as UserScriptBehavior;
        if(scriptBehavior == null)
        {
            Debug.LogError("No Script Behavior in intrinsic host data!");
            return null;
        }
        sceneObject = scriptBehavior.GetSceneObject();
        if(sceneObject == null)
        {
            Debug.LogError("No scene object in intrinsic call!");
            return null;
        }
        return sceneObject;
    }
    public void UpdateDRUserScriptID(ushort oldID, ushort newID)
    {
        DRUserScript userScript;
        if(!_id2UserScript.TryGetValue(oldID, out userScript))
        {
            Debug.LogError("Failed to update user script from #" + oldID + " to " + newID + " not found!");
            return;
        }
        Debug.Log("Updating user script #" + oldID + "->#" + newID);
        userScript.ObjectID = newID;
        _id2UserScript.Remove(oldID);
        _id2UserScript.Add(newID, userScript);
        _script2Instances.Add(newID, _script2Instances[oldID]);
        _script2Instances.Remove(oldID);
        if(_id2ParsedCode.TryGetValue(oldID, out Parser parser))
        {
            _id2ParsedCode.Remove(oldID);
            _id2ParsedCode.Add(newID, parser);
        }

        // Update the ID within BehaviorInfo
        for(int i = 0; i < _behaviorInfos.Count; i++)
        {
            MiniscriptBehaviorInfo behaviorInfo = _behaviorInfos[i];
            if (behaviorInfo.BehaviorID == oldID)
                behaviorInfo.UpdateID(newID);
        }
    }
    public void LocalUpdateToUserScript(DRUserScript userScript, string newTitle, string newSource, bool doSyncPos, DRUserScript.WhoRuns whoRuns)
    {
        // The code editor can sometimes hold an outdated DRUserScript.
        // So we need to map it to the correct one
        //TODO we need a better solution to this
        if (!_allUserScripts.Contains(userScript))
        {
            Debug.LogWarning("Update for non-existant user script?");
            for (int i = 0; i < _allUserScripts.Count; i++)
            {
                DRUserScript potential = _allUserScripts[i];
                if (potential.GetID() == userScript.GetID())
                {
                    Debug.Log("Updating DRUserScript to current instance.");
                    userScript = potential;
                    break;
                }
            }
        }
        //TODO I think this if should be replaced. It'd be better to just have different ServerTags
        // for the different types of updates
        if(userScript.Name != newTitle || userScript.GetCodeWithoutPostScript() != newSource || userScript.SyncPosRotScale != doSyncPos || userScript.WhoRunsScript != whoRuns)
        {
            //Debug.Log("Name was " + userScript.Name + " now " + newTitle);
            //Debug.Log("Local code was " + userScript.GetCodeWithoutPostScript());
            //Debug.Log("Local node now " + newSource);
            userScript.SetName(newTitle);
            userScript.SetCode(newSource);
            userScript.SetSyncPosRotScale(doSyncPos);
            userScript.SetWhoRuns(whoRuns);

            // TODO it would be ideal if we could only send out the parts of the script that changed
            DarkRiftWriter writer = DarkRiftWriter.Create();
            writer.Write(userScript);
            RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.UpdateUserScript, writer);
        }
        else
        {
            //Debug.LogWarning("Local update, but nothing changed?");
            //Debug.Log("?Local code was " + userScript.GetCodeWithoutPostScript());
            //Debug.Log("?Local node now " + newSource);
        }

        // Re-compile all the behaviors using this script
        List<UserScriptBehavior> behaviorsToUpdate = _script2Instances[userScript.GetID()];
        // Considering that we have at least one script needed this compiled code,
        // compile it now
        if(behaviorsToUpdate.Count > 0)
        {
            // Get the existing parser when possible
            Parser parser;
            if (!_id2ParsedCode.TryGetValue(userScript.GetID(), out parser))
            {
                parser = new Parser();
                _id2ParsedCode.Add(userScript.GetID(), parser);
            }
            else
            {
                // parser will be null when the code previously failed to compile
                if (parser == null)
                {
                    parser = new Parser();
                    _id2ParsedCode[userScript.GetID()] = parser;
                }
                else
                    parser.Reset();
            }
            string code = userScript.GetCodeWithPostScript(PostScriptText);
            parser.Parse(code);

            for (int i = 0; i < behaviorsToUpdate.Count; i++)
            {
                UserScriptBehavior scriptBehavior = behaviorsToUpdate[i];
                scriptBehavior.Recompile(parser);
                scriptBehavior.GetSceneObject().OnAttachedScriptUpdated(scriptBehavior);
            }
        }
    }
    public DRUserScript DuplicateUserScript(DRUserScript scriptToCopy)
    {
        throw new NotImplementedException();
    }
    public List<MiniscriptBehaviorInfo> GetAllNetworkBehaviors()
    {
        return _behaviorInfos;
    }
    public void OnServerCreatedUserScript(DRUserScript networkUserScript)
    {
        _allUserScripts.Add(networkUserScript);
        _id2UserScript.Add(networkUserScript.GetID(), networkUserScript);
        _script2Instances.Add(networkUserScript.GetID(), new List<UserScriptBehavior>());
        _behaviorInfos.Add(new MiniscriptBehaviorInfo(networkUserScript.Name, networkUserScript.GetID(), UserScriptDisplaySprite));
        //Debug.Log("Added user script #" + networkUserScript.GetID() + " called " + networkUserScript.Name);
    }
    public void ServerSentUpdatedUserScript(ushort userScriptID, DarkRiftReader reader)
    {
        // Integrate the update
        DRUserScript userScript;
        if(!_id2UserScript.TryGetValue(userScriptID, out userScript))
        {
            Debug.LogError("Failed to handle script update for " + userScriptID + " not found!");
            // TODO clear reader
            DRUserScript.ClearReaderForDeserializeUpdate(reader);
            return;
        }
        //Debug.Log("Code was " + userScript.GetCodeWithoutPostScript());
        userScript.DeserializeUpdate(reader);
        //Debug.Log("now " + userScript.GetCodeWithoutPostScript());

        //Debug.Log("UserScript update, name " + userScript.Name);
        // Update the Name within BehaviorInfo
        for(int i = 0; i < _behaviorInfos.Count; i++)
        {
            MiniscriptBehaviorInfo behaviorInfo = _behaviorInfos[i];
            if (behaviorInfo.BehaviorID == userScript.GetID())
            {
                //Debug.Log("Updating behavior info name to " + userScript.Name);
                behaviorInfo.SetName(userScript.Name);
            }
        }

        // Re-compile all the existing behaviors using this script
        // Also notify the containing object that the script has changed
        List<UserScriptBehavior> behaviorsToUpdate = _script2Instances[userScript.GetID()];
        // Considering that we have at least one script needed this compiled code,
        // compile it now
        if(behaviorsToUpdate.Count > 0)
        {
            // Get the existing parser when possible
            Parser parser;
            if (!_id2ParsedCode.TryGetValue(userScriptID, out parser))
            {
                parser = new Parser();
                _id2ParsedCode.Add(userScriptID, parser);
            }
            parser.Reset();
            string code = userScript.GetCodeWithPostScript(PostScriptText);
            //bool didParse = true;
            try
            {
                parser.Parse(code);
            }catch(Miniscript.CompilerException miniscriptErr)
            {
                //didParse = false;
                Debug.LogError("Failed to parse server script update: " + miniscriptErr.ToString());
            }

            for (int i = 0; i < behaviorsToUpdate.Count; i++)
            {
                UserScriptBehavior scriptBehavior = behaviorsToUpdate[i];
                // TODO we may not want to compile if we know that parsing failed
                scriptBehavior.Recompile(parser);
                scriptBehavior.GetSceneObject().OnAttachedScriptUpdated(scriptBehavior);
            }
        }

        if(CodeUI.Instance != null && CodeUI.Instance.CurrentUserScript == userScript)
        {
            Debug.Log("May update code display");
            CodeUI.Instance.RefreshScriptCodeAndSettings();
        }
    }
    public Parser GetParsedCodeForScript(ushort scriptID)
    {
        // See if we already have this parsed
        Parser parser;
        if (_id2ParsedCode.TryGetValue(scriptID, out parser))
            return parser;
        // We don't already have the script parsed, so let's do it now
        if(!_id2UserScript.TryGetValue(scriptID, out DRUserScript userScript))
        {
            Debug.LogError("Can't get code, we have no script ID# " + scriptID);
            return null;
        }

        parser = new Parser();
        string code = userScript.GetCodeWithPostScript(PostScriptText);
        try
        {
            parser.Parse(code);
        }catch(Miniscript.CompilerException compilerException)
        {
            Debug.LogError("Error parsing #" + scriptID + ": " + compilerException.ToString());
            parser = null;
        }
        _id2ParsedCode.Add(scriptID, parser);
        return parser;
    }
    public void OnServerSentUpdateScriptSuccess(ushort userScriptID)
    {
        // TODO change the UI to notify the user that the user script has gone through
    }
    public UserScriptBehavior InstantiateBehavior(ushort userScriptID, SceneObject sceneObject)
    {
        DRUserScript userScript;
        if(!_id2UserScript.TryGetValue(userScriptID, out userScript)) {
            Debug.LogError("Failed to instantiate behavior for #" + userScriptID);
            return null;
        }
        UserScriptBehavior scriptBehavior = sceneObject.gameObject.AddComponent<UserScriptBehavior>();
        scriptBehavior.SetUserScript(userScript);
        _script2Instances[userScriptID].Add(scriptBehavior);
        _allScriptInstances.Add(scriptBehavior);
        return scriptBehavior;
    }
    public void RemoveBehavior(UserScriptBehavior scriptBehavior)
    {
        // If this behavior is being removed when we're exiting a save, then
        // we can safely drop this
        if (Orchestrator.Instance.IsLoadingGameState)
        {
            //TODO this will cause problems down the line, if another
            // user removes a behavior while we're loading a game
            Debug.Log("Dropping remove behavior, we're loading");
            return;
        }
        ushort scriptID = scriptBehavior.GetScriptID();
        List<UserScriptBehavior> behaviors;
        if(!_script2Instances.TryGetValue(scriptID, out behaviors))
        {
            Debug.LogError("Failed to remove behavior, script ID #" + scriptID + " not found!");
            return;
        }
        behaviors.RemoveBySwap(scriptBehavior);
        if (_areWeRunningScripts)
        {
            int idx = _allScriptInstances.IndexOf(scriptBehavior);
            _behaviorsPendingRemoval.Add(idx);
        }
        else
            _allScriptInstances.RemoveBySwap(scriptBehavior);
    }
    /// <summary>
    /// Called when an instance of a user script hits a runtime exception
    /// It would be good to display the error in the code UI (if it's open and
    /// currently has the error'd script open) and maybe post a error log somewhere
    /// </summary>
    /// <param name="scriptBehavior"></param>
    public void OnScriptRuntimeError(UserScriptBehavior scriptBehavior)
    {

    }

    public BehaviorInfo GetBehaviorInfoFromID(ushort behaviorID)
    {
        foreach(var behaviorInfo in _behaviorInfos)
        {
            if (behaviorInfo.BehaviorID == behaviorID)
                return behaviorInfo;
        }
        return null;
    }
    public bool TryGetDRUserScript(ushort index, out DRUserScript userScript)
    {
        return _id2UserScript.TryGetValue(index, out userScript);
    }
    public List<ExposedFunction> GetAllAlwaysExposedFunctions()
    {
        if (!_hasAddedOtherFunctionsToList)
        {
            _alwaysExposedFunctions.AddRange(UserManager.Instance.GetAllAlwaysExposedFunctions());
            _hasAddedOtherFunctionsToList = true;
        }
        return _alwaysExposedFunctions;
    }
    public List<ExposedEvent> GetAllAlwaysExposedEvents()
    {
       if(_alwaysExposedEvents.Count == 0)
        {
            _alwaysExposedEvents.Add(new ExposedEvent(OnUpdateEventName, "Runs every frame", null));
            _alwaysExposedEvents.Add(new ExposedEvent(OnFixedUpdateEventName, "Runs when we're calculating physics, use this for physics calculations like adding forces", null));
            _alwaysExposedEvents.Add(new ExposedEvent(OnKeyInputEventName, "When the user hits a keyboard key(s)", new Function.Param[] { new Function.Param("input", ValString.empty) }));
        }
        if (!_hasAddedOtherEventsToList)
        {
            _alwaysExposedEvents.AddRange(UserManager.Instance.GetAllAlwaysExposedEvents());
            _hasAddedOtherEventsToList = true;
        }
        return _alwaysExposedEvents;
    }
    public MiniscriptBehaviorInfo GetScriptByName(string scriptName)
    {
        //TODO a hash table here would be nice
        for(int i = 0; i < _behaviorInfos.Count; i++)
        {
            MiniscriptBehaviorInfo potential = _behaviorInfos[i];
            if (potential.Name == scriptName)
                return potential;
        }
        return null;
    } 

    public void HandleRPCOnScript(DarkRiftReader reader, byte tag)
    {
        // Read the target object
        ushort targetObj = reader.ReadUInt16();
        // Read the target scriptID
        ushort targetScript = reader.ReadUInt16();
        // Read the function
        string functionName = DRCompat.ReadStringSmallerThan255(reader);
        // Read the Value
        Value val;
        if(tag == ServerTags.RPC_All_Buffered_Value
            || tag == ServerTags.RPC_All_NonBuffered_Value
            || tag == ServerTags.RPC_Others_NonBuffered_Value
            || tag == ServerTags.RPC_Host_NonBuffered_Value
            || tag == ServerTags.RPC_TargetUser_NonBuffered_Value
            || tag == ServerTags.RPC_All_Buffered_Value
            || tag == ServerTags.RPC_Others_Buffered_Value
            || tag == ServerTags.RPC_Host_Buffered_Value)
        {
            // Read the value
            val = MiniscriptSerializer.Deserialize(reader);
        }
        else
            val = null;

        //Debug.Log("Recv RPC " + functionName + " with tag " + tag + " obj " + targetObj + " script " + targetScript);
        // Find the object, then the script
        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(targetObj, out SceneObject sceneObject))
        {
            Debug.LogWarning("Failed to handle RPC " + tag + " no scene object #" + targetObj);
            return;
        }

        BaseBehavior baseBehavior = sceneObject.GetBehaviorWithID(true, targetScript);
        if(baseBehavior == null)
        {
            Debug.LogWarning("Failed to handle RPC " + tag + " no script " + targetScript);
            return;
        }

        UserScriptBehavior scriptBehavior = baseBehavior as UserScriptBehavior;
        if(scriptBehavior == null)
        {
            Debug.LogError("RPC script wasn't user script!?");
            return;
        }

        // Now, finally, fire the event
        // TODO use a lookup here, so that we don't recreate ValStrings all the time
        scriptBehavior.InvokeEvent(ValString.Create(functionName), val);
    }

    public static void LogToCode(Context context, string message, CodeLogType logType)
    {
        UserScriptBehavior userScript = context.interpreter.hostData as UserScriptBehavior;
        int lineNum;
        // If the ctx is done, then it's probably an intrinsic
        // so we get line # from the parent context
        if (context.done)
        {
            Context parentCtx = context.parent;
            if (parentCtx.lineNum >= parentCtx.code.Count)
                lineNum = int.MaxValue;
            else
                lineNum = parentCtx.code[parentCtx.lineNum].location.lineNum;
        }
        else
        {
            if (context.lineNum >= context.code.Count)
                lineNum = int.MaxValue;
            else
                lineNum = context.code[context.lineNum].location.lineNum;
        }
        if(userScript != null)
            userScript.OnScriptOutput(message, lineNum, logType);

        if (logType == CodeLogType.Log)
            Debug.Log(message + " #" + lineNum);
        else if (logType == CodeLogType.Warning)
            Debug.LogWarning(message + " #" + lineNum);
        else if (logType == CodeLogType.Error)
            Debug.LogError(message + " #" + lineNum);
    }
    /// <summary>
    /// Initializes the miniscript functions (aka intrinsics)
    /// that we want the user to be able to call
    /// </summary>
    private static void InitializeMiniscriptIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;
        Miniscript.Intrinsics.InitIfNeeded();

        SceneObject.InitializeMiniscriptIntrinsics();
        UserManager.InitializeMiniscriptIntrinsics();
        // Initialize the intrinsics for out custom miniscript types
        ValUser.InitIntrinsics();
        ValVector3.InitIntrinsics();
        ValQuaternion.InitIntrinsics();
        ValSceneObject.InitIntrinsics();
        ValLine.InitIntrinsics();
        // Initialize intrinsics for our behaviors
        // We use to have it as an abstract method,
        // but that led to a lot of bugs because it
        // was a non-static method that we treated
        // statically... If only inheritance worked
        // with static...
        CharacterBehavior.LoadIntrinsics();
        VirtualCameraBehavior.LoadIntrinsics();
        AudioPlayerBehavior.LoadIntrinsics();
        CollisionTypeBehavior.LoadIntrinsics();
        ConfigurableJointBehavior.LoadIntrinsics();
        GrabbableBehavior.LoadIntrinsics();
        LineRendererBehavior.LoadIntrinsics();
        MovingPlatformBehavior.LoadIntrinsics();
        PhysicsBehavior.LoadIntrinsics();
        PhysSoundBehavior.LoadIntrinsics();
        RagdollBehavior.LoadIntrinsics();
        SpawnPointBehavior.LoadIntrinsics();
        UserScriptBehavior.LoadIntrinsics();
        HealthBehavior.LoadIntrinsics();

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("GetObject");
        intrinsic.AddParam("name", ValString.empty);
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Retrieves an object using either it's name, or it's ID", "obj"));
        intrinsic.code = (context, partialResult) => {
            Value recvValue = context.GetVar("name");
            ValString recvStr = recvValue as ValString;
            if(recvValue == null)
            {
                LogToCode(context, "No name for GetObject", CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            string objectNameToGet = recvStr.value;

            SceneObject sceneObject;
            if(!SceneObjectManager.Instance.GetSceneObjectByName(objectNameToGet, out sceneObject))
            {
                LogToCode(context, "No Object by the name of " + objectNameToGet, CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(new ValSceneObject(sceneObject));
		};
        intrinsic = Intrinsic.Create("GetAllObjects");
        intrinsic.AddParam(FilterValStr.value, ValString.empty);
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Gets all objects, with an optional name filter", "objs"));
        intrinsic.code = (context, partialResult) => {
            Value filterValue = context.GetVar(FilterValStr);
            ValString recvStr = filterValue as ValString;
            List<SceneObject> sceneObjects = SceneObjectManager.Instance.GetAllSceneObjects();
            // TODO handle filter
            ValList list = ValList.Create(sceneObjects.Count);
            for(int i = 0; i < sceneObjects.Count; i++)
                list.Add(new ValSceneObject(sceneObjects[i]));

            return new Intrinsic.Result(list);
		};
        intrinsic = Intrinsic.Create("SpawnObject");
        intrinsic.AddParam("nameOfExisting", ValString.empty);
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Spawns a new object, based on the name of an existing object", "existingObjectName"));
        intrinsic.code = (context, partialResult) => {
            Value recvValue = context.GetVar("nameOfExisting");
            ValString recvStr = recvValue as ValString;
            if(recvValue == null)
            {
                LogToCode(context, "No name for SpawnObject", CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            string objectNameToCopy = recvStr.value;
            UserScriptBehavior userScript = context.interpreter.hostData as UserScriptBehavior;
            SceneObject sceneObject;
            if(!SceneObjectManager.Instance.GetSceneObjectByName(objectNameToCopy, out sceneObject))
            {
                LogToCode(context, "No object by the name of " + objectNameToCopy, CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            // Now create a new object, based off the original
            GameObject newObj = GameObject.Instantiate(sceneObject.gameObject, SceneObjectManager.Instance.RootObject);
            SceneObject newSceneObject = newObj.GetComponent<SceneObject>();
            // Force the new scene object to init now
            // So that we can use it's variables and functions
            newObj.name = sceneObject.gameObject.name;
            newSceneObject.Start();
            // Check that the object was made correctly
            // this will fail if we're out of IDs
            if (newSceneObject == null || !newSceneObject.HasInit)
                return Intrinsic.Result.Null;
            return new Intrinsic.Result(new ValSceneObject(newSceneObject));
		};
        intrinsic = Intrinsic.Create("DestroyObject");
        intrinsic.AddParam("objectName", ValString.empty);
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Destroy an object. Either pass the name to destroy, or pass nothing to destroy the current object", "objectName"));
        intrinsic.code = (context, partialResult) => {
            Value recvValue = context.GetVar("objectName");
            ValString recvStr = recvValue as ValString;
            string objectName = recvStr?.value;
            UserScriptBehavior userScript = context.interpreter.hostData as UserScriptBehavior;
            SceneObject sceneObject;
            if (string.IsNullOrEmpty(objectName))
            {
                sceneObject = GetSceneObjectFromContext(context);
            }
            else
            {
                if(!SceneObjectManager.Instance.GetSceneObjectByName(objectName, out sceneObject))
                {
                    LogToCode(context, "No object by the name of " + objectName, CodeLogType.Error);
                    return new Intrinsic.Result(ValNumber.zero);
                }
            }

            // Now destroy the object
            sceneObject.OnDeleted();
            GameObject.Destroy(sceneObject.gameObject);
            return new Intrinsic.Result(ValNumber.one);
		};
        intrinsic = Intrinsic.Create("Raycast");
        intrinsic.AddParam("origin");
        intrinsic.AddParam("dir");
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Raycast a line into the scene, to detect collisions. Returns 0 if hit nothing, or position/object when it hits something", "posObj"));
        intrinsic.code = (context, partialResult) => {
            Value originValue = context.GetVar("origin");
            VecType vecType = ParseVecInput(originValue, out double num, out Vector2 vec2, out Vector3 origin, out Quaternion quat);
            if(vecType != VecType.Vector3)
            {
                LogToCode(context, "A Vector3 is needed for origin", CodeLogType.Error);
                return Intrinsic.Result.False;
            }
            Value dirValue = context.GetVar("dir");
            vecType = ParseVecInput(dirValue, out num, out vec2, out Vector3 dir, out quat);
            if(vecType != VecType.Vector3)
            {
                LogToCode(context, "A Vector3 is needed for dir", CodeLogType.Error);
                return Intrinsic.Result.False;
            }

            Ray ray = new Ray(origin, dir);
            bool didHit = Physics.Raycast(ray, out RaycastHit hit, 10000, GLLayers.AllCanRaycast);
            if (!didHit)
                return Intrinsic.Result.False;

            ValMap map = ValMap.Create();
            map[ValString.positionStr] = new ValVector3(hit.point);

            // Handle if we hit a scene object
            SceneObject sceneObject = hit.transform.GetSceneObjectFromTransform();
            if (sceneObject != null)
                map["object"] = new ValSceneObject(sceneObject);

            return new Intrinsic.Result(map);
		};
        // Override print to include line number
        intrinsic = Intrinsic.Create("print");
        intrinsic.AddParam("s", ValString.empty);
        intrinsic.code = (context, partialResult) => {
            Value s = context.GetVar("s");
            LogToCode(context, s != null ? s.ToString() : "", CodeLogType.Log);
            return Intrinsic.Result.Null;
		};
        intrinsic = Intrinsic.Create("SyncRun");
        intrinsic.AddParam("function", ValString.empty);
        intrinsic.AddParam("target", ValString.Create("all"));
        intrinsic.AddParam("value", ValNull.instance);
        intrinsic.AddParam("reliable", ValNumber.zero);
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Run a function over the network. Pass the function's name, who you want want to run the function (\"all\",\"others\",\"host\",userID), and the value (if any) that you want to send. You can add \"_buffered\" to have the function run on new clients as they join", "didRun"));
        intrinsic.code = (context, partialResult) => {
            Value functionToCall = context.GetVar("function");
            if(functionToCall == null)
            {
                LogToCode(context, "No function provided", CodeLogType.Error);
                return Intrinsic.Result.False;
            }
            ValString funcStr = functionToCall as ValString;
            if(funcStr == null)
            {
                LogToCode(context, "SyncRun needs a function string", CodeLogType.Error);
                return Intrinsic.Result.False;
            }
            if(funcStr.value.Length > 255)
            {
                LogToCode(context, "Function needs a length smaller than 255", CodeLogType.Error);
                return Intrinsic.Result.False;
            }

            Value val = context.GetVar("value");
            Value target = context.GetVar("target");
            ValNumber reliable = context.GetVar("reliable") as ValNumber;
            ValString targetStr = target as ValString;
            byte msgTag;
            bool hasNullValue = (val == null || val == ValNull.instance);
            if(targetStr == null)
            {
                ValNumber targetNum = target as ValNumber;
                if(targetNum == null)
                {
                    LogToCode(context, "No target provided", CodeLogType.Error);
                    return Intrinsic.Result.False;
                }
                msgTag = hasNullValue ? ServerTags.RPC_TargetUser_NonBuffered : ServerTags.RPC_TargetUser_NonBuffered_Value;
            }
            else
            {
                if(targetStr.value == "all")
                    msgTag = hasNullValue ? ServerTags.RPC_All_NonBuffered : ServerTags.RPC_All_NonBuffered_Value;
                else if(targetStr.value == "others")
                    msgTag = hasNullValue ? ServerTags.RPC_Others_NonBuffered : ServerTags.RPC_Others_NonBuffered_Value;
                else if(targetStr.value == "host")
                    msgTag = hasNullValue ? ServerTags.RPC_Host_NonBuffered : ServerTags.RPC_Host_NonBuffered_Value;
                else if(targetStr.value == "all_buffered")
                    msgTag = hasNullValue ? ServerTags.RPC_All_Buffered : ServerTags.RPC_All_Buffered_Value;
                else if(targetStr.value == "others_buffered")
                    msgTag = hasNullValue ? ServerTags.RPC_Others_Buffered : ServerTags.RPC_Others_Buffered_Value;
                else if(targetStr.value == "host_buffered")
                    msgTag = hasNullValue ? ServerTags.RPC_Host_Buffered : ServerTags.RPC_Host_Buffered_Value;
                else
                {
                    LogToCode(context, "Unknown SyncRun target " + targetStr.value, CodeLogType.Error);
                    return Intrinsic.Result.False;
                }
            }

            bool useReliable = reliable != null ? reliable.BoolValue() : false;
            SceneObject sceneObject = GetSceneObjectFromContext(context);
            UserScriptBehavior userScript = GetScriptFromContext(context);

            if(userScript.WhoRuns != DRUserScript.WhoRuns.Everyone)
            {
                LogToCode(context, "Sending RPC, but script is set to " + userScript.WhoRuns + " runs! This may cause excessive network usage", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            //Debug.Log("Sending RPC " + funcStr.value + " tag " + msgTag + " object " + sceneObject.GetObjectID() + " script " + userScript.GetScriptID());

            int msgLen = 2 * sizeof(ushort) + 1 + funcStr.value.Length;
            if (hasNullValue)
                msgLen += 1 + MiniscriptSerializer.ApproximateSizeForValue(val);

            using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
            {
                writer.Write(sceneObject.GetID());
                writer.Write(userScript.GetScriptID());
                DRCompat.WriteStringSmallerThen255(writer, funcStr.value);
                // Serialize the value
                if (!hasNullValue)
                    MiniscriptSerializer.Serialize(writer, val);
                using (Message msg = Message.Create(msgTag, writer))
                {
                    if(useReliable)
                        DarkRiftConnection.Instance.SendReliableMessage(msg);
                    else
                        DarkRiftConnection.Instance.SendUnreliableMessage(msg);//TODO this should be merged with other messages!!!
                }
            }

            return new Intrinsic.Result(ValNumber.one);
		};
        // dot
        intrinsic = Intrinsic.Create("dot");
        intrinsic.AddParam("lhs");
        intrinsic.AddParam("rhs");
        intrinsic.code = (context, partialResult) => {
            Value lhsVal = context.GetVar("lhs");
            Value rhsVal = context.GetVar("rhs");
            if (lhsVal == null || rhsVal == null)
                return Intrinsic.Result.False;

            double lhsNum;
            Vector2 lhsVec2;
            Vector3 lhsVec3;
            Quaternion lhsRot;
            VecType lhsType = ParseVecInput(lhsVal, out lhsNum, out lhsVec2, out lhsVec3, out lhsRot);
            if(lhsType != VecType.Vector3
                && lhsType != VecType.Quaternion)
            {
                LogToCode(context, "Dot requires a Vector2,Vector3, or Quaternion. Recv a " + lhsType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            double rhsNum;
            Vector2 rhsVec2;
            Vector3 rhsVec3;
            Quaternion rhsRot;
            VecType rhsType = ParseVecInput(rhsVal, out rhsNum, out rhsVec2, out rhsVec3, out rhsRot);
            if(rhsType != lhsType)
            {
                LogToCode(context, "Dot requires both params have the same type Recv " + lhsType + ", " + rhsType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            if(lhsType == VecType.Vector3)
                return new Intrinsic.Result(ValNumber.Create(Vector3.Dot(lhsVec3, rhsVec3)));
            else if(lhsType == VecType.Quaternion)
                return new Intrinsic.Result(ValNumber.Create(Quaternion.Dot(lhsRot, rhsRot)));
            return Intrinsic.Result.False;
		};
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Get the dot product between two vectors", "res"));

        // cross
        intrinsic = Intrinsic.Create("cross");
        intrinsic.AddParam("lhs");
        intrinsic.AddParam("rhs");
        intrinsic.code = (context, partialResult) => {
            Value lhsVal = context.GetVar("lhs");
            Value rhsVal = context.GetVar("rhs");
            if (lhsVal == null || rhsVal == null)
                return Intrinsic.Result.False;

            double lhsNum;
            Vector2 lhsVec2;
            Vector3 lhsVec3;
            Quaternion lhsRot;
            VecType lhsType = ParseVecInput(lhsVal, out lhsNum, out lhsVec2, out lhsVec3, out lhsRot);
            if(lhsType != VecType.Vector3)
                return Intrinsic.Result.False;
            double rhsNum;
            Vector2 rhsVec2;
            Vector3 rhsVec3;
            Quaternion rhsRot;
            VecType rhsType = ParseVecInput(rhsVal, out rhsNum, out rhsVec2, out rhsVec3, out rhsRot);
            if(rhsType != VecType.Vector3)
                return Intrinsic.Result.False;

            return new Intrinsic.Result(new ValVector3(Vector3.Cross(lhsVec3, rhsVec3)));
		};
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Multiply two numbers/vectors/rotations", "res"));

        // angle
        intrinsic = Intrinsic.Create("angle");
        intrinsic.AddParam("lhs");
        intrinsic.AddParam("rhs");
        intrinsic.code = (context, partialResult) => {
            Value lhsVal = context.GetVar("lhs");
            Value rhsVal = context.GetVar("rhs");
            if (lhsVal == null || rhsVal == null)
            {
                LogToCode(context, "Angle requires a Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            double lhsNum;
            Vector2 lhsVec2;
            Vector3 lhsVec3;
            Quaternion lhsRot;
            VecType lhsType = ParseVecInput(lhsVal, out lhsNum, out lhsVec2, out lhsVec3, out lhsRot);
            if(lhsType != VecType.Vector2
                && lhsType != VecType.Vector3
                && lhsType != VecType.Quaternion)
            {
                LogToCode(context, "Angle requires a Vector2,Vector3, or Quaternion. Recv " + lhsType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            double rhsNum;
            Vector2 rhsVec2;
            Vector3 rhsVec3;
            Quaternion rhsRot;
            VecType rhsType = ParseVecInput(rhsVal, out rhsNum, out rhsVec2, out rhsVec3, out rhsRot);
            if(rhsType != lhsType)
            {
                LogToCode(context, "Angle requires both parameters be of the same type. lhs: " + lhsType + " rhs: " + rhsType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            float angle;
            if (lhsType == VecType.Vector2)
                angle = Vector2.Angle(lhsVec2, rhsVec2);
            else if (lhsType == VecType.Vector3)
                angle = Vector3.Angle(lhsVec3, rhsVec3);
            else if (lhsType == VecType.Quaternion)
                angle = Quaternion.Angle(lhsRot, rhsRot);
            else
                angle = -666f;

            return new Intrinsic.Result(ValNumber.Create(angle));
		};
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Returns the angle between two vectors or rotations", "angle_deg"));

        // abs
        intrinsic = Intrinsic.Create("abs");
        intrinsic.AddParam("val");
        intrinsic.code = (context, partialResult) => {
            Value val = context.GetVar("val");
            if (val == null)
            {
                LogToCode(context, "val requires a number,Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            VecType vecType = ParseVecInput(val, out double num, out Vector2 vec2, out Vector3 vec3, out Quaternion rot);
            if(vecType == VecType.None)
            {
                LogToCode(context, "Abs requires a Number,Vector2,Vector3, or Quaternion. Recv " + vecType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            if (vecType == VecType.Double)
                return new Intrinsic.Result(ValNumber.Create(System.Math.Abs(num)));
            else if (vecType == VecType.Vector2)
                return Intrinsic.Result.Null;//TODO
            else if (vecType == VecType.Vector3)
                return new Intrinsic.Result(new ValVector3(new Vector3(Mathf.Abs(vec3.x), Mathf.Abs(vec3.y), Mathf.Abs(vec3.z))));
            else if (vecType == VecType.Quaternion)
            {
                Vector3 euler = rot.eulerAngles;
                euler = new Vector3(Mathf.Abs(vec3.x), Mathf.Abs(vec3.y), Mathf.Abs(vec3.z));
                return new Intrinsic.Result(new ValQuaternion(Quaternion.Euler(euler)));
            }

            return Intrinsic.Result.Null;
		};

        // clamp
        intrinsic = Intrinsic.Create("clamp");
        intrinsic.AddParam("val");
        intrinsic.AddParam("min");
        intrinsic.AddParam("max");
        intrinsic.code = (context, partialResult) => {
            ValNumber val = context.GetVar("val") as ValNumber;
            if (val == null)
            {
                LogToCode(context, "clamp requires a number,Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            ValNumber minVal = context.GetVar("min") as ValNumber;
            if (minVal == null)
            {
                LogToCode(context, "clamp requires a number", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            ValNumber maxVal = context.GetVar("max") as ValNumber;
            if (maxVal == null)
            {
                LogToCode(context, "clamp requires a number", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            if (val.value <= minVal.value)
                return new Intrinsic.Result(minVal.value);
            else if (val.value >= maxVal.value)
                return new Intrinsic.Result(maxVal.value);
            return new Intrinsic.Result(val.value);
		};

        // lerp
        intrinsic = Intrinsic.Create("lerp");
        // Lerp from x to y by t
        intrinsic.AddParam("x");
        intrinsic.AddParam("y");
        intrinsic.AddParam("t");
        intrinsic.AddParam("lerpType", "linear");
        intrinsic.code = (context, partialResult) => {
            Value xVal = context.GetVar("x");
            if (xVal == null)
            {
                LogToCode(context, "lerp requires a number,Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            Value yVal = context.GetVar("y");
            if (yVal == null)
            {
                LogToCode(context, "val requires a number,Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            Value tVal = context.GetVar("t");
            if (tVal == null)
            {
                LogToCode(context, "val requires a number,Vector2,Vector3, or Quaternion. Recv a null param", CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            VecType xVecType = ParseVecInput(xVal, out double xNum, out Vector2 xVec2, out Vector3 xVec3, out Quaternion xRot);
            if(xVecType == VecType.None)
            {
                LogToCode(context, "lerp requires a Number,Vector2,Vector3, or Quaternion for X. Recv " + xVecType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }
            VecType yVecType = ParseVecInput(yVal, out double yNum, out Vector2 yVec2, out Vector3 yVec3, out Quaternion yRot);
            if(xVecType != yVecType)
            {
                LogToCode(context, "lerp requires X and Y be of the same type. Recv " + xVecType + ", " + yVecType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            VecType tVecType = ParseVecInput(tVal, out double tNum, out Vector2 tVec2, out Vector3 tVec3, out Quaternion tRot);
            if(tVecType != VecType.Double
                && !(xVecType == VecType.Vector2 && tVecType == VecType.Vector2)
                && !(xVecType == VecType.Vector3 && tVecType == VecType.Vector3))
            {
                LogToCode(context, "lerp requires a t that's either a number, or a vector2/vector3 when both X and Y the same type. T type " + tVecType, CodeLogType.Warning);
                return Intrinsic.Result.False;
            }

            string lerpType = null;
            ValString lerpTypeVal = context.GetVar("lerpType") as ValString;
            if (lerpTypeVal != null)
                lerpType = lerpTypeVal.value;

            if(string.IsNullOrEmpty(lerpType) || lerpType == "linear")
            {
                if (xVecType == VecType.Double)
                    return new Intrinsic.Result(ValNumber.Create(Mathf.Lerp((float)xNum, (float)yNum, (float)tNum)));
                else if (xVecType == VecType.Vector2)
                    return Intrinsic.Result.Null;//TODO
                else if (xVecType == VecType.Vector3)
                {
                    float x, y, z;
                    if(tVecType == VecType.Double)
                    {
                        x = (float)tNum;
                        y = x;
                        z = x;
                    }
                    else
                    {
                        x = tVec3.x;
                        y = tVec3.y;
                        z = tVec3.z;
                    }
                    return new Intrinsic.Result(new ValVector3(new Vector3(
                        Mathf.Lerp(xVec3.x, yVec3.x, x),
                        Mathf.Lerp(xVec3.y, yVec3.y, y),
                        Mathf.Lerp(xVec3.z, yVec3.z, z)
                        )));
                }
                else if (xVecType == VecType.Quaternion)
                {
                    float x, y, z;
                    if(tVecType == VecType.Double)
                    {
                        x = (float)tNum;
                        y = x;
                        z = x;
                    }
                    else
                    {
                        x = tVec3.x;
                        y = tVec3.y;
                        z = tVec3.z;
                    }
                    Vector3 xEuler = xRot.eulerAngles;
                    Vector3 yEuler = yRot.eulerAngles;
                    Vector3 lerpEuler = new Vector3(
                        Mathf.Lerp(xEuler.x, yEuler.x, x),
                        Mathf.Lerp(xEuler.y, yEuler.y, y),
                        Mathf.Lerp(xEuler.z, yEuler.z, z)
                        );
                    return new Intrinsic.Result(new ValQuaternion(Quaternion.Euler(lerpEuler)));
                }
            }
            else if(lerpType == "cos")
            {
                // TODO

            }
            LogToCode(context, "Unknown lerp type " + lerpType, CodeLogType.Warning);
            return Intrinsic.Result.False;
		};

        intrinsic = Intrinsic.Create("GetKeyInput");
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Get the text that the user entered during the last frame", "str"));
        intrinsic.code = (context, partialResult) => {
            return new Intrinsic.Result(ValString.Create(Input.inputString));
		};
    }
    public static VecType ParseVecInput(Value val, out double numOut, out Vector2 vec2Out, out Vector3 vec3Out, out Quaternion rotOut)
    {
        numOut = 0.0;
        vec2Out = Vector2.zero;
        vec3Out = Vector3.zero;
        rotOut = Quaternion.identity;

        // First try the base types
        ValNumber valNum = val as ValNumber;
        if(valNum != null)
        {
            numOut = valNum.value;
            return VecType.Double;
        }
        //TODO vector2
        ValVector3 valVec3 = val as ValVector3;
        if(valVec3 != null)
        {
            vec3Out = valVec3.Vector3;
            return VecType.Vector3;
        }
        ValQuaternion valQuat = val as ValQuaternion;
        if(valQuat != null)
        {
            rotOut = valQuat.Quaternion;
            return VecType.Quaternion;
        }
        // Then try parsing from a map
        ValMap valMap = val as ValMap;
        if (valMap == null)
            return VecType.None;

        Value dimVal;
        ValNumber dimValNum;
        // X
        if (!valMap.TryGetValue(ValString.xStr, out dimVal))
            return VecType.None;
        dimValNum = dimVal as ValNumber;
        if(dimValNum == null)
            return VecType.None;
        float x = (float)dimValNum.value;
        // Y
        if (!valMap.TryGetValue(ValString.yStr, out dimVal))
            return VecType.None;
        dimValNum = dimVal as ValNumber;
        if(dimValNum == null)
            return VecType.None;
        float y = (float)dimValNum.value;
        // Z
        if (!valMap.TryGetValue(ValString.zStr, out dimVal))
        {
            vec2Out = new Vector2(x, y);
            return VecType.Vector2;
        }
        dimValNum = dimVal as ValNumber;
        if(dimValNum == null)
        {
            vec2Out = new Vector2(x, y);
            return VecType.Vector2;
        }
        float z = (float)dimValNum.value;
        // W
        if (!valMap.TryGetValue(ValString.wStr, out dimVal))
        {
            vec3Out = new Vector3(x, y, z);
            return VecType.Vector3;
        }
        dimValNum = dimVal as ValNumber;
        if(dimValNum == null)
        {
            vec3Out = new Vector3(x, y, z);
            return VecType.Vector3;
        }
        float w = (float)dimValNum.value;

        rotOut = new Quaternion(x, y, z, w);
        return VecType.Quaternion;
    }
    public static bool ParseVector3Input(Context ctx, out Vector3 vec3Out)
    {
        // First try the base types
        Value valX = ctx.GetVar(ValString.xStr);
        ValVector3 valVec = valX as ValVector3;
        if(valVec != null)
        {
            vec3Out = valVec.Vector3;
            return true;
        }
        ValNumber valXNum = valX as ValNumber;
        if(valXNum == null)
        {
            vec3Out = Vector3.zero;
            return false;
        }
        Value valY = ctx.GetVar(ValString.yStr);
        ValNumber valYNum = valY as ValNumber;
        if(valYNum == null)
        {
            vec3Out = Vector3.zero;
            return false;
        }
        Value valZ = ctx.GetVar(ValString.zStr);
        ValNumber valZNum = valZ as ValNumber;
        if(valZNum == null)
        {
            vec3Out = Vector3.zero;
            return false;
        }
        vec3Out = new Vector3((float)valXNum.value, (float)valYNum.value, (float)valZNum.value);
        return true;
    }
    public static VecType ParseVecInput(Value valX, Value valY, Value valZ, Value valW, out double numOut, out Vector2 vec2Out, out Vector3 vec3Out, out Quaternion rotOut)
    {
        // Parse valX, which can be any val type
        VecType xType = ParseVecInput(valX, out numOut, out vec2Out, out vec3Out, out rotOut);
        // If this is a non-number type, then we're good
        if (xType != VecType.Double)
            return xType;
        // Otherwise, we now need to parse the rest
        vec2Out = Vector2.zero;
        vec3Out = Vector3.zero;
        rotOut = Quaternion.identity;

        ValNumber numY = valY as ValNumber;
        if (numY == null)
            return xType;
        float x = (float)numOut;
        float y = (float)numY.value;
        ValNumber numZ = valZ as ValNumber;
        if (numZ == null)
        {
            vec2Out = new Vector2(x, y);
            return VecType.Vector2;
        }
        float z = (float)numZ.value;
        ValNumber numW = valW as ValNumber;
        if (numW == null)
        {
            vec3Out = new Vector3(x, y, z);
            return VecType.Vector3;
        }
        float w = (float)numW.value;
        rotOut = new Quaternion(x, y, z, w);
        return VecType.Quaternion;
    }
    public static bool ParseTruth(Value val)
    {
        if (val == null)
            return false;
        return val.BoolValue();
    }
    public static bool ParsePositionRotation(Value posVal, Value rotVal, out Vector3 position, out Quaternion rotation)
    {
        double d;
        Vector2 vec2;
        Vector3 junkPos;
        Quaternion junkRot;
        var posType = ParseVecInput(posVal, out d, out vec2, out position, out junkRot);
        if (posType != VecType.Vector3)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }
        var rotType = ParseVecInput(rotVal, out d, out vec2, out junkPos, out rotation);
        if (rotType == VecType.Vector3)
        {
            rotation = Quaternion.Euler(junkPos);
        }
        else if (rotType != VecType.Quaternion)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }
        return true;
    }
    public static bool ParseColorInput(Value val, out Color colorOut)
    {
        ValString valStr = val as ValString;
        if(valStr == null)
        {
            colorOut = Color.red;
            return false;
        }
        switch (valStr.value)
        {
            case "cyan":
                colorOut = Color.cyan;
                return true;
            case "grey":
            case "gray":
                colorOut = Color.gray;
                return true;
            case "magenta":
                colorOut = Color.magenta;
                return true;
            case "red":
                colorOut = Color.red;
                return true;
            case "yellow":
                colorOut = Color.yellow;
                return true;
            case "black":
                colorOut = Color.black;
                return true;
            case "white":
                colorOut = Color.white;
                return true;
            case "green":
                colorOut = Color.green;
                return true;
            case "blue":
                colorOut = Color.blue;
                return true;
        }
        // See if this is a HTML color code
        // like eb0000
        if (valStr.value.Length == 6
            || valStr.value.Length == 8)
            return ColorUtility.TryParseHtmlString(valStr.value, out colorOut);

        colorOut = Color.red;
        return false;
    }
    public static ValMap IntoMap(Vector3 pos, Quaternion rot)
    {
        ValMap valMap = ValMap.Create();
        valMap.SetElem(ValString.positionStr, new ValVector3(pos));
        valMap.SetElem(ValString.rotationStr, new ValQuaternion(rot));
        return valMap;
    }
    public static ValMap IntoMap(Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angularVelocity)
    {
        ValMap valMap = ValMap.Create();
        valMap.SetElem(ValString.positionStr, new ValVector3(pos));
        valMap.SetElem(ValString.rotationStr, new ValQuaternion(rot));
        valMap.SetElem(ValString.velocityStr, new ValVector3(velocity));
        valMap.SetElem(ValString.angularVelocityStr, new ValVector3(angularVelocity));
        return valMap;
    }
    public void GL_FixedUpdate(float dt)
    {
        // Fire the FixedUpdate event
        _areWeRunningScripts = true;
        for (int i = 0; i < _allScriptInstances.Count; i++)
        {
            // Drop this if we're destroying this script
            UserScriptBehavior scriptBehavior = _allScriptInstances[i];
            if (!scriptBehavior.CanRun)
                continue;
            scriptBehavior.InvokeEvent(OnFixedUpdateEventName);
            scriptBehavior.FireEventsAndRun(true);
        }

        // Remove all the behaviors that we were waiting to remove
        for(int i = _behaviorsPendingRemoval.Count - 1; i >= 0; i--)
        {
            //Debug.Log("Rem " + _behaviorsPendingRemoval[i]);
            _allScriptInstances.RemoveBySwap(_behaviorsPendingRemoval[i]);
        }
        _behaviorsPendingRemoval.Clear();
        _areWeRunningScripts = false;
    }
    private void Update()
    {
        // If we're paused, then we don't want to run anything
        if (!TimeManager.Instance.IsPlayingOrStepped)
            return;
        // TODO handle if code is selected
        string input = Input.inputString;
        //TODO we should probably run these at multiple times
        // and the C# behaviors should probably always run before the
        // miniscript behaviors
        //if(_allScriptInstances.Count > 0)
            //Debug.Log("Num script instances " + _allScriptInstances.Count);
        _areWeRunningScripts = true;
        for (int i = 0; i < _allScriptInstances.Count; i++)
        {
            // Drop this if we're destroying this script
            UserScriptBehavior scriptBehavior = _allScriptInstances[i];
            if (!scriptBehavior.CanRun)
                continue;
            if(!string.IsNullOrEmpty(input))
                scriptBehavior.InvokeEvent(OnKeyInputEventName, ValString.Create(input));
            scriptBehavior.InvokeEvent(OnUpdateEventName);
            scriptBehavior.FireEventsAndRun(false);
        }

        // Remove all the behaviors that we were waiting to remove
        for(int i = _behaviorsPendingRemoval.Count - 1; i >= 0; i--)
        {
            //Debug.Log("Rem " + _behaviorsPendingRemoval[i]);
            _allScriptInstances.RemoveBySwap(_behaviorsPendingRemoval[i]);
        }
        _behaviorsPendingRemoval.Clear();
        _areWeRunningScripts = false;
    }
}
