using UnityEngine;
using System.Collections;
using System;

namespace DarkRiftAudio
{
    [RequireComponent(typeof(AudioSource))]
    public class DarkRiftAudioPlayer : MonoBehaviour
    {
        public float Gain = 1;
        public ushort UserID { get; private set; }
        /// <summary>
        /// Notification that a new audio sample is available for processing
        /// It will be called on the audio thread
        /// It will contain the audio data, which you may want to process in
        /// your own code, and it contains the percent of the data left
        /// un-read
        /// </summary>
        public Action<float[], DRMouthPose, float> OnAudioSample;

        private DarkRiftAudioClient _audioClient;
        private AudioSource _audioSource;
        private bool _isPlaying = false;
        private float _pendingAudioVolume = -1f;

        void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            // In editor, double check that "auto-play" is turned off
#if UNITY_EDITOR
            if (_audioSource.playOnAwake)
                Debug.LogWarning("For best performance, please turn \"Play On Awake\" off");
#endif
            // In principle, this line shouldn't need to be here.
            // however, from profiling it seems that Unity will
            // call OnAudioFilterRead when the audioSource hits
            // Awake, even if PlayOnAwake is off
            _audioSource.Stop();

            if (_pendingAudioVolume >= 0)
                _audioSource.volume = _pendingAudioVolume;
            _pendingAudioVolume = -1f;
        }
        public void Initialize(DarkRiftAudioClient audioClient, ushort userID)
        {
            //Debug.Log("Initialized " + session, this);
            UserID = userID;
            _audioClient = audioClient;
            _audioClient.AudioPlayerInit(UserID);
        }
        public void Reset()
        {
            _audioClient = null;
            UserID = 0;
            OnAudioSample = null;
            _isPlaying = false;
            if (_audioSource != null)
                _audioSource.Stop();
            _pendingAudioVolume = -1f;
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioClient == null)
                return;
            //Debug.Log("Filter read for: " + GetUsername());

            int numRead = _audioClient.LoadArrayWithVoiceData(UserID, data, 0, data.Length, out DRMouthPose mouthPose);
            float percentUnderrun = 1f - numRead / data.Length;

            if (OnAudioSample != null)
                OnAudioSample(data, mouthPose, percentUnderrun);

            //Debug.Log("playing audio with avg: " + data.Average() + " and max " + data.Max());
            if (Gain == 1)
                return;

            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Clamp(data[i] * Gain, -1f, 1f);
            //Debug.Log("playing audio with avg: " + data.Average() + " and max " + data.Max());
        }
        public void SetVolume(float volume)
        {
            if (_audioSource == null)
                _pendingAudioVolume = volume;
            else
                _audioSource.volume = volume;
        }
        public void Destroy()
        {
            // We want to destroy this immediately, so that
            // we can reuse the ID as quickly as possible
            if (_audioClient != null)
                _audioClient.AudioPlayerRemoved(UserID);
            _audioClient = null;
        }
        private void OnDestroy()
        {
            if (Orchestrator.Instance.IsAppClosing)
                return;
            Destroy();
        }
        void Update()
        {
            if (_audioClient == null)
                return;
            if (!_isPlaying && _audioClient.HasPlayableAudio(UserID))
            {
                _audioSource.Play();
                _isPlaying = true;
                //Debug.Log("Playing audio for: " + GetUsername());
            }
            else if (_isPlaying && !_audioClient.HasPlayableAudio(UserID))
            {
                _audioSource.Stop();
                _isPlaying = false;
                //Debug.Log("Stopping audio for: " + GetUsername());
            }
        }
    }
}
