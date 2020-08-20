using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityRawInput;

public class TestingSimultaneousGrab : MonoBehaviour
{
    public string BundleID;
    public ushort ModelIndex;
    public string ModelName;

    Vector3 moveDir;
    //bool wasF12Down = false;
    bool wasF11Down = false;

    // Start is called before the first frame update
    void Start()
    {
        RawKeyInput.Start(true);
        moveDir = new Vector3(Random.value, 0, Random.value); // Random dir, but on table
    }

    void TestSimulataneousInstantiate()
    {
        SceneObjectManager.Instance.UserAddObject(BundleID, ModelName, ModelIndex, Vector3.zero, Quaternion.identity);
    }
    void TestSimultaneousGrab()
    {
        // Used to test that things work when two people grab
        // an object at the same time. The tester needs two
        // instances of the app running, both will respond to
        // the key click
        Transform transformToGrab = SceneObjectManager.Instance.RootObject.GetChild(0);
        SceneObject sceneObj = transformToGrab.gameObject.GetComponent<SceneObject>();

        StartCoroutine(GrabAndMove(sceneObj, moveDir));
    }
    IEnumerator GrabAndMove(SceneObject sceneObject, Vector3 dir)
    {
        Debug.Log("Will now be grab+move " + sceneObject.GetID().ToString());
        //bool did = sceneObject.TryGrab();
        //if (!did)
        //{
        //    Debug.LogError("Failed to grab while testing!!");
        //    yield break;
        //}

        float dragTime = 5f;
        float endTime = Time.time + dragTime;
        while(Time.time < endTime)
        {
            // Stop grabbing if the scene object isn't registered as such
            if(sceneObject.CurrentGrabState != SceneObject.GrabState.PendingGrabbedBySelf
                && sceneObject.CurrentGrabState != SceneObject.GrabState.GrabbedBySelf)
            {
                Debug.Log("Ending grab session early, state: " + sceneObject.CurrentGrabState + " after time " + (endTime - Time.time));
                yield break;
            }

            Vector3 moveVec = dir * Time.deltaTime;
            sceneObject.transform.Translate(moveVec, Space.Self);
            yield return null;
        }

        Debug.Log("Done moving");
        sceneObject.EndGrab();
    }
    private void OnApplicationQuit()
    {
        RawKeyInput.Stop();
    }
    void Update()
    {
        if (RawKeyInput.IsKeyDown(RawKey.F11))
        {
            if (!wasF11Down)
                TestSimulataneousInstantiate();
            wasF11Down = true;
        }
        else
            wasF11Down = false;

        if (RawKeyInput.IsKeyDown(RawKey.F12))
        {
            //if(!wasF12Down)
                //TestSimultaneousGrab();
            //wasF12Down = true;
        }
        else
        {
            //wasF12Down = false;
        }
    }
}
