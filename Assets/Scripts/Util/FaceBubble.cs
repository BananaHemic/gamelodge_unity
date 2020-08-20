using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FaceBubble : MonoBehaviour
{
    public RawImage Outline;
    public RawImage Display;

    public void DisplayTexture(RenderTexture tex)
    {
        Display.texture = tex;
    }
}
