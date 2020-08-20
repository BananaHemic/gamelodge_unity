using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class LocalFaceAnimator : MonoBehaviour
{
    //private FaceDisplay _faceDisplay;
    //private LiveConnection _live;

    //private int _numPacketsDropped = 0;
    //const int FacewareServerPort = 802;
    //const int MaxMissedPackets = 90;

    //void Start()
    //{
        
    //}

    //public static string GetLocalIPAddress()
    //{
    //    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
    //    foreach (var ip in host.AddressList)
    //    {
    //        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    //        {
    //            return ip.ToString();
    //        }
    //    }

    //    throw new System.Exception("No network adapters with an IPv4 address in the system!");
    //}
    //public void Init()
    //{
    //    //Setup Connection to Live
    //    _live = new LiveConnection(GetLocalIPAddress(), FacewareServerPort)
    //    {
    //        m_Reconnect = true,
    //        m_DropPackets = true
    //    };
    //    _live.Connect();
    //}

    //private void OnDestroy()
    //{
    //    if (_live != null)
    //        _live.Dispose();
    //    _live = null;
    //}
    //public void SetFaceDisplay(FaceDisplay faceDisplay)
    //{
    //    _faceDisplay = faceDisplay;
    //}

    //void Update()
    //{
    //    if (_live == null || !_live.IsConnected())
    //        return;
    //    var json = _live.GetLiveData();
    //    if(json == null || json.Count == 0)
    //    {
    //        _numPacketsDropped++;

    //        if(_numPacketsDropped > MaxMissedPackets)
    //        {
    //            Debug.LogWarning("Live server not connected, will try to reconnect");
    //            _numPacketsDropped = 0;
    //            _live.Disconnect();
    //            _live.Connect();
    //        }
    //        return;
    //    }

    //    _numPacketsDropped = 0;
    //    var values = json["animationValues"];
    //    Dictionary<string, float> animationValues = new Dictionary<string, float>();
    //    foreach (var key in values.Keys)
    //    {
    //        if (key == "head_RightTilt" || key == "head_LeftTilt")
    //            animationValues.Add(key, Math.Abs(values[key].AsFloat));
    //        else
    //            animationValues.Add(key, values[key].AsFloat);
    //    }

    //    // Apply it to any local models
    //    if (_faceDisplay != null)
    //        _faceDisplay.AddFaceData(animationValues);
    //    // Send it to all other clients
    //    SendAndReceiveFaceData.Instance.QueueSendFaceData(animationValues);
    //}
}
