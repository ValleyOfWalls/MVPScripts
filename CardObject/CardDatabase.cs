using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages all card data definitions in the game.
/// Should be placed in the scene before any objects that need to access card data.
/// </summary>
public class CardDatabase : NetworkBehaviour
{
    public static CardDatabase Instance { get; private set; }

    [SerializeField]
    private List<CardData> allCardDataList = new List<CardData>(); // Assign all CardData SOs here in Inspector

    // OR, load from Resources:
    // [SerializeField] private string cardDataPathInResources = "CardData"; 

    private Dictionary<int, CardData> cardDataById = new Dictionary<int, CardData>();
    
    // Initialization tracking
    private bool isDatabaseInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeLocalDatabase(); // Initialize immediately in Awake for both server and client
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Ensure database is initialized on client
        if (Instance == this)
        {
            InitializeLocalDatabase();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Mark as initialized on server and ensure database is loaded
        if (Instance == this)
        {
            InitializeLocalDatabase();
        }
    }

    /// <summary>
    /// Initialize the local database with card data from the inspector
    /// </summary>
    private void InitializeLocalDatabase()
    {
        if (isDatabaseInitialized) return; // Skip if already initialized

        cardDataById.Clear();
        
        // Load cards from the list assigned in Inspector
        foreach (CardData data in allCardDataList)
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
        
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} cards from Inspector list on {(IsServerInitialized ? "server" : "client")}.");
        isDatabaseInitialized = true;

        if (cardDataById.Count == 0)
        {
            Debug.LogWarning("CardDatabase is empty. Make sure to assign CardData ScriptableObjects to the list in the Inspector.");
        }
    }

    /// <summary>
    /// Get a card by ID
    /// </summary>
    public CardData GetCardById(int cardId)
    {
        // Ensure database is initialized
        if (!isDatabaseInitialized)
        {
            InitializeLocalDatabase();
        }

        // Try to get the card
        if (cardDataById.TryGetValue(cardId, out CardData data))
        {
            return data;
        }
        
        Debug.LogError($"CardDatabase: Card with ID {cardId} not found. This indicates a synchronization issue between server and clients.");
        return null;
    }

    /// <summary>
    /// Get all cards in the database
    /// </summary>
    public List<CardData> GetAllCards()
    {
        if (!isDatabaseInitialized)
        {
            InitializeLocalDatabase();
        }
        return new List<CardData>(cardDataById.Values);
    }

    /// <summary>
    /// Get a list of random cards
    /// </summary>
    public List<CardData> GetRandomCards(int count)
    {
        if (count <= 0) return new List<CardData>();
        
        if (!isDatabaseInitialized)
        {
            InitializeLocalDatabase();
        }

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
    /// Check if database contains a card with the specified ID
    /// </summary>
    public bool ContainsCard(int cardId)
    {
        if (!isDatabaseInitialized)
        {
            InitializeLocalDatabase();
        }
        return cardDataById.ContainsKey(cardId);
    }
} 