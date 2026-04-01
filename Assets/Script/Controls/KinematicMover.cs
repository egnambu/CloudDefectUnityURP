using UnityEngine;

namespace MadeInJupiter.Controls
{
    public class KinematicMover : MonoBehaviour
    {
        // Always store FRAME DELTA (units per frame)
        public Vector3 FrameDelta { get; private set; }

        private Vector3 _lastPosition;
        private bool _initialized;

        void OnEnable()
        {
            _initialized = false;
        }

        void LateUpdate()
        {
            if (!_initialized)
            {
                _lastPosition = transform.position;
                FrameDelta = Vector3.zero;
                _initialized = true;
                
                Debug.Log($"<color=green>[KinematicMover] Initialized on {gameObject.name} at {_lastPosition}</color>");
                return;
            }

            // PURE delta — no division
            FrameDelta = transform.position - _lastPosition;

            // Optional: Only log if there is actual movement to keep the console clean
            if (FrameDelta.sqrMagnitude > 0.000001f)
            {
                Debug.Log($"<color=cyan>[KinematicMover] {gameObject.name} Moved | Delta: {FrameDelta.ToString("F4")}</color>");
            }

            _lastPosition = transform.position;
        }
    }
}

