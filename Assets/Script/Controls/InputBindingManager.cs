using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;
using System;

/// <summary>
/// FPS game action types for input binding
/// </summary>
public enum GameAction
{
    // Movement
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Jump,
    Crouch,
    Sprint,
    
    // Combat
    Fire,
    AimDownSights,
    Reload,
    SwitchWeapon,
    Melee,
    ThrowGrenade,
    
    // Abilities
    Ability1,
    Ability2,
    Ultimate,
    
    // UI
    Pause,
    Interact,
    Scoreboard,
    
    // Menu Navigation
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    MenuConfirm,
    MenuBack
}

/// <summary>
/// Manages rebindable keyboard and mouse controls for FPS gameplay
/// </summary>
public class InputBindingManager : MonoBehaviour
{
    private static InputBindingManager instance;
    public static InputBindingManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("InputBindingManager");
                instance = go.AddComponent<InputBindingManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // Binding storage: [GameAction] = Control path or mouse button
    private Dictionary<GameAction, string> keyBindings = new Dictionary<GameAction, string>();
    
    // Cache for performance (store actual controls)
    private Dictionary<GameAction, ButtonControl> cachedKeyControls = new Dictionary<GameAction, ButtonControl>();
    
    // Cached action states for this frame (to avoid repeated dictionary lookups)
    private struct ActionState
    {
        public bool isPressed;
        public bool wasPressedThisFrame;
        public bool wasReleasedThisFrame;
    }
    private Dictionary<GameAction, ActionState> frameActionStates = new Dictionary<GameAction, ActionState>();
    private bool frameStatesCached = false;
    
    // Input device references
    private Keyboard keyboard;
    private Mouse mouse;
    
    // Mouse sensitivity
    public float mouseSensitivity = 1.0f;
    public bool invertYAxis = false;
    
    // Public properties with automatic assignment
    public Keyboard KeyboardDevice
    {
        get
        {
            if (keyboard == null)
            {
                keyboard = Keyboard.current;
            }
            return keyboard;
        }
    }

    public Mouse MouseDevice
    {
        get
        {
            if (mouse == null)
            {
                mouse = Mouse.current;
            }
            return mouse;
        }
    }
    
    // Check if input devices are connected
    public bool IsKeyboardConnected => KeyboardDevice != null;
    public bool IsMouseConnected => MouseDevice != null;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeDefaultBindings();
        RegisterDeviceCallbacks();
    }

    void Update()
    {
        // Reset frame cache flag at start of frame
        frameStatesCached = false;
    }

    void OnDestroy()
    {
        UnregisterDeviceCallbacks();
    }

    /// Register callbacks for device connection changes
    private void RegisterDeviceCallbacks()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    /// Unregister callbacks for device connection changes
    private void UnregisterDeviceCallbacks()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    /// Handle device connection/disconnection events
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Disconnected || 
            change == InputDeviceChange.Reconnected ||
            change == InputDeviceChange.Added ||
            change == InputDeviceChange.Removed)
        {
            if (device is Keyboard || device is Mouse)
            {
                Debug.Log($"[InputBindingManager] Device change detected: {device.name} - {change}");
                
                // Clear device references on disconnect
                if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
                {
                    if (device is Keyboard)
                    {
                        keyboard = null;
                        Debug.LogWarning("[InputBindingManager] Keyboard DISCONNECTED!");
                    }
                    else if (device is Mouse)
                    {
                        mouse = null;
                        Debug.LogWarning("[InputBindingManager] Mouse DISCONNECTED!");
                    }
                }
                
                RefreshControlCache();
            }
        }
    }

    /// Set up default FPS bindings (WASD + mouse)
    private void InitializeDefaultBindings()
    {
        // Movement (WASD)
        SetDefaultBinding(GameAction.MoveForward, "/Keyboard/w");
        SetDefaultBinding(GameAction.MoveBackward, "/Keyboard/s");
        SetDefaultBinding(GameAction.MoveLeft, "/Keyboard/a");
        SetDefaultBinding(GameAction.MoveRight, "/Keyboard/d");
        SetDefaultBinding(GameAction.Jump, "/Keyboard/space");
        SetDefaultBinding(GameAction.Crouch, "/Keyboard/leftCtrl");
        SetDefaultBinding(GameAction.Sprint, "/Keyboard/leftShift");
        
        // Combat
        SetDefaultBinding(GameAction.Fire, "/Mouse/leftButton");
        SetDefaultBinding(GameAction.AimDownSights, "/Mouse/rightButton");
        SetDefaultBinding(GameAction.Reload, "/Keyboard/r");
        SetDefaultBinding(GameAction.SwitchWeapon, "/Keyboard/q");
        SetDefaultBinding(GameAction.Melee, "/Keyboard/v");
        SetDefaultBinding(GameAction.ThrowGrenade, "/Keyboard/g");
        
        // Abilities
        SetDefaultBinding(GameAction.Ability1, "/Keyboard/e");
        SetDefaultBinding(GameAction.Ability2, "/Keyboard/f");
        SetDefaultBinding(GameAction.Ultimate, "/Keyboard/x");
        
        // UI
        SetDefaultBinding(GameAction.Pause, "/Keyboard/escape");
        SetDefaultBinding(GameAction.Interact, "/Keyboard/f");
        SetDefaultBinding(GameAction.Scoreboard, "/Keyboard/tab");
        
        // Menu Navigation
        SetDefaultBinding(GameAction.MenuUp, "/Keyboard/w");
        SetDefaultBinding(GameAction.MenuDown, "/Keyboard/s");
        SetDefaultBinding(GameAction.MenuLeft, "/Keyboard/a");
        SetDefaultBinding(GameAction.MenuRight, "/Keyboard/d");
        SetDefaultBinding(GameAction.MenuConfirm, "/Keyboard/enter");
        SetDefaultBinding(GameAction.MenuBack, "/Keyboard/escape");
        
        Debug.Log("[InputBindingManager] Default FPS bindings initialized");
    }

    private void SetDefaultBinding(GameAction action, string controlPath)
    {
        keyBindings[action] = controlPath;
    }

    /// Cache all action states for this frame (call once per frame for performance)
    private void CacheFrameActionStates()
    {
        if (frameStatesCached) return;
        
        frameActionStates.Clear();
        
        foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
        {
            var control = GetButtonControl(action);
            
            ActionState state = new ActionState
            {
                isPressed = control != null && control.isPressed,
                wasPressedThisFrame = control != null && control.wasPressedThisFrame,
                wasReleasedThisFrame = control != null && control.wasReleasedThisFrame
            };
            
            frameActionStates[action] = state;
        }
        
        frameStatesCached = true;
    }

    /// Check if an action was pressed this frame
    public bool GetActionDown(GameAction action)
    {
        CacheFrameActionStates();
        return frameActionStates.ContainsKey(action) && frameActionStates[action].wasPressedThisFrame;
    }

    /// Check if an action is currently held
    public bool GetAction(GameAction action)
    {
        CacheFrameActionStates();
        return frameActionStates.ContainsKey(action) && frameActionStates[action].isPressed;
    }

    /// Check if an action was released this frame
    public bool GetActionUp(GameAction action)
    {
        CacheFrameActionStates();
        return frameActionStates.ContainsKey(action) && frameActionStates[action].wasReleasedThisFrame;
    }


    /// Get mouse delta for camera look (FPS style)
    public Vector2 GetMouseDelta()
    {
        if (MouseDevice == null) return Vector2.zero;
        
        Vector2 delta = MouseDevice.delta.ReadValue() * mouseSensitivity;
        
        if (invertYAxis)
        {
            delta.y = -delta.y;
        }
        
        return delta;
    }

    /// Get the actual ButtonControl for an action
    private ButtonControl GetButtonControl(GameAction action)
    {
        // Check cache first
        if (cachedKeyControls.ContainsKey(action))
        {
            var cached = cachedKeyControls[action];
            if (cached != null && cached.device != null)
            {
                return cached;
            }
        }

        // Not in cache, resolve it
        if (!keyBindings.ContainsKey(action))
        {
            return null;
        }

        string controlPath = keyBindings[action];
        var control = ResolveControl(controlPath);
        
        // Cache it
        cachedKeyControls[action] = control;
        
        return control;
    }

    /// Resolve a control path to an actual ButtonControl
    private ButtonControl ResolveControl(string controlPath)
    {
        try
        {
            if (controlPath.StartsWith("/Mouse/"))
            {
                if (MouseDevice == null) return null;
                
                string path = controlPath.Replace("/Mouse/", "");
                return MouseDevice.GetChildControl(path) as ButtonControl;
            }
            else if (controlPath.StartsWith("/Keyboard/"))
            {
                if (KeyboardDevice == null) return null;
                
                string path = controlPath.Replace("/Keyboard/", "");
                return KeyboardDevice.GetChildControl(path) as ButtonControl;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InputBindingManager] Failed to resolve control path '{controlPath}': {e.Message}");
        }
        
        return null;
    }

    /// Refresh the control cache (called only on device change events)
    private void RefreshControlCache()
    {
        Debug.Log("[InputBindingManager] Refreshing control cache due to device change");
        cachedKeyControls.Clear();
        frameActionStates.Clear();
        frameStatesCached = false;
    }

    /// Start rebinding an action - wait for player to press a key/button
    public void StartRebind(GameAction action, Action<string> onComplete)
    {
        StartCoroutine(RebindCoroutine(action, onComplete));
    }

    private System.Collections.IEnumerator RebindCoroutine(GameAction action, Action<string> onComplete)
    {
        if (KeyboardDevice == null && MouseDevice == null)
        {
            Debug.LogWarning("[InputBindingManager] No input devices connected!");
            onComplete?.Invoke(null);
            yield break;
        }

        Debug.Log($"[InputBindingManager] Waiting for input for {action}...");

        ButtonControl pressedControl = null;
        string deviceType = "";
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            // Check keyboard
            if (KeyboardDevice != null)
            {
                foreach (var control in KeyboardDevice.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame && button != KeyboardDevice.anyKey)
                    {
                        pressedControl = button;
                        deviceType = "Keyboard";
                        break;
                    }
                }
            }

            // Check mouse buttons
            if (MouseDevice != null && pressedControl == null)
            {
                if (MouseDevice.leftButton.wasPressedThisFrame)
                {
                    pressedControl = MouseDevice.leftButton;
                    deviceType = "Mouse";
                }
                else if (MouseDevice.rightButton.wasPressedThisFrame)
                {
                    pressedControl = MouseDevice.rightButton;
                    deviceType = "Mouse";
                }
                else if (MouseDevice.middleButton.wasPressedThisFrame)
                {
                    pressedControl = MouseDevice.middleButton;
                    deviceType = "Mouse";
                }
            }

            if (pressedControl != null) break;
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (pressedControl == null)
        {
            Debug.LogWarning("[InputBindingManager] Rebind timeout");
            onComplete?.Invoke(null);
            yield break;
        }

        // Build control path
        string controlPath = $"/{deviceType}/{pressedControl.name}";
        
        // Set the binding
        SetBinding(action, controlPath);
        
        Debug.Log($"[InputBindingManager] {action} bound to {pressedControl.displayName}");
        onComplete?.Invoke(pressedControl.displayName);
    }

    /// Manually set a binding
    public void SetBinding(GameAction action, string controlPath)
    {
        keyBindings[action] = controlPath;
        
        // Clear cache for this action
        if (cachedKeyControls.ContainsKey(action))
        {
            cachedKeyControls.Remove(action);
        }
    }

    /// Get the current binding for display purposes
    public string GetBindingDisplayName(GameAction action)
    {
        var control = GetButtonControl(action);
        if (control != null)
        {
            return control.displayName;
        }
        
        // Fallback: return path if control not found
        if (keyBindings.ContainsKey(action))
        {
            return keyBindings[action].Replace("/Keyboard/", "").Replace("/Mouse/", "");
        }
        
        return "Not Bound";
    }

    /// Reset bindings to defaults
    public void ResetToDefaults()
    {
        keyBindings.Clear();
        cachedKeyControls.Clear();
        
        InitializeDefaultBindings();
        Debug.Log("[InputBindingManager] Bindings reset to defaults");
    }

    /// Save bindings to PlayerPrefs
    public void SaveBindings()
    {
        foreach (var kvp in keyBindings)
        {
            string key = $"Binding_{kvp.Key}";
            PlayerPrefs.SetString(key, kvp.Value);
        }
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.SetInt("InvertYAxis", invertYAxis ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("[InputBindingManager] Bindings saved");
    }

    /// Load bindings from PlayerPrefs
    public void LoadBindings()
    {
        foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
        {
            string key = $"Binding_{action}";
            if (PlayerPrefs.HasKey(key))
            {
                string controlPath = PlayerPrefs.GetString(key);
                SetBinding(action, controlPath);
            }
        }
        
        if (PlayerPrefs.HasKey("MouseSensitivity"))
        {
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
        }
        
        if (PlayerPrefs.HasKey("InvertYAxis"))
        {
            invertYAxis = PlayerPrefs.GetInt("InvertYAxis") == 1;
        }
        
        Debug.Log("[InputBindingManager] Bindings loaded");
    }
}