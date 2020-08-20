using Miniscript;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioPlayerBehavior : BaseBehavior
{
    public AudioClip SelectedAudioClip;
    public bool Autoplay;
    public bool Loop;
    public bool Spatialize = true;
    public AudioRolloffMode RollOff = AudioRolloffMode.Logarithmic;
    private readonly SerializedBundleItemReference _audioClipReference = new SerializedBundleItemReference(nameof(SelectedAudioClip));
    const int AudioClipKey = 0;
    const int AutoPlayKey = 1;
    const int LoopKey = 2;
    const int SpatializeKey = 3;
    const int RollOffKey = 4;

    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly List<ExposedVariable> _userVariables = new List<ExposedVariable>();
    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>();

    private static bool _hasLoadedIntrinsics = false;
    private AudioSource _audioSource;
    private bool _waitingOnAudioClipLoad = false;
    private bool _hasPendingClipPlayingState = false;
    private bool _pendingClipPlay = false;

    private string _loadedClipBundleID;
    private ushort _loadedClipBundleIndex;
    private int _currentlyLoadingID;
    private bool _cachedAutoplay = false;

    protected override void ChildInit()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1.0f;
        _audioSource.spatialize = Spatialize;
        base.AddBundleItemReference(_audioClipReference);
        // If possible, use the data in the Reference to
        // fill in the audio clip
        RefreshProperties();
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // AudioClip
        byte[] audioClipArray;
        if (_serializedBehavior.TryReadProperty(AudioClipKey, out audioClipArray, out int _))
            _audioClipReference.UpdateFrom(audioClipArray);
        // Autoplay
        byte[] autoplayArray;
        if (_serializedBehavior.TryReadProperty(AutoPlayKey, out autoplayArray, out int _))
        {
            Autoplay = BitConverter.ToBoolean(autoplayArray, 0);
            Debug.Log("Autoplay " + Autoplay);
        }
        // Loop
        byte[] loopArray;
        if (_serializedBehavior.TryReadProperty(LoopKey, out loopArray, out int _))
            Loop = BitConverter.ToBoolean(loopArray, 0);
        // Spatialize
        byte[] spatializeArray;
        if (_serializedBehavior.TryReadProperty(SpatializeKey, out spatializeArray, out int _))
            Spatialize = BitConverter.ToBoolean(spatializeArray, 0);
        // Rolloff
        byte[] rolloffArray;
        if (_serializedBehavior.TryReadProperty(RollOffKey, out rolloffArray, out int _))
            RollOff = (AudioRolloffMode)rolloffArray[0];
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        _serializedBehavior.LocallySetData(AudioClipKey, _audioClipReference.GetSerialized());
        _serializedBehavior.LocallySetData(AutoPlayKey, BitConverter.GetBytes(Autoplay));
        _serializedBehavior.LocallySetData(LoopKey, BitConverter.GetBytes(Loop));
        _serializedBehavior.LocallySetData(SpatializeKey, BitConverter.GetBytes(Spatialize));
        _serializedBehavior.LocallySetData(RollOffKey, new byte[] { (byte)RollOff });
    }
    void OnAudioClipLoaded(int loadID, AudioClip clip)
    {
        if (!_waitingOnAudioClipLoad)
            Debug.LogWarning("Clip loaded, but loading flag not set");
        _waitingOnAudioClipLoad = false;
        //Debug.Log("Audio clip loaded");
        if (SelectedAudioClip == clip)
        {
            //Debug.Log("Already have that clip selected");
            return;
        }
        if(_currentlyLoadingID != loadID)
        {
            Debug.LogWarning("Dropping audio load, was load ID #" + loadID + " expected " + _currentlyLoadingID);
            return;
        }
        SelectedAudioClip = clip;
        if(_audioSource != null)
        {
            _audioSource.clip = clip;
            if (_hasPendingClipPlayingState)
            {
                if (_pendingClipPlay)
                    _audioSource.Play();
                else
                    _audioSource.Stop();
            }
            else
            {
                // If there's no state pending, then play based on Autoplay
                if (Autoplay)
                    _audioSource.Play();
            }
            _hasPendingClipPlayingState = false;
        }
    }
    public override void RefreshProperties()
    {
        //Debug.Log("AudioPlayer refresh properties " + _audioClipReference.BundleID + " #" + _audioClipReference.BundleIndex);
        if (string.IsNullOrEmpty(_audioClipReference.BundleID))
        {
            SelectedAudioClip = null;
            _audioSource.clip = null;
            _loadedClipBundleID = null;
            _loadedClipBundleIndex = ushort.MaxValue;
        }
        else
        {
            if(_loadedClipBundleID != _audioClipReference.BundleID
                || _loadedClipBundleIndex != _audioClipReference.BundleIndex)
            {
                _loadedClipBundleID = _audioClipReference.BundleID;
                _loadedClipBundleIndex = _audioClipReference.BundleIndex;
                int loadID = ++_currentlyLoadingID;
                _waitingOnAudioClipLoad = true;
                BundleManager.Instance.LoadAudioClipFromBundle(_audioClipReference.BundleID, _audioClipReference.BundleIndex, loadID, OnAudioClipLoaded);
            }
        }
        _audioSource.loop = Loop;
        _audioSource.spatialize = Spatialize;
        _audioSource.rolloffMode = RollOff;
        if (Autoplay != _cachedAutoplay)
        {
            if (!_waitingOnAudioClipLoad)
            {
                if (Autoplay)
                    _audioSource.Play();
                else
                    _audioSource.Stop();
            }
            _cachedAutoplay = Autoplay;
        }
    }
    public override void Destroy()
    {
        if (_audioSource != null)
            GameObject.Destroy(_audioSource);
        _audioSource = null;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("PlayAudio");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Plays the selected audio clip, stopping any current playback", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                Debug.LogError("No scene object in intric call!");
                return Intrinsic.Result.Null;
            }

            AudioPlayerBehavior audioPlayerBehavior = sceneObject.GetBehaviorByType<AudioPlayerBehavior>();
            if(audioPlayerBehavior == null)
            {
                Debug.LogError("AudioPlayer behavior not present!");
                return new Intrinsic.Result(ValNumber.zero);
            }

            // If we're waiting on the clip to load, just mark a flag so that we begin play
            // once it loads
            if (audioPlayerBehavior._waitingOnAudioClipLoad)
            {
                audioPlayerBehavior._hasPendingClipPlayingState = true;
                audioPlayerBehavior._pendingClipPlay = true;
            }
            else
                audioPlayerBehavior._audioSource.Play();
            return new Intrinsic.Result(ValNumber.one);
		};

        intrinsic = Intrinsic.Create("StopAudio");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Stops audio playback", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                Debug.LogError("No scene object in intric call!");
                return Intrinsic.Result.Null;
            }

            AudioPlayerBehavior audioPlayerBehavior = sceneObject.GetBehaviorByType<AudioPlayerBehavior>();
            if(audioPlayerBehavior == null)
            {
                Debug.LogError("AudioPlayer behavior not present!");
                return Intrinsic.Result.False;
            }

            // If we're waiting on the clip to load, just mark a flag so that we stop
            // once it loads
            if (audioPlayerBehavior._waitingOnAudioClipLoad)
            {
                audioPlayerBehavior._hasPendingClipPlayingState = true;
                audioPlayerBehavior._pendingClipPlay = false;
            }
            else
                audioPlayerBehavior._audioSource.Stop();
            return Intrinsic.Result.True;
		};
        intrinsic = Intrinsic.Create("PlayAudioStacked");
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                Debug.LogError("No scene object in intric call!");
                return Intrinsic.Result.Null;
            }

            AudioPlayerBehavior audioPlayerBehavior = sceneObject.GetBehaviorByType<AudioPlayerBehavior>();
            if(audioPlayerBehavior == null)
            {
                Debug.LogError("AudioPlayer behavior not present!");
                return Intrinsic.Result.Null;
            }

            // We don't do the pendingPlay flag approach here,
            // as it's expected that this is for more ephemeral sounds
            //TODO we should clarify this behavior somewhere
            audioPlayerBehavior._audioSource.PlayOneShot(audioPlayerBehavior._audioSource.clip);
            return new Intrinsic.Result(ValNumber.one);
		};
        _userFunctions.Add(new ExposedFunction(intrinsic, "Plays the selected audio clip, on top of any currently playing clip", null));
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return false;
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
    public override List<ExposedEvent> GetEvents()
    {
        return _userEvents;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return _userVariables;
    }
}
