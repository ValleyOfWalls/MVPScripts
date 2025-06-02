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
    BuffStats,
    DebuffStats,
    
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

/// <summary>
/// Data structure for card sequencing requirements
/// </summary>
[Serializable]
public class CardSequenceRequirement
{
    public bool hasSequenceRequirement;
    public CardType requiredPreviousCardType;
    public bool requiresExactPrevious;
    public bool requiresAnyInTurn;
    public bool allowIfComboActive;
    public bool allowIfInStance;
    public StanceType requiredStance;
}

/// <summary>
/// Data structure for stance effects
/// </summary>
[Serializable]
public class StanceEffect
{
    [Header("Stance Configuration")]
    public StanceType stanceType;
    public bool overridePreviousStance;
    
    [Header("Stat Modifiers")]
    public int damageModifier;
    public int defenseModifier;
    public int energyModifier;
    public int drawModifier;
    public int healthModifier;
    
    [Header("Special Effects")]
    public bool grantsThorns;
    public int thornsAmount;
    public bool grantsShield;
    public int shieldAmount;
    public bool enhancesCritical;
    public int criticalBonus;
    
    [Header("Ongoing Effects")]
    public CardEffectType onTurnStartEffect;
    public int onTurnStartAmount;
    public CardEffectType onTurnEndEffect;
    public int onTurnEndAmount;
}

/// <summary>
/// Data structure for persistent fight effects
/// </summary>
[Serializable]
public class PersistentFightEffect
{
    [Header("Basic Configuration")]
    public string effectName = "Persistent Effect";
    public CardEffectType effectType = CardEffectType.Damage;
    public int potency = 1;
    
    [Header("Trigger Timing")]
    public int triggerInterval = 1; // Every X turns
    public bool lastEntireFight = true;
    public int turnDuration = 0; // If not entire fight
    
    [Header("Requirements")]
    public bool requiresStance = false;
    public StanceType requiredStance = StanceType.None;
    
    [Header("Stacking")]
    public bool stackable = true;
}

/// <summary>
/// Legacy data structure for zone effects (for backward compatibility)
/// </summary>
[Serializable]
public class ZoneEffect
{
    public CardEffectType effectType;
    public int baseAmount;
    public int duration;
    public ElementalType elementalType;
    public bool affectAllPlayers;
    public bool affectAllPets;
    public bool affectCaster;
    public bool excludeOpponents;
    public ScalingType scalingType;
    public float scalingMultiplier;
}

/// <summary>
/// Legacy interface for cards that affect status
/// </summary>
public interface IStatusEffect
{
    string StatusName { get; }
    int StatusPotency { get; }
    int StatusDuration { get; }
    ElementalType ElementalType { get; }
}

/// <summary>
/// Legacy interface for cards that scale
/// </summary>
public interface IScalingEffect
{
    ScalingType ScalingType { get; }
    float ScalingMultiplier { get; }
    int MaxScaling { get; }
}

/// <summary>
/// Legacy interface for cards with complex mechanics
/// </summary>
public interface IComplexCard
{
    bool HasConditionalBehavior { get; }
    bool HasMultipleEffects { get; }
    bool HasZoneEffect { get; }
    bool HasPersistentEffect { get; }
}

/// <summary>
/// Legacy interface for cards that affect multiple targets
/// </summary>
public interface IMultiTargetEffect
{
    bool AffectAllPlayers { get; }
    bool AffectAllPets { get; }
    bool IncludeCaster { get; }
    bool ExcludeOpponents { get; }
}

/// <summary>
/// Legacy interface for cards that require specific conditions
/// </summary>
public interface IConditionalCard
{
    ConditionalType ConditionType { get; }
    int ConditionValue { get; }
    bool ConditionMet { get; }
}

/// <summary>
/// Legacy interface for cards that affect stances
/// </summary>
public interface IStanceCard
{
    StanceType AffectedStance { get; }
    bool OverridePreviousStance { get; }
}

/// <summary>
/// Legacy interface for cards with persistent effects
/// </summary>
public interface IPersistentCard
{
    string PersistentEffectName { get; }
    int TriggerInterval { get; }
    bool LastEntireFight { get; }
    bool RequiresStance { get; }
}

/// <summary>
/// Legacy interface for cards with elemental effects
/// </summary>
public interface IElementalCard
{
    ElementalType ElementalType { get; }
    bool HasElementalStatus { get; }
}

/// <summary>
/// Enhanced tracking data for various card mechanics
/// </summary>
[Serializable]
public class CardTrackingData
{
    [Header("Play Tracking")]
    public int timesPlayedThisFight;
    public int comboCount;
    public bool hasComboModifier;
    
    [Header("Card References")]
    public CardData upgradedVersion;
    
    [Header("Deck Tracking")]
    public int cardsWithSameNameInDeck;
    public int cardsWithSameNameInHand;
    public int cardsWithSameNameInDiscard;
    
    [Header("Turn Tracking")]
    public int zeroCostCardsThisTurn;
    public int zeroCostCardsThisFight;
    public CardType lastPlayedCardType;
}

/// <summary>
/// Enhanced entity tracking data for damage, healing, and perfection streaks
/// </summary>
[Serializable]
public class EntityTrackingData
{
    [Header("Damage Tracking")]
    public int damageTakenThisFight;
    public int damageTakenLastRound;
    public int damageDealtThisFight;
    public int damageDealtLastRound;
    
    [Header("Healing Tracking")]
    public int healingReceivedThisFight;
    public int healingReceivedLastRound;
    public int healingGivenThisFight;
    public int healingGivenLastRound;
    
    [Header("Perfection Tracking")]
    public int perfectionStreak; // Turns without taking damage
    public int currentTurnNumber;
    public bool tookDamageThisTurn;
    
    [Header("Combat State")]
    public int comboCount;
    public bool isStunned;
    public bool isInLimitBreak;
    public StanceType currentStance;
    
    [Header("Turn Tracking")]
    public int zeroCostCardsThisTurn;
    public int zeroCostCardsThisFight;
    public int cardsPlayedThisTurn;
    public int cardsPlayedThisFight;
    public CardType lastPlayedCardType;
    
    [Header("Strength")]
    public int strengthStacks;
}

/// <summary>
/// Unified card effect structure - replaces MultiEffect, ConditionalEffect, and main effect
/// </summary>
[Serializable]
public class CardEffect
{
    [Header("═══ EFFECT CONFIGURATION ═══")]
    [Tooltip("What this effect does")]
    public CardEffectType effectType = CardEffectType.Damage;
    
    [Tooltip("Base power/amount of the effect")]
    public int amount = 3;
    
    [ShowIfAny("effectType", 
        (int)CardEffectType.ApplyWeak, 
        (int)CardEffectType.ApplyBreak, 
        (int)CardEffectType.ApplyThorns, 
        (int)CardEffectType.ApplyStun, 
        (int)CardEffectType.ApplyDamageOverTime, 
        (int)CardEffectType.ApplyHealOverTime, 
        (int)CardEffectType.RaiseCriticalChance)]
    [Tooltip("Duration for status effects (turns to last)")]
    public int duration = 3;
    
    [Header("═══ TARGETING ═══")]
    [Tooltip("Who this effect targets")]
    public CardTargetType targetType = CardTargetType.Opponent;
    
    [Tooltip("Elemental type for special interactions")]
    public ElementalType elementalType = ElementalType.None;
    
    [Header("═══ CONDITIONAL TRIGGER (Optional) ═══")]
    [Tooltip("This effect only triggers if a condition is met")]
    public ConditionalType conditionType = ConditionalType.None;
    
    [ConditionalField("conditionType", ConditionalType.None, true)]
    [Tooltip("Value to compare against for the condition")]
    public int conditionValue = 0;
    
    [ConditionalField("conditionType", ConditionalType.None, true)]
    [Tooltip("Alternative effect if condition is not met")]
    public bool hasAlternativeEffect = false;
    
    [ConditionalField("hasAlternativeEffect", true, true)]
    [Tooltip("Effect to use if condition fails")]
    public CardEffectType alternativeEffectType = CardEffectType.Damage;
    
    [ConditionalField("hasAlternativeEffect", true, true)]
    [Tooltip("Amount for alternative effect")]
    public int alternativeEffectAmount = 1;
    
    [Header("═══ SCALING (Optional) ═══")]
    [Tooltip("This effect scales with game state")]
    public ScalingType scalingType = ScalingType.None;
    
    [ConditionalField("scalingType", ScalingType.None, true)]
    [Tooltip("How much to multiply the scaling value")]
    public float scalingMultiplier = 1.0f;
    
    [ConditionalField("scalingType", ScalingType.None, true)]
    [Tooltip("Maximum value this effect can scale to")]
    public int maxScaling = 10;
}

/// <summary>
/// Legacy data structure for scaling effects (for backward compatibility)
/// </summary>
[Serializable]
public class ScalingEffect
{
    [Header("Scaling Configuration")]
    public ScalingType scalingType;
    public float scalingMultiplier = 1.0f;
    public int baseAmount;
    public int maxScaling = 999; // Cap for scaling
    
    [Header("Effect")]
    public CardEffectType effectType;
    public ElementalType elementalType;
}

/// <summary>
/// Legacy data structure for conditional effects (for backward compatibility)
/// </summary>
[Serializable]
public class ConditionalEffect
{
    [Header("Condition")]
    public ConditionalType conditionType;
    public int conditionValue;
    public bool conditionMet;
    
    [Header("Effect if Condition Met")]
    public CardEffectType effectType;
    public int effectAmount;
    public int effectDuration;
    public ElementalType elementalType;
    
    [Header("Alternative Effect if Condition Not Met")]
    public bool hasAlternativeEffect;
    public CardEffectType alternativeEffectType;
    public int alternativeEffectAmount;
    public int alternativeEffectDuration;
    
    [Header("Scaling")]
    public bool useScaling;
    public ScalingEffect scalingEffect;
}

/// <summary>
/// Legacy data structure for multi-effects (for backward compatibility)
/// </summary>
[Serializable]
public class MultiEffect
{
    public CardEffectType effectType;
    public int amount;
    public int duration;
    public ElementalType elementalType;
    public CardTargetType targetType; // Can override the main card's target type
    
    [Header("Scaling")]
    public bool useScaling;
    public ScalingEffect scalingEffect;
}

/*
 * ═══════════════════════════════════════════════════════════════════════════════════════════
 * REMOVED REDUNDANT EFFECT TYPES - These functionalities are covered elsewhere:
 * ═══════════════════════════════════════════════════════════════════════════════════════════
 * 
 * ✅ SCALING EFFECTS (covered by CardEffect.scalingType/scalingMultiplier):
 *    - ScaleByZeroCostCards, ScaleByCardsPlayed, ScaleByDamageDealt, ScaleByHealth
 * 
 * ✅ CONDITIONAL EFFECTS (covered by CardEffect.conditionType/conditionValue):
 *    - ConditionalEffect → Use conditionType field in CardEffect
 * 
 * ✅ MULTI-EFFECTS (covered by multiple CardEffect entries):
 *    - MultiEffect → Just add multiple effects to the effects list
 * 
 * ✅ ZONE EFFECTS (covered by CardTargetType.All/AllEnemies/AllAllies/Everyone):
 *    - ZoneDamageAll, ZoneHealAll, ZoneBuffAll, ZoneDebuffAll, ZoneDrawAll, 
 *      ZoneEnergyAll, ZoneStatusAll → Use core effects + appropriate target types
 * 
 * ✅ PERSISTENT EFFECTS (covered by PersistentFightEffect structure):
 *    - PersistentDamageAura, PersistentHealingAura, PersistentEnergyRegen,
 *      PersistentDrawBonus, PersistentDamageReduction, PersistentCritBonus
 *    → Use PersistentFightEffect with core effect types (Damage, Heal, etc.)
 * 
 * RESULT: CardEffectType now only contains core mechanical effects that can't be 
 * represented by other card data fields. This eliminates redundancy and confusion.
 */ 