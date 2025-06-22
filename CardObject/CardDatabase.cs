using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    [Header("All Cards")]
    [SerializeField]
    private List<CardData> allCardDataList = new List<CardData>(); // All cards for backwards compatibility

    [Header("Card Categories")]
    [SerializeField]
    private List<CardData> starterCardsList = new List<CardData>(); // Basic starter cards (BasicAttack, BasicDefend, etc.)
    
    [SerializeField]
    private List<CardData> draftableCardsList = new List<CardData>(); // Cards available in draft packs
    
    [SerializeField]
    private List<CardData> upgradedCardsList = new List<CardData>(); // Upgraded versions of cards (not draftable)

    // OR, load from Resources:
    // [SerializeField] private string cardDataPathInResources = "CardData"; 

    private Dictionary<int, CardData> cardDataById = new Dictionary<int, CardData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persistent
            InitializeDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDatabase()
    {
        cardDataById.Clear();

        // Combine all card lists for the main dictionary
        List<CardData> allCards = new List<CardData>();
        allCards.AddRange(allCardDataList);
        allCards.AddRange(starterCardsList);
        allCards.AddRange(draftableCardsList);
        allCards.AddRange(upgradedCardsList);
        
        // Remove duplicates (in case a card appears in multiple lists)
        allCards = allCards.Distinct().ToList();

        // Sort cards by their existing CardId to ensure consistent ordering
        allCards.Sort((a, b) => a.CardId.CompareTo(b.CardId));

        // Ensure cards have sequential IDs starting from 1
        int nextId = 1;
        
        foreach (CardData data in allCards)
        {
            if (data != null)
            {
                // Check if the card already has a proper ID (greater than 0)
                int cardId = data.CardId;
                
                // If card doesn't have a proper ID, assign one using reflection
                if (cardId <= 0)
                {
                    // Use reflection to set the ID field since CardId is a property
                    var field = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(data, nextId);
                        cardId = nextId;
                        Debug.Log($"CardDatabase: Assigned ID {cardId} to card '{data.CardName}'");
                    }
                    else
                    {
                        Debug.LogError($"CardDatabase: Failed to set ID for card '{data.CardName}'. Make sure _cardId field exists.");
                    }
                }
                
                // Try to add the card to the dictionary
                if (!cardDataById.ContainsKey(cardId))
                {
                    cardDataById.Add(cardId, data);
                    nextId = Mathf.Max(nextId, cardId + 1); // Update nextId to be greater than any existing ID
                }
                else
                {
                    Debug.LogWarning($"CardDatabase: Duplicate CardId {cardId} found for '{data.CardName}' and '{cardDataById[cardId].CardName}'. Ignoring duplicate.");
                }
            }
        }
        
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} total cards:");
        Debug.Log($"  - All Cards List: {allCardDataList.Count}");
        Debug.Log($"  - Starter Cards: {starterCardsList.Count}");
        Debug.Log($"  - Draftable Cards: {draftableCardsList.Count}");
        Debug.Log($"  - Upgraded Cards: {upgradedCardsList.Count}");

        if (cardDataById.Count == 0)
        {
            Debug.LogWarning("CardDatabase is empty. Make sure to assign CardData ScriptableObjects to the appropriate lists in the Inspector.");
        }
    }

    public CardData GetCardById(int cardId)
    {
        if (cardDataById.TryGetValue(cardId, out CardData data))
        {
            return data;
        }
        Debug.LogWarning($"CardDatabase: Card with ID {cardId} not found.");
        return null;
    }

    public List<CardData> GetAllCards()
    {
        return new List<CardData>(cardDataById.Values);
    }

    /// <summary>
    /// Gets all starter cards (basic cards that come with starter decks)
    /// </summary>
    public List<CardData> GetStarterCards()
    {
        return new List<CardData>(starterCardsList);
    }

    /// <summary>
    /// Gets all draftable cards (cards that can appear in draft packs)
    /// </summary>
    public List<CardData> GetDraftableCards()
    {
        return new List<CardData>(draftableCardsList);
    }

    /// <summary>
    /// Gets all upgraded cards (upgraded versions, not draftable)
    /// </summary>
    public List<CardData> GetUpgradedCards()
    {
        return new List<CardData>(upgradedCardsList);
    }

    public List<CardData> GetRandomCards(int count)
    {
        return GetRandomCardsFromList(GetAllCards(), count, false);
    }

    /// <summary>
    /// Gets random cards allowing duplicates - useful for draft packs
    /// Now uses only draftable cards instead of all cards
    /// </summary>
    /// <param name="count">Number of cards to get</param>
    /// <returns>List of random draftable cards, potentially with duplicates</returns>
    public List<CardData> GetRandomCardsWithDuplicates(int count)
    {
        return GetRandomDraftableCardsWithDuplicates(count);
    }

    /// <summary>
    /// Gets random draftable cards allowing duplicates - for draft packs
    /// </summary>
    /// <param name="count">Number of cards to get</param>
    /// <returns>List of random draftable cards, potentially with duplicates</returns>
    public List<CardData> GetRandomDraftableCardsWithDuplicates(int count)
    {
        if (count <= 0) return new List<CardData>();
        
        if (draftableCardsList.Count == 0)
        {
            Debug.LogWarning("CardDatabase: No draftable cards available to select from. Make sure draftable cards are assigned in the Inspector.");
            return new List<CardData>();
        }
        
        List<CardData> result = new List<CardData>();
        
        // Select random cards allowing duplicates
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, draftableCardsList.Count);
            result.Add(draftableCardsList[randomIndex]);
        }
        
        return result;
    }

    /// <summary>
    /// Gets random starter cards allowing duplicates
    /// </summary>
    /// <param name="count">Number of cards to get</param>
    /// <returns>List of random starter cards, potentially with duplicates</returns>
    public List<CardData> GetRandomStarterCardsWithDuplicates(int count)
    {
        if (count <= 0) return new List<CardData>();
        
        if (starterCardsList.Count == 0)
        {
            Debug.LogWarning("CardDatabase: No starter cards available to select from.");
            return new List<CardData>();
        }
        
        List<CardData> result = new List<CardData>();
        
        // Select random cards allowing duplicates
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, starterCardsList.Count);
            result.Add(starterCardsList[randomIndex]);
        }
        
        return result;
    }

    /// <summary>
    /// Helper method to get random cards from a specific list
    /// </summary>
    private List<CardData> GetRandomCardsFromList(List<CardData> sourceList, int count, bool allowDuplicates)
    {
        if (count <= 0) return new List<CardData>();
        
        if (sourceList.Count == 0)
        {
            Debug.LogWarning("CardDatabase: Source list is empty.");
            return new List<CardData>();
        }
        
        if (allowDuplicates)
        {
            List<CardData> result = new List<CardData>();
            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, sourceList.Count);
                result.Add(sourceList[randomIndex]);
            }
            return result;
        }
        else
        {
            // Create a shuffled list of available cards
            List<CardData> availableCards = new List<CardData>(sourceList);
            int n = availableCards.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                CardData temp = availableCards[k];
                availableCards[k] = availableCards[n];
                availableCards[n] = temp;
            }
            
            // Return the requested number of cards (or all if count > available)
            return availableCards.Take(Mathf.Min(count, availableCards.Count)).ToList();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor function to automatically find and populate all CardData assets in the project
    /// Now categorizes cards based on their CardCategory field
    /// </summary>
    [ContextMenu("Auto-Populate Card Database")]
    public void AutoPopulateCardDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:CardData");
        List<CardData> foundCards = new List<CardData>();
        List<CardData> foundStarterCards = new List<CardData>();
        List<CardData> foundDraftableCards = new List<CardData>();
        List<CardData> foundUpgradedCards = new List<CardData>();
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            CardData cardData = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            
            if (cardData != null)
            {
                foundCards.Add(cardData);
                
                // Categorize based on CardCategory field
                switch (cardData.CardCategory)
                {
                    case CardCategory.Starter:
                        foundStarterCards.Add(cardData);
                        break;
                    case CardCategory.Draftable:
                        foundDraftableCards.Add(cardData);
                        break;
                    case CardCategory.Upgraded:
                        foundUpgradedCards.Add(cardData);
                        break;
                    default:
                        // Default to draftable if category is not set properly
                        foundDraftableCards.Add(cardData);
                        Debug.LogWarning($"CardDatabase: Card '{cardData.CardName}' has unknown category, defaulting to Draftable");
                        break;
                }
            }
        }
        
        // Sort cards by name for consistency
        foundCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
        foundStarterCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
        foundDraftableCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
        foundUpgradedCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
        
        // Update the lists
        allCardDataList = foundCards;
        starterCardsList = foundStarterCards;
        draftableCardsList = foundDraftableCards;
        upgradedCardsList = foundUpgradedCards;
        
        // Mark as dirty so the changes are saved
        EditorUtility.SetDirty(this);
        
        Debug.Log($"CardDatabase: Auto-populated with {foundCards.Count} total CardData assets:");
        Debug.Log($"  - Starter Cards: {foundStarterCards.Count}");
        Debug.Log($"  - Draftable Cards: {foundDraftableCards.Count}");
        Debug.Log($"  - Upgraded Cards: {foundUpgradedCards.Count}");
        
        // Reinitialize the database
        InitializeDatabase();
    }
    
    /// <summary>
    /// Editor function to validate all cards in the database
    /// </summary>
    [ContextMenu("Validate Card Database")]
    public void ValidateCardDatabase()
    {
        int validCards = 0;
        int invalidCards = 0;
        List<string> issues = new List<string>();
        
        // Validate all cards across all lists
        List<CardData> allCardsToValidate = new List<CardData>();
        allCardsToValidate.AddRange(allCardDataList);
        allCardsToValidate.AddRange(starterCardsList);
        allCardsToValidate.AddRange(draftableCardsList);
        allCardsToValidate.AddRange(upgradedCardsList);
        
        // Remove duplicates
        allCardsToValidate = allCardsToValidate.Distinct().ToList();
        
        foreach (CardData card in allCardsToValidate)
        {
            if (card == null)
            {
                invalidCards++;
                issues.Add("Found null CardData reference");
                continue;
            }
            
            // Basic validation
            bool isValid = true;
            
            if (string.IsNullOrEmpty(card.CardName))
            {
                isValid = false;
                issues.Add($"Card ID {card.CardId}: Missing card name");
            }
            
            if (string.IsNullOrEmpty(card.Description))
            {
                isValid = false;
                issues.Add($"Card '{card.CardName}': Missing description");
            }
            
            if (card.EnergyCost < 0)
            {
                isValid = false;
                issues.Add($"Card '{card.CardName}': Negative energy cost ({card.EnergyCost})");
            }
            
            if (!card.HasEffects)
            {
                isValid = false;
                issues.Add($"Card '{card.CardName}': No effects defined");
            }
            
            if (isValid)
                validCards++;
            else
                invalidCards++;
        }
        
        Debug.Log($"CardDatabase Validation Complete:\n" +
                  $"✅ Valid Cards: {validCards}\n" +
                  $"❌ Invalid Cards: {invalidCards}\n" +
                  $"Total Cards: {allCardsToValidate.Count}");
        
        if (issues.Count > 0)
        {
            Debug.LogWarning("Card Database Issues Found:\n" + string.Join("\n", issues));
        }
    }
    
    /// <summary>
    /// Editor function to generate a summary report of all cards
    /// </summary>
    [ContextMenu("Generate Card Summary")]
    public void GenerateCardSummary()
    {
        List<CardData> allCardsToSummarize = new List<CardData>();
        allCardsToSummarize.AddRange(allCardDataList);
        allCardsToSummarize.AddRange(starterCardsList);
        allCardsToSummarize.AddRange(draftableCardsList);
        allCardsToSummarize.AddRange(upgradedCardsList);
        
        // Remove duplicates
        allCardsToSummarize = allCardsToSummarize.Distinct().ToList();
        
        if (allCardsToSummarize.Count == 0)
        {
            Debug.Log("CardDatabase: No cards found. Use 'Auto-Populate Card Database' first.");
            return;
        }
        
        // Count by type
        var typeGroups = allCardsToSummarize
            .Where(c => c != null)
            .GroupBy(c => c.CardType)
            .OrderBy(g => g.Key);
        
        // Count by energy cost
        var costGroups = allCardsToSummarize
            .Where(c => c != null)
            .GroupBy(c => c.EnergyCost)
            .OrderBy(g => g.Key);
        
        System.Text.StringBuilder summary = new System.Text.StringBuilder();
        summary.AppendLine($"=== CARD DATABASE SUMMARY ===");
        summary.AppendLine($"Total Unique Cards: {allCardsToSummarize.Count}");
        summary.AppendLine($"Starter Cards: {starterCardsList.Count}");
        summary.AppendLine($"Draftable Cards: {draftableCardsList.Count}");
        summary.AppendLine($"Upgraded Cards: {upgradedCardsList.Count}");
        summary.AppendLine($"Legacy All Cards List: {allCardDataList.Count}");
        summary.AppendLine();
        
        summary.AppendLine("📋 Cards by Type:");
        foreach (var group in typeGroups)
        {
            summary.AppendLine($"  {group.Key}: {group.Count()} cards");
        }
        summary.AppendLine();
        
        summary.AppendLine("⚡ Cards by Energy Cost:");
        foreach (var group in costGroups)
        {
            summary.AppendLine($"  {group.Key} energy: {group.Count()} cards");
        }
        summary.AppendLine();
        
        // Find cards with interesting mechanics
        var comboCards = allCardsToSummarize.Where(c => c != null && c.BuildsCombo).Count();
        var finisherCards = allCardsToSummarize.Where(c => c != null && c.RequiresCombo).Count();
        var conditionalCards = allCardsToSummarize.Where(c => c != null && c.HasEffects && c.Effects.Any(e => e.conditionType != ConditionalType.None)).Count();
        var scalingCards = allCardsToSummarize.Where(c => c != null && c.HasEffects && c.Effects.Any(e => e.scalingType != ScalingType.None)).Count();
        
        summary.AppendLine("🎯 Special Mechanics:");
        summary.AppendLine($"  Combo Builders: {comboCards}");
        summary.AppendLine($"  Finishers: {finisherCards}");
        summary.AppendLine($"  Conditional Effects: {conditionalCards}");
        summary.AppendLine($"  Scaling Effects: {scalingCards}");
        
        Debug.Log(summary.ToString());
    }
#endif
} 