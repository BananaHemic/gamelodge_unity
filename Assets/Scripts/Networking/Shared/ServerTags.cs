using System;
using System.Collections.Generic;

/// <summary>
///     The tags for messages between the server and the client.
/// </summary>
static class ServerTags
{
    // NOTE: please update this everytime you
    // add a tag. This is so that we can quickly
    // see what number to give new tags
    private const byte CurrentLargestTag = 86;

    /// <summary>
    /// Sent by the server for when there's a new client
    /// in the room
    /// Server:
    ///     DRUser
    /// Client: (only for recordings)
    ///     DRUser
    /// </summary>
    public const byte SpawnPlayer = 0;
    /// <summary>
    /// Sent by the server for when a client has left
    /// the room
    /// Server:
    ///     UserID (ushort)
    /// Client: (only for recordings)
    ///     UserID (ushort)
    /// </summary>
    public const byte DespawnPlayer = 1;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    /// Server:
    ///     UserID
    ///     Position
    ///     Rotation
    /// </summary>
    public const byte PlayerMovement_Build = 2;
    /// <summary>
    /// Compressed user audio data. Currently, this
    /// must be sent as it's own standalone packet
    /// Client:
    ///     audio type (byte, leftmost bit sets isLast)
    ///     sequence number (uint)
    ///     audio data len (byte)
    ///     opus audio data
    ///     position data
    /// Server:
    ///     userID of talker
    ///     audio type (byte, leftmost bit sets isLast)
    ///     sequence number (uint)
    ///     audio data len (byte)
    ///     opus audio data
    ///     position data
    /// </summary>
    public const byte VoiceData = 3;
    /// <summary>
    /// Request to add an object
    /// Client:
    ///     DRObject (using temporary ID)
    /// Server:
    ///     DRObject
    /// </summary>
    public const byte AddObject = 4;
    public const byte AddObject_Response = 5;
    /// <summary>
    /// Request to add an object
    /// Client:
    ///     ObjectID
    /// Server:
    ///     ObjectID
    /// </summary>
    public const byte RemoveObject = 6;
    public const byte TransformObject_Pos = 7;
    public const byte TransformObject_Rot = 8;
    public const byte TransformObject_Scale = 9;
    public const byte TransformObject_PosRot = 10;
    /// <summary>
    /// When sent from the client, it represents 
    /// a ping, when sent from the server, it represents
    /// a pong
    /// </summary>
    public const byte PingPong = 11;
    /// <summary>
    /// When sent from the client, it is a request to grab
    /// an object. When sent from server, represents an
    /// update on who is grabbing it. This will also set
    /// ownership on the object
    /// Client:
    ///     Object to grab
    ///     GrabBodyPart the part of the body that is grabbing this object (byte)
    ///     RelativePos the position relative to what's grabbing it (Vec3)
    ///     RelativeRot the rotation relative to what's grabbing it (Quat)
    /// Server:
    ///     User ID grabbing
    ///     Object that they're now grabbing
    ///     GrabBodyPart the part of the body that is grabbing this object (byte)
    ///     RelativePos the position relative to what's grabbing it (Vec3)
    ///     RelativeRot the rotation relative to what's grabbing it (Quat)
    /// </summary>
    public const byte GrabObject = 12;
    /// <summary>
    /// When sent from the client, it is a request to release
    /// an object. When sent from server, represents an
    /// update on who is grabbing it
    /// Client:
    ///     ObjectID of the object released
    ///     ServerTime when release occurred
    /// Server:
    ///     UserID who released it
    ///     ObjectID of the object released
    ///     ServerTime when release occurred
    /// </summary>
    public const byte ReleaseGrabObject = 13;
    /// <summary>
    /// When sent from the client, it is a request to take ownership
    /// of an object. The server will drop this if someone else is grabbing the object
    /// Client:
    ///     Object ID to take
    ///     Server Time when ownership began
    /// Server:
    ///     OwnerID who just took ownership
    ///     Object ID taken
    ///     Server Time when ownership began
    /// </summary>
    public const byte OwnershipChange = 14;
    /// <summary>
    /// A request to add a SerializedBehavior
    /// Client:
    ///     Object to add to
    ///     The behavior serialized
    /// Server:
    ///     Object to add to
    ///     The behavior serialized
    /// </summary>
    public const byte AddBehavior = 15;
    /// <summary>
    /// A request to remove a SerializedBehavior
    /// Client:
    ///     ObjectID to add to
    ///     Is the behavior a network/miniscript script (bool)
    ///     The behavior ID
    /// Server:
    ///     ObjectID to add to
    ///     Is the behavior a network/miniscript script (bool)
    ///     The behavior ID
    /// </summary>
    public const byte RemoveBehavior = 16;
    /// <summary>
    /// A request to change a parameter for a SerializedBehavior
    /// Client:
    ///     Object to add to
    ///     Is the behavior a network/miniscript script (bool)
    ///     The behavior ID
    ///     Key of the param we want to change (var int)
    ///     Length of the serialized param data (var int)
    ///     The serialized param data (bytes)
    /// Server: (echos to client)
    ///     Object to add to
    ///     Is the behavior a network/miniscript script (bool)
    ///     The behavior ID
    ///     The number of keys updated (var int)
    ///     Key of the param we want to change (var int)
    ///     Length of the serialized param data (var int)
    ///     The serialized param data (bytes)
    /// </summary>
    public const byte UpdateBehavior = 17;
    /// <summary>
    /// A request from client->server to save the current game state
    /// Client:
    ///     SaveGameRequest  (the game metadata we want saved)
    /// </summary>
    public const byte SaveGame = 18;
    /// <summary>
    /// A request to change the name of an object
    /// Client:
    ///     ObjectID
    ///     byte how long the string is
    ///     char[] characters
    /// Server:
    ///     UserID
    ///     ObjectID
    ///     byte how long the string is
    ///     char[] ascii characters
    /// </summary>
    public const byte SetObjectName = 19;
    /// <summary>
    /// Request to add a material
    /// Client:
    ///     DRMaterial (using temporary ID)
    /// Server:
    ///     DRMaterial
    /// </summary>
    public const byte AddMaterial = 20;
    /// <summary>
    /// Notification from the server to the client that
    /// the material has been added
    /// Server:
    ///     ushort oldID
    ///     ushort newID
    /// </summary>
    public const byte AddMaterial_Response_Success = 21;
    /// <summary>
    /// Notification from the server to the client that
    /// the material has already exists
    /// Server:
    ///     ushort oldID
    ///     ushort ID of the material with the same characteristics.
    ///         Client should dispose the previously made DRMaterial,
    ///         and replace it with the one with this ID
    /// </summary>
    public const byte AddMaterial_Response_Fail_Redundant = 22;
    /// <summary>
    /// A change for the color of a material
    /// Client:
    ///     ushort materialID
    ///     int (encoded) propertyIndex
    ///     3 bytes for r/g/b components
    /// Server:
    ///     ushort userID who initiated the change
    ///     ushort materialID
    ///     int propertyIndex TODO this could probably just be a byte
    ///     3 bytes for r/g/b components
    /// </summary>
    public const byte MaterialColorChange = 23;
    /// <summary>
    /// A change for the color(s) of a material
    /// Client:
    ///     ushort materialID
    ///     int (encoded) how many properties changed
    ///     int (encoded) propertyIndex
    ///     3 bytes for r/g/b components
    /// Server:
    ///     ushort userID who initiated the change
    ///     ushort materialID
    ///     int (encoded) how many properties changed
    ///     int (encoded) propertyIndex
    ///     3 bytes for r/g/b components
    /// </summary>
    public const byte MaterialColorChangeMultiple = 24;
    /// <summary>
    /// A request to create a User Script
    /// Client:
    ///     The user script serialized
    /// Server:
    ///     The user script serialized
    /// </summary>
    public const byte CreateUserScript = 25;
    /// <summary>
    /// Notification from the server to the client that
    /// the user script has been created
    /// Server:
    ///     ushort oldID
    ///     ushort newID
    /// </summary>
    public const byte CreateUserScript_Response_Success = 26;
    /// <summary>
    /// Notification from the server to the client that
    /// the UserScript in a certain bundle has already exists
    /// in the scene
    /// Server:
    ///     ushort oldID
    ///     ushort ID of the UserScript with the same characteristics.
    ///         Client should dispose the previously made DRUserScript,
    ///         and replace it with the one with this ID
    /// </summary>
    public const byte CreateUserScript_Response_Fail_Redundant = 27;
    /// <summary>
    /// A request to update an existing User Script
    //TODO we need to echo this back to the sender in some way.
    // Otherwise, two clients who update the script at the same
    // time may see the other's update but not their own
    /// Client:
    ///     The user script serialized
    /// Server:
    ///     The user script serialized
    /// </summary>
    public const byte UpdateUserScript = 28;
    /// <summary>
    /// Notification from the server to the client that
    /// the user script has been updated
    /// Server:
    ///     ushort scriptID
    /// </summary>
    public const byte UpdateUserScript_Response_Success = 29;
    /// <summary>
    /// Server-sent messages that contains all info needed
    /// to bring a client up to date with an existing game
    /// (Other than players)
    /// Server:
    ///     GameState
    // TODO remove for joinGame
    /// </summary>
    public const byte GameState = 30;
    /// <summary>
    /// When called from user->server represents
    /// a request for SpawnInfo. When called from
    /// server->user, it means that the server is
    /// asking the host for how to spawn a player
    /// Server:
    ///     The ID of the player who needs spawn info (ushort)
    /// Client:
    ///     No data.
    /// </summary>
    public const byte SpawnInfoRequest = 31;
    /// <summary>
    /// Info about where to spawn/which object to
    /// take possession of. When received
    /// on the client, it's a command to spawn at
    /// a certain location. When received on the
    /// server, it's the info for a client,
    /// which will need to be relayed to everyone
    /// Server:
    ///     who the spawn is for (ushort)
    ///     ObjectID of the SceneObject that is now being possessed
    /// Client:
    ///     who the spawn is for (ushort)
    ///     ObjectID of the SceneObject that is now being possessed
    /// </summary>
    public const byte PossessObj = 78;
    /// <summary>
    /// Notification that the user is no longer
    /// possessing some object. The server may
    /// then delete the object, depending on some
    /// settings.
    /// Client:
    /// Server:
    ///     UserID that is no longer possessing anything
    /// </summary>
    public const byte DepossessObj = 32;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    ///     Input (vec2)
    ///     IsSprintDown (bool)
    /// Server:
    ///     UserID
    ///     Position
    ///     Rotation
    ///     Input (vec2)
    ///     IsSprintDown (bool)
    /// </summary>
    public const byte PlayerMovement_Play_Grounded = 33;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    ///     Object that user is on (ushort)
    ///     Input (vec2)
    ///     IsSprintDown (bool)
    /// Server:
    ///     UserID
    ///     Position
    ///     Rotation
    ///     Object that user is on (ushort)
    ///     Input (vec2)
    ///     IsSprintDown (bool)
    /// </summary>
    public const byte PlayerMovement_Play_Grounded_OnObject = 82;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    ///     Velocity
    ///     AngularVelocity
    /// Server:
    ///     Object ID
    ///     Position
    ///     Rotation
    ///     Velocity
    ///     AngularVelocity
    /// </summary>
    public const byte TransformObject_PosRotVelAngVel = 34;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    /// Server:
    ///     Object ID
    ///     Position
    ///     Rotation
    /// </summary>
    public const byte TransformObject_PosRot_Rest = 35;
    /// <summary>
    /// Client:
    ///     Position
    ///     Rotation
    ///     Input (vec2)
    ///     BaseVelocity (vec3)
    /// Server:
    ///     UserID
    ///     Position
    ///     Rotation
    ///     Input (vec2)
    ///     BaseVelocity (vec3)
    /// </summary>
    public const byte PlayerMovement_Play_NotGrounded = 36;
    /// <summary>
    /// Client:
    ///     DRUserPose
    /// Server:
    ///     User ID
    ///     DRUserPose
    /// </summary>
    public const byte UserPose_Full = 37;
    /// <summary>
    /// Client:
    ///     DRUserPose (only sending data for the head)
    /// Server:
    ///     User ID
    ///     DRUserPose
    /// </summary>
    public const byte UserPose_Single = 41;
    /// <summary>
    /// Client:
    ///     DRUserPose (head + 2 hands)
    /// Server:
    ///     User ID
    ///     DRUserPose
    /// </summary>
    public const byte UserPose_ThreePoints = 42;
    /// <summary>
    /// Client:
    ///     empty, means that the client wants to know
    ///         what rooms are available. TODO pagination / sorting
    /// Server:
    ///     DRGameSummary[]
    /// </summary>
    public const byte ListGames = 38;
    /// <summary>
    /// Sent by the client when DR has said
    /// that they're connected
    /// Client:
    ///     Username (long unicode string)
    /// </summary>
    public const byte FinishServerConnection = 39;
    /// <summary>
    /// A request from client->server to load a new game state
    /// Client:
    ///    S3_ID (string) the ID for this game within S3
    /// </summary>
    public const byte LoadGame = 40;

    public const byte RPC_All_NonBuffered = 43;
    public const byte RPC_Others_NonBuffered = 44;
    public const byte RPC_Host_NonBuffered = 45;
    public const byte RPC_TargetUser_NonBuffered = 46;

    public const byte RPC_All_Buffered = 47;
    public const byte RPC_Others_Buffered = 48;
    public const byte RPC_Host_Buffered = 49;

    public const byte RPC_All_NonBuffered_Value = 50;
    public const byte RPC_Others_NonBuffered_Value = 51;
    public const byte RPC_Host_NonBuffered_Value = 52;
    public const byte RPC_TargetUser_NonBuffered_Value = 53;

    public const byte RPC_All_Buffered_Value = 54;
    public const byte RPC_Others_Buffered_Value = 55;
    public const byte RPC_Host_Buffered_Value = 56;

    /// <summary>
    /// The game will now play a recorded game
    /// Client:
    ///     DRGameState
    ///     DRUserList     the users who are in the recording
    /// Server:
    ///     DRGameState
    ///     DRUserList     the users who are in the recording
    ///     Client who is playing the recording
    /// </summary>
    public const byte InitiateRecordingPlayback = 57;
    /// <summary>
    /// Echo from client->server that the client has received
    /// and integrated the provided game state. The server needs
    /// to know this so that it can tell if the client's messages
    /// are still relevant for the new game state
    //TODO use
    /// </summary>
    public const byte HasInitiatedRecordingPlaybackResponse = 58;
    /// <summary>
    /// Message from server->client telling the client that the
    /// server failed to start playing back the recording. It
    /// could be that the playback state had an ID that is in
    /// use
    /// </summary>
    public const byte InitiateRecordingPlayback_Fail = 59;
    /// <summary>
    /// Notification or request to stop playback of a recorded game
    /// Client:
    ///     WillRestart (bool) whether were about to play a new recording immediately
    /// </summary>
    public const byte EndRecordingPlayback = 60;
    /// <summary>
    /// Used by a client that's playing back a recording
    /// to re-send out the OwnershipChange message it
    /// received. Same as normal OwnershipChange from 
    /// Server -> Client
    /// </summary>
    public const byte OwnershipChange_Recorded = 61;
    /// <summary>
    /// For the most part, all recorded messages are the
    /// same, EXCEPT they begin with the ushort for the
    /// userID that sent it out. This is processed only on
    /// the server.
    /// </summary>
    public const byte ReleaseGrabObject_Recorded = 62;
    public const byte GrabObject_Recorded = 63;
    public const byte PlayerMovement_Build_Recorded = 64;
    public const byte VoiceData_Recorded = 65;
    public const byte MaterialColorChange_Recorded = 66;
    public const byte MaterialColorChangeMultiple_Recorded = 67;
    public const byte SetObjectName_Recorded = 68;
    public const byte PlayerMovement_Play_Grounded_Recorded = 69;
    public const byte PlayerMovement_Play_Grounded_OnObject_Recorded = 83;
    public const byte PlayerMovement_Play_NotGrounded_Recorded = 70;
    public const byte UserPose_Full_Recorded = 71;
    public const byte UserPose_Single_Recorded = 72;
    public const byte UserPose_ThreePoints_Recorded = 73;

    /// <summary>
    /// Used to either play or pause the program
    /// Client:
    ///     isPlaying (bool)
    /// Server:
    ///     isPlaying (bool)
    ///     userID who set playing/paused
    /// </summary>
    public const byte PlayPause = 74;
    public const byte PlayPause_Recorded = 75;
    /// <summary>
    /// Client:
    ///     Object ID
    ///     Grabbing Body Part (byte)
    ///     Position
    ///     Rotation
    ///     Velocity
    ///     AngularVelocity
    /// Server:
    ///     ClientID who's grabbing
    ///     Object ID
    ///     Grabbing Body Part (byte)
    ///     Position
    ///     Rotation
    ///     Velocity
    ///     AngularVelocity
    /// </summary>
    public const byte TransformObject_GrabPhysicsPosRotVelAngVel = 76;
    // Essentially just the server version of the above
    public const byte TransformObject_GrabPhysicsPosRotVelAngVel_Recorded = 77;
    /// <summary>
    /// The main ragdoll state, with full info for only the important joints
    /// Send this infrequently, as it is very big (287 bytes)
    /// Client:
    ///     ObjectID
    ///     -- for each joint (head, torso, torso, upper arm l/r, upper leg l/r)
    ///         position
    ///         rotation (compressed)
    ///         velocity
    ///         angular velocity
    /// </summary>
    public const byte Ragdoll_Main = 79;
    /// <summary>
    /// When the board width/height changes
    /// </summary>
    public const byte BoardSizeChange = 80;
    /// <summary>
    /// When the visibility of the board changes
    /// </summary>
    public const byte BoardVisiblityChange = 81;
    /// <summary>
    /// Object enabled change
    /// Client:
    ///     ObjectID
    ///     IsEnabled (bool)
    /// Server:
    ///     ObjectID
    ///     IsEnabled (bool)
    /// </summary>
    public const byte ObjectEnableChange = 84;
    /// <summary>
    /// User changed a blend shape for themself
    /// Client:
    ///     Blend shape Index (var int)
    ///     Blend shape value (float)
    /// Server:
    ///     UserID (ushort)
    ///     Blend shape Index (var int)
    ///     Blend shape value (float)
    /// </summary>
    public const byte UserBlendChange = 85;
    // Same as above, but client sends UserID
    public const byte UserBlendChange_Recorded = 86;

    public static byte Tag2Recorded(byte tag)
    {
        switch (tag)
        {
            case OwnershipChange:
                return OwnershipChange_Recorded;
            case ReleaseGrabObject:
                return ReleaseGrabObject_Recorded;
            case GrabObject:
                return GrabObject_Recorded;
            case PlayerMovement_Build:
                return PlayerMovement_Build_Recorded;
            case VoiceData:
                return VoiceData_Recorded;
            case MaterialColorChange:
                return MaterialColorChange_Recorded;
            case MaterialColorChangeMultiple:
                return MaterialColorChangeMultiple_Recorded;
            case SetObjectName:
                return SetObjectName_Recorded;
            case PlayerMovement_Play_Grounded:
                return PlayerMovement_Play_Grounded_Recorded;
            case PlayerMovement_Play_NotGrounded:
                return PlayerMovement_Play_NotGrounded_Recorded;
            case PlayerMovement_Play_Grounded_OnObject:
                return PlayerMovement_Play_Grounded_OnObject_Recorded;
            case UserPose_Full:
                return UserPose_Full_Recorded;
            case UserPose_Single:
                return UserPose_Single_Recorded;
            case UserPose_ThreePoints:
                return UserPose_ThreePoints_Recorded;
            case PlayPause:
                return PlayPause_Recorded;
            case TransformObject_GrabPhysicsPosRotVelAngVel:
                return TransformObject_GrabPhysicsPosRotVelAngVel_Recorded;
            case UserBlendChange:
                return UserBlendChange_Recorded;
            default:
                DRCompat.LogError("No recorded version of tag #" + tag);
                return byte.MaxValue;
        }

    }
}