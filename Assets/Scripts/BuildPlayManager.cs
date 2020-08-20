using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRift;

public class BuildPlayManager : GenericSingleton<BuildPlayManager>, IComparer<SpawnPointBehavior>
{
    public Transform UserTransform;

    public bool IsSpawnedInPlayMode { get; private set; }

    // All the available spawn points
    private readonly List<SpawnPointBehavior> _allSpawns = new List<SpawnPointBehavior>(16);
    private readonly List<int> _workingPotentialSpawns = new List<int>(8);
    // Info for where we should return the camera to after the user leaves play mode
    private bool _hasPrevBuildMode;
    private Vector3 _prevBuildModePos;
    private Quaternion _prevBuildModeRot;
    private Vector3 _prevBuildModeScale;
    // Info for determining which spawn to next use
    private uint _nextSpawnIdx = 0;
    private bool _spawnListNeedsResort = false;

    public void SetSpawnedPlayModeTesting(bool spawnedPlay)
    {
#if !UNITY_EDITOR
        Debug.LogError("spawned mode testing used in build!");
#endif
        IsSpawnedInPlayMode = spawnedPlay;
    }
    /// <summary>
    /// Called from the server when the host has decided where we
    /// should spawn.
    /// </summary>
    /// <param name="spawnInfo"></param>
    public void OnReceivedSpawnInfo()
    {
        if(Orchestrator.Instance.CurrentMode != Orchestrator.Modes.PlayMode)
        {
            Debug.LogWarning("Received spawn info when not in play mode");
            return;
        }
        IsSpawnedInPlayMode = true;
        Debug.Log("Recv spawn, setting active");
    }
    /// <summary>
    /// Called when we're the host, and the server needs us to tell a user where
    /// they should be spawning
    /// </summary>
    /// <param name="clientID"></param>
    public void OnServerRequestedSpawnInfoForUser(ushort clientID)
    {
        Debug.Log("Handling spawn info request");
        var spawnPoint = GetSpawnPointToUse(clientID);
        Vector3 pos;
        Quaternion rot;
        string avatarBundleID;
        ushort avatarBundleIndex;
        ushort objectToUse;

        if(spawnPoint != null)
        {
            pos = spawnPoint.transform.position;
            rot = spawnPoint.transform.rotation;

            // The spawn point can either reference a bundle item,
            // or an instance within the scene
            var reference = spawnPoint.GetAvatarReference();
            if(reference.CurrentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.BundleItem)
            {
                Debug.Log("Using bundle item spawn");
                avatarBundleID = spawnPoint.GetAvatarReference().BundleID;
                avatarBundleIndex = spawnPoint.GetAvatarReference().BundleIndex;
                objectToUse = ushort.MaxValue;

                if(string.IsNullOrEmpty(avatarBundleID))
                {
                    // If the spawn point avatar is set to default, use
                    // the dummy prefab
                    avatarBundleID = BuiltinAssetManager.BuiltinBundleID;
                    avatarBundleIndex = BuiltinAssetManager.DummyPrefabID;
                    Debug.LogWarning("Spawn point using default avatar");
                }
            }
            else
            {
                Debug.Log("Using obj spawn");
                objectToUse = reference.SceneObjectReference.GetID();
                avatarBundleID = null;
                avatarBundleIndex = ushort.MaxValue;
            }
        }
        else
        {
            Debug.Log("Using default spawn");
            // TODO use World Settings for default avatar / spawn point
            pos = Vector3.zero;
            rot = Quaternion.identity;
            avatarBundleID = BuiltinAssetManager.BuiltinBundleID;
            avatarBundleIndex = BuiltinAssetManager.DummyPrefabID;
            objectToUse = ushort.MaxValue;
        }

        // Make the object if needed
        if(objectToUse == ushort.MaxValue)
        {
            string objName = "User#" + clientID;
            SceneObject sceneObject = SceneObjectManager.Instance.UserAddObject(avatarBundleID, objName, avatarBundleIndex, pos, rot);
            objectToUse = sceneObject.GetID();
            Debug.Log("Using spawn point " + (spawnPoint == null ? "null" : spawnPoint.name) + " made obj #" + sceneObject.GetID());
            // Add the needed behaviors
            BaseBehavior baseBehavior = sceneObject.AddBehavior(CSharpBehaviorManager.Instance.CharacterBehaviorInfo, true, true, null);
        }

        DarkRiftWriter writer = DarkRiftWriter.Create();
        writer.Write(clientID);// not strictly needed, but helps keep things safe and clean
        writer.Write(objectToUse);
        RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.PossessObj, writer);
    }
    public int Compare(SpawnPointBehavior x, SpawnPointBehavior y)
    {
        return ExtensionMethods.SafeDifferenceInt(x.SpawnOrder, y.SpawnOrder);
    }
    private SpawnPointBehavior GetSpawnPointToUse(ushort clientID)
    {
        if (_allSpawns.Count == 0)
            return null;
        if (_spawnListNeedsResort)
        {
            //Debug.Log("Resorting");
            _allSpawns.Sort(this);
            _spawnListNeedsResort = false;
        }
        // First, determine if the current spawnIdx is too big
        if(_nextSpawnIdx > _allSpawns[_allSpawns.Count - 1].SpawnOrder)
        {
            Debug.Log("Rolling over spawn order");
            _nextSpawnIdx = 0;
        }
        _workingPotentialSpawns.Clear();
        uint potentialSpawnOrder = uint.MaxValue;
        // We go through the spawns until we find one that has an equal or greater
        // spawn order. If there are multiple that have the same spawn ID, we randomly
        // pick from those
        for(int i = 0; i < _allSpawns.Count; i++)
        {
            SpawnPointBehavior potentialSpawn = _allSpawns[i];
            uint spawnOrder = potentialSpawn.SpawnOrder;
            // SpawnOrder too low, just skip
            if (spawnOrder < _nextSpawnIdx)
                continue;

            // Make sure that this spawn hasn't already been
            // populated
            var avatarRef = potentialSpawn.GetAvatarReference();
            if(avatarRef.CurrentMode == SerializedSceneObjectORBundleItemReference.SerializeMode.SceneObject
                && avatarRef.SceneObjectReference.PossessedBy != ushort.MaxValue)
            {
                Debug.LogWarning("Skipping a potential spawn, as it's been possessed by " + avatarRef.SceneObjectReference.PossessedBy);
                continue;
            }

            // If this is our first potential spawn,
            // we need to note the spawn order we'll be using
            if(potentialSpawnOrder == uint.MaxValue)
                potentialSpawnOrder = spawnOrder;
            else if (spawnOrder > potentialSpawnOrder)
            {
                // We already have a potential, if the current spawn has a larger
                // spawnIdx, then we're done looking
                break;
            }
            _workingPotentialSpawns.Add(i);
        }
        if(_workingPotentialSpawns.Count == 0)
        {
            Debug.LogWarning("Failed to find spawn! Idx: " + _nextSpawnIdx);
            return null;
        }
        SpawnPointBehavior spawnPoint;
        // Otherwise, we need to use a random number to get the spawn that
        // we want to use
        int idx;
        if (_workingPotentialSpawns.Count == 1)
            idx = _workingPotentialSpawns[0];
        else
            idx = Random.Range(0, _workingPotentialSpawns.Count);
        spawnPoint = _allSpawns[idx];
        //Debug.Log("Current order: " + _nextSpawnIdx + " chose one with order: " + spawnPoint.SpawnOrder + " at random");
        if (!spawnPoint.CanSpawnMultipleUsers)
            _nextSpawnIdx = spawnPoint.SpawnOrder + 1;
        return spawnPoint;
    }
    public void RegisterSpawnPoint(SpawnPointBehavior spawnPointBehavior)
    {
        _allSpawns.Add(spawnPointBehavior);
        _spawnListNeedsResort = true;
    }
    public void DeRegisterSpawnPoint(SpawnPointBehavior spawnPointBehavior)
    {
        for(int i = 0; i < _allSpawns.Count; i++)
        {
            SpawnPointBehavior candidate = _allSpawns[i];
            if(candidate == spawnPointBehavior)
            {
                // We use RemoveAt so that the list remains sorted
                _allSpawns.RemoveAt(i);
                return;
            }
        }
        Debug.LogError("Failed to find spawn behavior for " + spawnPointBehavior.name);
    }
    public void SpawnPointOrderChange(SpawnPointBehavior spawnPointBehavior)
    {
        // We just need to resort the list before we next handle a spawn
        _spawnListNeedsResort = true;
    }
    public void EnterPlayingMode()
    {
        // First, we send a message to the server that we're in play mode
        // The server will relay this message to the host who will then let
        // the host and us know where/how to spawn
        RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.SpawnInfoRequest, null);

        _hasPrevBuildMode = true;
        _prevBuildModePos = UserTransform.position;
        _prevBuildModeRot = UserTransform.rotation;
        _prevBuildModeScale = UserTransform.localScale;
        UserTransform.localScale = Vector3.one;
    }
    public void OnLeavingPlayingMode()
    {
        IsSpawnedInPlayMode = false;
        UserDisplay userDisplay = UserManager.Instance.LocalUserDisplay;
        // Notify the server that we're no longer possessing the object that
        // we were possessing
        if (userDisplay.DRUserObj.PossessedObj == ushort.MaxValue)
            Debug.LogWarning("Leaving play mode without possessed object?");
        else
        {
            Debug.Log("Sending depossess of #" + userDisplay.DRUserObj.PossessedObj);
            using (DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(DarkRiftConnection.Instance.OurID);
                using (Message msg = Message.Create(ServerTags.DepossessObj, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
        // Update the local DRUser
        // we do this now, instead of waiting for the server echo
        // because we want to handle fast possess/depossess
        userDisplay.EndPossess();
        userDisplay.DRUserObj.LocalPlayerEnterBuildMode();

        // Return the camera to where it last was in build mode
        if (_hasPrevBuildMode)
        {
            UserTransform.position = _prevBuildModePos;
            UserTransform.rotation = _prevBuildModeRot;
            UserTransform.localScale = _prevBuildModeScale;
        }
    }
}
