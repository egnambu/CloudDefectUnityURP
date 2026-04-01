using UnityEngine;

namespace MadeInJupiter.Network
{
    /// <summary>
    /// Persistent player identity system.
    /// Generates a unique device ID (GUID) on first run, stores it in PlayerPrefs.
    /// Also stores a local username that persists between sessions.
    /// </summary>
    public static class PlayerIdentity
    {
        private const string PREF_DEVICE_ID = "MIJ_DeviceId";
        private const string PREF_USERNAME  = "MIJ_Username";

        private static string _cachedDeviceId;
        private static string _cachedUsername;

        /// <summary>
        /// Unique device identifier. Generated once per device and stored in PlayerPrefs.
        /// Falls back to SystemInfo.deviceUniqueIdentifier if PlayerPrefs is cleared.
        /// </summary>
        public static string DeviceId
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedDeviceId))
                    return _cachedDeviceId;

                _cachedDeviceId = PlayerPrefs.GetString(PREF_DEVICE_ID, string.Empty);

                if (string.IsNullOrEmpty(_cachedDeviceId))
                {
                    // Generate a new GUID for this device
                    _cachedDeviceId = System.Guid.NewGuid().ToString("N"); // 32 hex chars, no hyphens
                    PlayerPrefs.SetString(PREF_DEVICE_ID, _cachedDeviceId);
                    PlayerPrefs.Save();
                    Debug.Log($"[PlayerIdentity] Generated new DeviceId: {_cachedDeviceId}");
                }
                else
                {
                    Debug.Log($"[PlayerIdentity] Loaded existing DeviceId: {_cachedDeviceId}");
                }

                return _cachedDeviceId;
            }
        }

        /// <summary>
        /// Local player username. Stored in PlayerPrefs, defaults to "Player" + last 4 chars of DeviceId.
        /// </summary>
        public static string Username
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedUsername))
                    return _cachedUsername;

                _cachedUsername = PlayerPrefs.GetString(PREF_USERNAME, string.Empty);

                if (string.IsNullOrEmpty(_cachedUsername))
                {
                    // Generate a default username from the device ID
                    string id = DeviceId;
                    _cachedUsername = "Player_" + id.Substring(id.Length - 4);
                    PlayerPrefs.SetString(PREF_USERNAME, _cachedUsername);
                    PlayerPrefs.Save();
                }

                return _cachedUsername;
            }
        }

        /// <summary>
        /// Set a new username and persist it.
        /// </summary>
        public static void SetUsername(string newUsername)
        {
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                Debug.LogWarning("[PlayerIdentity] Cannot set empty username.");
                return;
            }

            _cachedUsername = newUsername.Trim();
            PlayerPrefs.SetString(PREF_USERNAME, _cachedUsername);
            PlayerPrefs.Save();
            Debug.Log($"[PlayerIdentity] Username set to: {_cachedUsername}");
        }

        /// <summary>
        /// Clear cached values (useful if you want to force a reload from PlayerPrefs).
        /// Does NOT delete the PlayerPrefs entries.
        /// </summary>
        public static void ClearCache()
        {
            _cachedDeviceId = null;
            _cachedUsername = null;
        }

        /// <summary>
        /// Completely reset identity (new GUID, clear username). Use with caution.
        /// </summary>
        public static void ResetIdentity()
        {
            PlayerPrefs.DeleteKey(PREF_DEVICE_ID);
            PlayerPrefs.DeleteKey(PREF_USERNAME);
            PlayerPrefs.Save();
            _cachedDeviceId = null;
            _cachedUsername = null;
            Debug.Log("[PlayerIdentity] Identity reset. New ID will be generated on next access.");
        }
    }
}
