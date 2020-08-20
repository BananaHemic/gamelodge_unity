using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public static class MissingReferences
{
#if UNITY_EDITOR
    [MenuItem("Tools/Find Missing references in scene")]
    public static void FindMissingReferences()
    {
        var objects = GameObject.FindObjectsOfType<GameObject>();

        foreach (var go in objects)
        {
            var components = go.GetComponents<MonoBehaviour>();

            foreach (var c in components)
            {
                if(c == null) {
                    Debug.LogError("Null component: " + FullObjectPath(go), go);
                    continue;
                }
                SerializedObject so = new SerializedObject(c);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                        {
                            Debug.LogError("Missing reference found in: " + FullObjectPath(go) + ", Property : " + sp.name, go);
                        }
                    }
                }
            }
        }
    }

    private static void ShowError(string objectName, string propertyName)
    {
    }

    private static string FullObjectPath(GameObject go)
    {
        return go.transform.parent == null ? go.name : FullObjectPath(go.transform.parent.gameObject) + "/" + go.name;
    }
#endif
}