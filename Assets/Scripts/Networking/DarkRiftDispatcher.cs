using DarkRift;
using DarkRift.Client;
using DarkRift.Dispatching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class DarkRiftDispatcher : IDisposable
{
    /// <summary>
    ///     The ID the client has been assigned.
    /// </summary>
    public ushort ID
    {
        get
        {
            return Client.ID;
        }
    }
    /// <summary>
    ///     Returns the state of the connection with the server.
    /// </summary>
    public ConnectionState ConnectionState
    {
        get
        {
            return Client.ConnectionState;
        }
    }

    public struct ReceivedMessage
    {
        public Message message;
        public SendMode sendMode;
    }
    /// <summary>
    /// 	The actual client connecting to the server.
    /// </summary>
    /// <value>The client.</value>
    public DarkRiftClient Client { get; }

    public delegate void OnMessageRecv(Message args, SendMode sendMode);
    public delegate void OnDisconnect(DisconnectedEventArgs args);

    private readonly System.Object _pendingMessageLock = new System.Object();
    private readonly Queue<ReceivedMessage> _pendingMessages = new Queue<ReceivedMessage>();
    private readonly System.Object _pendingDisconnectLock = new System.Object();
    private readonly Queue<DisconnectedEventArgs> _pendingDisconnects = new Queue<DisconnectedEventArgs>();
    private readonly OnMessageRecv _onMsg;
    private readonly OnDisconnect _onDisconnect;
    private static int NumInstances = 0;

    private DarkRiftClient.ConnectCompleteHandler _connectCallback;
    private Exception _pendingConnectException;
    private bool _hasPendingConnectCall = false;
    private bool _disposed = false;

    const int MaxCachedWriters = 256;
    const int MaxCachedReaders = 16;
    const int MaxCachedMessages = 128;
    const int MaxCachedAsyncArgs = 32;
    const int MaxCachedActionDispatcherTasks = 32;
    const int MaxRecycledArrays = 512;
    const int MaxArrayOfSize = 256;

    public DarkRiftDispatcher(OnMessageRecv onMessage, OnDisconnect onDisconnect)
    {
        if(NumInstances != 0)
            Debug.LogError("Existing instance(s)! " + NumInstances);
        NumInstances++;

        _onMsg = onMessage;
        _onDisconnect = onDisconnect;
        ClientObjectCacheSettings clientObjectCacheSettings = DarkRiftClient.DefaultClientCacheSettings;
        clientObjectCacheSettings.MaxWriters = MaxCachedWriters;
        clientObjectCacheSettings.MaxReaders = MaxCachedReaders;
        clientObjectCacheSettings.MaxMessages = MaxCachedMessages;
        clientObjectCacheSettings.MaxSocketAsyncEventArgs = MaxCachedAsyncArgs;
        clientObjectCacheSettings.MaxActionDispatcherTasks = MaxCachedActionDispatcherTasks;
        clientObjectCacheSettings.MaxAutoRecyclingArrays = MaxRecycledArrays;

        clientObjectCacheSettings.MaxExtraSmallMemoryBlocks = MaxArrayOfSize;
        clientObjectCacheSettings.MaxSmallMemoryBlocks = MaxArrayOfSize;
        clientObjectCacheSettings.MaxMediumMemoryBlocks = MaxArrayOfSize;
        clientObjectCacheSettings.MaxLargeMemoryBlocks = MaxArrayOfSize;
        clientObjectCacheSettings.MaxExtraLargeMemoryBlocks = MaxArrayOfSize;
        
        Client = new DarkRiftClient(clientObjectCacheSettings);
        
        Client.MessageReceived += OnClientReceivedMessage;
        Client.Disconnected += OnClientDisconnect;
    }
    public void Connect(IPAddress ip, int port, DarkRiftClient.ConnectCompleteHandler callback)
    {
        _connectCallback = callback;
        Client.ConnectInBackground(
            ip,
            port,
            true, // Turn on NoDelay for min latency
            OnConnectCallback
        );
    }
    private void OnConnectCallback(Exception e)
    {
        if (this == null || _disposed)
            return;
        // Notify the client on the main thread
        _pendingConnectException = e;
        _hasPendingConnectCall = true;
    }
    private void OnClientReceivedMessage(object sender, MessageReceivedEventArgs messageReceivedEvent)
    {
        if (this == null || _disposed)
            return;
        lock (_pendingMessageLock)
        {
            _pendingMessages.Enqueue(new ReceivedMessage
            {
                message = messageReceivedEvent.GetMessage(),
                sendMode = messageReceivedEvent.SendMode
            });
        }
    }
    private void OnClientDisconnect(object sender, DisconnectedEventArgs disconnectedEvent)
    {
        if (this == null || _disposed)
            return;
        lock (_pendingDisconnectLock)
            _pendingDisconnects.Enqueue(disconnectedEvent);
    }
    /// <summary>
    ///     Sends a message to the server.
    /// </summary>
    /// <param name="message">The message template to send.</param>
    /// <returns>Whether the send was successful.</returns>
    public bool SendMessage(Message message, SendMode sendMode)
    {
        return Client.SendMessage(message, sendMode);
    }
    private ReceivedMessage GetNextMessage()
    {
        lock (_pendingMessageLock)
        {
            if (_pendingMessages.Count == 0)
                return default(ReceivedMessage);
            return _pendingMessages.Dequeue();
        }
    }
    private DisconnectedEventArgs GetNextDisconnect()
    {
        lock (_pendingDisconnectLock)
        {
            if (_pendingDisconnects.Count == 0)
                return null;
            return _pendingDisconnects.Dequeue();
        }
    }
    public void Poll()
    {
        // Fire any connect callbacks if we have them
        if (_hasPendingConnectCall)
        {
            _connectCallback(_pendingConnectException);
            _pendingConnectException = null;
            _hasPendingConnectCall = false;
        }

        // Fire all pending messages
        ReceivedMessage pendingMessage = GetNextMessage();
        while(pendingMessage.message != null)
        {
            _onMsg(pendingMessage.message, pendingMessage.sendMode);
            pendingMessage.message.Dispose();
            pendingMessage = GetNextMessage();
        }

        // Fire any pending disconnect(s)
        DisconnectedEventArgs pendingDisconnect = GetNextDisconnect();
        while(pendingDisconnect != null)
        {
            _onDisconnect(pendingDisconnect);
            pendingDisconnect = GetNextDisconnect();
        }
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
#if UNITY_EDITOR
        //Debug.LogWarning("Disposing DR dispatcher!");
        Client.MessageReceived -= OnClientReceivedMessage;
        Client.Disconnected -= OnClientDisconnect;
        Client.Dispose();
        NumInstances--;
        //Debug.LogWarning("DR dispatcher disposed");
#endif
    }
}
