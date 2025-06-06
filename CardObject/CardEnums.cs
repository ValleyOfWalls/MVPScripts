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
    ApplyDamageOverTime,
    ApplyHealOverTime,
    RaiseCriticalChance,
    ApplyThorns,
    ApplyShield,
    ApplyElementalStatus,
    ApplyStun,
    ApplyLimitBreak,
    ApplyStrength,
    ApplyCurse,
    
    // ═══ CARD MANIPULATION ═══
    DiscardRandomCards,
    
    // ═══ STANCE EFFECTS ═══
    EnterStance,
    ExitStance
}

/// <summary>
/// Enhanced card target types
/// </summary>
public enum CardTargetType
{
    Self,           // Target the caster (player or pet)
    Opponent,       // Target the opponent (enemy player or pet)
    Ally,           // Target your ally (player targets pet, pet targets player)
    Random,         // Target is chosen randomly
    All,            // Target all entities
    AllAllies,      // Target all allies
    AllEnemies,     // Target all enemies
    AllPlayers,     // Target all players globally
    AllPets,        // Target all pets globally
    Everyone        // Target everyone globally (zone effect)
}

/// <summary>
/// Elemental types for status effects
/// </summary>
public enum ElementalType
{
    None,
    Fire,
    Ice,
    Lightning,
    Void
}

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