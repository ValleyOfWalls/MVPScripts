using UnityEngine;

/// <summary>
/// CombatCalculator centralizes all combat-related calculations to ensure consistency
/// across different parts of the game. This includes damage, block, status effects,
/// and other combat math operations.
/// </summary>
public class CombatCalculator
{
    private GameManager gameManager;
    
    // Constants for combat calculations
    public const int BASE_CRIT_CHANCE = 5; // Base 5% crit chance
    public const int CRIT_DAMAGE_MULTIPLIER = 2; // Crits do double damage
    public const float WEAK_DAMAGE_REDUCTION = 0.5f; // 50% damage reduction
    public const float BREAK_DAMAGE_INCREASE = 0.5f; // 50% more damage taken
    
    public CombatCalculator(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    #region Damage Calculations
    
    /// <summary>
    /// Calculate damage after considering block, status effects, and critical hits.
    /// </summary>
    /// <param name="rawDamage">Base damage amount</param>
    /// <param name="attackerStrength">Strength value of the attacker</param>
    /// <param name="targetBlock">Block value of the target</param>
    /// <param name="isAttackerWeak">Whether the attacker has Weak status</param>
    /// <param name="isTargetBroken">Whether the target has Break status</param>
    /// <param name="attackerCritChance">Critical hit chance of the attacker</param>
    /// <returns>Tuple containing final damage, block consumed, and whether a crit occurred</returns>
    public DamageResult CalculateDamage(
        int rawDamage, 
        int attackerStrength, 
        int targetBlock,
        bool isAttackerWeak,
        bool isTargetBroken,
        int attackerCritChance)
    {
        DamageResult result = new DamageResult();
        
        // Start with raw damage
        int baseDamage = rawDamage;
        
        // Add strength bonus
        int damageAfterStrength = baseDamage + attackerStrength;
        
        // Apply weakness if attacker is weak
        float weakMultiplier = isAttackerWeak ? (1f - WEAK_DAMAGE_REDUCTION) : 1f;
        int damageAfterWeak = Mathf.FloorToInt(damageAfterStrength * weakMultiplier);
        
        // Check for critical hit
        result.IsCritical = Random.Range(0, 100) < attackerCritChance;
        float critMultiplier = result.IsCritical ? CRIT_DAMAGE_MULTIPLIER : 1f;
        int damageAfterCrit = Mathf.FloorToInt(damageAfterWeak * critMultiplier);
        
        // Store damage before block/break
        result.DamageBeforeBlock = damageAfterCrit;
        
        // Calculate block interaction
        result.BlockConsumed = Mathf.Min(damageAfterCrit, targetBlock);
        result.DamageAfterBlock = damageAfterCrit - result.BlockConsumed;
        
        // DEBUG: Log pre-break damage values
        Debug.Log($"DEBUG PRE-BREAK: Raw={rawDamage}, Strength={attackerStrength}, isWeak={isAttackerWeak}, isBroken={isTargetBroken}, Block={targetBlock}, DamageAfterBlock={result.DamageAfterBlock}");
        
        // Apply break status if target is broken and damage got through block
        if (isTargetBroken && result.DamageAfterBlock > 0)
        {
            // COMPLETELY REDONE: Explicitly calculate 1.5x damage instead of using the constant
            // to ensure we don't accidentally get 2x or any unexpected multiplier
            int originalDamage = result.DamageAfterBlock;
            int breakBonus = Mathf.FloorToInt(originalDamage * 0.5f); // Exactly 50% more
            result.DamageAfterBlock = originalDamage + breakBonus;
            
            Debug.Log($"DEBUG BREAK APPLIED: Original damage={originalDamage}, Break bonus={breakBonus}, Final damage={result.DamageAfterBlock}");
        }
        
        // Ensure damage is not negative
        result.DamageAfterBlock = Mathf.Max(0, result.DamageAfterBlock);
        
        return result;
    }
    
    /// <summary>
    /// Calculate scaled damage based on various card scaling effects
    /// </summary>
    public int CalculateCardDamageWithScaling(
        CardData cardData,
        int currentTurn,
        int previousPlaysThisCombat,
        int totalCopies,
        bool isBelowHealthThreshold,
        int strengthBonus)
    {
        // Base damage
        int baseDamage = cardData.damage;
        
        // Turn scaling
        int turnBonusDamage = 0;
        if (cardData.damageScalingPerTurn > 0)
        {
            turnBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerTurn * currentTurn);
        }
        
        // Play count scaling
        int playBonusDamage = 0;
        if (cardData.damageScalingPerPlay > 0)
        {
            // Only apply scaling if this is not the first play (previousPlaysThisCombat > 0)
            if (previousPlaysThisCombat > 0)
            {
                playBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerPlay * previousPlaysThisCombat);
            }
        }
        
        // Copy scaling
        int copyBonusDamage = 0;
        if (cardData.damageScalingPerCopy > 0)
        {
            copyBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerCopy * totalCopies);
        }
        
        // Sum base and scaling bonuses
        int totalDamageBeforeEnhancements = baseDamage + turnBonusDamage + playBonusDamage + copyBonusDamage;
        
        // Apply low health multiplier if applicable
        float damageMultiplier = 1.0f;
        if (isBelowHealthThreshold && cardData.damageMultiplierBelowThreshold > 1.0f)
        {
            damageMultiplier *= cardData.damageMultiplierBelowThreshold;
        }
        
        // Calculate damage after multiplier
        int damageAfterMultipliers = Mathf.FloorToInt(totalDamageBeforeEnhancements * damageMultiplier);
        
        // Apply strength bonus
        int finalDamage = damageAfterMultipliers + strengthBonus;
        
        // Ensure damage is not negative
        return Mathf.Max(0, finalDamage);
    }
    
    #endregion
    
    #region Block Calculations
    
    /// <summary>
    /// Calculate block amount with all scaling factors
    /// </summary>
    public int CalculateCardBlockWithScaling(
        CardData cardData,
        int currentTurn,
        int previousPlaysThisCombat,
        int totalCopies,
        bool isBelowHealthThreshold)
    {
        // Base block
        int baseBlock = cardData.block;
        
        // Turn scaling
        int turnBonusBlock = 0;
        if (cardData.blockScalingPerTurn > 0)
        {
            turnBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerTurn * currentTurn);
        }
        
        // Play count scaling
        int playBonusBlock = 0;
        if (cardData.blockScalingPerPlay > 0)
        {
            // Only apply scaling if this is not the first play (previousPlaysThisCombat > 0)
            if (previousPlaysThisCombat > 0)
            {
                playBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerPlay * previousPlaysThisCombat);
            }
        }
        
        // Copy scaling
        int copyBonusBlock = 0;
        if (cardData.blockScalingPerCopy > 0)
        {
            copyBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerCopy * totalCopies);
        }
        
        // Sum base and scaling bonuses
        int totalBlockBeforeEnhancements = baseBlock + turnBonusBlock + playBonusBlock + copyBonusBlock;
        
        // Apply low health multiplier if applicable
        float blockMultiplier = 1.0f;
        if (isBelowHealthThreshold && cardData.blockMultiplierBelowThreshold > 1.0f)
        {
            blockMultiplier *= cardData.blockMultiplierBelowThreshold;
        }
        
        // Calculate block after multiplier
        int finalBlock = Mathf.FloorToInt(totalBlockBeforeEnhancements * blockMultiplier);
        
        // Ensure block is not negative
        return Mathf.Max(0, finalBlock);
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Checks if an entity's health is below the threshold percentage specified in the card
    /// </summary>
    public bool IsEntityBelowHealthThreshold(CardData cardData, int currentHealth, int maxHealth)
    {
        if (cardData.healthThresholdPercent <= 0 || maxHealth <= 0)
            return false;
        
        float currentHealthPercent = ((float)currentHealth / maxHealth) * 100f;
        return currentHealthPercent < cardData.healthThresholdPercent;
    }
    
    /// <summary>
    /// Calculate effective crit chance for an entity based on base chance and temporary buffs
    /// </summary>
    public int CalculateEffectiveCritChance(int baseCritChance, int temporaryBonusCritChance)
    {
        return baseCritChance + temporaryBonusCritChance;
    }
    
    #endregion
    
    /// <summary>
    /// Structure to hold damage calculation results
    /// </summary>
    public struct DamageResult
    {
        public int DamageAfterBlock;
        public int BlockConsumed;
        public bool IsCritical;
        public int DamageBeforeBlock;
    }
} 