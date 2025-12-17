using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class SteamLogin : MonoBehaviour
{
    [Header("Your Next.js API URL (update to your domain or localhost)")]
    public string nextJsAuthUrl = "http://localhost:3000/api/auth/steam";
    public string nextJsCheckUrl = "http://localhost:3000/api/auth/check";
    public float pollInterval = 5f; // Poll every 2 seconds
    public float pollTimeout = 100f; // Timeout 

    private string steamId;
    private string personName;
    private string avatarUrl;
    private string currentSessionId;

    [SerializeField] private Button loginButton;
    [SerializeField] private InputField inputField;
    [SerializeField] private Text field;

    void Awake()
    {
        string savedSessionId = PlayerPrefs.GetString("sessionId", "");
        if (!string.IsNullOrEmpty(savedSessionId))
        {
            // Load saved data
            steamId = PlayerPrefs.GetString("steamId", "");
            personName = PlayerPrefs.GetString("personName", "");
            avatarUrl = PlayerPrefs.GetString("avatarUrl", "");

            if (string.IsNullOrEmpty(steamId))
            {
                // Session exists but not verified, continue polling
                StartCoroutine(PollForAuthentication(savedSessionId));
            }
            else
            {
                // Already authenticated
                loginButton.gameObject.SetActive(false);
                inputField.gameObject.SetActive(true);
                field.text = personName;
                Debug.Log("SessionId: " + PlayerPrefs.GetString("sessionId", "Not set"));
            }
        }
    }

    /// <summary>
    /// Called when player clicks "Login with Steam".
    /// Opens browser to handle Steam OpenID auth via Next.js.
    /// </summary>
    public void LoginWithSteam()
    {
        // Generate a unique session ID
        currentSessionId = Guid.NewGuid().ToString();

        // Append sessionId to the auth URL
        string authUrlWithSession = $"{nextJsAuthUrl}?sessionId={currentSessionId}";
        Debug.Log("SteamID: " + PlayerPrefs.GetString("steamId", "Not set"));
        Debug.Log("Person Name: " + PlayerPrefs.GetString("personName", "Not set"));
        Debug.Log("Avatar URL: " + PlayerPrefs.GetString("avatarUrl", "Not set"));
        Debug.Log($"Opening Steam login with sessionId: {currentSessionId}");
        Application.OpenURL(authUrlWithSession);

        // Start polling for authentication completion
        StartCoroutine(PollForAuthentication(currentSessionId));
    }

    /// <summary>
    /// Polls the check endpoint until authentication is complete or timeout occurs.
    /// </summary>
    private IEnumerator PollForAuthentication(string sessionId)
    {
        float elapsedTime = 0f;
        string checkUrl = $"{nextJsCheckUrl}?sessionId={sessionId}";

        Debug.Log("Started polling for Steam authentication...");

        while (elapsedTime < pollTimeout)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(checkUrl))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string responseText = req.downloadHandler.text;
                    Debug.Log($"Poll response: {responseText}");

                    AuthCheckResponse response = JsonUtility.FromJson<AuthCheckResponse>(responseText);

                    if (response.verified && !string.IsNullOrEmpty(response.steamId))
                    {
                        Debug.Log("✅ Steam authentication successful!");

                        // Store user data
                        steamId = response.steamId;
                        personName = response.personaname;
                        avatarUrl = response.avatarUrl;

                        // Save locally for persistence
                        PlayerPrefs.SetString("steamId", steamId);
                        PlayerPrefs.SetString("personName", personName);
                        PlayerPrefs.SetString("avatarUrl", avatarUrl);
                        PlayerPrefs.SetString("sessionId", sessionId);
                        PlayerPrefs.Save();

                        //UI TESTING
                        loginButton.gameObject.SetActive(false);
                        inputField.gameObject.SetActive(true);
                        yield break; // Exit the coroutine
                    }
                }
                else
                {
                    Debug.LogWarning($"Poll request failed: {req.error}");
                }
            }

            // Wait before next poll
            yield return new WaitForSeconds(pollInterval);
            elapsedTime += pollInterval;
        }

        Debug.LogError("Steam authentication polling timed out!");
    }

    public void ClearSavedData()
    {
        PlayerPrefs.DeleteKey("steamId");
        PlayerPrefs.DeleteKey("personName");
        PlayerPrefs.DeleteKey("avatarUrl");
        PlayerPrefs.DeleteKey("sessionId");
        PlayerPrefs.Save();
        // Reset local variables
        steamId = null;
        personName = null;
        avatarUrl = null;
        currentSessionId = null;
        // Reset UI
        loginButton.gameObject.SetActive(true);
        inputField.gameObject.SetActive(false);
        field.text = "";
    }

    [System.Serializable]
    private class AuthCheckResponse
    {
        public bool verified;
        public string steamId;
        public string personaname;
        public string avatarUrl;
        public string sessionId;
    }
}
