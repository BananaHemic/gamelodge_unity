using AdvancedColorPicker;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaterialSettings : GenericSingleton<MaterialSettings>
{
    public GameObject MaterialCardPrefab;
    public RectTransform MaterialCardContainer;
    public CanvasToggle ColorPickerToggle;
    public VerticalLayoutGroup MaterialCardContainingLayout;
    public ContentSizeFitter MaterialCardContainingContentFitter;
    public TMP_Text MaterialNameLabel;

    private SceneObject _sceneObject;
    private readonly List<MaterialCard> _materialCards = new List<MaterialCard>();
    // The info for the color picker
    private MaterialCard _colorPickerMaterialCard;
    private SceneMaterial _colorPickerMaterial;
    private int _colorPickerPropIdx;
    private ColorPicker _colorPicker;

    const string MaterialNameFormat = "Materials for: {0}";
    const string NoObjectText = "Select an object to view its materials.";

    protected override void Awake()
    {
        base.Awake();
        _colorPicker = ColorPickerToggle.GetComponent<ColorPicker>();
        InitForSelectedObject(null);
    }
    public void InitForSelectedObject(SceneObject sceneObject)
    {
        //Debug.Log("materials init for " + (sceneObject == null ? "null" : sceneObject.Name));
        _sceneObject = sceneObject;

        foreach (var matCard in _materialCards)
            GameObject.Destroy(matCard.gameObject);
        _materialCards.Clear();

        if (_sceneObject == null)
        {
            //Debug.Log("No scene object for MaterialSettings");
            ColorPickerToggle.SetOn(false);
            MaterialNameLabel.text = NoObjectText;
            return;
        }
        MaterialNameLabel.text = string.Format(MaterialNameFormat, sceneObject.Name);
        // Init materials
        //Material[] mats = _sceneObject.transform.GetChild(0).GetComponent<MeshRenderer>().materials;
        SceneMaterial[] sceneMaterials = sceneObject.SceneMaterials;

        // This happens when the materials haven't yet loaded
        if (sceneMaterials == null)
        {
            Debug.Log("SceneObject " + sceneObject.GetID() + " does not have materials yet");
            ColorPickerToggle.SetOn(false);
            return;
        }

        for(int i = 0; i < sceneMaterials.Length; i++)
        {
            // Some elements may be null if this client is waiting for info from the server
            SceneMaterial sceneMaterial = sceneMaterials[i];
            if (sceneMaterial == null)
            {
                Debug.LogWarning("Skipping SceneMaterial for #" + i);
                continue;
            }
            Debug.Log("Making material card for " + sceneMaterial.MaterialInfo.Name + " with shader " + sceneMaterial.MaterialInfo.ShaderInfo.Name);

            GameObject cardObj = GameObject.Instantiate(MaterialCardPrefab);
            cardObj.transform.SetParent(MaterialCardContainer, false);

            MaterialCard materialCard = cardObj.GetComponent<MaterialCard>();
            _materialCards.Add(materialCard);
            materialCard.Init(this, sceneMaterial);
        }
    }
    private void OnDisable()
    {
        // Clear out all the stuff we added
        //foreach (var matCard in _materialCards)
        //    GameObject.Destroy(matCard.gameObject);
        //_materialCards.Clear();
        ColorPickerToggle.SetOn(false);
    }
    public void OnColorPickerCloseClicked()
    {
        Debug.Log("User closed color picker");
        ColorPickerToggle.SetOn(false);
    }

    public void OnColorPickerColorChange(Color newColor)
    {
        //Debug.Log("New color " + newColor);
        if (_colorPickerMaterialCard == null)
        {
            Debug.LogWarning("Dropping color change, no mat card");
            return;
        }
        _colorPickerMaterialCard.OnColorChange(_colorPickerPropIdx, newColor);
        // Change the material locally
        _colorPickerMaterial.SetColor(_colorPickerPropIdx, newColor, true, false);
    }

    public void OpenColorClicker(MaterialCard requester, SceneMaterial sceneMaterial, int propIdx, Color color)
    {
        Debug.Log("Will open color picker with color " + color);
        _colorPickerMaterialCard = requester;
        _colorPickerMaterial = sceneMaterial;
        _colorPickerPropIdx = propIdx;
        ColorPickerToggle.SetOn(true);
        _colorPicker.CurrentColor = color;
        Debug.Log("Done opening color picker");
    }
}
