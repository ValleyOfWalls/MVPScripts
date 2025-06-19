using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Creates a diagonal shine effect that moves across UI elements or 3D objects.
/// Works with both UI Images/RawImages and 3D renderers with materials.
/// Attach to any GameObject with a Renderer or Image component.
/// </summary>
public class DiagonalShineEffect : MonoBehaviour
{
    [Header("Shine Settings")]
    [SerializeField] private bool enableShine = true;
    [SerializeField] private float shineInterval = 3f; // Time between shine effects
    [SerializeField] private float shineDuration = 1f; // Duration of each shine animation
    [SerializeField] private float shineIntensity = 1.5f; // Brightness multiplier for shine
    [SerializeField] private float shineWidth = 0.3f; // Width of the shine band (0-1)
    [SerializeField] private float shineAngle = 45f; // Angle of the shine in degrees
    [SerializeField] private bool randomizeInterval = true; // Add randomness to shine timing
    [SerializeField] private Vector2 intervalRange = new Vector2(2f, 5f); // Random interval range
    
    [Header("Shine Movement")]
    [SerializeField] private AnimationCurve shineCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool reverseDirection = false; // Reverse shine direction
    [SerializeField] private float shineStartOffset = -0.5f; // Start position offset (-1 to 1)
    [SerializeField] private float shineEndOffset = 1.5f; // End position offset (-1 to 1)
    
    [Header("Color Settings")]
    [SerializeField] private Color shineColor = Color.white;
    [SerializeField] private bool useOriginalColorTint = true; // Blend with original color
    [SerializeField] private float colorBlendFactor = 0.5f; // How much to blend with original color
    
    [Header("Advanced Settings")]
    [SerializeField] private bool shineOnStart = false; // Play shine effect immediately on start
    [SerializeField] private bool shineOnEnable = false; // Play shine effect when enabled
    [SerializeField] private bool useUnscaledTime = false; // Use unscaled time for animations
    [SerializeField] private LayerMask affectedLayers = -1; // Which layers to affect (for 3D objects)
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool showGizmos = false;
    
    // Component references
    private Image uiImage;
    private RawImage uiRawImage;
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Material shineMaterial;
    
    // State tracking
    private bool isShining = false;
    private Coroutine shineCoroutine;
    private Coroutine intervalCoroutine;
    private Color originalColor;
    private Vector2 currentShinePosition;
    
    // Material property IDs (for performance)
    private static readonly int ShinePosition = Shader.PropertyToID("_ShinePosition");
    private static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
    private static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
    private static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
    private static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        if (shineOnStart)
        {
            PlayShineEffect();
        }
        
        if (enableShine)
        {
            StartShineLoop();
        }
    }
    
    private void OnEnable()
    {
        if (shineOnEnable && Application.isPlaying)
        {
            PlayShineEffect();
        }
    }
    
    private void OnDisable()
    {
        StopAllShineEffects();
    }
    
    private void OnDestroy()
    {
        CleanupMaterials();
    }
    
    #endregion
    
    #region Initialization
    
    private void InitializeComponents()
    {
        // Try to find UI components first
        uiImage = GetComponent<Image>();
        uiRawImage = GetComponent<RawImage>();
        
        // If no UI components, try 3D renderer
        if (uiImage == null && uiRawImage == null)
        {
            objectRenderer = GetComponent<Renderer>();
            
            if (objectRenderer != null)
            {
                // Store original material
                originalMaterial = objectRenderer.material;
                CreateShineMaterial();
            }
        }
        
        // Store original color
        StoreOriginalColor();
        
        LogDebug($"Initialized shine effect on {gameObject.name}. UI Image: {uiImage != null}, UI RawImage: {uiRawImage != null}, Renderer: {objectRenderer != null}");
    }
    
    private void StoreOriginalColor()
    {
        if (uiImage != null)
        {
            originalColor = uiImage.color;
        }
        else if (uiRawImage != null)
        {
            originalColor = uiRawImage.color;
        }
        else if (objectRenderer != null && originalMaterial != null)
        {
            originalColor = originalMaterial.color;
        }
        else
        {
            originalColor = Color.white;
        }
    }
    
    private void CreateShineMaterial()
    {
        if (originalMaterial == null) return;
        
        // Create a copy of the original material for shine effects
        shineMaterial = new Material(originalMaterial);
        
        // Set initial shine properties if the shader supports them
        if (shineMaterial.HasProperty(ShinePosition))
        {
            shineMaterial.SetVector(ShinePosition, Vector2.zero);
        }
        if (shineMaterial.HasProperty(ShineWidth))
        {
            shineMaterial.SetFloat(ShineWidth, shineWidth);
        }
        if (shineMaterial.HasProperty(ShineAngle))
        {
            shineMaterial.SetFloat(ShineAngle, shineAngle);
        }
        if (shineMaterial.HasProperty(ShineIntensity))
        {
            shineMaterial.SetFloat(ShineIntensity, 0f); // Start with no shine
        }
        if (shineMaterial.HasProperty(ShineColor))
        {
            shineMaterial.SetColor(ShineColor, shineColor);
        }
        
        objectRenderer.material = shineMaterial;
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Plays a single shine effect
    /// </summary>
    public void PlayShineEffect()
    {
        if (!enabled || (!uiImage && !uiRawImage && !objectRenderer))
        {
            LogDebug("Cannot play shine effect: component not enabled or no valid target found");
            return;
        }
        
        if (shineCoroutine != null)
        {
            StopCoroutine(shineCoroutine);
        }
        
        shineCoroutine = StartCoroutine(ShineAnimation());
    }
    
    /// <summary>
    /// Starts the automatic shine loop
    /// </summary>
    public void StartShineLoop()
    {
        if (!enableShine) return;
        
        StopShineLoop();
        intervalCoroutine = StartCoroutine(ShineIntervalLoop());
    }
    
    /// <summary>
    /// Stops the automatic shine loop
    /// </summary>
    public void StopShineLoop()
    {
        if (intervalCoroutine != null)
        {
            StopCoroutine(intervalCoroutine);
            intervalCoroutine = null;
        }
    }
    
    /// <summary>
    /// Stops all shine effects
    /// </summary>
    public void StopAllShineEffects()
    {
        StopShineLoop();
        
        if (shineCoroutine != null)
        {
            StopCoroutine(shineCoroutine);
            shineCoroutine = null;
        }
        
        isShining = false;
        RestoreOriginalAppearance();
    }
    
    /// <summary>
    /// Enables or disables the shine effect
    /// </summary>
    public void SetShineEnabled(bool enabled)
    {
        enableShine = enabled;
        
        if (enabled)
        {
            StartShineLoop();
        }
        else
        {
            StopAllShineEffects();
        }
    }
    
    #endregion
    
    #region Animation Coroutines
    
    private IEnumerator ShineIntervalLoop()
    {
        while (enableShine)
        {
            float waitTime = randomizeInterval ? 
                Random.Range(intervalRange.x, intervalRange.y) : 
                shineInterval;
            
            float elapsedTime = 0f;
            while (elapsedTime < waitTime)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
            
            if (enableShine)
            {
                PlayShineEffect();
            }
        }
    }
    
    private IEnumerator ShineAnimation()
    {
        isShining = true;
        LogDebug($"Starting shine animation on {gameObject.name}");
        
        float elapsed = 0f;
        
        while (elapsed < shineDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = elapsed / shineDuration;
            float curveValue = shineCurve.Evaluate(t);
            
            // Calculate shine position
            float shinePos = reverseDirection ? 
                Mathf.Lerp(shineEndOffset, shineStartOffset, curveValue) :
                Mathf.Lerp(shineStartOffset, shineEndOffset, curveValue);
            
            currentShinePosition = new Vector2(shinePos, 0f);
            
            // Apply shine effect based on component type
            ApplyShineEffect(curveValue, shinePos);
            
            yield return null;
        }
        
        // Ensure clean end state
        RestoreOriginalAppearance();
        isShining = false;
        shineCoroutine = null;
        
        LogDebug($"Shine animation completed on {gameObject.name}");
    }
    
    #endregion
    
    #region Shine Application
    
    private void ApplyShineEffect(float normalizedTime, float shinePosition)
    {
        // Calculate shine intensity based on position and width
        float distanceFromCenter = Mathf.Abs(shinePosition - 0.5f);
        float maxDistance = (shineEndOffset - shineStartOffset) * 0.5f;
        float intensity = 1f - Mathf.Clamp01(distanceFromCenter / maxDistance);
        
        // Apply width modulation
        if (shinePosition >= -shineWidth * 0.5f && shinePosition <= 1f + shineWidth * 0.5f)
        {
            intensity *= shineIntensity;
        }
        else
        {
            intensity = 0f;
        }
        
        if (uiImage != null)
        {
            ApplyUIShine(uiImage, intensity);
        }
        else if (uiRawImage != null)
        {
            ApplyUIShine(uiRawImage, intensity);
        }
        else if (objectRenderer != null && shineMaterial != null)
        {
            Apply3DShine(intensity, shinePosition);
        }
    }
    
    private void ApplyUIShine(Graphic graphic, float intensity)
    {
        if (graphic == null) return;
        
        Color shineColorFinal = useOriginalColorTint ? 
            Color.Lerp(originalColor, shineColor, colorBlendFactor) : 
            shineColor;
        
        Color currentColor = Color.Lerp(originalColor, shineColorFinal, intensity);
        graphic.color = currentColor;
    }
    
    private void Apply3DShine(float intensity, float shinePosition)
    {
        if (shineMaterial == null) return;
        
        // Update material properties if supported
        if (shineMaterial.HasProperty(ShinePosition))
        {
            Vector2 pos = CalculateShinePositionForAngle(shinePosition);
            shineMaterial.SetVector(ShinePosition, pos);
        }
        
        if (shineMaterial.HasProperty(ShineIntensity))
        {
            shineMaterial.SetFloat(ShineIntensity, intensity);
        }
        
        if (shineMaterial.HasProperty(ShineWidth))
        {
            shineMaterial.SetFloat(ShineWidth, shineWidth);
        }
        
        if (shineMaterial.HasProperty(ShineAngle))
        {
            shineMaterial.SetFloat(ShineAngle, shineAngle);
        }
        
        if (shineMaterial.HasProperty(ShineColor))
        {
            Color shineColorFinal = useOriginalColorTint ? 
                Color.Lerp(originalColor, shineColor, colorBlendFactor) : 
                shineColor;
            shineMaterial.SetColor(ShineColor, shineColorFinal);
        }
        
        // Fallback: modify main color if no shine properties
        if (!shineMaterial.HasProperty(ShineIntensity))
        {
            Color shineColorFinal = useOriginalColorTint ? 
                Color.Lerp(originalColor, shineColor, colorBlendFactor) : 
                shineColor;
            
            Color currentColor = Color.Lerp(originalColor, shineColorFinal, intensity);
            shineMaterial.color = currentColor;
        }
    }
    
    private Vector2 CalculateShinePositionForAngle(float shinePosition)
    {
        float angleRad = shineAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        return direction * shinePosition;
    }
    
    private void RestoreOriginalAppearance()
    {
        if (uiImage != null)
        {
            uiImage.color = originalColor;
        }
        else if (uiRawImage != null)
        {
            uiRawImage.color = originalColor;
        }
        else if (objectRenderer != null && shineMaterial != null)
        {
            if (shineMaterial.HasProperty(ShineIntensity))
            {
                shineMaterial.SetFloat(ShineIntensity, 0f);
            }
            else
            {
                shineMaterial.color = originalColor;
            }
        }
    }
    
    #endregion
    
    #region Material Management
    
    private void CleanupMaterials()
    {
        if (shineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(shineMaterial);
            }
            else
            {
                DestroyImmediate(shineMaterial);
            }
            shineMaterial = null;
        }
    }
    
    #endregion
    
    #region Editor Support
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Clamp values to reasonable ranges
        shineInterval = Mathf.Max(0.1f, shineInterval);
        shineDuration = Mathf.Max(0.1f, shineDuration);
        shineIntensity = Mathf.Max(0f, shineIntensity);
        shineWidth = Mathf.Clamp01(shineWidth);
        colorBlendFactor = Mathf.Clamp01(colorBlendFactor);
        
        if (intervalRange.x > intervalRange.y)
        {
            intervalRange.y = intervalRange.x;
        }
        intervalRange.x = Mathf.Max(0.1f, intervalRange.x);
        intervalRange.y = Mathf.Max(0.1f, intervalRange.y);
        
        // Update material properties in editor if possible
        if (Application.isPlaying && shineMaterial != null)
        {
            if (shineMaterial.HasProperty(ShineWidth))
                shineMaterial.SetFloat(ShineWidth, shineWidth);
            if (shineMaterial.HasProperty(ShineAngle))
                shineMaterial.SetFloat(ShineAngle, shineAngle);
            if (shineMaterial.HasProperty(ShineColor))
                shineMaterial.SetColor(ShineColor, shineColor);
        }
    }
    
    /// <summary>
    /// Context menu to test shine effect
    /// </summary>
    [UnityEditor.MenuItem("CONTEXT/DiagonalShineEffect/Test Shine Effect")]
    private static void TestShineEffectContextMenu(UnityEditor.MenuCommand command)
    {
        DiagonalShineEffect shineEffect = (DiagonalShineEffect)command.context;
        if (shineEffect != null && Application.isPlaying)
        {
            shineEffect.PlayShineEffect();
        }
    }
#endif
    
    #endregion
    
    #region Debug and Gizmos
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[DiagonalShineEffect] {message}");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // Draw shine direction
        Vector3 center = transform.position;
        float angleRad = shineAngle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
        
        Gizmos.color = shineColor;
        Gizmos.DrawLine(center - direction * 2f, center + direction * 2f);
        
        // Draw shine position if shining
        if (isShining)
        {
            Vector3 shinePos = center + (Vector3)currentShinePosition;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(shinePos, 0.1f);
        }
    }
    
    #endregion
} 