using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ModelFolderItem : SceneDraggable
{
    public BundleItem BundleItem { get; private set; }
    public string ItemName { get; private set; }
    public ushort BundleIndex { get; private set; }
    public SubBundle.SubBundleType SubBundleType { get; private set; }
    public GameObject LoadingIcon;

    private Image _image;
    private Transform _originalTransformParent;
    private int _siblingIndexOnStartDrag;
    // The box around the object, which we use until the
    // model is loaded
    private GameObject _placeholderBox;
    // the model for the object used to show the user
    // where the model would land and what it looks like
    private GameObject _placeholderModel;

    private bool _wasHittingTable;
    private Vector3 _lastTablePos = Vector3.zero;
    private Quaternion _lastTableRot = Quaternion.identity;
    private bool _holdingControlLock = false;
    private uint _loadID;
    private bool _isLoading;

    void Awake()
    {
        _image = GetComponent<Image> ();
    }
    public void Init(string name, BundleItem bundleItem, ushort bundleIndex)
    {
        _image.sprite = null;
        ItemName = name;
        BundleItem = bundleItem;
        BundleIndex = bundleIndex;
        SubBundleType = bundleItem.ContainingSubBundle.TypeOfSubBundle;
        _originalTransformParent = transform.parent;
        if (_isLoading)
            Debug.LogError("ModelFolderItem init while still loading! Loading #" + _loadID);

        if (SubBundleType == SubBundle.SubBundleType.ScriptableObject)
        {
            //Debug.Log("Loading SO image ", this);
            _loadID = ushort.MaxValue;
            _isLoading = false;
            LoadingIcon.SetActive(false);
            SetSprite(FileCache.Instance.GetImageSprite("document"));
        }
        else
        {
            LoadingIcon.SetActive(true);
            _isLoading = true;
            _loadID = BundleManager.Instance.LoadPreviewImage(bundleItem.ContainingSubBundle.ContainingBundle, bundleItem.Address, SetImage);
        }
    }
    private void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
        _image.enabled = true;
    }
    public void SetImage(Texture2D image)
    {
        _isLoading = false;
        LoadingIcon.SetActive(false);

        if (image == null)
        {
            Debug.LogError("Null texture for bundle " + BundleItem.Address + " model " + ItemName + " type " + SubBundleType);
            return;
        }
        Sprite sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
        SetSprite(sprite);
    }
    protected override void OnDragBegin()
    {
        _siblingIndexOnStartDrag = transform.GetSiblingIndex();
        AssetPanel.Instance.GetDummyModelFolderItem(_originalTransformParent, _siblingIndexOnStartDrag);
        transform.SetParent(AssetPanel.Instance.ItemDragContainer, true);
        _wasHittingTable = false;
        // Get a box to show the user where the object would land, and begin
        // loading the actual model
        if (_placeholderBox == null)
        {
            _placeholderBox = PlaceholderManager.Instance.LoadPlaceholderBox(null, BundleItem, GLLayers.IgnoreSelectLayerNum);
            _placeholderBox.SetActive(false);
            PlaceholderManager.Instance.LoadPlaceholderModel(BundleItem, OnLoadedModel);
        }
    }
    public override void DrawDragOnUI(Vector3 mousePosition)
    {
        GameObject model = _placeholderModel != null ? _placeholderModel : _placeholderBox;
        model.SetActive(false);
        _image.enabled = true;
        transform.position = mousePosition;
        transform.localRotation = Quaternion.identity;
        _wasHittingTable = false;
    }
    public override void DrawDragOnTable(Vector3 mousePosition)
    {
        ModelAABB aabb = BundleItem.AABBInfo;
        if (aabb.IsValid)
        {
            // Move the object up so that it lays on the tabletop
            var delta = new Vector3(0, aabb.Extents.y - aabb.Center.y, 0);
            mousePosition += delta;
            // If this is a really flat object, then add a bit to
            // where it lands on the table. This isn't a perfect solution,
            // as some stuff like the damaged floors from the dungeon pack
            // still artifact with the floor, even though they're around 30mm
            if (aabb.Extents.y < 0.001f)
            {
                //Debug.Log("Adding to mousePosition");
                mousePosition += new Vector3(0, 0.001f, 0);
            }
        }
        else
            Debug.Log("AABB is not valid?");

        GameObject model = _placeholderModel != null ? _placeholderModel : _placeholderBox;
        model.SetActive(true);
        model.transform.position = mousePosition;
        float controllerX = UIManager.Instance.CursorController.GetRotateObjectAxis().x;
        if (!_holdingControlLock)
            _holdingControlLock = ControlLock.Instance.TryLock(ControlLock.ControlType.XJoystick_Right);
        if(_holdingControlLock)
            model.transform.Rotate(Vector3.up * controllerX * UIManager.Instance.RotateSpeedMax * TimeManager.Instance.RenderUnscaledDeltaTime);
        _image.enabled = false;
        _wasHittingTable = true;
        _lastTablePos = mousePosition;
        _lastTableRot = _placeholderModel != null ? _placeholderModel.transform.localRotation : Quaternion.identity;
    }
    public override void DrawDragOnSkybox(Vector3 mousePosition, Quaternion rotation)
    {
        GameObject model = _placeholderModel != null ? _placeholderModel : _placeholderBox;
        model.SetActive(false);
        _image.enabled = true;
        transform.position = mousePosition;
        transform.rotation = rotation;
        _wasHittingTable = false;
    }
    protected override void OnDragEnd()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.SetParent(_originalTransformParent);
        transform.SetSiblingIndex(_siblingIndexOnStartDrag);
        AssetPanel.Instance.ReturnDummyModelFolderItem();
        _image.enabled = true;
        if (_holdingControlLock)
            ControlLock.Instance.ReturnLock(ControlLock.ControlType.XJoystick_Right);
        _holdingControlLock = false;

        // See if it was dragged into the game view
        if (_wasHittingTable)
        {
            Debug.Log("Spawning " + ItemName + " from " + BundleItem.ContainingSubBundle.ContainingBundle + " #" + BundleIndex + " at " + _lastTablePos + " rot " + _lastTableRot.eulerAngles);
            SceneObjectManager.Instance.UserAddObject(BundleItem.ContainingSubBundle.ContainingBundle, ItemName, BundleIndex, _lastTablePos, _lastTableRot);
        }
        if(_placeholderBox != null)
            PlaceholderManager.Instance.ReturnPlaceholderBox(_placeholderBox);
        _placeholderBox = null;
        if (_placeholderModel != null)
            PlaceholderManager.Instance.ReturnPlaceholderModel(_placeholderModel);
        _placeholderModel = null;
        _lastTablePos = Vector3.zero;
        _lastTableRot = Quaternion.identity;
    }
    private void OnLoadedModel(GameObject model)
    {
        //Debug.Log("Model rot " + model.transform.localRotation);
        _lastTableRot = _lastTableRot * model.transform.localRotation;
        if (_placeholderBox != null)
            PlaceholderManager.Instance.ReturnPlaceholderBox(_placeholderBox);
        _placeholderBox = null;

        if (!_isDragging)
        {
            Debug.LogWarning("Model loaded when not dragging");
            PlaceholderManager.Instance.ReturnPlaceholderModel(model);
            return;
        }
        //Debug.Log("Model loaded for model folder item");
        _placeholderModel = model;
        _placeholderModel.SetActive(false);
    }
    void OnDisable()
    {
        if (_placeholderBox != null)
            PlaceholderManager.Instance.ReturnPlaceholderBox(_placeholderBox);
        _placeholderBox = null;
        if (_placeholderModel != null)
            PlaceholderManager.Instance.ReturnPlaceholderModel(_placeholderModel);
        _placeholderModel = null;
        if (_isLoading)
        {
            Debug.Log("Cancelling ModelFolderItem image load #" + _loadID);
            BundleManager.Instance.CancelLoad(_loadID);
            _isLoading = false;
        }
    }
}
