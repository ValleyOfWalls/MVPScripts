using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
} 