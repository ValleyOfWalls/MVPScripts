using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages point budget calculations for randomized card generation.
/// Uses configuration objects to determine how points are allocated across effects and costs.
/// </summary>
public class CardPointBudgetManager
{
    private RandomCardConfig config;
    
    public CardPointBudgetManager(RandomCardConfig config)
    {
        this.config = config;
    }
    
    /// <summary>
    /// Calculate the complete budget breakdown for a card
    /// </summary>
    public CardBudgetBreakdown CalculateBudget(CardRarity rarity)
    {
        if (config?.pointBudgetConfig == null)
        {
            Debug.LogError("[BUDGET] ❌ CardPointBudgetManager: Missing point budget configuration! Using fallback values.");
            return CreateFallbackBudget(rarity);
        }
        
        int totalBudget = config.pointBudgetConfig.GetPointBudget(rarity);
        int energyCost = config.pointBudgetConfig.CalculateEnergyCost(totalBudget);
        int remainingBudget = config.pointBudgetConfig.GetRemainingBudget(totalBudget, energyCost);
        
        Debug.Log($"[BUDGET] ✅ Using configured values for {rarity} card - " +
                  $"Total: {totalBudget}, Energy: {energyCost}, Effects: {remainingBudget}");
        
        return new CardBudgetBreakdown
        {
            rarity = rarity,
            totalBudget = totalBudget,
            energyCost = energyCost,
            effectBudget = remainingBudget,
            upgradeBudgetReduction = 0f // Will be calculated later when upgrade is assigned
        };
    }
    
    /// <summary>
    /// Calculate the point cost for a specific effect
    /// </summary>
    public float CalculateEffectCost(CardEffectType effectType, float effectValue, ConditionalType conditionalType = ConditionalType.None)
    {
        if (config?.effectCostConfig == null)
        {
            Debug.LogWarning("CardPointBudgetManager: Missing effect cost configuration!");
            return effectValue; // Fallback 1:1 ratio
        }
        
        float baseCost = config.effectCostConfig.GetEffectPointCost(effectType, effectValue);
        
        // Apply conditional multiplier if this is a conditional effect
        if (conditionalType != ConditionalType.None)
        {
            float conditionalMultiplier = config.effectCostConfig.GetConditionalMultiplier(conditionalType);
            baseCost *= conditionalMultiplier;
        }
        
        return baseCost;
    }
    
    /// <summary>
    /// Calculate the point cost reduction for an upgrade condition
    /// </summary>
    public float CalculateUpgradeCost(UpgradeConditionType conditionType, int requiredValue = 1)
    {
        if (config?.upgradeCostConfig == null)
        {
            Debug.LogWarning("CardPointBudgetManager: Missing upgrade cost configuration!");
            return 1f; // Minimal fallback cost
        }
        
        return config.upgradeCostConfig.GetUpgradeConditionCost(conditionType, requiredValue);
    }
    
    /// <summary>
    /// Apply upgrade condition cost to a budget breakdown
    /// </summary>
    public CardBudgetBreakdown ApplyUpgradeCost(CardBudgetBreakdown budget, UpgradeConditionType conditionType, int requiredValue = 1)
    {
        float upgradeCost = CalculateUpgradeCost(conditionType, requiredValue);
        
        budget.upgradeBudgetReduction = upgradeCost;
        budget.effectBudget = Mathf.Max(1f, budget.effectBudget - upgradeCost);
        
        return budget;
    }
    
    /// <summary>
    /// Check if a list of effects fits within the budget
    /// </summary>
    public bool CanAffordEffects(CardBudgetBreakdown budget, List<ProposedCardEffect> effects)
    {
        float totalCost = 0f;
        
        foreach (var effect in effects)
        {
            totalCost += CalculateEffectCost(effect.effectType, effect.amount, effect.conditionalType);
        }
        
        return totalCost <= budget.effectBudget;
    }
    
    /// <summary>
    /// Generate a list of effects that fit within the budget
    /// </summary>
    public List<ProposedCardEffect> GenerateAffordableEffects(CardBudgetBreakdown budget)
    {
        var effects = new List<ProposedCardEffect>();
        float remainingBudget = budget.effectBudget;
        
        int numEffects = UnityEngine.Random.Range(config.minEffectsPerCard, config.maxEffectsPerCard + 1);
        
        for (int i = 0; i < numEffects && remainingBudget > 0; i++)
        {
            var effect = GenerateRandomEffect(remainingBudget, budget.rarity);
            if (effect != null)
            {
                float cost = CalculateEffectCost(effect.effectType, effect.amount, effect.conditionalType);
                if (cost <= remainingBudget)
                {
                    effects.Add(effect);
                    remainingBudget -= cost;
                }
            }
        }
        
        return effects;
    }
    
    /// <summary>
    /// Generate a random effect that fits within the budget
    /// </summary>
    private ProposedCardEffect GenerateRandomEffect(float budgetLimit, CardRarity rarity)
    {
        // Get available effect types
        var effectTypes = System.Enum.GetValues(typeof(CardEffectType)).Cast<CardEffectType>().ToList();
        
        // Try multiple times to find a suitable effect
        for (int attempts = 0; attempts < 10; attempts++)
        {
            var effectType = effectTypes[UnityEngine.Random.Range(0, effectTypes.Count)];
            
            // Calculate appropriate amount based on budget and effect type
            float maxAffordableAmount = GetMaxAffordableAmount(effectType, budgetLimit);
            if (maxAffordableAmount < 1f) continue;
            
            int amount = Mathf.RoundToInt(UnityEngine.Random.Range(1f, maxAffordableAmount + 1f));
            
            // Randomly decide if this should be a conditional effect
            bool makeConditional = UnityEngine.Random.value < 0.3f; // 30% chance
            ConditionalType conditionalType = ConditionalType.None;
            
            if (makeConditional)
            {
                // Use a weighted selection of conditional types, favoring simpler ones
                conditionalType = GetRandomConditionalType();
            }
            
            // Generate elemental type if this is an elemental effect
            ElementalType elementalType = ElementalType.None;
            if (effectType == CardEffectType.ApplyElementalStatus || effectType == CardEffectType.Damage)
            {
                if (UnityEngine.Random.value < 0.4f) // 40% chance for elemental typing
                {
                    var elements = new[] { ElementalType.Fire, ElementalType.Ice, ElementalType.Lightning, ElementalType.Void };
                    elementalType = elements[UnityEngine.Random.Range(0, elements.Length)];
                }
            }
            
            // Generate duration for duration-based effects
            int duration = 0;
            if (effectType == CardEffectType.ApplyElementalStatus || 
                effectType == CardEffectType.ApplyStun ||
                effectType == CardEffectType.EnterStance ||
                effectType == CardEffectType.ApplyLimitBreak)
            {
                duration = UnityEngine.Random.Range(1, 4); // 1-3 turns
            }

            return new ProposedCardEffect
            {
                effectType = effectType,
                amount = amount,
                conditionalType = conditionalType,
                targetType = GetRandomTargetForEffect(effectType),
                elementalType = elementalType,
                duration = duration
            };
        }
        
        return null; // Couldn't find suitable effect
    }
    
    /// <summary>
    /// Calculate maximum affordable amount for an effect type within budget
    /// </summary>
    private float GetMaxAffordableAmount(CardEffectType effectType, float budget)
    {
        if (config?.effectCostConfig == null) return budget;
        
        // Test with amount = 1 to get cost per unit
        float costPerUnit = config.effectCostConfig.GetEffectPointCost(effectType, 1f);
        if (costPerUnit <= 0f) return budget; // Avoid division by zero
        
        return budget / costPerUnit;
    }
    
    /// <summary>
    /// Get a random conditional type with weighted selection
    /// </summary>
    private ConditionalType GetRandomConditionalType()
    {
        // Weighted selection favoring simpler, more commonly useful conditions
        var weightedConditions = new[]
        {
            // High probability - simple, frequently useful conditions
            (ConditionalType.IfTargetHealthBelow, 15),
            (ConditionalType.IfSourceHealthBelow, 15),
            (ConditionalType.IfCardsInHand, 12),
            (ConditionalType.IfComboCount, 12),
            (ConditionalType.IfEnergyRemaining, 10),
            
            // Medium probability - moderately complex conditions  
            (ConditionalType.IfTargetHealthAbove, 8),
            (ConditionalType.IfSourceHealthAbove, 8),
            (ConditionalType.IfCardsInDeck, 6),
            (ConditionalType.IfCardsInDiscard, 6),
            (ConditionalType.IfZeroCostCardsThisTurn, 5),
            (ConditionalType.IfTimesPlayedThisFight, 5),
            
            // Lower probability - complex or situational conditions
            (ConditionalType.IfDamageTakenThisFight, 3),
            (ConditionalType.IfDamageTakenLastRound, 3),
            (ConditionalType.IfHealingReceivedThisFight, 3),
            (ConditionalType.IfHealingReceivedLastRound, 3),
            (ConditionalType.IfZeroCostCardsThisFight, 2),
            (ConditionalType.IfPerfectionStreak, 2),
            (ConditionalType.IfInStance, 2),
            (ConditionalType.IfLastCardType, 2)
        };
        
        int totalWeight = weightedConditions.Sum(c => c.Item2);
        int randomValue = UnityEngine.Random.Range(0, totalWeight);
        
        int currentWeight = 0;
        foreach (var (condition, weight) in weightedConditions)
        {
            currentWeight += weight;
            if (randomValue < currentWeight)
            {
                return condition;
            }
        }
        
        // Fallback
        return ConditionalType.IfTargetHealthBelow;
    }

    /// <summary>
    /// Get a random appropriate target for an effect type
    /// </summary>
    private CardTargetType GetRandomTargetForEffect(CardEffectType effectType)
    {
        return effectType switch
        {
            // Damage effects - primarily target enemies
            CardEffectType.Damage => UnityEngine.Random.value < 0.8f ? CardTargetType.Opponent : CardTargetType.Random,
            
            // Healing effects - balanced between self and ally pet
            CardEffectType.Heal => UnityEngine.Random.value < 0.5f ? CardTargetType.Self : CardTargetType.Ally,
            
            // Utility effects - card draw stays self-targeted
            CardEffectType.DrawCard => CardTargetType.Self,
            CardEffectType.DiscardRandomCards => CardTargetType.Self,
            
            // Energy effects - REMOVED per requirements (energy restoration no longer fits game flow)
            
            // Defensive buffs - heavily favor ally pet targeting for tactical play
            CardEffectType.ApplyShield => UnityEngine.Random.value < 0.3f ? CardTargetType.Self : CardTargetType.Ally,
            CardEffectType.ApplyThorns => UnityEngine.Random.value < 0.3f ? CardTargetType.Self : CardTargetType.Ally,
            
            // Positive status effects - balanced between self and ally
            CardEffectType.ApplyStrength => UnityEngine.Random.value < 0.4f ? CardTargetType.Self : CardTargetType.Ally,
            CardEffectType.ApplySalve => UnityEngine.Random.value < 0.3f ? CardTargetType.Self : CardTargetType.Ally,
            CardEffectType.RaiseCriticalChance => UnityEngine.Random.value < 0.4f ? CardTargetType.Self : CardTargetType.Ally,
            CardEffectType.ApplyLimitBreak => UnityEngine.Random.value < 0.6f ? CardTargetType.Self : CardTargetType.Ally,
            
            // Elemental and special effects - can target allies for buffs
            CardEffectType.ApplyElementalStatus => UnityEngine.Random.value < 0.4f ? CardTargetType.Self : CardTargetType.Ally,
            
            // Stance effects - generally self-targeted but can buff allies
            CardEffectType.EnterStance => UnityEngine.Random.value < 0.8f ? CardTargetType.Self : CardTargetType.Ally,
            CardEffectType.ExitStance => CardTargetType.Self,
            
            // Negative status effects - target enemies
            CardEffectType.ApplyWeak => UnityEngine.Random.value < 0.7f ? CardTargetType.Opponent : CardTargetType.Random,
            CardEffectType.ApplyBreak => UnityEngine.Random.value < 0.7f ? CardTargetType.Opponent : CardTargetType.Random,
            CardEffectType.ApplyBurn => UnityEngine.Random.value < 0.7f ? CardTargetType.Opponent : CardTargetType.Random,
            CardEffectType.ApplyStun => UnityEngine.Random.value < 0.8f ? CardTargetType.Opponent : CardTargetType.Random,
            CardEffectType.ApplyCurse => UnityEngine.Random.value < 0.8f ? CardTargetType.Opponent : CardTargetType.Random,
            
            // Default fallback
            _ => UnityEngine.Random.value < 0.5f ? CardTargetType.Self : CardTargetType.Opponent
        };
    }
    
    /// <summary>
    /// Create a fallback budget when configuration is missing
    /// </summary>
    private CardBudgetBreakdown CreateFallbackBudget(CardRarity rarity)
    {
        int budget = rarity switch
        {
            CardRarity.Common => 20,
            CardRarity.Uncommon => 30,
            CardRarity.Rare => 45,
            _ => 20
        };
        
        // Calculate proper energy cost for fallback
        int energyCost = rarity switch
        {
            CardRarity.Common => UnityEngine.Random.Range(12, 16),
            CardRarity.Uncommon => UnityEngine.Random.Range(18, 24),
            CardRarity.Rare => UnityEngine.Random.Range(28, 35),
            _ => UnityEngine.Random.Range(12, 16)
        };
        
        Debug.LogWarning($"[BUDGET] ⚠️ Using FALLBACK values for {rarity} card - " +
                        $"Total: {budget}, Energy: {energyCost}, Effects: {budget * 0.6f:F1}");
        
        return new CardBudgetBreakdown
        {
            rarity = rarity,
            totalBudget = budget,
            energyCost = energyCost,
            effectBudget = budget * 0.6f, // 60% of budget for effects
            upgradeBudgetReduction = 0f
        };
    }
}

/// <summary>
/// Represents the point budget breakdown for a card
/// </summary>
[System.Serializable]
public class CardBudgetBreakdown
{
    public CardRarity rarity;
    public int totalBudget;
    public int energyCost;
    public float effectBudget;
    public float upgradeBudgetReduction;
    
    public float FinalEffectBudget => Mathf.Max(1f, effectBudget - upgradeBudgetReduction);
}

/// <summary>
/// Represents a proposed effect for a card during generation
/// </summary>
[System.Serializable]
public class ProposedCardEffect
{
    public CardEffectType effectType;
    public int amount;
    public ConditionalType conditionalType;
    public CardTargetType targetType;
    public int duration;
    public ElementalType elementalType;
} 