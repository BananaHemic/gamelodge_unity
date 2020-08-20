using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DarkRift;

namespace DarkRiftAudio
{
    public class ManageAudioSendBuffer : IDisposable
    {
        private readonly AudioEncodingBuffer _encodingBuffer;
        private readonly List<PcmArray> _pcmArrays;
        private readonly DarkRiftAudioClient _audioClient;
        private readonly AutoResetEvent _waitHandle;
        private OpusEncoder _encoder;
        private bool _isRunning;

        private Thread _encodingThread;
        private uint sequenceIndex;
        private bool _stopSendingRequested = false;
        private int _pendingBitrate = 0;
        /// <summary>
        /// How long of a duration, in ms should there be
        /// between sending two packets. This helps
        /// ensure that fewer udp packets are dropped
        /// </summary>
        const long MinSendingElapsedMilliseconds = 5;
        /// <summary>
        /// How many pending uncompressed buffers
        /// are too many to use any sleep. This
        /// is so that the sleep never causes us
        /// to have an uncompressed buffer overflow
        /// </summary>
        const int MaxPendingBuffersForSleep = 4;
        /// <summary>
        /// How big we anticipate a full audio packet
        /// to be, rounded up just in case
        /// </summary>
        const int ExpectedAudioPktLen = 128;

        public ManageAudioSendBuffer(DarkRiftAudioClient mumbleClient)
        {
            _isRunning = true;
            _audioClient = mumbleClient;
            _pcmArrays = new List<PcmArray>();
            _encodingBuffer = new AudioEncodingBuffer();
            _waitHandle = new AutoResetEvent(false);
        }
        public void SetWriteLatestMouthPoseFunc(DarkRiftAudioClient.WriteLatestMouthPose writeMouthPoseFunc)
        {
            _encodingBuffer.SetWriteLatestMouthPoseFunc(writeMouthPoseFunc);
        }
        internal void InitForSampleRate(int sampleRate)
        {
            if(_encoder != null)
            {
                Debug.LogError("Destroying opus encoder");
                _encoder.Dispose();
                _encoder = null;
            }
            _encoder = new OpusEncoder(sampleRate, 1) { EnableForwardErrorCorrection = false };

            if(_pendingBitrate > 0)
            {
                Debug.Log("Using pending bitrate");
                SetBitrate(_pendingBitrate);
            }

            if (_encodingThread == null)
            {
                _encodingThread = new Thread(EncodingThreadEntry)
                {
                    IsBackground = true
                };
                _encodingThread.Start();
            }
        }
        public int GetBitrate()
        {
            if (_encoder == null)
                return -1;
            return _encoder.Bitrate;
        }
        public void SetBitrate(int bitrate)
        {
            if(_encoder == null)
            {
                // We'll use the provided bitrate once we've created the encoder
                _pendingBitrate = bitrate;
                return;
            }
            _encoder.Bitrate = bitrate;
        }
        ~ManageAudioSendBuffer()
        {
            Dispose();
        }
        public PcmArray GetAvailablePcmArray()
        {
            foreach(PcmArray ray in _pcmArrays)
            {
                if (ray._refCount == 0)
                {
                    ray.Ref();
                    //Debug.Log("re-using buffer");
                    return ray;
                }
            }
            PcmArray newArray = new PcmArray(_audioClient.NumSamplesPerOutgoingPacket, _pcmArrays.Count);
            _pcmArrays.Add(newArray);

            if(_pcmArrays.Count > 10)
            {
                Debug.LogWarning(_pcmArrays.Count + " audio buffers in-use. There may be a leak");
            }
            //Debug.Log("New buffer length is: " + _pcmArrays.Count);
            return newArray;
        }
        public void SendVoice(PcmArray pcm)
        {
            _stopSendingRequested = false;
            _encodingBuffer.Add(pcm);
            _waitHandle.Set();
        }
        public void SendVoiceStopSignal()
        {
            _encodingBuffer.Stop();
            _stopSendingRequested = true;
        }
        public void Dispose()
        {
            _isRunning = false;
            _waitHandle.Set();

            if(_encodingThread != null)
                _encodingThread.Abort();
            _encodingThread = null;
            if(_encoder != null)
                _encoder.Dispose();
            _encoder = null;
        }
        private void EncodingThreadEntry()
        {
            // Wait for an initial voice packet
            _waitHandle.WaitOne();
            //Debug.Log("Starting encoder thread");
            bool isLastPacket = false;

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            DRMouthPose mouthPose = new DRMouthPose();

            while (true)
            {
                if(!_isRunning)
                    return;
                try
                {
                    // Keep running until a stop has been requested and we've encoded the rest of the buffer
                    // Then wait for a new voice packet
                    while (_stopSendingRequested && isLastPacket)
                        _waitHandle.WaitOne();
                    if(!_isRunning)
                        return;

                    bool isEmpty;
                    ArraySegment<byte> encodedArray = _encodingBuffer.Encode(_encoder, mouthPose, out isLastPacket, out isEmpty);

                    if (isEmpty && !isLastPacket)
                    {
                        // This should not normally occur
                        Thread.Sleep(DarkRiftAudio.DarkRiftAudioConstants.FRAME_SIZE_MS);
                        Debug.LogWarning("Empty Packet");
                        continue;
                    }
                    if (isLastPacket)
                        Debug.Log("Will send last packet");

                    using (DarkRiftWriter packetWriter = DarkRiftWriter.Create(ExpectedAudioPktLen))
                    {
                        // Write packet type
                        byte pktType = _audioClient.GetCurrentAudioSendType();
                        //Mark the leftmost bit if this is the last packet
                        if (isLastPacket)
                        {
                            pktType |= (1 << 7);
                            Debug.Log("Adding end flag");
                        }
                        packetWriter.Write(pktType);

                        // Write the sequence index
                        packetWriter.Write(sequenceIndex);
                        // Write the length of the audio packet
                        if(encodedArray.Count > byte.MaxValue)
                        {
                            Debug.LogError("Packet too large!!!");
                            return;
                        }
                        packetWriter.Write((byte)encodedArray.Count);
                        // Write the compressed audio data
                        packetWriter.WriteRaw(encodedArray.Array, encodedArray.Offset, encodedArray.Count);
                        // Write the mouth pose data
                        packetWriter.Write(mouthPose);
                        //Debug.Log("seq: " + sequenceIndex + " final len: " + finalPacket.Length + " pos: " + buff.PositionalDataLength);
                        //Debug.Log("seq: " + sequenceIndex + " | " + finalPacket.Length);

                        stopwatch.Stop();
                        long timeSinceLastSend = stopwatch.ElapsedMilliseconds;
                        //Debug.Log("Elapsed: " + timeSinceLastSend + " pending: " + _encodingBuffer.GetNumUncompressedPending());

                        if (timeSinceLastSend < MinSendingElapsedMilliseconds
                            && _encodingBuffer.GetNumUncompressedPending() < MaxPendingBuffersForSleep)
                        {
                            Thread.Sleep((int)(MinSendingElapsedMilliseconds - timeSinceLastSend));
                            //Debug.Log("Slept: " + stopwatch.ElapsedMilliseconds);
                        }


                        //Debug.Log("Full audio size: " + packetWriter.Length);
                        _audioClient.SendAudioPacketThreaded(packetWriter);
                    }
                    
                    sequenceIndex += DarkRiftAudioConstants.NUM_FRAMES_PER_OUTGOING_PACKET;
                    //If we've hit a stop packet, then reset the seq number
                    if (isLastPacket)
                        sequenceIndex = 0;
                    stopwatch.Reset();
                    stopwatch.Start();
                }
                catch (Exception e){
                    if(e is System.Threading.ThreadAbortException)
                    {
                        // This is ok
                        break;
                    }
                    else
                    {
                        Debug.LogError("Error: " + e);
                    }
                }
            }
            Debug.Log("Terminated encoding thread");
        }
    }
    /// <summary>
    /// Small class to help this script re-use float arrays after their data has become encoded
    /// Obviously, it's weird to ref-count in a managed environment, but it really
    /// Does help identify leaks and makes zero-copy buffer sharing easier
    /// </summary>
    public class PcmArray
    {
        public readonly int Index;
        public float[] Pcm;
        internal int _refCount;

        public PcmArray(int pcmLength, int index)
        {
            Pcm = new float[pcmLength];
            Index = index;
            _refCount = 1;
        }
        public void Ref()
        {
            _refCount++;
        }
        public void UnRef()
        {
            _refCount--;
            if(_refCount < 0)
                Debug.LogError("Too many unrefs! " + _refCount);
        }
    }
}
