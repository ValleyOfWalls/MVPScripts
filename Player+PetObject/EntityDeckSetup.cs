using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// Initializes a network entity's starter deck.
/// Can use either a pre-assigned DeckData or accept runtime DeckData from character selection.
/// Attach to: NetworkEntity prefabs to handle initial deck setup.
/// </summary>
public class EntityDeckSetup : NetworkBehaviour
{
    [Header("Deck Configuration")]
    [SerializeField] private DeckData starterDeckDefinition;
    [SerializeField] private bool allowRuntimeDeckOverride = true;
    
    // Reference to the entity's deck
    private NetworkEntityDeck entityDeck;
    private NetworkBehaviour parentEntity;
    
    // Runtime deck data from character selection
    private DeckData runtimeDeckData;
    
    // Flag to track if initialization has been done
    private bool initialized = false;

    private void Awake()
    {
        // Get references
        entityDeck = GetComponent<NetworkEntityDeck>();
        parentEntity = GetComponent<NetworkEntity>();
        
        if (entityDeck == null)
        {
            Debug.LogError($"EntityDeckSetup on {gameObject.name} requires a NetworkEntityDeck component on the same GameObject.");
        }
        
        if (parentEntity == null)
        {
            Debug.LogError($"EntityDeckSetup on {gameObject.name} requires a NetworkEntity component on the same GameObject.");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Don't initialize immediately - wait for potential runtime deck assignment
        // from character selection system
        if (!allowRuntimeDeckOverride || starterDeckDefinition != null)
        {
            // Initialize with default deck if runtime override is disabled or we have a fallback
            InitializeStarterDeck();
        }
    }

    /// <summary>
    /// Sets the runtime deck data from character selection
    /// Call this BEFORE OnStartServer or immediately after spawning
    /// </summary>
    [Server]
    public void SetRuntimeDeckData(DeckData deckData)
    {
        if (!IsServerInitialized && !IsServerStarted)
        {
            // Called before server start - store for later
            runtimeDeckData = deckData;
            Debug.Log($"EntityDeckSetup on {gameObject.name}: Runtime deck data set to '{deckData?.DeckName}' (will apply on server start)");
            return;
        }
        
        if (initialized)
        {
            Debug.LogWarning($"EntityDeckSetup on {gameObject.name}: Attempting to set runtime deck data after initialization is complete");
            return;
        }
        
        runtimeDeckData = deckData;
        Debug.Log($"EntityDeckSetup on {gameObject.name}: Runtime deck data set to '{deckData?.DeckName}'");
        
        // Initialize immediately since server is already started
        InitializeStarterDeck();
    }

    /// <summary>
    /// Initializes the entity's starter deck using runtime data if available, fallback to serialized data
    /// </summary>
    [Server]
    public void InitializeStarterDeck()
    {
        if (!IsServerInitialized || initialized || entityDeck == null) return;
        
        NetworkEntity entity = parentEntity as NetworkEntity;
        string entityType = entity != null ? entity.EntityType.ToString() : "unknown";
        
        // Determine which deck data to use
        DeckData deckToUse = null;
        string deckSource = "";
        
        if (allowRuntimeDeckOverride && runtimeDeckData != null)
        {
            deckToUse = runtimeDeckData;
            deckSource = "runtime selection";
        }
        else if (starterDeckDefinition != null)
        {
            deckToUse = starterDeckDefinition;
            deckSource = "serialized field";
        }
        
        // Initialize deck if we have data
        if (deckToUse != null && deckToUse.CardsInDeck != null)
        {
            // Clear the entity deck first
            entityDeck.ClearDeck();
            
            // Add each card from the deck definition
            foreach (CardData cardDataSO in deckToUse.CardsInDeck)
            {
                if (cardDataSO != null)
                {
                    entityDeck.AddCard(cardDataSO.CardId);
                }
            }
            
            Debug.Log($"Initialized starter deck for {gameObject.name} ({entityType}) with {deckToUse.CardsInDeck.Count} cards from {deckSource} deck '{deckToUse.DeckName}'");
        }
        else
        {
            Debug.LogWarning($"No deck data available for {gameObject.name} ({entityType}). " +
                           $"Runtime deck: {(runtimeDeckData != null ? runtimeDeckData.DeckName : "null")}, " +
                           $"Serialized deck: {(starterDeckDefinition != null ? starterDeckDefinition.DeckName : "null")}. " +
                           $"Deck will be empty.");
        }
        
        initialized = true;
    }
    
    /// <summary>
    /// Force initialization if it hasn't happened yet (useful for late setup)
    /// </summary>
    [Server]
    public void ForceInitialization()
    {
        if (!initialized)
        {
            InitializeStarterDeck();
        }
    }
    
    /// <summary>
    /// Check if this entity has been initialized with a deck
    /// </summary>
    public bool IsInitialized => initialized;
    
    /// <summary>
    /// Get the deck data being used (runtime or serialized)
    /// </summary>
    public DeckData GetActiveDeckData()
    {
        if (allowRuntimeDeckOverride && runtimeDeckData != null)
            return runtimeDeckData;
        return starterDeckDefinition;
    }
} 