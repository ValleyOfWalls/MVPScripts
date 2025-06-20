using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Entry for custom effect database
/// </summary>
[System.Serializable]
public class CustomEffectEntry
{
    [Tooltip("Unique name to reference this effect")]
    public string effectName;
    
    [Tooltip("The prefab to use for this effect")]
    public GameObject effectPrefab;
}

/// <summary>
/// Singleton manager for handling visual effect animations between entities.
/// Attach to: A GameObject in the scene (preferably on a manager object).
/// </summary>
public class EffectAnimationManager : NetworkBehaviour
{
    [Header("Effect Prefabs")]
    [SerializeField] private GameObject defaultEffectPrefab;
    
    [Header("Custom Effect Database")]
    [Tooltip("Database of custom effects that can be referenced by name")]
    [SerializeField] private CustomEffectEntry[] customEffectDatabase = new CustomEffectEntry[0];
    
    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 20;
    [SerializeField] private Transform effectParent;
    
    [Header("Effect Settings")]
    [SerializeField] private float defaultEffectDuration = 2f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Singleton instance
    public static EffectAnimationManager Instance { get; private set; }
    
    // Object pools for effects
    private Queue<GameObject> defaultEffectPool;
    
    // Custom effect pools (by prefab name)
    private Dictionary<string, Queue<GameObject>> customEffectPools;
    private Dictionary<string, GameObject> customEffectPrefabs;
    
    // Direct prefab registration (by unique ID)
    private Dictionary<string, GameObject> registeredPrefabs;
    
    // Active effects tracking
    private List<EffectAnimation> activeEffects = new List<EffectAnimation>();
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        InitializeManager();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        // Always initialize on client, even if server is also running on same machine
        InitializeManager();
        Debug.Log("EffectAnimationManager: Client initialization completed");
    }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // Note: Don't use DontDestroyOnLoad with NetworkBehaviour
            // The network manager will handle persistence
        }
        else if (Instance != this)
        {
            Debug.LogWarning("EffectAnimationManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }
    
    private void InitializeManager()
    {
        Debug.Log($"EffectAnimationManager: Starting initialization - IsServer: {IsServerStarted}, IsClient: {IsClientStarted}");
        
        // Create effect parent if not assigned
        if (effectParent == null)
        {
            GameObject parentGO = new GameObject("EffectAnimations");
            effectParent = parentGO.transform;
            effectParent.SetParent(transform);
        }
        
        Debug.Log($"EffectAnimationManager: Default effect prefab assigned: {defaultEffectPrefab != null}");
        
        // Initialize custom effect dictionaries
        customEffectPools = new Dictionary<string, Queue<GameObject>>();
        customEffectPrefabs = new Dictionary<string, GameObject>();
        
        // Initialize direct prefab registration
        registeredPrefabs = new Dictionary<string, GameObject>();
        
        // Initialize custom effect database
        InitializeCustomEffectDatabase();
        
        // Initialize object pools
        InitializePools();
        
        Debug.Log($"EffectAnimationManager: Initialized successfully");
    }
    
    /// <summary>
    /// Initializes the custom effect database
    /// </summary>
    private void InitializeCustomEffectDatabase()
    {
        foreach (var entry in customEffectDatabase)
        {
            if (!string.IsNullOrEmpty(entry.effectName) && entry.effectPrefab != null)
            {
                customEffectPrefabs[entry.effectName] = entry.effectPrefab;
                Debug.Log($"EffectAnimationManager: Registered custom effect '{entry.effectName}' -> {entry.effectPrefab.name}");
            }
        }
    }
    
    /// <summary>
    /// Gets a custom effect entry by name
    /// </summary>
    private CustomEffectEntry GetCustomEffectEntry(string effectName)
    {
        Debug.Log($"EffectAnimationManager: GetCustomEffectEntry called for '{effectName}' - Database has {customEffectDatabase.Length} entries");
        
        foreach (var entry in customEffectDatabase)
        {
            Debug.Log($"EffectAnimationManager: Checking database entry: '{entry.effectName}' -> {(entry.effectPrefab != null ? entry.effectPrefab.name : "null")}");
            if (entry.effectName == effectName)
            {
                Debug.Log($"EffectAnimationManager: Found matching entry for '{effectName}'");
                return entry;
            }
        }
        
        Debug.Log($"EffectAnimationManager: No matching entry found for '{effectName}'");
        return null;
    }
    
    private void InitializePools()
    {
        // Initialize default effect pool
        defaultEffectPool = new Queue<GameObject>();
        
        if (defaultEffectPrefab == null)
        {
            Debug.LogWarning($"EffectAnimationManager: No default effect prefab assigned, will create procedural effects on demand");
        }
        else
        {
            // Pre-instantiate pool objects
            for (int i = 0; i < poolSize; i++)
            {
                GameObject pooledEffect = Instantiate(defaultEffectPrefab, effectParent);
                pooledEffect.SetActive(false);
                defaultEffectPool.Enqueue(pooledEffect);
            }
            
            Debug.Log($"EffectAnimationManager: Created pool of {poolSize} objects for default effects");
        }
    }
    
    /// <summary>
    /// Plays an effect animation between two entities using default effect
    /// </summary>
    [Server]
    public void PlayEffectAnimation(NetworkEntity sourceEntity, NetworkEntity targetEntity, float duration = 0f)
    {
        PlayEffectAnimationWithPrefab(sourceEntity, targetEntity, null, duration);
    }
    
    /// <summary>
    /// Plays an effect animation between two entities using a named custom effect
    /// </summary>
    [Server]
    public void PlayNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration = 0f)
    {
        CustomEffectEntry entry = GetCustomEffectEntry(effectName);
        if (entry != null && entry.effectPrefab != null)
        {
            Debug.Log($"EffectAnimationManager: Found custom effect entry for '{effectName}' with prefab {entry.effectPrefab.name}");
            
            if (sourceEntity != null && targetEntity != null)
            {
                var sourceEffectSource = sourceEntity.GetComponentInChildren<EffectAnimationSource>();
                var targetEffectSource = targetEntity.GetComponentInChildren<EffectAnimationSource>();
                
                if (sourceEffectSource != null)
                {
                    Vector3 sourcePosition = sourceEffectSource.GetEffectPosition();
                    Vector3 targetPosition = targetEffectSource != null ? 
                        targetEffectSource.GetEffectPosition() : 
                        targetEntity.transform.position + Vector3.up;
                    
                    if (duration <= 0f) duration = defaultEffectDuration;
                    
                    Debug.Log($"EffectAnimationManager: Playing custom effect '{effectName}' from {sourcePosition} to {targetPosition}");
                    RpcPlayCustomEffect(sourcePosition, targetPosition, effectName, duration, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
                }
                else
                {
                    Debug.LogWarning($"EffectAnimationManager: No EffectAnimationSource found on source entity {sourceEntity.EntityName.Value} or its children");
                }
            }
        }
        else
        {
            Debug.LogWarning($"EffectAnimationManager: Custom effect '{effectName}' not found in database, using default effect");
            PlayEffectAnimation(sourceEntity, targetEntity, duration);
        }
    }
    
    /// <summary>
    /// Plays an effect animation between two entities using a custom prefab
    /// </summary>
    [Server]
    public void PlayEffectAnimationWithPrefab(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, float duration = 0f)
    {
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogError("EffectAnimationManager: Cannot play effect - source or target entity is null");
            return;
        }
        
        // Get effect source components (check both the entity and its children)
        EffectAnimationSource sourceEffectSource = sourceEntity.GetComponent<EffectAnimationSource>();
        if (sourceEffectSource == null)
        {
            sourceEffectSource = sourceEntity.GetComponentInChildren<EffectAnimationSource>();
        }
        
        EffectAnimationSource targetEffectSource = targetEntity.GetComponent<EffectAnimationSource>();
        if (targetEffectSource == null)
        {
            targetEffectSource = targetEntity.GetComponentInChildren<EffectAnimationSource>();
        }
        
        if (sourceEffectSource == null)
        {
            Debug.LogWarning($"EffectAnimationManager: No EffectAnimationSource found on source entity {sourceEntity.EntityName.Value} or its children");
            return;
        }
        
        // Get source and target positions
        Vector3 sourcePosition = sourceEffectSource.GetEffectPosition();
        Vector3 targetPosition;
        
        if (targetEffectSource != null)
        {
            targetPosition = targetEffectSource.GetEffectPosition();
        }
        else
        {
            // Fallback to entity transform position
            targetPosition = targetEntity.transform.position + Vector3.up; // Add small offset
        }
        
        // Use default duration if not specified
        if (duration <= 0f)
        {
            duration = defaultEffectDuration;
        }
        
        if (debugMode)
        {
            string effectName = customPrefab != null ? customPrefab.name : "Default";
            Debug.Log($"EffectAnimationManager: Playing {effectName} effect from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
            Debug.Log($"Source position: {sourcePosition}, Target position: {targetPosition}");
        }
        
        // If we have a custom prefab, try to use it but fall back gracefully
        if (customPrefab != null)
        {
            Debug.LogWarning($"EffectAnimationManager: Custom prefab {customPrefab.name} specified, but GameObject references cannot be sent over network. Falling back to default effect.");
            RpcPlayDefaultEffect(sourcePosition, targetPosition, duration, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
        else
        {
            RpcPlayDefaultEffect(sourcePosition, targetPosition, duration, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
    }
    
    /// <summary>
    /// Check if visual effects should be shown for the given entity IDs using centralized EntityVisibilityManager
    /// </summary>
    private bool ShouldShowVisualEffectsForEntities(uint sourceEntityId, uint targetEntityId)
    {
        // Use centralized visibility management from EntityVisibilityManager
        EntityVisibilityManager visibilityManager = EntityVisibilityManager.Instance;
        if (visibilityManager == null)
        {
            Debug.Log("EffectAnimationManager: No EntityVisibilityManager found, allowing effects to show");
            return true;
        }
        
        return visibilityManager.ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// RPC to play default effect on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcPlayDefaultEffect(Vector3 sourcePosition, Vector3 targetPosition, float duration, uint sourceEntityId, uint targetEntityId)
    {
        // Check if effects should be shown on this client based on current fight visibility
        if (!ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"EffectAnimationManager: Skipping default effect - not in currently viewed fight");
            return;
        }
        
        StartCoroutine(PlayEffectCoroutine(sourcePosition, targetPosition, duration));
    }
    
    /// <summary>
    /// RPC to play custom effect on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcPlayCustomEffect(Vector3 sourcePosition, Vector3 targetPosition, string customEffectName, float duration, uint sourceEntityId, uint targetEntityId)
    {
        // Check if effects should be shown on this client based on current fight visibility
        if (!ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"EffectAnimationManager: Skipping custom effect '{customEffectName}' - not in currently viewed fight");
            return;
        }
        
        StartCoroutine(PlayCustomEffectCoroutine(sourcePosition, targetPosition, customEffectName, duration));
    }
    
    /// <summary>
    /// Coroutine that handles the effect animation
    /// </summary>
    private IEnumerator PlayEffectCoroutine(Vector3 sourcePosition, Vector3 targetPosition, float duration)
    {
        GameObject effectObject = GetPooledEffect();
        if (effectObject == null)
        {
            Debug.LogError($"EffectAnimationManager: Could not get pooled effect");
            yield break;
        }
        
        // Ensure proper material is applied (fix for pink squares)
        EnsureEffectHasProperMaterial(effectObject);
        
        // Setup the effect
        effectObject.transform.position = sourcePosition;
        effectObject.transform.LookAt(targetPosition);
        effectObject.SetActive(true);
        
        // Get particle system component
        ParticleSystem particles = effectObject.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Play();
        }
        
        // Create effect data for tracking
        EffectAnimation effectAnimation = new EffectAnimation
        {
            effectObject = effectObject,
            startPosition = sourcePosition,
            targetPosition = targetPosition,
            duration = duration,
            elapsedTime = 0f
        };
        
        activeEffects.Add(effectAnimation);
        
        // Animate the effect
        while (effectAnimation.elapsedTime < duration)
        {
            effectAnimation.elapsedTime += Time.deltaTime;
            float progress = effectAnimation.elapsedTime / duration;
            
            // Apply speed curve
            float curvedProgress = speedCurve.Evaluate(progress);
            
            // Interpolate position
            Vector3 currentPosition = Vector3.Lerp(sourcePosition, targetPosition, curvedProgress);
            effectObject.transform.position = currentPosition;
            
            // Keep looking at target
            Vector3 direction = (targetPosition - currentPosition).normalized;
            if (direction != Vector3.zero)
            {
                effectObject.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return null;
        }
        
        // Effect reached target
        effectObject.transform.position = targetPosition;
        
        // Stop particles if they exist
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        // Wait a bit for particles to fade out
        yield return new WaitForSeconds(0.5f);
        
        // Return to pool
        ReturnEffectToPool(effectObject);
        activeEffects.Remove(effectAnimation);
        
        if (debugMode)
        {
            Debug.Log($"EffectAnimationManager: Completed effect animation");
        }
    }
    
    /// <summary>
    /// Coroutine that handles custom effect animation
    /// </summary>
    private IEnumerator PlayCustomEffectCoroutine(Vector3 sourcePosition, Vector3 targetPosition, string customEffectName, float duration)
    {
        GameObject effectObject = GetPooledCustomEffect(customEffectName);
        if (effectObject == null)
        {
            Debug.LogWarning($"EffectAnimationManager: Could not get pooled custom effect for {customEffectName}, falling back to default effect");
            // Fall back to default effect
            yield return PlayEffectCoroutine(sourcePosition, targetPosition, duration);
            yield break;
        }
        
        // Ensure proper material is applied (fix for pink squares)
        EnsureEffectHasProperMaterial(effectObject);
        
        // Setup the effect
        effectObject.transform.position = sourcePosition;
        effectObject.transform.LookAt(targetPosition);
        effectObject.SetActive(true);
        
        // Get particle system component
        ParticleSystem particles = effectObject.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Play();
        }
        
        // Create effect data for tracking
        EffectAnimation effectAnimation = new EffectAnimation
        {
            effectObject = effectObject,
            startPosition = sourcePosition,
            targetPosition = targetPosition,
            duration = duration,
            elapsedTime = 0f,
            customEffectName = customEffectName
        };
        
        activeEffects.Add(effectAnimation);
        
        // Animate the effect
        while (effectAnimation.elapsedTime < duration)
        {
            effectAnimation.elapsedTime += Time.deltaTime;
            float progress = effectAnimation.elapsedTime / duration;
            
            // Apply speed curve
            float curvedProgress = speedCurve.Evaluate(progress);
            
            // Interpolate position
            Vector3 currentPosition = Vector3.Lerp(sourcePosition, targetPosition, curvedProgress);
            effectObject.transform.position = currentPosition;
            
            // Keep looking at target
            Vector3 direction = (targetPosition - currentPosition).normalized;
            if (direction != Vector3.zero)
            {
                effectObject.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return null;
        }
        
        // Effect reached target
        effectObject.transform.position = targetPosition;
        
        // Stop particles if they exist
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        // Wait a bit for particles to fade out
        yield return new WaitForSeconds(0.5f);
        
        // Return to pool
        ReturnCustomEffectToPool(effectObject, customEffectName);
        activeEffects.Remove(effectAnimation);
        
        if (debugMode)
        {
            Debug.Log($"EffectAnimationManager: Completed custom {customEffectName} effect animation");
        }
    }
    
    /// <summary>
    /// Gets a pooled effect object
    /// </summary>
    private GameObject GetPooledEffect()
    {
        if (defaultEffectPool.Count > 0)
        {
            return defaultEffectPool.Dequeue();
        }
        
        if (defaultEffectPrefab != null)
        {
            GameObject newEffect = Instantiate(defaultEffectPrefab, effectParent);
            Debug.LogWarning($"EffectAnimationManager: Pool was empty, created new effect object from assigned prefab");
            return newEffect;
        }
        
        // No prefab assigned, try to create a procedural effect
        Debug.LogWarning($"EffectAnimationManager: No prefab assigned, creating procedural effect");
        GameObject proceduralPrefab = CreateProceduralEffect();
        if (proceduralPrefab != null)
        {
            // Store the procedural prefab for future use
            defaultEffectPrefab = proceduralPrefab;
            GameObject newEffect = Instantiate(proceduralPrefab, effectParent);
            Debug.Log($"EffectAnimationManager: Created procedural effect instance");
            return newEffect;
        }
        
        Debug.LogError($"EffectAnimationManager: Cannot create new effect - failed to create procedural effect");
        return null;
    }
    
    /// <summary>
    /// Returns an effect object to the pool
    /// </summary>
    private void ReturnEffectToPool(GameObject effectObject)
    {
        effectObject.SetActive(false);
        effectObject.transform.position = Vector3.zero;
        effectObject.transform.rotation = Quaternion.identity;
        
        defaultEffectPool.Enqueue(effectObject);
    }
    
    /// <summary>
    /// Gets a pooled custom effect object
    /// </summary>
    private GameObject GetPooledCustomEffect(string prefabName)
    {
        // Check if we have a pool for this custom effect
        if (customEffectPools.ContainsKey(prefabName) && customEffectPools[prefabName].Count > 0)
        {
            return customEffectPools[prefabName].Dequeue();
        }
        
        // Try to find the prefab and create pool if needed
        GameObject prefab = FindCustomEffectPrefab(prefabName);
        if (prefab != null)
        {
            // Create pool for this prefab
            CreateCustomEffectPool(prefabName, prefab);
            
            // Return first object from new pool
            if (customEffectPools[prefabName].Count > 0)
            {
                return customEffectPools[prefabName].Dequeue();
            }
        }
        
        Debug.LogWarning($"EffectAnimationManager: Cannot find or create custom effect prefab: {prefabName}");
        return null;
    }
    
    /// <summary>
    /// Returns a custom effect object to its pool
    /// </summary>
    private void ReturnCustomEffectToPool(GameObject effectObject, string prefabName)
    {
        effectObject.SetActive(false);
        effectObject.transform.position = Vector3.zero;
        effectObject.transform.rotation = Quaternion.identity;
        
        if (customEffectPools.ContainsKey(prefabName))
        {
            customEffectPools[prefabName].Enqueue(effectObject);
        }
    }
    
    /// <summary>
    /// Finds a custom effect prefab by name
    /// </summary>
    private GameObject FindCustomEffectPrefab(string prefabName)
    {
        Debug.Log($"EffectAnimationManager: FindCustomEffectPrefab called for '{prefabName}'");
        
        // First check if we already have it cached from the database
        if (customEffectPrefabs.ContainsKey(prefabName))
        {
            Debug.Log($"EffectAnimationManager: Found cached prefab for '{prefabName}': {customEffectPrefabs[prefabName].name}");
            return customEffectPrefabs[prefabName];
        }
        
        // Check the custom effect database
        CustomEffectEntry entry = GetCustomEffectEntry(prefabName);
        if (entry != null && entry.effectPrefab != null)
        {
            Debug.Log($"EffectAnimationManager: Found prefab in database for '{prefabName}': {entry.effectPrefab.name}");
            customEffectPrefabs[prefabName] = entry.effectPrefab;
            return entry.effectPrefab;
        }
        else
        {
            Debug.Log($"EffectAnimationManager: No database entry found for '{prefabName}' (entry: {entry != null}, prefab: {entry?.effectPrefab != null})");
        }
        
        // Fallback: Try to load from Resources folder (legacy support)
        GameObject prefab = Resources.Load<GameObject>($"Effects/{prefabName}");
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>(prefabName);
        }
        
        if (prefab != null)
        {
            Debug.Log($"EffectAnimationManager: Found prefab in Resources for '{prefabName}': {prefab.name}");
            customEffectPrefabs[prefabName] = prefab;
            return prefab;
        }
        
        Debug.LogWarning($"EffectAnimationManager: Could not find prefab for '{prefabName}' anywhere");
        return null;
    }
    
    /// <summary>
    /// Creates a pool for a custom effect prefab
    /// </summary>
    private void CreateCustomEffectPool(string prefabName, GameObject prefab)
    {
        if (customEffectPools.ContainsKey(prefabName))
            return;
        
        Queue<GameObject> pool = new Queue<GameObject>();
        
        // Pre-instantiate a smaller number for custom effects
        int customPoolSize = Mathf.Max(5, poolSize / 4);
        for (int i = 0; i < customPoolSize; i++)
        {
            GameObject pooledEffect = Instantiate(prefab, effectParent);
            pooledEffect.SetActive(false);
            pool.Enqueue(pooledEffect);
        }
        
        customEffectPools[prefabName] = pool;
        Debug.Log($"EffectAnimationManager: Created custom pool of {customPoolSize} objects for {prefabName}");
    }
    
    /// <summary>
    /// Creates a procedural effect when no prefab is assigned
    /// </summary>
    private GameObject CreateProceduralEffect()
    {
        try
        {
            Debug.Log($"EffectAnimationManager: Creating procedural default effect");
            
            GameObject effect = new GameObject($"Procedural_Default_Effect");
            ParticleSystem ps = effect.AddComponent<ParticleSystem>();
            
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1f;
            main.startSpeed = 5f;
            main.startSize = 0.2f;
            main.maxParticles = 50;
            main.startColor = Color.yellow;
            
            var emission = ps.emission;
            emission.rateOverTime = 30;
            
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            
            // Apply proper material for particle effects
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Material effectMaterial = GetDefaultParticleMaterial();
                if (effectMaterial != null)
                {
                    renderer.material = effectMaterial;
                    Debug.Log($"EffectAnimationManager: Applied material {effectMaterial.name} to procedural effect");
                }
                else
                {
                    Debug.LogWarning($"EffectAnimationManager: Could not find or create suitable material for procedural effect");
                }
            }
            
            Debug.Log($"EffectAnimationManager: Successfully created procedural default effect");
            return effect;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"EffectAnimationManager: Failed to create procedural effect: {e.Message}");
            Debug.LogError($"EffectAnimationManager: Stack trace: {e.StackTrace}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets a default material suitable for particle effects
    /// </summary>
    private Material GetDefaultParticleMaterial()
    {
        // Try to find Unity's built-in particle material
        Material[] builtInMaterials = {
            Resources.GetBuiltinResource<Material>("Default-Particle.mat"),
            Resources.GetBuiltinResource<Material>("Sprites-Default.mat"),
            Resources.GetBuiltinResource<Material>("Default-Diffuse.mat")
        };
        
        foreach (var mat in builtInMaterials)
        {
            if (mat != null) 
            {
                Debug.Log($"EffectAnimationManager: Found built-in material: {mat.name}");
                return mat;
            }
        }
        
        // Try to create a simple unlit material as fallback
        Debug.LogWarning("EffectAnimationManager: No built-in particle materials found, creating fallback material");
        return CreateFallbackParticleMaterial();
    }
    
    /// <summary>
    /// Ensures an effect object has a proper material applied to its particle system renderer
    /// </summary>
    private void EnsureEffectHasProperMaterial(GameObject effectObject)
    {
        if (effectObject == null) return;
        
        // Check all particle system renderers in the effect object
        ParticleSystemRenderer[] renderers = effectObject.GetComponentsInChildren<ParticleSystemRenderer>();
        
        foreach (var renderer in renderers)
        {
            if (renderer != null && (renderer.material == null || renderer.material.name.Contains("Default-Material")))
            {
                Material properMaterial = GetDefaultParticleMaterial();
                if (properMaterial != null)
                {
                    renderer.material = properMaterial;
                    Debug.Log($"EffectAnimationManager: Applied proper material {properMaterial.name} to effect {effectObject.name}");
                }
                else
                {
                    Debug.LogWarning($"EffectAnimationManager: Could not get proper material for effect {effectObject.name}");
                }
            }
        }
    }

    /// <summary>
    /// Creates a fallback material for particle effects when built-in materials aren't available
    /// </summary>
    private Material CreateFallbackParticleMaterial()
    {
        try
        {
            // Try different shaders in order of preference
            string[] shaderNames = {
                "Sprites/Default",
                "Legacy Shaders/Particles/Alpha Blended Premultiply",
                "Legacy Shaders/Particles/Alpha Blended",
                "Unlit/Transparent",
                "Standard",
                "Unlit/Color"
            };
            
            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.name = $"EffectAnimation_Fallback_{shaderName.Replace("/", "_")}";
                    
                    // Set common properties for particle materials
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", Color.white);
                    }
                    
                    // Enable transparency if possible
                    if (material.HasProperty("_Mode"))
                    {
                        material.SetFloat("_Mode", 3f); // Transparent mode
                    }
                    
                    if (material.HasProperty("_SrcBlend"))
                    {
                        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetFloat("_ZWrite", 0f);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_ALPHABLEND_ON");
                        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                    }
                    
                    Debug.Log($"EffectAnimationManager: Created fallback material with shader: {shaderName}");
                    return material;
                }
            }
            
            Debug.LogError("EffectAnimationManager: Could not find any suitable shader for fallback material");
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"EffectAnimationManager: Failed to create fallback material: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger effect animations
    /// </summary>
    public static void TriggerEffectAnimation(NetworkEntity sourceEntity, NetworkEntity targetEntity, float duration = 0f)
    {
        Debug.Log($"EffectAnimationManager: TriggerEffectAnimation called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Duration: {duration}");
        
        if (Instance == null)
        {
            Debug.LogError("EffectAnimationManager: No instance found. Make sure EffectAnimationManager is in the scene.");
            return;
        }
        
        Debug.Log($"EffectAnimationManager: Instance found, IsServerStarted: {Instance.IsServerStarted}");
        
        if (Instance.IsServerStarted)
        {
            Debug.Log($"EffectAnimationManager: Calling PlayEffectAnimation on server");
            Instance.PlayEffectAnimation(sourceEntity, targetEntity, duration);
        }
        else
        {
            Debug.Log($"EffectAnimationManager: Triggering local visual effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalVisualEffect(sourceEntity, targetEntity, duration);
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger custom effect animations
    /// </summary>
    public static void TriggerCustomEffectAnimation(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, float duration = 0f)
    {
        if (Instance == null)
        {
            Debug.LogError("EffectAnimationManager: No instance found. Make sure EffectAnimationManager is in the scene.");
            return;
        }
        
        if (Instance.IsServerStarted)
        {
            Instance.PlayEffectAnimationWithPrefab(sourceEntity, targetEntity, customPrefab, duration);
        }
        else
        {
            Debug.Log($"EffectAnimationManager: Triggering local custom prefab effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalCustomPrefabEffect(sourceEntity, targetEntity, customPrefab, duration);
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger named custom effects
    /// </summary>
    public static void TriggerNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration = 0f)
    {
        Debug.Log($"EffectAnimationManager: TriggerNamedCustomEffect called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Effect: {effectName}, Duration: {duration}");
        
        if (Instance == null)
        {
            Debug.LogError("EffectAnimationManager: No instance found. Make sure EffectAnimationManager is in the scene.");
            return;
        }
        
        Debug.Log($"EffectAnimationManager: Instance found, IsServerStarted: {Instance.IsServerStarted}");
        
        if (Instance.IsServerStarted)
        {
            Debug.Log($"EffectAnimationManager: Calling PlayNamedCustomEffect on server");
            Instance.PlayNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
        }
        else
        {
            Debug.Log($"EffectAnimationManager: Triggering local named custom effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
        }
    }
    
    /// <summary>
    /// Triggers local visual effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalVisualEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, float duration)
    {
        Debug.Log($"EffectAnimationManager: TriggerLocalVisualEffect called");
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the visual effect locally
        StartCoroutine(PlayEffectCoroutine(sourcePosition, targetPosition, duration));
    }
    
    /// <summary>
    /// Triggers local named custom effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration)
    {
        Debug.Log($"EffectAnimationManager: TriggerLocalNamedCustomEffect called - Effect: {effectName}");
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the custom visual effect locally
        StartCoroutine(PlayCustomEffectCoroutine(sourcePosition, targetPosition, effectName, duration));
    }
    
    /// <summary>
    /// Triggers local custom prefab effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalCustomPrefabEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, float duration)
    {
        Debug.Log($"EffectAnimationManager: TriggerLocalCustomPrefabEffect called - Prefab: {customPrefab?.name}");
        
        if (customPrefab == null)
        {
            Debug.LogWarning("EffectAnimationManager: Custom prefab is null, falling back to default effect");
            TriggerLocalVisualEffect(sourceEntity, targetEntity, duration);
            return;
        }
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the default effect locally (since we can't use custom prefabs locally without registration)
        StartCoroutine(PlayEffectCoroutine(sourcePosition, targetPosition, duration));
    }
    
    /// <summary>
    /// Gets the position for an effect on an entity, with fallback positioning
    /// </summary>
    private Vector3 GetEffectPosition(NetworkEntity entity, bool isSource)
    {
        if (entity == null)
        {
            Debug.LogWarning("EffectAnimationManager: Entity is null, using world origin");
            return Vector3.zero;
        }
        
        // Try to get EffectAnimationSource
        EffectAnimationSource effectSource = entity.GetComponent<EffectAnimationSource>();
        if (effectSource == null)
        {
            effectSource = entity.GetComponentInChildren<EffectAnimationSource>();
        }
        
        if (effectSource != null)
        {
            Vector3 position = effectSource.GetEffectPosition();
            Debug.Log($"EffectAnimationManager: Got position from EffectAnimationSource for {entity.EntityName.Value}: {position}");
            return position;
        }
        else
        {
            // Fallback to entity position
            Vector3 fallbackPosition = entity.transform.position + Vector3.up * 1f; // Slightly above entity
            Debug.Log($"EffectAnimationManager: No EffectAnimationSource found on {entity.EntityName.Value}, using fallback position: {fallbackPosition}");
            return fallbackPosition;
        }
    }

    /// <summary>
    /// Clears all active effects (useful for scene transitions)
    /// </summary>
    [Server]
    public void ClearAllEffects()
    {
        foreach (var effect in activeEffects)
        {
            if (effect.effectObject != null)
            {
                if (string.IsNullOrEmpty(effect.customEffectName))
                {
                    ReturnEffectToPool(effect.effectObject);
                }
                else
                {
                    ReturnCustomEffectToPool(effect.effectObject, effect.customEffectName);
                }
            }
        }
        
        activeEffects.Clear();
        Debug.Log("EffectAnimationManager: Cleared all active effects");
    }
    
    /// <summary>
    /// Data structure for tracking active effects
    /// </summary>
    private class EffectAnimation
    {
        public GameObject effectObject;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public float duration;
        public float elapsedTime;
        public string customEffectName; // For custom effects
    }
    
    #region Editor Tools
    
    [Header("Testing")]
    [SerializeField] private Transform testSourceTransform;
    [SerializeField] private Transform testTargetTransform;
    
    [ContextMenu("Test Effect")]
    private void TestEffect()
    {
        if (Application.isPlaying && testSourceTransform != null && testTargetTransform != null)
        {
            StartCoroutine(PlayEffectCoroutine(
                testSourceTransform.position, 
                testTargetTransform.position, 
                defaultEffectDuration
            ));
        }
    }
    
    #endregion
} 