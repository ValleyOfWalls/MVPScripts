using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages an entity's discard pile during combat
/// </summary>
public class CombatDiscard : NetworkBehaviour
{
    // List of physical card GameObjects in the discard pile
    private List<GameObject> discardCards = new List<GameObject>();
    
    // Parent transform for holding discard cards
    [SerializeField] private Transform discardParent;
    
    // Inspector-visible representation of the discard pile (read-only, for debugging)
    [Header("Discard Pile (Read-Only)")]
    [SerializeField, Tooltip("Cards currently in this entity's discard pile. Read-only representation for debugging.")]
    private List<int> inspectorDiscardList = new List<int>();
    
    [SerializeField, Tooltip("Names of cards in the discard pile (if available in CardDatabase)")]
    private List<string> inspectorDiscardNames = new List<string>();
    
    // Delegate for discard pile changes
    public delegate void DiscardChanged();
    public event DiscardChanged OnDiscardChanged;

    private void Awake()
    {
        // If discardParent is not assigned, default to a child of this object
        if (discardParent == null)
        {
            // Check if one already exists
            Transform existingDiscardTransform = transform.Find("DiscardPosition");
            if (existingDiscardTransform != null)
            {
                discardParent = existingDiscardTransform;
            }
            else
            {
                // Create a new transform for discard positioning
                GameObject discardPositionObj = new GameObject("DiscardPosition");
                discardPositionObj.transform.SetParent(transform);
                discardPositionObj.transform.localPosition = Vector3.zero;
                discardParent = discardPositionObj.transform;
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
    /// Adds a card to the discard pile
    /// </summary>
    /// <param name="cardObj">The card GameObject to add</param>
    [Server]
    public void AddCard(GameObject cardObj)
    {
        if (!IsServerInitialized || cardObj == null) return;
        
        // Set the card's parent to the discard transform
        cardObj.transform.SetParent(discardParent);
        
        // Disable the card GameObject while in discard
        cardObj.SetActive(false);
        
        // Position it at the discard position
        cardObj.transform.position = discardParent.position;
        cardObj.transform.rotation = discardParent.rotation;
        
        // Update the card's container status
        Card cardComponent = cardObj.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetCurrentContainer(CardLocation.Discard);
        }
        
        // Add to the discard list
        discardCards.Add(cardObj);
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of discard change
        OnDiscardChanged?.Invoke();
    }

    /// <summary>
    /// Removes a card from the discard pile
    /// </summary>
    /// <param name="index">Index of the card to remove</param>
    /// <returns>The removed card GameObject, or null if invalid</returns>
    [Server]
    public GameObject RemoveCard(int index)
    {
        if (!IsServerInitialized || index < 0 || index >= discardCards.Count) return null;
        
        GameObject cardObj = discardCards[index];
        discardCards.RemoveAt(index);
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of discard change
        OnDiscardChanged?.Invoke();
        
        return cardObj;
    }

    /// <summary>
    /// Gets all card GameObjects from the discard pile
    /// </summary>
    /// <returns>List of card GameObjects</returns>
    public List<GameObject> GetAllCards()
    {
        return new List<GameObject>(discardCards);
    }

    /// <summary>
    /// Gets all card IDs from the discard pile (for compatibility)
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCardIds()
    {
        List<int> result = new List<int>();
        
        foreach (GameObject cardObj in discardCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    result.Add(cardComponent.CardId);
                }
            }
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
        
        // Destroy all card GameObjects in the discard
        foreach (GameObject cardObj in discardCards)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
        
        // Clear the list
        discardCards.Clear();
        
        // Update inspector lists
        UpdateInspectorLists();
        
        // Notify of discard change
        OnDiscardChanged?.Invoke();
    }

    /// <summary>
    /// Gets the number of cards in the discard pile
    /// </summary>
    /// <returns>Card count</returns>
    public int GetCardCount()
    {
        return discardCards.Count;
    }

    /// <summary>
    /// Checks if the discard pile is empty
    /// </summary>
    /// <returns>True if the discard pile has no cards</returns>
    public bool IsEmpty()
    {
        return discardCards.Count == 0;
    }
    
    /// <summary>
    /// Updates the inspector lists with current card data
    /// </summary>
    private void UpdateInspectorLists()
    {
        // Update the inspector discard list
        inspectorDiscardList.Clear();
        inspectorDiscardNames.Clear();
        
        foreach (GameObject cardObj in discardCards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null)
                {
                    int cardId = cardComponent.CardId;
                    inspectorDiscardList.Add(cardId);
                    
                    // Try to get card name if available
                    string cardName = GetCardName(cardId);
                    inspectorDiscardNames.Add(cardName);
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