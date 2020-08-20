using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class Orchestrator : GenericSingleton<Orchestrator>
{
    /// <summary>
    /// Only turned on for testing
    /// </summary>
    public bool AutoJoinOrCreate;
    public string TestingRoomName;

    public string CurrentRoomID { get; private set; }
    // If we are loading a game state this frame
    // We need this to be frame based because OnDestroy
    // is called at the end of the frame, and the functions
    // called by OnDestroy need to know if the destroy was
    // apropos a GameState change
    public bool IsLoadingGameState { get { return 
                Time.frameCount == _frameCountOnLastLoadedGameState; } }
    public bool IsAddingObjectsFromGameState { get; private set; }
    public bool IsAppClosing { get; private set; }
    public bool IsPausedWaitingForObjectLoad { get; private set; }
    public Camera MainCamera { get; private set; }
    public SavedGameMetadata CurrentSavedGame { get; private set; }
    public DRGameState BaseGameState { get; private set; }
    public Modes CurrentMode { get; private set; }
    public static Action<Modes> OnModeChange;
    public static Action OnDoneLoadingObjectsFromGameState;
    public static Action OnGameLoadedAndPlaying;
    public bool HasLoadedGameState { get; private set; }
    private int _frameCountOnLastLoadedGameState = -1;
    private readonly List<BaseBehavior> _behaviorsPendingRefresh = new List<BaseBehavior>(64);

    public enum Modes
    {
        BuildMode,
        PlayMode
    }

    private Coroutine _getGameStateAndLoad;
    private Coroutine _pauseUntilLoadedRoutine;

    const string GameSceneName = "Game";

    protected override void Awake()
    {
        base.Awake();
        //Debug.Log("Orchestrator awake");
    }
    public void OnCameraChanged(Camera newCam)
    {
        MainCamera = newCam;
    }
    void Start()
    {
        DarkRiftConnection.OnJoinedRoom += OnJoinedRoom;
    }
    public void SetToMode(Modes toMode)
    {
        if (toMode == CurrentMode)
            return;
        CurrentMode = toMode;
        BuildPlayModeUI.Instance.TransitionToMode(toMode);
        switch (toMode)
        {
            case Modes.PlayMode:
                BuildPlayManager.Instance.EnterPlayingMode();
                break;
            case Modes.BuildMode:
                BuildPlayManager.Instance.OnLeavingPlayingMode();
                break;
        }
        if (OnModeChange != null)
            OnModeChange(CurrentMode);
    }
    private void OnApplicationQuit()
    {
        Debug.Log("App closing");
        IsAppClosing = true;
    }
    public void GetGameStateAndLoad(SavedGameMetadata savedGame)
    {
        // Write the S3 ID of the game state that we want to load
        int msgLen = 1 + savedGame.S3_ID.Length;
        using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
        {
            DRCompat.WriteStringSmallerThen255(writer, savedGame.S3_ID);
            using (Message msg = Message.Create(ServerTags.LoadGame, writer))
                DarkRiftConnection.Instance.SendReliableMessage(msg);
        }
    }
    public void LoadSavedGame(DRGameState drGameState, bool isLocalRecording)
    {
        Debug.Log("Loading from GameState");
        _frameCountOnLastLoadedGameState = Time.frameCount;
        HasLoadedGameState = true;
        SceneObjectManager.Instance.LocallyClearAllSceneObjects();
        UserScriptManager.Instance.LocallyClearAllUserScripts();
        SceneMaterialManager.Instance.LocallyClearAllSceneMaterials();
        Board.Instance.OnGameStateLoaded(drGameState);
        ObjectPanel.Instance.LocallyClearAll();
        if (_pauseUntilLoadedRoutine != null)
            StopCoroutine(_pauseUntilLoadedRoutine);
        _pauseUntilLoadedRoutine = StartCoroutine(PauseUntilAllObjectsLoaded(drGameState, isLocalRecording));
        BaseGameState = drGameState;
    }
    private IEnumerator PauseUntilAllObjectsLoaded(DRGameState drGameState, bool isLocalRecording)
    {
        IsPausedWaitingForObjectLoad = true;
        // When loading a save while the game is running, to prevent explosions
        // we have to set the timescale to 0, _then_ we wait for a frame, and
        // then begin loading the objects. Once all objects have loaded, we can
        // finally set the timescale to 1
        TimeManager.Instance.Pause(this);
        //TODO re-enable. We just need to immediately get the game state objects
        // so that we can process the objects that are grabbed by users
        //yield return null;
        // Add all the game state stuff
        // User Scripts
        List<DRUserScript> userScripts = drGameState.GetAllUserScripts();
        //Debug.Log("Loading " + scripts.Count + " scripts");
        for(int i = 0; i < userScripts.Count;i++)
        {
            //Debug.Log("Adding server script #" + kvp.Key);
            DRUserScript script = userScripts[i];
            UserScriptManager.Instance.OnServerCreatedUserScript(script);
        }
        // Materials
        var mats = drGameState.GetAllMaterials();
        foreach (var kvp in mats)
            SceneMaterialManager.Instance.OnServerAddedMaterial(kvp.Value);
        // Objects
        var objs = drGameState.GetAllObjects();
        //Debug.Log("GameState has " + objs.Count + " objects");
        IsAddingObjectsFromGameState = true;
        foreach (var kvp in objs)
            SceneObjectManager.Instance.OnServerAddedObject(kvp.Value, isLocalRecording, true);
        IsAddingObjectsFromGameState = false;

        // Notify behaviors that were waiting for all objects to be made
        for (int i = 0; i < _behaviorsPendingRefresh.Count; i++)
            _behaviorsPendingRefresh[i].RefreshProperties();
        _behaviorsPendingRefresh.Clear();
        //Debug.Log("Pausing until all objects loaded");

        if (OnDoneLoadingObjectsFromGameState != null)
            OnDoneLoadingObjectsFromGameState();

        while (SceneObjectManager.Instance.AreAnyObjectsLoading())
            yield return null;

        Debug.Log("All objects loaded, will initialize physics");
        IsPausedWaitingForObjectLoad = false;
        TimeManager.Instance.Play(this);
        if (OnGameLoadedAndPlaying != null)
            OnGameLoadedAndPlaying();
    }
    public void RefreshBehaviorAfterGamestateLoad(BaseBehavior behavior)
    {
        _behaviorsPendingRefresh.Add(behavior);
    }
    public void CreateRoom(string title, string gameTemplateID)
    {
        Debug.Log("Will create room " + title);
        StartCoroutine(CreateRoomRoutine(title, gameTemplateID));
    }
    IEnumerator CreateRoomRoutine(string title, string gameTemplateID)
    {
        //while (!PhotonConnection.Instance.CanJoinRoom())
            //yield return null;

        if(SceneManager.GetActiveScene().name != GameSceneName)
        {
            var asyncOp = SceneManager.LoadSceneAsync(GameSceneName);
            while (!asyncOp.isDone)
                yield return null;
        }

        // When creating a room, we can connect to Firebase, then to Photon
        //SceneSerializer.Instance.CreateGameRoom(title, gameTemplateID);
        //PhotonConnection.Instance.JoinOrCreateRoom(SceneSerializer.Instance.CurrentGameID);
    }
    public void JoinRoom(string roomID)
    {
        //CurrentRoomID = roomID;
        StartCoroutine(JoinRoomRoutine(roomID));
    }
    IEnumerator JoinRoomRoutine(string gameID)
    {
        //while (!PhotonConnection.Instance.CanJoinRoom())
            //yield return null;

        if(SceneManager.GetActiveScene().name != GameSceneName)
        {
            var asyncOp = SceneManager.LoadSceneAsync(GameSceneName);
            while (!asyncOp.isDone)
                yield return null;
        }

        //PhotonConnection.Instance.JoinOrCreateRoom(gameID);
        // Make sure to connect to Photon before entering game room
        //while (!PhotonConnection.Instance.IsInGameRoom())
            //yield return null; //TODO timeout
        //SceneSerializer.Instance.LoadGameRoom(gameID);
    }
    void OnJoinedRoom()
    {
        //TODO we should really have a loading screen or something
        if(SceneManager.GetActiveScene().name != GameSceneName)
            SceneManager.LoadScene(GameSceneName);
    }
}
