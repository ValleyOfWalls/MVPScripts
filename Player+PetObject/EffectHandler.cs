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
        /* Debug.Log($"EffectHandler: OnActiveEffectsChanged called on {entity.EntityName.Value} - Operation: {op}, Key: {key}, Item: {item}, AsServer: {asServer}"); */
        
        // Refresh our local cache whenever the network dictionary changes
        RefreshEffectCache();
        
        // Notify listeners that effects have changed
        int listenerCount = OnEffectsChanged?.GetInvocationList()?.Length ?? 0;
        /* Debug.Log($"EffectHandler: About to invoke OnEffectsChanged for {entity.EntityName.Value}, Listener count: {listenerCount}"); */
        
        // Debug stats UI controllers if no listeners found
        if (listenerCount == 0)
        {
            DebugStatsUIControllers();
        }
        
        OnEffectsChanged?.Invoke();
        
        Debug.Log($"EffectHandler: OnEffectsChanged event invoked for {entity.EntityName.Value}, Active effects count: {activeEffects.Count}");
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
        
        // Special handling for different effect types
        switch (effectName)
        {
            case "Strength":
            case "Curse":
            case "Burn":
            case "Salve":
                // Strength, Curse, Burn, and Salve don't use duration - they tick down by 1 each turn
                // If effect already exists, add to the potency
                if (activeEffects.ContainsKey(effectName))
                {
                    string existingData = activeEffects[effectName];
                    string[] parts = existingData.Split('|');
                    if (parts.Length >= 4)
                    {
                        int existingPotency = int.Parse(parts[1]);
                        potency += existingPotency; // Stack the effect
                    }
                }
                duration = 999; // High duration since it ticks down by potency
                break;
                
            case "Break":
            case "Weak":
                // Break and Weak don't use amount - they only have duration
                // If effect already exists, add to the duration
                if (activeEffects.ContainsKey(effectName))
                {
                    string existingData = activeEffects[effectName];
                    string[] parts = existingData.Split('|');
                    if (parts.Length >= 4)
                    {
                        int existingDuration = int.Parse(parts[2]);
                        duration += existingDuration; // Stack the duration
                        /* Debug.Log($"EffectHandler: Stacking {effectName} duration, new total: {duration}"); */
                    }
                }
                potency = 1; // Always 1 for Break/Weak since they're binary effects
                break;
                
            case "Thorns":
                // Thorns stacks with existing Thorns effects
                // If effect already exists, add to the potency
                if (activeEffects.ContainsKey(effectName))
                {
                    string existingData = activeEffects[effectName];
                    string[] parts = existingData.Split('|');
                    if (parts.Length >= 4)
                    {
                        int existingPotency = int.Parse(parts[1]);
                        potency += existingPotency; // Stack the effect
                        /* Debug.Log($"EffectHandler: Stacking {effectName} potency, new total: {potency}"); */
                    }
                }
                // Thorns lasts until start of entity's next turn - give it 1 turn duration
                duration = 1;
                break;
                
            case "Shield":
                // Shield stacks with existing Shield effects
                // If effect already exists, add to the potency
                if (activeEffects.ContainsKey(effectName))
                {
                    string existingData = activeEffects[effectName];
                    string[] parts = existingData.Split('|');
                    if (parts.Length >= 4)
                    {
                        int existingPotency = int.Parse(parts[1]);
                        potency += existingPotency; // Stack the effect
                        /* Debug.Log($"EffectHandler: Stacking {effectName} potency, new total: {potency}"); */
                    }
                }
                // Shield lasts until consumed or manually removed
                duration = 999; // High duration since it's consumed by damage
                break;
                
            case "Stun":
                // Stun now sets fizzle card count instead of blocking turns
                EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
                if (entityTracker != null)
                {
                    entityTracker.AddFizzleCards(potency); // potency = number of cards to fizzle
                    entityTracker.SetStunned(true); // Legacy flag for backwards compatibility
                }
                // Set duration to 1 since fizzle is handled per-card, not per-turn
                duration = 1;
                break;
        }
        
        // Format effect data as a string for syncing
        string effectData = $"{effectName}|{potency}|{duration}|{sourceId}";
        
        // Add or update in sync dictionary
        activeEffects[effectName] = effectData;
        
        /* Debug.Log($"EffectHandler: Added effect {effectName} with potency {potency} for {duration} turns to {entity.EntityName.Value}"); */
        
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
            // Handle special cleanup for Strength effects
            if (effectName == "Strength")
            {
                EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
                if (entityTracker != null && entityTracker.IsServerInitialized)
                {
                    int strengthPotency = GetEffectPotency(effectName);
                    if (strengthPotency > 0)
                    {
                        entityTracker.RemoveStrength(strengthPotency);
                        Debug.Log($"EffectHandler: Removed {strengthPotency} Strength from EntityTracker for {entity.EntityName.Value}");
                    }
                }
            }
            
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
        
        // Clear EntityTracker strength stacks when clearing Strength effects
        if (HasEffect("Strength"))
        {
            EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
            if (entityTracker != null && entityTracker.IsServerInitialized)
            {
                int strengthStacks = entityTracker.StrengthStacks;
                if (strengthStacks > 0)
                {
                    entityTracker.RemoveStrength(strengthStacks);
                    Debug.Log($"EffectHandler: Cleared {strengthStacks} Strength stacks from EntityTracker for {entity.EntityName.Value}");
                }
            }
        }
        
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
        
        /* Debug.Log($"EffectHandler: Processing end of turn effects for {entity.EntityName.Value}"); */
        
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
                case "Burn":
                    if (lifeHandler != null)
                    {
                        lifeHandler.TakeDamage(potency, sourceEntity);
                        /* Debug.Log($"EffectHandler: Applied {potency} Burn damage to {entity.EntityName.Value}"); */
                    }
                    
                    // Reduce potency by 1 each turn
                    potency--;
                    if (potency <= 0)
                    {
                        activeEffects.Remove(effectKey);
                        /* Debug.Log($"EffectHandler: Burn effect expired on {entity.EntityName.Value}"); */
                        continue; // Skip normal duration processing
                    }
                    else
                    {
                        // Update with reduced potency
                        activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
                        continue; // Skip normal duration processing
                    }
                    
                case "Salve":
                    if (lifeHandler != null)
                    {
                        lifeHandler.Heal(potency, sourceEntity);
                        /* Debug.Log($"EffectHandler: Applied {potency} Salve healing to {entity.EntityName.Value}"); */
                    }
                    
                    // Reduce potency by 1 each turn
                    potency--;
                    if (potency <= 0)
                    {
                        activeEffects.Remove(effectKey);
                        /* Debug.Log($"EffectHandler: Salve effect expired on {entity.EntityName.Value}"); */
                        continue; // Skip normal duration processing
                    }
                    else
                    {
                        // Update with reduced potency
                        activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
                        continue; // Skip normal duration processing
                    }
                    
                case "Thorns":
                    // Thorns is processed when taking damage, not at end of turn
                    // Thorns should last until start of entity's next turn, so skip duration processing
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has Thorns ({potency}) active"); */
                    continue; // Skip normal duration processing - thorns is removed at start of turn instead
                    
                case "Shield":
                    // Shield persists, no end-of-turn processing needed
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has Shield ({potency}) active"); */
                    break;
                    
                case "Fire":
                    // Fire DoT effect
                    if (lifeHandler != null)
                    {
                        lifeHandler.TakeDamage(potency, sourceEntity);
                        /* Debug.Log($"EffectHandler: Applied {potency} Fire damage to {entity.EntityName.Value}"); */
                    }
                    break;
                    
                case "Ice":
                    // Ice slows/freezes - could reduce energy or skip turn
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} is affected by Ice ({potency})"); */
                    break;
                    
                case "Lightning":
                    // Lightning could chain or cause energy loss
                    if (entity.CurrentEnergy.Value > 0)
                    {
                        entity.ChangeEnergy(-potency);
                        /* Debug.Log($"EffectHandler: Lightning drained {potency} energy from {entity.EntityName.Value}"); */
                    }
                    break;
                    
                case "Void":
                    // Void could prevent healing or cause other effects
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} is affected by Void ({potency})"); */
                    break;
                    
                case "Stun":
                    // Stun now causes fizzle instead of preventing card play entirely
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has fizzle effect for {duration} more turns"); */
                    break;
                    
                case "LimitBreak":
                    // Limit break enhances abilities
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} is in Limit Break state"); */
                    break;
                    
                case "Strength":
                    // Strength increases damage output - the primary positive damage modifier
                    // Handled by both EntityTracker.StrengthStacks and EffectHandler for damage calculations
                    // Ticks down by 1 each turn instead of duration-based
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has +{potency} strength"); */
                    
                    // Reduce potency by 1 each turn
                    potency--;
                    if (potency <= 0)
                    {
                        activeEffects.Remove(effectKey);
                        /* Debug.Log($"EffectHandler: Strength effect expired on {entity.EntityName.Value}"); */
                        continue; // Skip normal duration processing
                    }
                    else
                    {
                        // Update with reduced potency
                        activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
                        continue; // Skip normal duration processing
                    }
                    
                case "Curse":
                    // Curse reduces damage output - handled by other systems when dealing damage
                    // Ticks down by 1 each turn instead of duration-based
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has -{potency} damage curse active"); */
                    
                    // Reduce potency by 1 each turn
                    potency--;
                    if (potency <= 0)
                    {
                        activeEffects.Remove(effectKey);
                        /* Debug.Log($"EffectHandler: Curse effect expired on {entity.EntityName.Value}"); */
                        continue; // Skip normal duration processing
                    }
                    else
                    {
                        // Update with reduced potency
                        activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
                        continue; // Skip normal duration processing
                    }
                    
                case "Weak":
                    // Weak reduces damage output
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} is weakened for {duration} more turns"); */
                    break;
                    
                case "Break":
                    // Break increases damage taken
                    /* Debug.Log($"EffectHandler: {entity.EntityName.Value} is broken and takes extra damage for {duration} more turns"); */
                    break;
                    
                // Add more end-of-turn effect types here
            }
            
            // Decrement duration
            duration--;
            
            // Remove or update the effect
            if (duration <= 0)
            {
                activeEffects.Remove(effectKey);
                /* Debug.Log($"EffectHandler: Effect {effectName} expired on {entity.EntityName.Value}"); */
                
                // Handle effect expiration
                HandleEffectExpiration(effectName);
            }
            else
            {
                // Update with new duration
                activeEffects[effectKey] = $"{effectName}|{potency}|{duration}|{sourceId}";
            }
        }
    }
    
    /// <summary>
    /// Handles special logic when effects expire
    /// </summary>
    private void HandleEffectExpiration(string effectName)
    {
        switch (effectName)
        {
            case "Stun":
                // Stun effect expiry is now handled by fizzle card count
                EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
                if (entityTracker != null)
                {
                    // Legacy stun clear for backwards compatibility
                    entityTracker.SetStunned(false);
                    // Note: Fizzle count is managed separately and counts down per card played
                }
                break;
                
            case "LimitBreak":
                // Limit break system removed
                break;
        }
    }
    
    /// <summary>
    /// Processes thorns damage when this entity takes damage
    /// </summary>
    [Server]
    public void ProcessThornsReflection(NetworkEntity attacker, int damageAmount)
    {
        if (!IsServerInitialized || attacker == null) return;
        
        // Refresh effect cache to ensure we have the latest data
        RefreshEffectCache();
        
        if (HasEffect("Thorns"))
        {
            int thornsPotency = GetEffectPotency("Thorns");
            // Thorns reflects the full amount regardless of damage taken
            int reflectedDamage = thornsPotency;
            
            LifeHandler attackerLifeHandler = attacker.GetComponent<LifeHandler>();
            if (attackerLifeHandler != null)
            {
                attackerLifeHandler.TakeDamage(reflectedDamage, entity);
                Debug.Log($"EffectHandler: {entity.EntityName.Value} reflected {reflectedDamage} thorns damage to {attacker.EntityName.Value} (took {damageAmount} damage, thorns potency: {thornsPotency})");
            }
            else
            {
                Debug.LogError($"EffectHandler: Could not find LifeHandler on attacker {attacker.EntityName.Value} for thorns reflection");
            }
        }
        else
        {
            Debug.Log($"EffectHandler: {entity.EntityName.Value} does not have Thorns effect when processing reflection (damage taken: {damageAmount})");
        }
    }
    
    /// <summary>
    /// Processes shield absorption when this entity takes damage
    /// </summary>
    [Server]
    public int ProcessShieldAbsorption(int incomingDamage)
    {
        if (!IsServerInitialized) return incomingDamage;
        
        if (HasEffect("Shield"))
        {
            int shieldAmount = GetEffectPotency("Shield");
            int absorbedDamage = Mathf.Min(shieldAmount, incomingDamage);
            int remainingDamage = incomingDamage - absorbedDamage;
            
            // Reduce shield amount
            int newShieldAmount = shieldAmount - absorbedDamage;
            if (newShieldAmount <= 0)
            {
                RemoveEffect("Shield");
                Debug.Log($"EffectHandler: {entity.EntityName.Value}'s shield was destroyed, absorbed {absorbedDamage} damage");
            }
            else
            {
                // Update shield with reduced amount
                UpdateEffectPotency("Shield", newShieldAmount);
                /* Debug.Log($"EffectHandler: {entity.EntityName.Value}'s shield absorbed {absorbedDamage} damage, {newShieldAmount} shield remaining"); */
            }
            
            return remainingDamage;
        }
        
        return incomingDamage;
    }
    
    /// <summary>
    /// Updates the potency of an existing effect
    /// </summary>
    [Server]
    private void UpdateEffectPotency(string effectName, int newPotency)
    {
        if (!IsServerInitialized || !activeEffects.ContainsKey(effectName)) return;
        
        string effectData = activeEffects[effectName];
        string[] parts = effectData.Split('|');
        
        if (parts.Length >= 4)
        {
            parts[1] = newPotency.ToString(); // Update potency
            activeEffects[effectName] = string.Join("|", parts);
        }
    }
    
    /// <summary>
    /// Processes effects at the start of this entity's turn
    /// </summary>
    [Server]
    public void ProcessStartOfTurnEffects()
    {
        if (!IsServerInitialized) return;
        
        /* Debug.Log($"EffectHandler: Processing start of turn effects for {entity.EntityName.Value}"); */
        
        // Remove Shield at the start of the entity's turn
        if (HasEffect("Shield"))
        {
            RemoveEffect("Shield");
            Debug.Log($"EffectHandler: Removed Shield from {entity.EntityName.Value} at start of turn");
        }
        
        // Remove Thorns at the start of the entity's turn (they only last until start of next turn)
        if (HasEffect("Thorns"))
        {
            RemoveEffect("Thorns");
            Debug.Log($"EffectHandler: Removed Thorns from {entity.EntityName.Value} at start of turn");
        }
        
        // Process any other start-of-turn effects here as needed
    }
    
    /// <summary>
    /// Processes effects at the start of a round
    /// </summary>
    [Server]
    public void ProcessStartOfRoundEffects()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log($"EffectHandler: Processing start of round effects for {entity.EntityName.Value}");
        
        // Process start-of-round effects here as needed for your game mechanics
        // Note: Shield removal now happens at start of individual entity turns
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
                
            case "Strength":
                // Update EntityTracker strength stacks for damage calculation compatibility
                EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
                if (entityTracker != null && entityTracker.IsServerInitialized)
                {
                    entityTracker.AddStrength(potency);
                    Debug.Log($"EffectHandler: Added {potency} Strength to EntityTracker for {entity.EntityName.Value}");
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
        // Refresh cache to ensure we have the latest data
        RefreshEffectCache();
        
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
    /// Gets a formatted list of active effect names and potencies for UI display
    /// </summary>
    public List<string> GetActiveEffects()
    {
        RefreshEffectCache();
        List<string> effectNames = new List<string>();
        
        // Add current stance if not None
        EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            StanceType currentStance = entityTracker.CurrentStance;
            Debug.Log($"EffectHandler: GetActiveEffects for {entity.EntityName.Value} - Current stance: {currentStance}");
            if (currentStance != StanceType.None)
            {
                effectNames.Add($"Stance: {currentStance}");
                Debug.Log($"EffectHandler: Added stance '{currentStance}' to effect list for {entity.EntityName.Value}");
            }
            else
            {
                Debug.Log($"EffectHandler: Stance is None for {entity.EntityName.Value}, not adding to effect list");
            }
        }
        else
        {
            Debug.Log($"EffectHandler: No EntityTracker found on {entity.EntityName.Value}");
        }
        
        foreach (var effect in effectCache.Values)
        {
            if (effect.RemainingDuration > 0 || effect.Potency > 0)
            {
                string effectDisplay;
                
                // Format based on effect type mechanics
                switch (effect.EffectName)
                {
                    case "Strength":
                    case "Curse":
                        // Always show potency for damage modifiers
                        effectDisplay = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    case "Burn":
                    case "Salve":
                        // Always show potency for DoT/HoT effects (they tick down by potency)
                        effectDisplay = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    case "Break":
                    case "Weak":
                        // Show duration only (they don't have meaningful potency)
                        effectDisplay = effect.RemainingDuration > 1 ? 
                            $"{effect.EffectName} ({effect.RemainingDuration})" : 
                            effect.EffectName;
                        break;
                        
                    case "Thorns":
                        // Always show potency for Thorns since the amount is important
                        effectDisplay = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    case "Shield":
                        // Always show potency for Shield since the amount is important
                        effectDisplay = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    default:
                        // For other effects, always show potency if > 0
                        effectDisplay = effect.Potency > 0 ? 
                            $"{effect.EffectName} ({effect.Potency})" : 
                            effect.EffectName;
                        break;
                }
                
                effectNames.Add(effectDisplay);
            }
        }
        
        return effectNames;
    }
    
    /// <summary>
    /// Gets the total damage modification from all active curse effects (negative Strength)
    /// </summary>
    public int GetDamageModification()
    {
        int modification = 0;
        
        // Subtract damage from curses (negative strength)
        if (HasEffect("Curse"))
        {
            modification -= GetEffectPotency("Curse");
        }
        
        return modification;
    }
    
    /// <summary>
    /// Gets the damage taken multiplier from effects like Break
    /// </summary>
    public float GetDamageTakenMultiplier()
    {
        float multiplier = 1.0f;
        
        if (HasEffect("Break"))
        {
            // Break increases damage taken by 50% (this matches GameManager's BreakStatusModifier)
            multiplier *= 1.5f;
        }
        
        return multiplier;
    }
    
    /// <summary>
    /// Gets the damage dealt multiplier from effects like Weak
    /// </summary>
    public float GetDamageDealtMultiplier()
    {
        float multiplier = 1.0f;
        
        if (HasEffect("Weak"))
        {
            // Use OnlineGameManager's WeakStatusModifier for consistency
            if (OnlineGameManager.Instance != null)
            {
                multiplier *= OnlineGameManager.Instance.WeakStatusModifier.Value;
                /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has Weak effect, using OnlineGameManager modifier: {OnlineGameManager.Instance.WeakStatusModifier.Value}"); */
            }
            else
            {
                // Fallback to hardcoded value if OnlineGameManager not available
                multiplier *= 0.75f;
                /* Debug.Log($"EffectHandler: {entity.EntityName.Value} has Weak effect, using fallback modifier: 0.75"); */
            }
        }
        
        return multiplier;
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
    
    /// <summary>
    /// Debug method to check all EntityStatsUIController instances and their status
    /// </summary>
    private void DebugStatsUIControllers()
    {
        /* Debug.Log($"=== DEBUGGING STATS UI CONTROLLERS FOR {entity.EntityName.Value} (ID: {entity.ObjectId}) ==="); */
        
        // Find all EntityStatsUIController instances in the scene
        EntityStatsUIController[] allControllers = FindObjectsByType<EntityStatsUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        /* Debug.Log($"Found {allControllers.Length} EntityStatsUIController instances in scene (including inactive)"); */
        
        bool foundUnlinkedControllers = false;
        bool foundControllerForThisEntity = false;
        
        for (int i = 0; i < allControllers.Length; i++)
        {
            var controller = allControllers[i];
            NetworkEntity linkedEntity = controller.GetLinkedEntity();
            bool isVisible = controller.IsVisible();
            bool isLinked = controller.IsLinked();
            
            // Get the stats entity (the UI entity itself)
            NetworkEntity statsEntity = controller.GetComponent<NetworkEntity>();
            string statsEntityInfo = statsEntity != null ? $"StatsEntity: {statsEntity.EntityName.Value} (ID: {statsEntity.ObjectId}, Type: {statsEntity.EntityType})" : "StatsEntity: NULL";
            
            /* Debug.Log($"  [{i}] Controller on {controller.gameObject.name}:"); */
            /* Debug.Log($"      {statsEntityInfo}"); */
            /* Debug.Log($"      Linked to: {(linkedEntity != null ? $"{linkedEntity.EntityName.Value} (ID: {linkedEntity.ObjectId})" : "NULL")}"); */
            /* Debug.Log($"      Visible: {isVisible}, IsLinked: {isLinked}"); */
            
            if (!isLinked)
            {
                foundUnlinkedControllers = true;
            }
            
            // Check if this controller is linked to our entity
            if (linkedEntity != null && linkedEntity.ObjectId == entity.ObjectId)
            {
                foundControllerForThisEntity = true;
                /* Debug.Log($"      *** THIS CONTROLLER IS LINKED TO {entity.EntityName.Value}! ***"); */
                
                // Check if it's subscribed to our EffectHandler
                if (controller.GetComponent<EffectHandler>() != null)
                {
                    var effectHandler = linkedEntity.GetComponent<EffectHandler>();
                    if (effectHandler != null)
                    {
                        int listenerCount = effectHandler.OnEffectsChanged?.GetInvocationList()?.Length ?? 0;
                        /* Debug.Log($"      EffectHandler listener count: {listenerCount}"); */
                    }
                }
            }
        }
        
        /* Debug.Log($"Summary: Found controller for this entity: {foundControllerForThisEntity}, Found unlinked controllers: {foundUnlinkedControllers}"); */
        
        // If we found unlinked controllers, try to force reconnect them
        if (foundUnlinkedControllers)
        {
            /* Debug.Log($"Found unlinked controllers, attempting force reconnect..."); */
            EntityStatsUIController.ForceReconnectAllStatsUIControllers();
        }
        else if (!foundControllerForThisEntity)
        {
            /* Debug.Log($"No controller found for {entity.EntityName.Value} (ID: {entity.ObjectId}), but all controllers are linked. This suggests a mismatch in entity relationships."); */
        }
        
        /* Debug.Log($"=== END STATS UI DEBUG ==="); */
    }
}

// ═══════════════════════════════════════════════════════════════
// ZONE EFFECT STRUCTURES (moved from CardEnums.cs)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Legacy data structure for zone effects (for backward compatibility)
/// </summary>
[System.Serializable]
public class ZoneEffect
{
    public CardEffectType effectType;
    public int baseAmount;
    public int duration;

    public bool affectAllPlayers;
    public bool affectAllPets;
    public bool affectCaster;
    public bool excludeOpponents;
    public ScalingType scalingType;
    public float scalingMultiplier;
} 