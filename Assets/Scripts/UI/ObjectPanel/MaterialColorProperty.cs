using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MaterialColorProperty : MonoBehaviour, IMaterialProperty
{
    public TextMeshProUGUI PropertyNameText;
    public Button ColorButton;
    public Image ButtonImage;

    private MaterialSettings _materialSettings;
    private MaterialCard _card;
    private SceneMaterial _sceneMaterial;
    /// <summary>
    /// The index of this property in the array
    /// of all properties for this material
    /// </summary>
    private int _propertyIndex;
    private Color _lastColor;

    public void ColorChanged(Color newColor)
    {
        _lastColor = newColor;
        // We don't display opacity on the button because it would be confusing
        newColor.a = 1f;
        ButtonImage.color = newColor;
    }

    public void Init(MaterialSettings materialSettings, MaterialCard card, SceneMaterial sceneMaterial, int propertyIdx, string propertyName, string propertyDescription)
    {
        _materialSettings = materialSettings;
        _card = card;
        _sceneMaterial = sceneMaterial;
        _propertyIndex = propertyIdx;

        // Remove the starting _ from properties
        PropertyNameText.text = propertyName.StartsWith("_") ? propertyName.Substring(1) : propertyName;
        //Debug.Log("Initializing for shader " + propertyName + " description " + propertyDescription + " idx " + propertyIdx + " calc ID " + Shader.PropertyToID(propertyName));
        // Get the color for this button
        sceneMaterial.GetColor(_propertyIndex, OnLoadedColorFromAsset);
        //MaterialManager.Instance.GetMaterialColor(sceneMaterial, _propertyID, OnLoadedColorFromAsset);
    }
    void OnLoadedColorFromAsset(Color color)
    {
        //Debug.Log("Got color from asset: " + color);
        ColorChanged(color);
    }
    public void OnClick()
    {
        //Debug.Log("Color prop clicked");
        _materialSettings.OpenColorClicker(_card, _sceneMaterial, _propertyIndex, _lastColor);
    }
    public int GetPropertyIndex()
    {
        return _propertyIndex;
    }
}
