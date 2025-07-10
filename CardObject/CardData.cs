using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines the category of a card for different game systems
/// </summary>
public enum CardCategory
{
    [Tooltip("Basic cards that come with starter decks (BasicAttack, BasicDefend, etc.)")]
    Starter,
    
    [Tooltip("Advanced cards available in draft packs and shops")]
    Draftable,
    
    [Tooltip("Upgraded versions of cards (not available in drafts or shops)")]
    Upgraded
}

[CreateAssetMenu(fileName = "New CardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("═══ BASIC CARD INFO ═══")]
    [SerializeField] private int _cardId = 0;
    [SerializeField] private string _cardName = "New Card";
    [TextArea(2, 4)]
    [SerializeField] private string _description = "Card Description";
    [SerializeField] private Sprite _cardArtwork;
    [SerializeField] private CardRarity _rarity = CardRarity.Common;

    [Header("═══ CARD CATEGORY ═══")]
    [Tooltip("Determines where this card can appear (starter decks, draft packs, shops, etc.)")]
    [SerializeField] private CardCategory _cardCategory = CardCategory.Draftable;

    [Header("═══ CORE MECHANICS ═══")]
    [Tooltip("What type of card this is (affects sequencing and AI)")]
    [SerializeField] private CardType _cardType = CardType.Attack;
    
    [Tooltip("Energy cost to play this card")]
    [SerializeField] private int _energyCost = 2;
    
    [Tooltip("Initiative determines execution order in queue (higher = executes first, 0 = use default order)")]
    [SerializeField] private int _initiative = 0;

    [Header("═══ CARD EFFECTS ═══")]
    [Tooltip("All effects this card performs when played")]
    [SerializeField] private List<CardEffect> _effects = new List<CardEffect>();

    [Header("═══ COMBO SYSTEM ═══")]
    [Tooltip("This card builds combo when played")]
    [SerializeField] private bool _buildsCombo = false;
    
    [Tooltip("This card can only be played when you have combo")]
    [SerializeField] private bool _requiresCombo = false;
    
    [ConditionalField("_requiresCombo", true, true)]
    [Tooltip("Amount of combo required to play this card")]
    [SerializeField] private int _requiredComboAmount = 1;

    [Header("═══ STANCE EFFECTS ═══")]
    [Tooltip("This card changes combat stance")]
    [SerializeField] private bool _changesStance = false;
    
    [ConditionalField("_changesStance", true, true)]
    [SerializeField] private StanceType _newStance = StanceType.Aggressive;



    [Header("═══ TRACKING & UPGRADES ═══")]
    [SerializeField] private CardData _upgradedVersion;
    
    [Tooltip("Track how many times this card is played")]
    [SerializeField] private bool _trackPlayCount = true;
    
    [Tooltip("Track damage/healing for scaling")]
    [SerializeField] private bool _trackDamageHealing = true;
    
    [Header("═══ UPGRADE CONDITIONS ═══")]
    [Tooltip("This card can upgrade to another version")]
    [SerializeField] private bool _canUpgrade = false;
    
    [ConditionalField("_canUpgrade", true, true)]
    [Tooltip("Type of condition required for upgrade")]
    [SerializeField] private UpgradeConditionType _upgradeConditionType = UpgradeConditionType.TimesPlayedThisFight;
    
    [ConditionalField("_canUpgrade", true, true)]
    [Tooltip("Value that must be reached for upgrade")]
    [SerializeField] private int _upgradeRequiredValue = 5;
    
    [ConditionalField("_canUpgrade", true, true)]
    [Tooltip("How to compare the tracked value")]
    [SerializeField] private UpgradeComparisonType _upgradeComparisonType = UpgradeComparisonType.GreaterThanOrEqual;
    
    [ConditionalField("_canUpgrade", true, true)]
    [Tooltip("Upgrade all copies of this card when conditions are met")]
    [SerializeField] private bool _upgradeAllCopies = false;

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC PROPERTIES - CLEAN INTERFACE
    // ═══════════════════════════════════════════════════════════════
    
    public int CardId => _cardId;
    public string CardName => _cardName;
    public string Description => _description;
    public Sprite CardArtwork => _cardArtwork;
    public CardRarity Rarity => _rarity;
    public CardCategory CardCategory => _cardCategory;
    public CardType CardType => _cardType;
    public int EnergyCost => _energyCost;
    public int Initiative => _initiative;
    public CardData UpgradedVersion => _upgradedVersion;
    
    // Core mechanics
    public bool HasEffects => _effects != null && _effects.Count > 0;
    public List<CardEffect> Effects => _effects ?? new List<CardEffect>();
    
    public bool ChangesStance => _changesStance;
    public StanceType NewStance => _newStance;
    

    
    public bool TrackPlayCount => _trackPlayCount;
    public bool TrackDamageHealing => _trackDamageHealing;
    
    // Upgrade system
    public bool CanUpgrade => _canUpgrade;
    public UpgradeConditionType UpgradeConditionType => _upgradeConditionType;
    public int UpgradeRequiredValue => _upgradeRequiredValue;
    public UpgradeComparisonType UpgradeComparisonType => _upgradeComparisonType;
    public bool UpgradeAllCopies => _upgradeAllCopies;
    
    // Combo system
    public bool BuildsCombo => _buildsCombo;
    public bool RequiresCombo => _requiresCombo;
    public int RequiredComboAmount => _requiredComboAmount;
    
    // Utility properties
    public bool IsZeroCost => _energyCost == 0;

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Validates that the card configuration follows design rules
    /// </summary>
    public bool ValidateCardConfiguration(out List<string> errors)
    {
        errors = new List<string>();
        
        // Check for restricted effect types
        foreach (var effect in _effects)
        {
            // Removed effects are no longer supported - validation removed since enums no longer exist
            
            // Verify that status effects have valid names
            if (IsStatusEffect(effect.effectType) && effect.amount <= 0)
            {
                errors.Add($"Status effect {effect.effectType} must have a positive amount value.");
            }
            

            
            // Random targeting is not allowed
            if (effect.targetType == CardTargetType.Random)
            {
                errors.Add("Random targeting is not allowed. Use specific targets (Self, Ally, Opponent).");
            }
            
            // IfEnergyRemaining conditions are not allowed
            if (effect.conditionType == ConditionalType.IfEnergyRemaining)
            {
                errors.Add("IfEnergyRemaining conditions are not allowed. Use other conditional types.");
            }
            
            // Stance effects should not have duration
            if (effect.effectType == CardEffectType.EnterStance && effect.amount > 0)
            {
                errors.Add("Stance effects should not have duration. Stances are state-based, not time-limited.");
            }
            

            
            // Check for effects that shouldn't scale
            if (effect.scalingType != ScalingType.None && !CanEffectTypeScale(effect.effectType))
            {
                errors.Add($"{effect.effectType} effects cannot scale. Remove scaling from this effect.");
            }
            
            // Negative effects should specify targets when not targeting enemies
            if (IsNegativeEffect(effect.effectType) && effect.targetType != CardTargetType.Opponent && effect.targetType != CardTargetType.Self)
            {
                // This is actually fine for ally targeting in some cases, so just a warning
                // errors.Add($"Negative effect {effect.effectType} should typically target enemies unless intentionally targeting allies.");
            }
        }
        
        return errors.Count == 0;
    }
    
    /// <summary>
    /// Check if an effect type can logically scale
    /// </summary>
    private bool CanEffectTypeScale(CardEffectType effectType)
    {
        return effectType switch
        {
            // Effects that make sense to scale
            CardEffectType.Damage => true,
            CardEffectType.Heal => true,
            CardEffectType.ApplyShield => true,
            CardEffectType.ApplyThorns => true,
            CardEffectType.ApplyStrength => true,
            CardEffectType.ApplySalve => true,
            CardEffectType.ApplyWeak => true,
            CardEffectType.ApplyBreak => true,
            CardEffectType.ApplyBurn => true,
            CardEffectType.ApplyCurse => true,
            CardEffectType.Amplify => true,              // Amplification amount can scale
            
            // Effects that don't make sense to scale
            CardEffectType.ExitStance => false,          // Can't exit stance multiple times
            CardEffectType.EnterStance => false,         // Entering stance is binary
            CardEffectType.ApplyStun => false,           // Stun duration doesn't scale well
            CardEffectType.RaiseCriticalChance => false, // Percentage scaling would be weird
            CardEffectType.RedirectNextAttack => false,  // Targeting is not scalable
            CardEffectType.Siphon => false,              // Effect transfer is binary
            CardEffectType.Revenge => false,             // Implemented as conditional, not direct effect
            CardEffectType.Corrupt => false,             // Conversion is binary
            CardEffectType.Mimic => false,               // Copies exact effect
            CardEffectType.HealthSwap => false,          // Health values are absolute

            
            _ => false // Default to no scaling for unknown effects
        };
    }
    
    /// <summary>
    /// Check if an effect type is considered negative
    /// </summary>
    private bool IsNegativeEffect(CardEffectType effectType)
    {
        return effectType switch
        {
            CardEffectType.ApplyWeak => true,
            CardEffectType.ApplyBreak => true,
            CardEffectType.ApplyBurn => true,
            CardEffectType.ApplyStun => true,
            CardEffectType.ApplyCurse => true,
            CardEffectType.Siphon => true,               // Steals from target
            CardEffectType.Corrupt => true,              // Corrupts target's effects

            _ => false
        };
    }
    
    /// <summary>
    /// Helper method to check if an effect type is a status effect
    /// </summary>
    private bool IsStatusEffect(CardEffectType effectType)
    {
        return effectType switch
        {
            CardEffectType.ApplyBreak => true,
            CardEffectType.ApplyWeak => true,
            CardEffectType.ApplyBurn => true,
            CardEffectType.ApplySalve => true,
            CardEffectType.ApplyThorns => true,
            CardEffectType.ApplyShield => true,

            CardEffectType.ApplyStun => true,

            CardEffectType.ApplyStrength => true,
            CardEffectType.ApplyCurse => true,
            _ => false
        };
    }

    /// <summary>
    /// Validate this card configuration and log any issues (for use in Inspector)
    /// </summary>
    [ContextMenu("Validate Card Configuration")]
    public void ValidateInEditor()
    {
        if (ValidateCardConfiguration(out List<string> errors))
        {
            Debug.Log($"✅ Card '{_cardName}' configuration is valid!");
        }
        else
        {
            Debug.LogError($"❌ Card '{_cardName}' has configuration errors:");
            foreach (string error in errors)
            {
                Debug.LogError($"  • {error}");
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS FOR CARD SETUP
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Quick setup for a basic damage card
    /// </summary>
    public void SetupBasicDamageCard(string name, int damage, int cost, CardTargetType target = CardTargetType.Opponent)
    {
        _cardName = name;
        _description = $"Deal {damage} damage";
        _cardType = CardType.Attack;
        _energyCost = cost;
        
        if (_effects == null) _effects = new List<CardEffect>();
        _effects.Clear();
        _effects.Add(new CardEffect
        {
            effectType = CardEffectType.Damage,
            amount = damage,
            targetType = target,
            duration = 0
        });
    }

    /// <summary>
    /// Quick setup for a basic heal card
    /// </summary>
    public void SetupBasicHealCard(string name, int healing, int cost, CardTargetType target = CardTargetType.Self)
    {
        _cardName = name;
        _description = $"Heal {healing} health";
        _cardType = CardType.Skill;
        _energyCost = cost;
        
        if (_effects == null) _effects = new List<CardEffect>();
        _effects.Clear();
        _effects.Add(new CardEffect
        {
            effectType = CardEffectType.Heal,
            amount = healing,
            targetType = target,
            duration = 0
        });
    }

    /// <summary>
    /// Quick setup for a status effect card
    /// </summary>
    public void SetupStatusCard(string name, CardEffectType statusType, int potency, int duration, int cost)
    {
        _cardName = name;
        _description = GetStatusDescription(statusType, potency, duration);
        _cardType = CardType.Skill;
        _energyCost = cost;
        
        if (_effects == null) _effects = new List<CardEffect>();
        _effects.Clear();
        _effects.Add(new CardEffect
        {
            effectType = statusType,
            amount = potency,
            targetType = GetDefaultTargetForStatus(statusType),
            duration = duration
        });
    }

    /// <summary>
    /// Add an effect to this card
    /// </summary>
    public void AddEffect(CardEffectType effectType, int amount, CardTargetType target = CardTargetType.Self, int duration = 0)
    {
        if (_effects == null) _effects = new List<CardEffect>();
        
        _effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = target,
            duration = duration
        });
    }

    /// <summary>
    /// Make this card build combo when played
    /// </summary>
    public void MakeComboCard()
    {
        _buildsCombo = true;
        _cardType = CardType.Combo;
    }

    /// <summary>
    /// Make this card require combo to be played
    /// </summary>
    public void RequireCombo(int comboAmount = 1)
    {
        _requiresCombo = true;
        _requiredComboAmount = comboAmount;
        _cardType = CardType.Finisher;
    }

    /// <summary>
    /// Make this card change stance when played
    /// </summary>
    public void ChangeStance(StanceType newStance)
    {
        _changesStance = true;
        _newStance = newStance;
    }

    /// <summary>
    /// Set the category of this card (Starter, Draftable, or Upgraded)
    /// </summary>
    public void SetCardCategory(CardCategory category)
    {
        _cardCategory = category;
    }
    
    /// <summary>
    /// Setup upgrade conditions for this card
    /// </summary>
    public void SetupUpgrade(CardData upgradedVersion, UpgradeConditionType conditionType, int requiredValue, 
        UpgradeComparisonType comparisonType = UpgradeComparisonType.GreaterThanOrEqual, bool upgradeAllCopies = false)
    {
        _canUpgrade = true;
        _upgradedVersion = upgradedVersion;
        _upgradeConditionType = conditionType;
        _upgradeRequiredValue = requiredValue;
        _upgradeComparisonType = comparisonType;
        _upgradeAllCopies = upgradeAllCopies;
    }

    /// <summary>
    /// Add a conditional effect to this card with OR logic (alternative replaces main effect)
    /// </summary>
    public void AddConditionalEffectOR(CardEffectType mainEffectType, int mainAmount, CardTargetType target, 
        ConditionalType conditionType, int conditionValue, CardEffectType altEffectType, int altAmount)
    {
        if (_effects == null) _effects = new List<CardEffect>();
        
        _effects.Add(new CardEffect
        {
            effectType = mainEffectType,
            amount = mainAmount,
            targetType = target,
            duration = 0,
            conditionType = conditionType,
            conditionValue = conditionValue,
            hasAlternativeEffect = true,
            alternativeEffectType = altEffectType,
            alternativeEffectAmount = altAmount,
            alternativeLogic = AlternativeEffectLogic.Replace
        });
    }

    /// <summary>
    /// Add a conditional effect to this card with AND logic (alternative adds to main effect)
    /// </summary>
    public void AddConditionalEffectAND(CardEffectType mainEffectType, int mainAmount, CardTargetType target, 
        ConditionalType conditionType, int conditionValue, CardEffectType bonusEffectType, int bonusAmount)
    {
        if (_effects == null) _effects = new List<CardEffect>();
        
        _effects.Add(new CardEffect
        {
            effectType = mainEffectType,
            amount = mainAmount,
            targetType = target,
            duration = 0,
            conditionType = conditionType,
            conditionValue = conditionValue,
            hasAlternativeEffect = true,
            alternativeEffectType = bonusEffectType,
            alternativeEffectAmount = bonusAmount,
            alternativeLogic = AlternativeEffectLogic.Additional
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // PROCEDURAL GENERATION SETTERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Set card ID (for procedural generation)
    /// </summary>
    public void SetCardId(int cardId)
    {
        _cardId = cardId;
    }

    /// <summary>
    /// Set card name (for procedural generation)
    /// </summary>
    public void SetCardName(string cardName)
    {
        _cardName = cardName;
    }

    /// <summary>
    /// Set card description (for procedural generation)
    /// </summary>
    public void SetDescription(string description)
    {
        _description = description;
    }

    /// <summary>
    /// Set card rarity (for procedural generation)
    /// </summary>
    public void SetRarity(CardRarity rarity)
    {
        _rarity = rarity;
    }

    /// <summary>
    /// Set energy cost (for procedural generation)
    /// </summary>
    public void SetEnergyCost(int energyCost)
    {
        _energyCost = energyCost;
    }

    /// <summary>
    /// Set card type (for procedural generation)
    /// </summary>
    public void SetCardType(CardType cardType)
    {
        _cardType = cardType;
    }

    /// <summary>
    /// Set initiative (for procedural generation)
    /// </summary>
    public void SetInitiative(int initiative)
    {
        _initiative = initiative;
    }

    /// <summary>
    /// Set effects list (for procedural generation)
    /// </summary>
    public void SetEffects(List<CardEffect> effects)
    {
        _effects = effects ?? new List<CardEffect>();
    }

    /// <summary>
    /// Set upgrade properties (for procedural generation)
    /// </summary>
    public void SetUpgradeProperties(bool canUpgrade, UpgradeConditionType conditionType, int requiredValue, UpgradeComparisonType comparisonType)
    {
        _canUpgrade = canUpgrade;
        _upgradeConditionType = conditionType;
        _upgradeRequiredValue = requiredValue;
        _upgradeComparisonType = comparisonType;
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get list of valid target types for this card
    /// </summary>
    public List<CardTargetType> GetValidTargetTypes()
    {
        List<CardTargetType> validTargets = new List<CardTargetType>();
        
        // Collect all unique target types from all effects
        if (HasEffects)
        {
            foreach (var effect in _effects)
            {
                if (!validTargets.Contains(effect.targetType))
                {
                    validTargets.Add(effect.targetType);
                }
            }
        }
        
        return validTargets;
    }

    /// <summary>
    /// Get the primary target type for this card
    /// </summary>
    public CardTargetType GetEffectiveTargetType()
    {
        return HasEffects ? _effects[0].targetType : CardTargetType.Opponent;
    }

    /// <summary>
    /// Check if this card can target the specified type
    /// </summary>
    public bool CanTarget(CardTargetType targetType)
    {
        return GetValidTargetTypes().Contains(targetType);
    }

    /// <summary>
    /// Check if this card can be played with the current combo state
    /// </summary>
    public bool CanPlayWithCombo(int comboCount)
    {
        if (!_requiresCombo) return true;
        return comboCount >= _requiredComboAmount;
    }

    private string GetStatusDescription(CardEffectType statusType, int potency, int duration)
    {
        switch (statusType)
        {
            case CardEffectType.ApplyWeak:
                return $"Apply Weak for {duration} turns";
            case CardEffectType.ApplyBreak:
                return $"Apply Break for {duration} turns";
            case CardEffectType.ApplyBurn:
                return $"Apply {potency} Burn";
            case CardEffectType.ApplySalve:
                return $"Apply {potency} Salve";
            case CardEffectType.ApplyThorns:
                return $"Apply {potency} Thorns until your next turn";
            case CardEffectType.ApplyShield:
                return $"Gain {potency} Shield";
            case CardEffectType.ApplyStun:
                return $"Stun for {duration} turns";
            case CardEffectType.ApplyStrength:
                return $"Gain {potency} Strength";
            case CardEffectType.ApplyCurse:
                return $"Apply {potency} Curse";
            default:
                return $"Apply {statusType}";
        }
    }

    private CardTargetType GetDefaultTargetForStatus(CardEffectType statusType)
    {
        switch (statusType)
        {
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyBurn:
            case CardEffectType.ApplyStun:
            case CardEffectType.ApplyCurse:
                return CardTargetType.Opponent;
            case CardEffectType.ApplySalve:
            case CardEffectType.ApplyThorns:
            case CardEffectType.ApplyShield:
            case CardEffectType.RaiseCriticalChance:
            case CardEffectType.ApplyStrength:
                return CardTargetType.Self;
            default:
                return CardTargetType.Opponent;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// CARD EFFECT STRUCTURES (moved from CardEnums.cs)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Unified card effect structure - replaces MultiEffect, ConditionalEffect, and main effect
/// </summary>
[System.Serializable]
public class CardEffect
{
    [Header("═══ EFFECT CONFIGURATION ═══")]
    [Tooltip("What this effect does")]
    public CardEffectType effectType = CardEffectType.Damage;
    
    [ShowIfAny("effectType", 
        (int)CardEffectType.Damage, 
        (int)CardEffectType.Heal, 
        (int)CardEffectType.ApplyBurn, 
        (int)CardEffectType.ApplySalve, 
        (int)CardEffectType.ApplyShield,
        (int)CardEffectType.ApplyStrength,
        (int)CardEffectType.ApplyCurse,
        (int)CardEffectType.ApplyThorns,
        (int)CardEffectType.RaiseCriticalChance,
        (int)CardEffectType.Amplify,
        (int)CardEffectType.RedirectNextAttack)]

    [Tooltip("Base power/amount of the effect")]
    public int amount = 3;
    
    [ShowIfAny("effectType", 
        (int)CardEffectType.ApplyWeak, 
        (int)CardEffectType.ApplyBreak, 
        (int)CardEffectType.ApplyStun, 
        (int)CardEffectType.RaiseCriticalChance)]
    [Tooltip("Duration for status effects (turns to last)")]
    public int duration = 3;
    
    [Header("═══ TARGETING ═══")]
    [Tooltip("Who this effect targets")]
    public CardTargetType targetType = CardTargetType.Opponent;
    

    
    [Header("═══ CONDITIONAL TRIGGER (Optional) ═══")]
    [Tooltip("This effect only triggers if a condition is met")]
    public ConditionalType conditionType = ConditionalType.None;
    
    [ShowIfAny("conditionType", 
        (int)ConditionalType.IfTargetHealthBelow,
        (int)ConditionalType.IfTargetHealthAbove,
        (int)ConditionalType.IfSourceHealthBelow,
        (int)ConditionalType.IfSourceHealthAbove,
        (int)ConditionalType.IfCardsInHand,
        (int)ConditionalType.IfCardsInDeck,
        (int)ConditionalType.IfCardsInDiscard,
        (int)ConditionalType.IfTimesPlayedThisFight,
        (int)ConditionalType.IfDamageTakenThisFight,
        (int)ConditionalType.IfDamageTakenLastRound,
        (int)ConditionalType.IfHealingReceivedThisFight,
        (int)ConditionalType.IfHealingReceivedLastRound,
        (int)ConditionalType.IfPerfectionStreak,
        (int)ConditionalType.IfComboCount,
        (int)ConditionalType.IfZeroCostCardsThisTurn,
        (int)ConditionalType.IfZeroCostCardsThisFight,
        (int)ConditionalType.IfInStance,
        (int)ConditionalType.IfLastCardType,
        (int)ConditionalType.IfEnergyRemaining)]
    [Tooltip("Value to compare against for the condition (e.g., 50 for 'if health above 50')")]
    public int conditionValue = 0;
    
    [ShowIfAny("conditionType", 
        (int)ConditionalType.IfTargetHealthBelow,
        (int)ConditionalType.IfTargetHealthAbove,
        (int)ConditionalType.IfSourceHealthBelow,
        (int)ConditionalType.IfSourceHealthAbove,
        (int)ConditionalType.IfCardsInHand,
        (int)ConditionalType.IfCardsInDeck,
        (int)ConditionalType.IfCardsInDiscard,
        (int)ConditionalType.IfTimesPlayedThisFight,
        (int)ConditionalType.IfDamageTakenThisFight,
        (int)ConditionalType.IfDamageTakenLastRound,
        (int)ConditionalType.IfHealingReceivedThisFight,
        (int)ConditionalType.IfHealingReceivedLastRound,
        (int)ConditionalType.IfPerfectionStreak,
        (int)ConditionalType.IfComboCount,
        (int)ConditionalType.IfZeroCostCardsThisTurn,
        (int)ConditionalType.IfZeroCostCardsThisFight,
        (int)ConditionalType.IfInStance,
        (int)ConditionalType.IfLastCardType,
        (int)ConditionalType.IfEnergyRemaining)]
    [Tooltip("Alternative effect if condition is not met")]
    public bool hasAlternativeEffect = false;
    
    [ShowIfAny("conditionType", 
        (int)ConditionalType.IfTargetHealthBelow,
        (int)ConditionalType.IfTargetHealthAbove,
        (int)ConditionalType.IfSourceHealthBelow,
        (int)ConditionalType.IfSourceHealthAbove,
        (int)ConditionalType.IfCardsInHand,
        (int)ConditionalType.IfCardsInDeck,
        (int)ConditionalType.IfCardsInDiscard,
        (int)ConditionalType.IfTimesPlayedThisFight,
        (int)ConditionalType.IfDamageTakenThisFight,
        (int)ConditionalType.IfDamageTakenLastRound,
        (int)ConditionalType.IfHealingReceivedThisFight,
        (int)ConditionalType.IfHealingReceivedLastRound,
        (int)ConditionalType.IfPerfectionStreak,
        (int)ConditionalType.IfComboCount,
        (int)ConditionalType.IfZeroCostCardsThisTurn,
        (int)ConditionalType.IfZeroCostCardsThisFight,
        (int)ConditionalType.IfInStance,
        (int)ConditionalType.IfLastCardType,
        (int)ConditionalType.IfEnergyRemaining)]
    [ConditionalField("hasAlternativeEffect", true, false)]
    [Tooltip("How alternative effect interacts with main effect")]
    public AlternativeEffectLogic alternativeLogic = AlternativeEffectLogic.Replace;
    
    [ShowIfAny("conditionType", 
        (int)ConditionalType.IfTargetHealthBelow,
        (int)ConditionalType.IfTargetHealthAbove,
        (int)ConditionalType.IfSourceHealthBelow,
        (int)ConditionalType.IfSourceHealthAbove,
        (int)ConditionalType.IfCardsInHand,
        (int)ConditionalType.IfCardsInDeck,
        (int)ConditionalType.IfCardsInDiscard,
        (int)ConditionalType.IfTimesPlayedThisFight,
        (int)ConditionalType.IfDamageTakenThisFight,
        (int)ConditionalType.IfDamageTakenLastRound,
        (int)ConditionalType.IfHealingReceivedThisFight,
        (int)ConditionalType.IfHealingReceivedLastRound,
        (int)ConditionalType.IfPerfectionStreak,
        (int)ConditionalType.IfComboCount,
        (int)ConditionalType.IfZeroCostCardsThisTurn,
        (int)ConditionalType.IfZeroCostCardsThisFight,
        (int)ConditionalType.IfInStance,
        (int)ConditionalType.IfLastCardType,
        (int)ConditionalType.IfEnergyRemaining)]
    [ConditionalField("hasAlternativeEffect", true, false)]
    [Tooltip("Effect to use if condition fails")]
    public CardEffectType alternativeEffectType = CardEffectType.Damage;
    
    [ShowIfAny("conditionType", 
        (int)ConditionalType.IfTargetHealthBelow,
        (int)ConditionalType.IfTargetHealthAbove,
        (int)ConditionalType.IfSourceHealthBelow,
        (int)ConditionalType.IfSourceHealthAbove,
        (int)ConditionalType.IfCardsInHand,
        (int)ConditionalType.IfCardsInDeck,
        (int)ConditionalType.IfCardsInDiscard,
        (int)ConditionalType.IfTimesPlayedThisFight,
        (int)ConditionalType.IfDamageTakenThisFight,
        (int)ConditionalType.IfDamageTakenLastRound,
        (int)ConditionalType.IfHealingReceivedThisFight,
        (int)ConditionalType.IfHealingReceivedLastRound,
        (int)ConditionalType.IfPerfectionStreak,
        (int)ConditionalType.IfComboCount,
        (int)ConditionalType.IfZeroCostCardsThisTurn,
        (int)ConditionalType.IfZeroCostCardsThisFight,
        (int)ConditionalType.IfInStance,
        (int)ConditionalType.IfLastCardType,
        (int)ConditionalType.IfEnergyRemaining)]
    [ConditionalField("hasAlternativeEffect", true, false)]
    [Tooltip("Amount for alternative effect")]
    public int alternativeEffectAmount = 1;
    
    [Header("═══ SCALING (Optional) ═══")]
    [Tooltip("This effect scales with game state")]
    public ScalingType scalingType = ScalingType.None;
    
    [ShowIfAny("scalingType", 
        (int)ScalingType.ZeroCostCardsThisTurn,
        (int)ScalingType.ZeroCostCardsThisFight,
        (int)ScalingType.CardsPlayedThisTurn,
        (int)ScalingType.CardsPlayedThisFight,
        (int)ScalingType.DamageDealtThisTurn,
        (int)ScalingType.DamageDealtThisFight,
        (int)ScalingType.CurrentHealth,
        (int)ScalingType.MissingHealth,
        (int)ScalingType.ComboCount,
        (int)ScalingType.HandSize)]
    [Tooltip("How much to multiply the scaling value")]
    public float scalingMultiplier = 1.0f;
    
    [ShowIfAny("scalingType", 
        (int)ScalingType.ZeroCostCardsThisTurn,
        (int)ScalingType.ZeroCostCardsThisFight,
        (int)ScalingType.CardsPlayedThisTurn,
        (int)ScalingType.CardsPlayedThisFight,
        (int)ScalingType.DamageDealtThisTurn,
        (int)ScalingType.DamageDealtThisFight,
        (int)ScalingType.CurrentHealth,
        (int)ScalingType.MissingHealth,
        (int)ScalingType.ComboCount,
        (int)ScalingType.HandSize)]
    [Tooltip("Maximum value this effect can scale to")]
    public int maxScaling = 10;

    [Header("═══ STANCE EXIT (Optional) ═══")]
    [Tooltip("If true, this effect will also exit the current stance when triggered")]
    public bool shouldExitStance = false;
    
    [Header("═══ VISUAL EFFECTS (Optional) ═══")]
    [Tooltip("How this effect's visual animation should behave")]
    public EffectAnimationBehavior animationBehavior = EffectAnimationBehavior.Auto;
    
    [ShowIfAny("animationBehavior", 
        (int)EffectAnimationBehavior.ProjectileFromSource,
        (int)EffectAnimationBehavior.BeamToTarget,
        (int)EffectAnimationBehavior.AreaEffect)]
    [Tooltip("Name of custom effect from EffectAnimationManager's database (overrides defaults)")]
    public string customEffectName;
    
    [ShowIfAny("animationBehavior", 
        (int)EffectAnimationBehavior.ProjectileFromSource,
        (int)EffectAnimationBehavior.BeamToTarget,
        (int)EffectAnimationBehavior.AreaEffect)]
    [Tooltip("How long the effect takes to travel/play (0 = use manager default)")]
    public float customDuration = 0f;
    
    [ShowIfAny("animationBehavior", 
        (int)EffectAnimationBehavior.InstantOnTarget,
        (int)EffectAnimationBehavior.ProjectileFromSource,
        (int)EffectAnimationBehavior.BeamToTarget)]
    [Tooltip("Custom sound effect name to play with this effect")]
    public string customSoundEffectName;
    
    [Tooltip("Delay before this effect's animation plays (useful for sequencing multiple effects)")]
    public float animationDelay = 0f;
    
    [Header("═══ FINISHING ANIMATIONS ═══")]
    [ShowIfAny("animationBehavior", 
        (int)EffectAnimationBehavior.ProjectileFromSource,
        (int)EffectAnimationBehavior.BeamToTarget,
        (int)EffectAnimationBehavior.InstantOnTarget)]
    [Tooltip("Play a finishing animation on target after the main effect")]
    public bool hasFinishingAnimation = false;
    
    [ConditionalField("hasFinishingAnimation", true, true)]
    [Tooltip("Custom finishing animation name to use from EffectAnimationManager database")]
    public string finishingAnimationName;
    
    [ConditionalField("hasFinishingAnimation", true, true)]
    [Tooltip("Custom sound effect name to play with the finishing animation")]
    public string finishingSoundEffectName;
    
    [ConditionalField("hasFinishingAnimation", true, true)]
    [Tooltip("Delay before the finishing animation plays after main effect reaches target")]
    [Range(0f, 2f)]
    public float finishingAnimationDelay = 0.1f;
    
    [ShowIfAny("animationBehavior", 
        (int)EffectAnimationBehavior.InstantOnTarget,
        (int)EffectAnimationBehavior.OnSourceOnly)]
    [Tooltip("Custom particle prefab for instant effects (plays directly on target/source)")]
    public GameObject instantEffectPrefab;
}

/// <summary>
/// Data structure for card sequencing requirements
/// </summary>
[System.Serializable]
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
/// Enhanced tracking data for various card mechanics
/// </summary>
[System.Serializable]
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
    
    [Header("Advanced Tracking - This Fight")]
    public int timesDrawnThisFight; // How many times drawn
    public int timesHeldAtTurnEnd; // Times in hand at end of turn
    public int timesDiscardedManually; // Manual discards
    public int timesFinalCardInHand; // Times as last card in hand
    public int timesPlayedBackToBack; // Played after same card type
    public int timesOnlyCardPlayedInTurn; // Only card played that turn
    
    [Header("Advanced Tracking - Lifetime")]
    public int timesDrawnLifetime; // Total times drawn across all fights
    public int timesHeldAtTurnEndLifetime; // Total times held at turn end
    public int timesDiscardedManuallyLifetime; // Total manual discards
    public int timesFinalCardInHandLifetime; // Total times as final card
    public int timesPlayedBackToBackLifetime; // Total back-to-back plays
    public int timesOnlyCardPlayedInTurnLifetime; // Total solo plays
}

// ═══════════════════════════════════════════════════════════════
// LEGACY SUPPORT STRUCTURES (for backward compatibility)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Legacy data structure for scaling effects (for backward compatibility)
/// </summary>
[System.Serializable]
public class ScalingEffect
{
    [Header("Scaling Configuration")]
    public ScalingType scalingType;
    public float scalingMultiplier = 1.0f;
    public int baseAmount;
    public int maxScaling = 999; // Cap for scaling
    
    [Header("Effect")]
    public CardEffectType effectType;
}

/// <summary>
/// Legacy data structure for conditional effects (for backward compatibility)
/// </summary>
[System.Serializable]
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
    
    [Header("Alternative Effect if Condition Not Met")]
    public bool hasAlternativeEffect;
    public CardEffectType alternativeEffectType;
    public int alternativeEffectAmount;
    public int alternativeEffectDuration;
    
    [Header("Scaling")]
    public bool useScaling;
    public ScalingEffect scalingEffect;
}

// ═══════════════════════════════════════════════════════════════
// VISUAL EFFECT ENUMS
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Defines how a card effect's visual animation should behave
/// </summary>
public enum EffectAnimationBehavior
{
    [Tooltip("Automatically determine based on effect type and card keywords")]
    Auto,
    
    [Tooltip("No visual effect animation (silent effect)")]
    None,
    
    [Tooltip("Effect plays instantly on target without projectile animation")]
    InstantOnTarget,
    
    [Tooltip("Projectile/effect animates from source to target")]
    ProjectileFromSource,
    
    [Tooltip("Effect plays on source entity only")]
    OnSourceOnly,
    
    [Tooltip("Area effect that affects multiple targets simultaneously")]
    AreaEffect,
    
    [Tooltip("Beam or continuous effect between source and target")]
    BeamToTarget
}

/// <summary>
/// Legacy data structure for multi-effects (for backward compatibility)
/// </summary>
[System.Serializable]
public class MultiEffect
{
    public CardEffectType effectType;
    public int amount;
    public int duration;
    public CardTargetType targetType; // Can override the main card's target type
    
    [Header("Scaling")]
    public bool useScaling;
    public ScalingEffect scalingEffect;
} 