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
    
    [Tooltip("Fallback attack type if prefab is missing")]
    public AttackEffectSource.AttackType fallbackType = AttackEffectSource.AttackType.Default;
}

/// <summary>
/// Singleton manager for handling attack particle effects between entities.
/// Attach to: A GameObject in the scene (preferably on a manager object).
/// </summary>
public class AttackEffectManager : NetworkBehaviour
{
    [Header("Effect Prefabs")]
    [SerializeField] private GameObject meleeEffectPrefab;
    [SerializeField] private GameObject rangedEffectPrefab;
    [SerializeField] private GameObject magicEffectPrefab;
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
    public static AttackEffectManager Instance { get; private set; }
    
    // Object pools for effects
    private Dictionary<AttackEffectSource.AttackType, Queue<GameObject>> effectPools;
    private Dictionary<AttackEffectSource.AttackType, GameObject> effectPrefabs;
    
    // Custom effect pools (by prefab name)
    private Dictionary<string, Queue<GameObject>> customEffectPools;
    private Dictionary<string, GameObject> customEffectPrefabs;
    
    // Direct prefab registration (by unique ID)
    private Dictionary<string, GameObject> registeredPrefabs;
    
    // Active effects tracking
    private List<AttackEffect> activeEffects = new List<AttackEffect>();
    
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
        Debug.Log("AttackEffectManager: Client initialization completed");
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
            Debug.LogWarning("AttackEffectManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }
    
    private void InitializeManager()
    {
        Debug.Log($"AttackEffectManager: Starting initialization - IsServer: {IsServerStarted}, IsClient: {IsClientStarted}");
        
        // Create effect parent if not assigned
        if (effectParent == null)
        {
            GameObject parentGO = new GameObject("AttackEffects");
            effectParent = parentGO.transform;
            effectParent.SetParent(transform);
        }
        
        // Initialize effect prefab dictionary
        effectPrefabs = new Dictionary<AttackEffectSource.AttackType, GameObject>
        {
            { AttackEffectSource.AttackType.Melee, meleeEffectPrefab },
            { AttackEffectSource.AttackType.Ranged, rangedEffectPrefab },
            { AttackEffectSource.AttackType.Magic, magicEffectPrefab },
            { AttackEffectSource.AttackType.Default, defaultEffectPrefab }
        };
        
        Debug.Log($"AttackEffectManager: Effect prefabs - Melee: {meleeEffectPrefab != null}, Ranged: {rangedEffectPrefab != null}, Magic: {magicEffectPrefab != null}, Default: {defaultEffectPrefab != null}");
        
        // Initialize custom effect dictionaries
        customEffectPools = new Dictionary<string, Queue<GameObject>>();
        customEffectPrefabs = new Dictionary<string, GameObject>();
        
        // Initialize direct prefab registration
        registeredPrefabs = new Dictionary<string, GameObject>();
        
        // Initialize custom effect database
        InitializeCustomEffectDatabase();
        
        // Initialize object pools
        InitializePools();
        
        Debug.Log($"AttackEffectManager: Initialized successfully - Pools created: {effectPools.Count}");
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
                Debug.Log($"AttackEffectManager: Registered custom effect '{entry.effectName}' -> {entry.effectPrefab.name}");
            }
        }
    }
    
    /// <summary>
    /// Gets a custom effect entry by name
    /// </summary>
    private CustomEffectEntry GetCustomEffectEntry(string effectName)
    {
        Debug.Log($"AttackEffectManager: GetCustomEffectEntry called for '{effectName}' - Database has {customEffectDatabase.Length} entries");
        
        foreach (var entry in customEffectDatabase)
        {
            Debug.Log($"AttackEffectManager: Checking database entry: '{entry.effectName}' -> {(entry.effectPrefab != null ? entry.effectPrefab.name : "null")}");
            if (entry.effectName == effectName)
            {
                Debug.Log($"AttackEffectManager: Found matching entry for '{effectName}'");
                return entry;
            }
        }
        
        Debug.Log($"AttackEffectManager: No matching entry found for '{effectName}'");
        return null;
    }
    
    private void InitializePools()
    {
        effectPools = new Dictionary<AttackEffectSource.AttackType, Queue<GameObject>>();
        
        foreach (var kvp in effectPrefabs)
        {
            AttackEffectSource.AttackType attackType = kvp.Key;
            GameObject prefab = kvp.Value;
            
            if (prefab == null)
            {
                Debug.LogWarning($"AttackEffectManager: No prefab assigned for {attackType} attack type, will create procedural effects on demand");
                // Create empty pool that will be filled with procedural effects when needed
                effectPools[attackType] = new Queue<GameObject>();
                continue;
            }
            
            Queue<GameObject> pool = new Queue<GameObject>();
            
            // Pre-instantiate pool objects
            for (int i = 0; i < poolSize; i++)
            {
                GameObject pooledEffect = Instantiate(prefab, effectParent);
                pooledEffect.SetActive(false);
                pool.Enqueue(pooledEffect);
            }
            
            effectPools[attackType] = pool;
            Debug.Log($"AttackEffectManager: Created pool of {poolSize} objects for {attackType} effects");
        }
    }
    
    /// <summary>
    /// Plays an attack effect between two entities using attack type
    /// </summary>
    [Server]
    public void PlayAttackEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, AttackEffectSource.AttackType attackType, float duration = 0f)
    {
        PlayAttackEffectWithPrefab(sourceEntity, targetEntity, null, attackType, duration);
    }
    
    /// <summary>
    /// Plays an attack effect between two entities using a named custom effect
    /// </summary>
    [Server]
    public void PlayNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration = 0f)
    {
        CustomEffectEntry entry = GetCustomEffectEntry(effectName);
        if (entry != null && entry.effectPrefab != null)
        {
            Debug.Log($"AttackEffectManager: Found custom effect entry for '{effectName}' with prefab {entry.effectPrefab.name}");
            
            // Play ONLY the custom effect using the name-based system
            if (sourceEntity != null && targetEntity != null)
            {
                var sourceEffectSource = sourceEntity.GetComponentInChildren<AttackEffectSource>();
                var targetEffectSource = targetEntity.GetComponentInChildren<AttackEffectSource>();
                
                if (sourceEffectSource != null)
                {
                    Vector3 sourcePosition = sourceEffectSource.GetEffectPosition(entry.fallbackType);
                    Vector3 targetPosition = targetEffectSource != null ? 
                        targetEffectSource.GetEffectPosition(AttackEffectSource.AttackType.Default) : 
                        targetEntity.transform.position + Vector3.up;
                    
                    if (duration <= 0f) duration = defaultEffectDuration;
                    
                    Debug.Log($"AttackEffectManager: Playing custom effect '{effectName}' from {sourcePosition} to {targetPosition}");
                    RpcPlayCustomAttackEffect(sourcePosition, targetPosition, effectName, duration, entry.fallbackType, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
                }
                else
                {
                    Debug.LogWarning($"AttackEffectManager: No AttackEffectSource found on source entity {sourceEntity.EntityName.Value} or its children");
                }
            }
        }
        else
        {
            Debug.LogWarning($"AttackEffectManager: Custom effect '{effectName}' not found in database, using default effect");
            PlayAttackEffect(sourceEntity, targetEntity, AttackEffectSource.AttackType.Default, duration);
        }
    }
    
    /// <summary>
    /// Plays an attack effect between two entities using a custom prefab
    /// </summary>
    [Server]
    public void PlayAttackEffectWithPrefab(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, AttackEffectSource.AttackType fallbackAttackType = AttackEffectSource.AttackType.Default, float duration = 0f)
    {
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogError("AttackEffectManager: Cannot play effect - source or target entity is null");
            return;
        }
        
        // Get effect source components (check both the entity and its children)
        AttackEffectSource sourceEffectSource = sourceEntity.GetComponent<AttackEffectSource>();
        if (sourceEffectSource == null)
        {
            sourceEffectSource = sourceEntity.GetComponentInChildren<AttackEffectSource>();
        }
        
        AttackEffectSource targetEffectSource = targetEntity.GetComponent<AttackEffectSource>();
        if (targetEffectSource == null)
        {
            targetEffectSource = targetEntity.GetComponentInChildren<AttackEffectSource>();
        }
        
        if (sourceEffectSource == null)
        {
            Debug.LogWarning($"AttackEffectManager: No AttackEffectSource found on source entity {sourceEntity.EntityName.Value} or its children");
            return;
        }
        
        // Determine which attack type to use for positioning
        AttackEffectSource.AttackType positioningType = customPrefab != null ? fallbackAttackType : fallbackAttackType;
        
        // Get source and target positions
        Vector3 sourcePosition = sourceEffectSource.GetEffectPosition(positioningType);
        Vector3 targetPosition;
        
        if (targetEffectSource != null)
        {
            // Use the target's center point for receiving effects
            targetPosition = targetEffectSource.GetEffectPosition(AttackEffectSource.AttackType.Default);
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
            string effectName = customPrefab != null ? customPrefab.name : fallbackAttackType.ToString();
            Debug.Log($"AttackEffectManager: Playing {effectName} effect from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
            Debug.Log($"Source position: {sourcePosition}, Target position: {targetPosition}");
        }
        
        // If we have a custom prefab, try to use it but fall back gracefully
        if (customPrefab != null)
        {
            Debug.LogWarning($"AttackEffectManager: Custom prefab {customPrefab.name} specified, but GameObject references cannot be sent over network. Falling back to {fallbackAttackType} attack type.");
            // For now, fall back to the attack type - we'll improve this in a future iteration
            RpcPlayAttackEffect(sourcePosition, targetPosition, fallbackAttackType, duration, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
        else
        {
            RpcPlayAttackEffect(sourcePosition, targetPosition, fallbackAttackType, duration, (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
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
            Debug.Log("AttackEffectManager: No EntityVisibilityManager found, allowing effects to show");
            return true;
        }
        
        return visibilityManager.ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// RPC to play attack effect on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcPlayAttackEffect(Vector3 sourcePosition, Vector3 targetPosition, AttackEffectSource.AttackType attackType, float duration, uint sourceEntityId, uint targetEntityId)
    {
        // Check if effects should be shown on this client based on current fight visibility
        if (!ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"AttackEffectManager: Skipping attack effect - not in currently viewed fight");
            return;
        }
        
        StartCoroutine(PlayEffectCoroutine(sourcePosition, targetPosition, attackType, duration));
    }
    
    /// <summary>
    /// RPC to play custom attack effect on all clients (legacy - for Resources-based effects)
    /// </summary>
    [ObserversRpc]
    private void RpcPlayCustomAttackEffect(Vector3 sourcePosition, Vector3 targetPosition, string customPrefabName, float duration, AttackEffectSource.AttackType fallbackType, uint sourceEntityId, uint targetEntityId)
    {
        // Check if effects should be shown on this client based on current fight visibility
        if (!ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"AttackEffectManager: Skipping custom effect '{customPrefabName}' - not in currently viewed fight");
            return;
        }
        
        StartCoroutine(PlayCustomEffectCoroutine(sourcePosition, targetPosition, customPrefabName, duration, fallbackType));
    }
    
    /// <summary>
    /// RPC to play direct custom effect using registered prefab
    /// </summary>
    [ObserversRpc]
    private void RpcPlayDirectCustomEffect(Vector3 sourcePosition, Vector3 targetPosition, string prefabId, float duration, AttackEffectSource.AttackType fallbackType, uint sourceEntityId, uint targetEntityId)
    {
        // Check if effects should be shown on this client based on current fight visibility
        if (!ShouldShowVisualEffectsForEntities(sourceEntityId, targetEntityId))
        {
            Debug.Log($"AttackEffectManager: Skipping direct custom effect '{prefabId}' - not in currently viewed fight");
            return;
        }
        
        StartCoroutine(PlayDirectCustomEffectCoroutine(sourcePosition, targetPosition, prefabId, duration, fallbackType));
    }
    
    /// <summary>
    /// RPC to register a prefab on all clients (sends the actual prefab reference)
    /// </summary>
    [ObserversRpc]
    private void RpcRegisterPrefab(string prefabId, GameObject prefab)
    {
        if (prefab != null && !registeredPrefabs.ContainsKey(prefabId))
        {
            registeredPrefabs[prefabId] = prefab;
            Debug.Log($"AttackEffectManager: Client registered prefab {prefab.name} with ID {prefabId}");
        }
    }
    
    /// <summary>
    /// Coroutine that handles the effect animation
    /// </summary>
    private IEnumerator PlayEffectCoroutine(Vector3 sourcePosition, Vector3 targetPosition, AttackEffectSource.AttackType attackType, float duration)
    {
        GameObject effectObject = GetPooledEffect(attackType);
        if (effectObject == null)
        {
            Debug.LogError($"AttackEffectManager: Could not get pooled effect for {attackType}");
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
        
        // Create attack effect data for tracking
        AttackEffect attackEffect = new AttackEffect
        {
            effectObject = effectObject,
            startPosition = sourcePosition,
            targetPosition = targetPosition,
            duration = duration,
            elapsedTime = 0f,
            attackType = attackType
        };
        
        activeEffects.Add(attackEffect);
        
        // Animate the effect
        while (attackEffect.elapsedTime < duration)
        {
            attackEffect.elapsedTime += Time.deltaTime;
            float progress = attackEffect.elapsedTime / duration;
            
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
        ReturnEffectToPool(effectObject, attackType);
        activeEffects.Remove(attackEffect);
        
        if (debugMode)
        {
            Debug.Log($"AttackEffectManager: Completed {attackType} effect animation");
        }
    }
    
    /// <summary>
    /// Coroutine that handles custom effect animation
    /// </summary>
    private IEnumerator PlayCustomEffectCoroutine(Vector3 sourcePosition, Vector3 targetPosition, string customPrefabName, float duration, AttackEffectSource.AttackType fallbackType = AttackEffectSource.AttackType.Default)
    {
        GameObject effectObject = GetPooledCustomEffect(customPrefabName);
        if (effectObject == null)
        {
            Debug.LogWarning($"AttackEffectManager: Could not get pooled custom effect for {customPrefabName}, falling back to {fallbackType} effect");
            // Fall back to specified attack type effect
            yield return PlayEffectCoroutine(sourcePosition, targetPosition, fallbackType, duration);
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
        
        // Create attack effect data for tracking
        AttackEffect attackEffect = new AttackEffect
        {
            effectObject = effectObject,
            startPosition = sourcePosition,
            targetPosition = targetPosition,
            duration = duration,
            elapsedTime = 0f,
            attackType = AttackEffectSource.AttackType.Default, // Not used for custom effects
            customPrefabName = customPrefabName
        };
        
        activeEffects.Add(attackEffect);
        
        // Animate the effect
        while (attackEffect.elapsedTime < duration)
        {
            attackEffect.elapsedTime += Time.deltaTime;
            float progress = attackEffect.elapsedTime / duration;
            
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
        ReturnCustomEffectToPool(effectObject, customPrefabName);
        activeEffects.Remove(attackEffect);
        
        if (debugMode)
        {
            Debug.Log($"AttackEffectManager: Completed custom {customPrefabName} effect animation");
        }
    }
    
    /// <summary>
    /// Gets a pooled effect object
    /// </summary>
    private GameObject GetPooledEffect(AttackEffectSource.AttackType attackType)
    {
        if (!effectPools.ContainsKey(attackType))
        {
            Debug.LogError($"AttackEffectManager: No pool exists for {attackType}");
            return null;
        }
        
        Queue<GameObject> pool = effectPools[attackType];
        
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        
        // Pool is empty, try to get the assigned prefab
        GameObject prefab = null;
        effectPrefabs.TryGetValue(attackType, out prefab);
        
        if (prefab != null)
        {
            GameObject newEffect = Instantiate(prefab, effectParent);
            Debug.LogWarning($"AttackEffectManager: Pool for {attackType} was empty, created new effect object from assigned prefab");
            return newEffect;
        }
        
        // No prefab assigned, try to create a procedural effect
        Debug.LogWarning($"AttackEffectManager: No prefab assigned for {attackType}, creating procedural effect");
        GameObject proceduralPrefab = CreateProceduralEffect(attackType);
        if (proceduralPrefab != null)
        {
            // Store the procedural prefab for future use
            effectPrefabs[attackType] = proceduralPrefab;
            GameObject newEffect = Instantiate(proceduralPrefab, effectParent);
            Debug.Log($"AttackEffectManager: Created procedural effect instance for {attackType}");
            return newEffect;
        }
        
        Debug.LogError($"AttackEffectManager: Cannot create new effect - failed to create procedural effect for {attackType}");
        return null;
    }
    
    /// <summary>
    /// Returns an effect object to the pool
    /// </summary>
    private void ReturnEffectToPool(GameObject effectObject, AttackEffectSource.AttackType attackType)
    {
        effectObject.SetActive(false);
        effectObject.transform.position = Vector3.zero;
        effectObject.transform.rotation = Quaternion.identity;
        
        if (effectPools.ContainsKey(attackType))
        {
            effectPools[attackType].Enqueue(effectObject);
        }
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
        
        Debug.LogWarning($"AttackEffectManager: Cannot find or create custom effect prefab: {prefabName}");
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
        Debug.Log($"AttackEffectManager: FindCustomEffectPrefab called for '{prefabName}'");
        
        // First check if we already have it cached from the database
        if (customEffectPrefabs.ContainsKey(prefabName))
        {
            Debug.Log($"AttackEffectManager: Found cached prefab for '{prefabName}': {customEffectPrefabs[prefabName].name}");
            return customEffectPrefabs[prefabName];
        }
        
        // Check the custom effect database
        CustomEffectEntry entry = GetCustomEffectEntry(prefabName);
        if (entry != null && entry.effectPrefab != null)
        {
            Debug.Log($"AttackEffectManager: Found prefab in database for '{prefabName}': {entry.effectPrefab.name}");
            customEffectPrefabs[prefabName] = entry.effectPrefab;
            return entry.effectPrefab;
        }
        else
        {
            Debug.Log($"AttackEffectManager: No database entry found for '{prefabName}' (entry: {entry != null}, prefab: {entry?.effectPrefab != null})");
        }
        
        // Fallback: Try to load from Resources folder (legacy support)
        GameObject prefab = Resources.Load<GameObject>($"Effects/{prefabName}");
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>(prefabName);
        }
        
        if (prefab != null)
        {
            Debug.Log($"AttackEffectManager: Found prefab in Resources for '{prefabName}': {prefab.name}");
            customEffectPrefabs[prefabName] = prefab;
            return prefab;
        }
        
        Debug.LogWarning($"AttackEffectManager: Could not find prefab for '{prefabName}' anywhere");
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
        Debug.Log($"AttackEffectManager: Created custom pool of {customPoolSize} objects for {prefabName}");
    }
    
    /// <summary>
    /// Registers a custom prefab for direct use (no Resources folder required)
    /// </summary>
    private string RegisterCustomPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        
        // Create unique ID based on prefab name and instance ID
        string prefabId = $"{prefab.name}_{prefab.GetInstanceID()}";
        
        // Register the prefab if not already registered
        if (!registeredPrefabs.ContainsKey(prefabId))
        {
            registeredPrefabs[prefabId] = prefab;
            Debug.Log($"AttackEffectManager: Registered custom prefab {prefab.name} with ID {prefabId}");
        }
        
        return prefabId;
    }
    
    /// <summary>
    /// Gets a registered prefab by ID
    /// </summary>
    private GameObject GetRegisteredPrefab(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId)) return null;
        
        registeredPrefabs.TryGetValue(prefabId, out GameObject prefab);
        return prefab;
    }
    
    /// <summary>
    /// Coroutine that handles direct custom effect animation using registered prefabs
    /// </summary>
    private IEnumerator PlayDirectCustomEffectCoroutine(Vector3 sourcePosition, Vector3 targetPosition, string prefabId, float duration, AttackEffectSource.AttackType fallbackType)
    {
        GameObject prefab = GetRegisteredPrefab(prefabId);
        if (prefab == null)
        {
            Debug.LogWarning($"AttackEffectManager: Could not find registered prefab with ID {prefabId}, falling back to {fallbackType} effect");
            yield return PlayEffectCoroutine(sourcePosition, targetPosition, fallbackType, duration);
            yield break;
        }
        
        // Directly instantiate the prefab (no pooling for custom effects to avoid complexity)
        GameObject effectObject = Instantiate(prefab, effectParent);
        
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
        
        // Create attack effect data for tracking
        AttackEffect attackEffect = new AttackEffect
        {
            effectObject = effectObject,
            startPosition = sourcePosition,
            targetPosition = targetPosition,
            duration = duration,
            elapsedTime = 0f,
            attackType = fallbackType,
            customPrefabName = prefabId
        };
        
        activeEffects.Add(attackEffect);
        
        // Animate the effect
        while (attackEffect.elapsedTime < duration)
        {
            attackEffect.elapsedTime += Time.deltaTime;
            float progress = attackEffect.elapsedTime / duration;
            
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
        
        // Destroy the effect object (no pooling for custom effects)
        if (effectObject != null)
        {
            Destroy(effectObject);
        }
        
        activeEffects.Remove(attackEffect);
        
        if (debugMode)
        {
            Debug.Log($"AttackEffectManager: Completed direct custom effect animation for {prefab.name}");
        }
    }
    
    /// <summary>
    /// Coroutine to play effect after ensuring prefab registration
    /// </summary>
    private IEnumerator PlayEffectAfterRegistration(Vector3 sourcePosition, Vector3 targetPosition, string prefabId, float duration, AttackEffectSource.AttackType fallbackType, uint sourceEntityId, uint targetEntityId)
    {
        // Small delay to ensure RPC registration completes
        yield return new WaitForSeconds(0.1f);
        
        // Now play the effect
        RpcPlayDirectCustomEffect(sourcePosition, targetPosition, prefabId, duration, fallbackType, sourceEntityId, targetEntityId);
    }
    
    /// <summary>
    /// Creates a procedural effect when no prefab is assigned
    /// </summary>
    private GameObject CreateProceduralEffect(AttackEffectSource.AttackType attackType)
    {
        try
        {
            Debug.Log($"AttackEffectManager: Creating procedural effect for {attackType}");
            
            GameObject effect = new GameObject($"Procedural_{attackType}_Effect");
            ParticleSystem ps = effect.AddComponent<ParticleSystem>();
            
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1f;
            main.startSpeed = 5f;
            main.startSize = 0.2f;
            main.maxParticles = 50;
            
            // Set colors based on attack type
            Color effectColor;
            switch (attackType)
            {
                case AttackEffectSource.AttackType.Melee:
                    effectColor = Color.red;
                    break;
                case AttackEffectSource.AttackType.Ranged:
                    effectColor = Color.blue;
                    break;
                case AttackEffectSource.AttackType.Magic:
                    effectColor = Color.magenta;
                    break;
                default:
                    effectColor = Color.yellow;
                    break;
            }
            main.startColor = effectColor;
            
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
                    Debug.Log($"AttackEffectManager: Applied material {effectMaterial.name} to procedural effect for {attackType}");
                }
                else
                {
                    Debug.LogWarning($"AttackEffectManager: Could not find or create suitable material for procedural effect");
                }
            }
            
            Debug.Log($"AttackEffectManager: Successfully created procedural effect for {attackType}");
            return effect;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AttackEffectManager: Failed to create procedural effect for {attackType}: {e.Message}");
            Debug.LogError($"AttackEffectManager: Stack trace: {e.StackTrace}");
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
                Debug.Log($"AttackEffectManager: Found built-in material: {mat.name}");
                return mat;
            }
        }
        
        // Try to create a simple unlit material as fallback
        Debug.LogWarning("AttackEffectManager: No built-in particle materials found, creating fallback material");
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
                    Debug.Log($"AttackEffectManager: Applied proper material {properMaterial.name} to effect {effectObject.name}");
                }
                else
                {
                    Debug.LogWarning($"AttackEffectManager: Could not get proper material for effect {effectObject.name}");
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
                    material.name = $"AttackEffect_Fallback_{shaderName.Replace("/", "_")}";
                    
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
                    
                    Debug.Log($"AttackEffectManager: Created fallback material with shader: {shaderName}");
                    return material;
                }
            }
            
            Debug.LogError("AttackEffectManager: Could not find any suitable shader for fallback material");
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AttackEffectManager: Failed to create fallback material: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger attack effects
    /// </summary>
    public static void TriggerAttackEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, AttackEffectSource.AttackType attackType, float duration = 0f)
    {
        Debug.Log($"AttackEffectManager: TriggerAttackEffect called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Type: {attackType}, Duration: {duration}");
        
        if (Instance == null)
        {
            Debug.LogError("AttackEffectManager: No instance found. Make sure AttackEffectManager is in the scene.");
            return;
        }
        
        Debug.Log($"AttackEffectManager: Instance found, IsServerStarted: {Instance.IsServerStarted}");
        
        if (Instance.IsServerStarted)
        {
            Debug.Log($"AttackEffectManager: Calling PlayAttackEffect on server");
            Instance.PlayAttackEffect(sourceEntity, targetEntity, attackType, duration);
        }
        else
        {
            Debug.Log($"AttackEffectManager: Triggering local visual effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalVisualEffect(sourceEntity, targetEntity, attackType, duration);
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger custom attack effects
    /// </summary>
    public static void TriggerCustomAttackEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, AttackEffectSource.AttackType fallbackAttackType, float duration = 0f)
    {
        if (Instance == null)
        {
            Debug.LogError("AttackEffectManager: No instance found. Make sure AttackEffectManager is in the scene.");
            return;
        }
        
        if (Instance.IsServerStarted)
        {
            Instance.PlayAttackEffectWithPrefab(sourceEntity, targetEntity, customPrefab, fallbackAttackType, duration);
        }
        else
        {
            Debug.Log($"AttackEffectManager: Triggering local custom prefab effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalCustomPrefabEffect(sourceEntity, targetEntity, customPrefab, fallbackAttackType, duration);
        }
    }
    
    /// <summary>
    /// Public method for external scripts to trigger named custom effects
    /// </summary>
    public static void TriggerNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration = 0f)
    {
        Debug.Log($"AttackEffectManager: TriggerNamedCustomEffect called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Effect: {effectName}, Duration: {duration}");
        
        if (Instance == null)
        {
            Debug.LogError("AttackEffectManager: No instance found. Make sure AttackEffectManager is in the scene.");
            return;
        }
        
        Debug.Log($"AttackEffectManager: Instance found, IsServerStarted: {Instance.IsServerStarted}");
        
        if (Instance.IsServerStarted)
        {
            Debug.Log($"AttackEffectManager: Calling PlayNamedCustomEffect on server");
            Instance.PlayNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
        }
        else
        {
            Debug.Log($"AttackEffectManager: Triggering local named custom effect on client");
            // For clients, trigger the visual effect locally without server validation
            Instance.TriggerLocalNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
        }
    }
    
    /// <summary>
    /// Triggers local visual effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalVisualEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, AttackEffectSource.AttackType attackType, float duration)
    {
        Debug.Log($"AttackEffectManager: TriggerLocalVisualEffect called - Type: {attackType}");
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, attackType, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, attackType, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the visual effect locally
        StartCoroutine(PlayEffectCoroutine(sourcePosition, targetPosition, attackType, duration));
    }
    
    /// <summary>
    /// Triggers local named custom effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalNamedCustomEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration)
    {
        Debug.Log($"AttackEffectManager: TriggerLocalNamedCustomEffect called - Effect: {effectName}");
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, AttackEffectSource.AttackType.Default, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, AttackEffectSource.AttackType.Default, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the custom visual effect locally
        StartCoroutine(PlayCustomEffectCoroutine(sourcePosition, targetPosition, effectName, duration, AttackEffectSource.AttackType.Default));
    }
    
    /// <summary>
    /// Triggers local custom prefab effect without server validation (for client-side visual effects)
    /// </summary>
    private void TriggerLocalCustomPrefabEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, AttackEffectSource.AttackType fallbackAttackType, float duration)
    {
        Debug.Log($"AttackEffectManager: TriggerLocalCustomPrefabEffect called - Prefab: {customPrefab?.name}");
        
        if (customPrefab == null)
        {
            Debug.LogWarning("AttackEffectManager: Custom prefab is null, falling back to attack type effect");
            TriggerLocalVisualEffect(sourceEntity, targetEntity, fallbackAttackType, duration);
            return;
        }
        
        // Register the prefab and trigger the effect
        string prefabId = RegisterCustomPrefab(customPrefab);
        
        // Get positions for the effect
        Vector3 sourcePosition = GetEffectPosition(sourceEntity, fallbackAttackType, true);
        Vector3 targetPosition = GetEffectPosition(targetEntity, fallbackAttackType, false);
        
        if (duration <= 0f)
            duration = defaultEffectDuration;
        
        // Start the custom prefab effect locally
        StartCoroutine(PlayDirectCustomEffectCoroutine(sourcePosition, targetPosition, prefabId, duration, fallbackAttackType));
    }
    
    /// <summary>
    /// Gets the position for an effect on an entity, with fallback positioning
    /// </summary>
    private Vector3 GetEffectPosition(NetworkEntity entity, AttackEffectSource.AttackType attackType, bool isSource)
    {
        if (entity == null)
        {
            Debug.LogWarning("AttackEffectManager: Entity is null, using world origin");
            return Vector3.zero;
        }
        
        // Try to get AttackEffectSource
        AttackEffectSource effectSource = entity.GetComponent<AttackEffectSource>();
        if (effectSource == null)
        {
            effectSource = entity.GetComponentInChildren<AttackEffectSource>();
        }
        
        if (effectSource != null)
        {
            Vector3 position = effectSource.GetEffectPosition(attackType);
            Debug.Log($"AttackEffectManager: Got position from AttackEffectSource for {entity.EntityName.Value}: {position}");
            return position;
        }
        else
        {
            // Fallback to entity position
            Vector3 fallbackPosition = entity.transform.position + Vector3.up * 1f; // Slightly above entity
            Debug.Log($"AttackEffectManager: No AttackEffectSource found on {entity.EntityName.Value}, using fallback position: {fallbackPosition}");
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
                ReturnEffectToPool(effect.effectObject, effect.attackType);
            }
        }
        
        activeEffects.Clear();
        Debug.Log("AttackEffectManager: Cleared all active effects");
    }
    
    /// <summary>
    /// Data structure for tracking active effects
    /// </summary>
    private class AttackEffect
    {
        public GameObject effectObject;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public float duration;
        public float elapsedTime;
        public AttackEffectSource.AttackType attackType;
        public string customPrefabName; // For custom effects
    }
    
    #region Editor Tools
    
    [Header("Testing")]
    [SerializeField] private Transform testSourceTransform;
    [SerializeField] private Transform testTargetTransform;
    [SerializeField] private AttackEffectSource.AttackType testAttackType = AttackEffectSource.AttackType.Default;
    
    [ContextMenu("Test Effect")]
    private void TestEffect()
    {
        if (Application.isPlaying && testSourceTransform != null && testTargetTransform != null)
        {
            StartCoroutine(PlayEffectCoroutine(
                testSourceTransform.position, 
                testTargetTransform.position, 
                testAttackType, 
                defaultEffectDuration
            ));
        }
    }
    
    #endregion
} 