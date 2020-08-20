using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Text;

public class ModelAABB
{
    public readonly Vector3 Center;
    public readonly Vector3 Extents;
    public readonly bool IsValid;
    const string CenterKey = "c";
    const string ExtentsKey = "e";

    public ModelAABB()
    {
        IsValid = false;
    }
    public ModelAABB(AABB aabb, Vector3 modelScale)
    {
        if (!aabb.IsValid)
        {
            IsValid = false;
            return;
        }
        IsValid = true;
        // Account for the model scale in the AABB
        Center = new Vector3(aabb.Center.x * modelScale.x, aabb.Center.y * modelScale.y, aabb.Center.z * modelScale.z);
        Extents = new Vector3(aabb.Extents.x * modelScale.x, aabb.Extents.y * modelScale.y, aabb.Extents.z * modelScale.z);
    }
    public ModelAABB(JObject json)
    {
        if (json == null)
            return;

        if (json.TryGetValue(CenterKey, out JToken centerVal))
        {
            IsValid = true;
            int offsetCenter = 0;
            Center = centerVal.Value<string>().DeSerializeVec3FromString(ref offsetCenter);
        }
        if(json.TryGetValue(ExtentsKey, out JToken extentsVal))
        {
            IsValid = true;
            int offsetExtents = 0;
            Extents = extentsVal.Value<string>().DeSerializeVec3FromString(ref offsetExtents);
        }
    }
    public void ToJson(StringBuilder sb)
    {
        if (!IsValid)
        {
            sb.Append("{}");
            return;
        }
        sb.Append("{\"");
        sb.Append(CenterKey);
        sb.Append("\":\"");
        Center.SerializeToString(sb);
        sb.Append("\",\"");
        sb.Append(ExtentsKey);
        sb.Append("\":\"");
        Extents.SerializeToString(sb);
        sb.Append("\"}");
        //return sb;
    }
}
