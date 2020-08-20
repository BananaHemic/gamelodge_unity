using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRift;
using System;

public class DarkRiftPingTime : GenericSingleton<DarkRiftPingTime>
{
    public float PreviousRTT { get; private set; }
    public uint ServerTime { get
        {
            // Time = (server time) + RTT + Time since last server update
            return _previousServerTime + (uint)System.Math.Round((PreviousRTT + (_serverTimeStopwatch.ElapsedTicks / TimeSpan.TicksPerSecond)) / 1000.0);
        } }

    //private double _timeOfLastPing;
    private readonly System.Diagnostics.Stopwatch _pingStopwatch = new System.Diagnostics.Stopwatch();
    private readonly System.Diagnostics.Stopwatch _serverTimeStopwatch = new System.Diagnostics.Stopwatch();
    private Coroutine _pingRoutine;
    private bool _didRecvPingThisFrame = false;
    private uint _previousServerTime;
    const float PingPeriod = 0.5f;

    void Start()
    {
        DarkRiftConnection.OnConnected += StartPinging;
    }
    private void StartPinging()
    {
        // We receive pongs on a separate thread, to ensure accurate results
        DarkRiftConnection.Instance.Dispatcher.Client.MessageReceived += MessageReceivedThreaded;
        _pingRoutine = StartCoroutine(PingServer());
    }

    private void MessageReceivedThreaded(object sender, DarkRift.Client.MessageReceivedEventArgs e)
    {
        using (Message msg = e.GetMessage())
        {
            if (msg.Tag != ServerTags.PingPong)
                return;
            using (DarkRiftReader reader = msg.GetReader())
            {
                //Debug.Log("threaded recv ping " + reader.Length);
                OnReceivePongThreaded(reader);
            }
        }
    }
    private void OnReceivePongThreaded(DarkRiftReader reader)
    {
        _didRecvPingThisFrame = true;
        // Calculate ping
        //PreviousRTT = (float)(AudioSettings.dspTime - _timeOfLastPing);
        PreviousRTT = _pingStopwatch.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
        //Debug.Log("RTT: " + PreviousRTT);
        // Get the time of the server, (when it sent it)
        _previousServerTime = reader.ReadUInt32();
        _serverTimeStopwatch.Restart();
        //Debug.Log("Server time " + _previousServerTime + " interpolated to: " + ServerTime);
    }
    private float CurrentTime()
    {
        return Time.realtimeSinceStartup;
    }
    private IEnumerator PingServer()
    {
        float startTime = CurrentTime();
        while (CurrentTime() - startTime < 0.1f)
            yield return null;
        Message pingMessage = Message.CreateEmpty(ServerTags.PingPong);

        while (true)
        {
            // Send the ping
            _didRecvPingThisFrame = false;
            //_timeOfLastPing = AudioSettings.dspTime;
            _pingStopwatch.Restart();
            startTime = CurrentTime();
            //Debug.Log("Sending ping " + _timeOfLastPing);
            DarkRiftConnection.Instance.SendUnreliableMessage(pingMessage);
            // Wait for the next ping, or the timeout
            while (!_didRecvPingThisFrame && (CurrentTime() - startTime) < PingPeriod)
                yield return null;
            if (!_didRecvPingThisFrame)
            {
                Debug.LogWarning("Dropped ping");
                continue;
            }

            // Wait until we're next expected to send out a ping
            while (CurrentTime() - startTime < PingPeriod)
                yield return null;
        }
    }
}
