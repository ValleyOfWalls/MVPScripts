using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;

/// <summary>
/// Calculates damage for card effects, taking into account statuses and modifiers.
/// Attach to: Any GameObject that needs damage calculation functionality.
/// </summary>
public class DamageCalculator : MonoBehaviour
{
    // No references needed - accesses OnlineGameManager directly
    
    private void Awake()
    {
        // No initialization needed
    }
    
    /// <summary>
    /// Calculates the final damage amount based on card data and entity status effects
    /// </summary>
    /// <param name="source">The entity playing the card</param>
    /// <param name="target">The entity being targeted</param>
    /// <param name="cardData">The card being played</param>
    /// <returns>Final calculated damage amount</returns>
    public int CalculateDamage(NetworkEntity source, NetworkEntity target, CardData cardData)
    {
        if (source == null || target == null || cardData == null)
        {
            Debug.LogError("DamageCalculator: Cannot calculate damage with null source, target, or card data");
            return 0;
        }
        
        /* Debug.Log($"DamageCalculator: Calculating damage for card {cardData.CardName} from {source.EntityName.Value} to {target.EntityName.Value}"); */
        
        // Calculate base damage from card's damage effects
        int baseDamage = 0;
        if (cardData.HasEffects)
        {
            foreach (var effect in cardData.Effects)
            {
                if (effect.effectType == CardEffectType.Damage)
                {
                    baseDamage += effect.amount;
                }
            }
        }
        
        if (baseDamage == 0)
        {
            Debug.LogWarning($"DamageCalculator: Card {cardData.CardName} has no damage effects");
            return 0;
        }

        // Use the overloaded method to apply modifiers
        return CalculateDamage(source, target, baseDamage);
    }

    /// <summary>
    /// Calculates the final damage amount for a pre-calculated base damage value
    /// Use this for individual effects that have already been processed (scaled, conditional, etc.)
    /// </summary>
    /// <param name="source">The entity dealing damage</param>
    /// <param name="target">The entity being targeted</param>
    /// <param name="baseDamage">The pre-calculated base damage amount</param>
    /// <returns>Final calculated damage amount with all modifiers applied</returns>
    public int CalculateDamage(NetworkEntity source, NetworkEntity target, int baseDamage)
    {
        if (source == null || target == null)
        {
            Debug.LogError("DamageCalculator: Cannot calculate damage with null source or target");
            return 0;
        }

        if (baseDamage <= 0)
        {
            return 0; // No damage to calculate
        }

        float modifiedDamage = baseDamage;
        
        /* Debug.Log($"DamageCalculator: Calculating damage from {source.EntityName.Value} to {target.EntityName.Value} - Base: {baseDamage}"); */
        
        // Apply source modifiers (buffs/debuffs affecting outgoing damage)
        modifiedDamage = ApplySourceModifiers(source, modifiedDamage);
        /* Debug.Log($"DamageCalculator: After source modifiers: {modifiedDamage}"); */
        
        // Apply target modifiers (buffs/debuffs affecting incoming damage)
        modifiedDamage = ApplyTargetModifiers(target, modifiedDamage);
        /* Debug.Log($"DamageCalculator: After target modifiers: {modifiedDamage}"); */
        
        // Apply critical hit chance if applicable
        modifiedDamage = ApplyCriticalHitChance(source, target, modifiedDamage);
        /* Debug.Log($"DamageCalculator: After critical hit check: {modifiedDamage}"); */
        
        // Round to nearest integer for final damage value
        int finalDamage = Mathf.RoundToInt(modifiedDamage);
        /* Debug.Log($"DamageCalculator: Final damage: {finalDamage}"); */
        
        return finalDamage;
    }
    
    private float ApplySourceModifiers(NetworkEntity source, float damage)
    {
        float modifiedDamage = damage;
        
        // Get source entity's effect handler
        EffectHandler sourceEffects = source.GetComponent<EffectHandler>();
        if (sourceEffects == null) return modifiedDamage;
        
        // Apply damage modification from curse effects (negative strength)
        int damageModification = sourceEffects.GetDamageModification();
        modifiedDamage += damageModification;
        if (damageModification != 0)
        {
            Debug.Log($"DamageCalculator: Source has curse effects, damage modified by {damageModification} to {modifiedDamage}");
        }
        
        // Check for Strength effect (positive damage bonus) - use EntityTracker for consistency
        // NOTE: Strength must be applied BEFORE Weak multiplier to be affected by it
        EntityTracker sourceTracker = source.GetComponent<EntityTracker>();
        if (sourceTracker != null)
        {
            int strengthValue = sourceTracker.StrengthStacks;
            if (strengthValue > 0)
            {
                modifiedDamage += strengthValue;
                /* Debug.Log($"DamageCalculator: Source has Strength {strengthValue}, damage increased to {modifiedDamage}"); */
            }
        }
        
        // Ensure damage doesn't go below 0 after curse/strength effects
        if (modifiedDamage < 0)
        {
            Debug.Log($"DamageCalculator: Damage clamped from {modifiedDamage} to 0 (curse effects too strong)");
            modifiedDamage = 0;
        }
        
        // Apply damage dealt multiplier (from effects like Weak)
        // NOTE: This must be applied AFTER flat bonuses/penalties (Strength/Curse)
        float damageMultiplier = sourceEffects.GetDamageDealtMultiplier();
        if (damageMultiplier != 1.0f)
        {
            modifiedDamage = modifiedDamage * damageMultiplier;
            /* Debug.Log($"DamageCalculator: Source damage multiplier {damageMultiplier}, damage is now {modifiedDamage}"); */
        }
        
        return modifiedDamage;
    }
    
    private float ApplyTargetModifiers(NetworkEntity target, float damage)
    {
        float modifiedDamage = damage;
        
        // Get target entity's effect handler
        EffectHandler targetEffects = target.GetComponent<EffectHandler>();
        if (targetEffects == null) return modifiedDamage;
        
        // Apply damage taken multiplier (from effects like Break)
        float damageTakenMultiplier = targetEffects.GetDamageTakenMultiplier();
        if (damageTakenMultiplier != 1.0f)
        {
            modifiedDamage = modifiedDamage * damageTakenMultiplier;
            Debug.Log($"DamageCalculator: Target damage taken multiplier {damageTakenMultiplier}, damage is now {modifiedDamage}");
        }
        
        // Check for Armor effect
        if (targetEffects.HasEffect("Armor"))
        {
            int armorValue = targetEffects.GetEffectPotency("Armor");
            modifiedDamage = Mathf.Max(1, modifiedDamage - armorValue); // Ensure at least 1 damage
            /* Debug.Log($"DamageCalculator: Target has Armor {armorValue}, damage reduced to {modifiedDamage}"); */
        }
        
        return modifiedDamage;
    }
    
    private float ApplyCriticalHitChance(NetworkEntity source, NetworkEntity target, float damage)
    {
        // Skip critical hit calculation if crits are disabled or OnlineGameManager not available
        if (OnlineGameManager.Instance == null || !OnlineGameManager.Instance.CriticalHitsEnabled.Value)
        {
            return damage;
        }
        
        // Get source effect handler to check for increased crit chance
        EffectHandler sourceEffects = source.GetComponent<EffectHandler>();
        
        // Start with base crit chance from OnlineGameManager
        float critChance = OnlineGameManager.Instance.BaseCriticalChance.Value;
        
        // Increase crit chance based on effects if applicable
        if (sourceEffects != null && sourceEffects.HasEffect("CriticalUp"))
        {
            int critBonus = sourceEffects.GetEffectPotency("CriticalUp");
            critChance += (critBonus / 100f); // Assuming potency is in percentage points
        }
        
        // Check for critical hit
        if (Random.value < critChance)
        {
            float critDamage = damage * OnlineGameManager.Instance.CriticalHitModifier.Value;
            /* Debug.Log($"DamageCalculator: Critical hit! Damage increased from {damage} to {critDamage}"); */
            
            // TODO: Consider notifying UI for critical hit display effect
            
            return critDamage;
        }
        
        return damage;
    }
} 