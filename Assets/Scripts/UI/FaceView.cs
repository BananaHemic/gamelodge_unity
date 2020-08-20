using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FaceView : GenericSingleton<FaceView>
{
    public RawImage SelfieImage;
    public GameObject OtherFaceBubblePrefab;
    public GameObject SelfieStickPrefab;

    private FaceCamera _ourFaceCamera;
    private readonly Dictionary<UserDisplay, FaceCamera> _otherFaceCameras = new Dictionary<UserDisplay, FaceCamera>();

    private void Start()
    {
        SelfieImage.enabled = false;
    }

    public void CreateForSelf(UserDisplay owner)
    {
        var stick = GameObject.Instantiate(SelfieStickPrefab, owner.transform);
        FaceCamera faceCam = stick.GetComponent<FaceCamera>();

        _ourFaceCamera = faceCam;
        SelfieImage.texture = faceCam.InitAsSelf();
        SelfieImage.enabled = true;
    }
    public void CreateForOther(UserDisplay owner)
    {
        var stick = GameObject.Instantiate(SelfieStickPrefab, owner.transform);
        FaceCamera faceCam = stick.GetComponent<FaceCamera>();
        GameObject otherFaceImage = GameObject.Instantiate(OtherFaceBubblePrefab, transform);
        FaceBubble faceBubble = otherFaceImage.GetComponent<FaceBubble>();

        faceCam.InitAsOther(faceBubble);
        _otherFaceCameras.Add(owner, faceCam);
    }
    public void RemoveForSelf()
    {
        if(_ourFaceCamera == null)
        {
            Debug.LogError("Can't remove self face camera, it's null");
            return;
        }
        _ourFaceCamera.Dispose();
        Destroy(_ourFaceCamera.gameObject.gameObject);
        _ourFaceCamera = null;
    }
    public void RemoveForOther(UserDisplay other)
    {
        FaceCamera cam;
        if(!_otherFaceCameras.TryGetValue(other, out cam))
        {
            Debug.LogWarning("Can't remove face view, not found");
            return;
        }
        _otherFaceCameras.Remove(other);
        cam.Dispose();
        Destroy(cam.gameObject);
    }

    private void Update()
    {
        Camera mainCamera = Camera.main;
        RectTransform rectTransform = transform as RectTransform;
        float w = rectTransform.rect.width;
        float h = rectTransform.rect.height;
        float maxX = w / 2 - FaceCamera.OtherRenderDimension / 2f;
        float maxY = h / 2 - FaceCamera.OtherRenderDimension / 2f;

        // Move all the images of other people
        foreach(var kvp in _otherFaceCameras)
        {
            Vector3 userPosition = kvp.Key.transform.position;
            Vector3 viewPort = mainCamera.WorldToViewportPoint(userPosition);

            //Debug.Log(viewPort.ToPrettyString()
                //+ "\nwidth: " + w + " height: " + h
                //+ "\nmaxX: " + maxX + " maxY: " + maxY);
            // If it's in the screen, don't bother displaying it
            if (viewPort.z > -0.1
                && viewPort.x > 0 && viewPort.x < 1
                && viewPort.y > 0 && viewPort.y < 1)
            {
                kvp.Value.OurFaceBubble.gameObject.SetActive(false);
                return;
            }
            //kvp.Value.SetVisibility(true);
            kvp.Value.OurFaceBubble.gameObject.SetActive(true);

            var rectTrans = kvp.Value.OurFaceBubble.transform as RectTransform;
            float posX = Mathf.Clamp((viewPort.x - 0.5f) * w, -maxX, maxX);
            float posY = Mathf.Clamp((viewPort.y - 0.5f) * h, -maxY, maxY);

            if(viewPort.z < 0)
            {
                posX = posX > 0 ? -maxX : maxX;
                posY = posY > 0 ? -maxY : maxY;
            }
            rectTrans.anchoredPosition = new Vector2(posX, posY);
        }
    }
}
