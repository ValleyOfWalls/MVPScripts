using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

/// <summary>
/// Creates comprehensive test cards using the CardFactory.
/// Separated from CardFactory to keep the factory general-purpose.
/// </summary>
public static class TestCardDatabase
{
    private const string TEST_CARD_PATH = "TestCards/";
    
    [MenuItem("Tools/Test Cards/Create All Test Cards")]
    public static void CreateAllTestCards()
    {
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

    }
    
    [MenuItem("Tools/Test Cards/Clear Test Cards")]
    public static void ClearTestCards()
    {
        CardFactory.ClearCards(TEST_CARD_PATH);
    }
    
    #region Basic Effect Cards
    
    private static void CreateBasicEffectCards()
    {
        // Basic Damage
        CardFactory.CreateCard("Test_Damage_Basic", "Deal 3 damage", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent);
        
        // Basic Heal
        CardFactory.CreateCard("Test_Heal_Basic", "Heal 5 health", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 5, CardTargetType.Self);
        
        // Card Draw
        CardFactory.CreateCard("Test_Draw_Cards", "Draw 2 cards", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.DrawCard, 2, CardTargetType.Self);
        
        // Energy Restore
        CardFactory.CreateCard("Test_Energy_Restore", "Restore 3 energy", CardType.Skill, 0, TEST_CARD_PATH)
            .AddEffect(CardEffectType.RestoreEnergy, 3, CardTargetType.Self);
        
        // Zero Cost Card for scaling tests
        CardFactory.CreateCard("Test_Zero_Cost", "Zero cost card for scaling tests", CardType.Attack, 0, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 1, CardTargetType.Opponent);
    }
    
    #endregion
    
    #region Status Effect Cards
    
    private static void CreateStatusEffectCards()
    {
        // Break Status
        CardFactory.CreateCard("Test_Break_Status", "Apply Break (reduce armor)", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyBreak, 2, CardTargetType.Opponent, 3);
        
        // Weak Status
        CardFactory.CreateCard("Test_Weak_Status", "Apply Weak (reduce damage)", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyWeak, 2, CardTargetType.Opponent, 3);
        
        // Damage Over Time
        CardFactory.CreateCard("Test_DOT", "Apply poison (2 damage for 3 turns)", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyDamageOverTime, 2, CardTargetType.Opponent, 3);
        
        // Heal Over Time
        CardFactory.CreateCard("Test_HOT", "Apply regeneration (3 heal for 3 turns)", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyHealOverTime, 3, CardTargetType.Self, 3);
        
        // Critical Chance
        CardFactory.CreateCard("Test_Crit_Boost", "Raise critical chance", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.RaiseCriticalChance, 25, CardTargetType.Self, 3);
        
        // Thorns
        CardFactory.CreateCard("Test_Thorns", "Apply thorns (reflect 2 damage)", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyThorns, 2, CardTargetType.Self, 5);
        
        // Shield
        CardFactory.CreateCard("Test_Shield", "Apply shield (absorb 8 damage)", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self, 3);
        
        // Stun
        CardFactory.CreateCard("Test_Stun", "Stun target (lose next turn)", CardType.Skill, 3, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyStun, 1, CardTargetType.Opponent, 1);
        
        // Limit Break
        CardFactory.CreateCard("Test_Limit_Break", "Enter limit break state", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyLimitBreak, 1, CardTargetType.Self);
        
        // Strength
        CardFactory.CreateCard("Test_Strength", "Gain +3 damage permanently", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyStrength, 3, CardTargetType.Self);
        
        // Random Discard
        CardFactory.CreateCard("Test_Discard_Random", "Opponent discards 2 random cards", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.DiscardRandomCards, 2, CardTargetType.Opponent);
    }
    
    #endregion
    
    #region Target Variation Cards
    
    private static void CreateTargetVariationCards()
    {
        // Self targeting
        CardFactory.CreateCard("Test_Target_Self", "Heal self for 4", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 4, CardTargetType.Self);
        
        // Opponent targeting
        CardFactory.CreateCard("Test_Target_Opponent", "Damage opponent for 4", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
        
        // Ally targeting
        CardFactory.CreateCard("Test_Target_Ally", "Heal ally for 3", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Ally);
        
        // Random targeting
        CardFactory.CreateCard("Test_Target_Random", "Damage random target for 3", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Random);
        
        // All targeting
        CardFactory.CreateCard("Test_Target_All", "Draw 1 card for all", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.All);
        
        // All allies
        CardFactory.CreateCard("Test_Target_AllAllies", "Heal all allies for 2", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.AllAllies);
        
        // All enemies
        CardFactory.CreateCard("Test_Target_AllEnemies", "Damage all enemies for 2", CardType.Attack, 3, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.AllEnemies);
        
        // Flexible targeting card
        CardFactory.CreateCard("Test_Flexible_Targeting", "Can target self or ally", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Self)
            .SetFlexibleTargeting(true, true);
    }
    
    #endregion
    
    #region Scaling Effect Cards
    
    private static void CreateScalingEffectCards()
    {
        // Zero Cost Scaling (Turn)
        CardFactory.CreateCard("Test_Scale_ZeroCost_Turn", "Deal 1 damage + 1 per zero-cost card this turn", CardType.Attack, 1, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ZeroCostCardsThisTurn, 1.0f, 10);
        
        // Zero Cost Scaling (Fight)
        CardFactory.CreateCard("Test_Scale_ZeroCost_Fight", "Deal 1 damage + 1 per zero-cost card this fight", CardType.Attack, 1, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ZeroCostCardsThisFight, 1.0f, 15);
        
        // Cards Played Turn
        CardFactory.CreateCard("Test_Scale_CardsPlayed_Turn", "Heal 1 + 1 per card played this turn", CardType.Skill, 1, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Heal, 1, CardTargetType.Self, ScalingType.CardsPlayedThisTurn, 1.0f, 8);
        
        // Cards Played Fight
        CardFactory.CreateCard("Test_Scale_CardsPlayed_Fight", "Shield 2 + 1 per card played this fight", CardType.Skill, 2, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.ApplyShield, 2, CardTargetType.Self, ScalingType.CardsPlayedThisFight, 1.0f, 20);
        
        // Damage Dealt Scaling
        CardFactory.CreateCard("Test_Scale_Damage_Dealt", "Heal based on damage dealt", CardType.Skill, 1, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Heal, 0, CardTargetType.Self, ScalingType.DamageDealtThisFight, 0.5f, 50);
        
        // Combo Scaling
        CardFactory.CreateCard("Test_Scale_Combo", "Damage scales with combo count", CardType.Attack, 2, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.ComboCount, 2.0f, 20);
        
        // Health Scaling
        CardFactory.CreateCard("Test_Scale_Missing_Health", "Damage scales with missing health", CardType.Attack, 2, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.Opponent, ScalingType.MissingHealth, 0.1f, 10);
    }
    
    #endregion
    
    #region Conditional Effect Cards
    
    private static void CreateConditionalEffectCards()
    {
        // Original "OR" Logic Examples (Replace behavior)
        
        // Health-based conditional
        CardFactory.CreateCard("Test_Conditional_Health", "Execute if target below 50% health", CardType.Attack, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Damage, 10, CardTargetType.Opponent, 
                ConditionalType.IfTargetHealthBelow, 50, CardEffectType.Damage, 2, AlternativeEffectLogic.Replace);
        
        // Cards in hand conditional
        CardFactory.CreateCard("Test_Conditional_Hand", "Bonus if 3+ cards in hand", CardType.Attack, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Damage, 6, CardTargetType.Opponent, 
                ConditionalType.IfCardsInHand, 3);
        
        // Combo conditional with alternative (OR logic - either big damage OR draw)
        CardFactory.CreateCard("Test_Conditional_Combo_OR", "Big damage if combo, draw if not", CardType.Attack, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Damage, 8, CardTargetType.Opponent, 
                ConditionalType.IfComboCount, 3, CardEffectType.DrawCard, 1, AlternativeEffectLogic.Replace);
        
        // New "AND" Logic Examples (Additional behavior)
        
        // Health conditional with healing bonus (AND logic - always heal, bonus if low health)
        CardFactory.CreateCard("Test_Conditional_Health_AND", "Heal 3, heal 6 more if below 25% health", CardType.Skill, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Heal, 3, CardTargetType.Self, 
                ConditionalType.IfSourceHealthBelow, 25, CardEffectType.Heal, 6, AlternativeEffectLogic.Additional);
        
        // Energy conditional with bonus draw (AND logic - always damage, bonus draw if high energy)
        CardFactory.CreateCard("Test_Conditional_Energy_AND", "Deal 4 damage, draw 2 if 3+ energy", CardType.Attack, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Damage, 4, CardTargetType.Opponent, 
                ConditionalType.IfEnergyRemaining, 3, CardEffectType.DrawCard, 2, AlternativeEffectLogic.Additional);
        
        // Perfection streak with shield bonus (AND logic - always attack, bonus shield if perfect)
        CardFactory.CreateCard("Test_Conditional_Perfect_AND", "Deal 5 damage, gain 4 shield if perfect streak", CardType.Attack, 3, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Damage, 5, CardTargetType.Opponent, 
                ConditionalType.IfPerfectionStreak, 5, CardEffectType.ApplyShield, 4, AlternativeEffectLogic.Additional);
        
        // Hand size conditional with energy bonus (AND logic - always draw, bonus energy if few cards)
        CardFactory.CreateCard("Test_Conditional_LowHand_AND", "Draw 1 card, restore 2 energy if 2 or fewer cards", CardType.Skill, 1, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.DrawCard, 1, CardTargetType.Self, 
                ConditionalType.IfCardsInHand, 2, CardEffectType.RestoreEnergy, 2, AlternativeEffectLogic.Additional);
        
        // Complex example showing the difference
        CardFactory.CreateCard("Test_Comparison_OR", "Conditional OR: Big heal if hurt, small damage if not", CardType.Skill, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Heal, 8, CardTargetType.Self, 
                ConditionalType.IfSourceHealthBelow, 50, CardEffectType.Damage, 3, AlternativeEffectLogic.Replace);
        
        CardFactory.CreateCard("Test_Comparison_AND", "Conditional AND: Always heal 3, heal 5 more if hurt", CardType.Skill, 2, TEST_CARD_PATH)
            .AddConditionalEffect(CardEffectType.Heal, 3, CardTargetType.Self, 
                ConditionalType.IfSourceHealthBelow, 50, CardEffectType.Heal, 5, AlternativeEffectLogic.Additional);
    }
    
    #endregion
    
    #region Multi-Effect Cards
    
    private static void CreateMultiEffectCards()
    {
        // Damage + Heal combo
        CardFactory.CreateCard("Test_Multi_Damage_Heal", "Damage enemy and heal self", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 3, CardTargetType.Self);
        
        // Damage + Status combo
        CardFactory.CreateCard("Test_Multi_Damage_Status", "Damage and apply weak", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent)
            .AddEffect(CardEffectType.ApplyWeak, 2, CardTargetType.Opponent, 3);
        
        // Triple effect card
        CardFactory.CreateCard("Test_Multi_Triple", "Damage, heal, and draw", CardType.Attack, 3, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Self)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        
        // Area effect card
        CardFactory.CreateCard("Test_Multi_Area", "Damage opponent, heal ally", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Ally);
        
        // Healing Strike
        CardFactory.CreateCard("Test_Multi_Healing_Strike", "Deal 4 damage and heal 2", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent)
            .AddEffect(CardEffectType.Heal, 2, CardTargetType.Self);
        
        // Zero Cost Explosion (scaling + multi-target)
        CardFactory.CreateCard("Test_Multi_Zero_Cost_Explosion", "Damage scales with zero-cost cards", CardType.Attack, 3, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 2, CardTargetType.AllEnemies, ScalingType.ZeroCostCardsThisFight, 1.5f, 12);
        
        // Damage Aura (persistent effect)
        CardFactory.CreateCard("Test_Multi_Damage_Aura", "Deal 1 damage to all enemies each turn", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Damage Aura", CardEffectType.Damage, 1, true);
    }
    
    #endregion
    
    #region Stance System Cards
    
    private static void CreateStanceSystemCards()
    {
        // Enter Aggressive Stance
        CardFactory.CreateCard("Test_Stance_Aggressive", "Enter Aggressive stance (+2 damage, -1 defense)", CardType.Stance, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Aggressive, CardTargetType.Self)
            .SetStance(StanceType.Aggressive);
        
        // Enter Defensive Stance
        CardFactory.CreateCard("Test_Stance_Defensive", "Enter Defensive stance (+2 defense, +2 shield)", CardType.Stance, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Defensive, CardTargetType.Self)
            .SetStance(StanceType.Defensive);
        
        // Enter Focused Stance
        CardFactory.CreateCard("Test_Stance_Focused", "Enter Focused stance (+1 energy, +1 draw)", CardType.Stance, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Focused, CardTargetType.Self)
            .SetStance(StanceType.Focused);
        
        // Enter Guardian Stance
        CardFactory.CreateCard("Test_Stance_Guardian", "Enter Guardian stance (+3 shield, +1 thorns)", CardType.Stance, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Guardian, CardTargetType.Self)
            .SetStance(StanceType.Guardian);
        
        // Enter Mystic Stance
        CardFactory.CreateCard("Test_Stance_Mystic", "Enter Mystic stance (enhances elemental effects)", CardType.Stance, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.EnterStance, (int)StanceType.Mystic, CardTargetType.Self)
            .SetStance(StanceType.Mystic);
        
        // Exit Stance
        CardFactory.CreateCard("Test_Stance_Exit", "Exit current stance", CardType.Skill, 0, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ExitStance, 0, CardTargetType.Self);
    }
    
    #endregion
    
    #region Persistent Effect Cards
    
    private static void CreatePersistentEffectCards()
    {
        // Damage Aura
        CardFactory.CreateCard("Test_Persistent_Damage_Aura", "Deal 1 damage to all enemies each turn", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Damage Aura", CardEffectType.Damage, 1, true);
        
        // Healing Aura
        CardFactory.CreateCard("Test_Persistent_Heal_Aura", "Heal 2 health each turn for 5 turns", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Self) // Immediate heal
            .AddPersistentEffect("Healing Aura", CardEffectType.Heal, 2, false);
        
        // Energy Regen
        CardFactory.CreateCard("Test_Persistent_Energy_Regen", "Restore 1 energy each turn", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.RestoreEnergy, 0, CardTargetType.Self) // Minimal immediate effect
            .AddPersistentEffect("Energy Regen", CardEffectType.RestoreEnergy, 1, true);
        
        // Draw Bonus
        CardFactory.CreateCard("Test_Persistent_Draw_Bonus", "Draw 1 extra card each turn", CardType.Skill, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self) // Immediate draw
            .AddPersistentEffect("Draw Bonus", CardEffectType.DrawCard, 1, true);
    }
    
    #endregion
    
    #region Card Type & Sequencing
    
    private static void CreateSequencingCards()
    {
        // Basic Attack
        CardFactory.CreateCard("Test_Attack_Basic", "Basic attack card", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent);
        
        // Combo Builder
        CardFactory.CreateCard("Test_Combo_Builder", "Combo builder attack", CardType.Combo, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent)
            .SetCombo(true);
        
        // Finisher (requires combo)
        CardFactory.CreateCard("Test_Finisher_Combo", "Finisher requiring combo", CardType.Finisher, 3, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 8, CardTargetType.Opponent)
            .RequireCombo(3);
        
        // Skill card
        CardFactory.CreateCard("Test_Skill_Basic", "Basic skill card", CardType.Skill, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 4, CardTargetType.Self);
        
        // Spell card
        CardFactory.CreateCard("Test_Spell_Basic", "Basic spell card", CardType.Spell, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.DrawCard, 2, CardTargetType.Self);
        
        // Counter (combo-dependent)
        CardFactory.CreateCard("Test_Counter_Attack", "Counter requiring combo", CardType.Counter, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 5, CardTargetType.Opponent)
            .RequireCombo(1);
        
        // Reaction (combo-triggered)
        CardFactory.CreateCard("Test_Reaction_Skill", "Reaction to combo", CardType.Reaction, 1, TEST_CARD_PATH)
            .AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.Self)
            .RequireCombo(1);
    }
    
    #endregion
    
    #region Elemental Effects
    
    private static void CreateElementalEffectCards()
    {
        // Fire Damage
        CardFactory.CreateCard("Test_Fire_Damage", "Fire damage with burn", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent, 0, ElementalType.Fire)
            .AddEffect(CardEffectType.ApplyElementalStatus, 2, CardTargetType.Opponent, 3, ElementalType.Fire);
        
        // Ice Damage
        CardFactory.CreateCard("Test_Ice_Damage", "Ice damage with slow", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 2, CardTargetType.Opponent, 0, ElementalType.Ice)
            .AddEffect(CardEffectType.ApplyElementalStatus, 1, CardTargetType.Opponent, 2, ElementalType.Ice);
        
        // Lightning Damage
        CardFactory.CreateCard("Test_Lightning_Damage", "Lightning with chain effect", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent, 0, ElementalType.Lightning)
            .AddEffect(CardEffectType.ApplyElementalStatus, 3, CardTargetType.AllEnemies, 2, ElementalType.Lightning);
        
        // Void Damage
        CardFactory.CreateCard("Test_Void_Damage", "Void damage with corruption", CardType.Attack, 2, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Damage, 3, CardTargetType.Opponent, 0, ElementalType.Void)
            .AddEffect(CardEffectType.ApplyElementalStatus, 2, CardTargetType.Opponent, 4, ElementalType.Void);
    }
    
    #endregion
    
    #region Complex Combinations
    
    private static void CreateComplexCombinationCards()
    {
        // Scaling + Conditional + Multi-effect
        CardFactory.CreateCard("Test_Complex_Scaling_Conditional", "Complex scaling with conditionals", CardType.Attack, 3, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 2, CardTargetType.Opponent, ScalingType.ComboCount, 1.5f, 12)
            .AddConditionalEffect(CardEffectType.ApplyStun, 1, CardTargetType.Opponent, ConditionalType.IfComboCount, 3, CardEffectType.Heal, 2)
            .AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        
        // Global + Persistent + Stance
        CardFactory.CreateCard("Test_Complex_Global_Persistent", "Global effect with persistent aura", CardType.Skill, 3, TEST_CARD_PATH)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Everyone)
            .AddPersistentEffect("Global Healing", CardEffectType.Heal, 1, true)
            .SetStance(StanceType.Guardian);
        
        // Elemental + Scaling + Multi-target
        CardFactory.CreateCard("Test_Complex_Elemental_Scaling", "Elemental scaling multi-target", CardType.Attack, 3, TEST_CARD_PATH)
            .AddScalingEffect(CardEffectType.Damage, 1, CardTargetType.AllEnemies, ScalingType.DamageDealtThisTurn, 0.3f, 8, ElementalType.Fire)
            .AddEffect(CardEffectType.ApplyElementalStatus, 1, CardTargetType.AllEnemies, 2, ElementalType.Fire)
            .AddEffect(CardEffectType.Heal, 1, CardTargetType.Self);
    }
    
    #endregion
}

#endif 