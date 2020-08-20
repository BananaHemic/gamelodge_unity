using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SavedGameItemView : MonoBehaviour
{
    public GameObject LoadingIcon;
    private Image _image;
    private SavedGameMetadata _savedGame;
    private Coroutine _loadImageRoutine;

    void Awake()
    {
        _image = GetComponent<Image> ();
    }
    private IEnumerator LoadGameImage()
    {
        if (string.IsNullOrEmpty(_savedGame.ImageID))
        {
            LoadingIcon.SetActive(false);
            yield break;
        }
        LoadingIcon.SetActive(true);
        string imgUrl = string.Format("https://gamelodge-game-snapshots.s3.amazonaws.com/{0}.png", _savedGame.ImageID);
        UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imgUrl);
        yield return webRequest.SendWebRequest();

        if (webRequest.isNetworkError)
        {
            Debug.LogError("Network error when getting game snapshot from: " + imgUrl + " error: " + webRequest.error);
            yield break;
        }
        if (webRequest.isHttpError)
        {
            Debug.LogError("HTTP error when getting game snapshot from: " + imgUrl + " resp: " + webRequest.responseCode);
            yield break;
        }

        Texture2D image = ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
        Sprite sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
        _image.sprite = sprite;
        LoadingIcon.SetActive(false);
    }
    public void Init(SavedGameMetadata savedGame)
    {
        _savedGame = savedGame;
        if (_loadImageRoutine != null)
            StopCoroutine(_loadImageRoutine);
        _loadImageRoutine = StartCoroutine(LoadGameImage());
    }
    public void OnClicked()
    {
        LoadGamesViewWorldPanel.Instance.OnSavedGameClicked(_savedGame);
    }
}
