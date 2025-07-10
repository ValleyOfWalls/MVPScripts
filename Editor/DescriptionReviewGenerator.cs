using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Editor script to generate and review a complete theoretical card pool for a game
/// Shows both procedural cards and starter decks using the same pathways as the actual game
/// </summary>
public class CardPoolPreviewGenerator : EditorWindow
{
    private Vector2 scrollPosition;
    private List<string> generatedDescriptions = new List<string>();
    private List<string> generatedNames = new List<string>();
    private List<CardRarity> generatedRarities = new List<CardRarity>();
    private List<int> generatedEnergyCosts = new List<int>();
    private List<int> generatedInitiatives = new List<int>();
    private List<bool> generatedHasUpgrade = new List<bool>();
    private List<string> generatedUpgradeDescriptions = new List<string>();
    private List<string> generatedCardTypes = new List<string>(); // "Procedural", "Warrior Starter", etc.
    private List<string> generatedUpgradeConditions = new List<string>(); // New: upgrade condition descriptions
    private bool isGenerating = false;
    private bool showUpgradedVersions = true;
    private bool useActualGameDistribution = true;
    private bool showStarterDecks = true;
    private bool showProceduralCards = true;
    private bool useThemedStarterDecks = true; // New option
    private int targetDraftPoolSize = 40; // New option
    
    [MenuItem("Tools/Card System/Card Pool Preview Generator")]
    public static void ShowWindow()
    {
        GetWindow<CardPoolPreviewGenerator>("Card Pool Preview Generator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Card Pool Preview Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("This tool generates a complete theoretical card pool using the actual game systems.", EditorStyles.helpBox);
        GUILayout.Label("It shows exactly what players would see in-game with current config settings.", EditorStyles.helpBox);
        GUILayout.Space(10);
        
        // Options
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Card Pool Options:", EditorStyles.boldLabel);
        
        // Card type toggles
        EditorGUILayout.BeginHorizontal();
        showProceduralCards = EditorGUILayout.Toggle("Show Procedural Cards", showProceduralCards);
        showStarterDecks = EditorGUILayout.Toggle("Show Starter Decks", showStarterDecks);
        EditorGUILayout.EndHorizontal();
        
        // Starter deck options
        EditorGUI.BeginDisabledGroup(!showStarterDecks);
        EditorGUILayout.BeginHorizontal();
        useThemedStarterDecks = EditorGUILayout.Toggle("Use Themed Starter Decks", useThemedStarterDecks);
        if (!useThemedStarterDecks)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("(Random decks ignore class)", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
        
        // Draft pool size
        EditorGUI.BeginDisabledGroup(!showProceduralCards);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Draft Pool Size:", GUILayout.Width(100));
        targetDraftPoolSize = EditorGUILayout.IntSlider(targetDraftPoolSize, 10, 100);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
        
        // Display options
        EditorGUILayout.BeginHorizontal();
        showUpgradedVersions = EditorGUILayout.Toggle("Show Upgraded Versions", showUpgradedVersions);
        useActualGameDistribution = EditorGUILayout.Toggle("Use Game Rarity Distribution", useActualGameDistribution);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(5);
        
        // Info box showing what will be generated
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Generation Preview:", EditorStyles.boldLabel);
        
        int totalCards = 0;
        if (showStarterDecks)
        {
            int starterCards = 6 * 10; // 6 classes × 10 cards each
            totalCards += starterCards;
            string starterType = useThemedStarterDecks ? "themed" : "random";
            GUILayout.Label($"• {starterCards} starter cards ({starterType})", EditorStyles.miniLabel);
        }
        
        if (showProceduralCards)
        {
            totalCards += targetDraftPoolSize;
            string distribution = useActualGameDistribution ? "game config" : "fallback";
            GUILayout.Label($"• {targetDraftPoolSize} procedural cards ({distribution} distribution)", EditorStyles.miniLabel);
        }
        
        GUILayout.Label($"Total: ~{totalCards} cards", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(isGenerating);
        if (GUILayout.Button("Generate Complete Card Pool", GUILayout.Height(30)))
        {
            GenerateCompleteCardPool();
        }
        EditorGUI.EndDisabledGroup();
        
        if (isGenerating)
        {
            GUILayout.Label("Generating card pool...", EditorStyles.centeredGreyMiniLabel);
        }
        
        GUILayout.Space(10);
        
        if (generatedDescriptions.Count > 0)
        {
            GUILayout.Label($"Generated {generatedDescriptions.Count} Cards:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Copy All to Clipboard"))
            {
                CopyAllToClipboard();
            }
            
            GUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < generatedDescriptions.Count; i++)
            {
                DrawDescriptionCard(i);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
    
    private void DrawDescriptionCard(int index)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Header with card number, name, rarity, energy cost, and initiative
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"#{index + 1}", GUILayout.Width(30));
        
        // Color the rarity
        Color originalColor = GUI.color;
        GUI.color = GetRarityColor(generatedRarities[index]);
        GUILayout.Label($"[{generatedRarities[index]}]", EditorStyles.boldLabel, GUILayout.Width(80));
        GUI.color = originalColor;
        
        GUILayout.Label(generatedNames[index], EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        // Card type indicator
        GUI.color = GetCardTypeColor(generatedCardTypes[index]);
        GUILayout.Label($"({generatedCardTypes[index]})", EditorStyles.miniLabel, GUILayout.Width(100));
        GUI.color = originalColor;
        
        // Energy cost and initiative display
        GUILayout.Label($"⚡{generatedEnergyCosts[index]}", GUILayout.Width(40));
        if (generatedInitiatives[index] > 0)
        {
            GUILayout.Label($"🎯{generatedInitiatives[index]}", GUILayout.Width(40));
        }
        
        if (GUILayout.Button("Copy", GUILayout.Width(50)))
        {
            string cardInfo = $"{generatedNames[index]} ({generatedRarities[index]}) - ⚡{generatedEnergyCosts[index]} ({generatedCardTypes[index]})";
            if (generatedInitiatives[index] > 0) cardInfo += $" 🎯{generatedInitiatives[index]}";
            cardInfo += $"\n{generatedDescriptions[index]}";
            
            if (generatedHasUpgrade[index] && !string.IsNullOrEmpty(generatedUpgradeConditions[index]))
            {
                cardInfo += $"\n\nUPGRADE CONDITION: {generatedUpgradeConditions[index]}";
            }
            
            if (showUpgradedVersions && generatedHasUpgrade[index] && !string.IsNullOrEmpty(generatedUpgradeDescriptions[index]))
            {
                cardInfo += $"\n\nUPGRADED: {generatedUpgradeDescriptions[index]}";
            }
            
            GUIUtility.systemCopyBuffer = cardInfo;
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Description text
        EditorGUILayout.SelectableLabel(generatedDescriptions[index], EditorStyles.wordWrappedLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
        
        // Upgrade condition if applicable
        if (generatedHasUpgrade[index] && !string.IsNullOrEmpty(generatedUpgradeConditions[index]))
        {
            GUILayout.Space(3);
            GUI.color = Color.cyan;
            GUILayout.Label("UPGRADE CONDITION:", EditorStyles.boldLabel);
            GUI.color = originalColor;
            EditorGUILayout.SelectableLabel(generatedUpgradeConditions[index], EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
        }
        
        // Upgraded version if applicable
        if (showUpgradedVersions && generatedHasUpgrade[index] && !string.IsNullOrEmpty(generatedUpgradeDescriptions[index]))
        {
            GUILayout.Space(5);
            GUI.color = Color.yellow;
            GUILayout.Label("UPGRADED:", EditorStyles.boldLabel);
            GUI.color = originalColor;
            EditorGUILayout.SelectableLabel(generatedUpgradeDescriptions[index], EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }
    
    private Color GetRarityColor(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => Color.white,
            CardRarity.Uncommon => Color.green,
            CardRarity.Rare => Color.cyan,
            _ => Color.white
        };
    }
    
    private Color GetCardTypeColor(string cardType)
    {
        if (cardType.Contains("Starter"))
        {
            return Color.yellow;
        }
        else if (cardType.Contains("Procedural"))
        {
            return Color.cyan;
        }
        return Color.white;
    }
    
    private void GenerateCompleteCardPool()
    {
        isGenerating = true;
        generatedDescriptions.Clear();
        generatedNames.Clear();
        generatedRarities.Clear();
        generatedEnergyCosts.Clear();
        generatedInitiatives.Clear();
        generatedHasUpgrade.Clear();
        generatedUpgradeDescriptions.Clear();
        generatedCardTypes.Clear();
        generatedUpgradeConditions.Clear();
        
        try
        {
            // Find the config assets
            RandomCardConfig config = AssetDatabase.LoadAssetAtPath<RandomCardConfig>("Assets/MVPScripts/RandomCardConfig.asset");
            if (config == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find RandomCardConfig.asset", "OK");
                isGenerating = false;
                return;
            }
            
            // Create the procedural generator
            ProceduralCardGenerator generator = new ProceduralCardGenerator(config);
            
            // Generate starter decks first (if enabled)
            if (showStarterDecks)
            {
                GenerateStarterDecks(generator);
            }
            
            // Generate procedural cards (if enabled)
            if (showProceduralCards)
            {
                GenerateProceduralCards(generator, config);
            }
            
            Debug.Log($"Generated complete card pool: {generatedDescriptions.Count} cards total");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating card pool: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to generate card pool: {e.Message}", "OK");
        }
        finally
        {
            isGenerating = false;
        }
    }
    
    private void GenerateStarterDecks(ProceduralCardGenerator generator)
    {
        // Generate starter decks for each character class using the same pathways as the game
        string[] characterClasses = { "Warrior", "Enhanced", "Assassin" };
        string[] petClasses = { "Beast", "Elemental", "Spirit" };
        
        if (useThemedStarterDecks)
        {
            // Generate themed starter decks (class-specific)
            foreach (string characterClass in characterClasses)
            {
                Debug.Log($"Generating themed starter deck for {characterClass}...");
                
                // Use the same method as RandomizedCardDatabaseManager
                var starterCards = generator.GenerateThematicStarterDeck(characterClass, 10);
                
                foreach (var card in starterCards)
                {
                    AddCardToPreview(card, $"{characterClass} Starter");
                }
            }
            
            // Generate pet starter decks too
            foreach (string petClass in petClasses)
            {
                Debug.Log($"Generating themed pet starter deck for {petClass}...");
                
                var starterCards = generator.GenerateThematicStarterDeck(petClass, 10);
                
                foreach (var card in starterCards)
                {
                    AddCardToPreview(card, $"{petClass} Pet Starter");
                }
            }
                    }
                    else
                    {
            // Generate random starter decks (not class-specific)
            foreach (string characterClass in characterClasses)
            {
                Debug.Log($"Generating random starter deck for {characterClass}...");
                
                // Generate random starter deck using the same logic as ProceduralCardGenerator
                var starterCards = GenerateRandomStarterDeck(generator, characterClass, 10);
                
                foreach (var card in starterCards)
                {
                    AddCardToPreview(card, $"{characterClass} Random Starter");
                }
            }
            
            // Generate random pet starter decks too
            foreach (string petClass in petClasses)
            {
                Debug.Log($"Generating random pet starter deck for {petClass}...");
                
                var starterCards = GenerateRandomStarterDeck(generator, petClass, 10);
                
                foreach (var card in starterCards)
                {
                    AddCardToPreview(card, $"{petClass} Random Pet Starter");
                }
            }
        }
    }
    
    /// <summary>
    /// Generate a random starter deck using the same logic as ProceduralCardGenerator.GenerateRandomStarterDeck
    /// This is needed because that method is private and we can't access it from the editor
    /// </summary>
    private List<CardData> GenerateRandomStarterDeck(ProceduralCardGenerator generator, string characterClass, int maxUniqueCards = 10)
    {
        // Find the config to get rarity distribution
        RandomCardConfig config = AssetDatabase.LoadAssetAtPath<RandomCardConfig>("Assets/MVPScripts/RandomCardConfig.asset");
        if (config?.rarityDistributionConfig == null)
        {
            Debug.LogError("Missing rarity distribution configuration!");
            return new List<CardData>();
        }
        
        var cards = new List<CardData>();
        var distConfig = config.rarityDistributionConfig;
        
        // Calculate total cards needed for starter deck
        int totalCardsNeeded = distConfig.starterDeckCommons + distConfig.starterDeckUncommons + distConfig.starterDeckRares;
        
        // Limit unique cards to the maximum specified
        int uniqueCardsToGenerate = Mathf.Min(maxUniqueCards, totalCardsNeeded);
        
        Debug.Log($"Generating random starter deck for {characterClass}: {uniqueCardsToGenerate} unique cards out of {totalCardsNeeded} total");
        
        // Generate unique cards with balanced rarity distribution
        for (int i = 0; i < uniqueCardsToGenerate; i++)
        {
            CardRarity rarity = DetermineRarityForUniqueCard(i, uniqueCardsToGenerate, distConfig);
            cards.Add(generator.GenerateRandomCard(rarity));
        }
        
        // Fill remaining slots with duplicates of existing cards
        int duplicatesNeeded = totalCardsNeeded - uniqueCardsToGenerate;
        if (duplicatesNeeded > 0 && cards.Count > 0)
        {
            Debug.Log($"Adding {duplicatesNeeded} duplicates to reach target deck size of {totalCardsNeeded}");
            
            for (int i = 0; i < duplicatesNeeded; i++)
            {
                var cardToDuplicate = cards[UnityEngine.Random.Range(0, cards.Count)];
                cards.Add(DuplicateCard(cardToDuplicate));
            }
        }
        
        return cards;
    }
    
    /// <summary>
    /// Determine the rarity for a unique card based on its position and target distribution
    /// Replicates the logic from ProceduralCardGenerator.DetermineRarityForUniqueCard
    /// </summary>
    private CardRarity DetermineRarityForUniqueCard(int cardIndex, int totalUniqueCards, RarityDistributionConfig distConfig)
    {
        // Calculate how many of each rarity we want in our unique cards
        float totalOriginalCards = distConfig.starterDeckCommons + distConfig.starterDeckUncommons + distConfig.starterDeckRares;
        
        int targetCommons = Mathf.RoundToInt((distConfig.starterDeckCommons / totalOriginalCards) * totalUniqueCards);
        int targetUncommons = Mathf.RoundToInt((distConfig.starterDeckUncommons / totalOriginalCards) * totalUniqueCards);
        int targetRares = totalUniqueCards - targetCommons - targetUncommons; // Remainder goes to rares
        
        // Ensure we have at least some distribution
        if (targetCommons == 0 && totalUniqueCards > 2) targetCommons = totalUniqueCards - 2;
        if (targetRares == 0 && totalUniqueCards > 1) targetRares = 1;
        if (targetUncommons == 0 && totalUniqueCards > targetCommons + targetRares) targetUncommons = totalUniqueCards - targetCommons - targetRares;
        
        // Distribute cards based on index
        if (cardIndex < targetCommons)
            return CardRarity.Common;
        else if (cardIndex < targetCommons + targetUncommons)
            return CardRarity.Uncommon;
        else
            return CardRarity.Rare;
    }
    
    /// <summary>
    /// Create a duplicate of a card for deck repetition
    /// Replicates the logic from ProceduralCardGenerator.DuplicateCard
    /// </summary>
    private CardData DuplicateCard(CardData original)
    {
        var duplicate = ScriptableObject.CreateInstance<CardData>();
        
        // Copy all properties
        duplicate.SetCardName(original.CardName);
        duplicate.SetDescription(original.Description);
        duplicate.SetRarity(original.Rarity);
        duplicate.SetEnergyCost(original.EnergyCost);
        duplicate.SetCardType(original.CardType);
        duplicate.SetInitiative(original.Initiative);
        duplicate.SetEffects(new List<CardEffect>(original.Effects));
        
        // Copy upgrade properties if they exist
        if (original.CanUpgrade)
        {
            duplicate.SetUpgradeProperties(original.CanUpgrade, original.UpgradeConditionType, 
                original.UpgradeRequiredValue, original.UpgradeComparisonType);
        }
        
        // Generate new unique ID (simplified version)
        duplicate.SetCardId(UnityEngine.Random.Range(100000, 999999));
        
        return duplicate;
    }
    
    /// <summary>
    /// Generate a human-readable description of the upgrade condition
    /// </summary>
    private string GenerateUpgradeConditionDescription(CardData card)
    {
        if (!card.CanUpgrade)
            return "";
        
        var conditionType = card.UpgradeConditionType;
        int requiredValue = card.UpgradeRequiredValue;
        var comparisonType = card.UpgradeComparisonType;
        
        string comparisonText = comparisonType switch
        {
            UpgradeComparisonType.GreaterThanOrEqual => "at least",
            UpgradeComparisonType.Equal => "exactly",
            UpgradeComparisonType.LessThanOrEqual => "at most",
            UpgradeComparisonType.GreaterThan => "more than",
            UpgradeComparisonType.LessThan => "less than",
            _ => "at least"
        };
        
        return conditionType switch
        {
            // Card-specific tracking
            UpgradeConditionType.TimesPlayedThisFight => $"Play {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.TimesPlayedAcrossFights => $"Play {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} across all fights",
            UpgradeConditionType.CopiesInDeck => $"Have {comparisonText} {requiredValue} cop{(requiredValue != 1 ? "ies" : "y")} in deck",
            UpgradeConditionType.CopiesInHand => $"Have {comparisonText} {requiredValue} cop{(requiredValue != 1 ? "ies" : "y")} in hand",
            UpgradeConditionType.CopiesInDiscard => $"Have {comparisonText} {requiredValue} cop{(requiredValue != 1 ? "ies" : "y")} in discard pile",
            UpgradeConditionType.AllCopiesPlayedFromHand => "Play all copies from hand",
            
            // Combat performance
            UpgradeConditionType.DamageDealtThisFight => $"Deal {comparisonText} {requiredValue} damage this fight",
            UpgradeConditionType.DamageDealtInSingleTurn => $"Deal {comparisonText} {requiredValue} damage in a single turn",
            UpgradeConditionType.DamageTakenThisFight => $"Take {comparisonText} {requiredValue} damage this fight",
            UpgradeConditionType.HealingGivenThisFight => $"Heal {comparisonText} {requiredValue} health this fight",
            UpgradeConditionType.HealingReceivedThisFight => $"Receive {comparisonText} {requiredValue} healing this fight",
            UpgradeConditionType.PerfectionStreakAchieved => $"Achieve {comparisonText} {requiredValue} perfect turn{(requiredValue != 1 ? "s" : "")} in a row",
            
            // Combo and tactical
            UpgradeConditionType.ComboCountReached => $"Reach {comparisonText} {requiredValue} combo count",
            UpgradeConditionType.PlayedWithCombo => $"Play with {comparisonText} {requiredValue} combo count",
            UpgradeConditionType.PlayedInStance => $"Play while in stance {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")}",
            UpgradeConditionType.PlayedAsFinisher => $"Play as finishing move (combo card with sufficient combo) {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")}", 
            UpgradeConditionType.ZeroCostCardsThisTurn => $"Play {comparisonText} {requiredValue} zero-cost card{(requiredValue != 1 ? "s" : "")} this turn",
            UpgradeConditionType.ZeroCostCardsThisFight => $"Play {comparisonText} {requiredValue} zero-cost card{(requiredValue != 1 ? "s" : "")} this fight",
            
            // Health-based - use actual thresholds from CardUpgradeManager
            UpgradeConditionType.PlayedAtLowHealth => "Play while at 25% health or below",
            UpgradeConditionType.PlayedAtHighHealth => "Play while at 75% health or above", 
            UpgradeConditionType.PlayedAtHalfHealth => "Play while at half health or below",
            UpgradeConditionType.SurvivedFightWithCard => "Survive a fight with this card in deck",
            
            // Turn-based
            UpgradeConditionType.PlayedOnConsecutiveTurns => $"Play on {comparisonText} {requiredValue} consecutive turn{(requiredValue != 1 ? "s" : "")}",
            UpgradeConditionType.PlayedMultipleTimesInTurn => $"Play {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} in one turn",
            
            // Victory conditions
            UpgradeConditionType.WonFightUsingCard => $"Win {comparisonText} {requiredValue} fight{(requiredValue != 1 ? "s" : "")} using this card",
            UpgradeConditionType.DefeatedOpponentWithCard => $"Defeat opponent with this card {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")}",
            UpgradeConditionType.LostFightWithCard => $"Lose {comparisonText} {requiredValue} fight{(requiredValue != 1 ? "s" : "")} with this card in deck",
            
            // Advanced tracking (per-fight)
            UpgradeConditionType.ComboUseBackToBack => $"Play back-to-back with same card type {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")}",
            UpgradeConditionType.DrawnOften => $"Draw {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.HeldAtTurnEnd => $"Hold at turn end {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.DiscardedManually => $"Discard manually {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.FinalCardInHand => $"Be the last card in hand {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.OnlyCardPlayedThisTurn => $"Be the only card played {comparisonText} {requiredValue} turn{(requiredValue != 1 ? "s" : "")} this fight",
            
            // Deck composition
            UpgradeConditionType.FamiliarNameInDeck => $"Have {comparisonText} {requiredValue} card{(requiredValue != 1 ? "s" : "")} with similar names in deck",
            UpgradeConditionType.OnlyCardTypeInDeck => "Be the only card of this type in deck",
            UpgradeConditionType.AllCardsCostLowEnough => "All cards in deck cost 1 energy or less",
            UpgradeConditionType.DeckSizeBelow => $"Have deck size {comparisonText} {requiredValue} card{(requiredValue != 1 ? "s" : "")}",
            
            // Status and battle conditions
            UpgradeConditionType.SurvivedStatusEffect => $"Survive {comparisonText} {requiredValue} status effect{(requiredValue != 1 ? "s" : "")} this fight",
            UpgradeConditionType.BattleLengthOver => $"Battle lasts {comparisonText} {requiredValue} turn{(requiredValue != 1 ? "s" : "")}",
            UpgradeConditionType.PerfectTurnPlayed => $"Play {comparisonText} {requiredValue} perfect turn{(requiredValue != 1 ? "s" : "")} this fight",
            
            // Lifetime tracking
            UpgradeConditionType.DrawnOftenLifetime => $"Draw {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.HeldAtTurnEndLifetime => $"Hold at turn end {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.DiscardedManuallyLifetime => $"Discard manually {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.FinalCardInHandLifetime => $"Be the last card in hand {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.ComboUseBackToBackLifetime => $"Play back-to-back {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.OnlyCardPlayedInTurnLifetime => $"Be the only card played {comparisonText} {requiredValue} time{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.TotalFightsWon => $"Win {comparisonText} {requiredValue} fight{(requiredValue != 1 ? "s" : "")} total",
            UpgradeConditionType.TotalFightsLost => $"Lose {comparisonText} {requiredValue} fight{(requiredValue != 1 ? "s" : "")} total",
            UpgradeConditionType.TotalBattleTurns => $"Participate in {comparisonText} {requiredValue} battle turn{(requiredValue != 1 ? "s" : "")} total",
            UpgradeConditionType.TotalPerfectTurns => $"Achieve {comparisonText} {requiredValue} perfect turn{(requiredValue != 1 ? "s" : "")} lifetime",
            UpgradeConditionType.TotalStatusEffectsSurvived => $"Survive {comparisonText} {requiredValue} different status effect{(requiredValue != 1 ? "s" : "")} lifetime",
            
            _ => $"Meet upgrade condition: {conditionType}"
        };
    }
    
    private void GenerateProceduralCards(ProceduralCardGenerator generator, RandomCardConfig config)
    {
        // Generate procedural cards with realistic distribution
        List<CardRarity> raritiesToGenerate = new List<CardRarity>();
        
        if (useActualGameDistribution && config.rarityDistributionConfig != null)
        {
            // Use actual draft pack distribution for procedural cards
            var distConfig = config.rarityDistributionConfig;
            int totalCards = targetDraftPoolSize;
            int commons = Mathf.RoundToInt(totalCards * distConfig.draftCommonPercentage / 100f);
            int uncommons = Mathf.RoundToInt(totalCards * distConfig.draftUncommonPercentage / 100f);
            int rares = totalCards - commons - uncommons;
            
            for (int i = 0; i < commons; i++) raritiesToGenerate.Add(CardRarity.Common);
            for (int i = 0; i < uncommons; i++) raritiesToGenerate.Add(CardRarity.Uncommon);
            for (int i = 0; i < rares; i++) raritiesToGenerate.Add(CardRarity.Rare);
            
            Debug.Log($"Using game distribution for {totalCards} cards: {commons} commons, {uncommons} uncommons, {rares} rares");
        }
        else
        {
            // Fallback distribution: 60% Common, 30% Uncommon, 10% Rare
            int totalCards = targetDraftPoolSize;
            int commons = Mathf.RoundToInt(totalCards * 0.6f);
            int uncommons = Mathf.RoundToInt(totalCards * 0.3f);
            int rares = totalCards - commons - uncommons;
            
            for (int i = 0; i < commons; i++) raritiesToGenerate.Add(CardRarity.Common);
            for (int i = 0; i < uncommons; i++) raritiesToGenerate.Add(CardRarity.Uncommon);
            for (int i = 0; i < rares; i++) raritiesToGenerate.Add(CardRarity.Rare);
            
            Debug.Log($"Using fallback distribution for {totalCards} cards: {commons} commons, {uncommons} uncommons, {rares} rares");
        }
        
        // Shuffle the list for variety
        for (int i = 0; i < raritiesToGenerate.Count; i++)
        {
            CardRarity temp = raritiesToGenerate[i];
            int randomIndex = Random.Range(i, raritiesToGenerate.Count);
            raritiesToGenerate[i] = raritiesToGenerate[randomIndex];
            raritiesToGenerate[randomIndex] = temp;
        }
        
        // Generate the cards using the same pathway as the game
        foreach (var rarity in raritiesToGenerate)
        {
            var card = generator.GenerateRandomCard(rarity);
            AddCardToPreview(card, "Procedural");
        }
    }
    
    private void AddCardToPreview(CardData card, string cardType)
    {
        generatedNames.Add(card.CardName);
        generatedDescriptions.Add(card.Description);
        generatedRarities.Add(card.Rarity);
        generatedEnergyCosts.Add(card.EnergyCost);
        generatedInitiatives.Add(card.Initiative);
        generatedCardTypes.Add(cardType);
        
        // Check if card has upgrade and generate upgraded version
        bool hasUpgrade = card.CanUpgrade;
        generatedHasUpgrade.Add(hasUpgrade);
        
        // Generate upgrade condition description
        string upgradeCondition = "";
        if (hasUpgrade)
        {
            upgradeCondition = GenerateUpgradeConditionDescription(card);
            var upgradedCard = CreateUpgradedVersion(card);
            generatedUpgradeDescriptions.Add(upgradedCard.Description);
        }
        else
        {
            generatedUpgradeDescriptions.Add("");
        }
        
        generatedUpgradeConditions.Add(upgradeCondition);
    }
    
    private CardData CreateUpgradedVersion(CardData originalCard)
    {
        // Create a copy of the original card and apply upgrades
        var upgradedCard = ScriptableObject.CreateInstance<CardData>();
        
        // Copy basic properties
        upgradedCard.SetCardName($"{originalCard.CardName}+");
        upgradedCard.SetRarity(originalCard.Rarity);
        upgradedCard.SetEnergyCost(originalCard.EnergyCost);
        upgradedCard.SetCardType(originalCard.CardType);
        upgradedCard.SetInitiative(originalCard.Initiative);
        
        // Copy and upgrade effects
        var upgradedEffects = new List<CardEffect>();
        foreach (var effect in originalCard.Effects)
        {
            var upgradedEffect = new CardEffect
            {
                effectType = effect.effectType,
                amount = CalculateUpgradedAmount(effect.effectType, effect.amount),
                targetType = effect.targetType,
                duration = effect.duration,
                conditionType = effect.conditionType,
                conditionValue = effect.conditionValue,
                hasAlternativeEffect = effect.hasAlternativeEffect,
                alternativeLogic = effect.alternativeLogic,
                alternativeEffectType = effect.alternativeEffectType,
                alternativeEffectAmount = effect.alternativeEffectAmount,
                scalingType = effect.scalingType,
                scalingMultiplier = effect.scalingMultiplier,
                maxScaling = effect.maxScaling,
                shouldExitStance = effect.shouldExitStance,
                animationBehavior = effect.animationBehavior
            };
            upgradedEffects.Add(upgradedEffect);
        }
        
        upgradedCard.SetEffects(upgradedEffects);
        
        // Generate upgraded description using the same system
        var budget = new CardBudgetBreakdown 
        { 
            rarity = originalCard.Rarity, 
            energyCost = originalCard.EnergyCost,
            effectBudget = 50f // High budget for upgraded effects
        };
        
        var proposedEffects = upgradedEffects.ConvertAll(e => new ProposedCardEffect 
        { 
            effectType = e.effectType, 
            amount = e.amount, 
            targetType = e.targetType,
            conditionalType = e.conditionType,
            duration = e.duration
        });
        
        // Use the same description generation logic
        string upgradedDescription = GenerateCardDescription(upgradedEffects);
        upgradedCard.SetDescription(upgradedDescription);
        
        return upgradedCard;
    }
    
    private int CalculateUpgradedAmount(CardEffectType effectType, int originalAmount)
    {
        // Upgrade logic: increase effects by 30-50%
        float upgradeMultiplier = Random.Range(1.3f, 1.5f);
        int upgradedAmount = Mathf.RoundToInt(originalAmount * upgradeMultiplier);
        
        // Ensure minimum improvement
        return Mathf.Max(originalAmount + 1, upgradedAmount);
    }
    
    private string GenerateCardDescription(List<CardEffect> effects)
    {
        // Simplified version of the description generation from ProceduralCardGenerator
        var descriptions = new List<string>();
        
        foreach (var effect in effects)
        {
            string effectDesc = effect.effectType switch
            {
                CardEffectType.Damage => $"Deal {effect.amount} damage",
                CardEffectType.Heal => $"Heal {effect.amount} health",
                CardEffectType.ApplyShield => $"Gain {effect.amount} shield",
                CardEffectType.ApplyThorns => $"Gain {effect.amount} thorns",
                CardEffectType.ApplyStrength => $"Gain {effect.amount} strength",
                CardEffectType.ApplySalve => $"Apply {effect.amount} salve",
                CardEffectType.ApplyWeak => $"Apply {effect.amount} weak to enemy",
                CardEffectType.ApplyBreak => $"Apply {effect.amount} break to enemy",
                CardEffectType.ApplyBurn => $"Apply {effect.amount} burn to enemy",
                CardEffectType.ApplyCurse => $"Apply {effect.amount} curse to enemy",
                CardEffectType.ApplyStun => $"Stun enemy for {effect.amount} cards",
                CardEffectType.RaiseCriticalChance => $"Increase critical chance by {effect.amount}%",
                CardEffectType.EnterStance => "Enter combat stance",
                CardEffectType.ExitStance => "Exit current stance",
                _ => effect.effectType.ToString().Replace("Apply", "Apply ")
            };
            
            // Add conditional information
            if (effect.conditionType != ConditionalType.None)
            {
                string condition = effect.conditionType switch
                {
                    ConditionalType.IfInStance => $"If in {GetStanceName(effect.conditionValue)} stance",
                    ConditionalType.IfTargetHealthBelow => $"If target health below {effect.conditionValue}",
                    ConditionalType.IfSourceHealthBelow => $"If your health below {effect.conditionValue}",
                    ConditionalType.IfComboCount => $"If combo count {effect.conditionValue}+",
                    _ => "If condition met"
                };
                effectDesc = $"{condition}: {effectDesc}";
            }
            
            descriptions.Add(effectDesc);
        }
        
        return string.Join(". ", descriptions) + ".";
    }
    
    private string GetStanceName(int stanceValue)
    {
        StanceType stance = (StanceType)stanceValue;
        return stance switch
        {
            StanceType.Aggressive => "Aggressive",
            StanceType.Defensive => "Defensive",
            _ => "Combat"
        };
    }
    
    private void CopyAllToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Generated Card Pool (Theoretical) ===");
        sb.AppendLine();
        
        for (int i = 0; i < generatedDescriptions.Count; i++)
        {
            sb.AppendLine($"#{i + 1} {generatedNames[i]} ({generatedRarities[i]}) - ⚡{generatedEnergyCosts[i]} ({generatedCardTypes[i]})");
            if (generatedInitiatives[i] > 0) sb.AppendLine($"Initiative: {generatedInitiatives[i]}");
            sb.AppendLine(generatedDescriptions[i]);
            
            if (generatedHasUpgrade[i] && !string.IsNullOrEmpty(generatedUpgradeConditions[i]))
            {
                sb.AppendLine($"UPGRADE CONDITION: {generatedUpgradeConditions[i]}");
            }
            
            if (showUpgradedVersions && generatedHasUpgrade[i] && !string.IsNullOrEmpty(generatedUpgradeDescriptions[i]))
            {
                sb.AppendLine($"UPGRADED: {generatedUpgradeDescriptions[i]}");
            }
            
            sb.AppendLine();
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Generated card pool copied to clipboard");
    }
} 