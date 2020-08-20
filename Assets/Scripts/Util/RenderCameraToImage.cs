using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderCameraToImage : MonoBehaviour {

    public int resWidth;
    public int resHeight;
    private Camera _cam;
    private int _numSaved = 0;

	void Start () {
        _cam = GetComponent<Camera>();
	}

    private void RenderAndSave()
    {
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 32);
        _cam.targetTexture = rt;
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGBA32, false);
        _cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        _cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        byte[] bytes = screenShot.EncodeToPNG();
        string filename = "snap_" + _numSaved.ToString() + ".png";
        System.IO.File.WriteAllBytes(filename, bytes);
        Debug.Log(string.Format("Took screenshot to: {0}", filename));
        _numSaved++;
    }
	
	void Update () {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RenderAndSave();
        }
	}
}
