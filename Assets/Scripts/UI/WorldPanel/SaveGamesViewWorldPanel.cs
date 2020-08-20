using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DarkRift;
using System.IO;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine.Rendering.PostProcessing;

public class SaveGamesViewWorldPanel : MonoBehaviour
{
    public TMP_InputField TitleField;
    public TMP_InputField DescriptionField;
    public TMP_InputField TagsField;
    public Image GameImage;

    private RenderTexture _renderTexture;
    private byte[] _snapshotBytes;
    private Coroutine _saveRoutine;

    const int PreviewImageWidth = 1024;
    const int PreviewImageHeight = 1024 * 9 / 16;
    const string TitleIdentifier = "saveGameTitle";
    const string DescriptionIdentifier = "saveGameDescription";
    const string TagsIdentifier = "saveGameTags";

    public void OnPanelSelected()
    {

    }
    public void OnOpenExistingSnapshotClicked()
    {

    }
    public void OnMakeSnapshotClicked()
    {
        // Generate a render texture
        if(_renderTexture == null)
        {
            _renderTexture = new RenderTexture(PreviewImageWidth, PreviewImageHeight, 0)
            {
                antiAliasing = 4
            };
        }
        // Render an image
        var prevTex = Orchestrator.Instance.MainCamera.targetTexture;
        Orchestrator.Instance.MainCamera.targetTexture = _renderTexture;
        RenderTexture prevRT = RenderTexture.active;
        RenderTexture.active = _renderTexture;
        PostProcessLayer postProcessLayer = Orchestrator.Instance.MainCamera.GetComponent<PostProcessLayer>();
        if(postProcessLayer != null)
            postProcessLayer.enabled = false;
        Orchestrator.Instance.MainCamera.Render();

        // Save the image 
        Texture2D img = new Texture2D(PreviewImageWidth, PreviewImageHeight);
        img.ReadPixels(new Rect(0, 0, PreviewImageWidth, PreviewImageWidth), 0, 0);
        img.Apply();
        _snapshotBytes = img.EncodeToPNG();
        GameImage.sprite = Sprite.Create(img, new Rect(0.0f, 0.0f, img.width, img.height), new Vector2(0.5f, 0.5f), 100.0f);

        // Restore previous
        if(postProcessLayer != null)
            postProcessLayer.enabled = true;
        RenderTexture.active = prevRT;
        Orchestrator.Instance.MainCamera.targetTexture = prevTex;
    }
    public void OnSaveButtonClicked()
    {
        Debug.Log("Will save game");
        if(_saveRoutine != null)
        {
            Debug.LogWarning("Dropping save, as we're currently saving");
            return;
        }
        _saveRoutine = StartCoroutine(SaveGame());
    }
    private IEnumerator SaveGame()
    {
        string imageKey;
        // If we have a new snapshot, we'll need to first upload that
        if(_snapshotBytes != null)
        {
            byte[] uploadBytes = _snapshotBytes;
            _snapshotBytes = null;
            imageKey = UploadAssets.CreateRandomS3KeyString(32);
            // Get the upload url
            string endpoint = string.Format("{0}:{1}/get-upload-game-snapshot-url?imageID={2}", GLVars.Instance.APIAddress, GLVars.Instance.APIPort, imageKey);
            UnityWebRequest webReq = UnityWebRequest.Get(endpoint);

            yield return webReq.SendWebRequest();
            if (webReq.isNetworkError)
            {
                Debug.LogError("Network error when trying to upload snapshot: " + webReq.error + " for url " + endpoint);
                _saveRoutine = null;
                yield break;
            }
            if (webReq.isHttpError)
            {
                Debug.LogError("HTTP error when trying to upload asset: " + webReq.error);
                Debug.LogError(webReq.downloadHandler.text);
                _saveRoutine = null;
                yield break;
            }

            // Parse out the provided url to upload to
            Debug.Log(webReq.downloadHandler.text);
            JObject json = JObject.Parse(webReq.downloadHandler.text);

            JToken urlToken;
            if (!json.TryGetValue("url", out urlToken))
            {
                Debug.LogError("Failed to parse url from api");
                _saveRoutine = null;
                yield break;
            }
            string uploadUrl = urlToken.Value<string>();

            // Upload the snapshot
            UnityWebRequest uploadReq = UnityWebRequest.Put(uploadUrl, uploadBytes);
            uploadReq.SetRequestHeader("x-amz-acl", "public-read"); // Make sure everyone can read this file
            yield return uploadReq.SendWebRequest();

            if (uploadReq.isNetworkError)
            {
                Debug.LogError("Network error when uploading snapshot");
                Debug.LogError(uploadReq.error);
                _saveRoutine = null;
                yield break;
            }
            if (uploadReq.isHttpError)
            {
                Debug.LogError("HTTP error when uploading snapshot");
                Debug.LogError(uploadReq.downloadHandler.text);
                _saveRoutine = null;
                yield break;
            }
            Debug.Log("Upload completed, response: " + uploadReq.downloadHandler.text);
        }
        else
        {
            imageKey = Orchestrator.Instance.CurrentSavedGame != null ? Orchestrator.Instance.CurrentSavedGame.ImageID : "";
        }

        SaveGameRequest saveGame = new SaveGameRequest(TitleField.text, DescriptionField.text, imageKey, TagsField.text);
        DarkRiftWriter writer = DarkRiftWriter.Create();
        writer.Write(saveGame);
        RealtimeNetworkUpdater.Instance.EnqueueReliableMessage(ServerTags.SaveGame, writer);
        _saveRoutine = null;
        Debug.Log("Save request sent");
    }
    public void OnTitleInputFieldSelect()
    {
        RLDHelper.Instance.RegisterInputSelected(TitleIdentifier);
    }
    public void OnTitleInputFielDeselect()
    {
        RLDHelper.Instance.RegisterInputDeselected(TitleIdentifier);
    }
    public void OnDescriptionInputFieldSelect()
    {
        RLDHelper.Instance.RegisterInputSelected(DescriptionIdentifier);
    }
    public void OnDescriptionInputFielDeselect()
    {
        RLDHelper.Instance.RegisterInputDeselected(DescriptionIdentifier);
    }
    public void OnTagsInputFieldSelect()
    {
        RLDHelper.Instance.RegisterInputSelected(TagsIdentifier);
    }
    public void OnTagsInputFielDeselect()
    {
        RLDHelper.Instance.RegisterInputDeselected(TagsIdentifier);
    }
}
