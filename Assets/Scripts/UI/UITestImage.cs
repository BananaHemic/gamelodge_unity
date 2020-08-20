using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
using System.Reflection;
using System.Collections.Generic;

[ExecuteInEditMode]
public class UITestImage : Image
{
    List<ICanvasElement> GraphicsList;
    List<ICanvasElement> LayoutList;
    [SerializeField]
    public bool DebugGraphics = false;

    void Update()
    {
        if (!Application.isPlaying)
            return;
        CheckLists();

        if (DebugGraphics && GraphicsList.Count > 0)
        {
            string Log = "Graphics Queue:\n";
            for (int i = 0; i < GraphicsList.Count; i++)
            {
                Log += GraphicsList[i] + "\n";
            }
            Debug.LogError(Log);
        }

        if (LayoutList.Count > 0)
        {
            string Log = "Layout Queue:\n";
            for (int i = 0; i < LayoutList.Count; i++)
            {
                Log += LayoutList[i] + "\n";
            }
            Debug.LogError(Log);
        }
    }

    void CheckLists()
    {
        if (GraphicsList == null)
        {
            System.Type TargetType = typeof(CanvasUpdateRegistry);
            FieldInfo GraphicsQField = TargetType.GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            object GraphicsQValue = GraphicsQField.GetValue(CanvasUpdateRegistry.instance);
            FieldInfo ListField = GraphicsQValue.GetType().GetField("m_List", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            GraphicsList = ListField.GetValue(GraphicsQValue) as List<ICanvasElement>;
        }

        if (LayoutList == null)
        {
            System.Type TargetType = typeof(CanvasUpdateRegistry);
            FieldInfo LayoutQField = TargetType.GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            object LayoutQValue = LayoutQField.GetValue(CanvasUpdateRegistry.instance);
            FieldInfo ListField = LayoutQValue.GetType().GetField("m_List", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            LayoutList = ListField.GetValue(LayoutQValue) as List<ICanvasElement>;
        }
    }

    protected override void UpdateGeometry()
    {
        base.UpdateGeometry();
        Debug.LogError("Updating Geometry: " + this);
    }

    public override void Rebuild(CanvasUpdate executing)
    {
        base.Rebuild(executing);
        Debug.LogError("Rebuild: " + executing + " : " + this);
    }
}