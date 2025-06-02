using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR

/// <summary>
/// Factory class for creating comprehensive test cards using the unified CardEffect system.
/// Use this in the Unity Editor to generate test cards for every feature.
/// </summary>
public static class CardFactory
{
    private const string CARD_DATA_PATH = "Assets/CardData/TestCards/";
    private static int nextCardId = 10000; // Start high to avoid conflicts
    
    [MenuItem("Tools/Card Factory/Create All Test Cards")]
    public static void CreateAllTestCards()
    {
        CreateDirectoryIfNotExists();
        
        Debug.Log("=== Creating Comprehensive Test Card Set (Unified System) ===");
        
        // Basic Effect Tests
        CreateBasicEffectCards();
        
        // Status Effect Tests
        CreateStatusEffectCards();
        
        // Target Variation Tests
        CreateTargetVariationCards();
        
        // Scaling Effect Tests
        CreateScalingEffectCards();
        
        // Conditional Effect Tests
        CreateConditionalEffectCards();
        
        // Multi-Effect Tests
        CreateMultiEffectCards();
        
        // Stance System Tests
        CreateStanceSystemCards();
        
        // Persistent Effect Tests
        CreatePersistentEffectCards();
        
        // Card Type & Sequencing Tests
        CreateSequencingCards();
        
        // Elemental Effect Tests
        CreateElementalEffectCards();
        
        // Complex Combination Tests
        CreateComplexCombinationCards();
        
        AssetDatabase.Refresh();
        Debug.Log("=== All Test Cards Created (Unified System) ===");
    }
    
    [MenuItem("Tools/Card Factory/Clear Test Cards")]
    public static void ClearTestCards()
    {
        if (Directory.Exists(CARD_DATA_PATH))
        {
            Directory.Delete(CARD_DATA_PATH, true);
            AssetDatabase.Refresh();
            Debug.Log("Test cards cleared.");
        }
    }
    
    private static void CreateDirectoryIfNotExists()
    {
        if (!Directory.Exists(CARD_DATA_PATH))
        {
            Directory.CreateDirectory(CARD_DATA_PATH);
        }
    }
    
    #region Basic Effect Cards
    
    private static void CreateBasicEffectCards()
    {
        Debug.Log("Creating Basic Effect Cards...");
        
        // Basic Damage
        CreateCard("Test_Damage_Basic", "Deal 3 damage", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent);
        
        // Basic Heal
        CreateCard("Test_Heal_Basic", "Heal 5 health", CardType.Skill, 1)
            .AddEffect(CardEffectType.Heal, 5, CardTargetType.Self);
        
        // Card Draw
        CreateCard("Test_Draw_Cards", "Draw 2 cards", CardType.Skill, 1)
            .AddEffect(CardEffectType.DrawCard, 2, CardTargetType.Self);
        
        // Energy Restore
        CreateCard("Test_Energy_Restore", "Restore 3 energy", CardType.Skill, 0)
            .AddEffect(CardEffectType.RestoreEnergy, 3, CardTargetType.Self);
        
        // Zero Cost Card for scaling tests
        CreateCard("Test_Zero_Cost", "Zero cost card for scaling tests", CardType.Attack, 0)
            .AddEffect(CardEffectType.Damage, 1, CardTargetType.Opponent);
    }
    
    #endregion
    
    #region Status Effect Cards
    
    private static void CreateStatusEffectCards()
    {
        Debug.Log("Creating Status Effect Cards...");
        
        // Break Status
        CreateCard("Test_Break_Status", "Apply Break (reduce armor)", CardType.Skill, 1)
            .AddEffect(CardEffectType.ApplyBreak, 2, CardTargetType.Opponent, 3);
        
        // Weak Status
        CreateCard("Test_Weak_Status", "Apply Weak (reduce damage)", CardType.Skill, 1)
            .AddEffect(CardEffectType.ApplyWeak, 2, CardTargetType.Opponent, 3);
        
        // Damage Over Time
        CreateCard("Test_DOT", "Apply poison (2 damage for 3 turns)", CardType.Skill, 2)
            .AddEffect(CardEffectType.ApplyDamageOverTime, 2, CardTargetType.Opponent, 3);
        
        // Heal Over Time
        CreateCard("Test_HOT", "Apply regeneration (3 heal for 3 turns)", CardType.Skill, 2)
            .AddEffect(CardEffectType.ApplyHealOverTime, 3, CardTargetType.Self, 3);
        
        // Critical Chance
        CreateCard("Test_Crit_Boost", "Raise critical chance", CardType.Skill, 1)
            .AddEffect(CardEffectType.RaiseCriticalChance, 25, CardTargetType.Self, 3);
        
        // Thorns
        CreateCard("Test_Thorns", "Apply thorns (reflect 2 damage)", CardType.Skill, 2)
            .AddEffect(CardEffectType.ApplyThorns, 2, CardTargetType.Self, 5);
        
        // Shield
        CreateCard("Test_Shield", "Apply shield (absorb 8 damage)", CardType.Skill, 2)
            .AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self, 3);
        
        // Stun
        CreateCard("Test_Stun", "Stun target (lose next turn)", CardType.Skill, 3)
            .AddEffect(CardEffectType.ApplyStun, 1, CardTargetType.Opponent, 1);
        
        // Limit Break
        CreateCard("Test_Limit_Break", "Enter limit break state", CardType.Skill, 2)
            .AddEffect(CardEffectType.ApplyLimitBreak, 1, CardTargetType.Self);
        
        // Strength
        CreateCard("Test_Strength", "Gain +3 damage permanently", CardType.Skill, 1)
            .AddEffect(CardEffectType.ApplyStrength, 3, CardTargetType.Self);
        
        // Random Discard
        CreateCard("Test_Discard_Random", "Opponent discards 2 random cards", CardType.Skill, 2)
            .AddEffect(CardEffectType.DiscardRandomCards, 2, CardTargetType.Opponent);
    }
    
    #endregion
    
    #region Target Variation Cards
    
    private static void CreateTargetVariationCards()
    {
        Debug.Log("Creating Target Variation Cards...");
        
        // Self targeting
        CreateCard("Test_Target_Self", "Heal self for 4", CardType.Skill, 1)
            .AddEffect(CardEffectType.Heal, 4, CardTargetType.Self);
        
        // Opponent targeting
        CreateCard("Test_Target_Opponent", "Damage opponent for 4", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
        
        // Ally targeting
        CreateCard("Test_Target_Ally", "Heal ally for 3", CardType.Skill, 1)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Ally);
        
        // Random targeting
        CreateCard("Test_Target_Random", "Damage random target for 3", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Random);
        
        // All targeting
        CreateCard("Test_Target_All", "Draw 1 card for all", CardType.Skill, 2)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.All);
        
        // All allies
        CreateCard("Test_Target_AllAllies", "Heal all allies for 2", CardType.Skill, 2)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.AllAllies);
        
        // All enemies
        CreateCard("Test_Target_AllEnemies", "Damage all enemies for 2", CardType.Attack, 3)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.AllEnemies);
        
        // Flexible targeting card
        CreateCard("Test_Flexible_Targeting", "Can target self or ally", CardType.Skill, 1)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Self)
            .SetFlexibleTargeting(true, true);
    }
    
    #endregion
    
    #region Scaling Effect Cards
    
    private static void CreateScalingEffectCards()
    {
        Debug.Log("Creating Scaling Effect Cards...");
        
        // Zero Cost Scaling (Turn)
        CreateCard("Test_Scale_ZeroCost_Turn", "Deal 1 damage + 1 per zero-cost card this turn", CardType.Attack, 1)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ZeroCostCardsThisTurn, 1.0f, 10);
        
        // Zero Cost Scaling (Fight)
        CreateCard("Test_Scale_ZeroCost_Fight", "Deal 1 damage + 1 per zero-cost card this fight", CardType.Attack, 1)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ZeroCostCardsThisFight, 1.0f, 15);
        
        // Cards Played Turn
        CreateCard("Test_Scale_CardsPlayed_Turn", "Heal 1 + 1 per card played this turn", CardType.Skill, 1)
            .AddScalingEffect(CardEffectType.Heal, 1, CardTargetType.Self, ScalingType.CardsPlayedThisTurn, 1.0f, 8);
        
        // Cards Played Fight
        CreateCard("Test_Scale_CardsPlayed_Fight", "Shield 2 + 1 per card played this fight", CardType.Skill, 2)
            .AddScalingEffect(CardEffectType.ApplyShield, 2, CardTargetType.Self, ScalingType.CardsPlayedThisFight, 1.0f, 20);
        
        // Damage Dealt Scaling
        CreateCard("Test_Scale_Damage_Dealt", "Heal based on damage dealt", CardType.Skill, 1)
            .AddScalingEffect(CardEffectType.Heal, 0, CardTargetType.Self, ScalingType.DamageDealtThisFight, 0.5f, 50);
        
        // Combo Scaling
        CreateCard("Test_Scale_Combo", "Damage scales with combo count", CardType.Attack, 2)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ComboCount, 2.0f, 20);
        
        // Health Scaling
        CreateCard("Test_Scale_Missing_Health", "Damage scales with missing health", CardType.Attack, 2)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.MissingHealth, 0.1f, 10);
    }
    
    #endregion
    
    #region Conditional Effect Cards
    
    private static void CreateConditionalEffectCards()
    {
        Debug.Log("Creating Conditional Effect Cards...");
        
        // Health-based conditional
        CreateCard("Test_Conditional_Health", "Execute if target below 50% health", CardType.Attack, 2)
            .AddConditionalEffect(CardEffectType.Damage, 10, CardTargetType.Opponent, 
                ConditionalType.IfTargetHealthBelow, 50, CardEffectType.Damage, 2);
        
        // Cards in hand conditional
        CreateCard("Test_Conditional_Hand", "Bonus if 3+ cards in hand", CardType.Attack, 2)
            .AddConditionalEffect(CardEffectType.Damage, 6, CardTargetType.Opponent, 
                ConditionalType.IfCardsInHand, 3);
        
        // Perfection streak conditional
        CreateCard("Test_Conditional_Perfection", "Bonus damage if perfect streak", CardType.Attack, 2)
            .AddConditionalEffect(CardEffectType.Damage, 8, CardTargetType.Opponent, 
                ConditionalType.IfPerfectionStreak, 3);
        
        // Combo conditional with alternative
        CreateCard("Test_Conditional_Combo", "Big damage if combo, draw if not", CardType.Attack, 2)
            .AddConditionalEffect(CardEffectType.Damage, 5, CardTargetType.Opponent, 
                ConditionalType.IfComboCount, 3, CardEffectType.DrawCard, 1);
        
        // Energy conditional
        CreateCard("Test_Conditional_Energy", "Bonus if 2+ energy remaining", CardType.Skill, 1)
            .AddConditionalEffect(CardEffectType.Heal, 6, CardTargetType.Self, 
                ConditionalType.IfEnergyRemaining, 2);
    }
    
    #endregion
    
    #region Multi-Effect Cards
    
    private static void CreateMultiEffectCards()
    {
        Debug.Log("Creating Multi-Effect Cards...");
        
        // Damage + Heal combo
        CreateCard("Test_Multi_Damage_Heal", "Damage enemy and heal self", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Self);
        
        // Damage + Status combo
        CreateCard("Test_Multi_Damage_Status", "Damage and apply weak", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent)
            .AddEffect(CardEffectType.ApplyWeak, 2, CardTargetType.Opponent, 3);
        
        // Triple effect card
        CreateCard("Test_Multi_Triple", "Damage, heal, and draw", CardType.Attack, 3)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Self)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        
        // Area effect card
        CreateCard("Test_Multi_Area", "Damage opponent, heal ally", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Ally);
        
        // Healing Strike
        CreateCard("Test_Multi_Healing_Strike", "Deal 4 damage and heal 2", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Self);
        
        // Zero Cost Explosion (scaling + multi-target)
        CreateCard("Test_Multi_Zero_Cost_Explosion", "Damage scales with zero-cost cards", CardType.Attack, 3)
            .AddScalingEffect(CardEffectType.Damage, 2, CardTargetType.AllEnemies, ScalingType.ZeroCostCardsThisFight, 1.5f, 12);
        
        // Damage Aura (persistent effect)
        CreateCard("Test_Multi_Damage_Aura", "Deal 1 damage to all enemies each turn", CardType.Skill, 2)
            .AddEffect(CardEffectType.Damage, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Damage Aura", CardEffectType.Damage, 1, true);
    }
    
    #endregion
    
    #region Stance System Cards
    
    private static void CreateStanceSystemCards()
    {
        Debug.Log("Creating Stance System Cards...");
        
        // Enter Aggressive Stance
        CreateCard("Test_Stance_Aggressive", "Enter Aggressive stance (+2 damage, -1 defense)", CardType.Stance, 1)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Aggressive, CardTargetType.Self)
            .SetStance(StanceType.Aggressive);
        
        // Enter Defensive Stance
        CreateCard("Test_Stance_Defensive", "Enter Defensive stance (+2 defense, +2 shield)", CardType.Stance, 1)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Defensive, CardTargetType.Self)
            .SetStance(StanceType.Defensive);
        
        // Enter Focused Stance
        CreateCard("Test_Stance_Focused", "Enter Focused stance (+1 energy, +1 draw)", CardType.Stance, 1)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Focused, CardTargetType.Self)
            .SetStance(StanceType.Focused);
        
        // Enter Guardian Stance
        CreateCard("Test_Stance_Guardian", "Enter Guardian stance (+3 shield, +1 thorns)", CardType.Stance, 1)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Guardian, CardTargetType.Self)
            .SetStance(StanceType.Guardian);
        
        // Enter Mystic Stance
        CreateCard("Test_Stance_Mystic", "Enter Mystic stance (enhances elemental effects)", CardType.Stance, 1)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Mystic, CardTargetType.Self)
            .SetStance(StanceType.Mystic);
        
        // Exit Stance
        CreateCard("Test_Stance_Exit", "Exit current stance", CardType.Skill, 0)
            .AddEffect(CardEffectType.ExitStance, 0, CardTargetType.Self);
    }
    
    #endregion
    
    #region Persistent Effect Cards
    
    private static void CreatePersistentEffectCards()
    {
        Debug.Log("Creating Persistent Effect Cards...");
        
        // Damage Aura
        CreateCard("Test_Persistent_Damage_Aura", "Deal 1 damage to all enemies each turn", CardType.Skill, 2)
            .AddEffect(CardEffectType.Damage, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Damage Aura", CardEffectType.Damage, 1, true);
        
        // Healing Aura
        CreateCard("Test_Persistent_Heal_Aura", "Heal 2 health each turn for 5 turns", CardType.Skill, 2)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Self) // Immediate heal
            .AddPersistentEffect("Healing Aura", CardEffectType.Heal, 2, false);
        
        // Energy Regen
        CreateCard("Test_Persistent_Energy_Regen", "Restore 1 energy each turn", CardType.Skill, 1)
            .AddEffect(CardEffectType.RestoreEnergy, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Energy Regen", CardEffectType.RestoreEnergy, 1, true);
        
        // Draw Bonus
        CreateCard("Test_Persistent_Draw_Bonus", "Draw 1 extra card each turn", CardType.Skill, 2)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self) // Immediate draw
            .AddPersistentEffect("Draw Bonus", CardEffectType.DrawCard, 1, true);
    }
    
    #endregion
    
    #region Card Type & Sequencing
    
    private static void CreateSequencingCards()
    {
        Debug.Log("Creating Sequencing Cards...");
        
        // Basic Attack
        CreateCard("Test_Attack_Basic", "Basic attack card", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent);
        
        // Combo Builder
        CreateCard("Test_Combo_Builder", "Combo builder attack", CardType.Combo, 1)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent)
            .SetCombo(true);
        
        // Finisher (requires combo)
        CreateCard("Test_Finisher_Combo", "Finisher requiring combo", CardType.Finisher, 3)
            .AddEffect(CardEffectType.Damage, 8, CardTargetType.Opponent)
            .RequireCombo(3);
        
        // Skill card
        CreateCard("Test_Skill_Basic", "Basic skill card", CardType.Skill, 1)
            .AddEffect(CardEffectType.Heal, 4, CardTargetType.Self);
        
        // Spell card
        CreateCard("Test_Spell_Basic", "Basic spell card", CardType.Spell, 1)
            .AddEffect(CardEffectType.DrawCard, 2, CardTargetType.Self);
        
        // Counter (combo-dependent)
        CreateCard("Test_Counter_Attack", "Counter requiring combo", CardType.Counter, 2)
            .AddEffect(CardEffectType.Damage, 5, CardTargetType.Opponent)
            .RequireCombo(1);
        
        // Reaction (combo-triggered)
        CreateCard("Test_Reaction_Skill", "Reaction to combo", CardType.Reaction, 1)
            .AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.Self)
            .RequireCombo(1);
    }
    
    #endregion
    
    #region Elemental Effects
    
    private static void CreateElementalEffectCards()
    {
        Debug.Log("Creating Elemental Effect Cards...");
        
        // Fire Damage
        CreateCard("Test_Fire_Damage", "Fire damage with burn", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent, 0, ElementalType.Fire)
            .AddEffect(CardEffectType.ApplyElementalStatus, 2, CardTargetType.Opponent, 3, ElementalType.Fire);
        
        // Ice Damage
        CreateCard("Test_Ice_Damage", "Ice damage with slow", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent, 0, ElementalType.Ice)
            .AddEffect(CardEffectType.ApplyElementalStatus, 1, CardTargetType.Opponent, 2, ElementalType.Ice);
        
        // Lightning Damage
        CreateCard("Test_Lightning_Damage", "Lightning with chain effect", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent, 0, ElementalType.Lightning)
            .AddEffect(CardEffectType.ApplyElementalStatus, 3, CardTargetType.AllEnemies, 2, ElementalType.Lightning);
        
        // Void Damage
        CreateCard("Test_Void_Damage", "Void damage with corruption", CardType.Attack, 2)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent, 0, ElementalType.Void)
            .AddEffect(CardEffectType.ApplyElementalStatus, 2, CardTargetType.Opponent, 4, ElementalType.Void);
    }
    
    #endregion
    
    #region Complex Combinations
    
    private static void CreateComplexCombinationCards()
    {
        Debug.Log("Creating Complex Combination Cards...");
        
        // Scaling + Conditional + Multi-effect
        CreateCard("Test_Complex_Scaling_Conditional", "Complex scaling with conditionals", CardType.Attack, 3)
            .AddScalingEffect(CardEffectType.Damage, 2, CardTargetType.Opponent, ScalingType.ComboCount, 1.5f, 12)
            .AddConditionalEffect(CardEffectType.ApplyStun, 1, CardTargetType.Opponent, ConditionalType.IfComboCount, 3, CardEffectType.Heal, 2)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        
        // Global + Persistent + Stance
        CreateCard("Test_Complex_Global_Persistent", "Global effect with persistent aura", CardType.Skill, 3)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Everyone)
            .AddPersistentEffect("Global Healing", CardEffectType.Heal, 1, true)
            .SetStance(StanceType.Guardian);
        
        // Elemental + Scaling + Multi-target
        CreateCard("Test_Complex_Elemental_Scaling", "Elemental scaling multi-target", CardType.Attack, 3)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.AllEnemies, ScalingType.DamageDealtThisTurn, 0.3f, 8, ElementalType.Fire)
            .AddEffect(CardEffectType.ApplyElementalStatus, 1, CardTargetType.AllEnemies, 2, ElementalType.Fire)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Self);
    }
    
    #endregion
    
    #region Card Creation Helpers
    
    private static CardDataBuilder CreateCard(string cardName, string description, CardType cardType, int energyCost)
    {
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
        string assetPath = CARD_DATA_PATH + cardName + ".asset";
        AssetDatabase.CreateAsset(cardData, assetPath);
        
        Debug.Log($"Created test card: {cardName}");
        return new CardDataBuilder(cardData);
    }
    
    private static void SetPrivateField(object obj, string fieldName, object value)
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
    
    #endregion
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
        ConditionalType conditionType, int conditionValue, CardEffectType alternativeEffectType = CardEffectType.Damage, int alternativeAmount = 0)
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
            scalingType = ScalingType.None,
            elementalType = ElementalType.None
        });
        
        UpdateCardData();
        return this;
    }
    
    public CardDataBuilder SetCombo(bool buildsCombo)
    {
        SetPrivateField(cardData, "_buildsCombo", buildsCombo);
        if (buildsCombo)
        {
            SetPrivateField(cardData, "_cardType", CardType.Combo);
        }
        return this;
    }
    
    public CardDataBuilder RequireCombo(int comboAmount = 1)
    {
        SetPrivateField(cardData, "_requiresCombo", true);
        SetPrivateField(cardData, "_requiredComboAmount", comboAmount);
        SetPrivateField(cardData, "_cardType", CardType.Finisher);
        return this;
    }
    
    public CardDataBuilder SetStance(StanceType stanceType)
    {
        SetPrivateField(cardData, "_changesStance", true);
        SetPrivateField(cardData, "_newStance", stanceType);
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
        
        SetPrivateField(cardData, "_persistentEffects", persistentEffects);
        EditorUtility.SetDirty(cardData);
        return this;
    }
    
    public CardDataBuilder SetFlexibleTargeting(bool canTargetSelf, bool canTargetAllies)
    {
        SetPrivateField(cardData, "_canAlsoTargetSelf", canTargetSelf);
        SetPrivateField(cardData, "_canAlsoTargetAllies", canTargetAllies);
        return this;
    }
    
    private void UpdateCardData()
    {
        SetPrivateField(cardData, "_effects", effects);
        EditorUtility.SetDirty(cardData);
    }
    
    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }
}

#endif 