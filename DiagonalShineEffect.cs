using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates a diagonal shine effect that moves across UI elements using a custom shader.
/// Works with UI Images that support custom materials.
/// Attach to any GameObject with Image components (either on this object or child objects).
/// </summary>
public class DiagonalShineEffect : MonoBehaviour
{
    [Header("Shine Settings")]
    [SerializeField] private bool enableShine = true;
    [SerializeField] private float shineInterval = 3f; // Time between shine effects
    [SerializeField] private float shineDuration = 1f; // Duration of each shine animation
    [SerializeField] private float shineIntensity = 2f; // Brightness of shine
    [SerializeField] private float shineWidth = 0.1f; // Width of the shine band (0-1)
    [SerializeField] private float shineAngle = 45f; // Angle of the shine in degrees
    [SerializeField] private bool randomizeInterval = true; // Add randomness to shine timing
    [SerializeField] private Vector2 intervalRange = new Vector2(2f, 5f); // Random interval range
    [SerializeField] private float shineSoftness = 0.1f; // Softness of shine edges
    
    [Header("Shine Movement")]
    [SerializeField] private AnimationCurve shineCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool reverseDirection = false; // Reverse shine direction
    [SerializeField] private float shineStartPosition = -0.5f; // Start position (-1 to 2)
    [SerializeField] private float shineEndPosition = 1.5f; // End position (-1 to 2)
    
    [Header("Color Settings")]
    [SerializeField] private Color shineColor = Color.white;
    
    [Header("Material Settings")]
    [SerializeField] private Material shineMaterialTemplate; // Template material with DiagonalShine shader
    [SerializeField] private bool includeChildComponents = true; // Search for Image components in child objects
    
    [Header("Advanced Settings")]
    [SerializeField] private bool shineOnStart = false; // Play shine effect immediately on start
    [SerializeField] private bool shineOnEnable = false; // Play shine effect when enabled
    [SerializeField] private bool useUnscaledTime = false; // Use unscaled time for animations
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Component tracking
    private List<ImageShineData> imageShineData = new List<ImageShineData>();
    
    // State tracking
    private bool isShining = false;
    private Coroutine shineCoroutine;
    private Coroutine intervalCoroutine;
    
    // Material property IDs (for performance)
    private static readonly int ShineLocation = Shader.PropertyToID("_ShineLocation");
    private static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
    private static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
    private static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
    private static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
    private static readonly int ShineSoftness = Shader.PropertyToID("_ShineSoftness");
    
    // Class to track each image and its materials
    [System.Serializable]
    private class ImageShineData
    {
        public Image image;
        public Material originalMaterial;
        public Material shineMaterial;
        public bool hadMaterial;
        
        public ImageShineData(Image img)
        {
            image = img;
            originalMaterial = img.material;
            hadMaterial = originalMaterial != null && originalMaterial != img.defaultMaterial;
        }
    }
    
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
        imageShineData.Clear();
        
        // Find all images to apply shine to
        List<Image> imagesToProcess = new List<Image>();
        
        // Check for image on this GameObject
        Image mainImage = GetComponent<Image>();
        if (mainImage != null)
        {
            imagesToProcess.Add(mainImage);
        }
        
        // Check child images if enabled
        if (includeChildComponents)
        {
            Image[] childImages = GetComponentsInChildren<Image>(true);
            foreach (Image img in childImages)
            {
                if (img != mainImage) // Don't add the main image twice
                {
                    imagesToProcess.Add(img);
                }
            }
        }
        
        // Create shine data for each image
        foreach (Image img in imagesToProcess)
        {
            ImageShineData data = new ImageShineData(img);
            CreateShineMaterial(data);
            imageShineData.Add(data);
        }
        
        LogDebug($"Initialized shine effect on {gameObject.name}. Found {imageShineData.Count} images to process.");
    }
    
    private void CreateShineMaterial(ImageShineData data)
    {
        if (shineMaterialTemplate == null)
        {
            LogDebug($"No shine material template assigned. Please assign a material using the DiagonalShine shader.");
            return;
        }
        
        // Create instance of the shine material
        data.shineMaterial = new Material(shineMaterialTemplate);
        
        // Set initial shine properties
        data.shineMaterial.SetFloat(ShineLocation, shineStartPosition);
        data.shineMaterial.SetFloat(ShineWidth, shineWidth);
        data.shineMaterial.SetFloat(ShineAngle, shineAngle);
        data.shineMaterial.SetFloat(ShineIntensity, 0f); // Start with no shine
        data.shineMaterial.SetColor(ShineColor, shineColor);
        data.shineMaterial.SetFloat(ShineSoftness, shineSoftness);
        
        // Copy the original texture to the shine material
        if (data.image.sprite != null)
        {
            data.shineMaterial.SetTexture("_MainTex", data.image.sprite.texture);
        }
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Plays a single shine effect
    /// </summary>
    public void PlayShineEffect()
    {
        if (!enabled || imageShineData.Count == 0)
        {
            LogDebug("Cannot play shine effect: component not enabled or no valid images found");
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
        RestoreOriginalMaterials();
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
        
        // Apply shine materials to all images
        foreach (var data in imageShineData)
        {
            if (data.image != null && data.shineMaterial != null)
            {
                data.image.material = data.shineMaterial;
            }
        }
        
        float elapsed = 0f;
        
        while (elapsed < shineDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = elapsed / shineDuration;
            float curveValue = shineCurve.Evaluate(t);
            
            // Calculate shine position
            float shinePos = reverseDirection ? 
                Mathf.Lerp(shineEndPosition, shineStartPosition, curveValue) :
                Mathf.Lerp(shineStartPosition, shineEndPosition, curveValue);
            
            // Update all shine materials
            foreach (var data in imageShineData)
            {
                if (data.shineMaterial != null)
                {
                    data.shineMaterial.SetFloat(ShineLocation, shinePos);
                    data.shineMaterial.SetFloat(ShineIntensity, shineIntensity);
                    data.shineMaterial.SetFloat(ShineWidth, shineWidth);
                    data.shineMaterial.SetFloat(ShineAngle, shineAngle);
                    data.shineMaterial.SetColor(ShineColor, shineColor);
                    data.shineMaterial.SetFloat(ShineSoftness, shineSoftness);
                }
            }
            
            yield return null;
        }
        
        // Restore original materials
        RestoreOriginalMaterials();
        
        isShining = false;
        shineCoroutine = null;
        
        LogDebug($"Shine animation completed on {gameObject.name}");
    }
    
    #endregion
    
    #region Material Management
    
    private void RestoreOriginalMaterials()
    {
        foreach (var data in imageShineData)
        {
            if (data.image != null)
            {
                data.image.material = data.hadMaterial ? data.originalMaterial : null;
            }
        }
    }
    
    private void CleanupMaterials()
    {
        foreach (var data in imageShineData)
        {
            if (data.shineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(data.shineMaterial);
                }
                else
                {
                    DestroyImmediate(data.shineMaterial);
                }
            }
        }
        imageShineData.Clear();
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
        shineWidth = Mathf.Clamp(shineWidth, 0.001f, 1f);
        shineSoftness = Mathf.Clamp01(shineSoftness);
        
        if (intervalRange.x > intervalRange.y)
        {
            intervalRange.y = intervalRange.x;
        }
        intervalRange.x = Mathf.Max(0.1f, intervalRange.x);
        intervalRange.y = Mathf.Max(0.1f, intervalRange.y);
        
        // Update material properties in real-time if playing
        if (Application.isPlaying && isShining)
        {
            foreach (var data in imageShineData)
            {
                if (data.shineMaterial != null)
                {
                    data.shineMaterial.SetFloat(ShineWidth, shineWidth);
                    data.shineMaterial.SetFloat(ShineAngle, shineAngle);
                    data.shineMaterial.SetColor(ShineColor, shineColor);
                    data.shineMaterial.SetFloat(ShineIntensity, shineIntensity);
                    data.shineMaterial.SetFloat(ShineSoftness, shineSoftness);
                }
            }
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
    
    /// <summary>
    /// Context menu to create shine material template
    /// </summary>
    [UnityEditor.MenuItem("CONTEXT/DiagonalShineEffect/Create Shine Material Template")]
    private static void CreateShineMaterialTemplate(UnityEditor.MenuCommand command)
    {
        // This would help users create the material easily
        LogDebugStatic("Please create a Material using the 'UI/DiagonalShine' shader and assign it to the Shine Material Template field.");
    }
#endif
    
    #endregion
    
    #region Debug
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[DiagonalShineEffect] {message}");
        }
    }
    
    private static void LogDebugStatic(string message)
    {
        Debug.Log($"[DiagonalShineEffect] {message}");
    }
    
    #endregion
} 