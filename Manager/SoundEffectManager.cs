using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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
/// Singleton manager for handling sound effects synchronized across clients based on fight visibility.
/// Attach to: A GameObject in the scene (preferably on a manager object).
/// </summary>
public class SoundEffectManager : NetworkBehaviour
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
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        InitializeManager();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        InitializeManager();
        Debug.Log("SoundEffectManager: Client initialization completed");
    }
    
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
    
    private void InitializeManager()
    {
        Debug.Log($"SoundEffectManager: Starting initialization - IsServer: {IsServerStarted}, IsClient: {IsClientStarted}");
        
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
        audioGO.SetActive(false);
        
        AudioSource audioSource = audioGO.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatializeAudio ? 1f : 0f; // 3D if spatialized, 2D if not
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Custom;
        audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
        
        return audioSource;
    }
    
    /// <summary>
    /// Plays a default sound effect at a specific position
    /// </summary>
    [Server]
    public void PlaySoundEffect(Vector3 position, uint sourceEntityId, uint targetEntityId)
    {
        PlaySoundEffectWithClip(position, defaultSoundClip, defaultVolume, defaultPitch, false, sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// Plays a named custom sound effect at a specific position
    /// </summary>
    [Server]
    public void PlayNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId, uint targetEntityId)
    {
        if (customSoundLookup.TryGetValue(soundName, out CustomSoundEntry soundEntry))
        {
            Debug.Log($"SoundEffectManager: Playing custom sound '{soundName}' at {position}");
            RpcPlayNamedSoundEffect(position, soundName, sourceEntityId, targetEntityId);
        }
        else
        {
            Debug.LogWarning($"SoundEffectManager: Custom sound '{soundName}' not found in database, using default sound");
            PlaySoundEffect(position, sourceEntityId, targetEntityId);
        }
    }
    
    /// <summary>
    /// Plays a sound effect with specific clip and parameters
    /// </summary>
    [Server]
    public void PlaySoundEffectWithClip(Vector3 position, AudioClip clip, float volume, float pitch, bool loop, uint sourceEntityId, uint targetEntityId)
    {
        if (clip == null)
        {
            Debug.LogWarning("SoundEffectManager: Audio clip is null, cannot play sound");
            return;
        }
        
        if (debugMode)
        {
            Debug.Log($"SoundEffectManager: Playing sound {clip.name} at {position}");
        }
        
        RpcPlaySoundEffect(position, clip.name, volume, pitch, loop, sourceEntityId, targetEntityId);
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
    /// RPC to play default sound effect on clients in the viewed fight
    /// </summary>
    [ObserversRpc]
    private void RpcPlaySoundEffect(Vector3 position, string clipName, float volume, float pitch, bool loop, uint sourceEntityId, uint targetEntityId)
    {
        // Check if sounds should be played on this client based on current fight visibility
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"SoundEffectManager: Skipping sound effect '{clipName}' - not in currently viewed fight");
            return;
        }
        
        // Try to find the clip in our database or resources
        AudioClip clip = FindAudioClip(clipName);
        if (clip == null)
        {
            Debug.LogWarning($"SoundEffectManager: Could not find audio clip '{clipName}'");
            return;
        }
        
        PlayAudioClipAtPosition(position, clip, volume, pitch, loop);
    }
    
    /// <summary>
    /// RPC to play named custom sound effect on clients in the viewed fight
    /// </summary>
    [ObserversRpc]
    private void RpcPlayNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId, uint targetEntityId)
    {
        // Check if sounds should be played on this client based on current fight visibility
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"SoundEffectManager: Skipping named sound effect '{soundName}' - not in currently viewed fight");
            return;
        }
        
        if (customSoundLookup.TryGetValue(soundName, out CustomSoundEntry soundEntry))
        {
            PlayAudioClipAtPosition(position, soundEntry.audioClip, soundEntry.volume, soundEntry.pitch, soundEntry.loop);
        }
        else
        {
            Debug.LogWarning($"SoundEffectManager: Named sound '{soundName}' not found in lookup, trying fallback");
            
            // Try to find default clip as fallback
            if (defaultSoundClip != null)
            {
                PlayAudioClipAtPosition(position, defaultSoundClip, defaultVolume, defaultPitch, false);
            }
        }
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
        audioSource.gameObject.SetActive(true);
        
        // Play the sound
        audioSource.Play();
        activeAudioSources.Add(audioSource);
        
        if (debugMode)
        {
            Debug.Log($"SoundEffectManager: Playing {clip.name} at {position} (Volume: {volume}, Pitch: {pitch}, Loop: {loop})");
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
        audioSource.gameObject.SetActive(false);
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
    
    /// <summary>
    /// Public method for external scripts to trigger sound effects
    /// </summary>
    public static void TriggerSoundEffect(Vector3 position, uint sourceEntityId, uint targetEntityId)
    {
        if (Instance == null)
        {
            Debug.LogError("SoundEffectManager: No instance found. Make sure SoundEffectManager is in the scene.");
            return;
        }
        
        if (Instance.IsServerStarted)
        {
            Instance.PlaySoundEffect(position, sourceEntityId, targetEntityId);
        }
        else
        {
            Debug.Log("SoundEffectManager: Triggering local sound effect on client");
            Instance.TriggerLocalSoundEffect(position, sourceEntityId, targetEntityId);
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger named sound effects
    /// </summary>
    public static void TriggerNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId, uint targetEntityId)
    {
        if (Instance == null)
        {
            Debug.LogError("SoundEffectManager: No instance found. Make sure SoundEffectManager is in the scene.");
            return;
        }
        
        if (Instance.IsServerStarted)
        {
            Instance.PlayNamedSoundEffect(position, soundName, sourceEntityId, targetEntityId);
        }
        else
        {
            Debug.Log($"SoundEffectManager: Triggering local named sound effect '{soundName}' on client");
            Instance.TriggerLocalNamedSoundEffect(position, soundName, sourceEntityId, targetEntityId);
        }
    }
    
    /// <summary>
    /// Triggers local sound effect without server validation
    /// </summary>
    private void TriggerLocalSoundEffect(Vector3 position, uint sourceEntityId, uint targetEntityId)
    {
        // Check if sounds should be played on this client
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log("SoundEffectManager: Skipping local sound effect - not in currently viewed fight");
            return;
        }
        
        if (defaultSoundClip != null)
        {
            PlayAudioClipAtPosition(position, defaultSoundClip, defaultVolume, defaultPitch, false);
        }
        else
        {
            Debug.LogWarning("SoundEffectManager: No default sound clip assigned for local playback");
        }
    }
    
    /// <summary>
    /// Triggers local named sound effect without server validation
    /// </summary>
    private void TriggerLocalNamedSoundEffect(Vector3 position, string soundName, uint sourceEntityId, uint targetEntityId)
    {
        // Check if sounds should be played on this client
        if (!ShouldPlaySoundEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"SoundEffectManager: Skipping local named sound effect '{soundName}' - not in currently viewed fight");
            return;
        }
        
        if (customSoundLookup.TryGetValue(soundName, out CustomSoundEntry soundEntry))
        {
            PlayAudioClipAtPosition(position, soundEntry.audioClip, soundEntry.volume, soundEntry.pitch, soundEntry.loop);
        }
        else
        {
            Debug.LogWarning($"SoundEffectManager: Named sound '{soundName}' not found for local playback, using default");
            TriggerLocalSoundEffect(position, sourceEntityId, targetEntityId);
        }
    }
} 