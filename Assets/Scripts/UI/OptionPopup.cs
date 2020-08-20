using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OptionPopup : GenericSingleton<OptionPopup>, ILoopScrollDataSource, ILoopScrollPrefabSource
{
    public delegate void OptionSelectedCallback(bool wasCancel, int selectedIndex);
    private enum SpriteMode
    {
        AllSameSprite,
        SpriteList
    };

    public LoopVerticalScrollRect ScrollRect;
    public TMP_Text TitleText;
    public GameObject OptionButtonPrefab;
    public RectTransform OptionContainer;
    public Image Background;

    //private readonly List<GameObject> _loadedOptions = new List<GameObject>();
    private OptionSelectedCallback _currentCallback;
    private CanvasToggle _canvasToggle;
    private SpriteMode _currentSpriteMode;
    private readonly List<string> _workingOptionStringList = new List<string>();
    private readonly List<Sprite> _workingOptionSpriteList = new List<Sprite>();
    private readonly List<int> _callbackData = new List<int>();

    void Start()
    {
        _canvasToggle = GetComponent<CanvasToggle>();
        ScrollRect.Init(this, this);
    }
    public void GetListsToLoadInto(out List<string> optionList, out List<int> callbackIDs, out List<Sprite> optionIcons)
    {
        _callbackData.Clear();
        _workingOptionStringList.Clear();
        _workingOptionSpriteList.Clear();
        optionList = _workingOptionStringList;
        callbackIDs = _callbackData;
        optionIcons = _workingOptionSpriteList;
    }
    public void LoadOptions(string titleText, List<string> optionTexts, Sprite optionIcon, OptionSelectedCallback callback, List<int> callbackData)
    {
        //Debug.Log("Init spriteList allsame, num " + optionTexts.Count);
        _currentCallback = callback;
        _currentSpriteMode = SpriteMode.AllSameSprite;
        TitleText.text = titleText;
        if(!Object.ReferenceEquals(_callbackData, callbackData))
        {
            Debug.LogError("Please use the callback data list from GetListsToLoadInto");
            _callbackData.Clear();
            _callbackData.AddRange(callbackData);
        }
        if (_workingOptionSpriteList.Count > 0)
            Debug.LogError("Dirty sprite list: " + _workingOptionStringList.Count);
        if(!Object.ReferenceEquals(_workingOptionStringList, optionTexts))
            Debug.LogError("Please use the string list from GetListsToLoadInto");

        _workingOptionSpriteList.Add(optionIcon);
        ScrollRect.ClearCells();
        for(int i = 0; i < optionTexts.Count; i++)
            ScrollRect.AddItem(null, false);
        ScrollRect.RefillCells();
        OptionPopupTransition.Instance.OpenOptions();
    }
    public void LoadOptions(string titleText, List<string> optionTexts, List<Sprite> optionIcons, OptionSelectedCallback callback, List<int> callbackData)
    {
        //Debug.Log("Init spriteList options, num " + optionTexts.Count);
        _currentCallback = callback;
        TitleText.text = titleText;
        _currentSpriteMode = SpriteMode.SpriteList;
        if(!Object.ReferenceEquals(_callbackData, callbackData))
        {
            Debug.LogError("Please use the callback data list from GetListsToLoadInto");
            _callbackData.Clear();
            _callbackData.AddRange(callbackData);
        }
        if(!Object.ReferenceEquals(_workingOptionSpriteList, optionIcons))
            Debug.LogError("Please use the sprite list from GetListsToLoadInto");
        if(!Object.ReferenceEquals(_workingOptionStringList, optionTexts))
            Debug.LogError("Please use the string list from GetListsToLoadInto");
        if(optionTexts.Count != optionIcons.Count)
        {
            Debug.LogError("Inconsistent sprite icon lengths!");
            return;
        }

        ScrollRect.ClearCells();
        for(int i = 0; i < optionTexts.Count; i++)
            ScrollRect.AddItem(null, false);
        ScrollRect.RefillCells();
        OptionPopupTransition.Instance.OpenOptions();
    }

    public void OnCancelClicked()
    {
        if (_currentCallback != null)
            _currentCallback(true, 0);
        Close();
    }
    public void OnOptionSelected(int index)
    {
        if (_currentCallback != null)
            _currentCallback(false, _callbackData[index]);
        Close();
    }
    public void SetActive(bool active)
    {
        _canvasToggle.SetOn(active);
        // Clear out the cells when we're fully closed, just for perf
        if (!active)
        {
            _workingOptionSpriteList.Clear();
            _workingOptionSpriteList.Clear();
            ScrollRect.ClearCells();
        }
    }
    public void Close()
    {
        OptionPopupTransition.Instance.CloseOptions();
    }
    public GameObject GetObject(Transform parent)
    {
        return SimplePool.Instance.SpawnUI(OptionButtonPrefab, parent);
    }
    public void ReturnObject(GameObject go)
    {
        SimplePool.Instance.DespawnUI(go);
    }
    public void ProvideData(GameObject go, int idx, object userData)
    {
        OptionButton optionButton = go.GetComponent<OptionButton>();
        //Debug.Log("Loading idx " + idx);
        Sprite icon = _currentSpriteMode == SpriteMode.AllSameSprite ? _workingOptionSpriteList[0] : _workingOptionSpriteList[idx];
        optionButton.Init(icon, _workingOptionStringList[idx], idx);
    }
}
