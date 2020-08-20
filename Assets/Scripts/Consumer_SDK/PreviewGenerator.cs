using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewGenerator : IDisposable
{
    private readonly int _previewWidth;
    private readonly int _previewHeight;
    private Camera _renderCamera;
    private Color _backgroundColor;
    private Light _previewLight;

    const int PreviewObjectLayer = 31;
    const int PreviewLayerMask = 1 << PreviewObjectLayer;

    public PreviewGenerator(int width, int height, Color backgroundColor)
    {
        _previewWidth = width;
        _previewHeight = height;
        _backgroundColor = backgroundColor;

        RenderTexture renderTexture = new RenderTexture(_previewWidth, _previewHeight, 24);
        if (renderTexture == null || !renderTexture.Create())
        {
            Debug.LogError("Render texture failure!");
            return;
        }

        GameObject renderCameraObject = new GameObject("Render Camera");
        Camera renderCam = renderCameraObject.AddComponent<Camera>();

        renderCam.backgroundColor = backgroundColor;
        renderCam.fieldOfView = 65.0f;
        renderCam.clearFlags = CameraClearFlags.Color;
        renderCam.nearClipPlane = 0.0001f;
        renderCam.targetTexture = renderTexture;
        renderCam.cullingMask = PreviewLayerMask;
        _renderCamera = renderCam;

        GameObject lightObject = new GameObject("Preview light");
        _previewLight = lightObject.AddComponent<Light>();
        _previewLight.type = LightType.Directional;
        _previewLight.intensity = 1.1f;
    }

    public Texture2D Generate(GameObject unityPrefab)
    {
        RenderTexture oldRenderTexture = UnityEngine.RenderTexture.active;
        RenderTexture.active = _renderCamera.targetTexture;
        GL.Clear(true, true, _backgroundColor);

        GameObject previewObject = GameObject.Instantiate(unityPrefab);
        previewObject.transform.position = Vector3.zero;
        previewObject.transform.rotation = Quaternion.identity;
        previewObject.transform.localScale = unityPrefab.transform.lossyScale;
        previewObject.layer = PreviewObjectLayer;

        AABB previewAABB = CalculateAABB.GetHierarchyAABB(previewObject);
        float radius = previewAABB.Size.magnitude / 2f; //TODO not the real radius...
        
        Transform camTransform = _renderCamera.transform;
        _renderCamera.transform.rotation = Quaternion.AngleAxis(-45.0f, Vector3.up) * Quaternion.AngleAxis(35.0f, Vector3.right);
        _renderCamera.transform.position = previewAABB.Center - camTransform.forward * (radius * 1.2f + _renderCamera.nearClipPlane);             

        _previewLight.transform.forward = camTransform.forward;
        _renderCamera.Render();
        previewObject.SetActive(false);
        GameObject.DestroyImmediate(previewObject);

        Texture2D previewTexture = new Texture2D(_previewWidth, _previewHeight, TextureFormat.ARGB32, true, true);
        previewTexture.ReadPixels(new Rect(0, 0, _previewWidth, _previewHeight), 0, 0);
        previewTexture.Apply();
        UnityEngine.RenderTexture.active = oldRenderTexture;

        return previewTexture;
    }
    public void Dispose()
    {
        if (_renderCamera != null)
            GameObject.DestroyImmediate(_renderCamera);
        _renderCamera = null;
        if (_previewLight != null)
            GameObject.DestroyImmediate(_previewLight);
        _previewLight = null;
    }
}
