using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FaceCamera : MonoBehaviour
{
    private Camera _selfieCamera;
    private RenderTexture _renderTexture;
    public FaceBubble OurFaceBubble { get; private set; }

    const int SelfRenderDimension = 256;
    public readonly static int OtherRenderDimension = 192;

    public RenderTexture InitAsSelf()
    {
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(SelfRenderDimension, SelfRenderDimension, 0)
            {
                antiAliasing = 4
            };
        }
        //TODO handle if it's not created
        if (_selfieCamera == null)
            _selfieCamera = GetComponent<Camera>();
        _selfieCamera.targetTexture = _renderTexture;

        return _renderTexture;
    }
    public void InitAsOther(FaceBubble faceBubble)
    {
        OurFaceBubble = faceBubble;
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(OtherRenderDimension, OtherRenderDimension, 0)
            {
                antiAliasing = 4
            };
        }
        //TODO handle if it's not created
        if (_selfieCamera == null)
            _selfieCamera = GetComponent<Camera>();
        _selfieCamera.targetTexture = _renderTexture;
        OurFaceBubble.DisplayTexture(_renderTexture);

        // Other people have some different camera settings
        _selfieCamera.clearFlags = CameraClearFlags.Skybox;
        _selfieCamera.cullingMask = GLLayers.DefaultLayerMask
            | GLLayers.LocalUser_PlayLayerMask
            | GLLayers.OtherUser_PlayLayerMask
            | GLLayers.TableLayerMask
            | GLLayers.TerrainLayerMask
            | GLLayers.PhysicsObject_NonWalkableLayerMask;

        //return _renderTexture;
    }
    public void Dispose()
    {
        if (OurFaceBubble != null)
            Destroy(OurFaceBubble.gameObject);
        OurFaceBubble = null;

        if (_renderTexture != null)
            Destroy(_renderTexture);
        _renderTexture = null;
    }
}
