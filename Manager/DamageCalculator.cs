using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;

/// <summary>
/// Calculates damage for card effects, taking into account statuses and modifiers.
/// Attach to: GameManager GameObject.
/// </summary>
public class DamageCalculator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    
    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
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
        
        Debug.Log($"DamageCalculator: Calculating damage for card {cardData.CardName} from {source.EntityName.Value} to {target.EntityName.Value}");
        
        // Start with base damage from the card
        float baseDamage = cardData.Amount;
        Debug.Log($"DamageCalculator: Base damage is {baseDamage}");
        
        // Apply source modifiers (buffs/debuffs affecting outgoing damage)
        float modifiedDamage = ApplySourceModifiers(source, baseDamage);
        Debug.Log($"DamageCalculator: After source modifiers: {modifiedDamage}");
        
        // Apply target modifiers (buffs/debuffs affecting incoming damage)
        modifiedDamage = ApplyTargetModifiers(target, modifiedDamage);
        Debug.Log($"DamageCalculator: After target modifiers: {modifiedDamage}");
        
        // Apply critical hit chance if applicable
        modifiedDamage = ApplyCriticalHitChance(source, target, modifiedDamage);
        Debug.Log($"DamageCalculator: After critical hit check: {modifiedDamage}");
        
        // Round to nearest integer for final damage value
        int finalDamage = Mathf.RoundToInt(modifiedDamage);
        Debug.Log($"DamageCalculator: Final damage: {finalDamage}");
        
        return finalDamage;
    }
    
    private float ApplySourceModifiers(NetworkEntity source, float damage)
    {
        float modifiedDamage = damage;
        
        // Get source entity's effect handler
        EffectHandler sourceEffects = source.GetComponent<EffectHandler>();
        if (sourceEffects == null) return modifiedDamage;
        
        // Check for effects that modify outgoing damage
        if (sourceEffects.HasEffect("Weak"))
        {
            modifiedDamage *= gameManager.WeakStatusModifier.Value;
            Debug.Log($"DamageCalculator: Source has Weak status, damage reduced to {modifiedDamage}");
        }
        
        if (sourceEffects.HasEffect("Strength"))
        {
            int strengthValue = sourceEffects.GetEffectPotency("Strength");
            modifiedDamage += strengthValue;
            Debug.Log($"DamageCalculator: Source has Strength {strengthValue}, damage increased to {modifiedDamage}");
        }
        
        // Add more effect checks as needed
        
        return modifiedDamage;
    }
    
    private float ApplyTargetModifiers(NetworkEntity target, float damage)
    {
        float modifiedDamage = damage;
        
        // Get target entity's effect handler
        EffectHandler targetEffects = target.GetComponent<EffectHandler>();
        if (targetEffects == null) return modifiedDamage;
        
        // Check for effects that modify incoming damage
        if (targetEffects.HasEffect("Break"))
        {
            modifiedDamage *= gameManager.BreakStatusModifier.Value;
            Debug.Log($"DamageCalculator: Target has Break status, damage increased to {modifiedDamage}");
        }
        
        if (targetEffects.HasEffect("Armor"))
        {
            int armorValue = targetEffects.GetEffectPotency("Armor");
            modifiedDamage = Mathf.Max(1, modifiedDamage - armorValue); // Ensure at least 1 damage
            Debug.Log($"DamageCalculator: Target has Armor {armorValue}, damage reduced to {modifiedDamage}");
        }
        
        // Add more effect checks as needed
        
        return modifiedDamage;
    }
    
    private float ApplyCriticalHitChance(NetworkEntity source, NetworkEntity target, float damage)
    {
        // Skip critical hit calculation if crits are disabled in GameManager
        if (!gameManager.CriticalHitsEnabled.Value)
        {
            return damage;
        }
        
        // Get source effect handler to check for increased crit chance
        EffectHandler sourceEffects = source.GetComponent<EffectHandler>();
        
        // Start with base crit chance from GameManager
        float critChance = gameManager.BaseCriticalChance.Value;
        
        // Increase crit chance based on effects if applicable
        if (sourceEffects != null && sourceEffects.HasEffect("CriticalUp"))
        {
            int critBonus = sourceEffects.GetEffectPotency("CriticalUp");
            critChance += (critBonus / 100f); // Assuming potency is in percentage points
        }
        
        // Check for critical hit
        if (Random.value < critChance)
        {
            float critDamage = damage * gameManager.CriticalHitModifier.Value;
            Debug.Log($"DamageCalculator: Critical hit! Damage increased from {damage} to {critDamage}");
            
            // TODO: Consider notifying UI for critical hit display effect
            
            return critDamage;
        }
        
        return damage;
    }
} 