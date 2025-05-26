using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Collections;
using FishNet.Object.Synchronizing;

/// <summary>
/// Prepares a combat deck from entity's collection
/// Attach to: NetworkEntity prefabs to handle their combat deck setup
/// </summary>
public class CombatDeckSetup : NetworkBehaviour
{
    [Header("Required Components")]
    [SerializeField] private CardSpawner cardSpawner;
    [SerializeField] private CardParenter cardParenter;
    [SerializeField] private NetworkEntityDeck entityDeck;
    [SerializeField] private HandManager handManager;

    // Reference to the owner entity
    private NetworkEntity ownerEntity;
    private Transform deckTransform;
    private int deckTransformResolutionAttempts = 0;
    private const int MAX_TRANSFORM_RESOLUTION_ATTEMPTS = 3;

    private readonly SyncVar<bool> _isSetupComplete = new SyncVar<bool>();
    public bool IsSetupComplete => _isSetupComplete.Value;

    private void Awake()
    {
        // Get required components
        if (cardSpawner == null) cardSpawner = GetComponent<CardSpawner>();
        if (cardParenter == null) cardParenter = GetComponent<CardParenter>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();
        if (handManager == null) handManager = GetComponent<HandManager>();

        // Get owner entity reference
        ownerEntity = GetComponent<NetworkEntity>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        // Get deck transform on both server and client when network is ready
        GetDeckTransform();
        ValidateComponents();
    }

    /// <summary>
    /// Gets the appropriate deck transform based on entity type
    /// </summary>
    private void GetDeckTransform()
    {
        if (ownerEntity == null)
        {
            ownerEntity = GetComponent<NetworkEntity>();
            if (ownerEntity == null)
            {
                Debug.LogError($"CombatDeckSetup on {gameObject.name}: Cannot find NetworkEntity component");
                return;
            }
        }

        // Get the appropriate UI component based on entity type
        if (ownerEntity.EntityType == EntityType.Player)
        {
            var playerUI = GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                deckTransform = playerUI.GetDeckTransform();
                Debug.Log($"CombatDeckSetup on {gameObject.name}: Using NetworkPlayerUI deck transform. Path: {GetTransformPath(deckTransform)}");
            }
        }
        else if (ownerEntity.EntityType == EntityType.Pet)
        {
            var petUI = GetComponent<NetworkPetUI>();
            if (petUI != null)
            {
                deckTransform = petUI.GetDeckTransform();
                Debug.Log($"CombatDeckSetup on {gameObject.name}: Using NetworkPetUI deck transform. Path: {GetTransformPath(deckTransform)}");
            }
        }

        // Fall back to NetworkEntityUI if specific UI not found
        if (deckTransform == null)
        {
            var entityUI = GetComponent<NetworkEntityUI>();
            if (entityUI != null)
            {
                deckTransform = entityUI.GetDeckTransform();
                Debug.Log($"CombatDeckSetup on {gameObject.name}: Using NetworkEntityUI deck transform. Path: {GetTransformPath(deckTransform)}");
            }
        }

        // Last resort - try to find by name
        if (deckTransform == null)
        {
            // Look for transforms with appropriate names
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.Contains("Deck"))
                {
                    deckTransform = t;
                    Debug.Log($"CombatDeckSetup on {gameObject.name}: Found deck transform by name search: {t.name}");
                    break;
                }
            }
        }

        if (deckTransform == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Could not find a deck transform in any UI component. Creating fallback.");
            
            // Create a fallback transform
            GameObject fallbackObj = new GameObject("FallbackDeckTransform");
            fallbackObj.transform.SetParent(transform);
            fallbackObj.transform.localPosition = Vector3.zero;
            deckTransform = fallbackObj.transform;
        }
    }

    private string GetTransformPath(Transform transform)
    {
        if (transform == null) return "null";
        
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }

    private void ValidateComponents()
    {
        if (cardSpawner == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing CardSpawner component");
        if (cardParenter == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing CardParenter component");
        if (entityDeck == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing NetworkEntityDeck component");
        if (handManager == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing HandManager component");
        if (ownerEntity == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing NetworkEntity component");
        if (deckTransform == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing deck transform reference");
    }

    /// <summary>
    /// Sets up the combat deck for this entity
    /// </summary>
    [Server]
    public void SetupCombatDeck()
    {
        if (!IsServerInitialized) return;

        // Ensure we have a valid deck transform
        if (deckTransform == null)
        {
            if (deckTransformResolutionAttempts < MAX_TRANSFORM_RESOLUTION_ATTEMPTS)
            {
                deckTransformResolutionAttempts++;
                GetDeckTransform();
                
                if (deckTransform == null)
                {
                    Debug.LogError($"CombatDeckSetup on {gameObject.name}: Cannot setup combat deck - deck transform still null after {deckTransformResolutionAttempts} attempts");
                    
                    // Retry after a delay
                    StartCoroutine(RetrySetupAfterDelay(0.5f));
                    return;
                }
            }
            else
            {
                Debug.LogError($"CombatDeckSetup on {gameObject.name}: Failed to resolve deck transform after {MAX_TRANSFORM_RESOLUTION_ATTEMPTS} attempts.");
                return;
            }
        }

        StartCoroutine(SpawnDeckCards());
    }
    
    private IEnumerator RetrySetupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetupCombatDeck();
    }

    private IEnumerator SpawnDeckCards()
    {
        if (entityDeck == null || cardSpawner == null || cardParenter == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing required components for deck setup");
            yield break;
        }

        if (deckTransform == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Cannot spawn deck cards - deckTransform is null");
            yield break;
        }

        // Log detailed information about this entity
        Debug.Log($"=== CombatDeckSetup.SpawnDeckCards for {gameObject.name} ===");
        Debug.Log($"Entity Type: {ownerEntity?.EntityType}");
        Debug.Log($"Entity Name: {ownerEntity?.EntityName.Value}");
        Debug.Log($"Entity IsOwner: {ownerEntity?.IsOwner}");
        Debug.Log($"Entity Owner ClientId: {ownerEntity?.Owner?.ClientId ?? -1}");
        Debug.Log($"=== Starting card spawn process ===");

        // Get all card IDs from the entity's deck
        List<int> cardIds = entityDeck.GetAllCardIds();
        if (cardIds == null || cardIds.Count == 0)
        {
            Debug.LogWarning($"Entity {gameObject.name} has no cards in NetworkEntityDeck");
            // Mark setup as complete even if there are no cards, so the process doesn't hang
            if (IsServerInitialized)
            {
                _isSetupComplete.Value = true;
            }
            yield break;
        }

        Debug.Log($"Spawning {cardIds.Count} cards for {gameObject.name}");

        // Spawn each card
        foreach (int cardId in cardIds)
        {
            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null)
            {
                Debug.LogWarning($"Card data not found for ID {cardId}");
                continue;
            }

            Debug.Log($"About to spawn card {cardData.CardName} for entity {ownerEntity?.EntityName.Value} (ClientId: {ownerEntity?.Owner?.ClientId ?? -1})");

            // Spawn the card
            GameObject cardObject = cardSpawner.SpawnCard(cardData);
            if (cardObject == null)
            {
                Debug.LogError($"Failed to spawn card {cardData.CardName} for {gameObject.name}");
                continue;
            }

            // Log the card's ownership after spawning
            NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
            Card card = cardObject.GetComponent<Card>();
            Debug.Log($"Card {cardData.CardName} spawned - NetworkObject Owner ClientId: {cardNetObj?.Owner?.ClientId ?? -1}");
            Debug.Log($"Card {cardData.CardName} - Card.OwnerEntity: {(card?.OwnerEntity != null ? card.OwnerEntity.EntityName.Value : "null")}");
            Debug.Log($"Card {cardData.CardName} - Card.OwnerEntity ClientId: {(card?.OwnerEntity?.Owner?.ClientId ?? -1)}");

            // Log the parent transform before setup
            Debug.Log($"Setting up card {cardObject.name} with parent transform {deckTransform.name} (exists: {deckTransform != null}, path: {GetTransformPath(deckTransform)})");

            // Set up card ownership and parenting
            cardParenter.SetupCard(cardObject, ownerEntity, deckTransform);

            // Initialize the card's container state
            if (card != null)
            {
                card.SetCurrentContainer(CardLocation.Deck);
            }

            // Small delay between spawning cards to prevent network congestion
            yield return new WaitForSeconds(0.05f);
        }

        Debug.Log($"Finished setting up combat deck for {gameObject.name} with {cardIds.Count} cards");
        
        // Mark setup as complete
        if (IsServerInitialized)
        {
            _isSetupComplete.Value = true;
        }
    }
} 