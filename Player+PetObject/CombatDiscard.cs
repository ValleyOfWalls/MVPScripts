using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages an entity's discard pile during combat
/// </summary>
public class CombatDiscard : NetworkBehaviour
{
    // Synced list of card IDs in the discard pile
    private readonly SyncList<int> discardCardIds = new SyncList<int>();
    
    // Inspector-visible representation of the discard pile (read-only, for debugging)
    [Header("Discard Pile (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's discard pile. Read-only representation for debugging.")]
    private List<int> inspectorDiscardList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the discard pile (if available in CardDatabase)")]
    private List<string> inspectorDiscardNames = new List<string>();
    
    // Delegate for discard pile changes
    public delegate void DiscardChanged();
    public event DiscardChanged OnDiscardChanged;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for discard pile changes
        discardCardIds.OnChange += HandleDiscardChanged;
        
        // Initialize inspector lists
        UpdateInspectorLists();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from discard pile changes
        discardCardIds.OnChange -= HandleDiscardChanged;
    }

    /// <summary>
    /// Adds a card to the discard pile
    /// </summary>
    /// <param name="cardId">ID of the card to add</param>
    [Server]
    public void AddCard(int cardId)
    {
        if (!IsServerInitialized) return;
        
        discardCardIds.Add(cardId);
    }

    /// <summary>
    /// Removes a card from the discard pile
    /// </summary>
    /// <param name="index">Index of the card to remove</param>
    /// <returns>ID of the removed card, or -1 if invalid</returns>
    [Server]
    public int RemoveCard(int index)
    {
        if (!IsServerInitialized || index < 0 || index >= discardCardIds.Count) return -1;
        
        int cardId = discardCardIds[index];
        discardCardIds.RemoveAt(index);
        return cardId;
    }

    /// <summary>
    /// Gets all card IDs from the discard pile
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCards()
    {
        List<int> result = new List<int>();
        
        foreach (int cardId in discardCardIds)
        {
            result.Add(cardId);
        }
        
        return result;
    }

    /// <summary>
    /// Clears the discard pile
    /// </summary>
    [Server]
    public void ClearDiscard()
    {
        if (!IsServerInitialized) return;
        
        discardCardIds.Clear();
    }

    /// <summary>
    /// Gets the number of cards in the discard pile
    /// </summary>
    /// <returns>Card count</returns>
    public int GetCardCount()
    {
        return discardCardIds.Count;
    }

    /// <summary>
    /// Checks if the discard pile is empty
    /// </summary>
    /// <returns>True if the discard pile has no cards</returns>
    public bool IsEmpty()
    {
        return discardCardIds.Count == 0;
    }

    /// <summary>
    /// Handles changes to the discard pile
    /// </summary>
    private void HandleDiscardChanged(SyncListOperation op, int index, int oldValue, int newValue, bool asServer)
    {
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify subscribers
        OnDiscardChanged?.Invoke();
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector discard list
        inspectorDiscardList.Clear();
        inspectorDiscardNames.Clear();
        
        foreach (int cardId in discardCardIds)
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