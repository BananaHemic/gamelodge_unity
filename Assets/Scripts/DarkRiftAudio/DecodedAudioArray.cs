using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace DarkRiftAudio
{
    /// <summary>
    /// Small class to help this script re-use float arrays after their data has become encoded
    /// Obviously, it's weird to ref-count in a managed environment, but it really
    /// Does help identify leaks and makes zero-copy buffer sharing easier
    /// </summary>
    public class DecodedAudioArray : IDisposable
    {
        public readonly int Index;
        public float[] PcmData;
        public DRMouthPose MouthPose;
        public int PcmLength { get; private set; }
        public int ReadOffset { get; set; }
        internal int _refCount;
        const int MaxDecodedAudioSize = 4096;
        private static readonly List<DecodedAudioArray> _decodedArrays = new List<DecodedAudioArray>();

        private DecodedAudioArray(int index, DRMouthPose mouthPose)
        {
            PcmData = new float[MaxDecodedAudioSize];
            Index = index;
            MouthPose = new DRMouthPose(mouthPose);
            PcmLength = 0;
            ReadOffset = 0;
            _refCount = 1;
        }
        public void SetLength(int len)
        {
            if (len > PcmData.Length)
                throw new System.Exception("Too big of an array got " + len);
            PcmLength = len;
        }
        public void Ref()
        {
            _refCount++;
        }
        public void UnRef()
        {
            _refCount--;
        }
        public static DecodedAudioArray GetAvailableDecodedAudioArray(DRMouthPose mouthPose)
        {
            foreach (DecodedAudioArray ray in _decodedArrays)
            {
                if (ray._refCount == 0)
                {
                    ray.Ref();
                    //Debug.Log("re-using buffer");
                    ray.MouthPose.CopyFrom(mouthPose);
                    ray.ReadOffset = 0;
                    return ray;
                }
            }
            DecodedAudioArray newArray = new DecodedAudioArray(_decodedArrays.Count, mouthPose);
            _decodedArrays.Add(newArray);
            //Debug.LogWarning("New decoded buffer length is: " + _decodedArrays.Count);
            //if (_decodedArrays.Count >= 20 && _decodedArrays.Count % 20 == 0)
                //Debug.LogError("Large amount of decoded arrays! " + _decodedArrays.Count);
            return newArray;
        }
        public void Dispose()
        {
            if (_refCount != 0)
                Debug.LogError("DecodedAudioArray dispose early!!");
        }
    }
}
