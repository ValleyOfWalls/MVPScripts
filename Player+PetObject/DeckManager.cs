using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// Manages adding cards to an entity's persistent deck through NetworkEntityDeck.
/// Attach to: NetworkEntity prefabs alongside NetworkEntityDeck.
/// </summary>
public class DeckManager : NetworkBehaviour
{
    private NetworkBehaviour parentEntity;
    private NetworkEntityDeck entityDeck;

    private void Awake()
    {
        // Get references
        parentEntity = GetComponent<NetworkEntity>();
        
        entityDeck = GetComponent<NetworkEntityDeck>();
        
        if (parentEntity == null)
        {
            Debug.LogError("DeckManager requires a NetworkEntity component on the same GameObject.");
        }
        
        if (entityDeck == null)
        {
            Debug.LogError("DeckManager requires a NetworkEntityDeck component on the same GameObject.");
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
        
        // Request the server to add the card through our ServerRpc
        if (parentEntity.IsOwner)
        {
            CmdAddCardToDeck(cardId);
            return true;
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
        
        // Request the server to remove the card through our ServerRpc
        if (parentEntity.IsOwner)
        {
            CmdRemoveCardFromDeck(cardId);
            return true;
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
    /// Server RPC to add a card to the entity's persistent deck
    /// </summary>
    [ServerRpc]
    private void CmdAddCardToDeck(int cardId)
    {
        if (entityDeck == null) return;
        
        NetworkEntity entity = parentEntity as NetworkEntity;
        string entityName = entity != null ? $"Entity {entity.EntityName.Value}" : "Unknown Entity";
            
        entityDeck.AddCard(cardId);
        Debug.Log($"{entityName} added card ID {cardId} to their deck via DeckManager.");
    }
    
    /// <summary>
    /// Server RPC to remove a card from the entity's persistent deck
    /// </summary>
    [ServerRpc]
    private void CmdRemoveCardFromDeck(int cardId)
    {
        if (entityDeck == null) return;
        
        NetworkEntity entity = parentEntity as NetworkEntity;
        string entityName = entity != null ? $"Entity {entity.EntityName.Value}" : "Unknown Entity";
            
        bool removed = entityDeck.RemoveCard(cardId);
        Debug.Log($"{entityName} {(removed ? "removed" : "failed to remove")} card ID {cardId} from their deck via DeckManager.");
    }
} 