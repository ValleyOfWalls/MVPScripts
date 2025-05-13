using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// Initializes a network entity's starter deck.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle initial deck setup.
/// </summary>
public class EntityDeckSetup : NetworkBehaviour
{
    [Header("Deck Configuration")]
    [SerializeField] private DeckData starterDeckDefinition;
    
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
            Debug.LogError($"EntityDeckSetup on {gameObject.name} requires a NetworkEntityDeck component on the same GameObject.");
        }
        
        if (parentEntity == null)
        {
            Debug.LogError($"EntityDeckSetup on {gameObject.name} requires a NetworkPlayer or NetworkPet component on the same GameObject.");
        }
        
        if (starterDeckDefinition == null)
        {
            Debug.LogWarning($"EntityDeckSetup on {gameObject.name} has no StarterDeckDefinition assigned. Entity will start with an empty deck.");
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
        
        // Log entity type for debugging
        string entityType = "unknown";
        if (parentEntity is NetworkPlayer)
        {
            entityType = "player";
        }
        else if (parentEntity is NetworkPet)
        {
            entityType = "pet";
        }
        
        // Use StarterDeckDefinition to initialize deck
        if (starterDeckDefinition != null && starterDeckDefinition.CardsInDeck != null)
        {
            // Clear the entity deck first
            entityDeck.ClearDeck();
            
            // Add each card from the starter deck definition
            foreach (CardData cardDataSO in starterDeckDefinition.CardsInDeck)
            {
                if (cardDataSO != null)
                {
                    entityDeck.AddCard(cardDataSO.CardId);
                }
            }
            
            Debug.Log($"Initialized starter deck for {gameObject.name} ({entityType}) with {starterDeckDefinition.CardsInDeck.Count} cards from DeckData '{starterDeckDefinition.DeckName}'");
        }
        else
        {
            Debug.LogWarning($"No starter deck definition found for {gameObject.name} ({entityType}). Deck will be empty. Assign a DeckData to the StarterDeckDefinition field in the inspector.");
        }
        
        initialized = true;
    }
} 