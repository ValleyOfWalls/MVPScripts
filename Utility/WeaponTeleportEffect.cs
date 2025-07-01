using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates a looping teleportation effect for weapons similar to Star Rail
/// Makes the weapon fade/dissolve in and out of existence continuously
/// Compatible with URP rendering pipeline
/// </summary>
public class WeaponTeleportEffect : MonoBehaviour
{
    [Header("Teleport Effect Settings - Technological")]
    [SerializeField] private float teleportInDuration = 0.6f; // Faster, more tech-like
    [SerializeField] private float teleportOutDuration = 0.4f; // Snappier exit
    [SerializeField] private float holdVisibleDuration = 2.0f; // Longer display time
    [SerializeField] private float holdInvisibleDuration = 0.8f; // Shorter between cycles
    
    [Header("Visual Effects")]
    [SerializeField] private AnimationCurve teleportCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useScaleEffect = true;
    [SerializeField] private bool useDissolveEffect = true;
    [SerializeField] private bool useGlowEffect = true;
    [SerializeField] private bool useAlphaFallback = true;
    
    [Header("Scale Animation")]
    [SerializeField] private Vector3 startScale = new Vector3(0.1f, 0.1f, 0.1f);
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Dissolve Effect")]
    [SerializeField] private string dissolvePropertyName = "_DissolveAmount";
    [SerializeField] private float dissolveStart = 1f;
    [SerializeField] private float dissolveEnd = 0f;
    
    [Header("Glow Effect - Technological")]
    [SerializeField] private string emissionPropertyName = "_EmissionColor";
    [SerializeField] private Color glowColor = new Color(0f, 1f, 1f, 1f); // Bright cyan
    [SerializeField] private float glowIntensity = 3f;
    [SerializeField] private AnimationCurve glowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool additiveGlow = true; // Add to existing emission instead of replacing
    
    [Header("Flash Effect - Technological")]
    [SerializeField] private bool useFlashEffect = true;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor = new Color(0f, 0.8f, 1f, 1f); // Cyan tech color
    [SerializeField] private float flashIntensity = 8f;
    [SerializeField] private AnimationCurve flashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem teleportInParticles;
    [SerializeField] private ParticleSystem teleportOutParticles;
    [SerializeField] private ParticleSystem ambientParticles;
    [SerializeField] private ParticleSystem flashParticles;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportInSound;
    [SerializeField] private AudioClip teleportOutSound;
    [SerializeField] private AudioClip flashSound;
    [SerializeField] private float audioVolume = 0.7f;
    
    [Header("Target Configuration")]
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField] private bool autoFindRenderer = true;
    
    [Header("Debug")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool logEvents = false;
    [SerializeField] private bool showRuntimeInfo = true;
    
    [Header("Runtime Status (Read Only)")]
    [SerializeField, ReadOnly] private string currentEffectMode = "Not Initialized";
    [SerializeField, ReadOnly] private bool dissolveShaderFound = false;
    [SerializeField, ReadOnly] private int materialsCreated = 0;
    [SerializeField, ReadOnly] private bool isCurrentlyTeleporting = false;
    [SerializeField, ReadOnly] private string currentPhase = "Idle";
    
    // Private variables
    private SkinnedMeshRenderer weaponRenderer;
    private Material[] originalMaterials;
    private Material[] effectMaterials;
    private Vector3 originalScale;
    private bool isLooping = false;
    private Coroutine loopCoroutine;
    private Shader dissolveShader;
    private Texture2D defaultNoiseTexture;
    private bool hasDissolveShader = false;
    
    // Original material values storage for preservation
    private Dictionary<Material, MaterialPropertyCache> originalPropertyCache;
    
    // Property cache class to store original values
    [System.Serializable]
    private class MaterialPropertyCache
    {
        public Color originalEmission = Color.black;
        public Color originalBaseColor = Color.white;
        public float originalAlpha = 1f;
        public Dictionary<string, float> originalFloats = new Dictionary<string, float>();
        public Dictionary<string, Color> originalColors = new Dictionary<string, Color>();
        public Dictionary<string, Vector4> originalVectors = new Dictionary<string, Vector4>();
        public bool hasEmission = false;
        public string[] originalKeywords;
    }
    
    // Material property IDs for performance
    private int dissolvePropertyID;
    private int emissionPropertyID;
    private int alphaPropertyID;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        if (autoStart)
        {
            StartTeleportLoop();
        }
    }
    
    private void InitializeComponents()
    {
        // Get the skinned mesh renderer
        if (targetRenderer != null)
        {
            weaponRenderer = targetRenderer;
        }
        else if (autoFindRenderer)
        {
            weaponRenderer = GetComponent<SkinnedMeshRenderer>();
            if (weaponRenderer == null)
            {
                // Try to find in children
                weaponRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }
        
        if (weaponRenderer == null)
        {
            Debug.LogError($"WeaponTeleportEffect: No SkinnedMeshRenderer found. Please assign one in Target Configuration or enable Auto Find Renderer.");
            enabled = false;
            return;
        }
        
        // Store original scale
        originalScale = transform.localScale;
        
        // Cache material property IDs
        dissolvePropertyID = Shader.PropertyToID(dissolvePropertyName);
        emissionPropertyID = Shader.PropertyToID(emissionPropertyName);
        alphaPropertyID = Shader.PropertyToID("_BaseColor");
        
        // Setup materials for effects
        SetupEffectMaterials();
        
        // Setup audio source if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Configure audio source
        audioSource.playOnAwake = false;
        audioSource.volume = audioVolume;
        
        if (logEvents)
        {
            Debug.Log($"WeaponTeleportEffect: Initialized on {gameObject.name}");
        }
    }
    
    private void SetupEffectMaterials()
    {
        originalMaterials = weaponRenderer.materials;
        effectMaterials = new Material[originalMaterials.Length];
        originalPropertyCache = new Dictionary<Material, MaterialPropertyCache>();
        
        // Find or create the dissolve shader
        FindOrCreateDissolveShader();
        
        // Create default noise texture if needed
        CreateDefaultNoiseTexture();
        
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            // Cache original material properties
            CacheOriginalMaterialProperties(originalMaterials[i]);
            
            // Create dissolve material from original
            effectMaterials[i] = CreateDissolveMaterial(originalMaterials[i]);
        }
        
        weaponRenderer.materials = effectMaterials;
        
        // Update debug info
        materialsCreated = effectMaterials.Length;
        
        if (logEvents)
        {
            Debug.Log($"WeaponTeleportEffect: Created {effectMaterials.Length} dissolve materials with property preservation");
        }
    }
    
    private void CacheOriginalMaterialProperties(Material originalMaterial)
    {
        var cache = new MaterialPropertyCache();
        
        // Cache common properties that might exist
        string[] commonFloatProps = { "_Metallic", "_Smoothness", "_Glossiness", "_BumpScale", "_OcclusionStrength", "_DissolveAmount" };
        string[] commonColorProps = { "_BaseColor", "_Color", "_EmissionColor", "_SpecColor" };
        string[] commonVectorProps = { "_MainTex_ST", "_BaseMap_ST" };
        
        // Cache float properties
        foreach (string prop in commonFloatProps)
        {
            if (originalMaterial.HasProperty(prop))
            {
                cache.originalFloats[prop] = originalMaterial.GetFloat(prop);
            }
        }
        
        // Cache color properties
        foreach (string prop in commonColorProps)
        {
            if (originalMaterial.HasProperty(prop))
            {
                Color color = originalMaterial.GetColor(prop);
                cache.originalColors[prop] = color;
                
                // Special handling for base color alpha
                if (prop == "_BaseColor" || prop == "_Color")
                {
                    cache.originalBaseColor = color;
                    cache.originalAlpha = color.a;
                }
                
                // Special handling for emission
                if (prop == "_EmissionColor")
                {
                    cache.originalEmission = color;
                    cache.hasEmission = color.maxColorComponent > 0.01f;
                }
            }
        }
        
        // Cache vector properties
        foreach (string prop in commonVectorProps)
        {
            if (originalMaterial.HasProperty(prop))
            {
                cache.originalVectors[prop] = originalMaterial.GetVector(prop);
            }
        }
        
        // Cache enabled keywords
        cache.originalKeywords = originalMaterial.shaderKeywords;
        
        originalPropertyCache[originalMaterial] = cache;
        
        if (logEvents)
        {
            Debug.Log($"WeaponTeleportEffect: Cached {cache.originalFloats.Count} floats, {cache.originalColors.Count} colors, {cache.originalVectors.Count} vectors for {originalMaterial.name}");
        }
    }
    
    private void FindOrCreateDissolveShader()
    {
        // Try to find existing dissolve shader
        dissolveShader = Shader.Find("Custom/URP/WeaponDissolve");
        
        if (dissolveShader == null)
        {
            hasDissolveShader = false;
            currentEffectMode = "Alpha Transparency Fallback";
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: No dissolve shader found, will use alpha fallback");
            }
        }
        else
        {
            hasDissolveShader = true;
            currentEffectMode = "Dissolve Shader";
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: Found existing dissolve shader");
            }
        }
        
        // Update debug info
        dissolveShaderFound = hasDissolveShader;
    }
    

    
    private void CreateDefaultNoiseTexture()
    {
        if (defaultNoiseTexture != null) return;
        
        // Create a simple noise texture programmatically
        int size = 256;
        defaultNoiseTexture = new Texture2D(size, size, TextureFormat.R8, false);
        defaultNoiseTexture.name = "Generated_NoiseTexture";
        
        Color[] pixels = new Color[size * size];
        
        // Generate Perlin noise
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                pixels[y * size + x] = new Color(noiseValue, noiseValue, noiseValue, 1f);
            }
        }
        
        defaultNoiseTexture.SetPixels(pixels);
        defaultNoiseTexture.Apply();
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Generated default noise texture");
        }
    }
    
    private Material CreateDissolveMaterial(Material originalMaterial)
    {
        Material dissolveMaterial;
        
        if (hasDissolveShader && dissolveShader != null)
        {
            // Create material with dissolve shader
            dissolveMaterial = new Material(dissolveShader);
            
            // Copy properties from original material
            CopyMaterialProperties(originalMaterial, dissolveMaterial);
            
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: Created material with dissolve shader");
            }
        }
        else
        {
            // Fallback: use original material with alpha transparency
            dissolveMaterial = new Material(originalMaterial);
            
            // Try to make material transparent for alpha fade
            if (useAlphaFallback)
            {
                SetupTransparentMaterial(dissolveMaterial);
            }
            
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: Using alpha transparency fallback");
            }
        }
        
        // Set up dissolve properties
        SetupDissolveProperties(dissolveMaterial);
        
        return dissolveMaterial;
    }
    
    private void SetupTransparentMaterial(Material material)
    {
        // Try to set up transparency for common URP shaders
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1); // Transparent
        }
        
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0); // Alpha blend
        }
        
        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0); // Disable alpha clipping
        }
        
        // Set render queue for transparency
        material.renderQueue = 3000;
        
        // Enable transparency keywords
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Set up material for transparency");
        }
    }
    
    private void CopyMaterialProperties(Material source, Material destination)
    {
        // Copy common properties that both shaders might have
        string[] commonProperties = {
            "_BaseMap", "_MainTex", "_BaseColor", "_Color",
            "_BumpMap", "_NormalMap", "_Metallic", "_Smoothness",
            "_Glossiness", "_SpecColor"
        };
        
        foreach (string propName in commonProperties)
        {
            int propID = Shader.PropertyToID(propName);
            
            if (source.HasProperty(propID) && destination.HasProperty(propID))
            {
                // Copy texture properties
                if (source.GetTexture(propID) != null)
                {
                    destination.SetTexture(propID, source.GetTexture(propID));
                }
                
                // Copy color properties
                try
                {
                    destination.SetColor(propID, source.GetColor(propID));
                }
                catch
                {
                    // Property might not be a color, skip
                }
                
                // Copy float properties
                try
                {
                    destination.SetFloat(propID, source.GetFloat(propID));
                }
                catch
                {
                    // Property might not be a float, skip
                }
            }
        }
        
        if (logEvents)
        {
            Debug.Log($"WeaponTeleportEffect: Copied properties from {source.name} to dissolve material");
        }
    }
    
    private void SetupDissolveProperties(Material material)
    {
        // Set dissolve texture
        if (material.HasProperty("_DissolveTexture"))
        {
            material.SetTexture("_DissolveTexture", defaultNoiseTexture);
        }
        
        // Set initial dissolve amount (fully visible)
        if (material.HasProperty("_DissolveAmount"))
        {
            material.SetFloat("_DissolveAmount", dissolveEnd);
        }
        
        // Set initial emission
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
        }
        
        // Enable emission keyword if available
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
        }
    }
    
    /// <summary>
    /// Start the continuous teleportation loop
    /// </summary>
    public void StartTeleportLoop()
    {
        if (isLooping) return;
        
        isLooping = true;
        isCurrentlyTeleporting = true;
        currentPhase = "Starting Loop";
        loopCoroutine = StartCoroutine(TeleportLoopCoroutine());
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Started teleport loop");
        }
    }
    
    /// <summary>
    /// Stop the teleportation loop
    /// </summary>
    public void StopTeleportLoop()
    {
        if (!isLooping) return;
        
        isLooping = false;
        isCurrentlyTeleporting = false;
        currentPhase = "Stopping";
        
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
        
        // Reset to visible state with original materials
        ResetToNormalState();
        
        currentPhase = "Idle";
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Stopped teleport loop");
        }
    }
    
    private IEnumerator TeleportLoopCoroutine()
    {
        currentPhase = "Initializing";
        
        // Start invisible
        SetWeaponVisibility(true, true); // Keep renderer enabled but set dissolve to invisible
        
        while (isLooping)
        {
            currentPhase = "Teleporting In";
            // Teleport in (includes flash effect)
            yield return StartCoroutine(TeleportIn());
            
            currentPhase = "Visible (Original Material)";
            // Hold visible - restore original materials during visible phase
            RestoreOriginalMaterials();
            yield return new WaitForSeconds(holdVisibleDuration);
            
            currentPhase = "Teleporting Out";
            // Switch back to effect materials for teleport out
            RestoreEffectMaterials();
            yield return StartCoroutine(TeleportOut());
            
            currentPhase = "Invisible";
            // Hold invisible
            yield return new WaitForSeconds(holdInvisibleDuration);
        }
        
        currentPhase = "Loop Ended";
    }
    
    private IEnumerator PlayFlashEffect()
    {
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Playing flash effect");
        }
        
        // Play flash sound
        PlayTeleportSound(flashSound);
        
        // Trigger flash particles
        if (flashParticles != null)
        {
            flashParticles.Play();
        }
        
        // Flash the emission
        float elapsedTime = 0f;
        
        while (elapsedTime < flashDuration)
        {
            float progress = elapsedTime / flashDuration;
            float flashValue = flashCurve.Evaluate(progress);
            
            // Apply flash glow
            Color currentFlash = flashColor * (flashIntensity * flashValue);
            SetEmissionColor(currentFlash);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure flash ends cleanly
        SetEmissionColor(Color.black);
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Flash effect complete");
        }
    }
    
    private IEnumerator TeleportIn()
    {
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Teleporting in");
        }
        
        // Play sound effect
        PlayTeleportSound(teleportInSound);
        
        // Start particle effect
        if (teleportInParticles != null)
        {
            teleportInParticles.Play();
        }
        
        float elapsedTime = 0f;
        
        while (elapsedTime < teleportInDuration)
        {
            float progress = elapsedTime / teleportInDuration;
            float curveValue = teleportCurve.Evaluate(progress);
            
            // Apply scale effect
            if (useScaleEffect)
            {
                float scaleProgress = scaleCurve.Evaluate(progress);
                Vector3 currentScale = Vector3.Lerp(startScale, originalScale, scaleProgress);
                transform.localScale = currentScale;
            }
            
            // Apply dissolve effect
            if (useDissolveEffect)
            {
                float dissolveValue = Mathf.Lerp(dissolveStart, dissolveEnd, curveValue);
                SetDissolveAmount(dissolveValue);
            }
            
            // Apply glow effect
            if (useGlowEffect)
            {
                float glowProgress = glowCurve.Evaluate(progress);
                Color currentGlow = glowColor * (glowIntensity * glowProgress);
                SetEmissionColor(currentGlow);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final state
        if (useScaleEffect)
        {
            transform.localScale = originalScale;
        }
        
        if (useDissolveEffect)
        {
            SetDissolveAmount(dissolveEnd);
        }
        
        if (useGlowEffect)
        {
            SetEmissionColor(Color.black);
        }
        
        // Add flash effect at the end of teleport in
        if (useFlashEffect)
        {
            yield return StartCoroutine(PlayFlashEffect());
        }
        
        // Weapon is already visible, just ensure final state
    }
    
    private IEnumerator TeleportOut()
    {
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Teleporting out");
        }
        
        // Play sound effect
        PlayTeleportSound(teleportOutSound);
        
        // Start particle effect
        if (teleportOutParticles != null)
        {
            teleportOutParticles.Play();
        }
        
        float elapsedTime = 0f;
        
        while (elapsedTime < teleportOutDuration)
        {
            float progress = elapsedTime / teleportOutDuration;
            float curveValue = teleportCurve.Evaluate(progress);
            
            // Apply scale effect (reverse)
            if (useScaleEffect)
            {
                float scaleProgress = scaleCurve.Evaluate(1f - progress);
                Vector3 currentScale = Vector3.Lerp(startScale, originalScale, scaleProgress);
                transform.localScale = currentScale;
            }
            
            // Apply dissolve effect (reverse)
            if (useDissolveEffect)
            {
                float dissolveValue = Mathf.Lerp(dissolveEnd, dissolveStart, curveValue);
                SetDissolveAmount(dissolveValue);
            }
            
            // Apply glow effect
            if (useGlowEffect)
            {
                float glowProgress = glowCurve.Evaluate(progress);
                Color currentGlow = glowColor * (glowIntensity * glowProgress);
                SetEmissionColor(currentGlow);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final state - set to invisible via dissolve
        if (useDissolveEffect)
        {
            SetDissolveAmount(dissolveStart);
        }
        transform.localScale = startScale;
        
        if (useGlowEffect)
        {
            SetEmissionColor(Color.black);
        }
    }
    
    private void SetWeaponVisibility(bool visible, bool immediate = false)
    {
        // Always keep renderer enabled to avoid AABB issues
        weaponRenderer.enabled = true;
        
        if (immediate)
        {
            if (visible)
            {
                transform.localScale = originalScale;
                if (useDissolveEffect) SetDissolveAmount(dissolveEnd);
            }
            else
            {
                transform.localScale = startScale;
                if (useDissolveEffect) SetDissolveAmount(dissolveStart);
            }
        }
    }
    
    private void SetDissolveAmount(float amount)
    {
        if (effectMaterials == null) return;
        
        foreach (var material in effectMaterials)
        {
            if (hasDissolveShader && material.HasProperty(dissolvePropertyID))
            {
                // Use dissolve shader
                material.SetFloat(dissolvePropertyID, amount);
            }
            else if (useAlphaFallback)
            {
                // Use alpha transparency fallback
                SetMaterialAlpha(material, 1f - amount); // Invert: 0 = invisible, 1 = visible
            }
        }
    }
    
    private void SetMaterialAlpha(Material material, float alpha)
    {
        // Find the original material to preserve base color
        Material originalMat = FindOriginalMaterialFor(material);
        
        // Try different common alpha properties while preserving original RGB
        if (material.HasProperty("_BaseColor"))
        {
            Color baseColor = material.GetColor("_BaseColor");
            
            // Preserve original RGB if we have cached values
            if (originalMat != null && originalPropertyCache.ContainsKey(originalMat))
            {
                var cache = originalPropertyCache[originalMat];
                baseColor = cache.originalBaseColor;
            }
            
            baseColor.a = alpha;
            material.SetColor("_BaseColor", baseColor);
        }
        else if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            
            // Preserve original RGB if we have cached values
            if (originalMat != null && originalPropertyCache.ContainsKey(originalMat))
            {
                var cache = originalPropertyCache[originalMat];
                if (cache.originalColors.ContainsKey("_Color"))
                {
                    color = cache.originalColors["_Color"];
                }
            }
            
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }
    
    private void SetEmissionColor(Color color)
    {
        if (effectMaterials == null) return;
        
        foreach (var material in effectMaterials)
        {
            if (material.HasProperty(emissionPropertyID))
            {
                Color finalEmission = color;
                
                // If additive glow is enabled, add to original emission
                if (additiveGlow)
                {
                    // Find the original material this effect material corresponds to
                    Material originalMat = FindOriginalMaterialFor(material);
                    if (originalMat != null && originalPropertyCache.ContainsKey(originalMat))
                    {
                        var cache = originalPropertyCache[originalMat];
                        finalEmission = cache.originalEmission + color;
                    }
                }
                
                material.SetColor(emissionPropertyID, finalEmission);
                
                // Enable emission if we're adding glow
                if (color.maxColorComponent > 0.01f)
                {
                    material.EnableKeyword("_EMISSION");
                }
            }
        }
    }
    
    private Material FindOriginalMaterialFor(Material effectMaterial)
    {
        // Find which original material this effect material was created from
        for (int i = 0; i < effectMaterials.Length; i++)
        {
            if (effectMaterials[i] == effectMaterial && i < originalMaterials.Length)
            {
                return originalMaterials[i];
            }
        }
        return null;
    }
    
    private void PlayTeleportSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, audioVolume);
        }
    }
    
    private void RestoreOriginalMaterials()
    {
        if (originalMaterials != null && weaponRenderer != null)
        {
            weaponRenderer.materials = originalMaterials;
            
            // Ensure all original properties are restored
            RestoreAllOriginalProperties();
            
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: Restored original materials with all properties");
            }
        }
    }
    
    private void RestoreAllOriginalProperties()
    {
        if (originalPropertyCache == null) return;
        
        foreach (var material in originalMaterials)
        {
            if (originalPropertyCache.ContainsKey(material))
            {
                var cache = originalPropertyCache[material];
                
                // Restore float properties
                foreach (var kvp in cache.originalFloats)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetFloat(kvp.Key, kvp.Value);
                    }
                }
                
                // Restore color properties
                foreach (var kvp in cache.originalColors)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetColor(kvp.Key, kvp.Value);
                    }
                }
                
                // Restore vector properties
                foreach (var kvp in cache.originalVectors)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetVector(kvp.Key, kvp.Value);
                    }
                }
                
                // Restore keywords
                if (cache.originalKeywords != null)
                {
                    material.shaderKeywords = cache.originalKeywords;
                }
            }
        }
    }
    
    private void RestoreEffectMaterials()
    {
        if (effectMaterials != null && weaponRenderer != null)
        {
            weaponRenderer.materials = effectMaterials;
            
            if (logEvents)
            {
                Debug.Log("WeaponTeleportEffect: Restored effect materials");
            }
        }
    }
    
    private void ResetToNormalState()
    {
        transform.localScale = originalScale;
        
        // Restore original materials
        RestoreOriginalMaterials();
        
        if (useGlowEffect)
        {
            SetEmissionColor(Color.black);
        }
        
        // Stop all particle effects
        if (teleportInParticles != null) teleportInParticles.Stop();
        if (teleportOutParticles != null) teleportOutParticles.Stop();
        if (ambientParticles != null) ambientParticles.Stop();
        
        if (logEvents)
        {
            Debug.Log("WeaponTeleportEffect: Reset to normal state with original materials");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up material instances
        if (effectMaterials != null)
        {
            foreach (var material in effectMaterials)
            {
                if (material != null)
                {
                    DestroyImmediate(material);
                }
            }
        }
    }
    
    private void OnValidate()
    {
        // Ensure durations are positive
        teleportInDuration = Mathf.Max(0.1f, teleportInDuration);
        teleportOutDuration = Mathf.Max(0.1f, teleportOutDuration);
        holdVisibleDuration = Mathf.Max(0f, holdVisibleDuration);
        holdInvisibleDuration = Mathf.Max(0f, holdInvisibleDuration);
        flashDuration = Mathf.Max(0.1f, flashDuration);
        
        // Ensure audio volume is in valid range
        audioVolume = Mathf.Clamp01(audioVolume);
        
        // Ensure intensities are positive
        glowIntensity = Mathf.Max(0f, glowIntensity);
        flashIntensity = Mathf.Max(0f, flashIntensity);
    }
    
    // Public methods for external control
    public void SetTeleportSpeed(float speedMultiplier)
    {
        teleportInDuration /= speedMultiplier;
        teleportOutDuration /= speedMultiplier;
    }
    
    public void SetGlowColor(Color newColor)
    {
        glowColor = newColor;
    }
    
    public void SetGlowIntensity(float intensity)
    {
        glowIntensity = Mathf.Max(0f, intensity);
    }
    
    public void SetFlashColor(Color newColor)
    {
        flashColor = newColor;
    }
    
    public void SetFlashIntensity(float intensity)
    {
        flashIntensity = Mathf.Max(0f, intensity);
    }
    
    /// <summary>
    /// Trigger a flash effect manually (for testing or special events)
    /// </summary>
    [ContextMenu("Trigger Flash Effect")]
    public void TriggerFlash()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(PlayFlashEffect());
        }
    }
    
    public bool IsLooping => isLooping;
    
    /// <summary>
    /// Get detailed runtime information about the effect
    /// </summary>
    public string GetRuntimeInfo()
    {
        return $"Effect Mode: {currentEffectMode}\n" +
               $"Dissolve Shader Found: {dissolveShaderFound}\n" +
               $"Materials Created: {materialsCreated}\n" +
               $"Currently Teleporting: {isCurrentlyTeleporting}\n" +
               $"Current Phase: {currentPhase}\n" +
               $"Original Materials: {(originalMaterials != null ? originalMaterials.Length : 0)}\n" +
               $"Effect Materials: {(effectMaterials != null ? effectMaterials.Length : 0)}";
    }
    
    /// <summary>
    /// Force switch to original materials (for debugging)
    /// </summary>
    [ContextMenu("Switch to Original Materials")]
    public void ForceOriginalMaterials()
    {
        RestoreOriginalMaterials();
        Debug.Log("WeaponTeleportEffect: Forced switch to original materials");
    }
    
    /// <summary>
    /// Force switch to effect materials (for debugging)
    /// </summary>
    [ContextMenu("Switch to Effect Materials")]
    public void ForceEffectMaterials()
    {
        RestoreEffectMaterials();
        Debug.Log("WeaponTeleportEffect: Forced switch to effect materials");
    }
    
    /// <summary>
    /// Print detailed runtime information to console
    /// </summary>
    [ContextMenu("Print Runtime Info")]
    public void PrintRuntimeInfo()
    {
        Debug.Log($"WeaponTeleportEffect Runtime Info:\n{GetRuntimeInfo()}");
    }
} 