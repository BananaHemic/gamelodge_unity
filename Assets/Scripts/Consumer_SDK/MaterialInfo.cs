using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Text;

/// <summary>
/// The material information present in a bundle.
/// The data in here should never change at runtime.
/// </summary>
public class MaterialInfo
{
    public string BundleID { get; private set; }
    public string Address { get; private set; }
    public string Name { get; private set; }
    /// <summary>
    /// The index of this material within the list
    /// of materials for the bundle
    /// </summary>
    public ushort Index { get; private set; }
    public int ShaderIdx { get; private set; }
    public ShaderInfo ShaderInfo { get; private set; }

    const string AddressKey = "a";
    const string NameKey = "n";
    const string ShaderIdxKey = "s";

    public MaterialInfo(string bundleID, string address, Material material, ushort matIndex, int shaderIdx)
    {
        Address = address;
        Name = material.name;
        ShaderIdx = shaderIdx;
        BundleID = bundleID;
        Index = matIndex;
    }
    public MaterialInfo(JToken json, string bundleID, ushort index, ShaderInfo[] shaderInfos)
    {
        BundleID = bundleID;
        Address = json.Value<string>(AddressKey);
        Name = json.Value<string>(NameKey);
        Index = index;
        ShaderIdx = json.Value<int>(ShaderIdxKey);
        ShaderInfo = shaderInfos[ShaderIdx];
    }
    public void ToJson(StringBuilder sb)
    {
        sb.Append("{\"");
        sb.Append(AddressKey);
        sb.Append("\":\"");
        sb.Append(Address);
        sb.Append("\",\"");
        sb.Append(NameKey);
        sb.Append("\":\"");
        sb.Append(Name);
        sb.Append("\",\"");
        sb.Append(ShaderIdxKey);
        sb.Append("\":");
        sb.Append(ShaderIdx);
        sb.Append("}");
    }
}
