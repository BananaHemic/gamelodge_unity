using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DarkRiftAudio;

public class ArrayResegment
{
    private readonly Queue<PcmArray> _pendingArrays = new Queue<PcmArray>();
    private int _currentOffset = 0;
    private int _numSrcSamples = 0;
    private readonly int _maxSize;

    public ArrayResegment(int maxSize)
    {
        _maxSize = maxSize;
    }
    public void Push(PcmArray newArray)
    {
        if (_maxSize > 0
            && _numSrcSamples + newArray.Pcm.Length > _maxSize)
        {
            //Debug.LogWarning("Dropped array sample");
            return;
        }
        _numSrcSamples += newArray.Pcm.Length;
        newArray.Ref();
        _pendingArrays.Enqueue(newArray);
        //_pendingArrays.Enqueue((float[])newArray.Clone());
        //Debug.Log("Pushed: " + newArray.Pcm.Length + " Total: " + _numSrcSamples);
    }
    private void CopyBinaural(float[] srcArray, int srcOffset, float[] dstArray, int dstOffset, int srcCopyLength)
    {
        int dstIdx = dstOffset;
        for (int i = 0; i < srcCopyLength; i++)
        {
            float val = srcArray[srcOffset + i];
            dstArray[dstIdx++] = val;
            dstArray[dstIdx++] = val;
        }
    }
    public bool TryPullSize(int readSize, float[] readBuffer, bool isBinaural)
    {
        int binauralFactor = isBinaural ? 2 : 1;
        // Exit early if we don't have enough data
        if (_numSrcSamples * binauralFactor < readSize
            || readSize == 0)
            return false;

        int numSrcCopied = 0;
        int numDstCopiesRemaining = readSize;
        while (numDstCopiesRemaining > 0)
        {
            // Get the first buffer
            PcmArray topArray = _pendingArrays.Peek();
            // How many are in the current array
            int numInSrcArray = topArray.Pcm.Length - _currentOffset;
            // How many samples we'll be copying over
            int numSrcToCopy = (numInSrcArray < numDstCopiesRemaining / binauralFactor) ? numInSrcArray : numDstCopiesRemaining / binauralFactor;
            // Copy it to the destination buffer
            if(isBinaural)
                CopyBinaural(topArray.Pcm, _currentOffset, readBuffer, numSrcCopied, numSrcToCopy);
            else
                Array.Copy(topArray.Pcm, _currentOffset, readBuffer, numSrcCopied, numSrcToCopy);

            // Update our internal counters
            numSrcCopied += numSrcToCopy;
            numDstCopiesRemaining -= numSrcToCopy * binauralFactor;
            if (numSrcToCopy == numInSrcArray)
            {
                _pendingArrays.Dequeue();
                topArray.UnRef();
                _currentOffset = 0;
            }
            else
            {
                _currentOffset += numSrcToCopy;
            }
        }
        _numSrcSamples -= numSrcCopied;
        //Debug.Log("Pulled: " + numSrcCopied);
        return true;
    }
}
