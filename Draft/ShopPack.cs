using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

/// <summary>
/// Represents a networked shop containing cards available for purchase during draft.
/// Attach to: ShopPack prefab that will be spawned during the draft phase.
/// This contains a shared pool of cards that all players can purchase from.
/// </summary>
public class ShopPack : NetworkBehaviour
{
    [Header("Shop Display Configuration")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private bool isVisible = true;
    
    [Header("Card Management")]
    [SerializeField] private CardSpawner cardSpawner;
    
    [Header("Debug Info")]
    [SerializeField] private List<GameObject> inspectorCards = new List<GameObject>();
    
    // Networked list of card object IDs in this shop
    private readonly SyncList<int> cardNetworkIds = new SyncList<int>();
    
    // Local cache of card GameObjects
    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    
    // Events
    public event Action<ShopPack, GameObject, NetworkEntity> OnCardPurchased;
    public event Action<ShopPack> OnShopRefreshed;
    
    private OnlineGameManager gameManager;
    
    public Transform CardContainer => cardContainer;
    public bool IsEmpty => cardNetworkIds.Count == 0;
    public int CardCount => cardNetworkIds.Count;
    
    private void Awake()
    {
        // Auto-assign components if not set
        if (cardSpawner == null) cardSpawner = GetComponent<CardSpawner>();
        if (cardContainer == null) cardContainer = transform;
        
        // Validate required components
        if (cardSpawner == null)
        {
            Debug.LogError($"ShopPack on {gameObject.name}: CardSpawner component is required but not found!");
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        gameManager = OnlineGameManager.Instance;
        cardNetworkIds.OnChange += OnCardListChanged;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Subscribe to SyncList changes to maintain local card list
        cardNetworkIds.OnChange += OnCardListChanged;
        
        // If cards were already added before we subscribed, rebuild the local list
        if (cardNetworkIds.Count > 0)
        {
            RebuildLocalCardList();
        }
        
        Debug.Log($"ShopPack: Client started with {cardNetworkIds.Count} cards in shop");
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        cardNetworkIds.OnChange -= OnCardListChanged;
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        cardNetworkIds.OnChange -= OnCardListChanged;
        
        // Clean up when server stops
        ClearShop();
    }
    
    /// <summary>
    /// Initializes the shop with cards based on the provided configuration
    /// </summary>
    [Server]
    public void InitializeShop(int shopSize, int minCardCost, int maxCardCost)
    {
        /* Debug.Log($"ShopPack: Initializing shop with {shopSize} cards, cost range: {minCardCost}-{maxCardCost}"); */
        
        // Clear any existing cards
        ClearShop();
        
        // Generate and spawn cards for the shop
        for (int i = 0; i < shopSize; i++)
        {
            CreateShopCard(minCardCost, maxCardCost);
        }
        
        Debug.Log($"ShopPack: Shop initialized with {cardNetworkIds.Count} cards");
        OnShopRefreshed?.Invoke(this);
    }
    
    /// <summary>
    /// Creates a single card for the shop with random cost within the specified range
    /// </summary>
    [Server]
    private void CreateShopCard(int minCost, int maxCost)
    {
        if (cardSpawner == null)
        {
            Debug.LogError("ShopPack: Cannot create shop card - CardSpawner is null");
            return;
        }
        
        // Get random draftable cards from the database (shops should sell draftable cards, not starter or upgraded cards)
        if (CardDatabase.Instance == null)
        {
            Debug.LogError("ShopPack: Cannot create shop card - CardDatabase instance not found");
            return;
        }
        
        List<CardData> draftableCards = CardDatabase.Instance.GetDraftableCards();
        if (draftableCards.Count == 0)
        {
            Debug.LogError("ShopPack: Cannot create shop card - no draftable cards in database");
            return;
        }
        
        // Select a random draftable card
        CardData randomCardData = draftableCards[UnityEngine.Random.Range(0, draftableCards.Count)];
        
        // Generate a random cost within the specified range
        int cardCost = UnityEngine.Random.Range(minCost, maxCost + 1);
        
        /* Debug.Log($"ShopPack: Creating shop card '{randomCardData.CardName}' with cost {cardCost}"); */
        
        // Spawn the card using CardSpawner.SpawnUnownedCard
        GameObject cardObject = cardSpawner.SpawnUnownedCard(randomCardData);
        if (cardObject != null)
        {
            // Set the card as purchasable and assign cost
            Card card = cardObject.GetComponent<Card>();
            if (card != null)
            {
                card.SetPurchasable(true, cardCost);
                
                /* Debug.Log($"ShopPack: Created shop card '{card.CardName}' with cost {cardCost}"); */
            }
            
            // Parent the card to our container
            if (cardContainer != null)
            {
                cardObject.transform.SetParent(cardContainer, false);
            }
            
            // Add to our network list
            NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
            if (cardNetObj != null)
            {
                cardNetworkIds.Add(cardNetObj.ObjectId);
            }
            
            // Add to local list
            spawnedCards.Add(cardObject);
            
            // Sync the parenting to all clients (similar to DraftPack)
            ObserversSyncCardParenting(cardNetObj.ObjectId, this.NetworkObject.ObjectId, randomCardData.CardName);
        }
        else
        {
            Debug.LogError("ShopPack: Failed to spawn card for shop");
        }
    }
    
    /// <summary>
    /// Handles the purchase of a card from the shop
    /// </summary>
    [Server]
    public bool PurchaseCard(GameObject cardObject, NetworkEntity buyer)
    {
        if (cardObject == null || buyer == null)
        {
            Debug.LogError("ShopPack: Cannot purchase card - null parameters");
            return false;
        }
        
        Card card = cardObject.GetComponent<Card>();
        if (card == null)
        {
            Debug.LogError("ShopPack: Cannot purchase card - no Card component");
            return false;
        }
        
        int cardCost = card.PurchaseCost;
        
        // Check if buyer has enough currency
        if (buyer.Currency.Value < cardCost)
        {
            Debug.LogWarning($"ShopPack: {buyer.EntityName.Value} cannot afford card costing {cardCost} (has {buyer.Currency.Value})");
            return false;
        }
        
        // Check if card is in our shop
        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj == null || !cardNetworkIds.Contains(cardNetObj.ObjectId))
        {
            Debug.LogError("ShopPack: Card is not in this shop");
            return false;
        }
        
        // Deduct currency
        buyer.Currency.Value -= cardCost;
        
        // Remove card from shop
        RemoveCardFromShop(cardObject);
        
        // Card is no longer purchasable since it's been bought
        card.SetPurchasable(false);
        
        // Notify all clients that this card is no longer available
        NotifyCardUnavailable(cardNetObj.ObjectId, card.CardName);
        
        /* Debug.Log($"ShopPack: {buyer.EntityName.Value} purchased '{card.CardName}' for {cardCost} gold"); */
        
        // Fire purchase event
        OnCardPurchased?.Invoke(this, cardObject, buyer);
        
        return true;
    }
    
    /// <summary>
    /// Removes a card from the shop
    /// </summary>
    [Server]
    private void RemoveCardFromShop(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj != null)
        {
            cardNetworkIds.Remove(cardNetObj.ObjectId);
        }
        
        spawnedCards.Remove(cardObject);
        
        Debug.Log($"ShopPack: Removed card from shop. Remaining cards: {cardNetworkIds.Count}");
    }
    
    /// <summary>
    /// Clears all cards from the shop
    /// </summary>
    [Server]
    private void ClearShop()
    {
        /* Debug.Log($"ShopPack: Clearing shop of {cardNetworkIds.Count} cards"); */
        
        // Despawn all existing cards
        for (int i = spawnedCards.Count - 1; i >= 0; i--)
        {
            if (spawnedCards[i] != null)
            {
                NetworkObject cardNetObj = spawnedCards[i].GetComponent<NetworkObject>();
                if (cardNetObj != null && cardNetObj.IsSpawned)
                {
                    FishNet.InstanceFinder.ServerManager.Despawn(cardNetObj);
                }
            }
        }
        
        cardNetworkIds.Clear();
        spawnedCards.Clear();
        
        Debug.Log("ShopPack: Shop cleared");
    }
    
    /// <summary>
    /// Refreshes the shop with new cards (if needed for future features)
    /// </summary>
    [Server]
    public void RefreshShop(int shopSize, int minCardCost, int maxCardCost)
    {
        Debug.Log("ShopPack: Refreshing shop");
        InitializeShop(shopSize, minCardCost, maxCardCost);
    }
    
    /// <summary>
    /// Called when the network list of card IDs changes
    /// </summary>
    private void OnCardListChanged(SyncListOperation operation, int index, int oldValue, int newValue, bool asServer)
    {
        /* Debug.Log($"ShopPack: Card list changed - Operation: {operation}, Index: {index}, Value: {newValue}"); */
        
        // Rebuild local card list when changes occur
        if (!asServer)
        {
            RebuildLocalCardList();
        }
    }
    
    /// <summary>
    /// Rebuilds the local card list from network IDs
    /// </summary>
    private void RebuildLocalCardList()
    {
        spawnedCards.Clear();
        
        foreach (int cardNetId in cardNetworkIds)
        {
            NetworkObject cardNetObj = null;
            bool found = false;
            
            if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
            {
                found = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(cardNetId, out cardNetObj);
            }
            
            if (found && cardNetObj != null)
            {
                spawnedCards.Add(cardNetObj.gameObject);
            }
        }
        
        Debug.Log($"ShopPack: Rebuilt local card list with {spawnedCards.Count} cards");
    }
    
    /// <summary>
    /// Gets all cards currently in the shop
    /// </summary>
    public List<GameObject> GetShopCards()
    {
        return new List<GameObject>(spawnedCards);
    }
    
    /// <summary>
    /// Checks if a specific card is in this shop
    /// </summary>
    public bool ContainsCard(GameObject cardObject)
    {
        return spawnedCards.Contains(cardObject);
    }
    
    /// <summary>
    /// Gets cards in the shop that the specified buyer can afford
    /// </summary>
    public List<GameObject> GetAffordableCards(NetworkEntity buyer)
    {
        if (buyer == null) return new List<GameObject>();
        
        int buyerCurrency = buyer.Currency.Value;
        return spawnedCards.Where(cardObj =>
        {
            Card card = cardObj.GetComponent<Card>();
            return card != null && card.PurchaseCost <= buyerCurrency;
        }).ToList();
    }
    
    /// <summary>
    /// Gets the cheapest card in the shop
    /// </summary>
    public GameObject GetCheapestCard()
    {
        if (spawnedCards.Count == 0) return null;
        
        GameObject cheapest = spawnedCards[0];
        int cheapestCost = int.MaxValue;
        
        foreach (var cardObj in spawnedCards)
        {
            Card card = cardObj.GetComponent<Card>();
            if (card != null && card.PurchaseCost < cheapestCost)
            {
                cheapest = cardObj;
                cheapestCost = card.PurchaseCost;
            }
        }
        
        return cheapest;
    }
    
    /// <summary>
    /// Gets the most expensive card in the shop
    /// </summary>
    public GameObject GetMostExpensiveCard()
    {
        if (spawnedCards.Count == 0) return null;
        
        GameObject mostExpensive = spawnedCards[0];
        int highestCost = -1;
        
        foreach (var cardObj in spawnedCards)
        {
            Card card = cardObj.GetComponent<Card>();
            if (card != null && card.PurchaseCost > highestCost)
            {
                mostExpensive = cardObj;
                highestCost = card.PurchaseCost;
            }
        }
        
        return mostExpensive;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (IsServerInitialized)
        {
            cardNetworkIds.OnChange -= OnCardListChanged;
        }
    }
    
    /// <summary>
    /// Syncs card parenting to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversSyncCardParenting(int cardNetObjId, int shopNetObjId, string cardName)
    {
        Debug.Log($"ShopPack.ObserversSyncCardParenting called on {(IsServerInitialized ? "Server" : "Client")} - Card NOB ID: {cardNetObjId}, Shop NOB ID: {shopNetObjId}, Card Name: {cardName}");
        
        // Use coroutine for proper timing
        StartCoroutine(SyncCardParentingWithDelay(cardNetObjId, shopNetObjId, cardName));
    }
    
    private System.Collections.IEnumerator SyncCardParentingWithDelay(int cardNetObjId, int shopNetObjId, string cardName)
    {
        // Wait a frame to ensure NetworkObjects are properly spawned
        yield return null;
        
        /* Debug.Log($"ShopPack: Syncing card parenting after delay - Card NOB ID: {cardNetObjId}, Shop NOB ID: {shopNetObjId}"); */
        
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
            Debug.LogError($"ShopPack: Failed to find card NetworkObject with ID {cardNetObjId}");
            yield break;
        }
        
        GameObject cardObject = cardNetObj.gameObject;
        cardObject.name = $"ShopCard_{cardName}";
        
        // Ensure this ShopPack instance matches the intended shop
        if (this.NetworkObject.ObjectId != shopNetObjId)
        {
            Debug.LogError($"ShopPack.SyncCardParentingWithDelay on {gameObject.name} (Shop NOB ID: {this.NetworkObject.ObjectId}): Received shopNetObjId {shopNetObjId} which does not match this shop. Card will not be parented here.");
            yield break;
        }
        
        if (cardContainer == null)
        {
            Debug.LogError($"ShopPack on {gameObject.name} (Shop NOB ID: {this.NetworkObject.ObjectId}): cardContainer is null. Card {cardName} (NOB ID: {cardNetObjId}) cannot be parented to container.");
            yield break;
        }
        
        /* Debug.Log($"ShopPack on {gameObject.name}: Parenting card {cardName} (NOB ID: {cardNetObjId}) to cardContainer: {cardContainer.name}"); */
        cardObject.transform.SetParent(cardContainer, false);
        
        // Don't set localPosition to Vector3.zero - let GridLayoutGroup handle positioning
        // Only reset rotation and scale
        cardObject.transform.localRotation = Quaternion.identity;
        cardObject.transform.localScale = Vector3.one;
        
        // Shop cards should be visible
        cardObject.SetActive(true);
    }
    
    /// <summary>
    /// Notifies all clients that a card is no longer available for purchase
    /// </summary>
    [ObserversRpc]
    private void NotifyCardUnavailable(int cardNetObjId, string cardName)
    {
        /* Debug.Log($"ShopPack.NotifyCardUnavailable called on {(IsServerInitialized ? "Server" : "Client")} - Card NOB ID: {cardNetObjId}, Card Name: {cardName}"); */
        
        // Find the card object
        NetworkObject cardNetObj = null;
        bool found = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            found = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            found = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        
        if (found && cardNetObj != null)
        {
            GameObject cardObject = cardNetObj.gameObject;
            
            // Notify all DraftCanvasManagers to hide selection UI for this card
            DraftCanvasManager[] canvasManagers = FindObjectsByType<DraftCanvasManager>(FindObjectsSortMode.None);
            foreach (DraftCanvasManager canvasManager in canvasManagers)
            {
                canvasManager.HideSelectionUIForCard(cardObject);
            }
            
            /* Debug.Log($"ShopPack: Notified {canvasManagers.Length} canvas managers that card {cardName} is no longer available"); */
        }
        else
        {
            Debug.LogWarning($"ShopPack: Could not find card NetworkObject with ID {cardNetObjId} to notify unavailability");
        }
    }
} 