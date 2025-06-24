using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Entry for custom sound database
/// </summary>
[System.Serializable]
public class CustomSoundEntry
{
    [Tooltip("Unique name to reference this sound")]
    public string soundName;
    
    [Tooltip("The audio clip to use for this sound")]
    public AudioClip audioClip;
    
    [Tooltip("Volume for this specific sound (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Tooltip("Pitch for this specific sound")]
    [Range(0.1f, 3f)]
    public float pitch = 1f;
    
    [Tooltip("Whether this sound should loop")]
    public bool loop = false;
}

/// <summary>
/// Singleton manager for handling sound effects with local playback support.
/// Can be used with or without networking - handles both scenarios gracefully.
/// Attach to: A GameObject in the scene (preferably on a manager object).
/// </summary>
public class SoundEffectManager : MonoBehaviour
{
    [Header("Fallback Sound")]
    [SerializeField] private AudioClip defaultSoundClip;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.7f;
    [SerializeField, Range(0.1f, 3f)] private float defaultPitch = 1f;
    
    [Header("Custom Sound Database")]
    [Tooltip("Database of custom sounds that can be referenced by name")]
    [SerializeField] private CustomSoundEntry[] customSoundDatabase = new CustomSoundEntry[0];
    
    [Header("Audio Settings")]
    [SerializeField] private int maxAudioSources = 20;
    [SerializeField] private bool spatializeAudio = true;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private AnimationCurve volumeRolloff = AnimationCurve.Linear(0, 1, 1, 0);
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Singleton instance
    public static SoundEffectManager Instance { get; private set; }
    
    // Audio source pool
    private Queue<AudioSource> audioSourcePool;
    private List<AudioSource> activeAudioSources = new List<AudioSource>();
    
    // Custom sound database lookup
    private Dictionary<string, CustomSoundEntry> customSoundLookup;
    
    // Audio source parent
    private Transform audioSourceParent;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("SoundEffectManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Always initialize on Start to ensure it works in all scenarios
        InitializeManager();
    }
    
    private void InitializeManager()
    {
        /* Debug.Log($"SoundEffectManager: Starting initialization"); */
        
        // Create audio source parent
        if (audioSourceParent == null)
        {
            GameObject parentGO = new GameObject("SoundEffects");
            audioSourceParent = parentGO.transform;
            audioSourceParent.SetParent(transform);
        }
        
        // Initialize custom sound database
        InitializeCustomSoundDatabase();
        
        // Initialize audio source pool
        InitializeAudioSourcePool();
        
        Debug.Log($"SoundEffectManager: Initialized successfully");
    }
    
    /// <summary>
    /// Initializes the custom sound database lookup
    /// </summary>
    private void InitializeCustomSoundDatabase()
    {
        customSoundLookup = new Dictionary<string, CustomSoundEntry>();
        
        foreach (var entry in customSoundDatabase)
        {
            if (!string.IsNullOrEmpty(entry.soundName) && entry.audioClip != null)
            {
                customSoundLookup[entry.soundName] = entry;
                Debug.Log($"SoundEffectManager: Registered custom sound '{entry.soundName}' -> {entry.audioClip.name}");
            }
        }
    }
    
    /// <summary>
    /// Initializes the audio source pool
    /// </summary>
    private void InitializeAudioSourcePool()
    {
        audioSourcePool = new Queue<AudioSource>();
        
        for (int i = 0; i < maxAudioSources; i++)
        {
            AudioSource audioSource = CreatePooledAudioSource();
            audioSourcePool.Enqueue(audioSource);
        }
        
        Debug.Log($"SoundEffectManager: Created pool of {maxAudioSources} audio sources");
    }
    
    /// <summary>
    /// Creates a pooled audio source with default settings
    /// </summary>
    private AudioSource CreatePooledAudioSource()
    {
        GameObject audioGO = new GameObject("PooledAudioSource");
        audioGO.transform.SetParent(audioSourceParent);
        
        AudioSource audioSource = audioGO.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatializeAudio ? 1f : 0f; // 3D if spatialized, 2D if not
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Custom;
        audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
        
        return audioSource;
    }
    
    /// <summary>
    /// Check if sound effects should be played for the given entity IDs using centralized EntityVisibilityManager
    /// </summary>
    private bool ShouldPlaySoundEffectsForEntities(uint sourceEntityId, uint targetEntityId)
    {
        // Use centralized visibility management from EntityVisibilityManager
        EntityVisibilityManager visibilityManager = EntityVisibilityManager.Instance;
        if (visibilityManager == null)
        {
            Debug.Log("SoundEffectManager: No EntityVisibilityManager found, allowing sounds to play");
            return true;
        }
        
        return visibilityManager.ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// Plays an audio clip at a specific position using pooled audio sources
    /// </summary>
    private void PlayAudioClipAtPosition(Vector3 position, AudioClip clip, float volume, float pitch, bool loop)
    {
        AudioSource audioSource = GetPooledAudioSource();
        if (audioSource == null)
        {
            Debug.LogWarning("SoundEffectManager: No available audio sources in pool");
            return;
        }
        
        // Setup audio source
        audioSource.transform.position = position;
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        
        // Make sure the audio source GameObject is active
        if (!audioSource.gameObject.activeInHierarchy)
        {
            audioSource.gameObject.SetActive(true);
        }
        
        // Play the sound
        audioSource.Play();
        activeAudioSources.Add(audioSource);
        
        if (debugMode)
        {
            /* Debug.Log($"SoundEffectManager: Playing {clip.name} at {position} (Volume: {volume}, Pitch: {pitch}, Loop: {loop})"); */
        }
        
        // Start coroutine to return to pool when finished
        if (!loop)
        {
            StartCoroutine(ReturnAudioSourceWhenFinished(audioSource, clip.length / pitch));
        }
    }
    
    /// <summary>
    /// Gets a pooled audio source
    /// </summary>
    private AudioSource GetPooledAudioSource()
    {
        // Ensure pool is initialized
        if (audioSourcePool == null)
        {
            Debug.LogWarning("SoundEffectManager: Audio source pool not initialized, initializing now");
            InitializeAudioSourcePool();
        }
        
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }
        
        // Pool is empty, create a new one
        Debug.LogWarning("SoundEffectManager: Audio source pool was empty, creating new audio source");
        return CreatePooledAudioSource();
    }
    
    /// <summary>
    /// Returns an audio source to the pool after it finishes playing
    /// </summary>
    private IEnumerator ReturnAudioSourceWhenFinished(AudioSource audioSource, float duration)
    {
        yield return new WaitForSeconds(duration + 0.1f); // Small buffer
        
        if (audioSource != null)
        {
            ReturnAudioSourceToPool(audioSource);
        }
    }
    
    /// <summary>
    /// Returns an audio source to the pool
    /// </summary>
    private void ReturnAudioSourceToPool(AudioSource audioSource)
    {
        if (audioSource == null) return;
        
        audioSource.Stop();
        audioSource.clip = null;
        audioSource.transform.position = Vector3.zero;
        
        activeAudioSources.Remove(audioSource);
        audioSourcePool.Enqueue(audioSource);
        
        if (debugMode)
        {
            Debug.Log("SoundEffectManager: Returned audio source to pool");
        }
    }
    
    /// <summary>
    /// Finds an audio clip by name in the database or resources
    /// </summary>
    private AudioClip FindAudioClip(string clipName)
    {
        // First check if it's in our custom database
        foreach (var entry in customSoundDatabase)
        {
            if (entry.audioClip != null && entry.audioClip.name == clipName)
            {
                return entry.audioClip;
            }
        }
        
        // Check if it's the default clip
        if (defaultSoundClip != null && defaultSoundClip.name == clipName)
        {
            return defaultSoundClip;
        }
        
        // Fallback: try to load from Resources
        AudioClip clip = Resources.Load<AudioClip>($"Sounds/{clipName}");
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>(clipName);
        }
        
        return clip;
    }
    
    #region Public API
    
    /// <summary>
    /// Public method for external scripts to trigger sound effects
    /// </summary>
    public static void TriggerSoundEffect(Vector3 position, uint sourceEntityId = 0, uint targetEntityId = 0)
    {
        if (Instance == null)
        {
            Debug.LogError("SoundEffectManager: No instance found. Make sure SoundEffectManager is in the scene.");
            return;
        }
        
        Instance.PlaySoundEffect(position, sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// Public method for external scripts to trigger named sound effects
    /// </summary>
    public static void TriggerNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId = 0, uint targetEntityId = 0)
    {
        if (Instance == null)
        {
            Debug.LogError("SoundEffectManager: No instance found. Make sure SoundEffectManager is in the scene.");
            return;
        }
        
        Instance.PlayNamedSoundEffect(position, soundName, sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// Play a default sound effect at a specific position
    /// </summary>
    public void PlaySoundEffect(Vector3 position, uint sourceEntityId = 0, uint targetEntityId = 0)
    {
        // Check if sounds should be played based on visibility
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            if (debugMode)
            {
                Debug.Log("SoundEffectManager: Skipping sound effect - not in currently viewed fight");
            }
            return;
        }
        
        if (defaultSoundClip != null)
        {
            PlayAudioClipAtPosition(position, defaultSoundClip, defaultVolume, defaultPitch, false);
        }
        else
        {
            Debug.LogWarning("SoundEffectManager: No default sound clip assigned");
        }
    }
    
    /// <summary>
    /// Play a named custom sound effect at a specific position
    /// </summary>
    public void PlayNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId = 0, uint targetEntityId = 0)
    {
        // Check if sounds should be played based on visibility
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            if (debugMode)
            {
                Debug.Log($"SoundEffectManager: Skipping named sound effect '{soundName}' - not in currently viewed fight");
            }
            return;
        }
        
        // Ensure initialization
        if (customSoundLookup == null)
        {
            Debug.LogWarning("SoundEffectManager: Custom sound lookup not initialized, initializing now");
            InitializeCustomSoundDatabase();
        }
        
        if (customSoundLookup.TryGetValue(soundName, out CustomSoundEntry soundEntry))
        {
            if (debugMode)
            {
                /* Debug.Log($"SoundEffectManager: Playing custom sound '{soundName}' at {position}"); */
            }
            PlayAudioClipAtPosition(position, soundEntry.audioClip, soundEntry.volume, soundEntry.pitch, soundEntry.loop);
        }
        else
        {
            Debug.LogWarning($"SoundEffectManager: Named sound '{soundName}' not found in database, using default sound");
            PlaySoundEffect(position, sourceEntityId, targetEntityId);
        }
    }
    
    /// <summary>
    /// Play a sound effect with specific clip and parameters
    /// </summary>
    public void PlaySoundEffectWithClip(Vector3 position, AudioClip clip, float volume, float pitch, bool loop, uint sourceEntityId = 0, uint targetEntityId = 0)
    {
        if (clip == null)
        {
            Debug.LogWarning("SoundEffectManager: Audio clip is null, cannot play sound");
            return;
        }
        
        // Check if sounds should be played based on visibility
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            if (debugMode)
            {
                Debug.Log($"SoundEffectManager: Skipping sound effect '{clip.name}' - not in currently viewed fight");
            }
            return;
        }
        
        if (debugMode)
        {
            Debug.Log($"SoundEffectManager: Playing sound {clip.name} at {position}");
        }
        
        PlayAudioClipAtPosition(position, clip, volume, pitch, loop);
    }
    
    /// <summary>
    /// Stop all currently playing sounds
    /// </summary>
    public void StopAllSounds()
    {
        foreach (var audioSource in activeAudioSources)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
        
        // Return all to pool
        while (activeAudioSources.Count > 0)
        {
            ReturnAudioSourceToPool(activeAudioSources[0]);
        }
    }
    
    /// <summary>
    /// Set master volume for all sound effects
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        defaultVolume = Mathf.Clamp01(volume);
        
        // Update all active audio sources
        foreach (var audioSource in activeAudioSources)
        {
            if (audioSource != null)
            {
                audioSource.volume = defaultVolume;
            }
        }
    }
    
    /// <summary>
    /// Get current master volume
    /// </summary>
    public float GetMasterVolume() => defaultVolume;
    
    #endregion
} 