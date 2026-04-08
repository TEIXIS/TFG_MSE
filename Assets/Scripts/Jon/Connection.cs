using System;
using System.Net.Sockets;
using UnityEngine;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections.Concurrent;
using System.Collections.Generic;

/*
 Script that communicates Unity apps using C# sockets.
 The client side waits until its LANDiscovery component finds any server app and connects to it.
 The server side just waits for clients to connect.

 Messages are sent in JSON format by serializing the UpdateMessage class above. Any other class can be sent with only a small
 modification to this script.

 The client side can send messages with the Send() method, that directly accepts a string. 
 By using JsonUtility.ToJson() this is easily done.

 The server side receives messages and stores them in the messageQueue, accessible from other scripts.
 */

public class Connection : MonoBehaviour
{
	// Has to match on both sides.
	public int serverPort = 5000;

	// Received messages are stored here, in text format. If using JSON, convert them back to a class before using them.
	public readonly ConcurrentQueue<string> messageQueue = new();

	public enum Side
	{
		Server,
		Client
	};

    public Side side;
	public bool debug;
    public LANDiscovery lanDiscovery;

    public NetworkStream stream;
	private byte[] buffer = new byte[1024];
	public bool connected = false;

	// Only for proper clean up
	private readonly ConcurrentQueue<Thread> connectionThreads = new();
	private volatile bool closeRequest = false;
	private readonly string closeString = "close";

	// If client
	TcpClient tcpClient;
	public DisplayServers displayServers;
	private List<Action> onDisconnectCallbacks = new();

	// If server
	private List<Action> onClientConnectCallbacks = new();
	private volatile bool runningServer = false;
	private TcpListener tcpListener;
	private Thread listener;

	public bool ConnectToServer(string ipAddress)
	{
		try
		{
			if (side != Side.Client)
				return false;

			tcpClient = new(ipAddress, serverPort);
			stream = tcpClient.GetStream();

			if (debug)
				Debug.Log("Connected to server");
			connected = true;

			Thread thread = new(() => Receive());
			thread.IsBackground = true;
			thread.Start();
			connectionThreads.Enqueue(thread);
			return true;
		}
		catch (Exception e)
		{
			displayServers.OnConnectionError();
			Debug.LogError("Error connecting to server: " + e.Message);
			return false;
		}
	}

	public void RegisterOnClientConnectCallback(Action callback) 
	{
		onClientConnectCallbacks.Add(callback);
	}

	public void RegisterOnDisconnectCallback(Action callback) 
	{
		onDisconnectCallbacks.Add(callback);
	}

	void ListenerFunc()
	{
		tcpListener = new TcpListener(IPAddress.Any, serverPort);
		tcpListener.Start();
		if (debug)
			Debug.Log("Started server.");

		while (runningServer)
		{
			if (tcpListener.Pending())
			{
				TcpClient tcpClient = tcpListener.AcceptTcpClient();
				if (debug)
					Debug.Log("Client connected");
				connected = true;

				stream = tcpClient.GetStream();
				Thread thread = new(() => Receive());
				thread.IsBackground = true;
				thread.Start();
				connectionThreads.Enqueue(thread);

				foreach (var callback in onClientConnectCallbacks)
					callback();
			}
			else
			{
				Thread.Sleep(50);
			}
		}
	}

	void StartServer()
	{
		if (runningServer)
			return;

		listener = new(ListenerFunc);
		listener.IsBackground = true;
		listener.Start();
		runningServer = true;
	}

	void CloseConnection()
	{
		if (side == Side.Client)
		{
			try
			{
				if (stream != null)
					stream.Close();

				if (tcpClient != null)
					tcpClient.Close();

				connected = false;
				
				while (!connectionThreads.IsEmpty)
				{
					connectionThreads.TryDequeue(out var thread);
					thread.Abort();
				}

				if (debug)
					Debug.Log("Connection closed");
			}
			catch (Exception e)
			{
				Debug.LogError("Error closing connection: " + e.Message);
			}
		}
		else if (side == Side.Server) 
		{
			while (!connectionThreads.IsEmpty)
			{
				connectionThreads.TryDequeue(out var thread);
				thread.Abort();
			}

			if (debug)
				Debug.Log("Closed connection with all clients.");
		}

		foreach (var callback in onDisconnectCallbacks)
			callback();
	}

	void CloseServer() 
	{
		if (!runningServer)
			return;

		runningServer = false;
		connected = false;
		tcpListener.Stop();
		listener.Join();

		while (!connectionThreads.IsEmpty)
		{
			connectionThreads.TryDequeue(out var thread);
			thread.Abort();
		}

		if (debug)
			Debug.Log("Stopped server.");
	}

	public void Send(string msg)
	{
		if (!connected)
			return;

		try
		{
			byte[] msgBytes = Encoding.ASCII.GetBytes(msg + "\n");
			stream.Write(msgBytes, 0, msgBytes.Length);
			if (debug)
				Debug.Log("Sent message: " + msg);
		}
		catch (System.IO.IOException)
		{
			if (side == Side.Client)
				displayServers.OnDisconnectError();

			if (debug)
				Debug.Log("Connection closed by the other end.");
			CloseConnection();
			lanDiscovery.ResetState();
		}
		catch (System.Threading.ThreadAbortException)
		{

		}
		catch (Exception e)
		{
			Debug.LogError("Error sending message: " + e.Message);
			CloseConnection();
			lanDiscovery.ResetState();
		}
	}

	private void Receive()
	{
		try
		{
			var sb = new StringBuilder();
			var buffer = new byte[1024];

			while (true)
			{
				int bytesRead = stream.Read(buffer, 0, buffer.Length);
				if (bytesRead == 0)
					break;

				sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

				int newlineIndex;
				while ((newlineIndex = sb.ToString().IndexOf('\n')) >= 0)
				{
					string line = sb.ToString(0, newlineIndex).TrimEnd('\r');
					sb.Remove(0, newlineIndex + 1);

					if (line.Equals(closeString)) 
					{
						closeRequest = true;
						return;
					}

					// line is ONE complete JSON message
					if (debug)
						Debug.Log("Received message: " + line);
					messageQueue.Enqueue(line);
				}
			}
		}
		catch (System.IO.IOException) 
		{
			// Caused when the thread is aborted, catch separately to avoid misleading logs
		}
		catch (Exception e)
		{
			Debug.LogError("Error receiving message: " + e.Message);
		}
	}

	private void Start()
	{
		if (side == Side.Server) 
		{
			StartServer();
		}
	}

	private void Update()
	{
		if (closeRequest) 
		{
			CloseConnection();
			closeRequest = false;
		}
	}

	private void OnApplicationQuit()
	{
		if (side == Side.Client) 
		{
			Send(closeString);
			CloseConnection();
		}
		else 
		{
			Send(closeString);
			CloseServer();
		}
	}
}
