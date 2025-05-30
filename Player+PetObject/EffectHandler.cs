using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System;

/// <summary>
/// Handles status effects applied to an entity, tracking their duration and potency.
/// Attach to: Entity GameObjects along with NetworkEntity.
/// </summary>
public class EffectHandler : NetworkBehaviour
{
    [System.Serializable]
    public class StatusEffect
    {
        public string EffectName;
        public int Potency;
        public int RemainingDuration;
        public int SourceEntityId;
        
        public StatusEffect(string name, int potency, int duration, int sourceId)
        {
            EffectName = name;
            Potency = potency;
            RemainingDuration = duration;
            SourceEntityId = sourceId;
        }
        
        public override string ToString()
        {
            return $"{EffectName} ({Potency}) - {RemainingDuration} turns left";
        }
    }
    
    [Header("References")]
    [SerializeField] private NetworkEntity entity;
    [SerializeField] private LifeHandler lifeHandler;
    
    // Network synchronized dictionary of active effects
    // Keys are effect names, values are effect data as serialized strings
    private readonly SyncDictionary<string, string> activeEffects = new SyncDictionary<string, string>();
    
    // Local-only effect cache (reconstructed from syncdict for easier access)
    private Dictionary<string, StatusEffect> effectCache = new Dictionary<string, StatusEffect>();
    
    // Event fired when effects change
    public event Action OnEffectsChanged;
    
    private CombatManager combatManager;
    
    private void Awake()
    {
        // Get required references
        if (entity == null) entity = GetComponent<NetworkEntity>();
        if (lifeHandler == null) lifeHandler = GetComponent<LifeHandler>();
        
        // Subscribe to network dictionary changes
        activeEffects.OnChange += OnActiveEffectsChanged;
    }
    
    private void Start()
    {
        // Find combat manager
        combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager == null)
        {
            Debug.LogWarning($"EffectHandler on {gameObject.name}: Could not find CombatManager");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from network dictionary changes
        activeEffects.OnChange -= OnActiveEffectsChanged;
    }
    
    private void OnActiveEffectsChanged(SyncDictionaryOperation op, string key, string item, bool asServer)
    {
        // Refresh our local cache whenever the network dictionary changes
        RefreshEffectCache();
        
        // Notify listeners that effects have changed
        OnEffectsChanged?.Invoke();
    }
    
    private void RefreshEffectCache()
    {
        // Clear existing cache
        effectCache.Clear();
        
        // Rebuild from network dictionary
        foreach (var kvp in activeEffects)
        {
            try
            {
                string[] parts = kvp.Value.Split('|');
                if (parts.Length >= 4)
                {
                    string name = parts[0];
                    int potency = int.Parse(parts[1]);
                    int duration = int.Parse(parts[2]);
                    int sourceId = int.Parse(parts[3]);
                    
                    effectCache[kvp.Key] = new StatusEffect(name, potency, duration, sourceId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectHandler: Error parsing effect data: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Adds or updates a status effect on this entity
    /// </summary>
    /// <param name="effectName">Name of the effect</param>
    /// <param name="potency">Strength of the effect</param>
    /// <param name="duration">Duration in turns</param>
    /// <param name="source">Entity that applied the effect</param>
    [Server]
    public void AddEffect(string effectName, int potency, int duration, NetworkEntity source)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"EffectHandler on {gameObject.name}: AddEffect called outside of server context");
            return;
        }
        
        int sourceId = source != null ? source.ObjectId : 0;
        
        // Format effect data as a string for syncing
        string effectData = $"{effectName}|{potency}|{duration}|{sourceId}";
        
        // Add or update in sync dictionary
        activeEffects[effectName] = effectData;
        
        Debug.Log($"EffectHandler: Added effect {effectName} with potency {potency} for {duration} turns to {entity.EntityName.Value}");
        
        // Trigger any immediate effects (like DoT or HoT)
        ProcessImmediateEffects(effectName, potency, source);
    }
    
    /// <summary>
    /// Removes a status effect from this entity
    /// </summary>
    [Server]
    public void RemoveEffect(string effectName)
    {
        if (!IsServerInitialized) return;
        
        if (activeEffects.ContainsKey(effectName))
        {
            activeEffects.Remove(effectName);
            Debug.Log($"EffectHandler: Removed effect {effectName} from {entity.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Clears all status effects from this entity
    /// </summary>
    [Server]
    public void ClearAllEffects()
    {
        if (!IsServerInitialized) return;
        
        activeEffects.Clear();
        Debug.Log($"EffectHandler: Cleared all effects from {entity.EntityName.Value}");
    }
    
    /// <summary>
    /// Processes all effects at the end of this entity's turn
    /// </summary>
    [Server]
    public void ProcessEndOfTurnEffects()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log($"EffectHandler: Processing end of turn effects for {entity.EntityName.Value}");
        
        // Get a list of keys to avoid collection modified exception
        List<string> effectKeys = new List<string>(activeEffects.Keys);
        
        foreach (string effectKey in effectKeys)
        {
            string effectData = activeEffects[effectKey];
            string[] parts = effectData.Split('|');
            
            if (parts.Length < 4) continue;
            
            string effectName = parts[0];
            int potency = int.Parse(parts[1]);
            int duration = int.Parse(parts[2]);
            int sourceId = int.Parse(parts[3]);
            
            // Find source entity
            NetworkEntity sourceEntity = FindEntityById(sourceId);
            
            // Process specific effects
            switch (effectName)
            {
                case "DamageOverTime":
                    if (lifeHandler != null)
                    {
                        lifeHandler.TakeDamage(potency, sourceEntity);
                        Debug.Log($"EffectHandler: Applied {potency} DoT damage to {entity.EntityName.Value}");
                    }
                    break;
                    
                case "HealOverTime":
                    if (lifeHandler != null)
                    {
                        lifeHandler.Heal(potency, sourceEntity);
                        Debug.Log($"EffectHandler: Applied {potency} HoT healing to {entity.EntityName.Value}");
                    }
                    break;
                    
                // Add more end-of-turn effect types here
            }
            
            // Decrement duration
            duration--;
            
            // Remove or update the effect
            if (duration <= 0)
            {
                activeEffects.Remove(effectKey);
                Debug.Log($"EffectHandler: Effect {effectName} expired on {entity.EntityName.Value}");
            }
            else
            {
                // Update with new duration
                activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
            }
        }
    }
    
    /// <summary>
    /// Processes effects at the start of this entity's turn
    /// </summary>
    [Server]
    public void ProcessStartOfTurnEffects()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log($"EffectHandler: Processing start of turn effects for {entity.EntityName.Value}");
        
        // Similar to ProcessEndOfTurnEffects but for start-of-turn effects
        // Implement as needed for your game mechanics
    }
    
    /// <summary>
    /// Processes effects at the start of a round
    /// </summary>
    [Server]
    public void ProcessStartOfRoundEffects()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log($"EffectHandler: Processing start of round effects for {entity.EntityName.Value}");
        
        // Similar to ProcessEndOfTurnEffects but for start-of-round effects
        // Implement as needed for your game mechanics
    }
    
    /// <summary>
    /// Processes any immediate effects when an effect is first applied
    /// </summary>
    private void ProcessImmediateEffects(string effectName, int potency, NetworkEntity source)
    {
        // Handle any immediate effects, like instant damage or healing
        switch (effectName)
        {
            case "InstantDamage":
                if (lifeHandler != null)
                {
                    lifeHandler.TakeDamage(potency, source);
                    Debug.Log($"EffectHandler: Applied {potency} instant damage to {entity.EntityName.Value}");
                }
                break;
                
            case "InstantHeal":
                if (lifeHandler != null)
                {
                    lifeHandler.Heal(potency, source);
                    Debug.Log($"EffectHandler: Applied {potency} instant healing to {entity.EntityName.Value}");
                }
                break;
                
            // Add more immediate effect types here
        }
    }
    
    /// <summary>
    /// Checks if the entity has a specific effect
    /// </summary>
    public bool HasEffect(string effectName)
    {
        if (activeEffects.ContainsKey(effectName))
        {
            return true;
        }
        
        // Check for effect categories (e.g., "Damage" category containing "Burn", "Poison", etc.)
        foreach (var kvp in activeEffects)
        {
            if (kvp.Key.StartsWith(effectName)) return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets the potency of a specific effect
    /// </summary>
    public int GetEffectPotency(string effectName)
    {
        // First try direct lookup in our cache
        if (effectCache.TryGetValue(effectName, out StatusEffect effect))
        {
            return effect.Potency;
        }
        
        // Otherwise look in the network dictionary and parse
        if (activeEffects.TryGetValue(effectName, out string effectData))
        {
            string[] parts = effectData.Split('|');
            if (parts.Length >= 2)
            {
                return int.Parse(parts[1]);
            }
        }
        
        return 0; // No effect or invalid format
    }
    
    /// <summary>
    /// Gets the remaining duration of a specific effect
    /// </summary>
    public int GetEffectDuration(string effectName)
    {
        // First try direct lookup in our cache
        if (effectCache.TryGetValue(effectName, out StatusEffect effect))
        {
            return effect.RemainingDuration;
        }
        
        // Otherwise look in the network dictionary and parse
        if (activeEffects.TryGetValue(effectName, out string effectData))
        {
            string[] parts = effectData.Split('|');
            if (parts.Length >= 3)
            {
                return int.Parse(parts[2]);
            }
        }
        
        return 0; // No effect or invalid format
    }
    
    /// <summary>
    /// Gets all active effects as a list
    /// </summary>
    public List<StatusEffect> GetAllEffects()
    {
        RefreshEffectCache();
        return new List<StatusEffect>(effectCache.Values);
    }
    
    /// <summary>
    /// Finds a NetworkEntity by its object ID
    /// </summary>
    private NetworkEntity FindEntityById(int entityId)
    {
        if (entityId == 0) return null;
        
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
} 