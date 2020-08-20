using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FaceDataSerializer
{
    public static readonly string[] AnimationKeyNames = new string[]
    {
        "brows_leftBrow_down",
        "brows_leftBrow_up",
        "brows_midBrows_down",
        "brows_midBrows_up",
        "brows_rightBrow_down",
        "brows_rightBrow_up",
        "eyes_leftEye_blink",
        "eyes_leftEye_wide",
        "eyes_lookDown",
        "eyes_lookLeft",
        "eyes_lookRight",
        "eyes_lookUp",
        "eyes_rightEye_blink",
        "eyes_rightEye_wide",
        "head_Down",
        "head_Left",
        "head_LeftTilt",
        "head_Right",
        "head_RightTilt",
        "head_Up",
        "jaw_left",
        "jaw_open",
        "jaw_right",
        "mouth_down",
        "mouth_left",
        "mouth_leftMouth_frown",
        "mouth_leftMouth_narrow",
        "mouth_leftMouth_smile",
        "mouth_leftMouth_stretch",
        "mouth_lowerLip_left_down",
        "mouth_lowerLip_right_down",
        "mouth_phoneme_ch",
        "mouth_phoneme_mbp",
        "mouth_phoneme_oo",
        "mouth_right",
        "mouth_rightMouth_frown",
        "mouth_rightMouth_narrow",
        "mouth_rightMouth_smile",
        "mouth_rightMouth_stretch",
        "mouth_up",
        "mouth_upperLip_left_up",
        "mouth_upperLip_right_up"
    };
    private readonly Dictionary<string, int> _animationKey2Index = new Dictionary<string, int>();
    private readonly Dictionary<int, string> _index2AnimationKey= new Dictionary<int, string>();

    public FaceDataSerializer()
    {
        // Build up the animation key -> index for faster perf
        for(int i = 0; i < AnimationKeyNames.Length; i++)
        {
            string animKeyName = AnimationKeyNames[i];
            _animationKey2Index.Add(animKeyName, i);
            _index2AnimationKey.Add(i, animKeyName);
        }
    }

    public bool DeserializeAnimationValues(byte[] serializedBytes, Dictionary<string, float> animationValues)
    {
        if(serializedBytes.Length % 2 != 0)
        {
            Debug.LogError("Expected even # of serialized bytes, got: " + serializedBytes.Length);
            return false;
        }

        animationValues.Clear();
        const int maxVal = (1 << 10) - 1; // Max is all 10 bits set (1023)
        int i = 0;
        while(i < serializedBytes.Length)
        {
            byte firstByte = serializedBytes[i++];
            byte secondByte = serializedBytes[i++];

            int nameIndex = firstByte >> 2; // Name index is first 6 bits
            int quantizedValue = (((byte.MaxValue >> 6) & firstByte) << 8) | secondByte; // mask out first 6 bits of the first byte, then use those as first 2 bits of final val
            float value = (float)quantizedValue / maxVal;

            //Debug.Log("firstByte: " + firstByte + " secondByte: " + secondByte + " = " + quantizedValue + " or " + value);

            //Debug.Log("Got as name index: " + nameIndex);

            if(nameIndex >= AnimationKeyNames.Length)
            {
                Debug.LogError("Impossible key index of " + nameIndex);
                return false;
            }
            string valueName = _index2AnimationKey[nameIndex];
            animationValues.Add(valueName, value);
        }

        // Now add all remaining animation values with a 0 val
        for(i = 0; i < AnimationKeyNames.Length; i++)
        {
            string keyName = AnimationKeyNames[i];
            if (!animationValues.ContainsKey(keyName))
                animationValues.Add(keyName, 0);
        }

        return true;
    }
    /// <summary>
    /// Serialize the face values into a binary array.
    /// It encodes the value index in 6 bits, and the value in 10 bits.
    /// Error is on average 10^-4, so this method is practically lossless
    /// </summary>
    /// <param name="animationValues"></param>
    /// <param name="serializedBytes"></param>
    /// <returns></returns>
    public ArraySegment<byte> SerializeAnimationValues(Dictionary<string, float> animationValues, byte[] serializedBytes)
    {
        int offset = 0;
        const int maxVal = (1 << 10) - 1; // Max is all 10 bits set (1023)
        int numAnimations = 0;
        foreach(var anim in animationValues)
        {
            float val = anim.Value;
            // Skip 0 values (of which there are generally 1/2)
            if (val == 0)
                continue;
            numAnimations++;

            byte nameIndex = (byte)_animationKey2Index[anim.Key];
            // name index will be between 0 and 41 inclusive
            // So store it in the first 6 bits (63 max)
            byte firstByte = (byte)(nameIndex << 2);

            // Get the value that we want to save
            if (val > 1f || val < 0)
                Debug.LogError("Unexpected face anim value " + val + " for " + anim.Key);
            // quantized Val is 10 bits
            int quantizedVal = Mathf.RoundToInt(val * maxVal);//TODO we know that the quantized val is never 0, so we could leverage that to get a teensy amount of precision
            firstByte |= (byte)(quantizedVal >> 8); // Store the first 2 bits at the end of the first byte
            byte secondByte = (byte)(byte.MaxValue & quantizedVal); // Store the remaining 8 bits of the value in the second byte

            //Debug.Log("Stored " + quantizedVal + " into " + firstByte + ", " + secondByte);

            serializedBytes[offset++] = firstByte;
            serializedBytes[offset++] = secondByte;
        }

        int numBytes = numAnimations * 2;
        ArraySegment<byte> serializeSegment = new ArraySegment<byte>(serializedBytes, 0, numBytes);
        return serializeSegment;
    }
}
