using UnityEngine;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

/*
 Script that allows two devices to find eachother on a local network. One acts as the discovery server, broadcasting its local
 IP address and a name to identify itself, the other listens to these broadcasts and publicly stores the received IP to connect 
 via sockets from a different component. 

 To developers using this script, only the public variables should matter.
 */

public class LANDiscovery : MonoBehaviour
{
    // Has to match in the server and client. Irrelevant otherwise.
    public int port = 56566;

    // Time in seconds between broadcasts.
    public float broadcastInterval = 1.0f;

    // Name of this specific application, sent alongside the local IP to identify this app.
    public string serverName = "test";

    public enum Side
    {
        Server,
        Client
    };

    // Server: broadcasts itself, so clients can discover it and connect to it.
    // Client: listens to broadcasts and obtains the local IP address + name.
    public Side side;

    // Set to true to log steps.
    public bool debug;

    // If client: name+IP of discovered server devices.
    public ConcurrentDictionary<string, string> discoveredServers = new();


	/*  Implementation    */
	private UdpClient client;
    private Thread listener;

    private bool listening;
    private bool broadcasting;

    private volatile bool discovered = false;
    

    public void ResetState()
    {
        discovered = false;
        if (side == Side.Client) 
        {
            discoveredServers.Clear();
            StartListening();
        }
        else
            StartBroadcast();
    }

    // Client: returns true if a server has been discovered.
    public bool DiscoveredServer()
    {
        if (side == Side.Server)
            return false;

        return discovered;
    }

    private void Start()
    {
      //  Comento això perque no comenci el broadcast a través de la red tot just obrir la app
      //  ResetState();
    }

    private void OnApplicationQuit()
    {
        if (side == Side.Client)
            StopListening();
        else
            StopBroadcast();
    }

    public void StartBroadcast()
    {
        if (broadcasting)
            return;

        client = new();
        client.EnableBroadcast = true;

        broadcasting = true;

        InvokeRepeating(nameof(Broadcast), 0.0f, broadcastInterval);
        if (debug)
            Debug.Log("Started LAN broadcast");
    }

    public void StopBroadcast()
    {
        if (!broadcasting)
            return;

        broadcasting = false;
        CancelInvoke(nameof(Broadcast));
        client?.Close();
        client = null;
		if (debug)
			Debug.Log("Stopped LAN broadcast");
    }

    private void Broadcast()
    {
        // Send server details, for now IP and a "name". This name should be unique to tell multiple apps apart.
        try
        {
            string localIP = GetLocalIPAddress();
            string msg = $"{serverName}|{localIP}";
            byte[] data = Encoding.UTF8.GetBytes(msg);

            IPEndPoint endPoint = new(IPAddress.Broadcast, port);
            client.Send(data, data.Length, endPoint);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed LAN broadcast: " + e.Message);
        }

    }

    public void StartListening()
    {
        if (listening)
            return;

        listening = true;
        listener = new(Listen);
        listener.IsBackground = true;
        listener.Start();
		if (debug)
			Debug.Log("Listening...");
    }

    public void StopListening()
    {
        if (!listening)
            return;

        listening = false;
        listener?.Abort();
        listener = null;

		if (debug)
			Debug.Log("Stopped listening");
    }

    private void Listen()
    {
        using UdpClient listener = new(port);
        IPEndPoint endPoint = new(IPAddress.Any, port);

        try
        {
            while (listening)
            {
                byte[] bytes = listener.Receive(ref endPoint);
                string msg = Encoding.UTF8.GetString(bytes);

                string[] parts = msg.Split("|");
                if (parts.Length == 2)
                {
                    string name = parts[0];
                    string address = parts[1];

					if (debug)
						Debug.Log("Discovered " + name + " as " + address);
                    discovered = true;

                    // Ensures previous values are replaced
                    discoveredServers[name] = address;
                }
            }
        }
        catch (System.Threading.ThreadAbortException)
        {

        }
        catch (Exception e)
        {
            Debug.LogError("Failed to listen to LAN broadcast: " + e.Message + " (" + e.GetType() + ")");
        }
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var address in host.AddressList)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address.ToString();
        }
        return "0.0.0.0";
    }
}
