using UnityEngine;

/// <summary>
/// Manages offline/local game settings that don't need network synchronization.
/// This includes graphics settings, randomization preferences, and other local configurations.
/// Available immediately on scene load, not dependent on network connection.
/// </summary>
public class OfflineGameManager : MonoBehaviour
{
    [Header("Singleton")]
    public static OfflineGameManager Instance { get; private set; }

    [Header("Randomization Settings")]
    [SerializeField, Tooltip("If true, all cards will have randomized abilities, costs, initiatives, and rarities.")]
    private bool enableRandomizedCards = false;

    [SerializeField, Tooltip("When randomization is enabled, controls whether starting decks should be themed (class-appropriate synergies) or completely random.")]
    private bool useThemedStarterDecks = true;

    [Header("Display Settings")]
    [SerializeField, Tooltip("If true, enables VSync to synchronize frame rate with monitor refresh rate. Note: VSync is automatically disabled when using frame rate limiting (maxFrameRate > 0).")]
    private bool enableVSync = false;

    [SerializeField, Tooltip("Maximum frame rate limit. Set to -1 for unlimited, 0 to use platform default, or any positive value.")]
    private int maxFrameRate = -1;

    [Header("Audio Settings")]
    [SerializeField, Range(0f, 1f), Tooltip("Master volume level")]
    private float masterVolume = 1f;

    [SerializeField, Range(0f, 1f), Tooltip("Music volume level")]
    private float musicVolume = 0.7f;

    [SerializeField, Range(0f, 1f), Tooltip("Sound effects volume level")]
    private float sfxVolume = 0.8f;

    // Public properties
    public bool EnableRandomizedCards => enableRandomizedCards;
    public bool UseThemedStarterDecks => useThemedStarterDecks;
    public bool EnableVSync => enableVSync;
    public int MaxFrameRate => maxFrameRate;
    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("OfflineGameManager: Initialized and set as singleton");
        }
        else
        {
            Debug.Log("OfflineGameManager: Instance already exists, destroying duplicate");
            Destroy(gameObject);
            return;
        }

        // Apply settings immediately
        ApplyDisplaySettings();
        ApplyAudioSettings();
    }

    private void Start()
    {
        // Reapply display settings in Start to ensure they take effect
        Debug.Log("OfflineGameManager: Start() - Reapplying display settings to ensure they take effect");
        ApplyDisplaySettings();
    }

    /// <summary>
    /// Apply display settings (frame rate, VSync)
    /// </summary>
    private void ApplyDisplaySettings()
    {
        Debug.Log($"OfflineGameManager: Applying display settings - maxFrameRate: {maxFrameRate}, enableVSync: {enableVSync}");
        
        // When using frame rate limiting, VSync must be disabled
        if (maxFrameRate > 0)
        {
            // Disable VSync to allow frame rate limiting
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = maxFrameRate;
            Debug.Log($"OfflineGameManager: Frame rate limited to {maxFrameRate} FPS (VSync disabled for frame rate limiting)");
            
            if (enableVSync)
            {
                Debug.LogWarning("OfflineGameManager: VSync was enabled but has been disabled to allow frame rate limiting. To use VSync, set maxFrameRate to -1 or 0.");
            }
        }
        else if (maxFrameRate == -1)
        {
            // Unlimited frame rate
            if (enableVSync)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                Debug.Log("OfflineGameManager: VSync enabled with unlimited frame rate");
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                Debug.Log("OfflineGameManager: Frame rate set to unlimited (VSync disabled)");
            }
        }
        else // maxFrameRate == 0 (platform default)
        {
            if (enableVSync)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = 0;
                Debug.Log("OfflineGameManager: VSync enabled with platform default frame rate");
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 0;
                Debug.Log("OfflineGameManager: Frame rate set to platform default (VSync disabled)");
            }
        }
        
        // Force a frame to ensure settings take effect
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Apply audio settings
    /// </summary>
    private void ApplyAudioSettings()
    {
        AudioListener.volume = masterVolume;
        Debug.Log($"OfflineGameManager: Applied audio settings - Master: {masterVolume}, Music: {musicVolume}, SFX: {sfxVolume}");
    }

    /// <summary>
    /// Update randomization setting (for runtime changes)
    /// </summary>
    public void SetRandomizationEnabled(bool enabled)
    {
        enableRandomizedCards = enabled;
        Debug.Log($"OfflineGameManager: Randomization setting changed to {enabled}");
        
        // Notify RandomizedCardDatabaseManager of the change
        var randomDbManager = FindFirstObjectByType<RandomizedCardDatabaseManager>();
        if (randomDbManager != null)
        {
            randomDbManager.OnRandomizationSettingChanged();
        }
        else
        {
            Debug.LogWarning("OfflineGameManager: RandomizedCardDatabaseManager not found to notify of setting change");
        }
    }

    /// <summary>
    /// Update themed starter decks setting (for runtime changes)
    /// </summary>
    public void SetThemedStarterDecks(bool useThemed)
    {
        useThemedStarterDecks = useThemed;
        Debug.Log($"OfflineGameManager: Themed starter decks setting changed to {useThemed}");
    }

    /// <summary>
    /// Update display settings at runtime
    /// </summary>
    public void SetDisplaySettings(int frameRate, bool vsync)
    {
        maxFrameRate = frameRate;
        enableVSync = vsync;
        ApplyDisplaySettings();
    }

    /// <summary>
    /// Update audio settings at runtime
    /// </summary>
    public void SetAudioSettings(float master, float music, float sfx)
    {
        masterVolume = Mathf.Clamp01(master);
        musicVolume = Mathf.Clamp01(music);
        sfxVolume = Mathf.Clamp01(sfx);
        ApplyAudioSettings();
    }

    // Debug methods
    [ContextMenu("Reapply Display Settings")]
    public void ReapplyDisplaySettings()
    {
        Debug.Log("OfflineGameManager: Manually reapplying display settings...");
        ApplyDisplaySettings();
    }

    [ContextMenu("Log Current Settings")]
    public void LogCurrentSettings()
    {
        Debug.Log($"OfflineGameManager: Current Settings:");
        Debug.Log($"  - Randomization: {enableRandomizedCards}");
        Debug.Log($"  - Themed Starter Decks: {useThemedStarterDecks}");
        Debug.Log($"  - Frame Rate: {maxFrameRate}");
        Debug.Log($"  - VSync: {enableVSync}");
        Debug.Log($"  - Master Volume: {masterVolume}");
        Debug.Log($"  - Music Volume: {musicVolume}");
        Debug.Log($"  - SFX Volume: {sfxVolume}");
        Debug.Log($"Runtime Display Settings:");
        Debug.Log($"  - QualitySettings.vSyncCount: {QualitySettings.vSyncCount}");
        Debug.Log($"  - Application.targetFrameRate: {Application.targetFrameRate}");
        Debug.Log($"  - AudioListener.volume: {AudioListener.volume}");
    }
} 