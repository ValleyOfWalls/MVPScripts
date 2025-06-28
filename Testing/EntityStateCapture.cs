using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Captures and restores entity states for testing purposes
/// </summary>
[System.Serializable]
public class EntityState
{
    public int entityId;
    public string entityName;
    public int maxHealth;
    public int currentHealth;
    public int maxEnergy;
    public int currentEnergy;
    public List<StatusEffectSnapshot> activeEffects;
    public int currency;
    
    // EntityTracker state
    public StanceType currentStance;
    public int comboCount;
    public bool isStunned;
    public bool isInLimitBreak;

    public EntityState()
    {
        activeEffects = new List<StatusEffectSnapshot>();
        currentStance = StanceType.None;
        comboCount = 0;
        isStunned = false;
        isInLimitBreak = false;
    }
}

[System.Serializable]
public class StatusEffectSnapshot
{
    public string effectName;
    public int potency;
    public int remainingDuration;
    public int sourceEntityId;

    public StatusEffectSnapshot(string name, int pot, int dur, int sourceId)
    {
        effectName = name;
        potency = pot;
        remainingDuration = dur;
        sourceEntityId = sourceId;
    }
}

/// <summary>
/// Utility class for capturing and restoring entity states during testing
/// </summary>
public static class EntityStateCapture
{
    /// <summary>
    /// Capture the current state of an entity
    /// </summary>
    public static EntityState CaptureEntityState(NetworkEntity entity)
    {
        if (entity == null) return null;

        var state = new EntityState
        {
            entityId = entity.ObjectId,
            entityName = entity.EntityName.Value,
            maxHealth = entity.MaxHealth.Value,
            currentHealth = entity.CurrentHealth.Value,
            maxEnergy = entity.MaxEnergy.Value,
            currentEnergy = entity.CurrentEnergy.Value,
            currency = entity.Currency.Value
        };

        // Capture active effects
        var effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            var effects = effectHandler.GetAllEffects();
            foreach (var effect in effects)
            {
                state.activeEffects.Add(new StatusEffectSnapshot(
                    effect.EffectName,
                    effect.Potency,
                    effect.RemainingDuration,
                    effect.SourceEntityId
                ));
            }
        }
        
        // Capture EntityTracker state
        var entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            state.currentStance = entityTracker.CurrentStance;
            state.comboCount = entityTracker.ComboCount;
            state.isStunned = entityTracker.IsStunned;
            state.isInLimitBreak = entityTracker.IsInLimitBreak;
        }

        return state;
    }

    /// <summary>
    /// Restore an entity to a previously captured state
    /// </summary>
    public static void RestoreEntityState(NetworkEntity entity, EntityState state)
    {
        if (entity == null || state == null) return;

        // Clear all effects first
        var effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            effectHandler.ClearAllEffects();
        }

        // Restore basic stats
        entity.MaxHealth.Value = state.maxHealth;
        entity.CurrentHealth.Value = state.currentHealth;
        entity.MaxEnergy.Value = state.maxEnergy;
        entity.CurrentEnergy.Value = state.currentEnergy;
        entity.SetCurrency(state.currency);

        // Restore effects
        if (effectHandler != null && state.activeEffects != null)
        {
            foreach (var effectSnapshot in state.activeEffects)
            {
                // Find source entity for effect restoration
                NetworkEntity sourceEntity = FindEntityById(effectSnapshot.sourceEntityId);
                effectHandler.AddEffect(
                    effectSnapshot.effectName,
                    effectSnapshot.potency,
                    effectSnapshot.remainingDuration,
                    sourceEntity
                );
            }
        }
        
        // Restore EntityTracker state
        var entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            entityTracker.SetStance(state.currentStance);
            entityTracker.SetComboCount(state.comboCount);
            entityTracker.SetStunned(state.isStunned);
            entityTracker.SetLimitBreak(state.isInLimitBreak);
        }
    }

    /// <summary>
    /// Capture states of all entities involved in combat
    /// </summary>
    public static Dictionary<int, EntityState> CaptureAllCombatEntityStates()
    {
        var states = new Dictionary<int, EntityState>();
        
        // Find all combat entities
        var entities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .Where(e => e.EntityType == EntityType.Player || e.EntityType == EntityType.Pet)
            .ToArray();

        foreach (var entity in entities)
        {
            var state = CaptureEntityState(entity);
            if (state != null)
            {
                states[entity.ObjectId] = state;
            }
        }

        return states;
    }

    /// <summary>
    /// Restore all entities to their captured states
    /// </summary>
    public static void RestoreAllEntityStates(Dictionary<int, EntityState> states)
    {
        if (states == null) return;

        foreach (var kvp in states)
        {
            var entity = FindEntityById(kvp.Key);
            if (entity != null)
            {
                RestoreEntityState(entity, kvp.Value);
            }
        }
    }

    /// <summary>
    /// Find a NetworkEntity by its ObjectId
    /// </summary>
    private static NetworkEntity FindEntityById(int objectId)
    {
        return Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .FirstOrDefault(e => e.ObjectId == objectId);
    }

    /// <summary>
    /// Create a deep copy of entity states for comparison
    /// </summary>
    public static Dictionary<int, EntityState> CloneEntityStates(Dictionary<int, EntityState> original)
    {
        var clone = new Dictionary<int, EntityState>();
        
        foreach (var kvp in original)
        {
            var originalState = kvp.Value;
            var clonedState = new EntityState
            {
                entityId = originalState.entityId,
                entityName = originalState.entityName,
                maxHealth = originalState.maxHealth,
                currentHealth = originalState.currentHealth,
                maxEnergy = originalState.maxEnergy,
                currentEnergy = originalState.currentEnergy,
                currency = originalState.currency
            };

            // Clone effects
            foreach (var effect in originalState.activeEffects)
            {
                clonedState.activeEffects.Add(new StatusEffectSnapshot(
                    effect.effectName,
                    effect.potency,
                    effect.remainingDuration,
                    effect.sourceEntityId
                ));
            }
            
            // Clone EntityTracker state
            clonedState.currentStance = originalState.currentStance;
            clonedState.comboCount = originalState.comboCount;
            clonedState.isStunned = originalState.isStunned;
            clonedState.isInLimitBreak = originalState.isInLimitBreak;

            clone[kvp.Key] = clonedState;
        }

        return clone;
    }
} 