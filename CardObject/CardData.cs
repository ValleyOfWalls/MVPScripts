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
    // PUBLIC PROPERTIES - CLEAN INTERFACE
    // ═══════════════════════════════════════════════════════════════
    
    public int CardId => _cardId;
    public string CardName => _cardName;
    public string Description => _description;
    public Sprite CardArtwork => _cardArtwork;
    public CardType CardType => _cardType;
    public int EnergyCost => _energyCost;
    public CardData UpgradedVersion => _upgradedVersion;
    
    // Core mechanics
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
    
    // Combo system
    public bool BuildsCombo => _buildsCombo;
    public bool RequiresCombo => _requiresCombo;
    public int RequiredComboAmount => _requiredComboAmount;
    
    // Utility properties
    public bool IsZeroCost => _energyCost == 0;

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
        if (_effects == null) _effects = new List<CardEffect>();
        
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
            elementalType = ElementalType.None,
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
            elementalType = ElementalType.None,
            conditionType = conditionType,
            conditionValue = conditionValue,
            hasAlternativeEffect = true,
            alternativeEffectType = bonusEffectType,
            alternativeEffectAmount = bonusAmount,
            alternativeLogic = AlternativeEffectLogic.Additional
        });
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
        if (HasEffects)
            validTargets.Add(_effects[0].targetType);
        
        if (_canAlsoTargetSelf && !validTargets.Contains(CardTargetType.Self))
            validTargets.Add(CardTargetType.Self);
            
        if (_canAlsoTargetAllies && !validTargets.Contains(CardTargetType.Ally))
            validTargets.Add(CardTargetType.Ally);
            
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
            case CardEffectType.ApplyStun:
            case CardEffectType.ApplyCurse:
                return CardTargetType.Opponent;
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
// LEGACY SUPPORT CLASSES (can be removed if not used elsewhere)
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