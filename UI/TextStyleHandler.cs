using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Entry for individual TextMeshProUGUI style configuration
/// </summary>
[System.Serializable]
public class TextStyleEntry
{
    [Tooltip("The TextMeshProUGUI component this configuration applies to")]
    [SerializeField] public TextMeshProUGUI textComponent;
    
    [Tooltip("The full path/name of the GameObject this text is on")]
    [SerializeField, ReadOnly] public string objectPath = "";
    
    [Tooltip("Whether this text has an existing ReliableMenuText override")]
    [SerializeField, ReadOnly] public bool hasReliableMenuText = false;
    
    [Header("Text Style Settings")]
    [Tooltip("Font to apply to this text")]
    public TMP_FontAsset font;
    
    [Tooltip("Font size")]
    public float fontSize = 24f;
    
    [Tooltip("Main text color (used if gradient is disabled)")]
    public Color textColor = Color.white;
    
    [Header("Gradient Settings")]
    [Tooltip("Enable gradient coloring")]
    public bool useGradient = false;
    
    [Tooltip("Top left gradient color")]
    public Color gradientTopLeft = Color.white;
    
    [Tooltip("Top right gradient color")]
    public Color gradientTopRight = Color.white;
    
    [Tooltip("Bottom left gradient color")]
    public Color gradientBottomLeft = Color.gray;
    
    [Tooltip("Bottom right gradient color")]
    public Color gradientBottomRight = Color.gray;
    
    [Header("Text Alignment")]
    [Tooltip("Text alignment")]
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    
    [Header("Override Settings")]
    [Tooltip("Apply these settings even if the text has a ReliableMenuText override")]
    public bool forceOverrideExisting = false;
    
    [Tooltip("Should this text be styled?")]
    public bool enableStyling = true;
    
    [Tooltip("Apply a preset style")]
    public string presetName = "";
    
    [Header("ReliableMenuText Settings")]
    [Tooltip("Add ReliableMenuText component for interactive effects")]
    public bool addReliableMenuText = false;
    
    [Tooltip("ReliableMenuText style index to apply (0-4)")]
    public int reliableMenuTextStyleIndex = 0;
    
    [Tooltip("Enable hover effects on ReliableMenuText")]
    public bool enableReliableMenuTextHover = true;
    
    [Tooltip("Enable sparkles on ReliableMenuText")]
    public bool enableReliableMenuTextSparkles = true;
    
    [Tooltip("Enable breathing effect on ReliableMenuText")]
    public bool enableReliableMenuTextBreathing = false;
    
    // Runtime tracking
    [System.NonSerialized] public bool needsUpdate = false;
    [System.NonSerialized] public ReliableMenuText appliedReliableMenuText;
}

/// <summary>
/// Predefined text style preset
/// </summary>
[System.Serializable]
public class TextStylePreset
{
    [Header("Preset Info")]
    public string presetName = "Default";
    
    [Header("Basic Properties")]
    public TMP_FontAsset font;
    public float fontSize = 24f;
    public Color textColor = Color.white;
    
    [Header("Gradient Settings")]
    public bool useGradient = false;
    public Color gradientTopLeft = Color.white;
    public Color gradientTopRight = Color.white;
    public Color gradientBottomLeft = Color.gray;
    public Color gradientBottomRight = Color.gray;
    
    [Header("Alignment")]
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
}

/// <summary>
/// Master controller for managing TextMeshProUGUI styles across the entire scene.
/// Automatically discovers all TextMeshProUGUI components and provides a centralized interface for styling.
/// Attach to: A master GameObject in the scene (like a UI Manager).
/// Dependencies: TextMeshPro
/// </summary>
public class TextStyleHandler : MonoBehaviour
{
    [Header("Text Discovery")]
    [Tooltip("Automatically scan for TextMeshProUGUI components on start")]
    [SerializeField] private bool autoScanOnStart = true;
    
    [Tooltip("Include disabled text components in the scan")]
    [SerializeField] private bool includeDisabledText = true;
    
    [Tooltip("Include text components that are children of disabled GameObjects")]
    [SerializeField] private bool includeTextOnDisabledObjects = false;
    
    [Header("Global Default Settings")]
    [Tooltip("Default font for all text (if not individually configured)")]
    [SerializeField] private TMP_FontAsset globalDefaultFont;
    
    [Tooltip("Default font size for all text")]
    [SerializeField] private float globalDefaultFontSize = 24f;
    
    [Tooltip("Default text color")]
    [SerializeField] private Color globalDefaultColor = Color.white;
    
    [Tooltip("Default text alignment")]
    [SerializeField] private TextAlignmentOptions globalDefaultAlignment = TextAlignmentOptions.Center;
    
    [Header("Style Presets")]
    [Tooltip("Predefined style presets that can be applied")]
    [SerializeField] private TextStylePreset[] stylePresets = new TextStylePreset[0];
    
    [Header("Text Style Database")]
    [Tooltip("All discovered text components and their style configurations")]
    [SerializeField] private TextStyleEntry[] textStyles = new TextStyleEntry[0];
    
    [Header("Management Settings")]
    [Tooltip("Automatically remove null/destroyed text references")]
    [SerializeField] private bool autoCleanupNullText = true;
    
    [Tooltip("Sort text components alphabetically by their object path")]
    [SerializeField] private bool sortTextAlphabetically = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Runtime lookups
    private Dictionary<TextMeshProUGUI, TextStyleEntry> textLookup;
    private Dictionary<string, TextStylePreset> presetLookup;
    private List<TextMeshProUGUI> lastScanResults;
    
    // Stats for inspector display
    [Header("Statistics (Read-Only)")]
    [SerializeField, ReadOnly] private int totalTextFound = 0;
    [SerializeField, ReadOnly] private int textWithReliableMenuText = 0;
    [SerializeField, ReadOnly] private int textWithStyling = 0;
    [SerializeField, ReadOnly] private int textNeedingUpdate = 0;
    
    private void Awake()
    {
        InitializeLookup();
        SetupDefaultPresets();
    }
    
    private void Start()
    {
        if (autoScanOnStart)
        {
            ScanForText();
            ApplyAllStyles();
        }
    }
    
    private void OnValidate()
    {
        // Update statistics when inspector values change
        UpdateStatistics();
        
        // Rebuild preset lookup
        InitializePresetLookup();
    }
    
    /// <summary>
    /// Initialize the text lookup dictionary
    /// </summary>
    private void InitializeLookup()
    {
        textLookup = new Dictionary<TextMeshProUGUI, TextStyleEntry>();
        lastScanResults = new List<TextMeshProUGUI>();
        
        foreach (var entry in textStyles)
        {
            if (entry.textComponent != null)
            {
                textLookup[entry.textComponent] = entry;
            }
        }
        
        InitializePresetLookup();
    }
    
    /// <summary>
    /// Initialize the preset lookup dictionary
    /// </summary>
    private void InitializePresetLookup()
    {
        presetLookup = new Dictionary<string, TextStylePreset>();
        
        foreach (var preset in stylePresets)
        {
            if (!string.IsNullOrEmpty(preset.presetName))
            {
                presetLookup[preset.presetName] = preset;
            }
        }
    }
    
    /// <summary>
    /// Setup default style presets
    /// </summary>
    private void SetupDefaultPresets()
    {
        if (stylePresets == null || stylePresets.Length == 0)
        {
            stylePresets = new TextStylePreset[4];
            
            // Default preset
            stylePresets[0] = new TextStylePreset
            {
                presetName = "Default",
                fontSize = 24f,
                textColor = Color.white,
                useGradient = false,
                alignment = TextAlignmentOptions.Center
            };
            
            // Eggshell + Gold preset
            stylePresets[1] = new TextStylePreset
            {
                presetName = "Eggshell Gold",
                fontSize = 24f,
                textColor = new Color(0.96f, 0.94f, 0.87f, 1f), // Eggshell base
                useGradient = true,
                gradientTopLeft = new Color(0.98f, 0.96f, 0.89f, 1f), // Light eggshell
                gradientTopRight = new Color(0.98f, 0.96f, 0.89f, 1f),
                gradientBottomLeft = new Color(1f, 0.84f, 0.4f, 1f), // Gold
                gradientBottomRight = new Color(1f, 0.84f, 0.4f, 1f),
                alignment = TextAlignmentOptions.Center
            };
            
            // Professional Blue preset
            stylePresets[2] = new TextStylePreset
            {
                presetName = "Professional Blue",
                fontSize = 24f,
                textColor = new Color(0.2f, 0.4f, 0.8f, 1f),
                useGradient = true,
                gradientTopLeft = new Color(0.3f, 0.6f, 1f, 1f),
                gradientTopRight = new Color(0.3f, 0.6f, 1f, 1f),
                gradientBottomLeft = new Color(0.1f, 0.2f, 0.6f, 1f),
                gradientBottomRight = new Color(0.1f, 0.2f, 0.6f, 1f),
                alignment = TextAlignmentOptions.Center
            };
            
            // Combat Red preset
            stylePresets[3] = new TextStylePreset
            {
                presetName = "Combat Red",
                fontSize = 26f,
                textColor = new Color(0.9f, 0.2f, 0.2f, 1f),
                useGradient = true,
                gradientTopLeft = new Color(1f, 0.4f, 0.4f, 1f),
                gradientTopRight = new Color(1f, 0.4f, 0.4f, 1f),
                gradientBottomLeft = new Color(0.7f, 0.1f, 0.1f, 1f),
                gradientBottomRight = new Color(0.7f, 0.1f, 0.1f, 1f),
                alignment = TextAlignmentOptions.Center
            };
            
            InitializePresetLookup();
        }
    }
    
    /// <summary>
    /// Scan the scene for all TextMeshProUGUI components and update the database
    /// </summary>
    [ContextMenu("Scan For Text")]
    public void ScanForText()
    {
        if (debugMode)
        {
            Debug.Log("TextStyleHandler: Starting text scan...");
        }
        
        // Find all TextMeshProUGUI components in the scene
        TextMeshProUGUI[] allText = FindObjectsOfType<TextMeshProUGUI>(includeTextOnDisabledObjects);
        
        List<TextMeshProUGUI> validText = new List<TextMeshProUGUI>();
        
        foreach (var text in allText)
        {
            // Check if we should include this text
            if (!includeDisabledText && !text.gameObject.activeInHierarchy)
            {
                continue;
            }
            
            validText.Add(text);
        }
        
        lastScanResults = validText;
        
        // Update the text database
        UpdateTextDatabase(validText);
        
        if (debugMode)
        {
            /* Debug.Log($"TextStyleHandler: Found {validText.Count} TextMeshProUGUI components in scene"); */
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Update the text database with newly found text components
    /// </summary>
    private void UpdateTextDatabase(List<TextMeshProUGUI> foundText)
    {
        List<TextStyleEntry> newEntries = new List<TextStyleEntry>();
        
        // Keep existing entries for text that still exists
        foreach (var existingEntry in textStyles)
        {
            if (existingEntry.textComponent != null && foundText.Contains(existingEntry.textComponent))
            {
                // Update the entry's metadata
                UpdateTextEntryMetadata(existingEntry);
                newEntries.Add(existingEntry);
            }
            else if (!autoCleanupNullText && existingEntry.textComponent == null)
            {
                // Keep null entries if auto cleanup is disabled
                newEntries.Add(existingEntry);
            }
        }
        
        // Add new entries for text not already in the database
        foreach (var text in foundText)
        {
            bool alreadyExists = newEntries.Any(entry => entry.textComponent == text);
            if (!alreadyExists)
            {
                TextStyleEntry newEntry = CreateTextStyleEntry(text);
                newEntries.Add(newEntry);
            }
        }
        
        // Sort if requested
        if (sortTextAlphabetically)
        {
            newEntries = newEntries.OrderBy(entry => entry.objectPath).ToList();
        }
        
        textStyles = newEntries.ToArray();
        
        // Rebuild lookup
        InitializeLookup();
    }
    
    /// <summary>
    /// Create a new text style entry for a discovered text component
    /// </summary>
    private TextStyleEntry CreateTextStyleEntry(TextMeshProUGUI text)
    {
        TextStyleEntry entry = new TextStyleEntry
        {
            textComponent = text,
            font = globalDefaultFont,
            fontSize = globalDefaultFontSize,
            textColor = globalDefaultColor,
            alignment = globalDefaultAlignment,
            useGradient = false,
            gradientTopLeft = Color.white,
            gradientTopRight = Color.white,
            gradientBottomLeft = Color.gray,
            gradientBottomRight = Color.gray,
            enableStyling = true,
            needsUpdate = true
        };
        
        UpdateTextEntryMetadata(entry);
        
        return entry;
    }
    
    /// <summary>
    /// Update metadata for a text entry (path, override status, etc.)
    /// </summary>
    private void UpdateTextEntryMetadata(TextStyleEntry entry)
    {
        if (entry.textComponent != null)
        {
            // Generate object path
            entry.objectPath = GetGameObjectPath(entry.textComponent.gameObject);
            
            // Check for existing ReliableMenuText override
            ReliableMenuText existingReliableText = entry.textComponent.GetComponent<ReliableMenuText>();
            entry.hasReliableMenuText = existingReliableText != null;
            
            if (debugMode && entry.hasReliableMenuText)
            {
                Debug.Log($"TextStyleHandler: Text '{entry.objectPath}' has existing ReliableMenuText override");
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
    /// Apply styles to all text components
    /// </summary>
    [ContextMenu("Apply All Styles")]
    public void ApplyAllStyles()
    {
        if (debugMode)
        {
            Debug.Log("TextStyleHandler: Applying styles to all text components...");
        }
        
        int appliedCount = 0;
        int skippedCount = 0;
        
        foreach (var entry in textStyles)
        {
            if (ApplyStyleToText(entry))
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
            /* Debug.Log($"TextStyleHandler: Applied styles to {appliedCount} text components, skipped {skippedCount}"); */
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Apply style to a specific text component
    /// </summary>
    private bool ApplyStyleToText(TextStyleEntry entry)
    {
        if (entry.textComponent == null || !entry.enableStyling)
        {
            return false;
        }
        
        // Check if text has override and we shouldn't force
        if (entry.hasReliableMenuText && !entry.forceOverrideExisting)
        {
            if (debugMode)
            {
                Debug.Log($"TextStyleHandler: Skipping '{entry.objectPath}' - has ReliableMenuText and force is disabled");
            }
            return false;
        }
        
        // Apply preset if specified
        if (!string.IsNullOrEmpty(entry.presetName) && presetLookup.ContainsKey(entry.presetName))
        {
            ApplyPresetToEntry(entry, presetLookup[entry.presetName]);
        }
        
        // Apply font if specified
        if (entry.font != null)
        {
            entry.textComponent.font = entry.font;
        }
        else if (globalDefaultFont != null)
        {
            entry.textComponent.font = globalDefaultFont;
        }
        
        // Apply font size
        entry.textComponent.fontSize = entry.fontSize;
        
        // Apply gradient or solid color
        if (entry.useGradient)
        {
            VertexGradient gradient = new VertexGradient();
            gradient.topLeft = entry.gradientTopLeft;
            gradient.topRight = entry.gradientTopRight;
            gradient.bottomLeft = entry.gradientBottomLeft;
            gradient.bottomRight = entry.gradientBottomRight;
            entry.textComponent.colorGradient = gradient;
            entry.textComponent.enableVertexGradient = true;
        }
        else
        {
            entry.textComponent.enableVertexGradient = false;
            entry.textComponent.color = entry.textColor;
        }
        
        // Apply alignment
        entry.textComponent.alignment = entry.alignment;
        
        // Handle ReliableMenuText component
        if (entry.addReliableMenuText)
        {
            ReliableMenuText reliableMenuText = entry.textComponent.GetComponent<ReliableMenuText>();
            if (reliableMenuText == null)
            {
                reliableMenuText = entry.textComponent.gameObject.AddComponent<ReliableMenuText>();
            }
            
            // Configure ReliableMenuText settings
            reliableMenuText.EnableHoverEffects = entry.enableReliableMenuTextHover;
            reliableMenuText.EnableSparkles = entry.enableReliableMenuTextSparkles;
            reliableMenuText.EnableBreathingEffect = entry.enableReliableMenuTextBreathing;
            
            // Set the style index if valid
            if (entry.reliableMenuTextStyleIndex >= 0 && entry.reliableMenuTextStyleIndex < 5)
            {
                reliableMenuText.SetStyle(entry.reliableMenuTextStyleIndex);
            }
            
            entry.appliedReliableMenuText = reliableMenuText;
            
            if (debugMode)
            {
                /* Debug.Log($"TextStyleHandler: Applied ReliableMenuText (style {entry.reliableMenuTextStyleIndex}) to '{entry.objectPath}'"); */
            }
        }
        
        entry.needsUpdate = false;
        
        if (debugMode)
        {
            string styleInfo = entry.useGradient ? "Gradient" : entry.textColor.ToString();
            string reliableInfo = entry.addReliableMenuText ? " + ReliableMenuText" : "";
            /* Debug.Log($"TextStyleHandler: Applied style to '{entry.objectPath}' - Size: {entry.fontSize}, Style: {styleInfo}{reliableInfo}"); */
        }
        
        return true;
    }
    
    /// <summary>
    /// Apply a preset to an entry
    /// </summary>
    private void ApplyPresetToEntry(TextStyleEntry entry, TextStylePreset preset)
    {
        entry.font = preset.font;
        entry.fontSize = preset.fontSize;
        entry.textColor = preset.textColor;
        entry.useGradient = preset.useGradient;
        entry.gradientTopLeft = preset.gradientTopLeft;
        entry.gradientTopRight = preset.gradientTopRight;
        entry.gradientBottomLeft = preset.gradientBottomLeft;
        entry.gradientBottomRight = preset.gradientBottomRight;
        entry.alignment = preset.alignment;
    }
    
    /// <summary>
    /// Update statistics for inspector display
    /// </summary>
    private void UpdateStatistics()
    {
        totalTextFound = textStyles.Length;
        textWithReliableMenuText = textStyles.Count(entry => entry.hasReliableMenuText);
        textWithStyling = textStyles.Count(entry => entry.enableStyling);
        textNeedingUpdate = textStyles.Count(entry => entry.needsUpdate);
    }
    
    /// <summary>
    /// Mark all text as needing updates
    /// </summary>
    [ContextMenu("Mark All For Update")]
    public void MarkAllForUpdate()
    {
        foreach (var entry in textStyles)
        {
            entry.needsUpdate = true;
        }
        
        UpdateStatistics();
    }
    
    #region Public API
    
    /// <summary>
    /// Get the style entry for a specific text component
    /// </summary>
    public TextStyleEntry GetTextStyleEntry(TextMeshProUGUI text)
    {
        textLookup.TryGetValue(text, out TextStyleEntry entry);
        return entry;
    }
    
    /// <summary>
    /// Apply a preset to all text components
    /// </summary>
    public void ApplyPresetToAll(string presetName)
    {
        if (!presetLookup.ContainsKey(presetName))
        {
            Debug.LogWarning($"TextStyleHandler: Preset '{presetName}' not found");
            return;
        }
        
        foreach (var entry in textStyles)
        {
            entry.presetName = presetName;
            entry.needsUpdate = true;
        }
        
        ApplyAllStyles();
    }
    
    /// <summary>
    /// Apply style to a specific text component by reference
    /// </summary>
    public bool ApplyStyleToText(TextMeshProUGUI text)
    {
        if (textLookup.TryGetValue(text, out TextStyleEntry entry))
        {
            return ApplyStyleToText(entry);
        }
        
        return false;
    }
    
    /// <summary>
    /// Get list of all managed text components
    /// </summary>
    public TextMeshProUGUI[] GetAllManagedText()
    {
        return textStyles.Where(entry => entry.textComponent != null).Select(entry => entry.textComponent).ToArray();
    }
    
    /// <summary>
    /// Get list of text components that have ReliableMenuText
    /// </summary>
    public TextMeshProUGUI[] GetTextWithReliableMenuText()
    {
        return textStyles.Where(entry => entry.textComponent != null && entry.hasReliableMenuText).Select(entry => entry.textComponent).ToArray();
    }
    
    /// <summary>
    /// Get list of text components that need updates
    /// </summary>
    public TextMeshProUGUI[] GetTextNeedingUpdate()
    {
        return textStyles.Where(entry => entry.textComponent != null && entry.needsUpdate).Select(entry => entry.textComponent).ToArray();
    }
    
    /// <summary>
    /// Get available preset names
    /// </summary>
    public string[] GetPresetNames()
    {
        return stylePresets.Where(preset => !string.IsNullOrEmpty(preset.presetName)).Select(preset => preset.presetName).ToArray();
    }
    
    /// <summary>
    /// Add ReliableMenuText to all text components
    /// </summary>
    public void AddReliableMenuTextToAll(int styleIndex = 0, bool enableHover = true, bool enableSparkles = true, bool enableBreathing = false)
    {
        foreach (var entry in textStyles)
        {
            if (entry.textComponent != null)
            {
                entry.addReliableMenuText = true;
                entry.reliableMenuTextStyleIndex = styleIndex;
                entry.enableReliableMenuTextHover = enableHover;
                entry.enableReliableMenuTextSparkles = enableSparkles;
                entry.enableReliableMenuTextBreathing = enableBreathing;
                entry.needsUpdate = true;
            }
        }
        
        ApplyAllStyles();
    }
    
    /// <summary>
    /// Remove ReliableMenuText from all managed text components
    /// </summary>
    public void RemoveAllReliableMenuText()
    {
        foreach (var entry in textStyles)
        {
            if (entry.textComponent != null)
            {
                ReliableMenuText reliableMenuText = entry.textComponent.GetComponent<ReliableMenuText>();
                if (reliableMenuText != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(reliableMenuText);
                    }
                    else
                    {
                        #if UNITY_EDITOR
                        DestroyImmediate(reliableMenuText);
                        #endif
                    }
                }
                
                entry.addReliableMenuText = false;
                entry.appliedReliableMenuText = null;
                entry.needsUpdate = true;
            }
        }
        
        UpdateStatistics();
    }
    
    /// <summary>
    /// Apply specific ReliableMenuText style to all text components
    /// </summary>
    public void SetReliableMenuTextStyleForAll(int styleIndex)
    {
        foreach (var entry in textStyles)
        {
            if (entry.addReliableMenuText)
            {
                entry.reliableMenuTextStyleIndex = styleIndex;
                entry.needsUpdate = true;
            }
        }
        
        ApplyAllStyles();
    }
    
    #endregion
}

#if UNITY_EDITOR
/// <summary>
/// Custom editor for TextStyleHandler with helpful buttons and improved layout
/// </summary>
[CustomEditor(typeof(TextStyleHandler))]
public class TextStyleHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TextStyleHandler handler = (TextStyleHandler)target;
        
        // Default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan For Text"))
        {
            handler.ScanForText();
            EditorUtility.SetDirty(handler);
        }
        
        if (GUILayout.Button("Apply All Styles"))
        {
            handler.ApplyAllStyles();
            EditorUtility.SetDirty(handler);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mark All For Update"))
        {
            handler.MarkAllForUpdate();
            EditorUtility.SetDirty(handler);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // ReliableMenuText management
        EditorGUILayout.LabelField("ReliableMenuText Management", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add ReliableMenuText (Eggshell Gold)"))
        {
            handler.AddReliableMenuTextToAll(4, true, true, false); // Style index 4 is Eggshell Gold
            EditorUtility.SetDirty(handler);
        }
        
        if (GUILayout.Button("Add ReliableMenuText (Professional Gold)"))
        {
            handler.AddReliableMenuTextToAll(0, true, true, false); // Style index 0 is Professional Gold
            EditorUtility.SetDirty(handler);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove All ReliableMenuText"))
        {
            if (EditorUtility.DisplayDialog("Remove All ReliableMenuText", 
                "This will remove all ReliableMenuText components from managed text. This cannot be undone.", 
                "Remove", "Cancel"))
            {
                handler.RemoveAllReliableMenuText();
                EditorUtility.SetDirty(handler);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Preset quick apply buttons
        EditorGUILayout.LabelField("Quick Preset Apply", EditorStyles.boldLabel);
        string[] presetNames = handler.GetPresetNames();
        
        for (int i = 0; i < presetNames.Length; i += 2)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button($"Apply '{presetNames[i]}' to All"))
            {
                handler.ApplyPresetToAll(presetNames[i]);
                EditorUtility.SetDirty(handler);
            }
            
            if (i + 1 < presetNames.Length)
            {
                if (GUILayout.Button($"Apply '{presetNames[i + 1]}' to All"))
                {
                    handler.ApplyPresetToAll(presetNames[i + 1]);
                    EditorUtility.SetDirty(handler);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space();
        
        // Show helpful information
        EditorGUILayout.LabelField("Text Overview", EditorStyles.boldLabel);
        
        TextMeshProUGUI[] textWithReliableMenuText = handler.GetTextWithReliableMenuText();
        TextMeshProUGUI[] textNeedingUpdate = handler.GetTextNeedingUpdate();
        
        if (textWithReliableMenuText.Length > 0)
        {
            EditorGUILayout.HelpBox($"{textWithReliableMenuText.Length} text component(s) have existing ReliableMenuText. " +
                "Enable 'Force Override Existing' to apply styles to these components.", MessageType.Info);
        }
        
        if (textNeedingUpdate.Length > 0)
        {
            EditorGUILayout.HelpBox($"{textNeedingUpdate.Length} text component(s) are marked as needing updates. " +
                "Click 'Apply All Styles' to update them.", MessageType.Warning);
        }
        
        if (handler.GetAllManagedText().Length == 0)
        {
            EditorGUILayout.HelpBox("No text components found. Click 'Scan For Text' to discover TextMeshProUGUI components in the scene.", MessageType.Warning);
        }
    }
}
#endif 