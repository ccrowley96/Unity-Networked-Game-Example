using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


/// <summary>
/// Shared class for defining, marshalling and unmarshalling messages. Network messages are byte arrays. Network
/// messages convey information such as: sever informs client of its player id; server informs client which avatar
/// is to be used by that client; server informs client (or client informs server) of position/rotate/animation state
/// of an avatar.
/// </summary>
class Messages{

	/// <summary>
	/// Messages have id's representing the message type. These are a single byte in length,
	/// which gives us a maximum of 256 message types.
	/// </summary>
	public static readonly byte setPlayerId = (byte)1;
	public static readonly byte useAvatar = (byte)2;
	public static readonly byte setAvatarPositionRotation = (byte)3;

	// collect all message types, for use in debugging
	static byte[] allMessageTypes = {setPlayerId, useAvatar, setAvatarPositionRotation};

	public static int maxMessageSize = 128; // maximum size of a message in bytes

	// id's for the two supported avatars
	public static readonly byte flora = (byte)1;
	public static readonly byte tommy = (byte)2;

	// The Unity networking API can return a NetworkError. This turns the values of
	// this enumerated type into a readable string.
	public static string NetworkErrorToString(byte error) {
		switch(error) {
		case (byte)NetworkError.BadMessage:
			return "BadMessage";
		case (byte)NetworkError.CRCMismatch:
			return "CRCMismatch";
		case (byte)NetworkError.DNSFailure:
			return "DNSFailure";
		case (byte)NetworkError.MessageToLong:
			return "MessageToLong (sic)";
		case (byte)NetworkError.NoResources:
			return "NoResources";
		case (byte)NetworkError.Timeout:
			return "TimeOut";
		case (byte)NetworkError.VersionMismatch:
			return "VersionMismatch";
		case (byte)NetworkError.WrongChannel:
			return "WrongChannel";
		case (byte)NetworkError.WrongConnection:
			return "WrongConnection";
		case (byte)NetworkError.WrongHost:
			return "WrongHost";
		case (byte)NetworkError.WrongOperation:
			return "WrongOperation";
		case (byte)NetworkError.Ok:
			return "Ok";
		}
		return "Unknown network errror";
	}

	/// <summary>
	/// Writes out a network error to the log file.
	/// </summary>
	/// <param name="error">a network error</param>
	public static void LogNetworkError(byte error) {
		if(error != (byte)NetworkError.Ok) {
			Debug.LogFormat("Got network error: {0}", NetworkErrorToString(error));
		}
	}

	/// <summary>
	/// Write out information about the connection with the given host id and connection id.
	/// </summary>
	/// <param name="hostId">Host identifier</param>
	/// <param name="connectionId">Connection identifier</param>
	public static void LogConnectionInfo(int hostId, int connectionId) {
		string address;
		int port;
		UnityEngine.Networking.Types.NetworkID network;
		UnityEngine.Networking.Types.NodeID dstNode;
		byte error;

		NetworkTransport.GetConnectionInfo(
			hostId, connectionId, out address, out port, out network,
			out dstNode, out error);

		Debug.LogFormat("Connection info: from IP {0}:{1}", address, port);
	}


	/// <summary>
	/// Adds the byte representation of the given int into the given byte array, starting at
	/// the given count. The count is updated. 
	/// </summary>
	/// <param name="m">M.</param>
	/// <param name="count">Count.</param>
	static void AddIntToByteArray(Int16 n, ref byte[] m, ref int count) {
		byte[] b = BitConverter.GetBytes(n);
		Debug.Assert(b.Length == 2);  // should be 4 bytes for an int32
		for(int i=0; i<b.Length; i++) {
			m[count++] = b[i];
		}
	}
		
	/// <summary>
	/// Creates a network message encoding the given (x,z) position and rotation. The network
	/// message consists of a sequence of bytes. The method UnmarshallPositionRotationMessage
	/// is used to convert the network message back into position and rotation messages.
	/// 
	/// Note there is a small loss of precision as positions are converted to integers. Also note
	/// we are assuming rotation of avatar is around y-axis only and motion is in x-z plane.
	/// The integer values encoded in the message represent the true float values * 100, rounded
	/// to the nearest integer. When unmarshalled from the message, these must be divided by 100.
	/// </summary>
	/// <returns>The position and rotation as a byte array</returns>
	/// 
	/// <param name="avatarId">Avatar identifier (flora or tommy)</param>
	/// <param name="x">The avatar's x-position</param>
	/// <param name="z">The avatar's y-position</param>
	/// <param name="r">The avatar's rotation (in degrees)</param>
	/// <param name="movementState">Movement state (idle, walking, turning)</param>
	public static byte[] CreateSetAvatarPositionRotationMessage(
		byte avatarId, float x, float z, float r, byte movementState) {
		// Format is:
		//	 - byte 0: message type
		//   - byte 1: avatar id
		//	 - byte 2: movement state
		//   - bytes 3-4: x value
		//   - bytes 5-6: z value
		//   - bytes 7-8: rotation (degrees around y-axis)

		Debug.Assert(avatarId == flora || avatarId == tommy);

		// Look up methods in the Convert class - e.g., ConvertToInt16

		byte[] m = new byte[9];

		// ... Control info
		m[0] = setAvatarPositionRotation;
		m[1] = avatarId;
		m[2] = movementState;

		// x position 
		short int_x = (short) Math.Round(x * 100);
		byte [] x_bytes = BitConverter.GetBytes(int_x);
		m[3] = x_bytes[0];
		m[4] = x_bytes[1];

		// z position 
		short int_z = (short) Math.Round(z * 100);
		byte [] z_bytes = BitConverter.GetBytes(int_z);
		m[5] = z_bytes[0];
		m[6] = z_bytes[1];

		// r rotation
		short int_r = (short) Math.Round(r * 100);
		byte [] r_bytes = BitConverter.GetBytes(int_r);
		m[7] = r_bytes[0];
		m[8] = r_bytes[1];
	
		Debug.Assert(GetMessageType(m) == setAvatarPositionRotation);

		return m;
	}

	/// <summary>
	/// Creates message sent from server to client to inform it which avatar this client
	/// should be using (tommy or flora).
	/// </summary>
	/// <returns>The use avatar message.</returns>
	/// <param name="avatarId">Avatar identifier.</param>
	public static byte[] CreateUseAvatarMessage(byte avatarId) {
		Debug.Assert(avatarId == tommy || avatarId == flora);

		byte[] m = new byte[2];
		m[0] = useAvatar;
		m[1] = avatarId;
		return m;
	}

	/// <summary>
	/// Extracts avatar id from a network message.
	/// </summary>
	/// <returns>The avatar identifier from message.</returns>
	/// <param name="message">message</param>
	public static byte GetAvatarIdFromMessage(byte[] message) {
		Debug.Assert(message.Length >= 2);
		Debug.Assert(GetMessageType(message) == useAvatar
			|| GetMessageType(message) == setAvatarPositionRotation);
		Debug.Assert(message[1] == flora || message[1] == tommy);

		return message[1];
	}

	/// <summary>
	/// Creates a message sent from the server to the client informing the client of its playerId. Player id's must
	/// be between 0..255.
	/// </summary>
	/// <returns>The set player identifier message.</returns>
	/// <param name="playerId">Player identifier.</param>
	public static byte[] CreateSetPlayerIdMessage(int playerId) {
		Debug.Assert(playerId >= 0 && playerId < 256);

		byte[] m = new byte[2];
		m[0] = setPlayerId;
		m[1] = Convert.ToByte(playerId);

		return m;
	}

	/// <summary>
	/// Extracts player id from a network message
	/// </summary>
	/// <returns>The player identifier from message.</returns>
	/// <param name="message">Message.</param>
	public static int GetPlayerIdFromMessage(byte[] message) {
		Debug.Assert(message.Length >= 2);
		Debug.Assert(GetMessageType(message) == setPlayerId);
		return Convert.ToInt32(message[1]);
	}

	/// <summary>
	/// Extracts a float value from network message. The byte string
	/// representing the float value starts at position startPos in the
	/// given byte array.
	/// </summary>
	/// <returns>The float.</returns>
	/// <param name="message">Message.</param>
	/// <param name="startPos">Start position.</param>
	static float ExtractFloat(byte[] message, int startPos) {
		Debug.Assert(startPos >= 0 && startPos+2 < message.Length);
		Int16 n = BitConverter.ToInt16(message, startPos);
		return n;
	}

	/// <summary>
	/// Given a SetPositionRotation message, extracts the new position and rotation.
	/// </summary>
	/// <returns>The position rotation message.</returns>
	/// <param name="message">Message.</param>
	/// <param name="avatarId">The avatar being moved</param> 
	/// <param name="x">The x coordinate.</param>
	/// <param name="z">The z coordinate.</param>
	/// <param name="r">The rotation component.</param>
	public static void UnmarshallPositionRotationMessage(
			byte[] message, out byte avatarId, out float x, out float z, out float r, out byte m) {
		Debug.Assert(message.Length >= 14);
		Debug.Assert(GetMessageType(message) == setAvatarPositionRotation);
		Debug.Assert(message[1] == flora || message[1] == tommy);

		avatarId = message[1];

		m = message[2];
		Debug.Assert(AnimationInfo.IsLegalMovementState(m));

		// Format is:
		//	 - byte 0: message type
		//   - byte 1: avatar id
		//	 - byte 2: movement state
		//   - bytes 3-4: x value
		//   - bytes 5-6: z value
		//   - bytes 7-8: rotation (degrees around y-axis)
		// ...

		// X position
		byte [] x_bytes = new byte[] {message[3], message[4]};
		short x_val = BitConverter.ToInt16(x_bytes, 0);
		x = (float) x_val / 100f;

		// Z position
		byte [] z_bytes = new byte[] {message[5], message[6]};
		short z_val = BitConverter.ToInt16(z_bytes, 0);
		z = (float) z_val / 100f;

		// R position
		byte [] r_bytes = new byte[] {message[5], message[6]};
		short r_val = BitConverter.ToInt16(r_bytes, 0);
		r = (float) r_val / 100f;
	}

	/// <summary>
	/// Returns the type of the given message.
	/// </summary>
	/// <returns>The message type</returns>
	/// <param name="message">A network message</param>
	public static byte GetMessageType(byte[] message) {
		Debug.Assert(message.Length >= 1);
		Debug.Assert(Array.IndexOf(allMessageTypes,message[0]) >= 0);
		return message[0];
	}
}

/**
 * Provides server-side network services.
 * Receives connections from clients.
 * Provides message marshalling/unmarshalling services.
 * 
 */
public class ServerNetworkScript : MonoBehaviour {

	const int serverPort = 51343;  // port used by server
	const int maxConnections = 5;

	// To send messages, we will need a host id (this client), a connection id (reference to server),
	// and a channel id (socket connection itself.) These are all set when this client is initialized.
	int hostId;
	int connectionId;

	/// <summary>
	/// We have two types of channel - used for control and movement info
	/// </summary>
	int controlChannelId;  // reliable channel for control messages
	int dataChannelId;  // unreliable channel for movement info

	/// <summary>
	/// Stores information about the clients. This maps the connection id for the client onto
	/// the avatar that the client was assigned.
	/// </summary>
	Dictionary<int,byte> connections = new Dictionary<int,byte>();

	/// <summary>
	/// When a client indicates that it would like to connect, we log the client in our connections
	/// list. To initialize the client, we send the client its player id (setPlayerId message),
	/// and we tell the client which avatar to use (setAvatar message). Avatars are assigned
	/// to clients based on the order in which they connect - the first client is assigned the Flora
	/// avatar; the second is assigned the Tommy avatar.
	/// </summary>
	/// <param name="recHostId">Rec host identifier.</param>
	/// <param name="connectionId">Connection identifier.</param>
	/// <param name="channelId">Channel identifier.</param>
	void AddNewClient(int recHostId, int connectionId, int channelId) {
		Messages.LogConnectionInfo(hostId, connectionId);

		Debug.Assert(connections.Count < 2);
		Debug.Assert(!connections.ContainsKey(connectionId));

		// The first connection gets flora; the second gets tommy
		byte avatarForThisConnection = connections.Count == 0 ? Messages.flora : Messages.tommy;
		Debug.LogFormat("Setting avatar for player {0} to {1}", connectionId, avatarForThisConnection);

		// ...
	}

	/// <summary>
	/// When a movement message is received from one client, we send the same movement message to
	/// the other client.
	/// </summary>
	/// <param name="senderConnectionId">client sending the movement message</param>
	/// <param name="message">movement message</param>
	/// <param name="messageLength">number of bytes in movement message</param>
	void ForwardMovementMessage(int senderConnectionId, byte[] message, int messageLength) {
		// send to all clients other than originator
		
		// ...
	}

	/// <summary>
	/// This is the workhorse method on the server. It reads in messages from the clients.
	/// These are used to establish connection to the server, to disconnect from the
	/// server, or to transmit data to the server. (Data is in the form of movement network
	/// events.)
	/// </summary>
	void ReceiveMessagesFromClients() {
		byte[] recBuffer = new byte[1024]; 
		int bufferSize = 1024;
		int dataSize;
		byte error;
		int connectionId;
		int channelId;
		int recHostId;

		bool messagesAvailable = true;

		while(messagesAvailable) {
			NetworkEventType recData = NetworkTransport.Receive(
				out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
			Messages.LogNetworkError(error);

			switch (recData) {
			case NetworkEventType.Nothing:
				messagesAvailable = false;
				break;
			case NetworkEventType.ConnectEvent:
				AddNewClient(recHostId, connectionId, channelId);
				break;
			case NetworkEventType.DataEvent:
				Debug.Log(string.Format("Message received from host {0}, connection {1}, channel {2}",
					recHostId, connectionId, channelId));

				Debug.Assert(Messages.GetMessageType(recBuffer) == Messages.setAvatarPositionRotation);
				ForwardMovementMessage(connectionId, recBuffer, dataSize);
				break;
			case NetworkEventType.DisconnectEvent:
				Debug.Log(string.Format("Disconnection received from host {0}, connection {1}, channel {2}",
					recHostId, connectionId, channelId));
				//TODO: Add disconnection handling code
				break;
			}
		}
	}

	/// <summary>
	/// Initializes the network. Creates a socket (hostId) that the server uses to communicate with the clients.
	/// Creates the two channels used for communication (reliable channel for control information, unreliable
	/// channel for avatar movement.) The network must be initialized before messages can be sent to/received from
	/// the clients.
	/// </summary>
	void InitNetwork() {
		// Establish connection to server and get client id.
		NetworkTransport.Init();

		// Set up channels for control messages (reliable) and movement messages (unreliable)
		ConnectionConfig config = new ConnectionConfig();
		controlChannelId  = config.AddChannel(QosType.Reliable);
		dataChannelId = config.AddChannel(QosType.Unreliable);

		// Create socket end-point
		HostTopology topology = new HostTopology(config, maxConnections);
		hostId = NetworkTransport.AddHost(topology, serverPort);

		Debug.Log(string.Format("Server created with hostId {0}", hostId));
	}

	void Start () {
        Application.runInBackground = true;

        InitNetwork();
	}

	// Update is called once per frame
	void Update () {
		ReceiveMessagesFromClients();
	}
}
