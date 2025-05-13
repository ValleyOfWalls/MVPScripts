using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

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
    /// Sets up the combat deck for this entity
    /// </summary>
    [Server]
    public void SetupCombatDeck(List<int> cardIds)
    {
        if (!IsServerInitialized) return;
        
        // First, reset the deck to prepare for setup
        CombatDeck combatDeck = GetComponent<CombatDeck>();
        if (combatDeck == null)
        {
            Debug.LogError("CombatDeckSetup: CombatDeck component not found");
            return;
        }
        
        combatDeck.ResetSetupFlag();
        
        Debug.Log($"Setting up combat deck for {gameObject.name} with {cardIds.Count} cards");
        
        // Add the cards to the combat deck all at once
        combatDeck.SetupDeck(cardIds);
    }
    
    /// <summary>
    /// Adds cards to the combat deck for this entity
    /// </summary>
    [Server]
    public void AddCardToDeck(int cardId)
    {
        if (!IsServerInitialized) return;
        
        CombatDeck combatDeck = GetComponent<CombatDeck>();
        if (combatDeck == null)
        {
            Debug.LogError("CombatDeckSetup: CombatDeck component not found");
            return;
        }
        
        combatDeck.AddCardById(cardId);
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