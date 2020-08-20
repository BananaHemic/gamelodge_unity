using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Photon;
//using Photon.Pun;
//using Photon.Realtime;
//using ExitGames.Client.Photon;
using System;

public class SendAndReceiveFaceData : GenericSingleton<SendAndReceiveFaceData>
{
    const float SendInterval = 1f / 50f; // 50 times a second, 20ms (runs with framerate, so expect some jitter if it doesn't % the user's framerate / camera)
    const int NumAnimationTypes = 42; // From faceware
    const int NumBytesPerValue = 2;
    const int MaxVoiceMessageSize = NumAnimationTypes * NumBytesPerValue; // Avg size is ~20 * 2
    public static readonly byte FacePhotonEventCode = 6;

    private readonly IEnumerator _faceSendInterval = new WaitForSecondsRealtime(SendInterval);
    //private readonly Dictionary<string, float> _localPreviousAnimationValues = new Dictionary<string, float>();
    private readonly byte[] _localAnimationSerialized = new byte[MaxVoiceMessageSize];
    private readonly Dictionary<int, UserDisplay> _faceDataReceivers = new Dictionary<int, UserDisplay>();
    private readonly FaceDataSerializer _faceSerializer = new FaceDataSerializer();

    private Dictionary<string, float> _queuedAnimData = null;
    private Coroutine _sendFaceDataRoutine;

    protected override void Awake()
    {
        base.Awake();
    }

    public void RegisterFaceDataReceiver(int photonUserNumber, UserDisplay networkUser)
    {
        _faceDataReceivers.Add(photonUserNumber, networkUser);
    }
    public void RemoveFaceDataReceiver(int photonUserNumber)
    {
        _faceDataReceivers.Remove(photonUserNumber);
    }
    public void RecvSerializedFaceData(byte[] data, int senderNum)
    {
        UserDisplay networkUser;
        if(!_faceDataReceivers.TryGetValue(senderNum, out networkUser))
        {
            Debug.LogWarning("No face received for #" + senderNum);
            return;
        }

        //networkUser.OnFaceData(data);
    }
    private void SendFaceDataPacket(ArraySegment<byte> segment)
    {
        //var sendOpt = new SendOptions()
        //{
        //    Reliability = false,
        //    Channel = 0, //TODO not sure how this is used
        //    Encrypt = false
        //};

        //var opt = new RaiseEventOptions
        //{
        //    InterestGroup = 0, // Send to all
        //    //Receivers = ReceiverGroup.All
        //    Receivers = ReceiverGroup.Others
        //};
        //PhotonNetwork.NetworkingClient.OpRaiseEvent(FacePhotonEventCode, segment, opt, sendOpt);
    }

    public void QueueSendFaceData(Dictionary<string, float> animData)
    {
        _queuedAnimData = animData;
        if(_sendFaceDataRoutine == null)
            _sendFaceDataRoutine = StartCoroutine(SendFaceDataInterval());
    }
    public bool DeserializeAnimationValues(byte[] data, Dictionary<string, float> deserialized)
    {
        return _faceSerializer.DeserializeAnimationValues(data, deserialized);
    }
    IEnumerator SendFaceDataInterval()
    {
        //while (PhotonNetwork.CurrentRoom == null)
            //yield return null;

        while (true)
        {
            // Poll until Faceware provides a new image
            while (_queuedAnimData == null)
                yield return null;
            //Debug.Log("Sending face pkt");
            // Serialize the data
            var pkt = _faceSerializer.SerializeAnimationValues(_queuedAnimData, _localAnimationSerialized);
            // Send the data over the network
            SendFaceDataPacket(pkt);
            _queuedAnimData = null;

            yield return _faceSendInterval;
        }
    }
}
