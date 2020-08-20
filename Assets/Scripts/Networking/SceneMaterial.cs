using System;
using System.Collections;
using System.Collections.Generic;
using DarkRift;
using UnityEngine;

/// <summary>
/// A networked material.
/// This handles sending and receiving updates
/// to a material.
/// </summary>
public class SceneMaterial : IRealtimeObject
{
    public MaterialInfo MaterialInfo { get; private set; }
    public Material LoadedMaterial { get; private set; }

    private DRMaterial _drMaterial;
    /// <summary>
    /// Converts from the network-sent propertyID to the id used for setting the material
    /// </summary>
    private readonly Dictionary<int, int> _serverPropertyIndex2ShaderID = new Dictionary<int, int>();
    /// <summary>
    /// The listeners who want to be notified once we have our material loaded
    /// </summary>
    private readonly List<Action<Material>> _callbackOnRecvMaterial = new List<Action<Material>>();
    /// <summary>
    /// The color material properties that we're waiting to send
    /// </summary>
    private readonly Dictionary<int, Color> _colorPropertiesPendingSet = new Dictionary<int, Color>();
    /// <summary>
    /// The listeners who want to know what a certain color is
    /// </summary>
    private readonly List<Tuple<int, Action<Color>>> _callbackOnRecvColor = new List<Tuple<int, Action<Color>>>();

    /// <summary>
    /// The data that we're waiting to send out via TCP
    /// This is cleared once we send out a reliable message
    /// </summary>
    private readonly Dictionary<int, Color> _colorPropertiesPendingReliableSend = new Dictionary<int, Color>();

    private bool _isLoadingMaterial;
    private uint _currentPriority = 0;
    private float _lastPropertyUpdateTime;
    private Coroutine _reliablePropertyUpdater;
    private Col3 _workingColor = new Col3();
    // How long we wait for changes to the material to stop until
    // we send out an update
    const float TimeAfterPropertyStillSendReliable = 0.2f;

    public SceneMaterial(MaterialInfo materialInfo, DRMaterial drMaterial)
    {
        MaterialInfo = materialInfo;
        _drMaterial = drMaterial;
        // If the server has changed anything from
        // the default, make sure to load the material
        // and set what needs to be set
        if(_drMaterial.IsDifferentThanDefault())
            GetMaterial(null);
    }

    /// <summary>
    /// Used when we created this with a DRMaterial, but the server asked us
    /// to use a different one instead. Normally, because someone else made an equivalent
    /// DRMaterial at the same time
    /// </summary>
    /// <param name="correctMaterial"></param>
    public void ReplaceDRMaterial(DRMaterial correctMaterial)
    {
        _drMaterial = correctMaterial;
    }
    /// <summary>
    /// We can't send propertyIDs over the network, so we instead send out the index of the property
    /// within the material. We then convert that to a propertyID for efficient procedural settings
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private int PropertyIndexToPropertyID(int index)
    {
        int propertyID;
        if (_serverPropertyIndex2ShaderID.TryGetValue(index, out propertyID))
            return propertyID;
        propertyID = Shader.PropertyToID(MaterialInfo.ShaderInfo.Properties[index].Name);
        //Debug.Log("Creating a propertyID for index " + index + " ID: " + propertyID);
        _serverPropertyIndex2ShaderID.Add(index, propertyID);
        return propertyID;
    }
    public void SetColor(int propertyIndex, Color newColor, bool didWeChange, bool fromServer)
    {
        //if (fromServer)
            //Debug.Log("We got a new color from the server: " + newColor);
        // If the change is from the server, then we only display this if we haven't
        // made any changes since that update. This is because I could send a color change, then receive a color
        // change from someone else, and then receive my change echo'd back. In that case I want to use the color
        // echo'd from the server
        if(fromServer)
        {
            _drMaterial.SetColorProperty(propertyIndex, newColor);
            if (_colorPropertiesPendingReliableSend.ContainsKey(propertyIndex))
            {
                Debug.Log("Dropping color change from server, we have newer version");
                return;
            }
        }

        if(LoadedMaterial != null)
        {
            _drMaterial.SetColorProperty(propertyIndex, newColor);
            int propertyID = PropertyIndexToPropertyID(propertyIndex);
            LoadedMaterial.SetColor(propertyID, newColor);
            //Debug.Log("Set material color " + propertyIndex);
        }
        else
        {
            //Debug.Log("Queueing change of color for id #" + propertyIndex);
            // Queue this property change for when we actually have the material
            _colorPropertiesPendingSet[propertyIndex] = newColor;
            GetMaterial(null);
        }
        if (!fromServer && didWeChange)
        {
            //TODO network the change
            _currentPriority = ExtensionMethods.ClampedAdd(_currentPriority, RealtimeNetworkUpdater.Instance.PriorityIncreasePerFrameMaterial);
            int msgLen = sizeof(ushort) + 1 + Col3.MessageLen;
            DarkRiftWriter unreliableWriter = DarkRiftWriter.Create(msgLen);
            // The object ID
            unreliableWriter.Write(_drMaterial.GetID());
            // The property Index
            unreliableWriter.EncodeInt32(propertyIndex);
            // The color data
            _workingColor.UpdateFrom(newColor);
            unreliableWriter.Write(_workingColor);

            // Enqueue a unreliable network update for this behavior
            RealtimeNetworkUpdater.Instance.EnqueueUnreliableUpdate(this, unreliableWriter, ServerTags.MaterialColorChange, _currentPriority);
            // Keep track that this is something that needs to be eventually sent over TCP
            _colorPropertiesPendingReliableSend[propertyIndex] = newColor;
            _lastPropertyUpdateTime = TimeManager.Instance.RenderUnscaledTime;
            // Start a coroutine for this update if we don't already have one
            if (_reliablePropertyUpdater == null)
                _reliablePropertyUpdater = SceneMaterialManager.Instance.StartCoroutineSceneMaterial(SendOutReliablePropertyAfterTime());
        }
    }
    private IEnumerator SendOutReliablePropertyAfterTime()
    {
        while (TimeManager.Instance.RenderUnscaledTime - _lastPropertyUpdateTime < TimeAfterPropertyStillSendReliable)
            yield return null;

        Debug.Log("Sending out reliable material update");
        _currentPriority = 0;
        // How many properties changed
        int numPropsChanged = _colorPropertiesPendingReliableSend.Count;
        int msgLen = sizeof(ushort) + 1 + numPropsChanged + numPropsChanged * (1 + Col3.MessageLen);
        DarkRiftWriter writer = DarkRiftWriter.Create(msgLen);
        // The object ID
        writer.Write(_drMaterial.GetID());
        writer.EncodeInt32(numPropsChanged);
        foreach(var kvp in _colorPropertiesPendingReliableSend)
        {
            // The propertyIndex
            writer.EncodeInt32(kvp.Key);
            // The color
            _workingColor.UpdateFrom(kvp.Value);
            writer.Write(_workingColor);
        }
        RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.MaterialColorChangeMultiple, writer);
        _colorPropertiesPendingReliableSend.Clear();
        _reliablePropertyUpdater = null;
    }
    public void ClearPriority()
    {
        _currentPriority = 0;
    }
    public void GetMaterial(Action<Material> onDone)
    {
        if(LoadedMaterial != null)
        {
            if(onDone != null)
                onDone(LoadedMaterial);
            return;
        }
        if(onDone != null)
            _callbackOnRecvMaterial.Add(onDone);
        // Load the material
        if (!_isLoadingMaterial)
        {
            _isLoadingMaterial = true;
            BundleManager.Instance.LoadMaterial(this, OnMaterialLoaded);
        }
    }
    private void OnMaterialLoaded(Material material)
    {
        _isLoadingMaterial = false;
        if (LoadedMaterial != null)
            Debug.LogWarning("Double setting LoadedMaterial for " + MaterialInfo.Name);
        LoadedMaterial = material;
        // Apply the properties from the server object
        foreach(var kvp in _drMaterial.GetAllColorProps())
        {
            int propertyID = PropertyIndexToPropertyID(kvp.Key);
            LoadedMaterial.SetColor(propertyID, kvp.Value.ToColor());
        }

        // Apply the properties that we were waiting to send
        foreach(var kvp in _colorPropertiesPendingSet)
            LoadedMaterial.SetColor(kvp.Key, kvp.Value);
        _colorPropertiesPendingSet.Clear();

        // Notify everyone we received the material
        foreach (var callback in _callbackOnRecvMaterial)
        {
            if(callback != null)
                callback(LoadedMaterial);
        }
        _callbackOnRecvMaterial.Clear();
        // Notify everyone that we got our colors
        foreach(var callback in _callbackOnRecvColor)
        {
            if (callback == null)
                continue;

            int propertyIndex = callback.Item1;
            int propertyID = PropertyIndexToPropertyID(propertyIndex);
            Color color = LoadedMaterial.GetColor(propertyID);
            if (callback.Item2 != null)
                callback.Item2(color);
        }
        _callbackOnRecvColor.Clear();
    }
    public void SetObjectID(ushort newObjectID)
    {
        _drMaterial.ObjectID = newObjectID;
        Debug.Log("Have new ID for sceneMaterial " + newObjectID);
    }
    public ushort GetID()
    {
        return _drMaterial.GetID();
    }
    public DRMaterial GetDRMaterial()
    {
        return _drMaterial;
    }
    public void GetColor(int propertyIndex, Action<Color> onLoadedColorFromAsset)
    {
        if(LoadedMaterial != null)
        {
            if(onLoadedColorFromAsset != null)
            {
                int propertyID = PropertyIndexToPropertyID(propertyIndex);
                Color col = LoadedMaterial.GetColor(propertyID);
                onLoadedColorFromAsset(col);
            }
            return;
        }
        else
        {
            _callbackOnRecvColor.Add(new Tuple<int, Action<Color>>(propertyIndex, onLoadedColorFromAsset));
            GetMaterial(null);
        }
    }
    // Materials operate in push mode, there's no need for this method
    public bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority)
    {
        throw new NotImplementedException();
    }
}
