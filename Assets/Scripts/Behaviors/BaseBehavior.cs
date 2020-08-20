using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRift;
using Miniscript;

/// <summary>
/// An instance of a script, either one that's premade in C#, or
/// one that's user created in miniscript. This base class
/// is in charge of writing changes to public properties to
/// the network.
/// </summary>
public abstract class BaseBehavior : MonoBehaviour, IRealtimeObject
{
    protected SceneObject _sceneObject;
    protected BehaviorInfo _behaviorInfo;
    protected SerializedBehavior _serializedBehavior;
    private uint _currentPriority;
    // We need to store what references the script has, so that any display scripts know what they need to set
    private readonly Dictionary<string, SerializedBundleItemReference> _allBundleItemReferences = new Dictionary<string, SerializedBundleItemReference>();
    private readonly Dictionary<string, SerializedSceneObjectORBundleItemReference> _allSceneObjectORBundleItemReferences = new Dictionary<string, SerializedSceneObjectORBundleItemReference>();
    private readonly Dictionary<string, SerializedSceneObjectReference> _allSceneObjectReferences = new Dictionary<string, SerializedSceneObjectReference>();

    public virtual void Init(BehaviorInfo behaviorData, SceneObject sceneObject)
    {
        _behaviorInfo = behaviorData;
        _sceneObject = sceneObject;
        _currentPriority = 0;
        ChildInit();
    }
    /// <summary>
    /// Configure the object to use the behavior.
    /// This may mean adding/getting components
    /// Setting layers/tags, etc. NOTE that this
    /// may be called AFTER SetSerializedBehavior
    /// </summary>
    protected abstract void ChildInit();
    public abstract void WriteCurrentValuesToSerializedBehavior();
    /// <summary>
    /// Reload properties from the serialized behavior. Will
    /// only use new values from the server if we don't have
    /// our own dirty copy. BaseBehavior will call RefreshProperties
    /// after this, if this was due to a change.
    /// If this was during the object's initialization, then this
    /// will run before ChildInit and will not be followed by a RefreshProperties
    /// </summary>
    public abstract void UpdateParamsFromSerializedObject();
    /// <summary>
    /// Allows child classes to additionally configure the
    /// SerializedBehavior. Flag setting should go here
    /// </summary>
    protected virtual void InitSerializedBehavior() { }
    /// <summary>
    /// Notification that the embedded properties have changed
    /// Children should update their internal state. Not
    /// called by the base class during init
    /// </summary>
    public abstract void RefreshProperties();
    public abstract bool DoesRequirePosRotScaleSyncing();
    public abstract bool DoesRequireCollider();
    public abstract bool DoesRequireRigidbody();
    public abstract List<ExposedFunction> GetFunctions();
    public abstract List<ExposedVariable> GetVariables();
    public abstract List<ExposedEvent> GetEvents();
    public virtual void OnModelLoaded() {}

    public void CreateSerializedBehavior(DRObject drObject)
    {
        _serializedBehavior = new SerializedBehavior(_behaviorInfo.IsNetworkedScript(), _behaviorInfo.BehaviorID);
        InitSerializedBehavior();
        WriteCurrentValuesToSerializedBehavior();
        Debug.Log("During add, has flags " + _serializedBehavior.AreAnyFlagsSet);
        DarkRiftConnection.Instance.AddBehaviorToObject(drObject, _serializedBehavior);
        _serializedBehavior.OnServerCreation();
    }
    protected void AddBundleItemReference(SerializedBundleItemReference bundleItemReference)
    {
        // Removed as this can never happen
        //if (_allBundleItemReferences.ContainsKey(bundleItemReference.ReferenceName))
        //{
        //    Debug.LogError("Attempted to double add reference named " + bundleItemReference.ReferenceName);
        //    return;
        //}
        _allBundleItemReferences.Add(bundleItemReference.VariableName, bundleItemReference);
    }
    protected void AddSceneObjectReference(SerializedSceneObjectReference sceneObjectReference)
    {
        _allSceneObjectReferences.Add(sceneObjectReference.VariableName, sceneObjectReference);
    }
    protected void AddSceneObjectOrBundleItemReference(SerializedSceneObjectORBundleItemReference orReference)
    {
        _allSceneObjectORBundleItemReferences.Add(orReference.VariableName, orReference);
    }
    public SerializedBundleItemReference GetBundleItemReference(string referenceName)
    {
        return _allBundleItemReferences[referenceName];
    }
    public SerializedSceneObjectReference GetSceneObjectReference(string referenceName)
    {
        return _allSceneObjectReferences[referenceName];
    }
    public SerializedSceneObjectORBundleItemReference GetSceneObjectORBundleItemReference(string referenceName)
    {
        return _allSceneObjectORBundleItemReferences[referenceName];
    }
    public BehaviorInfo GetBehaviorInfo()
    {
        return _behaviorInfo;
    }
    public void SetSerializedBehavior(SerializedBehavior serializedBehavior, bool refreshProperties)
    {
        _serializedBehavior = serializedBehavior;
        UpdateParamsFromSerializedObject();
        if(refreshProperties)
            RefreshProperties();
    }
    public virtual SerializedBehavior GetSerializedObject()
    {
        return _serializedBehavior;
    }
    public SceneObject GetSceneObject()
    {
        return _sceneObject;
    }
    public void OnPropertiesChange(bool updateServer, bool updateReliable)
    {
        RefreshProperties();
        // If this property change came from the server, then all we need
        // to do is refresh
        if (!updateServer)
            return;
        // Update the local-copy portion of SerializedBehavior
        WriteCurrentValuesToSerializedBehavior();
        // If this a network Miniscript file, or local C# script, and if this behavior needs flags
        bool hasFlags = _serializedBehavior.AreAnyFlagsSet;
        byte header = SerializedBehavior.MakeHeader(_behaviorInfo.IsNetworkedScript(), hasFlags);
        //Debug.Log("Updating property, reliable: " + updateReliable);
        // If this is a reliable update (meaning we just stopped dragging)
        // then we send out an update with everything that is unacked
        if (updateReliable)
        {
            DarkRiftWriter writer = DarkRiftWriter.Create();
            // The object ID
            writer.Write(_sceneObject.GetID());
            writer.Write(header);
            // The behavior ID
            writer.Write(_behaviorInfo.BehaviorID);
            // The key/value data
            _serializedBehavior.WriteUpdateToServer(writer, hasFlags, SendMode.Reliable);
            Debug.Log("Sending behavior update, has flags " + hasFlags);
            RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.UpdateBehavior, writer);

            // Notify the behavior that we just sent out an update
            _serializedBehavior.OnUpdateSentToServer();
            // Priority is 0, we just sent out our data
            _currentPriority = 0;
            return;
        }
        // If we have some different parameters then the server,
        // queue an update
        if (!_serializedBehavior.NeedsUpdateToServer())
        {
            _currentPriority = 0;
            return;
        }
        _currentPriority = ExtensionMethods.ClampedAdd(_currentPriority, RealtimeNetworkUpdater.Instance.PriorityIncreasePerFrameBehavior);
        DarkRiftWriter unreliableWriter = DarkRiftWriter.Create();
        // The object ID
        unreliableWriter.Write(_sceneObject.GetID());
        // If this a network Miniscript file, or local C# script
        unreliableWriter.Write(header);
        // The behavior ID
        unreliableWriter.Write(_behaviorInfo.BehaviorID);
        // The key/value data
        Debug.Log("Sending behavior update, has flags " + hasFlags);
        _serializedBehavior.WriteUpdateToServer(unreliableWriter, hasFlags, SendMode.Unreliable);

        // Enqueue a unreliable network update for this behavior
        RealtimeNetworkUpdater.Instance.EnqueueUnreliableUpdate(this, unreliableWriter, ServerTags.UpdateBehavior, _currentPriority);
    }
    /// <summary>
    /// Load the miniscript intrinsics. Child must
    /// make sure to check if it has statically done
    /// this before
    /// </summary>
    //protected abstract void LoadIntrinsics();

    // Not used, this script uses push-mode realtime updates
    public bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority)
    {
        throw new System.NotImplementedException();
    }

    // when the system has sent out an update
    public void ClearPriority()
    {
        _currentPriority = 0;
        _serializedBehavior.OnUpdateSentToServer();
    }

    // TODO we should notify children if we're destroying the whole object, to make cleanup a bit faster
    public abstract void Destroy();
}
