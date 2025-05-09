using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using System.Linq;

/// <summary>
/// Maintains a deck of cards used during combat, manages shuffling and drawing.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle their combat decks.
/// </summary>
[System.Serializable]
public class CombatDeck : NetworkBehaviour
{
    // List of card IDs in the deck, synced across network
    private readonly SyncList<int> deckCardIds = new();
    
    // Inspector-visible representation of the deck (read-only, for debugging)
    [Header("Current Deck (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's deck. Read-only representation for debugging.")]
    private List<int> inspectorDeckList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the deck (if available in CardDatabase)")]
    private List<string> inspectorDeckNames = new List<string>();
    
    // Random number generator for shuffling
    private System.Random rng = new System.Random();
    
    // Flag to track if setup has been done
    private bool setupDone = false;
    
    // Event for deck changes
    public delegate void DeckChanged();
    public event DeckChanged OnDeckChanged;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for changes to the card lists
        deckCardIds.OnChange += HandleDeckChanged;
        
        // Update inspector lists on client start
        UpdateInspectorLists();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from changes
        deckCardIds.OnChange -= HandleDeckChanged;
    }
    
    /// <summary>
    /// Sets up this combat deck with cards from the entity's persistent deck
    /// </summary>
    /// <param name="cardIds">List of card IDs to add to this combat deck</param>
    [Server]
    public void SetupDeck(List<int> cardIds)
    {
        if (!IsServerInitialized) return;
        
        if (setupDone)
        {
            Debug.LogWarning($"Combat deck for {gameObject.name} has already been set up. Call ResetSetupFlag() first to redo setup.");
            return;
        }
        
        ClearDeck();
        
        // Add each card to the deck
        foreach (int cardId in cardIds)
        {
            deckCardIds.Add(cardId);
        }
        
        // Shuffle the deck
        ShuffleDeck();
        
        setupDone = true;
        Debug.Log($"Combat deck for {gameObject.name} set up with {deckCardIds.Count} cards.");
    }
    
    /// <summary>
    /// Resets the setup flag to allow re-setup for next combat
    /// </summary>
    [Server]
    public void ResetSetupFlag()
    {
        if (!IsServerInitialized) return;
        setupDone = false;
        Debug.Log($"Combat deck setup flag reset for {gameObject.name}.");
    }
    
    /// <summary>
    /// Adds a card to the combat deck
    /// </summary>
    /// <param name="cardId">The ID of the card to add</param>
    [Server]
    public void AddCard(int cardId)
    {
        if (!IsServerInitialized) return;
        deckCardIds.Add(cardId);
    }
    
    /// <summary>
    /// Draws a specific number of cards from the deck
    /// </summary>
    /// <param name="count">Number of cards to draw</param>
    /// <returns>List of drawn card IDs</returns>
    [Server]
    public List<int> DrawCards(int count)
    {
        if (!IsServerInitialized) return new List<int>();
        
        List<int> drawnCards = new List<int>();
        
        for (int i = 0; i < count; i++)
        {
            // Check if deck is empty
            if (deckCardIds.Count == 0)
            {
                // No more cards to draw
                break;
            }
            
            // Draw the top card
            int cardId = deckCardIds[0];
            deckCardIds.RemoveAt(0);
            drawnCards.Add(cardId);
        }
        
        return drawnCards;
    }
    
    /// <summary>
    /// Shuffles the deck
    /// </summary>
    [Server]
    public void ShuffleDeck()
    {
        if (!IsServerInitialized) return;
        
        if (deckCardIds.Count <= 1) return; // No need to shuffle 0 or 1 card
        
        // Copy to list for shuffling
        List<int> shuffledList = deckCardIds.ToList();
        
        // Fisher-Yates shuffle
        for (int i = shuffledList.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int temp = shuffledList[i];
            shuffledList[i] = shuffledList[j];
            shuffledList[j] = temp;
        }
        
        // Replace the original deck with the shuffled deck
        deckCardIds.Clear();
        foreach (int cardId in shuffledList)
        {
            deckCardIds.Add(cardId);
        }
        
        Debug.Log($"Combat deck for {gameObject.name} was shuffled.");
    }
    
    /// <summary>
    /// Adds cards to the deck and shuffles them in
    /// </summary>
    [Server]
    public void AddCardsToDeck(List<int> cardIds)
    {
        if (!IsServerInitialized) return;
        
        // Add all cards to the deck
        foreach (int cardId in cardIds)
        {
            deckCardIds.Add(cardId);
        }
        
        // Shuffle the deck
        ShuffleDeck();
        
        Debug.Log($"Added {cardIds.Count} cards to deck for {gameObject.name}.");
    }
    
    /// <summary>
    /// Clears the deck
    /// </summary>
    [Server]
    public void ClearDeck()
    {
        if (!IsServerInitialized) return;
        
        deckCardIds.Clear();
        
        Debug.Log($"Combat deck cleared for {gameObject.name}.");
    }
    
    /// <summary>
    /// Gets all card IDs in the deck
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetDeckCardIds()
    {
        return new List<int>(deckCardIds);
    }
    
    /// <summary>
    /// Gets the number of cards in the deck
    /// </summary>
    /// <returns>Card count</returns>
    public int GetDeckSize()
    {
        return deckCardIds.Count;
    }
    
    /// <summary>
    /// Handler for deck changes
    /// </summary>
    private void HandleDeckChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateInspectorLists();
        
        // Notify subscribers of deck changes
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector deck list
        inspectorDeckList.Clear();
        inspectorDeckNames.Clear();
        
        foreach (int cardId in deckCardIds)
        {
            inspectorDeckList.Add(cardId);
            
            // Try to get card name if available
            string cardName = GetCardName(cardId);
            inspectorDeckNames.Add(cardName);
        }
    }
    
    /// <summary>
    /// Gets a card name from the CardDatabase if available
    /// </summary>
    private string GetCardName(int cardId)
    {
        if (CardDatabase.Instance != null)
        {
            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            return cardData != null ? cardData.CardName : $"Unknown Card {cardId}";
        }
        return $"Card ID {cardId}";
    }
    
    // Call this method in the Unity Editor to manually refresh the inspector view
    [ContextMenu("Refresh Inspector View")]
    public void RefreshInspectorView()
    {
        UpdateInspectorLists();
    }
} 