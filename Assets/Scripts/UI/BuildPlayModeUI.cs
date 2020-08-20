using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RLD;

public class BuildPlayModeUI : GenericSingleton<BuildPlayModeUI>
{
    public Sprite PlayButtonSprite;
    public Sprite BuildButtonSprite;
    public Image BuildPlayButtonImage;
    public BuildUIManager BuildUI;
    public CanvasGroup BuildGroup;
    public PlayUIManager PlayUI;
    public CanvasGroup PlayGroup;

    public float TransitionTime;
    public bool IsTransitioning { get; private set; }

    private Coroutine _transition;
    private float _progress;

    private void Start()
    {
        //Debug.LogWarning("BuildPlayModeUI configuring, for mode " + Orchestrator.Instance.CurrentMode);
        PlayUI.SetOn(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode);
        BuildUI.SetOn(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode);
        BuildPlayButtonImage.sprite = Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode ? BuildButtonSprite : PlayButtonSprite;
        Orchestrator.OnModeChange += OnVRModeChange;
    }
    private void OnVRModeChange(Orchestrator.Modes mode)
    {
        BuildPlayButtonImage.sprite = mode == Orchestrator.Modes.PlayMode ? BuildButtonSprite : PlayButtonSprite;
    }
    public void OnBuildPlayButtonClicked()
    {
        if(Orchestrator.Instance.CurrentMode == Orchestrator.Modes.PlayMode)
            Orchestrator.Instance.SetToMode(Orchestrator.Modes.BuildMode);
        else
            Orchestrator.Instance.SetToMode(Orchestrator.Modes.PlayMode);
    }
    void OnDisable()
    {
        if (_transition != null)
            Debug.LogError("Transition was not finished!");
    }
    IEnumerator TransitionRoutine(Orchestrator.Modes toMode)
    {
        IsTransitioning = true;
        if (_progress >= 1 || _progress == 0)
            _progress = 0;
        else
            _progress = 1 - _progress;

        // Turn all modes on, but not interactable
        PlayUI.SetVisibilityAndInteractability(true, false);
        BuildUI.SetVisibilityAndInteractability(true, false);

        while (_progress <= 1)
        {
            float buildAlpha = 0;
            float playAlpha = 0;
            if(toMode == Orchestrator.Modes.BuildMode)
            {
                buildAlpha = _progress;
                playAlpha = 1 - _progress;
            }else if(toMode == Orchestrator.Modes.PlayMode)
            {
                buildAlpha = 1 - _progress;
                playAlpha = _progress;
            }
            BuildGroup.alpha = buildAlpha;
            PlayGroup.alpha = playAlpha;

            _progress += TimeManager.Instance.RenderUnscaledDeltaTime / TransitionTime;
            yield return null;
        }
        _progress = 1;
        if(toMode == Orchestrator.Modes.BuildMode)
        {
            BuildGroup.alpha = 1;
        }else if(toMode == Orchestrator.Modes.PlayMode)
        {
            PlayGroup.alpha = 1;
        }
        // 
        PlayUI.SetOn(toMode == Orchestrator.Modes.PlayMode);
        BuildUI.SetOn(toMode == Orchestrator.Modes.BuildMode);

        IsTransitioning = false;
        _transition = null;
    }
    public void TransitionToMode(Orchestrator.Modes toMode)
    {
        //Debug.Log("Transitioning to: " + toMode);
        if (_transition != null)
            StopCoroutine(_transition);
        _transition = StartCoroutine(TransitionRoutine(toMode));
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            var toMode = Orchestrator.Instance.CurrentMode == Orchestrator.Modes.BuildMode
                ? Orchestrator.Modes.PlayMode
                : Orchestrator.Modes.BuildMode;
            Orchestrator.Instance.SetToMode(toMode);
            //ToggleMode();
        }
    }
}
