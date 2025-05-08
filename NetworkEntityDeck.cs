using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;

/// <summary>
/// Maintains an entity's collection of cards that persists between combats.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to store their persistent card collections.
/// </summary>
[System.Serializable]
public class NetworkEntityDeck : NetworkBehaviour
{
    // SyncList that contains all card IDs in the entity's persistent deck
    private readonly SyncList<int> cardIds = new();
    
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
        Debug.Log($"{gameObject.name} added card ID {cardId} to persistent deck. Total cards: {cardIds.Count}");
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
                Debug.Log($"{gameObject.name} removed card ID {cardId} from persistent deck. Total cards: {cardIds.Count}");
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
        Debug.Log($"{gameObject.name} cleared persistent deck.");
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
        // Notify subscribers that the card collection has changed
        OnDeckChanged?.Invoke();
    }
} 