using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles parenting of card GameObjects to appropriate transforms during gameplay.
/// Attach to: NetworkEntity prefabs to handle their card parenting operations.
/// </summary>
public class CardParenter : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntityUI entityUI;

    private Transform handTransform;
    private Transform deckTransform;
    private Transform discardTransform;

    private void Awake()
    {
        // Get required components
        if (entityUI == null) entityUI = GetComponent<NetworkEntityUI>();

        // Get transforms from UI
        if (entityUI != null)
        {
            handTransform = entityUI.GetHandTransform();
            deckTransform = entityUI.GetDeckTransform();
            discardTransform = entityUI.GetDiscardTransform();
        }

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (entityUI == null)
            Debug.LogError($"CardParenter on {gameObject.name}: Missing NetworkEntityUI component");
        if (handTransform == null)
            Debug.LogError($"CardParenter on {gameObject.name}: Missing hand transform reference");
        if (deckTransform == null)
            Debug.LogError($"CardParenter on {gameObject.name}: Missing deck transform reference");
        if (discardTransform == null)
            Debug.LogError($"CardParenter on {gameObject.name}: Missing discard transform reference");
    }

    /// <summary>
    /// Sets up a card's ownership and parents it to the correct transform
    /// </summary>
    [Server]
    public void SetupCard(GameObject cardObject, NetworkBehaviour owner, Transform parentTransform)
    {
        if (!IsServerInitialized || cardObject == null || owner == null || parentTransform == null)
        {
            Debug.LogError("CardParenter: Cannot setup card - missing required parameters");
            return;
        }

        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError("CardParenter: Card is missing NetworkObject component");
            return;
        }

        // Get the Card component to access CardData
        Card card = cardObject.GetComponent<Card>();
        if (card == null)
        {
            Debug.LogError("CardParenter: Card is missing Card component");
            return;
        }

        // Initially disable the card GameObject before spawning
        cardObject.SetActive(false);

        // Rename the card based on its CardData
        if (card.CardData != null)
        {
            cardObject.name = card.CardData.CardName;
        }

        // NOTE: Removed GiveOwnership call - the card should already have correct ownership from CardSpawner
        // The CardSpawner.SpawnCardInternal method already spawns with the correct owner
        /* Debug.Log($"CardParenter: Card {cardObject.name} already has ownership - Owner ClientId: {cardNetObj.Owner?.ClientId ?? -1}"); */

        // Parent to transform using centralized positioning system
        // Note: SetupCard is typically used for deck setup, so we use the non-hand positioning method
        HandLayoutManager.OnCardMovedToNonHandLocation(cardObject, parentTransform);

        // NOTE: Removed ServerManager.Spawn call - the card should already be spawned by CardSpawner
        // The CardSpawner.SpawnCardInternal method already handles network spawning
        /* Debug.Log($"CardParenter: Card {cardObject.name} already spawned - IsSpawned: {cardNetObj.IsSpawned}"); */

        // Sync state to all clients
        int cardNetworkId = cardNetObj.ObjectId;
        int parentNetworkId = parentTransform.GetComponentInParent<NetworkObject>()?.ObjectId ?? -1;
        
        /* Debug.Log($"Server: Setting up card with NetworkID {cardNetworkId}, parent transform NetworkID: {parentNetworkId}"); */
        
        ObserversSyncState(cardNetworkId, parentNetworkId, cardObject.name, false);
    }

    [ObserversRpc]
    private void ObserversSyncState(int cardNetObjId, int parentNetObjId, string cardName, bool isActive)
    {
        /* Debug.Log($"CardParenter.ObserversSyncState called on {(IsServerInitialized ? "Server" : "Client")} - Card NOB ID: {cardNetObjId}, Expected Parent Entity NOB ID: {parentNetObjId}, Card Name: {cardName}, SetActive: {isActive}"); */
        /* Debug.Log($"CardParenter.ObserversSyncState - This CardParenter is on entity {gameObject.name} with NOB ID: {this.NetworkObject.ObjectId}"); */
        
        NetworkObject cardNetObj = null;
        bool foundCard = false;
        
        if (NetworkManager.IsClientStarted)
        {
            foundCard = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        else if (NetworkManager.IsServerStarted) // Should not happen for client-side logic, but good for completeness
        {
            foundCard = NetworkManager.ServerManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        
        if (!foundCard || cardNetObj == null)
        {
            Debug.LogError($"CardParenter: Failed to find card NetworkObject with ID {cardNetObjId} on {(IsServerInitialized ? "Server" : "Client")}.");
            return;
        }

        GameObject cardObject = cardNetObj.gameObject;
        cardObject.name = cardName;

        // FIRST VALIDATION: Ensure this CardParenter instance matches the intended parent entity
        /* Debug.Log($"CardParenter.ObserversSyncState - VALIDATION 1: this.NetworkObject.ObjectId ({this.NetworkObject.ObjectId}) vs parentNetObjId ({parentNetObjId})"); */
        if (this.NetworkObject.ObjectId != parentNetObjId)
        {
            /* Debug.Log($"CardParenter.ObserversSyncState on {gameObject.name} (Entity NOB ID: {this.NetworkObject.ObjectId}): Received parentNetObjId {parentNetObjId} which does not match this entity. Card {cardName} belongs to a different entity - ignoring."); */
            // Don't parent cards that belong to other entities
            return;
        }

        // SECOND VALIDATION: Check if the card actually belongs to this entity
        Card card = cardObject.GetComponent<Card>();
        /* Debug.Log($"CardParenter.ObserversSyncState - VALIDATION 2: Card component found: {card != null}"); */
        if (card != null && card.OwnerEntity != null)
        {
            NetworkObject cardOwnerNetObj = card.OwnerEntity.GetComponent<NetworkObject>();
            /* Debug.Log($"CardParenter.ObserversSyncState - VALIDATION 2: Card.OwnerEntity = {card.OwnerEntity.EntityName.Value}, cardOwnerNetObj.ObjectId = {cardOwnerNetObj?.ObjectId ?? -1}, this.NetworkObject.ObjectId = {this.NetworkObject.ObjectId}"); */
            if (cardOwnerNetObj != null && cardOwnerNetObj.ObjectId != this.NetworkObject.ObjectId)
            {
                /* Debug.Log($"CardParenter.ObserversSyncState on {gameObject.name}: Card {cardName} belongs to entity {card.OwnerEntity.EntityName.Value} (NOB ID: {cardOwnerNetObj.ObjectId}), not this entity (NOB ID: {this.NetworkObject.ObjectId}). Ignoring."); */
                return;
            }
        }

        if (deckTransform == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name} (Entity NOB ID: {this.NetworkObject.ObjectId}): deckTransform is null on client. Card {cardName} (NOB ID: {cardNetObjId}) cannot be parented to deck.");
            // Fallback: Use EntityVisibilityManager for visibility filtering even if we can't parent properly
            EntityVisibilityManager fallbackEntityVisManager = FindEntityVisibilityManager();
            if (fallbackEntityVisManager != null)
            {
                fallbackEntityVisManager.ApplyCardVisibilityFilter(cardObject, isActive);
            }
            else
            {
                cardObject.SetActive(isActive);
                Debug.LogWarning($"CardParenter: No EntityVisibilityManager found, using fallback for card {cardName}");
            }
            return;
        }

        /* Debug.Log($"CardParenter on {gameObject.name} (Client): VALIDATION PASSED - Parenting card {cardName} (NOB ID: {cardNetObjId}) to deckTransform: {deckTransform.name} (Path: {GetTransformPath(deckTransform)})"); */
        
        // Use centralized positioning system for deck placement
        HandLayoutManager.OnCardMovedToNonHandLocation(cardObject, deckTransform);
        
        // Use EntityVisibilityManager for proper visibility filtering
        EntityVisibilityManager entityVisManager = FindEntityVisibilityManager();
        if (entityVisManager != null)
        {
            entityVisManager.ApplyCardVisibilityFilter(cardObject, isActive);
        }
        else
        {
            // Fallback: set active state directly if no EntityVisibilityManager found
            cardObject.SetActive(isActive);
            Debug.LogWarning($"CardParenter: No EntityVisibilityManager found, using fallback for card {cardName}");
        }
    }

    private string GetTransformPath(Transform currentTransform)
    {
        if (currentTransform == null) return "null";
        string path = currentTransform.name;
        while (currentTransform.parent != null)
        {
            currentTransform = currentTransform.parent;
            path = currentTransform.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// Moves a card to the specified location
    /// </summary>
    public void MoveCard(GameObject card, CardLocation location)
    {
        if (!IsServerInitialized)
        {
            Debug.LogWarning($"CardParenter on {gameObject.name}: Attempted to move card on non-server instance");
            return;
        }

        if (card == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name}: Attempted to move null card");
            return;
        }

        // Get target transform based on location
        Transform targetTransform = location switch
        {
            CardLocation.Hand => handTransform,
            CardLocation.Deck => deckTransform,
            CardLocation.Discard => discardTransform,
            _ => null
        };

        if (targetTransform == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name}: Invalid target transform for location {location}");
            return;
        }

        // Move the card on all clients
        RpcMoveCard(card, targetTransform);
    }

    /// <summary>
    /// Moves multiple cards to the specified location
    /// </summary>
    public void MoveCards(List<GameObject> cards, CardLocation location)
    {
        if (!IsServerInitialized)
        {
            Debug.LogWarning($"CardParenter on {gameObject.name}: Attempted to move cards on non-server instance");
            return;
        }

        if (cards == null || cards.Count == 0)
        {
            Debug.LogError($"CardParenter on {gameObject.name}: Attempted to move null or empty card list");
            return;
        }

        // Get target transform based on location
        Transform targetTransform = location switch
        {
            CardLocation.Hand => handTransform,
            CardLocation.Deck => deckTransform,
            CardLocation.Discard => discardTransform,
            _ => null
        };

        if (targetTransform == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name}: Invalid target transform for location {location}");
            return;
        }

        // Move each card on all clients
        foreach (GameObject card in cards)
        {
            if (card != null)
            {
                RpcMoveCard(card, targetTransform);
            }
        }
    }

    [ObserversRpc]
    private void RpcMoveCard(GameObject card, Transform targetTransform)
    {
        if (card == null || targetTransform == null) return;

        // Use centralized positioning system
        if (targetTransform == handTransform)
        {
            // Moving to hand - use HandLayoutManager (required)
            HandLayoutManager handLayoutManager = HandLayoutManager.GetHandLayoutManager(targetTransform);
            if (handLayoutManager != null)
            {
                handLayoutManager.OnCardAddedToHand(card);
            }
            else
            {
                Debug.LogError($"CardParenter: No HandLayoutManager found on hand transform {targetTransform.name}. Card {card.name} will not be positioned correctly.");
            }
        }
        else
        {
            // Moving to non-hand location (deck/discard) - use centralized method
            HandLayoutManager.OnCardMovedToNonHandLocation(card, targetTransform);
        }

        // Determine if this location should show cards (only hand shows cards)
        bool locationShouldBeVisible = targetTransform == handTransform;
        
        // Use EntityVisibilityManager for proper visibility filtering
        EntityVisibilityManager entityVisManager = FindEntityVisibilityManager();
        if (entityVisManager != null)
        {
            entityVisManager.ApplyCardVisibilityFilter(card, locationShouldBeVisible);
        }
        else
        {
            // Fallback: set active state directly if no EntityVisibilityManager found
            card.SetActive(locationShouldBeVisible);
            Debug.LogWarning($"CardParenter: No EntityVisibilityManager found, using fallback for card {card.name}");
        }
    }

    /// <summary>
    /// Gets all cards at the specified location
    /// </summary>
    public List<GameObject> GetCardsAtLocation(CardLocation location)
    {
        Transform sourceTransform = location switch
        {
            CardLocation.Hand => handTransform,
            CardLocation.Deck => deckTransform,
            CardLocation.Discard => discardTransform,
            _ => null
        };

        if (sourceTransform == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name}: Invalid source transform for location {location}");
            return new List<GameObject>();
        }

        return Enumerable.Range(0, sourceTransform.childCount)
            .Select(i => sourceTransform.GetChild(i).gameObject)
            .ToList();
    }

    /// <summary>
    /// Finds the EntityVisibilityManager instance
    /// </summary>
    private EntityVisibilityManager FindEntityVisibilityManager()
    {
        // Try to find via GamePhaseManager first
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null) return entityVisManager;
        }
        
        // Fallback to direct search
        return FindFirstObjectByType<EntityVisibilityManager>();
    }
} 