using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using DarkRift;

namespace DarkRiftAudio {
    public class AudioDecodeThread : IDisposable{

        private readonly DarkRiftAudioClient _mumbleClient;
        private readonly AutoResetEvent _waitHandle;
        private readonly Thread _decodeThread;
        private readonly int _outputSampleRate;
        private readonly int _outputChannelCount;
        private readonly Queue<OpusDecoder> _unusedDecoders = new Queue<OpusDecoder>();
        private readonly Dictionary<ushort, DecoderState> _currentDecoders = new Dictionary<ushort, DecoderState>();
        private readonly Queue<MessageData> _messageQueue = new Queue<MessageData>();

        private bool _isDisposing = false;

        /// <summary>
        /// How many packets go missing before we figure they were lost
        /// Due to murmur
        /// </summary>
        const long MaxMissingPackets = 25;
        const int SubBufferSize = DarkRiftAudioConstants.OUTPUT_FRAME_SIZE * DarkRiftAudioConstants.MAX_FRAMES_PER_PACKET * DarkRiftAudioConstants.MAX_CHANNELS;

        public AudioDecodeThread(int outputSampleRate, int outputChannelCount, DarkRiftAudioClient mumbleClient)
        {
            _mumbleClient = mumbleClient;
            _waitHandle = new AutoResetEvent(false);
            _outputSampleRate = outputSampleRate;
            _outputChannelCount = outputChannelCount;
            _decodeThread = new Thread(DecodeThread);
            _decodeThread.Start();
        }

        internal void AddDecoder(ushort playerID)
        {
            MessageData addDecoderMsg = new MessageData
            {
                TypeOfMessage = MessageType.AllocDecoderState,
                PlayerID = playerID
            };
            lock (_messageQueue)
                _messageQueue.Enqueue(addDecoderMsg);
            _waitHandle.Set();
        }
        internal void RemoveDecoder(ushort playerID)
        {
            MessageData removeDecoderMsg = new MessageData
            {
                TypeOfMessage = MessageType.FreeDecoder,
                PlayerID = playerID
            };
            lock (_messageQueue)
                _messageQueue.Enqueue(removeDecoderMsg);
            _waitHandle.Set();
        }
        internal void AddCompressedAudio(DarkRiftReader reader, MessageDirection msgDir, bool isLocalRecordedMessage)
        {
            // writing code in ManageAudioSendBuffer
            ushort audioSender;
            if (msgDir == MessageDirection.Server2Client)
            {
                audioSender = reader.ReadUInt16();
                if(isLocalRecordedMessage
                    && GameRecordingManager.Instance.TryGetCurrentIDForRecordedPlayer(audioSender, out ushort newSenderID))
                    audioSender = newSenderID;
            }
            else
                audioSender = GameRecordingManager.Instance.RecordedClientID;
            int initialPos = reader.Position;
            byte audioType = reader.ReadByte();
            uint sequence = reader.ReadUInt32();
            byte pktLen = reader.ReadByte();
            // Read the data for the user's phoneme
            EncodedAudioArray encodedAudio = EncodedAudioArray.GetAvailableEncodedAudioArray();
            encodedAudio.SetLength(pktLen);
            for (int i = 0; i < pktLen; i++)
                encodedAudio.CompressedAudio[i] = reader.ReadByte();
            // Read the user's mouth phonemes
            reader.ReadSerializableInto(ref encodedAudio.MouthPose);

            // If this is a recorded message, we'll want to echo this to the server
            if (isLocalRecordedMessage)
            {
                // Reduce allocations by providing the expected message length
                int expectedLen = (reader.Position - initialPos) + 2;
                using(DarkRiftWriter writer = DarkRiftWriter.Create(expectedLen))
                {
                    writer.Write(audioSender);
                    writer.Write(audioType);
                    writer.Write(sequence);
                    writer.Write(pktLen);
                    for (int i = 0; i < pktLen; i++)
                        writer.Write(encodedAudio.CompressedAudio[i]);
                    writer.Write(encodedAudio.MouthPose);

                    using (Message msg = Message.Create(ServerTags.VoiceData_Recorded, writer))
                        DarkRiftConnection.Instance.SendUnreliableMessage(msg);
                }
            }

            bool isLast = (audioType & (1 << 7)) != 0;
            AddCompressedAudio(audioSender, encodedAudio, sequence, isLast);

        }
        private void AddCompressedAudio(ushort playerID, EncodedAudioArray encodedAudio, long sequence,
            bool isLast)
        {
            if (_isDisposing)
                return;

            MessageData compressed = new MessageData
            {
                TypeOfMessage = MessageType.DecompressData,
                PlayerID = playerID,
                EncodedAudio = encodedAudio,
                Sequence = sequence,
                IsLast = isLast
            };

            lock (_messageQueue)
                _messageQueue.Enqueue(compressed);
            _waitHandle.Set();
        }

        private void DecodeThread()
        {
            while (!_isDisposing)
            {
                _waitHandle.WaitOne();
                // Keep looping until either disposed
                // or the message queue is depleted
                while (!_isDisposing)
                {
                    try
                    {
                        MessageData messageData;
                        lock (_messageQueue)
                        {
                            if (_messageQueue.Count == 0)
                                break;
                            messageData = _messageQueue.Dequeue();
                        }

                        OpusDecoder decoder = null;
                        DecoderState decoderState;

                        switch (messageData.TypeOfMessage)
                        {
                            case MessageType.AllocDecoderState:
                                // If we receive an alloc decoder state message
                                // then we just need make an entry for it in
                                // current decoders. We don't bother assigning
                                // an actual opus decoder until we get data
                                // this is because there may be lots of users
                                // in current decoders, but only a few of them
                                // actually are sending audio
                                _currentDecoders[messageData.PlayerID] = new DecoderState();
                                //Debug.Log("Alloc'd DecoderState for session: " + messageData.Session);
                                break;
                            case MessageType.FreeDecoder:
                                if (_currentDecoders.TryGetValue(messageData.PlayerID, out decoderState))
                                {
                                    // Return the OpusDecoder
                                    if(decoderState.Decoder != null)
                                        _unusedDecoders.Enqueue(decoderState.Decoder);
                                    _currentDecoders.Remove(messageData.PlayerID);
                                    //Debug.Log("Removing DecoderState for session: " + messageData.Session);
                                }
                                else
                                    Debug.Log("Failed to remove decoder for session: " + messageData.PlayerID);
                                break;
                            case MessageType.DecompressData:
                                // Drop this audio, if there's no assigned decoder ready to receive it
                                if (!_currentDecoders.TryGetValue(messageData.PlayerID, out decoderState))
                                {
                                    Debug.LogWarning("No DecoderState for session: " + messageData.PlayerID);
                                    messageData.EncodedAudio.UnRef();
                                    break;
                                }
                                // Make an OpusDecoder if there isn't one
                                if(decoderState.Decoder == null)
                                {
                                    if (_unusedDecoders.Count > 0)
                                    {
                                        decoder = _unusedDecoders.Dequeue();
                                        decoder.ResetState();
                                    }
                                    else
                                    {
                                        decoder = new OpusDecoder(_outputSampleRate, _outputChannelCount);
                                    }
                                    //Debug.Log("Added OpusDecoder for DecoderState session: " + messageData.Session);
                                    decoderState.Decoder = decoder;
                                }
                                DecodeAudio(messageData.PlayerID, decoderState, messageData.EncodedAudio, messageData.Sequence,
                                    messageData.IsLast);
                                messageData.EncodedAudio.UnRef();
                                break;
                            default:
                                Debug.LogError("Message type not implemented:" + messageData.TypeOfMessage);
                                break;
                        }
                    }catch(Exception e)
                    {
                        Debug.LogError("Exception in decode thread: " + e.ToString());
                    }
                }
            }
        }
        private void DecodeAudio(ushort playerID, DecoderState decoderState, EncodedAudioArray encodedAudio, long sequence, bool isLast)
        {
            // We tell the decoded buffer to re-evaluate whether it needs to store
            // a few packets if the previous packet was marked last, or if there
            // was an abrupt change in sequence number
            bool reevaluateInitialBuffer = decoderState.WasPrevPacketMarkedLast;

            // Account for missing packets, out-of-order packets, & abrupt sequence changes
            if (decoderState.NextSequenceToDecode != 0)
            {
                long seqDiff = sequence - decoderState.NextSequenceToDecode;

                // If new packet is VERY late, then the sequence number has probably reset
                if(seqDiff < -MaxMissingPackets)
                {
                    Debug.Log("Sequence has possibly reset diff = " + seqDiff);
                    decoderState.Decoder.ResetState();
                    reevaluateInitialBuffer = true;
                }
                // If the packet came before we were expecting it to, but after the last packet, the sampling has probably changed
                // unless the packet is a last packet (in which case the sequence may have only increased by 1)
                else if (sequence > decoderState.LastReceivedSequence && seqDiff < 0 && !isLast)
                {
                    Debug.Log("Mumble sample rate may have changed");
                }
                // If the sequence number changes abruptly (which happens with push to talk)
                else if (seqDiff > MaxMissingPackets)
                {
                    Debug.Log("Mumble packet sequence changed abruptly pkt: " + sequence + " last: " + decoderState.LastReceivedSequence);
                    reevaluateInitialBuffer = true;
                }
                // If the packet is a bit late, drop it
                else if (seqDiff < 0 && !isLast)
                {
                    Debug.LogWarning("Received old packet " + sequence + " expecting " + decoderState.NextSequenceToDecode);
                    return;
                }
                // If we missed a packet, add a null packet to tell the decoder what happened
                else if (seqDiff > 0)
                {
                    Debug.LogWarning("dropped packet, recv: " + sequence + ", expected " + decoderState.NextSequenceToDecode);
                    //NumPacketsLost += packet.Value.Sequence - _nextSequenceToDecode;
                    DecodedAudioArray decodedAudio = DecodedAudioArray.GetAvailableDecodedAudioArray(encodedAudio.MouthPose);
                    int emptySampleNumRead = decoderState.Decoder.Decode(null, decodedAudio.PcmData);
                    decodedAudio.SetLength(emptySampleNumRead);
                    decoderState.NextSequenceToDecode = sequence + emptySampleNumRead / ((_outputSampleRate / 100) * _outputChannelCount);
                    //Debug.Log("Null read returned: " + emptySampleNumRead + " samples");

                    // Send this decoded data to the corresponding buffer
                    _mumbleClient.ReceiveDecodedVoice(playerID, decodedAudio, reevaluateInitialBuffer);
                    reevaluateInitialBuffer = false;
                }
            }

            //Debug.Log("Recv: " + sequence + " expected: " + decoderState.NextSequenceToDecode);

            int numRead = 0;
            if (encodedAudio.Length != 0)
            {
                DecodedAudioArray decodedAudio = DecodedAudioArray.GetAvailableDecodedAudioArray(encodedAudio.MouthPose);
                numRead = decoderState.Decoder.Decode(encodedAudio.CompressedAudio, decodedAudio.PcmData);
                decodedAudio.SetLength(numRead);
                // Send this decoded data to the corresponding buffer
                _mumbleClient.ReceiveDecodedVoice(playerID, decodedAudio,
                    reevaluateInitialBuffer);
            }
            //else
                //Debug.Log("empty packet data?");

            if (numRead < 0)
            {
                Debug.LogError("num read is < 0");
                return;
            }

            //Debug.Log("numRead = " + numRead);
            decoderState.WasPrevPacketMarkedLast = isLast;
            decoderState.LastReceivedSequence = sequence;
            if (!isLast)
                decoderState.NextSequenceToDecode = sequence + numRead / ((_outputSampleRate / 100) * _outputChannelCount);
            else
            {
                Debug.Log("Resetting #" + playerID + " decoder");
                decoderState.NextSequenceToDecode = 0;
                // Re-evaluate whether we need to fill up a buffer of audio before playing
                //lock (_bufferLock)
                //{
                    //HasFilledInitialBuffer = (_encodedBuffer.Count + 1 >= InitialSampleBuffer);
                //}
                decoderState.Decoder.ResetState();
            }

            //Debug.Log("Recv: " + sequence + " next: " + decoderState.NextSequenceToDecode);
        }

        ~AudioDecodeThread()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposing)
                return;
            _isDisposing = true;
            _waitHandle.Set();
            _decodeThread.Join();
        }

        private enum MessageType
        {
            /// <summary>
            /// Signal that this is a request to
            /// decode some audio
            /// </summary>
            DecompressData,
            /// <summary>
            /// Signal that we need a new decoder
            /// for the given session
            /// </summary>
            AllocDecoderState,
            /// <summary>
            /// Signal that a certain decoder
            /// is not needed at the moment,
            /// and can be pooled/freed
            /// </summary>
            FreeDecoder
        }
        private struct MessageData
        {
            public MessageType TypeOfMessage;
            public ushort PlayerID;

            // Used only for CompressedData message
            public EncodedAudioArray EncodedAudio;
            public long Sequence;
            public bool IsLast;
        }
        private class DecoderState
        {
            // May be null
            public OpusDecoder Decoder;
            public long NextSequenceToDecode;
            public long LastReceivedSequence;
            public bool WasPrevPacketMarkedLast;
        }
    }
}
