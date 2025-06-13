using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR

/// <summary>
/// General-purpose factory class for creating cards programmatically.
/// Provides utilities for card creation and the fluent CardDataBuilder.
/// </summary>
public static class CardFactory
{
    private const string CARD_DATA_PATH = "Assets/CardData/";
    private static int nextCardId = 10000; // Start high to avoid conflicts
    
    /// <summary>
    /// Creates a new CardData asset with basic properties set
    /// </summary>
    public static CardDataBuilder CreateCard(string cardName, string description, CardType cardType, int energyCost, string subPath = "")
    {
        string fullPath = CARD_DATA_PATH + subPath;
        CreateDirectoryIfNotExists(fullPath);
        
        CardData cardData = ScriptableObject.CreateInstance<CardData>();
        
        // Set basic properties using reflection
        SetPrivateField(cardData, "_cardId", nextCardId++);
        SetPrivateField(cardData, "_cardName", cardName);
        SetPrivateField(cardData, "_description", description);
        SetPrivateField(cardData, "_cardType", cardType);
        SetPrivateField(cardData, "_energyCost", energyCost);
        
        // Initialize lists
        SetPrivateField(cardData, "_effects", new List<CardEffect>());
        SetPrivateField(cardData, "_persistentEffects", new List<PersistentFightEffect>());
        
        // Enable relevant tracking
        SetPrivateField(cardData, "_trackPlayCount", true);
        SetPrivateField(cardData, "_trackDamageHealing", true);
        
        // Save as asset
        string assetPath = fullPath + cardName + ".asset";
        AssetDatabase.CreateAsset(cardData, assetPath);
        

        return new CardDataBuilder(cardData);
    }
    
    /// <summary>
    /// Creates directory if it doesn't exist
    /// </summary>
    public static void CreateDirectoryIfNotExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
    
    /// <summary>
    /// Clears all cards in a specific directory
    /// </summary>
    public static void ClearCards(string subPath)
    {
        string fullPath = CARD_DATA_PATH + subPath;
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
            AssetDatabase.Refresh();
    
        }
    }
    
    /// <summary>
    /// Sets a private field value using reflection
    /// </summary>
    public static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogWarning($"Field {fieldName} not found on {obj.GetType()}");
        }
    }
}

/// <summary>
/// Builder class for fluent card creation
/// </summary>
public class CardDataBuilder
{
    private CardData cardData;
    private List<CardEffect> effects;
    private List<PersistentFightEffect> persistentEffects;
    
    public CardDataBuilder(CardData cardData)
    {
        this.cardData = cardData;
        this.effects = new List<CardEffect>();
        this.persistentEffects = new List<PersistentFightEffect>();
    }
    
    public CardDataBuilder AddEffect(CardEffectType effectType, int amount, CardTargetType targetType, int duration = 0, ElementalType elementalType = ElementalType.None)
    {
        effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = targetType,
            duration = duration,
            elementalType = elementalType,
            conditionType = ConditionalType.None,
            scalingType = ScalingType.None
        });
        
        UpdateCardData();
        return this;
    }
    
    public CardDataBuilder AddScalingEffect(CardEffectType effectType, int baseAmount, CardTargetType targetType, 
        ScalingType scalingType, float scalingMultiplier, int maxScaling, ElementalType elementalType = ElementalType.None)
    {
        effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = baseAmount,
            targetType = targetType,
            elementalType = elementalType,
            scalingType = scalingType,
            scalingMultiplier = scalingMultiplier,
            maxScaling = maxScaling,
            conditionType = ConditionalType.None
        });
        
        UpdateCardData();
        return this;
    }
    
    public CardDataBuilder AddConditionalEffect(CardEffectType effectType, int amount, CardTargetType targetType, 
        ConditionalType conditionType, int conditionValue, CardEffectType alternativeEffectType = CardEffectType.Damage, int alternativeAmount = 0, 
        AlternativeEffectLogic logic = AlternativeEffectLogic.Replace)
    {
        effects.Add(new CardEffect
        {
            effectType = effectType,
            amount = amount,
            targetType = targetType,
            conditionType = conditionType,
            conditionValue = conditionValue,
            hasAlternativeEffect = alternativeEffectType != CardEffectType.Damage || alternativeAmount > 0,
            alternativeEffectType = alternativeEffectType,
            alternativeEffectAmount = alternativeAmount,
            alternativeLogic = logic,
            scalingType = ScalingType.None,
            elementalType = ElementalType.None
        });
        
        UpdateCardData();
        return this;
    }
    
    public CardDataBuilder SetCombo(bool buildsCombo)
    {
        CardFactory.SetPrivateField(cardData, "_buildsCombo", buildsCombo);
        if (buildsCombo)
        {
            CardFactory.SetPrivateField(cardData, "_cardType", CardType.Combo);
        }
        return this;
    }
    
    public CardDataBuilder RequireCombo(int comboAmount = 1)
    {
        CardFactory.SetPrivateField(cardData, "_requiresCombo", true);
        CardFactory.SetPrivateField(cardData, "_requiredComboAmount", comboAmount);
        CardFactory.SetPrivateField(cardData, "_cardType", CardType.Finisher);
        return this;
    }
    
    public CardDataBuilder SetStance(StanceType stanceType)
    {
        CardFactory.SetPrivateField(cardData, "_changesStance", true);
        CardFactory.SetPrivateField(cardData, "_newStance", stanceType);
        return this;
    }
    
    public CardDataBuilder AddPersistentEffect(string effectName, CardEffectType effectType, int potency, bool lastEntireFight = true)
    {
        persistentEffects.Add(new PersistentFightEffect
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
        
        CardFactory.SetPrivateField(cardData, "_persistentEffects", persistentEffects);
        EditorUtility.SetDirty(cardData);
        return this;
    }
    
    public CardDataBuilder SetFlexibleTargeting(bool canTargetSelf, bool canTargetAllies)
    {
        CardFactory.SetPrivateField(cardData, "_canAlsoTargetSelf", canTargetSelf);
        CardFactory.SetPrivateField(cardData, "_canAlsoTargetAllies", canTargetAllies);
        return this;
    }
    
    private void UpdateCardData()
    {
        CardFactory.SetPrivateField(cardData, "_effects", effects);
        EditorUtility.SetDirty(cardData);
    }
}

#endif 