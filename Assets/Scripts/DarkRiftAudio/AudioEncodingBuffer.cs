/*
 * This puts data from the mics taken on the main thread
 * Then another thread pulls frame data out
 * 
 * We now assume that each mic packet placed into the buffer is an acceptable size
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace DarkRiftAudio
{
    public class AudioEncodingBuffer
    {
        private readonly Queue<TargettedSpeech> _unencodedBuffer = new Queue<TargettedSpeech>();

        //TODO not certain on this
        public readonly ArraySegment<byte> EmptyByteSegment = new ArraySegment<byte>(new byte[0] {});

        private readonly object _bufferLock = new System.Object();
        private readonly object _writePosFuncLock = new System.Object();
        private volatile bool _isWaitingToSendLastPacket = false;
        private DarkRiftAudioClient.WriteLatestMouthPose _writeLatestMouthPoseFunc;

        public void SetWriteLatestMouthPoseFunc(DarkRiftAudioClient.WriteLatestMouthPose writeMouthDataFunc)
        {
            // This is called from other threads, so use a lock to be extra safe
            lock(_writePosFuncLock)
                _writeLatestMouthPoseFunc = writeMouthDataFunc;
        }
        /// <summary>
        /// Add some raw PCM data to the buffer to send
        /// </summary>
        /// <param name="pcm"></param>
        /// <param name="target"></param>
        /// <param name="targetId"></param>
        public void Add(PcmArray pcm)
        {
            lock (_bufferLock)
            {
                _unencodedBuffer.Enqueue(new TargettedSpeech(pcm));
                Monitor.Pulse(_bufferLock);
            }
        }

        public void Stop()
        {
            lock (_bufferLock)
            {
                //If we still have an item in the queue, mark the last one as last
                _isWaitingToSendLastPacket = true;
                if (_unencodedBuffer.Count == 0)
                {
                    Debug.Log("Adding stop packet");
                    _unencodedBuffer.Enqueue(new TargettedSpeech(stop: true));
                }
                else
                    Debug.Log("Marking last packet");
                Monitor.Pulse(_bufferLock);
            }
        }
        public int GetNumUncompressedPending()
        {
            return _unencodedBuffer.Count;
        }

        public ArraySegment<byte> Encode(OpusEncoder encoder, DRMouthPose poseToUpdate, out bool isStop, out bool isEmpty)
        {
            isStop = false;
            isEmpty = false;
            PcmArray nextPcmToSend = null;
            ArraySegment<byte> encoder_buffer;

            lock (_bufferLock)
            {
                // Make sure we have data, or an end event
                if (_unencodedBuffer.Count == 0)
                    Monitor.Wait(_bufferLock);

                // If there are still no unencoded buffers, then we return an empty packet
                if (_unencodedBuffer.Count == 0)
                    isEmpty = true;
                else
                {
                    if (_unencodedBuffer.Count == 1 && _isWaitingToSendLastPacket)
                        isStop = true;

                    TargettedSpeech speech = _unencodedBuffer.Dequeue();
                    isStop = isStop || speech.IsStop;
                    nextPcmToSend = speech.PcmData;

                    if (isStop)
                        _isWaitingToSendLastPacket = false;
                }
            }

            if (nextPcmToSend == null || nextPcmToSend.Pcm.Length == 0)
                isEmpty = true;

            encoder_buffer = isEmpty ? EmptyByteSegment : encoder.Encode(nextPcmToSend.Pcm);

            // This is called from other threads, so use a lock to be extra safe
            lock (_writePosFuncLock)
            {
                if (_writeLatestMouthPoseFunc != null)
                    _writeLatestMouthPoseFunc(nextPcmToSend, poseToUpdate);
                else
                    poseToUpdate.Clear();
            }

            // Now we're done with the pcm, we can unref
            if (nextPcmToSend != null)
                nextPcmToSend.UnRef();

            if (isStop)
            {
                Debug.Log("Resetting encoder state");
                encoder.ResetState();
            }

            return encoder_buffer;
        }

        public struct CompressedBuffer
        {
            public ArraySegment<byte> EncodedData;
            public byte[] PositionalData;
            public int PositionalDataLength;
        }

        /// <summary>
        /// PCM data targetted at a specific person
        /// </summary>
        private struct TargettedSpeech
        {
            public readonly PcmArray PcmData;

            public bool IsStop;

            public TargettedSpeech(PcmArray pcm)
            {
                PcmData = pcm;

                IsStop = false;
            }
            
            public TargettedSpeech(bool stop)
            {
                IsStop = stop;
                PcmData = null;
            }
        }
    }
}
