using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MaterialCard : MonoBehaviour
{
    public GameObject MaterialColorPropertyPrefab;
    public TMP_InputField MaterialNameField;

    private SceneMaterial _sceneMaterial;
    //private readonly List<GameObject> _addedProperties = new List<GameObject>();
    private readonly List<IMaterialProperty> _materialProperties = new List<IMaterialProperty>();
    private readonly Dictionary<int, IMaterialProperty> _propID2MatProp = new Dictionary<int, IMaterialProperty>();

    void Start()
    {
        
    }

    public void Init(MaterialSettings materialSettings, SceneMaterial sceneMaterial)
    {
        _sceneMaterial = sceneMaterial;
        MaterialNameField.text = sceneMaterial.MaterialInfo.Name;

        // Add the material's properties
        for(int i = 0; i < sceneMaterial.MaterialInfo.ShaderInfo.Properties.Count;i++)
        {
            ShaderProperty prop = sceneMaterial.MaterialInfo.ShaderInfo.Properties[i];

            if(prop.PropertyType == ShaderProperty.ShaderPropertyType.Color)
            {
                GameObject colorObj = GameObject.Instantiate(MaterialColorPropertyPrefab);
                colorObj.transform.SetParent(transform, false);
                MaterialColorProperty colorProperty = colorObj.GetComponent<MaterialColorProperty>();
                _materialProperties.Add(colorProperty);
                _propID2MatProp.Add(i, colorProperty);
                colorProperty.Init(materialSettings, this, sceneMaterial, i, prop.Name, prop.Description);
            }
        }
    }

    public void OnOpenClicked()
    {

    }
    public void OnAddClicked()
    {

    }
    public void OnColorChange(int propIdx, Color newColor)
    {
        //Debug.Log("Prop #" + propIdx + " changed");
        IMaterialProperty materialProperty;
        if(!_propID2MatProp.TryGetValue(propIdx, out materialProperty))
        {
            Debug.LogError("Can't change prop ID #" + propIdx);
            return;
        }
        //IMaterialProperty materialProperty = _materialProperties[propIdx];
        materialProperty.ColorChanged(newColor);
    }
}
