using System.Collections;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Attach this to any CinemachineCamera in the scene.
    ///
    /// On each client it will:
    ///   1. Wait until the local player (HasInputAuthority) has spawned.
    ///   2. Find the child transform named <see cref="camTargetName"/> on that player.
    ///   3. Assign it as this camera's Follow (and optionally LookAt) target.
    ///
    /// Works independently per client — every machine only tracks its own local player.
    /// No changes to NetworkPlayerController are required.
    ///
    /// Setup:
    ///   - Add this component to every CinemachineCamera you want auto-assigned
    ///     (e.g. "FollowCam", "AimCam").
    ///   - Make sure the player prefab has a child GameObject named "CamTarget"
    ///     (or change <see cref="camTargetName"/> to match your prefab).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class LocalPlayerCameraTarget : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("Exact name of the child transform on the player prefab to use as the camera target.")]
        public string camTargetName = "CamTarget";

        [Tooltip("Also assign the target to LookAt (in addition to Follow).")]
        public bool setLookAt = true;

        [Header("Search Settings")]
        [Tooltip("Seconds between retries while waiting for the local player to spawn.")]
        [Range(0.05f, 1f)]
        public float retryInterval = 0.15f;

        // ─── Private ────────────────────────────────────────────────────

        private CinemachineCamera _cineCam;

        // ─── Lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            _cineCam = GetComponent<CinemachineCamera>();
        }

        void OnEnable()
        {
            StartCoroutine(AssignWhenLocalPlayerReady());
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        // ─── Core coroutine ─────────────────────────────────────────────

        /// <summary>
        /// Polls until the local player (HasInputAuthority) is found, then assigns
        /// the named child transform as this camera's tracking target.
        /// </summary>
        private IEnumerator AssignWhenLocalPlayerReady()
        {
            var wait = new WaitForSeconds(retryInterval);

            while (true)
            {
                NetworkPlayerController localController = FindLocalPlayerController();

                if (localController != null)
                {
                    Transform target = FindChildByName(localController.transform, camTargetName);

                    if (target != null)
                    {
                        _cineCam.Follow = target;

                        if (setLookAt)
                            _cineCam.LookAt = target;

                        Debug.Log($"[LocalPlayerCameraTarget] '{gameObject.name}' → tracking " +
                                  $"'{target.name}' on '{localController.gameObject.name}'.");
                    }
                    else
                    {
                        Debug.LogWarning($"[LocalPlayerCameraTarget] Child '{camTargetName}' not found " +
                                         $"on '{localController.gameObject.name}'. " +
                                         $"Verify the child name matches your prefab.");
                    }

                    // Whether or not the child was found, stop retrying —
                    // the player is spawned; a missing child is a setup issue, not a timing one.
                    yield break;
                }

                // Local player not yet spawned — wait and retry.
                yield return wait;
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first <see cref="NetworkPlayerController"/> that has input authority
        /// on this machine (i.e. the local player).
        /// </summary>
        private static NetworkPlayerController FindLocalPlayerController()
        {
            // FindObjectsByType avoids the overhead of sorting and is non-allocating on the type list.
            var controllers = FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);

            foreach (var ctrl in controllers)
            {
                if (ctrl.HasInputAuthority)
                    return ctrl;
            }

            return null;
        }

        /// <summary>
        /// Depth-first recursive search for a child transform by exact name.
        /// </summary>
        private static Transform FindChildByName(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildByName(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
