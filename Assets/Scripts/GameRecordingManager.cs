using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

/// <summary>
/// Manager for the recording and playback of game recordings
/// </summary>
public class GameRecordingManager : GenericSingleton<GameRecordingManager>
{
    public enum RecordingState
    {
        None,
        Recording,
        PlayingRecording,
        ServerUserPlayingRecording
    }
    public PlayRecordingMenu PlayRecordingMenu;
    public CreateRecordingDialog CreateRecordingMenu;
    public static readonly string RecordingFileExtension = ".gls";
    public static readonly string[] RecordingFileExtensions = new string[] { "gls" };
    public static readonly string GamelodgeRecordingFoldername = "Gamelodge Recordings";
    // The current ID of the recorded user who made the recording
    // This is the currentID, not their ID when they made the recording
    public ushort RecordedClientID { get {
            if (CurrentState != RecordingState.PlayingRecording)
                Debug.LogError("No client ID, not recording");
            return _recordedClientID;
        } }

    public string RecordingFolderPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_recordingFolderPath))
                return _recordingFolderPath;
            _recordingFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + GamelodgeRecordingFoldername;
            return _recordingFolderPath;
        }
    }
    public RecordingState CurrentState { get; private set; }
    private string _recordingFolderPath;
    private bool _hasMadeRecordingFolder;
    private DRStartFileMarker _initialFileMarker = new DRStartFileMarker();
    private DRFileMarker _nextFileMarker = new DRFileMarker();
    // Recording stuff
    private Thread _mainThread;
    private BinaryWriter _binaryWriter;
    private readonly object _fileWriterLock = new object();
    private float _lastRenderTime;
    private float _playbackTime2RecordedTime;
    // Playback stuff
    private DarkRiftReader _drReader;
    private BinaryReader _binaryReader;
    private bool _hasNextFileMarker;
    private ushort _recordedClientID;
    private List<DRUser> _recordedUsersPendingAdd;
    /// <summary>
    /// If we're waiting to play a recording b/c
    /// Orchestrator is loading assets
    /// </summary>
    private bool _isPendingOrchestratorLoad = false;
    // The mapping from the UserID that was present when the recording was made
    // to the mapping that is present during the playback
    // empty when not in PlayingRecording mode
    private readonly Dictionary<ushort, ushort> _recordedUserIDs2RuntimeUserID = new Dictionary<ushort, ushort>();
    /// <summary>
    /// A mapping from the recording object ID to the actual object
    /// </summary>
    private readonly Dictionary<ushort, DRObject> _recordedIDs2Objects = new Dictionary<ushort, DRObject>();

    protected override void Awake()
    {
        base.Awake();
        _mainThread = Thread.CurrentThread;
    }

    public void BeginRecording()
    {
        if(CurrentState != RecordingState.None)
        {
            Debug.LogError("Can't begin recording when we're in state " + CurrentState);
            // TODO we should be able to directly switch from one recording to another,
            // but note that when you do, that the WillRestart flag in EndRecording should
            // be on
            return;
        }
        if (_isPendingOrchestratorLoad)
        {
            Debug.LogError("Can't begin recording, we're waiting on orchestrator to load");
            return;
        }

        string filepath = CreateRecordingMenu.GetRecordingFilename(out bool isFullPath);

        // Setup the recording folder
        if (!_hasMadeRecordingFolder)
        {
            Debug.Log("Will use recording folder " + RecordingFolderPath);
            if (!Directory.Exists(RecordingFolderPath))
            {
                Debug.Log("Recording path will need to be created");
                Directory.CreateDirectory(RecordingFolderPath);
            }
            _hasMadeRecordingFolder = true;
        }
        if (!isFullPath)
        {
            filepath = RecordingFolderPath + Path.DirectorySeparatorChar + filepath;
            Debug.Log("Filename now " + filepath);
        }

        // Get just the filename
        int lastPathDirLen = filepath.LastIndexOf(Path.DirectorySeparatorChar) + 1;
        string filename = filepath.Substring(lastPathDirLen, filepath.Length - lastPathDirLen);
        PlayRecordingMenu.SetRecordingFilename(filename);
        _binaryWriter = new BinaryWriter(File.Open(filepath, FileMode.Create));

        // Write the timestamp to a file
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            _initialFileMarker.Update(DarkRiftConnection.Instance.OurID, TimeManager.Instance.RenderTime);
            writer.Write(_initialFileMarker);
            byte[] rawFileMarker = writer.GetRawBackingArray(out int markerLen);
            _binaryWriter.Write(rawFileMarker, 0, markerLen);
        }
        // Write the serialized game state to a file
        DRGameState gameState = DarkRiftConnection.Instance.GetFullCurrentGameState();
        int gameStateLen = 512;//TODO
        using (DarkRiftWriter writer = DarkRiftWriter.Create(gameStateLen))
        {
            writer.Write(gameState);
            byte[] rawGameState = writer.GetRawBackingArray(out int rawGameStateLen);
            _binaryWriter.Write(rawGameState, 0, rawGameStateLen);
        }
        // Write the current users to a list
        var allUsers = UserManager.Instance.GetAllUsers();
        // It'd be better for perf if we directly serialize
        // but I felt that it's better for future compatibility
        // to keep it in a DR object. Plus, # users in a game record
        // will be small
        DRUserList userList = new DRUserList(allUsers.Count);
        for(int i = 0; i < allUsers.Count; i++)
        {
            DRUser user = allUsers[i].DRUserObj;
            // Only store if this is a real player
            if (user.TypeOfUser == DRUser.UserType.Recorded)
            {
                Debug.LogWarning("Skipping user #" + user.ID + " from recording");
                continue;
            }
            userList.AddUser(user);
        }
        int drUserListLen = 128;//TODO
        using (DarkRiftWriter writer = DarkRiftWriter.Create(drUserListLen))
        {
            writer.Write(userList);
            byte[] rawUserList = writer.GetRawBackingArray(out int rawUserListLen);
            _binaryWriter.Write(rawUserList, 0, rawUserListLen);
        }

        CurrentState = RecordingState.Recording;
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.StartRecording();
    }
    public void OnSentMessage(Message message, SendMode sendMode)
    {
        RecordMessage(message, sendMode, MessageDirection.Client2Server);
    }
    public void OnRecvMessage(Message message, SendMode sendMode)
    {
        RecordMessage(message, sendMode, MessageDirection.Server2Client);
    }
    private void RecordMessage(Message message, SendMode sendMode, MessageDirection messageDirection)
    {
        if (CurrentState != RecordingState.Recording)
            return;
        if (message.Tag == ServerTags.PingPong)
            return;

        lock (_fileWriterLock)
        {
            if(_binaryWriter == null)
            {
                Debug.LogError("Recording, but no binary writer!");
                return;
            }

            // We can't get the unscaled time from separate
            // threads
            float time;
            if (Thread.CurrentThread == _mainThread)
                time = TimeManager.Instance.RenderTime;
            else
                time = _lastRenderTime;


            // Update and serialize the file marker
            _nextFileMarker.Update(time, message.DataLength, sendMode, messageDirection);
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(_nextFileMarker);
                // We write the tag here b/c we can't use binaryWriter, as it's LE for who-knows-why
                writer.Write(message.Tag);
                byte[] markerSerialized = writer.GetRawBackingArray(out int serializedMarkerLen);
                _binaryWriter.Write(markerSerialized, 0, serializedMarkerLen);
            }
            // Serialize the message
            byte[] rawData = message.GetRawBackingArray(out int position, out int len);
            _binaryWriter.Write(rawData, position, len);
            //Debug.Log("Saving mess #" + message.Tag + " len " + message.DataLength + " pos " + position + " len " + len);
        }
    }
    public void EndRecording()
    {
        if(CurrentState != RecordingState.Recording)
        {
            Debug.LogError("Can't end recording when we're in state " + CurrentState);
            return;
        }
        if (_isPendingOrchestratorLoad)
        {
            Debug.LogError("Can't end recording, we're waiting on orchestrator to load");
            return;
        }
        _binaryWriter.Close();
        _binaryWriter.Dispose();
        _binaryWriter = null;

        CurrentState = RecordingState.None;
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.EndRecording();
    }
    public void OnStartStopRecordingClicked()
    {
        if (CurrentState == RecordingState.None)
            BeginRecording();
        else
            EndRecording();
    }
    public void PlayRecording()
    {
        if(CurrentState != RecordingState.None)
        {
            Debug.LogError("Can't play recording when we're in state " + CurrentState);
            return;
        }
        if (_isPendingOrchestratorLoad)
        {
            Debug.LogError("Can't play recording, we're waiting on orchestrator to load");
            return;
        }

        string filepath = PlayRecordingMenu.GetRecordingFilename(out bool isFullPath);
        if (string.IsNullOrEmpty(filepath))
        {
            Debug.Log("Not playing recording, nothing selected");
            return;
        }
        if (!isFullPath)
        {
            filepath = RecordingFolderPath + Path.DirectorySeparatorChar + filepath;
            Debug.Log("Play recording Filename now " + filepath);
        }
        //Debug.Log("Opening \"" + filepath + "\"");
        // Open the file
        FileStream fileStream = File.Open(filepath, FileMode.Open);
        _binaryReader = new BinaryReader(fileStream);
        Debug.Log("Play recording file opened");
        //TODO add a DarkRiftReader based off of Stream, to avoid this unweildy interaction
        byte[] allBytes =  _binaryReader.ReadBytes((int)fileStream.Length);
        if (_drReader != null)
            Debug.LogError("Leaking DRReader in play recording");
        _drReader = DarkRiftReader.CreateFromArray(allBytes, 0, allBytes.Length);

        // Read the initial file marker and the game state
        _drReader.ReadSerializableInto(ref _initialFileMarker);
        Debug.Log("Initial timestamp is " + _initialFileMarker.Timestamp);
        ushort previousUserID = _initialFileMarker.RecordingUserID;
        int posA = _drReader.Position;
        DRGameState gameState = _drReader.ReadSerializable<DRGameState>();
        Debug.Log("Loaded game state version #" + gameState.Version);
        DRUserList userList = DRUserList.DeserializeWithVersion(_drReader, gameState.Version);
        int initialMessageLen = _drReader.Position - posA;
        // Try to preroll the first file marker, we need this in order
        // to play the message with correct sync
        if(_drReader.Position < _drReader.Length)
        {
            _drReader.ReadSerializableInto(ref _nextFileMarker);
            _hasNextFileMarker = true;
        }
        else
        {
            _hasNextFileMarker = false;
            Debug.LogWarning("Recorded file with only a header?");
        }
        if(_recordedUsersPendingAdd != null)
            Debug.LogError("Recorded users pending add not cleared");
        // Set all the users to be recorded
        _recordedUsersPendingAdd = userList.Users;
        // To get new IDs in a safe way, we start allocating IDs from the a large
        // number, and double check that the ID is unused. The server should tell
        // us to retry if an ID is used by a player
        ushort nextIDToAllocate = (ushort)(UserManager.Instance.LargestReceivedID + 32);
        if (Application.isEditor && _recordedUserIDs2RuntimeUserID.Count != 0)
            Debug.LogError("Leaking recorded user IDs!");
        for(int i = 0; i < _recordedUsersPendingAdd.Count; i++)
        {
            DRUser user = _recordedUsersPendingAdd[i];
            user.TypeOfUser = DRUser.UserType.Recorded;
            // Check that the ID isn't currently in use
            while (UserManager.Instance.HasUser(nextIDToAllocate))
                nextIDToAllocate++;
            ushort newID = nextIDToAllocate++;
            Debug.Log("player ID #" + user.ID + "->" + newID);
            _recordedUserIDs2RuntimeUserID.Add(user.ID, newID);
            user.ID = newID;
        }
        uint newOwnershipTime = DarkRiftPingTime.Instance.ServerTime;
        // We need to correct the owner for all objects to use the new
        // user ID.
        var allObjs = gameState.GetAllObjects();
        foreach(var kvp in allObjs)
        {
            DRObject obj = kvp.Value;
            // Keep track of the recordedID, so that we can
            // know which runtime object is being referenced
            // by a recordedID
            _recordedIDs2Objects.Add(obj.GetID(), obj);

            if (obj.OwnerID != ushort.MaxValue)
            {
                if (_recordedUserIDs2RuntimeUserID.TryGetValue(kvp.Value.OwnerID, out ushort newOwnerID))
                    obj.OwnerID = newOwnerID;
                else
                {
                    // This will happen when there was a user who left before recording began
                    obj.OwnerID = ushort.MaxValue;
                }
            }
            if (obj.GrabbedBy != ushort.MaxValue)
            {
                if (_recordedUserIDs2RuntimeUserID.TryGetValue(obj.GrabbedBy, out ushort newGrabbedBy))
                    obj.GrabbedBy = newGrabbedBy;
                else
                {
                    Debug.LogError("Unknown grabbed by user #" + obj.GrabbedBy + " for obj #" + obj.GetID());
                    obj.GrabbedBy = ushort.MaxValue;
                }
            }
            // TODO properly correct the ownership time.
            // we should figure out what the server time was
            // during the recording, and shift all times accordingly
            // this way ownership comparisons can still be correct
            // and they're still done relative to the current server time
            // TODO we also need to update the ownership times for all
            // recorded messages
            obj.OwnershipTime = newOwnershipTime;

            // TODO update the references to SceneObjects within SerializedBehavior
        }
        _recordedClientID = _recordedUserIDs2RuntimeUserID[previousUserID];
        Debug.Log("Recorder ID #" + _recordedClientID + " from " + previousUserID);

        // If we're going to keep script changes, then we need to update the
        // scripts in the game state
        if (PlayRecordingMenu.GetShouldKeepScriptChanges())
        {
            List<DRUserScript> localScripts = UserScriptManager.Instance.GetAllUserScripts();
            List<DRUserScript> allRecordedScripts = gameState.GetAllUserScripts();
            for(int i = 0; i < localScripts.Count; i++)
            {
                DRUserScript runtimeScript = localScripts[i];
                // See if there are any recorded scripts with
                // the same title. If there are, then we update
                // that script. Otherwise, we just add the runtime
                // script
                bool hasFoundRecordedCounterpart = false;
                for(int j = 0; j < allRecordedScripts.Count; j++)
                {
                    DRUserScript recordedScript = allRecordedScripts[j];
                    if(runtimeScript.Name == recordedScript.Name)
                    {
                        Debug.Log("Updating script " + runtimeScript.Name);
                        hasFoundRecordedCounterpart = true;
                        recordedScript.UpdateDataFrom(runtimeScript);
                        break;
                    }
                }
                if (!hasFoundRecordedCounterpart)
                {
                    // We need to get a unique ID for the script
                    ushort scriptID = 0;
                    int idx = allRecordedScripts.Count - 1;
                    while(scriptID == 0 && idx >= 0)
                        scriptID = allRecordedScripts[idx--].ObjectID;
                    // No good script IDs to start from, just start at 256
                    if (scriptID == 0)
                        scriptID = 256;
                    else
                        scriptID++;// Use one more than the largest existing ID
                    runtimeScript.ObjectID = scriptID;
                    gameState.AddUserScript(scriptID, runtimeScript);
                    Debug.Log("Added runtime script to recording: " + runtimeScript.Name);
                }
            }
        }
        // Tell the server that we want to play a recording
        //using (DarkRiftWriter writer = DarkRiftWriter.Create())
        using (DarkRiftWriter writer = DarkRiftWriter.Create(initialMessageLen))
        {
            writer.Write(gameState);
            writer.Write(userList);
            using (Message msg = Message.Create(ServerTags.InitiateRecordingPlayback, writer))
                DarkRiftConnection.Instance.SendReliableMessage(msg);
        }
        // Begin loading the new game state locally
        // We don't add users until this is done, because users
        // need to reference objects, so they should always load
        // after objects
        Orchestrator.OnGameLoadedAndPlaying += FinishStartingRecordingPlay;
        _isPendingOrchestratorLoad = true;
        Orchestrator.Instance.LoadSavedGame(gameState, true);

        CurrentState = RecordingState.PlayingRecording;
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
    }
    /// <summary>
    /// Called when the Orchestrator has finished loading stuff for
    /// our recording. Now we can get the time and load in the users
    /// </summary>
    private void FinishStartingRecordingPlay()
    {
        Orchestrator.OnGameLoadedAndPlaying -= FinishStartingRecordingPlay;
        _isPendingOrchestratorLoad = false;
        UserManager.Instance.SetRecordedUsers(_recordedUsersPendingAdd);
        _recordedUsersPendingAdd = null;
        // Setup the time conversion. (This could probably be done sooner, but w/e)
        _playbackTime2RecordedTime = _initialFileMarker.Timestamp - TimeManager.Instance.RenderTime;
    }
    /// <summary>
    /// Called when we do a SpawnPlayer from a recording.
    /// This function sets up the player to be used in a recorded
    /// way
    /// </summary>
    /// <param name="user"></param>
    /// <returns>New ID to use</returns>
    public ushort AddRecordedUser(DRUser user)
    {
        user.TypeOfUser = DRUser.UserType.Recorded;
        ushort nextIDToAllocate = (ushort)(UserManager.Instance.LargestReceivedID + 32);
        // Get a new ID
        while (UserManager.Instance.HasUser(nextIDToAllocate))
            nextIDToAllocate++;
        Debug.Log("player ID #" + user.ID + "->" + nextIDToAllocate);
        _recordedUserIDs2RuntimeUserID.Add(user.ID, nextIDToAllocate);
        user.ID = nextIDToAllocate;
        return nextIDToAllocate;
    }
    public void RestartPlayingRecording()
    {
        if(CurrentState != RecordingState.PlayingRecording)
        {
            Debug.LogError("Can't restart recording when we're in state " + CurrentState);
            return;
        }
        if (_isPendingOrchestratorLoad)
        {
            Debug.LogError("Can't restart recording, we're waiting on orchestrator to load");
            return;
        }

        // TODO this isn't ideal, it resends a bunch of stuff over the network, but
        // I really think it's good enough, doing it the clever way would make this much more
        // complex than it needs to be
        StopPlayingRecording(true);
        PlayRecording();
    }
    public void StopPlayingRecording(bool willRestart)
    {
        if(CurrentState != RecordingState.PlayingRecording)
        {
            Debug.LogError("Can't stop recording when we're in state " + CurrentState);
            return;
        }
        if (_isPendingOrchestratorLoad)
        {
            // TODO we should be able to stop recording playback here
            Debug.LogError("Can't stop recording playback, we're waiting on orchestrator to load");
            return;
        }
        if(_binaryReader != null)
        {
            _binaryReader.Close();
            _binaryReader.Dispose();
            _binaryReader = null;
        }
        if(_drReader != null)
        {
            _drReader.Dispose();
            _drReader = null;
        }
        _recordedUserIDs2RuntimeUserID.Clear();
        _recordedIDs2Objects.Clear();
        _recordedClientID = ushort.MaxValue;
        UserManager.Instance.RemoveAllRecordedUsers();
        // Notify the server
        using(DarkRiftWriter writer = DarkRiftWriter.Create(1))
        {
            writer.Write(willRestart);
            using (Message msg = Message.Create(ServerTags.EndRecordingPlayback, writer))
                DarkRiftConnection.Instance.SendReliableMessage(msg);
        }
        CurrentState = RecordingState.None;
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
    }
    public void StartPlayingRecordingFail()
    {
        Debug.LogError("Failed to start playing recording");
        StopPlayingRecording(false);
    }
    /// <summary>
    /// Gets if this player is a playback from a recorded
    /// file
    /// </summary>
    /// <param name="playerID"></param>
    /// <returns></returns>
    public bool TryGetCurrentIDForRecordedPlayer(ushort playerID, out ushort newID)
    {
        return _recordedUserIDs2RuntimeUserID.TryGetValue(playerID, out newID);
    }
    public void AddObjectFromRecording(DRObject drObject)
    {
        // We need to turn the object's ID into an ID that we can actually use in the system
        ushort tempID;
        if (!DarkRiftConnection.Instance.GenerateObjectTemporaryID(out tempID))
        {
            Debug.LogError("Out of temp IDs, can't add obj from recording");
            return;
        }

        _recordedIDs2Objects[drObject.GetID()] = drObject;
        Debug.Log("Created temp ID #" + tempID + " to recorded ID #" + drObject.GetID());
        drObject.TemporaryID = tempID;
        drObject.ObjectID = 0; // An objectID of 0 tells the object that it should be using the temp ID
    }
    public bool TryGetObjectIDFromRecordedID(ushort recordedID, out ushort objectID)
    {
        bool ret = _recordedIDs2Objects.TryGetValue(recordedID, out DRObject obj);
        if (ret)
        {
            objectID = obj.GetID();
            return true;
        }
        objectID = ushort.MaxValue;
        return false;
    }
    public bool UpdateObjectID(ushort oldID, ushort newID)
    {
        if(!_recordedIDs2Objects.TryGetValue(oldID, out DRObject drObject))
        {
            Debug.LogError("Can't handle object ID update for ID #" + oldID + "->" + newID);
            return false;
        }
        _recordedIDs2Objects[newID] = drObject;
        _recordedIDs2Objects.Remove(oldID);
        return true;
    }
    public void OnServerStartPlayingRecording(DarkRiftReader reader)
    {
        CurrentState = RecordingState.ServerUserPlayingRecording;
        DRGameState gameState = reader.ReadSerializable<DRGameState>();
        DRUserList userList = reader.ReadSerializable<DRUserList>();
        ushort playbackOwner = reader.ReadUInt16();
        Debug.Log("Recording initiated by #" + playbackOwner);
        // Begin loading the new simulation
        Orchestrator.Instance.LoadSavedGame(gameState, false);
        UserManager.Instance.SetRecordedUsers(userList.Users);
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
    }
    public void OnServerEndPlayingRecording()
    {
        if(CurrentState != RecordingState.ServerUserPlayingRecording)
            Debug.LogError("Recv end playing recording when state is " + CurrentState);
        CurrentState = RecordingState.None;
        Debug.Log("Recv end playing recording");
        UserManager.Instance.RemoveAllRecordedUsers();
        CreateRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
        PlayRecordingMenu.RefreshFromRecordingStateChange(CurrentState);
    }
    private void Update()
    {
        _lastRenderTime = TimeManager.Instance.RenderTime;

        if (Input.GetKeyDown(KeyCode.F9))
            RestartPlayingRecording();

        // If we're in playback mode, then we should pull frames when the time comes
        if(CurrentState == RecordingState.PlayingRecording && !_isPendingOrchestratorLoad)
        {
            if (!_hasNextFileMarker)
                return;
            while (true)
            {
                float targetTime = _playbackTime2RecordedTime + TimeManager.Instance.RenderTime;
                if (_nextFileMarker.Timestamp > targetTime)
                {
                    //Debug.Log("Waiting for time, now " + Time.unscaledTime + " rel past " + targetTime + " nextTS " + _nextFileMarker.Timestamp);
                    return;
                }
                //Debug.Log("Ready to play, now " + Time.unscaledTime + " rel past " + targetTime + " nextTS " + _nextFileMarker.Timestamp);
                // Parse out the next message
                // Figure out how many bytes we're going to read for this
                // message, as there can be multiple sub-messages in one message
                // first tag isn't included in the DataLength calculation
                int endPosition = _drReader.Position + _nextFileMarker.DataLength + sizeof(byte);
                while(_drReader.Position < endPosition)
                {
                    byte tag = _drReader.ReadByte();
                    //Debug.Log("Handling tag " + tag);
                    DarkRiftConnection.Instance.HandleMessage(tag, _drReader, _nextFileMarker.MessageDir, _nextFileMarker.SendType, true);
                }

                if(_drReader.Position >= _drReader.Length)
                {
                    Debug.Log("Out of messages in the file");
                    _hasNextFileMarker = false;
                    return;
                }
                _drReader.ReadSerializableInto(ref _nextFileMarker);
            }
        }

    }
#if UNITY_EDITOR
    private void OnApplicationQuit()
    {
        // Make sure to cleanup file references on mobile
        if(_binaryReader != null)
        {
            _binaryReader.Close();
            _binaryReader.Dispose();
            _binaryReader = null;
        }
    }
#endif
}
