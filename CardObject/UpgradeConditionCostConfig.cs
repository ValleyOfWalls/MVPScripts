using UnityEngine;
using System;

/// <summary>
/// Configuration for upgrade condition point costs.
/// Easier upgrade conditions reduce the point budget, harder ones reduce it less.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeConditionCostConfig", menuName = "Card System/Upgrade Condition Cost Config")]
public class UpgradeConditionCostConfig : ScriptableObject
{
    [Header("Upgrade Condition Cost Modifiers")]
    [SerializeField, Tooltip("Point reduction for very easy upgrade conditions")]
    [Range(0f, 10f)]
    public float veryEasyConditionCost = 0.5f;
    
    [SerializeField, Tooltip("Point reduction for easy upgrade conditions")]
    [Range(0f, 10f)]
    public float easyConditionCost = 1f;
    
    [SerializeField, Tooltip("Point reduction for medium upgrade conditions")]
    [Range(0f, 10f)]
    public float mediumConditionCost = 2f;
    
    [SerializeField, Tooltip("Point reduction for hard upgrade conditions")]
    [Range(0f, 10f)]
    public float hardConditionCost = 3f;
    
    [SerializeField, Tooltip("Point reduction for very hard upgrade conditions")]
    [Range(0f, 10f)]
    public float veryHardConditionCost = 4f;
    
    [Header("Per-Fight vs Lifetime Modifiers")]
    [SerializeField, Tooltip("Multiplier for per-fight conditions (easier than lifetime)")]
    [Range(0.1f, 1f)]
    public float perFightMultiplier = 0.7f;
    
    [SerializeField, Tooltip("Multiplier for lifetime conditions (harder)")]
    [Range(1f, 2f)]
    public float lifetimeMultiplier = 1.3f;
    
    /// <summary>
    /// Get the point cost reduction for a specific upgrade condition
    /// </summary>
    public float GetUpgradeConditionCost(UpgradeConditionType conditionType, int requiredValue = 1)
    {
        float baseCost = GetBaseConditionCost(conditionType);
        float lifetimeMod = IsLifetimeCondition(conditionType) ? lifetimeMultiplier : perFightMultiplier;
        
        // Scale cost by required value for conditions that use thresholds
        float valueMod = GetValueModifier(conditionType, requiredValue);
        
        return baseCost * lifetimeMod * valueMod;
    }
    
    /// <summary>
    /// Get base cost category for different condition types
    /// </summary>
    private float GetBaseConditionCost(UpgradeConditionType conditionType)
    {
        return conditionType switch
        {
            // Very Easy - happen naturally
            UpgradeConditionType.TimesPlayedThisFight => veryEasyConditionCost,
            UpgradeConditionType.CopiesInDeck => veryEasyConditionCost,
            UpgradeConditionType.CopiesInHand => veryEasyConditionCost,
            
            // Easy - common gameplay scenarios
            UpgradeConditionType.DamageDealtThisFight => easyConditionCost,
            UpgradeConditionType.HealingGivenThisFight => easyConditionCost,
            UpgradeConditionType.PlayedInStance => easyConditionCost,
            UpgradeConditionType.DrawnOften => easyConditionCost,
            UpgradeConditionType.ZeroCostCardsThisTurn => easyConditionCost,
            
            // Medium - require some planning
            UpgradeConditionType.ComboCountReached => mediumConditionCost,
            UpgradeConditionType.PlayedWithCombo => mediumConditionCost,
            UpgradeConditionType.PlayedAtLowHealth => mediumConditionCost,
            UpgradeConditionType.PlayedMultipleTimesInTurn => mediumConditionCost,
            UpgradeConditionType.DamageDealtInSingleTurn => mediumConditionCost,
            UpgradeConditionType.ZeroCostCardsThisFight => mediumConditionCost,
            
            // Hard - require specific strategies
            UpgradeConditionType.PerfectionStreakAchieved => hardConditionCost,
            UpgradeConditionType.PlayedAsFinisher => hardConditionCost,
            UpgradeConditionType.AllCopiesPlayedFromHand => hardConditionCost,
            UpgradeConditionType.PlayedOnConsecutiveTurns => hardConditionCost,
            UpgradeConditionType.WonFightUsingCard => hardConditionCost,
            
            // Very Hard - rare or difficult conditions
            UpgradeConditionType.DefeatedOpponentWithCard => veryHardConditionCost,
            UpgradeConditionType.LostFightWithCard => veryHardConditionCost,
            UpgradeConditionType.SurvivedFightWithCard => veryHardConditionCost,
            UpgradeConditionType.PerfectTurnPlayed => veryHardConditionCost,
            UpgradeConditionType.OnlyCardPlayedThisTurn => veryHardConditionCost,
            
            _ => mediumConditionCost
        };
    }
    
    /// <summary>
    /// Check if this is a lifetime (persistent) condition vs per-fight
    /// </summary>
    private bool IsLifetimeCondition(UpgradeConditionType conditionType)
    {
        return conditionType switch
        {
            UpgradeConditionType.TimesPlayedAcrossFights => true,
            UpgradeConditionType.DrawnOftenLifetime => true,
            UpgradeConditionType.HeldAtTurnEndLifetime => true,
            UpgradeConditionType.DiscardedManuallyLifetime => true,
            UpgradeConditionType.FinalCardInHandLifetime => true,
            UpgradeConditionType.ComboUseBackToBackLifetime => true,
            UpgradeConditionType.OnlyCardPlayedInTurnLifetime => true,
            UpgradeConditionType.TotalFightsWon => true,
            UpgradeConditionType.TotalFightsLost => true,
            UpgradeConditionType.TotalBattleTurns => true,
            UpgradeConditionType.TotalPerfectTurns => true,
            UpgradeConditionType.TotalStatusEffectsSurvived => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Get value modifier based on required threshold for conditions that scale
    /// </summary>
    private float GetValueModifier(UpgradeConditionType conditionType, int requiredValue)
    {
        // Most conditions scale linearly with required value
        bool scalesWithValue = conditionType switch
        {
            UpgradeConditionType.TimesPlayedThisFight => true,
            UpgradeConditionType.TimesPlayedAcrossFights => true,
            UpgradeConditionType.DamageDealtThisFight => true,
            UpgradeConditionType.DamageDealtInSingleTurn => true,
            UpgradeConditionType.ComboCountReached => true,
            UpgradeConditionType.PlayedOnConsecutiveTurns => true,
            UpgradeConditionType.PlayedMultipleTimesInTurn => true,
            _ => false
        };
        
        if (scalesWithValue && requiredValue > 1)
        {
            // Logarithmic scaling - each additional requirement is worth less
            return 1f + Mathf.Log10(requiredValue) * 0.3f;
        }
        
        return 1f;
    }
} 