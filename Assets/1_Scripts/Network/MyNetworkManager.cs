using Mirror;
using UnityEngine;

public class MyNetworkManager :  NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MyNetwork] Server started !");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[MyNetwork] Server Stopped !");
    }
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[MyNetwork] Client connected !");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[MyNetwork] Client Disconnected !");
    }
}
