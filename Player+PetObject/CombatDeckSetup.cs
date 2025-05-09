using UnityEngine;
using FishNet.Object;

/// <summary>
/// Prepares a combat deck from entity's collection
/// </summary>
public class CombatDeckSetup : NetworkBehaviour
{
    // References to entity's components
    private NetworkEntityDeck entityDeck;
    private CombatDeck combatDeck;
    
    // Flag to track if setup has been done
    private bool setupDone = false;

    private void Awake()
    {
        // Get references
        entityDeck = GetComponent<NetworkEntityDeck>();
        combatDeck = GetComponent<CombatDeck>();
        
        if (entityDeck == null)
        {
            Debug.LogError("CombatDeckSetup requires a NetworkEntityDeck component on the same GameObject.");
        }
        
        if (combatDeck == null)
        {
            Debug.LogError("CombatDeckSetup requires a CombatDeck component on the same GameObject.");
        }
    }

    /// <summary>
    /// Called by CombatSetup to prepare the combat deck
    /// </summary>
    [Server]
    public void SetupCombatDeck()
    {
        if (!IsServerInitialized || setupDone || entityDeck == null || combatDeck == null) return;
        
        // Clear combat deck
        combatDeck.ClearDeck();
        
        // Get all cards from entity deck
        var allCards = entityDeck.GetAllCardIds();
        
        // Add each card to combat deck
        foreach (int cardId in allCards)
        {
            combatDeck.AddCard(cardId);
        }
        
        // Shuffle the combat deck
        combatDeck.ShuffleDeck();
        
        setupDone = true;
        Debug.Log($"Setup combat deck for {gameObject.name} with {allCards.Count} cards");
    }
    
    /// <summary>
    /// Resets the setup flag to allow re-setup for next combat
    /// </summary>
    [Server]
    public void ResetSetupFlag()
    {
        if (!IsServerInitialized) return;
        setupDone = false;
    }
} 