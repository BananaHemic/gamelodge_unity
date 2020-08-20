using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class ShaderProperty
{
    public enum ShaderPropertyType
    {
        Color = 0,
        Vector = 1,
        Float = 2,
        Range = 3,
        TexEnv = 4
    }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public ShaderPropertyType PropertyType { get; private set; }
    public bool HasRange { get; private set; }
    public float Min { get; private set; }
    public float Max { get; private set; }
    public float Default { get; private set; }

    const string NameKey = "n";
    const string DescriptionKey = "D";
    const string TypeKey = "t";
    const string MinKey = "m";
    const string MaxKey = "M";
    const string DefaultKey = "d";

    public ShaderProperty(string name, string description, ShaderPropertyType propertyType, float min, float max, float def)
    {
        Name = name;
        Description = description;
        PropertyType = propertyType;
        HasRange = min != max;
        Min = min;
        Max = max;
        Default = def;
    }
    public ShaderProperty(JToken json)
    {
        Name = json.Value<string>(NameKey);
        Description = json.Value<string>(DescriptionKey);
        PropertyType = (ShaderPropertyType)json.Value<int>(TypeKey);

        JToken minVal = json[MinKey];
        if(minVal == null)
        {
            // This property has no range
            Min = 0;
            Max = 0;
            Default = 0;
            HasRange = false;
        }
        else
        {
            HasRange = true;
            Min = minVal.Value<float>();
            Max = json.Value<float>(MaxKey);
            Default = json.Value<float>(Default);
        }
    }
    public void ToJson(StringBuilder sb)
    {
        sb.Append("{\"");
        sb.Append(NameKey);
        sb.Append("\":\"");
        sb.Append(Name);
        sb.Append("\",\"");
        sb.Append(DescriptionKey);
        sb.Append("\":\"");
        sb.Append(Description);
        sb.Append("\",\"");
        sb.Append(TypeKey);
        sb.Append("\":");
        sb.Append((int)PropertyType);

        if (HasRange)
        {
            sb.Append(",\"");
            sb.Append(MinKey);
            sb.Append("\":");
            sb.Append(Min);
            sb.Append(",\"");
            sb.Append(MaxKey);
            sb.Append("\":");
            sb.Append(Max);
            sb.Append(",\"");
            sb.Append(DefaultKey);
            sb.Append("\":");
            sb.Append(Default);
        }
        sb.Append("}");
    }
}
public class ShaderInfo
{
    public string Name { get; private set; }
    public List<ShaderProperty> Properties { get; private set; }

    const string NameKey = "N";
    const string PropertiesKey = "p";

#if UNITY_EDITOR
    public ShaderInfo(Shader shader)
    {
        Name = shader.name;
        Properties = new List<ShaderProperty>();
        int numProps = ShaderUtil.GetPropertyCount(shader);
        for(int i = 0; i < numProps; i++)
        {
            string name = ShaderUtil.GetPropertyName(shader, i);
            string description = ShaderUtil.GetPropertyDescription(shader, i);
            ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);
            float min = 0, max = 0, def = 0;
            if(propertyType == ShaderUtil.ShaderPropertyType.Float)
            {
                def = ShaderUtil.GetRangeLimits(shader, i, 0);
                min = ShaderUtil.GetRangeLimits(shader, i, 1);
                max = ShaderUtil.GetRangeLimits(shader, i, 2);
            }
            ShaderProperty.ShaderPropertyType shaderPropertyType = ShaderUtilEnum2Custom(propertyType);
            ShaderProperty property = new ShaderProperty(name, description, shaderPropertyType, min, max, def);
            Properties.Add(property);
        }
    }
    private static ShaderProperty.ShaderPropertyType ShaderUtilEnum2Custom(ShaderUtil.ShaderPropertyType propertyType)
    {
        switch (propertyType)
        {
            case ShaderUtil.ShaderPropertyType.Color:
                return ShaderProperty.ShaderPropertyType.Color;
            case ShaderUtil.ShaderPropertyType.Float:
                return ShaderProperty.ShaderPropertyType.Float;
            case ShaderUtil.ShaderPropertyType.Range:
                return ShaderProperty.ShaderPropertyType.Range;
            case ShaderUtil.ShaderPropertyType.TexEnv:
                return ShaderProperty.ShaderPropertyType.TexEnv;
            case ShaderUtil.ShaderPropertyType.Vector:
                return ShaderProperty.ShaderPropertyType.Vector;
        }
        return ShaderProperty.ShaderPropertyType.Color;
    }
#endif
    public ShaderInfo(JToken json)
    {
        Name = json.Value<string>(NameKey);
        JArray props = json.Value<JArray>(PropertiesKey);
        Properties = new List<ShaderProperty>(props.Count);

        for (int i = 0; i < props.Count; i++)
            Properties.Add(new ShaderProperty(props[i]));
    }
    public void ToJson(StringBuilder sb)
    {
        sb.Append("{\"");
        sb.Append(NameKey);
        sb.Append("\":\"");
        sb.Append(Name);
        sb.Append("\",\"");
        sb.Append(PropertiesKey);
        sb.Append("\":[");
        for(int i = 0; i < Properties.Count;i++)
        {
            Properties[i].ToJson(sb);
            if (i != Properties.Count - 1)
                sb.Append(",");
        }
        sb.Append("]}");
    }
}
