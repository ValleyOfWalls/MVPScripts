using UnityEngine;

/// <summary>
/// Entry for phase-specific sound effects
/// </summary>
[System.Serializable]
public class PhaseSoundEntry
{
    [Tooltip("The game phase this sound is for")]
    public GamePhaseManager.GamePhase gamePhase;
    
    [Tooltip("Sound name to play when entering this phase (from SoundEffectManager database)")]
    public string phaseStartSoundName = "";
    
    [Tooltip("Sound name to play when exiting this phase (from SoundEffectManager database)")]
    public string phaseEndSoundName = "";
    
    [Tooltip("Delay before playing the start sound (in seconds)")]
    [Range(0f, 5f)]
    public float startSoundDelay = 0f;
    
    [Tooltip("Delay before playing the end sound (in seconds)")]
    [Range(0f, 5f)]
    public float endSoundDelay = 0f;
    
    [Tooltip("Volume multiplier for phase sounds")]
    [Range(0f, 2f)]
    public float volumeMultiplier = 1f;
    
    [Tooltip("Should the start sound interrupt previous phase sounds?")]
    public bool interruptPreviousSounds = false;
}
//
/// <summary>
/// Handles sound effects for game phase transitions.
/// Integrates with GamePhaseManager and SoundEffectManager.
/// Attach to: A GameObject in the scene, preferably the same one as GamePhaseManager.
/// Dependencies: GamePhaseManager, SoundEffectManager
/// </summary>
public class PhaseChangeSoundHandler : MonoBehaviour
{
    [Header("Phase Sound Configuration")]
    [Tooltip("Database of sounds for each phase transition")]
    [SerializeField] private PhaseSoundEntry[] phaseSounds = new PhaseSoundEntry[0];
    
    [Header("Global Settings")]
    [Tooltip("Global volume multiplier for all phase sounds")]
    [Range(0f, 2f)]
    [SerializeField] private float globalVolumeMultiplier = 1f;
    
    [Tooltip("Should phase sounds be spatialized? If false, plays as UI sounds")]
    [SerializeField] private bool use3DAudio = false;
    
    [Tooltip("Position to play sounds at (if 3D audio is enabled)")]
    [SerializeField] private Transform audioPosition;
    
    [Header("Fallback Audio")]
    [Tooltip("Default sound to play when entering any phase (if no specific sound configured)")]
    [SerializeField] private AudioClip fallbackPhaseStartClip;
    
    [Tooltip("Default sound to play when exiting any phase (if no specific sound configured)")]
    [SerializeField] private AudioClip fallbackPhaseEndClip;
    
    [Tooltip("Volume for fallback audio")]
    [Range(0f, 1f)]
    [SerializeField] private float fallbackVolume = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Phase sound lookup for fast access
    private System.Collections.Generic.Dictionary<GamePhaseManager.GamePhase, PhaseSoundEntry> phaseSoundLookup;
    
    // Track current and previous phases
    private GamePhaseManager.GamePhase currentPhase;
    private GamePhaseManager.GamePhase previousPhase;
    
    // Reference to the audio source for fallback sounds
    private AudioSource fallbackAudioSource;
    
    // Coroutine tracking for delayed sounds
    private Coroutine currentStartSoundCoroutine;
    private Coroutine currentEndSoundCoroutine;
    
    private void Awake()
    {
        InitializePhaseSoundLookup();
        SetupAudioPosition();
    }
    
    private void Start()
    {
        SubscribeToPhaseChanges();
        
        // Set initial phase
        if (GamePhaseManager.Instance != null)
        {
            currentPhase = GamePhaseManager.Instance.GetCurrentPhase();
            previousPhase = currentPhase;
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromPhaseChanges();
    }
    
    /// <summary>
    /// Initialize the phase sound lookup dictionary
    /// </summary>
    private void InitializePhaseSoundLookup()
    {
        phaseSoundLookup = new System.Collections.Generic.Dictionary<GamePhaseManager.GamePhase, PhaseSoundEntry>();
        
        foreach (var entry in phaseSounds)
        {
            phaseSoundLookup[entry.gamePhase] = entry;
            
            if (debugMode)
            {
                Debug.Log($"PhaseChangeSoundHandler: Registered sounds for {entry.gamePhase} phase - Start: '{entry.phaseStartSoundName}', End: '{entry.phaseEndSoundName}'");
            }
        }
    }
    
    /// <summary>
    /// Setup audio position reference
    /// </summary>
    private void SetupAudioPosition()
    {
        if (audioPosition == null && use3DAudio)
        {
            // Default to camera position or this transform
            Camera mainCam = Camera.main;
            audioPosition = mainCam != null ? mainCam.transform : transform;
        }
    }
    
    /// <summary>
    /// Subscribe to phase change events
    /// </summary>
    private void SubscribeToPhaseChanges()
    {
        if (GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.OnPhaseChanged += OnPhaseChanged;
            
            if (debugMode)
            {
                Debug.Log("PhaseChangeSoundHandler: Subscribed to GamePhaseManager phase change events");
            }
        }
        else
        {
            Debug.LogWarning("PhaseChangeSoundHandler: GamePhaseManager instance not found. Retrying in 1 second...");
            Invoke(nameof(SubscribeToPhaseChanges), 1f);
        }
    }
    
    /// <summary>
    /// Unsubscribe from phase change events
    /// </summary>
    private void UnsubscribeFromPhaseChanges()
    {
        if (GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
    }
    
    /// <summary>
    /// Called when the game phase changes
    /// </summary>
    private void OnPhaseChanged(GamePhaseManager.GamePhase newPhase)
    {
        if (debugMode)
        {
            Debug.Log($"PhaseChangeSoundHandler: Phase changed from {currentPhase} to {newPhase}");
        }
        
        // Play end sound for previous phase
        PlayPhaseEndSound(currentPhase);
        
        // Update phase tracking
        previousPhase = currentPhase;
        currentPhase = newPhase;
        
        // Play start sound for new phase
        PlayPhaseStartSound(newPhase);
    }
    
    /// <summary>
    /// Play the start sound for a phase
    /// </summary>
    private void PlayPhaseStartSound(GamePhaseManager.GamePhase phase)
    {
        if (phaseSoundLookup.TryGetValue(phase, out PhaseSoundEntry soundEntry))
        {
            if (!string.IsNullOrEmpty(soundEntry.phaseStartSoundName))
            {
                // Stop previous start sound if it should be interrupted
                if (soundEntry.interruptPreviousSounds && currentStartSoundCoroutine != null)
                {
                    StopCoroutine(currentStartSoundCoroutine);
                    currentStartSoundCoroutine = null;
                }
                
                // Play the sound with delay
                if (soundEntry.startSoundDelay > 0f)
                {
                    currentStartSoundCoroutine = StartCoroutine(PlaySoundDelayed(
                        soundEntry.phaseStartSoundName, 
                        soundEntry.startSoundDelay, 
                        soundEntry.volumeMultiplier,
                        $"phase start ({phase})"
                    ));
                }
                else
                {
                    PlayNamedSound(soundEntry.phaseStartSoundName, soundEntry.volumeMultiplier, $"phase start ({phase})");
                }
                
                return;
            }
        }
        
        // Fallback to default start sound
        if (fallbackPhaseStartClip != null)
        {
            PlayFallbackSound(fallbackPhaseStartClip, $"fallback phase start ({phase})");
        }
        else if (debugMode)
        {
            /* Debug.Log($"PhaseChangeSoundHandler: No start sound configured for phase {phase}"); */
        }
    }
    
    /// <summary>
    /// Play the end sound for a phase
    /// </summary>
    private void PlayPhaseEndSound(GamePhaseManager.GamePhase phase)
    {
        if (phaseSoundLookup.TryGetValue(phase, out PhaseSoundEntry soundEntry))
        {
            if (!string.IsNullOrEmpty(soundEntry.phaseEndSoundName))
            {
                // Play the sound with delay
                if (soundEntry.endSoundDelay > 0f)
                {
                    currentEndSoundCoroutine = StartCoroutine(PlaySoundDelayed(
                        soundEntry.phaseEndSoundName, 
                        soundEntry.endSoundDelay, 
                        soundEntry.volumeMultiplier,
                        $"phase end ({phase})"
                    ));
                }
                else
                {
                    PlayNamedSound(soundEntry.phaseEndSoundName, soundEntry.volumeMultiplier, $"phase end ({phase})");
                }
                
                return;
            }
        }
        
        // Fallback to default end sound
        if (fallbackPhaseEndClip != null)
        {
            PlayFallbackSound(fallbackPhaseEndClip, $"fallback phase end ({phase})");
        }
        else if (debugMode)
        {
            /* Debug.Log($"PhaseChangeSoundHandler: No end sound configured for phase {phase}"); */
        }
    }
    
    /// <summary>
    /// Coroutine to play a sound after a delay
    /// </summary>
    private System.Collections.IEnumerator PlaySoundDelayed(string soundName, float delay, float volumeMultiplier, string context)
    {
        yield return new WaitForSeconds(delay);
        PlayNamedSound(soundName, volumeMultiplier, context);
    }
    
    /// <summary>
    /// Play a named sound through SoundEffectManager
    /// </summary>
    private void PlayNamedSound(string soundName, float volumeMultiplier, string context)
    {
        Vector3 soundPosition = GetSoundPosition();
        
        if (debugMode)
        {
            Debug.Log($"PhaseChangeSoundHandler: Playing {context} sound '{soundName}' at {soundPosition} (Volume: {volumeMultiplier * globalVolumeMultiplier})");
        }
        
        if (SoundEffectManager.Instance != null)
        {
            SoundEffectManager.TriggerNamedSoundEffect(soundPosition, soundName);
        }
        else
        {
            Debug.LogWarning($"PhaseChangeSoundHandler: SoundEffectManager not available, cannot play {context} sound '{soundName}'");
        }
    }
    
    /// <summary>
    /// Play a fallback sound directly
    /// </summary>
    private void PlayFallbackSound(AudioClip clip, string context)
    {
        if (clip == null) return;
        
        Vector3 soundPosition = GetSoundPosition();
        
        if (debugMode)
        {
            Debug.Log($"PhaseChangeSoundHandler: Playing {context} fallback sound '{clip.name}' at {soundPosition}");
        }
        
        // Create or get fallback audio source
        if (fallbackAudioSource == null)
        {
            GameObject audioGO = new GameObject("PhaseChangeSoundAudio");
            audioGO.transform.SetParent(transform);
            fallbackAudioSource = audioGO.AddComponent<AudioSource>();
            fallbackAudioSource.playOnAwake = false;
            fallbackAudioSource.spatialBlend = use3DAudio ? 1f : 0f;
        }
        
        // Set position and play
        fallbackAudioSource.transform.position = soundPosition;
        fallbackAudioSource.clip = clip;
        fallbackAudioSource.volume = fallbackVolume * globalVolumeMultiplier;
        fallbackAudioSource.Play();
    }
    
    /// <summary>
    /// Get the position where sounds should be played
    /// </summary>
    private Vector3 GetSoundPosition()
    {
        if (use3DAudio && audioPosition != null)
        {
            return audioPosition.position;
        }
        
        // For UI sounds, use camera position or world origin
        Camera mainCam = Camera.main;
        return mainCam != null ? mainCam.transform.position : Vector3.zero;
    }
    
    #region Public API
    
    /// <summary>
    /// Manually trigger a phase start sound
    /// </summary>
    public void TriggerPhaseStartSound(GamePhaseManager.GamePhase phase)
    {
        PlayPhaseStartSound(phase);
    }
    
    /// <summary>
    /// Manually trigger a phase end sound
    /// </summary>
    public void TriggerPhaseEndSound(GamePhaseManager.GamePhase phase)
    {
        PlayPhaseEndSound(phase);
    }
    
    /// <summary>
    /// Set global volume multiplier
    /// </summary>
    public void SetGlobalVolumeMultiplier(float volume)
    {
        globalVolumeMultiplier = Mathf.Clamp(volume, 0f, 2f);
    }
    
    /// <summary>
    /// Get current global volume multiplier
    /// </summary>
    public float GetGlobalVolumeMultiplier() => globalVolumeMultiplier;
    
    /// <summary>
    /// Stop all currently playing phase sounds
    /// </summary>
    public void StopAllPhaseSounds()
    {
        if (currentStartSoundCoroutine != null)
        {
            StopCoroutine(currentStartSoundCoroutine);
            currentStartSoundCoroutine = null;
        }
        
        if (currentEndSoundCoroutine != null)
        {
            StopCoroutine(currentEndSoundCoroutine);
            currentEndSoundCoroutine = null;
        }
        
        if (fallbackAudioSource != null && fallbackAudioSource.isPlaying)
        {
            fallbackAudioSource.Stop();
        }
    }
    
    #endregion
} 