using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Combined utility class for generating CardData objects programmatically and editor tools
/// Handles proper setup of card effects, upgrade linking, validation, and Unity editor integration
/// </summary>
public static class CardGenerator
{
    // ═══════════════════════════════════════════════════════════════
    // UNITY EDITOR MENU INTEGRATION
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Tools/Card Generator/Open Card Generator Window")]
    public static void OpenCardGeneratorWindow()
    {
        CardGeneratorWindow.ShowWindow();
    }

    [MenuItem("Tools/Card Generator/Quick Create/Example Damage Cards")]
    public static void CreateExampleDamageCardsMenuItem()
    {
        var (baseCard, upgradedCard) = GenerateExampleDamageCards();
        
        SaveCardAsAsset(baseCard, "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Examples");
        SaveCardAsAsset(upgradedCard, "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Examples");
        
        Debug.Log($"Created example cards: {baseCard.CardName} -> {upgradedCard.CardName}");
        
        Selection.activeObject = baseCard;
        EditorGUIUtility.PingObject(baseCard);
    }

    [MenuItem("Tools/Card Generator/Quick Create/Starter Card Set")]
    public static void CreateStarterCardSetMenuItem()
    {
        var starterCards = GenerateStarterCardSet();
        
        foreach (var (baseCard, upgradedCard) in starterCards)
        {
            SaveCardAsAsset(baseCard, "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Examples");
            SaveCardAsAsset(upgradedCard, "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Examples");
        }
        
        Debug.Log($"Created starter card set: {starterCards.Count} card pairs");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Card Generator/Create Themed Decks/All Starter Decks")]
    public static void CreateAllStarterDecks()
    {
        CreateCharacterStarterDecks();
        CreatePetStarterDecks();
        Debug.Log("Created all 6 themed starter decks!");
    }

    [MenuItem("Tools/Card Generator/Create Themed Decks/Character Decks")]
    public static void CreateCharacterStarterDecks()
    {
        CreateWarriorDeck();
        CreateMysticDeck();
        CreateAssassinDeck();
        Debug.Log("Created all 3 character starter decks!");
    }

    [MenuItem("Tools/Card Generator/Create Themed Decks/Pet Decks")]
    public static void CreatePetStarterDecks()
    {
        CreateElementalDeck();
        CreateBeastDeck();
        CreateSpiritDeck();
        Debug.Log("Created all 3 pet starter decks!");
    }

    [MenuItem("Tools/Card Generator/Utilities/Validate Selected Card")]
    public static void ValidateSelectedCard()
    {
        CardData selectedCard = Selection.activeObject as CardData;
        if (selectedCard != null)
        {
            bool isValid = ValidateCard(selectedCard);
            Debug.Log($"Card validation result: {(isValid ? "VALID" : "INVALID")}");
            PrintCardInfo(selectedCard);
        }
        else
        {
            Debug.LogWarning("Please select a CardData asset to validate.");
        }
    }

    [MenuItem("Tools/Card Generator/Utilities/Print Selected Card Info")]
    public static void PrintSelectedCardInfo()
    {
        CardData selectedCard = Selection.activeObject as CardData;
        if (selectedCard != null)
        {
            PrintCardInfo(selectedCard);
        }
        else
        {
            Debug.LogWarning("Please select a CardData asset to print info.");
        }
    }

    [MenuItem("Tools/Card Generator/Utilities/Open Generated Cards Folder")]
    public static void OpenGeneratedCardsFolder()
    {
        string path = "Assets/MVPScripts/CardObject/GeneratedCards";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
        
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Tools/Card Generator/Utilities/Open Cards Folder")]
    public static void OpenCardsFolder()
    {
        string path = "Assets/MVPScripts/CardObject/GeneratedCards/Cards";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
        
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Tools/Card Generator/Utilities/Open Decks Folder")]
    public static void OpenDecksFolder()
    {
        string path = "Assets/MVPScripts/CardObject/GeneratedCards/Decks";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
        
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    // ═══════════════════════════════════════════════════════════════
    // MAIN CARD GENERATION METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a basic damage card with optional upgrade version
    /// </summary>
    public static CardData CreateDamageCard(string cardName, int damage, int energyCost, 
        CardTargetType target = CardTargetType.Opponent, CardCategory category = CardCategory.Draftable)
    {
        CardData card = CreateBaseCard(cardName, energyCost, CardType.Attack, category);
        
        // Add damage effect
        card.AddEffect(CardEffectType.Damage, damage, target);
        
        // Generate complete description including all mechanics
        SetCardDescription(card, GenerateCompleteCardDescription(card));
        
        return card;
    }

    /// <summary>
    /// Creates a basic heal card
    /// </summary>
    public static CardData CreateHealCard(string cardName, int healing, int energyCost,
        CardTargetType target = CardTargetType.Self, CardCategory category = CardCategory.Draftable)
    {
        CardData card = CreateBaseCard(cardName, energyCost, CardType.Skill, category);
        
        // Add heal effect
        card.AddEffect(CardEffectType.Heal, healing, target);
        
        // Generate complete description including all mechanics
        SetCardDescription(card, GenerateCompleteCardDescription(card));
        
        return card;
    }

    /// <summary>
    /// Creates a status effect card
    /// </summary>
    public static CardData CreateStatusCard(string cardName, CardEffectType statusType, int potency, 
        int duration, int energyCost, CardTargetType target = CardTargetType.Opponent,
        CardCategory category = CardCategory.Draftable)
    {
        CardData card = CreateBaseCard(cardName, energyCost, CardType.Skill, category);
        
        // Add status effect with proper structure
        CardEffect statusEffect = new CardEffect
        {
            effectType = statusType,
            amount = potency,
            duration = duration,
            targetType = target,
            elementalType = ElementalType.None
        };
        
        card.Effects.Add(statusEffect);
        
        // Generate complete description including all mechanics
        SetCardDescription(card, GenerateCompleteCardDescription(card));
        
        return card;
    }

    /// <summary>
    /// Creates a card with multiple effects
    /// </summary>
    public static CardData CreateMultiEffectCard(string cardName, int energyCost, CardType cardType,
        List<(CardEffectType type, int amount, CardTargetType target, int duration)> effects,
        CardCategory category = CardCategory.Draftable)
    {
        CardData card = CreateBaseCard(cardName, energyCost, cardType, category);
        
        foreach (var effect in effects)
        {
            CardEffect cardEffect = new CardEffect
            {
                effectType = effect.type,
                amount = effect.amount,
                targetType = effect.target,
                duration = effect.duration,
                elementalType = ElementalType.None
            };
            
            card.Effects.Add(cardEffect);
        }
        
        // Generate complete description including all mechanics
        SetCardDescription(card, GenerateCompleteCardDescription(card));
        
        return card;
    }

    // ═══════════════════════════════════════════════════════════════
    // UPGRADE SYSTEM METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Links a base card to its upgraded version with specified conditions
    /// </summary>
    public static void LinkCardUpgrade(CardData baseCard, CardData upgradedCard, 
        UpgradeConditionType conditionType, int requiredValue, 
        UpgradeComparisonType comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
        bool upgradeAllCopies = false)
    {
        if (baseCard == null || upgradedCard == null)
        {
            Debug.LogError("CardGenerator: Cannot link null cards for upgrade!");
            return;
        }

        baseCard.SetupUpgrade(upgradedCard, conditionType, requiredValue, comparisonType, upgradeAllCopies);
        
        // Mark the upgraded card as an upgraded version
        upgradedCard.SetCardCategory(CardCategory.Upgraded);
        
        Debug.Log($"CardGenerator: Linked '{baseCard.CardName}' to upgrade '{upgradedCard.CardName}' " +
                  $"(Condition: {conditionType} {comparisonType} {requiredValue})");
    }

    /// <summary>
    /// Creates an upgraded version of a base card with enhanced effects
    /// </summary>
    public static CardData CreateUpgradedVersion(CardData baseCard, string upgradedName, 
        float damageMultiplier = 1.5f, int costReduction = 0, bool addBonusEffect = false,
        CardEffectType bonusEffectType = CardEffectType.DrawCard, int bonusAmount = 1)
    {
        if (baseCard == null)
        {
            Debug.LogError("CardGenerator: Cannot create upgraded version of null card!");
            return null;
        }

        CardData upgradedCard = CreateBaseCard(upgradedName, 
            Mathf.Max(0, baseCard.EnergyCost - costReduction), 
            baseCard.CardType, CardCategory.Upgraded);

        // Copy and enhance effects from base card
        foreach (var baseEffect in baseCard.Effects)
        {
            CardEffect upgradedEffect = new CardEffect
            {
                effectType = baseEffect.effectType,
                amount = Mathf.RoundToInt(baseEffect.amount * damageMultiplier),
                duration = baseEffect.duration,
                targetType = baseEffect.targetType,
                elementalType = baseEffect.elementalType,
                conditionType = baseEffect.conditionType,
                conditionValue = baseEffect.conditionValue,
                hasAlternativeEffect = baseEffect.hasAlternativeEffect,
                alternativeLogic = baseEffect.alternativeLogic,
                alternativeEffectType = baseEffect.alternativeEffectType,
                alternativeEffectAmount = baseEffect.alternativeEffectAmount,
                scalingType = baseEffect.scalingType,
                scalingMultiplier = baseEffect.scalingMultiplier,
                maxScaling = baseEffect.maxScaling
            };
            
            upgradedCard.Effects.Add(upgradedEffect);
        }

        // Add bonus effect if requested
        if (addBonusEffect)
        {
            CardEffect bonusEffect = new CardEffect
            {
                effectType = bonusEffectType,
                amount = bonusAmount,
                targetType = CardTargetType.Self,
                elementalType = ElementalType.None
            };
            
            upgradedCard.Effects.Add(bonusEffect);
        }

        // Generate upgraded description
        GenerateUpgradedDescription(upgradedCard, baseCard, addBonusEffect, bonusEffectType, bonusAmount);

        return upgradedCard;
    }

    // ═══════════════════════════════════════════════════════════════
    // EXAMPLE CARD PAIRS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates the example cards requested: basic 5 damage card and its upgrade
    /// </summary>
    public static (CardData baseCard, CardData upgradedCard) GenerateExampleDamageCards()
    {
        // Create base 5 damage card
        CardData baseCard = CreateDamageCard("Strike", 5, 2, CardTargetType.Opponent, CardCategory.Starter);
        
        // Create upgraded version with enhanced damage and card draw
        CardData upgradedCard = CreateUpgradedVersion(baseCard, "Heavy Strike", 1.4f, 0, true, CardEffectType.DrawCard, 1);
        
        // Link them with "played 3 times this fight" condition
        LinkCardUpgrade(baseCard, upgradedCard, UpgradeConditionType.TimesPlayedThisFight, 3);
        
        Debug.Log($"CardGenerator: Created example card pair - {baseCard.CardName} -> {upgradedCard.CardName}");
        
        return (baseCard, upgradedCard);
    }

    /// <summary>
    /// Generate a set of starter cards for testing
    /// </summary>
    public static List<(CardData baseCard, CardData upgradedCard)> GenerateStarterCardSet()
    {
        var cardPairs = new List<(CardData, CardData)>();
        
        // Strike cards
        var strikePair = GenerateExampleDamageCards();
        cardPairs.Add(strikePair);
        
        // Defend card
        CardData defend = CreateStatusCard("Defend", CardEffectType.ApplyShield, 8, 0, 1, CardTargetType.Self, CardCategory.Starter);
        CardData defendUpgraded = CreateUpgradedVersion(defend, "Strong Defend", 1.25f, 0, false);
        LinkCardUpgrade(defend, defendUpgraded, UpgradeConditionType.DamageTakenThisFight, 15);
        cardPairs.Add((defend, defendUpgraded));
        
        // Heal card
        CardData heal = CreateHealCard("Recover", 6, 1, CardTargetType.Self, CardCategory.Starter);
        CardData healUpgraded = CreateUpgradedVersion(heal, "Greater Recover", 1.33f, 0, true, CardEffectType.RestoreEnergy, 1);
        LinkCardUpgrade(heal, healUpgraded, UpgradeConditionType.HealingReceivedThisFight, 20);
        cardPairs.Add((heal, healUpgraded));
        
        return cardPairs;
    }

    // ═══════════════════════════════════════════════════════════════
    // THEMED DECK GENERATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a Warrior-themed character deck (Aggression + Shield focus)
    /// </summary>
    public static void CreateWarriorDeck()
    {
        /* Debug.Log("Creating Warrior Deck..."); */
        
        // Create all cards for the deck
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS (4 types, repeating to 4 total) ===
        
        // Power Strike - Basic attack (STARTER: Basic attack, not draftable)
        var powerStrike = CreateDamageCard("Power Strike", 25, 30, CardTargetType.Opponent, CardCategory.Starter);
        var powerStrikeUpgraded = CreateUpgradedVersion(powerStrike, "Devastating Strike", 1.4f, 0, true, CardEffectType.ApplyStrength, 2);
        LinkCardUpgrade(powerStrike, powerStrikeUpgraded, UpgradeConditionType.DamageDealtThisFight, 150);
        cards.Add((powerStrike, powerStrikeUpgraded));
        
        // Berserker Rage - Advanced stance card (DRAFTABLE: Complex mechanic)
        var berserkerRage = CreateMultiEffectCard("Berserker Rage", 40, CardType.Stance, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 20, CardTargetType.Opponent, 0),
            (CardEffectType.ApplyStrength, 3, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        berserkerRage.ChangeStance(StanceType.Berserker);
        SetCardDescription(berserkerRage, GenerateCompleteCardDescription(berserkerRage)); // Regenerate with stance info
        var berserkerRageUpgraded = CreateUpgradedVersion(berserkerRage, "Primal Fury", 1.3f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(berserkerRage, berserkerRageUpgraded, UpgradeConditionType.PlayedInStance, 3);
        cards.Add((berserkerRage, berserkerRageUpgraded));
        
        // === DEFENSIVE CARDS (4 types, repeating to 4 total) ===
        
        // Shield Wall - Basic defense (STARTER: Basic defend, not draftable)
        var shieldWall = CreateStatusCard("Shield Wall", CardEffectType.ApplyShield, 40, 0, 25, CardTargetType.Self, CardCategory.Starter);
        var shieldWallUpgraded = CreateUpgradedVersion(shieldWall, "Fortress", 1.5f, 0, true, CardEffectType.ApplyThorns, 8);
        LinkCardUpgrade(shieldWall, shieldWallUpgraded, UpgradeConditionType.DamageTakenThisFight, 100);
        cards.Add((shieldWall, shieldWallUpgraded));
        
        // Guardian Stance - Advanced stance card (DRAFTABLE: Complex mechanic)
        var guardianStance = CreateMultiEffectCard("Guardian Stance", 35, CardType.Stance, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 25, CardTargetType.Self, 0),
            (CardEffectType.ApplyThorns, 10, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        guardianStance.ChangeStance(StanceType.Guardian);
        SetCardDescription(guardianStance, GenerateCompleteCardDescription(guardianStance)); // Regenerate with stance info
        var guardianStanceUpgraded = CreateUpgradedVersion(guardianStance, "Aegis Form", 1.2f, -5, false);
        LinkCardUpgrade(guardianStance, guardianStanceUpgraded, UpgradeConditionType.TimesPlayedThisFight, 2);
        cards.Add((guardianStance, guardianStanceUpgraded));
        
        // === UNIQUE CARDS (2 unique) ===
        
        // Taunt - Advanced utility (DRAFTABLE: Unique mechanic)
        var taunt = CreateMultiEffectCard("Taunt", 20, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 30, CardTargetType.Self, 0),
            (CardEffectType.ApplyWeak, 1, CardTargetType.Opponent, 2)
        }, CardCategory.Draftable);
        var tauntUpgraded = CreateUpgradedVersion(taunt, "Challenge", 1.5f, 0, true, CardEffectType.ApplyStun, 1);
        LinkCardUpgrade(taunt, tauntUpgraded, UpgradeConditionType.PlayedAtLowHealth, 1);
        cards.Add((taunt, tauntUpgraded));
        
        // Execute - Advanced conditional (DRAFTABLE: Conditional mechanic)
        var execute = CreateDamageCard("Execute", 15, 35, CardTargetType.Opponent, CardCategory.Draftable);
        execute.AddConditionalEffectOR(CardEffectType.Damage, 15, CardTargetType.Opponent, 
            ConditionalType.IfTargetHealthBelow, 30, CardEffectType.Damage, 60);
        var executeUpgraded = CreateUpgradedVersion(execute, "Execution", 1.0f, -10, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(execute, executeUpgraded, UpgradeConditionType.DefeatedOpponentWithCard, 1);
        cards.Add((execute, executeUpgraded));
        
        // Create the deck
        CreateDeckAsset("Warrior Starter Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Character", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Character/Warrior");
    }

    /// <summary>
    /// Creates a Mystic-themed character deck (Elemental magic + Energy manipulation)
    /// </summary>
    public static void CreateMysticDeck()
    {
        /* Debug.Log("Creating Mystic Deck..."); */
        
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS ===
        
        // Fireball - Basic elemental attack (STARTER: Basic attack, not draftable)
        var fireball = CreateStatusCard("Fireball", CardEffectType.Damage, 20, 0, 25, CardTargetType.Opponent, CardCategory.Starter);
        fireball.Effects[0].elementalType = ElementalType.Fire;
        SetCardDescription(fireball, GenerateCompleteCardDescription(fireball)); // Regenerate with elemental info
        var fireballUpgraded = CreateUpgradedVersion(fireball, "Inferno", 1.5f, 0, true, CardEffectType.ApplyBurn, 8);
        LinkCardUpgrade(fireball, fireballUpgraded, UpgradeConditionType.TimesPlayedThisFight, 4);
        cards.Add((fireball, fireballUpgraded));
        
        // Lightning Bolt - Advanced multi-effect (DRAFTABLE: Multi-effect + energy)
        var lightningBolt = CreateMultiEffectCard("Lightning Bolt", 30, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 18, CardTargetType.Opponent, 0),
            (CardEffectType.RestoreEnergy, 15, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        lightningBolt.Effects[0].elementalType = ElementalType.Lightning;
        SetCardDescription(lightningBolt, GenerateCompleteCardDescription(lightningBolt)); // Regenerate with elemental info
        var lightningBoltUpgraded = CreateUpgradedVersion(lightningBolt, "Chain Lightning", 1.3f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(lightningBolt, lightningBoltUpgraded, UpgradeConditionType.ZeroCostCardsThisFight, 3);
        cards.Add((lightningBolt, lightningBoltUpgraded));
        
        // === DEFENSIVE CARDS ===
        
        // Ice Shield - Basic elemental defense (STARTER: Basic defend, not draftable)
        var iceShield = CreateMultiEffectCard("Ice Shield", 25, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 35, CardTargetType.Self, 0),
            (CardEffectType.ApplyWeak, 1, CardTargetType.Opponent, 3)
        }, CardCategory.Starter);
        iceShield.Effects[0].elementalType = ElementalType.Ice;
        SetCardDescription(iceShield, GenerateCompleteCardDescription(iceShield)); // Regenerate with elemental info
        var iceShieldUpgraded = CreateUpgradedVersion(iceShield, "Frozen Barrier", 1.4f, 0, true, CardEffectType.ApplyStun, 1);
        LinkCardUpgrade(iceShield, iceShieldUpgraded, UpgradeConditionType.HealingReceivedThisFight, 50);
        cards.Add((iceShield, iceShieldUpgraded));
        
        // Mystic Stance - Advanced stance card (DRAFTABLE: Stance mechanic)
        var mysticStance = CreateMultiEffectCard("Mystic Stance", 20, CardType.Stance, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.RestoreEnergy, 25, CardTargetType.Self, 0),
            (CardEffectType.DrawCard, 1, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        mysticStance.ChangeStance(StanceType.Mystic);
        SetCardDescription(mysticStance, GenerateCompleteCardDescription(mysticStance)); // Regenerate with stance info
        var mysticStanceUpgraded = CreateUpgradedVersion(mysticStance, "Arcane Focus", 1.5f, 0, false);
        LinkCardUpgrade(mysticStance, mysticStanceUpgraded, UpgradeConditionType.TimesPlayedThisFight, 4);
        cards.Add((mysticStance, mysticStanceUpgraded));
        
        // === UNIQUE CARDS ===
        
        // Mana Burn - Advanced scaling (DRAFTABLE: Scaling mechanic)
        var manaBurn = CreateDamageCard("Mana Burn", 10, 35, CardTargetType.Opponent, CardCategory.Draftable);
        manaBurn.Effects[0].scalingType = ScalingType.CurrentHealth; // Placeholder for energy scaling
        manaBurn.Effects[0].scalingMultiplier = 0.3f;
        SetCardDescription(manaBurn, GenerateCompleteCardDescription(manaBurn)); // Regenerate with scaling info
        var manaBurnUpgraded = CreateUpgradedVersion(manaBurn, "Void Drain", 1.5f, 0, true, CardEffectType.RestoreEnergy, 20);
        LinkCardUpgrade(manaBurn, manaBurnUpgraded, UpgradeConditionType.TimesPlayedThisFight, 3);
        cards.Add((manaBurn, manaBurnUpgraded));
        
        // Elemental Mastery - Advanced scaling (DRAFTABLE: Complex scaling)
        var elementalMastery = CreateDamageCard("Elemental Mastery", 5, 40, CardTargetType.Opponent, CardCategory.Draftable);
        elementalMastery.Effects[0].scalingType = ScalingType.CardsPlayedThisTurn;
        elementalMastery.Effects[0].scalingMultiplier = 8.0f;
        SetCardDescription(elementalMastery, GenerateCompleteCardDescription(elementalMastery)); // Regenerate with scaling info
        var elementalMasteryUpgraded = CreateUpgradedVersion(elementalMastery, "Arcane Supremacy", 1.2f, -10, true, CardEffectType.DrawCard, 2);
        LinkCardUpgrade(elementalMastery, elementalMasteryUpgraded, UpgradeConditionType.PlayedMultipleTimesInTurn, 2);
        cards.Add((elementalMastery, elementalMasteryUpgraded));
        
        CreateDeckAsset("Mystic Starter Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Character", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Character/Mystic");
    }

    /// <summary>
    /// Creates an Assassin-themed character deck (Combo + Stealth focus)
    /// </summary>
    public static void CreateAssassinDeck()
    {
        /* Debug.Log("Creating Assassin Deck..."); */
        
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS ===
        
        // Quick Strike - Basic combo attack (STARTER: Basic attack, not draftable)
        var quickStrike = CreateDamageCard("Quick Strike", 15, 15, CardTargetType.Opponent, CardCategory.Starter);
        quickStrike.MakeComboCard();
        SetCardDescription(quickStrike, GenerateCompleteCardDescription(quickStrike)); // Regenerate with combo info
        var quickStrikeUpgraded = CreateUpgradedVersion(quickStrike, "Swift Blade", 1.3f, -5, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(quickStrike, quickStrikeUpgraded, UpgradeConditionType.TimesPlayedThisFight, 6);
        cards.Add((quickStrike, quickStrikeUpgraded));
        
        // Poison Strike - Advanced DoT combo (DRAFTABLE: DoT + combo mechanic)
        var poisonStrike = CreateMultiEffectCard("Poison Strike", 20, CardType.Combo, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 12, CardTargetType.Opponent, 0),
            (CardEffectType.ApplyBurn, 6, CardTargetType.Opponent, 0)
        }, CardCategory.Draftable);
        poisonStrike.MakeComboCard();
        SetCardDescription(poisonStrike, GenerateCompleteCardDescription(poisonStrike)); // Regenerate with combo info
        var poisonStrikeUpgraded = CreateUpgradedVersion(poisonStrike, "Venom Blade", 1.4f, 0, false);
        LinkCardUpgrade(poisonStrike, poisonStrikeUpgraded, UpgradeConditionType.ComboCountReached, 5);
        cards.Add((poisonStrike, poisonStrikeUpgraded));
        
        // === DEFENSIVE CARDS ===
        
        // Dodge - Basic evasion (STARTER: Basic defend, not draftable)
        var dodge = CreateMultiEffectCard("Dodge", 20, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 20, CardTargetType.Self, 0),
            (CardEffectType.RestoreEnergy, 15, CardTargetType.Self, 0)
        }, CardCategory.Starter);
        var dodgeUpgraded = CreateUpgradedVersion(dodge, "Shadow Step", 1.5f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(dodge, dodgeUpgraded, UpgradeConditionType.PlayedAtLowHealth, 2);
        cards.Add((dodge, dodgeUpgraded));
        
        // Smoke Bomb - Advanced debuff defense (DRAFTABLE: Multi-effect + debuff)
        var smokeBomb = CreateMultiEffectCard("Smoke Bomb", 25, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 25, CardTargetType.Self, 0),
            (CardEffectType.ApplyWeak, 1, CardTargetType.Opponent, 3)
        }, CardCategory.Draftable);
        var smokeBombUpgraded = CreateUpgradedVersion(smokeBomb, "Shadow Veil", 1.3f, 0, true, CardEffectType.ApplyStun, 1);
        LinkCardUpgrade(smokeBomb, smokeBombUpgraded, UpgradeConditionType.ComboUseBackToBack, 3);
        cards.Add((smokeBomb, smokeBombUpgraded));
        
        // === UNIQUE CARDS ===
        
        // Assassinate - Advanced combo finisher (DRAFTABLE: Combo requirement + scaling)
        var assassinate = CreateDamageCard("Assassinate", 45, 50, CardTargetType.Opponent, CardCategory.Draftable);
        assassinate.RequireCombo(3);
        assassinate.Effects[0].scalingType = ScalingType.ComboCount;
        assassinate.Effects[0].scalingMultiplier = 15.0f;
        SetCardDescription(assassinate, GenerateCompleteCardDescription(assassinate)); // Regenerate with combo + scaling info
        var assassinateUpgraded = CreateUpgradedVersion(assassinate, "Death Strike", 1.3f, -10, true, CardEffectType.DrawCard, 2);
        LinkCardUpgrade(assassinate, assassinateUpgraded, UpgradeConditionType.DefeatedOpponentWithCard, 1);
        cards.Add((assassinate, assassinateUpgraded));
        
        // Shadow Clone - Advanced utility (DRAFTABLE: Multi-draw + energy)
        var shadowClone = CreateMultiEffectCard("Shadow Clone", 30, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.DrawCard, 2, CardTargetType.Self, 0),
            (CardEffectType.RestoreEnergy, 20, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        var shadowCloneUpgraded = CreateUpgradedVersion(shadowClone, "Mirror Image", 1.0f, -10, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(shadowClone, shadowCloneUpgraded, UpgradeConditionType.PlayedMultipleTimesInTurn, 2);
        cards.Add((shadowClone, shadowCloneUpgraded));
        
        CreateDeckAsset("Assassin Starter Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Character", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Character/Assassin");
    }

    /// <summary>
    /// Creates an Elemental-themed pet deck (Elemental rotation + battlefield control)
    /// </summary>
    public static void CreateElementalDeck()
    {
        /* Debug.Log("Creating Elemental Pet Deck..."); */
        
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS ===
        
        // Flame Burst - Basic elemental attack (STARTER: Basic attack, not draftable)
        var flameBurst = CreateDamageCard("Flame Burst", 22, 25, CardTargetType.Opponent, CardCategory.Starter);
        flameBurst.Effects[0].elementalType = ElementalType.Fire;
        SetCardDescription(flameBurst, GenerateCompleteCardDescription(flameBurst)); // Regenerate with elemental info
        var flameBurstUpgraded = CreateUpgradedVersion(flameBurst, "Solar Flare", 1.4f, 0, true, CardEffectType.ApplyBurn, 5);
        LinkCardUpgrade(flameBurst, flameBurstUpgraded, UpgradeConditionType.TimesPlayedThisFight, 3);
        cards.Add((flameBurst, flameBurstUpgraded));
        
        // Frost Bolt - Advanced debuff attack (DRAFTABLE: Multi-effect + debuff)
        var frostBolt = CreateMultiEffectCard("Frost Bolt", 28, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 18, CardTargetType.Opponent, 0),
            (CardEffectType.ApplyWeak, 1, CardTargetType.Opponent, 2)
        }, CardCategory.Draftable);
        frostBolt.Effects[0].elementalType = ElementalType.Ice;
        SetCardDescription(frostBolt, GenerateCompleteCardDescription(frostBolt)); // Regenerate with elemental info
        var frostBoltUpgraded = CreateUpgradedVersion(frostBolt, "Blizzard Strike", 1.3f, 0, true, CardEffectType.ApplyStun, 1);
        LinkCardUpgrade(frostBolt, frostBoltUpgraded, UpgradeConditionType.DamageDealtThisFight, 120);
        cards.Add((frostBolt, frostBoltUpgraded));
        
        // === DEFENSIVE CARDS ===
        
        // Earth Shield - Basic elemental defense (STARTER: Basic defend, not draftable)
        var earthShield = CreateStatusCard("Earth Shield", CardEffectType.ApplyShield, 38, 0, 30, CardTargetType.Self, CardCategory.Starter);
        var earthShieldUpgraded = CreateUpgradedVersion(earthShield, "Stone Fortress", 1.3f, 0, true, CardEffectType.ApplyThorns, 12);
        LinkCardUpgrade(earthShield, earthShieldUpgraded, UpgradeConditionType.DamageTakenThisFight, 80);
        cards.Add((earthShield, earthShieldUpgraded));
        
        // Wind Barrier - Advanced utility defense (DRAFTABLE: Shield + energy)
        var windBarrier = CreateMultiEffectCard("Wind Barrier", 25, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 28, CardTargetType.Self, 0),
            (CardEffectType.RestoreEnergy, 12, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        var windBarrierUpgraded = CreateUpgradedVersion(windBarrier, "Gale Force", 1.4f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(windBarrier, windBarrierUpgraded, UpgradeConditionType.HealingReceivedThisFight, 40);
        cards.Add((windBarrier, windBarrierUpgraded));
        
        // === UNIQUE CARDS ===
        
        // Elemental Fusion - Advanced scaling (DRAFTABLE: Complex scaling + void element)
        var elementalFusion = CreateDamageCard("Elemental Fusion", 8, 35, CardTargetType.Opponent, CardCategory.Draftable);
        elementalFusion.Effects[0].scalingType = ScalingType.CardsPlayedThisTurn;
        elementalFusion.Effects[0].scalingMultiplier = 6.0f;
        elementalFusion.Effects[0].elementalType = ElementalType.Void;
        SetCardDescription(elementalFusion, GenerateCompleteCardDescription(elementalFusion)); // Regenerate with scaling + elemental info
        var elementalFusionUpgraded = CreateUpgradedVersion(elementalFusion, "Primal Convergence", 1.5f, 0, true, CardEffectType.DrawCard, 2);
        LinkCardUpgrade(elementalFusion, elementalFusionUpgraded, UpgradeConditionType.PlayedMultipleTimesInTurn, 2);
        cards.Add((elementalFusion, elementalFusionUpgraded));
        
        // Storm Call - Advanced ally support (DRAFTABLE: Ally targeting + high cost)
        var stormCall = CreateMultiEffectCard("Storm Call", 40, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 25, CardTargetType.Opponent, 0),
            (CardEffectType.RestoreEnergy, 30, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        stormCall.Effects[0].elementalType = ElementalType.Lightning;
        SetCardDescription(stormCall, GenerateCompleteCardDescription(stormCall)); // Regenerate with elemental info
        var stormCallUpgraded = CreateUpgradedVersion(stormCall, "Tempest", 1.2f, -5, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(stormCall, stormCallUpgraded, UpgradeConditionType.TimesPlayedThisFight, 2);
        cards.Add((stormCall, stormCallUpgraded));
        
        CreateDeckAsset("Elemental Pet Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Pet", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Pet/Elemental");
    }

    /// <summary>
    /// Creates a Beast-themed pet deck (Raw power + pack tactics)
    /// </summary>
    public static void CreateBeastDeck()
    {
        /* Debug.Log("Creating Beast Pet Deck..."); */
        
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS ===
        
        // Savage Bite - Basic beast attack (STARTER: Basic attack, not draftable)
        var savageBite = CreateDamageCard("Savage Bite", 28, 30, CardTargetType.Opponent, CardCategory.Starter);
        var savageBiteUpgraded = CreateUpgradedVersion(savageBite, "Primal Maw", 1.5f, 0, true, CardEffectType.ApplyBurn, 8);
        LinkCardUpgrade(savageBite, savageBiteUpgraded, UpgradeConditionType.DamageDealtThisFight, 140);
        cards.Add((savageBite, savageBiteUpgraded));
        
        // Pack Hunt - Advanced ally support (DRAFTABLE: Ally targeting + buff)
        var packHunt = CreateMultiEffectCard("Pack Hunt", 35, CardType.Attack, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 20, CardTargetType.Opponent, 0),
            (CardEffectType.ApplyStrength, 2, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var packHuntUpgraded = CreateUpgradedVersion(packHunt, "Alpha Strike", 1.3f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(packHunt, packHuntUpgraded, UpgradeConditionType.TimesPlayedThisFight, 3);
        cards.Add((packHunt, packHuntUpgraded));
        
        // === DEFENSIVE CARDS ===
        
        // Thick Hide - Basic beast defense (STARTER: Basic defend, not draftable)
        var thickHide = CreateStatusCard("Thick Hide", CardEffectType.ApplyShield, 45, 0, 25, CardTargetType.Self, CardCategory.Starter);
        var thickHideUpgraded = CreateUpgradedVersion(thickHide, "Armored Scales", 1.3f, 0, true, CardEffectType.ApplyThorns, 10);
        LinkCardUpgrade(thickHide, thickHideUpgraded, UpgradeConditionType.DamageTakenThisFight, 100);
        cards.Add((thickHide, thickHideUpgraded));
        
        // Howl - Advanced intimidation (DRAFTABLE: Multi-effect debuff/buff)
        var howl = CreateMultiEffectCard("Howl", 20, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyWeak, 1, CardTargetType.Opponent, 3),
            (CardEffectType.ApplyStrength, 2, CardTargetType.Self, 0)
        }, CardCategory.Draftable);
        var howlUpgraded = CreateUpgradedVersion(howl, "Terrifying Roar", 1.5f, 0, true, CardEffectType.ApplyStun, 1);
        LinkCardUpgrade(howl, howlUpgraded, UpgradeConditionType.PlayedAtLowHealth, 1);
        cards.Add((howl, howlUpgraded));
        
        // === UNIQUE CARDS ===
        
        // Feral Rage - Advanced scaling (DRAFTABLE: Missing health scaling)
        var feralRage = CreateDamageCard("Feral Rage", 15, 30, CardTargetType.Opponent, CardCategory.Draftable);
        feralRage.Effects[0].scalingType = ScalingType.MissingHealth;
        feralRage.Effects[0].scalingMultiplier = 0.4f;
        SetCardDescription(feralRage, GenerateCompleteCardDescription(feralRage)); // Regenerate with scaling info
        var feralRageUpgraded = CreateUpgradedVersion(feralRage, "Berserk Fury", 1.2f, 0, true, CardEffectType.ApplyStrength, 3);
        LinkCardUpgrade(feralRage, feralRageUpgraded, UpgradeConditionType.PlayedAtLowHealth, 3);
        cards.Add((feralRage, feralRageUpgraded));
        
        // Blood Bond - Advanced life link (DRAFTABLE: Damage + heal ally)
        var bloodBond = CreateMultiEffectCard("Blood Bond", 40, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 20, CardTargetType.Opponent, 0),
            (CardEffectType.Heal, 15, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var bloodBondUpgraded = CreateUpgradedVersion(bloodBond, "Life Link", 1.4f, -5, true, CardEffectType.ApplyStrength, 1);
        LinkCardUpgrade(bloodBond, bloodBondUpgraded, UpgradeConditionType.HealingReceivedThisFight, 60);
        cards.Add((bloodBond, bloodBondUpgraded));
        
        CreateDeckAsset("Beast Pet Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Pet", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Pet/Beast");
    }

    /// <summary>
    /// Creates a Spirit-themed pet deck (Support + healing focus)
    /// </summary>
    public static void CreateSpiritDeck()
    {
        /* Debug.Log("Creating Spirit Pet Deck..."); */
        
        var cards = new List<(CardData baseCard, CardData upgradedCard)>();
        
        // === OFFENSIVE CARDS ===
        
        // Spirit Bolt - Basic spirit attack (STARTER: Basic attack, not draftable)
        var spiritBolt = CreateMultiEffectCard("Spirit Bolt", 30, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 18, CardTargetType.Opponent, 0),
            (CardEffectType.RestoreEnergy, 15, CardTargetType.Ally, 0)
        }, CardCategory.Starter);
        var spiritBoltUpgraded = CreateUpgradedVersion(spiritBolt, "Soul Lance", 1.4f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(spiritBolt, spiritBoltUpgraded, UpgradeConditionType.TimesPlayedThisFight, 4);
        cards.Add((spiritBolt, spiritBoltUpgraded));
        
        // Drain Life - Advanced life steal (DRAFTABLE: Damage + heal ally)
        var drainLife = CreateMultiEffectCard("Drain Life", 35, CardType.Spell, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Damage, 15, CardTargetType.Opponent, 0),
            (CardEffectType.Heal, 20, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var drainLifeUpgraded = CreateUpgradedVersion(drainLife, "Soul Siphon", 1.5f, 0, false);
        LinkCardUpgrade(drainLife, drainLifeUpgraded, UpgradeConditionType.HealingReceivedThisFight, 80);
        cards.Add((drainLife, drainLifeUpgraded));
        
        // === DEFENSIVE CARDS ===
        
        // Spirit Shield - Basic ally defense (STARTER: Basic defend ally, not draftable)
        var spiritShield = CreateStatusCard("Spirit Shield", CardEffectType.ApplyShield, 35, 0, 25, CardTargetType.Ally, CardCategory.Starter);
        var spiritShieldUpgraded = CreateUpgradedVersion(spiritShield, "Ethereal Ward", 1.4f, 0, true, CardEffectType.ApplySalve, 8);
        LinkCardUpgrade(spiritShield, spiritShieldUpgraded, UpgradeConditionType.DamageTakenThisFight, 70);
        cards.Add((spiritShield, spiritShieldUpgraded));
        
        // Calming Presence - Advanced support (DRAFTABLE: Multi-heal + energy)
        var calmingPresence = CreateMultiEffectCard("Calming Presence", 30, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Heal, 25, CardTargetType.Ally, 0),
            (CardEffectType.RestoreEnergy, 20, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var calmingPresenceUpgraded = CreateUpgradedVersion(calmingPresence, "Peaceful Aura", 1.3f, 0, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(calmingPresence, calmingPresenceUpgraded, UpgradeConditionType.HealingReceivedThisFight, 100);
        cards.Add((calmingPresence, calmingPresenceUpgraded));
        
        // === UNIQUE CARDS ===
        
        // Spectral Form - Advanced dual shield (DRAFTABLE: Multi-target shield)
        var spectralForm = CreateMultiEffectCard("Spectral Form", 35, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.ApplyShield, 50, CardTargetType.Self, 0),
            (CardEffectType.ApplyShield, 25, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var spectralFormUpgraded = CreateUpgradedVersion(spectralForm, "Phantom State", 1.2f, 0, true, CardEffectType.ApplySalve, 10);
        LinkCardUpgrade(spectralForm, spectralFormUpgraded, UpgradeConditionType.PlayedAtLowHealth, 2);
        cards.Add((spectralForm, spectralFormUpgraded));
        
        // Soul Link - Advanced triple support (DRAFTABLE: Triple effect card)
        var soulLink = CreateMultiEffectCard("Soul Link", 25, CardType.Skill, new List<(CardEffectType, int, CardTargetType, int)>
        {
            (CardEffectType.Heal, 20, CardTargetType.Self, 0),
            (CardEffectType.Heal, 20, CardTargetType.Ally, 0),
            (CardEffectType.RestoreEnergy, 15, CardTargetType.Ally, 0)
        }, CardCategory.Draftable);
        var soulLinkUpgraded = CreateUpgradedVersion(soulLink, "Eternal Bond", 1.5f, -5, true, CardEffectType.DrawCard, 1);
        LinkCardUpgrade(soulLink, soulLinkUpgraded, UpgradeConditionType.TimesPlayedThisFight, 2);
        cards.Add((soulLink, soulLinkUpgraded));
        
        CreateDeckAsset("Spirit Pet Deck", cards, "Assets/MVPScripts/CardObject/GeneratedCards/Decks/Pet", "Assets/MVPScripts/CardObject/GeneratedCards/Cards/Pet/Spirit");
    }

    /// <summary>
    /// Creates a deck asset from a list of card pairs and duplicates cards to reach 10 total
    /// </summary>
    public static void CreateDeckAsset(string deckName, List<(CardData baseCard, CardData upgradedCard)> cardPairs, string deckFolderPath, string cardFolderPath)
    {
        // Create the deck
        DeckData deck = ScriptableObject.CreateInstance<DeckData>();
        
        // Use reflection to set the private fields
        var deckNameField = typeof(DeckData).GetField("_deckName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardsField = typeof(DeckData).GetField("_cardsInDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        deckNameField?.SetValue(deck, deckName);
        
        List<CardData> deckCards = new List<CardData>();
        
        // Add cards based on the deck structure:
        // 4 offensive (2 types, 2 copies each)
        // 4 defensive (2 types, 2 copies each)  
        // 2 unique (1 copy each)
        
        if (cardPairs.Count >= 6)
        {
            // Add 2 copies of first offensive card
            deckCards.Add(cardPairs[0].baseCard);
            deckCards.Add(cardPairs[0].baseCard);
            
            // Add 2 copies of second offensive card
            deckCards.Add(cardPairs[1].baseCard);
            deckCards.Add(cardPairs[1].baseCard);
            
            // Add 2 copies of first defensive card
            deckCards.Add(cardPairs[2].baseCard);
            deckCards.Add(cardPairs[2].baseCard);
            
            // Add 2 copies of second defensive card
            deckCards.Add(cardPairs[3].baseCard);
            deckCards.Add(cardPairs[3].baseCard);
            
            // Add 1 copy of each unique card
            deckCards.Add(cardPairs[4].baseCard);
            deckCards.Add(cardPairs[5].baseCard);
        }
        
        cardsField?.SetValue(deck, deckCards);
        
        // Save all cards first
        foreach (var (baseCard, upgradedCard) in cardPairs)
        {
            SaveCardAsAsset(baseCard, cardFolderPath);
            SaveCardAsAsset(upgradedCard, cardFolderPath);
        }
        
        // Save the deck
        SaveDeckAsAsset(deck, deckFolderPath, cardFolderPath);
        
        /* Debug.Log($"Created deck '{deckName}' with {deckCards.Count} cards"); */
    }

    /// <summary>
    /// Saves a DeckData as a ScriptableObject asset (overwrites if exists while preserving references)
    /// </summary>
    public static void SaveDeckAsAsset(DeckData deck, string folderPath, string cardFolderPath = null)
    {
        if (deck == null) return;

        // Ensure directory exists
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Create asset path (will overwrite if exists)
        string assetPath = $"{folderPath}/{deck.DeckName}.asset";
        
        // Check if asset already exists and update it instead of replacing
        DeckData existingDeck = AssetDatabase.LoadAssetAtPath<DeckData>(assetPath);
        if (existingDeck != null)
        {
            // Copy data from new deck to existing deck to preserve references
            var deckNameField = typeof(DeckData).GetField("_deckName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cardsField = typeof(DeckData).GetField("_cardsInDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            deckNameField?.SetValue(existingDeck, deck.DeckName);
            
            // Load the actual saved card assets instead of using the temporary ones
            List<CardData> savedCardsList = new List<CardData>();
            foreach (CardData tempCard in deck.CardsInDeck)
            {
                if (tempCard != null)
                {
                    CardData savedCard = null;
                    
                    // Try to find the saved card asset
                    if (!string.IsNullOrEmpty(cardFolderPath))
                    {
                        // Use provided card folder path
                        string cardAssetPath = $"{cardFolderPath}/{tempCard.CardName}.asset";
                        savedCard = AssetDatabase.LoadAssetAtPath<CardData>(cardAssetPath);
                    }
                    
                    if (savedCard == null)
                    {
                        // Fallback: search for the card by name in the entire project
                        string[] guids = AssetDatabase.FindAssets($"{tempCard.CardName} t:CardData");
                        foreach (string guid in guids)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            CardData foundCard = AssetDatabase.LoadAssetAtPath<CardData>(path);
                            if (foundCard != null && foundCard.CardName == tempCard.CardName)
                            {
                                savedCard = foundCard;
                                break;
                            }
                        }
                    }
                    
                    if (savedCard != null)
                    {
                        savedCardsList.Add(savedCard);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find saved card asset for: {tempCard.CardName}");
                        // Fallback to the temporary card if saved version not found
                        savedCardsList.Add(tempCard);
                    }
                }
            }
            
            cardsField?.SetValue(existingDeck, savedCardsList);
            
            EditorUtility.SetDirty(existingDeck);
            Debug.Log($"Updated existing deck asset (preserving references): {assetPath} with {savedCardsList.Count} cards");
        }
        else
        {
            // Create new asset if it doesn't exist
            AssetDatabase.CreateAsset(deck, assetPath);
            Debug.Log($"Created new deck asset: {assetPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a base CardData object with common settings
    /// </summary>
    private static CardData CreateBaseCard(string cardName, int energyCost, CardType cardType, CardCategory category)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields since we don't have direct access
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var categoryField = typeof(CardData).GetField("_cardCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(card, Random.Range(1000, 9999)); // Generate random ID
        cardNameField?.SetValue(card, cardName);
        energyCostField?.SetValue(card, energyCost);
        cardTypeField?.SetValue(card, cardType);
        categoryField?.SetValue(card, category);
        effectsField?.SetValue(card, new List<CardEffect>());
        
        return card;
    }

    /// <summary>
    /// Sets the description of a card using reflection
    /// </summary>
    private static void SetCardDescription(CardData card, string description)
    {
        var descriptionField = typeof(CardData).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        descriptionField?.SetValue(card, description);
    }

    /// <summary>
    /// Generates description for upgraded cards
    /// </summary>
    private static void GenerateUpgradedDescription(CardData upgradedCard, CardData baseCard, 
        bool addedBonusEffect, CardEffectType bonusType, int bonusAmount)
    {
        // Use the comprehensive description generation that includes all mechanics
        SetCardDescription(upgradedCard, GenerateCompleteCardDescription(upgradedCard));
    }

    /// <summary>
    /// Generates a complete description for a card including all mechanics
    /// </summary>
    private static string GenerateCompleteCardDescription(CardData card)
    {
        if (card == null) return "Invalid card";

        List<string> descriptionParts = new List<string>();

        // Basic effects from the card's effect list
        if (card.HasEffects)
        {
            foreach (var effect in card.Effects)
            {
                string effectDesc = GetEffectDescriptionWithScaling(effect);
                descriptionParts.Add(effectDesc);
            }
        }

        // Stance changes
        if (card.ChangesStance)
        {
            descriptionParts.Add($"Enter {card.NewStance} stance");
        }

        // Combo mechanics
        if (card.BuildsCombo)
        {
            descriptionParts.Add("Builds combo");
        }

        if (card.RequiresCombo)
        {
            string comboText = card.RequiredComboAmount == 1 ? "combo" : $"{card.RequiredComboAmount} combo";
            descriptionParts.Add($"Requires {comboText}");
        }

        // Additional targeting
        List<string> additionalTargets = new List<string>();
        if (card.CanAlsoTargetSelf) additionalTargets.Add("self");
        if (card.CanAlsoTargetAllies) additionalTargets.Add("allies");
        if (card.CanAlsoTargetOpponent) additionalTargets.Add("opponent");
        
        if (additionalTargets.Count > 0)
        {
            descriptionParts.Add($"Can also target: {string.Join(", ", additionalTargets)}");
        }

        // Combine all parts
        if (descriptionParts.Count == 0)
        {
            return "No effect";
        }

        return string.Join(". ", descriptionParts) + ".";
    }

    /// <summary>
    /// Gets effect description including scaling and conditional mechanics
    /// </summary>
    private static string GetEffectDescriptionWithScaling(CardEffect effect)
    {
        string baseDescription = GetEffectDescription(effect.effectType, effect.amount, effect.targetType, effect.duration);

        List<string> modifiers = new List<string>();

        // Add scaling information
        if (effect.scalingType != ScalingType.None)
        {
            string scalingDesc = GetScalingDescription(effect.scalingType, effect.scalingMultiplier);
            modifiers.Add(scalingDesc);
        }

        // Add elemental information
        if (effect.elementalType != ElementalType.None)
        {
            modifiers.Add($"({effect.elementalType})");
        }

        // Add conditional effects
        if (effect.hasAlternativeEffect && effect.conditionType != ConditionalType.None)
        {
            string conditionDesc = GetConditionalDescription(effect.conditionType, effect.conditionValue, 
                effect.alternativeEffectType, effect.alternativeEffectAmount, effect.alternativeLogic);
            modifiers.Add(conditionDesc);
        }

        // Combine base description with modifiers
        if (modifiers.Count > 0)
        {
            return $"{baseDescription} ({string.Join(", ", modifiers)})";
        }

        return baseDescription;
    }

    /// <summary>
    /// Gets scaling description for effects
    /// </summary>
    private static string GetScalingDescription(ScalingType scalingType, float multiplier)
    {
        string baseScaling = multiplier.ToString("F1");
        
        switch (scalingType)
        {
            case ScalingType.ZeroCostCardsThisTurn:
                return $"+{baseScaling} per zero-cost card this turn";
            case ScalingType.ZeroCostCardsThisFight:
                return $"+{baseScaling} per zero-cost card this fight";
            case ScalingType.CardsPlayedThisTurn:
                return $"+{baseScaling} per card played this turn";
            case ScalingType.CardsPlayedThisFight:
                return $"+{baseScaling} per card played this fight";
            case ScalingType.DamageDealtThisTurn:
                return $"+{baseScaling} per damage dealt this turn";
            case ScalingType.DamageDealtThisFight:
                return $"+{baseScaling} per damage dealt this fight";
            case ScalingType.CurrentHealth:
                return $"+{baseScaling} per current health";
            case ScalingType.MissingHealth:
                return $"+{baseScaling} per missing health";
            case ScalingType.ComboCount:
                return $"+{baseScaling} per combo";
            case ScalingType.HandSize:
                return $"+{baseScaling} per card in hand";
            default:
                return $"scales with {scalingType}";
        }
    }

    /// <summary>
    /// Gets conditional effect description
    /// </summary>
    private static string GetConditionalDescription(ConditionalType conditionType, int conditionValue, 
        CardEffectType altEffectType, int altAmount, AlternativeEffectLogic logicType)
    {
        string conditionDesc = GetConditionDescription(conditionType, conditionValue);
        string altEffectDesc = GetSimpleEffectDescription(altEffectType, altAmount);
        
        switch (logicType)
        {
            case AlternativeEffectLogic.Replace:
                return $"if {conditionDesc}: {altEffectDesc} instead";
            case AlternativeEffectLogic.Additional:
                return $"if {conditionDesc}: also {altEffectDesc}";
            default:
                return $"conditional: {altEffectDesc}";
        }
    }

    /// <summary>
    /// Gets condition description for conditional effects
    /// </summary>
    private static string GetConditionDescription(ConditionalType conditionType, int value)
    {
        switch (conditionType)
        {
            case ConditionalType.IfTargetHealthBelow:
                return $"target health < {value}%";
            case ConditionalType.IfTargetHealthAbove:
                return $"target health > {value}%";
            case ConditionalType.IfSourceHealthBelow:
                return $"your health < {value}%";
            case ConditionalType.IfSourceHealthAbove:
                return $"your health > {value}%";
            case ConditionalType.IfCardsInHand:
                return $"{value}+ cards in hand";
            case ConditionalType.IfCardsInDeck:
                return $"{value}+ cards in deck";
            case ConditionalType.IfCardsInDiscard:
                return $"{value}+ cards in discard";
            case ConditionalType.IfTimesPlayedThisFight:
                return $"played {value}+ times this fight";
            case ConditionalType.IfComboCount:
                return $"{value}+ combo";
            case ConditionalType.IfZeroCostCardsThisTurn:
                return $"{value}+ zero-cost cards this turn";
            case ConditionalType.IfZeroCostCardsThisFight:
                return $"{value}+ zero-cost cards this fight";
            case ConditionalType.IfInStance:
                return $"in {(StanceType)value} stance";
            case ConditionalType.IfEnergyRemaining:
                return $"{value}+ energy remaining";
            default:
                return $"condition {conditionType}";
        }
    }

    /// <summary>
    /// Gets simple effect description for conditional alternatives
    /// </summary>
    private static string GetSimpleEffectDescription(CardEffectType effectType, int amount)
    {
        switch (effectType)
        {
            case CardEffectType.Damage:
                return $"deal {amount} damage";
            case CardEffectType.Heal:
                return $"heal {amount} health";
            case CardEffectType.DrawCard:
                return amount == 1 ? "draw a card" : $"draw {amount} cards";
            case CardEffectType.RestoreEnergy:
                return $"restore {amount} energy";
            case CardEffectType.ApplyShield:
                return $"grant {amount} shield";
            case CardEffectType.ApplyStrength:
                return $"grant {amount} Strength";
            case CardEffectType.ApplyThorns:
                return $"grant {amount} Thorns";
            case CardEffectType.ApplyBurn:
                return $"apply {amount} Burn";
            case CardEffectType.ApplySalve:
                return $"apply {amount} Salve";
            case CardEffectType.ApplyWeak:
                return "apply Weak";
            case CardEffectType.ApplyBreak:
                return "apply Break";
            case CardEffectType.ApplyStun:
                return "apply Stun";
            default:
                return $"apply {effectType}";
        }
    }

    /// <summary>
    /// Gets user-friendly description for card effects with full mechanic information
    /// </summary>
    private static string GetEffectDescription(CardEffectType effectType, int amount, CardTargetType target, int duration)
    {
        string targetDesc = GetTargetDescription(target);
        
        switch (effectType)
        {
            case CardEffectType.Damage:
                return $"Deal {amount} damage to {targetDesc}";
            case CardEffectType.Heal:
                return $"Heal {amount} health to {targetDesc}";
            case CardEffectType.DrawCard:
                return amount == 1 ? "Draw a card" : $"Draw {amount} cards";
            case CardEffectType.RestoreEnergy:
                return $"Restore {amount} energy";
            case CardEffectType.ApplyShield:
                return $"Grant {amount} shield to {targetDesc}";
            case CardEffectType.ApplyWeak:
                return $"Apply Weak for {duration} turns to {targetDesc}";
            case CardEffectType.ApplyBreak:
                return $"Apply Break for {duration} turns to {targetDesc}";
            case CardEffectType.ApplyStun:
                return $"Stun {targetDesc} for {duration} turns";
            case CardEffectType.ApplyStrength:
                return $"Grant {amount} Strength to {targetDesc}";
            case CardEffectType.ApplyThorns:
                return $"Grant {amount} Thorns to {targetDesc}";
            case CardEffectType.ApplyBurn:
                return $"Apply {amount} Burn to {targetDesc}";
            case CardEffectType.ApplySalve:
                return $"Apply {amount} Salve to {targetDesc}";
            default:
                return $"Apply {effectType} ({amount}) to {targetDesc}";
        }
    }

    /// <summary>
    /// Gets user-friendly description for status effects
    /// </summary>
    private static string GetStatusDescription(CardEffectType statusType, int potency, int duration, CardTargetType target)
    {
        return GetEffectDescription(statusType, potency, target, duration);
    }

    /// <summary>
    /// Gets user-friendly description for target types
    /// </summary>
    private static string GetTargetDescription(CardTargetType target)
    {
        switch (target)
        {
            case CardTargetType.Self:
                return "yourself";
            case CardTargetType.Opponent:
                return "opponent";
            case CardTargetType.Ally:
                return "ally";
            case CardTargetType.Random:
                return "random target";
            default:
                return "target";
        }
    }

    /// <summary>
    /// Validates that a card has been properly set up
    /// </summary>
    public static bool ValidateCard(CardData card)
    {
        if (card == null)
        {
            Debug.LogError("CardGenerator: Card is null!");
            return false;
        }

        if (string.IsNullOrEmpty(card.CardName))
        {
            Debug.LogError($"CardGenerator: Card has no name!");
            return false;
        }

        if (card.EnergyCost < 0)
        {
            Debug.LogError($"CardGenerator: Card '{card.CardName}' has negative energy cost!");
            return false;
        }

        if (!card.HasEffects)
        {
            Debug.LogWarning($"CardGenerator: Card '{card.CardName}' has no effects - this might be intentional for utility cards");
        }

        /* Debug.Log($"CardGenerator: Card '{card.CardName}' validation passed!"); */
        return true;
    }

    /// <summary>
    /// Prints detailed information about a card for debugging
    /// </summary>
    public static void PrintCardInfo(CardData card)
    {
        if (card == null)
        {
            Debug.Log("CardGenerator: Cannot print info for null card");
            return;
        }

        /* Debug.Log($"═══ CARD INFO: {card.CardName} ═══"); */
        /* Debug.Log($"ID: {card.CardId}"); */
        /* Debug.Log($"Cost: {card.EnergyCost} energy"); */
        /* Debug.Log($"Type: {card.CardType}"); */
        /* Debug.Log($"Category: {card.CardCategory}"); */
        /* Debug.Log($"Description: {card.Description}"); */
        
        if (card.HasEffects)
        {
            Debug.Log($"Effects ({card.Effects.Count}):");
            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                /* Debug.Log($"  {i+1}. {effect.effectType} - Amount: {effect.amount}, Target: {effect.targetType}, Duration: {effect.duration}"); */
            }
        }

        if (card.CanUpgrade)
        {
            /* Debug.Log($"Upgrade: {card.UpgradeConditionType} {card.UpgradeComparisonType} {card.UpgradeRequiredValue}"); */
            /* Debug.Log($"Upgrades to: {(card.UpgradedVersion != null ? card.UpgradedVersion.CardName : "None")}"); */
        }

        /* Debug.Log("═══════════════════════════════════════"); */
    }

    /// <summary>
    /// Saves a CardData as a ScriptableObject asset (overwrites if exists while preserving references)
    /// </summary>
    public static void SaveCardAsAsset(CardData card, string folderPath)
    {
        if (card == null) return;

        // Ensure directory exists
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Create asset path (will overwrite if exists)
        string assetPath = $"{folderPath}/{card.CardName}.asset";
        
        // Check if asset already exists and update it instead of replacing
        CardData existingCard = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
        if (existingCard != null)
        {
            // Copy all data from new card to existing card to preserve references
            var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var categoryField = typeof(CardData).GetField("_cardCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descriptionField = typeof(CardData).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Copy all the basic properties
            cardIdField?.SetValue(existingCard, card.CardId);
            cardNameField?.SetValue(existingCard, card.CardName);
            energyCostField?.SetValue(existingCard, card.EnergyCost);
            cardTypeField?.SetValue(existingCard, card.CardType);
            categoryField?.SetValue(existingCard, card.CardCategory);
            effectsField?.SetValue(existingCard, card.Effects);
            descriptionField?.SetValue(existingCard, card.Description);
            
            EditorUtility.SetDirty(existingCard);
            Debug.Log($"Updated existing card asset (preserving references): {assetPath}");
        }
        else
        {
            // Create new asset if it doesn't exist
            AssetDatabase.CreateAsset(card, assetPath);
            Debug.Log($"Created new card asset: {assetPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

// ═══════════════════════════════════════════════════════════════
// CARD GENERATOR EDITOR WINDOW
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Editor window for creating cards with a GUI interface
/// </summary>
public class CardGeneratorWindow : EditorWindow
{
    // Window variables for custom card creation
    private string cardName = "New Card";
    private int damage = 5;
    private int energyCost = 2;
    private CardTargetType targetType = CardTargetType.Opponent;
    private CardCategory cardCategory = CardCategory.Draftable;
    private CardType cardType = CardType.Attack;
    
    // Upgrade variables
    private bool createUpgrade = true;
    private string upgradeName = "New Card+";
    private float damageMultiplier = 1.4f;
    private bool addBonusEffect = false;
    private CardEffectType bonusEffectType = CardEffectType.DrawCard;
    private int bonusAmount = 1;
    private UpgradeConditionType upgradeCondition = UpgradeConditionType.TimesPlayedThisFight;
    private int upgradeRequiredValue = 3;

    private Vector2 scrollPosition;

    public static void ShowWindow()
    {
        CardGeneratorWindow window = GetWindow<CardGeneratorWindow>("Card Generator");
        window.Show();
    }

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Label("Card Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Basic card properties
        GUILayout.Label("Basic Card Properties", EditorStyles.boldLabel);
        cardName = EditorGUILayout.TextField("Card Name", cardName);
        cardType = (CardType)EditorGUILayout.EnumPopup("Card Type", cardType);
        cardCategory = (CardCategory)EditorGUILayout.EnumPopup("Card Category", cardCategory);
        energyCost = EditorGUILayout.IntField("Energy Cost", energyCost);
        
        EditorGUILayout.Space();

        // Effect properties (simplified for damage cards)
        GUILayout.Label("Card Effect", EditorStyles.boldLabel);
        damage = EditorGUILayout.IntField("Damage Amount", damage);
        targetType = (CardTargetType)EditorGUILayout.EnumPopup("Target", targetType);
        
        EditorGUILayout.Space();

        // Upgrade properties
        GUILayout.Label("Upgrade Settings", EditorStyles.boldLabel);
        createUpgrade = EditorGUILayout.Toggle("Create Upgraded Version", createUpgrade);
        
        if (createUpgrade)
        {
            EditorGUI.indentLevel++;
            upgradeName = EditorGUILayout.TextField("Upgrade Name", upgradeName);
            damageMultiplier = EditorGUILayout.Slider("Damage Multiplier", damageMultiplier, 1.0f, 3.0f);
            
            addBonusEffect = EditorGUILayout.Toggle("Add Bonus Effect", addBonusEffect);
            if (addBonusEffect)
            {
                bonusEffectType = (CardEffectType)EditorGUILayout.EnumPopup("Bonus Effect", bonusEffectType);
                bonusAmount = EditorGUILayout.IntField("Bonus Amount", bonusAmount);
            }
            
            EditorGUILayout.Space();
            upgradeCondition = (UpgradeConditionType)EditorGUILayout.EnumPopup("Upgrade Condition", upgradeCondition);
            upgradeRequiredValue = EditorGUILayout.IntField("Required Value", upgradeRequiredValue);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // Create button
        if (GUILayout.Button("Create Card(s)", GUILayout.Height(30)))
        {
            CreateCustomCard();
        }

        EditorGUILayout.Space();

        // Quick actions
        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Create Example Damage Cards"))
        {
            CardGenerator.CreateExampleDamageCardsMenuItem();
        }
        
        if (GUILayout.Button("Create Full Starter Set"))
        {
            CardGenerator.CreateStarterCardSetMenuItem();
        }

        EditorGUILayout.EndScrollView();
    }

    private void CreateCustomCard()
    {
        // Create base card
        CardData baseCard = CardGenerator.CreateDamageCard(cardName, damage, energyCost, targetType, cardCategory);
        
        // Override card type if needed
        if (cardType != CardType.Attack)
        {
            var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardTypeField?.SetValue(baseCard, cardType);
        }

        CardGenerator.SaveCardAsAsset(baseCard, "Assets/MVPScripts/CardObject/GeneratedCards");

        CardData upgradedCard = null;
        if (createUpgrade)
        {
            // Create upgraded version
            upgradedCard = CardGenerator.CreateUpgradedVersion(
                baseCard, 
                upgradeName, 
                damageMultiplier, 
                0, 
                addBonusEffect, 
                bonusEffectType, 
                bonusAmount
            );

            // Link the upgrade
            CardGenerator.LinkCardUpgrade(
                baseCard, 
                upgradedCard, 
                upgradeCondition, 
                upgradeRequiredValue
            );

            CardGenerator.SaveCardAsAsset(upgradedCard, "Assets/MVPScripts/CardObject/GeneratedCards");
        }

        // Validate and select
        CardGenerator.ValidateCard(baseCard);
        if (upgradedCard != null)
            CardGenerator.ValidateCard(upgradedCard);

        Selection.activeObject = baseCard;
        EditorGUIUtility.PingObject(baseCard);

        string message = $"Created card: {baseCard.CardName}";
        if (upgradedCard != null)
            message += $" -> {upgradedCard.CardName}";
        
        /* Debug.Log(message); */
    }
} 