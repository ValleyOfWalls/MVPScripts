using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    [SerializeField]
    private List<CardData> allCardDataList = new List<CardData>(); // Assign all CardData SOs here in Inspector

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

        // Sort cards by their existing CardId to ensure consistent ordering
        allCardDataList.Sort((a, b) => a.CardId.CompareTo(b.CardId));

        // Ensure cards have sequential IDs starting from 1
        int nextId = 1;
        
        // Option 1: If using the list assigned in Inspector
        foreach (CardData data in allCardDataList)
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
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} cards from Inspector list.");

        // Option 2: If loading from Resources folder
        /*
        CardData[] loadedCards = Resources.LoadAll<CardData>(cardDataPathInResources);
        foreach (CardData data in loadedCards)
        {
            if (data != null)
            {
                if (!cardDataById.ContainsKey(data.CardId))
                {
                    cardDataById.Add(data.CardId, data);
                }
                else
                {
                    Debug.LogWarning($"CardDatabase: Duplicate CardId {data.CardId} found for '{data.CardName}' and '{cardDataById[data.CardId].CardName}'. Ignoring duplicate.");
                }
            }
        }
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} cards from Resources folder: Assets/Resources/{cardDataPathInResources}");
        */

        if (cardDataById.Count == 0)
        {
            Debug.LogWarning("CardDatabase is empty. Make sure to assign CardData ScriptableObjects to the list in the Inspector or place them in the Resources folder if using that loading method.");
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

    public List<CardData> GetRandomCards(int count)
    {
        if (count <= 0) return new List<CardData>();
        
        // Create a shuffled list of available cards
        List<CardData> availableCards = new List<CardData>(allCardDataList);
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

    /// <summary>
    /// Gets random cards allowing duplicates - useful for draft packs
    /// </summary>
    /// <param name="count">Number of cards to get</param>
    /// <returns>List of random cards, potentially with duplicates</returns>
    public List<CardData> GetRandomCardsWithDuplicates(int count)
    {
        if (count <= 0) return new List<CardData>();
        
        if (allCardDataList.Count == 0)
        {
            Debug.LogWarning("CardDatabase: No cards available to select from.");
            return new List<CardData>();
        }
        
        List<CardData> result = new List<CardData>();
        
        // Select random cards allowing duplicates
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, allCardDataList.Count);
            result.Add(allCardDataList[randomIndex]);
        }
        
        return result;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor function to automatically find and populate all CardData assets in the project
    /// </summary>
    [ContextMenu("Auto-Populate Card Database")]
    public void AutoPopulateCardDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:CardData");
        List<CardData> foundCards = new List<CardData>();
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            CardData cardData = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            
            if (cardData != null)
            {
                foundCards.Add(cardData);
            }
        }
        
        // Sort cards by name for consistency
        foundCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
        
        // Update the list
        allCardDataList = foundCards;
        
        // Mark as dirty so the changes are saved
        EditorUtility.SetDirty(this);
        
        Debug.Log($"CardDatabase: Auto-populated with {foundCards.Count} CardData assets found in project");
        
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
        
        foreach (CardData card in allCardDataList)
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
                  $"Total Cards: {allCardDataList.Count}");
        
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
        if (allCardDataList.Count == 0)
        {
            Debug.Log("CardDatabase: No cards found. Use 'Auto-Populate Card Database' first.");
            return;
        }
        
        // Count by type
        var typeGroups = allCardDataList
            .Where(c => c != null)
            .GroupBy(c => c.CardType)
            .OrderBy(g => g.Key);
        
        // Count by energy cost
        var costGroups = allCardDataList
            .Where(c => c != null)
            .GroupBy(c => c.EnergyCost)
            .OrderBy(g => g.Key);
        
        System.Text.StringBuilder summary = new System.Text.StringBuilder();
        summary.AppendLine($"=== CARD DATABASE SUMMARY ===");
        summary.AppendLine($"Total Cards: {allCardDataList.Count}");
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
        var comboCards = allCardDataList.Where(c => c != null && c.BuildsCombo).Count();
        var finisherCards = allCardDataList.Where(c => c != null && c.RequiresCombo).Count();
        var conditionalCards = allCardDataList.Where(c => c != null && c.HasEffects && c.Effects.Any(e => e.conditionType != ConditionalType.None)).Count();
        var scalingCards = allCardDataList.Where(c => c != null && c.HasEffects && c.Effects.Any(e => e.scalingType != ScalingType.None)).Count();
        
        summary.AppendLine("🎯 Special Mechanics:");
        summary.AppendLine($"  Combo Builders: {comboCards}");
        summary.AppendLine($"  Finishers: {finisherCards}");
        summary.AppendLine($"  Conditional Effects: {conditionalCards}");
        summary.AppendLine($"  Scaling Effects: {scalingCards}");
        
        Debug.Log(summary.ToString());
    }
#endif
} 