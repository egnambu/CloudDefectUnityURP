using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;


public class NetworkRunnerManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // The NetworkRunner is Fusion's core object — one per client or host.
    private NetworkRunner _runner;
    private BasicSpawner basicSpawner;
 
    // Called when you press "Host" in the UI
    public async void StartHost()
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true; // Host also sends input (for local player)
 
        var args = new StartGameArgs
        {
            GameMode   = GameMode.Host,          // This peer is the server
            SessionName = "MyGameSession",        // Room name on Photon Cloud
            Scene       = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };
 
        await _runner.StartGame(args);
    }
 
    // Called when you press "Join" in the UI
    public async void StartClient()
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true; // Client sends its own input
 
        var args = new StartGameArgs
        {
            GameMode    = GameMode.Client,
            SessionName = "MyGameSession",        // Must match the host's session name
            Scene       = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };
 
        await _runner.StartGame(args);
    }
 
    // ---------------------------------------------------------------
    // INetworkRunnerCallbacks — Fusion calls these automatically
    // ---------------------------------------------------------------
 
    // Called on the HOST when a new player connects
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");
 
        // Only the host spawns objects — clients receive them via state sync
        if (runner.IsServer)
        {
            // Find the spawner and tell it to spawn this player
            var spawner = FindAnyObjectByType<BasicSpawner>();
            spawner?.SpawnPlayer(runner, player);
        }
    }
 
    // Called when a player disconnects
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");
 
        var spawner = FindAnyObjectByType<BasicSpawner>();
        spawner?.DespawnPlayer(runner, player);
    }
 
    // Fusion collects input here every tick and sends it to the host
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
 
        // Read raw Unity input axes
        data.Direction = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
 
        // Set the struct as this frame's input
        input.Set(data);
    }

    // This struct is what gets sent from each client to the host every network tick.
// Keep it small — only include data that affects simulation.
// INetworkInput tells Fusion it's safe to serialize and send this over the wire.
public struct NetworkInputData : INetworkInput
{
    // Movement direction from WASD / left stick
    // Vector2: x = horizontal, y = vertical (treated as forward/back in 3D)
    public Vector2 Direction;
}



    public void OnConnectedToServer(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        throw new NotImplementedException();
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        throw new NotImplementedException();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        throw new NotImplementedException();
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        throw new NotImplementedException();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }



    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        throw new NotImplementedException();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        throw new NotImplementedException();
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        throw new NotImplementedException();
    }
}
