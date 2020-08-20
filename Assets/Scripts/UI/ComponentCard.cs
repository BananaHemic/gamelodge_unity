using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;

public class ComponentCard : MonoBehaviour
{
    public RectTransform FieldsContainer;
    public TextMeshProUGUI NameText;
    public Image ScriptImg;
    public GameObject RangedFloatPrefab;
    public GameObject FloatPrefab;
    public GameObject UnsignedIntPrefab;
    public GameObject SignedIntPrefab;
    public GameObject BooleanPrefab;
    public GameObject AudioClipFieldPrefab;
    public GameObject AvatarFieldPrefab;
    public GameObject SceneObjectFieldPrefab;
    public GameObject SoundMaterialFieldPrefab;
    public GameObject MaterialFieldPrefab;
    public GameObject DropdownPrefab;

    public BaseBehavior BehaviorInstance { get; private set; }

    private PropertiesAndBehaviors _propertiesAndCode;
    private readonly Dictionary<string, FieldInfo> _fieldInfo = new Dictionary<string, FieldInfo>();
    private readonly List<BasePropertyDisplay> _properties = new List<BasePropertyDisplay>();

    void AddFloatDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);

        // We show floats differently if they're ranged
        RangeAttribute range = (RangeAttribute)fieldInfo.GetCustomAttribute(typeof(RangeAttribute));
        if (range != null)
        {
            GameObject rangedFloatObj = SimplePool.Instance.SpawnUI(RangedFloatPrefab, FieldsContainer);
            RangedFloatPropertyDisplay rangedFloatProperty = rangedFloatObj.GetComponent<RangedFloatPropertyDisplay>();
            rangedFloatProperty.Init(fieldInfo, range.min, range.max, this, instance);
            _properties.Add(rangedFloatProperty as BasePropertyDisplay);
        }
        else
        {
            GameObject floatObj = SimplePool.Instance.SpawnUI(FloatPrefab, FieldsContainer);
            FloatPropertyDisplay floatProperty = floatObj.GetComponent<FloatPropertyDisplay>();
            floatProperty.Init(fieldInfo, this, instance);
            _properties.Add(floatProperty as BasePropertyDisplay);
        }
    }
    void AddUnsignedIntDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject unsignedIntObj = SimplePool.Instance.SpawnUI(UnsignedIntPrefab, FieldsContainer);
        UnsignedIntPropertyDisplay unsignedIntProperty = unsignedIntObj.GetComponent<UnsignedIntPropertyDisplay>();
        unsignedIntProperty.Init(fieldInfo, this, instance);
        _properties.Add(unsignedIntProperty as BasePropertyDisplay);
    }
    void AddSignedIntDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject intObj = SimplePool.Instance.SpawnUI(SignedIntPrefab, FieldsContainer);
        SignedIntPropertyDisplay signedIntProperty = intObj.GetComponent<SignedIntPropertyDisplay>();
        signedIntProperty.Init(fieldInfo, this, instance);
        _properties.Add(signedIntProperty as BasePropertyDisplay);
    }
    void AddBooleanDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject booleanObj = SimplePool.Instance.SpawnUI(BooleanPrefab, FieldsContainer);
        BooleanPropertyDisplay booleanDisplay = booleanObj.GetComponent<BooleanPropertyDisplay>();
        booleanDisplay.Init(fieldInfo, this, instance);
        _properties.Add(booleanDisplay as BasePropertyDisplay);
    }
    void AddEnumDropdownDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject dropdownObj = SimplePool.Instance.SpawnUI(DropdownPrefab, FieldsContainer);
        EnumDropdownPropertyDisplay dropdownDisplay = dropdownObj.GetComponent<EnumDropdownPropertyDisplay>();
        dropdownDisplay.Init(fieldInfo, this, instance);
        _properties.Add(dropdownDisplay as BasePropertyDisplay);
    }
    void AddAudioClipDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject audioClipObj = SimplePool.Instance.SpawnUI(AudioClipFieldPrefab, FieldsContainer);
        AudioClipPropertyDisplay audioPropertyDisplay = audioClipObj.GetComponent<AudioClipPropertyDisplay>();
        audioPropertyDisplay.Init(fieldInfo, this, instance);
        _properties.Add(audioPropertyDisplay);
    }
    void AddPhysSoundMaterialDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject soundMaterialObj = SimplePool.Instance.SpawnUI(SoundMaterialFieldPrefab, FieldsContainer);
        SoundMaterialPropertyDisplay soundMaterialPropertyDisplay = soundMaterialObj.GetComponent<SoundMaterialPropertyDisplay>();
        soundMaterialPropertyDisplay.Init(fieldInfo, this, instance);
        _properties.Add(soundMaterialPropertyDisplay);
    }
    void AddAvatarDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject avatarFieldObj = SimplePool.Instance.SpawnUI(AvatarFieldPrefab, FieldsContainer);
        AvatarPropertyDisplay avatarPropertyDisplay = avatarFieldObj.GetComponent<AvatarPropertyDisplay>();
        avatarPropertyDisplay.Init(fieldInfo, this, instance);
        _properties.Add(avatarPropertyDisplay);
    }
    void AddSceneObjectDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject sceneObjectFieldObj = SimplePool.Instance.SpawnUI(SceneObjectFieldPrefab, FieldsContainer);
        SceneObjectPropertyDisplay sceneObjectPropertyDisplay = sceneObjectFieldObj.GetComponent<SceneObjectPropertyDisplay>();
        sceneObjectPropertyDisplay.Init(fieldInfo, this, instance);
        _properties.Add(sceneObjectPropertyDisplay);
    }
    void AddMaterialDisplay(FieldInfo fieldInfo, BaseBehavior instance)
    {
        string name = fieldInfo.Name;
        _fieldInfo.Add(name, fieldInfo);
        GameObject materialFieldObj = SimplePool.Instance.SpawnUI(MaterialFieldPrefab, FieldsContainer);
        MaterialPropertyDisplay materialPropertyDisplay = materialFieldObj.GetComponent<MaterialPropertyDisplay>();
        materialPropertyDisplay.Init(fieldInfo, this, instance);
        _properties.Add(materialPropertyDisplay);
    }
    public void Init(BaseBehavior behavior, PropertiesAndBehaviors propertiesAndCode)
    {
        _propertiesAndCode = propertiesAndCode;
        BehaviorInstance = behavior;
        BehaviorInfo behaviorData = behavior.GetBehaviorInfo();
        NameText.SetText(behaviorData.Name);
        ScriptImg.sprite = behaviorData.DisplaySprite;

        Type behaviorType = behaviorData.GetBehaviorType();
        if(behaviorType != null)
        {
            FieldInfo[] publicFields = behaviorType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach(var field in publicFields)
            {
                //Debug.Log("Has field: " + field.Name);
                if (field.FieldType == typeof(float))
                    AddFloatDisplay(field, behavior);
                else if (field.FieldType == typeof(uint))
                    AddUnsignedIntDisplay(field, behavior);
                else if (field.FieldType == typeof(int))
                    AddSignedIntDisplay(field, behavior);
                else if (field.FieldType == typeof(bool))
                    AddBooleanDisplay(field, behavior);
                else if (field.FieldType.IsEnum)
                    AddEnumDropdownDisplay(field, behavior);
                else if (field.FieldType == typeof(AudioClip))
                    AddAudioClipDisplay(field, behavior);
                else if (field.FieldType == typeof(AvatarField))
                    AddAvatarDisplay(field, behavior);
                else if (field.FieldType == typeof(PhysSound.PhysSoundMaterial))
                    AddPhysSoundMaterialDisplay(field, behavior);
                else if (field.FieldType == typeof(SceneObject))
                    AddSceneObjectDisplay(field, behavior);
                else if (field.FieldType == typeof(Material))
                    AddMaterialDisplay(field, behavior);
                else
                {
                    Debug.LogWarning("Unserialized type of: " + field.FieldType);
                }
            }
        }

        // Refresh the layout, because Unity is buggy
        if(this.isActiveAndEnabled)
            StartCoroutine(SetInactiveActive(0.01f));
    }
    public void Reset()
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        BehaviorInstance = null;
        _propertiesAndCode = null;
        _fieldInfo.Clear();
        foreach (var prop in _properties)
        {
            prop.DeInit();
            SimplePool.Instance.DespawnUI(prop.gameObject);
        }
        _properties.Clear();
    }
    IEnumerator SetInactiveActive(float time)
    {
        yield return new WaitForSeconds(time);
        //Debug.Log("Refreshing ComponentCard");
        this.gameObject.SetActive(false);
        this.gameObject.SetActive(true);
    }
    public void Refresh()
    {
        //Debug.Log("Component refreshing");
        foreach (var prop in _properties)
            prop.Refresh();
    }
    public void OnRemoveClicked()
    {
        //Debug.Log("Remove");
        _propertiesAndCode.RemoveCard(this);
    }
}
