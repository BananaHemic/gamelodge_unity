using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarkRiftAudio
{
    /// <summary>
    /// Small class to help this script re-use float arrays after their data has become encoded
    /// Obviously, it's weird to ref-count in a managed environment, but it really
    /// Does help identify leaks and makes zero-copy buffer sharing easier
    /// </summary>
    public class EncodedAudioArray : IDisposable
    {
        public readonly int Index;
        public readonly byte[] CompressedAudio;
        public DRMouthPose MouthPose;
        public int Length { get; private set; }
        internal int _refCount;
        const int MaxPktSize = byte.MaxValue;
        private static readonly List<EncodedAudioArray> _encodedArrays = new List<EncodedAudioArray>();

        private EncodedAudioArray(int index)
        {
            CompressedAudio = new byte[MaxPktSize];
            Index = index;
            MouthPose = new DRMouthPose();
            Length = 0;
            _refCount = 1;
        }
        public void SetLength(int len)
        {
            if (len > CompressedAudio.Length)
                throw new System.Exception("Too big of an array got " + len);
            Length = len;
        }
        public void Ref()
        {
            _refCount++;
        }
        public void UnRef()
        {
            _refCount--;
        }
        public static EncodedAudioArray GetAvailableEncodedAudioArray()
        {
            foreach (EncodedAudioArray ray in _encodedArrays)
            {
                if (ray._refCount == 0)
                {
                    ray.Ref();
                    //Debug.Log("re-using buffer");
                    return ray;
                }
            }
            EncodedAudioArray newArray = new EncodedAudioArray(_encodedArrays.Count);
            _encodedArrays.Add(newArray);
            //Debug.LogWarning("New encoded buffer length is: " + _encodedArrays.Count);
            //if (_encodedArrays.Count > 20 && _encodedArrays.Count % 20 == 0)
                //Debug.LogError("Large amount of encoded arrays! " + _encodedArrays.Count);
            return newArray;
        }
        public void Dispose()
        {
            if (_refCount != 0)
                Debug.LogError("EncodedAudioArray dispose early!!");
        }
    }
}
