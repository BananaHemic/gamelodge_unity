using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class LoadGamesViewWorldPanel : GenericSingleton<LoadGamesViewWorldPanel>
{
    public GameObject LoadingIcon;
    public GameObject SavedGameViewItemPrefab;
    public RectTransform ItemContainer;

    private Coroutine _loadGamesRoutine;
    private bool _isCoroutineRunning = false;
    private bool _hasRetrievedGames = false;
    private List<SavedGameMetadata> _allSavedGames;
    private List<SavedGameItemView> _savedGameViews;

    private IEnumerator LoadSavedGames()
    {
        _isCoroutineRunning = true;
        string address = string.Format("{0}:{1}/games", GLVars.Instance.APIAddress, GLVars.Instance.APIPort);
        Debug.Log("Loading saved games from " + address);
        UnityWebRequest webRequest = UnityWebRequest.Get(address);
        yield return webRequest.SendWebRequest();

        if (webRequest.isNetworkError)
        {
            Debug.LogError("Network error listing games " + webRequest.error);
            _isCoroutineRunning = false;
            yield break;
        }
        if (webRequest.isHttpError)
        {
            Debug.LogError("HTTP error listing games " + webRequest.responseCode);
            _isCoroutineRunning = false;
            yield break;
        }
        _isCoroutineRunning = false;
        _hasRetrievedGames = true;

        LoadingIcon.SetActive(false);

        Debug.Log("List recv: " + webRequest.downloadHandler.text);
        JArray json = JArray.Parse(webRequest.downloadHandler.text);
        Debug.Log("Loaded " + json.Count + " saves");
        if (_allSavedGames == null)
            _allSavedGames = new List<SavedGameMetadata>(json.Count);
        else
            _allSavedGames.Clear();

        if (_savedGameViews == null)
            _savedGameViews = new List<SavedGameItemView>(json.Count);
        else
        {
            foreach (var view in _savedGameViews)
                SimplePool.Instance.DespawnUI(view.gameObject);
            _savedGameViews.Clear();
        }

        for(int i = 0; i < json.Count; i++)
        {
            SavedGameMetadata savedGameMetaData = new SavedGameMetadata((JObject)json[i]);
            _allSavedGames.Add(savedGameMetaData);
            var viewObj = SimplePool.Instance.SpawnUI(SavedGameViewItemPrefab, ItemContainer);
            SavedGameItemView view = viewObj.GetComponent<SavedGameItemView>();
            view.Init(savedGameMetaData);
        }
    }
    public void OnPanelSelected()
    {
        if(!_hasRetrievedGames && !_isCoroutineRunning)
        {
            LoadingIcon.SetActive(true);
            _loadGamesRoutine = StartCoroutine(LoadSavedGames());
        }
    }
    public void OnSavedGameClicked(SavedGameMetadata savedGameMetaData)
    {
        Debug.Log("Will open " + savedGameMetaData);
        Orchestrator.Instance.GetGameStateAndLoad(savedGameMetaData);
    }
}
