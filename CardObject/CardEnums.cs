using UnityEngine;
using System;

/// <summary>
/// Card types for sequencing and mechanics
/// </summary>
public enum CardType
{
    None,
    Attack,
    Skill,
    Spell,
    Combo,
    Finisher,
    Stance,
    Artifact,
    Ritual,
    Counter,
    Reaction
}

/// <summary>
/// Card rarity levels that determine point budgets and drop chances
/// </summary>
public enum CardRarity
{
    Common,
    Uncommon,
    Rare
}

/// <summary>
/// Simplified card effect types - only core mechanical effects that can't be represented elsewhere
/// </summary>
public enum CardEffectType
{
    // ═══ CORE EFFECTS ═══
    Damage,
    Heal,
    DrawCard,
    RestoreEnergy,
    
    // ═══ STATUS EFFECTS ═══
    ApplyBreak,
    ApplyWeak,
    ApplyBurn,
    ApplySalve,
    RaiseCriticalChance,
    ApplyThorns,
    ApplyShield,

    ApplyStun,

    ApplyStrength,
    ApplyCurse,
    
    // ═══ CARD MANIPULATION ═══

    
    // ═══ STANCE EFFECTS ═══
    EnterStance,
    ExitStance
}

/// <summary>
/// Card target types - single target with override options via "can also target" flags
/// </summary>
public enum CardTargetType
{
    Self,           // Target the caster (player or pet)
    Opponent,       // Target the opponent (enemy player or pet)
    Ally,           // Target your ally (player targets pet, pet targets player)
    Random          // Target is chosen randomly
}

/// <summary>
/// Elemental types for status effects
/// </summary>


/// <summary>
/// Stance types for the stance system
/// </summary>
public enum StanceType
{
    None,
    Aggressive,     // +damage, -defense
    Defensive,      // +defense, -damage
    Focused,        // +energy, +draw
    Berserker,      // +damage, +speed, -health
    Guardian,       // +shield, +thorns
    Mystic,         // +elemental effects
    LimitBreak      // Enhanced abilities (existing)
}

/// <summary>
/// Scaling types for dynamic effects
/// </summary>
public enum ScalingType
{
    None,
    ZeroCostCardsThisTurn,
    ZeroCostCardsThisFight,
    CardsPlayedThisTurn,
    CardsPlayedThisFight,
    DamageDealtThisTurn,
    DamageDealtThisFight,
    CurrentHealth,
    MissingHealth,
    ComboCount,
    HandSize
}

/// <summary>
/// Defines how alternative effects interact with main effects
/// </summary>
public enum AlternativeEffectLogic
{
    [Tooltip("Alternative effect replaces main effect (Either main OR alternative)")]
    Replace,    // Current behavior: either main effect OR alternative effect
    
    [Tooltip("Alternative effect is added to main effect (Both main AND alternative)")]
    Additional  // New behavior: main effect AND alternative effect
}

/// <summary>
/// Conditional effect types for complex card mechanics
/// </summary>
public enum ConditionalType
{
    None,
    IfTargetHealthBelow,
    IfTargetHealthAbove,
    IfSourceHealthBelow,
    IfSourceHealthAbove,
    IfCardsInHand,
    IfCardsInDeck,
    IfCardsInDiscard,
    IfTimesPlayedThisFight,
    IfDamageTakenThisFight,
    IfDamageTakenLastRound,
    IfHealingReceivedThisFight,
    IfHealingReceivedLastRound,
    IfPerfectionStreak,
    IfComboCount,
    IfZeroCostCardsThisTurn,
    IfZeroCostCardsThisFight,
    IfInStance,
    IfLastCardType,
    IfEnergyRemaining
}

/*
 * ═══════════════════════════════════════════════════════════════════════════════════════════
 * STRUCTURES MOVED TO LOGICAL HOMES:
 * ═══════════════════════════════════════════════════════════════════════════════════════════
 * 
 * ✅ MOVED TO CardData.cs:
 *    - CardEffect (Unified card effect structure)
 *    - CardSequenceRequirement (Card sequencing requirements)
 *    - PersistentFightEffect (Persistent fight effects)
 *    - CardTrackingData (Card play tracking)
 *    - ScalingEffect (Legacy - for backward compatibility)
 *    - ConditionalEffect (Legacy - for backward compatibility)
 *    - MultiEffect (Legacy - for backward compatibility)
 * 
 * ✅ MOVED TO EntityTracker.cs:
 *    - EntityTrackingData (Entity combat tracking)
 *    - StanceEffect (Stance configuration and effects)
 * 
 * ✅ MOVED TO EffectHandler.cs:
 *    - ZoneEffect (Zone effect configuration)
 * 
 * ✅ REMOVED LEGACY INTERFACES:
 *    - IStatusEffect, IScalingEffect, IComplexCard, IMultiTargetEffect
 *    - IConditionalCard, IStanceCard, IPersistentCard, IElementalCard
 *    (These interfaces were never actually implemented and were redundant)
 * 
 * RESULT: CardEnums.cs now contains only enums, which is its intended purpose.
 *         All data structures are now in their logical homes where they're actually used.
 */ 

/// <summary>
/// Types of upgrade conditions based on available tracking data
/// </summary>
public enum UpgradeConditionType
{
    // Card-specific tracking
    TimesPlayedThisFight,
    TimesPlayedAcrossFights, // Requires persistent tracking
    CopiesInDeck,
    CopiesInHand,
    CopiesInDiscard,
    AllCopiesPlayedFromHand,
    
    // Combat performance
    DamageDealtThisFight,
    DamageDealtInSingleTurn,
    DamageTakenThisFight,
    HealingGivenThisFight,
    HealingReceivedThisFight,
    PerfectionStreakAchieved,
    
    // Combo and tactical
    ComboCountReached,
    PlayedWithCombo,
    PlayedInStance,
    PlayedAsFinisher,
    ZeroCostCardsThisTurn,
    ZeroCostCardsThisFight,
    
    // Health-based
    PlayedAtLowHealth,
    PlayedAtHighHealth,
    PlayedAtHalfHealth, // Under 50% HP
    SurvivedFightWithCard,
    
    // Turn-based
    PlayedOnConsecutiveTurns,
    PlayedMultipleTimesInTurn,
    
    // Victory conditions
    WonFightUsingCard,
    DefeatedOpponentWithCard,
    LostFightWithCard, // Lose to Win condition
    
    // NEW: Advanced tracking conditions (per-fight)
    ComboUseBackToBack, // Played back-to-back with same card type
    DrawnOften, // Drawn X times this fight
    HeldAtTurnEnd, // In hand at end of turn X times this fight
    DiscardedManually, // Manually discarded X times this fight
    FinalCardInHand, // Last card in hand X times this fight
    FamiliarNameInDeck, // X cards share keyword/title fragment
    OnlyCardTypeInDeck, // Only card of its type in deck
    AllCardsCostLowEnough, // All cards cost 1 or less
    DeckSizeBelow, // Deck has fewer than X cards
    SurvivedStatusEffect, // Survived specific status effect this fight
    BattleLengthOver, // Battle lasted longer than X turns
    PerfectTurnPlayed, // Played in perfect turn (no damage, all energy used)
    OnlyCardPlayedThisTurn, // Only card played that turn
    
    // NEW: Lifetime tracking conditions
    DrawnOftenLifetime, // Drawn X times across all fights
    HeldAtTurnEndLifetime, // In hand at end of turn X times lifetime
    DiscardedManuallyLifetime, // Manually discarded X times lifetime
    FinalCardInHandLifetime, // Last card in hand X times lifetime
    ComboUseBackToBackLifetime, // Played back-to-back X times lifetime
    OnlyCardPlayedInTurnLifetime, // Only card played X times lifetime
    TotalFightsWon, // Won X fights total
    TotalFightsLost, // Lost X fights total
    TotalBattleTurns, // Participated in X total battle turns
    TotalPerfectTurns, // Achieved X perfect turns lifetime
    TotalStatusEffectsSurvived // Survived X different status effects lifetime
}

/// <summary>
/// How to compare tracked values against required values for upgrades
/// </summary>
public enum UpgradeComparisonType
{
    [Tooltip("Tracked value must be greater than or equal to required value")]
    GreaterThanOrEqual,
    
    [Tooltip("Tracked value must be exactly equal to required value")]
    Equal,
    
    [Tooltip("Tracked value must be less than or equal to required value")]
    LessThanOrEqual,
    
    [Tooltip("Tracked value must be greater than required value")]
    GreaterThan,
    
    [Tooltip("Tracked value must be less than required value")]
    LessThan
}