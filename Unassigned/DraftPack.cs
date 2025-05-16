using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Represents a pack of cards that players can draft from
/// </summary>
public class DraftPack : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private GameObject cardPrefab;

    // Synced list of card IDs available in this pack
    private readonly SyncList<int> availableCardIds = new SyncList<int>();
    
    // Local cache of spawned card GameObjects
    private Dictionary<int, GameObject> spawnedCards = new Dictionary<int, GameObject>();

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for card list changes
        availableCardIds.OnChange += HandleCardListChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from card list changes
        availableCardIds.OnChange -= HandleCardListChanged;
    }

    /// <summary>
    /// Initializes the draft pack with a set of cards
    /// Called from DraftPackSetup
    /// </summary>
    /// <param name="cardIds">List of card IDs to include in the pack</param>
    [Server]
    public void InitializePack(List<int> cardIds)
    {
        if (!IsServerInitialized) return;
        
        // Clear any existing cards
        availableCardIds.Clear();
        
        // Add the new cards
        foreach (int cardId in cardIds)
        {
            availableCardIds.Add(cardId);
        }
    }

    /// <summary>
    /// Removes a card from the pack after it's been drafted
    /// </summary>
    /// <param name="cardId">ID of the card to remove</param>
    /// <returns>True if the card was successfully removed</returns>
    [Server]
    public bool RemoveCard(int cardId)
    {
        if (!IsServerInitialized) return false;
        
        // Find and remove the card ID from the collection
        for (int i = 0; i < availableCardIds.Count; i++)
        {
            if (availableCardIds[i] == cardId)
            {
                availableCardIds.RemoveAt(i);
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Checks if the pack is empty
    /// </summary>
    /// <returns>True if the pack has no cards left</returns>
    public bool IsEmpty()
    {
        return availableCardIds.Count == 0;
    }

    /// <summary>
    /// Updates the visual display of cards in the pack
    /// </summary>
    private void UpdatePackDisplay()
    {
        if (cardsContainer == null) return;
        
        // Clear current display
        foreach (var spawnedCard in spawnedCards.Values)
        {
            Destroy(spawnedCard);
        }
        spawnedCards.Clear();
        
        // Create displays for each available card
        foreach (int cardId in availableCardIds)
        {
            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null) continue;
            
            // Instantiate card object
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            Card card = cardObj.GetComponent<Card>();
            DraftSelectionManager selectionManager = cardObj.GetComponent<DraftSelectionManager>();
            
            if (card != null)
            {
                // Initialize the card with data
                card.Initialize(cardData);
                
                if (selectionManager != null)
                {
                    selectionManager.SetDraftPack(this);
                }
                
                spawnedCards[cardId] = cardObj;
            }
        }
    }

    /// <summary>
    /// Called when a card is selected from this pack
    /// </summary>
    /// <param name="cardId">ID of the selected card</param>
    public void OnCardSelected(int cardId)
    {
        // Get the local player
        NetworkEntity localPlayer = null;
        NetworkEntity[] players = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity player in players)
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                break;
            }
        }
        
        if (localPlayer == null)
        {
            Debug.LogError("DraftPack: Could not find local player!");
            return;
        }
        
        // Request card selection from server
        DraftManager.Instance.CmdSelectCardFromPack(localPlayer.ObjectId, ObjectId, cardId);
    }

    /// <summary>
    /// Shows the pack UI for the current player
    /// </summary>
    public void ShowPackForPlayer()
    {
        // Make sure the GameObject is active
        gameObject.SetActive(true);
        
        // Refresh the display
        UpdatePackDisplay();
    }

    /// <summary>
    /// Handles changes to the cards in the pack
    /// </summary>
    private void HandleCardListChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdatePackDisplay();
    }
} 