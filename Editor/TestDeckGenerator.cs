using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Editor tool for generating test decks containing organized test cards
/// </summary>
public class TestDeckGenerator : EditorWindow
{
    private string outputPath = "Assets/MVPScripts/Testing/GeneratedTestDecks";
    private bool createCategorizedDecks = true;
    private bool createMasterDeck = true;
    private bool createSmallTestDeck = true;

    [MenuItem("Tools/Card Generator/Test Deck Generator")]
    public static void ShowWindow()
    {
        GetWindow<TestDeckGenerator>("Test Deck Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Test Deck Generator", EditorStyles.boldLabel);
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
        GUILayout.Label("Deck Generation Options:", EditorStyles.boldLabel);
        createMasterDeck = EditorGUILayout.Toggle("Master Test Deck (All Cards)", createMasterDeck);
        createCategorizedDecks = EditorGUILayout.Toggle("Categorized Decks (By Effect Type)", createCategorizedDecks);
        createSmallTestDeck = EditorGUILayout.Toggle("Small Test Deck (Core Effects Only)", createSmallTestDeck);

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Test Decks", GUILayout.Height(30)))
        {
            GenerateTestDecks();
        }

        if (GUILayout.Button("Clear Generated Decks"))
        {
            ClearGeneratedDecks();
        }

        GUILayout.Space(10);

        // Show current test cards count
        var testCards = FindAllTestCards();
        EditorGUILayout.HelpBox($"Found {testCards.Count} test cards to include in decks", MessageType.Info);
    }

    private void GenerateTestDecks()
    {
        // Ensure output directory exists
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var testCards = FindAllTestCards();
        if (testCards.Count == 0)
        {
            EditorUtility.DisplayDialog("No Test Cards", "No test cards found. Generate test cards first using the Test Card Generator.", "OK");
            return;
        }

        var generatedDecks = new List<DeckData>();

        // Generate master deck with all test cards
        if (createMasterDeck)
        {
            var masterDeck = CreateDeck("Master Test Deck", testCards);
            generatedDecks.Add(masterDeck);
        }

        // Generate categorized decks
        if (createCategorizedDecks)
        {
            generatedDecks.AddRange(CreateCategorizedDecks(testCards));
        }

        // Generate small test deck with core effects
        if (createSmallTestDeck)
        {
            var coreCards = testCards.Where(card => IsCoreTestCard(card)).ToList();
            var smallDeck = CreateDeck("Small Test Deck", coreCards);
            generatedDecks.Add(smallDeck);
        }

        // Save all generated decks
        foreach (var deck in generatedDecks)
        {
            string fileName = $"TESTDECK_{deck.name.Replace(" ", "_")}.asset";
            string fullPath = Path.Combine(outputPath, fileName);
            AssetDatabase.CreateAsset(deck, fullPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Generated {generatedDecks.Count} test decks in {outputPath}");
        EditorUtility.DisplayDialog("Test Decks Generated", $"Successfully generated {generatedDecks.Count} test decks!", "OK");
    }

    private List<CardData> FindAllTestCards()
    {
        var testCards = new List<CardData>();
        var testCardGuids = AssetDatabase.FindAssets("TEST_ t:CardData");
        
        foreach (var guid in testCardGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var cardData = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (cardData != null)
            {
                testCards.Add(cardData);
            }
        }
        
        return testCards.OrderBy(card => card.CardName).ToList();
    }

    private DeckData CreateDeck(string deckName, List<CardData> cards)
    {
        var deck = ScriptableObject.CreateInstance<DeckData>();
        
        // Use reflection to set private fields
        var deckNameField = typeof(DeckData).GetField("_deckName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardsField = typeof(DeckData).GetField("_cardsInDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        deckNameField?.SetValue(deck, deckName);
        cardsField?.SetValue(deck, cards);
        
        deck.name = deckName;
        
        return deck;
    }

    private List<DeckData> CreateCategorizedDecks(List<CardData> allCards)
    {
        var categorizedDecks = new List<DeckData>();

        // Group cards by their primary effect type
        var cardGroups = new Dictionary<string, List<CardData>>();

        foreach (var card in allCards)
        {
            string category = GetCardCategory(card);
            if (!cardGroups.ContainsKey(category))
            {
                cardGroups[category] = new List<CardData>();
            }
            cardGroups[category].Add(card);
        }

        // Create a deck for each category
        foreach (var group in cardGroups)
        {
            if (group.Value.Count > 0)
            {
                var deck = CreateDeck($"{group.Key} Test Deck", group.Value);
                categorizedDecks.Add(deck);
            }
        }

        return categorizedDecks;
    }

    private string GetCardCategory(CardData card)
    {
        if (card.Effects.Count == 0)
            return "Misc";

        var primaryEffect = card.Effects[0].effectType;

        // Group similar effects together
        switch (primaryEffect)
        {
            case CardEffectType.Damage:
                return "Damage";
            case CardEffectType.Heal:
                return "Healing";
            case CardEffectType.DrawCard:
            case CardEffectType.RestoreEnergy:
                return "Resource";
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyStun:
                return "Debuffs";
            case CardEffectType.ApplyShield:
            case CardEffectType.ApplyThorns:
            case CardEffectType.ApplySalve:
                return "Buffs";
            case CardEffectType.ApplyBurn:
            case CardEffectType.ApplyCurse:
            case CardEffectType.ApplyStrength:
                return "Status Effects";
            case CardEffectType.ApplyElementalStatus:
                return "Elemental";
            case CardEffectType.EnterStance:
            case CardEffectType.ExitStance:
                return "Stance";
            default:
                return "Misc";
        }
    }

    private bool IsCoreTestCard(CardData card)
    {
        // Define core test cards that cover basic functionality
        var coreEffects = new[]
        {
            CardEffectType.Damage,
            CardEffectType.Heal,
            CardEffectType.ApplyShield,
            CardEffectType.ApplyBurn,
            CardEffectType.DrawCard,
            CardEffectType.RestoreEnergy
        };

        return card.Effects.Count > 0 && coreEffects.Contains(card.Effects[0].effectType);
    }

    private void ClearGeneratedDecks()
    {
        if (Directory.Exists(outputPath))
        {
            var testDecks = Directory.GetFiles(outputPath, "TESTDECK_*.asset");
            foreach (var deck in testDecks)
            {
                AssetDatabase.DeleteAsset(deck);
            }
            AssetDatabase.Refresh();
            Debug.Log($"Cleared {testDecks.Length} test decks");
        }
    }
} 