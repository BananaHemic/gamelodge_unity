using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DarkRift;

/// <summary>
/// Polls all objects, to see if they need to be updated.
/// Each queried object replies with
/// 1) whether it needs any update (return)
/// 2) the serialized data
/// 3) priority of the update (low priority - 0 higher is more important)
/// 4) 
/// Unless there no updates at all during a frame, we send out a packet with
/// the available data. This means, that if we have a lot of object, the max
/// bitrate will be MaxPktSize * UpdateFrequency
/// Current settings makes that 25Kbps
/// </summary>
//TODO it would be ideal if we had some communication with the server, about
// which packets were dropped. This way we could inform the object that they
// need to re-increase their priority
public class RealtimeNetworkUpdater : GenericSingleton<RealtimeNetworkUpdater>
{
    /// <summary>
    /// How often should we send out updates on where our head
    /// and hands are?
    /// </summary>
    public float PoseSendRateHz = 5f;
    /// <summary>
    /// In play mode, how frequently should updates be sent out
    /// at the minimum? (Changes in input, or jumping will cause
    /// higher send rates)
    /// </summary>
    public float PlayModeMinSendRateHz = 5f;
    /// <summary>
    /// In build mode, how frequently should updates be sent out
    /// at the minimum? (Changes in input, or jumping will cause
    /// higher send rates)
    /// </summary>
    public float BuildModeMinSendRateHz = 5f;
    /// <summary>
    /// At creation or ownership-taken time, how
    /// much of a priority should the object begin with
    /// </summary>
    public uint InitialPriority = 1000;
    public uint PriorityIncreasePerFrameMoving = 1;
    public uint PriorityIncreasePerCollision = 500;
    public uint PriorityIncreasePerFrameBehavior = 200;
    public uint PriorityIncreasePerFrameMaterial = 150;
    public uint MinPhysicsGrabbedPriority = 1000;
    public uint UserPosePriority = 100000;
    public uint RagdollPoseHitPriority = 50000;
    /// <summary>
    /// How high of a priority does the object need to be considered for network sending at all?
    /// This is useful to not send out updates for objects that don't really need it
    /// </summary>
    public uint MinPriority = 3;
    /// <summary>
    /// How high of a priority does the object need to be considered for network sending at all
    /// For when the object has no RB and has not changed it's position or rotation. We need to 
    /// keep sending a little bit, because people might drop some messages here or there
    /// </summary>
    public uint MinPriorityForNoChange = 500;
    // TODO experiment with this value
    public int MaxPacketSize = 512; // https://gamedev.stackexchange.com/questions/101200/packet-size-vs-packet-frequency
    const int FinalWriterSize = 1024;
    //public int PrioritiIncrease

    //public int MinTargetBitrate 

    public enum TestMode
    {
        Off,
        ObjectSync,
        CharacterSync,
    }

    struct RealtimeObjectWithWriter
    {
        public IRealtimeObject RealtimeObject;
        public DarkRiftWriter Writer;
        public byte Tag;
    }
    struct RealtimeObjectWithWriterAndPriority
    {
        public RealtimeObjectWithWriter Data;
        public uint Priority;
    }
    struct WriterWithTag
    {
        public DarkRiftWriter Writer;
        public byte Tag;
    }

    const int InitialCapacity = 1024;
    private readonly List<IRealtimeObject> _allPullBasedRealtimeObjects = new List<IRealtimeObject>(InitialCapacity);
    private readonly List<RealtimeObjectWithWriterAndPriority> _pushBasedRealtimeObjectsThisFrame = new List<RealtimeObjectWithWriterAndPriority>(32);
    private readonly List<WriterWithTag> _pendingReliableMessages = new List<WriterWithTag>();
    /// <summary>
    /// We use a list here, instead of just using the values in SortedList, because you have to use a
    /// foreach loop in SortedList, which ends up allocating data
    /// </summary>
    private readonly List<DarkRiftWriter> _allocatedWriters = new List<DarkRiftWriter>(InitialCapacity);
    /// <summary>
    /// We use a sorted dictionary instead of a sorted list, because the inputs
    /// </summary>
    private readonly UIntComparer _uintComparer = new UIntComparer();
    private SortedList<uint, RealtimeObjectWithWriter> _priority2Obj;
    private Coroutine _testingUpdateRoutine;
    private TestMode _currentTestMode;

    void Start()
    {
        // We have to use our own comparer function, because SortedList doesn't
        // like having duplicate keys
        _priority2Obj = new SortedList<uint, RealtimeObjectWithWriter>(InitialCapacity, _uintComparer);
        //TODO it might make more sense to send off one of these at the end of the frame, and the other at the beginning of the frame
        ControllerAbstraction.OnSendNetworkMessagesToWire += SendReliableMessages;
        ControllerAbstraction.OnSendNetworkMessagesToWire += SendUnreliableMessages;
    }
    public void RegisterRealtimeObject(IRealtimeObject realtimeObject)
    {
        _allPullBasedRealtimeObjects.Add(realtimeObject);
    }
    public void RemoveRealtimeObject(IRealtimeObject realtimeObject)
    {
        _allPullBasedRealtimeObjects.RemoveBySwap(realtimeObject);
    }
    /// <summary>
    /// Adds a tag/writer pair to be send in a mushed reliable message
    /// at the end of the frame. Writer may be null to signify an empty
    /// message
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="writer"></param>
    public void EnqueueReliableMessage(byte tag, DarkRiftWriter writer)
    {
        _pendingReliableMessages.Add(new WriterWithTag
        {
            Writer = writer,
            Tag = tag,
        });
    }
    public void EnqueueUnreliableUpdate(IRealtimeObject realtimeObject, DarkRiftWriter writer, byte tag, uint priority)
    {
        RealtimeObjectWithWriter realtimeObjectWithWriter = new RealtimeObjectWithWriter()
        {
            RealtimeObject = realtimeObject,
            Writer = writer,
            Tag = tag
        };
        RealtimeObjectWithWriterAndPriority objectWithWriterAndPriority = new RealtimeObjectWithWriterAndPriority()
        {
            Data = realtimeObjectWithWriter,
            Priority = priority
        };
        // First make sure we don't already have an update for this object
        // if we do, just replace the old one with this one
        for(int i = 0; i < _pushBasedRealtimeObjectsThisFrame.Count; i++)
        {
            if(_pushBasedRealtimeObjectsThisFrame[i].Data.RealtimeObject == realtimeObject)
            {
                //Debug.Log("Replacing push based update with new version");
                // Clear the writer that was previosly used
                _pushBasedRealtimeObjectsThisFrame[i].Data.Writer.Dispose();
                _pushBasedRealtimeObjectsThisFrame[i] = objectWithWriterAndPriority;
                return;
            }
        }
        _pushBasedRealtimeObjectsThisFrame.Add(objectWithWriterAndPriority);
    }
    public void EnableTesting(TestMode testMode)
    {
#if !UNITY_EDITOR
        Debug.LogError("Realtime testing on for editor");
#endif
        _currentTestMode = testMode;
        _testingUpdateRoutine = StartCoroutine(TestingSendMessages());
    }
    private void SendReliableMessages()
    {
        if (_pendingReliableMessages.Count == 0)
            return;
        byte initialTag = byte.MaxValue;
        // We mush all reliable updates into a single TCP messages
        // for network efficiency
        using (DarkRiftWriter finalWriter = DarkRiftWriter.Create(FinalWriterSize))
        {
            for(int i = 0; i < _pendingReliableMessages.Count; i++)
            {
                WriterWithTag writerWithTag = _pendingReliableMessages[i];
                if (i == 0)
                    initialTag = writerWithTag.Tag;
                else
                    finalWriter.Write(writerWithTag.Tag);
                if(writerWithTag.Writer != null)
                {
                    finalWriter.WriteRaw(writerWithTag.Writer);
                    writerWithTag.Writer.Dispose();
                }
            }
            _pendingReliableMessages.Clear();
            // Send the packet
            using (Message msg = Message.Create(initialTag, finalWriter))
            {
                if (_currentTestMode == TestMode.Off)
                    DarkRiftConnection.Instance.SendReliableMessage(msg);
                else if(_currentTestMode == TestMode.ObjectSync)
                    TestSync.Instance.OnReliableServerMessage(msg, initialTag);
                //else if(_currentTestMode == TestMode.CharacterSync)
                    //TestCharacterSync.Instance.OnReliableServerMessage(msg, initialTag);
            }
        }
    }
    private void SendUnreliableMessages()
    {
        //long memPre = System.GC.GetTotalMemory(false);
        // Dispose manually, so that we get these objects back into the pool
        // We do this because it's ideal to have writers with the same memory
        // length that we already have
        for (int i = 0; i < _allocatedWriters.Count; i++)
            _allocatedWriters[i].Dispose();
        _allocatedWriters.Clear();
        _priority2Obj.Clear();

        // We only do pull-mode if not paused. Push mode works even when paused though
        if (TimeManager.Instance.IsPlayingOrStepped)
        {
            // Get all objects requesting updates in pull-mode
            DarkRiftWriter nextWriterToUse = DarkRiftWriter.Create();
            //Debug.Log("Handling " + _allPullBasedRealtimeObjects.Count + " pull based objects");
            for (int i = 0; i < _allPullBasedRealtimeObjects.Count; i++)
            {
                IRealtimeObject realtimeObject = _allPullBasedRealtimeObjects[i];
                uint priority = 0;
                byte tag = 0;

                if (!realtimeObject.NetworkUpdate(nextWriterToUse, out tag, out priority))
                    continue;

                // Keep track of this object and it's writer, associated with the priority;
                RealtimeObjectWithWriter withWriter = new RealtimeObjectWithWriter
                {
                    RealtimeObject = realtimeObject,
                    Writer = nextWriterToUse,
                    Tag = tag
                };
                _priority2Obj.Add(priority, withWriter);
                _allocatedWriters.Add(nextWriterToUse);

                // Make a new writer
                nextWriterToUse = DarkRiftWriter.Create();
                //Debug.Log("Obj #" + i + " has priority " + priority);
            }
            nextWriterToUse.Dispose();
        }

        // Get all objects requesting updates in push-mode
        for(int j = 0; j < _pushBasedRealtimeObjectsThisFrame.Count;j++)
        {
            RealtimeObjectWithWriterAndPriority pushObj = _pushBasedRealtimeObjectsThisFrame[j];
            _priority2Obj.Add(pushObj.Priority, pushObj.Data);
            _allocatedWriters.Add(pushObj.Data.Writer);
        }
        _pushBasedRealtimeObjectsThisFrame.Clear();

        // Exit early if there were no updates at all
        if (_priority2Obj.Count == 0)
        {
            //Debug.Log("No updates");
            return;
        }

        // Now that we have all object data associated with a priority, we
        // start sending data from low->high priority until we hit max packet size
        int totalMessageLength = 0;
        int index = 0;
        //Debug.Log("Sending update w/ priority " + _priority2Obj.Keys[index]);
        RealtimeObjectWithWriter sendObj = _priority2Obj.Values[index++];
        byte initialTag = sendObj.Tag;
        // Account for tag size in data length, and the 1 extra byte from DR
        totalMessageLength += sizeof(byte) + sizeof(byte);

        using (DarkRiftWriter finalWriter = DarkRiftWriter.Create(FinalWriterSize))
        {
            finalWriter.WriteRaw(sendObj.Writer);
            totalMessageLength += sendObj.Writer.Length;
            //Debug.Log(nPkts + ", adding " + sendObj.Writer.Length);
            sendObj.RealtimeObject?.ClearPriority();

            while (totalMessageLength < MaxPacketSize
                && index < _priority2Obj.Count)
            {
                //Debug.Log("Sending update w/ priority " + _priority2Obj.Keys[index]);
                sendObj = _priority2Obj.Values[index++];
                // Manually serialize the tag
                finalWriter.Write(sendObj.Tag);
                totalMessageLength += sizeof(byte);
                // Add the object data
                finalWriter.WriteRaw(sendObj.Writer);
                totalMessageLength += sendObj.Writer.Length;
                // Notify object about this send
                sendObj.RealtimeObject?.ClearPriority();
            }
            //long memPost = System.GC.GetTotalMemory(false);
            //Debug.Log("Allocation: " + (memPost - memPre) / 1024 + "kb");

            //Debug.Log("Sent " + index + " updates with a total of " + totalMessageLength + "bytes");
            // Send the packet
            //Debug.Log("Final size " + finalWriter.Length + " from " + nPkts + " packets");
            using (Message msg = Message.Create(initialTag, finalWriter))
            {
                if (_currentTestMode == TestMode.Off)
                    DarkRiftConnection.Instance.SendUnreliableMessage(msg);
                else if(_currentTestMode == TestMode.ObjectSync)
                    TestSync.Instance.OnUnreliableServerMessage(msg, initialTag);
                //else if(_currentTestMode == TestMode.CharacterSync)
                    //TestCharacterSync.Instance.OnUnreliableServerMessage(msg, initialTag);
            }
        }
    }
    IEnumerator TestingSendMessages()
    {
        WaitForEndOfFrame waitUntilFrameEnd = new WaitForEndOfFrame();
        while (true)
        {
            yield return waitUntilFrameEnd; // Let all other objects update first
            SendUnreliableMessages();
            SendReliableMessages();
        }
    }
}

public class UIntComparer : IComparer<uint>
{
    public int Compare(uint x, uint y)
    {
        if (x == y)
            return 1;

        // We flip this around, so (x=2,y=3) would return 2. This is to get a list in descending order
        // We do a 2x so that equal items have a difference of 1, and sequential ints have a difference of 2
        uint delMag;
        if (x > y)
        {
            delMag = x - y;
            if(2 * delMag < delMag) // Close to a wraparound
                return int.MinValue;
            delMag *= 2;
            if (delMag > int.MaxValue)
                return int.MinValue;
            return -(int)delMag;
        }
        delMag = y - x;
        if(2 * delMag < delMag) // Close to a wraparound
            return int.MaxValue;
        delMag *= 2;
        if (delMag > int.MaxValue)
            return int.MaxValue;
        return (int)delMag;
    }
}

public interface IRealtimeObject
{
    bool NetworkUpdate(DarkRiftWriter writer, out byte tag, out uint priority);
    void ClearPriority();
}
