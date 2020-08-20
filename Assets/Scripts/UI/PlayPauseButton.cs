using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayPauseButton : GenericSingleton<PlayPauseButton>
{
    public Sprite PlayIcon;
    public Sprite PauseIcon;
    public Image Image;

    /// <summary>
    /// The game can be paused for multiple reasons
    /// this flag is for if the game is paused b/c we
    /// or another user hit this button
    /// </summary>
    private bool _isPausedViaButton = false;
    /// <summary>
    /// If we've set the step frame button's
    /// interactability
    /// </summary>
    private bool _hasSetStepFrame = false;

    private void Start()
    {
        if(!_hasSetStepFrame)
            StepFrameButton.Instance.Button.interactable = false;
        _hasSetStepFrame = true;
    }

    public void OnPlayPauseClicked()
    {
        _isPausedViaButton = !_isPausedViaButton;
        Refresh();
            
        // Notify the server, and other clients
        DarkRiftConnection.Instance.SetPlayPause(!_isPausedViaButton);
    }
    private void Refresh()
    {
        if (_isPausedViaButton)
        {
            TimeManager.Instance.Pause(this);
            StepFrameButton.Instance.Button.interactable = true;
            Image.sprite = PlayIcon;
        }
        else
        {
            TimeManager.Instance.Play(this);
            StepFrameButton.Instance.Button.interactable = false;
            Image.sprite = PauseIcon;
        }
        _hasSetStepFrame = true;
    }
    public void OnServerSetPauseState(bool isPlaying, bool wasSetByUs)
    {
        if (!wasSetByUs)
        {
            _isPausedViaButton = !isPlaying;
            Refresh();
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
            OnPlayPauseClicked();
    }
}
