using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Represents a networked draft pack containing cards for drafting.
/// Attach to: DraftPack prefabs that will be spawned during the draft phase.
/// </summary>
public class DraftPack : NetworkBehaviour
{
    [Header("Pack Configuration")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private int packSize = 4;
    
    [Header("Debug Info")]
    [SerializeField] private List<GameObject> inspectorCards = new List<GameObject>();
    
    // Networked list of card object IDs in this pack
    private readonly SyncList<int> cardObjectIds = new SyncList<int>();
    
    // Track which player currently owns this pack
    public readonly SyncVar<int> CurrentOwnerPlayerId = new SyncVar<int>();
    
    // Track the original owner (for pack circulation)
    public readonly SyncVar<int> OriginalOwnerPlayerId = new SyncVar<int>();
    
    // Local cache of card GameObjects
    private List<GameObject> localCards = new List<GameObject>();
    
    // Events
    public event System.Action<DraftPack> OnPackEmpty;
    public event System.Action<DraftPack, NetworkEntity> OnOwnerChanged;
    
    private CardSpawner cardSpawner;
    private GameManager gameManager;
    
    public int CardCount => cardObjectIds.Count;
    public bool IsEmpty => cardObjectIds.Count == 0;
    public Transform CardContainer => cardContainer;
    
    private void Awake()
    {
        if (cardContainer == null)
        {
            // Create a default container if none is assigned
            GameObject containerObj = new GameObject("CardContainer");
            containerObj.transform.SetParent(transform);
            containerObj.transform.localPosition = Vector3.zero;
            cardContainer = containerObj.transform;
        }
        
        cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner == null)
        {
            Debug.LogError($"DraftPack {gameObject.name}: Missing CardSpawner component!");
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        gameManager = GameManager.Instance;
        cardObjectIds.OnChange += OnCardListChanged;
        CurrentOwnerPlayerId.OnChange += OnCurrentOwnerChanged;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        cardObjectIds.OnChange += OnCardListChanged;
        CurrentOwnerPlayerId.OnChange += OnCurrentOwnerChanged;
        
        // Update local card list when we start
        UpdateLocalCardList();
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        cardObjectIds.OnChange -= OnCardListChanged;
        CurrentOwnerPlayerId.OnChange -= OnCurrentOwnerChanged;
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        cardObjectIds.OnChange -= OnCardListChanged;
        CurrentOwnerPlayerId.OnChange -= OnCurrentOwnerChanged;
    }
    
    /// <summary>
    /// Initializes the pack with random cards from the database
    /// </summary>
    [Server]
    public void InitializePack(int size, NetworkEntity originalOwner)
    {
        if (!IsServerInitialized || cardSpawner == null)
        {
            Debug.LogError("DraftPack: Cannot initialize pack - server not initialized or missing CardSpawner");
            return;
        }
        
        packSize = size;
        OriginalOwnerPlayerId.Value = originalOwner.ObjectId;
        CurrentOwnerPlayerId.Value = originalOwner.ObjectId;
        
        Debug.Log($"DraftPack: Initializing pack with {size} cards for player {originalOwner.EntityName.Value}");
        
        // Get random cards from the database (allowing duplicates for draft)
        List<CardData> randomCards = CardDatabase.Instance.GetRandomCardsWithDuplicates(size);
        
        if (randomCards.Count < size)
        {
            Debug.LogWarning($"DraftPack: Only {randomCards.Count} cards generated, requested {size}. Check if CardDatabase has any cards.");
        }
        
        // Spawn cards and add them to the pack
        foreach (CardData cardData in randomCards)
        {
            GameObject cardObject = cardSpawner.SpawnUnownedCard(cardData);
            if (cardObject != null)
            {
                // Get the NetworkObject for the card
                NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
                if (cardNetObj == null)
                {
                    Debug.LogError($"DraftPack: Card {cardData.CardName} is missing NetworkObject component");
                    continue;
                }
                
                // Set the card's parent to our container on server
                cardObject.transform.SetParent(cardContainer);
                cardObject.transform.localPosition = Vector3.zero;
                cardObject.transform.localRotation = Quaternion.identity;
                cardObject.transform.localScale = Vector3.one;
                
                // Add to our networked list
                cardObjectIds.Add(cardNetObj.ObjectId);
                
                // Set the card as draftable
                Card cardComponent = cardObject.GetComponent<Card>();
                if (cardComponent != null)
                {
                    cardComponent.SetDraftable(true);
                }
                
                // Sync the parenting to all clients
                ObserversSyncCardParenting(cardNetObj.ObjectId, this.NetworkObject.ObjectId, cardData.CardName);
                
                Debug.Log($"DraftPack: Added card {cardData.CardName} (ID: {cardNetObj.ObjectId}) to pack");
            }
        }
        
        Debug.Log($"DraftPack: Pack initialization complete with {cardObjectIds.Count} cards");
    }
    
    /// <summary>
    /// Removes a card from the pack
    /// </summary>
    [Server]
    public bool RemoveCard(GameObject cardObject)
    {
        if (!IsServerInitialized || cardObject == null)
        {
            Debug.LogError("DraftPack: Cannot remove card - server not initialized or card is null");
            return false;
        }
        
        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError("DraftPack: Card object has no NetworkObject component");
            return false;
        }
        
        int cardId = cardNetObj.ObjectId;
        bool removed = cardObjectIds.Remove(cardId);
        
        if (removed)
        {
            Debug.Log($"DraftPack: Removed card {cardId} from pack. Remaining cards: {cardObjectIds.Count}");
            
            // Check if pack is now empty
            if (cardObjectIds.Count == 0)
            {
                Debug.Log("DraftPack: Pack is now empty");
                OnPackEmpty?.Invoke(this);
            }
        }
        else
        {
            Debug.LogWarning($"DraftPack: Failed to remove card {cardId} from pack");
        }
        
        return removed;
    }
    
    /// <summary>
    /// Sets the current owner of this pack
    /// </summary>
    [Server]
    public void SetCurrentOwner(NetworkEntity newOwner)
    {
        if (!IsServerInitialized || newOwner == null)
        {
            Debug.LogError("DraftPack: Cannot set owner - server not initialized or owner is null");
            return;
        }
        
        int previousOwner = CurrentOwnerPlayerId.Value;
        CurrentOwnerPlayerId.Value = newOwner.ObjectId;
        
        Debug.Log($"DraftPack: Owner changed from {previousOwner} to {newOwner.ObjectId} ({newOwner.EntityName.Value})");
    }
    
    /// <summary>
    /// Gets all card GameObjects in this pack
    /// </summary>
    public List<GameObject> GetCards()
    {
        return new List<GameObject>(localCards);
    }
    
    /// <summary>
    /// Checks if this pack is currently owned by the specified player
    /// </summary>
    public bool IsOwnedBy(NetworkEntity player)
    {
        return player != null && CurrentOwnerPlayerId.Value == player.ObjectId;
    }
    
    /// <summary>
    /// Gets the NetworkEntity that currently owns this pack
    /// </summary>
    public NetworkEntity GetCurrentOwner()
    {
        return FindEntityById(CurrentOwnerPlayerId.Value);
    }
    
    /// <summary>
    /// Gets the NetworkEntity that originally owned this pack
    /// </summary>
    public NetworkEntity GetOriginalOwner()
    {
        return FindEntityById(OriginalOwnerPlayerId.Value);
    }
    
    private void OnCardListChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateLocalCardList();
        UpdateInspectorCardList();
    }
    
    private void OnCurrentOwnerChanged(int prev, int next, bool asServer)
    {
        NetworkEntity newOwner = FindEntityById(next);
        if (newOwner != null)
        {
            OnOwnerChanged?.Invoke(this, newOwner);
            
            // Update draft pack visibility when ownership changes
            if (!asServer) // Only on clients
            {
                EntityVisibilityManager entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
                if (entityVisibilityManager != null)
                {
                    entityVisibilityManager.UpdateDraftPackVisibilityForPack(this);
                }
            }
        }
    }
    
    private void UpdateLocalCardList()
    {
        localCards.Clear();
        
        foreach (int cardId in cardObjectIds)
        {
            GameObject cardObject = FindCardObjectById(cardId);
            if (cardObject != null)
            {
                localCards.Add(cardObject);
                
                // Ensure the card is properly parented on clients
                if (!IsServerInitialized && cardContainer != null)
                {
                    // Only reparent if the card is not already a child of cardContainer
                    if (cardObject.transform.parent != cardContainer)
                    {
                        Debug.Log($"DraftPack: Reparenting card {cardObject.name} to cardContainer on client");
                        cardObject.transform.SetParent(cardContainer, false);
                        // Don't override positions - let the layout system handle positioning
                        
                        // Use EntityVisibilityManager to determine proper visibility
                        EntityVisibilityManager entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
                        if (entityVisibilityManager != null)
                        {
                            entityVisibilityManager.UpdateDraftPackVisibilityForPack(this);
                        }
                        else
                        {
                            // Fallback: set to active
                            cardObject.SetActive(true);
                        }
                    }
                }
            }
        }
        
        Debug.Log($"DraftPack: Updated local card list. Found {localCards.Count} cards");
    }
    
    private void UpdateInspectorCardList()
    {
        inspectorCards.Clear();
        inspectorCards.AddRange(localCards);
    }
    
    private GameObject FindCardObjectById(int objectId)
    {
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(objectId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(objectId, out netObj);
        }
        
        return netObj?.gameObject;
    }
    
    private NetworkEntity FindEntityById(int entityId)
    {
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
    
    /// <summary>
    /// Syncs card parenting to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversSyncCardParenting(int cardNetObjId, int packNetObjId, string cardName)
    {
        Debug.Log($"DraftPack.ObserversSyncCardParenting called on {(IsServerInitialized ? "Server" : "Client")} - Card NOB ID: {cardNetObjId}, Pack NOB ID: {packNetObjId}, Card Name: {cardName}");
        
        // Use a coroutine to ensure pack ownership is synchronized before updating visibility
        StartCoroutine(SyncCardParentingWithDelay(cardNetObjId, packNetObjId, cardName));
    }
    
    private System.Collections.IEnumerator SyncCardParentingWithDelay(int cardNetObjId, int packNetObjId, string cardName)
    {
        // Wait a frame to ensure SyncVars are synchronized
        yield return null;
        
        NetworkObject cardNetObj = null;
        bool foundCard = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            foundCard = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            foundCard = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        
        if (!foundCard || cardNetObj == null)
        {
            Debug.LogError($"DraftPack: Failed to find card NetworkObject with ID {cardNetObjId} on {(IsServerInitialized ? "Server" : "Client")}.");
            yield break;
        }

        GameObject cardObject = cardNetObj.gameObject;
        cardObject.name = cardName;

        // Ensure this DraftPack instance matches the intended pack
        if (this.NetworkObject.ObjectId != packNetObjId)
        {
            Debug.LogError($"DraftPack.SyncCardParentingWithDelay on {gameObject.name} (Pack NOB ID: {this.NetworkObject.ObjectId}): Received packNetObjId {packNetObjId} which does not match this pack. Card will not be parented here.");
            yield break;
        }

        if (cardContainer == null)
        {
            Debug.LogError($"DraftPack on {gameObject.name} (Pack NOB ID: {this.NetworkObject.ObjectId}): cardContainer is null on client. Card {cardName} (NOB ID: {cardNetObjId}) cannot be parented to pack.");
            yield break;
        }

        Debug.Log($"DraftPack on {gameObject.name} (Client): Parenting card {cardName} (NOB ID: {cardNetObjId}) to cardContainer: {cardContainer.name}");
        cardObject.transform.SetParent(cardContainer, false); // worldPositionStays = false to correctly apply local transforms
        // Don't override positions - let the layout system handle positioning
        
        Debug.Log($"DraftPack: Card {cardName} parented successfully, now checking visibility (CurrentOwnerPlayerId: {CurrentOwnerPlayerId.Value})");
        
        // Use EntityVisibilityManager to determine proper visibility for draft pack cards
        EntityVisibilityManager entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (entityVisibilityManager != null)
        {
            Debug.Log($"DraftPack: EntityVisibilityManager found, calling UpdateDraftPackVisibilityForPack for pack {gameObject.name}");
            // Let the EntityVisibilityManager handle visibility based on pack ownership
            entityVisibilityManager.UpdateDraftPackVisibilityForPack(this);
        }
        else
        {
            // Fallback: Draft pack cards should be visible by default
            Debug.LogWarning("DraftPack: EntityVisibilityManager not found, using fallback visibility");
            cardObject.SetActive(true);
        }
    }
} 