using DarkRift;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCharacterSync : GenericSingleton<TestCharacterSync>
{
    /*
    public bool FireInUpdate = true;
    UserDisplay _localPlayer;
    UserDisplay _networkPlayer;
    //DRUser _localNetObj;
    //DRUser _networkNetObj;
    private readonly Queue<MsgWithTag> _messages = new Queue<MsgWithTag>();
    private struct MsgWithTag
    {
        public Message Message;
        public byte Tag;
    }

    IEnumerator Start()
    {
        Application.targetFrameRate = 90;
        QualitySettings.vSyncCount = 0;
        BuildPlayManager.Instance.SetSpawnedPlayModeTesting(true);
        RealtimeNetworkUpdater.Instance.EnableTesting(RealtimeNetworkUpdater.TestMode.CharacterSync);
        yield return null;

        // Create a NetworkPlayerDisplay for the local
         _localPlayer = GameObject.Instantiate(UserManager.Instance.NetworkUserPrefab);
         _networkPlayer = GameObject.Instantiate(UserManager.Instance.NetworkUserPrefab);
        _localNetObj = new DRUser(new Vec3(), new Quat(), 0, true);
        _networkNetObj = new DRUser(new Vec3(), new Quat(), 1, true);
        _localPlayer.Init_Testing(_localNetObj, true);
        _networkPlayer.Init_Testing(_networkNetObj, false);
    }
    public void OnReliableServerMessage(Message msg, byte firstTag)
    {
    }
    public void OnUnreliableServerMessage(Message msg, byte firstTag)
    {
        // simulate drops with =
        if (Input.GetKey(KeyCode.Equals))
            return;
        if (FireInUpdate)
        {
            _messages.Enqueue(new MsgWithTag
            {
                Message = msg,
                Tag = firstTag
            });
        }
        else
        {
            ProcessMessage(msg, firstTag);
        }
        //Debug.Log("Message len: " + msg.DataLength);
        // Parse out everything
    }
    private void ProcessMessage(Message msg, byte firstTag)
    {
        byte tag = firstTag;
        using(DarkRiftReader reader = msg.GetReader())
        {
            while (true)
            {
                switch (tag)
                {
                    case ServerTags.PlayerMovement_Play_Grounded:
                    case ServerTags.PlayerMovement_Play_NotGrounded:
                        _networkPlayer.UpdateFromPlayerMovementPlayMessage(reader, tag);
                        break;
                    case ServerTags.UserPose_Single:
                    case ServerTags.UserPose_ThreePoints:
                    case ServerTags.UserPose_Full:
                        _networkPlayer.UpdateFromPoseMessage(reader, tag);
                        break;
                }
                //Debug.Log("Updating remote #" + id);
                //Debug.Log("len " + reader.Length + " pos " + reader.Position + " tag " + tag);
                if (reader.Length > reader.Position)
                    tag = reader.ReadByte();
                else
                    break;
            }
        }
    }
    private void Update()
    {
        while(_messages.Count > 0)
        {
            var msg = _messages.Dequeue();
            ProcessMessage(msg.Message, msg.Tag);
        }
        _localPlayer.LocalUpdate();
    }
    */
}
