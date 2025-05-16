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
    [SerializeField] private NetworkEntityDeck entityDeck;

    private Transform handTransform;
    private Transform deckTransform;
    private Transform discardTransform;

    private void Awake()
    {
        // Get required components
        if (entityUI == null) entityUI = GetComponent<NetworkEntityUI>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();

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
        if (entityDeck == null)
            Debug.LogError($"CardParenter on {gameObject.name}: Missing NetworkEntityDeck component");
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

        // Set the card's owner to match the entity's owner
        NetworkConnection ownerConnection = owner.Owner;
        if (ownerConnection != null)
        {
            cardNetObj.GiveOwnership(ownerConnection);
        }

        // Parent to deck transform
        cardObject.transform.SetParent(parentTransform);
        cardObject.transform.localPosition = Vector3.zero;
        cardObject.transform.localRotation = Quaternion.identity;
        cardObject.transform.localScale = Vector3.one;

        // Spawn the card
        ServerManager.Spawn(cardObject);

        // Sync state to all clients
        int cardNetworkId = cardNetObj.ObjectId;
        int parentNetworkId = parentTransform.GetComponentInParent<NetworkObject>()?.ObjectId ?? -1;
        
        Debug.Log($"Server: Setting up card with NetworkID {cardNetworkId}, parent transform NetworkID: {parentNetworkId}");
        
        ObserversSyncState(cardNetworkId, parentNetworkId, cardObject.name, false);
    }

    [ObserversRpc]
    private void ObserversSyncState(int cardNetObjId, int parentNetObjId, string cardName, bool isActive)
    {
        Debug.Log($"CardParenter.ObserversSyncState called on {(IsServerInitialized ? "Server" : "Client")} - Card NOB ID: {cardNetObjId}, Expected Parent Entity NOB ID: {parentNetObjId}, Card Name: {cardName}, SetActive: {isActive}");
        
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

        // Ensure this CardParenter instance matches the intended parent entity
        if (this.NetworkObject.ObjectId != parentNetObjId)
        {
            Debug.LogError($"CardParenter.ObserversSyncState on {gameObject.name} (Entity NOB ID: {this.NetworkObject.ObjectId}): Received parentNetObjId {parentNetObjId} which does not match this entity. Card will not be parented here.");
            // Fallback: Set active state as per RPC, but card will remain at root or its current parent.
            cardObject.SetActive(isActive);
            return;
        }

        if (deckTransform == null)
        {
            Debug.LogError($"CardParenter on {gameObject.name} (Entity NOB ID: {this.NetworkObject.ObjectId}): deckTransform is null on client. Card {cardName} (NOB ID: {cardNetObjId}) cannot be parented to deck.");
            // Fallback: Set active state as per RPC, but card will remain at root or its current parent.
            cardObject.SetActive(isActive);
            return;
        }

        Debug.Log($"CardParenter on {gameObject.name} (Client): Parenting card {cardName} (NOB ID: {cardNetObjId}) to deckTransform: {deckTransform.name} (Path: {GetTransformPath(deckTransform)})");
        cardObject.transform.SetParent(deckTransform, false); // worldPositionStays = false to correctly apply local transforms
        cardObject.transform.localPosition = Vector3.zero;
        cardObject.transform.localRotation = Quaternion.identity;
        cardObject.transform.localScale = Vector3.one;
        
        cardObject.SetActive(isActive); // Server initially sends 'false' for cards in deck
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

        // Set the card's parent to the target transform
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = Vector3.zero;

        // Enable/disable based on location
        bool shouldBeEnabled = targetTransform == handTransform;
        card.SetActive(shouldBeEnabled);
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
} 