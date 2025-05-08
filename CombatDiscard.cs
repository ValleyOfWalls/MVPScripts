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
    
    // Delegate for discard pile changes
    public delegate void DiscardChanged();
    public event DiscardChanged OnDiscardChanged;
    
    // UI element to show discard count
    [SerializeField] private TMPro.TextMeshProUGUI discardCountText;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for discard pile changes
        discardCardIds.OnChange += HandleDiscardChanged;
        
        // Update the UI
        UpdateDiscardCountUI();
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
        // Update the UI
        UpdateDiscardCountUI();
        
        // Notify subscribers
        OnDiscardChanged?.Invoke();
    }

    /// <summary>
    /// Updates the UI element showing discard count
    /// </summary>
    private void UpdateDiscardCountUI()
    {
        if (discardCountText != null)
        {
            discardCountText.text = $"Discard: {discardCardIds.Count}";
        }
    }
} 