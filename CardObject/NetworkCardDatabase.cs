using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Network-synchronized card database that ensures all players have the same randomized cards.
/// The host generates cards and syncs them to all clients when they join.
/// Only activated when randomization is enabled.
/// </summary>
public class NetworkCardDatabase : NetworkBehaviour
{
    [Header("Singleton")]
    public static NetworkCardDatabase Instance { get; private set; }

    [Header("Sync Settings")]
    [SerializeField, Tooltip("Maximum number of cards to sync at once (to avoid network message size limits)")]
    private int maxCardsPerSync = 50;

    [SerializeField, Tooltip("Delay between sync batches in seconds")]
    private float syncBatchDelay = 0.1f;

    // Network synchronized card collections
    private readonly SyncList<NetworkCardData> syncedDraftableCards = new SyncList<NetworkCardData>();
    private readonly SyncList<NetworkCardData> syncedStarterCards = new SyncList<NetworkCardData>();
    private readonly SyncList<NetworkCardData> syncedUpgradedCards = new SyncList<NetworkCardData>();
    
    // Sync state
    private readonly SyncVar<bool> cardsSynced = new SyncVar<bool>();
    private readonly SyncVar<int> totalCardsToSync = new SyncVar<int>();
    private readonly SyncVar<int> cardsSyncedCount = new SyncVar<int>();

    // Local state
    private bool isInitialized = false;
    private Dictionary<int, CardData> localCardDatabase = new Dictionary<int, CardData>();

    // Events
    public System.Action OnCardsSynced;
    public System.Action<float> OnSyncProgress; // float = progress 0-1

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[NETDB] NetworkCardDatabase: Initialized and set as singleton");
        }
        else
        {
            Debug.LogWarning("[NETDB] NetworkCardDatabase: Instance already exists, this should not happen with proper network spawning");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[NETDB] NetworkCardDatabase: OnStartServer - Server ready to sync cards");
        
        // Subscribe to sync list changes on server
        syncedDraftableCards.OnChange += OnSyncedCardsChanged;
        syncedStarterCards.OnChange += OnSyncedCardsChanged;
        syncedUpgradedCards.OnChange += OnSyncedCardsChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log("[NETDB] NetworkCardDatabase: OnStartClient - Client ready to receive cards");
        
        // Subscribe to sync list changes on client
        syncedDraftableCards.OnChange += OnSyncedCardsChanged;
        syncedStarterCards.OnChange += OnSyncedCardsChanged;
        syncedUpgradedCards.OnChange += OnSyncedCardsChanged;
        
        // Subscribe to sync state changes
        cardsSynced.OnChange += OnCardsSyncedChanged;
        cardsSyncedCount.OnChange += OnSyncProgressChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from events
        syncedDraftableCards.OnChange -= OnSyncedCardsChanged;
        syncedStarterCards.OnChange -= OnSyncedCardsChanged;
        syncedUpgradedCards.OnChange -= OnSyncedCardsChanged;
        cardsSynced.OnChange -= OnCardsSyncedChanged;
        cardsSyncedCount.OnChange -= OnSyncProgressChanged;
    }

    /// <summary>
    /// Server-only: Initialize and sync generated cards to all clients
    /// </summary>
    [Server]
    public void SyncGeneratedCards(List<CardData> draftableCards, List<CardData> starterCards, List<CardData> upgradedCards)
    {
        Debug.Log($"[NETDB] Server: Starting card sync - Draftable: {draftableCards.Count}, Starter: {starterCards.Count}, Upgraded: {upgradedCards.Count}");
        
        if (isInitialized)
        {
            Debug.LogWarning("[NETDB] Server: Cards already synced, ignoring duplicate sync request");
            return;
        }

        // Clear existing sync lists
        syncedDraftableCards.Clear();
        syncedStarterCards.Clear();
        syncedUpgradedCards.Clear();
        
        // Set total count for progress tracking
        int totalCards = draftableCards.Count + starterCards.Count + upgradedCards.Count;
        totalCardsToSync.Value = totalCards;
        cardsSyncedCount.Value = 0;
        cardsSynced.Value = false;

        // Start the sync process
        StartCoroutine(SyncCardsCoroutine(draftableCards, starterCards, upgradedCards));
    }

    /// <summary>
    /// Coroutine to sync cards in batches to avoid network message size limits
    /// </summary>
    private System.Collections.IEnumerator SyncCardsCoroutine(List<CardData> draftableCards, List<CardData> starterCards, List<CardData> upgradedCards)
    {
        Debug.Log("[NETDB] Server: Starting card sync coroutine");
        
        int syncedCount = 0;

        // Sync draftable cards
        yield return StartCoroutine(SyncDraftableCards(draftableCards));
        syncedCount += draftableCards.Count;
        
        // Sync starter cards
        yield return StartCoroutine(SyncStarterCards(starterCards));
        syncedCount += starterCards.Count;
        
        // Sync upgraded cards
        yield return StartCoroutine(SyncUpgradedCards(upgradedCards));
        syncedCount += upgradedCards.Count;

        // Mark sync as complete
        cardsSynced.Value = true;
        isInitialized = true;
        
        Debug.Log($"[NETDB] Server: Card sync complete! Synced {syncedCount} total cards");
        Debug.Log($"[NETDB] Final counts - Draftable: {syncedDraftableCards.Count}, Starter: {syncedStarterCards.Count}, Upgraded: {syncedUpgradedCards.Count}");
    }

    /// <summary>
    /// Sync draftable cards in batches
    /// </summary>
    private System.Collections.IEnumerator SyncDraftableCards(List<CardData> sourceCards)
    {
        Debug.Log($"[NETDB] Server: Syncing {sourceCards.Count} Draftable cards");
        
        for (int i = 0; i < sourceCards.Count; i += maxCardsPerSync)
        {
            int batchSize = Mathf.Min(maxCardsPerSync, sourceCards.Count - i);
            
            for (int j = 0; j < batchSize; j++)
            {
                var card = sourceCards[i + j];
                var networkCard = NetworkCardData.FromCardData(card);
                syncedDraftableCards.Add(networkCard);
            }
            
            // Update progress
            cardsSyncedCount.Value += batchSize;
            
            Debug.Log($"[NETDB] Server: Synced batch {i / maxCardsPerSync + 1} of Draftable cards ({batchSize} cards)");
            
            // Wait before next batch to avoid overwhelming the network
            if (i + batchSize < sourceCards.Count)
            {
                yield return new WaitForSeconds(syncBatchDelay);
            }
        }
        
        Debug.Log($"[NETDB] Server: Completed syncing {sourceCards.Count} Draftable cards");
    }

    /// <summary>
    /// Sync starter cards in batches
    /// </summary>
    private System.Collections.IEnumerator SyncStarterCards(List<CardData> sourceCards)
    {
        Debug.Log($"[NETDB] Server: Syncing {sourceCards.Count} Starter cards");
        
        for (int i = 0; i < sourceCards.Count; i += maxCardsPerSync)
        {
            int batchSize = Mathf.Min(maxCardsPerSync, sourceCards.Count - i);
            
            for (int j = 0; j < batchSize; j++)
            {
                var card = sourceCards[i + j];
                var networkCard = NetworkCardData.FromCardData(card);
                syncedStarterCards.Add(networkCard);
            }
            
            // Update progress
            cardsSyncedCount.Value += batchSize;
            
            Debug.Log($"[NETDB] Server: Synced batch {i / maxCardsPerSync + 1} of Starter cards ({batchSize} cards)");
            
            // Wait before next batch to avoid overwhelming the network
            if (i + batchSize < sourceCards.Count)
            {
                yield return new WaitForSeconds(syncBatchDelay);
            }
        }
        
        Debug.Log($"[NETDB] Server: Completed syncing {sourceCards.Count} Starter cards");
    }

    /// <summary>
    /// Sync upgraded cards in batches
    /// </summary>
    private System.Collections.IEnumerator SyncUpgradedCards(List<CardData> sourceCards)
    {
        Debug.Log($"[NETDB] Server: Syncing {sourceCards.Count} Upgraded cards");
        
        for (int i = 0; i < sourceCards.Count; i += maxCardsPerSync)
        {
            int batchSize = Mathf.Min(maxCardsPerSync, sourceCards.Count - i);
            
            for (int j = 0; j < batchSize; j++)
            {
                var card = sourceCards[i + j];
                var networkCard = NetworkCardData.FromCardData(card);
                syncedUpgradedCards.Add(networkCard);
            }
            
            // Update progress
            cardsSyncedCount.Value += batchSize;
            
            Debug.Log($"[NETDB] Server: Synced batch {i / maxCardsPerSync + 1} of Upgraded cards ({batchSize} cards)");
            
            // Wait before next batch to avoid overwhelming the network
            if (i + batchSize < sourceCards.Count)
            {
                yield return new WaitForSeconds(syncBatchDelay);
            }
        }
        
        Debug.Log($"[NETDB] Server: Completed syncing {sourceCards.Count} Upgraded cards");
    }

    /// <summary>
    /// Called when sync lists change (both server and client)
    /// </summary>
    private void OnSyncedCardsChanged(SyncListOperation op, int index, NetworkCardData oldItem, NetworkCardData newItem, bool asServer)
    {
        if (!asServer) // Client-side only
        {
            if (op == SyncListOperation.Add)
            {
                // Convert network card back to CardData and store locally
                var cardData = newItem.ToCardData();
                localCardDatabase[cardData.CardId] = cardData;
                
                Debug.Log($"[NETDB] Client: Received card '{cardData.CardName}' (ID: {cardData.CardId}, Category: {cardData.CardCategory})");
            }
        }
    }

    /// <summary>
    /// Called when sync completion status changes
    /// </summary>
    private void OnCardsSyncedChanged(bool prev, bool next, bool asServer)
    {
        if (!asServer && next) // Client received completion
        {
            Debug.Log($"[NETDB] Client: Card sync completed! Received {localCardDatabase.Count} total cards");
            ApplyCardsToLocalDatabase();
            OnCardsSynced?.Invoke();
        }
    }

    /// <summary>
    /// Called when sync progress changes
    /// </summary>
    private void OnSyncProgressChanged(int prev, int next, bool asServer)
    {
        if (!asServer) // Client-side only
        {
            float progress = totalCardsToSync.Value > 0 ? (float)next / totalCardsToSync.Value : 0f;
            Debug.Log($"[NETDB] Client: Sync progress {next}/{totalCardsToSync.Value} ({progress:P0})");
            OnSyncProgress?.Invoke(progress);
        }
    }

    /// <summary>
    /// Apply synced cards to the local CardDatabase
    /// </summary>
    private void ApplyCardsToLocalDatabase()
    {
        if (CardDatabase.Instance == null)
        {
            Debug.LogError("[NETDB] Client: CardDatabase.Instance not found, cannot apply synced cards");
            return;
        }

        Debug.Log($"[NETDB] Client: Applying {localCardDatabase.Count} synced cards to local CardDatabase");

        // Convert back to the format needed by CardDatabase
        var draftableCards = syncedDraftableCards.Select(nc => nc.ToCardData()).ToList();
        var starterCards = syncedStarterCards.Select(nc => nc.ToCardData()).ToList();
        var upgradedCards = syncedUpgradedCards.Select(nc => nc.ToCardData()).ToList();

        // Apply to CardDatabase using reflection (same method as RandomizedCardDatabaseManager)
        ReplaceCardsInDatabase(draftableCards, starterCards, upgradedCards);
        
        Debug.Log("[NETDB] Client: Successfully applied synced cards to local CardDatabase");
    }

    /// <summary>
    /// Replace cards in the local CardDatabase using reflection
    /// </summary>
    private void ReplaceCardsInDatabase(List<CardData> draftableCards, List<CardData> starterCards, List<CardData> upgradedCards)
    {
        // Use reflection to access private fields in CardDatabase
        var cardDatabaseType = typeof(CardDatabase);
        
        var draftableField = cardDatabaseType.GetField("draftableCards", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var starterField = cardDatabaseType.GetField("starterCards", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgradedField = cardDatabaseType.GetField("upgradedCards", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (draftableField != null)
        {
            draftableField.SetValue(CardDatabase.Instance, draftableCards);
            Debug.Log($"[NETDB] Replaced {draftableCards.Count} draftable cards in CardDatabase");
        }

        if (starterField != null)
        {
            starterField.SetValue(CardDatabase.Instance, starterCards);
            Debug.Log($"[NETDB] Replaced {starterCards.Count} starter cards in CardDatabase");
        }

        if (upgradedField != null)
        {
            upgradedField.SetValue(CardDatabase.Instance, upgradedCards);
            Debug.Log($"[NETDB] Replaced {upgradedCards.Count} upgraded cards in CardDatabase");
        }
    }

    /// <summary>
    /// Check if cards have been synced
    /// </summary>
    public bool AreCardsSynced => cardsSynced.Value;

    /// <summary>
    /// Get sync progress (0-1)
    /// </summary>
    public float GetSyncProgress()
    {
        return totalCardsToSync.Value > 0 ? (float)cardsSyncedCount.Value / totalCardsToSync.Value : 0f;
    }

    /// <summary>
    /// Get card by ID from synced cards (client-side)
    /// </summary>
    public CardData GetSyncedCard(int cardId)
    {
        bool found = localCardDatabase.TryGetValue(cardId, out CardData card);
        if (found)
        {
            Debug.Log($"[CARD-FLOW] NetworkCardDatabase: SUCCESS - Found synced card ID {cardId} - {card.CardName}");
        }
        else
        {
            // Only log first few failures to avoid spam
            if (cardId <= 10)
            {
                Debug.Log($"[CARD-FLOW] NetworkCardDatabase: FAILED - Card ID {cardId} not found in {localCardDatabase.Count} synced cards");
            }
        }
        return card;
    }

    /// <summary>
    /// Get all synced cards by category
    /// </summary>
    public List<CardData> GetSyncedCardsByCategory(CardCategory category)
    {
        var result = localCardDatabase.Values.Where(card => card.CardCategory == category).ToList();
        Debug.Log($"[NETDB] GetSyncedCardsByCategory({category}) - Found {result.Count} cards out of {localCardDatabase.Count} total");
        
        if (result.Count == 0 && localCardDatabase.Count > 0)
        {
            Debug.LogWarning($"[NETDB] No {category} cards found! Available categories:");
            var categories = localCardDatabase.Values.GroupBy(c => c.CardCategory);
            foreach (var group in categories)
            {
                Debug.LogWarning($"[NETDB]   {group.Key}: {group.Count()} cards");
            }
        }
        
        return result;
    }

    /// <summary>
    /// Get total number of synced cards (used by clients to wait for sync completion)
    /// </summary>
    public int GetTotalSyncedCardCount()
    {
        return localCardDatabase.Count;
    }
} 