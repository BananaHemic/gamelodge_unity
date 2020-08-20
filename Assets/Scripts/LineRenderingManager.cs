using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;

public class LineRenderingManager : GenericSingleton<LineRenderingManager>
{
    public Material LineMaterial;
    // TODO use DrawMeshInstanced for best performance

    public void ConfigureLineRenderer(XRLineRenderer lineRenderer, Vector3 origin, Vector3 direction, Color color)
    {
        lineRenderer.SetTotalColor(color);
        //lineRenderer.colorStart = color;
        //lineRenderer.colorEnd = color;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, origin + direction);
    }
    public XRLineRenderer GetLineRenderer(Vector3 origin, Vector3 direction, Color color)
    {
        // TODO keep a hashtable with color->material so that
        // we can reduce batches
        GameObject newObj = new GameObject("line");
        newObj.transform.parent = transform;
        XRLineRenderer lineRenderer = newObj.AddComponent<XRLineRenderer>();
        //XRLineRenderer lineRenderer = gameObject.AddComponent<XRLineRenderer>();
        lineRenderer.SetPositions(new Vector3[] { origin, origin + direction }, true);
        lineRenderer.m_UseWorldSpace = true;
        //lineRenderer.material = LineMaterial;
        var mats = new Material[] { LineMaterial };
        lineRenderer.materials = mats;
        lineRenderer.m_Materials = mats;
        //Debug.Log("Color " + color);
        ConfigureLineRenderer(lineRenderer, origin, direction, color);
        return lineRenderer;
    }
}
