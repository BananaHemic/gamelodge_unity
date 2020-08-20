using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;

[RequireComponent(typeof(Image))]
public abstract class BaseAssetFolderItem : MonoBehaviour
{
    public GameObject LoadingIcon;
    protected BundleMetaData _metaData;
    //TODO display name
    private Image _image;
    protected Button _button;
    private Coroutine _imageLoader;

    const string PreviewImageFormat = "https://gamelodge-assets-previews.s3.amazonaws.com/{0}.png";

    void Awake()
    {
        _image = GetComponent<Image> ();
        _button = GetComponent<Button>();
    }
    public virtual void Init(BundleMetaData metaData)
    {
        _metaData = metaData;
        _image.sprite = null;
        // load preview image for the asset
        if (_imageLoader != null)
            StopCoroutine(_imageLoader);
        _imageLoader = StartCoroutine(GetOnlineAssetImage(_metaData.ID));
    }
    IEnumerator GetOnlineAssetImage(string bundleID)
    {
        LoadingIcon.SetActive(true);
        string url = string.Format(PreviewImageFormat, bundleID);
        UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url);
        yield return webRequest.SendWebRequest();

        if (webRequest.isNetworkError)
        {
            Debug.LogError("Network error loading preview: " + webRequest.error + " for " + url);
            LoadingIcon.SetActive(false);
            yield break;
        }
        if (webRequest.isHttpError)
        {
            Debug.LogError("HTTP error loading preview: " + webRequest.error + " for " + url);
            LoadingIcon.SetActive(false);
            yield break;
        }

        //Debug.Log("Preview set for " + bundleID);
        SetImage(((DownloadHandlerTexture)webRequest.downloadHandler).texture);
        LoadingIcon.SetActive(false);
    }
    public void SetImage(Texture2D image)
    {
        Sprite sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
        _image.sprite = sprite;
    }
}
