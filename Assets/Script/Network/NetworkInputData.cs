using Fusion;
using UnityEngine;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Fusion 2 networked input data.
    /// Contains only what FreeWalk and AimWalk states need.
    /// Transmitted every tick from the input authority to the state authority.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        /// <summary>WASD / left-stick movement (raw, pre-camera-relative).</summary>
        public Vector2 MoveDirection;

        /// <summary>Mouse delta / right-stick look input.</summary>
        public Vector2 LookDirection;

        /// <summary>Yaw angle of the local camera (degrees). Used to compute camera-relative movement on all peers.</summary>
        public float CameraYaw;

        /// <summary>Packed button flags.</summary>
        public NetworkButtons Buttons;
    }

    /// <summary>
    /// Button indices for NetworkButtons packing.
    /// </summary>
    public enum InputButton
    {
        Aim    = 0,
        Sprint = 1,
    }
}
