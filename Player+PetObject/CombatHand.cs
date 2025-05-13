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
    
    // List of card GameObjects in the hand
    private List<GameObject> handCards = new List<GameObject>();
    
    // Parent transform for holding hand cards
    [SerializeField] private Transform handParent;
    
    // Reference to owner entity
    private NetworkBehaviour parentEntity;
    
    // Inspector-visible representation of the hand (read-only, for debugging)
    [Header("Current Hand (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's hand. Read-only representation for debugging.")]
    private List<int> inspectorHandList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the hand (if available in CardDatabase)")]
    private List<string> inspectorHandNames = new List<string>();
    
    // Reference to HandManager component
    private HandManager handManager;
    
    // Reference to CardSpawner component for card visualization
    private CardSpawner cardSpawner;
    
    // Event for hand changes
    public event Action OnHandChanged;

    private void Awake()
    {
        // Get reference to the HandManager component
        handManager = GetComponent<HandManager>();
        
        // Get reference to the CardSpawner component
        cardSpawner = GetComponent<CardSpawner>();
        
        // Get reference to the parent entity
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }
        
        if (handManager == null)
        {
            Debug.LogError("CombatHand requires a HandManager component on the same GameObject");
        }
        
        if (cardSpawner == null)
        {
            Debug.LogError("CombatHand requires a CardSpawner component on the same GameObject");
        }
        
        // If handParent is not assigned, default to a child of this object
        if (handParent == null)
        {
            // Try to get from UI component first
            if (parentEntity is NetworkPlayer)
            {
                NetworkPlayerUI playerUI = GetComponent<NetworkPlayerUI>();
                if (playerUI != null)
                {
                    handParent = playerUI.GetPlayerHandTransform();
                }
            }
            else if (parentEntity is NetworkPet)
            {
                NetworkPetUI petUI = GetComponent<NetworkPetUI>();
                if (petUI != null)
                {
                    handParent = petUI.GetPetHandTransform();
                }
            }
            
            // If still null, create a new transform
            if (handParent == null)
            {
                // Check if one already exists
                Transform existingHandTransform = transform.Find("HandPosition");
                if (existingHandTransform != null)
                {
                    handParent = existingHandTransform;
                }
                else
                {
                    // Create a new transform for hand positioning
                    GameObject handPositionObj = new GameObject("HandPosition");
                    handPositionObj.transform.SetParent(transform);
                    handPositionObj.transform.localPosition = Vector3.zero;
                    handParent = handPositionObj.transform;
                }
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Initialize inspector lists
        UpdateInspectorLists();
    }

    /// <summary>
    /// Adds a card to the hand
    /// </summary>
    /// <param name="cardObj">The card GameObject to add</param>
    /// <returns>True if card was added, false if hand is full</returns>
    [Server]
    public bool AddCard(GameObject cardObj)
    {
        if (!IsServerInitialized || cardObj == null) return false;
        
        // Check hand limit
        if (handCards.Count >= maxHandSize)
        {
            Debug.LogWarning($"Cannot add card to hand: Hand is full ({handCards.Count}/{maxHandSize})");
            return false;
        }
        
        // Set the card's parent to the hand parent transform
        cardObj.transform.SetParent(handParent);
        
        // Enable the card so it's visible
        cardObj.SetActive(true);
        
        // Update the card's container status
        Card cardComponent = cardObj.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetCurrentContainer(CardLocation.Hand);
        }
        
        // Add to our hand list
        handCards.Add(cardObj);
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of hand change
        OnHandChanged?.Invoke();
        
        return true;
    }
    
    /// <summary>
    /// Positions a card in the hand layout
    /// </summary>
    private void PositionCardInHand(GameObject cardObj)
    {
        if (handParent == null || cardObj == null) return;
        
        // Get the index of this card in the hand
        int index = handCards.IndexOf(cardObj);
        if (index == -1) index = handCards.Count; // Not found, assuming it's new
        
        // Simple linear positioning for now - this can be enhanced with a curved layout
        float spacing = 150f; // Card spacing in pixels
        float startX = -((handCards.Count - 1) * spacing) / 2f;
        
        // Set the local position
        cardObj.transform.localPosition = new Vector3(startX + (index * spacing), 0f, 0f);
        
        // Set rotation to face up
        cardObj.transform.localRotation = Quaternion.identity;
    }
    
    /// <summary>
    /// Refreshes the positions of all cards in hand
    /// </summary>
    private void RefreshHandPositions()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            PositionCardInHand(handCards[i]);
        }
    }

    /// <summary>
    /// Checks if a card with the given ID is in the hand
    /// </summary>
    public bool HasCard(int cardId)
    {
        foreach (GameObject cardObj in handCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null && cardComponent.CardId == cardId)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Removes a card with the given ID from the hand
    /// </summary>
    /// <param name="cardId">ID of the card to remove</param>
    /// <returns>The removed card GameObject, or null if not found</returns>
    [Server]
    public GameObject RemoveCardById(int cardId)
    {
        if (!IsServerInitialized) return null;
        
        // Find the card with the given ID
        for (int i = 0; i < handCards.Count; i++)
        {
            GameObject cardObj = handCards[i];
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null && cardComponent.CardId == cardId)
                {
                    // Remove the card from our list
                    handCards.RemoveAt(i);
                    
                    // Update inspector lists
                    UpdateInspectorLists();
                    
                    // Notify of hand change
                    OnHandChanged?.Invoke();
                    
                    // Return the card GameObject
                    return cardObj;
                }
            }
        }
        
        Debug.LogWarning($"Card with ID {cardId} not found in hand");
        return null;
    }
    
    /// <summary>
    /// Removes a specific card GameObject from the hand
    /// </summary>
    /// <param name="cardObj">The card GameObject to remove</param>
    /// <returns>True if the card was removed, false if not found</returns>
    [Server]
    public bool RemoveCard(GameObject cardObj)
    {
        if (!IsServerInitialized || cardObj == null) return false;
        
        // Remove the card from our list
        bool removed = handCards.Remove(cardObj);
        
        if (removed)
        {
            // Update inspector lists
            UpdateInspectorLists();
            
            // Notify of hand change
            OnHandChanged?.Invoke();
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Discards all cards from the hand
    /// </summary>
    /// <returns>List of discarded card GameObjects</returns>
    [Server]
    public List<GameObject> DiscardHand()
    {
        if (!IsServerInitialized) return new List<GameObject>();
        
        // Get a copy of the cards
        List<GameObject> discardedCards = new List<GameObject>(handCards);
        
        // Clear the hand
        handCards.Clear();
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of hand change
        OnHandChanged?.Invoke();
        
        return discardedCards;
    }

    /// <summary>
    /// Gets all card IDs in the hand (for compatibility)
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCards()
    {
        List<int> cardIds = new List<int>();
        
        foreach (GameObject cardObj in handCards)
        {
            Card cardComponent = cardObj.GetComponent<Card>();
            if (cardComponent != null)
            {
                cardIds.Add(cardComponent.CardId);
            }
        }
        
        return cardIds;
    }
    
    /// <summary>
    /// Gets all card GameObjects in the hand
    /// </summary>
    /// <returns>List of card GameObjects</returns>
    public List<GameObject> GetAllCardObjects()
    {
        return new List<GameObject>(handCards);
    }

    /// <summary>
    /// Gets the number of cards in the hand
    /// </summary>
    /// <returns>Card count</returns>
    public int GetCardCount()
    {
        return handCards.Count;
    }

    /// <summary>
    /// Gets the number of card slots available in hand
    /// </summary>
    /// <returns>Number of available slots</returns>
    public int GetAvailableSpace()
    {
        return maxHandSize - handCards.Count;
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
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector hand list
        inspectorHandList.Clear();
        inspectorHandNames.Clear();
        
        foreach (GameObject cardObj in handCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    int cardId = cardComponent.CardId;
                    inspectorHandList.Add(cardId);
                    
                    // Try to get card name if available
                    string cardName = GetCardName(cardId);
                    inspectorHandNames.Add(cardName);
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