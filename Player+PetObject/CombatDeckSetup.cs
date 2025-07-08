using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
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
        
        ValidateComponents();
        
        // Subscribe to hand entity changes to know when we can proceed with setup
        if (ownerEntity != null)
        {
            var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
            if (relationshipManager != null)
            {
                relationshipManager.handEntity.OnChange += OnHandEntityChanged;
            }
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        // Unsubscribe from events
        if (ownerEntity != null)
        {
            var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
            if (relationshipManager != null)
            {
                relationshipManager.handEntity.OnChange -= OnHandEntityChanged;
            }
        }
    }

    /// <summary>
    /// Called when the hand entity changes - triggers setup retry if needed
    /// </summary>
    private void OnHandEntityChanged(NetworkBehaviour prev, NetworkBehaviour next, bool asServer)
    {
        if (!asServer || !IsServerInitialized) return;
        
        // If we were waiting for a hand entity and now have one, retry setup
        if (prev == null && next != null && deckTransform == null)
        {
            Debug.Log($"CombatDeckSetup on {gameObject.name}: Hand entity became available, retrying setup");
            // Clear the attempt counter since we have a valid trigger
            deckTransformResolutionAttempts = 0;
            SetupCombatDeck();
        }
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
                /* Debug.Log($"CombatDeckSetup on {gameObject.name}: Found hand entity: {handEntity.EntityName.Value}"); */
                
                // Get deck transform from the hand entity
                var handEntityUI = handEntity.GetComponent<NetworkEntityUI>();
                if (handEntityUI != null)
                {
                    deckTransform = handEntityUI.GetDeckTransform();
                    if (deckTransform != null)
                    {
                        /* Debug.Log($"CombatDeckSetup on {gameObject.name}: Using hand entity deck transform. Path: {GetTransformPath(deckTransform)}"); */
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

        // If we still don't have what we need, start monitoring instead of using delays
        if (deckTransform == null || handEntity == null)
        {
            if (deckTransformResolutionAttempts < MAX_TRANSFORM_RESOLUTION_ATTEMPTS)
            {
                deckTransformResolutionAttempts++;
                Debug.LogWarning($"CombatDeckSetup on {gameObject.name}: Hand entity or deck transform not ready, monitoring for availability (attempt {deckTransformResolutionAttempts}/{MAX_TRANSFORM_RESOLUTION_ATTEMPTS})");
                
                // Monitor for hand entity availability instead of time-based retry
                StartCoroutine(MonitorForHandEntityAvailability());
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
    
    /// <summary>
    /// Monitors for hand entity availability using network-aware checking
    /// EVENT-DRIVEN: Responds to network spawn events instead of fixed polling
    /// </summary>
    private IEnumerator MonitorForHandEntityAvailability()
    {
        float startTime = Time.time;
        const float maxWaitTime = 3f; // Maximum wait time as safety fallback
        int checkCount = 0;
        
        // Start with more frequent checks, then back off
        float checkInterval = 0.016f; // Start checking every frame (60fps)
        const float maxCheckInterval = 0.1f; // Max 10 checks per second
        const float intervalIncrease = 1.02f; // Gradually slow down checking

        while (Time.time - startTime < maxWaitTime)
        {
            yield return new WaitForSeconds(checkInterval);
            checkCount++;

            // Try to get the hand entity and deck transform
            GetDeckTransform();

            // If we now have what we need, proceed with setup
            if (deckTransform != null && handEntity != null)
            {
                float actualWaitTime = Time.time - startTime;
                Debug.Log($"CombatDeckSetup on {gameObject.name}: Hand entity became available after {actualWaitTime:F2}s ({checkCount} checks), proceeding with setup");
                StartCoroutine(SpawnDeckCards());
                yield break;
            }
            
            // If no luck after several attempts, check if network is still spawning objects
            if (checkCount > 5 && NetworkManager != null)
            {
                // Check if there are any objects still spawning
                int activeSpawning = CountActiveSpawningObjects();
                if (activeSpawning == 0)
                {
                    // No objects spawning, likely won't find what we need
                    Debug.LogWarning($"CombatDeckSetup on {gameObject.name}: No network objects spawning, hand entity may not be available");
                }
            }

            // Gradually slow down checking to avoid excessive polling
            checkInterval = Mathf.Min(checkInterval * intervalIncrease, maxCheckInterval);
        }

        // Fallback if hand entity never becomes available
        float totalWaitTime = Time.time - startTime;
        Debug.LogError($"CombatDeckSetup on {gameObject.name}: Hand entity not available after {totalWaitTime:F1}s ({checkCount} checks)");
    }
    
    /// <summary>
    /// Counts network objects that are currently in the process of spawning
    /// Used to determine if we should keep waiting for entities
    /// </summary>
    private int CountActiveSpawningObjects()
    {
        if (NetworkManager?.ClientManager?.Objects == null) return 0;
        
        int spawningCount = 0;
        foreach (var kvp in NetworkManager.ClientManager.Objects.Spawned)
        {
            var netObj = kvp.Value;
            if (netObj != null && !netObj.IsSpawned)
            {
                spawningCount++;
            }
        }
        
        return spawningCount;
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
        // Check if randomization was enabled at game start
        bool randomizationEnabled = false;
        if (OfflineGameManager.Instance != null)
        {
            randomizationEnabled = OfflineGameManager.Instance.EnableRandomizedCards;
        }
        Debug.Log($"[CARD-FLOW] CombatDeckSetup for {gameObject.name} - Randomization setting: {randomizationEnabled}");

        // Get all card IDs from the entity's deck
        List<int> cardIds = entityDeck.GetAllCardIds();
        if (cardIds == null || cardIds.Count == 0)
        {
            Debug.LogWarning($"[CARD-FLOW] Entity {gameObject.name} has no cards in NetworkEntityDeck");
            // Mark setup as complete even if there are no cards, so the process doesn't hang
            if (IsServerInitialized)
            {
                _isSetupComplete.Value = true;
            }
            yield break;
        }

        // CRITICAL DIAGNOSTIC: Show first few card IDs to understand the deck content
        Debug.Log($"[CARD-FLOW] {gameObject.name} deck contains {cardIds.Count} cards. First 5 IDs: [{string.Join(", ", cardIds.Take(5))}]");
        
        // Check if these are original card IDs (1-100) or randomized IDs (large numbers)
        bool hasOriginalIds = cardIds.Any(id => id >= 1 && id <= 100);
        bool hasRandomizedIds = cardIds.Any(id => id > 1000000);
        Debug.Log($"[CARD-FLOW] Deck analysis - Original IDs (1-100): {hasOriginalIds}, Randomized IDs (>1M): {hasRandomizedIds}");

        // SHUFFLE THE DECK: Randomize card order for combat
        System.Random rng = new System.Random();
        int n = cardIds.Count;
        /* Debug.Log($"CombatDeckSetup: Shuffling {n} cards for {gameObject.name}"); */
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            int temp = cardIds[k];
            cardIds[k] = cardIds[n];
            cardIds[n] = temp;
        }
        /* Debug.Log($"CombatDeckSetup: Finished shuffling deck for {gameObject.name}"); */

        /* Debug.Log($"Spawning {cardIds.Count} cards for {gameObject.name}"); */

        // Spawn each card
        foreach (int cardId in cardIds)
        {
            // Check randomization status first
            bool hasRandomization = NetworkCardDatabase.Instance != null && NetworkCardDatabase.Instance.AreCardsSynced;
            Debug.Log($"[CARD-FLOW] CombatDeckSetup: Processing card ID {cardId} - NetworkDB Available: {NetworkCardDatabase.Instance != null}, Cards Synced: {hasRandomization}");
            
            // Try NetworkCardDatabase first, fallback to CardDatabase
            CardData cardData = null;
            if (hasRandomization)
            {
                cardData = NetworkCardDatabase.Instance.GetSyncedCard(cardId);
                if (cardData != null)
                {
                    Debug.Log($"[CARD-FLOW] CombatDeckSetup: Using RANDOMIZED card {cardData.CardName} (ID: {cardId})");
                }
            }
            
            if (cardData == null && CardDatabase.Instance != null)
            {
                cardData = CardDatabase.Instance.GetCardById(cardId);
                if (cardData != null)
                {
                    Debug.Log($"[CARD-FLOW] CombatDeckSetup: Using ORIGINAL card {cardData.CardName} (ID: {cardId})");
                }
            }
            
            if (cardData == null)
            {
                Debug.LogWarning($"[CARD-FLOW] CombatDeckSetup: CRITICAL - No card data found for ID {cardId} in any database!");
                continue;
            }

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
            /* Debug.Log($"Card {cardData.CardName} spawned - NetworkObject Owner ClientId: {cardNetObj?.Owner?.ClientId ?? -1}"); */
            /* Debug.Log($"Card {cardData.CardName} - Card.OwnerEntity: {(card?.OwnerEntity != null ? card.OwnerEntity.EntityName.Value : "null")}"); */
            /* Debug.Log($"Card {cardData.CardName} - Card.OwnerEntity ClientId: {(card?.OwnerEntity?.Owner?.ClientId ?? -1)}"); */

            // Log the parent transform before setup
            /* Debug.Log($"Setting up card {cardObject.name} with parent transform {deckTransform.name} (exists: {deckTransform != null}, path: {GetTransformPath(deckTransform)})"); */

            // Set up card ownership and parenting using the Hand entity's CardParenter
            handCardParenter.SetupCard(cardObject, ownerEntity, deckTransform);

            // Initialize the card's container state
            if (card != null)
            {
                card.SetCurrentContainer(CardLocation.Deck);
            }

            // Yield frame to prevent blocking - more responsive than time delay
            yield return null;
        }

        /* Debug.Log($"Finished setting up combat deck for {gameObject.name} with {cardIds.Count} cards"); */
        
        // Mark setup as complete
        if (IsServerInitialized)
        {
            _isSetupComplete.Value = true;
        }
    }
} 