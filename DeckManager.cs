using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// Manages adding cards to an entity's persistent deck through NetworkEntityDeck.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs alongside NetworkEntityDeck.
/// </summary>
public class DeckManager : MonoBehaviour
{
    private NetworkBehaviour parentEntity;
    private NetworkEntityDeck entityDeck;
    
    [SerializeField] private Transform deckDisplayContainer;
    [SerializeField] private GameObject cardPrefab;
    
    // Dictionary mapping card IDs to their display objects
    private Dictionary<int, GameObject> cardDisplayObjects = new Dictionary<int, GameObject>();

    private void Awake()
    {
        // Get references
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }
        
        entityDeck = GetComponent<NetworkEntityDeck>();
        
        if (parentEntity == null)
        {
            Debug.LogError("DeckManager requires a NetworkPlayer or NetworkPet component on the same GameObject.");
        }
        
        if (entityDeck == null)
        {
            Debug.LogError("DeckManager requires a NetworkEntityDeck component on the same GameObject.");
        }
    }

    private void OnEnable()
    {
        if (entityDeck != null)
        {
            // Subscribe to deck collection changes
            entityDeck.OnDeckChanged += UpdateDeckDisplay;
        }
    }

    private void OnDisable()
    {
        if (entityDeck != null)
        {
            // Unsubscribe from deck collection changes
            entityDeck.OnDeckChanged -= UpdateDeckDisplay;
        }
    }

    /// <summary>
    /// Adds a card to the entity's persistent deck
    /// </summary>
    /// <param name="cardId">The ID of the card to add</param>
    /// <returns>True if the card was successfully added</returns>
    public bool AddCardToDeck(int cardId)
    {
        if (entityDeck == null || parentEntity == null)
        {
            Debug.LogError("DeckManager: Cannot add card - missing components.");
            return false;
        }
        
        // Request the server to add the card
        if (parentEntity is NetworkPlayer player && player.IsOwner)
        {
            player.CmdAddCardToDeck(cardId);
            return true;
        }
        else if (parentEntity is NetworkPet pet && pet.IsOwner)
        {
            // Assuming NetworkPet has a similar method
            // pet.CmdAddCardToDeck(cardId);
            Debug.LogWarning("DeckManager: Adding cards to pet deck isn't currently supported from client.");
            return false;
        }
        
        Debug.LogWarning("DeckManager: Cannot add card - not the owner of this entity.");
        return false;
    }
    
    /// <summary>
    /// Removes a card from the entity's persistent deck
    /// </summary>
    /// <param name="cardId">The ID of the card to remove</param>
    /// <returns>True if request was sent (not guaranteed to succeed)</returns>
    public bool RemoveCardFromDeck(int cardId)
    {
        if (entityDeck == null || parentEntity == null)
        {
            Debug.LogError("DeckManager: Cannot remove card - missing components.");
            return false;
        }
        
        // Request the server to remove the card
        if (parentEntity is NetworkPlayer player && player.IsOwner)
        {
            player.CmdRemoveCardFromDeck(cardId);
            return true;
        }
        else if (parentEntity is NetworkPet pet && pet.IsOwner)
        {
            // Assuming NetworkPet has a similar method
            // pet.CmdRemoveCardFromDeck(cardId);
            Debug.LogWarning("DeckManager: Removing cards from pet deck isn't currently supported from client.");
            return false;
        }
        
        Debug.LogWarning("DeckManager: Cannot remove card - not the owner of this entity.");
        return false;
    }
    
    /// <summary>
    /// Gets the total number of cards in the entity's persistent deck
    /// </summary>
    /// <returns>Card count</returns>
    public int GetDeckSize()
    {
        if (entityDeck != null)
        {
            return entityDeck.GetTotalCardCount();
        }
        
        Debug.LogError("DeckManager: Cannot get deck size - missing entity deck component.");
        return 0;
    }

    /// <summary>
    /// Gets a list of all card IDs in the deck
    /// </summary>
    /// <returns>List of card IDs</returns>
    public List<int> GetAllCardIds()
    {
        return entityDeck.GetAllCardIds();
    }

    /// <summary>
    /// Updates the visual display of cards in the deck
    /// </summary>
    private void UpdateDeckDisplay()
    {
        if (deckDisplayContainer == null) return;

        // Clear existing display
        foreach (Transform child in deckDisplayContainer)
        {
            Destroy(child.gameObject);
        }
        cardDisplayObjects.Clear();

        // Get all card IDs
        List<int> allCards = entityDeck.GetAllCardIds();

        // Create displays for each card
        foreach (int cardId in allCards)
        {
            CardData cardData = entityDeck.GetCardData(cardId);
            if (cardData == null) continue;

            // Instantiate card object
            GameObject cardObj = Instantiate(cardPrefab, deckDisplayContainer);
            Card cardComponent = cardObj.GetComponent<Card>();
            
            if (cardComponent != null)
            {
                // Initialize the card with data
                cardComponent.Initialize(cardData);
                cardDisplayObjects[cardId] = cardObj;
            }
        }
    }
} 