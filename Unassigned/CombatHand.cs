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
    [Header("Hand Configuration")]
    [SerializeField] private int maxHandSize = 10;
    
    // List of card IDs in the hand, synced across network
    private readonly SyncList<int> handCardIds = new();
    
    // Inspector-visible representation of the hand (read-only, for debugging)
    [Header("Current Hand (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's hand. Read-only representation for debugging.")]
    private List<int> inspectorHandList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the hand (if available in CardDatabase)")]
    private List<string> inspectorHandNames = new List<string>();
    
    // Visual container for hand cards
    [Header("References")]
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
        
        // Update inspector lists on client start
        UpdateInspectorLists();
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
        
        // Add card to hand
        handCardIds.Add(cardId);
        return true;
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
        
        for (int i = 0; i < handCardIds.Count; i++)
        {
            if (handCardIds[i] == cardId)
            {
                handCardIds.RemoveAt(i);
                return true;
            }
        }
        
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
        
        List<int> discardedCards = new List<int>(handCardIds);
        handCardIds.Clear();
        return discardedCards;
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
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify subscribers
        OnHandChanged?.Invoke();
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector hand list
        inspectorHandList.Clear();
        inspectorHandNames.Clear();
        
        foreach (int cardId in handCardIds)
        {
            inspectorHandList.Add(cardId);
            
            // Try to get card name if available
            string cardName = GetCardName(cardId);
            inspectorHandNames.Add(cardName);
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