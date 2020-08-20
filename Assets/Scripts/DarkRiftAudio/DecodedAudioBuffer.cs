﻿/*
 * AudioDecodingBuffer
 * Receives decoded audio buffers, and copies them into the
 * array passed via Read()
 */
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Threading;

namespace DarkRiftAudio {
    public class DecodedAudioBuffer : IDisposable
    {
        public long NumPacketsLost { get; private set; }
        public bool HasFilledInitialBuffer { get; private set; }
        /// <summary>
        /// How many samples have been decoded
        /// </summary>
        private int _decodedCount;
        private DecodedAudioArray _currentPacket;
        private ushort _playerID;

        /// <summary>
        /// The audio DSP time when we last dequeued a buffer
        /// </summary>
        private double _lastBufferTime;
        private readonly object _posLock = new object();

        private readonly AudioDecodeThread _audioDecodeThread;
        private readonly object _bufferLock = new object();
        private readonly Queue<DecodedAudioArray> _decodedBuffer = new Queue<DecodedAudioArray>();

        /// <summary>
        /// How many incoming packets to buffer before audio begins to be played
        /// Higher values increase stability and latency
        /// </summary>
        const int InitialSampleBuffer = 3;

        public DecodedAudioBuffer(AudioDecodeThread audioDecodeThread)
        {
            _audioDecodeThread = audioDecodeThread;
        }
        public void Init(ushort playerID)
        {
            //Debug.Log("Init decoding buffer for: #" + playerID);
            _playerID = playerID;
            _audioDecodeThread.AddDecoder(_playerID);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            // Don't send audio until we've filled our initial buffer of packets
            if (!HasFilledInitialBuffer)
            {
                Array.Clear(buffer, offset, count);
                //Debug.Log("this should not happen");
                return 0;
            }

            //lock (_bufferLock)
            //{
                //Debug.Log("We now have " + _decodedBuffer.Count + " decoded packets");
            //}
            //Debug.LogWarning("Will read");

            int readCount = 0;
            while (readCount < count && _decodedCount > 0)
                readCount += ReadFromBuffer(buffer, offset + readCount, count - readCount);
            //Debug.Log("Read: " + readCount + " #decoded: " + _decodedCount);

            //Return silence if there was no data available
            if (readCount == 0)
            {
                //Debug.Log("Returning silence");
                Array.Clear(buffer, offset, count);
            } else if (readCount < count)
            {
                //Debug.LogWarning("Buffer underrun: " + (count - readCount) + " samples. Asked: " + count + " provided: " + readCount + " numDec: " + _decodedCount);
                Array.Clear(buffer, offset + readCount, count - readCount);
            }
            
            return readCount;
        }
        public DRMouthPose GetLatestMouthPose()
        {
            if (_currentPacket == null)
                return null;
            return _currentPacket.MouthPose;
        }

        /// <summary>
        /// Read data that has already been decoded
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="dstOffset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private int ReadFromBuffer(float[] dst, int dstOffset, int count)
        {
            // Get the next DecodedPacket to use
            int numInSample = _currentPacket == null ? 0 : _currentPacket.PcmLength - _currentPacket.ReadOffset;
            if(numInSample == 0)
            {
                lock (_bufferLock)
                {
                    if (_decodedBuffer.Count == 0)
                    {
                        Debug.LogError("No available decode buffers!");
                        return 0;
                    }
                    if (_currentPacket != null)
                        _currentPacket.UnRef();
                    _currentPacket = _decodedBuffer.Dequeue();

                    // If we have a packet, let's update the positions
                    lock (_posLock)
                    {
                        _lastBufferTime = AudioSettings.dspTime;
                    }
                    numInSample = _currentPacket.PcmLength - _currentPacket.ReadOffset;
                }
            }

            int readCount = Math.Min(numInSample, count);
            Array.Copy(_currentPacket.PcmData, _currentPacket.ReadOffset, dst, dstOffset, readCount);
            //Debug.Log("We have: "
                //+ _decodedCount + " samples decoded "
                //+ numInSample + " samples in curr packet "
                //+ _currentPacket.ReadOffset + " read offset "
                //+ readCount + " numRead");

            Interlocked.Add(ref _decodedCount, -readCount);
            _currentPacket.ReadOffset += readCount;
            return readCount;
        }

        internal void AddDecodedAudio(DecodedAudioArray decodedAudio, bool reevaluateInitialBuffer)
        {
            //if (reevaluateInitialBuffer)
                //Debug.Log("Will refill our initial buffer");

            int count = 0;
            lock (_bufferLock)
            {
                count = _decodedBuffer.Count;
                if(count > DarkRiftAudioConstants.RECEIVED_PACKET_BUFFER_SIZE)
                {
                    // TODO this seems to happen at times
                    //Debug.LogWarning("Max recv buffer size reached, dropping for user #" + _playerID);
                }
                else
                {
                    _decodedBuffer.Enqueue(decodedAudio);
                    Interlocked.Add(ref _decodedCount, decodedAudio.PcmLength);

                    // this is set if the previous received packet was a last packet
                    // or if there was an abrupt change in sequence number
                    if (reevaluateInitialBuffer)
                        HasFilledInitialBuffer = false;

                    if (!HasFilledInitialBuffer && (count + 1 >= InitialSampleBuffer))
                        HasFilledInitialBuffer = true;
                }
            }
            //Debug.Log("Adding " + pcmLength + " num packets: " + count + " total decoded: " + _decodedCount);
        }

        public void Reset()
        {
            lock (_bufferLock)
            {
                if(_playerID != 0)
                    _audioDecodeThread.RemoveDecoder(_playerID);
                NumPacketsLost = 0;
                HasFilledInitialBuffer = false;
                _decodedCount = 0;
                while (_decodedBuffer.Count != 0)
                    _decodedBuffer.Dequeue().UnRef();
                if (_currentPacket != null)
                    _currentPacket.UnRef();
                _currentPacket = null;
                _playerID = 0;
            }
        }
        public void Dispose()
        {
        }
    }
}