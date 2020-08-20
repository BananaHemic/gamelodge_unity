using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json.Linq;
using RLD;
using System;
using DarkRift;

public class SceneObjectManager : GenericSingleton<SceneObjectManager>
{
    public Transform RootObject;
    public GameObject UIBaseObject;

    public static Action<SceneObject> OnSceneObjectAdded;
    public static Action<SceneObject> OnSceneObjectRemoved;
    public static Action<SceneObject> OnSceneNameChange;

    const int NumExpectedSceneObjects = 512;

    // SceneObject objectID -> reference
    private readonly Dictionary<ushort, SceneObject> _id2SceneObject = new Dictionary<ushort, SceneObject>(NumExpectedSceneObjects);
    // SceneObject name -> reference
    // names are currently not unique, so this will find the first object named accordingly
    private readonly Dictionary<string, SceneObject> _name2SceneObject = new Dictionary<string, SceneObject>(NumExpectedSceneObjects);
    private readonly Dictionary<string, int> _nextNumForSceneObjectsWithBaseName = new Dictionary<string, int>(32);
    private readonly List<SceneObject> _allSceneObjects = new List<SceneObject>(NumExpectedSceneObjects);
    private readonly StringBuilder _workingSB = new StringBuilder();
    private Vec3 _workingVec3 = new Vec3();
    private Quat _workingQuat = new Quat();

    private IEnumerator Start()
    {
        yield return null;
        // Wait for RLD to be init
        while (RTObjectGroupDb.Get == null)
            yield return null;
        // Mark the top object as something that should not be selected
        RTObjectGroupDb.Get.Add(RootObject.gameObject);
        RTScene.Get.SetRootObjectIgnored(UIBaseObject, true);
    }
    public void LocallyClearAllSceneObjects()
    {
        //Debug.Log("Removing all " + _allSceneObjects.Count + " SceneObjects");
        BuildGrabbableManager.Instance.ClearAllBuildGrababbles();
        foreach (var sceneObject in _allSceneObjects)
            Destroy(sceneObject.gameObject);
        _allSceneObjects.Clear();
        _id2SceneObject.Clear();
        _name2SceneObject.Clear();
    }
    public List<SceneObject> GetAllSceneObjects()
    {
        return _allSceneObjects;
    }
    public bool GetSceneObjectByName(string name, out SceneObject sceneObject)
    {
        if (string.IsNullOrEmpty(name))
        {
            sceneObject = null;
            return false;
        }
        return _name2SceneObject.TryGetValue(name, out sceneObject);
    }
    public bool TryGetSceneObjectByID(ushort objectID, out SceneObject sceneObject)
    {
        return _id2SceneObject.TryGetValue(objectID, out sceneObject);
    }
    public string GetUniqueObjectName(string baseName)
    {
        if (!_name2SceneObject.ContainsKey(baseName))
        {
            //Debug.Log("immediately available name: \"" + baseName + "\"");
            return baseName;
        }
        baseName = baseName.RemoveEndNumbers();
        // We use a dictionary here to get what number
        // we should start checking from, to stop from
        // making too many strings
        if (!_nextNumForSceneObjectsWithBaseName.TryGetValue(baseName, out int num))
            num = 1;
        const int MaxTries = 1000;
        //Debug.Log("base name: " + baseName);
        for(int i = 0; i < MaxTries; i++)
        {
            _workingSB.Clear();
            _workingSB.Append(baseName);
            _workingSB.Append(num++);
            string potential = _workingSB.ToString();
            if (!_name2SceneObject.ContainsKey(potential))
            {
                _nextNumForSceneObjectsWithBaseName[baseName] = num;
                //Debug.Log("Using name \"" + potential + "\"");
                return potential;
            }
        }
        throw new Exception("Out of numbers for scene object!");
        //return "OutOfNames";
    }
    public SceneObject UserAddObject(string bundleID, string modelName, ushort modelIndex, Vector3 spawnPos, Quaternion spawnRot)
    {
        //Debug.Log("User added model " + modelName + " from " + bundleID);
        string generatedName = GetUniqueObjectName(modelName);
        // Make the created object serializable
        GameObject spawnedObject = new GameObject(generatedName, typeof(SceneObject));
        SceneObject sceneObject = spawnedObject.GetComponent<SceneObject>();
        spawnedObject.transform.parent = RootObject;
        spawnedObject.transform.localPosition = spawnPos;
        spawnedObject.transform.localRotation = spawnRot;
        spawnedObject.transform.localScale = Vector3.one;

        var drObject = DarkRiftConnection.Instance.CreateDRObject(bundleID, generatedName, modelIndex, spawnedObject.transform, null);
        if (drObject == null)
        {
            Debug.LogWarning("Failed to add object!");
            return null;
        }
        sceneObject.Init(drObject, true, false);
        _id2SceneObject.Add(drObject.GetID(), sceneObject);
        _allSceneObjects.Add(sceneObject);
        _name2SceneObject.Add(drObject.Name, sceneObject);
        if (OnSceneObjectAdded != null)
            OnSceneObjectAdded(sceneObject);
        // Now select the object for the user
        if(VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop
            && RLD.RTObjectSelection.Get != null)
            RLD.RTObjectSelection.Get.SetSelectedObjects(new List<GameObject>() { sceneObject.gameObject }, true);
        return sceneObject;
    }
    /// <summary>
    /// Called by SerializedSceneObject when it exists without being init
    /// This normally happens when the user has duplicated an object
    /// </summary>
    /// <param name="instanceID"></param>
    public void SceneObjectNeedsInit(string gameObjectName, SceneObject sceneObject)
    {
        // the name of a gameobject is the ID
        ushort objectID = ushort.Parse(gameObjectName);
        // The SerializedSceneObject that's created won't have any internal parameters set
        // So we load the existing object, and copy values over
        if (!_id2SceneObject.TryGetValue(objectID, out SceneObject existing))
        {
            Debug.LogError("No existing object with id " + sceneObject.gameObject.name);
            return;
        }
        List<BaseBehavior> existingBehaviors = existing.GetBehaviors();
        List<SerializedBehavior> behaviorsToAdd = new List<SerializedBehavior>(existingBehaviors.Count);
        for(int i = 0; i < existingBehaviors.Count; i++)
        {
            SerializedBehavior existingSerialized = existingBehaviors[i].GetSerializedObject();
            behaviorsToAdd.Add(existingSerialized.Duplicate());
        }
        string generatedName = GetUniqueObjectName(existing.Name);
        var drObject = DarkRiftConnection.Instance.CreateDRObject(existing.BundleID, generatedName, existing.BundleIndex, sceneObject.transform, behaviorsToAdd);
        if (drObject == null)
        {
            Debug.LogWarning("Failed to handle SceneObjectNeedsInit, destroying object");
            Destroy(sceneObject.gameObject);
            return;
        }
        sceneObject.Init(drObject, true, !existing.IsLoadingModel);
        if (!existing.IsLoadingModel)
            sceneObject.SetBundleItem(existing.BundleItem);
        sceneObject.DeserializeBehaviors(drObject);
        _id2SceneObject.Add(drObject.GetID(), sceneObject);
        if(!_name2SceneObject.ContainsKey(drObject.Name))
            _name2SceneObject.Add(drObject.Name, sceneObject);
        _allSceneObjects.Add(sceneObject);
        if (OnSceneObjectAdded != null)
            OnSceneObjectAdded(sceneObject);
    }
    /// <summary>
    /// Called when the server notifies us that an object was created
    /// We ignore if we already have the object
    /// </summary>
    /// <param name="dRObject"></param>
    public void OnServerAddedObject(DRObject drObject, bool isLocalRecordedMessage, bool fromInitialGameState)
    {
        if (isLocalRecordedMessage)
        {
            // We need to use a different object ID if this is from a recording
            // because we might be adding objects while the recording is running
            // During the initial game state, this isn't needed, as the server
            // just takes the objectIDs
            if (!fromInitialGameState)
            {
                GameRecordingManager.Instance.AddObjectFromRecording(drObject);

                // For recorded messages, we need to update the owner and grabber
                // unless this is from the initial gamestate, in which case the
                // GameRecordingManager has already done so
                ushort ownerID = drObject.OwnerID;
                if(ownerID != ushort.MaxValue)
                {
                    if(!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(ownerID, out ushort newOwnerID))
                    {
                        // This will happen if the owner left at the time of the recording
                        Debug.LogWarning("Failed to recording correct ownerID #" + ownerID);
                    }
                    else
                    {
                        drObject.OwnerID = newOwnerID;
                    }
                }
                ushort grabberID = drObject.GrabbedBy;
                if(grabberID != ushort.MaxValue)
                {
                    if(!GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(grabberID, out ushort newGrabberID))
                    {
                        Debug.LogError("Failed to recording correct grabber #" + grabberID);
                        return;
                    }
                    drObject.GrabbedBy = newGrabberID;
                }
                // TODO we should probably also update the ownership time
            }
        }
        //Debug.Log("Object was added: " + drObject.GetID());
        // See if this is new
        if (_id2SceneObject.ContainsKey(drObject.GetID()))
        {
            Debug.Log("Already have that object");
            return;
        }

        SceneObject sceneObject = SceneObject.FromDRObject(drObject, RootObject);
        _id2SceneObject.Add(drObject.GetID(), sceneObject);
        if(!_name2SceneObject.ContainsKey(drObject.Name))
            _name2SceneObject.Add(drObject.Name, sceneObject);
        _allSceneObjects.Add(sceneObject);
        if (OnSceneObjectAdded != null)
            OnSceneObjectAdded(sceneObject);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage && !fromInitialGameState)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(drObject);
                using (Message msg = Message.Create(ServerTags.AddObject, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    /// <summary>
    /// Called when the server notifies us that an object was removed
    /// We ignore if we dont have the object
    /// </summary>
    public void OnServerRemovedObject(ushort objectID, bool isLocalRecordedMessage)
    {
        // Correct the ID if this is from a recording
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get up-to-date ID for remove obj #" + objectID);
                return;
            }
            objectID = runtimeID;
        }
        //Debug.Log("Server object was removed: #" + objectID);
        // See if this was already removed
        SceneObject sceneObject;
        if (!_id2SceneObject.TryGetValue(objectID, out sceneObject))
        {
            //Debug.Log("Already have removed object");
            return;
        }
        string objectName = sceneObject.Name;

        _id2SceneObject.Remove(objectID);
        _name2SceneObject.Remove(objectName);
        _allSceneObjects.RemoveBySwap(sceneObject);
        RecheckName2SceneObject(objectName);
        if (OnSceneObjectRemoved != null)
            OnSceneObjectRemoved(sceneObject);
        Destroy(sceneObject.gameObject);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                using (Message msg = Message.Create(ServerTags.RemoveObject, writer))
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
            }
        }
    }
    /// <summary>
    /// Re-initializes the scene object, using the current values
    /// </summary>
    /// <param name="serializedSceneObject"></param>
    internal void SceneObjectRestored(SceneObject sceneObject)
    {
        // Get the SerializedBehavior for each behavior
        List<SerializedBehavior> serializedBehaviors = null;
        List<BaseBehavior> behaviors = sceneObject.GetBehaviors();
        if(behaviors != null)
        {
            serializedBehaviors = new List<SerializedBehavior>(behaviors.Count);
            foreach (BaseBehavior baseBehavior in behaviors)
                serializedBehaviors.Add(baseBehavior.GetSerializedObject());
        }
        var drObject = DarkRiftConnection.Instance.CreateDRObject(sceneObject.BundleID, sceneObject.Name, sceneObject.BundleIndex, sceneObject.transform, serializedBehaviors);
        if(drObject == null)
        {
            Debug.LogWarning("Failed to restore scene object!");
            return;
        }
        sceneObject.Init(drObject, true, !sceneObject.IsLoadingModel);
        _id2SceneObject.Add(drObject.GetID(), sceneObject);
        if(!_name2SceneObject.ContainsKey(drObject.Name))
            _name2SceneObject.Add(drObject.Name, sceneObject);
        _allSceneObjects.Add(sceneObject);
        if (OnSceneObjectAdded != null)
            OnSceneObjectAdded(sceneObject);
    }
    /// <summary>
    /// Called from OnDeleted when an initialized scene object is deleted
    /// (but not yet fully destroyed, most likely)
    /// </summary>
    /// <param name="sceneObject"></param>
    public void SceneObjectDeleted(SceneObject sceneObject)
    {
        ushort objectID = sceneObject.GetID();
        if (!_id2SceneObject.ContainsKey(objectID))
        {
            Debug.LogWarning("Not removing " + objectID);
            return;
        }
        DarkRiftConnection.Instance.RemoveObject(objectID);
        string objectName = sceneObject.Name;
        _id2SceneObject.Remove(objectID);
        _allSceneObjects.RemoveBySwap(sceneObject);
        _name2SceneObject.Remove(objectName);
        //Debug.Log("Registering delete of " + objectID);
        if (OnSceneObjectRemoved != null)
            OnSceneObjectRemoved(sceneObject);
        RecheckName2SceneObject(objectName);
    }
    public void OnSceneObjectGrabStateChange(ushort objectID, ushort grabber, uint timeReleased)
    {
        if (!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            // This can just mean that a new object was added, and we're receiving the 
            // update to grab before the update from firebase
            Debug.LogWarning("Can't handle grab change, object not found! " + objectID);
            return;
        }
        //Debug.Log("Grab state now: " + grabber + " for " + objectID);
        sceneObject.UpdateGrabState(grabber, timeReleased);
    }
    public void OnSceneObjectOwnershipChange(ushort objectID, ushort newOwner, uint ownershipTime)
    {
        if (!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't handle ownership change, object not found! " + objectID);
            return;
        }
        Debug.Log("Owner for #" + objectID + " now: " + newOwner);
        sceneObject.SetOwner(newOwner, ownershipTime);
    }
    public void OnServerChangeName(ushort userID, ushort objectID, string name)
    {
        if (!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't handle name change, object not found! " + objectID);
            return;
        }

        // If the object that just had it's name changed is the same as
        // the one in the name hash table, update the table
        if (_name2SceneObject.TryGetValue(sceneObject.Name, out SceneObject nameSceneObject)
            && nameSceneObject.GetID() == objectID)
        {
            _name2SceneObject.Remove(sceneObject.Name);
            // See if any other objects are using that name
            RecheckName2SceneObject(sceneObject.Name);
        }
        if (!_name2SceneObject.ContainsKey(name))
            _name2SceneObject.Add(name, sceneObject);
        sceneObject.UpdateName(name, userID == DarkRiftConnection.Instance.OurID, true);

        if (OnSceneNameChange != null)
            OnSceneNameChange(sceneObject);
    }
    public void OnServerObjectPosition(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Position(reader);
                return;
            }
            objectID = runtimeID;
        }

        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't read position, no object " + objectID);
            DRObject.ClearReader_Position(reader);
            return;
        }
        sceneObject.OnServerUpdatePosition(reader, false);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializePos(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_Pos, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectGrabPhysicsUpdate(DarkRiftReader reader, MessageDirection msgDir, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort grabberID;
        if (msgDir == MessageDirection.Server2Client)
            grabberID = reader.ReadUInt16();
        else
            grabberID = GameRecordingManager.Instance.RecordedClientID;

        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReaderGrabPhysicsPosRotVel(reader);
                return;
            }
            objectID = runtimeID;

            if(msgDir == MessageDirection.Server2Client
                && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(grabberID, out ushort newID))
            {
                //Debug.Log("Replacing grab physics ID #" + grabberID + "->" + newID);
                grabberID = newID;
            }
        }

        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't handle GrabPhysics update, no object " + objectID);
            DRObject.ClearReaderGrabPhysicsPosRotVel(reader);
            return;
        }
        sceneObject.OnServerUpdateGrabPhysicsPosRotVel(reader, grabberID, out byte grabbingBodyPart, out Vector3 grabPos, out Quaternion grabRot,
        out Vector3 velWorld, out Vector3 angVelWorld);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(grabberID);
                writer.Write(objectID);
                writer.Write(grabbingBodyPart);
                writer.Write(grabPos.ToVec3(_workingVec3));
                writer.Write(grabRot.ToQuat(_workingQuat));
                writer.Write(velWorld.ToVec3(_workingVec3));
                writer.Write(angVelWorld.ToVec3(_workingVec3));

                using (Message msg = Message.Create(ServerTags.TransformObject_GrabPhysicsPosRotVelAngVel_Recorded, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectRotation(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Rotation(reader);
                return;
            }
            objectID = runtimeID;
        }
        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't read rotation, no object " + objectID);
            DRObject.ClearReader_Rotation(reader);
            return;
        }
        sceneObject.OnServerUpdateRotation(reader, false);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializeRot(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_Rot, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectScale(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Scale(reader);
                return;
            }
            objectID = runtimeID;
        }
        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't read scale, no object " + objectID);
            DRObject.ClearReader_Scale(reader);
            return;
        }
        sceneObject.OnServerUpdateScale(reader);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializeScale(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_Scale, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectPositionRotation(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Position(reader);
                DRObject.ClearReader_Rotation(reader);
                return;
            }
            objectID = runtimeID;
        }
        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't read position/rotation, no object " + objectID);
            DRObject.ClearReader_Position(reader);
            DRObject.ClearReader_Rotation(reader);
            return;
        }
        //Debug.Log("Recv posrot");
        sceneObject.OnServerUpdatePosition(reader, false);
        sceneObject.OnServerUpdateRotation(reader, false);
        // We know it's not at rest, b/c otherwise we would have used tag PosRot_Rest
        sceneObject.OnServerUpdateAtRest(false);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializePos(writer);
                sceneObject.GetDRObject().SerializeRot(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_PosRot, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectPositionRotation_Rest(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Position(reader);
                DRObject.ClearReader_Rotation(reader);
                return;
            }
            objectID = runtimeID;
        }

        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogError("Can't read pos rot at rest, no object " + objectID);
            DRObject.ClearReader_Position(reader);
            DRObject.ClearReader_Rotation(reader);
            return;
        }
        //Debug.Log("Recv posrot rest");
        sceneObject.OnServerUpdatePosition(reader, true);
        sceneObject.OnServerUpdateRotation(reader, true);
        sceneObject.OnServerUpdateAtRest(true);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializePos(writer);
                sceneObject.GetDRObject().SerializeRot(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_PosRot_Rest, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerObjectPosRotVelAngVel(DarkRiftReader reader, SendMode sendMode, bool isLocalRecordedMessage)
    {
        ushort objectID = reader.ReadUInt16();
        // If this is a recording, we have to transform the ID
        // from the recorded version to the runtime version
        if (isLocalRecordedMessage)
        {
            if(!GameRecordingManager.Instance.TryGetObjectIDFromRecordedID(objectID, out ushort runtimeID))
            {
                Debug.LogError("Failed to get ID for #" + objectID);
                DRObject.ClearReader_Position(reader);
                DRObject.ClearReader_Rotation(reader);
                DRObject.ClearReader_Velocity(reader);
                DRObject.ClearReader_AngularVelocity(reader);
                return;
            }
            objectID = runtimeID;
        }

        if(!_id2SceneObject.TryGetValue(objectID, out SceneObject sceneObject))
        {
            Debug.LogWarning("Can't read pos/rot/vel, no object " + objectID);
            DRObject.ClearReader_Position(reader);
            DRObject.ClearReader_Rotation(reader);
            DRObject.ClearReader_Velocity(reader);
            DRObject.ClearReader_AngularVelocity(reader);
            return;
        }
        //Debug.Log("Recv posrot velang");
        sceneObject.OnServerUpdatePosition(reader, false);
        sceneObject.OnServerUpdateRotation(reader, false);
        sceneObject.OnServerUpdateVelocity(reader);
        sceneObject.OnServerUpdateAngularVelocity(reader);
        sceneObject.OnServerUpdateAtRest(false);

        // If this is a recorded message, we'll want to echo this to the server
        if (isLocalRecordedMessage)
        {
            using(DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(objectID);
                sceneObject.GetDRObject().SerializePos(writer);
                sceneObject.GetDRObject().SerializeRot(writer);
                sceneObject.GetDRObject().SerializeVelocity(writer);
                sceneObject.GetDRObject().SerializeAngularVelocity(writer);
                using (Message msg = Message.Create(ServerTags.TransformObject_PosRotVelAngVel, writer))
                    DarkRiftConnection.Instance.SendMessage(msg, sendMode);
            }
        }
    }
    public void OnServerAddedBehavior(ushort objectID, SerializedBehavior serializedBehavior)
    {
        SceneObject sceneObject;
        if(!_id2SceneObject.TryGetValue(objectID, out sceneObject))
        {
            Debug.LogError("Can't add behavior scene object ID, not found! " + objectID);
            return;
        }
        sceneObject.OnBehaviorAdded(serializedBehavior);
    }
    public void OnServerRemovedBehavior(ushort objectID, bool wasRemovedNetworked, ushort behaviorID)
    {
        SceneObject sceneObject;
        if(!_id2SceneObject.TryGetValue(objectID, out sceneObject))
        {
            Debug.LogError("Can't remove behavior scene object ID, not found! " + objectID);
            return;
        }
        sceneObject.OnBehaviorRemoved(wasRemovedNetworked, behaviorID);
    }
    public void OnServerUpdatedBehavior(ushort objectID, bool isBehaviorNetworkScript, bool hasFlags, ushort behaviorID, DarkRiftReader reader)
    {
        SceneObject sceneObject;
        if(!_id2SceneObject.TryGetValue(objectID, out sceneObject))
        {
            Debug.LogError("Can't update behavior scene object ID, not found! " + objectID);
            // Clear reader
            SerializedBehavior.ClearReaderForUpdate(reader, hasFlags);
            return;
        }
        sceneObject.OnBehaviorUpdate(isBehaviorNetworkScript, hasFlags, behaviorID, reader);
    }
    /// <summary>
    /// Sets an object's final ObjectID
    /// Called when the server has responded to our request to add an object
    /// </summary>
    /// <param name="oldTempID"></param>
    /// <param name="newObjectID"></param>
    public void UpdateObjectID(ushort oldTempID, ushort newObjectID)
    {
        SceneObject sceneObject;
        if(!_id2SceneObject.TryGetValue(oldTempID, out sceneObject))
        {
            Debug.LogError("Can't update scene object ID, not found! " + oldTempID + "->" + newObjectID);
            return;
        }
        _id2SceneObject.Remove(oldTempID);
        sceneObject.SetObjectID(newObjectID);
        _id2SceneObject.Add(newObjectID, sceneObject);
    }
    public bool AreAnyObjectsLoading()
    {
        for(int i = 0; i < _allSceneObjects.Count; i++)
        {
            if(_allSceneObjects[i].IsLoadingModel)
                return true;
        }
        return false;
    }
    // Re-evaluate if there is another scene object by
    // the same name
    private void RecheckName2SceneObject(string name)
    {
        for(int i = 0; i < _allSceneObjects.Count; i++)
        {
            SceneObject candidate = _allSceneObjects[i];
            if(candidate.Name == name)
            {
                _name2SceneObject.Add(name, candidate);
                break;
            }
        }
    }
    private bool _wasPrevGrab;
    //private IEnumerator UpdateGrabs()
    //{
    //    SceneObject sceneObject = _allSceneObjects[0];

    //    sceneObject.TryGrab();
    //    yield return null;
    //    yield return null;
    //    yield return null;
    //    yield return null;
    //    sceneObject.EndGrab();
    //    yield return null;
    //    yield return null;
    //    yield return null;
    //    sceneObject.TryGrab();
    //    yield return null;
    //    sceneObject.EndGrab();
    //}
    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.F10))
        //{
        //    foreach (var obj in _allSceneObjects)
        //        obj.SetObjectOutline(ObjectOutline.OutlineState.BuildHover);
        //}
        //if (Input.GetKeyDown(KeyCode.F9))
        //{
        //    foreach (var obj in _allSceneObjects)
        //        obj.SetObjectOutline(ObjectOutline.OutlineState.Off);
        //}
        //if (Input.GetKeyDown(KeyCode.F10))
        //    _allSceneObjects[0].EndGrab();
        //if (Input.GetKeyDown(KeyCode.F11))
        //    _allSceneObjects[0].TryGrab();
        //if (Input.GetKeyDown(KeyCode.F12))
        //{
        //    StartCoroutine(UpdateGrabs());
        //}
        //if(Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.S))
        //{
        //    Debug.Log("Will ask server to save game state");
        //    using (Message saveMsg = Message.CreateEmpty(ServerTags.SaveGame))
        //        DarkRiftConnection.Instance.SendReliableMessage(saveMsg);
        //}
        /*
        if (Input.GetKeyDown(KeyCode.C))
        {
            //CreateGameRoom();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            Debug.Log("Will load most recent game");
            StartCoroutine(LoadMostRecentGame());
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            SerializeSceneFull();
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            //DeserializeSceneFull();
        }
        */
    }
}
