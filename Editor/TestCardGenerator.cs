using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Editor tool for generating test cards that cover all possible card mechanics
/// </summary>
public class TestCardGenerator : EditorWindow
{
    private string outputPath = "Assets/MVPScripts/Testing/GeneratedTestCards";
    private bool generateBasicEffects = true;
    private bool generateStatusEffects = true;
    private bool generateConditionalEffects = true;
    private bool generateScalingEffects = true;
    private bool generateComboEffects = true;
    private bool generateStanceEffects = true;
    private bool generateTargetingVariations = true;

    [MenuItem("Tools/Card Generator/Test Card Generator")]
    public static void ShowWindow()
    {
        GetWindow<TestCardGenerator>("Test Card Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Test Card Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Output path
        EditorGUILayout.LabelField("Output Path:");
        outputPath = EditorGUILayout.TextField(outputPath);
        
        if (GUILayout.Button("Browse"))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select Output Folder", outputPath, "");
            if (!string.IsNullOrEmpty(newPath))
            {
                outputPath = newPath;
            }
        }

        GUILayout.Space(10);

        // Generation options
        GUILayout.Label("Generation Options:", EditorStyles.boldLabel);
        generateBasicEffects = EditorGUILayout.Toggle("Basic Effects (Damage, Heal, etc.)", generateBasicEffects);
        generateStatusEffects = EditorGUILayout.Toggle("Status Effects", generateStatusEffects);
        generateConditionalEffects = EditorGUILayout.Toggle("Conditional Effects", generateConditionalEffects);
        generateScalingEffects = EditorGUILayout.Toggle("Scaling Effects", generateScalingEffects);
        generateComboEffects = EditorGUILayout.Toggle("Combo Effects", generateComboEffects);
        generateStanceEffects = EditorGUILayout.Toggle("Stance Effects", generateStanceEffects);
        generateTargetingVariations = EditorGUILayout.Toggle("Targeting Variations", generateTargetingVariations);

        GUILayout.Space(10);

        if (GUILayout.Button("Generate All Test Cards", GUILayout.Height(30)))
        {
            GenerateTestCards();
        }

        if (GUILayout.Button("Clear Generated Cards"))
        {
            ClearGeneratedCards();
        }
    }

    private void GenerateTestCards()
    {
        // Ensure output directory exists
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        int cardId = 9000; // Start at high ID to avoid conflicts
        var generatedCards = new List<CardData>();

        if (generateBasicEffects)
        {
            generatedCards.AddRange(GenerateBasicEffectCards(ref cardId));
        }

        if (generateStatusEffects)
        {
            generatedCards.AddRange(GenerateStatusEffectCards(ref cardId));
        }

        if (generateConditionalEffects)
        {
            generatedCards.AddRange(GenerateConditionalEffectCards(ref cardId));
        }

        if (generateScalingEffects)
        {
            generatedCards.AddRange(GenerateScalingEffectCards(ref cardId));
        }

        if (generateComboEffects)
        {
            generatedCards.AddRange(GenerateComboEffectCards(ref cardId));
        }

        if (generateStanceEffects)
        {
            generatedCards.AddRange(GenerateStanceEffectCards(ref cardId));
        }

        if (generateTargetingVariations)
        {
            generatedCards.AddRange(GenerateTargetingVariations(ref cardId));
        }

        // Save all generated cards
        foreach (var card in generatedCards)
        {
            string fileName = $"TEST_{card.CardName.Replace(" ", "_")}.asset";
            string fullPath = Path.Combine(outputPath, fileName);
            AssetDatabase.CreateAsset(card, fullPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Generated {generatedCards.Count} test cards in {outputPath}");
    }

    private List<CardData> GenerateBasicEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        // Basic damage cards
        cards.Add(CreateBasicCard("Test Basic Damage", CardEffectType.Damage, 5, CardTargetType.Opponent, 1, cardId++));
        cards.Add(CreateBasicCard("Test Heavy Damage", CardEffectType.Damage, 15, CardTargetType.Opponent, 3, cardId++));
        cards.Add(CreateBasicCard("Test Self Damage", CardEffectType.Damage, 3, CardTargetType.Self, 1, cardId++));

        // Basic heal cards
        cards.Add(CreateBasicCard("Test Basic Heal", CardEffectType.Heal, 8, CardTargetType.Self, 2, cardId++));
        cards.Add(CreateBasicCard("Test Heal Ally", CardEffectType.Heal, 6, CardTargetType.Ally, 2, cardId++));
        cards.Add(CreateBasicCard("Test Heal Opponent", CardEffectType.Heal, 4, CardTargetType.Opponent, 1, cardId++));

        // Energy cards
        cards.Add(CreateBasicCard("Test Restore Energy", CardEffectType.RestoreEnergy, 3, CardTargetType.Self, 1, cardId++));
        cards.Add(CreateBasicCard("Test Draw Card", CardEffectType.DrawCard, 2, CardTargetType.Self, 1, cardId++));

        return cards;
    }

    private List<CardData> GenerateStatusEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        // All status effects
        var statusEffects = new[]
        {
            CardEffectType.ApplyBreak, CardEffectType.ApplyWeak, CardEffectType.ApplyBurn,
            CardEffectType.ApplySalve, CardEffectType.RaiseCriticalChance, CardEffectType.ApplyThorns,
            CardEffectType.ApplyShield, CardEffectType.ApplyStun, CardEffectType.ApplyLimitBreak,
            CardEffectType.ApplyStrength, CardEffectType.ApplyCurse
        };

        foreach (var effect in statusEffects)
        {
            cards.Add(CreateBasicCard($"Test {effect}", effect, 2, CardTargetType.Self, 2, cardId++));
            cards.Add(CreateBasicCard($"Test {effect} Opponent", effect, 2, CardTargetType.Opponent, 2, cardId++));
        }

        // Elemental status effects
        var elementalTypes = new[] { ElementalType.Fire, ElementalType.Ice, ElementalType.Lightning, ElementalType.Void };
        foreach (var element in elementalTypes)
        {
            var card = CreateBasicCard($"Test Elemental {element}", CardEffectType.ApplyElementalStatus, 1, CardTargetType.Opponent, 1, cardId++);
            card.Effects[0].elementalType = element;
            cards.Add(card);
        }

        return cards;
    }

    private List<CardData> GenerateConditionalEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        var conditionalTypes = System.Enum.GetValues(typeof(ConditionalType)).Cast<ConditionalType>()
            .Where(c => c != ConditionalType.None).ToArray();

        foreach (var conditionType in conditionalTypes)
        {
            var card = CreateCardWithConditionalEffect($"Test {conditionType}", conditionType, cardId++);
            cards.Add(card);
        }

        return cards;
    }

    private List<CardData> GenerateScalingEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        var scalingTypes = System.Enum.GetValues(typeof(ScalingType)).Cast<ScalingType>()
            .Where(s => s != ScalingType.None).ToArray();

        foreach (var scalingType in scalingTypes)
        {
            var card = CreateCardWithScaling($"Test Scaling {scalingType}", scalingType, cardId++);
            cards.Add(card);
        }

        return cards;
    }

    private List<CardData> GenerateComboEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        // Cards that build combo
        var comboBuilder = CreateBasicCard("Test Combo Builder", CardEffectType.Damage, 3, CardTargetType.Opponent, 1, cardId++);
        comboBuilder.MakeComboCard();
        cards.Add(comboBuilder);

        // Cards that require combo
        var comboSpender = CreateBasicCard("Test Combo Spender", CardEffectType.Damage, 8, CardTargetType.Opponent, 2, cardId++);
        comboSpender.RequireCombo(1);
        cards.Add(comboSpender);

        var bigComboSpender = CreateBasicCard("Test Big Combo Spender", CardEffectType.Damage, 15, CardTargetType.Opponent, 3, cardId++);
        bigComboSpender.RequireCombo(3);
        cards.Add(bigComboSpender);

        return cards;
    }

    private List<CardData> GenerateStanceEffectCards(ref int cardId)
    {
        var cards = new List<CardData>();

        var stanceTypes = System.Enum.GetValues(typeof(StanceType)).Cast<StanceType>()
            .Where(s => s != StanceType.None).ToArray();

        foreach (var stanceType in stanceTypes)
        {
            var card = CreateBasicCard($"Test Enter {stanceType}", CardEffectType.Damage, 2, CardTargetType.Opponent, 1, cardId++);
            card.ChangeStance(stanceType);
            cards.Add(card);
        }

        return cards;
    }

    private List<CardData> GenerateTargetingVariations(ref int cardId)
    {
        var cards = new List<CardData>();

        // Multi-target cards
        var multiTargetCard = CreateBasicCard("Test Multi Target", CardEffectType.Damage, 3, CardTargetType.Opponent, 2, cardId++);
        multiTargetCard.AddEffect(CardEffectType.Heal, 2, CardTargetType.Self);
        cards.Add(multiTargetCard);

        // Random target
        cards.Add(CreateBasicCard("Test Random Target", CardEffectType.Damage, 6, CardTargetType.Random, 2, cardId++));

        return cards;
    }

    private CardData CreateBasicCard(string name, CardEffectType effectType, int amount, CardTargetType targetType, int cost, int id)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var descriptionField = typeof(CardData).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardCategoryField = typeof(CardData).GetField("_cardCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(card, id);
        cardNameField?.SetValue(card, name);
        descriptionField?.SetValue(card, $"Test card: {effectType} {amount} to {targetType}");
        energyCostField?.SetValue(card, cost);
        cardCategoryField?.SetValue(card, CardCategory.Draftable);
        
        var effects = new List<CardEffect>
        {
            new CardEffect
            {
                effectType = effectType,
                amount = amount,
                targetType = targetType,
                duration = GetDefaultDurationForEffect(effectType)
            }
        };
        
        effectsField?.SetValue(card, effects);
        
        return card;
    }

    private CardData CreateCardWithConditionalEffect(string name, ConditionalType conditionType, int id)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var descriptionField = typeof(CardData).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardCategoryField = typeof(CardData).GetField("_cardCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(card, id);
        cardNameField?.SetValue(card, name);
        descriptionField?.SetValue(card, $"Test card: Damage 3 to Opponent");
        energyCostField?.SetValue(card, 2);
        cardCategoryField?.SetValue(card, CardCategory.Draftable);
        
        // Create ONLY the conditional effect (no basic effect)
        var effects = new List<CardEffect>
        {
            new CardEffect
            {
                effectType = CardEffectType.Damage,
                amount = 3,
                targetType = CardTargetType.Opponent,
                conditionType = conditionType,
                conditionValue = GetDefaultConditionValue(conditionType),
                hasAlternativeEffect = true,
                alternativeEffectType = CardEffectType.Heal,
                alternativeEffectAmount = 2,
                alternativeLogic = AlternativeEffectLogic.Replace
            }
        };
        
        effectsField?.SetValue(card, effects);
        
        return card;
    }

    private CardData CreateCardWithScaling(string name, ScalingType scalingType, int id)
    {
        var card = CreateBasicCard(name, CardEffectType.Damage, 1, CardTargetType.Opponent, 1, id);
        
        // Add scaling to the effect
        card.Effects[0].scalingType = scalingType;
        card.Effects[0].scalingMultiplier = 1.0f;
        card.Effects[0].maxScaling = 10;
        
        return card;
    }

    private int GetDefaultDurationForEffect(CardEffectType effectType)
    {
        switch (effectType)
        {
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBurn:
            case CardEffectType.ApplySalve:
            case CardEffectType.ApplyStun:
                return 2;
            case CardEffectType.ApplyShield:
            case CardEffectType.ApplyThorns:
                return 1;
            default:
                return 0;
        }
    }

    private int GetDefaultConditionValue(ConditionalType conditionType)
    {
        switch (conditionType)
        {
            case ConditionalType.IfTargetHealthBelow:
            case ConditionalType.IfSourceHealthBelow:
                return 50;
            case ConditionalType.IfTargetHealthAbove:
            case ConditionalType.IfSourceHealthAbove:
                return 75;
            case ConditionalType.IfCardsInHand:
                return 3;
            case ConditionalType.IfComboCount:
                return 2;
            default:
                return 1;
        }
    }

    private void ClearGeneratedCards()
    {
        if (Directory.Exists(outputPath))
        {
            var testCards = Directory.GetFiles(outputPath, "TEST_*.asset");
            foreach (var card in testCards)
            {
                AssetDatabase.DeleteAsset(card);
            }
            AssetDatabase.Refresh();
            Debug.Log($"Cleared {testCards.Length} test cards");
        }
    }
} 