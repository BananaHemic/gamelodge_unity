using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit;
using NUnit.Framework;
using DarkRift;
using DarkRift.Client;

public class TestPoseSerialization
{
    private float TestRot(Quaternion rot)
    {
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.WriteCompressedRotation(rot);
            byte[] rawData = writer.GetRawBackingArray(out int len);
            using (DarkRiftReader reader = DarkRiftReader.CreateFromArray(rawData, 0, len))
            {
                Quaternion outRot = reader.ReadCompressedRotation();
                return Quaternion.Angle(rot, outRot);
            }
        }
    }
    [Test]
    public void TestRotationSerialization()
    {
        ClientObjectCacheSettings clientObjectCacheSettings = DarkRiftClient.DefaultClientCacheSettings;
        var client = new DarkRiftClient(clientObjectCacheSettings);
        float sumAngleDelta = 0;
        int numSamples = 0;
        float maxError = 0;

        //TestRot(Quaternion.Euler(0, 20f, 0));
        //return;

        for (int i = 0; i < 10000; i++)
        {
            Quaternion randRot = Random.rotationUniform;
            float angle = TestRot(randRot);
            if(angle > 0.15f)
            {
                Debug.LogError("Failed for rot " + randRot.eulerAngles.ToPrettyString() + " error " + angle);
                Assert.Fail();
            }
            sumAngleDelta += angle;
            numSamples++;
            maxError = Mathf.Max(angle, maxError);
        }

        float angAngleError = sumAngleDelta / numSamples;
        Debug.Log("Average angular error: " + angAngleError + " deg max error: " + maxError + " deg");

    }
    [Test]
    public void TestHandFollowsRelativeOrientation()
    {
        Transform moveTransform = new GameObject().transform;
        Transform grabbedObjTransform = new GameObject().transform;
        // In ObjectFollowsHand mode, this is the relative position
        // of the object to the hand
        Vector3 objFollowGrabRelPos = new Vector3(-0.001593672f, 0.4683861f, 0.2650414f);
        Quaternion objFollowsGrabRelRot = new Quaternion(0.5983515f, -0.5819707f, 0.35306f, 0.4226517f);

        // The real orientation of the wrist
        // Got this from a test run, this is the pos in world space
        Vector3 wristPos = new Vector3(0.8892758f, 1.233734f, -1.504455f);
        Quaternion wristRot = new Quaternion(0.2378894f, 0.4784031f, 0.6741698f, 0.5099356f);
        // What the object position/rotation should be if this is in ObjectFollowsHand
        // we got this just by running the thing
        Vector3 targObjPos = new Vector3(0.8887548f, 1.328543f, -0.9746948f);
        Quaternion targObjRot = new Quaternion(0.9669172f, 0.2248316f, 0.04027906f, 0.1135778f);

        // In HandFollowsObject mode, this is the position/rotation of the
        // wrist, relative to the object
        Vector3 handFollowGrabRelPos = new Vector3(-0.05583112f, -0.04295288f, 0.5335477f);
        Quaternion handFollowGrabRelRot = new Quaternion(-0.6810822f, 0.5953256f, 0.05549833f, 0.4226517f);

        Debug.Log("r " + (objFollowsGrabRelRot * objFollowGrabRelPos).ToPrettyString());
        Debug.Log("r2 " + (Quaternion.Inverse(objFollowsGrabRelRot) * objFollowGrabRelPos).ToPrettyString());

        handFollowGrabRelRot = Quaternion.Inverse(objFollowsGrabRelRot);
        handFollowGrabRelPos = -(handFollowGrabRelRot * objFollowGrabRelPos);

        // Calculate where the wrist should be, using the hand follows
        // orientation that we just calculated
        grabbedObjTransform.transform.localPosition = targObjPos;
        grabbedObjTransform.transform.localRotation = targObjRot;
        Vector3 solvedWristPos = grabbedObjTransform.TransformPoint(handFollowGrabRelPos);
        Quaternion solvedWristRot = grabbedObjTransform.rotation * handFollowGrabRelRot;

        // Check
        Vector3 del = (wristPos - solvedWristPos);
        Debug.Log("solv off by " + del.ToPrettyString());
        Debug.Log("rot off by " + (wristRot.eulerAngles - solvedWristRot.eulerAngles).ToPrettyString());
        Assert.Less(Mathf.Abs(Quaternion.Angle(wristRot, solvedWristRot)), 0.01f, "Rotation is wrong");
        Assert.Less(del.magnitude, 0.01f, "Position is wrong");

        // Cleanup
        GameObject.DestroyImmediate(moveTransform.gameObject);
        GameObject.DestroyImmediate(grabbedObjTransform.gameObject);
    }
    [Test]
    public void TestMovingObjectFollowsHand()
    {
        Transform moveTransform = new GameObject().transform;
        Transform grabbedObjTransform = new GameObject().transform;
        // In HandFollowsObject mode, this is the position/rotation of the
        // wrist, relative to the object
        Vector3 handFollowGrabRelPos = new Vector3(-0.05583112f, -0.04295288f, 0.5335477f);
        Quaternion handFollowGrabRelRot = new Quaternion(-0.6810822f, 0.5953256f, 0.05549833f, 0.4226517f);
        Debug.Log("hand follow: " + handFollowGrabRelRot.eulerAngles.ToPrettyString());
        Debug.Log("inv hand follow: " + Quaternion.Inverse(handFollowGrabRelRot).eulerAngles.ToPrettyString());

        // In ObjectFollowsHand mode, this is the relative position
        // of the object to the hand
        Vector3 objFollowGrabRelPos = new Vector3(-0.001593672f, 0.4683861f, 0.2650414f);
        Quaternion objFollowsGrabRelRot = new Quaternion(0.5983515f, -0.5819707f, 0.35306f, 0.4226517f);

        // The real orientation of the wrist
        // Got this from a test run, this is the pos in world space
        Vector3 wristPos = new Vector3(0.8892758f, 1.233734f, -1.504455f);
        Quaternion wristRot = new Quaternion(0.2378894f, 0.4784031f, 0.6741698f, 0.5099356f);

        // What the object position/rotation should be if this is in ObjectFollowsHand
        // we got this just by running the thing
        Vector3 targObjPos = new Vector3(0.8887548f, 1.328543f, -0.9746948f);
        Quaternion targObjRot = new Quaternion(0.9669172f, 0.2248316f, 0.04027906f, 0.1135778f);

        // Calculate where the object should be, if it was in ObjectFollowsHand
        // but based on the relative position with it being
        // HandFollowsObject
        moveTransform.transform.localPosition = wristPos;
        moveTransform.transform.localRotation = wristRot;
        Vector3 solvedObjectPos = moveTransform.TransformPoint(-(Quaternion.Inverse(handFollowGrabRelRot) * handFollowGrabRelPos));
        Quaternion solvedObjectRot = moveTransform.rotation * Quaternion.Inverse(handFollowGrabRelRot);
        //Quaternion solvedObjectRot = handFollowGrabRelRot * moveTransform.rotation;
        //Quaternion solvedObjectRot = Quaternion.Inverse(handFollowGrabRelRot) * moveTransform.rotation;
        //Quaternion solvedObjectRot = Quaternion.Inverse(handFollowGrabRelRot) * moveTransform.rotation * handFollowGrabRelRot;
        //Quaternion solvedObjectRot =  moveTransform.rotation * handFollowGrabRelRot;
        //Quaternion solvedObjectRot =  Quaternion.Inverse(moveTransform.rotation) * handFollowGrabRelRot;
        //Quaternion solvedObjectRot = moveTransform.rotation * Quaternion.Inverse(handFollowGrabRelRot) * Quaternion.Inverse

        //Vector3 objFollowsSolvedPos = moveTransform.TransformPoint(objFollowGrabRelPos);
        //Quaternion objFollowsRot = moveTransform.rotation * objFollowsGrabRelRot;
        //Debug.Log(objFollowsSolvedPos.ToPrettyString());
        //Debug.Log(objFollowsRot.ToPrettyString());

        Vector3 del = (targObjPos - solvedObjectPos);
        Debug.Log("solv off by " + del.ToPrettyString());
        Debug.Log("rot off by " + (targObjRot.eulerAngles - solvedObjectRot.eulerAngles).ToPrettyString());
        Assert.Less(Quaternion.Angle(targObjRot, solvedObjectRot), 0.01f, "Rotation is wrong");
        Assert.Less(del.magnitude, 0.01f, "Position is wrong");

        // Figure out the orientation of the wrist when in HandFollowsObject mode
        // If the system is working, this will be the same orientation that we
        // started with on the wrists
        grabbedObjTransform.localPosition = solvedObjectPos;
        grabbedObjTransform.localRotation = solvedObjectRot;
        Vector3 wristPosFromHandFollowsObject = grabbedObjTransform.TransformPoint(handFollowGrabRelPos);
        Quaternion wristRotFromHandFollowsObject = grabbedObjTransform.rotation * handFollowGrabRelRot;

        GameObject.DestroyImmediate(moveTransform.gameObject);
        GameObject.DestroyImmediate(grabbedObjTransform.gameObject);

        Debug.Log("Pos off by " + (wristPos - wristPosFromHandFollowsObject).ToPrettyString() + " " + (wristPos - wristPosFromHandFollowsObject).magnitude);
        Debug.Log("Rot off by " + (wristRot.eulerAngles - wristRotFromHandFollowsObject.eulerAngles).ToPrettyString() + " " + Quaternion.Angle(wristRot, wristRotFromHandFollowsObject));
    }
    [Test]
    public void TestRelativeOrientation()
    {
        Vector3 objectRelPos = new Vector3(-0.005300045f, -0.09434287f, -0.2231648f);
        Quaternion objectRelRot = new Quaternion(-0.3695697f, 0, 0, 0.929203f);

        Vector3 targetPos = new Vector3(0.005300045f, -0.08470014f, 0.2270002f);
        Quaternion targetRot = Quaternion.Euler(43.378f, 0, 0);

        Quaternion resRot = Quaternion.Inverse(objectRelRot);
        Vector3 mul = objectRelRot * objectRelPos;
        Vector3 mul2 = resRot * objectRelPos;
        Debug.Log(mul.ToPrettyString());
        Debug.Log(mul2.ToPrettyString());
        Debug.Log(resRot.eulerAngles.ToPrettyString());
    }
    [Test]
    public void TestMouthPose()
    {
        var client = new DarkRiftClient();
        DRMouthPose userPose = new DRMouthPose();
        userPose.OU = 100f;
        userPose.RR = 33f;
        DarkRiftWriter writer = DarkRiftWriter.Create();
        userPose.Serialize(writer);
        byte[] serializedData = writer.GetDuplicatedContents();
        DarkRiftReader reader = DarkRiftReader.CreateFromArray(serializedData, 0, serializedData.Length);
        userPose.OU = 0;
        userPose.RR = 0;
        userPose.Deserialize(reader);

        Assert.IsTrue(Mathf.Abs(userPose.OU - 100.0f) < 0.001f, "OU wrong! Got " + userPose.OU + " expected " + 100.0f);
        Assert.IsTrue(Mathf.Abs(userPose.RR - 33.0f) < 0.5f, "RR wrong! Got " + userPose.RR + " expected " + 33.0f);
        Assert.IsTrue(Mathf.Abs(userPose.Sil - 0f) < 0.001f, "SIL wrong! Got " + userPose.Sil + " expected " + 0f);
    }
    [Test]
    public void TestPose()
    {
        /* Tested and working, but disabled since we switched to a struct
        var client = new DarkRiftClient();
        DRUserPose userPose = new DRUserPose();
        Vector3 origin = Random.insideUnitSphere * DRUserPose.MaxSignedDistance;
        //Vector3 origin = Vector3.zero;
        Vector3 headPos = origin + Vector3.up;
        Vector3 lHandPos = origin + Vector3.up + Vector3.left;
        //Vector3 lHandPos = new Vector3(-1.2f, 1f, 0);
        Vector3 rHandPos = origin + Vector3.up + Vector3.right;
        Quaternion headRot = Random.rotationUniform;
        Quaternion lHandRot = Random.rotationUniform;
        Quaternion rHandRot = Random.rotationUniform;

        Vector3 resHeadPos, resLHandPos, resRHandPos;
        Quaternion resHeadRot, resLHandRot, resRHandRot;

        //Debug.Log("a");
        userPose.UpdateFrom(origin, headPos, lHandPos, rHandPos, headRot, lHandRot, rHandRot);
        //Debug.Log("a");

        DarkRiftWriter writer = DarkRiftWriter.Create();
        //Debug.Log("a");
        userPose.Serialize(writer);
        //Debug.Log("a");
        byte[] serializedData = writer.GetDuplicatedContents();
        //Debug.Log("a");
        DarkRiftReader reader = DarkRiftReader.Create(serializedData, 0, serializedData.Length);
        //Debug.Log("a");
        userPose.Deserialize(reader);
        //Debug.Log("a");

        userPose.ReadInto(origin, out resHeadPos, out resLHandPos, out resRHandPos, out resHeadRot, out resLHandRot, out resRHandRot);
        Debug.Log(resHeadPos.ToPrettyString());

        Assert.IsTrue((resHeadPos - headPos).magnitude < 0.001f, "Head delta too big! Got " + resHeadPos.ToPrettyString() + " expected " + headPos.ToPrettyString());
        Assert.IsTrue((resLHandPos - lHandPos).magnitude < 0.001f, "HandL delta too big! Got " + resLHandPos.ToPrettyString() + " expected " + lHandPos.ToPrettyString());
        Assert.IsTrue((resRHandPos - rHandPos).magnitude < 0.001f, "HandR delta too big! Got " + resRHandPos.ToPrettyString() + " expected " + rHandPos.ToPrettyString());
        Assert.IsTrue(Quaternion.Angle(resHeadRot, headRot) < 0.001f, "Head Rot delta too big! Got " + resHeadRot.ToPrettyString() + " expected " + headRot.ToPrettyString());
        Assert.IsTrue(Quaternion.Angle(resLHandRot, lHandRot) < 0.001f, "L Hand Rot delta too big! Got " + resLHandRot.ToPrettyString() + " expected " + lHandRot.ToPrettyString());
        Assert.IsTrue(Quaternion.Angle(resRHandRot, rHandRot) < 0.001f, "R Hand Rot delta too big! Got " + resRHandRot.ToPrettyString() + " expected " + rHandRot.ToPrettyString());

        int numTrials = 32;
        for (int i = 0; i < numTrials; i++)
        {
            // Load in random values
            //origin = Random.insideUnitSphere * DRUserPose.MaxSignedDistance;
            //headPos = origin + Random.insideUnitSphere * DRUserPose.MaxSignedDistance;
            //lHandPos = origin + Random.insideUnitSphere * DRUserPose.MaxSignedDistance;
            //rHandPos = origin + Random.insideUnitSphere * DRUserPose.MaxSignedDistance;
            //headRot = Random.rotationUniform;
            //lHandRot = Random.rotationUniform;
            //rHandRot = Random.rotationUniform;

            writer = DarkRiftWriter.Create();
            userPose.Serialize(writer);
            serializedData = writer.GetDuplicatedContents();
            reader = DarkRiftReader.Create(serializedData, 0, serializedData.Length);
            userPose.Deserialize(reader);
            userPose.ReadInto(origin, out resHeadPos, out resLHandPos, out resRHandPos, out resHeadRot, out resLHandRot, out resRHandRot);
            Debug.Log(resHeadPos.ToPrettyString());

            Assert.IsTrue((resHeadPos - headPos).magnitude < 0.001f, "Head delta too big! Got " + resHeadPos.ToPrettyString() + " expected " + headPos.ToPrettyString());
            Assert.IsTrue((resLHandPos - lHandPos).magnitude < 0.001f, "HandL delta too big! Got " + resLHandPos.ToPrettyString() + " expected " + lHandPos.ToPrettyString());
            Assert.IsTrue((resRHandPos - rHandPos).magnitude < 0.001f, "HandR delta too big! Got " + resRHandPos.ToPrettyString() + " expected " + rHandPos.ToPrettyString());
            Assert.IsTrue(Quaternion.Angle(resHeadRot, headRot) < 0.001f, "Head Rot delta too big! Got " + resHeadRot.ToPrettyString() + " expected " + headRot.ToPrettyString());
            Assert.IsTrue(Quaternion.Angle(resLHandRot, lHandRot) < 0.001f, "L Hand Rot delta too big! Got " + resLHandRot.ToPrettyString() + " expected " + lHandRot.ToPrettyString());
            Assert.IsTrue(Quaternion.Angle(resRHandRot, rHandRot) < 0.001f, "R Hand Rot delta too big! Got " + resRHandRot.ToPrettyString() + " expected " + rHandRot.ToPrettyString());
        }
        */
    }
}
