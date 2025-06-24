using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Universal click and hover sound handler that can be attached to any interactive object.
/// Works with UI elements (Button, Image, etc.), 3D colliders, and any object that can receive mouse events.
/// Integrates with SoundEffectManager for consistent audio management.
/// </summary>
public class ClickSoundHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Click Sound Settings")]
    [Tooltip("Name of the custom sound to play from SoundEffectManager database. Leave empty to use default sound.")]
    [SerializeField] private string clickSoundName = "";
    
    [Tooltip("Should this play sound on left click?")]
    [SerializeField] private bool playOnLeftClick = true;
    
    [Tooltip("Should this play sound on right click?")]
    [SerializeField] private bool playOnRightClick = false;
    
    [Tooltip("Should this play sound on middle click?")]
    [SerializeField] private bool playOnMiddleClick = false;
    
    [Header("Hover Sound Settings")]
    [Tooltip("Enable hover sound effects")]
    [SerializeField] private bool enableHoverSounds = true;
    
    [Tooltip("Name of the custom sound to play on mouse enter. Leave empty to use default sound.")]
    [SerializeField] private string hoverEnterSoundName = "";
    
    [Tooltip("Name of the custom sound to play on mouse exit. Leave empty for no exit sound.")]
    [SerializeField] private string hoverExitSoundName = "";
    
    [Tooltip("Should hover sounds work on 3D objects? (Uses OnMouseEnter/Exit)")]
    [SerializeField] private bool enable3DHoverSounds = true;
    
    [Header("Fallback Audio (if SoundEffectManager not available)")]
    [Tooltip("Direct audio clip to play for clicks if SoundEffectManager is not available")]
    [SerializeField] private AudioClip fallbackClickClip;
    
    [Tooltip("Direct audio clip to play for hover enter if SoundEffectManager is not available")]
    [SerializeField] private AudioClip fallbackHoverEnterClip;
    
    [Tooltip("Direct audio clip to play for hover exit if SoundEffectManager is not available")]
    [SerializeField] private AudioClip fallbackHoverExitClip;
    
    [Tooltip("Volume for fallback audio")]
    [Range(0f, 1f)]
    [SerializeField] private float fallbackVolume = 0.7f;
    
    [Header("Settings")]
    [Tooltip("Use 3D positioned audio? If false, plays as 2D UI sound")]
    [SerializeField] private bool use3DAudio = false;
    
    [Tooltip("Override position for 3D audio. If null, uses this transform's position")]
    [SerializeField] private Transform audioPositionOverride;
    
    [Tooltip("Entity IDs for visibility checking. Use 0 if not applicable")]
    [SerializeField] private uint sourceEntityId = 0;
    [SerializeField] private uint targetEntityId = 0;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Components
    private Button button;
    private Collider objectCollider;
    private Collider2D objectCollider2D;
    private AudioSource fallbackAudioSource;
    
    private void Awake()
    {
        // Cache components
        button = GetComponent<Button>();
        objectCollider = GetComponent<Collider>();
        objectCollider2D = GetComponent<Collider2D>();
        
        // Set up for different types of objects
        SetupClickDetection();
    }
    
    private void SetupClickDetection()
    {
        // For UI elements, we rely on IPointerClickHandler
        // For 3D objects with colliders, we also handle OnMouseDown
        // The script will automatically work with both
        
        if (debugMode)
        {
            string componentInfo = "";
            if (button) componentInfo += "Button ";
            if (objectCollider) componentInfo += "Collider3D ";
            if (objectCollider2D) componentInfo += "Collider2D ";
            
            Debug.Log($"ClickSoundHandler: Setup on {gameObject.name} with components: {componentInfo}");
        }
    }
    
    /// <summary>
    /// Handle UI click events (works with Button, Image, etc.)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        bool shouldPlay = false;
        
        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                shouldPlay = playOnLeftClick;
                break;
            case PointerEventData.InputButton.Right:
                shouldPlay = playOnRightClick;
                break;
            case PointerEventData.InputButton.Middle:
                shouldPlay = playOnMiddleClick;
                break;
        }
        
        if (shouldPlay)
        {
            PlayClickSound();
        }
    }
    
    /// <summary>
    /// Handle 3D object clicks (works with Colliders)
    /// </summary>
    private void OnMouseDown()
    {
        // Only handle if this is a 3D collider and left click
        if ((objectCollider != null || objectCollider2D != null) && playOnLeftClick)
        {
            // Check if we clicked with left mouse button
            if (Input.GetMouseButtonDown(0))
            {
                PlayClickSound();
            }
        }
    }
    
    /// <summary>
    /// Handle UI hover enter events
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (enableHoverSounds)
        {
            PlayHoverEnterSound();
        }
    }
    
    /// <summary>
    /// Handle UI hover exit events
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (enableHoverSounds)
        {
            PlayHoverExitSound();
        }
    }
    
    /// <summary>
    /// Handle 3D object hover enter (works with Colliders)
    /// </summary>
    private void OnMouseEnter()
    {
        if (enableHoverSounds && enable3DHoverSounds && (objectCollider != null || objectCollider2D != null))
        {
            PlayHoverEnterSound();
        }
    }
    
    /// <summary>
    /// Handle 3D object hover exit (works with Colliders)
    /// </summary>
    private void OnMouseExit()
    {
        if (enableHoverSounds && enable3DHoverSounds && (objectCollider != null || objectCollider2D != null))
        {
            PlayHoverExitSound();
        }
    }
    
    /// <summary>
    /// Public method to manually trigger click sound
    /// </summary>
    public void TriggerClickSound()
    {
        PlayClickSound();
    }
    
    /// <summary>
    /// Public method to manually trigger hover enter sound
    /// </summary>
    public void TriggerHoverEnterSound()
    {
        PlayHoverEnterSound();
    }
    
    /// <summary>
    /// Public method to manually trigger hover exit sound
    /// </summary>
    public void TriggerHoverExitSound()
    {
        PlayHoverExitSound();
    }
    
    /// <summary>
    /// Main method to play the click sound
    /// </summary>
    private void PlayClickSound()
    {
        Vector3 audioPosition = GetAudioPosition();
        
        if (debugMode)
        {
            Debug.Log($"ClickSoundHandler: Playing click sound on {gameObject.name} at position {audioPosition}");
        }
        
        // Try to use SoundEffectManager first
        if (SoundEffectManager.Instance != null)
        {
            if (string.IsNullOrEmpty(clickSoundName))
            {
                // Use default sound
                SoundEffectManager.TriggerSoundEffect(audioPosition, sourceEntityId, targetEntityId);
            }
            else
            {
                // Use named custom sound
                SoundEffectManager.TriggerNamedSoundEffect(audioPosition, clickSoundName, sourceEntityId, targetEntityId);
            }
        }
        else
        {
            // Fallback to local audio playback
            PlayFallbackSound(audioPosition, fallbackClickClip, "click");
        }
    }
    
    /// <summary>
    /// Gets the position where audio should be played
    /// </summary>
    private Vector3 GetAudioPosition()
    {
        if (audioPositionOverride != null)
        {
            return audioPositionOverride.position;
        }
        
        if (use3DAudio)
        {
            return transform.position;
        }
        
        // For UI sounds, use camera position or world origin
        Camera mainCam = Camera.main;
        return mainCam != null ? mainCam.transform.position : Vector3.zero;
    }
    
    /// <summary>
    /// Main method to play the hover enter sound
    /// </summary>
    private void PlayHoverEnterSound()
    {
        Vector3 audioPosition = GetAudioPosition();
        
        if (debugMode)
        {
            Debug.Log($"ClickSoundHandler: Playing hover enter sound on {gameObject.name} at position {audioPosition}");
        }
        
        // Try to use SoundEffectManager first
        if (SoundEffectManager.Instance != null)
        {
            if (string.IsNullOrEmpty(hoverEnterSoundName))
            {
                // Use default sound
                SoundEffectManager.TriggerSoundEffect(audioPosition, sourceEntityId, targetEntityId);
            }
            else
            {
                // Use named custom sound
                SoundEffectManager.TriggerNamedSoundEffect(audioPosition, hoverEnterSoundName, sourceEntityId, targetEntityId);
            }
        }
        else
        {
            // Fallback to local audio playback
            PlayFallbackSound(audioPosition, fallbackHoverEnterClip, "hover enter");
        }
    }
    
    /// <summary>
    /// Main method to play the hover exit sound
    /// </summary>
    private void PlayHoverExitSound()
    {
        if (string.IsNullOrEmpty(hoverExitSoundName) && fallbackHoverExitClip == null)
        {
            // No exit sound configured
            return;
        }
        
        Vector3 audioPosition = GetAudioPosition();
        
        if (debugMode)
        {
            Debug.Log($"ClickSoundHandler: Playing hover exit sound on {gameObject.name} at position {audioPosition}");
        }
        
        // Try to use SoundEffectManager first
        if (SoundEffectManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(hoverExitSoundName))
            {
                // Use named custom sound
                SoundEffectManager.TriggerNamedSoundEffect(audioPosition, hoverExitSoundName, sourceEntityId, targetEntityId);
            }
        }
        else
        {
            // Fallback to local audio playback
            PlayFallbackSound(audioPosition, fallbackHoverExitClip, "hover exit");
        }
    }
    
    /// <summary>
    /// Plays fallback sound when SoundEffectManager is not available
    /// </summary>
    private void PlayFallbackSound(Vector3 position, AudioClip clip, string soundType)
    {
        if (clip == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"ClickSoundHandler: No SoundEffectManager and no fallback {soundType} clip assigned on {gameObject.name}");
            }
            return;
        }
        
        // Create or get fallback audio source
        if (fallbackAudioSource == null)
        {
            GameObject audioGO = new GameObject("InteractiveSoundAudio");
            audioGO.transform.SetParent(transform);
            fallbackAudioSource = audioGO.AddComponent<AudioSource>();
            fallbackAudioSource.playOnAwake = false;
            fallbackAudioSource.spatialBlend = use3DAudio ? 1f : 0f;
        }
        
        // Set position and play
        fallbackAudioSource.transform.position = position;
        fallbackAudioSource.clip = clip;
        fallbackAudioSource.volume = fallbackVolume;
        fallbackAudioSource.Play();
        
        if (debugMode)
        {
            /* Debug.Log($"ClickSoundHandler: Playing fallback {soundType} sound {clip.name} on {gameObject.name}"); */
        }
    }
    
    /// <summary>
    /// Set the click sound name at runtime
    /// </summary>
    public void SetClickSoundName(string newSoundName)
    {
        clickSoundName = newSoundName;
    }
    
    /// <summary>
    /// Set the hover enter sound name at runtime
    /// </summary>
    public void SetHoverEnterSoundName(string newSoundName)
    {
        hoverEnterSoundName = newSoundName;
    }
    
    /// <summary>
    /// Set the hover exit sound name at runtime
    /// </summary>
    public void SetHoverExitSoundName(string newSoundName)
    {
        hoverExitSoundName = newSoundName;
    }
    
    /// <summary>
    /// Enable or disable hover sounds at runtime
    /// </summary>
    public void SetHoverSoundsEnabled(bool enabled)
    {
        enableHoverSounds = enabled;
    }
    
    /// <summary>
    /// Set entity IDs for visibility checking
    /// </summary>
    public void SetEntityIds(uint source, uint target)
    {
        sourceEntityId = source;
        targetEntityId = target;
    }
    
    /// <summary>
    /// Enable/disable click sound
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
} 