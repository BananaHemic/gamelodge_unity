using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRift;
using System;
using Miniscript;

public class UserManager : GenericSingleton<UserManager>
{
    public RuntimeAnimatorController DefaultAnimationController;
    public UserDisplay NetworkUserPrefab;
    public GameObject NetworkBuildModelPrefab;
    public GameObject NetworkPlayModelPrefab;

    // The largest ID of all real received users
    // Does not consider recorded user IDs
    public ushort LargestReceivedID { get; private set; }

    public UserDisplay LocalUserDisplay { get; private set; }
    private readonly Dictionary<ushort, UserDisplay> _id2Display = new Dictionary<ushort, UserDisplay>(16);
    private readonly List<UserDisplay> _allUsers = new List<UserDisplay>(16);
    // The users who are running from a recording
    private readonly List<ushort> _recordedUsers = new List<ushort>();

    private static bool _hasInitializedIntrinsics = false;
    private static readonly List<ExposedFunction> _alwaysExposedFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedEvent> _alwaysExposedEvents = new List<ExposedEvent>();

    public ValUser GetLocalValUser()
    {
        return LocalUserDisplay.GetValUser();
    }
    public bool TryGetUserDisplay(ushort playerID, out UserDisplay userDisplay)
    {
        return _id2Display.TryGetValue(playerID, out userDisplay);
    }
    public bool HasUser(ushort playerID)
    {
        return _id2Display.ContainsKey(playerID);
    }
    public void SetRecordedUsers(List<DRUser> recordedUsers)
    {
        if (_recordedUsers.Count != 0)
            Debug.LogError("Double adding recorded users");

        for(int i = 0; i < recordedUsers.Count; i++)
        {
            DRUser user = recordedUsers[i];
            Debug.Log("Adding recorded user #" + user.ID);
            HandleSpawnPlayer(user, false);
        }
    }
    public void RemoveAllRecordedUsers()
    {
        Debug.Log("Removing " + _recordedUsers.Count + " recorded users");
        for(int i = 0; i < _recordedUsers.Count; i++)
            HandleDespawnPlayer(_recordedUsers[i], false, false);
        _recordedUsers.Clear();
    }
    public List<UserDisplay> GetAllUsers()
    {
        return _allUsers;
    }
    public void OnServerUserGrabObject(ushort userID, ushort objectID, DRUser.GrabbingBodyPart bodyPart, Vector3 relPos, Quaternion relRot)
    {
        if(!_id2Display.TryGetValue(userID, out UserDisplay userDisplay))
        {
            Debug.LogError("Failed to handle user grab object, no user #" + userID);
            return;
        }
        userDisplay.OnServerUserGrabObject(objectID, bodyPart, relPos, relRot);
    }
    public void OnServerUserReleaseObject(ushort userID, ushort objectID)
    {
        if(!_id2Display.TryGetValue(userID, out UserDisplay userDisplay))
        {
            Debug.LogError("Failed to handle user release object, no user #" + userID);
            return;
        }
        userDisplay.OnServerUserReleaseObject(objectID);
    }
    public ValUser GetValUser(ushort userID)
    {
        if (!_id2Display.TryGetValue(userID, out UserDisplay userDisplay))
            return null;
        return userDisplay.GetValUser();
    }
    public void OnServerPlayerMovement_Build(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
    {
        ushort playerID;
        if(msgDir == MessageDirection.Server2Client)
        {
            playerID = reader.ReadUInt16();
            // Handle if this is a recorded playback
            if (isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(playerID, out ushort newID))
            {
                //Debug.Log("Updating player movement ID");
                playerID = newID;
            }
        }
        else
        {
            // This is a recording, the ID is the recorded self
            playerID = GameRecordingManager.Instance.RecordedClientID;
        }
        //Debug.Log("Update from " + playerID + " with len " + (reader.Length - reader.Position));
        UserDisplay networkUserDisplay;
        if(!_id2Display.TryGetValue(playerID, out networkUserDisplay))
        {
            Debug.LogWarning("Dropping player movement, not found for #" + playerID);
            DRUser.ClearPlayerMovementMessage_Build(reader);
            return;
        }
        networkUserDisplay.UpdateFromPlayerMovementBuildMessage(reader);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(playerID);
                networkUserDisplay.DRUserObj.SerializePlayerMovement_Build(writer, out byte tag);
                tag = ServerTags.Tag2Recorded(tag);
                using (Message msg = Message.Create(tag, writer))
                    DarkRiftConnection.Instance.SendUnreliableMessage(msg);
            }
        }
        //Debug.Log("build pos move " + msgDir + " len " + (reader.Position - posA));
    }
    public void GL_FixedUpdate(float physicsTimestep)
    {
        for(int i = 0; i < _allUsers.Count; i++)
            _allUsers[i].GL_FixedUpdate();
    }

    public void OnServerUserPose(DarkRiftReader reader, byte tag, MessageDirection msgDir, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort playerID;
        if (msgDir == MessageDirection.Server2Client)
        {
            playerID = reader.ReadUInt16();
            // Handle if player is a recorded user
            if(isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(playerID, out ushort newID))
            {
                //Debug.Log("Replacing user pose ID #" + playerID + "->" + newID);
                playerID = newID;
            }
        }
        else
            playerID = GameRecordingManager.Instance.RecordedClientID;
        UserDisplay userDisplay;
        if(!_id2Display.TryGetValue(playerID, out userDisplay))
        {
            Debug.LogWarning("Dropping player movement, not found for #" + playerID);
            DRUserPose.ClearPose(tag, reader);
            return;
        }
        userDisplay.UpdateFromPoseMessage(reader, tag);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(playerID);
                userDisplay.DRUserObj.UserPose.Serialize(writer, out byte outTag);
                outTag = ServerTags.Tag2Recorded(outTag);
                using (Message msg = Message.Create(outTag, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerPossess(ushort playerID, ushort objectID)
    {
        UserDisplay networkPlayerDisplay;
        if(!_id2Display.TryGetValue(playerID, out networkPlayerDisplay))
        {
            Debug.LogWarning("Dropping spawn info, not found for #" + playerID);
            return;
        }
        networkPlayerDisplay.UpdateFromUserSpawn(objectID);

        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(objectID, out SceneObject possessed))
        {
            Debug.LogError("Failed to handle possess, no obj #" + objectID);
            return;
        }
        possessed.OnPossess(playerID);
    }
    public void OnServerDepossess(ushort playerID)
    {
        UserDisplay networkPlayerDisplay;
        if(!_id2Display.TryGetValue(playerID, out networkPlayerDisplay))
        {
            Debug.LogWarning("Dropping depossess, not found for #" + playerID);
            return;
        }

        if(!SceneObjectManager.Instance.TryGetSceneObjectByID(networkPlayerDisplay.DRUserObj.PossessedObj, out SceneObject deposObj))
        {
            Debug.LogWarning("Failed to handle depossess, no obj #" + networkPlayerDisplay.DRUserObj.PossessedObj);
        }
        else
        {
            deposObj.OnDepossess();
        }

        networkPlayerDisplay.EndPossess();
    }
    public void OnServerPlayerMovement_Play(DarkRiftReader reader, byte tag, MessageDirection msgDir, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort playerID;
        if (msgDir == MessageDirection.Server2Client)
        {
            playerID = reader.ReadUInt16();
            // Handle if player is a recorded user
            if(isLocalRecordedMessage
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(playerID, out ushort newID))
            {
                //Debug.Log("Replacing player ID #" + playerID + "->" + newID);
                playerID = newID;
            }
        }
        else
        {
            playerID = GameRecordingManager.Instance.RecordedClientID;
        }

        //Debug.Log("Update from " + playerID + " with len " + (reader.Length - reader.Position));
        UserDisplay userDisplay;
        if(!_id2Display.TryGetValue(playerID, out userDisplay))
        {
            Debug.LogWarning("Dropping player movement, not found for #" + playerID);
            DRUser.ClearPlayerMovementMessage_Play(reader, tag);
            return;
        }
        userDisplay.UpdateFromPlayerMovementPlayMessage(reader, tag, isLocalRecordedMessage);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(playerID);
                userDisplay.DRUserObj.SerializePlayerMovement_Play(writer, tag);
                tag = ServerTags.Tag2Recorded(tag);
                using (Message msg = Message.Create(tag, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void HandleSpawnPlayer(DRUser user, bool isLocalRecordedMessage)
    {
        if (isLocalRecordedMessage)
        {
            // If this is a recorded message, we need special handling for this player
            // because their ID could now be used by a real player, and we want to know
            // that this player should be removed once we exit recording playback
            ushort newID = GameRecordingManager.Instance.AddRecordedUser(user);
            _recordedUsers.Add(newID);
            Debug.Log("Adding recorded user #" + newID);
        }
        else
        {
            if(user.TypeOfUser == DRUser.UserType.Recorded)
            {
                Debug.Log("Handling spawn of recorded user #" + user.ID);
                _recordedUsers.Add(user.ID);
            }
        }
        Debug.Log("Spawning player #" + user.ID);
        UserDisplay userDisplay = GameObject.Instantiate(NetworkUserPrefab);
        Debug.Log("Added user #" + user.ID);
        _id2Display.Add(user.ID, userDisplay);
        _allUsers.Add(userDisplay);
        if(user.TypeOfUser == DRUser.UserType.Real)
            LargestReceivedID = (ushort)Mathf.Max(LargestReceivedID, user.ID);
        bool isLocalUser = user.ID == DarkRiftConnection.Instance.OurID;
        userDisplay.Init(user, isLocalUser);
        if (isLocalUser)
        {
            if (LocalUserDisplay != null)
                Debug.LogError("Overwriting local user!");
            LocalUserDisplay = userDisplay;
        }

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(user);
                using (Message msg = Message.Create(ServerTags.SpawnPlayer, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    public void HandleDespawnPlayer(ushort playerID, bool isLocalRecordedMessage, bool removeFromRecordedUsers)
    {
        // Handle if this player is a recorded player
        if (isLocalRecordedMessage)
        {
            if (!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(playerID, out ushort newPlayerID))
            {
                Debug.LogError("No recorded playerID #" + playerID);
                return;
            }
            playerID = newPlayerID;
            if(removeFromRecordedUsers)
                _recordedUsers.RemoveBySwap(playerID);
        }
        Debug.Log("Removing player #" + playerID);
        if(!_id2Display.TryGetValue(playerID, out UserDisplay userDisplay))
        {
            Debug.LogError("Failed to remove player model " + playerID);
            return;
        }

        // Remove from recorded users if another user is playing the recording
        if(!isLocalRecordedMessage
            && userDisplay.DRUserObj.TypeOfUser == DRUser.UserType.Recorded
            && removeFromRecordedUsers)
            _recordedUsers.RemoveBySwap(playerID);

        _id2Display.Remove(playerID);
        _allUsers.RemoveBySwap(userDisplay);
        userDisplay.Destroy();
        Destroy(userDisplay.gameObject);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(playerID);
                using (Message msg = Message.Create(ServerTags.DespawnPlayer, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    public GameObject CreateBuildNetworkModel(UserDisplay userDisplay)
    {
        // I don't use pooling here because I worry it won't be safe
        return GameObject.Instantiate(NetworkBuildModelPrefab, userDisplay.transform);
    }
    public GameObject CreatePlayNetworkModel()
    {
        return GameObject.Instantiate(NetworkPlayModelPrefab);
    }
    public List<ExposedFunction> GetAllAlwaysExposedFunctions()
    {
        return _alwaysExposedFunctions;
    }
    public List<ExposedEvent> GetAllAlwaysExposedEvents()
    {
       if(_alwaysExposedEvents.Count == 0)
        {
        }
        return _alwaysExposedEvents;
    }
    public static void InitializeMiniscriptIntrinsics()
    {
        if (_hasInitializedIntrinsics)
            return;
        _hasInitializedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("GetAllPossessedObjects");
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Returns all objects that are currently possessed by a user.", "objList"));
        intrinsic.code = (context, partialResult) => {

            var users = Instance.GetAllUsers();
            ValList list = ValList.Create(users.Count);
            for (int i = 0; i < users.Count; i++)
            {
                UserDisplay user = users[i];
                if (!user.DRUserObj.IsInPlayMode)
                    continue;
                if (user.PossessedBehavior == null)
                    continue;

                SceneObject possessedObj = user.PossessedBehavior.GetSceneObject();
                list.Add(new ValSceneObject(possessedObj));
            }

            return new Intrinsic.Result(list);
		};
        intrinsic = Intrinsic.Create("GetLocalPossessedObject");
        _alwaysExposedFunctions.Add(new ExposedFunction(intrinsic, "Returns the object that the local user is currently possessing", "obj"));
        intrinsic.code = (context, partialResult) => {

            var behave = Instance.LocalUserDisplay.PossessedBehavior;
            if (behave == null)
                return Intrinsic.Result.Null;
            return new Intrinsic.Result(new ValSceneObject(behave.GetSceneObject()));
		};
    }
}
