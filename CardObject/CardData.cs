using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New CardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("═══ BASIC CARD INFO ═══")]
    [SerializeField] private int _cardId = 0;
    [SerializeField] private string _cardName = "New Card";
    [TextArea(2, 4)]
    [SerializeField] private string _description = "Card Description";
    [SerializeField] private Sprite _cardArtwork;

    [Header("═══ CORE MECHANICS ═══")]
    [Tooltip("What type of card this is (affects sequencing and AI)")]
    [SerializeField] private CardType _cardType = CardType.Attack;
    
    [Tooltip("Energy cost to play this card")]
    [SerializeField] private int _energyCost = 2;

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

    [Header("═══ STANCE & PERSISTENT EFFECTS ═══")]
    [Tooltip("This card changes combat stance")]
    [SerializeField] private bool _changesStance = false;
    
    [ConditionalField("_changesStance", true, true)]
    [SerializeField] private StanceType _newStance = StanceType.Aggressive;
    
    [Tooltip("Effects that last the entire fight")]
    [SerializeField] private List<PersistentFightEffect> _persistentEffects = new List<PersistentFightEffect>();

    [Header("═══ ADVANCED TARGETING ═══")]
    [Tooltip("Can target self even if main target is different")]
    [SerializeField] private bool _canAlsoTargetSelf = false;
    
    [Tooltip("Can target allies even if main target is different")]
    [SerializeField] private bool _canAlsoTargetAllies = false;

    [Header("═══ TRACKING & UPGRADES ═══")]
    [SerializeField] private CardData _upgradedVersion;
    
    [Tooltip("Track how many times this card is played")]
    [SerializeField] private bool _trackPlayCount = true;
    
    [Tooltip("Track damage/healing for scaling")]
    [SerializeField] private bool _trackDamageHealing = true;

    // ═══════════════════════════════════════════════════════════════
    // DERIVED PROPERTIES FOR BACKWARD COMPATIBILITY
    // ═══════════════════════════════════════════════════════════════
    
    public int CardId => _cardId;
    public string CardName => _cardName;
    public string Description => _description;
    public Sprite CardArtwork => _cardArtwork;
    public CardType CardType => _cardType;
    public int EnergyCost => _energyCost;
    public CardData UpgradedVersion => _upgradedVersion;
    
    // Legacy properties for backward compatibility
    public CardEffectType EffectType => HasEffects ? _effects[0].effectType : CardEffectType.Damage;
    public CardTargetType TargetType => HasEffects ? _effects[0].targetType : CardTargetType.Opponent;
    public int Amount => HasEffects ? _effects[0].amount : 0;
    public int Duration => HasEffects ? _effects[0].duration : 0;
    public ElementalType ElementalType => HasEffects ? _effects[0].elementalType : ElementalType.None;
    public bool HasComboModifier => _buildsCombo;
    public bool IsFinisher => _requiresCombo;
    
    // Derived properties based on list contents
    public bool HasEffects => _effects != null && _effects.Count > 0;
    public List<CardEffect> Effects => _effects ?? new List<CardEffect>();
    
    public bool ChangesStance => _changesStance;
    public StanceType NewStance => _newStance;
    
    public bool CreatesPersistentEffects => _persistentEffects != null && _persistentEffects.Count > 0;
    public List<PersistentFightEffect> PersistentEffects => _persistentEffects ?? new List<PersistentFightEffect>();
    
    public bool CanAlsoTargetSelf => _canAlsoTargetSelf;
    public bool CanAlsoTargetAllies => _canAlsoTargetAllies;
    
    public bool TrackPlayCount => _trackPlayCount;
    public bool TrackDamageHealing => _trackDamageHealing;

    // Legacy compatibility properties
    public bool IsGlobalEffect => HasEffects && IsGlobalTargetType(_effects[0].targetType);
    public bool AffectAllPlayers => HasEffects && (_effects[0].targetType == CardTargetType.All || _effects[0].targetType == CardTargetType.AllPlayers);
    public bool AffectAllPets => HasEffects && (_effects[0].targetType == CardTargetType.All || _effects[0].targetType == CardTargetType.AllPets);
    public bool IncludeCaster => HasEffects && _effects[0].targetType == CardTargetType.All;
    public bool HasZoneEffect => HasEffects && IsGlobalTargetType(_effects[0].targetType);
    public List<ZoneEffect> ZoneEffects => ConvertToZoneEffects();
    public bool HasMultipleEffects => HasEffects && _effects.Count > 1;
    public List<CardEffect> MultiEffects => Effects;
    public bool HasConditionalBehavior => HasEffects && _effects.Any(e => e.conditionType != ConditionalType.None);
    public bool HasConditionalEffect => HasConditionalBehavior;
    public bool AffectsStance => _changesStance;
    public StanceEffect StanceEffect => ConvertToStanceEffect();
    public bool HasScalingEffect => HasEffects && _effects.Any(e => e.scalingType != ScalingType.None);
    public bool ScalesWithGameState => HasScalingEffect;
    public List<ScalingEffect> ScalingEffects => ConvertToScalingEffects();
    public bool HasPersistentEffect => CreatesPersistentEffects;
    public bool HasSequenceRequirement => _requiresCombo;
    public CardSequenceRequirement SequenceRequirement => ConvertToSequenceRequirement();
    public bool CanTargetSelf => _canAlsoTargetSelf;
    public bool CanTargetAlly => _canAlsoTargetAllies;
    public bool RequiresSpecificTarget => false;
    public CardTargetType DefaultTargetType => TargetType;
    public bool TrackDeckComposition => false;
    public bool TrackPerfection => false;
    public bool TrackZeroCostCards => _energyCost == 0;
    public bool TrackSequencing => _requiresCombo;
    
    // Additional legacy properties for backward compatibility
    public bool HasAdditionalEffects => HasMultipleEffects;
    public List<CardEffect> AdditionalEffects => HasEffects && _effects.Count > 1 ? _effects.Skip(1).ToList() : new List<CardEffect>();
    public ConditionalEffect ConditionalEffect => ConvertToConditionalEffect();

    // ═══════════════════════════════════════════════════════════════
    // SIMPLIFIED HELPER METHODS
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
            duration = 0,
            elementalType = ElementalType.None
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
            duration = 0,
            elementalType = ElementalType.None
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
            duration = duration,
            elementalType = ElementalType.None
        });
    }

    /// <summary>
    /// Add an effect to this card
    /// </summary>
    public void AddEffect(CardEffectType effectType, int amount, CardTargetType target = CardTargetType.Self, int duration = 0)
    {
        if (_effects == null)
            _effects = new List<CardEffect>();
            
        _effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = target,
            duration = duration,
            elementalType = ElementalType.None
        });
    }

    /// <summary>
    /// Legacy method - now just calls AddEffect
    /// </summary>
    public void AddAdditionalEffect(CardEffectType effectType, int amount, CardTargetType target = CardTargetType.Self)
    {
        AddEffect(effectType, amount, target);
    }

    /// <summary>
    /// Add an effect with conditional logic
    /// </summary>
    public void AddConditionalEffect(CardEffectType effectType, int amount, ConditionalType conditionType, int conditionValue, CardTargetType target = CardTargetType.Self)
    {
        if (_effects == null)
            _effects = new List<CardEffect>();
            
        _effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = target,
            conditionType = conditionType,
            conditionValue = conditionValue,
            elementalType = ElementalType.None
        });
    }

    /// <summary>
    /// Add an effect with scaling
    /// </summary>
    public void AddScalingEffect(CardEffectType effectType, int baseAmount, ScalingType scalingType, float multiplier, int maxScaling, CardTargetType target = CardTargetType.Self)
    {
        if (_effects == null)
            _effects = new List<CardEffect>();
            
        _effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = baseAmount,
            targetType = target,
            scalingType = scalingType,
            scalingMultiplier = multiplier,
            maxScaling = maxScaling,
            elementalType = ElementalType.None
        });
    }

    /// <summary>
    /// Make this a combo card
    /// </summary>
    public void MakeComboCard()
    {
        _buildsCombo = true;
        _cardType = CardType.Combo;
    }

    /// <summary>
    /// Make this require combo to play
    /// </summary>
    public void RequireCombo(CardType requiredType = CardType.Combo, bool allowWithActiveCombo = true)
    {
        _requiresCombo = true;
        _cardType = CardType.Finisher;
    }

    /// <summary>
    /// Make this require combo to play
    /// </summary>
    public void RequireCombo(int comboAmount = 1)
    {
        _requiresCombo = true;
        _requiredComboAmount = comboAmount;
        _cardType = CardType.Finisher;
    }

    /// <summary>
    /// Add simple scaling based on a metric - legacy method
    /// </summary>
    public void AddScaling(ScalingType scalingType, float multiplier, int maxBonus = 10)
    {
        if (!HasEffects) return;
        
        // Apply scaling to the first effect
        _effects[0].scalingType = scalingType;
        _effects[0].scalingMultiplier = multiplier;
        _effects[0].maxScaling = _effects[0].amount + maxBonus;
    }

    /// <summary>
    /// Make this card change stance
    /// </summary>
    public void ChangeStance(StanceType newStance)
    {
        _changesStance = true;
        _newStance = newStance;
    }

    /// <summary>
    /// Add a persistent effect that lasts the fight
    /// </summary>
    public void AddPersistentEffect(string effectName, CardEffectType effectType, int potency, bool lastEntireFight = true)
    {
        if (_persistentEffects == null)
            _persistentEffects = new List<PersistentFightEffect>();
            
        _persistentEffects.Add(new PersistentFightEffect
        {
            effectName = effectName,
            effectType = effectType,
            potency = potency,
            triggerInterval = 0,
            lastEntireFight = lastEntireFight,
            turnDuration = lastEntireFight ? 0 : 3,
            requiresStance = false,
            requiredStance = StanceType.None,
            stackable = true
        });
    }

    /// <summary>
    /// Set conditional behavior - legacy method
    /// </summary>
    public void SetConditionalBehavior(ConditionalType conditionType, int conditionValue, CardEffectType effectType, int effectAmount)
    {
        if (_effects == null)
            _effects = new List<CardEffect>();
            
        _effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = effectAmount,
            conditionType = conditionType,
            conditionValue = conditionValue,
            targetType = CardTargetType.Opponent,
            elementalType = ElementalType.None
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    private bool IsGlobalTargetType(CardTargetType targetType)
    {
        return targetType == CardTargetType.All || 
               targetType == CardTargetType.AllEnemies || 
               targetType == CardTargetType.AllAllies ||
               targetType == CardTargetType.AllPlayers ||
               targetType == CardTargetType.AllPets ||
               targetType == CardTargetType.Everyone;
    }

    private bool EffectTypeUsesDuration(CardEffectType effectType)
    {
        return effectType == CardEffectType.ApplyWeak ||
               effectType == CardEffectType.ApplyBreak ||
               effectType == CardEffectType.ApplyThorns ||
               effectType == CardEffectType.ApplyStun ||
               effectType == CardEffectType.ApplyDamageOverTime ||
               effectType == CardEffectType.ApplyHealOverTime ||
               effectType == CardEffectType.RaiseCriticalChance;
    }

    // ═══════════════════════════════════════════════════════════════
    // LEGACY CONVERSION METHODS (for backward compatibility)
    // ═══════════════════════════════════════════════════════════════

    private List<ZoneEffect> ConvertToZoneEffects()
    {
        if (!HasEffects || !IsGlobalTargetType(_effects[0].targetType)) return new List<ZoneEffect>();
        
        var firstEffect = _effects[0];
        return new List<ZoneEffect>
        {
            new ZoneEffect
            {
                effectType = firstEffect.effectType,
                baseAmount = firstEffect.amount,
                duration = firstEffect.duration,
                elementalType = firstEffect.elementalType,
                affectAllPlayers = AffectAllPlayers,
                affectAllPets = AffectAllPets,
                affectCaster = IncludeCaster,
                excludeOpponents = false,
                scalingType = firstEffect.scalingType,
                scalingMultiplier = firstEffect.scalingMultiplier
            }
        };
    }

    private StanceEffect ConvertToStanceEffect()
    {
        if (!_changesStance) return new StanceEffect();
        
        return new StanceEffect
        {
            stanceType = _newStance,
            overridePreviousStance = true,
            damageModifier = GetStanceModifier(_newStance, "damage"),
            defenseModifier = GetStanceModifier(_newStance, "defense"),
            energyModifier = GetStanceModifier(_newStance, "energy"),
            drawModifier = GetStanceModifier(_newStance, "draw"),
            healthModifier = 0,
            grantsThorns = _newStance == StanceType.Guardian,
            thornsAmount = _newStance == StanceType.Guardian ? 1 : 0,
            grantsShield = _newStance == StanceType.Defensive || _newStance == StanceType.Guardian,
            shieldAmount = _newStance == StanceType.Defensive ? 2 : (_newStance == StanceType.Guardian ? 3 : 0),
            enhancesCritical = _newStance == StanceType.Aggressive,
            criticalBonus = _newStance == StanceType.Aggressive ? 15 : 0,
            onTurnStartEffect = CardEffectType.Damage,
            onTurnStartAmount = 0,
            onTurnEndEffect = CardEffectType.Damage,
            onTurnEndAmount = 0
        };
    }

    private CardSequenceRequirement ConvertToSequenceRequirement()
    {
        return new CardSequenceRequirement
        {
            hasSequenceRequirement = _requiresCombo,
            requiredPreviousCardType = CardType.Combo,
            requiresExactPrevious = false,
            requiresAnyInTurn = true,
            allowIfComboActive = true,
            allowIfInStance = false,
            requiredStance = StanceType.None
        };
    }

    private string GetStatusDescription(CardEffectType statusType, int potency, int duration)
    {
        switch (statusType)
        {
            case CardEffectType.ApplyWeak:
                return $"Apply Weak {potency} for {duration} turns";
            case CardEffectType.ApplyBreak:
                return $"Apply Break {potency} for {duration} turns";
            case CardEffectType.ApplyThorns:
                return $"Apply Thorns {potency} for {duration} turns";
            case CardEffectType.ApplyShield:
                return $"Gain {potency} Shield";
            case CardEffectType.ApplyStun:
                return $"Stun for {duration} turns";
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
            case CardEffectType.ApplyStun:
                return CardTargetType.Opponent;
            case CardEffectType.ApplyThorns:
            case CardEffectType.ApplyShield:
            case CardEffectType.RaiseCriticalChance:
                return CardTargetType.Self;
            default:
                return CardTargetType.Opponent;
        }
    }

    private int GetStanceModifier(StanceType stance, string modifierType)
    {
        switch (stance)
        {
            case StanceType.Aggressive:
                return modifierType == "damage" ? 2 : (modifierType == "defense" ? -1 : 0);
            case StanceType.Defensive:
                return modifierType == "defense" ? 2 : 0;
            case StanceType.Focused:
                return modifierType == "energy" ? 1 : (modifierType == "draw" ? 1 : 0);
            case StanceType.Guardian:
                return modifierType == "defense" ? 1 : 0;
            default:
                return 0;
        }
    }

    // Legacy methods for compatibility
    public List<CardTargetType> GetValidTargetTypes()
    {
        List<CardTargetType> validTargets = new List<CardTargetType>();
        if (HasEffects)
            validTargets.Add(_effects[0].targetType);
        
        if (_canAlsoTargetSelf && !validTargets.Contains(CardTargetType.Self))
            validTargets.Add(CardTargetType.Self);
            
        if (_canAlsoTargetAllies && !validTargets.Contains(CardTargetType.Ally))
            validTargets.Add(CardTargetType.Ally);
            
        return validTargets;
    }

    public CardTargetType GetEffectiveTargetType()
    {
        return HasEffects ? _effects[0].targetType : CardTargetType.Opponent;
    }

    public bool CanTarget(CardTargetType targetType)
    {
        return GetValidTargetTypes().Contains(targetType);
    }

    public bool CanPlayWithSequence(CardType lastPlayedType, bool comboActive, StanceType currentStance)
    {
        if (!_requiresCombo) return true;
        return comboActive; // Simple: just need combo to be active
    }

    public bool CanPlayWithSequence(CardType lastPlayedType, int comboCount, StanceType currentStance)
    {
        if (!_requiresCombo) return true;
        return comboCount >= _requiredComboAmount;
    }

    public int GetScalingAmount(ScalingType scalingType, int baseValue, EntityTrackingData trackingData)
    {
        if (!HasEffects) return baseValue;

        foreach (var effect in _effects)
        {
            if (effect.scalingType == scalingType)
            {
                int scalingValue = GetScalingValue(scalingType, trackingData);
                int scaledAmount = effect.amount + Mathf.FloorToInt(scalingValue * effect.scalingMultiplier);
                return Mathf.Min(scaledAmount, effect.maxScaling);
            }
        }

        return baseValue;
    }

    private int GetScalingValue(ScalingType scalingType, EntityTrackingData trackingData)
    {
        switch (scalingType)
        {
            case ScalingType.ZeroCostCardsThisTurn:
                return trackingData.zeroCostCardsThisTurn;
            case ScalingType.ZeroCostCardsThisFight:
                return trackingData.zeroCostCardsThisFight;
            case ScalingType.CardsPlayedThisTurn:
                return trackingData.cardsPlayedThisTurn;
            case ScalingType.CardsPlayedThisFight:
                return trackingData.cardsPlayedThisFight;
            case ScalingType.DamageDealtThisTurn:
                return trackingData.damageDealtLastRound;
            case ScalingType.DamageDealtThisFight:
                return trackingData.damageDealtThisFight;
            case ScalingType.ComboCount:
                return trackingData.comboCount;
            default:
                return 0;
        }
    }

    public bool IsZeroCost => _energyCost == 0;

    public string GetSequenceRequirementText()
    {
        if (!_requiresCombo) return "";
        if (_requiredComboAmount == 1)
            return "Requires: Active combo";
        return $"Requires: {_requiredComboAmount} combo";
    }

    private List<ScalingEffect> ConvertToScalingEffects()
    {
        if (!HasEffects) return new List<ScalingEffect>();
        
        return _effects.Where(e => e.scalingType != ScalingType.None)
                      .Select(e => new ScalingEffect
                      {
                          scalingType = e.scalingType,
                          scalingMultiplier = e.scalingMultiplier,
                          baseAmount = e.amount,
                          maxScaling = e.maxScaling,
                          effectType = e.effectType,
                          elementalType = e.elementalType
                      }).ToList();
    }

    private ConditionalEffect ConvertToConditionalEffect()
    {
        var conditionalEffects = _effects.Where(e => e.conditionType != ConditionalType.None).ToList();
        if (!conditionalEffects.Any()) return new ConditionalEffect();
        
        var firstConditional = conditionalEffects[0];
        return new ConditionalEffect
        {
            conditionType = firstConditional.conditionType,
            conditionValue = firstConditional.conditionValue,
            conditionMet = false, // Always false in data, computed at runtime
            effectType = firstConditional.effectType,
            effectAmount = firstConditional.amount,
            effectDuration = firstConditional.duration,
            elementalType = firstConditional.elementalType,
            hasAlternativeEffect = firstConditional.hasAlternativeEffect,
            alternativeEffectType = firstConditional.alternativeEffectType,
            alternativeEffectAmount = firstConditional.alternativeEffectAmount,
            alternativeEffectDuration = 0,
            useScaling = firstConditional.scalingType != ScalingType.None,
            scalingEffect = firstConditional.scalingType != ScalingType.None ? new ScalingEffect
            {
                scalingType = firstConditional.scalingType,
                scalingMultiplier = firstConditional.scalingMultiplier,
                baseAmount = firstConditional.amount,
                maxScaling = firstConditional.maxScaling,
                effectType = firstConditional.effectType,
                elementalType = firstConditional.elementalType
            } : new ScalingEffect()
        };
    }
}

// ═══════════════════════════════════════════════════════════════
// SIMPLIFIED DATA STRUCTURES
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class AdditionalEffect
{
    public CardEffectType effectType = CardEffectType.Heal;
    public int amount = 2;
    public int duration = 0;
    public ElementalType elementalType = ElementalType.None;
    public CardTargetType targetType = CardTargetType.Self;
} 