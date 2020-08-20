using DarkRift;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSync : GenericSingleton<TestSync>
{
    public GameObject Prefab;
    public Transform Container;
    public float MaxVel = 15;
    public float MaxAngularVel = 15;
    public float SendRateHz = 45f;

    private float _timeOfLastSend;
    private readonly List<NetworkObject> _localObjects = new List<NetworkObject>();
    private readonly List<NetworkObject> _remoteObjects = new List<NetworkObject>();

    private void Start()
    {
        RealtimeNetworkUpdater.Instance.EnableTesting(RealtimeNetworkUpdater.TestMode.ObjectSync);
    }
    void AddObject()
    {
        GameObject local = SimplePool.Instance.Spawn(Prefab);
        local.transform.parent = Container;
        Rigidbody localRig = local.GetComponent<Rigidbody>();
        Vector3 vel = Random.onUnitSphere * Random.Range(1f, MaxVel);
        Vector3 angleVel = Random.onUnitSphere * Random.Range(1f, MaxAngularVel);
        localRig.velocity = vel;
        localRig.angularVelocity = angleVel;
        NetworkObject localNetObj = local.GetComponent<NetworkObject>();
        localNetObj.EnableTestingMode((ushort)_localObjects.Count, true);
        _localObjects.Add(localNetObj);
        local.GetComponent<MeshRenderer>().material.color = Color.green;
        //local.GetComponent<MeshRenderer>().enabled = false;

        GameObject remote = SimplePool.Instance.Spawn(Prefab);
        Rigidbody remoteRig = remote.GetComponent<Rigidbody>();
        remoteRig.velocity = vel;
        remoteRig.angularVelocity = angleVel;
        remote.transform.parent = Container;
        remote.layer = 31;
        NetworkObject remoteNetObj = remote.GetComponent<NetworkObject>();
        remoteNetObj.EnableTestingMode((ushort)_remoteObjects.Count, false);
        _remoteObjects.Add(remoteNetObj);
        remote.GetComponent<MeshRenderer>().material.color = Color.yellow;
    }

    void Deserialize(ushort tag, DarkRiftReader reader, NetworkObject remote)
    {
        Vector3 pos;
        Quaternion rot;
        Vector3 vel;
        Vector3 angVel;
        switch (tag)
        {
            case ServerTags.TransformObject_Pos:
                pos = reader.ReadSerializable<Vec3>().ToVector3();
                remote.ServerSentPosition(pos, false);
                remote.ServerSentForceRest(false);
                break;
            case ServerTags.TransformObject_PosRot:
                pos = reader.ReadSerializable<Vec3>().ToVector3();
                rot = reader.ReadSerializable<Quat>().ToQuaternion();
                remote.ServerSentPosition(pos, false);
                remote.ServerSentRotation(rot, false);
                remote.ServerSentForceRest(false);
                break;
            case ServerTags.TransformObject_PosRot_Rest:
                pos = reader.ReadSerializable<Vec3>().ToVector3();
                rot = reader.ReadSerializable<Quat>().ToQuaternion();
                remote.ServerSentPosition(pos, true);
                remote.ServerSentRotation(rot, true);
                remote.ServerSentForceRest(true);
                break;
            case ServerTags.TransformObject_PosRotVelAngVel:
                pos = reader.ReadSerializable<Vec3>().ToVector3();
                rot = reader.ReadSerializable<Quat>().ToQuaternion();
                vel = reader.ReadSerializable<Vec3>().ToVector3();
                angVel = reader.ReadSerializable<Vec3>().ToVector3();
                remote.ServerSentPosition(pos, false);
                remote.ServerSentRotation(rot, false);
                remote.ServerSentVelocity(vel);
                remote.ServerSentAngularVelocity(angVel);
                remote.ServerSentForceRest(false);
                break;
            default:
                Debug.LogError("Unhandled tag " + tag);
                return;
        }
    }
    public void OnReliableServerMessage(Message msg, ushort firstTag)
    {
        //Debug.Log("Message len: " + msg.DataLength);
        // Parse out everything
        ushort tag = firstTag;
        using(DarkRiftReader reader = msg.GetReader())
        {
            while (true)
            {
                ushort id = reader.ReadUInt16();
                //Debug.Log("Updating remote #" + id);
                NetworkObject remote = _remoteObjects[id];
                Deserialize(tag, reader, remote);
                if (reader.Length > reader.Position)
                    tag = reader.ReadUInt16();
                else
                    break;
            }
        }
    }
    public void OnUnreliableServerMessage(Message msg, ushort firstTag)
    {
        //Debug.Log("Message len: " + msg.DataLength);
        // Parse out everything
        ushort tag = firstTag;
        using(DarkRiftReader reader = msg.GetReader())
        {
            while (true)
            {
                ushort id = reader.ReadUInt16();
                //Debug.Log("Updating remote #" + id);
                NetworkObject remote = _remoteObjects[id];
                Deserialize(tag, reader, remote);
                if (reader.Length > reader.Position)
                    tag = reader.ReadUInt16();
                else
                    break;
            }
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
            AddObject();
    }
}
