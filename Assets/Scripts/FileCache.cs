/*
 * This is a wrapper around all asset IO
 * It handles loading files from
 * 1) Internal Unity system (aka. Assets/ folder)
 * 2) Assets that need to be fetched from the network
 * 3) Assets that were previously fetched from the network and are now loaded from file
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
//using Zooterkins;
using System.Text;
using System.IO;

public class FileCache : MonoBehaviour
{
    public static FileCache Instance { get; private set; }

    public Texture2D BlackTexture;
    public Sprite[] Sprites;
    public Texture2D[] Texes;
    public AudioClip[] Clips;
    public Shader[] Shaders;
#if UNITY_EDITOR || UNITY_ANDROID
    public Shader[] ShadersOES;
#endif
    public Material[] Materials;
    public GameObject[] Prefabs;

    //private readonly Dictionary<Material, Pipeline> _activePipelines = new Dictionary<Material, Pipeline>();
    private readonly List<string> _imagesCurrentlyDownloading = new List<string>();
#pragma warning disable CS0609
    private List<DownloadedMedia> _downloadedMedia;
    private bool _hasIdentifiedDownloadedMedia = false;
#pragma warning disable CS0609

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Destroying file cache", this);
            Destroy(Instance);
        }
        Instance = this;

        //StartCoroutine(IdentifyDownloadedMedia());
    }

    /// <summary>
    /// Search our StreamingAssets and persistent data path
    /// To know which files we've already retrieved
    /// </summary>
    //private IEnumerator IdentifyDownloadedMedia()
    //{
    //    DirectoryListAsync asyncListDirectory = new DirectoryListAsync(
    //        Application.streamingAssetsPath,
    //        Application.persistentDataPath);
    //    //Debug.Log("Will get downloaded media");
    //    yield return asyncListDirectory.WaitFor();
    //    _downloadedMedia = asyncListDirectory.DownloadedMedia;
    //    //Debug.Log("Recv downloaded media " + _downloadedMedia.Count);
    //    _hasIdentifiedDownloadedMedia = true;

    //    //foreach (var media in _downloadedMedia)
    //    //Debug.Log("Found: " + DownloadedMedia2URL(media));
    //}
    private bool IsMediaAlreadyDownloaded(string image)
    {
        if (!_hasIdentifiedDownloadedMedia)
            return false;
        for (int i = 0; i < _downloadedMedia.Count; i++)
        {
            if (_downloadedMedia[i].Filename == image)
                return true;
        }
        return false;
    }
    private DownloadedMedia GetDownloadedMedia(string image)
    {
        if (!_hasIdentifiedDownloadedMedia)
            return null;
        for (int i = 0; i < _downloadedMedia.Count; i++)
        {
            if (_downloadedMedia[i].Filename == image)
                return _downloadedMedia[i];
        }
        return null;
    }
    private static string DownloadedMedia2URL(DownloadedMedia downloadedMedia)
    {
        StringBuilder sb = new StringBuilder();
#if (UNITY_STANDALONE || UNITY_EDITOR)
        sb.Append("file:///");
#else
        sb.Append("file://");
#endif
        string path = null;
        if (downloadedMedia.MediaType == DownloadedMediaType.StreamingAssets)
            path = Application.streamingAssetsPath;
        else if (downloadedMedia.MediaType == DownloadedMediaType.PersistentPath)
            path = Application.persistentDataPath;

        sb.Append(path);
        if (!path.EndsWith("/"))
            sb.Append("/");
        sb.Append(downloadedMedia.Filename);
        return sb.ToString();
    }
    private string GetFilenameForDownloadedMedia(string image)
    {
        if (!_hasIdentifiedDownloadedMedia)
            return null;
        for (int i = 0; i < _downloadedMedia.Count; i++)
        {
            if (_downloadedMedia[i].Filename == image)
                return DownloadedMedia2URL(_downloadedMedia[i]);
        }
        return null;
    }
    public static string Url2ImageName(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        int lastSlashIndex = url.LastIndexOf('/');
        if (lastSlashIndex < 0)
            return url;
        // Make sure the slash isn't at the end
        if (lastSlashIndex + 1 >= url.Length)
            return null;
        return url.Substring(lastSlashIndex + 1);
    }
    /// <summary>
    /// Currently used only in the lobby to load the
    /// broadcast images. This prepares the image
    /// and loads it into RAM. Once this is finished, you
    /// can call GetImageTexture
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public IEnumerator LoadImage(string url)
    {
        // Make sure we've finished identifying the existing downloaded media
        while (!_hasIdentifiedDownloadedMedia)
            yield return null;
        string image = Url2ImageName(url);
        Debug.Log("Will try loading image: " + image);

        // Make sure we only have one download for a file
        while (_imagesCurrentlyDownloading.Contains(image))
        {
            Debug.Log("Waiting for previous image load for " + image);
            yield return null;
        }
        _imagesCurrentlyDownloading.Add(image);

        // First, check if we need to download the media
        DownloadedMedia media = GetDownloadedMedia(image);
        if (media == null)
        {
            yield return Download(url, image, true);
            _imagesCurrentlyDownloading.Remove(image);
            yield break;
        }
        else
        {
            Debug.Log("Image: " + image + " already downloaded");
        }

        // Make sure the texture is loaded in RAM
        if (media.texture == null)
        {
            string filename = DownloadedMedia2URL(media);
            Debug.Log(url + " needs to be loaded into RAM, from " + filename);
            using (UnityWebRequest loadTexReq = UnityWebRequest.Get(filename))
            {
                DownloadHandlerTexture texHandler = new DownloadHandlerTexture();
                loadTexReq.downloadHandler = texHandler;
                yield return loadTexReq.SendWebRequest();
                if (loadTexReq.isNetworkError || loadTexReq.isHttpError)
                {
                    Debug.LogError("Failed loading " + image + " from file: " + filename + " err: " + loadTexReq.error);
                    _imagesCurrentlyDownloading.Remove(image);
                    yield break;
                }
                media.texture = texHandler.texture;
            }
            //Debug.Log("Set tex");
        }
        _imagesCurrentlyDownloading.Remove(image);
    }

    public Sprite GetImageSprite(string texName)
    {
        // First check the textures we have in Unity
        for (int i = 0; i < Sprites.Length; i++)
        {
            if (texName == Sprites[i].name)
            {
                return Sprites[i];
            }
        }
        return null;
    }
    /// <summary>
    /// Returns a previously loaded texture2D
    /// Make sure to call LoadImage first
    /// Only handles static images, not pipelines
    /// Currently only used for the lobby images
    /// </summary>
    /// <param name="texName"></param>
    /// <returns></returns>
    public Texture2D GetImageTexture(string texName)
    {
        // First check the textures we have in Unity
        for (int i = 0; i < Texes.Length; i++)
        {
            if (texName == Texes[i].name)
            {
                return Texes[i];
            }
        }
        // Check textures that we've previously downloaded
        if (_hasIdentifiedDownloadedMedia)
        {
            for (int i = 0; i < _downloadedMedia.Count; i++)
            {
                if (_downloadedMedia[i].Filename != texName)
                    continue;

                // If we already have the texture in memory, use it
                if (_downloadedMedia[i].texture != null)
                    return _downloadedMedia[i].texture;
                else
                {
                    Debug.LogWarning("Texture " + texName + " downloaded, but not loaded in mem. Please use LoadImage first");
                    return null;
                }

            }
        }

        return null;
    }
    private IEnumerator Download(string url, string image, bool setTex)
    {
        string targetFilename = Path.Combine(Application.persistentDataPath, image);
        using (UnityWebRequest downloadReq = UnityWebRequest.Get(url))
        {
            using (DownloadHandlerFile downloadHandler = new DownloadHandlerFile(targetFilename)
            {
                removeFileOnAbort = true // Make sure that all files we store are complete
            })
            {
                downloadReq.downloadHandler = downloadHandler;
                Debug.Log("Downloading: " + image);
                yield return downloadReq.SendWebRequest();

                if (downloadReq.isNetworkError || downloadReq.isHttpError)
                {
                    Debug.LogError("Failed downloading " + image + " from: " + url + " into: " + targetFilename + " err: " + downloadReq.error);
                    yield break;
                }
            }
        }
        Debug.Log(image + " downloaded from " + url + " into " + targetFilename);
        // If this properly downloads, add it to the db
        var d = new DownloadedMedia()
        {
            Filename = image,
            MediaType = DownloadedMediaType.PersistentPath
        };
        _downloadedMedia.Add(d);

        yield return null;
        //TODO it really ought to be possible to both use a DownloadHandlerFile and
        // a DownloadHandlerTexture to both save the texture and keep a decompressed
        // version in memory
        if (setTex)
        {
#if UNITY_ANDROID
            // On Android, there appears to be a Unity bug where the file connection isn't
            // properly disposed of. So instead of loading from a file, we load from network
            // twice
            //TODO FIXME
            targetFilename = url;
#endif
            using (UnityWebRequest loadTexReq = UnityWebRequest.Get(targetFilename))
            {
                DownloadHandlerTexture texHandler = new DownloadHandlerTexture();
                loadTexReq.downloadHandler = texHandler;
                yield return loadTexReq.SendWebRequest();
                if (loadTexReq.isNetworkError || loadTexReq.isHttpError)
                {
                    Debug.LogError("Failed downloading during setTex " + image + " from file: " + targetFilename + " err: " + loadTexReq.error);
                    yield break;
                }
                d.texture = texHandler.texture;
            }
            Debug.Log("Set tex");
        }
    }
    /// <summary>
    /// Sets the mainTexture of the provided material
    /// to the texture corresponding to that provided 
    /// It first checks if the tex is a local texture,
    /// Then it checks if this is a piece of downloaded Media
    /// Then it tries to load as an SRT pipeline
    /// Finally, it checks if the texName is a url
    /// </summary>
    /// <param name="matToSet"></param>
    /// <param name="texName"></param>
    /// <param name="pipelineIndex">Index of pipeline, or -1 if no pipeline used</param>
    /// <returns>Whether setting the texture succeeded</returns>
    //public bool SetTex(Material matToSet, string texName, GameObject audioHolder, out int pipelineIndex)
    //{
    //    // If the name is null, show black
    //    if (texName == null)
    //    {
    //        matToSet.SetTexture(Zooterkins.Pipeline.TexIDs[0], BlackTexture);
    //        matToSet.EnableKeyword(Zooterkins.Pipeline.RGBA_Keyword);
    //        matToSet.SetTextureScale(Zooterkins.Pipeline.TexIDs[0], new Vector2(1f, 1f));
    //        matToSet.SetTextureOffset(Zooterkins.Pipeline.TexIDs[0], new Vector2(0, 0));
    //        pipelineIndex = -1;
    //        return true;
    //    }

    //    // First try with in-app textures
    //    for (int i = 0; i < Texes.Length; i++)
    //    {
    //        if (texName == Texes[i].name)
    //        {
    //            matToSet.SetTexture(Zooterkins.Pipeline.TexIDs[0], Texes[i]);
    //            matToSet.EnableKeyword(Zooterkins.Pipeline.RGBA_Keyword);
    //            matToSet.SetTextureScale(Zooterkins.Pipeline.TexIDs[0], new Vector2(1f, 1f));
    //            matToSet.SetTextureOffset(Zooterkins.Pipeline.TexIDs[0], new Vector2(0, 0));
    //            pipelineIndex = -1;
    //            return true;
    //        }
    //    }

    //    string pipelineUrl = null;
    //    // Check if this is a piece of downloaded media
    //    if (_hasIdentifiedDownloadedMedia)
    //    {
    //        for (int i = 0; i < _downloadedMedia.Count; i++)
    //        {
    //            if (_downloadedMedia[i].Filename != texName)
    //                continue;

    //            // Here, we always load downloaded media via pipeline
    //            pipelineUrl = DownloadedMedia2URL(_downloadedMedia[i]);
    //            break;
    //        }
    //    }

    //    // If the url is an int, load an SRT pipeline
    //    int latency = 0;
    //    int port = 0;
    //    if (pipelineUrl == null && int.TryParse(texName, out port))
    //    {
    //        Debug.Log("Loading live pipeline");
    //        if (string.IsNullOrEmpty(AmicaVideo.Instance.EdgeIP))
    //        {
    //            Debug.LogWarning("Not loading live pipeline, no edge");
    //            pipelineIndex = -1;
    //            return false;
    //        }

    //        StringBuilder sb = new StringBuilder();
    //        sb.Append("srt://");
    //        sb.Append(AmicaVideo.Instance.EdgeIP);
    //        sb.Append(":");
    //        sb.Append(texName);
    //        pipelineUrl = sb.ToString();
    //        latency = AmicaVideo.Instance.Srt_Latency;
    //    }

    //    // It's possible for the texName to be a URL
    //    if (pipelineUrl == null && texName.StartsWith("http"))
    //        pipelineUrl = texName;

    //    // If we still don't know where to look for this file
    //    // error out
    //    if (pipelineUrl == null)
    //    {
    //        Debug.LogError("Can't load texture: " + texName);
    //        pipelineIndex = -1;
    //        return false;
    //    }

    //    Pipeline pipeline;
    //    bool pipelineExists = _activePipelines.TryGetValue(matToSet, out pipeline);

    //    // If there's already a pipeline for this mat
    //    // either return early if it's using the same URL
    //    // or dispose it
    //    if (pipelineExists)
    //    {
    //        if (pipeline.Url == pipelineUrl)
    //        {
    //            pipelineIndex = pipeline.Index;
    //            return true;
    //        }
    //        else
    //            pipeline.Dispose();
    //    }

    //    pipeline = ZooterkinsManager.Instance.GetNewPipeline(pipelineUrl, latency, false, audioHolder);
    //    pipeline.AddMaterial(matToSet, true);
    //    _activePipelines[matToSet] = pipeline;
    //    pipelineIndex = pipeline.Index;
    //    return true;
    //}
    public AudioClip[] GetAudioArray(string[] audioNames)
    {
        AudioClip[] clips = new AudioClip[audioNames.Length];
        for (int i = 0; i < audioNames.Length; i++)
            clips[i] = GetAudio(audioNames[i]);
        return clips;
    }
    public AudioClip GetAudio(string audioName)
    {
        for (int i = 0; i < Clips.Length; i++)
        {
            if (audioName == Clips[i].name)
            {
                return Clips[i];
            }
        }
        Debug.LogWarning("Audio clip named " + audioName + " not found");
        return null;
    }
    public Shader GetShader(string name)
    {
        for (int i = 0; i < Shaders.Length; i++)
        {
            if (Shaders[i].name == name)
                return Shaders[i];
        }
        // We want to not reference OES shaders on PC
#if UNITY_EDITOR || UNITY_ANDROID
        for (int i = 0; i < ShadersOES.Length; i++)
        {
            if (ShadersOES[i].name == name)
                return ShadersOES[i];
        }
#endif
        return null;
    }
    public Material GetMaterial(string name)
    {
        for (int i = 0; i < Materials.Length; i++)
        {
            if (Materials[i].name == name)
                return Materials[i];
        }
        return null;
    }
    public GameObject GetPrefab(string name)
    {
        for (int i = 0; i < Prefabs.Length; i++)
        {
            if (Prefabs[i].name == name)
                return Prefabs[i];
        }
        return null;
    }
    //private void CloseAllPipelines()
    //{
    //    foreach (KeyValuePair<Material, Pipeline> matPipe in _activePipelines)
    //    {
    //        matPipe.Value.Dispose();
    //    }
    //    _activePipelines.Clear();
    //}

    public enum DownloadedMediaType
    {
        StreamingAssets,
        PersistentPath
    };
    public class DownloadedMedia
    {
        public string Filename;
        public DownloadedMediaType MediaType;
        /// <summary>
        /// The texture for this image, it's only set when
        /// 1) The image was downloaded this frame, so it was already in memory
        /// 2) We've loaded and used the image from a file, and when we did we set this
        /// </summary>
        public Texture2D texture;
    }
}
