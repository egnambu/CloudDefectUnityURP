using UnityEngine;

namespace MadeInJupiter.Controls
{
    /// <summary>
    /// Tracks moving platforms using local-space anchoring.
    /// Works with animation-driven, physics-driven, and transform-driven platforms.
    ///
    /// HOW IT WORKS:
    ///   When the player lands on a surface, their position is recorded in the
    ///   platform's LOCAL space (InverseTransformPoint). Each subsequent frame,
    ///   that local position is transformed BACK to world space. Because the
    ///   platform has moved/rotated since last frame, the resulting world position
    ///   is different — the difference IS the platform delta.
    ///
    ///   This naturally handles translation AND rotation of any complexity,
    ///   including bones driven by skeletal animation.
    ///
    /// USAGE (inside a grounded state's Tick):
    ///   1. Call UpdateBeforeMove()
    ///   2. Read PositionDelta and YawDelta
    ///   3. Add PositionDelta to CharacterController.Move()
    ///   4. Apply YawDelta rotation to the player transform
    ///   5. Call UpdateAfterMove() AFTER CharacterController.Move()
    ///
    /// Call ClearPlatform() when leaving the ground (jump, fall, hover, flight).
    /// </summary>
    public class MovingPlatformHandler
    {
        private Transform _platform;
        private Vector3 _localPosition;       // Player position in platform's local space
        private Quaternion _localRotation;     // Player rotation relative to platform

        /// <summary>World-space position offset caused by platform movement this frame.</summary>
        public Vector3 PositionDelta { get; private set; }

        /// <summary>Yaw rotation (degrees) caused by platform rotation this frame.</summary>
        public float YawDelta { get; private set; }

        /// <summary>True when the player is anchored to a platform.</summary>
        public bool IsOnPlatform => _platform != null;

        /// <summary>
        /// Call BEFORE applying any movement this frame.
        /// Raycasts downward to detect the surface, then computes position and yaw deltas.
        /// </summary>
        public void UpdateBeforeMove(
            Transform playerTransform,
            Vector3 groundCheckPos,
            LayerMask groundMask,
            float rayDistance = 1.5f)
        {
            if (!Physics.Raycast(groundCheckPos, Vector3.down, out RaycastHit hit, rayDistance, groundMask))
            {
                ClearPlatform();
                return;
            }

            Transform hitTransform = hit.collider.transform;

            if (hitTransform != _platform)
            {
                // ---- NEW PLATFORM ----
                // Anchor with zero delta so the player doesn't teleport on the first frame.
                _platform = hitTransform;
                StoreLocalSpace(playerTransform);
                PositionDelta = Vector3.zero;
                YawDelta = 0f;
                return;
            }

            // ---- SAME PLATFORM — compute deltas ----

            // Position: where the player WOULD be if perfectly glued to the platform
            Vector3 targetWorldPos = _platform.TransformPoint(_localPosition);
            PositionDelta = targetWorldPos - playerTransform.position;

            // Rotation: extract yaw-only delta (keep the player upright)
            Quaternion targetWorldRot = _platform.rotation * _localRotation;
            float targetYaw = targetWorldRot.eulerAngles.y;
            float currentYaw = playerTransform.eulerAngles.y;
            YawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
        }

        /// <summary>
        /// Call AFTER CharacterController.Move() to re-record the player's
        /// position in the platform's local space.  This accounts for the
        /// player's own movement (walking, gravity, etc.) so only genuine
        /// platform motion shows up as a delta next frame.
        /// </summary>
        public void UpdateAfterMove(Transform playerTransform)
        {
            if (_platform == null) return;
            StoreLocalSpace(playerTransform);
        }

        /// <summary>
        /// Forget the current platform. Call this when leaving the ground.
        /// </summary>
        public void ClearPlatform()
        {
            _platform = null;
            PositionDelta = Vector3.zero;
            YawDelta = 0f;
        }

        private void StoreLocalSpace(Transform playerTransform)
        {
            _localPosition = _platform.InverseTransformPoint(playerTransform.position);
            _localRotation = Quaternion.Inverse(_platform.rotation) * playerTransform.rotation;
        }
    }
}
