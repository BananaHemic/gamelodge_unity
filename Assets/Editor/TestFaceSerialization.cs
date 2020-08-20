using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit;
using NUnit.Framework;
using System;

public class TestFaceSerialization 
{
    [Test]
    public void TestFaceSerializationError()
    {
        FaceDataSerializer faceSerializer = new FaceDataSerializer();
        // Build a random animation data
        Dictionary<string, float> animationValues = new Dictionary<string, float>();
        for(int i = 0; i < FaceDataSerializer.AnimationKeyNames.Length; i++)
        {
            float val;
            // 50% chance value is 0
            if (UnityEngine.Random.value < 0.5)
                val = 0;
            else
                val = UnityEngine.Random.value;

            animationValues.Add(FaceDataSerializer.AnimationKeyNames[i], val);
        }

        foreach(var anim in animationValues)
            Debug.Log(anim.Key + ": " + anim.Value);

        byte[] testingBytes = new byte[100];
        // Serialize the animation values
        ArraySegment<byte> serialized = faceSerializer.SerializeAnimationValues(animationValues, testingBytes);
        Debug.Log("Serialized to: " + serialized.Count);

        // Deserialize the values
        byte[] serializedArray = serialized.ToByteArray();
        Dictionary<string, float> deserialized = new Dictionary<string, float>();
        bool didSerialize = faceSerializer.DeserializeAnimationValues(serializedArray, deserialized);
        Assert.IsTrue(didSerialize);

        foreach(var anim in deserialized)
            Debug.Log("res: " + anim.Key + ": " + anim.Value);
        // Calculate the error
        float sumError = 0;
        foreach(var original in animationValues)
        {
            float res;
            if(!deserialized.TryGetValue(original.Key, out res))
            {
                Assert.Fail("Failed to serialize key " + original.Key);
                return;
            }

            float delta = res - original.Value;
            Debug.Log(original.Key + " delta " + delta);
            sumError += Mathf.Abs(delta);
        }
        Debug.Log("total error: " + sumError + " avgError: " + sumError / animationValues.Count);
    }
}
