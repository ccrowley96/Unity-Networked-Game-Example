using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


/// <summary>
/// Provides client-side network services.
/// Automatically connects to the server and establishes a network connection.
/// </summary>
/// <author>Nick Graham</author>

public class ClientNetworkScript : MonoBehaviour {

	public int playerId; // this client's unique id, assigned by the server

	public float messageSendFrequency = 0.2f; // 200 ms

	const int _serverPort = 51343;  // port used by server

	const int _maxConnections = 5;

	string _serverHostIP = "127.0.0.1";	// use this IP address if the server is located on the same computer as the clients

	bool _isConnected = true;

	// To send messages, we will need a host id (this client), a connection id (reference to server),
	// and a channel id (socket connection itself.) These are all set when this client is initialized.
	int _hostId;
	int _connectionId;

	/// <summary>
	/// We have two types of channel - control and movement info
	/// </summary>
	int _controlChannelId;  // reliable channel for control messages
	int _dataChannelId;  // unreliable channel for movement info

	GameObject _localAvatar;
	GameObject _remoteAvatar;
	byte _localAvatarId;


	/// <summary>
	/// Initializes the network, creating a socket that will be used to communicate with the server,
	/// and two channels (reliable control channel, unreliable avatar movement channel.)
	/// </summary>
	void InitNetwork() {
		// Establish connection to server and get client id.
		NetworkTransport.Init();

		// Set up channels for control messages (reliable) and movement messages (unreliable)
		ConnectionConfig config = new ConnectionConfig();
		_controlChannelId  = config.AddChannel(QosType.Reliable);
		_dataChannelId = config.AddChannel(QosType.Unreliable);

		// Create socket end-point
		HostTopology topology = new HostTopology(config, _maxConnections);
		_hostId = NetworkTransport.AddHost(topology);

		// Establish connection to server
		byte error;
		_connectionId = NetworkTransport.Connect(_hostId, _serverHostIP, _serverPort, 0, out error);
		if(error != (byte)NetworkError.Ok) {
			Debug.LogFormat("Network error: {0}", Messages.NetworkErrorToString(error));
		}

		Debug.LogFormat("Establishing network connection with hostId {0}; connectionId {1}",
			_hostId, _connectionId);
	}

	/// <summary>
	/// One avatar is considered to be local (under control of the player), and the other is remote
	/// (under control of the other player.) A message from the server tells us which avatar to treat
	/// as remote and which as local. Each avatar has copies of both the local and remote avatar
	/// scripts; this method simply activates the appropriate script depending on its role in this client.
	/// </summary>
	/// <param name="localAvatarId">Local avatar identifier.</param>
	void InitializeAvatars(byte localAvatarId) {
		GameObject tommyAvatar = GameObject.Find("tommy");
		GameObject floraAvatar = GameObject.Find("flora");

		_localAvatarId = localAvatarId;

		if(localAvatarId == Messages.flora) {
			Debug.Log("Initializing Flora as local");
			_localAvatar = floraAvatar;
			_remoteAvatar = tommyAvatar;

		} else {
			Debug.Assert(localAvatarId == Messages.tommy);

			Debug.Log("Initializing Tommy as local");
			_localAvatar = tommyAvatar;
			_remoteAvatar = floraAvatar;
		}
		_localAvatar.GetComponent<InitializeAvatarScript>().InitializeAsPlayer();
		_remoteAvatar.GetComponent<InitializeAvatarScript>().InitializeAsRemote();
	}

	/// <summary>
	/// Following receipt of a position/rotation message from the server, update the position
	/// of the remote avatar. Unmarshall the message (to obtain the position, rotation and
	/// animation information) and then perform the necessary updates to the remote avatar.
	/// </summary>
	/// <param name="message">Message.</param>
	void UpdateRemoteAvatar(byte[] message) {
		byte avatarId;
		float x, z, r;
		byte m;

		// ... Attempt
		Messages.UnmarshallPositionRotationMessage(message, out avatarId, out x, out z, out r, out m);

		RemoteAvatarScript ras = _remoteAvatar.GetComponent<RemoteAvatarScript>();

		ras.targetPosition = new Vector3(x,_remoteAvatar.transform.position.y,z);
		ras.targetRotation = Quaternion.Euler(0f,r,0f);
		ras.targetMovementState = m;
	}

	/// <summary>
	/// Processes a message arriving from the server. There are three kinds of message:
	/// an initialization message to set this client's player id; an initialization message
	/// to inform this client which avatar to use; and a message updating the position
	/// of the remote avatar.
	/// </summary>
	/// <param name="message">Message.</param>
	/// <param name="messageLength">Message length.</param>
	void ProcessMessage(byte[] message, int messageLength) {
		byte messageType = Messages.GetMessageType(message);
		if(messageType == Messages.setPlayerId) {
			playerId = Messages.GetPlayerIdFromMessage(message);
			Debug.LogFormat("Got player id message: {0}", playerId);
		} else if(messageType == Messages.useAvatar) {
			byte avatarId = Messages.GetAvatarIdFromMessage(message);
			Debug.LogFormat("Received message: using avatar {0}", avatarId);
			InitializeAvatars(avatarId);
		} else if (messageType == Messages.setAvatarPositionRotation) {
			// Attempt
			UpdateRemoteAvatar(message);
		} else {
			Debug.AssertFormat(false, "Unknown message type {0}", messageType);
		}
	}

	/// <summary>
	/// Processes messages arriving from the server. This is the core routine for the client,
	/// that processes messages as they arrive.
	/// Performs initialization in response to server connection - waits for message from
	/// server to set the player id and to specify which avatar we are using.
	/// </summary>
	/// <returns>The server connection confirmation.</returns>
	void ProcessIncomingMessages() {

		byte[] recBuffer = new byte[Messages.maxMessageSize]; 
		int bufferSize = Messages.maxMessageSize;
		int dataSize;
		byte error;
		int connectionId;
		int channelId;
		int recHostId;
		bool messagesAvailable;


		if(_isConnected) {
			
			messagesAvailable = true;

			while(messagesAvailable) {
				NetworkEventType recData = NetworkTransport.Receive(
					out recHostId, out connectionId, out channelId,
					recBuffer, bufferSize, out dataSize, out error);
				Messages.LogNetworkError(error);

				switch (recData) {
				case NetworkEventType.Nothing:
					// We have processed the last pending message
					messagesAvailable = false;
					break;
				case NetworkEventType.ConnectEvent:
					Debug.Log(string.Format("Connection received from host {0}, connectionId {1}, channel {2}",
						recHostId, connectionId, channelId));
					Messages.LogConnectionInfo(_hostId, connectionId);
					break;
				case NetworkEventType.DataEvent:
					Debug.Log(string.Format("Message received from host {0}, connectionId {1}, channel {2}",
						recHostId, connectionId, channelId));
					ProcessMessage(recBuffer, dataSize);
					break;
				case NetworkEventType.DisconnectEvent:
					Debug.Log(string.Format("Disconnection received from host {0}, connectionId {1}, channel {2}",
						recHostId, connectionId, channelId));
					_isConnected = false;
					break;
				}
			}
		}
	}


	/// <summary>
	/// Sends the given message to the server. Messages are byte arrays. The channel id is used to
	/// specify whether this message should be sent reliably or unreliably.
	/// </summary>
	/// <param name="message">Message.</param>
	/// <param name="channelId">Channel identifier.</param>
	void SendMessageToServer(byte[] message, int channelId) {
		byte error;
		NetworkTransport.Send(_hostId, _connectionId, channelId, message, message.Length, out error);
		Messages.LogNetworkError(error);
	}

	/// <summary>
	/// This co-routine periodically sends the position/rotation/motion state of this avatar to the server.
	/// It grabs this information from the remote avatar, bundles it into a network message (byte array),
	/// and sends it to the server. The coroutine loops indefinitely.
	/// </summary>
	/// <returns>The local avatar positions.</returns>
	IEnumerator SendLocalAvatarPositions() {
		// Start sending messages when the remote avatar has been initialized. Stop
		// sending when the client is disconnected.
		while(true) {
			if(_isConnected && _localAvatar != null) {
				// ... Attempt
				byte[] message = Messages.CreateSetAvatarPositionRotationMessage(_localAvatarId, transform.position.x, transform.position.z, transform.rotation.y, _localAvatar.GetComponent<LocalAvatarScript>().movementState);
				SendMessageToServer(message, _dataChannelId);
			}
			yield return new WaitForSeconds(messageSendFrequency);
		}
	}

	/// <summary>
	/// Sets the prediction algorithm to one of three values:
	/// - none (no prediction)
	/// - dead reckoning
	/// - dead reckoning with smooth corrections.
	/// This is called from the algorithm-selector dropdown menu in the user interface.
	/// </summary>
	/// <param name="algorithmIndex">Algorithm index.</param>
	public void SetAlgorithm(int algorithmIndex) {
		Debug.LogFormat("Algorithm index: {0}", algorithmIndex);
		Debug.Assert(_remoteAvatar != null);
		_remoteAvatar.GetComponent<RemoteAvatarScript>().UpdateAlgorithm = (RemoteAvatarScript.Algorithm)algorithmIndex;
	}

	/// <summary>
	/// Sets the message update frequency. This is updated by the message frequency slider in the client UI.
	/// </summary>
	/// <param name="frequency">Value between 0 and 5, representing number of 200 ms chunks between messages (e.g., 5 = 1,000 ms)</param>
	public void SetFrequency(float frequency) {
		messageSendFrequency = frequency * 0.2f;
	}

	void Start () {
		Application.runInBackground = true;

		InitNetwork();
		StartCoroutine("SendLocalAvatarPositions");
	}

	void Update() {
		ProcessIncomingMessages();
	}
}
