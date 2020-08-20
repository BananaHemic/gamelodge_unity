using System;
using System.Net;
using UnityEngine;
using System.Collections.Generic;
using DarkRift;

namespace DarkRiftAudio
{
    public class DarkRiftAudioClient
    {
        // Actions for non-main threaded events
        public Action<uint> OnRecvAudioThreaded;

        private DecodingBufferPool _decodingBufferPool;
        private AudioDecodeThread _audioDecodeThread;
        private ManageAudioSendBuffer _manageSendBuffer;
        private DarkRiftMicrophone _mumbleMic;
        private readonly int _outputSampleRate;
        private readonly int _outputChannelCount;
        private readonly ushort _ourPlayerID;
        private byte _currentAudioType;

        public delegate void SendVoicePacketThreaded(DarkRiftWriter voicePacketWriter);
        private readonly SendVoicePacketThreaded _sendVoicePacketThreaded;
        /// <summary>
        /// Called on the encoding thread when the array is about to be
        /// compressed. You should use the PcmArray to load DRMouthPose
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public delegate void WriteLatestMouthPose(PcmArray data, DRMouthPose mouthPose);


        // TODO there are data structures that would be a lot faster here
        private readonly Dictionary<ushort, DecodedAudioBuffer> _audioDecodingBuffers = new Dictionary<ushort, DecodedAudioBuffer>();

        public int EncoderSampleRate { get; private set; }
        public int NumSamplesPerOutgoingPacket { get; private set; }

        public DarkRiftAudioClient(SendVoicePacketThreaded sendVoicePacketThreaded, ushort ourPlayerID=0)
        {
            _sendVoicePacketThreaded = sendVoicePacketThreaded;
            _ourPlayerID = ourPlayerID;

            switch (AudioSettings.outputSampleRate)
            {
                case 8000:
                case 12000:
                case 16000:
                case 24000:
                case 48000:
                    _outputSampleRate = AudioSettings.outputSampleRate;
                    break;
                default:
                    Debug.LogError("Incorrect sample rate of:" + AudioSettings.outputSampleRate + ". It should be 48000 please set this in Edit->Audio->SystemSampleRate");
                    _outputSampleRate = 48000;
                    break;
            }
            //Debug.Log("Using output sample rate: " + _outputSampleRate);

            switch (AudioSettings.speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    // TODO sometimes, even though the speaker mode is mono,
                    // on audiofilterread wants two channels
                    _outputChannelCount = 1;
                    break;
                case AudioSpeakerMode.Stereo:
                    _outputChannelCount = 2;
                    break;
                default:
                    Debug.LogError("Unsupported speaker mode " + AudioSettings.speakerMode + " please set this in Edit->Audio->DefaultSpeakerMode to either Mono or Stereo");
                    _outputChannelCount = 2;
                    break;
            }
            //Debug.Log("Using output channel count of: " + _outputChannelCount);


            _audioDecodeThread = new AudioDecodeThread(_outputSampleRate, _outputChannelCount, this);
            _decodingBufferPool = new DecodingBufferPool(_audioDecodeThread);
            _manageSendBuffer = new ManageAudioSendBuffer(this);
        }
        public void SetCurrentAudioType(byte audioType)
        {
            if((audioType & ~(1 << 7)) != 0)
            {
                Debug.LogError("Please leave leftmost bit unset for audio type! Provided: " + audioType);
            }
            _currentAudioType = audioType;
        }
        public void SetWriteLatestMouthPoseFunc(WriteLatestMouthPose writeMouthPoseFunc)
        {
            _manageSendBuffer.SetWriteLatestMouthPoseFunc(writeMouthPoseFunc);
        }
        internal byte GetCurrentAudioSendType()
        {
            return _currentAudioType;
        }
        public void AddCompressedAudioPacket(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
        {
            _audioDecodeThread.AddCompressedAudio(reader, msgDir, isLocalRecordedMessage);
        }
        internal void SendAudioPacketThreaded(DarkRiftWriter writer)
        {
            _sendVoicePacketThreaded(writer);
        }
        public void AddMumbleMic(DarkRiftMicrophone newMic)
        {
            _mumbleMic = newMic;
            _mumbleMic.Initialize(this);
            EncoderSampleRate = _mumbleMic.InitializeMic();

            if (EncoderSampleRate == -1)
                return;
            
            NumSamplesPerOutgoingPacket = DarkRiftAudioConstants.NUM_FRAMES_PER_OUTGOING_PACKET * EncoderSampleRate / 100;
            _manageSendBuffer.InitForSampleRate(EncoderSampleRate);
        }
        internal PcmArray GetAvailablePcmArray()
        {
            return _manageSendBuffer.GetAvailablePcmArray();
        }
        internal int GetBitrate()
        {
            return _manageSendBuffer.GetBitrate();
        }
        internal void SetBitrate(int bitrate)
        {
            _manageSendBuffer.SetBitrate(bitrate);
        }
        internal void AudioPlayerInit(ushort playerUserID)
        {
            // Make sure we don't double add
            if (_audioDecodingBuffers.ContainsKey(playerUserID))
                return;

            //Debug.Log("Adding decoder session #" + userState.Session);
            DecodedAudioBuffer buffer = _decodingBufferPool.GetDecodingBuffer();
            buffer.Init(playerUserID);
            _audioDecodingBuffers.Add(playerUserID, buffer);
        }
        internal void AudioPlayerRemoved(ushort playerUserID)
        {
            DecodedAudioBuffer buffer;
            if(_audioDecodingBuffers.TryGetValue(playerUserID, out buffer))
            {
                Debug.Log("Removing decoder session #" + playerUserID);
                _audioDecodingBuffers.Remove(playerUserID);
                _decodingBufferPool.ReturnDecodingBuffer(buffer);
            }
        }
        public void Close()
        {
            //Debug.Log("Closing DR Audio");
            if(_manageSendBuffer != null)
                _manageSendBuffer.Dispose();
            _manageSendBuffer = null;
            if (_audioDecodeThread != null)
                _audioDecodeThread.Dispose();
            _audioDecodeThread = null;
            //Debug.Log("DR Audio");
        }
        internal void SendVoicePacket(PcmArray floatData)
        {
            if(_manageSendBuffer != null)
                _manageSendBuffer.SendVoice(floatData);
        }
        internal void ReceiveDecodedVoice(ushort playerID, DecodedAudioArray decodedAudio, bool reevaluateInitialBuffer)
        {
            DecodedAudioBuffer decodingBuffer;
            if (_audioDecodingBuffers.TryGetValue(playerID, out decodingBuffer))
            {
                decodingBuffer.AddDecodedAudio(decodedAudio, reevaluateInitialBuffer);
            }
            else
            {
                // This is expected if the user joins a room where people are already talking
                // Buffers will be dropped until the decoding buffer has been created
                Debug.LogWarning("No decoding buffer found for session:" + playerID);
                decodedAudio.UnRef();
            }
        }
        internal bool HasPlayableAudio(ushort playerID)
        {
            DecodedAudioBuffer decodingBuffer;
            if (_audioDecodingBuffers.TryGetValue(playerID, out decodingBuffer))
            {
                if (!decodingBuffer.HasFilledInitialBuffer)
                    return false;
                return true;
            }
            return false;
        }
        internal int LoadArrayWithVoiceData(ushort playerID, float[] pcmArray, int offset, int length, out DRMouthPose mouthPose)
        {
            //Debug.Log("Will decode for " + session);
            //TODO use bool to show if loading worked or not
            DecodedAudioBuffer decodingBuffer;
            if (_audioDecodingBuffers.TryGetValue(playerID, out decodingBuffer))
            {
                int numRead = decodingBuffer.Read(pcmArray, offset, length);
                mouthPose = decodingBuffer.GetLatestMouthPose();
                return numRead;
            }
            else
                Debug.LogWarning("Decode buffer not found for session " + playerID);
            mouthPose = null;
            return -1;
        }
        /// <summary>
        /// Tell the encoder to send the last audio packet, then reset the sequence number
        /// </summary>
        internal void StopSendingVoice()
        {
            if(_manageSendBuffer != null)
                _manageSendBuffer.SendVoiceStopSignal();
        }
        internal static int GetNearestSupportedSampleRate(int listedRate)
        {
            int currentBest = -1;
            int currentDifference = int.MaxValue;

            for(int i = 0; i < DarkRiftAudioConstants.SUPPORTED_SAMPLE_RATES.Length; i++)
            {
                if(Math.Abs(listedRate - DarkRiftAudioConstants.SUPPORTED_SAMPLE_RATES[i]) < currentDifference)
                {
                    currentBest = DarkRiftAudioConstants.SUPPORTED_SAMPLE_RATES[i];
                    currentDifference = Math.Abs(listedRate - DarkRiftAudioConstants.SUPPORTED_SAMPLE_RATES[i]);
                }
            }

            return currentBest;
        }
    }
}