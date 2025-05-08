using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using System;

/// <summary>
/// Manages a combat entity's hand of cards during gameplay.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs alongside HandManager.
/// </summary>
[System.Serializable]
public class CombatHand : NetworkBehaviour
{
    // Maximum number of cards that can be in hand
    [SerializeField] private int maxHandSize = 10;
    
    // List of card IDs in the hand, synced across network
    private readonly SyncList<int> handCardIds = new();
    
    // Visual container for hand cards
    [SerializeField] private Transform handCardsContainer;
    
    // Reference to HandManager component
    private HandManager handManager;
    
    // Dictionary to track instantiated card objects by card ID
    private Dictionary<int, GameObject> cardObjects = new Dictionary<int, GameObject>();
    
    // Card prefab for visual representation
    [SerializeField] private GameObject cardPrefab;
    
    // Event for hand changes
    public event Action OnHandChanged;

    private void Awake()
    {
        // Get reference to the HandManager component
        handManager = GetComponent<HandManager>();
        
        if (handManager == null)
        {
            Debug.LogError("CombatHand requires a HandManager component on the same GameObject");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        handCardIds.OnChange += HandleHandChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        handCardIds.OnChange -= HandleHandChanged;
    }

    /// <summary>
    /// Adds a card to the hand
    /// </summary>
    /// <param name="cardId">ID of the card to add</param>
    /// <returns>True if successfully added, false if hand is full</returns>
    [Server]
    public bool AddCard(int cardId)
    {
        if (!IsServerInitialized) return false;
        
        // Check if hand is full
        if (handCardIds.Count >= maxHandSize)
        {
            Debug.LogWarning($"Cannot add card {cardId} to {gameObject.name}'s hand: Hand is full ({handCardIds.Count}/{maxHandSize})");
            return false;
        }
        
        // Let HandManager handle adding the card
        if (handManager != null)
        {
            handManager.DrawOneCard();
            handCardIds.Add(cardId);
            return true;
        }
        
        Debug.LogError("Cannot add card: HandManager component not found");
        return false;
    }

    /// <summary>
    /// Removes a card from the hand
    /// </summary>
    /// <param name="cardId">ID of the card to remove</param>
    /// <returns>True if successfully removed</returns>
    [Server]
    public bool RemoveCard(int cardId)
    {
        if (!IsServerInitialized) return false;
        
        // Let HandManager handle removing the card
        if (handManager != null)
        {
            handManager.MoveCardToDiscard(cardId);
            for (int i = 0; i < handCardIds.Count; i++)
            {
                if (handCardIds[i] == cardId)
                {
                    handCardIds.RemoveAt(i);
                    return true;
                }
            }
        }
        
        Debug.LogError("Cannot remove card: HandManager component not found");
        return false;
    }

    /// <summary>
    /// Checks if the hand contains a specific card
    /// </summary>
    /// <param name="cardId">ID of the card to check</param>
    /// <returns>True if the card is in the hand</returns>
    public bool HasCard(int cardId)
    {
        return handCardIds.Contains(cardId);
    }

    /// <summary>
    /// Gets all card IDs in the hand
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCards()
    {
        return new List<int>(handCardIds);
    }

    /// <summary>
    /// Discards all cards from the hand
    /// </summary>
    /// <returns>List of discarded card IDs</returns>
    [Server]
    public List<int> DiscardHand()
    {
        if (!IsServerInitialized) 
            return new List<int>();
        
        // Let HandManager handle discarding the hand
        if (handManager != null)
        {
            List<int> discardedCards = new List<int>(handCardIds);
            handManager.DiscardHand();
            handCardIds.Clear();
            return discardedCards;
        }
        
        Debug.LogError("Cannot discard hand: HandManager component not found");
        return new List<int>();
    }

    /// <summary>
    /// Gets the number of cards in the hand
    /// </summary>
    /// <returns>Card count</returns>
    public int GetCardCount()
    {
        return handCardIds.Count;
    }

    /// <summary>
    /// Checks if the hand is full
    /// </summary>
    /// <returns>True if the hand has reached max capacity</returns>
    public bool IsFull()
    {
        return GetCardCount() >= maxHandSize;
    }

    /// <summary>
    /// Checks if the hand is empty
    /// </summary>
    /// <returns>True if the hand has no cards</returns>
    public bool IsEmpty()
    {
        return GetCardCount() == 0;
    }

    /// <summary>
    /// Handles changes to the hand
    /// </summary>
    private void HandleHandChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        OnHandChanged?.Invoke();
    }
} 