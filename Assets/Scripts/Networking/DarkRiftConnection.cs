using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DarkRift.Client.Unity;
using DarkRift.Client;
using DarkRift;
using DarkRiftAudio;
using System.Net;
using UnityEditor;

public class DarkRiftConnection : GenericSingleton<DarkRiftConnection>
{
    public static Action OnConnected;
    public static Action OnJoinedRoom;

    public DarkRiftMicrophone DarkRiftMicrophone;
    public ushort OurID { get { return Dispatcher.ID; } }
    public DarkRiftDispatcher Dispatcher { get; private set; }

    private readonly Dictionary<ushort, ushort> _lastSentGrabOwners = new Dictionary<ushort, ushort>();
    public DarkRiftAudioClient AudioClient { get; private set; }

    /// <summary>
    /// The temporary IDs for DRObjects we have that are still in use
    /// </summary>
    private readonly HashSet<ushort> _outstandingObjectTemporaryIDs = new HashSet<ushort>();
    /// <summary>
    /// The previously used temporary ID for a DRObject
    /// </summary>
    private ushort _lastObjectTemporaryID = 0;
    /// <summary>
    /// The temporary IDs for DRMaterials we have that are still in use
    /// </summary>
    private readonly HashSet<ushort> _outstandingMaterialTemporaryIDs = new HashSet<ushort>();
    /// <summary>
    /// The previously used temporary ID for a DRMaterial
    /// </summary>
    private ushort _lastMaterialTemporaryID = 0;
    /// <summary>
    /// The temporary IDs for DRUserScripts we have that are still in use
    /// </summary>
    private readonly HashSet<ushort> _outstandingUserScriptTemporaryIDs = new HashSet<ushort>();
    /// <summary>
    /// The previously used temporary ID for a DRUserScript
    /// </summary>
    private ushort _lastUserScriptTemporaryID = 0;
    public const int MaxTempID = 255;
    /// <summary>
    /// The bitrate for user audio
    /// </summary>
    const int VoiceOpusBitrate = 20000;
    private Vec3 _workingVector3 = new Vec3();
    private Quat _workingQuat = new Quat();
    private Col3 _workingCol3 = new Col3();

    private void Start()
    {
        //Debug.Log("Connecting to DarkRift");
        Dispatcher = new DarkRiftDispatcher(OnMsgRecv, OnDisconnected);
        Dispatcher.Connect(IPAddress.Parse(GLVars.Instance.GameServerAddress), GLVars.Instance.GameServerPort, OnConnectCallback);
    }
    void OnConnectCallback(System.Exception e)
    {
        if (Dispatcher.ConnectionState == DarkRift.ConnectionState.Connected)
        {
            Debug.Log("Connected, our ID is " + OurID);
            if(OnConnected != null)
                OnConnected();
            // Wait for username info to arrive
            if (!VRSDKUtils.Instance.HasRecvDisplayName)
                StartCoroutine(ServerConnectAfterUsernameRetrieved());
            else
                FinishServerConnection();
        }
        else
            Debug.LogError("Failed to make initial connection to DR! " + Dispatcher.ConnectionState);
    }
    private IEnumerator ServerConnectAfterUsernameRetrieved()
    {
        Debug.Log("Will finish server connection after username retrieved");
        yield return null;
        
        for(int i = 0; i < 900 && !VRSDKUtils.Instance.HasRecvDisplayName; i++)
            yield return null;
        if (!VRSDKUtils.Instance.HasRecvDisplayName)
            Debug.Log("Timed out waiting for display name");
        FinishServerConnection();
    }
    public void FinishServerConnection()
    {
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(VRSDKUtils.Instance.DisplayName);
            using(Message msg = Message.Create(ServerTags.FinishServerConnection, writer))
                SendReliableMessage(msg);
        }
    }
    private void OnMsgRecv(Message message, SendMode sendMode)
    {
        if (Orchestrator.Instance == null || Orchestrator.Instance.IsAppClosing)
            return;

        // Create the audio client once we get our first message
        if(AudioClient == null)
        {
            AudioClient = new DarkRiftAudio.DarkRiftAudioClient(SendAudioPacket, OurID);
            AudioClient.AddMumbleMic(DarkRiftMicrophone);
            AudioClient.SetBitrate(VoiceOpusBitrate);
        }
        using (DarkRiftReader reader = message.GetReader())
        {
            byte tag = message.Tag;
            //Debug.Log("Recv tag " + tag);
            if (tag == ServerTags.PingPong)
                return;// Handled elsewhere

            // Drop incoming data if we don't have a game loaded
            // TODO this seems wrong to me, we still want to integrate
            // changes to the system, even if we're waiting on models
            // to load
            if (!Orchestrator.Instance.HasLoadedGameState
                && tag != ServerTags.GameState
                && tag != ServerTags.ListGames)
            {
                //Debug.Log("Dropping data of type " + tag);
                return;
            }
            GameRecordingManager.Instance.OnRecvMessage(message, sendMode);
            HandleMessage(tag, reader, MessageDirection.Server2Client, sendMode, false);
        }
    }
    public void HandleMessage(byte tag, DarkRiftReader reader, MessageDirection msgDir, SendMode sendMode, bool isLocalRecordedMessage)
    {
        byte prevTag = byte.MaxValue;
        bool hasData = true;
        while(hasData)
        {
            //if (isLocalRecordedMessage)
            //Debug.Log("Recv msg: " + tag + " " + reader.Position + "/" + reader.Length);
            switch (tag)
            {
                case ServerTags.SpawnPlayer:
                    // Local recordings may have a non-recent game state
                    DRUser user = !isLocalRecordedMessage
                        ? reader.ReadSerializable<DRUser>()
                        : DRUser.DeserializeWithVersion(reader, Orchestrator.Instance.BaseGameState.Version);
                    UserManager.Instance.HandleSpawnPlayer(user, isLocalRecordedMessage);
                    break;
                case ServerTags.DespawnPlayer:
                    ushort removerUser = reader.ReadUInt16();
                    UserManager.Instance.HandleDespawnPlayer(removerUser, isLocalRecordedMessage, true);
                    break;
                case ServerTags.PlayerMovement_Build:
                    UserManager.Instance.OnServerPlayerMovement_Build(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.AddObject:
                    SceneObjectManager.Instance.OnServerAddedObject(reader.ReadSerializable<DRObject>(), isLocalRecordedMessage, false);
                    break;
                case ServerTags.AddObject_Response:
                    UpdateObjectID(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.RemoveObject:
                    SceneObjectManager.Instance.OnServerRemovedObject(reader.ReadUInt16(), isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_Pos:
                    SceneObjectManager.Instance.OnServerObjectPosition(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_Rot:
                    SceneObjectManager.Instance.OnServerObjectRotation(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_Scale:
                    SceneObjectManager.Instance.OnServerObjectScale(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_PosRot:
                    SceneObjectManager.Instance.OnServerObjectPositionRotation(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_PosRot_Rest:
                    SceneObjectManager.Instance.OnServerObjectPositionRotation_Rest(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_PosRotVelAngVel:
                    SceneObjectManager.Instance.OnServerObjectPosRotVelAngVel(reader, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.TransformObject_GrabPhysicsPosRotVelAngVel:
                    SceneObjectManager.Instance.OnServerObjectGrabPhysicsUpdate(reader, msgDir, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.PingPong:
                    return;
                case ServerTags.GrabObject:
                    HandleServerGrabUpdate(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.ReleaseGrabObject:
                    HandleServerReleaseGrabObject(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.OwnershipChange:
                    HandleOwnershipChange(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.AddBehavior:
                    HandleAddBehavior(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.RemoveBehavior:
                    HandleRemoveBehavior(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.UpdateBehavior:
                    HandleUpdateBehavior(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.VoiceData:
                    if (AudioClient != null)
                        AudioClient.AddCompressedAudioPacket(reader, msgDir, isLocalRecordedMessage);
                    else
                        Debug.LogWarning("No audio client, dropping pkt");
                    return; // Voice packets are standalone, so we return here
                case ServerTags.SetObjectName:
                    HandleObjectNameChange(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.AddMaterial:
                    // TODO recorded support
                    SceneMaterialManager.Instance.OnServerAddedMaterial(reader.ReadSerializable<DRMaterial>());
                    break;
                case ServerTags.AddMaterial_Response_Success:
                    // TODO recorded support
                    UpdateMaterialID(reader);
                    break;
                case ServerTags.AddMaterial_Response_Fail_Redundant:
                    HandleAddMaterialFail_Redundant(reader);
                    break;
                case ServerTags.MaterialColorChange:
                    HandleMaterialColorChange(reader, false, msgDir, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.MaterialColorChangeMultiple:
                    HandleMaterialColorChange(reader, true, msgDir, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.CreateUserScript:
                    UserScriptManager.Instance.OnServerCreatedUserScript(reader.ReadSerializable<DRUserScript>());
                    break;
                case ServerTags.CreateUserScript_Response_Success:
                    UpdateUserScriptID(reader);
                    break;
                case ServerTags.CreateUserScript_Response_Fail_Redundant:
                    throw new NotImplementedException();
                case ServerTags.UpdateUserScript:
                    HandleUserScriptChange(reader);
                    break;
                case ServerTags.UpdateUserScript_Response_Success:
                    HandleUserScriptResponseSuccess(reader);
                    break;
                case ServerTags.GameState:
                    HandleGameState(reader);
                    break;
                case ServerTags.SpawnInfoRequest:
                    HandleSpawnInfoRequest(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.PossessObj:
                    HandlePossessObj(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.DepossessObj:
                    HandleDepossessObj(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.PlayerMovement_Play_Grounded:
                case ServerTags.PlayerMovement_Play_NotGrounded:
                case ServerTags.PlayerMovement_Play_Grounded_OnObject:
                    UserManager.Instance.OnServerPlayerMovement_Play(reader, tag, msgDir, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.RPC_All_NonBuffered:
                case ServerTags.RPC_Others_NonBuffered:
                case ServerTags.RPC_Host_NonBuffered:
                case ServerTags.RPC_TargetUser_NonBuffered:
                case ServerTags.RPC_All_Buffered:
                case ServerTags.RPC_Others_Buffered:
                case ServerTags.RPC_Host_Buffered:
                case ServerTags.RPC_All_NonBuffered_Value:
                case ServerTags.RPC_Others_NonBuffered_Value:
                case ServerTags.RPC_Host_NonBuffered_Value:
                case ServerTags.RPC_TargetUser_NonBuffered_Value:
                case ServerTags.RPC_All_Buffered_Value:
                case ServerTags.RPC_Others_Buffered_Value:
                case ServerTags.RPC_Host_Buffered_Value:
                    UserScriptManager.Instance.HandleRPCOnScript(reader, tag);
                    break;
                case ServerTags.UserPose_Single:
                case ServerTags.UserPose_ThreePoints:
                case ServerTags.UserPose_Full:
                    UserManager.Instance.OnServerUserPose(reader, tag, msgDir, sendMode, isLocalRecordedMessage);
                    break;
                case ServerTags.InitiateRecordingPlayback:
                    GameRecordingManager.Instance.OnServerStartPlayingRecording(reader);
                    break;
                case ServerTags.InitiateRecordingPlayback_Fail:
                    GameRecordingManager.Instance.StartPlayingRecordingFail();
                    break;
                case ServerTags.EndRecordingPlayback:
                    GameRecordingManager.Instance.OnServerEndPlayingRecording();
                    break;
                case ServerTags.PlayPause:
                    HandlePlayPause(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.Ragdoll_Main:
                    HandleRagdollMain(reader, msgDir, isLocalRecordedMessage);
                    break;
                case ServerTags.BoardSizeChange:
                    HandleBoardSizeChange(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.BoardVisiblityChange:
                    HandleBoardVisibilityChange(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.ObjectEnableChange:
                    HandleObjectEnableChange(reader, isLocalRecordedMessage);
                    break;
                case ServerTags.UserBlendChange:
                    HandleUserBlendChange(reader, isLocalRecordedMessage, msgDir);
                    break;
                default:
                    Debug.LogError("Unhandled tag: " + tag + " prev tag: " + prevTag);
                    break;
            }

            // If there's still data left
            // read the next tag and continue
            hasData = !isLocalRecordedMessage && reader.Position < reader.Length;
            prevTag = tag;
            if (hasData)
                tag = reader.ReadByte();
        }
    }
    private void HandleRagdollMain(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        int posA = reader.Position;
        ushort objID = reader.ReadUInt16();
        RagdollSerialization.Deserialize(out RagdollMain ragdollData, reader);

        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if (!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get object ID for #" + runtimeID);
                return;
            }
            objID = runtimeID;
        }

        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(objID, out SceneObject sceneObject))
        {
            Debug.LogError("Failed to get sceneobj #" + objID + " for ragdoll main");
            return;
        }
        RagdollBehavior ragdollBehavior = sceneObject.RagdollBehavior;
        if(ragdollBehavior == null)
        {
            Debug.LogError("Can't integrate ragdoll data, there is no ragdoll on #" + objID);
            return;
        }
        ragdollBehavior.IntegrateRagdollHitPose(ref ragdollData);

        if (isLocalRecordedMessage) {
            int msgLen = reader.Position - posA;
            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
            {
                writer.Write(objID);
                RagdollSerialization.Serialize(ref ragdollData, writer);
                using (Message msg = Message.Create(ServerTags.Ragdoll_Main, writer))
                    DarkRiftConnection.Instance.SendUnreliableMessage(msg);
            }
        }
    }
    private void HandleBoardSizeChange(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        float width = reader.ReadSingle();
        float height = reader.ReadSingle();

        Board.Instance.OnNetworkChangeBoardSize(width, height);

        if (isLocalRecordedMessage) {
            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create(8))
            {
                writer.Write(width);
                writer.Write(height);
                using (Message msg = Message.Create(ServerTags.BoardSizeChange, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleBoardVisibilityChange(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        bool visible = reader.ReadBoolean();
        Board.Instance.OnNetworkChangeBoardVisibility(visible);

        if (isLocalRecordedMessage) {
            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create(1))
            {
                writer.Write(visible);
                using (Message msg = Message.Create(ServerTags.BoardVisiblityChange, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleObjectEnableChange(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        bool enabled = reader.ReadBoolean();

        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort currentID))
            {
                Debug.LogError("Failed to get the current ID for rec ID #" + objectID);
                return;
            }
            objectID = currentID;
        }

        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(objectID, out SceneObject sceneObject))
        {
            Debug.LogError("Can't handle object enable change, no object #" + objectID);
            return;
        }

        sceneObject.SetEnabled(enabled, false);

        if (isLocalRecordedMessage) {
            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create(3))
            {
                writer.Write(objectID);
                writer.Write(enabled);
                using (Message msg = Message.Create(ServerTags.ObjectEnableChange, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleUserBlendChange(DarkRiftReader reader, bool isLocalRecordedMessage, MessageDirection msgDir)
    {
        ushort userID;
        if (isLocalRecordedMessage)
        {
            if(msgDir == MessageDirection.Client2Server)
                userID = GameRecordingManager.Instance.RecordedClientID;
            else
            {
                userID = reader.ReadUInt16();
                if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(userID, out ushort currentID))
                {
                    Debug.LogError("Failed to get the current ID for rec ID #" + userID);
                    // Clear the reader
                    reader.DecodeInt32();
                    reader.ReadSingle();
                    return;
                }
                userID = currentID;
            }
        }
        else
            userID = reader.ReadUInt16();

        int idx = reader.DecodeInt32();
        float val = reader.ReadSingle();
        if(!UserManager.Instance.TryGetUserDisplay(userID, out UserDisplay userDisplay))
        {
            Debug.LogError("Can't handle user blend change, no user #" + userID);
            return;
        }

        userDisplay.SetUserBlend(idx, val, false);

        if (isLocalRecordedMessage) {
            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create(7))
            {
                writer.Write(userID);
                writer.EncodeInt32(idx);
                writer.Write(val);
                using (Message msg = Message.Create(ServerTags.UserBlendChange_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandlePlayPause(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        bool isPlaying = reader.ReadBoolean();
        bool wasSentByUs;
        ushort sender;
        if (msgDir == MessageDirection.Server2Client)
        {
            sender = reader.ReadUInt16();
            wasSentByUs = OurID == sender;
        }
        else
        {
            wasSentByUs = false;
            if (!isLocalRecordedMessage)
                Debug.LogError("Got client2server PlayPause without it being a recorded message?");
            sender = GameRecordingManager.Instance.RecordedClientID;
        }

        PlayPauseButton.Instance.OnServerSetPauseState(isPlaying, wasSentByUs);

        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            // If this is a recorded message, where the client
            // received an echo about the play/pause, then we
            // can safely drop this
            if (sender == GameRecordingManager.Instance.RecordedClientID)
                return;
            if(!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(sender, out ushort runtimeID))
            {
                Debug.LogError("Failed to get user ID for #" + sender);
                return;
            }
            sender = runtimeID;

            // If this is a recorded message, we'll want to echo this to the server
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(isPlaying);
                writer.Write(sender);
                using (Message msg = Message.Create(ServerTags.PlayPause_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandlePossessObj(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        ushort userID = reader.ReadUInt16();
        ushort possessedObj = reader.ReadUInt16();

        if (isLocalRecordedMessage)
        {
            // If we sent this message in the recording, then we can drop this
            // this is because the SpawnInfo is echoed from the server, so we'll
            // just playback the version that was from the server. Otherwise, it's
            // sent twice
            if (msgDir == MessageDirection.Client2Server)
                return;
            if(!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(userID, out ushort spawnRecID))
            {
                Debug.LogError("Couldn't recording correct spawn user ID #" + userID);
                return;
            }
            userID = spawnRecID;

            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(possessedObj, out ushort spawnRecObjID))
            {
                Debug.LogError("Couldn't recording correct object ID #" + userID);
                return;
            }
            possessedObj = spawnRecObjID;
        } else if(GameRecordingManager.Instance.CurrentState == GameRecordingManager.RecordingState.PlayingRecording)
        {
            //Debug.LogError("recv spawn in playing");
            // If this is a non-recorded message from the server for a recorded player,
            // then we drop this. This is because it's just an echo of our message, which we've already handled
            if (!UserManager.Instance.TryGetUserDisplay(userID, out UserDisplay userDisplay))
            {
                Debug.LogError("No user for ID " + userDisplay);
                return;
            }
            if (userDisplay.DRUserObj.TypeOfUser == DRUser.UserType.Recorded)
                return;
        }
        Debug.Log("Recv spawn info for #" + userID);
        UserManager.Instance.OnServerPossess(userID, possessedObj);
        if(userID == OurID)
            BuildPlayManager.Instance.OnReceivedSpawnInfo();

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(userID);
                writer.Write(possessedObj);
                using (Message msg = Message.Create(ServerTags.PossessObj, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleDepossessObj(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        ushort userID = reader.ReadUInt16();

        if (isLocalRecordedMessage)
        {
            // If we sent this message in the recording, then we can drop this
            // this is because the DepossessObj is echoed from the server, so we'll
            // just playback the version that was from the server. Otherwise, it's
            // sent twice
            if (msgDir == MessageDirection.Client2Server)
                return;
            if(!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(userID, out ushort recUserID))
            {
                Debug.LogError("Couldn't recording correct spawn user ID #" + userID);
                return;
            }
            userID = recUserID;
        } else if(GameRecordingManager.Instance.CurrentState == GameRecordingManager.RecordingState.PlayingRecording)
        {
            // If this is a non-recorded message from the server for a recorded player,
            // then we drop this. This is because it's just an echo of our message, which we've already handled
            if (!UserManager.Instance.TryGetUserDisplay(userID, out UserDisplay userDisplay))
            {
                Debug.LogError("No user for ID " + userDisplay);
                return;
            }
            if (userDisplay.DRUserObj.TypeOfUser == DRUser.UserType.Recorded)
                return;
        }
        Debug.Log("Recv depossess for #" + userID);
        // We don't integrate depossess for ourselves, because
        // we already have that integrated. This is especially
        // important if we possess/depossess/possess quickly
        if(userID != OurID)
            UserManager.Instance.OnServerDepossess(userID);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(userID);
                using (Message msg = Message.Create(ServerTags.DepossessObj, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleSpawnInfoRequest(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        // We don't handle the SpawnInfoRequest if this is from a recording,
        // because we know that it's going to be handled by another recorded user
        if (isLocalRecordedMessage)
        {
            if (msgDir == MessageDirection.Server2Client)
                reader.ReadUInt16();
            return;
        }

        Debug.Log("Recv spawn info req");
        ushort clientID = reader.ReadUInt16();
        BuildPlayManager.Instance.OnServerRequestedSpawnInfoForUser(clientID);
    }
    private void HandleGameState(DarkRiftReader reader)
    {
        DRGameState gameState = reader.ReadSerializable<DRGameState>();
        Orchestrator.Instance.LoadSavedGame(gameState, false);
    }
    private void SendAudioPacket(DarkRiftWriter writer)
    {
        if (Dispatcher == null)
            return;
        using(Message msg = Message.Create(ServerTags.VoiceData, writer))
        {
            Dispatcher.SendMessage(msg, SendMode.Unreliable);
            GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Unreliable);
        }
    }
    private void HandleMaterialColorChange(DarkRiftReader reader, bool isMultiple, MessageDirection msgDir, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort userID;
        if (msgDir == MessageDirection.Server2Client)
        {
            userID = reader.ReadUInt16();
            // Handle if color changer is a recorded user
            if(isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(userID, out ushort newID))
            {
                Debug.Log("Replacing color changer ID #" + userID + "->" + newID);
                userID = newID;
            }
        }
        else
            userID = GameRecordingManager.Instance.RecordedClientID;
        ushort matID = reader.ReadUInt16();
        int num = isMultiple ? reader.DecodeInt32() : 1;
        bool isFromUs = userID == OurID;

        Queue<int> indexes = isLocalRecordedMessage ? new Queue<int>(num) : null;
        Queue<Color> colors = isLocalRecordedMessage ? new Queue<Color>(num) : null;

        for(int i = 0; i < num; i++)
        {
            int propertyIndex = reader.DecodeInt32();
            reader.ReadSerializableInto(ref _workingCol3);
            Color color = _workingCol3.ToColor();
            if (isLocalRecordedMessage)
            {
                indexes.Enqueue(propertyIndex);
                colors.Enqueue(color);
            }
            SceneMaterialManager.Instance.OnServerUpdateMaterialColor(matID, propertyIndex, color, isFromUs);
        }

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            int msgLen = 2 * sizeof(ushort) + num * (1 + Col3.MessageLen);
            if (isMultiple)
                msgLen++;
            using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
            {
                writer.Write(userID);
                writer.Write(matID);
                if (isMultiple)
                    writer.EncodeInt32(num);
                for(int i = 0; i < num; i++)
                {
                    writer.EncodeInt32(indexes.Dequeue());
                    _workingCol3.UpdateFrom(colors.Dequeue());
                    writer.Write(_workingCol3);
                }
                byte tag = isMultiple ? ServerTags.MaterialColorChangeMultiple_Recorded : ServerTags.MaterialColorChange_Recorded;
                using (Message msg = Message.Create(tag, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    private void HandleUserScriptChange(DarkRiftReader reader)
    {
        ushort scriptID = reader.ReadUInt16();
        UserScriptManager.Instance.ServerSentUpdatedUserScript(scriptID, reader);
    }
    private void HandleUserScriptResponseSuccess(DarkRiftReader reader)
    {
        ushort scriptID = reader.ReadUInt16();
        UserScriptManager.Instance.OnServerSentUpdateScriptSuccess(scriptID);
    }
    private void HandleObjectNameChange(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        ushort userID;
        if (msgDir == MessageDirection.Server2Client)
        {
            userID = reader.ReadUInt16();
            // Handle if name changer is a recorded user
            if(isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(userID, out ushort newID))
            {
                Debug.Log("Replacing name changer ID #" + userID + "->" + newID);
                userID = newID;
            }
        }
        else
            userID = GameRecordingManager.Instance.RecordedClientID;
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                return;
            }
            objectID = runtimeID;
        }
        string name = DRCompat.ReadStringSmallerThan255(reader);
        SceneObjectManager.Instance.OnServerChangeName(userID, objectID, name);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            // Expected message length
            int msgLen = (2 * sizeof(ushort) + 1 + name.Length);
            using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
            {
                writer.Write(userID);
                writer.Write(objectID);
                DRCompat.WriteStringSmallerThen255(writer, name);
                using (Message msg = Message.Create(ServerTags.SetObjectName_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleAddBehavior(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                return;
            }
            objectID = runtimeID;
        }
        SerializedBehavior serializedBehavior = reader.ReadSerializable<SerializedBehavior>();
        SceneObjectManager.Instance.OnServerAddedBehavior(objectID, serializedBehavior);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                writer.Write(serializedBehavior);
                using (Message msg = Message.Create(ServerTags.AddBehavior, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleRemoveBehavior(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                return;
            }
            objectID = runtimeID;
        }
        bool wasBehaviorNetworked = reader.ReadBoolean();
        ushort removedBehavior = reader.ReadUInt16();
        SceneObjectManager.Instance.OnServerRemovedBehavior(objectID, wasBehaviorNetworked, removedBehavior);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                writer.Write(wasBehaviorNetworked);
                writer.Write(removedBehavior);
                using (Message msg = Message.Create(ServerTags.RemoveBehavior, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleUpdateBehavior(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                return;
            }
            objectID = runtimeID;
        }
        byte header = reader.ReadByte();
        SerializedBehavior.ParseHeader(header, out bool isBehaviorNetworked, out bool hasFlags);
        ushort updatedBehavior = reader.ReadUInt16();
        SceneObjectManager.Instance.OnServerUpdatedBehavior(objectID, isBehaviorNetworked, hasFlags, updatedBehavior, reader);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                writer.Write(isBehaviorNetworked);
                writer.Write(updatedBehavior);
                using (Message msg = Message.Create(ServerTags.UpdateBehavior, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleOwnershipChange(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        ushort newOwner;
        if (msgDir == MessageDirection.Server2Client)
        {
            newOwner = reader.ReadUInt16();
            // Handle if owner is a recorded user
            if(isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(newOwner, out ushort newID))
            {
                Debug.Log("Replacing owner ID #" + newOwner + "->" + newID);
                newOwner = newID;
            }
        }
        else
            newOwner = GameRecordingManager.Instance.RecordedClientID;

        ushort objID = reader.ReadUInt16();
        //TODO we may want to map this from recorded ID to current ID, for Client2Server
        uint ownershipTime = reader.ReadUInt32();
        SceneObjectManager.Instance.OnSceneObjectOwnershipChange(objID, newOwner, ownershipTime);
        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(newOwner);
                writer.Write(objID);
                writer.Write(ownershipTime);
                using (Message msg = Message.Create(ServerTags.OwnershipChange_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleServerGrabUpdate(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        // We drop recorded grab/release updates that are from the client->server because
        // during a recording we essentially act as a third party, which would normally
        // only get the server->client messages
        if (isLocalRecordedMessage && msgDir == MessageDirection.Client2Server)
        {
            // Clear the reader
            reader.ReadUInt16(); // Object
            reader.ReadByte();   // Body part
            reader.ReadSerializableInto(ref _workingVector3);
            reader.ReadSerializableInto(ref _workingQuat);
            return;
        }
        // Who grabbed it
        ushort grabber = reader.ReadUInt16();
        // Handle if grabber is a recorded user
        if(isLocalRecordedMessage
            && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(grabber, out ushort newID))
        {
            Debug.Log("Replacing grabber ID #" + grabber + "->" + newID);
            grabber = newID;
        }
        // Object ID
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                reader.ReadByte();   // Body part
                reader.ReadSerializableInto(ref _workingVector3);
                reader.ReadSerializableInto(ref _workingQuat);
                return;
            }
            objectID = runtimeID;
        }
        // The type of grab
        DRUser.GrabbingBodyPart bodyPart = (DRUser.GrabbingBodyPart)reader.ReadByte();
        // The relative orientation
        reader.ReadSerializableInto(ref _workingVector3);
        reader.ReadSerializableInto(ref _workingQuat);

        Vector3 relPos = _workingVector3.ToVector3();
        Quaternion relRot = _workingQuat.ToQuaternion();

        // Update the object
        SceneObjectManager.Instance.OnSceneObjectGrabStateChange(objectID, grabber, 0);
        // Update the user who's grabbing it
        UserManager.Instance.OnServerUserGrabObject(grabber, objectID, bodyPart, relPos, relRot);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(grabber);
                writer.Write(objectID);
                writer.Write((byte)bodyPart);
                writer.Write(_workingVector3);
                writer.Write(_workingQuat);
                using (Message msg = Message.Create(ServerTags.GrabObject_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    private void HandleServerReleaseGrabObject(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        // We drop recorded grab/release updates that are from the client->server because
        // during a recording we essentially act as a third party, which would normally
        // only get the server->client messages
        if (isLocalRecordedMessage && msgDir == MessageDirection.Client2Server)
        {
            // Clear the reader
            reader.ReadUInt16(); // Object
            reader.ReadUInt32(); // Time of release
            return;
        }
        // UserID
        ushort userID = reader.ReadUInt16();
        // Handle if grabber is a recorded user
        if(isLocalRecordedMessage
            && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(userID, out ushort newID))
        {
            Debug.Log("Replacing release grabber ID #" + userID + "->" + newID);
            userID = newID;
        }

        // Object ID
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                return;
            }
            Debug.Log("Replacing release grab object ID #" + objectID + "->" + runtimeID);
            objectID = runtimeID;
        }
        // time released
        uint timeReleased = reader.ReadUInt32();

        // Time when the user released the object
        SceneObjectManager.Instance.OnSceneObjectGrabStateChange(objectID, DRObject.NoneGrabbing, timeReleased);
        // Update the user who released it
        UserManager.Instance.OnServerUserReleaseObject(userID, objectID);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(userID);
                writer.Write(objectID);
                writer.Write(timeReleased);
                using (Message msg = Message.Create(ServerTags.ReleaseGrabObject_Recorded, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    public bool GenerateObjectTemporaryID(out ushort objTempID)
    {
        if(_outstandingObjectTemporaryIDs.Count >= MaxTempID + 1)
        {
            Debug.LogWarning("Out of available object temporary IDs!!!");
            //throw new Exception("out of object IDs");
            objTempID = ushort.MaxValue;
            return false;
        }

        _lastObjectTemporaryID = (ushort)((_lastObjectTemporaryID + 1) % (MaxTempID + 1));
        int numAttempts = 0;
        while(numAttempts < (MaxTempID + 1))
        {
            if (!_outstandingObjectTemporaryIDs.Contains(_lastObjectTemporaryID))
            {
                // If the tempID we're trying is not in our list, then we're good
                _outstandingObjectTemporaryIDs.Add(_lastObjectTemporaryID);
                objTempID = _lastObjectTemporaryID;
                return true;
            }
            // Otherwise, try again
            _lastObjectTemporaryID = (ushort)((_lastObjectTemporaryID + 1) % (MaxTempID + 1));
            numAttempts++;
        }
        Debug.LogError("Out of available temporary object IDs (b)!!!");
        throw new Exception("out of Object IDs(b)");
    }
    private bool GenerateMaterialTemporaryID(out ushort matTempID)
    {
        if(_outstandingMaterialTemporaryIDs.Count >= MaxTempID + 1)
        {
            Debug.LogWarning("Out of available material temporary IDs!!!");
            //throw new Exception("out of material IDs");
            matTempID = ushort.MaxValue;
            return false;
        }

        _lastMaterialTemporaryID = (ushort)((_lastMaterialTemporaryID + 1) % (MaxTempID + 1));
        int numAttempts = 0;
        while(numAttempts < (MaxTempID + 1))
        {
            if (!_outstandingMaterialTemporaryIDs.Contains(_lastMaterialTemporaryID))
            {
                // If the tempID we're trying is not in our list, then we're good
                _outstandingMaterialTemporaryIDs.Add(_lastMaterialTemporaryID);
                matTempID = _lastMaterialTemporaryID;
                return true;
            }
            // Otherwise, try again
            _lastMaterialTemporaryID = (ushort)((_lastMaterialTemporaryID + 1) % (MaxTempID + 1));
            numAttempts++;
        }
        Debug.LogError("Out of available material temporary IDs (b)!!!");
        throw new Exception("out of material IDs(b)");
    }
    private bool GenerateUserScriptTemporaryID(out ushort scriptTempID)
    {
        if(_outstandingUserScriptTemporaryIDs.Count >= MaxTempID + 1)
        {
            Debug.LogWarning("Out of available user script temporary IDs!!!");
            //throw new Exception("out of script IDs");
            scriptTempID = ushort.MaxValue;
            return false;
        }

        _lastUserScriptTemporaryID = (ushort)((_lastUserScriptTemporaryID + 1) % (MaxTempID + 1));
        int numAttempts = 0;
        while(numAttempts < (MaxTempID + 1))
        {
            if (!_outstandingUserScriptTemporaryIDs.Contains(_lastUserScriptTemporaryID))
            {
                // If the tempID we're trying is not in our list, then we're good
                _outstandingUserScriptTemporaryIDs.Add(_lastUserScriptTemporaryID);
                scriptTempID = _lastUserScriptTemporaryID;
                return true;
            }
            // Otherwise, try again
            _lastUserScriptTemporaryID = (ushort)((_lastUserScriptTemporaryID + 1) % (MaxTempID + 1));
            numAttempts++;
        }
        Debug.LogError("Out of available user script temporary IDs (b)!!!");
        throw new Exception("out of user script IDs(b)");
    }
    public DRObject CreateDRObject(string bundleID, string objectName, ushort bundleIdx, Transform trans, List<SerializedBehavior> serializedBehaviors)
    {
        // First we need to get a new temporary ID for this object
        ushort tempID;
        if (!GenerateObjectTemporaryID(out tempID))
            return null;

        Debug.Log("Adding object, using temporary ID " + tempID);
        // Create the new object using the tempID
        DRObject newDRObj = new DRObject(tempID, OurID, bundleID, objectName, bundleIdx, trans.localPosition.ToVec3(), trans.localRotation.ToQuat(), trans.localScale.ToVec3(), DarkRiftPingTime.Instance.ServerTime, serializedBehaviors);

        // Notify the server about the creation of this object
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(newDRObj);
            using (Message message = Message.Create(ServerTags.AddObject, writer))
            {
                Debug.Log("Sending add obj message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }

        return newDRObj;
    }
    public void RemoveObject(ushort objectID)
    {
        // Notify the server about the creation of this object
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(objectID);
            using (Message message = Message.Create(ServerTags.RemoveObject, writer))
            {
                //Debug.Log("Sending rem obj message #" + objectID);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }
    }
    public void UpdateUserBlendShape(DRUser user, int idx, float val)
    {
        user.SetBlendShape(idx, val);
        using(DarkRiftWriter writer = DarkRiftWriter.Create(5))
        {
            writer.EncodeInt32(idx);
            writer.Write(val);
            using(Message message = Message.Create(ServerTags.UserBlendChange, writer))
            {
                // TODO add unreliable transmission as well
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }
    }
    public DRMaterial CreateDRMaterial(string bundleID, ushort materialIdx, string materialName)
    {
        // First we need to get a new temporary ID for this material
        ushort tempID;
        if (!GenerateMaterialTemporaryID(out tempID))
            return null;

        // Create the new object using the tempID
        DRMaterial drMat = new DRMaterial(tempID, bundleID, materialIdx, materialName);

        // Notify the server about the creation of this object
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(drMat);
            using (Message message = Message.Create(ServerTags.AddMaterial, writer))
            {
                Debug.Log("Sending add mat message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }

        return drMat;
    }
    public DRUserScript CreateDRUserScript(string title, string code, bool syncPosRotScale, DRUserScript.WhoRuns whoRuns, string bundleID=null, ushort bundleIdx=0)
    {
        // First we need to get a new temporary ID for this user script
        ushort tempID;
        if (!GenerateUserScriptTemporaryID(out tempID))
            return null;

        // Create the new user script using the tempID
        DRUserScript drScript = new DRUserScript(tempID, title, code, syncPosRotScale, whoRuns, bundleID, bundleIdx);

        // Notify the server about the creation of this script
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(drScript);
            using (Message message = Message.Create(ServerTags.CreateUserScript, writer))
            {
                Debug.Log("Sending create user script message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }

        return drScript;
    }
    public void SendUnreliableMessage(Message msg)
    {
        if (Dispatcher == null)
            return;
        Dispatcher.SendMessage(msg, SendMode.Unreliable);
        GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Unreliable);
    }
    public void SendReliableMessage(Message msg)
    {
        if (Dispatcher == null)
            return;
        Dispatcher.SendMessage(msg, SendMode.Reliable);
        GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
    }
    public void SendMessage(Message msg, SendMode sendMode)
    {
        if (sendMode == SendMode.Reliable)
            SendReliableMessage(msg);
        else
            SendUnreliableMessage(msg);
    }
    private void UpdateObjectID(DarkRiftReader reader, bool isLocalRecordedMessage)
    {
        ushort oldTempID = reader.ReadUInt16();
        ushort newObjectID = reader.ReadUInt16();

        if (isLocalRecordedMessage)
        {
            Debug.Log("Recorded updating ID for object, was " + oldTempID + " now " + newObjectID);
            GameRecordingManager.Instance.UpdateObjectID(oldTempID, newObjectID);
            // The recorded object IDs are only stored in GameRecordingManager,
            // so we can just update it there
            return;
        }
        Debug.Log("Updating ID for object, was " + oldTempID + " now " + newObjectID);

        // Update the last sent grab owners, which could potentially contain a temporary ID
        if (_lastSentGrabOwners.ContainsKey(oldTempID))
        {
            ushort grabOwner = _lastSentGrabOwners[oldTempID];
            _lastSentGrabOwners.Remove(oldTempID);
            _lastSentGrabOwners.Add(newObjectID, grabOwner);
        }

        bool rem = _outstandingObjectTemporaryIDs.Remove(oldTempID);
        if (!rem)
            Debug.LogWarning("Failed to remove object temporary ID #" + oldTempID);
        SceneObjectManager.Instance.UpdateObjectID(oldTempID, newObjectID);
    }
    private void UpdateMaterialID(DarkRiftReader reader)
    {
        ushort oldTempID = reader.ReadUInt16();
        ushort newObjectID = reader.ReadUInt16();
        Debug.Log("Updating ID for material, was " + oldTempID + " now " + newObjectID);

        bool rem = _outstandingMaterialTemporaryIDs.Remove(oldTempID);
        if (!rem)
            Debug.LogWarning("Failed to remove temporary ID for material, ID #" + oldTempID);
        SceneMaterialManager.Instance.UpdateMaterialID(oldTempID, newObjectID);
    }
    private void UpdateUserScriptID(DarkRiftReader reader)
    {
        ushort oldTempID = reader.ReadUInt16();
        ushort newObjectID = reader.ReadUInt16();
        Debug.Log("Updating ID for UserScript, was " + oldTempID + " now " + newObjectID);

        bool rem = _outstandingUserScriptTemporaryIDs.Remove(oldTempID);
        if (!rem)
            Debug.LogWarning("Failed to remove user script temp id #" + oldTempID);
        UserScriptManager.Instance.UpdateDRUserScriptID(oldTempID, newObjectID);
    }
    /// <summary>
    /// Called when we try adding a material that already exists on the server
    /// </summary>
    /// <param name="reader"></param>
    private void HandleAddMaterialFail_Redundant(DarkRiftReader reader)
    {
        ushort oldTempID = reader.ReadUInt16();
        ushort correctID = reader.ReadUInt16();
        Debug.Log("Replacing redundant added material #" + oldTempID + " now " + correctID);

        _outstandingMaterialTemporaryIDs.Remove(oldTempID);
        SceneMaterialManager.Instance.ReplaceRedundantDRMaterialWithCorrect(oldTempID, correctID);
    }
    public void OnDisconnected(DisconnectedEventArgs cause)
    {
        if (!cause.LocalDisconnect)
            Debug.LogError("Unexpected disconnected! " + cause.Error);
    }
    public void UpdateReliablePosition(DRObject obj, Vector3 newPos)
    {
        Vec3 pos = newPos.ToVec3();
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(obj.GetID());
            writer.Write(pos);
            using (Message message = Message.Create(ServerTags.TransformObject_Pos, writer))
            {
                Debug.Log("Sending move obj message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);

                // We deserialize into the object, so that we keep a consistent state with the
                // server, as the serialization may be lossy
                using(DarkRiftReader reader = message.GetReader())
                {
                    reader.ReadUInt16();
                    obj.DeserializePos(reader);
                }
            }
        }
    }
    public void UpdateReliableRotation(DRObject obj, Quaternion newRot)
    {
        Quat rot = newRot.ToQuat();
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(obj.GetID());
            writer.Write(rot);
            using (Message message = Message.Create(ServerTags.TransformObject_Rot, writer))
            {
                Debug.Log("Sending rot obj message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
                // We deserialize into the object, so that we keep a consistent state with the
                // server, as the serialization may be lossy
                using(DarkRiftReader reader = message.GetReader())
                {
                    reader.ReadUInt16();
                    obj.DeserializeRot(reader);
                }
            }
        }
    }
    public void UpdateReliableScale(DRObject obj, Vector3 newScale)
    {
        Vec3 scale = newScale.ToVec3();
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(obj.GetID());
            writer.Write(scale);
            using (Message message = Message.Create(ServerTags.TransformObject_Scale, writer))
            {
                Debug.Log("Sending scale obj message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
                // We deserialize into the object, so that we keep a consistent state with the
                // server, as the serialization may be lossy
                using(DarkRiftReader reader = message.GetReader())
                {
                    reader.ReadUInt16();
                    obj.DeserializeScale(reader);
                }
            }
        }
    }
    public void UpdateReliablePositionRotation(DRObject obj, Vector3 newPos, Quaternion newRot)
    {
        Vec3 pos = newPos.ToVec3();
        Quat rot = newRot.ToQuat();
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(obj.GetID());
            writer.Write(pos);
            writer.Write(rot);
            using (Message message = Message.Create(ServerTags.TransformObject_PosRot, writer))
            {
                Debug.Log("Sending posrot obj message, len " + message.DataLength);
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);

                // We deserialize into the object, so that we keep a consistent state with the
                // server, as the serialization may be lossy
                using(DarkRiftReader reader = message.GetReader())
                {
                    reader.ReadUInt16();
                    obj.DeserializePos(reader);
                    obj.DeserializeRot(reader);
                }
            }
        }
    }
    public void ObjectEnableChange(DRObject obj, bool isEnabled)
    {
        // First, update the DRObject
        obj.SetEnabled(isEnabled);
        // Update the server
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(obj.GetID());
            writer.Write(isEnabled);
            using (Message message = Message.Create(ServerTags.ObjectEnableChange, writer))
            {
                Dispatcher.SendMessage(message, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(message, SendMode.Reliable);
            }
        }
    }
    public void JoinOrCreateRoom(string roomID)
    {
    }
    public bool IsInGameRoom()
    {
        //Debug.Log("Is in game room " + PhotonNetwork.NetworkClientState);
        return Dispatcher.ConnectionState == DarkRift.ConnectionState.Connected;
    }
    public bool CanJoinRoom()
    {
        return true;
    }
    public void AddBehaviorToObject(DRObject dRObject, SerializedBehavior serializedBehavior)
    {
        dRObject.AddBehavior(serializedBehavior);
        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(dRObject.GetID());
            writer.Write(serializedBehavior);
            using (Message msg = Message.Create(ServerTags.AddBehavior, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
    }
    public void RemoveBehaviorFromObject(DRObject dRObject, bool wasBehaviorNetworkScript, ushort removedBehaviorID)
    {
        dRObject.RemoveBehavior(wasBehaviorNetworkScript, removedBehaviorID);
        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(dRObject.GetID());
            writer.Write(wasBehaviorNetworkScript);
            writer.Write(removedBehaviorID);
            using (Message msg = Message.Create(ServerTags.RemoveBehavior, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
    }
    public void UpdateObjectName(DRObject dRObject, string newName)
    {
        if (!dRObject.SetName(newName))
        {
            Debug.LogError("Failed to set name for " + dRObject.GetID() + " with " + newName);
            return;
        }
        int msgLen = sizeof(ushort) + 1 + newName.Length;
        using(DarkRiftWriter writer = DarkRiftWriter.Create(msgLen))
        {
            writer.Write(dRObject.GetID());
            DRCompat.WriteStringSmallerThen255(writer, newName);
            Debug.Log("Updating name for " + dRObject.GetID() + " name: " + newName);
            using (Message msg = Message.Create(ServerTags.SetObjectName, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
    }
    public bool CanTakeOwnership(DRObject ourObject, DRObject otherObject)
    {
        uint ourTime = ourObject.OwnershipTime;
        uint otherTime = otherObject.OwnershipTime;
        bool otherIsGreater;
        uint delta = ExtensionMethods.SafeDifference(otherTime, ourTime, out otherIsGreater);

        //// If there's a huge difference, then a rollover likely happened
        //// so whichever value is smaller has precedence
        if (delta > int.MaxValue)
        {
            Debug.Log("Likely rollover other: " + otherTime + " ourTime: " + ourTime);
            // If their value is lower, they have priority
            if (!otherIsGreater)
                return false;
        }

        //// If the times are the same (unlikly, but still) then whomever has the lower viewID
        //// takes prcedence
        if (otherTime == ourTime)
        {
            uint otherID = otherObject.OwnerID;
            Debug.Log("Deciding ownership based on owner ID. Other: " + otherID + " ours: " + OurID);
            return OurID < otherID;
        }

        //// Otherwise, ownership goes to whomever has the higher time (and thus more recent interaction)
        return !otherIsGreater;
    }
    public void TakeOwnership(DRObject objectToOwn, uint timeOfOwnership)
    {
        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(objectToOwn.GetID());
            writer.Write(timeOfOwnership);
            using (Message msg = Message.Create(ServerTags.OwnershipChange, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
    }
    public ushort GetLastSentGrabState(ushort objectID)
    {
        ushort ret;
        if (_lastSentGrabOwners.TryGetValue(objectID, out ret))
            return ret;
        return ushort.MaxValue;
    }
    public bool TryGrabObject(DRObject dRObject, DRUser.GrabbingBodyPart bodyPart, Vector3 relPos, Quaternion relRot)
    {
        _lastSentGrabOwners[dRObject.GetID()] = OurID;
        relPos.ToVec3(_workingVector3);
        relRot.ToQuat(_workingQuat);
        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(dRObject.GetID());
            writer.Write((byte)bodyPart);
            writer.Write(_workingVector3);
            writer.Write(_workingQuat);
            using (Message msg = Message.Create(ServerTags.GrabObject, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
        //Debug.Log("Requested grab " + dRObject.GetID());
        // Now that we've grabbed it, presume that we have the ownership
        dRObject.SetAnticipatedGrabbedBy(OurID);
        dRObject.SetAnticipatedOwner(OurID);
        return true;
    }
    public bool ReleaseGrab(DRObject dRObject)
    {
        _lastSentGrabOwners[dRObject.GetID()] = DRObject.NoneGrabbing;
        if (dRObject.GrabbedBy != OurID) // This is the anticipated grabbedBy, so this check should be fine
        {
            Debug.LogWarning("Can't ungrab, we aren't grabbing!");
            return false;
        }
        uint releaseTime = DarkRiftPingTime.Instance.ServerTime;
        dRObject.SetAnticipatedOwnershipTime(releaseTime);
        dRObject.SetAnticipatedGrabbedBy(DRObject.NoneGrabbing);

        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(dRObject.GetID());
            writer.Write(releaseTime);
            using (Message msg = Message.Create(ServerTags.ReleaseGrabObject, writer))
            {
                Dispatcher.SendMessage(msg, SendMode.Reliable);
                GameRecordingManager.Instance.OnSentMessage(msg, SendMode.Reliable);
            }
        }
        //Debug.Log("Requested release grab " + dRObject.GetID());
        return true;
        //    if (_lastSentGrabOwners.TryGetValue(instanceID, out int expected))
        //    {
        //        Debug.Log("Using expected, even though we have no existing. This should be very rare");
        //        var expectedTable = new ExitGames.Client.Photon.Hashtable();
        //        PhotonNetwork.CurrentRoom.SetCustomProperties(hashtable, expectedTable);
        //        Debug.Log("Setting grab for " + instanceID + " to: " + 0 + " expecting " + expected);
        //    }
        //    else
        //    {
        //        //Debug.Log("Requesting ungrab without CAS");
        //        PhotonNetwork.CurrentRoom.SetCustomProperties(hashtable);
        //        Debug.Log("Setting grab for " + instanceID + " to: " + 0 + " expecting " + "nothing");
        //    }
        //}
        //_lastSentGrabOwners[instanceID] = 0;

        //return true;
    }
    public void SetPlayPause(bool isPlaying)
    {
        using(DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(isPlaying);
            using (Message msg = Message.Create(ServerTags.PlayPause, writer))
                SendReliableMessage(msg);
        }
    }
    public DarkRiftAudioClient GetAudioClient()
    {
        return AudioClient;
    }
    public DRGameState GetFullCurrentGameState()
    {
        DRGameState gameState = new DRGameState();
        // Add all scripts
        var userScripts = UserScriptManager.Instance.GetAllUserScripts();
        for(int i = 0; i < userScripts.Count; i++)
        {
            var userScript = userScripts[i];
            gameState.AddUserScript(userScript.GetID(), userScript);
        }
        // Add all scene materials
        var materials = SceneMaterialManager.Instance.GetAllSceneMaterials();
        for(int i = 0; i < materials.Count; i++)
        {
            SceneMaterial mat = materials[i];
            gameState.AddMaterial(mat.GetID(), mat.GetDRMaterial());
        }
        // Add the materials that don't yet have a SceneMaterial for
        var pendingMaterials = SceneMaterialManager.Instance.GetAllDRMaterialsPendingSceneMaterial();
        for(int i = 0; i < pendingMaterials.Count; i++)
        {
            DRMaterial mat = pendingMaterials[i];
            gameState.AddMaterial(mat.GetID(), mat);
        }

        // Add all objects
        //TODO force update the state for objects that we own
        var objects = SceneObjectManager.Instance.GetAllSceneObjects();
        for(int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            gameState.AddObject(obj.GetID(), obj.GetDRObject());
        }

        gameState.SetBoardSize(Board.Instance.Width, Board.Instance.Height);
        gameState.SetBoardVisibility(Board.Instance.Visible);
        return gameState;
    }
    private void OnApplicationQuit()
    {
        if (Dispatcher != null)
            Dispatcher.Dispose();
        Dispatcher = null;
        if (AudioClient != null)
            AudioClient.Close();
        AudioClient = null;
    }
    private void Update()
    {
        // If in the editor, and we're compiling, immediately dispose the server connection
#if UNITY_EDITOR
        if(Dispatcher != null
            && (EditorApplication.isCompiling || !Application.isPlaying))
        {
            Debug.LogWarning("Disposing unity client due to recompile");
            Dispatcher.Dispose();
            Dispatcher = null;
        }
#endif
        if (Dispatcher != null)
            Dispatcher.Poll();
    }
}
