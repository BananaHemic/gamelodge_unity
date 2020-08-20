using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HierarchyObjectElement : MonoBehaviour
{
    public TextMeshProUGUI NameText;

    public ushort ObjectID { get {
            if (_sceneObject == null)
                return ushort.MaxValue;
            return _sceneObject.GetID();
        } }
    private SceneObject _sceneObject;

    public void Init(SceneObject sceneObject)
    {
        _sceneObject = sceneObject;
        NameText.text = sceneObject.Name;
    }
    public void RefreshName()
    {
        NameText.text = _sceneObject.Name;
    }
    public void OnClick()
    {
        ObjectPanel.Instance.OnHierarchyObjectClicked(_sceneObject);
    }
    public void DeInit()
    {
        _sceneObject = null;
    }
}
