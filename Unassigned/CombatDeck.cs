using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using System.Linq;

/// <summary>
/// Maintains a deck of cards used during combat, manages shuffling, drawing, and discard piles.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle their combat decks.
/// </summary>
[System.Serializable]
public class CombatDeck : NetworkBehaviour
{
    // List of card IDs in the deck, synced across network
    private readonly SyncList<int> deckCardIds = new();
    
    // List of card IDs in the discard pile, synced across network
    private readonly SyncList<int> discardPileCardIds = new();
    
    // Inspector-visible representation of the deck (read-only, for debugging)
    [Header("Current Deck (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's deck. Read-only representation for debugging.")]
    private List<int> inspectorDeckList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the deck (if available in CardDatabase)")]
    private List<string> inspectorDeckNames = new List<string>();
    
    // Inspector-visible representation of the discard pile (read-only, for debugging)
    [Header("Discard Pile (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's discard pile. Read-only representation for debugging.")]
    private List<int> inspectorDiscardList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the discard pile (if available in CardDatabase)")]
    private List<string> inspectorDiscardNames = new List<string>();
    
    // Visual transforms for card containers - serialized in inspector
    [Header("Transform References")]
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;
    
    // Random number generator for shuffling
    private System.Random rng = new System.Random();
    
    // Flag to track if setup has been done
    private bool setupDone = false;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        // Initialize calls removed - no longer needed in FishNet v4
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for changes to the card lists
        deckCardIds.OnChange += HandleDeckChanged;
        discardPileCardIds.OnChange += HandleDiscardChanged;
        
        // Update inspector lists on client start
        UpdateInspectorLists();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from changes
        deckCardIds.OnChange -= HandleDeckChanged;
        discardPileCardIds.OnChange -= HandleDiscardChanged;
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
                // Reshuffle discard pile into deck if possible
                if (discardPileCardIds.Count > 0)
                {
                    ShuffleDiscardIntoDeck();
                }
                else
                {
                    // No more cards to draw
                    break;
                }
            }
            
            // Draw the top card
            if (deckCardIds.Count > 0)
            {
                int cardId = deckCardIds[0];
                deckCardIds.RemoveAt(0);
                drawnCards.Add(cardId);
            }
        }
        
        return drawnCards;
    }
    
    /// <summary>
    /// Discards a specific card
    /// </summary>
    /// <param name="cardId">The ID of the card to discard</param>
    [Server]
    public void DiscardCard(int cardId)
    {
        if (!IsServerInitialized) return;
        discardPileCardIds.Add(cardId);
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
    /// Moves all cards from the discard pile back into the deck and shuffles
    /// </summary>
    [Server]
    public void ShuffleDiscardIntoDeck()
    {
        if (!IsServerInitialized) return;
        
        // Add all discard pile cards to the deck
        foreach (int cardId in discardPileCardIds)
        {
            deckCardIds.Add(cardId);
        }
        
        // Clear the discard pile
        discardPileCardIds.Clear();
        
        // Shuffle the deck
        ShuffleDeck();
        
        Debug.Log($"Discard pile shuffled into deck for {gameObject.name}.");
    }
    
    /// <summary>
    /// Clears the deck and discard pile
    /// </summary>
    [Server]
    public void ClearDeck()
    {
        if (!IsServerInitialized) return;
        
        deckCardIds.Clear();
        discardPileCardIds.Clear();
        
        Debug.Log($"Combat deck and discard pile cleared for {gameObject.name}.");
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
    /// Gets all card IDs in the discard pile
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetDiscardPileCardIds()
    {
        return new List<int>(discardPileCardIds);
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
    /// Gets the number of cards in the discard pile
    /// </summary>
    /// <returns>Card count</returns>
    public int GetDiscardPileSize()
    {
        return discardPileCardIds.Count;
    }
    
    /// <summary>
    /// Handler for deck changes
    /// </summary>
    private void HandleDeckChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateInspectorLists();
    }
    
    /// <summary>
    /// Handler for discard pile changes
    /// </summary>
    private void HandleDiscardChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateInspectorLists();
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
        
        // Update the inspector discard pile list
        inspectorDiscardList.Clear();
        inspectorDiscardNames.Clear();
        
        foreach (int cardId in discardPileCardIds)
        {
            inspectorDiscardList.Add(cardId);
            
            // Try to get card name if available
            string cardName = GetCardName(cardId);
            inspectorDiscardNames.Add(cardName);
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