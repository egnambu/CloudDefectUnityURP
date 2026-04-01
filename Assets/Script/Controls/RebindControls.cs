using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// In-game control rebinding system for Keyboard & Mouse and Gamepad.
/// Provides a fullscreen overlay menu with persistent binding storage.
/// 
/// ASSUMPTIONS:
/// - Canvas is set to Screen Space - Overlay or Camera
/// - UI uses TextMeshPro for text elements
/// - The InputActionAsset has control schemes named "Keyboard&Mouse" and "Gamepad"
/// - Bindings use standard Input System binding groups
/// 
/// UI HIERARCHY (auto-generated):
/// - RebindCanvas (fullscreen overlay)
///   - BackgroundPanel (semi-transparent black)
///   - MainMenuPanel (control scheme selection)
///     - Title
///     - KeyboardMouseButton
///     - GamepadButton
///     - CloseButton
///   - BindingsPanel (action list for selected scheme)
///     - Title
///     - ScrollView with action entries
///     - BackButton
///   - RebindOverlay (shown during rebind)
///     - PromptText
///     - CancelButton
/// </summary>
public class RebindControls : MonoBehaviour
{
    #region Inspector Fields
    
    [Header("Input Configuration")]
    [Tooltip("The InputActionAsset containing all player actions. Assign your .inputactions asset here.")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Tooltip("The action map name to rebind (e.g., 'PlayerA')")]
    [SerializeField] private string actionMapName = "PlayerA";
    
    [Header("UI References (Optional - Auto-generated if null)")]
    [SerializeField] private Canvas rebindCanvas;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject bindingsPanel;
    [SerializeField] private GameObject rebindOverlay;
    [SerializeField] private Transform bindingsContainer;
    [SerializeField] private TextMeshProUGUI rebindPromptText;
    
    [Header("UI Styling")]
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.9f);
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color buttonHighlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color accentColor = new Color(0.4f, 0.8f, 1f, 1f);
    
    [Header("Persistence")]
    [SerializeField] private string saveKey = "InputBindings";
    
    #endregion

    #region Private Fields
    
    // Control scheme identifiers (must match your InputActionAsset)
    private const string KEYBOARD_MOUSE_SCHEME = "Keyboard&Mouse";
    private const string GAMEPAD_SCHEME = "Gamepad";
    
    // Current state
    private string currentControlScheme;
    private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;
    private bool isMenuOpen;
    
    // UI element references (for dynamic generation)
    private List<GameObject> bindingEntries = new List<GameObject>();
    private TextMeshProUGUI bindingsPanelTitle;
    private Button backButton;
    
    // Actions to exclude from rebinding (system actions, etc.)
    private readonly HashSet<string> excludedActions = new HashSet<string>
    {
        // Add any action names you want to exclude from rebinding
    };
    
    #endregion

    #region Unity Lifecycle
    
    private void Awake()
    {
        // Validate input actions asset
        if (inputActions == null)
        {
            Debug.LogError("[RebindControls] InputActionAsset is not assigned! Please assign it in the Inspector.");
            enabled = false;
            return;
        }
        
        // Load saved bindings on startup
        LoadBindings();
    }
    
    private void Start()
    {
        // Generate UI if not assigned
        if (rebindCanvas == null)
        {
            GenerateUI();
        }
        
        // Ensure menu starts closed
        CloseMenu();
    }
    
    private void OnDestroy()
    {
        // Clean up any ongoing rebind operation
        CancelRebind();
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// Opens the rebind menu. Call this when Tab is pressed.
    /// Shows cursor and unlocks it for menu navigation.
    /// </summary>
    public void OpenRebindMenu()
    {
        if (isMenuOpen) return;
        
        isMenuOpen = true;
        
        // Show and unlock cursor for menu interaction
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        // Show the canvas and main menu
        rebindCanvas.gameObject.SetActive(true);
        ShowMainMenu();
    }
    
    /// <summary>
    /// Alias for OpenRebindMenu for convenience.
    /// </summary>
    public void RebindControlsMenu() => OpenRebindMenu();
    
    /// <summary>
    /// Closes the rebind menu and returns to gameplay.
    /// Hides and locks cursor.
    /// </summary>
    public void CloseMenu()
    {
        CancelRebind();
        
        isMenuOpen = false;
        
        // Hide and lock cursor for gameplay
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        // Hide all UI
        if (rebindCanvas != null)
            rebindCanvas.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Resets all bindings to their default values.
    /// </summary>
    public void ResetAllBindings()
    {
        foreach (var map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }
        
        // Clear saved data
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        
        // Refresh UI if showing bindings
        if (bindingsPanel != null && bindingsPanel.activeSelf)
        {
            ShowBindingsForScheme(currentControlScheme);
        }
        
        Debug.Log("[RebindControls] All bindings reset to defaults.");
    }
    
    /// <summary>
    /// Resets bindings for the current control scheme only.
    /// </summary>
    public void ResetCurrentSchemeBindings()
    {
        if (string.IsNullOrEmpty(currentControlScheme)) return;
        
        var actionMap = inputActions.FindActionMap(actionMapName);
        if (actionMap == null) return;
        
        foreach (var action in actionMap.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (IsBindingForScheme(binding, currentControlScheme))
                {
                    action.RemoveBindingOverride(i);
                }
            }
        }
        
        SaveBindings();
        ShowBindingsForScheme(currentControlScheme);
    }
    
    #endregion

    #region Menu Navigation
    
    /// <summary>
    /// Shows the main menu with control scheme selection.
    /// </summary>
    private void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        bindingsPanel.SetActive(false);
        rebindOverlay.SetActive(false);
    }
    
    /// <summary>
    /// Called when Keyboard & Mouse is selected.
    /// </summary>
    public void SelectKeyboardMouse()
    {
        currentControlScheme = KEYBOARD_MOUSE_SCHEME;
        ShowBindingsForScheme(KEYBOARD_MOUSE_SCHEME);
    }
    
    /// <summary>
    /// Called when Gamepad is selected.
    /// </summary>
    public void SelectGamepad()
    {
        currentControlScheme = GAMEPAD_SCHEME;
        ShowBindingsForScheme(GAMEPAD_SCHEME);
    }
    
    /// <summary>
    /// Returns to the main menu from the bindings panel.
    /// </summary>
    public void BackToMainMenu()
    {
        CancelRebind();
        ShowMainMenu();
    }
    
    #endregion

    #region Bindings Display
    
    /// <summary>
    /// Shows all actions and their bindings for the specified control scheme.
    /// </summary>
    private void ShowBindingsForScheme(string schemeName)
    {
        mainMenuPanel.SetActive(false);
        bindingsPanel.SetActive(true);
        rebindOverlay.SetActive(false);
        
        // Update title
        if (bindingsPanelTitle != null)
        {
            bindingsPanelTitle.text = schemeName == KEYBOARD_MOUSE_SCHEME 
                ? "Keyboard & Mouse Bindings" 
                : "Gamepad Bindings";
        }
        
        // Clear existing entries
        ClearBindingEntries();
        
        // Get the action map
        var actionMap = inputActions.FindActionMap(actionMapName);
        if (actionMap == null)
        {
            Debug.LogError($"[RebindControls] Action map '{actionMapName}' not found!");
            return;
        }
        
        // Create an entry for each action with bindings in this scheme
        foreach (var action in actionMap.actions)
        {
            if (excludedActions.Contains(action.name)) continue;
            
            // Find all bindings for this scheme
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                
                // Skip composite parent bindings (we'll show the parts)
                if (binding.isComposite) continue;
                
                // Check if this binding is for the current scheme
                if (!IsBindingForScheme(binding, schemeName)) continue;
                
                // Create UI entry for this binding
                CreateBindingEntry(action, i, binding);
            }
        }
    }
    
    /// <summary>
    /// Checks if a binding belongs to the specified control scheme.
    /// </summary>
    private bool IsBindingForScheme(InputBinding binding, string schemeName)
    {
        // If binding has no groups, it applies to all schemes
        if (string.IsNullOrEmpty(binding.groups))
        {
            // Check if the path matches the scheme's device
            if (schemeName == KEYBOARD_MOUSE_SCHEME)
            {
                return binding.path.Contains("<Keyboard>") || binding.path.Contains("<Mouse>");
            }
            else if (schemeName == GAMEPAD_SCHEME)
            {
                return binding.path.Contains("<Gamepad>");
            }
        }
        
        // Check if the scheme is in the binding's groups
        return binding.groups.Contains(schemeName);
    }
    
    /// <summary>
    /// Creates a UI entry for an action binding.
    /// </summary>
    private void CreateBindingEntry(InputAction action, int bindingIndex, InputBinding binding)
    {
        // Create entry container
        var entry = new GameObject($"Binding_{action.name}_{bindingIndex}");
        entry.transform.SetParent(bindingsContainer, false);
        
        var rect = entry.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 50);
        
        var layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 5, 5);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        
        var bgImage = entry.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        
        // Action name label
        string displayName = binding.isPartOfComposite 
            ? $"  {action.name} ({binding.name})" 
            : action.name;
        
        var nameObj = CreateTextElement(entry.transform, displayName, TextAlignmentOptions.Left);
        var nameLayout = nameObj.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1;
        nameLayout.minWidth = 200;
        
        // Current binding display
        string bindingDisplay = GetBindingDisplayString(action, bindingIndex);
        var bindingObj = CreateTextElement(entry.transform, bindingDisplay, TextAlignmentOptions.Center);
        bindingObj.GetComponent<TextMeshProUGUI>().color = accentColor;
        var bindingLayout = bindingObj.AddComponent<LayoutElement>();
        bindingLayout.minWidth = 150;
        bindingLayout.preferredWidth = 150;
        
        // Rebind button
        var rebindBtn = CreateButton(entry.transform, "Rebind", () => StartRebind(action, bindingIndex));
        var rebindLayout = rebindBtn.AddComponent<LayoutElement>();
        rebindLayout.minWidth = 80;
        rebindLayout.preferredWidth = 80;
        
        bindingEntries.Add(entry);
    }
    
    /// <summary>
    /// Gets a human-readable display string for a binding.
    /// </summary>
    private string GetBindingDisplayString(InputAction action, int bindingIndex)
    {
        return action.GetBindingDisplayString(bindingIndex, 
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }
    
    /// <summary>
    /// Clears all binding entry UI elements.
    /// </summary>
    private void ClearBindingEntries()
    {
        foreach (var entry in bindingEntries)
        {
            if (entry != null)
                Destroy(entry);
        }
        bindingEntries.Clear();
    }
    
    #endregion

    #region Rebinding Logic
    
    /// <summary>
    /// Starts the interactive rebind process for an action.
    /// </summary>
    private void StartRebind(InputAction action, int bindingIndex)
    {
        // Cancel any existing rebind
        CancelRebind();
        
        // Show rebind overlay
        rebindOverlay.SetActive(true);
        rebindPromptText.text = $"Press any key/button for:\n<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{action.name}</color>\n\nPress ESC to cancel";
        
        // Disable the action during rebind
        action.Disable();
        
        // Start the rebind operation
        currentRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsHavingToMatchPath(GetControlPathForScheme(currentControlScheme))
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnPotentialMatch(operation =>
            {
                // Filter out invalid controls
                var control = operation.selectedControl;
                if (control != null)
                {
                    // Check for conflicts
                    string conflictAction = CheckForConflict(action, control.path, bindingIndex);
                    if (!string.IsNullOrEmpty(conflictAction))
                    {
                        // Show conflict warning in prompt
                        rebindPromptText.text = $"Press any key/button for:\n<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{action.name}</color>\n\n<color=yellow>Warning: Already bound to {conflictAction}</color>\nPress again to confirm, ESC to cancel";
                    }
                }
            })
            .OnComplete(operation =>
            {
                OnRebindComplete(action, bindingIndex);
            })
            .OnCancel(operation =>
            {
                OnRebindCancelled(action);
            })
            .Start();
    }
    
    /// <summary>
    /// Gets the control path filter for a control scheme.
    /// </summary>
    private string GetControlPathForScheme(string schemeName)
    {
        switch (schemeName)
        {
            case KEYBOARD_MOUSE_SCHEME:
                return "<Keyboard>|<Mouse>";
            case GAMEPAD_SCHEME:
                return "<Gamepad>";
            default:
                return "";
        }
    }
    
    /// <summary>
    /// Checks if the proposed binding conflicts with existing bindings.
    /// Returns the name of the conflicting action, or null if no conflict.
    /// </summary>
    private string CheckForConflict(InputAction currentAction, string controlPath, int currentBindingIndex)
    {
        var actionMap = inputActions.FindActionMap(actionMapName);
        if (actionMap == null) return null;
        
        foreach (var action in actionMap.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                // Skip the current binding being rebound
                if (action == currentAction && i == currentBindingIndex) continue;
                
                var binding = action.bindings[i];
                
                // Skip if not for current scheme
                if (!IsBindingForScheme(binding, currentControlScheme)) continue;
                
                // Get the effective path (with overrides)
                string effectivePath = binding.effectivePath;
                
                // Check if paths match
                if (effectivePath.Equals(controlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return action.name;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Called when rebinding completes successfully.
    /// </summary>
    private void OnRebindComplete(InputAction action, int bindingIndex)
    {
        CleanupRebindOperation();
        
        // Re-enable the action
        action.Enable();
        
        // Save the new bindings
        SaveBindings();
        
        // Refresh the UI
        ShowBindingsForScheme(currentControlScheme);
        
        Debug.Log($"[RebindControls] Rebound {action.name} to {action.GetBindingDisplayString(bindingIndex)}");
    }
    
    /// <summary>
    /// Called when rebinding is cancelled.
    /// </summary>
    private void OnRebindCancelled(InputAction action)
    {
        CleanupRebindOperation();
        
        // Re-enable the action
        action.Enable();
        
        // Hide overlay
        rebindOverlay.SetActive(false);
        
        Debug.Log($"[RebindControls] Rebind cancelled for {action.name}");
    }
    
    /// <summary>
    /// Cancels any ongoing rebind operation.
    /// </summary>
    private void CancelRebind()
    {
        if (currentRebindOperation != null)
        {
            currentRebindOperation.Cancel();
            CleanupRebindOperation();
        }
        
        if (rebindOverlay != null)
            rebindOverlay.SetActive(false);
    }
    
    /// <summary>
    /// Cleans up the rebind operation resources.
    /// </summary>
    private void CleanupRebindOperation()
    {
        if (currentRebindOperation != null)
        {
            currentRebindOperation.Dispose();
            currentRebindOperation = null;
        }
    }
    
    #endregion

    #region Persistence (JSON Save/Load)
    
    /// <summary>
    /// Saves all binding overrides to PlayerPrefs as JSON.
    /// </summary>
    public void SaveBindings()
    {
        if (inputActions == null) return;
        
        // Serialize all binding overrides to JSON
        string json = inputActions.SaveBindingOverridesAsJson();
        
        // Save to PlayerPrefs
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
        
        Debug.Log("[RebindControls] Bindings saved.");
    }
    
    /// <summary>
    /// Loads binding overrides from PlayerPrefs.
    /// </summary>
    public void LoadBindings()
    {
        if (inputActions == null) return;
        
        // Check if saved data exists
        if (!PlayerPrefs.HasKey(saveKey)) return;
        
        string json = PlayerPrefs.GetString(saveKey);
        
        if (string.IsNullOrEmpty(json)) return;
        
        try
        {
            // Apply the saved overrides
            inputActions.LoadBindingOverridesFromJson(json);
            Debug.Log("[RebindControls] Bindings loaded from save.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RebindControls] Failed to load bindings: {e.Message}");
        }
    }
    
    #endregion

    #region UI Generation
    
    /// <summary>
    /// Dynamically generates the rebind menu UI.
    /// </summary>
    private void GenerateUI()
    {
        // Create Canvas
        var canvasObj = new GameObject("RebindCanvas");
        canvasObj.transform.SetParent(transform);
        rebindCanvas = canvasObj.AddComponent<Canvas>();
        rebindCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rebindCanvas.sortingOrder = 100; // Ensure it's on top
        
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create background panel
        var bgPanel = CreatePanel(rebindCanvas.transform, "BackgroundPanel", backgroundColor);
        SetFullStretch(bgPanel.GetComponent<RectTransform>());
        
        // Create Main Menu Panel
        mainMenuPanel = CreatePanel(rebindCanvas.transform, "MainMenuPanel", new Color(0.1f, 0.1f, 0.1f, 0.95f));
        var mainRect = mainMenuPanel.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(400, 350);
        mainRect.anchoredPosition = Vector2.zero;
        
        var mainLayout = mainMenuPanel.AddComponent<VerticalLayoutGroup>();
        mainLayout.spacing = 20;
        mainLayout.padding = new RectOffset(30, 30, 30, 30);
        mainLayout.childAlignment = TextAnchor.UpperCenter;
        mainLayout.childControlHeight = false;
        mainLayout.childForceExpandHeight = false;
        
        // Main menu title
        var titleObj = CreateTextElement(mainMenuPanel.transform, "CONTROLS", TextAlignmentOptions.Center, 32);
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
        titleObj.AddComponent<LayoutElement>().preferredHeight = 50;
        
        // Keyboard & Mouse button
        var kbmBtn = CreateButton(mainMenuPanel.transform, "Keyboard & Mouse", SelectKeyboardMouse);
        kbmBtn.GetComponent<LayoutElement>().preferredHeight = 50;
        
        // Gamepad button
        var gpBtn = CreateButton(mainMenuPanel.transform, "Gamepad", SelectGamepad);
        gpBtn.GetComponent<LayoutElement>().preferredHeight = 50;
        
        // Spacer
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(mainMenuPanel.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleHeight = 1;
        
        // Close button
        var closeBtn = CreateButton(mainMenuPanel.transform, "Close", CloseMenu);
        closeBtn.GetComponent<LayoutElement>().preferredHeight = 40;
        closeBtn.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f, 1f);
        
        // Create Bindings Panel
        bindingsPanel = CreatePanel(rebindCanvas.transform, "BindingsPanel", new Color(0.1f, 0.1f, 0.1f, 0.95f));
        var bindRect = bindingsPanel.GetComponent<RectTransform>();
        bindRect.anchorMin = new Vector2(0.1f, 0.1f);
        bindRect.anchorMax = new Vector2(0.9f, 0.9f);
        bindRect.offsetMin = Vector2.zero;
        bindRect.offsetMax = Vector2.zero;
        
        var bindLayout = bindingsPanel.AddComponent<VerticalLayoutGroup>();
        bindLayout.spacing = 10;
        bindLayout.padding = new RectOffset(20, 20, 20, 20);
        bindLayout.childAlignment = TextAnchor.UpperCenter;
        bindLayout.childControlHeight = false;
        bindLayout.childForceExpandHeight = false;
        
        // Bindings panel title
        var bindTitleObj = CreateTextElement(bindingsPanel.transform, "Bindings", TextAlignmentOptions.Center, 28);
        bindTitleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
        bindTitleObj.AddComponent<LayoutElement>().preferredHeight = 40;
        bindingsPanelTitle = bindTitleObj.GetComponent<TextMeshProUGUI>();
        
        // Create scroll view for bindings
        var scrollView = CreateScrollView(bindingsPanel.transform);
        bindingsContainer = scrollView.transform.Find("Viewport/Content");
        
        // Button row at bottom
        var buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(bindingsPanel.transform, false);
        var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 20;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandWidth = false;
        var rowLayoutElement = buttonRow.AddComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = 50;
        
        // Reset bindings button
        var resetBtn = CreateButton(buttonRow.transform, "Reset to Default", ResetCurrentSchemeBindings);
        resetBtn.GetComponent<LayoutElement>().preferredWidth = 150;
        resetBtn.GetComponent<LayoutElement>().preferredHeight = 40;
        
        // Back button
        var backBtn = CreateButton(buttonRow.transform, "Back", BackToMainMenu);
        backBtn.GetComponent<LayoutElement>().preferredWidth = 100;
        backBtn.GetComponent<LayoutElement>().preferredHeight = 40;
        
        // Create Rebind Overlay
        rebindOverlay = CreatePanel(rebindCanvas.transform, "RebindOverlay", new Color(0, 0, 0, 0.8f));
        SetFullStretch(rebindOverlay.GetComponent<RectTransform>());
        
        var overlayLayout = rebindOverlay.AddComponent<VerticalLayoutGroup>();
        overlayLayout.spacing = 20;
        overlayLayout.padding = new RectOffset(50, 50, 50, 50);
        overlayLayout.childAlignment = TextAnchor.MiddleCenter;
        overlayLayout.childControlHeight = false;
        overlayLayout.childForceExpandHeight = false;
        
        // Rebind prompt text
        var promptObj = CreateTextElement(rebindOverlay.transform, "Press any key...", TextAlignmentOptions.Center, 24);
        promptObj.AddComponent<LayoutElement>().preferredHeight = 100;
        rebindPromptText = promptObj.GetComponent<TextMeshProUGUI>();
        
        // Cancel button
        var cancelBtn = CreateButton(rebindOverlay.transform, "Cancel", CancelRebind);
        cancelBtn.GetComponent<LayoutElement>().preferredHeight = 40;
        cancelBtn.GetComponent<LayoutElement>().preferredWidth = 120;
        
        rebindOverlay.SetActive(false);
        bindingsPanel.SetActive(false);
    }
    
    /// <summary>
    /// Creates a UI panel with background.
    /// </summary>
    private GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        var rect = panel.AddComponent<RectTransform>();
        var image = panel.AddComponent<Image>();
        image.color = color;
        
        return panel;
    }
    
    /// <summary>
    /// Creates a TextMeshPro text element.
    /// </summary>
    private GameObject CreateTextElement(Transform parent, string text, TextAlignmentOptions alignment, int fontSize = 18)
    {
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);
        
        var rect = textObj.AddComponent<RectTransform>();
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        
        return textObj;
    }
    
    /// <summary>
    /// Creates a UI button with text.
    /// </summary>
    private GameObject CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject($"Button_{text}");
        btnObj.transform.SetParent(parent, false);
        
        var rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 40);
        
        var image = btnObj.AddComponent<Image>();
        image.color = buttonColor;
        
        var button = btnObj.AddComponent<Button>();
        button.targetGraphic = image;
        
        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHighlightColor;
        colors.pressedColor = accentColor;
        colors.selectedColor = buttonHighlightColor;
        button.colors = colors;
        
        button.onClick.AddListener(onClick);
        
        // Add layout element
        var layout = btnObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 40;
        layout.preferredWidth = 200;
        
        // Add text
        var textObj = CreateTextElement(btnObj.transform, text, TextAlignmentOptions.Center);
        SetFullStretch(textObj.GetComponent<RectTransform>());
        
        return btnObj;
    }
    
    /// <summary>
    /// Creates a scroll view for the bindings list.
    /// </summary>
    private GameObject CreateScrollView(Transform parent)
    {
        // ScrollView container
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(parent, false);
        
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        var scrollView = scrollObj.AddComponent<ScrollRect>();
        var scrollLayout = scrollObj.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        scrollLayout.flexibleWidth = 1;
        
        var scrollImage = scrollObj.AddComponent<Image>();
        scrollImage.color = new Color(0.05f, 0.05f, 0.05f, 0.5f);
        
        // Viewport
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        
        var viewRect = viewport.AddComponent<RectTransform>();
        SetFullStretch(viewRect);
        
        var viewMask = viewport.AddComponent<Mask>();
        viewMask.showMaskGraphic = false;
        
        var viewImage = viewport.AddComponent<Image>();
        viewImage.color = Color.white;
        
        // Content
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Configure scroll view
        scrollView.content = contentRect;
        scrollView.viewport = viewRect;
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Elastic;
        scrollView.scrollSensitivity = 30;
        
        return scrollObj;
    }
    
    /// <summary>
    /// Sets a RectTransform to stretch to fill its parent.
    /// </summary>
    private void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
    
    #endregion
}
