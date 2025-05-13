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
    // List of physical card GameObjects in the deck
    [Header("Card Objects")]
    private List<GameObject> deckCards = new List<GameObject>();

    // Parent transform for holding deck cards
    [SerializeField] private Transform deckParent;
    
    // Card prefab reference (should match CardSpawner's prefab)
    [SerializeField] private GameObject cardPrefab;
    
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
    
    // Reference to CardSpawner for creating cards
    private CardSpawner cardSpawner;

    private void Awake()
    {
        cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner == null)
        {
            Debug.LogError("CombatDeck: CardSpawner component is required but not found");
        }
        
        // If deckParent is not assigned, default to a child of this object
        if (deckParent == null)
        {
            // Check if one already exists
            Transform existingDeckTransform = transform.Find("DeckPosition");
            if (existingDeckTransform != null)
            {
                deckParent = existingDeckTransform;
            }
            else
            {
                // Create a new transform for deck positioning
                GameObject deckPositionObj = new GameObject("DeckPosition");
                deckPositionObj.transform.SetParent(transform);
                deckPositionObj.transform.localPosition = Vector3.zero;
                deckParent = deckPositionObj.transform;
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Update inspector lists for client visualization
        UpdateInspectorLists();
    }

    /// <summary>
    /// Sets up this combat deck by creating the initial physical card GameObjects
    /// that will be used throughout the combat phase
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
        
        // Create actual card GameObjects for each card ID - these same objects
        // will be moved between deck, hand, and discard throughout combat
        foreach (int cardId in cardIds)
        {
            // Spawn a card GameObject in the deck (disabled state)
            GameObject cardObj = SpawnCardInDeck(cardId);
            if (cardObj != null)
            {
                // Store the card object in our deck list
                deckCards.Add(cardObj);
            }
        }
        
        // Shuffle the deck
        ShuffleDeck();
        
        setupDone = true;
        Debug.Log($"Combat deck for {gameObject.name} set up with {deckCards.Count} physical card objects.");
        
        // Update inspector lists for debugging
        UpdateInspectorLists();
        
        // Notify of deck change
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Spawns a physical card GameObject in the deck position during initial deck setup
    /// </summary>
    private GameObject SpawnCardInDeck(int cardId)
    {
        if (cardSpawner == null)
        {
            Debug.LogError("Cannot spawn card: CardSpawner component not found");
            return null;
        }
        
        // Use the CardSpawner to create the initial card GameObject
        // The addToHand=false parameter ensures it's created for the deck
        GameObject cardObj = cardSpawner.SpawnCard(cardId, deckParent, false);
        
        if (cardObj != null)
        {
            // Set the card to be inactive while in the deck
            cardObj.SetActive(false);
            
            // Position the card at the deck transform
            cardObj.transform.position = deckParent.position;
            cardObj.transform.rotation = deckParent.rotation;
            
            // Add a reference to its deck
            Card cardComponent = cardObj.GetComponent<Card>();
            if (cardComponent != null)
            {
                cardComponent.SetCurrentContainer(CardLocation.Deck);
            }
            
            Debug.Log($"Card ID {cardId} spawned in deck for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"Failed to spawn card ID {cardId} in deck");
        }
        
        return cardObj;
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
    /// CAUTION: Adds a card to the combat deck by spawning a NEW card object
    /// Consider using AddCardToDeck(GameObject) when moving existing cards
    /// </summary>
    /// <param name="cardId">The ID of the card to spawn and add</param>
    [Server]
    private void AddCard(int cardId)
    {
        if (!IsServerInitialized) return;
        
        // Spawn a new card GameObject in the deck
        GameObject cardObj = SpawnCardInDeck(cardId);
        if (cardObj != null)
        {
            deckCards.Add(cardObj);
            
            // Update inspector lists
            UpdateInspectorLists();
            
            // Notify of deck change
            OnDeckChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Creates and adds a new card to the deck by ID - use this when you need
    /// to create a brand new card, not when moving existing card objects
    /// </summary>
    /// <param name="cardId">The ID of the card to create and add</param>
    [Server]
    public void AddCardById(int cardId)
    {
        AddCard(cardId);
    }
    
    /// <summary>
    /// Adds a single card GameObject to the deck
    /// This should be used when moving an existing card object from hand or discard
    /// </summary>
    /// <param name="cardObj">The card GameObject to add</param>
    [Server]
    public void AddCardToDeck(GameObject cardObj)
    {
        if (!IsServerInitialized || cardObj == null) return;
        
        // Set the card's parent to the deck transform
        cardObj.transform.SetParent(deckParent);
                
        // Set the card to be inactive while in the deck
        cardObj.SetActive(false);
                
        // Position the card at the deck transform
        cardObj.transform.position = deckParent.position;
        cardObj.transform.rotation = deckParent.rotation;
                
        // Update the card's container status
        Card cardComponent = cardObj.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetCurrentContainer(CardLocation.Deck);
        }
        
        // Add to our deck list
        deckCards.Add(cardObj);
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of deck change
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Draws a specific number of cards from the deck and moves them out of the deck
    /// </summary>
    /// <param name="count">Number of cards to draw</param>
    /// <returns>List of existing card GameObjects removed from the deck</returns>
    [Server]
    public List<GameObject> DrawCards(int count)
    {
        if (!IsServerInitialized) return new List<GameObject>();
        
        List<GameObject> drawnCards = new List<GameObject>();
        
        for (int i = 0; i < count; i++)
        {
            // Check if deck is empty
            if (deckCards.Count == 0)
            {
                // No more cards to draw
                break;
            }
            
            // Draw the top card - this removes the existing GameObject from the deck
            GameObject cardObj = deckCards[0];
            deckCards.RemoveAt(0);
            
            // Update the card's container status
            Card cardComponent = cardObj.GetComponent<Card>();
            if (cardComponent != null)
            {
                cardComponent.SetCurrentContainer(CardLocation.Hand);
            }
            
            drawnCards.Add(cardObj);
        }
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of deck change
        OnDeckChanged?.Invoke();
        
        return drawnCards;
    }
    
    /// <summary>
    /// Shuffles the deck - rearranges the actual card GameObjects in the list
    /// </summary>
    [Server]
    public void ShuffleDeck()
    {
        if (!IsServerInitialized) return;
        
        if (deckCards.Count <= 1) return; // No need to shuffle 0 or 1 card
        
        // Copy to list for shuffling
        List<GameObject> shuffledList = new List<GameObject>(deckCards);
        
        // Fisher-Yates shuffle
        for (int i = shuffledList.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            GameObject temp = shuffledList[i];
            shuffledList[i] = shuffledList[j];
            shuffledList[j] = temp;
        }
        
        // Replace the original deck with the shuffled deck
        deckCards = shuffledList;
        
        // Reorder the cards in the transform hierarchy
        for (int i = 0; i < deckCards.Count; i++)
        {
            deckCards[i].transform.SetSiblingIndex(i);
        }
        
        Debug.Log($"Combat deck for {gameObject.name} was shuffled.");
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of deck change
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Adds cards to the deck and shuffles them in
    /// </summary>
    [Server]
    public void AddCardsToDeck(List<GameObject> cardObjs)
    {
        if (!IsServerInitialized) return;
        
        // Add all cards to the deck and set them inactive
        foreach (GameObject cardObj in cardObjs)
        {
            if (cardObj != null)
            {
                // Set the card's parent to the deck transform
                cardObj.transform.SetParent(deckParent);
                
                // Set the card to be inactive while in the deck
                cardObj.SetActive(false);
                
                // Position the card at the deck transform
                cardObj.transform.position = deckParent.position;
                cardObj.transform.rotation = deckParent.rotation;
                
                // Update the card's container status
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    cardComponent.SetCurrentContainer(CardLocation.Deck);
                }
                
                // Add to our deck list
                deckCards.Add(cardObj);
            }
        }
        
        // Shuffle the deck
        ShuffleDeck();
        
        Debug.Log($"Added {cardObjs.Count} cards to deck for {gameObject.name}.");
    }
    
    /// <summary>
    /// Clears the deck by destroying all card GameObjects
    /// </summary>
    [Server]
    public void ClearDeck()
    {
        if (!IsServerInitialized) return;
        
        // Destroy all card GameObjects
        foreach (GameObject cardObj in deckCards)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
        
        // Clear the list
        deckCards.Clear();
        
        Debug.Log($"Combat deck cleared for {gameObject.name}.");
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of deck change
        OnDeckChanged?.Invoke();
    }
    
    /// <summary>
    /// Gets all card GameObjects in the deck
    /// </summary>
    /// <returns>List of card GameObjects</returns>
    public List<GameObject> GetDeckCards()
    {
        return new List<GameObject>(deckCards);
    }
    
    /// <summary>
    /// Gets the card IDs currently in the deck
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetDeckCardIds()
    {
        List<int> cardIds = new List<int>();
        
        foreach (GameObject cardObj in deckCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    cardIds.Add(cardComponent.CardId);
                }
            }
        }
        
        return cardIds;
    }
    
    /// <summary>
    /// Gets the number of cards in the deck
    /// </summary>
    /// <returns>Card count</returns>
    public int GetDeckSize()
    {
        return deckCards.Count;
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector deck list
        inspectorDeckList.Clear();
        inspectorDeckNames.Clear();
        
        foreach (GameObject cardObj in deckCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    int cardId = cardComponent.CardId;
                    inspectorDeckList.Add(cardId);
                    
                    // Try to get card name
                    string cardName = GetCardName(cardId);
                    inspectorDeckNames.Add(cardName);
                }
            }
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

// Add this enum to define card locations
public enum CardLocation
{
    Deck,
    Hand,
    Discard
} 