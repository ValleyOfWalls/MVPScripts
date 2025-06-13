using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;

/// <summary>
/// Stores and manages a persistent collection of cards for a network entity.
/// Attach to: NetworkEntity prefabs to store their persistent card collections.
/// </summary>
[System.Serializable]
public class NetworkEntityDeck : NetworkBehaviour
{
    // SyncList that contains all card IDs in the entity's persistent deck
    private readonly SyncList<int> cardIds = new();
    
    // Inspector-visible representation of the cards (read-only, for debugging)
    [Header("Cards in Deck (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's deck. Read-only representation for debugging.")]
    private List<int> inspectorCardList = new List<int>();
    
    // Optional inspector field to show card names (if available)
    [SerializeField, Tooltip("Names of cards in the deck (if available in CardDatabase)")]
    private List<string> inspectorCardNames = new List<string>();
    
    // Local cache of loaded card data
    private Dictionary<int, CardData> cardDataCache = new Dictionary<int, CardData>();
    
    // Delegate for deck changes
    public delegate void DeckCollectionChanged();
    public event DeckCollectionChanged OnDeckChanged;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for changes to the card list
        cardIds.OnChange += HandleCardListChanged;
        
        // Update inspector lists on client start
        UpdateInspectorLists();
        
        // Try to refresh again after a short delay to ensure CardDatabase is available
        Invoke("ForceRefreshInspectorView", 1.0f);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from changes to the card list
        cardIds.OnChange -= HandleCardListChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (IsServerInitialized)  // Updated from IsServer
        {
            // Server-side initialization
        }
    }

    /// <summary>
    /// Adds a card to the persistent deck
    /// </summary>
    /// <param name="cardId">The ID of the card to add</param>
    [Server]
    public void AddCard(int cardId)
    {
        if (!IsServerInitialized) return;
        
        cardIds.Add(cardId);

    }
    
    /// <summary>
    /// Removes a card from the persistent deck
    /// </summary>
    /// <param name="cardId">The ID of the card to remove</param>
    /// <returns>True if the card was successfully removed</returns>
    [Server]
    public bool RemoveCard(int cardId)
    {
        if (!IsServerInitialized) return false;
        
        for (int i = 0; i < cardIds.Count; i++)
        {
            if (cardIds[i] == cardId)
            {
                cardIds.RemoveAt(i);
    
                return true;
            }
        }
        
        Debug.LogWarning($"{gameObject.name} tried to remove card ID {cardId} from persistent deck, but it wasn't found.");
        return false;
    }
    
    /// <summary>
    /// Gets all card IDs in the persistent deck
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCardIds()
    {
        return new List<int>(cardIds);
    }
    
    /// <summary>
    /// Checks if the persistent deck contains a specific card
    /// </summary>
    /// <param name="cardId">The ID of the card to check</param>
    /// <returns>True if the card is in the deck</returns>
    public bool ContainsCard(int cardId)
    {
        foreach (int id in cardIds)
        {
            if (id == cardId)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Counts the number of copies of a specific card in the persistent deck
    /// </summary>
    /// <param name="cardId">The ID of the card to count</param>
    /// <returns>Number of copies</returns>
    public int GetCardCount(int cardId)
    {
        int count = 0;
        foreach (int id in cardIds)
        {
            if (id == cardId)
            {
                count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// Gets the total number of cards in the persistent deck
    /// </summary>
    /// <returns>Total number of cards</returns>
    public int GetTotalCardCount()
    {
        return cardIds.Count;
    }
    
    /// <summary>
    /// Clears all cards from the persistent deck
    /// </summary>
    [Server]
    public void ClearDeck()
    {
        if (!IsServerInitialized) return;
        
        cardIds.Clear();

    }

    /// <summary>
    /// Loads card data for a given ID
    /// </summary>
    /// <param name="cardId">ID of the card to load</param>
    /// <returns>Card data for the specified ID, or null if not found</returns>
    public CardData GetCardData(int cardId)
    {
        // Check if the data is already in cache
        if (cardDataCache.TryGetValue(cardId, out CardData cachedData))
        {
            return cachedData;
        }
        
        // Otherwise load it from the CardDatabase
        CardData data = CardDatabase.Instance.GetCardById(cardId);
        
        // Cache the data if found
        if (data != null)
        {
            cardDataCache[cardId] = data;
        }
        
        return data;
    }

    /// <summary>
    /// Handles changes to the card collection
    /// </summary>
    private void HandleCardListChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        // Update the inspector lists when cards change
        UpdateInspectorLists();
        
        // Notify subscribers that the card collection has changed
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector card list
        inspectorCardList.Clear();
        inspectorCardNames.Clear();
        
        foreach (int cardId in cardIds)
        {
            inspectorCardList.Add(cardId);
            
            // Try to get card name if available
            string cardName = "Unknown Card";
            
            // Check if CardDatabase is available
            if (CardDatabase.Instance != null)
            {
                CardData cardData = GetCardData(cardId);
                
                if (cardData != null)
                {
                    cardName = $"ID:{cardId} - {cardData.CardName}";
                }
                else
                {
                    cardName = $"ID:{cardId} - Not Found";
                    Debug.LogWarning($"Card ID {cardId} not found in database.");
                }
            }
            else
            {
                cardName = $"ID:{cardId} - No Database";
                // Don't log here to avoid log spam, as this might be called frequently
            }
            
            inspectorCardNames.Add(cardName);
        }
        
        // Verify that the names list is being populated
        if (inspectorCardNames.Count > 0 && inspectorCardList.Count != inspectorCardNames.Count)
        {
            Debug.LogWarning($"Mismatch between card list count ({inspectorCardList.Count}) and names list count ({inspectorCardNames.Count})");
        }
    }
    
    /// <summary>
    /// Force refresh the inspector lists, useful when the CardDatabase becomes available
    /// </summary>
    public void ForceRefreshInspectorView()
    {
        UpdateInspectorLists();

    }
    
    // Call this method in the Unity Editor to manually refresh the inspector view
    [ContextMenu("Refresh Inspector View")]
    public void RefreshInspectorView()
    {
        ForceRefreshInspectorView();
    }
} 