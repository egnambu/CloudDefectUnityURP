using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Central Fusion 2 network manager.
    /// Starts a session in Host mode, handles player join/leave, and spawns player prefabs.
    ///
    /// Setup:
    ///   1. Create an empty GameObject in your scene named "GameLauncher".
    ///   2. Add this component.
    ///   3. Assign the player prefab (must have a NetworkObject component).
    ///   4. Optionally assign a NetworkRunner prefab, or one will be created automatically.
    ///   5. The FusionInputProvider is added automatically at runtime.
    /// </summary>
    public class GameLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Prefabs")]
        [Tooltip("The networked player prefab to spawn for each player. Must have a NetworkObject.")]
        public NetworkPrefabRef playerPrefab;

        [Tooltip("Optional: A NetworkRunner prefab. If null, a new runner will be created.")]
        public NetworkRunner runnerPrefab;

        [Header("Session Settings")]
        [Tooltip("Room name for the Fusion session. Empty = random room.")]
        public string roomName = "MercenaryRoom";

        [Tooltip("Maximum number of players allowed in the session.")]
        public int maxPlayers = 8;

        [Header("Spawn Settings")]
        [Tooltip("Position where players spawn.")]
        public Vector3 spawnPosition = new Vector3(0f, 1f, 0f);

        [Header("Runtime Info (Read Only)")]
        [SerializeField] private string localDeviceId;
        [SerializeField] private string localUsername;
        [SerializeField] private int connectedPlayerCount;

        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

        /// <summary>The active NetworkRunner instance.</summary>
        public NetworkRunner Runner => _runner;

        /// <summary>Singleton-style access (optional). Set in Awake, cleared in OnDestroy.</summary>
        public static GameLauncher Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            // Display identity info
            localDeviceId = PlayerIdentity.DeviceId;
            localUsername = PlayerIdentity.Username;

            Debug.Log($"[GameLauncher] DeviceId: {localDeviceId}");
            Debug.Log($"[GameLauncher] Username: {localUsername}");

            // Auto-start in Host mode
            StartGame(GameMode.Host);
        }

        /// <summary>
        /// Starts a Fusion session with the specified game mode.
        /// </summary>
        public async void StartGame(GameMode mode)
        {
            Debug.Log($"[GameLauncher] Starting Fusion session as {mode}...");

            // Create or instantiate the runner
            if (runnerPrefab != null)
            {
                _runner = Instantiate(runnerPrefab);
            }
            else
            {
                var runnerGO = new GameObject("NetworkRunner");
                _runner = runnerGO.AddComponent<NetworkRunner>();
            }

            // Ensure the runner persists across scene loads
            DontDestroyOnLoad(_runner.gameObject);

            // Register this as a callback listener
            _runner.AddCallbacks(this);

            // Add input provider and explicitly register it with the runner
            FusionInputProvider inputProvider = _runner.GetComponent<FusionInputProvider>();
            if (inputProvider == null)
            {
                inputProvider = _runner.gameObject.AddComponent<FusionInputProvider>();
            }
            _runner.AddCallbacks(inputProvider);
            Debug.Log("[GameLauncher] FusionInputProvider registered with runner.");

            // Configure and start the session
            var startArgs = new StartGameArgs
            {
                GameMode = mode,
                SessionName = string.IsNullOrEmpty(roomName) ? null : roomName,
                PlayerCount = maxPlayers,
                SceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            };

            var result = await _runner.StartGame(startArgs);

            if (result.Ok)
            {
                Debug.Log($"[GameLauncher] Session started successfully. Mode: {mode}, Session: {_runner.SessionInfo.Name}");
            }
            else
            {
                Debug.LogError($"[GameLauncher] Failed to start session: {result.ShutdownReason}");
            }
        }

        /// <summary>
        /// Gracefully shutdown the runner and clean up.
        /// </summary>
        public async void Shutdown()
        {
            if (_runner != null)
            {
                await _runner.Shutdown();
                _runner = null;
            }

            _spawnedPlayers.Clear();
            connectedPlayerCount = 0;
            Debug.Log("[GameLauncher] Session shut down.");
        }

        // ─── INetworkRunnerCallbacks ────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[GameLauncher] Player {player.PlayerId} joined.");

            // Only the host/server spawns player objects
            if (runner.IsServer)
            {
                var spawnedObject = runner.Spawn(
                    playerPrefab,
                    spawnPosition,
                    Quaternion.identity,
                    player  // input authority
                );

                if (spawnedObject != null)
                {
                    _spawnedPlayers[player] = spawnedObject;
                    Debug.Log($"[GameLauncher] Spawned player prefab for Player {player.PlayerId}");
                }
                else
                {
                    Debug.LogError($"[GameLauncher] Failed to spawn player prefab for Player {player.PlayerId}. " +
                                   "Check that playerPrefab is assigned and has a NetworkObject component.");
                }
            }

            connectedPlayerCount = runner.ActivePlayers.Count();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[GameLauncher] Player {player.PlayerId} left.");

            if (_spawnedPlayers.TryGetValue(player, out var networkObject))
            {
                if (networkObject != null)
                {
                    runner.Despawn(networkObject);
                    Debug.Log($"[GameLauncher] Despawned player prefab for Player {player.PlayerId}");
                }
                _spawnedPlayers.Remove(player);
            }

            connectedPlayerCount = runner.ActivePlayers.Count();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[GameLauncher] Runner shutdown: {shutdownReason}");
            _spawnedPlayers.Clear();
            connectedPlayerCount = 0;
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[GameLauncher] Connected to server.");
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[GameLauncher] Disconnected from server: {reason}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[GameLauncher] Connection failed to {remoteAddress}: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // Accept all connections
            request.Accept();
        }

        // ─── Unused callbacks (required by interface) ───────────────────

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }

    // Extension to count active players since Fusion 2's IEnumerable doesn't have Count()
    internal static class PlayerRefExtensions
    {
        public static int Count(this IEnumerable<PlayerRef> players)
        {
            int count = 0;
            foreach (var _ in players) count++;
            return count;
        }
    }
}
