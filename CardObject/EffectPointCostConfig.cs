using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Configuration for effect point costs in the randomized card system.
/// Defines how many points each effect type costs and modifiers for conditional effects.
/// 
/// EFFECT SCALING (Updated for 100 HP, ~7 round fights):
/// - Damage: 0.4 pts/dmg → 25-50 damage per card (12-25% of HP)
/// - Healing: 0.35 pts/heal → 28-57 healing per card (meaningful recovery)
/// - Energy: 0.15 pts/energy → 66-133 energy per card (0.6-1.3 turns worth)
/// - Shield: 0.3 pts/shield → 33-66 shield per card (33-66% damage reduction)
/// - Status effects: 3-5 points → High impact, limited quantity per card
/// </summary>
[CreateAssetMenu(fileName = "EffectPointCostConfig", menuName = "Card System/Effect Point Cost Config")]
public class EffectPointCostConfig : ScriptableObject
{
    [Header("Core Effect Base Costs")]
    [SerializeField, Tooltip("Point cost per damage dealt")]
    public float damagePointCost = 0.4f;
    
    [SerializeField, Tooltip("Point cost per healing done")]
    public float healPointCost = 0.35f;
    
    [SerializeField, Tooltip("Point cost per card drawn")]
    public float drawCardPointCost = 3f;
    
    [SerializeField, Tooltip("Point cost per energy restored")]
    public float restoreEnergyPointCost = 0.15f;
    
    [Header("Status Effect Base Costs")]
    [SerializeField, Tooltip("Point cost for applying Break status")]
    public float applyBreakPointCost = 4f;
    
    [SerializeField, Tooltip("Point cost for applying Weak status")]
    public float applyWeakPointCost = 3f;
    
    [SerializeField, Tooltip("Point cost per burn damage per turn")]
    public float applyBurnPointCost = 0.6f;
    
    [SerializeField, Tooltip("Point cost per salve healing per turn")]
    public float applySalvePointCost = 0.5f;
    
    [SerializeField, Tooltip("Point cost for raising critical chance")]
    public float raiseCriticalChancePointCost = 2.5f;
    
    [SerializeField, Tooltip("Point cost per thorns damage")]
    public float applyThornsPointCost = 0.4f;
    
    [SerializeField, Tooltip("Point cost per shield amount")]
    public float applyShieldPointCost = 0.3f;
    
    [SerializeField, Tooltip("Point cost for elemental status effects")]
    public float applyElementalStatusPointCost = 3f;
    
    [SerializeField, Tooltip("Point cost for stun effects")]
    public float applyStunPointCost = 5f;
    
    [SerializeField, Tooltip("Point cost for limit break effects")]
    public float applyLimitBreakPointCost = 4.5f;
    
    [SerializeField, Tooltip("Point cost for strength effects")]
    public float applyStrengthPointCost = 2.5f;
    
    [SerializeField, Tooltip("Point cost for curse effects")]
    public float applyCursePointCost = 3.5f;
    
    [Header("Card Manipulation Costs")]
    [SerializeField, Tooltip("Point cost per card discarded")]
    public float discardRandomCardsPointCost = 2f;
    
    [Header("Stance Effect Costs")]
    [SerializeField, Tooltip("Point cost for stance entry effects")]
    public float enterStancePointCost = 4f;
    
    [SerializeField, Tooltip("Point cost for stance exit effects")]
    public float exitStancePointCost = 2f;
    
    [Header("Conditional Effect Modifiers")]
    [SerializeField, Tooltip("Multiplier for conditional effects (harder conditions = lower multiplier)")]
    [Range(0.1f, 1f)]
    public float easyConditionMultiplier = 0.9f;
    
    [SerializeField, Tooltip("Multiplier for moderately difficult conditions")]
    [Range(0.1f, 1f)]
    public float mediumConditionMultiplier = 0.7f;
    
    [SerializeField, Tooltip("Multiplier for very difficult conditions")]
    [Range(0.1f, 1f)]
    public float hardConditionMultiplier = 0.5f;
    
    /// <summary>
    /// Get the point cost for a specific effect type with value
    /// </summary>
    public float GetEffectPointCost(CardEffectType effectType, float effectValue)
    {
        return effectType switch
        {
            CardEffectType.Damage => effectValue * damagePointCost,
            CardEffectType.Heal => effectValue * healPointCost,
            CardEffectType.DrawCard => effectValue * drawCardPointCost,
            CardEffectType.RestoreEnergy => effectValue * restoreEnergyPointCost,
            CardEffectType.ApplyBreak => applyBreakPointCost,
            CardEffectType.ApplyWeak => applyWeakPointCost,
            CardEffectType.ApplyBurn => effectValue * applyBurnPointCost,
            CardEffectType.ApplySalve => effectValue * applySalvePointCost,
            CardEffectType.RaiseCriticalChance => raiseCriticalChancePointCost,
            CardEffectType.ApplyThorns => effectValue * applyThornsPointCost,
            CardEffectType.ApplyShield => effectValue * applyShieldPointCost,
            CardEffectType.ApplyElementalStatus => applyElementalStatusPointCost,
            CardEffectType.ApplyStun => applyStunPointCost,
            CardEffectType.ApplyLimitBreak => applyLimitBreakPointCost,
            CardEffectType.ApplyStrength => applyStrengthPointCost,
            CardEffectType.ApplyCurse => applyCursePointCost,
            CardEffectType.DiscardRandomCards => effectValue * discardRandomCardsPointCost,
            CardEffectType.EnterStance => enterStancePointCost,
            CardEffectType.ExitStance => exitStancePointCost,
            _ => 1f
        };
    }
    
    /// <summary>
    /// Get the conditional effect multiplier based on difficulty
    /// </summary>
    public float GetConditionalMultiplier(ConditionalType conditionalType)
    {
        return conditionalType switch
        {
            // Easy conditions (likely to trigger)
            ConditionalType.IfCardsInHand => easyConditionMultiplier,
            ConditionalType.IfTargetHealthAbove => easyConditionMultiplier,
            ConditionalType.IfEnergyRemaining => easyConditionMultiplier,
            
            // Medium conditions
            ConditionalType.IfTargetHealthBelow => mediumConditionMultiplier,
            ConditionalType.IfSourceHealthBelow => mediumConditionMultiplier,
            ConditionalType.IfTimesPlayedThisFight => mediumConditionMultiplier,
            ConditionalType.IfComboCount => mediumConditionMultiplier,
            ConditionalType.IfInStance => mediumConditionMultiplier,
            
            // Hard conditions (difficult to trigger)
            ConditionalType.IfPerfectionStreak => hardConditionMultiplier,
            ConditionalType.IfDamageTakenLastRound => hardConditionMultiplier,
            ConditionalType.IfHealingReceivedLastRound => hardConditionMultiplier,
            ConditionalType.IfZeroCostCardsThisFight => hardConditionMultiplier,
            
            _ => mediumConditionMultiplier
        };
    }
} 