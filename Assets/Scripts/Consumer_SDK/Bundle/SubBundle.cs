using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

public class SubBundle
{
    public readonly string ContainingBundle;
    public readonly List<BundleItem> BundleItems;
    public readonly SubBundleType TypeOfSubBundle;

    const string AABBKey = "ab";
    const string AddressesKey = "addr";
    const string MaterialIndexesKey = "mIdx";
    const string SmoothNormalsKey = "sNorm";
    const string ScriptsKey = "scripts";

    public enum SubBundleType
    {
        Prefab,
        Material,
        Model,
        Shader,
        Sound,
        Texture,
        ScriptableObject
    }

    public SubBundle(string containingBundle, SubBundleType subBundleType)
    {
        BundleItems = new List<BundleItem>();
        TypeOfSubBundle = subBundleType;
        ContainingBundle = containingBundle;
    }
    public SubBundle(string containingBundle, JObject json, SubBundleType subBundleType)
    {
        ContainingBundle = containingBundle;
        TypeOfSubBundle = subBundleType;
        // Addresses
        JArray addressArray = json.Value<JArray>(AddressesKey);
        JArray aabbArray = json.Value<JArray>(AABBKey);
        JArray materialIndexesJArray = json.Value<JArray>(MaterialIndexesKey);
        JArray smoothNormalArray = json.Value<JArray>(SmoothNormalsKey);
        JArray scriptArrayArray = json.Value<JArray>(ScriptsKey);

        if(addressArray.Count != aabbArray.Count)
        {
            Debug.LogError("Bad init for subBundle! " + addressArray.Count + "/" + aabbArray.Count + "/" + materialIndexesJArray.Count);
            return;
        }

        BundleItems = new List<BundleItem>(addressArray.Count);

        for(int i = 0; i < addressArray.Count; i++)
        {
            // Parse out material indexes
            JArray indexesJArray = (JArray)materialIndexesJArray[i];
            List<int> parsedIndexes = new List<int>(indexesJArray.Count);
            for (int j = 0; j < indexesJArray.Count; j++)
                parsedIndexes.Add(indexesJArray[j].Value<int>());

            // Parse out smooth normals
            JArray meshJArray = (JArray)smoothNormalArray[i];
            List<List<Vector3>> smoothedNormals = new List<List<Vector3>>(meshJArray.Count);
            for (int j = 0; j < meshJArray.Count; j++)
            {
                JArray normalsJArray = (JArray)meshJArray[j];
                List<Vector3> meshSmoothNormals = new List<Vector3>(normalsJArray.Count);
                smoothedNormals.Add(meshSmoothNormals);
                for(int k = 0; k < normalsJArray.Count; k++)
                {
                    int offset = 0;
                    meshSmoothNormals.Add(normalsJArray[k].Value<string>().DeSerializeVec3FromString(ref offset));
                }
            }

            // Parse out the attached scripts
            List<string> attachedScripts;
            if(scriptArrayArray == null)
                attachedScripts = new List<string>();
            else
            {
                JArray scriptsArray = (JArray)scriptArrayArray[i];
                attachedScripts = new List<string>(scriptsArray.Count);
                for (int j = 0; j < scriptsArray.Count; j++)
                {
                    string scriptName = scriptsArray.Value<string>(j);
                    attachedScripts.Add(scriptName);
                }
            }

            BundleItems.Add(new BundleItem((ushort)i, this, addressArray.Value<string>(i), new ModelAABB((JObject)aabbArray[i]), parsedIndexes, smoothedNormals, attachedScripts));
        }
    }
    public void AddElement(string address, ModelAABB modelAABB, List<int> materialIndexes, List<List<Vector3>> smoothNormals, List<string> attachedScripts)
    {
        BundleItems.Add(new BundleItem((ushort)BundleItems.Count, this, address, modelAABB, materialIndexes, smoothNormals, attachedScripts));
    }
    public void ToJson(string key, StringBuilder sb)
    {
        sb.Append("\"");
        sb.Append(key);
        sb.Append("\":{");
        // Addresses
        sb.Append("\"");
        sb.Append(AddressesKey);
        sb.Append("\":[");
        for(int i = 0; i < BundleItems.Count; i++)
        {
            sb.Append(JsonConvert.ToString(BundleItems[i].Address));
            if (i != BundleItems.Count - 1)
                sb.Append(",");
        }

        // AABB
        sb.Append("],\"");
        sb.Append(AABBKey);
        sb.Append("\":[");
        for(int i = 0; i < BundleItems.Count; i++)
        {
            BundleItems[i].AABBInfo.ToJson(sb);
            if (i != BundleItems.Count - 1)
                sb.Append(",");
        }

        // Material indexes
        sb.Append("],\"");
        sb.Append(MaterialIndexesKey);
        sb.Append("\":[");
        for(int i = 0; i < BundleItems.Count; i++)
        {
            List<int> indexes = BundleItems[i].MaterialIndexes;
            sb.Append("[");
            for(int j = 0; j < indexes.Count; j++)
            {
                sb.Append(indexes[j]);
                if (j != indexes.Count - 1)
                    sb.Append(",");
            }
            sb.Append("]");

            if (i != BundleItems.Count - 1)
                sb.Append(",");
        }
        // Attached Scripts
        sb.Append("],\"");
        sb.Append(ScriptsKey);
        sb.Append("\":[");
        for(int i = 0; i < BundleItems.Count; i++)
        {
            List<string> attachedScripts = BundleItems[i].AttachedScripts;
            sb.Append("[");
            for(int j = 0; j < attachedScripts.Count; j++)
            {
                sb.Append(JsonConvert.ToString(attachedScripts[j]));
                if (j != attachedScripts.Count - 1)
                    sb.Append(",");
            }
            sb.Append("]");
            if (i != BundleItems.Count - 1)
                sb.Append(",");
        }
        // Material indexes
        sb.Append("],\"");
        sb.Append(SmoothNormalsKey);
        sb.Append("\":[");
        for (int i = 0; i < BundleItems.Count; i++)
        {
            List<List<Vector3>> normalMeshes = BundleItems[i].SmoothNormals;
            sb.Append("[");
            if (normalMeshes != null)
            {
                for(int j = 0; j < normalMeshes.Count; j++)
                {
                    sb.Append("[");
                    List<Vector3> normals = normalMeshes[j];
                    for(int k = 0; k < normals.Count; k++)
                    {
                        sb.Append("\"");
                        sb.Append(normals[k].SerializeToString());
                        sb.Append("\"");
                        if (k != normals.Count - 1)
                            sb.Append(",");
                    }
                    sb.Append("]");
                    if (j != normalMeshes.Count - 1)
                        sb.Append(",");
                }
            }
            sb.Append("]");

            if (i != BundleItems.Count - 1)
                sb.Append(",");
        }
        sb.Append("]}");
    }
}
