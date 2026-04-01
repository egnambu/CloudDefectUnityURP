using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Reads local input from the Pilot1 input actions and feeds it into Fusion's input system.
    /// Attach this to the same GameObject as the NetworkRunner.
    /// Implements only INetworkRunnerCallbacks.OnInput; all other callbacks are no-ops.
    /// </summary>
    public class FusionInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        private Pilot1 _inputActions;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _aimHeld;
        private bool _sprintHeld;

        [Header("Input Settings")]
        [Range(0f, 0.2f)]
        public float stickDeadzone = 0.1f;

        void Awake()
        {
            _inputActions = new Pilot1();
        }

        void OnEnable()
        {
            _inputActions.Enable();
        }

        void OnDisable()
        {
            _inputActions.Disable();
        }

        void OnDestroy()
        {
            _inputActions.Dispose();
        }

        void Update()
        {
            // Cache input every frame so OnInput can use it (OnInput runs on Fusion tick, not Unity Update)
            _moveInput = _inputActions.PlayerA.Move.ReadValue<Vector2>();
            _lookInput = _inputActions.PlayerA.Look.ReadValue<Vector2>();
            _aimHeld = _inputActions.PlayerA.Aim.IsPressed();
            _sprintHeld = _inputActions.PlayerA.Sprint.IsPressed();

            // Apply deadzone to stick input
            if (_moveInput.magnitude < stickDeadzone)
            {
                _moveInput = Vector2.zero;
            }
            else
            {
                _moveInput = _moveInput.normalized * ((_moveInput.magnitude - stickDeadzone) / (1f - stickDeadzone));
            }
        }

        // ─── INetworkRunnerCallbacks ────────────────────────────────────

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData
            {
                MoveDirection = _moveInput,
                LookDirection = _lookInput,
                CameraYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f,
            };

            data.Buttons.Set(InputButton.Aim, _aimHeld);
            data.Buttons.Set(InputButton.Sprint, _sprintHeld);

            input.Set(data);
        }

        // Unused callbacks — required by interface
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }
}
