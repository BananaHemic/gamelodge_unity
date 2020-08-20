using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit;
using NUnit.Framework;
using System.Text;

public class TestFirebaseSerialization
{
    [Test]
    public void TestRotationSerialization()
    {
        string testCase1 = "-1.201475E-16|-0.1709114|-2.037423E-08|0.9852864";
        Quaternion testRot1 = new Quaternion(-1.201475E-16f, -0.1709114f, -2.037423E-08f, 0.9852864f);
        StringBuilder sb1 = new StringBuilder();
        testRot1.SerializeToString(sb1);
        Debug.Log("Serialized to: " + sb1.ToString());
        Assert.AreEqual(sb1.ToString(), testCase1);
        int offset = 0;
        Quaternion resRot1 = sb1.ToString().DeSerializeQuaternionFromString(ref offset);
        float error = Quaternion.Angle(testRot1, resRot1);
        Debug.Log("in: " + testRot1 + " out: " + resRot1);
        Assert.IsTrue(error < 0.1f);
        //return;


        for (int i = 0; i < 100; i++)
        {
            Quaternion input = UnityEngine.Random.rotationUniform;

            StringBuilder sb = new StringBuilder();
            input.SerializeToString(sb);
            offset = 0;
            Quaternion output = sb.ToString().DeSerializeQuaternionFromString(ref offset);

            error = Quaternion.Angle(input, output);
            Debug.Log("in: " + input + " out: " + output);

            Assert.IsTrue(error < 0.1f);
        }
    }
}
