using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Entry for individual button sound configuration
/// </summary>
[System.Serializable]
public class ButtonSoundEntry
{
    [Tooltip("The button this configuration applies to")]
    [SerializeField] public Button button;
    
    [Tooltip("The full path/name of the GameObject this button is on")]
    [SerializeField, ReadOnly] public string objectPath = "";
    
    [Tooltip("Whether this button has an existing ClickSoundHandler override")]
    [SerializeField, ReadOnly] public bool hasOverride = false;
    
    [Header("Click Sound Settings")]
    [Tooltip("Sound name to play on click (from SoundEffectManager database)")]
    public string clickSoundName = "";
    
    [Tooltip("Should this play sound on left click?")]
    public bool playOnLeftClick = true;
    
    [Tooltip("Should this play sound on right click?")]
    public bool playOnRightClick = false;
    
    [Header("Hover Sound Settings")]
    [Tooltip("Enable hover sound effects")]
    public bool enableHoverSounds = true;
    
    [Tooltip("Sound name to play on mouse enter (from SoundEffectManager database)")]
    public string hoverEnterSoundName = "";
    
    [Tooltip("Sound name to play on mouse exit (from SoundEffectManager database)")]
    public string hoverExitSoundName = "";
    
    [Header("Override Settings")]
    [Tooltip("Apply these settings even if the button has a ClickSoundHandler override")]
    public bool forceOverrideExisting = false;
    
    [Tooltip("Should this button be enabled for sound effects?")]
    public bool enableSounds = true;
    
    // Runtime tracking
    [System.NonSerialized] public ClickSoundHandler appliedClickHandler;
    [System.NonSerialized] public bool needsUpdate = false;
}

/// <summary>
/// Master controller for managing button sound effects across the entire scene.
/// Automatically discovers all buttons and provides a centralized interface for assigning sounds.
/// Attach to: A master GameObject in the scene (like a UI Manager).
/// Dependencies: SoundEffectManager (optional), ClickSoundHandler prefab/component
/// </summary>
public class ButtonSoundHandler : MonoBehaviour
{
    [Header("Button Discovery")]
    [Tooltip("Automatically scan for buttons on start")]
    [SerializeField] private bool autoScanOnStart = true;
    
    [Tooltip("Include disabled buttons in the scan")]
    [SerializeField] private bool includeDisabledButtons = true;
    
    [Tooltip("Include buttons that are children of disabled GameObjects")]
    [SerializeField] private bool includeButtonsOnDisabledObjects = false;
    
    [Header("Global Default Settings")]
    [Tooltip("Default click sound for all buttons (if not individually configured)")]
    [SerializeField] private string globalDefaultClickSound = "";
    
    [Tooltip("Default hover enter sound for all buttons (if not individually configured)")]
    [SerializeField] private string globalDefaultHoverEnterSound = "";
    
    [Tooltip("Default hover exit sound for all buttons (if not individually configured)")]
    [SerializeField] private string globalDefaultHoverExitSound = "";
    
    [Tooltip("Enable hover sounds by default")]
    [SerializeField] private bool globalEnableHoverSounds = true;
    
    [Header("Button Sound Database")]
    [Tooltip("All discovered buttons and their sound configurations")]
    [SerializeField] private ButtonSoundEntry[] buttonSounds = new ButtonSoundEntry[0];
    
    [Header("Management Settings")]
    [Tooltip("Automatically remove null/destroyed button references")]
    [SerializeField] private bool autoCleanupNullButtons = true;
    
    [Tooltip("Sort buttons alphabetically by their object path")]
    [SerializeField] private bool sortButtonsAlphabetically = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Runtime lookups
    private Dictionary<Button, ButtonSoundEntry> buttonLookup;
    private List<Button> lastScanResults;
    
    // Stats for inspector display
    [Header("Statistics (Read-Only)")]
    [SerializeField, ReadOnly] private int totalButtonsFound = 0;
    [SerializeField, ReadOnly] private int buttonsWithOverrides = 0;
    [SerializeField, ReadOnly] private int buttonsWithSounds = 0;
    [SerializeField, ReadOnly] private int buttonsNeedingUpdate = 0;
    
    private void Awake()
    {
        InitializeLookup();
    }
    
    private void Start()
    {
        if (autoScanOnStart)
        {
            ScanForButtons();
            ApplyAllSoundSettings();
        }
    }
    
    private void OnValidate()
    {
        // Update statistics when inspector values change
        UpdateStatistics();
    }
    
    /// <summary>
    /// Initialize the button lookup dictionary
    /// </summary>
    private void InitializeLookup()
    {
        buttonLookup = new Dictionary<Button, ButtonSoundEntry>();
        lastScanResults = new List<Button>();
        
        foreach (var entry in buttonSounds)
        {
            if (entry.button != null)
            {
                buttonLookup[entry.button] = entry;
            }
        }
    }
    
    /// <summary>
    /// Scan the scene for all buttons and update the database
    /// </summary>
    [ContextMenu("Scan For Buttons")]
    public void ScanForButtons()
    {
        if (debugMode)
        {
            Debug.Log("ButtonSoundHandler: Starting button scan...");
        }
        
        // Find all buttons in the scene
        Button[] allButtons = FindObjectsOfType<Button>(includeButtonsOnDisabledObjects);
        
        List<Button> validButtons = new List<Button>();
        
        foreach (var button in allButtons)
        {
            // Check if we should include this button
            if (!includeDisabledButtons && !button.gameObject.activeInHierarchy)
            {
                continue;
            }
            
            validButtons.Add(button);
        }
        
        lastScanResults = validButtons;
        
        // Update the button database
        UpdateButtonDatabase(validButtons);
        
        if (debugMode)
        {
            /* Debug.Log($"ButtonSoundHandler: Found {validButtons.Count} buttons in scene"); */
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Update the button database with newly found buttons
    /// </summary>
    private void UpdateButtonDatabase(List<Button> foundButtons)
    {
        List<ButtonSoundEntry> newEntries = new List<ButtonSoundEntry>();
        
        // Keep existing entries for buttons that still exist
        foreach (var existingEntry in buttonSounds)
        {
            if (existingEntry.button != null && foundButtons.Contains(existingEntry.button))
            {
                // Update the entry's metadata
                UpdateButtonEntryMetadata(existingEntry);
                newEntries.Add(existingEntry);
            }
            else if (!autoCleanupNullButtons && existingEntry.button == null)
            {
                // Keep null entries if auto cleanup is disabled
                newEntries.Add(existingEntry);
            }
        }
        
        // Add new entries for buttons not already in the database
        foreach (var button in foundButtons)
        {
            bool alreadyExists = newEntries.Any(entry => entry.button == button);
            if (!alreadyExists)
            {
                ButtonSoundEntry newEntry = CreateButtonSoundEntry(button);
                newEntries.Add(newEntry);
            }
        }
        
        // Sort if requested
        if (sortButtonsAlphabetically)
        {
            newEntries = newEntries.OrderBy(entry => entry.objectPath).ToList();
        }
        
        buttonSounds = newEntries.ToArray();
        
        // Rebuild lookup
        InitializeLookup();
    }
    
    /// <summary>
    /// Create a new button sound entry for a discovered button
    /// </summary>
    private ButtonSoundEntry CreateButtonSoundEntry(Button button)
    {
        ButtonSoundEntry entry = new ButtonSoundEntry
        {
            button = button,
            clickSoundName = globalDefaultClickSound,
            hoverEnterSoundName = globalDefaultHoverEnterSound,
            hoverExitSoundName = globalDefaultHoverExitSound,
            enableHoverSounds = globalEnableHoverSounds,
            playOnLeftClick = true,
            playOnRightClick = false,
            enableSounds = true,
            needsUpdate = true
        };
        
        UpdateButtonEntryMetadata(entry);
        
        return entry;
    }
    
    /// <summary>
    /// Update metadata for a button entry (path, override status, etc.)
    /// </summary>
    private void UpdateButtonEntryMetadata(ButtonSoundEntry entry)
    {
        if (entry.button != null)
        {
            // Generate object path
            entry.objectPath = GetGameObjectPath(entry.button.gameObject);
            
            // Check for existing ClickSoundHandler override
            ClickSoundHandler existingHandler = entry.button.GetComponent<ClickSoundHandler>();
            entry.hasOverride = existingHandler != null;
            
            if (debugMode && entry.hasOverride)
            {
                Debug.Log($"ButtonSoundHandler: Button '{entry.objectPath}' has existing ClickSoundHandler override");
            }
        }
    }
    
    /// <summary>
    /// Get the full path of a GameObject in the hierarchy
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    /// <summary>
    /// Apply sound settings to all buttons
    /// </summary>
    [ContextMenu("Apply All Sound Settings")]
    public void ApplyAllSoundSettings()
    {
        if (debugMode)
        {
            Debug.Log("ButtonSoundHandler: Applying sound settings to all buttons...");
        }
        
        int appliedCount = 0;
        int skippedCount = 0;
        
        foreach (var entry in buttonSounds)
        {
            if (ApplySoundSettingsToButton(entry))
            {
                appliedCount++;
            }
            else
            {
                skippedCount++;
            }
        }
        
        if (debugMode)
        {
            /* Debug.Log($"ButtonSoundHandler: Applied settings to {appliedCount} buttons, skipped {skippedCount}"); */
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Apply sound settings to a specific button
    /// </summary>
    private bool ApplySoundSettingsToButton(ButtonSoundEntry entry)
    {
        if (entry.button == null || !entry.enableSounds)
        {
            return false;
        }
        
        // Check if button has override and we shouldn't force
        if (entry.hasOverride && !entry.forceOverrideExisting)
        {
            if (debugMode)
            {
                Debug.Log($"ButtonSoundHandler: Skipping '{entry.objectPath}' - has override and force is disabled");
            }
            return false;
        }
        
        // Get or create ClickSoundHandler
        ClickSoundHandler clickHandler = entry.button.GetComponent<ClickSoundHandler>();
        if (clickHandler == null)
        {
            clickHandler = entry.button.gameObject.AddComponent<ClickSoundHandler>();
        }
        
        // Apply click sound (use entry value, fallback to global default, then empty)
        string clickSound = !string.IsNullOrEmpty(entry.clickSoundName) ? entry.clickSoundName : globalDefaultClickSound;
        if (!string.IsNullOrEmpty(clickSound))
        {
            clickHandler.SetClickSoundName(clickSound);
            if (debugMode)
            {
                /* Debug.Log($"ButtonSoundHandler: Set click sound '{clickSound}' for '{entry.objectPath}'"); */
            }
        }
        
        // Apply hover enter sound (use entry value, fallback to global default, then empty)
        string hoverEnterSound = !string.IsNullOrEmpty(entry.hoverEnterSoundName) ? entry.hoverEnterSoundName : globalDefaultHoverEnterSound;
        if (!string.IsNullOrEmpty(hoverEnterSound))
        {
            clickHandler.SetHoverEnterSoundName(hoverEnterSound);
            if (debugMode)
            {
                /* Debug.Log($"ButtonSoundHandler: Set hover enter sound '{hoverEnterSound}' for '{entry.objectPath}'"); */
            }
        }
        
        // Apply hover exit sound (use entry value, fallback to global default, then empty)
        string hoverExitSound = !string.IsNullOrEmpty(entry.hoverExitSoundName) ? entry.hoverExitSoundName : globalDefaultHoverExitSound;
        if (!string.IsNullOrEmpty(hoverExitSound))
        {
            clickHandler.SetHoverExitSoundName(hoverExitSound);
            if (debugMode)
            {
                /* Debug.Log($"ButtonSoundHandler: Set hover exit sound '{hoverExitSound}' for '{entry.objectPath}'"); */
            }
        }
        
        // Apply other settings
        clickHandler.SetHoverSoundsEnabled(entry.enableHoverSounds);
        
        // Store reference
        entry.appliedClickHandler = clickHandler;
        entry.needsUpdate = false;
        
        if (debugMode)
        {
            /* Debug.Log($"ButtonSoundHandler: Applied sound settings to '{entry.objectPath}' - Click: '{clickSound}', HoverEnter: '{hoverEnterSound}', HoverExit: '{hoverExitSound}'"); */
        }
        
        return true;
    }
    
    /// <summary>
    /// Update statistics for inspector display
    /// </summary>
    private void UpdateStatistics()
    {
        totalButtonsFound = buttonSounds.Length;
        buttonsWithOverrides = buttonSounds.Count(entry => entry.hasOverride);
        buttonsWithSounds = buttonSounds.Count(entry => entry.enableSounds && 
            (!string.IsNullOrEmpty(entry.clickSoundName) || !string.IsNullOrEmpty(entry.hoverEnterSoundName)));
        buttonsNeedingUpdate = buttonSounds.Count(entry => entry.needsUpdate);
    }
    
    /// <summary>
    /// Remove all ClickSoundHandler components from managed buttons
    /// </summary>
    [ContextMenu("Remove All Sound Handlers")]
    public void RemoveAllSoundHandlers()
    {
        if (debugMode)
        {
            Debug.Log("ButtonSoundHandler: Removing all ClickSoundHandler components...");
        }
        
        int removedCount = 0;
        
        foreach (var entry in buttonSounds)
        {
            if (entry.button != null)
            {
                ClickSoundHandler existingHandler = entry.button.GetComponent<ClickSoundHandler>();
                if (existingHandler != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(existingHandler);
                    }
                    else
                    {
                        #if UNITY_EDITOR
                        DestroyImmediate(existingHandler);
                        #endif
                    }
                    removedCount++;
                }
                
                entry.appliedClickHandler = null;
                entry.hasOverride = false;
                entry.needsUpdate = true;
            }
        }
        
        if (debugMode)
        {
            /* Debug.Log($"ButtonSoundHandler: Removed {removedCount} ClickSoundHandler components"); */
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Mark all buttons as needing updates
    /// </summary>
    [ContextMenu("Mark All For Update")]
    public void MarkAllForUpdate()
    {
        foreach (var entry in buttonSounds)
        {
            entry.needsUpdate = true;
        }
        
        UpdateStatistics();
    }
    
    #region Public API
    
    /// <summary>
    /// Get the sound entry for a specific button
    /// </summary>
    public ButtonSoundEntry GetButtonSoundEntry(Button button)
    {
        buttonLookup.TryGetValue(button, out ButtonSoundEntry entry);
        return entry;
    }
    
    /// <summary>
    /// Set the global default click sound
    /// </summary>
    public void SetGlobalDefaultClickSound(string soundName)
    {
        globalDefaultClickSound = soundName;
    }
    
    /// <summary>
    /// Set the global default hover enter sound
    /// </summary>
    public void SetGlobalDefaultHoverEnterSound(string soundName)
    {
        globalDefaultHoverEnterSound = soundName;
    }
    
    /// <summary>
    /// Apply sound settings to a specific button by reference
    /// </summary>
    public bool ApplySoundSettingsToButton(Button button)
    {
        if (buttonLookup.TryGetValue(button, out ButtonSoundEntry entry))
        {
            return ApplySoundSettingsToButton(entry);
        }
        
        return false;
    }
    
    /// <summary>
    /// Get list of all managed buttons
    /// </summary>
    public Button[] GetAllManagedButtons()
    {
        return buttonSounds.Where(entry => entry.button != null).Select(entry => entry.button).ToArray();
    }
    
    /// <summary>
    /// Get list of buttons that have overrides
    /// </summary>
    public Button[] GetButtonsWithOverrides()
    {
        return buttonSounds.Where(entry => entry.button != null && entry.hasOverride).Select(entry => entry.button).ToArray();
    }
    
    /// <summary>
    /// Get list of buttons that need updates
    /// </summary>
    public Button[] GetButtonsNeedingUpdate()
    {
        return buttonSounds.Where(entry => entry.button != null && entry.needsUpdate).Select(entry => entry.button).ToArray();
    }
    
    #endregion
}

#if UNITY_EDITOR
/// <summary>
/// Custom editor for ButtonSoundHandler with helpful buttons and improved layout
/// </summary>
[CustomEditor(typeof(ButtonSoundHandler))]
public class ButtonSoundHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ButtonSoundHandler handler = (ButtonSoundHandler)target;
        
        // Default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan For Buttons"))
        {
            handler.ScanForButtons();
            EditorUtility.SetDirty(handler);
        }
        
        if (GUILayout.Button("Apply All Settings"))
        {
            handler.ApplyAllSoundSettings();
            EditorUtility.SetDirty(handler);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mark All For Update"))
        {
            handler.MarkAllForUpdate();
            EditorUtility.SetDirty(handler);
        }
        
        if (GUILayout.Button("Remove All Handlers"))
        {
            if (EditorUtility.DisplayDialog("Remove All Sound Handlers", 
                "This will remove all ClickSoundHandler components from managed buttons. This cannot be undone.", 
                "Remove", "Cancel"))
            {
                handler.RemoveAllSoundHandlers();
                EditorUtility.SetDirty(handler);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Show helpful information
        EditorGUILayout.LabelField("Button Overview", EditorStyles.boldLabel);
        
        Button[] buttonsWithOverrides = handler.GetButtonsWithOverrides();
        Button[] buttonsNeedingUpdate = handler.GetButtonsNeedingUpdate();
        
        if (buttonsWithOverrides.Length > 0)
        {
            EditorGUILayout.HelpBox($"{buttonsWithOverrides.Length} button(s) have existing ClickSoundHandler overrides. " +
                "Enable 'Force Override Existing' to apply settings to these buttons.", MessageType.Info);
        }
        
        if (buttonsNeedingUpdate.Length > 0)
        {
            EditorGUILayout.HelpBox($"{buttonsNeedingUpdate.Length} button(s) are marked as needing updates. " +
                "Click 'Apply All Settings' to update them.", MessageType.Warning);
        }
        
        if (handler.GetAllManagedButtons().Length == 0)
        {
            EditorGUILayout.HelpBox("No buttons found. Click 'Scan For Buttons' to discover buttons in the scene.", MessageType.Warning);
        }
    }
}
#endif 