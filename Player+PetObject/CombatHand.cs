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
    
    // Event for hand changes
    public event Action OnHandChanged;

    private void Awake()
    {
        // Get reference to the HandManager component
        handManager = GetComponent<HandManager>();
        
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

        // Also update the entity's SyncList if this is a NetworkPet
        SyncWithEntityHand();
        
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
                
                // Also update the entity's SyncList
                SyncWithEntityHand();
                
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
        
        // Also update the entity's SyncList
        SyncWithEntityHand();
        
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
    /// Gets the number of card slots available in hand
    /// </summary>
    /// <returns>Number of available slots</returns>
    public int GetAvailableSpace()
    {
        return maxHandSize - handCardIds.Count;
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

    /// <summary>
    /// Synchronizes this CombatHand's cards with the parent entity's SyncList
    /// </summary>
    [Server]
    public void SyncWithEntityHand()
    {
        if (!IsServerInitialized) return;
        
        // For NetworkPet, we need to update its playerHandCardIds SyncList
        NetworkPet pet = parentEntity as NetworkPet;
        if (pet != null)
        {
            // Clear the pet's SyncList and repopulate it
            pet.playerHandCardIds.Clear();
            foreach (int cardId in handCardIds)
            {
                pet.playerHandCardIds.Add(cardId);
            }
            
            Debug.Log($"Synchronized {handCardIds.Count} cards from CombatHand to NetworkPet's playerHandCardIds");
        }
        
        // For NetworkPlayer, update its playerHandCardIds SyncList
        NetworkPlayer player = parentEntity as NetworkPlayer;
        if (player != null)
        {
            // Clear the player's SyncList and repopulate it
            player.playerHandCardIds.Clear();
            foreach (int cardId in handCardIds)
            {
                player.playerHandCardIds.Add(cardId);
            }
            
            Debug.Log($"Synchronized {handCardIds.Count} cards from CombatHand to NetworkPlayer's playerHandCardIds");
        }
    }
    
    /// <summary>
    /// Updates the CombatHand with the entity's SyncList (reverse sync)
    /// This is useful when initializing or when the entity's SyncList is modified externally
    /// </summary>
    [Server]
    public void SyncFromEntityHand()
    {
        if (!IsServerInitialized) return;
        
        // If we're a NetworkPet
        NetworkPet pet = parentEntity as NetworkPet;
        if (pet != null && pet.playerHandCardIds.Count > 0)
        {
            handCardIds.Clear();
            foreach (int cardId in pet.playerHandCardIds)
            {
                handCardIds.Add(cardId);
            }
            
            Debug.Log($"Synchronized {pet.playerHandCardIds.Count} cards from NetworkPet's playerHandCardIds to CombatHand");
            return;
        }
        
        // If we're a NetworkPlayer
        NetworkPlayer player = parentEntity as NetworkPlayer;
        if (player != null && player.playerHandCardIds.Count > 0)
        {
            handCardIds.Clear();
            foreach (int cardId in player.playerHandCardIds)
            {
                handCardIds.Add(cardId);
            }
            
            Debug.Log($"Synchronized {player.playerHandCardIds.Count} cards from NetworkPlayer's playerHandCardIds to CombatHand");
        }
    }
} 