// C# example:
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine.Rendering;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class PostBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 1; } }

    /// <summary>
    /// Unity adds some shaders to the AlwaysIncludedShaders list
    /// that are not really needed for this app. So, to remove it,
    /// we listen to when Unity is preparing to build and manually remove them
    /// </summary>
    /// <param name="target"></param>
    /// <param name="pathToBuiltProject"></param>
    public void OnPreprocessBuild(BuildReport report)
    {
        var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var serializedObject = new SerializedObject(graphicsSettingsObj);
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
        Debug.Log("num included = " + arrayProp.arraySize);
        bool didRem = false;
        int newSize = -1;
        for (int i = arrayProp.arraySize - 1; i >= 0; i--)
        {
            var arrayElem = arrayProp.GetArrayElementAtIndex(i);
            var refVal = arrayElem.objectReferenceValue;
            Shader shader = (Shader)refVal;
            string shaderName = shader?.name;

            if (shaderName == "Hidden/VideoDecode"
                || shaderName == "Hidden/VideoComposite"
                || shaderName == "Hidden/Compositing")
            {
                Debug.Log("Removing " + shaderName);
                arrayProp.DeleteArrayElementAtIndex(i);
                didRem = true;
                // update the size if applicable
                if (newSize == -1 || newSize == i + 1)
                    newSize = i;
            }
        }

        if (didRem)
        {
            if (newSize != -1)
                arrayProp.arraySize = newSize;
            Debug.Log("Saving changes");
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}