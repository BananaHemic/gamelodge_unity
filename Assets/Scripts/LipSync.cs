using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRiftAudio;
using System;
using System.Text;

/// <summary>
/// Takes audio and moves a mouth mesh accordingly.
/// Used for both other users audio, and our mic audio
/// Other user's mouth pose is received from the DarkRiftAudioPlayer OnAudioSample signal,
/// which is called on the audio thread
/// Our user's audio is received from the DarkRiftMicrophone OnMicData signal, 
/// which is called on the main thread
/// </summary>
public class LipSync : MonoBehaviour
{
    public float Gain = 2f;
    public float MouthBlendSmoothness = 50f;
    private UserDisplay _userDisplay;
    private DarkRiftAudioPlayer _audioPlayer;
    private bool _hasInit = false;
    private readonly float[] _currentVisemes = new float[DRMouthPose.NumPhonemes];
    private readonly float[] _appliedVisemes = new float[DRMouthPose.NumPhonemes];
    private bool _hasViseme = false;

    // Local stuff for when we're using our mic
    private uint _lipSyncContext = 0;	// 0 is no context
    private OVRLipSync.Frame _frame;
    private static int _dspLength = 0;
    private float[] _sizedMicAudio;
    // Array resegment is used on PC to create properly DSP sized buffers
    // for the OVR LipSync
    private ArrayResegment _arrayResegment;
    private readonly OVRLipSync.ContextProviders _provider = OVRLipSync.ContextProviders.Enhanced_with_Laughter;

    public void InitLocal(UserDisplay userDisplay)
    {
        if (_hasInit)
        {
            Debug.LogError("Not double initializing LipSync!", this);
            return;
        }
        _hasInit = true;
        _userDisplay = userDisplay;
        _frame = new OVRLipSync.Frame();
        lock (this)
        {
            if (OVRLipSync.CreateContext(ref _lipSyncContext, _provider) != OVRLipSync.Result.Success)
            {
                Debug.LogError("OVRPhonemeContext.Start ERROR: Could not create Phoneme context.");
                return;
            }
        }
        DarkRiftConnection.Instance.AudioClient.SetWriteLatestMouthPoseFunc(GenerateMouthPose);
        // Set the dsp buffer size if needed
        if (_dspLength == 0)
        {
            int dspLength;
            int junk;
            AudioSettings.GetDSPBufferSize(out dspLength, out junk);
            // TODO should be set dynamically
            _dspLength = dspLength * 2;
            //Debug.Log("Found dsp to be: " + _dspLength);
        }
        _sizedMicAudio = new float[_dspLength];
        _arrayResegment = new ArrayResegment((3 * _dspLength) / 2);

        if ((OVRLipSync.IsInitialized() != OVRLipSync.Result.Success))
        {
            Debug.LogError("LipSync not initialized?");
            return;
        }
    }
    public void InitNetwork(UserDisplay userDisplay, DarkRiftAudioPlayer audioPlayer)
    {
        if (_hasInit)
        {
            Debug.LogError("Not double initializing LipSync!", this);
            return;
        }
        _hasInit = true;
        _userDisplay = userDisplay;
        _audioPlayer = audioPlayer;
        _audioPlayer.OnAudioSample += OnAudioSample;
    }

    private static int Audio2Amplitude(float[] data, float percUnderrun)
    {
        // Lip sync is too expensive on mobile, so we just lazily
        // calulate the energy
        float sumOfSquared = 0;
        int numSamples = Mathf.RoundToInt(data.Length * (1f - percUnderrun));
        int i = 0;
        while (i < numSamples)
        {
            float val = data[i++];
            sumOfSquared += val * val;
        }

        float rms = Mathf.Sqrt(sumOfSquared / numSamples);
        const float AmplitudeScale = 7 * 100f;
        //Debug.Log("rms: " + rms);
        return Mathf.RoundToInt(Mathf.Clamp(rms * AmplitudeScale, 0, 100));
    }
    // Called on the audio thread
    void OnAudioSample(float[] pcm, DRMouthPose mouthPose, float percFilled)
    {
        //StringBuilder sb = new StringBuilder();
        //sb.Append("Recv: [");
        //for(int i = 0; i < DRMouthPose.NumPhonemes; i++)
        //{
        //    sb.Append(mouthPose.PhonemeWeights[i]);
        //    sb.Append(",");
        //}
        //sb.Append("]");
        //Debug.Log(sb.ToString());
        OnNewMouthPose(mouthPose);
    }
    // Called on the encoding thread
    void GenerateMouthPose(PcmArray data, DRMouthPose mouthPose)
    {
        if (data == null)// This will happen with stop packets
            return;
        _arrayResegment.Push(data);

        bool didPull = _arrayResegment.TryPullSize(_dspLength, _sizedMicAudio, false);
        // If we don't have enough data for a new LipSync calculation
        // just use the previous value
        if (!didPull)
            return;

        OVRLipSync.ProcessFrame(_lipSyncContext, _sizedMicAudio, _frame, false);
        // Copy the data from our lip sync frame into DRMouthPose
        // Viseme blend weights are in range of 0->1.0, we need to make range 100
        for(int i = 0; i < mouthPose.PhonemeWeights.Length - 1; i++)
        {
            float weight = Mathf.Clamp01(_frame.Visemes[i] * Gain) * 100.0f;
            mouthPose.PhonemeWeights[i] = weight;
        }
        // Laughter is handled separately
        float laughterWeight = Mathf.Clamp01(_frame.laughterScore * Gain) * 100.0f;
        mouthPose.PhonemeWeights[mouthPose.PhonemeWeights.Length - 1] = laughterWeight;

        OnNewMouthPose(mouthPose);
        //StringBuilder sb = new StringBuilder();
        //sb.Append("Sending: [");
        //for(int i = 0; i < DRMouthPose.NumPhonemes; i++)
        //{
        //    sb.Append(_mouthPose.PhonemeWeights[i]);
        //    sb.Append(",");
        //}
        //sb.Append("]");
        //Debug.Log(sb.ToString());
    }
    void OnNewMouthPose(DRMouthPose mouthPose)
    {
        if (mouthPose == null)
            return;//TODO this seems to happen sometimes, not certain why
        if (this == null)
            return;
        lock (this)
        {
            if (!_hasInit)
                return;
            if (mouthPose == null)
                Debug.LogError("null mouth pose");
            else if (mouthPose.PhonemeWeights == null)
                Debug.LogError("null phoneme weights");
            if (_currentVisemes == null)
                Debug.LogError("null curr visemes");
            // Copy new data to current
            _hasViseme = true;
            Array.Copy(mouthPose.PhonemeWeights, _currentVisemes, DRMouthPose.NumPhonemes);
        }
    }
    public void SetModelToLastViseme()
    {
        if (!_hasViseme)
            return;

        CharacterBehavior possessedBehavior = _userDisplay.PossessedBehavior;
        if (possessedBehavior == null)
            return;
        SkinnedMeshRenderer meshRenderer = possessedBehavior.AvatarMeshs.Length > 0 ? possessedBehavior.AvatarMeshs[0] : null;
        if (meshRenderer == null)// Happens when the user is in play mode, or loading their avatar
            return;

        // Apply the lerped mouth movements
        if(possessedBehavior.AvatarLipType == CharacterBehavior.LipSyncType.Viseme16)
        {
            for (int i = 0; i < DRMouthPose.NumPhonemes; i++)
            {
                float val = Mathf.Lerp(_appliedVisemes[i], _currentVisemes[i], Mathf.Exp(-MouthBlendSmoothness * TimeManager.Instance.RenderUnscaledDeltaTime));
                _appliedVisemes[i] = val;
                meshRenderer.SetBlendShapeWeight(i, val);
            }
        }
    }
    void Update()
    {
        if (!_hasInit)
            return;

        SetModelToLastViseme();
        /* This just shows how in sync the audio is relative to everything else
        int micPos = _mic.GetMicPosition();
        int ourPos = _src.timeSamples;
        if (micPos == ourPos)
            Debug.Log(micPos + "|" + ourPos);
        else
            Debug.LogWarning(micPos + "|" + ourPos + "  " + (micPos - ourPos));
        */
    }

#if UNITY_EDITOR
    void OnDestroy()
    {
        lock (this)
        {
            _hasInit = false;
            if (_lipSyncContext != 0)
            {
                // Don't bother deleting the context if we're shutting down and the app is
                // not in editor mode
                if (!Orchestrator.Instance.IsAppClosing || Application.isEditor)
                {
                    if (OVRLipSync.DestroyContext(_lipSyncContext) != OVRLipSync.Result.Success)
                        Debug.LogError("OVRPhonemeContext.OnDestroy ERROR: Could not delete Phoneme context.");
                }
                if (!Orchestrator.Instance.IsAppClosing)
                    DarkRiftConnection.Instance.AudioClient.SetWriteLatestMouthPoseFunc(null);
            }
        }
    }
#endif
}
