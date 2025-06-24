using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Entry for phase-specific music tracks
/// </summary>
[System.Serializable]
public class PhaseMusicEntry
{
    [Tooltip("The game phase this music applies to")]
    public GamePhaseManager.GamePhase gamePhase;
    
    [Tooltip("List of audio clips for this phase - one will be randomly selected")]
    public AudioClip[] musicTracks;
    
    [Tooltip("Volume multiplier for this phase (0-1)")]
    [Range(0f, 1f)]
    public float phaseVolume = 1f;
    
    [Tooltip("Should tracks loop individually or shuffle to next track when finished?")]
    public bool loopIndividualTracks = true;
    
    [Tooltip("Should tracks shuffle randomly or play in order?")]
    public bool shuffleTracks = true;
}

/// <summary>
/// Singleton manager for handling background music with phase transitions and crossfading.
/// Integrates with GamePhaseManager to automatically transition music based on game phases.
/// Attach to: A GameObject in the scene (preferably on a manager object).
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Music Database")]
    [Tooltip("Music tracks organized by game phase")]
    [SerializeField] private PhaseMusicEntry[] phaseMusicDatabase = new PhaseMusicEntry[0];
    
    [Header("Audio Settings")]
    [Tooltip("Master volume for all music")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.7f;
    
    [Tooltip("Default crossfade duration in seconds")]
    [Range(0.1f, 10f)]
    [SerializeField] private float defaultCrossfadeDuration = 2f;
    
    [Tooltip("Fade in duration when starting music")]
    [Range(0.1f, 5f)]
    [SerializeField] private float fadeInDuration = 1f;
    
    [Tooltip("Fade out duration when stopping music")]
    [Range(0.1f, 5f)]
    [SerializeField] private float fadeOutDuration = 1f;
    
    [Header("Playback Settings")]
    [Tooltip("Should music start automatically when the scene loads?")]
    [SerializeField] private bool autoStartMusic = true;
    
    [Tooltip("Should music persist between scene loads?")]
    [SerializeField] private bool persistBetweenScenes = true;
    
    [Tooltip("Minimum time before switching to next track in shuffle mode (seconds)")]
    [Range(10f, 300f)]
    [SerializeField] private float minimumTrackDuration = 30f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Singleton instance
    public static MusicManager Instance { get; private set; }
    
    // Audio sources for crossfading
    private AudioSource primaryAudioSource;
    private AudioSource secondaryAudioSource;
    private AudioSource currentActiveSource;
    
    // Current music state
    private GamePhaseManager.GamePhase currentPhase;
    private PhaseMusicEntry currentPhaseEntry;
    private List<AudioClip> currentShuffledPlaylist;
    private int currentTrackIndex = 0;
    
    // Coroutines
    private Coroutine crossfadeCoroutine;
    private Coroutine trackMonitorCoroutine;
    
    // Audio source parent
    private Transform audioSourceParent;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            Debug.LogWarning("MusicManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        InitializeAudioSources();
    }
    
    private void Start()
    {
        // Subscribe to GamePhaseManager events
        if (GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.OnPhaseChanged += OnPhaseChanged;
            currentPhase = GamePhaseManager.Instance.GetCurrentPhase();
            
            if (autoStartMusic)
            {
                StartMusicForCurrentPhase();
            }
        }
        else
        {
            Debug.LogWarning("MusicManager: GamePhaseManager not found. Music will not automatically transition with phases.");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
    }
    
    /// <summary>
    /// Initialize the dual audio source setup for crossfading
    /// </summary>
    private void InitializeAudioSources()
    {
        // Create audio source parent
        GameObject parentGO = new GameObject("MusicAudioSources");
        audioSourceParent = parentGO.transform;
        audioSourceParent.SetParent(transform);
        
        // Create primary audio source
        GameObject primaryGO = new GameObject("PrimaryMusicSource");
        primaryGO.transform.SetParent(audioSourceParent);
        primaryAudioSource = primaryGO.AddComponent<AudioSource>();
        SetupAudioSource(primaryAudioSource);
        
        // Create secondary audio source
        GameObject secondaryGO = new GameObject("SecondaryMusicSource");
        secondaryGO.transform.SetParent(audioSourceParent);
        secondaryAudioSource = secondaryGO.AddComponent<AudioSource>();
        SetupAudioSource(secondaryAudioSource);
        
        // Set primary as current active source
        currentActiveSource = primaryAudioSource;
        
        /* Debug.Log("MusicManager: Initialized dual audio source setup for crossfading"); */
    }
    
    /// <summary>
    /// Setup audio source with default music settings
    /// </summary>
    private void SetupAudioSource(AudioSource audioSource)
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D audio for music
        audioSource.volume = 0f; // Start at 0, will be faded in
        audioSource.loop = true; // Individual tracks can override this
    }
    
    /// <summary>
    /// Handle phase change events from GamePhaseManager
    /// </summary>
    private void OnPhaseChanged(GamePhaseManager.GamePhase newPhase)
    {
        if (debugMode)
        {
            Debug.Log($"MusicManager: Phase changed to {newPhase}");
        }
        
        currentPhase = newPhase;
        TransitionToPhaseMusic(newPhase);
    }
    
    /// <summary>
    /// Start music for the current phase
    /// </summary>
    private void StartMusicForCurrentPhase()
    {
        TransitionToPhaseMusic(currentPhase, true);
    }
    
    /// <summary>
    /// Transition to music for a specific phase
    /// </summary>
    private void TransitionToPhaseMusic(GamePhaseManager.GamePhase phase, bool isInitialStart = false)
    {
        PhaseMusicEntry phaseEntry = GetPhaseMusicEntry(phase);
        if (phaseEntry == null || phaseEntry.musicTracks.Length == 0)
        {
            if (debugMode)
            {
                Debug.LogWarning($"MusicManager: No music tracks found for phase {phase}");
            }
            
            // Fade out current music if no tracks for this phase
            if (!isInitialStart)
            {
                FadeOutCurrentMusic();
            }
            return;
        }
        
        currentPhaseEntry = phaseEntry;
        SetupPlaylistForPhase(phaseEntry);
        
        AudioClip nextTrack = GetNextTrack();
        if (nextTrack != null)
        {
            if (isInitialStart)
            {
                PlayTrackWithFadeIn(nextTrack);
            }
            else
            {
                CrossfadeToTrack(nextTrack);
            }
        }
    }
    
    /// <summary>
    /// Get the music entry for a specific phase
    /// </summary>
    private PhaseMusicEntry GetPhaseMusicEntry(GamePhaseManager.GamePhase phase)
    {
        return phaseMusicDatabase.FirstOrDefault(entry => entry.gamePhase == phase);
    }
    
    /// <summary>
    /// Setup the shuffled playlist for a phase
    /// </summary>
    private void SetupPlaylistForPhase(PhaseMusicEntry phaseEntry)
    {
        currentShuffledPlaylist = new List<AudioClip>(phaseEntry.musicTracks);
        
        if (phaseEntry.shuffleTracks)
        {
            // Shuffle the playlist
            for (int i = 0; i < currentShuffledPlaylist.Count; i++)
            {
                AudioClip temp = currentShuffledPlaylist[i];
                int randomIndex = Random.Range(i, currentShuffledPlaylist.Count);
                currentShuffledPlaylist[i] = currentShuffledPlaylist[randomIndex];
                currentShuffledPlaylist[randomIndex] = temp;
            }
            
            if (debugMode)
            {
                Debug.Log($"MusicManager: Shuffled playlist for phase {phaseEntry.gamePhase} ({currentShuffledPlaylist.Count} tracks)");
            }
        }
        
        currentTrackIndex = 0;
    }
    
    /// <summary>
    /// Get the next track in the current playlist
    /// </summary>
    private AudioClip GetNextTrack()
    {
        if (currentShuffledPlaylist == null || currentShuffledPlaylist.Count == 0)
            return null;
        
        AudioClip track = currentShuffledPlaylist[currentTrackIndex];
        
        // Advance to next track
        currentTrackIndex = (currentTrackIndex + 1) % currentShuffledPlaylist.Count;
        
        // If we've completed the playlist and shuffling is enabled, reshuffle
        if (currentTrackIndex == 0 && currentPhaseEntry != null && currentPhaseEntry.shuffleTracks)
        {
            SetupPlaylistForPhase(currentPhaseEntry);
        }
        
        return track;
    }
    
    /// <summary>
    /// Play a track with fade in
    /// </summary>
    private void PlayTrackWithFadeIn(AudioClip track)
    {
        if (track == null) return;
        
        // Stop any existing coroutines
        StopAllMusicCoroutines();
        
        // Setup the audio source
        currentActiveSource.clip = track;
        currentActiveSource.loop = currentPhaseEntry?.loopIndividualTracks ?? true;
        currentActiveSource.volume = 0f;
        currentActiveSource.Play();
        
        // Start fade in
        StartCoroutine(FadeAudioSource(currentActiveSource, 0f, GetTargetVolume(), fadeInDuration));
        
        // Start track monitoring if not looping individual tracks
        if (currentPhaseEntry != null && !currentPhaseEntry.loopIndividualTracks)
        {
            trackMonitorCoroutine = StartCoroutine(MonitorTrackEnd());
        }
        
        if (debugMode)
        {
            /* Debug.Log($"MusicManager: Playing track {track.name} with fade in"); */
        }
    }
    
    /// <summary>
    /// Crossfade to a new track
    /// </summary>
    private void CrossfadeToTrack(AudioClip track)
    {
        if (track == null) return;
        
        // Stop existing crossfade
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }
        
        crossfadeCoroutine = StartCoroutine(PerformCrossfade(track));
    }
    
    /// <summary>
    /// Perform the crossfade between tracks
    /// </summary>
    private IEnumerator PerformCrossfade(AudioClip newTrack)
    {
        // Get the inactive audio source for the new track
        AudioSource newSource = (currentActiveSource == primaryAudioSource) ? secondaryAudioSource : primaryAudioSource;
        AudioSource oldSource = currentActiveSource;
        
        // Setup new track
        newSource.clip = newTrack;
        newSource.loop = currentPhaseEntry?.loopIndividualTracks ?? true;
        newSource.volume = 0f;
        newSource.Play();
        
        // Crossfade
        float crossfadeTime = 0f;
        float targetVolume = GetTargetVolume();
        
        while (crossfadeTime < defaultCrossfadeDuration)
        {
            crossfadeTime += Time.deltaTime;
            float progress = crossfadeTime / defaultCrossfadeDuration;
            
            // Fade out old source
            oldSource.volume = Mathf.Lerp(targetVolume, 0f, progress);
            
            // Fade in new source
            newSource.volume = Mathf.Lerp(0f, targetVolume, progress);
            
            yield return null;
        }
        
        // Ensure final volumes
        oldSource.volume = 0f;
        newSource.volume = targetVolume;
        
        // Stop old source and switch active source
        oldSource.Stop();
        currentActiveSource = newSource;
        
        // Start track monitoring if not looping individual tracks
        if (currentPhaseEntry != null && !currentPhaseEntry.loopIndividualTracks)
        {
            if (trackMonitorCoroutine != null)
            {
                StopCoroutine(trackMonitorCoroutine);
            }
            trackMonitorCoroutine = StartCoroutine(MonitorTrackEnd());
        }
        
        if (debugMode)
        {
            /* Debug.Log($"MusicManager: Crossfaded to track {newTrack.name}"); */
        }
        
        crossfadeCoroutine = null;
    }
    
    /// <summary>
    /// Monitor when a track ends to play the next one
    /// </summary>
    private IEnumerator MonitorTrackEnd()
    {
        if (currentActiveSource.clip == null) yield break;
        
        float trackLength = currentActiveSource.clip.length;
        float minimumWait = Mathf.Min(minimumTrackDuration, trackLength * 0.8f);
        
        // Wait for minimum duration
        yield return new WaitForSeconds(minimumWait);
        
        // Wait for track to finish
        while (currentActiveSource.isPlaying && currentActiveSource.time < trackLength - 0.1f)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Play next track
        AudioClip nextTrack = GetNextTrack();
        if (nextTrack != null)
        {
            CrossfadeToTrack(nextTrack);
        }
        
        trackMonitorCoroutine = null;
    }
    
    /// <summary>
    /// Fade out current music
    /// </summary>
    private void FadeOutCurrentMusic()
    {
        if (currentActiveSource != null && currentActiveSource.isPlaying)
        {
            StartCoroutine(FadeAudioSource(currentActiveSource, currentActiveSource.volume, 0f, fadeOutDuration, true));
        }
    }
    
    /// <summary>
    /// Generic fade coroutine for audio sources
    /// </summary>
    private IEnumerator FadeAudioSource(AudioSource source, float startVolume, float targetVolume, float duration, bool stopAfterFade = false)
    {
        float fadeTime = 0f;
        
        while (fadeTime < duration)
        {
            fadeTime += Time.deltaTime;
            float progress = fadeTime / duration;
            source.volume = Mathf.Lerp(startVolume, targetVolume, progress);
            yield return null;
        }
        
        source.volume = targetVolume;
        
        if (stopAfterFade && targetVolume <= 0f)
        {
            source.Stop();
        }
    }
    
    /// <summary>
    /// Get target volume based on master volume and phase volume
    /// </summary>
    private float GetTargetVolume()
    {
        float phaseVolume = currentPhaseEntry?.phaseVolume ?? 1f;
        return masterVolume * phaseVolume;
    }
    
    /// <summary>
    /// Stop all music-related coroutines
    /// </summary>
    private void StopAllMusicCoroutines()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }
        
        if (trackMonitorCoroutine != null)
        {
            StopCoroutine(trackMonitorCoroutine);
            trackMonitorCoroutine = null;
        }
    }
    
    #region Public API
    
    /// <summary>
    /// Set master volume for all music
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        
        // Update current playing volume
        if (currentActiveSource != null && currentActiveSource.isPlaying)
        {
            currentActiveSource.volume = GetTargetVolume();
        }
    }
    
    /// <summary>
    /// Get current master volume
    /// </summary>
    public float GetMasterVolume() => masterVolume;
    
    /// <summary>
    /// Manually trigger music for a specific phase
    /// </summary>
    public void PlayMusicForPhase(GamePhaseManager.GamePhase phase)
    {
        TransitionToPhaseMusic(phase);
    }
    
    /// <summary>
    /// Stop all music with fade out
    /// </summary>
    public void StopMusic()
    {
        StopAllMusicCoroutines();
        FadeOutCurrentMusic();
    }
    
    /// <summary>
    /// Pause current music
    /// </summary>
    public void PauseMusic()
    {
        if (currentActiveSource != null)
        {
            currentActiveSource.Pause();
        }
    }
    
    /// <summary>
    /// Resume paused music
    /// </summary>
    public void ResumeMusic()
    {
        if (currentActiveSource != null)
        {
            currentActiveSource.UnPause();
        }
    }
    
    /// <summary>
    /// Skip to next track in current phase
    /// </summary>
    public void SkipToNextTrack()
    {
        if (currentShuffledPlaylist != null && currentShuffledPlaylist.Count > 1)
        {
            AudioClip nextTrack = GetNextTrack();
            if (nextTrack != null)
            {
                CrossfadeToTrack(nextTrack);
            }
        }
    }
    
    /// <summary>
    /// Get current playing track name
    /// </summary>
    public string GetCurrentTrackName()
    {
        return currentActiveSource?.clip?.name ?? "None";
    }
    
    /// <summary>
    /// Get current phase
    /// </summary>
    public GamePhaseManager.GamePhase GetCurrentPhase() => currentPhase;
    
    /// <summary>
    /// Check if music is currently playing
    /// </summary>
    public bool IsPlaying()
    {
        return currentActiveSource != null && currentActiveSource.isPlaying;
    }
    
    #endregion
} 