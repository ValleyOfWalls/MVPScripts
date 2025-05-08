using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// Initializes a network entity's starter deck.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle initial deck setup.
/// </summary>
public class EntityDeckSetup : NetworkBehaviour
{
    [Header("Starter Deck")]
    [SerializeField] private List<Card> starterDeckPrefabs;
    
    // Reference to the entity's deck
    private NetworkEntityDeck entityDeck;
    private NetworkBehaviour parentEntity;
    
    // Flag to track if initialization has been done
    private bool initialized = false;

    private void Awake()
    {
        // Get references
        entityDeck = GetComponent<NetworkEntityDeck>();
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }
        
        if (entityDeck == null)
        {
            Debug.LogError("EntityDeckSetup requires a NetworkEntityDeck component on the same GameObject.");
        }
        
        if (parentEntity == null)
        {
            Debug.LogError("EntityDeckSetup requires a NetworkPlayer or NetworkPet component on the same GameObject.");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Initialize the starter deck on the server
        InitializeStarterDeck();
    }

    /// <summary>
    /// Initializes the entity's starter deck
    /// </summary>
    [Server]
    private void InitializeStarterDeck()
    {
        if (!IsServerInitialized || initialized || entityDeck == null) return;
        
        // Initialize from StarterDeckDefinition if present, otherwise use starterDeckPrefabs
        DeckData starterDeckDefinition = null;
        
        // Try to get StarterDeckDefinition from parent entity
        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;
        
        if (player != null)
        {
            starterDeckDefinition = player.StarterDeckDefinition;
            player.currentDeckCardIds.Clear();
            player.playerHandCardIds.Clear();
            player.discardPileCardIds.Clear();
        }
        else if (pet != null)
        {
            starterDeckDefinition = pet.StarterDeckDefinition;
            pet.currentDeckCardIds.Clear();
            pet.playerHandCardIds.Clear();
            pet.discardPileCardIds.Clear();
        }
        
        // First try to use StarterDeckDefinition if available
        if (starterDeckDefinition != null && starterDeckDefinition.CardsInDeck != null)
        {
            foreach (CardData cardDataSO in starterDeckDefinition.CardsInDeck)
            {
                if (cardDataSO != null)
                {
                    entityDeck.AddCard(cardDataSO.CardId);
                }
            }
            
            Debug.Log($"Initialized starter deck for {gameObject.name} with {starterDeckDefinition.CardsInDeck.Count} cards from StarterDeckDefinition");
        }
        // Otherwise, use starterDeckPrefabs
        else if (starterDeckPrefabs != null && starterDeckPrefabs.Count > 0)
        {
            // Add each card from the starter deck to the entity's deck
            foreach (Card card in starterDeckPrefabs)
            {
                if (card != null)
                {
                    entityDeck.AddCard(card.CardId);
                }
            }
            
            Debug.Log($"Initialized starter deck for {gameObject.name} with {starterDeckPrefabs.Count} cards from starterDeckPrefabs");
        }
        else
        {
            Debug.LogWarning($"No starter deck definition found for {gameObject.name}. Deck will be empty.");
        }
        
        initialized = true;
    }
} 