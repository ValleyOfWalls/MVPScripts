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
    [SerializeField] private NetworkEntityDeck entityDeck;

    // Reference to the owner entity and its hand
    private NetworkEntity ownerEntity;
    private NetworkEntity handEntity;
    private Transform deckTransform;
    private int deckTransformResolutionAttempts = 0;
    private const int MAX_TRANSFORM_RESOLUTION_ATTEMPTS = 3;

    private readonly SyncVar<bool> _isSetupComplete = new SyncVar<bool>();
    public bool IsSetupComplete => _isSetupComplete.Value;

    private void Awake()
    {
        // Get required components that should be on the main entity
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();

        // Get owner entity reference
        ownerEntity = GetComponent<NetworkEntity>();
        
        // CardSpawner and CardParenter are now on Hand entities - we'll find them when needed
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        // Don't try to get deck transform immediately - Hand entities may not be spawned yet
        // We'll resolve this when SetupCombatDeck is actually called
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

        // Find the hand entity through RelationshipManager
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager != null && relationshipManager.HandEntity != null)
        {
            handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
            if (handEntity != null)
            {
                Debug.Log($"CombatDeckSetup on {gameObject.name}: Found hand entity: {handEntity.EntityName.Value}");
                
                // Get deck transform from the hand entity
                var handEntityUI = handEntity.GetComponent<NetworkEntityUI>();
                if (handEntityUI != null)
                {
                    deckTransform = handEntityUI.GetDeckTransform();
                    if (deckTransform != null)
                    {
                        Debug.Log($"CombatDeckSetup on {gameObject.name}: Using hand entity deck transform. Path: {GetTransformPath(deckTransform)}");
                        return;
                    }
                }
            }
        }

        // If we get here, the hand entity or its components aren't ready yet
        Debug.LogWarning($"CombatDeckSetup on {gameObject.name}: Hand entity not ready yet. RelationshipManager: {relationshipManager != null}, HandEntity: {relationshipManager?.HandEntity != null}");
        handEntity = null;
        deckTransform = null;
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
        if (ownerEntity == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing NetworkEntity component");
        if (entityDeck == null)
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing NetworkEntityDeck component");
        
        // CardSpawner and CardParenter are now on Hand entities, not main entities
        // We'll find them through the Hand entity when needed
        
        // deckTransform will be resolved when SetupCombatDeck is called
    }

    /// <summary>
    /// Sets up the combat deck for this entity
    /// </summary>
    [Server]
    public void SetupCombatDeck()
    {
        if (!IsServerInitialized) return;

        // Try to find the hand entity and deck transform
        if (deckTransform == null || handEntity == null)
        {
            GetDeckTransform();
        }

        // If we still don't have what we need, retry with delay
        if (deckTransform == null || handEntity == null)
        {
            if (deckTransformResolutionAttempts < MAX_TRANSFORM_RESOLUTION_ATTEMPTS)
            {
                deckTransformResolutionAttempts++;
                Debug.LogWarning($"CombatDeckSetup on {gameObject.name}: Hand entity or deck transform not ready, retrying in 0.5s (attempt {deckTransformResolutionAttempts}/{MAX_TRANSFORM_RESOLUTION_ATTEMPTS})");
                
                // Retry after a delay
                StartCoroutine(RetrySetupAfterDelay(0.5f));
                return;
            }
            else
            {
                Debug.LogError($"CombatDeckSetup on {gameObject.name}: Failed to find hand entity or deck transform after {MAX_TRANSFORM_RESOLUTION_ATTEMPTS} attempts.");
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
        if (entityDeck == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Missing required components for deck setup");
            yield break;
        }

        if (deckTransform == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Cannot spawn deck cards - deckTransform is null");
            yield break;
        }

        // Find CardSpawner and CardParenter from the Hand entity
        CardSpawner handCardSpawner = null;
        CardParenter handCardParenter = null;
        
        if (handEntity != null)
        {
            handCardSpawner = handEntity.GetComponent<CardSpawner>();
            handCardParenter = handEntity.GetComponent<CardParenter>();
            
            if (handCardSpawner == null)
                Debug.LogError($"CombatDeckSetup on {gameObject.name}: Hand entity {handEntity.EntityName.Value} is missing CardSpawner component");
            if (handCardParenter == null)
                Debug.LogError($"CombatDeckSetup on {gameObject.name}: Hand entity {handEntity.EntityName.Value} is missing CardParenter component");
        }
        else
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Hand entity is null - this should not happen at this point");
        }

        if (handCardSpawner == null || handCardParenter == null)
        {
            Debug.LogError($"CombatDeckSetup on {gameObject.name}: Cannot find CardSpawner or CardParenter on hand entity");
            yield break;
        }

        // Log detailed information about this entity
        Debug.Log($"=== CombatDeckSetup.SpawnDeckCards for {gameObject.name} ===");
        Debug.Log($"Entity Type: {ownerEntity?.EntityType}");
        Debug.Log($"Entity Name: {ownerEntity?.EntityName.Value}");
        Debug.Log($"Entity IsOwner: {ownerEntity?.IsOwner}");
        Debug.Log($"Entity Owner ClientId: {ownerEntity?.Owner?.ClientId ?? -1}");
        Debug.Log($"Hand Entity: {handEntity?.EntityName.Value}");
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

            // Spawn the card using the Hand entity's CardSpawner
            GameObject cardObject = handCardSpawner.SpawnCard(cardData);
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

            // Set up card ownership and parenting using the Hand entity's CardParenter
            handCardParenter.SetupCard(cardObject, ownerEntity, deckTransform);

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