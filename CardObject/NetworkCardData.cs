using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Network-serializable representation of CardData for synchronization across clients.
/// Contains essential card data in a format that can be efficiently transmitted over the network.
/// </summary>
[System.Serializable]
public struct NetworkCardData
{
    // Basic card info
    public int cardId;
    public string cardName;
    public string description;
    public CardRarity rarity;
    public CardCategory cardCategory;
    public CardType cardType;
    public int energyCost;
    public int initiative;

    // Core mechanics
    public bool buildsCombo;
    public bool requiresCombo;
    public int requiredComboAmount;
    public bool changesStance;
    public StanceType newStance;

    // Targeting


    // Tracking
    public bool trackPlayCount;
    public bool trackDamageHealing;

    // Upgrade system
    public bool canUpgrade;
    public UpgradeConditionType upgradeConditionType;
    public int upgradeRequiredValue;
    public UpgradeComparisonType upgradeComparisonType;
    public bool upgradeAllCopies;
    public int upgradedVersionId; // Reference to upgraded card by ID

    // Effects (serialized as array for network efficiency)
    public NetworkCardEffect[] effects;

    /// <summary>
    /// Create NetworkCardData from a CardData object
    /// </summary>
    public static NetworkCardData FromCardData(CardData cardData)
    {
        var networkCard = new NetworkCardData
        {
            cardId = cardData.CardId,
            cardName = cardData.CardName,
            description = cardData.Description,
            rarity = cardData.Rarity,
            cardCategory = cardData.CardCategory,
            cardType = cardData.CardType,
            energyCost = cardData.EnergyCost,
            initiative = cardData.Initiative,

            buildsCombo = cardData.BuildsCombo,
            requiresCombo = cardData.RequiresCombo,
            requiredComboAmount = cardData.RequiredComboAmount,
            changesStance = cardData.ChangesStance,
            newStance = cardData.NewStance,



            trackPlayCount = cardData.TrackPlayCount,
            trackDamageHealing = cardData.TrackDamageHealing,

            canUpgrade = cardData.CanUpgrade,
            upgradeConditionType = cardData.UpgradeConditionType,
            upgradeRequiredValue = cardData.UpgradeRequiredValue,
            upgradeComparisonType = cardData.UpgradeComparisonType,
            upgradeAllCopies = cardData.UpgradeAllCopies,
            upgradedVersionId = cardData.UpgradedVersion?.CardId ?? -1,

            effects = cardData.Effects?.Select(e => NetworkCardEffect.FromCardEffect(e)).ToArray() ?? new NetworkCardEffect[0]
        };

        return networkCard;
    }

    /// <summary>
    /// Convert NetworkCardData back to a CardData object
    /// </summary>
    public CardData ToCardData()
    {
        var cardData = ScriptableObject.CreateInstance<CardData>();

        // Set basic properties using public setter methods
        cardData.SetCardId(cardId);
        cardData.SetCardName(cardName);
        cardData.SetDescription(description);
        cardData.SetRarity(rarity);
        cardData.SetCardCategory(cardCategory);
        cardData.SetCardType(cardType);
        cardData.SetEnergyCost(energyCost);
        cardData.SetInitiative(initiative);

        // Convert effects back to CardEffect list
        var cardEffects = effects?.Select(ne => ne.ToCardEffect()).ToList() ?? new List<CardEffect>();
        cardData.SetEffects(cardEffects);

        // Set combo properties
        if (buildsCombo)
            cardData.MakeComboCard();
        if (requiresCombo)
            cardData.RequireCombo(requiredComboAmount);

        // Set stance properties
        if (changesStance)
            cardData.ChangeStance(newStance);

        // Set upgrade properties
        if (canUpgrade)
        {
            cardData.SetUpgradeProperties(canUpgrade, upgradeConditionType, upgradeRequiredValue, upgradeComparisonType);
        }

        // Note: Upgraded version reference will need to be resolved after all cards are created
        // This is handled by the NetworkCardDatabase after all cards are synced

        return cardData;
    }
}

/// <summary>
/// Network-serializable representation of CardEffect
/// </summary>
[System.Serializable]
public struct NetworkCardEffect
{
    public CardEffectType effectType;
    public int amount;
    public CardTargetType targetType;
    public int duration;


    // Conditional effects
    public ConditionalType conditionType;
    public int conditionValue;
    public CardEffectType alternativeEffectType;
    public int alternativeEffectAmount;

    // Scaling
    public ScalingType scalingType;
    public float scalingMultiplier;

    // Animation
    public EffectAnimationBehavior animationBehavior;
    public float animationDelay;

    /// <summary>
    /// Create NetworkCardEffect from a CardEffect object
    /// </summary>
    public static NetworkCardEffect FromCardEffect(CardEffect effect)
    {
        return new NetworkCardEffect
        {
            effectType = effect.effectType,
            amount = effect.amount,
            targetType = effect.targetType,
            duration = effect.duration,

            conditionType = effect.conditionType,
            conditionValue = effect.conditionValue,
            alternativeEffectType = effect.alternativeEffectType,
            alternativeEffectAmount = effect.alternativeEffectAmount,
            scalingType = effect.scalingType,
            scalingMultiplier = effect.scalingMultiplier,
            animationBehavior = effect.animationBehavior,
            animationDelay = effect.animationDelay
        };
    }

    /// <summary>
    /// Convert NetworkCardEffect back to a CardEffect object
    /// </summary>
    public CardEffect ToCardEffect()
    {
        return new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = targetType,
            duration = duration,

            conditionType = conditionType,
            conditionValue = conditionValue,
            alternativeEffectType = alternativeEffectType,
            alternativeEffectAmount = alternativeEffectAmount,
            scalingType = scalingType,
            scalingMultiplier = scalingMultiplier,
            animationBehavior = animationBehavior,
            animationDelay = animationDelay
        };
    }
} 